using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class HyperAmplificationTests
{
    public static void Lod(string path)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var terrain=new LocalTerrain(world,horizontalScale:.1);
        var at=TerrainMapViewport.FromDegrees(35.6,-83.5);
        var frame=new LocalTerrainFrame(at,terrain.RadiusM,terrain.Patch(at).Sample(0,0).ElevationM,.1);
        LocalTerrainMesh.Build(terrain,frame,3*3276.8,-4*3276.8,3276.8,16);
        var plan=TerrainLod.Plan(0,0,100,200000,.1);int done=0;
        Parallel.ForEach(plan.OrderBy(p=>TerrainLod.DistanceSquared(p.Key,0,0)),new ParallelOptions{MaxDegreeOfParallelism=2},pair=>
        {
            var key=pair.Key;
            try{LocalTerrainMesh.Build(terrain,frame,key.X*key.SizeM,key.Z*key.SizeM,key.SizeM,pair.Value,default,TerrainLod.EdgeSteps(key,plan));}
            catch(Exception error){throw new Exception($"LOD tile {key.X},{key.Z},level{key.Level},size{key.SizeM},segments{pair.Value}: {error.Message}",error);}
            int n=Interlocked.Increment(ref done);if(n%50==0)Console.WriteLine($"LOD {n}/{plan.Count}; memory {GC.GetTotalMemory(false)/1048576.0:F0}MiB");
        });
        Console.WriteLine("PASS all distant/near LOD tiles: "+done);
    }
    public static void Review(string path, string output, bool fresh)
    {
        Directory.CreateDirectory(output);
        var world = WorldFile.Read(File.ReadAllBytes(path)); var watch = Stopwatch.StartNew();
        if (fresh) world = new WorldPipeline().Generate(world.Recipe, p => Console.WriteLine($"{watch.Elapsed.TotalSeconds:F2}s {p.Stage}"));
        double generation = fresh ? watch.Elapsed.TotalSeconds : 0;
        string hash = StateHash.Compute(world); long memory = GC.GetTotalMemory(true); watch.Restart();
        var terrain = new LocalTerrain(world, horizontalScale: .1);
        Console.WriteLine($"Prepared {world.Surface.Cells.Length} cells / {terrain.Amplification?.RootFaces} base faces in {watch.Elapsed.TotalSeconds:F3}s; generation {generation:F3}s; managed retention {(GC.GetTotalMemory(true)-memory)/1048576.0:F1}MiB");
        if (terrain.Amplification == null) throw new Exception("Full world did not activate hyper-amplification.");
        int count = 0;
        void Check(bool ok, string message) { count++; if (!ok) throw new Exception(message); }
        var sites = world.Recipe.TerrainSource == "Earth" ? new[] {
            ("himalayas",TerrainMapViewport.FromDegrees(27.98,86.92)),
            ("andes",TerrainMapViewport.FromDegrees(-32.65,-70.01)),
            ("appalachians",TerrainMapViewport.FromDegrees(35.6,-83.5)),
            ("hill-country",TerrainMapViewport.FromDegrees(30.2,-99.0)),
            ("lowland",TerrainMapViewport.FromDegrees(30.5,31.2)),
            ("plateau",TerrainMapViewport.FromDegrees(32,88))
        } : Enumerable.Range(0, world.Surface.Cells.Length).Where(i=>world.ElevationM[i]>30)
            .OrderBy(i=>world.ElevationM[i]).Where((i,k)=>k%Math.Max(1,world.Surface.Cells.Length/20)==0).Take(6)
            .Select(i=>("generated-"+i,world.Surface.Cells[i].Position)).ToArray();
        var rows = new List<string>{"site,x,z,height,water,mask"};
        foreach (var (name, at) in sites)
        {
            watch.Restart();var p = terrain.Patch(at, 24000);
            var frame = new LocalTerrainFrame(at, terrain.RadiusM,0,.1);
            var other = terrain.Patch(frame.Position(150,0),24000);
            double low = double.MaxValue, high = double.MinValue;
            for (int z = -12; z <= 12; z++) for (int x = -12; x <= 12; x++)
            {
                var v = frame.Position(x*60,z*60); var s = p.Sample(v); var t = other.Sample(v);
                Check(double.IsFinite(s.ElevationM),"Finite detailed terrain");
                Check(s.ElevationM==t.ElevationM && s.WaterSurfaceM.Equals(t.WaterSurfaceM),$"Overlapping patches disagree at {name}/{x},{z}: {s.ElevationM:R} vs {t.ElevationM:R}");
                Check(!s.Underwater || double.IsFinite(s.WaterSurfaceM),"Wet terrain has a finite water surface");
                low=Math.Min(low,s.ElevationM);high=Math.Max(high,s.ElevationM);
                rows.Add($"{name},{x*60},{z*60},{s.ElevationM:R},{s.WaterSurfaceM:R},{s.WaterMaskM:R}");
            }
            Console.WriteLine($"{name}: {low:F1}..{high:F1}m, range {high-low:F1}m, samples {watch.Elapsed.TotalMilliseconds:F1}ms, groups {terrain.Amplification.SubdivisionGroups}");
            watch.Restart();
            var mesh=LocalTerrainMesh.Build(terrain,frame,0,0,204.8,128);
            Console.WriteLine($"  near tile: {watch.Elapsed.TotalMilliseconds:F1}ms, {mesh.Positions.Length/3} vertices, {mesh.WaterTriangles.Length/3} water triangles");
        }
        File.WriteAllLines(Path.Combine(output,"probes.csv"),rows);
        // Query-order/cache lifetime must not become part of the world seed.
        var stable=new List<(V At,double Height,double Water)>();
        foreach(var site in sites.Take(3))
        {
            var frame=new LocalTerrainFrame(site.Item2,terrain.RadiusM,0,.1);
            var patch=terrain.Patch(site.Item2,24000);
            for(int j=0;j<24;j++)
            {var at=frame.Position(j*53-600,j*37-450);var s=patch.Sample(at);stable.Add((at,s.ElevationM,s.WaterSurfaceM));}
        }
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        Parallel.ForEach(stable.AsEnumerable().Reverse(),sample=>
        {
            var s=terrain.Patch(sample.At,2000).Sample(sample.At);
            if(s.ElevationM!=sample.Height||!s.WaterSurfaceM.Equals(sample.Water))
                throw new Exception($"Cache eviction/concurrent query order changed terrain or water: {sample.Height:R}/{sample.Water:R} -> {s.ElevationM:R}/{s.WaterSurfaceM:R} at {sample.At.X:R},{sample.At.Y:R},{sample.At.Z:R}.");
        });
        Check(true,"Concurrent cache regeneration preserves the canonical surface");
        for(int j=0;j<32;j++)
        {
            int t=(j*7919)%(world.Surface.Triangles.Length/3)*3;
            V a=world.Surface.Cells[world.Surface.Triangles[t]].Position,b=world.Surface.Cells[world.Surface.Triangles[t+1]].Position;
            V mid=(a+b).Unit,normal=V.Cross(a,b).Unit;
            var p=terrain.Patch(mid,2000);var q=terrain.Patch((mid+normal*.00001).Unit,2000);
            var near=new List<double>();
            foreach(double side in new[]{-1d,1d})
            {
                var at=(mid+normal*(side*.02/(terrain.RadiusM*.1))).Unit;
                var x=p.Sample(at);var y=q.Sample(at);
                Check(x.ElevationM==y.ElevationM&&x.WaterSurfaceM.Equals(y.WaterSurfaceM),"Shared coarse-edge point lookup is independent of patch origin");
                near.Add(x.ElevationM);
            }
            Check(Math.Abs(near[0]-near[1])<.5,"No coarse-face height cliff across a four-centimetre crossing");
        }
        foreach(int i in Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>world.Hydrology.LakeCoverageFraction[i]>.8&&world.Hydrology.WaterBody[i]>=0).Take(16))
        {
            int body=world.Hydrology.WaterBody[i];var s=terrain.Patch(world.Surface.Cells[i].Position,2000).Sample(0,0);
            Check(s.Underwater,"Known full-lake control retains its basin water");
            Check(s.WaterSurfaceM==world.Hydrology.Waters[body].SurfaceElevationM,"Standing lake water inherits a single exact source level");
        }
        foreach(var reach in terrain.Hydrology.Channels.Where(c=>c.StartM>20&&c.StartM<3000&&c.WidthM>10).Take(24))
        {
            double previous=double.PositiveInfinity;int wet=0;
            for(int k=1;k<16;k++)
            {
                var at=terrain.Amplification.RiverPoint(reach.From,k/16.0);
                var s=terrain.Patch(at,2000).Sample(0,0);
                Check(double.IsFinite(s.ElevationM),"Finite actual river samples");
                Check(s.ElevationM<=s.PreWaterElevationM+.002,"Valley profile cannot raise a causeway");
                if(s.Underwater)wet++;
                Check(double.IsNaN(s.WaterSurfaceM)||s.WaterSurfaceM<=previous+.1,$"Actual river flows uphill at {reach.From}/{k}: {s.WaterSurfaceM} after {previous}");
                if(!double.IsNaN(s.WaterSurfaceM))previous=s.WaterSurfaceM;
            }
            Console.WriteLine($"river {reach.From}: {wet}/15 wet samples");
            Check(wet>=12,"Major channel must have water over its bed along its actual curved route");
        }
        Check(StateHash.Compute(world)==hash,"Amplification must not mutate saved geography/history");
        Check(terrain.Amplification.CachedGroups<=32768,"Subdivision working set is bounded");
        Console.WriteLine($"PASS {count} hyper-amplification checks; retained managed total {GC.GetTotalMemory(true)/1048576.0:F1}MiB");
    }
}
