using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

public static partial class WorkerApi
{
    static World world;
    static IEnumerator<ViewData> view;
    public static void Main() { }
    [JSExport]
    public static string Generate(string recipe)
    {
        world = new WorldPipeline().Generate(WorldJson.ReadRecipe(recipe), p => Progress(p.Stage, p.Percent));
        return WorldJson.Write(WorldSummary.From(world));
    }
    [JSExport] public static void BeginView(string options) { view?.Dispose(); view = SurfaceMesh.Chunks(world, WorldJson.Read<ViewOptions>(options)).GetEnumerator(); }
    [JSExport] public static byte[] NextView()
    {
        if (view == null || !view.MoveNext()) return Array.Empty<byte>();
        var data = view.Current; int p = data.Positions.Length, t = data.Triangles.Length, l = data.Lines.Length;
        var packed = new byte[20 + 4 * (p * 2 + t + l * 2)];
        var header = new[] { p, t, l, data.IsMap ? 1 : 0, data.Unlit ? 1 : 0 }; int offset = 0;
        void Copy(Array array) { int size = Buffer.ByteLength(array); Buffer.BlockCopy(array, 0, packed, offset, size); offset += size; }
        Copy(header); Copy(data.Positions); Copy(data.Colors); Copy(data.Triangles); Copy(data.Lines); Copy(data.LineColors);
        if (!BitConverter.IsLittleEndian) for (int i = 0; i < packed.Length; i += 4) Array.Reverse(packed, i, 4);
        return packed;
    }
    [JSExport] public static string Describe(int cell) => GlobeView.Describe(world, cell);
    [JSExport] public static string DescribePoint(double x,double y,double z) => GlobeView.Describe(world,Erosion.Nearest(world.Surface,new V(x,y,z)));
    [JSExport] public static string Export(string kind) => kind == "heightmap" ? Convert.ToBase64String(WorldJson.Heightmap(world)) : kind == "csv" ? WorldJson.Csv(world) : WorldJson.Write(world);
    [JSImport("progress", "worldgen")]
    public static partial void Progress(string stage, int percent);
}
