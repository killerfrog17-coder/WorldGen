using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class NearbyPossession {public Possession Item;public bool Natural;public Vector3 Point;public Bounds Bounds;}
    PlayerInventory Inventory=>PlayerRules.Inventory??(PlayerRules.Inventory=new PlayerInventory());
    readonly HashSet<ulong> inventoryCollected=new HashSet<ulong>();
    readonly List<NearbyPossession> inventoryNearby=new List<NearbyPossession>(),inventoryMarkers=new List<NearbyPossession>();
    readonly Dictionary<string,GameObject> inventoryVisuals=new Dictionary<string,GameObject>();
    readonly Dictionary<string,PossessionPlace> inventoryVisualPlaces=new Dictionary<string,PossessionPlace>();
    bool inventoryOpen,inventoryInitialized;double inventoryNearbyAt;
    string inventoryNotice="",inventorySelected="";float inventoryNoticeUntil;
    Material inventoryBagMaterial;
    readonly HashSet<ArtModel> inventoryPinnedArt=new HashSet<ArtModel>();
    readonly bool inventoryReviewMode=Environment.GetCommandLineArgs().Contains("--worldgen-item-use-check")||Environment.GetCommandLineArgs().Contains("--worldgen-portable-craft-check")||Environment.GetCommandLineArgs().Contains("--worldgen-inventory-check")||Environment.GetCommandLineArgs().Contains("--worldgen-stone-craft-check");
    Transform inventoryBoneOwner;Dictionary<string,Transform> inventoryBones;
    void InitializeInventory()
    {
        if(inventoryInitialized)return;inventoryInitialized=true;Inventory.Validate();
        // Demo NPC humans are not restored with the session. Their numeric
        // IDs can be reused, so an old arrow must not bind to a new person.
        foreach(var item in Inventory.Items.Where(p=>p.AttachedHuman!=0))ReleaseProjectileAttachment(item);
        foreach(var id in Inventory.CollectedNatural)inventoryCollected.Add(id);foreach(var job in Inventory.Crafts)PauseItemCraft(job);scatterDirty=true;
        if(Inventory.ToolWorkEquipment!=null){var borrowed=Inventory.ToolWorkEquipment;Inventory.ToolWorkEquipment=null;RestoreWorkHands(new ItemCraftJob{Equipment=borrowed});}
    }
    bool CollectedRock(ScatterInstance p)=>(p.Kind==ScatterKind.Stone||p.Kind==ScatterKind.Boulder||p.Kind==ScatterKind.Branch||p.Kind==ScatterKind.Fungus)&&inventoryCollected.Contains(p.Id);
    static Possession RockPossession(ScatterInstance p)
    {
        if(p.Kind==ScatterKind.Branch)return WoodCrafting.Branch(p);
        // Match visible dimensions. Massive boulders are scenery, not pocket rocks.
        double mass=p.WidthM*p.WidthM*.86*p.HeightM*Math.PI/6*2600;
        bool small=p.WidthM<=.2&&mass<=2;
        return new Possession{Id="rock-"+p.Id.ToString("x16"),NaturalId=p.Id,Kind=small?PossessionKind.SmallRock:PossessionKind.LargeRock,Place=PossessionPlace.Ground,
            WidthM=p.WidthM,HeightM=p.HeightM,DepthM=p.WidthM*.86,MassKg=Math.Max(.025,mass),
            CellsWide=Math.Max(1,(int)Math.Ceiling(p.WidthM/.1)),CellsHigh=Math.Max(1,(int)Math.Ceiling(p.HeightM/.1)),Variant=p.Variant,Position=p.Position,Tint=p.Color};
    }
    bool InventoryReach(Vector3 point)
    {
        var eye=new Vector3((float)(motion.X-originX),(float)motion.Y,(float)(motion.Z-originZ));
        if((point-eye).sqrMagnitude>3*3)return false;
        return InventorySight(point);
    }
    bool InventorySight(Vector3 point)
    {
        var eye=new Vector3((float)(motion.X-originX),(float)motion.Y,(float)(motion.Z-originZ));
        for(int i=1;i<8;i++){var p=Vector3.Lerp(eye,point,i/8f);if(VisibleGround(p.x+originX,p.z+originZ,double.NegativeInfinity)>p.y+.06)return false;}
        return true;
    }
    void RefreshInventoryNearby(bool force=false)
    {
        if(!Ready||!inventoryInitialized||!force&&Time.unscaledTimeAsDouble<inventoryNearbyAt)return;
        inventoryNearbyAt=Time.unscaledTimeAsDouble+.2;inventoryNearby.Clear();inventoryMarkers.Clear();
        var player=new Vector3((float)(motion.X-originX),(float)motion.Y,(float)(motion.Z-originZ));
        var feet=player-Vector3.up*(float)TerrainMotion.EyeHeightM;
        void Candidate(NearbyPossession candidate)
        {
            bool reachable=InventoryReach(candidate.Point);
            if((candidate.Point-feet).sqrMagnitude<=16&&(reachable||InventorySight(candidate.Point)))inventoryMarkers.Add(candidate);
            if(reachable)inventoryNearby.Add(candidate);
        }
        foreach(var chunk in scatterChunks.Values)
        {
            if(chunk.Roots==null||chunk.AllBounds.SqrDistance(player)>36)continue;
            foreach(var group in chunk.Groups)
            {
                if(group.Bounds.SqrDistance(player)>36)continue;
                foreach(int i in group.Indices)
                {
                    var p=chunk.Source.Instances[i];if(p.Kind!=ScatterKind.Stone&&p.Kind!=ScatterKind.Boulder&&p.Kind!=ScatterKind.Branch||CollectedRock(p))continue;
                    var point=chunk.Roots[i]+Vector3.up*(float)p.HeightM*.5f;
                    if((point-feet).sqrMagnitude>25)continue;var item=RockPossession(p);
                    if(item.MassKg>25||p.Kind!=ScatterKind.Branch&&(p.WidthM>.55||p.HeightM>.45))continue;
                    Candidate(new NearbyPossession{Item=item,Natural=true,Point=point,Bounds=new Bounds(point,new Vector3((float)p.WidthM+.06f,(float)p.HeightM+.06f,(float)p.WidthM+.06f))});
                }
            }
        }
        foreach(var item in Inventory.Items.Where(i=>i.Place==PossessionPlace.Ground))
        {
            if((item.Position-Position).Length*frame.RadiusM>5)continue;
            TerrainHandoff.Coordinates(frame,item.Position,out double x,out double z);
            var point=inventoryDrops.TryGetValue(item.Id,out var drop)&&drop.Body!=null?transform.InverseTransformPoint(drop.Body.worldCenterOfMass):new Vector3((float)(x-originX),(float)(Ground(x,z)+item.GroundOffsetM)+(float)item.HeightM*.5f,(float)(z-originZ));
            var bounds=new Bounds(point,new Vector3((float)item.WidthM+.06f,(float)item.HeightM+.06f,(float)item.DepthM+.06f));
            if((WoodCrafting.Wood(item)||PortableTechnology.Item(item.Kind)?.Long==true)&&inventoryVisuals.TryGetValue(item.Id,out var woodVisual))
            {
                var renderers=woodVisual.GetComponentsInChildren<Renderer>();if(renderers.Length>0){bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);point=transform.InverseTransformPoint(bounds.center);bounds.center=point;}
            }
            Candidate(new NearbyPossession{Item=item,Point=point,Bounds=bounds});
        }
        foreach(var item in Inventory.Items.Where(i=>i.Place==PossessionPlace.Corpse&&CorpseItemReach(i)))inventoryNearby.Add(new NearbyPossession{Item=item});
        inventoryNearby.Sort((a,b)=>(a.Point-player).sqrMagnitude.CompareTo((b.Point-player).sqrMagnitude));
    }
    NearbyPossession InventoryCandidate(string id)
    {
        var owned=Inventory.Find(id);if(owned!=null)return new NearbyPossession{Item=owned};
        return inventoryNearby.FirstOrDefault(i=>i.Item.Id==id);
    }
    bool AccessiblePossession(Possession item)
    {
        var visited=new HashSet<string>();
        while(item!=null&&visited.Add(item.Id))
        {if(item.Place==PossessionPlace.Corpse)return CorpseItemReach(item);if(item.Place==PossessionPlace.Ground)return inventoryNearby.Any(n=>n.Item.Id==item.Id);if(item.Place!=PossessionPlace.Container)return true;item=Inventory.Find(item.ContainerId);}
        return false;
    }
    void InventoryFeedback(string text)
    {inventoryNotice=text;inventoryNoticeUntil=Time.unscaledTime+4;if(!inventoryOpen)PostExplorerChat(text,true);}
    bool MovePossession(string id,PossessionPlace place,string bag="",int x=0,int y=0,bool rotated=false,bool autoStow=false,bool autoHand=false)
    {
        CancelItemCharge();CancelWorldToolWork();RefreshInventoryNearby(true);var candidate=InventoryCandidate(id);if(candidate==null)return false;
        var item=candidate.Item;var previousPlace=item.Place;
        if(candidate.Natural&&!inventoryNearby.Any(n=>n.Item.Id==id)||!candidate.Natural&&!AccessiblePossession(item)){InventoryFeedback("That item is out of reach.");return false;}
        if(place==PossessionPlace.Container)
        {var dest=autoStow?Inventory.WornContainers.FirstOrDefault():Inventory.Find(bag);if(dest==null||!AccessiblePossession(dest)){InventoryFeedback("That bag is out of reach.");return false;}}
        if(place==PossessionPlace.Ground&&previousPlace!=PossessionPlace.Ground)UpdateInventoryVisuals();
        if(candidate.Natural)Inventory.Items.Add(item);
        string reason;bool moved=autoStow?Inventory.Stow(id,out reason):autoHand?Inventory.Hold(id,out reason):Inventory.Move(id,place,bag,x,y,rotated,out reason);
        if(!moved){if(candidate.Natural)Inventory.Items.Remove(item);InventoryFeedback(reason);return false;}
        foreach(var job in Inventory.Crafts.Where(j=>PortableCrafting.Affects(j,id)))
        {job.Equipment?.PreviousHands.RemoveAll(p=>p.Id==id);PauseItemCraft(job);}
        if(candidate.Natural){inventoryCollected.Add(item.NaturalId);Inventory.CollectedNatural.Add(item.NaturalId);scatterDirty=true;}
        if(place==PossessionPlace.Ground&&!autoStow&&!autoHand)
        {
            if(previousPlace!=PossessionPlace.Ground)ReleasePossession(item,previousPlace==PossessionPlace.Container);
        }
        if(item.Place!=PossessionPlace.Ground){RemoveDroppedBody(item.Id);item.GroundMoving=false;}
        inventoryNotice="";inventoryNoticeUntil=0;inventorySelected=id;inventoryNearbyAt=0;if(inventoryOpen)RenderInventoryPreview();return true;
    }
    void RequestPossession(string id,PossessionPlace place,string bag="",int x=0,int y=0,bool rotated=false,bool autoStow=false,bool autoHand=false)
    {
        MovePossession(id,place,bag,x,y,rotated,autoStow,autoHand);
    }
    bool HandleInventoryKeys()
    {
        var k=ActiveKeyboard;if(k==null||!Ready||DeathVisible||Spectating||chatOpen||paused||mapOpen||ecologyOpen||!Application.isFocused&&!inventoryReviewMode)return inventoryOpen;
        if(HandleCampKeys(k))return true;
        if(!inventoryNameEditing&&(k.iKey.wasPressedThisFrame||k.tabKey.wasPressedThisFrame)){SetInventoryOpen(!inventoryOpen);return true;}
        if(inventoryOpen)
        {
            if(k.escapeKey.wasPressedThisFrame){SetInventoryOpen(false);return true;}
            if(inventoryDrag!=null){if(k.gKey.wasPressedThisFrame){if(inventoryDrag.Item.Place!=PossessionPlace.Ground)RequestPossession(inventoryDrag.Item.Id,PossessionPlace.Ground);inventoryDrag=null;}else if(k.rKey.wasPressedThisFrame)inventoryDragRotated=!inventoryDragRotated;return true;}
            if(inventoryNameEditing)return true;
        }
        if(k.eKey.wasPressedThisFrame||k.bKey.wasPressedThisFrame)RefreshInventoryNearby(true);
        if(k.qKey.wasPressedThisFrame){CancelItemCharge();CancelWorldToolWork();Inventory.SwapHands();if(inventoryOpen)RenderInventoryPreview();}
        var held=Inventory.QuickHeld;
        var drop=inventoryOpen?HoveredInventoryPossession():held;
        if(k.gKey.wasPressedThisFrame&&drop!=null&&drop.Place!=PossessionPlace.Ground)RequestPossession(drop.Id,PossessionPlace.Ground);
        if(k.rKey.wasPressedThisFrame&&held!=null)RequestPossession(held.Id,PossessionPlace.Container,autoStow:true);
        if(k.bKey.wasPressedThisFrame)
        {
            var pack=Inventory.Bag;if(pack!=null)RequestPossession(pack.Id,PossessionPlace.Ground);
            else {pack=inventoryNearby.FirstOrDefault(n=>PortableTechnology.Wear(n.Item)==PossessionPlace.Back)?.Item;if(pack!=null)RequestPossession(pack.Id,PossessionPlace.Back);}
        }
        if(!inventoryOpen&&k.eKey.wasPressedThisFrame&&!k.f3Key.isPressed)
        {
            var target=AimedPossession();
            float itemDistance=target!=null&&target.Bounds.IntersectRay(new Ray(view.transform.localPosition,view.transform.forward),out float hitDistance)?hitDistance:float.MaxValue;
            if(TryCampInteract(itemDistance))return true;
            if(TryNatureInteract(itemDistance))return true;
            if(TryWaterInteract(itemDistance))return true;
            if(target!=null)RequestPossession(target.Item.Id,PossessionPlace.RightHand,autoHand:true);
        }
        return inventoryOpen;
    }
    void SetInventoryOpen(bool open)
    {
        if(open)CancelItemCharge();natureMenu=null;corpseMenu=null;inventoryOpen=open;inventoryHitTargets.Clear();inventoryDrag=null;inventoryContext=null;inventoryNameEditing=false;
        if(open){RefreshInventoryNearby(true);PrepareInventoryPreview();}else if(inventoryPreviewRoot!=null)inventoryPreviewRoot.SetActive(false);
        GUIUtility.keyboardControl=0;CaptureMouse();
    }
    void UpdateInventory()
    {
        if(!Ready)return;InitializeInventory();UpdateGiveItems();UpdatePortableAppearances();
        if((DeathVisible||Spectating)&&inventoryOpen)SetInventoryOpen(false);
        if(DeathVisible){PauseItemCraft(ActiveItemCraft);CancelWorldToolWork();return;}
        if(inventoryOpen||CraftWorking)RefreshInventoryNearby();UpdateStoneCrafting();UpdateWorldToolWork();UpdateItemThumbnails();
    }
    GameObject CreatePossessionVisual(Possession item,int layer)
    {
        var root=new GameObject(item.Name);root.layer=layer;
        if(PortableTechnology.Item(item.Kind)!=null)
        {CreatePortableParts(root,item,layer);}
        else if(item.Kind==PossessionKind.TestBag)
        {
            var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);if(Application.isPlaying)Destroy(sphere.GetComponent<Collider>());else DestroyImmediate(sphere.GetComponent<Collider>());sphere.transform.SetParent(root.transform,false);sphere.transform.localPosition=Vector3.up*.5f;sphere.transform.localScale=new Vector3(1,1,.55f);sphere.layer=layer;
            if(inventoryBagMaterial==null){inventoryBagMaterial=new Material(Resources.Load<Shader>("WorldGenInventoryPreview"));inventoryBagMaterial.SetColor("_Color",new Color(.49f,.38f,.21f));}
            sphere.GetComponent<Renderer>().sharedMaterial=inventoryBagMaterial;
            var props=new MaterialPropertyBlock();props.SetFloat("_WorldLighting",layer==29?0:1);sphere.GetComponent<Renderer>().SetPropertyBlock(props);
        }
        else if(WoodCrafting.Wood(item))
        {
            var art=Art(ScatterKind.Branch,item.Variant,0);inventoryPinnedArt.Add(art);
            AddCraftMesh(root,LocalScatterMeshes.BuildWood(item.Variant,item.Kind,item.BarkRemoved),art.Parts[0].Material,item,layer);
        }
        else
        {
            var art=Art(ScatterKind.Stone,item.Variant,0);inventoryPinnedArt.Add(art);
            foreach(var part in art.Parts)
            {
                if(item.Kind==PossessionKind.StoneFlake||item.FlakeRemovals>0)
                {
                    AddCraftMesh(root,item.Kind==PossessionKind.StoneFlake?CuttingFlakeMesh():WorkedStoneMesh(part.Mesh,item.FlakeRemovals),part.Material,item,layer);continue;
                }
                var obj=new GameObject("Rock surface",typeof(MeshFilter),typeof(MeshRenderer));obj.layer=layer;obj.transform.SetParent(root.transform,false);
                obj.GetComponent<MeshFilter>().sharedMesh=part.Mesh;var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=part.Material;
                var props=new MaterialPropertyBlock();props.SetVector("_Tint",new Vector4((float)item.Tint.X,(float)item.Tint.Y,(float)item.Tint.Z,1));props.SetVector("_LodTransition",new Vector4(0,1,0,0));renderer.SetPropertyBlock(props);
            }
        }
        root.transform.localScale=new Vector3((float)item.WidthM,(float)item.HeightM,(float)(item.Kind==PossessionKind.TestBag?item.DepthM/.55:item.Kind==PossessionKind.StoneFlake||WoodCrafting.Wood(item)||PortableTechnology.Item(item.Kind)!=null?item.DepthM:item.WidthM));return root;
    }
    void UpdateInventoryVisuals()
    {
        if(!inventoryInitialized||humanPlayer==null||!Ready)return;
        if(viewItemActions.Count>0)foreach(var id in viewItemActions.Where(p=>Time.unscaledTimeAsDouble-p.Value.At>Math.Max(.2,p.Value.Duration)).Select(p=>p.Key).ToArray())viewItemActions.Remove(id);
        var body=humanPlayer.Root.transform;
        if(inventoryBoneOwner!=body){inventoryBoneOwner=body;inventoryBones=body.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());}
        if(!deathPresented)
        {
            if(SharedHumanMotion){UpdateSharedHumanAction();FitSharedHeldHands();AimSharedHead();}
            else {ClearSharedAmmo();RestoreSharedSlings();PoseInventoryHands(humanPlayer,inventoryBones);}
        }
        var present=new HashSet<string>();
        foreach(var item in Inventory.Items)
        {
            if(item.Place==PossessionPlace.Corpse)continue;
            bool beltItem=item.Place==PossessionPlace.Container&&PortableTechnology.Belt(Inventory.Find(item.ContainerId))&&Inventory.Carried(item);
            if(item.Place==PossessionPlace.Container&&!beltItem)continue;
            bool near=item.InFlight||item.Place!=PossessionPlace.Ground||(item.Position-Position).Length*frame.RadiusM<160;
            if(!near)continue;present.Add(item.Id);
            if(!inventoryVisuals.TryGetValue(item.Id,out var root)){root=CreatePossessionVisual(item,30);root.transform.SetParent(transform,false);inventoryVisuals[item.Id]=root;}
            root.SetActive(!Spectating||item.Place==PossessionPlace.Ground);
            if(SharedHumanMotion&&(item.Place==PossessionPlace.RightHand||item.Place==PossessionPlace.LeftHand)&&Time.unscaledTimeAsDouble-viewConsumeAt<HumanActionDuration(viewConsumeDrink?"Drink":"Eat"))
            {root.SetActive(false);continue;}
            var sharedCharge=itemCharge??pendingRanged?.Charge;
            if(SharedHumanMotion&&sharedCharge!=null&&!sharedCharge.Throw&&sharedCharge.Ammo==item)
            {root.SetActive(false);continue;}
            if(PortableTechnology.Item(item.Kind)!=null&&(!inventoryVisualPlaces.TryGetValue(item.Id,out var priorPortable)||priorPortable!=item.Place)){root.transform.localScale=new Vector3((float)item.WidthM,(float)item.HeightM,(float)item.DepthM);RefreshPortableRoot(root,item);inventoryVisualPlaces[item.Id]=item.Place;}
            if(item.Kind==PossessionKind.TestBag&&(!inventoryVisualPlaces.TryGetValue(item.Id,out var previous)||previous!=item.Place))
            {
                inventoryVisualPlaces[item.Id]=item.Place;
                foreach(var renderer in root.GetComponentsInChildren<Renderer>()){var props=new MaterialPropertyBlock();renderer.GetPropertyBlock(props);props.SetFloat("_InventoryWearerId",item.Place==PossessionPlace.Back?1:0);renderer.SetPropertyBlock(props);}
            }
            if(item.Place==PossessionPlace.Ground)
            {
                if(!inventoryDrops.ContainsKey(item.Id))
                {
                    TerrainHandoff.Coordinates(frame,item.Position,out double x,out double z);
                    root.transform.localPosition=new Vector3((float)(x-originX),(float)(Ground(x,z)+item.GroundOffsetM)-.01f,(float)(z-originZ));root.transform.localRotation=Quaternion.Euler((float)item.GroundRotation.X,(float)item.GroundRotation.Y,(float)item.GroundRotation.Z);
                    if(item.GroundMoving){BeginDroppedBody(item,root,new Vector3((float)item.GroundVelocity.X,(float)item.GroundVelocity.Y,(float)item.GroundVelocity.Z));if(inventoryDrops.TryGetValue(item.Id,out var restoredDrop))restoredDrop.Body.angularVelocity=new Vector3((float)item.GroundSpin.X,(float)item.GroundSpin.Y,(float)item.GroundSpin.Z);if(item.InFlight)RegisterProjectile(item);}
                }
            }
            else if(PortableTechnology.Item(item.Kind)!=null&&(PortableTechnology.Worn(item.Place)||beltItem))
            {PosePortableWear(root,item,body,inventoryBones);}
            else if(item.Place==PossessionPlace.Back)
            {root.transform.position=inventoryBones["spine_02"].position-body.forward*.18f-Vector3.up*.2f;root.transform.rotation=body.rotation;}
            else
            {
                if(SharedHumanMotion){PoseSharedHeldItem(root,item);continue;}
                bool right=item.Place==PossessionPlace.RightHand;string name="hand_"+(right?"r":"l");
                var hand=inventoryBones[name];
                Vector3 grip=item.Hands==2?(inventoryBones["hand_r"].position+inventoryBones["hand_l"].position)*.5f:hand.position;root.transform.rotation=body.rotation;
                if(WoodCrafting.Wood(item)||PortableTechnology.Item(item.Kind)?.Long==true)
                {
                    root.transform.rotation=body.rotation*Quaternion.Euler(35,0,0);
                }
                root.transform.position=grip-root.transform.up*(float)item.HeightM*(StoneCrafting.Rock(item)||item.Kind==PossessionKind.Bow?.5f:WoodCrafting.Wood(item)||PortableTechnology.Item(item.Kind)?.Long==true?.35f:.18f);
                if(item.Kind==PossessionKind.Bow)UpdateBowString(root,item);
            }
        }
        UpdateDroppedBodies();
        foreach(var id in inventoryVisuals.Keys.Where(id=>!present.Contains(id)).ToArray()){RemoveDroppedBody(id);DestroyInventoryVisual(inventoryVisuals[id]);inventoryVisuals.Remove(id);inventoryVisualPlaces.Remove(id);}
        if(SharedHumanMotion)UpdateSharedAmmo();
        UpdateViewArms();
    }
    void PoseInventoryHands(HumanActor actor,Dictionary<string,Transform> bones)
    {
        float scale=actor.Definition.EyeHeight/1.65f;var body=actor.Root.transform;
        bool both=Inventory.AtHand(PossessionPlace.RightHand)?.Hands==2;
        foreach(var side in new[]{"r","l"})
        {
            var item=Inventory.AtHand(side=="r"?PossessionPlace.RightHand:PossessionPlace.LeftHand);if(item==null)continue;
            var upper=bones["upperarm_"+side];var lower=bones["lowerarm_"+side];var hand=bones["hand_"+side];
            var target=body.TransformPoint(new Vector3((side=="r"?1:-1)*(both?.14f:.24f)*scale,.98f*scale,.34f*scale));
            float a=(lower.position-upper.position).magnitude,b=(hand.position-lower.position).magnitude;
            var delta=target-upper.position;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,(a+b)*.98f);var axis=delta.normalized;target=upper.position+axis*d;
            var bend=Vector3.ProjectOnPlane(body.forward-Vector3.up*.5f,axis).normalized;
            float along=(a*a+d*d-b*b)/(2*d),height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));var elbow=upper.position+axis*along+bend*height;
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
        }
    }
    void DisposeInventory()
    {
        ClearSharedAmmo();RestoreSharedSlings();
        DisposeViewArms();
        if(inventoryBowStringMaterial!=null)Destroy(inventoryBowStringMaterial);
        DisposeCamp();
        DisposeItemAudio();
        if(worldToolWork?.Line!=null)Destroy(worldToolWork.Line.gameObject);if(tackleLineMaterial!=null)Destroy(tackleLineMaterial);
        ClearInventoryPhysics();DisposeInventoryPreview();DisposePortableAppearance();foreach(var root in inventoryVisuals.Values)if(root!=null)DestroyInventoryVisual(root);inventoryVisuals.Clear();
        if(inventoryBagMaterial!=null)Destroy(inventoryBagMaterial);
        if(inventoryDropMaterial!=null)Destroy(inventoryDropMaterial);
        if(stoneCraftVoice!=null){stoneCraftVoice.Stop();Destroy(stoneCraftVoice.gameObject);}
    }
}
