using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class SurfaceTransitionTests
{
    public static void CoverageCache()
    {
        int checks=0;void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        var cache=new TerrainFadeCache();var input=new List<TerrainFadeStamp>();var random=new Random(8128);
        TerrainFadeTree previous=null;float[] previousNodes=Array.Empty<float>(),previousTimes=Array.Empty<float>();
        for(int iteration=0;iteration<500;iteration++)
        {
            var parent=new TerrainTileKey(-2,3,iteration%9==0?5:4,iteration%11==0?.2:.1);
            input.Clear();
            for(int j=0;j<iteration%25;j++)
            {
                int depth=1+random.Next(4),span=1<<depth;
                var child=new TerrainTileKey(parent.X*span+random.Next(span+2),parent.Z*span+random.Next(span+2),parent.Level-depth,parent.Scale);
                input.Add(new TerrainFadeStamp(child,random.Next(8),random.Next(8)));
            }
            var expected=TerrainCoverage.BuildFaded(parent,input);var actual=cache.Update(parent,input);
            Check(expected.Nodes.SequenceEqual(actual.Nodes)&&expected.Timings.SequenceEqual(actual.Timings),"Cached coverage changed the uncached topology or fade data");
            Check(ReferenceEquals(actual,cache.Update(parent,input)),"Identical input did not reuse its output");
            if(previous!=null)Check(previous.Nodes.SequenceEqual(previousNodes)&&previous.Timings.SequenceEqual(previousTimes),"A changed input mutated an earlier published coverage snapshot");
            previous=actual;previousNodes=(float[])actual.Nodes.Clone();previousTimes=(float[])actual.Timings.Clone();
        }
        var tile=new TerrainTileKey(0,0,2,.1);input.Clear();input.Add(new TerrainFadeStamp(new TerrainTileKey(0,0,1,.1),1,0));
        var original=cache.Update(tile,input);input[0]=new TerrainFadeStamp(input[0].Key,2,3);
        var changed=cache.Update(tile,input);
        Check(!ReferenceEquals(original,changed)&&original.Timings[0]==1&&changed.Timings[0]==2&&changed.Timings[1]==3,"Fade-only edits were ignored or overwrote live output");
        for(int i=0;i<100;i++)cache.Update(tile,input);
        long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)cache.Update(tile,input);
        Check(GC.GetAllocatedBytesForCurrentThread()-before<=256,"Unchanged resident coverage still allocates per update");
        Console.WriteLine($"PASS {checks} coverage cache checks: exact uncached topology/timings, changed key/scale/fades, immutable prior outputs, and allocation-free steady updates.");
    }
    public static void Run(string path)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        Check(TerrainCoverage.Fade(1,1,0)==0&&TerrainCoverage.Fade(2.5,1,0)==.5&&TerrainCoverage.Fade(4,1,0)==1,"Three-second smooth incoming fade");
        Check(TerrainCoverage.Fade(1,0,1)==1&&TerrainCoverage.Fade(2.5,0,1)==.5&&TerrainCoverage.Fade(4,0,1)==0,"Outgoing complement");
        var parent=new TerrainTileKey(-1,-1,2,.1);var child=new TerrainTileKey(-2,-2,1,.1);var nested=new TerrainTileKey(-4,-4,0,.1);
        var tree=TerrainCoverage.BuildFaded(parent,new[]{new TerrainFadeStamp(nested,2),new TerrainFadeStamp(child,1)});
        Check(!TerrainCoverage.Visible(tree.Nodes,.1,.1)&&TerrainCoverage.Visible(tree.Nodes,.9,.9),"Faded coverage has literal partial footprint");
        Check(tree.Timings.Length==4&&tree.Timings[0]==1,"Coarser descendant supplies union fade over capped children");
        foreach(var kind in new[]{ScatterKind.Broadleaf,ScatterKind.Conifer,ScatterKind.SavannaTree,ScatterKind.Shrub,ScatterKind.Boulder,ScatterKind.Stone,ScatterKind.Grass})
        {
            double prior=1;for(double d=0;d<=2000;d+=2)
            {var b=ScatterLodBlend.At(kind,2,d);Check(b.Blend>=0&&b.Blend<=1&&b.Visibility>=0&&b.Visibility<=prior+1e-12,"Bounded LOD blend and distance fade");prior=b.Visibility;}
        }
        var world=WorldFile.Read(File.ReadAllBytes(path));string hash=StateHash.Compute(world);var terrain=new LocalTerrain(world,horizontalScale:.1);
        int cell=Enumerable.Range(0,world.ElevationM.Length).Where(i=>world.ElevationM[i]>200&&terrain.Profile(i).MountainHeightM>50).First();
        var center=world.Surface.Cells[cell].Position;var patch=terrain.Patch(center,10000);var other=terrain.Patch(patch.Position(200,100),10000);
        int changed=0,wet=0;double maximum=0;var watch=System.Diagnostics.Stopwatch.StartNew();
        for(int z=-20;z<=20;z++)for(int x=-20;x<=20;x++)
        {
            var point=patch.Position(x*18,z*18);var a=patch.Sample(point);var b=other.Sample(point);
            Check(a.ElevationM==b.ElevationM&&a.WaterSurfaceM.Equals(b.WaterSurfaceM),"Independent patches return exactly the same smooth height/water");
            double delta=Math.Abs(a.ElevationM-a.PreWaterElevationM);
            if(a.WaterKind==LocalWaterKind.None){maximum=Math.Max(maximum,delta);if(delta>.005)changed++;Check(delta<1.81,"Fine reconstruction/detail are bounded, not new mountains");}
            if(a.Underwater){wet++;Check(a.ElevationM<a.WaterSurfaceM,"Wet ground remains below unchanged water");}
        }
        Check(changed>20,"Actual mountain has non-planar sub-triangle detail");Check(StateHash.Compute(world)==hash,"No authoritative generation/history changes");
        Console.WriteLine($"PASS {checks} surface/transition checks; changed {changed}, wet {wet}, max fine delta {maximum:F3}m; actual 3362 samples {watch.Elapsed.TotalSeconds:F2}s; source {hash}");
    }
}
