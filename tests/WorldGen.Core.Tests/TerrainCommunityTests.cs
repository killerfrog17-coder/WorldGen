using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class TerrainCommunityTests
{
    public static void Run(string path)
    {
        var world=WorldFile.ReadFile(path);
        var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var biomes=Enumerable.Range(0,world.Surface.Cells.Length).Select(i=>terrain.Profile(i).Biome).ToArray();
        var chosen=new HashSet<int>{108692,145551,130822};
        // Cover every available biome plus coast-adjacent cells and their ocean
        // neighbours. These include the refined-shoreline fallback, not only
        // the simple inland fast path.
        foreach(var group in biomes.Select((b,i)=>(b,i)).GroupBy(p=>p.b))chosen.Add(group.First().i);
        foreach(var c in world.Surface.Cells.Where(c=>biomes[c.Id]!=Biome.Ocean&&c.Neighbors.Any(n=>biomes[n]==Biome.Ocean)).Take(12))
        {chosen.Add(c.Id);foreach(int n in c.Neighbors.Where(n=>biomes[n]==Biome.Ocean).Take(1))chosen.Add(n);}
        int count=0,ocean=0;double originalMs=0,queryMs=0;long queryBytes=0;
        foreach(int cell in chosen.Where(c=>c<world.Surface.Cells.Length))
        {
            var center=world.Surface.Cells[cell].Position;var patch=terrain.Patch(center,30000);
            var points=(from x in new[]{-18000d,-7000,0,7000,18000} from z in new[]{-18000d,-7000,0,7000,18000} select patch.Position(x,z)).ToArray();
            var expected=new int[points.Length];var watch=Stopwatch.StartNew();
            for(int i=0;i<points.Length;i++)expected[i]=patch.Sample(points[i]).Cell;
            originalMs+=watch.Elapsed.TotalMilliseconds;
            patch.CommunityCell(points[0]);long before=GC.GetAllocatedBytesForCurrentThread();watch.Restart();
            for(int i=0;i<points.Length;i++)
            {
                int actual=patch.CommunityCell(points[i]);
                if(actual!=expected[i])throw new Exception($"Community changed at source {cell}, probe {i}: {expected[i]} -> {actual}");
                count++;if(biomes[actual]==Biome.Ocean)ocean++;
            }
            queryMs+=watch.Elapsed.TotalMilliseconds;queryBytes+=GC.GetAllocatedBytesForCurrentThread()-before;
        }
        if(ocean==0||count<500)throw new Exception("Insufficient actual-world coverage");
        Console.WriteLine($"PASS {count} literal full-sample community comparisons across {chosen.Count} patches, {ocean} ocean results; full {originalMs:F3}ms, community {queryMs:F3}ms, query bytes {queryBytes}. Coast queries retain full sampling; no allocation claim for those.");
    }
}
