using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class TerrainQueryCostTests
{
    public static void Run(string input)
    {
        var world=WorldFile.ReadFile(input);var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var center=TerrainMapViewport.FromDegrees(27.068410281540114,-167.35387593971296);
        var frame=new LocalTerrainFrame(center,terrain.RadiusM,0,.1);var patch=terrain.Patch(center,20480);
        var rows=new List<(double elapsed,double x,double z,double[] phases)>();int comparisons=0;double total=0;
        for(int z=-20;z<=20;z++)for(int x=-20;x<=20;x++)
        {
            var point=frame.Position(x*25,z*25);TerrainQueryTiming.Enabled=true;TerrainQueryTiming.Clear();var watch=Stopwatch.StartNew();
            var sample=patch.Sample(point);double elapsed=watch.Elapsed.TotalMilliseconds;total+=elapsed;
            var phases=Enumerable.Range(0,6).Select(TerrainQueryTiming.Milliseconds).ToArray();rows.Add((elapsed,x*25,z*25,phases));
            TerrainQueryTiming.Enabled=false;var repeated=patch.Sample(point);
            if(!sample.Equals(repeated))throw new Exception("Query instrumentation changed canonical sample");comparisons++;
        }
        Console.WriteLine($"PASS {comparisons} exact instrumented/uninstrumented samples; first-query total {total:F3}ms. Component microbenchmark, NOT game FPS. Phases inclusive when pond surveys recurse.");
        Console.WriteLine("elapsed_ms,x,z,locate_ms,profiles_ms,shape_ms,rivers_ms,detail_ms,ponds_ms");
        foreach(var row in rows.OrderByDescending(r=>r.elapsed).Take(20))Console.WriteLine(string.Join(",",new[]{row.elapsed,row.x,row.z}.Concat(row.phases).Select(v=>v.ToString("F4",System.Globalization.CultureInfo.InvariantCulture))));
    }
}
