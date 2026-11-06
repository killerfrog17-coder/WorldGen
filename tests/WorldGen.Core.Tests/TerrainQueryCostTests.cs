using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class TerrainQueryCostTests
{
    public static void VerifyWorld(string input)
    {
        var world=WorldFile.ReadFile(input);var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var biomes=Enumerable.Range(0,world.Surface.Cells.Length).Select(i=>terrain.Profile(i).Biome).ToArray();
        var selected=new HashSet<int>{108692,145551,130822};
        foreach(var group in biomes.Select((b,i)=>(b,i)).GroupBy(p=>p.b))selected.Add(group.First().i);
        foreach(var cell in world.Surface.Cells.Where(c=>biomes[c.Id]!=Biome.Ocean&&c.Neighbors.Any(n=>biomes[n]==Biome.Ocean)).Take(12))
        {selected.Add(cell.Id);selected.Add(cell.Neighbors.First(n=>biomes[n]==Biome.Ocean));}
        int compared=0,wet=0,spanCount=0;
        foreach(int cell in selected)
        {
            var center=world.Surface.Cells[cell].Position;var patch=terrain.Patch(center,20480);
            var points=(from x in new[]{-18000d,-7000,0,7000,18000} from z in new[]{-18000d,-7000,0,7000,18000} select patch.Position(x,z)).ToArray();
            var expected=points.Select(patch.Sample).ToArray();spanCount+=patch.RiverSpans().Length;
            for(int i=0;i<points.Length;i++)
            {
                var actual=patch.Sample(points[i]);if(!actual.Equals(expected[i]))throw new Exception($"Indexed query changed full sample in cell {cell}, probe {i}");
                compared++;if(actual.Underwater)wet++;
            }
        }
        if(wet==0||spanCount==0)throw new Exception("Missing native water/river coverage");
        Console.WriteLine($"PASS {compared} full literal/indexed samples across {selected.Count} actual biome/coastal patches; {wet} wet samples and {spanCount} indexed spans. Every sample field is unchanged.");
    }
    public static void Run(string input)
    {
        var world=WorldFile.ReadFile(input);var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var center=TerrainMapViewport.FromDegrees(27.068410281540114,-167.35387593971296);
        var frame=new LocalTerrainFrame(center,terrain.RadiusM,0,.1);var patch=terrain.Patch(center,20480);
        var rows=new List<(double elapsed,double x,double z,double[] phases)>();var expected=new List<LocalTerrainSample>();int comparisons=0;double total=0;
        var probes=new List<(double x,double z)>();for(int step=0;step<=2000;step++)probes.Add((step*.25,0));
        for(int z=-20;z<=20;z++)for(int x=-20;x<=20;x++)probes.Add((x*25,z*25));
        foreach(var probe in probes)
        {
            var point=frame.Position(probe.x,probe.z);TerrainQueryTiming.Enabled=true;TerrainQueryTiming.Clear();var watch=Stopwatch.StartNew();
            var sample=patch.Sample(point);expected.Add(sample);double elapsed=watch.Elapsed.TotalMilliseconds;total+=elapsed;
            var phases=Enumerable.Range(0,6).Select(TerrainQueryTiming.Milliseconds).ToArray();rows.Add((elapsed,probe.x,probe.z,phases));
            TerrainQueryTiming.Enabled=false;var repeated=patch.Sample(point);
            if(!sample.Equals(repeated))throw new Exception("Query instrumentation changed canonical sample");comparisons++;
        }
        var indexWatch=Stopwatch.StartNew();var warmLiteral=new LocalTerrainSample[probes.Count];
        for(int i=0;i<probes.Count;i++)warmLiteral[i]=patch.Sample(frame.Position(probes[i].x,probes[i].z));
        double literalMs=indexWatch.Elapsed.TotalMilliseconds;
        indexWatch.Restart();var spans=patch.RiverSpans();double indexMs=indexWatch.Elapsed.TotalMilliseconds;
        indexWatch.Restart();var indexed=new LocalTerrainSample[probes.Count];
        for(int i=0;i<probes.Count;i++)indexed[i]=patch.Sample(frame.Position(probes[i].x,probes[i].z));
        double indexedMs=indexWatch.Elapsed.TotalMilliseconds;
        for(int i=0;i<probes.Count;i++)if(!indexed[i].Equals(expected[i])||!warmLiteral[i].Equals(expected[i]))throw new Exception("Indexed river query changed sample at "+probes[i]);
        Console.WriteLine($"PASS {probes.Count} complete literal/indexed samples, {spans.Length} river spans; index build {indexMs:F3}ms, warm literal sweep {literalMs:F3}ms, indexed sweep {indexedMs:F3}ms, wet samples {expected.Count(p=>p.Underwater)}.");
        Console.WriteLine($"PASS {comparisons} exact instrumented/uninstrumented samples; first-query total {total:F3}ms. Component microbenchmark, NOT game FPS. Phases inclusive when pond surveys recurse.");
        Console.WriteLine("elapsed_ms,x,z,locate_ms,profiles_ms,shape_ms,rivers_ms,detail_ms,ponds_ms");
        foreach(var row in rows.OrderByDescending(r=>r.elapsed).Take(20))Console.WriteLine(string.Join(",",new[]{row.elapsed,row.x,row.z}.Concat(row.phases).Select(v=>v.ToString("F4",System.Globalization.CultureInfo.InvariantCulture))));
    }
}
