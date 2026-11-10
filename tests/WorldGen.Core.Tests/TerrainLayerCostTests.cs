using System.Diagnostics;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class TerrainLayerCostTests
{
    public static void Run(string worldPath,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadPlanetaryFile(worldPath);var model=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);var rows=new List<object>();
        foreach(string label in new[]{"flight-forest-normal-v14","flight-canyon-hard-v15"})
        {
            using var doc=JsonDocument.Parse(File.ReadAllText("docs/reports/fast-flight-20261110/evidence/"+label+"/review-site.json"));var p=doc.RootElement.GetProperty("Position");
            var at=new V(p.GetProperty("X").GetDouble(),p.GetProperty("Y").GetDouble(),p.GetProperty("Z").GetDouble());var patch=model.Patch(at,5000);patch.RiverSpans();var frame=new LocalTerrainFrame(at,model.RadiusM,patch.Elevation(at),.1);
            foreach(var request in new[]{(102.4,64),(819.2,64),(1638.4,32)})
            {
                var timing=new TerrainBuildTiming();var mesh=LocalTerrainMesh.Build(model,frame,0,0,request.Item1,request.Item2,timing:timing);
                rows.Add(new{Site=label,Size=request.Item1,Segments=request.Item2,mesh.BuildMilliseconds,timing.GeometrySamples,timing.MaterialSamples,Stages=TerrainBuildTiming.Names.Select((name,i)=>new{Name=name,Ms=timing.Milliseconds[i],Percent=100*timing.Milliseconds[i]/timing.Milliseconds.Sum()}).ToArray()});
                Console.WriteLine(label+" "+request+" "+mesh.BuildMilliseconds.ToString("F1")+"ms: "+string.Join("; ",timing.Milliseconds.Select((n,i)=>TerrainBuildTiming.Names[i]+" "+n.ToString("F1"))));
                File.WriteAllText(Path.Combine(output,"mesh-stages.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
            }
            var scatter=new LocalScatter(model,.1);var key=scatter.Address(at);
            foreach(int level in new[]{0,2,4,6})
            {
                int span=LocalScatter.ForestPageSide*(1<<level);var pageKey=new ForestPageKey(key.Face,key.X*LocalScatter.TreeCandidateSide/span,key.Y*LocalScatter.TreeCandidateSide/span,level);var watch=Stopwatch.StartNew();var page=scatter.BuildForest(pageKey);watch.Stop();
                rows.Add(new{Site=label,ForestLevel=level,Ms=watch.Elapsed.TotalMilliseconds,page.Probes,Roots=page.Roots.Length});Console.WriteLine(label+" forest L"+level+" "+watch.Elapsed.TotalMilliseconds.ToString("F1")+"ms / "+page.Probes+" probes");
                File.WriteAllText(Path.Combine(output,"mesh-stages.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
            }
        }
    }
}
