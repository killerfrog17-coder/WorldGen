using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    bool campPlansOpen;CampBlueprint blueprintPlacement;V blueprintAnchor;bool blueprintAnchored;
    float blueprintYaw;Vector3 blueprintPoint,blueprintCursorGround;bool blueprintValid;string blueprintReason="";
    GameObject blueprintGhost;string blueprintGhostKey="";Material blueprintGhostMaterial,blueprintPlacementMaterial;
    readonly Dictionary<string,(CampBlueprint Plan,int Revision,double Cover)> blueprintProtectionCache=new Dictionary<string,(CampBlueprint,int,double)>();
    CampCover blueprintCover;CampStore blueprintStore;Vector2 blueprintMenuScroll;
    sealed class BlueprintJob {public CampObject Camp;public string Key;public CraftEquipmentState Hands;public double X,Z;}
    BlueprintJob blueprintJob;
    bool BlueprintBusy=>campPlansOpen||blueprintPlacement!=null;
    static Vector3 PlanVector(V v)=>new Vector3((float)v.X,(float)v.Y,(float)v.Z);
    bool IsBlueprint(CampObject c)=>c?.Kind==CampKind.Blueprint&&c.Blueprint!=null;
    Vector3 BlueprintOrigin(CampObject c)
    {TerrainHandoff.Coordinates(frame,c.Position,out double x,out double z);return new Vector3((float)(x-originX),(float)CampFloor(x,z),(float)(z-originZ));}
    Possession[] StoredAt(CampObject c)=>Inventory.Items.Where(i=>i.Place==PossessionPlace.CampStorage&&i.CampOwner==c.Id).ToArray();
    bool CampStoreReach(Possession item)
    {var c=Camp.Objects.FirstOrDefault(c=>c.Id==item.CampOwner);if(!IsBlueprint(c)||(c.Position-Position).Length*frame.RadiusM>10)return false;return InventoryReach(BlueprintNearest(c,PlayerFeet()));}
    Vector3 BlueprintNearest(CampObject c,Vector3 from)
    {
        var origin=BlueprintOrigin(c);var rotation=Quaternion.Euler(0,(float)c.Heading,0);var local=Quaternion.Inverse(rotation)*(from-origin);
        return origin+rotation*new Vector3(Mathf.Clamp(local.x,-(float)c.Blueprint.WidthM*.5f,(float)c.Blueprint.WidthM*.5f),.3f,Mathf.Clamp(local.z,-(float)c.Blueprint.DepthM*.5f,(float)c.Blueprint.DepthM*.5f));
    }
    bool BlueprintRay(CampObject c,Ray ray,out float distance)
    {
        if(CampBlueprints.Complete(c.Blueprint)&&campVisuals.TryGetValue(c.Id,out var visual)&&visual.Root.TryGetComponent<MeshCollider>(out var collider))
        {var worldRay=new Ray(transform.TransformPoint(ray.origin),transform.TransformDirection(ray.direction));bool hit=collider.Raycast(worldRay,out var contact,20);distance=contact.distance;return hit&&InventoryReach(ray.GetPoint(distance));}
        var rotation=Quaternion.Euler(0,(float)c.Heading,0);var local=new Ray(Quaternion.Inverse(rotation)*(ray.origin-BlueprintOrigin(c)),Quaternion.Inverse(rotation)*ray.direction);
        float height=(float)CampBlueprints.Height(c.Blueprint);var bounds=new Bounds(Vector3.up*height*.5f,new Vector3((float)c.Blueprint.WidthM+.12f,height+.16f,(float)c.Blueprint.DepthM+.12f));
        return bounds.IntersectRay(local,out distance)&&InventoryReach(ray.GetPoint(distance));
    }
    bool HandleBlueprintKeys(Keyboard k)
    {
        if(blueprintPlacement!=null)
        {
            bool primary=Mouse.current?.leftButton.wasPressedThisFrame==true,secondary=Mouse.current?.rightButton.wasPressedThisFrame==true;
            if(primary||secondary)campPointerConsumedFrame=Time.frameCount;
            if(k.escapeKey.wasPressedThisFrame){CancelBlueprintPlacement();return true;}
            if(secondary){if(blueprintAnchored){blueprintAnchored=false;blueprintGhostKey="";}else CancelBlueprintPlacement();return true;}
            if(k.rKey.wasPressedThisFrame){blueprintYaw+=k.shiftKey.isPressed?-15:15;blueprintGhostKey="";}
            UpdateBlueprintPlacement();if(primary)BlueprintPlacementClick();return true;
        }
        if(campPlansOpen){if(k.escapeKey.wasPressedThisFrame||k.vKey.wasPressedThisFrame){campPlansOpen=false;CaptureMouse();}return true;}
        if(!inventoryOpen&&!CampUiOpen&&k.vKey.wasPressedThisFrame&&!campSleeping&&!campResting)
        {CancelWorldToolWork();CancelBlueprintWork();CancelCampPlacement();campPlansOpen=true;CaptureMouse();return true;}
        return false;
    }
    void BeginBlueprint(CampDesign design)
    {
        CancelBlueprintPlacement();CancelCampPlacement();CancelWorldToolWork();CancelBlueprintWork();CancelItemCharge();PauseItemCraft(ActiveItemCraft);
        campPlansOpen=false;campMenu=null;SetInventoryOpen(false);blueprintPlacement=CampBlueprints.Create(design,design==CampDesign.Windbreak?2:2,2,blueprintCover,blueprintStore);blueprintYaw=heading;blueprintAnchored=false;CaptureMouse();UpdateBlueprintPlacement();
    }
    bool BlueprintGround(out Vector3 point)
    {
        point=default;var ray=new Ray(view.transform.localPosition,view.transform.forward);if(ray.direction.y>-.02f)return false;
        float previous=0;
        for(float t=.2f;t<=16+(float)humanCamera.Distance;t+=.25f)
        {
            var p=ray.GetPoint(t);if(p.y>CampFloor(p.x+originX,p.z+originZ)){previous=t;continue;}
            float lo=previous,hi=t;for(int i=0;i<7;i++){float m=(lo+hi)*.5f;var v=ray.GetPoint(m);if(v.y>CampFloor(v.x+originX,v.z+originZ))lo=m;else hi=m;}
            p=ray.GetPoint(hi);p.y=(float)CampFloor(p.x+originX,p.z+originZ);
            if(Vector3.Distance(PlayerFeet(),p)>14||TraceCombatObstacle(new Ray(view.transform.position,view.transform.forward),Math.Max(0,hi-.08f),checkGround:false)!=null)return false;
            point=p;return true;
        }
        return false;
    }
    void UpdateBlueprintPlacement()
    {
        if(blueprintPlacement==null||!Ready)return;blueprintValid=BlueprintGround(out var hover);blueprintReason="Aim at dry ground within reach.";
        if(!blueprintValid){if(blueprintGhost!=null)blueprintGhost.SetActive(false);return;}
        blueprintCursorGround=hover;var p=blueprintPlacement;var rotation=Quaternion.Euler(0,blueprintYaw,0);
        if(!blueprintAnchored)blueprintPoint=p.Design==CampDesign.BrushHut?hover:hover+rotation*new Vector3((float)p.WidthM*.5f,0,p.Design==CampDesign.LeanTo||p.Design==CampDesign.MaterialStore?(float)p.DepthM*.5f:0);
        else
        {
            TerrainHandoff.Coordinates(frame,blueprintAnchor,out double x,out double z);var start=new Vector3((float)(x-originX),(float)CampFloor(x,z),(float)(z-originZ));var delta=Quaternion.Inverse(rotation)*(hover-start);
            double w=Math.Abs(delta.x),d=Math.Abs(delta.z);
            if(p.Design==CampDesign.BrushHut)w=d=Vector3.Distance(new Vector3(start.x,0,start.z),new Vector3(hover.x,0,hover.z))*2;
            p=blueprintPlacement=CampBlueprints.Create(p.Design,w,d,p.Cover,p.Store);
            if(p.Design==CampDesign.BrushHut)blueprintPoint=start;
            else blueprintPoint=start+rotation*new Vector3((float)p.WidthM*.5f*(delta.x<0?-1:1),0,(p.Design==CampDesign.Windbreak||p.Design==CampDesign.DryingRack)?0:(float)p.DepthM*.5f*(delta.z<0?-1:1));
        }
        blueprintPoint.y=(float)CampFloor(blueprintPoint.x+originX,blueprintPoint.z+originZ);
        blueprintValid=BlueprintSite(p,blueprintPoint,blueprintYaw,out blueprintReason);
        string key=p.Design+"/"+p.Cover+"/"+p.Store+"/"+p.WidthM+"/"+p.DepthM;
        if(key!=blueprintGhostKey||blueprintGhost==null)
        {
            DestroyBlueprintGhost();blueprintGhostKey=key;blueprintGhost=new GameObject("Camp blueprint preview");blueprintGhost.transform.SetParent(transform,false);blueprintGhost.layer=30;
            if(blueprintPlacementMaterial==null)blueprintPlacementMaterial=new Material(Resources.Load<Shader>("WorldGenCampPlacement"));
            AddBlueprintMesh(blueprintGhost,LocalScatterMeshes.BuildBlueprint(p,false,true),blueprintPlacementMaterial,"Camp blueprint preview");
        }
        blueprintGhost.SetActive(true);blueprintGhost.transform.localPosition=blueprintPoint;blueprintGhost.transform.localRotation=rotation;
        blueprintPlacementMaterial.SetColor("_BaseColor",blueprintValid?new Color(.45f,.85f,.72f,.3f):new Color(1,.28f,.2f,.3f));
    }
    bool BlueprintSite(CampBlueprint p,Vector3 center,float yaw,out string reason)
    {
        reason="";var rot=Quaternion.Euler(0,yaw,0);float low=float.MaxValue,high=float.MinValue;
        foreach(float x in new[]{-.5f,0,.5f})foreach(float z in new[]{-.5f,0,.5f})
        {
            if(p.Design==CampDesign.BrushHut&&x!=0&&z!=0)continue;
            var v=center+rot*new Vector3(x*(float)p.WidthM,0,z*(float)p.DepthM);var sample=walkingPatch.Sample(frame.Position(v.x+originX,v.z+originZ));
            var water=SampleMotionWater(v.x+originX,v.z+originZ);float ground=(float)CampFloor(v.x+originX,v.z+originZ);if(sample.Underwater||double.IsFinite(water.Surface)&&water.Surface>ground+.01){reason="The footprint must be above water.";return false;}low=Math.Min(low,ground);high=Math.Max(high,ground);
        }
        if(high-low>.22){reason="Choose flatter ground or a smaller footprint.";return false;}
        float h=(float)CampBlueprints.Height(p);
        var colliders=Physics.OverlapBox(transform.TransformPoint(center+Vector3.up*(h*.5f+.1f)),new Vector3((float)p.WidthM*.5f,h*.5f,(float)p.DepthM*.5f),transform.rotation*rot,1<<ScatterSolidLayer,QueryTriggerInteraction.Ignore);
        if(colliders.Length>0){reason="The frame would intersect a tree, rock or existing structure.";return false;}
        foreach(var c in Camp.Objects.Where(c=>c.Kind==CampKind.Blueprint))
        {if((c.Position-Position).Length*frame.RadiusM>40)continue;var offset=Quaternion.Inverse(rot)*(BlueprintOrigin(c)-center);if(Math.Abs(offset.x)<(p.WidthM+c.Blueprint.WidthM)*.5&&Math.Abs(offset.z)<(p.DepthM+c.Blueprint.DepthM)*.5){reason="Leave room for the existing blueprint.";return false;}}
        return true;
    }
    void BlueprintPlacementClick()
    {
        if(!blueprintValid){InventoryFeedback(blueprintReason);return;}
        if(!blueprintAnchored){blueprintAnchor=frame.Position(blueprintCursorGround.x+originX,blueprintCursorGround.z+originZ);blueprintAnchored=true;blueprintGhostKey="";return;}
        var c=new CampObject{Kind=CampKind.Blueprint,Blueprint=blueprintPlacement.Copy(),Position=frame.Position(blueprintPoint.x+originX,blueprintPoint.z+originZ),Heading=blueprintYaw,LastSimulatedSeconds=Camp.SimulatedSeconds};
        Camp.Objects.Add(c);CancelBlueprintPlacement();campUpdateAt=0;UpdateCamp();InventoryFeedback(CampBlueprints.Name(c.Blueprint.Design)+" plan placed.");
    }
    void DestroyBlueprintGhost(){if(blueprintGhost==null)return;foreach(var f in blueprintGhost.GetComponentsInChildren<MeshFilter>())Destroy(f.sharedMesh);Destroy(blueprintGhost);blueprintGhost=null;}
    void CancelBlueprintPlacement(){blueprintPlacement=null;blueprintAnchored=false;DestroyBlueprintGhost();blueprintGhostKey="";}
    string BlueprintPrompt(CampObject c)
    {
        var p=c.Blueprint;if(blueprintJob?.Camp==c)return "Working on "+CampBlueprints.Name(p.Design)+"  [E cancel]";
        if(CampBlueprints.Complete(p))return CampBlueprints.Name(p.Design)+(p.Design==CampDesign.MaterialStore?" · store / take materials":p.Design==CampDesign.DryingRack?" · hang / take food":" · inspect")+"  [E]";
        var next=CampBlueprints.Components(p).First(cmp=>!CampBlueprints.Done(p,cmp));return CampBlueprints.Name(p.Design)+" · "+CampBlueprints.Requirement(next)+"  [E]  [Shift+E inspect]";
    }
    bool BlueprintInteract(CampObject c)
    {
        if(ActiveKeyboard?.shiftKey.isPressed==true||CampBlueprints.Complete(c.Blueprint)){campMenu=c;CaptureMouse();return true;}
        if(blueprintJob!=null){CancelBlueprintWork();return true;}
        BuildBlueprintPart(c);return true;
    }
    void BuildBlueprintPart(CampObject c)
    {
        var p=c.Blueprint;var defs=CampBlueprints.Components(p);int stage=CampBlueprints.Stage(p);var next=defs.FirstOrDefault(d=>d.Stage==stage&&!CampBlueprints.Done(p,d));if(next==null)return;
        var available=CraftAccessible();bool cut=UnlimitedResources||available.Any(i=>(PortableTechnology.Capabilities(i)&(CraftCapability.Cut|CraftCapability.Chop))!=0&&ItemUseRules.Condition(i)>0);
        // Prefer a component fitting the held material, then accessible nearby supplies.
        if(!CampBlueprints.HasMaterial(p,next))
        {
            var held=Inventory.QuickHeld;
            var candidates=available.OrderBy(i=>i==held?0:1).ThenBy(i=>i.MassKg).ToArray();
            var found=(from item in candidates from component in defs where component.Stage==stage&&!CampBlueprints.HasMaterial(p,component)&&CampBlueprints.Suitable(p,component,item)&&(cut||component.Kg>0||item.HeightM<=component.LengthM+.1) select (item,component)).FirstOrDefault();
            if(found.item!=null){next=found.component;var item=found.item;if(Inventory.Find(item.Id)==null&&!MovePossession(item.Id,PossessionPlace.Ground))return;
                if(!CampBlueprints.Supply(p,next.Key,Inventory,item.Id,cut,out var offcut,out var why)){InventoryFeedback(why);return;}
                RemoveDroppedBody(item.Id);if(offcut!=null){offcut.Position=c.Position;offcut.GroundOffsetM=.08;offcut.GroundMoving=true;}inventoryNearbyAt=0;InvalidateCampVisual(c);
            }
            else if(UnlimitedResources)
            {
                var item=BlueprintCreativeMaterial(p,next);Inventory.Items.Add(item);CampBlueprints.Supply(p,next.Key,Inventory,item.Id,true,out _,out _);InvalidateCampVisual(c);
            }
            else{InventoryFeedback(!cut&&candidates.Any(i=>CampBlueprints.Suitable(p,next,i))?"A cutting tool is needed to shorten the available wood.":CampBlueprints.Requirement(next)+" needed. Nearby materials can be used.");return;}
        }
        if(!CampBlueprints.HasMaterial(p,next)){InventoryFeedback("Added material; more is needed for this component.");return;}
        if(UnlimitedResources){CampBlueprints.Work(p,next.Key,next.Seconds);InvalidateCampVisual(c);campUpdateAt=0;return;}
        CancelItemCharge();CancelWorldToolWork();PauseItemCraft(ActiveItemCraft);CancelBlueprintWork();
        var hands=PrepareWorkHands(Array.Empty<string>());Inventory.ToolWorkEquipment=hands;blueprintJob=new BlueprintJob{Camp=c,Key=next.Key,Hands=hands,X=motion.X,Z=motion.Z};InventoryFeedback("Working on "+next.Name.ToLowerInvariant()+"...");
    }
    Possession BlueprintCreativeMaterial(CampBlueprint p,CampComponent part)
    {
        Possession item;
        if(part.Kg>0){item=PortableTechnology.Create(part.Kind==CampPartKind.Binding?PossessionKind.Cordage:p.Cover==CampCover.Hide?PossessionKind.WorkedHide:p.Cover==CampCover.Bark?PossessionKind.BarkSheet:PossessionKind.DryGrass,Position);item.MassKg=part.Kg;}
        else item=new Possession{Id="creative-wood-"+Guid.NewGuid().ToString("N"),Kind=PossessionKind.Branch,HeightM=part.LengthM,WidthM=part.WidthM*1.15,DepthM=part.WidthM*1.15,MassKg=part.LengthM*part.WidthM*part.WidthM*450,WoodStraightness=.9,WoodSoundness=.9,Position=Position};
        return item;
    }
    void UpdateBlueprintWork()
    {
        if(blueprintJob==null)return;var job=blueprintJob;
        if(DeathVisible||Spectating||!Camp.Objects.Contains(job.Camp)||Math.Pow(motion.X-job.X,2)+Math.Pow(motion.Z-job.Z,2)>.3||!InventoryReach(BlueprintNearest(job.Camp,PlayerFeet()))){CancelBlueprintWork();return;}
        if(paused||mapOpen||ecologyOpen||chatOpen||!Ready)return;
        double dt=Math.Min(.25,Time.unscaledDeltaTime);if(!SpendCombatStamina(dt*.15)){CancelBlueprintWork();return;}
        CampBlueprints.Work(job.Camp.Blueprint,job.Key,dt*86400/(PlayerRules.Settings.RealMinutesPerDay*60*PlayerRules.Settings.ItemCraftingTimeMultiplier));
        if(Time.unscaledTimeAsDouble>=craftSoundAt){PlayItemEffect("fibre-handle",view.transform.position,.25f,true);craftSoundAt=Time.unscaledTimeAsDouble+1;}
        var definition=CampBlueprints.Components(job.Camp.Blueprint).First(d=>d.Key==job.Key);
        if(CampBlueprints.Done(job.Camp.Blueprint,definition)){CancelBlueprintWork();InvalidateCampVisual(job.Camp);campUpdateAt=0;InventoryFeedback(CampBlueprints.Complete(job.Camp.Blueprint)?"Construction complete.":"Component fitted.");}
    }
    void CancelBlueprintWork()
    {var work=blueprintJob;if(work==null)return;blueprintJob=null;Inventory.ToolWorkEquipment=null;RestoreWorkHands(new ItemCraftJob{Equipment=work.Hands});if(stoneCraftVoice!=null)stoneCraftVoice.Stop();}
    void StoreBlueprintItem(CampObject c,Possession item)
    {
        if(!CampBlueprints.Fits(c.Blueprint,StoredAt(c),item)){InventoryFeedback("That item does not fit this store or rack.");return;}
        if(Inventory.Find(item.Id)==null&&!MovePossession(item.Id,PossessionPlace.Ground))return;
        if(CampBlueprints.Deposit(c,Inventory,item.Id,out var why)){RemoveDroppedBody(item.Id);InvalidateCampVisual(c);inventoryNearbyAt=0;}else InventoryFeedback(why);
    }
    void TakeBlueprintItem(CampObject c,Possession item)
    {if(!CampStoreReach(item))return;if(!Inventory.Hold(item.Id,out _)&&!Inventory.Stow(item.Id,out _)){Inventory.Move(item.Id,PossessionPlace.Ground,"",0,0,false,out _);item.Position=Position;item.GroundMoving=true;item.GroundOffsetM=.08;}item.CampOwner=null;c.Blueprint.Revision++;InvalidateCampVisual(c);inventoryNearbyAt=0;}
    void UndoBlueprintPart(CampObject c)
    {
        if(StoredAt(c).Length>0){InventoryFeedback("Empty the rack or store first.");return;}CancelBlueprintWork();var p=c.Blueprint;
        var last=CampBlueprints.Components(p).LastOrDefault(d=>CampBlueprints.State(p,d)!=null);if(last==null){RemoveCamp(c);return;}
        var state=CampBlueprints.State(p,last);foreach(var item in state.Materials)DropBlueprintMaterial(c,item);p.Parts.Remove(state);p.Revision++;InvalidateCampVisual(c);inventoryNearbyAt=0;
    }
    void DropBlueprintMaterial(CampObject c,Possession item)
    {item.Place=PossessionPlace.Ground;item.CampOwner=null;item.ContainerId="";item.Position=c.Position;item.GroundOffsetM=.1;item.GroundMoving=true;item.InFlight=false;item.GroundVelocity=item.GroundSpin=new V();if(!Inventory.Items.Any(p=>p.Id==item.Id))Inventory.Items.Add(item);}
    CampProtection BlueprintProtectionAt(V at)
    {
        TerrainHandoff.Coordinates(frame,at,out double x,out double z);double rain=0,wind=0,shade=0;
        foreach(var c in Camp.Objects.Where(IsBlueprint))
        {
            if((c.Position-at).Length*frame.RadiusM>20)continue;TerrainHandoff.Coordinates(frame,c.Position,out double cx,out double cz);var r=Quaternion.Inverse(Quaternion.Euler(0,(float)c.Heading,0));var local=r*new Vector3((float)(x-cx),0,(float)(z-cz));var airflow=r*new Vector3((float)CurrentWeather.WindEastMps,0,(float)CurrentWeather.WindNorthMps);
            if(!blueprintProtectionCache.TryGetValue(c.Id,out var cached)||cached.Plan!=c.Blueprint||cached.Revision!=c.Blueprint.Revision){cached=(c.Blueprint,c.Blueprint.Revision,CampBlueprints.CoverFraction(c.Blueprint));blueprintProtectionCache[c.Id]=cached;}
            var protect=CampBlueprints.Protection(c.Blueprint,local.x,local.z,airflow.x,airflow.z,cached.Cover);rain=Math.Max(rain,protect.Rain);wind=Math.Max(wind,protect.Wind);shade=Math.Max(shade,protect.Shade);
        }
        return new CampProtection(rain,wind,shade);
    }
    void UpdateBlueprintContents(CampObject c,double seconds)
    {
        if(c.Blueprint.Design!=CampDesign.DryingRack||seconds<=0)return;bool smoke=Camp.Objects.Any(f=>f.Kind==CampKind.Fire&&f.FirePowerKw>.3&&f.FirePowerKw<5&&(f.Position-c.Position).Length*frame.RadiusM<1.2);
        foreach(var food in StoredAt(c)){bool before=food.Dried;CampBlueprints.Dry(food,seconds,CurrentWeather.RainMmPerHour,CurrentWeather.Humidity,Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps),CurrentWeather.TemperatureC,smoke);if(before!=food.Dried){c.Blueprint.Revision++;InvalidateCampVisual(c);}}
    }
    void DrawBlueprintUi()
    {
        if(blueprintPlacement!=null)
        {
            var p=blueprintPlacement;var components=CampBlueprints.Components(p);int wood=components.Count(c=>c.Kg==0);double binding=components.Where(c=>c.Kind==CampPartKind.Binding).Sum(c=>c.Kg),cover=components.Where(c=>c.Kind==CampPartKind.Cover).Sum(c=>c.Kg);
            string action=blueprintAnchored?"Confirm [LMB] · Rotate [R] · Back [RMB]":"Set "+(p.Design==CampDesign.BrushHut?"centre":"first corner / end")+" [LMB] · Rotate [R] · Cancel [RMB]";
            var style=new GUIStyle(GUI.skin.box){alignment=TextAnchor.MiddleCenter,wordWrap=true,fontSize=15};GUI.Box(new Rect(Screen.width*.5f-300,Screen.height-130,600,108),CampBlueprints.Name(p.Design)+" · "+p.WidthM.ToString("0.00")+" × "+p.DepthM.ToString("0.00")+" m\n"+wood+" wood pieces · "+binding.ToString("0.00")+" kg binding · "+cover.ToString("0.0")+" kg covering\n"+(blueprintValid?action:blueprintReason),style);
        }
        if(blueprintJob!=null)
        {
            var p=blueprintJob.Camp.Blueprint;var d=CampBlueprints.Components(p).First(c=>c.Key==blueprintJob.Key);float progress=(float)(CampBlueprints.State(p,d).WorkSeconds/d.Seconds);
            var bar=new Rect(Screen.width*.5f-150,Screen.height-70,300,8);InventoryFill(bar,new Color(.08f,.1f,.1f,.85f));bar.width*=progress;InventoryFill(bar,new Color(.75f,.68f,.4f));GUI.Label(new Rect(Screen.width*.5f-150,Screen.height-100,300,24),"Fitting "+d.Name.ToLowerInvariant());
        }
        if(!campPlansOpen)return;var rect=new Rect(Screen.width*.5f-225,Screen.height*.5f-240,450,480);GUI.Box(rect,"");GUILayout.BeginArea(new Rect(rect.x+18,rect.y+15,rect.width-36,rect.height-30));GUILayout.Label("Camp plans",new GUIStyle(GUI.skin.label){fontSize=24});
        GUILayout.Label("Covering");blueprintCover=(CampCover)GUILayout.SelectionGrid((int)blueprintCover,new[]{"Vegetation","Hide","Bark"},3,GUILayout.Height(30));
        GUILayout.Label("Material store");blueprintStore=(CampStore)GUILayout.SelectionGrid((int)blueprintStore,new[]{"Wood","Stone","Plants"},3,GUILayout.Height(30));GUILayout.Space(10);
        foreach(CampDesign d in Enum.GetValues(typeof(CampDesign)))if(GUILayout.Button(CampBlueprints.Name(d),GUILayout.Height(39)))BeginBlueprint(d);
        GUILayout.Space(8);GUILayout.Label("Fire: lay tinder directly.\nBedding: spread material, then add layers.");if(GUILayout.Button("Close",GUILayout.Height(30))){campPlansOpen=false;CaptureMouse();}GUILayout.EndArea();
    }
    void DrawBlueprintDetails(CampObject c)
    {
        var p=c.Blueprint;var components=CampBlueprints.Components(p);GUILayout.Label(CampBlueprints.Name(p.Design)+" · "+p.WidthM.ToString("0.00")+" × "+p.DepthM.ToString("0.00")+" m");
        GUILayout.Label(components.Count(d=>CampBlueprints.Done(p,d))+" / "+components.Count+" components fitted");
        if(!CampBlueprints.Complete(p))
        {
            int stage=CampBlueprints.Stage(p);foreach(var group in components.Where(d=>d.Stage==stage&&!CampBlueprints.Done(p,d)).GroupBy(d=>CampBlueprints.Requirement(d)))GUILayout.Label(group.Count()+" × "+group.Key);
            if(GUILayout.Button("Add material / continue work",GUILayout.Height(36))){campMenu=null;CaptureMouse();BuildBlueprintPart(c);}
        }
        else if(p.Design==CampDesign.MaterialStore||p.Design==CampDesign.DryingRack)
        {
            foreach(var item in CraftAccessible().Where(i=>i.Place!=PossessionPlace.CampStorage&&CampBlueprints.StoreAccepts(p,i)).ToArray())
            {bool prior=GUI.enabled;GUI.enabled=CampBlueprints.Fits(p,StoredAt(c),item);if(GUILayout.Button(new GUIContent("Add "+item.Name,GUI.enabled?"":"No suitable space remaining"),GUILayout.Height(32)))StoreBlueprintItem(c,item);GUI.enabled=prior;}
            foreach(var item in StoredAt(c))if(GUILayout.Button("Take "+item.Name+(!item.Dried&&p.Design==CampDesign.DryingRack?" · "+Math.Min(99,item.DryingSeconds/216).ToString("0")+"%":""),GUILayout.Height(32)))TakeBlueprintItem(c,item);
        }
        if(p.Design==CampDesign.LeanTo||p.Design==CampDesign.BrushHut||p.Design==CampDesign.Windbreak)
        {
            foreach(var item in CraftAccessible().Where(i=>CampBlueprints.CoverMaterial(p,i)).GroupBy(i=>i.Kind).Select(g=>g.First()))
                if(GUILayout.Button("Thicken covering: "+item.Name,GUILayout.Height(32)))
                {if(Inventory.Find(item.Id)==null&&!MovePossession(item.Id,PossessionPlace.Ground))break;if(CampBlueprints.Thicken(p,Inventory,item.Id,out var why)){RemoveDroppedBody(item.Id);InvalidateCampVisual(c);inventoryNearbyAt=0;}else InventoryFeedback(why);break;}
        }
        if(GUILayout.Button("Remove last fitted component",GUILayout.Height(32)))UndoBlueprintPart(c);
        if(GUILayout.Button("Dismantle plan and recover materials",GUILayout.Height(32)))RemoveCamp(c);
    }
}
