using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

// Mixed map/physical sampling must not inherit a finer query's face hint.
// This is an actual-world regression, not an alternate terrain implementation.
static class TerrainFootprintOrderTests
{
    public static void Run(string input,string output)
    {
        Directory.CreateDirectory(output);
        var world=WorldFile.ReadPlanetaryFile(input);
        var model=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var sites=new[]{
            ("river-bank",new V(-.042102725,-.875960391,-.480542145)),
            ("fresh-marsh",new V(-.477462766,.654419239,-.586314563)),
            ("tidal-mangrove",new V(-.876280631,.404619675,-.261562942)),
            ("crescent-dunes",new V(.132257016,.908432958,.396557236)),
            ("jungle",new V(.381914478,.918764738,-.100063418))};
        bool Equal(double a,double b)=>a==b||double.IsNaN(a)&&double.IsNaN(b);
        bool Same(LocalTerrainSample a,LocalTerrainSample b)=>Equal(a.ElevationM,b.ElevationM)&&
            Equal(a.PreWaterElevationM,b.PreWaterElevationM)&&Equal(a.WaterSurfaceM,b.WaterSurfaceM)&&
            Equal(a.WaterMaskM,b.WaterMaskM)&&Equal(a.WaterFootprintM,b.WaterFootprintM)&&
            a.WaterKind==b.WaterKind&&a.WaterBodyKey==b.WaterBodyKey&&a.Biome==b.Biome&&a.Cell==b.Cell;
        var rows=new List<object>();var neighbourFailures=new List<object>();int checks=0,failures=0;double largest=0;
        foreach(var (name,at) in sites)foreach(double spacing in new[]{129d,512,2048})
        {
            var cold=model.Patch(at,5000).SampleMap(at,spacing);
            var patch=model.Patch(at,5000);var physical=patch.Sample(at);
            var afterFine=patch.SampleMap(at,spacing);
            var afterRepeat=patch.SampleMap(at,spacing);
            patch.SampleMap(at,spacing*2);
            var afterCoarser=patch.SampleMap(at,spacing);
            var physicalAgain=patch.Sample(at);
            bool fine=Same(cold,afterFine),repeat=Same(cold,afterRepeat),coarser=Same(cold,afterCoarser),full=Same(physical,physicalAgain);
            checks+=4;failures+=new[]{fine,repeat,coarser,full}.Count(ok=>!ok);
            largest=Math.Max(largest,Math.Max(Math.Abs(cold.ElevationM-afterFine.ElevationM),Math.Abs(cold.ElevationM-afterCoarser.ElevationM)));
            rows.Add(new{Name=name,SpacingM=spacing,ColdM=cold.ElevationM,AfterFineM=afterFine.ElevationM,
                AfterRepeatM=afterRepeat.ElevationM,AfterCoarserM=afterCoarser.ElevationM,
                PhysicalM=physical.ElevationM,PhysicalAgainM=physicalAgain.ElevationM,
                FineOrderExact=fine,RepeatExact=repeat,CoarserOrderExact=coarser,PhysicalExact=full});
        }
        foreach(var (name,at) in sites)foreach(double spacing in new[]{129d,512,2048})
        {
            var frame=new WorldGen.Presentation.LocalTerrainFrame(at,model.RadiusM,0,.1);
            var points=(from z in Enumerable.Range(-2,5) from x in Enumerable.Range(-2,5) select frame.Position(x*50,z*50)).ToArray();
            var expected=points.Select(p=>model.Patch(p,5000).SampleMap(p,spacing)).ToArray();
            var forward=model.Patch(at,5000);var reverse=model.Patch(at,5000);
            foreach(bool reversed in new[]{false,true})for(int j=0;j<points.Length;j++)
            {
                int i=reversed?points.Length-1-j:j;var patch=reversed?reverse:forward;
                if(!reversed)patch.Sample(points[i]);
                var actual=patch.SampleMap(points[i],spacing);checks++;
                if(Same(expected[i],actual))continue;
                failures++;largest=Math.Max(largest,Math.Abs(expected[i].ElevationM-actual.ElevationM));
                neighbourFailures.Add(new{Name=name,SpacingM=spacing,Index=i,Reversed=reversed,ExpectedM=expected[i].ElevationM,ActualM=actual.ElevationM});
            }
        }
        File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{checks,failures,LargestHeightDifferenceM=largest,Rows=rows,NeighbourFailures=neighbourFailures},new JsonSerializerOptions{WriteIndented=true}));
        if(failures>0)throw new Exception($"Map footprint depends on query order: {failures}/{checks} mismatches, max {largest:R}m");
        Console.WriteLine($"PASS {checks} actual mixed map/physical footprint-order checks");
    }
}
