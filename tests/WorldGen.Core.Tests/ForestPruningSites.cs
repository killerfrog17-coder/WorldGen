using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class ForestPruningSites
{
    public static void Prepare(string input,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadPlanetaryFile(input);
        var model=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        int selected=-1;
        foreach(int i in Enumerable.Range(0,world.ElevationM.Length).OrderBy(i=>world.ElevationM[i]).Take(64))
            if(model.Patch(world.Surface.Cells[i].Position,8400).MaximumForestCoverage==0){selected=i;break;}
        if(selected<0)throw new Exception("Actual world did not supply a proven zero-forest control");
        var point=world.Surface.Cells[selected].Position;
        File.WriteAllText(Path.Combine(output,"ocean-l4.json"),JsonSerializer.Serialize(new{ForestOnly=true,ForestLevel=4,Requests=Array.Empty<object>(),Sites=new[]{new{Name="zero-forest-ocean",Position=new{point.X,point.Y,point.Z}}}}));
        point=new V(.381914478,.918764738,-.100063418);
        File.WriteAllText(Path.Combine(output,"jungle-l4.json"),JsonSerializer.Serialize(new{ForestOnly=true,ForestLevel=4,Requests=Array.Empty<object>(),Sites=new[]{new{Name="dense-jungle",Position=new{point.X,point.Y,point.Z}}}}));
        Console.WriteLine("Prepared actual zero-forest ocean cell "+selected+" and dense-jungle L4 controls");
    }
}
