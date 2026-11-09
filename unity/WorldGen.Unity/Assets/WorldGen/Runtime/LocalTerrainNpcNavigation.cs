using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class NpcSolid{public double X,Z,Y,Radius,Height;}
    bool LivingNoticesVisitor(HumanActor actor,double distance)
    {
        if(actor.SimulationAge<actor.VisitorCheckAt)return actor.SeesVisitor;
        actor.VisitorCheckAt=actor.SimulationAge+.55+(HumanHash(actor.ResidentId)%11)*.027;
        var eye=actor.SneakNeck.position+transform.up*.08f;var localEye=transform.InverseTransformPoint(eye);
        var target=transform.TransformPoint(new Vector3((float)(motion.X-originX),(float)motion.Y,(float)(motion.Z-originZ)));
        actor.SeesVisitor=!Physics.Linecast(eye,target,1<<ScatterSolidLayer,QueryTriggerInteraction.Ignore);
        if(!actor.SeesVisitor)return false;
        for(int k=1;k<=4;k++)
        {
            double t=k/5d,x=actor.Motor.X+(motion.X-actor.Motor.X)*t,z=actor.Motor.Z+(motion.Z-actor.Motor.Z)*t;
            double height=localEye.y+(motion.Y-localEye.y)*t;
            if(VisibleGround(x,z,height-3)>height-.1){actor.SeesVisitor=false;break;}
        }
        return actor.SeesVisitor;
    }
    bool ResolveLivingDryPoint(V proposal,V fallback,out V safe)
    {
        safe=proposal;TerrainHandoff.Coordinates(frame,proposal,out double x,out double z);
        if(double.IsNaN(VisibleGround(x,z,double.NaN)))return false;
        if(!walkingPatch.Sample(proposal).Underwater)return true;
        if(walkingPatch.Sample(fallback).Underwater)return false;
        V dry=fallback,wet=proposal;
        for(int i=0;i<10;i++){var middle=HumanLocalLife.Interpolate(dry,wet,.5);if(walkingPatch.Sample(middle).Underwater)wet=middle;else dry=middle;}
        double distance=HumanDistance(dry,fallback);safe=HumanLocalLife.Interpolate(dry,fallback,Math.Min(1,1/Math.Max(1,distance)));return true;
    }
    V LivingDryGoal(HumanActor actor,V proposal)
    {
        if(actor.HasSafeGoal&&HumanDistance(actor.GoalProposal,proposal)<2)return actor.GoalSafe;
        actor.GoalProposal=proposal;
        if(!ResolveLivingDryPoint(proposal,actor.Planet,out var safe))return actor.Planet;
        actor.HasSafeGoal=true;actor.GoalSafe=safe;return safe;
    }
    void CacheLivingSolids(HumanActor actor)
    {
        actor.NearbySolids.Clear();var center=new Vector3((float)(actor.Motor.X-originX),(float)(actor.Motor.Y-TerrainMotion.EyeHeightM)+1,(float)(actor.Motor.Z-originZ));
        var region=new Bounds(center,new Vector3(7,4,7));
        foreach(var chunk in scatterChunks.Values)
        {
            if(chunk.Roots==null||chunk.Groups==null||!chunk.AllBounds.Intersects(region))continue;
            foreach(var group in chunk.Groups)
            {
                if(!group.Bounds.Intersects(region))continue;
                foreach(int i in group.Indices)
                {
                    var p=chunk.Source.Instances[i];bool tree=SolidTree(p.Kind);
                    if(!tree&&(p.Kind!=ScatterKind.Boulder&&p.Kind!=ScatterKind.Outcrop&&p.Kind!=ScatterKind.Stone||p.HeightM<.18||CollectedRock(p)))continue;
                    var root=chunk.Roots[i];double radius=tree?p.WidthM*(p.Kind==ScatterKind.GeneratedTree?new TreeMorphology(p.Variant).TrunkRadius:.035):p.WidthM*.43;
                    double dx=root.x-center.x,dz=root.z-center.z;if(dx*dx+dz*dz>Math.Pow(3.5+radius,2))continue;
                    actor.NearbySolids.Add(new NpcSolid{X=root.x+originX,Z=root.z+originZ,Y=root.y,Radius=Math.Max(.025,radius),Height=p.HeightM*(tree?.88:1)});
                }
            }
        }
    }
    void ResolveLivingScatterContact(HumanActor actor)
    {
        foreach(var solid in actor.NearbySolids)
        {
            double feet=actor.Motor.Y-TerrainMotion.EyeHeightM;if(feet>=solid.Y+solid.Height+.05||feet+1.2<solid.Y)continue;
            double dx=actor.Motor.X-solid.X,dz=actor.Motor.Z-solid.Z,length=Math.Sqrt(dx*dx+dz*dz),radius=solid.Radius+.2;
            if(length>=radius)continue;if(length<.001){dx=Math.Sin(actor.Yaw*Math.PI/180);dz=Math.Cos(actor.Yaw*Math.PI/180);length=1;}
            actor.Motor.X=solid.X+dx/length*radius;actor.Motor.Z=solid.Z+dz/length*radius;
            actor.Motor.Y=VisibleGround(actor.Motor.X,actor.Motor.Z,feet)+TerrainMotion.EyeHeightM;
        }
    }
    void SteerLivingScatter(HumanActor actor,double targetX,double targetZ)
    {
        double a=actor.TargetYaw*Math.PI/180,fx=Math.Sin(a),fz=Math.Cos(a);
        foreach(var solid in actor.NearbySolids)
        {
            double feet=actor.Motor.Y-TerrainMotion.EyeHeightM;if(feet>=solid.Y+solid.Height+.05||feet+1.2<solid.Y)continue;
            double dx=solid.X-actor.Motor.X,dz=solid.Z-actor.Motor.Z,along=dx*fx+dz*fz,cross=dx*fz-dz*fx;
            if(along<-.15||along>1.6+solid.Radius||Math.Abs(cross)>solid.Radius+.35)continue;
            double side=Math.Abs(cross)>.05?-Math.Sign(cross):((HumanHash(actor.ResidentId)&1)==0?1:-1);
            actor.AvoidYaw=actor.TargetYaw+(float)(side*65);actor.AvoidUntil=actor.SimulationAge+.65;return;
        }
    }
    CampObject LivingHouse(HumanActor actor)
    {
        if(!livingGroups.TryGetValue(actor.GroupId,out var group)||group.Situation.Phase==HumanCampPhase.Travelling)return null;
        if(actor.HouseSite==group.Situation.Camp.Id&&actor.SimulationAge<actor.HouseCheckAt)return actor.CampHouse;
        actor.HouseSite=group.Situation.Camp.Id;actor.HouseCheckAt=actor.SimulationAge+1;actor.CampHouse=null;
        foreach(var house in Camp.Objects)if(house.NpcGroup==actor.GroupId&&house.NpcHousehold==actor.Resident.Household&&house.Blueprint!=null){actor.CampHouse=house;break;}
        return actor.CampHouse;
    }
    bool LivingDoorwayGoal(HumanActor actor,CampObject house,ref double x,ref double z)
    {
        var plan=house.Blueprint;TerrainHandoff.Coordinates(frame,house.Position,out double cx,out double cz);double a=house.Heading*Math.PI/180;
        bool Inside(double px,double pz)
        {
            double dx=px-cx,dz=pz-cz,lx=dx*Math.Cos(a)-dz*Math.Sin(a),lz=dx*Math.Sin(a)+dz*Math.Cos(a);
            if(plan.Design==CampDesign.BrushHut||plan.Design==CampDesign.ConicalTent)return lx*lx+lz*lz<Math.Pow(plan.WidthM*.5+.05,2);
            if(plan.Design==CampDesign.LongHut){double end=Math.Max(0,Math.Abs(lz)-Math.Max(0,plan.DepthM-plan.WidthM)*.5);return lx*lx+end*end<Math.Pow(plan.WidthM*.5+.05,2);}
            return Math.Abs(lx)<plan.WidthM*.5+.05&&Math.Abs(lz)<plan.DepthM*.5+.05;
        }
        bool from=Inside(actor.Motor.X,actor.Motor.Z),to=Inside(x,z);if(from==to)return false;
        double entrySide=plan.Design==CampDesign.LeanTo?-1:1;
        double doorX=cx+Math.Sin(a)*(plan.DepthM*.5+.85)*entrySide,doorZ=cz+Math.Cos(a)*(plan.DepthM*.5+.85)*entrySide;
        double actorX=(actor.Motor.X-cx)*Math.Cos(a)-(actor.Motor.Z-cz)*Math.Sin(a),actorZ=(actor.Motor.X-cx)*Math.Sin(a)+(actor.Motor.Z-cz)*Math.Cos(a);
        // Once aligned in front of the opening, continue through it. Routing
        // back to an exterior waypoint here creates a stop/turn oscillation.
        if(!from&&Math.Abs(actorX)<.32&&actorZ*entrySide>plan.DepthM*.5-.15)return false;
        x=doorX;z=doorZ;return true;
    }
    void ResolveLivingSolidContact(HumanActor actor,double oldX,double oldY,double oldZ)
    {
        var motor=actor.Motor;var delta=new Vector3((float)(motor.X-oldX),0,(float)(motor.Z-oldZ));float distance=delta.magnitude;if(distance<.00001f)return;
        float height=(actor.AgeModel?.EyeHeight??actor.Definition.EyeHeight)*actor.Root.transform.localScale.y+.1f;
        if(actor.NpcCrawling)height=Math.Min(height,.76f*actor.AgeMotionRatio*actor.Root.transform.localScale.y);else if(actor.NpcCrouching)height=Math.Min(height,1.27f);float radius=Math.Max(.1f,Math.Min(.24f,height*.15f));
        var feet=new Vector3((float)(oldX-originX),(float)(oldY-TerrainMotion.EyeHeightM),(float)(oldZ-originZ));
        Vector3 bottom=transform.TransformPoint(feet+Vector3.up*(radius+.08f)),top=transform.TransformPoint(feet+Vector3.up*Math.Max(radius+.08f,height-radius));
        Vector3 direction=transform.TransformDirection(delta/distance);
        if(!Physics.CapsuleCast(bottom,top,radius,direction,out var hit,distance+.015f,1<<ScatterSolidLayer,QueryTriggerInteraction.Ignore))return;
        float allowed=Mathf.Clamp(hit.distance-.015f,0,distance);Vector3 travel=direction*allowed;
        Vector3 remainder=Vector3.ProjectOnPlane(direction*(distance-allowed),hit.normal);remainder.y=0;
        if(remainder.sqrMagnitude>.00001f)
        {
            float length=remainder.magnitude;var slide=remainder/length;
            if(Physics.CapsuleCast(bottom+travel,top+travel,radius,slide,out var next,length+.01f,1<<ScatterSolidLayer,QueryTriggerInteraction.Ignore))length=Math.Max(0,next.distance-.01f);
            travel+=slide*length;
        }
        var local=transform.InverseTransformVector(travel);motor.X=oldX+local.x;motor.Z=oldZ+local.z;
        if(motor.Grounded)motor.Y=VisibleGround(motor.X,motor.Z,oldY-TerrainMotion.EyeHeightM)+TerrainMotion.EyeHeightM;
    }
}
