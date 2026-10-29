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
