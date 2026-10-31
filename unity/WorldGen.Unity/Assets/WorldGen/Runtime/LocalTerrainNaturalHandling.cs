using System;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class NaturalGripFrame
    {
        public string Id;
        public Vector3 Axis;
        public float Intent;
        public Vector3 SmallGrip;
        public Transform GripOwner;
        public bool HasGrip;
    }
    readonly NaturalGripFrame naturalRight=new NaturalGripFrame(),naturalLeft=new NaturalGripFrame();
    float naturalHandlingDt;
    Vector3 naturalBowHook;bool naturalBowHookValid;
    Vector3 NaturalBowStringGrip=>naturalBowHookValid?inventoryBones["hand_r"].TransformPoint(naturalBowHook):SharedGripPosition("r");
    void CacheNaturalBowHook()
    {
        if(naturalBowHookValid)return;
        Vector3 tip=Vector3.zero;
        foreach(string finger in new[]{"index","middle","ring"})
        {
            var last=inventoryBones[finger+"_03_r"];var previous=inventoryBones[finger+"_02_r"];
            tip+=last.position+(last.position-previous.position)*.65f;
        }
        naturalBowHook=inventoryBones["hand_r"].InverseTransformPoint(tip/3);naturalBowHookValid=true;
    }
    Possession SharedMainHeld=>itemCharge?.Weapon??pendingRanged?.Charge.Weapon??Inventory.AtHand(PossessionPlace.RightHand)??Inventory.AtHand(PossessionPlace.LeftHand);
    float NaturalScale=>humanPlayer.Definition.EyeHeight/1.65f;
    float NaturalBowYaw=>NaturalBow!=null&&!motion.Swimming?55*sharedBowRaise:0;
    Possession NaturalBow
    {
        get
        {
            var main=SharedMainHeld;if(main?.Kind==PossessionKind.Bow)return main;
            var r=Inventory.AtHand(PossessionPlace.RightHand);var l=Inventory.AtHand(PossessionPlace.LeftHand);
            return r?.Kind==PossessionKind.Bow?r:l?.Kind==PossessionKind.Bow?l:null;
        }
    }
    string NaturalSide(Possession item)
    {
        var bow=NaturalBow;
        if(bow!=null)return item?.Id==bow.Id?"l":"r";
        return item?.Place==PossessionPlace.LeftHand?"l":"r";
    }
    bool NaturalActing(Possession item)
    {
        if(item==null)return false;
        if(combatGuarding)return true;
        if(itemCharge?.Weapon==item||pendingRanged?.Charge.Weapon==item)return true;
        if(viewItemActions.TryGetValue(item.Id,out var action)&&Time.unscaledTimeAsDouble-action.At<action.Duration)return true;
        string role=viewArmDiagnosticClip;
        return role!=null&&!role.Contains("Ready")&&role!="ToolWalk"&&role!="CarryLarge"&&role!="Empty"&&role!="Idle"||CraftWorking;
    }
    bool NaturalPole(Possession item)=>LongViewSpear(item)||WoodCrafting.Wood(item)&&item.HeightM>1||item.Kind==PossessionKind.BowStave;
    NaturalGripFrame NaturalFrame(Possession item)=>NaturalSide(item)=="r"?naturalRight:naturalLeft;
    Vector3 NaturalCarryAxis(Possession item)
    {
        var body=humanPlayer.Root.transform;float side=NaturalSide(item)=="r"?1:-1;
        if(motion.Swimming)return body.forward;
        if(item.Kind==PossessionKind.Bow)return body.forward;
        if(NaturalPole(item))return (body.forward*.76f-body.right*(SupportSpear(item)?.65f:.16f)*side+Vector3.up*.06f).normalized;
        if(item.Hands==2)return Vector3.up;
        return (body.forward*.32f-Vector3.up*.94f+body.right*.06f*side).normalized;
    }
    Vector3 NaturalLookAxis
    {
        get
        {
            float raised=0;
            if(itemCharge?.Throw==true)raised=12*Mathf.Clamp01((float)((Time.unscaledTimeAsDouble-itemCharge.Started)/itemCharge.Duration));
            else if(pendingRanged?.Charge.Throw==true)
                raised=12*Mathf.Clamp01((float)((pendingRanged.FireAt-Time.unscaledTimeAsDouble)/Math.Max(.01,HumanReleaseDelay(pendingRanged.Charge))));
            return transform.rotation*Quaternion.Euler(Mathf.Clamp(pitch-raised,-65,65),heading,0)*Vector3.forward;
        }
    }
    void UpdateNaturalFrame(Possession item)
    {
        if(item==null)return;var state=NaturalFrame(item);var carry=NaturalCarryAxis(item);
        if(state.Id!=item.Id){state.Id=item.Id;state.Axis=carry;state.Intent=0;state.HasGrip=false;}
        bool active=NaturalActing(item);state.Intent=Mathf.MoveTowards(state.Intent,active?1:0,naturalHandlingDt/.2f);
        Vector3 target;
        if(active&&humanPlayer.Shared.ActionRole=="WorkDig")target=Vector3.down;
        else if(NaturalPole(item)||item.Kind==PossessionKind.Bow||item.Kind==PossessionKind.SpearThrower)target=Vector3.Slerp(carry,NaturalLookAxis,Mathf.SmoothStep(0,1,state.Intent));
        else target=Vector3.Slerp(carry,humanPlayer.Shared.ActionRole.StartsWith("ToolCutB",StringComparison.Ordinal)?NaturalLookAxis:SharedGripAxis(NaturalSide(item)),Mathf.SmoothStep(0,1,state.Intent));
        // A weapon follows its carrier, not the camera, during relaxed carrying.
        state.Axis=target.normalized;
    }
    Vector3 NaturalWeaponDirection(Possession item)
    {
        if(item==null||humanPlayer==null)return transform.forward;
        var state=NaturalFrame(item);return state.Id==item.Id?state.Axis:NaturalCarryAxis(item);
    }
    float NaturalReach(string side)
        =>(Vector3.Distance(inventoryBones["upperarm_"+side].position,inventoryBones["lowerarm_"+side].position)+Vector3.Distance(inventoryBones["lowerarm_"+side].position,inventoryBones["hand_"+side].position))*.98f;
    float NaturalBodyClearance(Vector3 a,Vector3 b,float radius)
    {
        float s=NaturalScale;
        float chest=humanPlayer.PropShape?.Torso??.20f*s;
        float hips=humanPlayer.PropShape?.Hips??.18f*s;
        float result=WorldGenPropClearance.Capsule(a,b,radius,inventoryBones["pelvis"].position,inventoryBones["spine_01"].position,hips);
        result=Mathf.Min(result,WorldGenPropClearance.Capsule(a,b,radius,inventoryBones["spine_01"].position,inventoryBones["spine_03"].position,chest));
        result=Mathf.Min(result,WorldGenPropClearance.Capsule(a,b,radius,inventoryBones["neck_01"].position,inventoryBones["head"].position,humanPlayer.PropShape?.Head??.10f*s));
        result=Mathf.Min(result,WorldGenPropClearance.Capsule(a,b,radius,inventoryBones["thigh_l"].position,inventoryBones["calf_l"].position,.08f*s));
        result=Mathf.Min(result,WorldGenPropClearance.Capsule(a,b,radius,inventoryBones["thigh_r"].position,inventoryBones["calf_r"].position,.08f*s));
        return result;
    }
    float NaturalPoleClearance(Possession item,Vector3 grip,Vector3 axis)
        =>NaturalBodyClearance(grip-axis*(float)item.HeightM*.35f,grip+axis*(float)item.HeightM*.65f,Mathf.Max(.012f*NaturalScale,(float)item.WidthM*.2f));
    void OrientNaturalWrist(string side,Vector3 shaft,float weight=1)
    {
        if(shaft.sqrMagnitude<1e-6)return;shaft.Normalize();
        var hand=inventoryBones["hand_"+side];var lower=inventoryBones["lowerarm_"+side];
        Vector3 width=inventoryBones["index_01_"+side].position-inventoryBones["pinky_01_"+side].position;if(side=="r")width=-width;
        if(width.sqrMagnitude<1e-7)return;
        var initial=hand.rotation;hand.rotation=Quaternion.FromToRotation(width,shaft)*initial;
        Vector3 fingers=Vector3.ProjectOnPlane(inventoryBones["middle_01_"+side].position-hand.position,shaft);
        Vector3 continuation=Vector3.ProjectOnPlane(hand.position-lower.position,shaft);
        if(fingers.sqrMagnitude>1e-7&&continuation.sqrMagnitude>1e-7)
        {
            float roll=Vector3.SignedAngle(fingers,continuation,shaft);
            hand.rotation=Quaternion.AngleAxis(roll,shaft)*hand.rotation;
        }
        hand.rotation=Quaternion.Slerp(initial,hand.rotation,weight);
    }
    void NaturalGrip(string side,Vector3 target,Vector3 axis,Vector3? elbow=null,float weight=1,float extension=.98f,bool stringGrip=false)
    {
        Vector3 Grip()=>stringGrip?NaturalBowStringGrip:SharedGripPosition(side);
        target=Vector3.Lerp(Grip(),target,weight);
        for(int i=0;i<3;i++)
        {
            FitSharedSupport(side,target-(Grip()-inventoryBones["hand_"+side].position),elbow,extension);
            OrientNaturalWrist(side,axis,weight);
        }
    }
    void FitNaturalPole(Possession item,bool two)
    {
        var body=humanPlayer.Root.transform;float s=NaturalScale;string main=NaturalSide(item),support=main=="r"?"l":"r";float side=main=="r"?1:-1;
        var state=NaturalFrame(item);Vector3 axis=NaturalWeaponDirection(item);
        string role=humanPlayer.Shared.ActionRole;float extension=0,attack=0;
        if(role.StartsWith("SpearThrust",StringComparison.Ordinal)&&NaturalActing(item)&&humanPlayer.Shared.Clips.TryGetValue(role,out var clip))
        {
            float phase=Mathf.Clamp01((float)(humanPlayer.Shared.ActionTime/clip.Duration)),contact=Mathf.Clamp(clip.Contact,.15f,.8f);
            attack=phase<contact?Mathf.SmoothStep(0,1,phase/contact):Mathf.SmoothStep(1,0,(phase-contact)/(1-contact));extension=.22f*attack;
        }
        bool throwing=itemCharge?.Weapon==item&&itemCharge.Throw||pendingRanged?.Charge.Weapon==item&&pendingRanged.Charge.Throw||role.StartsWith("SpearThrow",StringComparison.Ordinal)||role.StartsWith("Throw",StringComparison.Ordinal);
        if(throwing)
        {
            var raw=SharedGripPosition(main);Vector3 safe=raw;
            for(int i=0;i<8&&NaturalPoleClearance(item,safe,axis)<.025f*s;i++)safe+=body.right*(side*.025f*s);
            NaturalGrip(main,safe,axis,weight:Mathf.Clamp01(state.Intent));return;
        }
        float resting=inventoryBones["pelvis"].position.y+(two?.18f:.14f)*s;
        float active=(inventoryBones["upperarm_l"].position.y+inventoryBones["upperarm_r"].position.y)*.5f-.06f*s;
        float height=Mathf.Lerp(resting,active,state.Intent);
        float forward=two?Mathf.Lerp(.16f,-.08f,state.Intent)+extension:.13f+extension;
        Vector3 target=body.position+Vector3.up*(height-body.position.y)+body.right*((two?.28f:.34f)*side*s)+body.forward*(forward*s);
        for(int i=0;i<6&&NaturalPoleClearance(item,target,axis)<.025f*s;i++)target+=body.right*(side*.02f*s);
        if(two)
        {
            float available=WorldGenPropClearance.FurthestReach(target,axis,inventoryBones["upperarm_"+support].position,NaturalReach(support)-.025f*s);
            // Shorten the extension before stretching the supporting arm.
            if(available<.28f*s&&extension>0){target-=body.forward*Mathf.Min(extension*s,.28f*s-available);}
        }
        NaturalGrip(main,target,axis);
        if(two)
        {
            Vector3 rear=SharedGripPosition(main);
            float reach=WorldGenPropClearance.FurthestReach(rear,axis,inventoryBones["upperarm_"+support].position,NaturalReach(support)-.015f*s);
            float spacing=Mathf.Clamp(reach,.24f*s,.4f*s);
            NaturalGrip(support,rear+axis*spacing,axis);
        }
    }
    void FitNaturalSmallItem(Possession item)
    {
        string side=NaturalSide(item);float sign=side=="r"?1:-1,s=NaturalScale;var body=humanPlayer.Root.transform;
        Vector3 Ease(Vector3 point)
        {
            var state=NaturalFrame(item);var local=body.InverseTransformPoint(point);
            if(!state.HasGrip||state.GripOwner!=body){state.SmallGrip=local;state.GripOwner=body;state.HasGrip=true;}
            else state.SmallGrip=Vector3.Lerp(state.SmallGrip,local,1-Mathf.Exp(-naturalHandlingDt*25));
            return body.TransformPoint(state.SmallGrip);
        }
        if(NaturalActing(item))
        {
            Vector3 axis=NaturalWeaponDirection(item),raw=SharedGripPosition(side),safe=Ease(raw);
            float length=(float)item.HeightM,radius=Mathf.Clamp((float)item.WidthM*.18f,.012f*s,.07f*s);
            float Clearance(Vector3 grip)=>NaturalBodyClearance(grip-axis*length*.18f,grip+axis*length*.82f,radius);
            // Correct local self-intersections without replacing the generated
            // strike trajectory. Bound the adjustment by anatomical arm reach.
            for(int i=0;i<7&&Clearance(safe)<.012f*s;i++)
            {
                Vector3 outward=safe+body.right*(sign*.022f*s),front=safe+body.forward*.022f*s;
                safe=Clearance(outward)>Clearance(front)?outward:front;
            }
            NaturalGrip(side,safe,axis);return;
        }
        float outside=.29f*s+Mathf.Min(.1f,(float)item.WidthM*.3f);
        Vector3 target=body.position+body.right*(outside*sign)+body.forward*.14f*s+Vector3.up*(inventoryBones["pelvis"].position.y-body.position.y+.15f*s);
        NaturalGrip(side,Ease(target),NaturalWeaponDirection(item));
    }
    void FitNaturalLarge(Possession item,bool swimming=false)
    {
        var body=humanPlayer.Root.transform;float s=NaturalScale;
        float half=Mathf.Clamp((float)item.WidthM*.35f,.11f*s,.27f*s);
        Vector3 center=swimming?inventoryBones["head"].position+body.forward*.28f*s-Vector3.up*.12f*s:
            body.position+body.forward*(.30f*s+Mathf.Min(.1f,(float)item.DepthM*.25f))+Vector3.up*(inventoryBones["pelvis"].position.y-body.position.y+.15f*s);
        NaturalGrip("r",center+body.right*half,body.right,body.right-body.up*.6f);
        NaturalGrip("l",center-body.right*half,body.right,-body.right-body.up*.6f);
    }
    void FitNaturalBow(Possession bow)
    {
        var body=humanPlayer.Root.transform;float s=NaturalScale;double now=Time.unscaledTimeAsDouble;
        float progress=0;bool releasing=viewReleaseKind==ItemPrimaryUse.Bow&&now-viewReleaseAt<.65;float releaseAge=(float)(now-viewReleaseAt);
        if(viewArmDiagnosticClip=="BowRelease"){releasing=true;releaseAge=(float)viewArmDiagnosticTime;}
        if(itemCharge?.Weapon==bow&&!itemCharge.Throw)progress=Mathf.Clamp01((float)((now-itemCharge.Started)/itemCharge.Duration));
        else if(releasing)progress=1;
        if(viewArmDiagnosticClip=="BowDraw")progress=Mathf.Clamp01((float)(viewArmDiagnosticTime/HumanActionDuration("BowDraw")));
        if(sharedBowItem!=bow.Id){sharedBowItem=bow.Id;sharedBowDraw=sharedBowRaise=0;naturalBowHookValid=false;}
        float raised=releasing?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.6f,releaseAge)):Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/.3f));
        float draw=releasing?1-Mathf.SmoothStep(0,1,Mathf.Clamp01(releaseAge/.06f)):Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,1,progress));
        sharedBowRaise=Mathf.MoveTowards(sharedBowRaise,raised,naturalHandlingDt/.2f);sharedBowDraw=Mathf.MoveTowards(sharedBowDraw,draw,naturalHandlingDt/.15f);
        Vector3 forward=NaturalWeaponDirection(bow),up=Vector3.ProjectOnPlane(Vector3.up,forward).normalized;
        if(sharedBowRaise>.01f&&!motion.Swimming)
        {
            Vector3 shoulders=Vector3.ProjectOnPlane(inventoryBones["upperarm_r"].position-inventoryBones["upperarm_l"].position,Vector3.up).normalized;
            Vector3 desired=Quaternion.AngleAxis(15*sharedBowRaise,Vector3.up)*body.right;
            float yaw=Vector3.SignedAngle(shoulders,desired,Vector3.up);
            var spine=inventoryBones["spine_01"];spine.rotation=Quaternion.AngleAxis(yaw*sharedBowRaise,Vector3.up)*spine.rotation;
        }
        AimSharedHead();
        Vector3 eye=body.TransformPoint(humanPlayer.Shared.LocalEye);
        Vector3 aimRight=Vector3.Cross(up,forward).normalized;
        Vector3 anchor=humanPlayer.PropShape?.HasBowAnchor==true?inventoryBones["head"].TransformPoint(humanPlayer.PropShape.BowAnchorInHead):eye+aimRight*.045f*s-Vector3.up*.07f*s+forward*.02f*s;
        Vector3 rayBase=anchor-up*(float)bow.HeightM*.05f;
        float available=WorldGenPropClearance.FurthestReach(rayBase,forward,inventoryBones["upperarm_l"].position,NaturalReach("l")+.08f*s);
        float length=Mathf.Max(.20f*s,Mathf.Min(available,.72f*s));
        Vector3 aimLeft=rayBase+forward*length;
        Vector3 relaxed=body.position-body.right*.32f*s+body.forward*.18f*s+Vector3.up*(inventoryBones["pelvis"].position.y-body.position.y+.19f*s);
        Vector3 carryUp=(Vector3.up+body.forward*.35f-body.right*.08f).normalized;
        Vector3 bowUp=Vector3.Slerp(carryUp,up,sharedBowRaise).normalized;
        NaturalGrip("l",Vector3.Lerp(relaxed,aimLeft,sharedBowRaise),bowUp,extension:Mathf.Lerp(.98f,.993f,sharedBowRaise));
        if(sharedBowRaise>.001f)
        {
            CacheNaturalBowHook();
            // String starts at the real brace, ends at a cheek anchor. The bow
            // arm is limited by its measured reach rather than an assumed .55m.
            Vector3 grip=SharedGripPosition("l");Vector3 brace=grip+up*(float)bow.HeightM*.05f-forward*(float)bow.WidthM*.38f;
            Vector3 right=Vector3.Lerp(brace,anchor,sharedBowDraw);
            if(releasing)
            {
                Vector3 follow=anchor-forward*(.045f*s*Mathf.Sin(Mathf.Clamp01(releaseAge/.3f)*Mathf.PI)) + body.right*.025f*s;
                Vector3 rest=body.position+body.right*.29f*s+body.forward*.14f*s+Vector3.up*(inventoryBones["pelvis"].position.y-body.position.y+.15f*s);
                right=Vector3.Lerp(rest,follow,sharedBowRaise);
            }
            NaturalGrip("r",right,-up,aimRight-forward*.55f+Vector3.up*.1f,sharedBowRaise,stringGrip:true);
        }
    }
    void FitNaturalSwimming(Possession main,Possession other)
    {
        var body=humanPlayer.Root.transform;float s=NaturalScale;
        if(main.Kind==PossessionKind.Bow&&NaturalActing(main)){FitNaturalBow(main);return;}
        if(main.Hands==2&&!NaturalPole(main)&&main.Kind!=PossessionKind.Bow){FitNaturalLarge(main,true);return;}
        for(int i=0;i<2;i++)
        {
            var item=i==0?main:other;
            if(item==null)continue;string side=NaturalSide(item);float sign=side=="r"?1:-1;
            Vector3 target=inventoryBones["head"].position+body.right*(sign*.42f*s)+body.forward*.10f*s-Vector3.up*.10f*s;
            Vector3 axis=item.Kind==PossessionKind.Bow?(Vector3.up+body.forward*.35f).normalized:NaturalWeaponDirection(item);
            NaturalGrip(side,target,axis,body.forward+body.right*(sign*.25f)-Vector3.up*.1f);
        }
    }
    void FitNaturalHeldHands()
    {
        naturalHandlingDt=SharedGripDelta();var main=SharedMainHeld;
        if(main==null){sharedBowDraw=sharedBowRaise=0;sharedBowItem=null;return;}
        var other=Inventory.AtHand(main.Place==PossessionPlace.LeftHand?PossessionPlace.RightHand:PossessionPlace.LeftHand);
        UpdateNaturalFrame(main);UpdateNaturalFrame(other);
        if(humanPlayer.Shared.ActionRole=="Eat"||humanPlayer.Shared.ActionRole=="Drink")return;
        if(motion.Swimming){FitNaturalSwimming(main,other);return;}
        bool gather=Time.unscaledTimeAsDouble-viewGatherAt<HumanActionDuration("Gather");
        if(main.Kind==PossessionKind.Bow)FitNaturalBow(main);
        else if(NaturalPole(main))FitNaturalPole(main,SupportSpear(main)&&!gather);
        else if(main.Hands<2)FitNaturalSmallItem(main);
        else FitNaturalLarge(main);
        var charge=itemCharge??pendingRanged?.Charge;
        if(other!=null&&charge?.Ammo!=other&&!(main.Kind==PossessionKind.Bow&&sharedBowRaise>.001f))
        {
            if(other.Kind==PossessionKind.Bow)FitNaturalBow(other);
            else if(NaturalPole(other))FitNaturalPole(other,false);
            else FitNaturalSmallItem(other);
        }
    }
    void PoseNaturalHeldItem(GameObject root,Possession item)
    {
        string side=NaturalSide(item);Vector3 grip=SharedGripPosition(side),axis=NaturalWeaponDirection(item);bool bow=item.Kind==PossessionKind.Bow;
        if(bow)
        {
            Vector3 carryUp=(Vector3.up+humanPlayer.Root.transform.forward*.35f-humanPlayer.Root.transform.right*.08f).normalized;
            Vector3 up=Vector3.Slerp(carryUp,Vector3.ProjectOnPlane(Vector3.up,axis).normalized,sharedBowRaise).normalized;
            root.transform.rotation=Quaternion.LookRotation(Vector3.Cross(-axis,up),up);
        }
        else
        {
            var fingers=inventoryBones["middle_01_"+side].position-inventoryBones["hand_"+side].position;
            Vector3 normal=Vector3.Cross(fingers,axis).normalized;
            root.transform.rotation=normal.sqrMagnitude>.001f?Quaternion.LookRotation(normal,axis):Quaternion.FromToRotation(Vector3.up,axis);
        }
        float fraction=StoneCrafting.Rock(item)||bow?.5f:WoodCrafting.Wood(item)||PortableTechnology.Item(item.Kind)?.Long==true?.35f:.18f;
        if(item.Hands==2&&!NaturalPole(item)&&!bow){grip=(SharedGripPosition("r")+SharedGripPosition("l"))*.5f;fraction=.08f;}
        root.transform.position=grip-root.transform.up*(float)item.HeightM*fraction;
        if(bow)UpdateBowString(root,item,true);
        if(item.Kind==PossessionKind.Sling)PoseSharedSling(root,item);
    }
}
