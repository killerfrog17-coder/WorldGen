using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    WorldGenFirstPersonRig viewArms;
    readonly List<Mesh> viewArmMeshes=new List<Mesh>();
    readonly List<SkinnedMeshRenderer> viewArmRenderers=new List<SkinnedMeshRenderer>();
    double viewReleaseAt=-100,viewGatherAt=-100,viewPunchAt=-100,viewConsumeAt=-100;
    double viewPunchDuration=.55;
    double viewTravelPhase;
    float viewTravelAmount;
    bool viewReleaseLeft,viewArmsVisible,viewReleaseSpear,viewConsumeDrink;
    ItemPrimaryUse viewReleaseKind;
    Vector3 viewGatherTarget;
    GameObject viewNockedArrow;string viewNockId;
    Renderer[] viewNockRenderers=Array.Empty<Renderer>();
    Material inventoryBowStringMaterial;
    int viewArmsFrame=-1;
    bool viewArmPoseImmediate;
    float viewArmDiagnosticDt=-1;
    string viewArmDiagnosticClip;double viewArmDiagnosticTime;
    readonly Dictionary<Renderer,ShadowCastingMode> viewWorldItemShadows=new Dictionary<Renderer,ShadowCastingMode>();
    sealed class ViewPossession {public GameObject Root;public PossessionKind Kind;public Renderer[] Renderers;public Mesh SlingMesh;public Vector3[] SlingRest,SlingVertices;public Color[] SlingColors;public Vector3 SlingDirection=Vector3.down;}
    readonly Dictionary<string,ViewPossession> viewPossessions=new Dictionary<string,ViewPossession>();
    readonly HashSet<string> viewPossessionsPresent=new HashSet<string>();

    Vector3 BowDrawPoint(float draw)=>SharedHumanMotion&&inventoryBones.ContainsKey("hand_r")?NaturalBowStringGrip:viewArms!=null&&viewArms.Bones.TryGetValue("Nock.R",out var nock)?nock.position:view.transform.TransformPoint(new Vector3(.02f,-.045f,.36f-.2f*draw));
    void CreateBowString(GameObject root,int layer)
    {
        if(inventoryBowStringMaterial==null){inventoryBowStringMaterial=new Material(Resources.Load<Shader>("WorldGenInventoryPreview"));inventoryBowStringMaterial.SetColor("_Color",new Color(.59f,.47f,.31f));}
        var obj=new GameObject("Bow string",typeof(LineRenderer));obj.layer=layer;obj.transform.SetParent(root.transform,false);
        var line=obj.GetComponent<LineRenderer>();line.sharedMaterial=inventoryBowStringMaterial;line.useWorldSpace=false;line.positionCount=3;line.widthMultiplier=.003f;line.shadowCastingMode=ShadowCastingMode.Off;
        line.SetPositions(new[]{new Vector3(.38f,0,0),new Vector3(.38f,.5f,0),new Vector3(.38f,1,0)});
    }
    void UpdateBowString(GameObject root,Possession item,bool firstPerson=false)
    {
        var line=root.GetComponentInChildren<LineRenderer>();if(line==null)return;
        bool drawing=SharedHumanMotion?sharedBowItem==item.Id&&sharedBowDraw>.002f&&!(viewReleaseKind==ItemPrimaryUse.Bow&&Time.unscaledTimeAsDouble-viewReleaseAt<.65):firstPerson&&itemCharge?.Weapon==item&&!itemCharge.Throw;
        if(drawing)line.SetPosition(1,root.transform.InverseTransformPoint(BowDrawPoint(1)));
        else line.SetPosition(1,new Vector3(.38f,.5f,0));
    }
    bool SupportSpear(Possession item)=>item!=null&&ItemUseRules.Profile(item).Primary==ItemPrimaryUse.Thrust&&item.HeightM>=1.1&&
        (Inventory.AtHand(item.Place==PossessionPlace.LeftHand?PossessionPlace.RightHand:PossessionPlace.LeftHand)==null||item.Hands==2)&&!(itemCharge?.Throw??false)&&(!SharedHumanMotion||pendingRanged?.Charge.Throw!=true);
    static bool LongViewSpear(Possession item)=>item!=null&&item.HeightM>=1.1&&ItemUseRules.Profile(item).Primary==ItemPrimaryUse.Thrust;
    void EnsureViewArms()
    {
        if(viewArms!=null){viewArms.SetPerson(humanPlayer.Definition);return;}
        viewArms=new WorldGenFirstPersonRig(view.transform,humanPlayer.Definition);
        viewArmRenderers.AddRange(viewArms.Renderers);foreach(var renderer in viewArmRenderers)viewArmMeshes.Add(renderer.sharedMesh);
        RenderPipelineManager.beginCameraRendering+=BeginViewArmsCamera;RenderPipelineManager.endCameraRendering+=EndViewArmsCamera;
    }
    void RestoreWorldHeldRendering()
    {foreach(var pair in viewWorldItemShadows)if(pair.Key!=null)pair.Key.shadowCastingMode=pair.Value;viewWorldItemShadows.Clear();}
    void BeginViewArmsCamera(ScriptableRenderContext context,Camera camera)
    {
        RestoreWorldHeldRendering();bool show=viewArmsVisible&&camera==view;
        if(SharedHumanMotion)return;
        foreach(var r in viewArmRenderers)if(r!=null)r.enabled=show;
        foreach(var p in viewPossessions.Values)foreach(var r in p.Renderers)if(r!=null)r.enabled=show;
        foreach(var r in viewNockRenderers)if(r!=null)r.enabled=show;
        if(camera!=view||!humanHideBody||Spectating||!inventoryInitialized)return;
        // Real held items remain at the world avatar's hands. The local camera
        // excludes their colour pass but preserves their real world shadows.
        foreach(var item in Inventory.Items)
        {
            if(item.Place!=PossessionPlace.RightHand&&item.Place!=PossessionPlace.LeftHand)continue;
            if(!inventoryVisuals.TryGetValue(item.Id,out var root))continue;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {if(!viewWorldItemShadows.ContainsKey(renderer))viewWorldItemShadows.Add(renderer,renderer.shadowCastingMode);renderer.shadowCastingMode=ShadowCastingMode.ShadowsOnly;}
        }
    }
    void EndViewArmsCamera(ScriptableRenderContext context,Camera camera)
    {
        foreach(var r in viewArmRenderers)if(r!=null)r.enabled=false;
        foreach(var p in viewPossessions.Values)foreach(var r in p.Renderers)if(r!=null)r.enabled=false;
        foreach(var r in viewNockRenderers)if(r!=null)r.enabled=false;
        RestoreWorldHeldRendering();
    }
    void ClearViewNock()
    {if(viewNockedArrow!=null)DestroyInventoryVisual(viewNockedArrow);viewNockedArrow=null;viewNockId=null;viewNockRenderers=Array.Empty<Renderer>();}
    void UpdateViewNock()
    {
        var charge=itemCharge??pendingRanged?.Charge;
        var ammo=charge!=null&&!charge.Throw?charge.Ammo:null;
        if(ammo==null||charge.Weapon==null){ClearViewNock();return;}
        if(viewNockId!=ammo.Id)
        {
            ClearViewNock();viewNockId=ammo.Id;viewNockedArrow=CreatePossessionVisual(ammo,30);viewNockedArrow.name="Private loaded ammunition";viewNockedArrow.transform.SetParent(view.transform,true);
            viewNockRenderers=viewNockedArrow.GetComponentsInChildren<Renderer>();foreach(var r in viewNockRenderers){r.shadowCastingMode=ShadowCastingMode.Off;r.enabled=false;}
        }
        if(!viewPossessions.TryGetValue(charge.Weapon.Id,out var launcher))return;
        if(charge.Weapon.Kind==PossessionKind.Bow)
        {
            viewNockedArrow.transform.position=BowDrawPoint(1);
            var rest=launcher.Root.transform.position+launcher.Root.transform.up*(float)charge.Weapon.HeightM*.55f;
            viewNockedArrow.transform.rotation=Quaternion.FromToRotation(Vector3.up,(rest-viewNockedArrow.transform.position).normalized);
        }
        else if(charge.Weapon.Kind==PossessionKind.SpearThrower)
        {viewNockedArrow.transform.position=launcher.Root.transform.TransformPoint(new Vector3(.25f,.96f,.12f));viewNockedArrow.transform.rotation=launcher.Root.transform.rotation;}
        else
        {viewNockedArrow.transform.rotation=view.transform.rotation;viewNockedArrow.transform.position=SlingPouch(launcher,charge.Weapon)-view.transform.up*(float)ammo.HeightM*.5f;}
    }
    void BeginGatherGesture(Vector3 worldPoint)
    {if(SharedHumanMotion)CancelItemCharge();viewGatherAt=Time.unscaledTimeAsDouble;viewGatherTarget=view.transform.InverseTransformPoint(worldPoint);}
    void BeginConsumeGesture(bool drink)
    {if(SharedHumanMotion)CancelItemCharge();viewConsumeAt=Time.unscaledTimeAsDouble;viewConsumeDrink=drink;}
    bool ViewLaunchCentre(ItemCharge charge,out Vector3 centre)
    {
        if(SharedHumanMotion)
        {
            UpdateSharedHumanAction();FitSharedHeldHands();AimSharedHead();
            if(charge.Throw&&inventoryVisuals.TryGetValue(charge.Ammo.Id,out var held))
            {PoseSharedHeldItem(held,charge.Ammo);centre=held.transform.position+held.transform.up*(float)charge.Ammo.HeightM*.5f;return true;}
            UpdateSharedAmmo();if(sharedLoadedAmmo!=null){centre=sharedLoadedAmmo.transform.position+sharedLoadedAmmo.transform.up*(float)charge.Ammo.HeightM*.5f;return true;}
            centre=default;return false;
        }
        centre=default;if(!humanCamera.FirstPerson||!viewArmsVisible||viewArms==null)return false;
        UpdateViewArms();GameObject source=null;
        if(charge.Throw){if(viewPossessions.TryGetValue(charge.Ammo.Id,out var held))source=held.Root;}
        else source=viewNockedArrow;
        if(source==null)return false;
        centre=source.transform.position+source.transform.up*(float)charge.Ammo.HeightM*.5f;return true;
    }
    struct ViewPose {public string Clip;public double Time,Key;public ViewPose(string clip,double time,double key=0){Clip=clip;Time=time;Key=key;}}
    ViewPose PoseForHand(Possession item,bool left,double now)
    {
        string Side(string role)=>left?role+"_Left":role;
        double Clock(string role)=>SharedHumanMotion&&role=="ToolWalk"?humanPlayer.Shared.GaitPhase*HumanActionDuration(role):now%HumanActionDuration(role);
        ViewPose Phase(string role,double at,double duration)=>new ViewPose(Side(role),Math.Max(0,(now-at)/Math.Max(.1,duration))*HumanActionDuration(role),at);
        if(itemCharge?.Weapon==item&&item!=null)
        {
            string role=itemCharge.Throw?LongViewSpear(item)?"SpearThrowCharge":"ThrowCharge":item.Kind==PossessionKind.Bow?"BowDraw":item.Kind==PossessionKind.SpearThrower?"SpearThrowCharge":"SlingWhirl";
            return new ViewPose(role=="BowDraw"?role:Side(role),role=="SlingWhirl"?(now-itemCharge.Started)%HumanActionDuration(role):Math.Min(1,(now-itemCharge.Started)/itemCharge.Duration)*HumanActionDuration(role),itemCharge.Started);
        }
        if(item!=null&&viewItemActions.TryGetValue(item.Id,out var action)&&!action.Release&&now-action.At<action.Duration)
            return Phase(action.Clip??ViewStrikeClip(item),action.At,action.Duration);
        if(combatGuarding&&item?.Kind!=PossessionKind.Bow)return new ViewPose(Side(item==null?"FistsGuard":"ToolGuard"),10);
        if(item?.Kind==PossessionKind.Bow)return new ViewPose("BowReady",Clock("BowReady"));
        if(item?.Kind==PossessionKind.Sling)return new ViewPose(Side("SlingReady"),Clock("SlingReady"));
        if(item?.Kind==PossessionKind.SpearThrower)return new ViewPose(Side("SpearReady1"),Clock("SpearReady1"));
        if(item?.Hands==2&&!LongViewSpear(item))return new ViewPose("CarryLarge",Clock("CarryLarge"));
        if(LongViewSpear(item)){string role=SupportSpear(item)?"SpearReady2":"SpearReady1";return new ViewPose(Side(role),Clock(role));}
        if(item!=null){string role=huntingSpeed>.4?"ToolWalk":"ToolReady";return new ViewPose(Side(role),Clock(role));}
        return new ViewPose("Empty",Clock("Empty"));
    }
    void UpdateViewArms()
    {
        if(humanPlayer==null||!inventoryInitialized)return;
        bool visible=!SharedHumanMotion&&humanHideBody&&!DeathVisible&&!Spectating&&!inventoryOpen&&!campSleeping&&!campResting;
        if(!visible){viewArmsVisible=false;if(viewArms!=null)viewArms.Root.SetActive(false);return;}
        EnsureViewArms();viewArms.Root.SetActive(true);viewArmsVisible=true;
        var right=Inventory.AtHand(PossessionPlace.RightHand);var left=Inventory.AtHand(PossessionPlace.LeftHand);var main=itemCharge?.Weapon??pendingRanged?.Charge.Weapon??right??left;
        double now=Time.unscaledTimeAsDouble;bool mainLeft=main?.Place==PossessionPlace.LeftHand;
        var primary=PoseForHand(main,mainLeft,now);Possession off=mainLeft?right:left;bool offLeft=!mainLeft;
        bool two=main?.Kind==PossessionKind.Bow||main?.Hands==2||SupportSpear(main);
        if(itemCharge!=null&&!itemCharge.Throw||pendingRanged!=null&&!pendingRanged.Charge.Throw)two=true;
        string releaseClip=viewReleaseKind==ItemPrimaryUse.Bow?"BowRelease":viewReleaseSpear?"SpearThrowRelease":viewReleaseKind==ItemPrimaryUse.Sling?"SlingRelease":"ThrowRelease";
        if(now-viewReleaseAt<viewArms.Duration(releaseClip))
        {
            string role=releaseClip;
            primary=new ViewPose(role+(viewReleaseLeft?"_Left":""),now-viewReleaseAt,viewReleaseAt);
            off=viewReleaseLeft?right:left;offLeft=!viewReleaseLeft;two=viewReleaseKind==ItemPrimaryUse.Bow;
        }
        else if(main==null&&now-viewPunchAt<viewPunchDuration)primary=new ViewPose("Punch",(now-viewPunchAt)/viewPunchDuration*viewArms.Duration("Punch"),viewPunchAt);
        else if(now-viewConsumeAt<viewArms.Duration(viewConsumeDrink?"Drink":"Eat")){primary=new ViewPose(viewConsumeDrink?"Drink":"Eat",now-viewConsumeAt,viewConsumeAt);two=true;}
        else if(now-viewGatherAt<viewArms.Duration("Gather"))
        {
            bool gatherLeft=right!=null&&left==null;
            if(gatherLeft){offLeft=true;off=null;two=false;}
            else if(right==null){primary=new ViewPose("Gather",now-viewGatherAt,viewGatherAt);off=left;offLeft=true;two=false;}
        }
        else if(CraftWorking)
        {
            string work="WorkTwist";var job=ActiveItemCraft;
            if(worldToolWork!=null)work=worldToolWork.Label.StartsWith("Digging",StringComparison.Ordinal)?"WorkDig":"WorkTwist";
            else if(job!=null)
            {
                var capability=PortableTechnology.Capabilities(right);
                work=job.Action==ItemCraftAction.Flake||(capability&(CraftCapability.Strike|CraftCapability.Grind))!=0?"WorkKnapping":(capability&(CraftCapability.Cut|CraftCapability.Scrape|CraftCapability.Pierce))!=0?"WorkCut":"WorkTwist";
            }
            primary=new ViewPose(work,now%viewArms.Duration(work));two=true;
        }
        if(viewArmDiagnosticClip!=null){primary=new ViewPose(viewArmDiagnosticClip,viewArmDiagnosticTime);two=true;}
        float dt=viewArmDiagnosticDt>=0?viewArmDiagnosticDt:viewArmsFrame==Time.frameCount||paused?0:Math.Min(.1f,Time.unscaledDeltaTime);viewArmsFrame=Time.frameCount;
        viewArms.Pose(0,primary.Clip,primary.Time,primary.Key,dt,viewArmPoseImmediate);
        bool gatherOff=right!=null&&left==null&&!two&&now-viewGatherAt<viewArms.Duration("Gather");
        bool overlay=!two&&(off!=null||gatherOff);viewArms.Offhand(overlay,offLeft);
        if(overlay)
        {
            var secondary=gatherOff?new ViewPose("Gather_Left",now-viewGatherAt,viewGatherAt):PoseForHand(off,offLeft,now);
            viewArms.Pose(1,secondary.Clip,secondary.Time,secondary.Key,dt,viewArmPoseImmediate);
        }
        viewArms.Evaluate();PoseViewTravel(dt,main);UpdateViewPossessions(right,left);UpdateViewNock();
    }
    void PoseViewTravel(float dt,Possession main)
    {
        // Move the complete private pose a little with the player's gait.
        // Hands, the supporting grip and loaded ammunition stay together;
        // camera motion and the world avatar are unaffected.
        if(viewArmPoseImmediate||viewArmDiagnosticClip!=null)
        {viewArms.Root.transform.localPosition=Vector3.zero;viewArms.Root.transform.localRotation=Quaternion.identity;viewTravelAmount=0;return;}
        float speed=motion.Grounded&&!motion.Swimming&&!motion.Flying?(float)Math.Min(6,huntingSpeed):0;
        bool busy=itemCharge!=null||pendingRanged!=null||CraftWorking||Time.unscaledTimeAsDouble-viewGatherAt<1.05||Time.unscaledTimeAsDouble-viewConsumeAt<1.2||main!=null&&viewItemActions.TryGetValue(main.Id,out var action)&&Time.unscaledTimeAsDouble-action.At<action.Duration;
        float amount=Mathf.Clamp01(speed/1.6f)*Mathf.Lerp(.65f,1.45f,speed/6)*(busy?.15f:1);
        if(dt>0)
        {
            viewTravelAmount=Mathf.Lerp(viewTravelAmount,amount,1-Mathf.Exp(-dt*12));
            float stride=Mathf.Lerp(1.5f,2.5f,Mathf.InverseLerp(1.5f,6,speed));
            viewTravelPhase=(viewTravelPhase+speed/stride*dt*Math.PI*2)%(Math.PI*2);
        }
        float wave=(float)Math.Sin(viewTravelPhase),doubleWave=(float)Math.Cos(viewTravelPhase*2);
        viewArms.Root.transform.localPosition=new Vector3(wave*.006f,doubleWave*.012f,0)*viewTravelAmount;
        viewArms.Root.transform.localRotation=Quaternion.Euler(doubleWave*.45f*viewTravelAmount,0,wave*.35f*viewTravelAmount);
    }
    void UpdateViewPossessions(Possession right,Possession left)
    {
        viewPossessionsPresent.Clear();
        foreach(var item in new[]{right,left})
        {
            if(Time.unscaledTimeAsDouble-viewConsumeAt<viewArms.Duration(viewConsumeDrink?"Drink":"Eat"))continue;
            var charge=itemCharge??pendingRanged?.Charge;
            if(item==null||charge!=null&&!charge.Throw&&charge.Ammo==item||!viewPossessionsPresent.Add(item.Id))continue;
            if(!viewPossessions.TryGetValue(item.Id,out var visual)||visual.Kind!=item.Kind)
            {
                if(visual!=null)DestroyInventoryVisual(visual.Root);
                var root=CreatePossessionVisual(item,30);root.name="Private held "+item.Kind;root.transform.SetParent(view.transform,true);
                visual=new ViewPossession{Root=root,Kind=item.Kind,Renderers=root.GetComponentsInChildren<Renderer>()};viewPossessions[item.Id]=visual;
                foreach(var renderer in visual.Renderers){renderer.enabled=false;renderer.shadowCastingMode=ShadowCastingMode.Off;}
            }
            bool bow=item.Kind==PossessionKind.Bow;bool workWood=item.Place==PossessionPlace.LeftHand&&(ActiveItemCraft!=null||viewArmDiagnosticClip=="WorkCut")&&(WoodCrafting.Wood(item)||PortableTechnology.Item(item.Kind)?.Long==true);
            bool longItem=LongViewSpear(item)||item.Kind==PossessionKind.SpearThrower||workWood;
            string side=item.Place==PossessionPlace.LeftHand?"L":"R";var socket=viewArms.Bones[(longItem?"SpearGrip.":"Grip.")+(bow?"L":side)];
            visual.Root.transform.rotation=socket.rotation*(bow?Quaternion.Euler(0,90,0):Quaternion.identity);
            float grip=(float)item.HeightM*(bow?.5f:StoneCrafting.Rock(item)?.5f:WoodCrafting.Wood(item)||PortableTechnology.Item(item.Kind)?.Long==true?.35f:.18f);
            visual.Root.transform.position=socket.position-visual.Root.transform.up*grip;
            if(item.Hands==2&&!bow&&!longItem)
            {visual.Root.transform.rotation=view.transform.rotation;visual.Root.transform.position=(viewArms.Bones["Grip.R"].position+viewArms.Bones["Grip.L"].position)*.5f+view.transform.up*.01f;}
            if(bow)UpdateBowString(visual.Root,item,true);
            if(item.Kind==PossessionKind.Sling)PoseViewSling(visual,item);
        }
        foreach(var id in viewPossessions.Keys.Where(id=>!viewPossessionsPresent.Contains(id)).ToArray())
        {DestroyInventoryVisual(viewPossessions[id].Root);viewPossessions.Remove(id);}
    }
    void DisposeViewArms()
    {
        RenderPipelineManager.beginCameraRendering-=BeginViewArmsCamera;RenderPipelineManager.endCameraRendering-=EndViewArmsCamera;
        RestoreWorldHeldRendering();ClearViewNock();foreach(var v in viewPossessions.Values)DestroyInventoryVisual(v.Root);viewPossessions.Clear();
        viewArmsVisible=false;viewArmRenderers.Clear();viewArmMeshes.Clear();viewArms?.Dispose();viewArms=null;
    }
}
