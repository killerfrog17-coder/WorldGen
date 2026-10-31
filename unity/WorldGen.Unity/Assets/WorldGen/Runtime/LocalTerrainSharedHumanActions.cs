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
    int sharedGripFrame=-1;float sharedSpearBlend,sharedBowDraw;string sharedSpearItem,sharedBowItem;
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
        bool two=main?.Kind==PossessionKind.Bow||main?.Hands==2||SupportSpear(main);
        double now=Time.unscaledTimeAsDouble;
        if(itemCharge!=null&&!itemCharge.Throw||pendingRanged!=null&&!pendingRanged.Charge.Throw)two=true;
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
            if(right==null)primary=new ViewPose("Gather",now-viewGatherAt,viewGatherAt);
            else if(left==null){off=null;offLeft=true;two=false;}
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
        bool gatherOff=right!=null&&left==null&&!two&&now-viewGatherAt<HumanActionDuration("Gather");
        string other=null;double otherTime=0;
        if(!two&&(off!=null||gatherOff))
        {
            var pose=gatherOff?new ViewPose("Gather_Left",now-viewGatherAt,viewGatherAt):PoseForHand(off,offLeft,now);
            other=pose.Clip;otherTime=pose.Time;
        }
        float dt=viewArmDiagnosticDt>=0?viewArmDiagnosticDt:sharedActionFrame==Time.frameCount||paused?0:Math.Min(.1f,Time.unscaledDeltaTime);sharedActionFrame=Time.frameCount;
        bool fullBody=primary.Clip.StartsWith("Work",StringComparison.Ordinal)||primary.Clip=="Gather"||primary.Clip=="Eat"||primary.Clip=="Drink"||
            humanPlayer.Shared.Clips.TryGetValue(primary.Clip,out var metadata)&&!metadata.Loop;
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
        var hand=inventoryBones["hand_"+side];
        Vector3 width=inventoryBones["index_01_"+side].position-inventoryBones["pinky_01_"+side].position;
        if(side=="r")width=-width;
        if(width.sqrMagnitude>.000001&&direction.sqrMagnitude>.000001)hand.rotation=Quaternion.Slerp(hand.rotation,Quaternion.FromToRotation(width,direction)*hand.rotation,weight);
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
    void FitSharedSupport(string side,Vector3 target,Vector3? hint=null)
    {
        var body=humanPlayer.Root.transform;var upper=inventoryBones["upperarm_"+side];var lower=inventoryBones["lowerarm_"+side];var hand=inventoryBones["hand_"+side];
        float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,hand.position);
        Vector3 delta=target-upper.position;float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.002f,(a+b)*.98f);
        Vector3 axis=delta.normalized,bend=Vector3.ProjectOnPlane(hint??(body.right*(side=="r"?1:-1)-body.up*.8f),axis).normalized;
        float along=(a*a+distance*distance-b*b)/(2*distance),height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        Vector3 elbow=upper.position+axis*along+bend*height;
        upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,upper.position+axis*distance-lower.position)*lower.rotation;
    }
    void FitSharedBowHands(Possession bow)
    {
        // The motion supplies the torso/shoulders; these world-space constraints
        // fit the existing bow dimensions and the current player's proportions.
        var body=humanPlayer.Root.transform;float scale=humanPlayer.Definition.EyeHeight/1.65f;
        float draw=0;double now=Time.unscaledTimeAsDouble;
        if(itemCharge?.Weapon==bow&&!itemCharge.Throw)draw=Mathf.Clamp01((float)((now-itemCharge.Started)/itemCharge.Duration));
        else if(viewReleaseKind==ItemPrimaryUse.Bow&&now-viewReleaseAt<.3)draw=1-Mathf.Clamp01((float)((now-viewReleaseAt)/.3));
        if(viewArmDiagnosticClip=="BowDraw")draw=Mathf.Clamp01((float)(viewArmDiagnosticTime/HumanActionDuration("BowDraw")));
        if(sharedBowItem!=bow.Id){sharedBowItem=bow.Id;sharedBowDraw=0;}
        sharedBowDraw=Mathf.MoveTowards(sharedBowDraw,draw,SharedGripDelta()/.12f);draw=sharedBowDraw;
        Vector3 forward=SharedSpearDirection,up=Vector3.ProjectOnPlane(Vector3.up,forward).normalized;
        Vector3 anchor=inventoryBones["head"].position+body.right*.14f*scale+forward*.02f*scale;
        Vector3 left=anchor+forward*.55f*scale-up*(float)bow.HeightM*.05f;
        Vector3 right=anchor+forward*(.38f*(1-draw))*scale;
        for(int i=0;i<2;i++)
        {
            FitSharedSupport("l",left-(SharedGripPosition("l")-inventoryBones["hand_l"].position));FitSharedWrist("l",up);
            FitSharedSupport("r",right-(SharedGripPosition("r")-inventoryBones["hand_r"].position),body.right-body.forward*.6f);FitSharedWrist("r",up);
        }
    }
    void FitSharedSpearHands(Possession spear,float weight)
    {
        var body=humanPlayer.Root.transform;float scale=humanPlayer.Definition.EyeHeight/1.65f;
        string main=spear.Place==PossessionPlace.LeftHand?"l":"r",support=main=="r"?"l":"r";
        string role=humanPlayer.Shared.ActionRole;float extension=0,swing=0;
        if(role.StartsWith("SpearThrust",StringComparison.Ordinal)&&humanPlayer.Shared.Clips.TryGetValue(role,out var action))
        {
            float phase=Mathf.Clamp01((float)(humanPlayer.Shared.ActionTime/action.Duration));float contact=Mathf.Clamp(action.Contact,.1f,.9f);
            swing=phase<=contact?Mathf.SmoothStep(0,1,phase/contact):Mathf.SmoothStep(1,0,(phase-contact)/(1-contact));extension=.22f*swing;
        }
        Vector3 axis=SharedSpearDirection;
        float height=Mathf.Lerp(inventoryBones["pelvis"].position.y+.15f*scale,inventoryBones["head"].position.y-.05f*scale,swing);
        Vector3 target=body.position+Vector3.up*(height-body.position.y)+body.right*(main=="r"?.11f:-.11f)*scale+axis*(.15f+extension)*scale;
        target=Vector3.Lerp(SharedGripPosition(main),target,weight);
        for(int i=0;i<2;i++)
        {
            FitSharedSupport(main,target-(SharedGripPosition(main)-inventoryBones["hand_"+main].position));FitSharedWrist(main,axis,weight);
        }
        Vector3 grip=SharedGripPosition(main),shoulder=inventoryBones["upperarm_"+support].position;
        float reach=(Vector3.Distance(shoulder,inventoryBones["lowerarm_"+support].position)+Vector3.Distance(inventoryBones["lowerarm_"+support].position,inventoryBones["hand_"+support].position))*.97f;
        Vector3 from=grip-shoulder;float along=Vector3.Dot(from,axis),perpendicular=Mathf.Max(0,from.sqrMagnitude-along*along);
        float furthest=-along+Mathf.Sqrt(Mathf.Max(0,reach*reach-perpendicular));
        float spacing=Mathf.Clamp(furthest,.12f,.3f*scale);
        Vector3 supportTarget=Vector3.Lerp(SharedGripPosition(support),grip+axis*spacing,weight);
        for(int i=0;i<2;i++)
        {
            FitSharedSupport(support,supportTarget-(SharedGripPosition(support)-inventoryBones["hand_"+support].position));FitSharedWrist(support,axis,weight);
        }
    }
    Vector3 SharedSpearDirection
    {
        get
        {
            if(humanPlayer.Shared.ActionRole=="WorkDig")return Vector3.down;
            float raised=0;
            if(itemCharge?.Throw==true)raised=12*(float)Math.Min(1,(Time.unscaledTimeAsDouble-itemCharge.Started)/itemCharge.Duration);
            else if(pendingRanged?.Charge.Throw==true)
            {double delay=HumanReleaseDelay(pendingRanged.Charge);raised=12*Mathf.Clamp01((float)((pendingRanged.FireAt-Time.unscaledTimeAsDouble)/Math.Max(.01,delay)));}
            return Quaternion.Euler(Mathf.Clamp(pitch-raised,-72,65),heading,0)*Vector3.forward;
        }
    }
    void FitSharedHeldHands()
    {
        var main=itemCharge?.Weapon??pendingRanged?.Charge.Weapon??Inventory.AtHand(PossessionPlace.RightHand)??Inventory.AtHand(PossessionPlace.LeftHand);
        if(main==null){sharedSpearBlend=sharedBowDraw=0;sharedSpearItem=sharedBowItem=null;return;}
        bool longSpear=LongViewSpear(main),longTool=longSpear||main.Kind==PossessionKind.SpearThrower;string side=main.Place==PossessionPlace.LeftHand?"l":"r";
        bool consuming=humanPlayer.Shared.ActionRole=="Drink"||humanPlayer.Shared.ActionRole=="Eat";
        bool gathering=Time.unscaledTimeAsDouble-viewGatherAt<HumanActionDuration("Gather");
        if(longSpear)
        {
            if(sharedSpearItem!=main.Id){sharedSpearItem=main.Id;sharedSpearBlend=0;}
            bool support=SupportSpear(main)&&!gathering&&!consuming&&humanPlayer.Shared.ActionRole!="WorkDig";
            sharedSpearBlend=Mathf.MoveTowards(sharedSpearBlend,support?1:0,SharedGripDelta()/.15f);
            if(sharedSpearBlend>.001f){FitSharedSpearHands(main,sharedSpearBlend);return;}
        }
        else {sharedSpearBlend=0;sharedSpearItem=null;}
        if(consuming)return;
        if(!CraftWorking&&Time.unscaledTimeAsDouble-viewGatherAt>HumanActionDuration("Gather")&&Time.unscaledTimeAsDouble-viewConsumeAt>HumanActionDuration(viewConsumeDrink?"Drink":"Eat"))
        {
            var spine=inventoryBones["spine_02"];spine.rotation=Quaternion.AngleAxis(Mathf.Clamp(pitch,-55,55)*.45f,humanPlayer.Root.transform.right)*spine.rotation;
        }
        if(main.Hands==2&&!longSpear&&main.Kind!=PossessionKind.Bow)return;
        if(main.Kind==PossessionKind.Bow){FitSharedBowHands(main);return;}
        if(longTool||!SharedToolFollowsHand&&main.Kind!=PossessionKind.Sling)FitSharedWrist(side,longTool?SharedSpearDirection:Vector3.up);
        if(longSpear&&SupportSpear(main)&&!gathering)
        {
            string support=side=="r"?"l":"r";
            Vector3 target=SharedGripPosition(side)+SharedSpearDirection*.25f;
            for(int i=0;i<2;i++){FitSharedSupport(support,target-(SharedGripPosition(support)-inventoryBones["hand_"+support].position));FitSharedWrist(support,SharedSpearDirection);}
        }
    }
    void PoseSharedHeldItem(GameObject root,Possession item)
    {
        bool left=item.Place==PossessionPlace.LeftHand;string side=left?"l":"r";
        Vector3 grip=SharedGripPosition(item.Kind==PossessionKind.Bow?"l":side);
        bool longItem=LongViewSpear(item)||item.Kind==PossessionKind.SpearThrower;
        Vector3 axis=longItem?SharedSpearDirection:SharedToolFollowsHand&&item.Kind!=PossessionKind.Bow?SharedGripAxis(side):Vector3.up;
        if(item.Kind==PossessionKind.Bow)
        {
            Vector3 shot=SharedSpearDirection,up=Vector3.ProjectOnPlane(Vector3.up,shot).normalized;
            root.transform.rotation=Quaternion.LookRotation(Vector3.Cross(-shot,up),up);
        }
        else if(longItem)root.transform.rotation=Quaternion.FromToRotation(Vector3.up,axis);
        else if(SharedToolFollowsHand)
        {
            Vector3 fingers=inventoryBones["middle_01_"+side].position-inventoryBones["hand_"+side].position;
            Vector3 normal=Vector3.Cross(fingers,axis).normalized;
            root.transform.rotation=normal.sqrMagnitude>.01f?Quaternion.LookRotation(normal,axis):humanPlayer.Root.transform.rotation;
        }
        else root.transform.rotation=humanPlayer.Root.transform.rotation;
        float amount=StoneCrafting.Rock(item)||item.Kind==PossessionKind.Bow?.5f:WoodCrafting.Wood(item)||PortableTechnology.Item(item.Kind)?.Long==true?.35f:.18f;
        if(item.Hands==2&&!longItem&&item.Kind!=PossessionKind.Bow)grip=(SharedGripPosition("r")+SharedGripPosition("l"))*.5f;
        root.transform.position=grip-root.transform.up*(float)item.HeightM*amount;
        if(item.Kind==PossessionKind.Bow)UpdateBowString(root,item,true);
        if(item.Kind==PossessionKind.Sling)PoseSharedSling(root,item);
    }
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
        else if(charge.Weapon.Kind==PossessionKind.SpearThrower)center=SharedGripPosition(charge.Left?"l":"r")+direction*.25f;
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
        string role=humanPlayer.Shared.ActionRole;
        if(role.StartsWith("Work",StringComparison.Ordinal)||role.StartsWith("Gather",StringComparison.Ordinal)||role=="Eat"||role=="Drink")return;
        float yaw=humanPlayer.Yaw+Mathf.Clamp(Mathf.DeltaAngle(humanPlayer.Yaw,heading),-70,70);
        humanPlayer.Shared.AimHead(transform.rotation*Quaternion.Euler(Mathf.Clamp(pitch,-65,65),yaw,0));
    }
}
