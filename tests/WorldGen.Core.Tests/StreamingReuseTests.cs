using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class StreamingReuseTests
{
    public static void Run(string path)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var discarded=new List<int>();var cache=new TerrainLru<int,int>(10,2,discarded.Add);
        cache.Put(1,11,4);cache.Put(2,22,4);Check(cache.Take(1,out int value)&&value==11&&discarded.Count==0,"Cache hit transfers exact object without disposal");
        cache.Put(1,11,4);cache.Put(3,33,4);Check(!cache.Contains(2)&&discarded.SequenceEqual(new[]{22}),"Oldest inactive product evicted at count bound");
        cache.Put(4,44,7);Check(cache.Bytes<=10&&cache.Count==1&&cache.Contains(4),"Byte budget enforced even below count budget");
        cache.Put(5,55,100);Check(discarded.Contains(55)&&!cache.Contains(5),"Oversized product is never retained");cache.Clear();Check(cache.Bytes==0&&cache.Count==0,"Frame invalidation empties cache");
        foreach(double scale in new[]{.01,.1,1d,2d})foreach(double altitude in new[]{0d,3000d})foreach(double x in new[]{-1721d,97d})
        {
            var plan=TerrainLod.Plan(x,-55,altitude,200000,scale);var watch=System.Diagnostics.Stopwatch.StartNew();var edges=TerrainEdges.Build(plan);double fast=watch.Elapsed.TotalMilliseconds;
            foreach(var p in plan){var old=TerrainLod.EdgeSteps(p.Key,plan);if(!edges[p.Key].SequenceEqual(old))Console.WriteLine($"Mismatch scale {scale}, tile {p.Key.X},{p.Key.Z} L{p.Key.Level}: new [{string.Join(",",edges[p.Key])}], old [{string.Join(",",old)}]");Check(edges[p.Key].SequenceEqual(old),"Indexed stitch plan is exactly the old all-pairs result");}
            Console.WriteLine($"Scale {scale}, altitude {altitude}: {plan.Count} tiles, indexed edges {fast:F2}ms");
        }
        var key=new TerrainTileKey(-1,2,0,.1);Check(!new TerrainProductKey(key,64,new double[4]).Equals(new TerrainProductKey(key,64,new[]{0d,1d,0d,0d})),"Cached edge variants never alias");
        var small=new TerrainSampleCache(2);var a=new V(1,0,0);var b=new V(0,1,0);var c=new V(0,0,1);
        small.Store(a,new LocalTerrainSample{ElevationM=12});small.Store(b,new LocalTerrainSample{ElevationM=24});Check(small.TryGet(a,out var s)&&s.ElevationM==12,"Point cache preserves exact sample");
        small.Store(c,new LocalTerrainSample());Check(small.Count==2&&!small.TryGet(a,out _),"Point sample cache is count bounded");
        var world=WorldFile.Read(File.ReadAllBytes(path));string hash=StateHash.Compute(world);var terrain=new LocalTerrain(world,horizontalScale:.1);
        var sampleCache=new TerrainSampleCache();var at=terrain.Amplification.RiverPoint(terrain.Hydrology.Channels.First(t=>t.WidthM>10&&t.StartM>10).From,.5);
        var frame=new LocalTerrainFrame(at,terrain.RadiusM,0,.1);
        foreach(var edges in new[]{new double[4],new[]{12.8,0d,0d,12.8}})
        {
            var expected=LocalTerrainMesh.Build(terrain,frame,0,0,204.8,32,edgeSteps:edges);
            var watch=System.Diagnostics.Stopwatch.StartNew();var actual=LocalTerrainMesh.Build(terrain,frame,0,0,204.8,32,edgeSteps:edges,sampleCache:sampleCache);double first=watch.Elapsed.TotalMilliseconds;
            watch.Restart();var warm=LocalTerrainMesh.Build(terrain,frame,0,0,204.8,32,edgeSteps:edges,sampleCache:sampleCache);
            foreach(var pair in new[]{(actual,expected),(warm,expected)})
            {
                foreach(var arrays in new[]{(pair.Item1.Positions,pair.Item2.Positions),(pair.Item1.Normals,pair.Item2.Normals),(pair.Item1.Colors,pair.Item2.Colors),(pair.Item1.SurfaceWeights,pair.Item2.SurfaceWeights),(pair.Item1.GroundWeightsB,pair.Item2.GroundWeightsB),(pair.Item1.Lithology,pair.Item2.Lithology),(pair.Item1.WaterPositions,pair.Item2.WaterPositions),(pair.Item1.WaterColors,pair.Item2.WaterColors)})Check(arrays.Item1.SequenceEqual(arrays.Item2),"Cached/uncached mesh floats identical");
                Check(pair.Item1.Triangles.SequenceEqual(pair.Item2.Triangles)&&pair.Item1.WaterTriangles.SequenceEqual(pair.Item2.WaterTriangles)&&pair.Item1.WaterClipTriangles.SequenceEqual(pair.Item2.WaterClipTriangles),"Cached/uncached land and water topology identical");
            }
            Console.WriteLine($"Actual river tile: cold cache {first:F1}ms / warm {watch.Elapsed.TotalMilliseconds:F1}ms; {sampleCache.Hits} hits, {sampleCache.Count} entries");
        }
        Check(sampleCache.Hits>1000&&sampleCache.Count<=32768,"Actual mesh requests reuse bounded exact samples");
        Check(StateHash.Compute(world)==hash,"Streaming reuse leaves source world/history unchanged");
        Console.WriteLine("PASS "+checks+" exact streaming/cache/stitch checks; unchanged "+hash);
    }
}
