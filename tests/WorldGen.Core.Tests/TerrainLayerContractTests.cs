using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class TerrainLayerContractTests
{
    public static void Run(string path,string output)
    {
        var world=WorldFile.ReadPlanetaryFile(path);var model=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);int checks=0;
        var fields=new[]{"ElevationM","PreWaterElevationM","BaseElevationM","WaterSurfaceM","WaterMaskM","RiverInfluence","RiverSand","RiverGravel","WaterKind","WaterBodyKey","WaterGradient","WaterFootprintM","WaterFlow","WaterSpeedMPerS","WaterTurbulence","ContinuousWaterSurface","WaterMinimumM","WaterMaximumM"}.Select(n=>typeof(LocalTerrainSample).GetField(n)??throw new Exception(n)).ToArray();
        foreach(string label in new[]{"flight-forest-normal-v14","flight-canyon-hard-v15"})
        {
            using var doc=JsonDocument.Parse(File.ReadAllText("docs/reports/fast-flight-20261110/evidence/"+label+"/review-site.json"));var p=doc.RootElement.GetProperty("Position");
            var at=new V(p.GetProperty("X").GetDouble(),p.GetProperty("Y").GetDouble(),p.GetProperty("Z").GetDouble());var patch=model.Patch(at,50000);patch.RiverSpans();
            var frame=new LocalTerrainFrame(at,model.RadiusM,0,.1);
            for(int z=-8;z<=8;z++)for(int x=-8;x<=8;x++)
            {
                var point=frame.Position(x*173.25,z*173.25);var full=patch.Sample(point);var geometry=patch.Geometry(point);var ecology=patch.Ecology(point,geometry);
                foreach(var field in fields)
                {
                    var expected=field.GetValue(full);
                    if(!Equals(expected,field.GetValue(geometry))||!Equals(expected,field.GetValue(ecology)))throw new Exception(label+" geometry differs: "+field.Name+" at "+x+","+z);
                    checks+=2;
                }
            }
            var scatter=new LocalScatter(model,.1);var key=scatter.Address(at);
            var fullScatter=scatter.Build(key);var withoutTrees=scatter.Build(key,includeTrees:false);
            if(!fullScatter.Instances.Where(i=>i.Kind!=ScatterKind.GeneratedTree).SequenceEqual(withoutTrees.Instances))throw new Exception("Non-tree scatter changed when suppressing distant tree census");checks++;
            for(int level=1;level<=8;level++)
            {
                int span=LocalScatter.ForestPageSide*(1<<level);var pageKey=new ForestPageKey(key.Face,key.X*LocalScatter.TreeCandidateSide/span,key.Y*LocalScatter.TreeCandidateSide/span,level);
                var page=scatter.BuildForest(pageKey);var again=scatter.BuildForest(pageKey);
                if(page.Probes!=0||page.Roots.Length>64||page.Roots.Any(r=>r.Population!=0)||!page.Roots.SequenceEqual(again.Roots))throw new Exception("Distant canopy census/determinism contract failed");checks++;
            }
        }
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"geometry-contract.json"),JsonSerializer.Serialize(new{Checks=checks,Fields=fields.Select(f=>f.Name),Result="Exact geometry and hydrology; ecological attributes intentionally excluded"}));
        Console.WriteLine(checks+" exact geometry/water field checks passed");
    }
}
