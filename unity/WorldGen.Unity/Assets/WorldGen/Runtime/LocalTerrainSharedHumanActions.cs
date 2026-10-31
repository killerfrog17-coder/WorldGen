using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    GameObject sharedLoadedAmmo;
    string sharedAmmoId;
    int sharedActionFrame=-1;
    int sharedGripFrame=-1;float sharedBowDraw,sharedBowRaise;string sharedBowItem;
    readonly Dictionary<string,ViewPossession> sharedSlings=new Dictionary<string,ViewPossession>();
    double HumanActionDuration(string role)=>SharedHumanMotion?humanPlayer.Shared.Duration(role):viewArms?.Duration(role)??1;
    double HumanReleaseDelay(ItemCharge charge)
    {
        if(!SharedHumanMotion)return .62*.24;
        string role=charge.Throw?(LongViewSpear(charge.Weapon)?"SpearThrowRelease":"ThrowRelease"):
            charge.Weapon.Kind==PossessionKind.Bow?"BowRelease":charge.Weapon.Kind==PossessionKind.Sling?"SlingRelease":"SpearThrowRelease";
        return humanPlayer.Shared.Clips.TryGetValue(role,out var clip)&&clip.Release>=0?clip.Release:.62*.24;
    }
    void UpdateSharedHumanAction()
    {
        if(!SharedHumanMotion||DeathVisible||humanPlayer==null)return;
        var right=Inventory.AtHand(PossessionPlace.RightHand);var left=Inventory.AtHand(PossessionPlace.LeftHand);
        var main=itemCharge?.Weapon??pendingRanged?.Charge.Weapon??right??left;
        bool mainLeft=main?.Place==PossessionPlace.LeftHand;
        var primary=PoseForHand(main,mainLeft,Time.unscaledTimeAsDouble);
        Possession off=mainLeft?right:left;bool offLeft=!mainLeft;
        bool two=main?.Hands==2||SupportSpear(main);
        double now=Time.unscaledTimeAsDouble;
        if(itemCharge!=null&&!itemCharge.Throw||pendingRanged!=null&&!pendingRanged.Charge.Throw)two=true;
        if(main?.Kind==PossessionKind.Bow&&!two)offLeft=false;
        string release=viewReleaseKind==ItemPrimaryUse.Bow?"BowRelease":viewReleaseSpear?"SpearThrowRelease":viewReleaseKind==ItemPrimaryUse.Sling?"SlingRelease":"ThrowRelease";
        if(now-viewReleaseAt<HumanActionDuration(release))
        {
            primary=new ViewPose(release+(viewReleaseLeft?"_Left":""),now-viewReleaseAt,viewReleaseAt);
            two=viewReleaseKind==ItemPrimaryUse.Bow;
        }
        else if(main==null&&now-viewPunchAt<viewPunchDuration)primary=new ViewPose("Punch",(now-viewPunchAt)/viewPunchDuration*HumanActionDuration("Punch"),viewPunchAt);
        else if(now-viewConsumeAt<HumanActionDuration(viewConsumeDrink?"Drink":"Eat"))
        {primary=new ViewPose(viewConsumeDrink?"Drink":"Eat",now-viewConsumeAt,viewConsumeAt);two=true;}
        else if(now-viewGatherAt<HumanActionDuration("Gather"))
        {
            if(main==null)primary=new ViewPose("Gather",now-viewGatherAt,viewGatherAt);
            else if(off==null)
            {
                // Let the free physical hand pick up, including when a bow is
                // logically in the right slot but carried in the left hand.
                string occupied=NaturalSide(main),free=occupied=="r"?"l":"r";
                primary=new ViewPose(free=="l"?"Gather_Left":"Gather",now-viewGatherAt,viewGatherAt);
                off=main;offLeft=occupied=="l";two=false;
            }
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
            primary=new ViewPose(work,now%HumanActionDuration(work));two=true;
        }
        if(viewArmDiagnosticClip!=null){primary=new ViewPose(viewArmDiagnosticClip,viewArmDiagnosticTime);two=true;}
        string other=null;double otherTime=0;
        if(!two&&off!=null)
        {
            var pose=PoseForHand(off,offLeft,now);
            other=pose.Clip;otherTime=pose.Time;
        }
        float dt=viewArmDiagnosticDt>=0?viewArmDiagnosticDt:sharedActionFrame==Time.frameCount||paused?0:Math.Min(.1f,Time.unscaledDeltaTime);sharedActionFrame=Time.frameCount;
        // Ordinary strikes/drawing a bow must not import a generated squat.
        // Legs remain in the actual grounded locomotion pose for those actions.
        bool fullBody=primary.Clip=="Gather"||primary.Clip=="Gather_Left"||primary.Clip=="WorkDig"||
            primary.Clip.StartsWith("Throw",StringComparison.Ordinal)||primary.Clip.StartsWith("SpearThrow",StringComparison.Ordinal);
        if(motion.Swimming)
        {
            fullBody=false;
            if(!SharedDeliberateAim&&now-viewConsumeAt>HumanActionDuration(viewConsumeDrink?"Drink":"Eat"))
            {primary=new ViewPose("Empty",0);other=null;}
        }
        humanPlayer.Shared.Action(primary.Clip,primary.Time,primary.Clip!="Empty",fullBody,other,otherTime,offLeft,dt);
        humanPlayer.Graph.Evaluate(0);
    }
    Vector3 SharedGripPosition(string side)
    {
        var hand=inventoryBones["hand_"+side];var middle=inventoryBones["middle_01_"+side];
        return Vector3.Lerp(hand.position,middle.position,.7f);
    }
    float SharedGripDelta()
    {
        if(viewArmDiagnosticDt>=0)return viewArmDiagnosticDt;
        if(paused||sharedGripFrame==Time.frameCount)return 0;
        sharedGripFrame=Time.frameCount;return Math.Min(.1f,Time.unscaledDeltaTime);
    }
    void FitSharedWrist(string side,Vector3 direction,float weight=1)
    {
        OrientNaturalWrist(side,direction,weight);
    }

    Vector3 SharedGripAxis(string side)
    {
        Vector3 axis=inventoryBones["index_01_"+side].position-inventoryBones["pinky_01_"+side].position;
        return (side=="r"?-axis:axis).normalized;
    }
    bool SharedToolFollowsHand
    {
        get
        {
            string role=humanPlayer.Shared.ActionRole;
            return role.StartsWith("ToolCut",StringComparison.Ordinal)||role.StartsWith("ToolBlunt",StringComparison.Ordinal)||role.StartsWith("ToolChop",StringComparison.Ordinal)||role.StartsWith("Work",StringComparison.Ordinal);
        }
    }
    void FitSharedSupport(string side,Vector3 target,Vector3? hint=null,float extension=.98f)
    {
        var body=humanPlayer.Root.transform;var upper=inventoryBones["upperarm_"+side];var lower=inventoryBones["lowerarm_"+side];var hand=inventoryBones["hand_"+side];
        float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,hand.position);
        Vector3 delta=target-upper.position;float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.002f,(a+b)*extension);
        Vector3 axis=delta.normalized,bend=Vector3.ProjectOnPlane(hint??(body.right*(side=="r"?1:-1)-body.up*.8f),axis).normalized;
        float along=(a*a+distance*distance-b*b)/(2*distance),height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        Vector3 elbow=upper.position+axis*along+bend*height;
        upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,upper.position+axis*distance-lower.position)*lower.rotation;
    }
    bool SharedDeliberateAim=>itemCharge!=null||pendingRanged!=null||viewItemActions.Count>0||Time.unscaledTimeAsDouble-viewPunchAt<viewPunchDuration;
    Vector3 SharedSpearDirection=>NaturalWeaponDirection(SharedMainHeld);
    void FitSharedHeldHands()=>FitNaturalHeldHands();
    void PoseSharedHeldItem(GameObject root,Possession item)=>PoseNaturalHeldItem(root,item);
    void ClearSharedAmmo()
    {if(sharedLoadedAmmo!=null)DestroyInventoryVisual(sharedLoadedAmmo);sharedLoadedAmmo=null;sharedAmmoId=null;}
    void UpdateSharedAmmo()
    {
        var charge=itemCharge??pendingRanged?.Charge;var ammo=charge!=null&&!charge.Throw?charge.Ammo:null;
        if(!SharedHumanMotion||ammo==null||DeathVisible||Spectating){ClearSharedAmmo();return;}
        if(sharedAmmoId!=ammo.Id)
        {
            ClearSharedAmmo();sharedAmmoId=ammo.Id;sharedLoadedAmmo=CreatePossessionVisual(ammo,30);sharedLoadedAmmo.name="Shared loaded ammunition";sharedLoadedAmmo.transform.SetParent(transform,true);
        }
        Vector3 center=SharedGripPosition("r"),direction=SharedSpearDirection;
        if(charge.Weapon.Kind==PossessionKind.Bow&&inventoryVisuals.TryGetValue(charge.Weapon.Id,out var bow))
        {Vector3 rest=bow.transform.position+bow.transform.up*(float)charge.Weapon.HeightM*.55f;direction=(rest-center).normalized;}
        else if(charge.Weapon.Kind==PossessionKind.SpearThrower&&inventoryVisuals.TryGetValue(charge.Weapon.Id,out var thrower))center=thrower.transform.TransformPoint(new Vector3(.4f,1,.2f));
        else if(charge.Weapon.Kind==PossessionKind.Sling)center=SharedSlingPouch(charge.Weapon);
        sharedLoadedAmmo.transform.rotation=Quaternion.FromToRotation(Vector3.up,direction);
        sharedLoadedAmmo.transform.position=center-direction*(float)ammo.HeightM*.5f;
    }
    Vector3 SharedSlingPouch(Possession item)
    {
        var charge=itemCharge??pendingRanged?.Charge;Vector3 grip=SharedGripPosition(item.Place==PossessionPlace.LeftHand?"l":"r");
        float length=Mathf.Clamp((float)item.WidthM*3.7f,.4f,.7f);
        if(charge?.Weapon!=item||charge.Throw)return grip-Vector3.up*length;
        float phase=(float)(Time.unscaledTimeAsDouble-charge.Started)*Mathf.PI*2*1.7f;
        return grip+(humanPlayer.Root.transform.forward*Mathf.Sin(phase)+Vector3.up*Mathf.Cos(phase))*length;
    }
    void PoseSharedSling(GameObject root,Possession item)
    {
        if(!sharedSlings.TryGetValue(item.Id,out var cache)||cache.Root!=root)
        {
            var filter=root.GetComponentInChildren<MeshFilter>();if(filter==null)return;
            cache=new ViewPossession{Root=root,SlingMesh=filter.sharedMesh,SlingRest=filter.sharedMesh.vertices,SlingColors=filter.sharedMesh.colors};
            cache.SlingVertices=new Vector3[cache.SlingRest.Length];sharedSlings[item.Id]=cache;
        }
        Vector3 grip=SharedGripPosition(item.Place==PossessionPlace.LeftHand?"l":"r"),pouch=SharedSlingPouch(item),along=(pouch-grip).normalized,width=humanPlayer.Root.transform.right,normal=Vector3.Cross(width,along).normalized;
        for(int i=0;i<cache.SlingRest.Length;i++)
        {
            Vector3 p=cache.SlingRest[i],world;
            if(cache.SlingColors[i].g<.4f)world=pouch+width*p.x*.13f+along*(p.y-.525f)*.34f+normal*p.z*.08f;
            else
            {
                bool upper=p.x<0;Vector3 a=new Vector3(upper?-.3f:.3f,.5f,0),b=new Vector3(upper?-.48f:.48f,upper?1:0,0),axis=b-a;
                float t=Mathf.Clamp01(Vector3.Dot(p-a,axis)/axis.sqrMagnitude);Vector3 radial=p-Vector3.Lerp(a,b,t);
                world=Vector3.Lerp(pouch+width*(upper?-.035f:.035f),grip+width*(upper?-.006f:.006f),t)+width*radial.x*.04f+normal*radial.z*.06f;
            }
            cache.SlingVertices[i]=root.transform.InverseTransformPoint(world);
        }
        cache.SlingMesh.vertices=cache.SlingVertices;cache.SlingMesh.RecalculateNormals();cache.SlingMesh.RecalculateBounds();
    }
    void RestoreSharedSlings()
    {
        foreach(var c in sharedSlings.Values)if(c.Root!=null&&c.SlingMesh!=null){c.SlingMesh.vertices=c.SlingRest;c.SlingMesh.RecalculateNormals();c.SlingMesh.RecalculateBounds();}
        sharedSlings.Clear();
    }
    void AimSharedHead()
    {
        if(!SharedHumanMotion||DeathVisible||motion.Swimming)return;
        if(!humanCamera.FirstPerson&&!SharedDeliberateAim&&!NaturalActing(SharedMainHeld))return;
        string role=humanPlayer.Shared.ActionRole;
        if(role.StartsWith("Work",StringComparison.Ordinal)||role.StartsWith("Gather",StringComparison.Ordinal)||role=="Eat"||role=="Drink")return;
        float yaw=humanPlayer.Yaw+Mathf.Clamp(Mathf.DeltaAngle(humanPlayer.Yaw,heading),-70,70);
        humanPlayer.Shared.AimHead(transform.rotation*Quaternion.Euler(Mathf.Clamp(pitch,-65,65),yaw,0));
    }
}
