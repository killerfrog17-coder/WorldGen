using System.Diagnostics;
using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;

static class CoverageAncestorTests
{
    static HashSet<T> Partial<T>(object coverage) => (HashSet<T>)coverage.GetType()
        .GetField("partial",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(coverage);
    public static void Run()
    {
        int checks=0;
        void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        var rng=new Random(84451);
        var terrain=new TerrainCoverageFootprint(Array.Empty<TerrainTileKey>());
        // Independent exhaustive ancestor union: no early exit and no reuse.
        // Cover negative coordinates, mixed scales, duplicates, high levels,
        // insertion order and clearing/rebuilding a previously occupied cache.
        for(int trial=0;trial<160;trial++)
        {
            var keys=new List<TerrainTileKey>();
            for(int i=0;i<(trial%17==0?0:350);i++)
            {
                var k=new TerrainTileKey(rng.Next(-80,80),rng.Next(-80,80),rng.Next(0,24),i%3==0?.1:1);
                keys.Add(k);if(i%5==0)keys.Add(k);
            }
            var expected=new HashSet<TerrainTileKey>();
            foreach(var k in keys)for(int depth=1;depth<=23-k.Level;depth++)
                expected.Add(new TerrainTileKey(k.X>>depth,k.Z>>depth,k.Level+depth,k.Scale));
            terrain.Reset(keys);
            Check(Partial<TerrainTileKey>(terrain).SetEquals(expected),"Terrain ancestor union changed");
            keys.Reverse();terrain.Reset(keys);
            Check(Partial<TerrainTileKey>(terrain).SetEquals(expected),"Terrain ancestor union depends on order");
            var forestKeys=new List<ForestPageKey>();
            for(int i=0;i<(trial%17==0?0:350);i++)
                forestKeys.Add(new ForestPageKey(i%6,rng.Next(0,80),rng.Next(0,80),rng.Next(0,25)));
            var forestExpected=new HashSet<ForestPageKey>();
            foreach(var k in forestKeys)for(int depth=1;depth<=24-k.Level;depth++)
                forestExpected.Add(new ForestPageKey(k.Face,k.X>>depth,k.Y>>depth,k.Level+depth));
            var forest=new ForestCoverageFootprint(forestKeys,65536);
            Check(Partial<ForestPageKey>(forest).SetEquals(forestExpected),"Forest ancestor union changed");
            forestKeys.Reverse();forest=new ForestCoverageFootprint(forestKeys,65536);
            Check(Partial<ForestPageKey>(forest).SetEquals(forestExpected),"Forest ancestor union depends on order");
        }
        var plan=TerrainLod.Plan(0,0,6000,scale:.1);
        var surface=plan.Where((p,i)=>i%5!=0).ToDictionary(p=>p.Key,p=>p.Value);
        var footprint=new TerrainQualityFootprint(surface);
        for(int i=0;i<50;i++){footprint.Reset(surface);footprint.NearestGap(plan,0,0,5880);}
        var watch=Stopwatch.StartNew();double checksum=0;
        for(int i=0;i<1000;i++){footprint.Reset(surface);checksum+=footprint.NearestGap(plan,i%100,0,5880);}
        watch.Stop();Console.WriteLine($"Quality footprint: {watch.Elapsed.TotalMilliseconds:F3} ms /1000 rebuild+query; checksum {checksum:R}");
        Console.WriteLine($"PASS {checks} exhaustive ancestor-set comparisons. Fixture timing is not game FPS.");
    }
}
