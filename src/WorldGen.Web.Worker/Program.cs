using System;
using System.Runtime.InteropServices.JavaScript;
using WorldGen.Core;
using WorldGen.Serialization;

public static partial class WorkerApi
{
    public static void Main() { }
    [JSExport]
    public static string Generate(string recipe)
    {
        var world = new WorldPipeline().Generate(WorldJson.ReadRecipe(recipe), p => Progress(p.Stage, p.Percent));
        return WorldJson.Write(world);
    }
    [JSImport("progress", "worldgen")]
    public static partial void Progress(string stage, int percent);
}
