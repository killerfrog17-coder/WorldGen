using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Serialization;

static class ForestBudgetTests
{
    public static void Run(string path,double range=60000)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));string before=StateHash.Compute(world);var terrain=new LocalTerrain(world,horizontalScale:.1);var scatter=new LocalScatter(terrain,.1);
        foreach(bool hills in new[]{false,true})
        {
            int cell=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>world.ElevationM[i]>30&&world.ElevationM[i]<3200&&world.Ecology.Habitat[i].Biome==Biome.TropicalRainforest).OrderBy(i=>hills?-world.Ecology.Habitat[i].ReliefM:world.Ecology.Habitat[i].ReliefM).First();
            var watch=Stopwatch.StartNew();var plan=scatter.PlanForest(world.Surface.Cells[cell].Position,range,606,4096,120);int capacityBound=0,sourceRoots=0,probes=0,empty=0;var ids=new HashSet<ulong>();
            Console.WriteLine($"cell{cell}: plan {plan.Length} pages; levels "+string.Join(",",plan.GroupBy(k=>k.Level).Select(g=>$"L{g.Key}:{g.Count()}")));
            for(int i=0;i<plan.Length;i++)
            {
                var page=scatter.BuildForest(plan[i]);sourceRoots+=page.Roots.Length;
                // A regional descriptor expands into anchors that may each
                // retain a hidden fallback beside the stand. This is a bound
                // on resident records, not the number of visible trees.
                foreach(var root in page.Roots)capacityBound+=LocalScatter.ForestCanopyAnchorCount(root)*(LocalScatter.ForestCanopyUsesStand(root)?LocalScatter.MaximumForestRepresentationsPerAnchor:1);
                probes+=page.Probes;if(page.Roots.Length==0)empty++;
                foreach(var p in page.Roots)if(!ids.Add(p.Tree.Id))throw new Exception("Duplicate representative across disjoint pages");
                if(capacityBound>LocalScatter.MaximumForestRoots)throw new Exception("Forest resident-record capacity bound exceeds the runtime pool before coverage completed");
                if(i%100==0)Console.WriteLine($"{i}/{plan.Length}: at most {capacityBound} resident root records / {sourceRoots} descriptors, {watch.Elapsed.TotalSeconds:F2}s");
            }
            Console.WriteLine($"PASS cell{cell}: at most {capacityBound} resident root records / {sourceRoots} descriptors, {empty} empty pages, {probes} probes; {watch.Elapsed.TotalSeconds:F2}s; full {range/1000}km hierarchy fits capacity bound, no duplicate IDs");
        }
        if(StateHash.Compute(world)!=before)throw new Exception("Forest budget audit mutated source world");Console.WriteLine("PASS source unchanged "+before);
    }
}
