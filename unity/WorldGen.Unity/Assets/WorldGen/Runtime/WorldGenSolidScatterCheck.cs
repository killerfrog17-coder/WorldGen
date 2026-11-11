using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckSteepPlayerSolidContact(string output)
    {
        double bx=0,bz=0,gx=0,gz=0,steepest=1;
        foreach(double radius in new[]{16d,32,64})for(int direction=0;direction<8;direction++)
        {
            double angle=direction*Math.PI/4,x=Math.Cos(angle)*radius,z=Math.Sin(angle)*radius;
            double h=VisibleGround(x,z,double.NaN);if(!double.IsFinite(h))continue;
            double dx=VisibleGround(x+.5,z,h)-VisibleGround(x-.5,z,h),dz=VisibleGround(x,z+.5,h)-VisibleGround(x,z-.5,h),grade=dx*dx+dz*dz;
            if(grade>steepest){steepest=grade;bx=x;bz=z;gx=dx;gz=dz;}
        }
        if(steepest<=1)throw new InvalidOperationException("Player collision proof needs actual resident steep terrain");
        double length=Math.Sqrt(gx*gx+gz*gz);gx/=length;gz/=length;double sx=bx-gx*.8,sz=bz-gz*.8,tx=-gz,tz=gx;
        var savedMode=Mode;bool savedPause=paused,savedTerrain=TerrainDiagnostics.HoldTerrain,savedPlants=TerrainDiagnostics.HoldPlants;
        double savedX=motion.X,savedY=motion.Y,savedZ=motion.Z,savedVertical=motion.VerticalSpeed;
        bool savedFlying=motion.Flying,savedGrounded=motion.Grounded,savedNoClip=motion.NoClip;var savedWater=motion.Water;
        var obstacle=new GameObject("Steep terrain adjacent solid proof");obstacle.layer=ScatterSolidLayer;obstacle.transform.SetParent(transform,false);
        int steps=0,contacts=0;double minimumClearance=double.PositiveInfinity;
        try
        {
            paused=true;TerrainDiagnostics.HoldTerrain=TerrainDiagnostics.HoldPlants=true;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
            motion.Water=null;motion.Place(sx,sz,SurfaceGround(sx,sz));PreparePlayerSolids();
            obstacle.transform.localPosition=new Vector3((float)(bx-gx*.2+tx*.45-originX),(float)SurfaceGround(sx,sz)+1,(float)(bz-gz*.2+tz*.45-originZ));
            obstacle.transform.localRotation=Quaternion.LookRotation(new Vector3((float)gx,0,(float)gz),Vector3.up);
            var box=obstacle.AddComponent<BoxCollider>();box.size=new Vector3(.3f,3,1.4f);Physics.SyncTransforms();
            double yaw=Math.Atan2(gx+tx*.65,gz+tz*.65)*180/Math.PI;
            for(int i=0;i<180;i++)
            {
                PreparePlayerSolids();motion.Move(1d/60,1,0,0,yaw,0,false,false,PlayerSolidGround,walk:true);
                double surface=VisibleGround(motion.X,motion.Z,double.NaN);if(!double.IsFinite(surface))throw new InvalidOperationException("Actual player left resident terrain during steep collision proof");
                double clearance=motion.Y-TerrainMotion.EyeHeightM-surface;minimumClearance=Math.Min(minimumClearance,clearance);
                if(clearance<-.025)throw new InvalidOperationException("CharacterController pushed actual player below rendered terrain: "+clearance);
                var feet=PlayerFeet();float radius=playerSolid.radius;
                Vector3 low=transform.TransformPoint(feet+Vector3.up*radius),high=transform.TransformPoint(feet+Vector3.up*(playerSolid.height-radius));
                if(Physics.CheckCapsule(low,high,Math.Max(.01f,radius-.03f),1<<ScatterSolidLayer,QueryTriggerInteraction.Ignore))throw new InvalidOperationException("Steep terrain correction moved the player body inside an adjacent solid");
                var middle=transform.TransformPoint(feet+Vector3.up*playerSolid.height*.5f);
                if((box.ClosestPoint(middle)-middle).sqrMagnitude<=(radius+.04f)*(radius+.04f))contacts++;
                steps++;if(i%30==29)yield return null;
            }
            if(contacts==0)throw new InvalidOperationException("Actual player proof did not reach the adjacent solid");
            File.WriteAllText(Path.Combine(output,"player-solid-steep-contact.txt"),"PASS actual Creative player motor +CharacterController on resident steep terrain; steps="+steps+"; adjacent-solid contact steps="+contacts+"; start grade="+Math.Sqrt(steepest)+"; minimum final foot/rendered-triangle clearance="+minimumClearance+"m; body remains outside solids.\n");
        }
        finally
        {
            obstacle.SetActive(false);Destroy(obstacle);Physics.SyncTransforms();PlayerRules.Mode=savedMode;ApplyPlayerMode();
            motion.X=savedX;motion.Y=savedY;motion.Z=savedZ;motion.VerticalSpeed=savedVertical;motion.Flying=savedFlying;motion.Grounded=savedGrounded;motion.NoClip=savedNoClip;motion.Water=savedWater;
            paused=savedPause;TerrainDiagnostics.HoldTerrain=savedTerrain;TerrainDiagnostics.HoldPlants=savedPlants;solidRefreshAt=0;UpdateCamera();
        }
    }
    public IEnumerator CheckSolidScatterFlow(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("SOLID SCATTER "+text);report.Add("PASS "+text);Debug.Log("SOLID SCATTER PASS "+text);}
        PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();PreparePlayerSolids();
        double deadline=Time.unscaledTimeAsDouble+40;
        while(!scatterChunks.Values.Any(c=>c.Roots!=null&&c.Source.Instances.Any(p=>SolidTree(p.Kind)))&&Time.unscaledTimeAsDouble<deadline)yield return null;
        paused=true;
        var chunk=scatterChunks.Values.First(c=>c.Roots!=null&&c.Source.Instances.Any(p=>SolidTree(p.Kind)));
        int index=Array.FindIndex(chunk.Source.Instances,p=>SolidTree(p.Kind)&&p.HeightM>3);
        Check(index>=0,"Loaded natural tree source is available");var tree=chunk.Source.Instances[index];var root=chunk.Roots[index];
        double x=root.x+originX,z=root.z+originZ,floor=root.y+.4;
        float radius=Mathf.Max(.015f,(float)tree.WidthM*(tree.Kind==ScatterKind.GeneratedTree?(float)new TreeMorphology(tree.Variant).TrunkRadius:.035f));
        void Place(double a,double b,double y,bool fly=false){motion.Place(a,b,y,fly,0);if(fly)motion.Y=y+TerrainMotion.EyeHeightM;solidRefreshAt=0;PreparePlayerSolids();}
        Place(x-radius-1,z,floor);
        Check(scatterSolids.ContainsKey(tree.Id),"Actual visible trunk receives a nearby collider");
        for(int i=0;i<120;i++)motion.Move(1.0/60,1,0,0,90,0,true,false,(a,b)=>floor);
        Check(motion.X<x-radius-.18&&motion.X>x-radius-.55,"Sprinting stops at trunk surface with player body clearance");
        Place(x-radius-.8,z-.6,floor);
        for(int i=0;i<90;i++)motion.Move(1.0/60,1,0,0,45,0,false,false,(a,b)=>floor);
        Check(motion.Z>z+.3,"Oblique movement slides around a trunk instead of freezing all movement");
        Place(x-radius-1,z,floor,true);motion.FlySpeed=200;
        motion.Move(.25,1,0,0,90,0,true,false,(a,b)=>floor);
        Check(motion.X<x+radius,"Fast Creative flight cannot tunnel through a trunk");
        PlayerRules.Mode=GameMode.Spectator;ApplyPlayerMode();Place(x-radius-1,z,floor,true);
        motion.Move(.02,1,0,0,90,0,false,false,(a,b)=>floor);
        Check(motion.X>x+radius,"Spectator retains noclip through tree colliders");
        PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
        var rockChunk=scatterChunks.Values.FirstOrDefault(c=>c.Roots!=null&&c.Source.Instances.Any(p=>(p.Kind==ScatterKind.Boulder||p.Kind==ScatterKind.Outcrop)&&p.HeightM>.5));
        Check(rockChunk!=null,"Natural boulder or outcrop source is available");
        int rockIndex=Array.FindIndex(rockChunk.Source.Instances,p=>(p.Kind==ScatterKind.Boulder||p.Kind==ScatterKind.Outcrop)&&p.HeightM>.5);
        var rock=rockChunk.Source.Instances[rockIndex];var rockRoot=rockChunk.Roots[rockIndex];x=rockRoot.x+originX;z=rockRoot.z+originZ;
        Place(x,z,rockRoot.y+rock.HeightM+2);
        Check(scatterSolids.TryGetValue(rock.Id,out var solid)&&solid.Root.GetComponentInChildren<MeshCollider>()!=null,"Boulders use their actual near-model mesh rather than a box");
        Physics.SyncTransforms();var bounds=solid.Root.GetComponentInChildren<MeshCollider>().bounds;
        var topOrigin=new Vector3(bounds.center.x,bounds.max.y+1,bounds.center.z);
        Check(Physics.Raycast(topOrigin,Vector3.down,out var surface,100,1<<ScatterSolidLayer)&&surface.collider.transform.IsChildOf(solid.Root.transform),"A solid rock surface exists at the rendered boulder");
        Vector3 local=transform.InverseTransformPoint(surface.point);x=local.x+originX;z=local.z+originZ;
        Place(x,z,local.y+2);
        double naturalFloor=Ground(x,z);
        for(int i=0;i<180;i++)motion.Move(1.0/60,0,0,0,0,0,false,false,PlayerSolidGround);
        Check(motion.Grounded&&Math.Abs(motion.Y-TerrainMotion.EyeHeightM-local.y)<.08,"Player lands and remains supported on a boulder");
        Check(motion.Y-TerrainMotion.EyeHeightM>naturalFloor+.1,"Boulder support does not snap the player back through it to the terrain");
        var ray=new Ray(surface.point+Vector3.up,Vector3.down);var obstacle=TraceCombatObstacle(ray,1.1f,null,false);
        Check(obstacle!=null&&obstacle.Source.Id==rock.Id,"Combat obstruction keeps the original scatter source identity");
        double precisionX=originX+8000,precisionZ=originZ+8000,precisionFloor=Math.Max(local.y+1000,10000);
        Place(precisionX,precisionZ,precisionFloor);
        for(int i=0;i<240;i++)motion.Move(1.0/240,1,0,0,90,0,false,false,(a,b)=>precisionFloor,walk:true);
        Check(Math.Abs(motion.X-precisionX-TerrainMotion.WalkSpeedMps)<.00001,"Unobstructed high-FPS walking retains double precision far from the local origin");
        File.WriteAllLines(Path.Combine(output,"checks.txt"),report);paused=false;
    }
}
