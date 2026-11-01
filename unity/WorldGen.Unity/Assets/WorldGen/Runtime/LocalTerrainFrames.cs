using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class FrameAnchor
    {
        public CampObject Parent;public double T;public V Offset,Ground;public FrameJointKind Kind;
        public FrameAnchor Copy()=>(FrameAnchor)MemberwiseClone();
    }
    FrameAnchor frameFirst,frameHover;CampObject frameDraft;
    float campPlacementYaw,framePlacementRoll;
    bool frameDirty=true;FrameAnalysis frameAnalysis;
    readonly Dictionary<string,double> frameGroundHeights=new Dictionary<string,double>();
    Material frameBindingMaterial;
    static void FrameBasis(V at,out V east,out V north)
    {east=V.Cross(Math.Abs(at.Z)<.99?new V(0,0,1):new V(0,1,0),at).Unit;north=V.Cross(at,east).Unit;}
    V SaveFrameVector(V at,Vector3 v)
    {FrameBasis(at,out var east,out var north);V world=frame.East*v.x+frame.Origin*v.y+frame.North*v.z;return new V(V.Dot(world,east),V.Dot(world,at),V.Dot(world,north));}
    Vector3 LocalFrameVector(V at,V v)
    {FrameBasis(at,out var east,out var north);var world=east*v.X+at*v.Y+north*v.Z;return new Vector3((float)V.Dot(world,frame.East),(float)V.Dot(world,frame.Origin),(float)V.Dot(world,frame.North));}
    Vector3 FrameAxis(CampObject c)=>c.Kind==CampKind.Stake?Vector3.up:LocalFrameVector(c.Position,c.FrameAxis).normalized;
    Vector3 FrameStart(CampObject c)
    {TerrainHandoff.Coordinates(frame,c.Position,out double x,out double z);return new Vector3((float)(x-originX),(float)CampFloor(x,z)+(float)(c.Kind==CampKind.Stake?-c.EmbedM:c.FrameHeightM),(float)(z-originZ));}
    Vector3 FramePoint(CampObject c,double t)=>FrameStart(c)+FrameAxis(c)*(float)(c.Materials[0].HeightM*t);
    Quaternion FrameRotation(CampObject c)=>Quaternion.FromToRotation(Vector3.up,FrameAxis(c))*Quaternion.Euler(0,(float)c.FrameRoll,0);
    Vector3 AnchorPoint(FrameAnchor a)
    {
        if(a.Parent!=null)return FramePoint(a.Parent,a.T)+LocalFrameVector(a.Parent.Position,a.Offset);
        TerrainHandoff.Coordinates(frame,a.Ground,out double x,out double z);return new Vector3((float)(x-originX),(float)CampFloor(x,z)+(float)campPlaceMaterial.WidthM*.5f,(float)(z-originZ));
    }
    void SetFramePose(CampObject c,Vector3 start,Vector3 axis)
    {double x=start.x+originX,z=start.z+originZ;c.Position=frame.Position(x,z);c.FrameHeightM=start.y-CampFloor(x,z);c.FrameAxis=SaveFrameVector(c.Position,axis).Unit;}
    static bool FrameRaySegment(Ray ray,Vector3 start,Vector3 end,float radius,out float distance,out float t)
    {
        var delta=end-start;float length=delta.magnitude;t=0;distance=0;if(length<.001f)return false;
        var axis=delta/length;var w=start-ray.origin;float b=Vector3.Dot(axis,ray.direction),d=Vector3.Dot(axis,w),e=Vector3.Dot(ray.direction,w);
        float along=Mathf.Abs(1-b*b)<.00001f?Mathf.Clamp(-d,0,length):Mathf.Clamp((b*e-d)/(1-b*b),0,length);
        var p=start+axis*along;float depth=Vector3.Dot(p-ray.origin,ray.direction);if(depth<0)return false;
        float miss=(p-ray.GetPoint(depth)).sqrMagnitude;if(miss>radius*radius)return false;
        t=along/length;distance=Mathf.Max(0,depth-Mathf.Sqrt(radius*radius-miss));return true;
    }
    bool TryFrameAnchor(out FrameAnchor anchor)
    {
        anchor=null;var c=AimedCamp(out float distance);
        if(c!=null&&FrameRules.Structural(c))
        {
            if(TraceCombatObstacle(new Ray(view.transform.position,view.transform.forward),Math.Max(0,distance-.06f),checkGround:false)!=null)return false;
            var p=c.Materials[0];var hit=view.transform.localPosition+view.transform.forward*distance;
            double t=Mathf.Clamp01(Vector3.Dot(hit-FrameStart(c),FrameAxis(c))/(float)p.HeightM);
            var kind=FrameJointKind.Lash;Vector3 offset;
            if(c.Kind==CampKind.Stake&&t>.7&&FrameRules.Fork(p)&&campPlaceMaterial.WidthM<=p.WidthM*2)
            {
                t=FrameRules.ForkT+(1-FrameRules.ForkT)*(campPlaceMaterial.WidthM+p.WidthM*.69)/(p.WidthM*2.8);
                offset=Quaternion.Euler(0,(float)c.Heading,0)*Vector3.right*(float)(p.WidthM*.43+campPlaceMaterial.WidthM*.5);kind=FrameJointKind.ForkRest;
            }
            else if(c.Kind==CampKind.Stake&&t>.86)
            {t=1;offset=Vector3.up*(float)campPlaceMaterial.WidthM*.5f;kind=FrameJointKind.EndRest;}
            else if(c.Kind==CampKind.FrameWood&&Math.Abs(FrameAxis(c).y)<.78)
            {offset=Vector3.up*(float)(p.WidthM+campPlaceMaterial.WidthM)*.5f;kind=FrameJointKind.CrossRest;}
            else
            {
                t=Math.Max(.05,Math.Min(FrameRules.Fork(p)?.72:.94,t));var radial=Vector3.ProjectOnPlane(view.transform.localPosition-FramePoint(c,t),FrameAxis(c)).normalized;
                if(radial.sqrMagnitude<.1f)radial=Vector3.right;offset=radial*(float)(p.WidthM+campPlaceMaterial.WidthM)*.5f;
            }
            anchor=new FrameAnchor{Parent=c,T=t,Offset=SaveFrameVector(c.Position,offset),Kind=kind};return true;
        }
        if(!CampGround(out var point,out var sample)||sample.Underwater)return false;
        anchor=new FrameAnchor{Ground=frame.Position(point.x+originX,point.z+originZ)};return true;
    }
    bool CycleFramePlacement(float scroll)
    {
        if(campPlaceMaterial==null||scroll==0)return false;
        campPlacementYaw+=Math.Sign(scroll)*15;framePlacementRoll+=Math.Sign(scroll)*15;UpdateCampPlacement();return true;
    }
    CampObject DraftFrame(FrameAnchor first,FrameAnchor second,out string reason)
    {
        reason="";if(first==null||second==null){reason="Choose two supports.";return null;}
        first=first.Copy();second=second.Copy();
        if(second.Parent==null&&first.Parent!=null){var swap=first;first=second;second=swap;}
        if(second.Parent==null||first.Parent!=null&&first.Parent==second.Parent){reason="Choose two different supports, or the ground and a support.";return null;}
        if((first.Parent!=null&&!Camp.Objects.Contains(first.Parent))||!Camp.Objects.Contains(second.Parent)){reason="That support is no longer present.";return null;}
        var a=AnchorPoint(first);var b=AnchorPoint(second);var direction=(b-a).normalized;
        // An incorrectly oriented fork can still balance wood on its end, but
        // does not receive the stronger seated-fork connection.
        foreach(var point in new[]{first,second})if(point.Kind==FrameJointKind.ForkRest&&point.Parent!=null)
        {
            var side=Quaternion.Euler(0,(float)point.Parent.Heading,0)*Vector3.right;
            if(Mathf.Abs(Vector3.Dot(Vector3.ProjectOnPlane(direction,Vector3.up).normalized,side))>.72f)
            {point.T=1;point.Kind=FrameJointKind.EndRest;point.Offset=SaveFrameVector(point.Parent.Position,Vector3.up*(float)campPlaceMaterial.WidthM*.5f);}
        }
        a=AnchorPoint(first);b=AnchorPoint(second);double gap=Vector3.Distance(a,b),length=campPlaceMaterial.HeightM;
        double firstT=first.Parent==null?0:.08,secondT=firstT+gap/length;
        if(gap<length*.25){reason="Spread the supports farther apart along this piece.";return null;}
        if(secondT>.98){reason="This wood is too short to reach both supports.";return null;}
        if(Vector3.Distance(PlayerFeet(),a)>4||Vector3.Distance(PlayerFeet(),b)>4){reason="Move closer to the supports.";return null;}
        direction=(b-a).normalized;var draft=new CampObject{Kind=CampKind.FrameWood,FrameGrounded=first.Parent==null,FrameRoll=framePlacementRoll,LastSimulatedSeconds=Camp.SimulatedSeconds};
        CampRules.Add(draft,campPlaceMaterial);SetFramePose(draft,a-direction*(float)(firstT*length),direction);
        foreach(var pair in new[]{(first,firstT),(second,secondT)})if(pair.Item1.Parent!=null)
            draft.FrameJoints.Add(new FrameJoint{ParentId=pair.Item1.Parent.Id,ParentT=pair.Item1.T,SelfT=pair.Item2,Kind=pair.Item1.Kind,Offset=pair.Item1.Offset});
        return draft;
    }
    List<Possession> FrameBindings()
    {RefreshInventoryNearby();return CraftAccessible().Where(p=>FrameRules.Binding(p)&&!Inventory.Crafts.Any(j=>PortableCrafting.Affects(j,p.Id))).GroupBy(p=>p.Id).Select(g=>g.First()).ToList();}
    double RequiredFrameBinding(CampObject c)=>c.FrameJoints.Where(j=>j.Kind==FrameJointKind.Lash&&!j.Lashed).Sum(j=>FrameRules.BindingKg(c,Camp.Objects.First(p=>p.Id==j.ParentId)));
    bool FrameClear(CampObject c,out string reason)
    {
        reason="";var start=FrameStart(c);var end=FramePoint(c,1);float radius=(float)c.Materials[0].WidthM*.5f;
        for(int i=0;i<=12;i++){var p=Vector3.Lerp(start,end,i/12f);if(p.y<CampFloor(p.x+originX,p.z+originZ)+radius*.15){reason="The wood would pass into the ground.";return false;}}
        foreach(var collider in Physics.OverlapCapsule(transform.TransformPoint(start),transform.TransformPoint(end),radius*.8f,1<<ScatterSolidLayer,QueryTriggerInteraction.Ignore))
        {
            bool support=c.FrameJoints.Any(j=>campVisuals.TryGetValue(j.ParentId,out var visual)&&collider.transform.IsChildOf(visual.Root.transform));
            if(!support){reason="The wood would pass through another solid object.";return false;}
        }
        return true;
    }
    bool FrameDraftSupported(CampObject c,out string reason)
    {
        reason="";var test=c.Copy();
        foreach(var j in test.FrameJoints.Where(j=>j.Kind==FrameJointKind.Lash&&!j.Lashed))
        {var p=PortableTechnology.Create(PossessionKind.Cordage,test.Position);p.MassKg=FrameRules.BindingKg(test,Camp.Objects.First(parent=>parent.Id==j.ParentId));j.Bindings.Add(p);}
        var state=new CampState{Objects=Camp.Objects.Concat(new[]{test}).ToList()};var analysis=FrameRules.Analyse(state);var result=analysis.Nodes[test.Id];
        if(!result.Supported){reason=result.Reason;return false;}
        if(analysis.Nodes.Values.Any(n=>n.Member.Kind==CampKind.FrameWood&&n.Member.Id!=test.Id&&!n.Supported&&frameAnalysis!=null&&frameAnalysis.Nodes.TryGetValue(n.Member.Id,out var old)&&old.Supported))
        {reason="This added weight would bring part of the frame down.";return false;}
        return true;
    }
    void UpdateFramePlacement()
    {
        frameDraft=null;frameHover=null;campPlaceValid=false;campPlaceReason=frameFirst==null?"Choose the first support or the foot on the ground. [LMB]":"Aim at the second support. [LMB]";
        bool found=TryFrameAnchor(out frameHover);campPreview.enabled=false;
        if(frameFirst==null)
        {
            campPlaceValid=found;if(found){campPlacePoint=AnchorPoint(frameHover);campPlaceReason="Set first contact [LMB] · Rotate [wheel] · Cancel [RMB]";}
            if(campGhost!=null){campGhost.SetActive(found);campGhost.transform.localPosition=campPlacePoint;campGhost.transform.localRotation=Quaternion.Euler(0,campPlacementYaw,0);}
        }
        else if(found)
        {
            frameDraft=DraftFrame(frameFirst,frameHover,out var reason);
            if(frameDraft!=null)
            {
                campPlaceValid=FrameClear(frameDraft,out reason)&&FrameDraftSupported(frameDraft,out reason);
                double needed=RequiredFrameBinding(frameDraft);
                if(campPlaceValid&&!UnlimitedResources&&FrameBindings().Sum(p=>p.MassKg)+1e-8<needed){campPlaceValid=false;reason="Bring cordage, sinew cord or rawhide strips to lash these side joints.";}
                campPlaceReason=campPlaceValid?(needed>0?"Lash into place [LMB]":"Rest wood in place [LMB]")+" · Roll [wheel] · Cancel [RMB]":reason;
                if(campGhost!=null){campGhost.SetActive(true);campGhost.transform.localPosition=FrameStart(frameDraft);campGhost.transform.localRotation=FrameRotation(frameDraft);}
                campPreview.enabled=true;campPreview.positionCount=2;campPreview.loop=false;campPreview.SetPosition(0,FrameStart(frameDraft));campPreview.SetPosition(1,FramePoint(frameDraft,1));
            }
            else{campPlaceReason=reason;if(campGhost!=null)campGhost.SetActive(false);}
        }
        else if(campGhost!=null)campGhost.SetActive(false);
        var color=campPlaceValid?new Color(.32f,.8f,.5f,.25f):new Color(.95f,.3f,.2f,.25f);campPreview.sharedMaterial.SetColor("_BaseColor",color);if(campGhostMaterial!=null)campGhostMaterial.SetColor("_BaseColor",color);
    }
    void FramePlacementClick()
    {
        Physics.SyncTransforms();UpdateFramePlacement();if(!campPlaceValid){InventoryFeedback(campPlaceReason);return;}
        if(frameFirst==null){frameFirst=frameHover.Copy();UpdateFramePlacement();return;}
        var draft=frameDraft;var wood=campPlaceMaterial;var first=frameFirst.Copy();var second=frameHover.Copy();double required=RequiredFrameBinding(draft);
        void Place()
        {
            // Re-read moving supports and inventory after timed work; do not
            // consume wood/bindings until every dependency still passes.
            Physics.SyncTransforms();campPlaceMaterial=wood;var fresh=DraftFrame(first,second,out string reason);campPlaceMaterial=null;
            if(fresh==null||!FrameClear(fresh,out reason)||!FrameDraftSupported(fresh,out reason)){InventoryFeedback(reason);return;}
            var bindings=FrameBindings();double need=RequiredFrameBinding(fresh);
            if(!UnlimitedResources&&bindings.Sum(p=>p.MassKg)+1e-8<need){InventoryFeedback("The binding material is no longer available.");return;}
            foreach(var p in bindings)if(Inventory.Find(p.Id)==null&&!MovePossession(p.Id,PossessionPlace.Ground)){InventoryFeedback("Binding material is out of reach.");return;}
            var owned=TakeCampMaterial(wood);if(owned==null)return;fresh.Materials.Clear();CampRules.Add(fresh,owned);
            foreach(var joint in fresh.FrameJoints.Where(j=>j.Kind==FrameJointKind.Lash))
            {
                var parent=Camp.Objects.First(c=>c.Id==joint.ParentId);
                if(UnlimitedResources){var cord=PortableTechnology.Create(PossessionKind.Cordage,fresh.Position);cord.MassKg=FrameRules.BindingKg(fresh,parent);joint.Bindings.Add(cord);}
                else if(!FrameRules.Lash(fresh,parent,joint,Inventory,bindings.Select(p=>p.Id),out reason))throw new InvalidOperationException("Validated lashing could not be applied: "+reason);
            }
            Camp.Objects.Add(fresh);frameDirty=true;campUpdateAt=0;UpdateCamp();inventoryNearbyAt=0;InventoryFeedback("Frame wood placed. Unsecured rests can be lashed from the joint's interaction menu.");
        }
        if(required>0&&!UnlimitedResources)
        {
            var binding=FrameBindings().FirstOrDefault();if(binding==null)return;
            if(Inventory.Find(binding.Id)==null&&!MovePossession(binding.Id,PossessionPlace.Ground))return;
            CancelCampPlacement();BeginWorldToolWork(binding,"Lashing the frame",90*draft.FrameJoints.Count(j=>j.Kind==FrameJointKind.Lash),Place);
        }
        else{CancelCampPlacement();Place();}
    }
    void LashFrameJoint(CampObject member,FrameJoint joint)
    {
        var parent=Camp.Objects.FirstOrDefault(c=>c.Id==joint.ParentId);if(parent==null||joint.Lashed)return;
        var sources=FrameBindings();if(!UnlimitedResources&&sources.Sum(p=>p.MassKg)+1e-8<FrameRules.BindingKg(member,parent)){InventoryFeedback("Requires cordage, sinew cord or rawhide strips.");return;}
        void Finish()
        {
            if(!Camp.Objects.Contains(member)||!Camp.Objects.Contains(parent)||joint.Lashed)return;
            var bindings=FrameBindings();foreach(var p in bindings)if(Inventory.Find(p.Id)==null&&!MovePossession(p.Id,PossessionPlace.Ground))return;
            if(UnlimitedResources){var cord=PortableTechnology.Create(PossessionKind.Cordage,member.Position);cord.MassKg=FrameRules.BindingKg(member,parent);joint.Bindings.Add(cord);}
            else if(!FrameRules.Lash(member,parent,joint,Inventory,bindings.Select(p=>p.Id),out var reason)){InventoryFeedback(reason);return;}
            InvalidateCampVisual(member);frameDirty=true;campUpdateAt=0;inventoryNearbyAt=0;InventoryFeedback("Joint lashed.");
        }
        campMenu=null;CaptureMouse();if(UnlimitedResources){Finish();return;}
        var tool=sources.First();if(Inventory.Find(tool.Id)==null&&!MovePossession(tool.Id,PossessionPlace.Ground))return;
        BeginWorldToolWork(tool,"Lashing the joint",90,Finish);
    }
    void DropFrameMaterial(CampObject c,Possession p,bool wood)
    {
        p.Position=c.Position;p.Place=PossessionPlace.Ground;p.ContainerId="";p.InFlight=false;p.GroundOffsetM=wood?c.FrameHeightM:.08;
        var axis=LocalFrameVector(c.Position,c.FrameAxis);var rotation=wood?Quaternion.FromToRotation(Vector3.up,axis)*Quaternion.Euler(0,(float)c.FrameRoll,0):Quaternion.identity;
        p.GroundRotation=new V(rotation.eulerAngles.x,rotation.eulerAngles.y,rotation.eulerAngles.z);p.GroundVelocity=p.GroundSpin=new V();p.GroundMoving=true;
        if(!Inventory.Items.Any(i=>i.Id==p.Id))Inventory.Items.Add(p);
    }
    void UntieFrameJoint(CampObject member,FrameJoint joint)
    {foreach(var p in FrameRules.Untie(joint))DropFrameBinding(member,joint,p);InvalidateCampVisual(member);frameDirty=true;campUpdateAt=0;inventoryNearbyAt=0;}
    void DropFrameBinding(CampObject member,FrameJoint joint,Possession p)
    {
        DropFrameMaterial(member,p,false);if((member.Position-Position).Length*frame.RadiusM>250)return;
        var point=FramePoint(member,joint.SelfT);double x=point.x+originX,z=point.z+originZ;p.Position=frame.Position(x,z);p.GroundOffsetM=point.y-Ground(x,z);
    }
    void RefreshFrameStructure()
    {
        foreach(var c in Camp.Objects.Where(FrameRules.Structural))
        {
            if((c.Position-Position).Length*frame.RadiusM>250)continue;
            TerrainHandoff.Coordinates(frame,c.Position,out double x,out double z);double floor=CampFloor(x,z);
            if(!frameGroundHeights.TryGetValue(c.Id,out double old)||Math.Abs(old-floor)>.005){frameDirty=true;frameGroundHeights[c.Id]=floor;}
        }
        if(!frameDirty)return;frameDirty=false;frameAnalysis=FrameRules.Analyse(Camp);
        var broken=new HashSet<string>();
        foreach(var c in frameAnalysis.Order.Where(c=>c.Kind==CampKind.FrameWood))
        {
            if(!frameAnalysis.Nodes[c.Id].Supported||c.FrameJoints.Any(j=>broken.Contains(j.ParentId))){broken.Add(c.Id);continue;}
            if((c.Position-Position).Length*frame.RadiusM>250)continue;
            var ordered=c.FrameJoints.OrderBy(j=>j.SelfT).ToArray();var first=ordered[0];var last=ordered[ordered.Length-1];
            Vector3 Contact(FrameJoint j){var parent=frameAnalysis.Nodes[j.ParentId].Member;return FramePoint(parent,j.ParentT)+LocalFrameVector(parent.Position,j.Offset);}
            Vector3 a=c.FrameGrounded?FrameStart(c):Contact(first),b=Contact(last);double ta=c.FrameGrounded?0:first.SelfT;
            double expected=(last.SelfT-ta)*c.Materials[0].HeightM;
            if(Math.Abs(Vector3.Distance(a,b)-expected)>Math.Max(.08,c.Materials[0].WidthM*2)){broken.Add(c.Id);continue;}
            var axis=(b-a).normalized;var start=a-axis*(float)(ta*c.Materials[0].HeightM);var before=FrameStart(c);var oldAxis=FrameAxis(c);
            SetFramePose(c,start,axis);if((before-start).sqrMagnitude>.000001||(oldAxis-axis).sqrMagnitude>.000001)InvalidateCampVisual(c);
        }
        foreach(var c in Camp.Objects.Where(c=>c.Kind==CampKind.FrameWood&&(!frameAnalysis.Nodes[c.Id].Supported||broken.Contains(c.Id))).ToArray())
        {
            foreach(var p in c.Materials)DropFrameMaterial(c,p,true);foreach(var j in c.FrameJoints)foreach(var p in j.Bindings)DropFrameBinding(c,j,p);
            Camp.Objects.Remove(c);InvalidateCampVisual(c);if(campMenu==c)campMenu=null;
        }
        frameAnalysis=FrameRules.Analyse(Camp);inventoryNearbyAt=0;
        foreach(var id in frameGroundHeights.Keys.Where(id=>!frameAnalysis.Nodes.ContainsKey(id)).ToArray())frameGroundHeights.Remove(id);
    }
    string FrameSummary(CampObject c)=>frameAnalysis!=null&&frameAnalysis.Nodes.TryGetValue(c.Id,out var n)?(n.Stability>.6?"Firm":n.Stability>.22?"Supported, but loose":"Weak footing or loose joints"):"Frame wood";
    void CreateFrameVisual(CampObject c,GameObject root)
    {
        var p=c.Materials[0];var wood=CreatePossessionVisual(p,30);wood.transform.SetParent(root.transform,false);
        var collider=root.AddComponent<CapsuleCollider>();collider.radius=(float)p.WidthM*.5f;collider.height=(float)p.HeightM;collider.center=Vector3.up*collider.height*.5f;root.layer=ScatterSolidLayer;
        if(frameBindingMaterial==null){frameBindingMaterial=new Material(Resources.Load<Shader>("WorldGenInventoryPreview"));frameBindingMaterial.SetColor("_Color",new Color(.48f,.34f,.2f));frameBindingMaterial.SetFloat("_WorldLighting",1);}
        foreach(var joint in c.FrameJoints.Where(j=>j.Lashed))
        {
            var parent=Camp.Objects.FirstOrDefault(o=>o.Id==joint.ParentId);float width=(float)(p.WidthM+(parent?.Materials[0].WidthM??p.WidthM));
            var knot=new GameObject("Lashing",typeof(LineRenderer));knot.layer=30;knot.transform.SetParent(root.transform,false);var line=knot.GetComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=frameBindingMaterial;line.widthMultiplier=.0035f;line.positionCount=99;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            var inverse=Quaternion.Inverse(FrameRotation(c));var child=FramePoint(c,joint.SelfT);var parentPoint=parent!=null?FramePoint(parent,joint.ParentT):child;
            var center=inverse*((child+parentPoint)*.5f-FrameStart(c));var parentAxis=parent!=null?inverse*FrameAxis(parent):Vector3.right;
            var normal=Vector3.Cross(Vector3.up,parentAxis).normalized;if(normal.sqrMagnitude<.1f)normal=Vector3.right;
            var winding=(Vector3.up+parentAxis).normalized;if(winding.sqrMagnitude<.1f)winding=Vector3.up;
            var across=Vector3.Cross(winding,normal).normalized;
            for(int i=0;i<99;i++){float angle=i/32f*Mathf.PI*2;line.SetPosition(i,center+(normal*Mathf.Cos(angle)+across*Mathf.Sin(angle))*width*.82f+winding*(i/98f-.5f)*.02f);}
        }
    }
}
