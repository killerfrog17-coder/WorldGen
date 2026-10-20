using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class GeographicScaffoldTests
{
    public static void Review(string path,bool generate)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));
        var watch=Stopwatch.StartNew();
        if(generate)
        {
            world=new WorldPipeline().Generate(world.Recipe,p=>Console.WriteLine($"{watch.Elapsed.TotalSeconds:F2}s {p.Stage}"));
            Console.WriteLine($"Fresh full pipeline: {watch.Elapsed.TotalSeconds:F3}s");
        }
        double generation=generate?watch.Elapsed.TotalSeconds:0;
        string hash=StateHash.Compute(world);long memory=GC.GetTotalMemory(true);watch.Restart();
        Console.WriteLine("Preparing geographic terrain");
        var terrain=new LocalTerrain(world,horizontalScale:.1);
        double preparation=watch.Elapsed.TotalSeconds;
        long retained=GC.GetTotalMemory(true)-memory;
        var g=terrain.Scaffold??throw new Exception("Completed world did not use geographic scaffold");
        Console.WriteLine($"Prepared {g.Positions.Length} controls in {preparation:F3}s; mesh {g.MeshMilliseconds:F0}ms, routing {g.RoutingMilliseconds:F0}ms; total derived-terrain managed retention delta {retained/1048576.0:F1}MiB");
        int assertions=0;
        void Check(bool ok,string message){assertions++;if(!ok)throw new Exception(message);}
        int n=g.Positions.Length;
        Check(n==world.Surface.Cells.Length+world.Surface.Edges.Length,"Shared edge refinement count");
        Check(g.Order.Distinct().Count()==n,"Routing order is a complete permutation");
        var ranks=new int[n];for(int k=0;k<n;k++)ranks[g.Order[k]]=k;
        for(int i=0;i<n;i++)
        {
            int r=g.Receiver[i];
            Check(r<0||ranks[r]<ranks[i],"Drainage is acyclic and destination precedes source");
            Check(r<0||g.Heights[r]<=g.Heights[i]+.000001,"Control channels run downhill");
            Check(double.IsFinite(g.Heights[i])&&double.IsFinite(g.Discharge[i])&&g.Discharge[i]>=0,"Finite geographic controls and nonnegative discharge");
            Check(r<0||g.Discharge[r]+.000001>=g.Discharge[i],"Downstream accumulated discharge cannot shrink");
        }
        Console.WriteLine("Control graph verified");
        // Crossing displaced child edges and original faces must not change the
        // answer according to which streamed patch happens to request it.
        for(int k=0;k<40;k++)
        {
            int t=(k*7919)%(g.Triangles.Length/3)*3;
            V a=g.Positions[g.Triangles[t]],b=g.Positions[g.Triangles[t+1]],c=g.Positions[g.Triangles[t+2]];
            V center=(a+b+c).Unit;var p=terrain.Patch(center,20000);
            var q=terrain.Patch((center+a*.00001).Unit,20000);
            foreach(double f in new[]{.001,.25,.5,.75,.999})
            {
                V v=(center*(1-f)+(a+b).Unit*f).Unit;
                // At 200k the half-edge lies within the indexed test extent.
                if((v-center).Length*terrain.RadiusM>19000)continue;
                var x=p.Sample(v);var y=q.Sample(v);
                Check(x.ElevationM==y.ElevationM&&x.WaterSurfaceM.Equals(y.WaterSurfaceM),"Independent patches agree across shared geographic edges");
            }
        }
        Check(terrain.Hydrology.Channels.All(c=>c.EndM<=c.StartM),"All channel surface profiles descend");
        var channelEnds=new Dictionary<int,double>();
        foreach(var c in terrain.Hydrology.Channels)
        {
            foreach(var end in new[]{(c.From,c.StartM),(c.To,c.EndM)})
            {if(channelEnds.TryGetValue(end.Item1,out double h))Check(h==end.Item2,"Tributaries and downstream reaches meet at exactly one water level");else channelEnds.Add(end.Item1,end.Item2);}
            double previous=c.Level(0);for(int k=1;k<=32;k++){double level=c.Level(k/32.0);Check(level<=previous+.000001,"Sampled channel profile never flows uphill");previous=level;}
        }
        foreach(var c in terrain.Hydrology.Channels.OrderByDescending(c=>c.MaximumLoweringM).Take(24))
        {
            V p=c.Position(.5);var patch=terrain.Patch(p,20000);
            if(terrain.Hydrology.Region(p,20000).ShoreCount>0)continue;
            var sample=patch.Sample(p);
            Check(sample.ElevationM<=sample.PreWaterElevationM+.001,"Actual strongly lowered river cannot raise the uncarved field");
            // An ocean surface is correctly above its submerged original bed.
            if(sample.PreWaterElevationM>=0||sample.OceanInfluence<.001)
                Check(sample.WaterSurfaceM<=sample.PreWaterElevationM+1,$"No elevated runway above the original centerline terrain: channel {c.From}->{c.To}, profile {c.Level(.5):F2}, water {sample.WaterSurfaceM:F2}, raw {sample.PreWaterElevationM:F2}, ocean {sample.OceanInfluence:F4}");
        }
        Console.WriteLine($"River conditioning {terrain.Hydrology.ConditioningMilliseconds:F0}ms; largest profile lowering {terrain.Hydrology.Channels.Max(c=>c.MaximumLoweringM):F1}m");
        Check(terrain.Hydrology.Channels.Any(c=>c.From>=world.Surface.Cells.Length),"Channels actually use independent intermediate vertices");
        foreach(var c in terrain.Hydrology.Channels.Where(c=>c.Headwater).Take(32))
            Check(c.Width(0)==0&&c.Width(.5)>0,"Headwaters taper rather than beginning as full-width runway caps");
        Check(StateHash.Compute(world)==hash,"Geographic preparation and queries preserve authoritative world/history");
        Console.WriteLine($"PASS {assertions} scaffold checks; {world.Recipe.TerrainSource}, {world.Surface.Cells.Length} cells -> {n} controls / {g.Triangles.Length/3} triangles; scaffold {g.PreparationMilliseconds:F0}ms (mesh {g.MeshMilliseconds:F0}, routing {g.RoutingMilliseconds:F0}), all terrain prep {preparation:F3}s, retained scaffold arrays {g.RetainedBytes/1048576.0:F1}MiB, channels {terrain.Hydrology.Channels.Length}; pipeline + terrain {generation+preparation:F3}s (pipeline measured={generate})");
    }
}
