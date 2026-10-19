using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class LocalTerrainTests
{
    public static void Run(Action<bool, string> check)
    {
        var recipe = Recipe.Preset("Preview"); recipe.Detail = 5000;
        var surface = SphericalMesh.Build(recipe, default); int n = surface.Cells.Length;
        World Fixture(int height, int fold, int plateau) => new World
        {
            Recipe = recipe.Clone(), Surface = surface, ElevationM = Enumerable.Repeat(height, n).ToArray(),
            FoldPermille = Enumerable.Repeat(fold, n).ToArray(), PlateauPermille = Enumerable.Repeat(plateau, n).ToArray(),
            Hydrology = new HydrologyResult { OceanConnected = Enumerable.Repeat(height < 0, n).ToArray() }
        };
        var flat = Fixture(2500, 0, 1000); var mountains = Fixture(2500, 1000, 0);
        var plain = Fixture(250, 0, 0); var sea = Fixture(-150, 0, 0);
        var systems = new[] { new LocalTerrain(flat), new LocalTerrain(mountains), new LocalTerrain(plain), new LocalTerrain(sea) };
        var origin = surface.Cells[221].Position; var frame = new LocalTerrainFrame(origin, systems[0].RadiusM, 2500);
        double Range(LocalTerrain terrain)
        {
            var patch = terrain.Patch(origin, 16000); var values = new List<double>();
            for (int z = -16; z <= 16; z++) for (int x = -16; x <= 16; x++) values.Add(patch.Sample(x * 800, z * 800).ElevationM);
            return values.Max() - values.Min();
        }
        double plateauRange = Range(systems[0]), mountainRange = Range(systems[1]), plainRange = Range(systems[2]);
        check(mountainRange > plateauRange * 5, "Equal-altitude plateau and mountains have genuinely different local relief");
        check(plateauRange < 15 && plainRange < 45, "High plateau and low plain stay comparatively smooth");
        var patchA = systems[1].Patch(origin, 6000); var patchB = systems[1].Patch(frame.Position(2000, 0), 6000);
        foreach (int x in new[] { 0, 1999, 2000, 2001, 4000 })
        {
            V position = frame.Position(x, 500);
            check(patchA.Sample(position).ElevationM == patchB.Sample(position).ElevationM, "Independent patch plans agree at overlapping planetary coordinates");
        }
        var again = new LocalTerrain(mountains).Patch(origin, 6000);
        check(again.Sample(333, 777).ElevationM == patchA.Sample(333, 777).ElevationM, "Cold sampler reconstruction is deterministic");
        var heights = new double[16]; Parallel.For(0, heights.Length, i => heights[i] = patchA.Sample(333, 777).ElevationM);
        check(heights.All(h => h == heights[0]), "Concurrent queries do not depend on scheduling or mutable random state");
        var water = systems[3].Patch(origin).Sample(0, 0);
        check(water.Underwater && water.ElevationM < -100 && water.WaterSurfaceM == 0, "Ocean has an actual submerged bed and separate surface");
        check(!systems[2].Patch(origin).Sample(0, 0).Underwater, "Dry land has no water plane as terrain");
        var island=Fixture(-1000,1000,0);island.ElevationM[221]=3000;island.Hydrology.OceanConnected[221]=false;
        var islandTerrain=new LocalTerrain(island);var peak=islandTerrain.Patch(origin).Sample(0,0);
        check(peak.BaseElevationM==3000 && !peak.Underwater,"A coastal mountain is a hard height anchor, never averaged away into surrounding ocean");
        foreach(int id in surface.Cells[221].Neighbors)
            check(islandTerrain.Patch(surface.Cells[id].Position).Sample(0,0).BaseElevationM==-1000,"Adjacent submarine source heights are retained too");
        var high = LocalTerrainMesh.Build(systems[1], frame, 0, 0, 2048, 64);
        var neighbour = LocalTerrainMesh.Build(systems[1], frame, 2048, 0, 2048, 64);
        var low = LocalTerrainMesh.Build(systems[1], frame, 2048, 0, 2048, 16);
        for (int z = 0; z <= 64; z++)
            check(high.Positions[(z * 65 + 64) * 3 + 1] == neighbour.Positions[z * 65 * 3 + 1], "Equal LOD tile edges match exactly");
        for (int z = 0; z <= 16; z++)
            check(high.Positions[(z * 4 * 65 + 64) * 3 + 1] == low.Positions[z * 17 * 3 + 1], "Different LODs preserve shared samples");
        check(high.Triangles.All(i => i >= 0 && i < high.Positions.Length / 3), "All ground/skirt indices are valid");
        check(high.Positions.All(float.IsFinite) && high.Normals.All(float.IsFinite), "Terrain vertices and normals are finite");
        for (int i = 0; i < high.GroundVertices; i++)
            if (i % 47 == 0) check(Math.Abs(high.Normals[i * 3] * high.Normals[i * 3] + high.Normals[i * 3 + 1] * high.Normals[i * 3 + 1] + high.Normals[i * 3 + 2] * high.Normals[i * 3 + 2] - 1) < 1e-5, "Normals are normalized");
        var cancel = new CancellationTokenSource(); cancel.Cancel(); bool cancelled = false;
        try { LocalTerrainMesh.Build(systems[1], frame, 0, 0, token: cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
        check(cancelled, "Chunk work responds to cancellation");
        bool bounds = false; try { patchA.Sample(9000, 0); } catch (ArgumentOutOfRangeException) { bounds = true; }
        check(bounds, "A patch cannot silently sample beyond its spatial index");
        bool invalid = false; try { patchA.Sample(double.NaN, 0); } catch (ArgumentOutOfRangeException) { invalid = true; }
        check(invalid, "Non-finite patch coordinates are rejected");
        invalid = false; try { systems[1].Patch(new V()); } catch (ArgumentOutOfRangeException) { invalid = true; }
        check(invalid, "Invalid planet anchors are rejected");
        foreach(int id in new[]{0,n/2,n-1})
        {
            var pole = systems[1].Patch(surface.Cells[id].Position,6000);
            check(double.IsFinite(pole.Sample(500,1000).ElevationM), "Both poles and equator support local terrain queries");
        }
        Console.WriteLine($"relief: plateau {plateauRange:F1}m; mountains {mountainRange:F1}m; plain {plainRange:F1}m; 64-segment tile {high.BuildMilliseconds:F1}ms; candidates {patchA.CandidateCount}");
    }

    public static void World(string path, Action<bool, string> check)
    {
        var world = WorldFile.Read(File.ReadAllBytes(path)); string hash = StateHash.Compute(world);
        var watch = Stopwatch.StartNew(); var terrain = new LocalTerrain(world); double setup = watch.Elapsed.TotalMilliseconds;
        int cell = Array.FindIndex(world.ElevationM, h => h > 500 && h < 1800); if (cell < 0) cell = 0;
        V position = world.Surface.Cells[cell].Position; var patch = terrain.Patch(position, 10000);
        var frame = new LocalTerrainFrame(position, terrain.RadiusM, patch.Sample(0, 0).ElevationM);
        var durations = new List<double>();
        for (int i = 0; i < 12; i++)
        {
            var tile = LocalTerrainMesh.Build(terrain, frame, (i % 4 - 2) * 2048, (i / 4 - 1) * 2048, 2048, i < 3 ? 128 : 32);
            durations.Add(tile.BuildMilliseconds); check(tile.Positions.All(float.IsFinite), "Actual-world mesh is finite");
        }
        check(hash == StateHash.Compute(world), "Local exploration preserves the complete saved world/history hash");
        check(patch.CandidateCount < world.Surface.Cells.Length / 10, "Local queries use a spatially bounded subset, not all world cells");
        Console.WriteLine($"{world.Surface.Cells.Length} cells: one-time descriptors/index {setup:F0}ms; candidates {patch.CandidateCount}; tile mean {durations.Average():F1}ms; worst {durations.Max():F1}ms");
    }

    public static void Native(string worldPath, string probesPath, Action<bool,string> check)
    {
        var world = WorldFile.Read(File.ReadAllBytes(worldPath)); var terrain = new LocalTerrain(world);
        foreach(string line in File.ReadAllLines(probesPath))
        {
            var values = line.Split(',').Select(s=>double.Parse(s,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var patch = terrain.Patch(new V(values[0],values[1],values[2]),6000);
            check(patch.Sample(values[3],values[4]).ElevationM == values[5], "IL2CPP and CLR local terrain samples match exactly");
        }
    }
}
