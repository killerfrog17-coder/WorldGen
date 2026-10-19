using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class LocalTerrainTests
{
    public static void Run(Action<bool, string> check)
    {
        Navigation(check);
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
        TerrainMapViewport.Degrees(origin,out double mapLat,out double mapLon);
        var map=new TerrainMapViewport{Latitude=mapLat,Longitude=mapLon,LongitudeSpan=.0001};
        var pixels=TerrainMapImage.Build(systems[1],map,32,16);
        check(pixels.Rgba.Length==32*16*4&&pixels.Rgba.Where((_,i)=>i%4==3).All(a=>a==255),"Deep debug-map zoom samples local terrain and returns complete opaque pixels");
        var cancelledMap=new CancellationTokenSource();cancelledMap.Cancel();bool mapCancelled=false;
        try{TerrainMapImage.Build(systems[1],map,32,16,cancelledMap.Token);}catch(OperationCanceledException){mapCancelled=true;}
        check(mapCancelled,"An obsolete debug-map request can be cancelled before its first row");
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
        var horizon=LocalTerrainMesh.Build(systems[1],frame,0,0,131072,8);
        check(horizon.Positions.All(float.IsFinite)&&horizon.GroundVertices==81,"Far-horizon tiles are valid coarse products, not giant near-detail meshes");
        var neighbour = LocalTerrainMesh.Build(systems[1], frame, 2048, 0, 2048, 64);
        var low = LocalTerrainMesh.Build(systems[1], frame, 2048, 0, 2048, 16);
        var stitched=LocalTerrainMesh.Build(systems[1],frame,0,0,2048,64,edgeSteps:new[]{0.0,128,0,0});
        for(int z=0;z<=64;z++)
        {
            int lo=z/4,hi=Math.Min(16,lo+1);double t=(z%4)/4.0,expected=low.Positions[lo*17*3+1]*(1-t)+low.Positions[hi*17*3+1]*t;
            check(Math.Abs(stitched.Positions[(z*65+64)*3+1]-expected)<.001,"Fine edge follows the actual neighbouring coarse segment without exposed skirt teeth");
        }
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

    static void Navigation(Action<bool,string> check)
    {
        var player=new TerrainMotion();player.Place(0,0,0);double Ground(double x,double z)=>0;
        player.Move(.02,0,0,0,0,0,false,true,Ground);
        check(player.Y>TerrainMotion.EyeHeightM&&!player.Grounded,"Space initiates a physical jump rather than fly movement");
        double maximum=player.Y;for(int i=0;i<100;i++){player.Move(.02,0,0,0,0,0,false,false,Ground);maximum=Math.Max(maximum,player.Y);}
        check(player.Grounded&&player.Y==TerrainMotion.EyeHeightM&&maximum>2.8,"Jump follows gravity and lands on the analytic terrain");
        for(int i=0;i<50;i++)player.Move(.02,1,0,0,0,0,false,false,Ground);
        check(Math.Abs(player.Z-4.5)<1e-8,"Walking speed is in metres per second independent of frame rate");
        player.Place(0,0,0);player.Move(.05,1,0,0,0,0,false,false,(x,z)=>z>.1?100:0);
        check(player.Z==0,"Walking cannot snap up a sheer cliff");
        player.Flying=true;player.Move(.05,1,0,0,0,-45,false,false,Ground);
        check(player.Z>0&&player.Y>TerrainMotion.EyeHeightM,"Flying follows the mouse-look pitch");
        var viewport=new TerrainMapViewport();var anchor=viewport.Position(.7,.4);viewport.Zoom(.1,.7,.4);
        check(D.Angle(anchor,viewport.Position(.7,.4))<1e-7,"Map zoom preserves the position beneath the cursor");
        viewport.Longitude=179;viewport.Pan(-.2,0);
        check(viewport.Longitude>=-180&&viewport.Longitude<180,"Map panning wraps the antimeridian");
        viewport.Zoom(1e-8);check(viewport.LongitudeSpan<1e-5,"Map is not limited to a fixed cell-level zoom");
        var far=TerrainLod.Plan(333,-777,2);var high=TerrainLod.Plan(333,-777,60000);
        check(far.Count<800&&high.Count<far.Count,"Distance LOD is bounded and reduces detail at altitude");
        check(far.Keys.Max(k=>k.SizeM)>=32768&&far.Keys.Min(k=>k.SizeM)==2048,"LOD mixes near and far tile sizes");
        double area=0;foreach(var k in far.Keys)area+=k.SizeM*k.SizeM;
        check(area>Math.PI*200000*200000,"LOD covers the requested 200 km horizon including boundary tiles");
        var keys=far.Keys.ToArray();bool overlaps=false;
        for(int i=0;i<keys.Length;i++)for(int j=i+1;j<keys.Length;j++)
        {var a=keys[i];var b=keys[j];if(Math.Max(a.X*a.SizeM,b.X*b.SizeM)<Math.Min((a.X+1)*a.SizeM,(b.X+1)*b.SizeM)&&Math.Max(a.Z*a.SizeM,b.Z*b.SizeM)<Math.Min((a.Z+1)*a.SizeM,(b.Z+1)*b.SizeM))overlaps=true;}
        check(!overlaps,"LOD leaves partition the surface without overlapping parents");
        for(int i=0;i<40;i++){double a=i*Math.PI/20,x=199999*Math.Cos(a)+333,z=199999*Math.Sin(a)-777;
            check(keys.Any(k=>x>=k.X*k.SizeM&&x<(k.X+1)*k.SizeM&&z>=k.Z*k.SizeM&&z<(k.Z+1)*k.SizeM),"LOD covers every sampled horizon direction");}
        Console.WriteLine($"LOD: {far.Count} ground-level / {high.Count} high-altitude tiles");
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
        foreach(var site in new[]{(27.98,86.92),(-32.65,-70.01),(38.07,-122.96),(32.0,88.0)})
        {
            var local=terrain.Patch(TerrainMapViewport.FromDegrees(site.Item1,site.Item2),10000);var center=local.Sample(0,0);var values=new List<double>();
            for(int z=-8;z<=8;z++)for(int x=-8;x<=8;x++)values.Add(local.Sample(x*1000,z*1000).ElevationM);
            Console.WriteLine($"site {site}: elevation {center.ElevationM:F0}m; coarse relief {terrain.Profile(center.Cell).CoarseReliefM:F0}m; mountain amplitude {center.MountainHeightM:F0}m; 16km range {values.Max()-values.Min():F0}m");
        }
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
