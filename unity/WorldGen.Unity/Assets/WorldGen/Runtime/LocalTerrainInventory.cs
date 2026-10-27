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
    PlayerInventory Inventory=>PlayerRules.Inventory??(PlayerRules.Inventory=PlayerInventory.Starter());
    readonly HashSet<ulong> inventoryCollected=new HashSet<ulong>();
    readonly List<NearbyPossession> inventoryNearby=new List<NearbyPossession>();
    readonly Dictionary<string,GameObject> inventoryVisuals=new Dictionary<string,GameObject>();
    readonly Dictionary<string,PossessionPlace> inventoryVisualPlaces=new Dictionary<string,PossessionPlace>();
    bool inventoryOpen,inventoryInitialized;double inventoryNearbyAt;
    string inventoryNotice="",inventorySelected="";float inventoryNoticeUntil;
    Action inventoryPending;double inventoryPendingAt;
    Material inventoryBagMaterial;
    readonly HashSet<ArtModel> inventoryPinnedArt=new HashSet<ArtModel>();
    readonly bool inventoryReviewMode=Environment.GetCommandLineArgs().Contains("--worldgen-inventory-check");
    Transform inventoryBoneOwner;Dictionary<string,Transform> inventoryBones;
    bool InventoryBusy=>inventoryPending!=null;
    void InitializeInventory()
    {
        if(inventoryInitialized)return;inventoryInitialized=true;Inventory.Validate();
        foreach(var id in Inventory.CollectedNatural)inventoryCollected.Add(id);scatterDirty=true;
    }
    bool CollectedRock(ScatterInstance p)=>(p.Kind==ScatterKind.Stone||p.Kind==ScatterKind.Boulder)&&inventoryCollected.Contains(p.Id);
    static Possession RockPossession(ScatterInstance p)
    {
        // Match visible dimensions. Massive boulders are scenery, not pocket rocks.
        double mass=p.WidthM*p.WidthM*.86*p.HeightM*Math.PI/6*2600;
        bool small=p.WidthM<=.17&&mass<=1.5;
        return new Possession{Id="rock-"+p.Id.ToString("x16"),NaturalId=p.Id,Kind=small?PossessionKind.SmallRock:PossessionKind.LargeRock,Place=PossessionPlace.Ground,
            WidthM=p.WidthM,HeightM=p.HeightM,DepthM=p.WidthM*.86,MassKg=Math.Max(.025,mass),
            CellsWide=Math.Max(1,(int)Math.Ceiling(p.WidthM/.1)),CellsHigh=Math.Max(1,(int)Math.Ceiling(p.HeightM/.1)),Variant=p.Variant,Position=p.Position,Tint=p.Color};
    }
    bool InventoryReach(Vector3 point)
    {
        var eye=new Vector3((float)(motion.X-originX),(float)motion.Y,(float)(motion.Z-originZ));
        if((point-eye).sqrMagnitude>3*3)return false;
        for(int i=1;i<8;i++){var p=Vector3.Lerp(eye,point,i/8f);if(VisibleGround(p.x+originX,p.z+originZ,double.NegativeInfinity)>p.y+.06)return false;}
        return true;
    }
    void RefreshInventoryNearby(bool force=false)
    {
        if(!Ready||!inventoryInitialized||!force&&Time.unscaledTimeAsDouble<inventoryNearbyAt)return;
        inventoryNearbyAt=Time.unscaledTimeAsDouble+.2;inventoryNearby.Clear();
        var player=new Vector3((float)(motion.X-originX),(float)motion.Y,(float)(motion.Z-originZ));
        foreach(var chunk in scatterChunks.Values)
        {
            if(chunk.Roots==null||chunk.AllBounds.SqrDistance(player)>16)continue;
            foreach(var group in chunk.Groups)
            {
                if(group.Bounds.SqrDistance(player)>16)continue;
                foreach(int i in group.Indices)
                {
                    var p=chunk.Source.Instances[i];if(p.Kind!=ScatterKind.Stone&&p.Kind!=ScatterKind.Boulder||CollectedRock(p))continue;
                    var point=chunk.Roots[i]+Vector3.up*(float)p.HeightM*.5f;
                    if(!InventoryReach(point))continue;var item=RockPossession(p);
                    if(item.MassKg>25||p.WidthM>.55||p.HeightM>.45)continue;
                    inventoryNearby.Add(new NearbyPossession{Item=item,Natural=true,Point=point,Bounds=new Bounds(point,new Vector3((float)p.WidthM+.06f,(float)p.HeightM+.06f,(float)p.WidthM+.06f))});
                }
            }
        }
        foreach(var item in Inventory.Items.Where(i=>i.Place==PossessionPlace.Ground))
        {
            if((item.Position-Position).Length*frame.RadiusM>4)continue;
            TerrainHandoff.Coordinates(frame,item.Position,out double x,out double z);
            var point=new Vector3((float)(x-originX),(float)Ground(x,z)+(float)item.HeightM*.5f,(float)(z-originZ));
            if(InventoryReach(point))inventoryNearby.Add(new NearbyPossession{Item=item,Point=point,Bounds=new Bounds(point,new Vector3((float)item.WidthM+.06f,(float)item.HeightM+.06f,(float)item.DepthM+.06f))});
        }
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
        {if(item.Place==PossessionPlace.Ground)return inventoryNearby.Any(n=>n.Item.Id==item.Id);if(item.Place!=PossessionPlace.Container)return true;item=Inventory.Find(item.ContainerId);}
        return false;
    }
    void InventoryFeedback(string text)
    {inventoryNotice=text;inventoryNoticeUntil=Time.unscaledTime+4;if(!inventoryOpen)PostExplorerChat(text,true);}
    void ScheduleInventory(Action action,bool bagAccess)
    {
        if(InventoryBusy)return;
        if(bagAccess){inventoryPending=action;inventoryPendingAt=Time.unscaledTimeAsDouble+.65;}else action();
    }
    bool MovePossession(string id,PossessionPlace place,string bag="",int x=0,int y=0,bool rotated=false,bool autoStow=false,bool autoHand=false)
    {
        RefreshInventoryNearby(true);var candidate=InventoryCandidate(id);if(candidate==null)return false;
        var item=candidate.Item;
        if(candidate.Natural&&!inventoryNearby.Any(n=>n.Item.Id==id)||!candidate.Natural&&!AccessiblePossession(item)){InventoryFeedback("That item is out of reach.");return false;}
        if(place==PossessionPlace.Container)
        {var dest=Inventory.Find(bag);if(autoStow)dest=Inventory.Bag;if(dest==null||!AccessiblePossession(dest)){InventoryFeedback("That bag is out of reach.");return false;}}
        if(candidate.Natural)Inventory.Items.Add(item);
        string reason;bool moved=autoStow?Inventory.Stow(id,out reason):autoHand?Inventory.Hold(id,out reason):Inventory.Move(id,place,bag,x,y,rotated,out reason);
        if(!moved){if(candidate.Natural)Inventory.Items.Remove(item);InventoryFeedback(reason);return false;}
        if(candidate.Natural){inventoryCollected.Add(item.NaturalId);Inventory.CollectedNatural.Add(item.NaturalId);scatterDirty=true;}
        if(place==PossessionPlace.Ground&&!autoStow&&!autoHand)
        {
            double yaw=heading*Math.PI/180;item.Position=frame.Position(motion.X+Math.Sin(yaw)*.85,motion.Z+Math.Cos(yaw)*.85);
        }
        inventoryNotice="";inventoryNoticeUntil=0;inventorySelected=id;inventoryNearbyAt=0;if(inventoryOpen)RenderInventoryPreview();return true;
    }
    void RequestPossession(string id,PossessionPlace place,string bag="",int x=0,int y=0,bool rotated=false,bool autoStow=false,bool autoHand=false)
    {
        var item=InventoryCandidate(id)?.Item;if(item==null)return;
        bool access=autoStow||place==PossessionPlace.Container||item.Place==PossessionPlace.Container||item.Kind==PossessionKind.TestBag;
        ScheduleInventory(()=>MovePossession(id,place,bag,x,y,rotated,autoStow,autoHand),access);
    }
    bool HandleInventoryKeys()
    {
        var k=ActiveKeyboard;if(k==null||!Ready||DeathVisible||Spectating||chatOpen||paused||mapOpen||ecologyOpen||!Application.isFocused&&!inventoryReviewMode)return inventoryOpen;
        if(!inventoryNameEditing&&(k.iKey.wasPressedThisFrame||k.tabKey.wasPressedThisFrame)){SetInventoryOpen(!inventoryOpen);return true;}
        if(inventoryOpen)
        {
            if(k.escapeKey.wasPressedThisFrame){SetInventoryOpen(false);return true;}
            if(inventoryDrag!=null){if(k.rKey.wasPressedThisFrame)inventoryDragRotated=!inventoryDragRotated;return true;}
            if(inventoryNameEditing)return true;
        }
        if(k.eKey.wasPressedThisFrame||k.bKey.wasPressedThisFrame)RefreshInventoryNearby(true);
        if(k.digit1Key.wasPressedThisFrame)Inventory.ActiveHand=PossessionPlace.RightHand;
        if(k.digit2Key.wasPressedThisFrame)Inventory.ActiveHand=PossessionPlace.LeftHand;
        if(k.qKey.wasPressedThisFrame)Inventory.ActiveHand=Inventory.ActiveHand==PossessionPlace.RightHand?PossessionPlace.LeftHand:PossessionPlace.RightHand;
        var held=Inventory.AtHand(Inventory.ActiveHand);
        if(k.gKey.wasPressedThisFrame&&held!=null)RequestPossession(held.Id,PossessionPlace.Ground);
        if(k.rKey.wasPressedThisFrame&&held!=null)RequestPossession(held.Id,PossessionPlace.Container,autoStow:true);
        if(k.bKey.wasPressedThisFrame)
        {
            var pack=Inventory.Bag;if(pack!=null)RequestPossession(pack.Id,PossessionPlace.Ground);
            else {pack=inventoryNearby.FirstOrDefault(n=>n.Item.Kind==PossessionKind.TestBag)?.Item;if(pack!=null)RequestPossession(pack.Id,PossessionPlace.Back);}
        }
        if(!inventoryOpen&&k.eKey.wasPressedThisFrame&&!k.f3Key.isPressed)
        {
            var ray=new Ray(view.transform.localPosition,view.transform.forward);float closest=float.MaxValue;NearbyPossession target=null;
            if(inventoryReviewMode)foreach(var n in inventoryNearby)Debug.Log("INVENTORY RAY "+ray+" point="+n.Point+" screen="+view.WorldToViewportPoint(n.Point)+" heading="+heading+" pitch="+pitch+" motor="+motion.X+","+motion.Y+","+motion.Z);
            foreach(var n in inventoryNearby)if(n.Bounds.IntersectRay(ray,out float t)&&t<closest){target=n;closest=t;}
            if(inventoryReviewMode)Debug.Log("INVENTORY AIM target="+target?.Item.Id+" hand="+Inventory.ActiveHand+" candidates="+inventoryNearby.Count);
            if(target!=null)RequestPossession(target.Item.Id,Inventory.ActiveHand,autoHand:true);
        }
        return inventoryOpen;
    }
    void SetInventoryOpen(bool open)
    {
        inventoryOpen=open;inventoryDrag=null;inventoryContext=null;inventoryNameEditing=false;
        if(open){RefreshInventoryNearby(true);PrepareInventoryPreview();}else if(inventoryPreviewRoot!=null)inventoryPreviewRoot.SetActive(false);
        GUIUtility.keyboardControl=0;CaptureMouse();
    }
    void UpdateInventory()
    {
        if(!Ready)return;InitializeInventory();
        if((DeathVisible||Spectating)&&inventoryOpen)SetInventoryOpen(false);
        if(DeathVisible){inventoryPending=null;return;}
        if(inventoryPending!=null&&Time.unscaledTimeAsDouble>=inventoryPendingAt&&!paused&&(Application.isFocused||inventoryReviewMode))
        {var action=inventoryPending;inventoryPending=null;action();}
        if(inventoryOpen)RefreshInventoryNearby();
    }
    GameObject CreatePossessionVisual(Possession item,int layer)
    {
        var root=new GameObject(item.Name);root.layer=layer;
        if(item.Kind==PossessionKind.TestBag)
        {
            var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);if(Application.isPlaying)Destroy(sphere.GetComponent<Collider>());else DestroyImmediate(sphere.GetComponent<Collider>());sphere.transform.SetParent(root.transform,false);sphere.transform.localPosition=Vector3.up*.5f;sphere.transform.localScale=new Vector3(1,1,.55f);sphere.layer=layer;
            if(inventoryBagMaterial==null){inventoryBagMaterial=new Material(Resources.Load<Shader>("WorldGenInventoryPreview"));inventoryBagMaterial.SetColor("_Color",new Color(.49f,.38f,.21f));}
            sphere.GetComponent<Renderer>().sharedMaterial=inventoryBagMaterial;
            var props=new MaterialPropertyBlock();props.SetFloat("_WorldLighting",layer==29?0:1);sphere.GetComponent<Renderer>().SetPropertyBlock(props);
        }
        else
        {
            var art=Art(ScatterKind.Stone,item.Variant,0);inventoryPinnedArt.Add(art);
            foreach(var part in art.Parts)
            {
                var obj=new GameObject("Rock surface",typeof(MeshFilter),typeof(MeshRenderer));obj.layer=layer;obj.transform.SetParent(root.transform,false);
                obj.GetComponent<MeshFilter>().sharedMesh=part.Mesh;var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=part.Material;
                var props=new MaterialPropertyBlock();props.SetVector("_Tint",new Vector4((float)item.Tint.X,(float)item.Tint.Y,(float)item.Tint.Z,1));props.SetVector("_LodTransition",new Vector4(0,1,0,0));renderer.SetPropertyBlock(props);
            }
        }
        root.transform.localScale=new Vector3((float)item.WidthM,(float)item.HeightM,(float)(item.Kind==PossessionKind.TestBag?item.DepthM/.55:item.WidthM));return root;
    }
    void UpdateInventoryVisuals()
    {
        if(!inventoryInitialized||humanPlayer==null||!Ready)return;
        var body=humanPlayer.Root.transform;
        if(inventoryBoneOwner!=body){inventoryBoneOwner=body;inventoryBones=body.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());}
        if(!deathPresented)PoseInventoryHands(humanPlayer,inventoryBones);
        var present=new HashSet<string>();
        foreach(var item in Inventory.Items)
        {
            if(item.Place==PossessionPlace.Container)continue;
            bool near=item.Place!=PossessionPlace.Ground||(item.Position-Position).Length*frame.RadiusM<160;
            if(!near)continue;present.Add(item.Id);
            if(!inventoryVisuals.TryGetValue(item.Id,out var root)){root=CreatePossessionVisual(item,30);root.transform.SetParent(transform,false);inventoryVisuals[item.Id]=root;}
            root.SetActive(!Spectating||item.Place==PossessionPlace.Ground);
            if(item.Kind==PossessionKind.TestBag&&(!inventoryVisualPlaces.TryGetValue(item.Id,out var previous)||previous!=item.Place))
            {
                inventoryVisualPlaces[item.Id]=item.Place;
                foreach(var renderer in root.GetComponentsInChildren<Renderer>()){var props=new MaterialPropertyBlock();renderer.GetPropertyBlock(props);props.SetFloat("_InventoryWearerId",item.Place==PossessionPlace.Back?1:0);renderer.SetPropertyBlock(props);}
            }
            if(item.Place==PossessionPlace.Ground)
            {TerrainHandoff.Coordinates(frame,item.Position,out double x,out double z);root.transform.localPosition=new Vector3((float)(x-originX),(float)Ground(x,z)-.01f,(float)(z-originZ));root.transform.localRotation=Quaternion.identity;}
            else if(item.Place==PossessionPlace.Back)
            {root.transform.position=inventoryBones["spine_02"].position-body.forward*.18f-Vector3.up*.2f;root.transform.rotation=body.rotation;}
            else
            {
                bool right=item.Place==PossessionPlace.RightHand;string name="hand_"+(right?"r":"l");
                var hand=inventoryBones[name];
                if(humanHideBody&&!deathPresented)
                {root.transform.position=view.transform.TransformPoint(new Vector3(item.Hands==2?0:right?.29f:-.29f,-.32f,.62f));root.transform.rotation=view.transform.rotation;}
                else
                {root.transform.position=(item.Hands==2?(inventoryBones["hand_r"].position+inventoryBones["hand_l"].position)*.5f:hand.position)-Vector3.up*(float)item.HeightM*.35f;root.transform.rotation=body.rotation;}
            }
        }
        foreach(var id in inventoryVisuals.Keys.Where(id=>!present.Contains(id)).ToArray()){Destroy(inventoryVisuals[id]);inventoryVisuals.Remove(id);inventoryVisualPlaces.Remove(id);}
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
        DisposeInventoryPreview();foreach(var root in inventoryVisuals.Values)if(root!=null)Destroy(root);inventoryVisuals.Clear();
        if(inventoryBagMaterial!=null)Destroy(inventoryBagMaterial);
    }
}
