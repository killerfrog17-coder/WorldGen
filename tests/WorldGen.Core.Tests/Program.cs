using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

int assertions = 0;
void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
void Connected(int[] members, Func<int, int[]> neighbors, string name)
{ var set = members.ToHashSet(); var visited = new HashSet<int> { members[0] }; var queue = new Queue<int>(); queue.Enqueue(members[0]); while (queue.Count > 0) foreach (int n in neighbors(queue.Dequeue())) if (set.Contains(n) && visited.Add(n)) queue.Enqueue(n); Check(visited.Count == members.Length, name + " disconnected"); }
void Validate(World w)
{
    var s = w.Surface; int n = s.Cells.Length; Check(n - s.Edges.Length + s.Triangles.Length / 3 == 2, "Euler characteristic");
    double expected = 4 * Math.PI * Math.Pow(w.Recipe.RadiusKm * 1000.0, 2); Check(Math.Abs(s.Cells.Sum(c => (double)c.AreaM2) / expected - 1) < .00001, "Spherical area");
    foreach (var c in s.Cells) { Check(Math.Abs(c.Position.Length - 1) < .000001, "Unit sphere"); Check(c.AreaM2 > 0, "Positive area"); foreach (int nb in c.Neighbors) Check(s.Cells[nb].Neighbors.Contains(c.Id), "Symmetric adjacency"); }
    Check(n == w.Recipe.Detail, "Detail is terrain resolution"); Check(w.CoarseSurface.Cells.Length == 20000, "Fixed 20,000-cell coarse plate base"); Check(w.Microplates.Length == w.Recipe.MicroplateCount, "Plate count");
    foreach (var p in w.Microplates)
    {
        Check(p.Cells.Length >= 1, "Useful plate"); Connected(p.Cells, i => s.Cells[i].Neighbors, "plate");
        Check(p.Cells.All(c => w.CellMicroplate[c] == p.Id), "Membership provenance");
        Check(p.ParentAffinityId >= 0 && p.ActiveActorId == p.Id, "Independent plate motion");
        Check(p.Cells.Sum(c => s.Cells[c].AreaM2) == p.AreaM2, "Plate area");
    }
    foreach (var p in w.Parents)
    {
        Connected(p.Microplates, i => w.Microplates[i].Neighbors, "superplate");
        Check(p.ContinentalFraction >= 0 && p.ContinentalFraction <= 1, "Superplate crust fraction");
    }
    if (w.Microplates.Length >= 8) Check(w.Boundaries.Any(b => b.Ghost), "Parent seams produce ghost boundaries");
    Check(w.Boundaries.Any(b => !b.Ghost && b.Kind != BoundaryKind.Inactive), "Parent boundaries remain active");
    Check(w.Climate.Classification.Length == n && w.Climate.Classification.All(c => Enum.IsDefined(c)), "Climate classes");
    foreach (var season in new[] { w.Climate.NorthernSummer, w.Climate.NorthernWinter })
    {
        Check(season.TemperatureTenthsC.Length == n && season.TemperatureTenthsC.All(t => t >= -900 && t <= 650), "Climate temperature bounds");
        Check(season.PrecipitationMm.Length == n && season.PrecipitationMm.All(p => p >= 0), "Seasonal precipitation bounds");
        Check(season.WindEastMilli.Length == n && season.CurrentEastMilli.Length == n, "Circulation retained");
    }
    Check(w.Features.All(f => f.Cells.All(c => c >= 0 && c < n)), "Feature membership");
    for (int i = 0; i < n; i++) Check(w.FoldPermille[i] >= 0 && w.CratonPermille[i] >= 0 && w.BasinPermille[i] >= 0 && w.PlateauPermille[i] >= 0 && w.FoldPermille[i] + w.CratonPermille[i] + w.BasinPermille[i] + w.PlateauPermille[i] == 1000, "Continuous province partition");
    Check(w.Hydrology.Waters.Length == 0 && w.Hydrology.Rivers.Length == 0 && w.Hydrology.Receiver.Length == 0, "No deferred hydrology generated");
    Check(w.Hydrology.OceanConnected.Length == n, "Ocean connectivity mask");
    Check(Enumerable.Range(0, n).All(i => !w.Hydrology.OceanConnected[i] || w.ElevationM[i] < 0), "Ocean connectivity only marks below-sea-level cells");
    Check(!w.ProtectedInlandSea.Any(b => b), "No custom inland-sea preservation");
}
try
{
    Check(StateHash.Compute(-0.0) == StateHash.Compute(0.0), "Signed zero has a canonical hash across JSON runtimes");
    foreach(double x in new[]{1e-20,1e-8,.2,1.0,100.0,1e12})Check(Math.Abs(D.Sqrt(x)-D.Q(Math.Sqrt(x)))<=1e-9,"Deterministic square root accuracy");
    var recipe = Recipe.Preset("Preview"); var pipe = new WorldPipeline(); var world = pipe.Generate(recipe); Validate(world);
    string[] hashes = world.Stages.Select(s => s.Hash).ToArray();
    foreach (var repeat in new[] { pipe.Generate(recipe), pipe.Reapply(recipe), pipe.Generate(WorldJson.ReadRecipe(WorldJson.Write(recipe))), pipe.Reconstruct(WorldJson.ReadWorld(WorldJson.Write(world))) }) Check(hashes.SequenceEqual(repeat.Stages.Select(s => s.Hash)), "All-stage reconstruction equivalence");
    Check(StateHash.Compute(world) == StateHash.Compute(WorldJson.ReadWorld(WorldJson.Write(world))), "Result roundtrip");
    var invalid = recipe.Clone(); invalid.AlgorithmVersion = "future"; try { pipe.Generate(invalid); throw new Exception("Version accepted"); } catch (ArgumentException) { assertions++; }
    try { WorldJson.ReadRecipe(WorldJson.Write(recipe).Replace("\"Hydraulic\":0.5,", "")); throw new Exception("Incomplete recipe accepted"); } catch (ArgumentException) { assertions++; }
    var minimum = recipe.Clone(); minimum.MicroplateCount = 4; minimum.ContinentCount = 2; Validate(pipe.Generate(minimum));
    var isolated = recipe.Clone(); isolated.ContinentCount = 2; isolated.ContinentalComplexCount = 2;
    var isolatedWorld = pipe.Generate(isolated);
    Check(isolatedWorld.Metrics.Single(m => m.Name == "continental_complex_components").Value == 2, "Requested isolated continental complexes");
    var connected = recipe.Clone(); connected.ContinentCount = 6; connected.ContinentalComplexCount = 1;
    var connectedWorld = pipe.Generate(connected);
    Check(connectedWorld.Metrics.Single(m => m.Name == "continental_complex_components").Value == 1, "Requested connected continental complex");
    using (var cancel = new CancellationTokenSource()) { cancel.Cancel(); try { pipe.Generate(recipe, cancellation: cancel.Token); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { assertions++; } }
    var from = new V(1, 0, 0); var to = new V(0, 1, 0); var direction = new V(0, 1, 0); var transported = V.Transport(direction, from, to); Check(Math.Abs(V.Dot(transported, to)) < .000001 && V.Dot(transported, new V(-1, 0, 0)) > .99999, "Great-circle transport");
    var detailRecipe = recipe.Clone(); detailRecipe.Detail = 5000; detailRecipe.Irregularity = .1;
    var lowDetail = pipe.Generate(detailRecipe); Validate(lowDetail);
    Check(world.CoarseHash == lowDetail.CoarseHash, "Detail and irregularity preserve the coarse partition");
    Check(world.Stages.Last().Hash != lowDetail.Stages.Last().Hash && lowDetail.ElevationM.Length == 5000, "Detail changes authoritative terrain");
    int changedHeights = 0;
    foreach (var cell in lowDetail.Surface.Cells)
        if (Math.Abs(lowDetail.ElevationM[cell.Id] - world.ElevationM[Erosion.Nearest(world.Surface, cell.Position)]) > 25) changedHeights++;
    Check(changedHeights > 500, "Fine terrain is recalculated, not merely rendered with extra polygons");
    string beforeView = StateHash.Compute(world);
    foreach (Layer layer in new[] { Layer.Terrain, Layer.Satellite, Layer.Climate, Layer.Heightmap })
    {
        var globe = GlobeView.Build(world, layer, detail: 20000);
        Check(globe.CellIds.All(i => i >= 0 && i < world.Recipe.Detail), "Cell surface picking provenance");
        if (layer != Layer.Heightmap) for (int i = 0; i < globe.Colors.Length; i += 9)
            Check(globe.Colors[i] == globe.Colors[i + 3] && globe.Colors[i] == globe.Colors[i + 6], "Flat cell colors are crisp");
        foreach (double longitude in new[] { 0.0, 179.0, -179.0 })
        {
            var map = MapView.Build(world, layer, centerLongitude: longitude, detail: 20000);
            Check(map.IsMap && map.Unlit && map.CellIds.Length * 3 == map.Positions.Length, "Map projection contract");
            Check(map.Positions.All(float.IsFinite), "Finite map coordinates");
            double mapArea = 0;
            for (int t = 0; t < map.Triangles.Length; t += 3)
            {
                int a = map.Triangles[t] * 3, b = map.Triangles[t + 1] * 3, c = map.Triangles[t + 2] * 3;
                double xa = map.Positions[a], xb = map.Positions[b], xc = map.Positions[c];
                Check(xa >= -2.00001 && xa <= 2.00001 && xb >= -2.00001 && xb <= 2.00001 && xc >= -2.00001 && xc <= 2.00001, "Map seam clipping");
                Check(Math.Max(xa, Math.Max(xb, xc)) - Math.Min(xa, Math.Min(xb, xc)) <= 2.001, "No triangles crossing map width");
                mapArea += Math.Abs((xb - xa) * (map.Positions[c + 1] - map.Positions[a + 1]) - (map.Positions[b + 1] - map.Positions[a + 1]) * (xc - xa)) / 2;
            }
            Check(Math.Abs(mapArea - 8) < .08, "Projected map covers the globe without gaps or overlap: " + mapArea + " " + layer + " " + longitude);
        }
    }
    var mapGrid = SurfaceMesh.Chunks(world, new ViewOptions { Projection = Projection.Map, GridSpacing = 30 }).Last();
    for (int i = 0; i < mapGrid.Lines.Length; i += 6)
        Check(Math.Abs(mapGrid.Lines[i] - mapGrid.Lines[i + 3]) < .0002 || Math.Abs(mapGrid.Lines[i + 1] - mapGrid.Lines[i + 4]) < .0002, "Map grid stays on meridians/parallels through pole endpoints");
    var overlayOptions = new ViewOptions { Wireframe = true, ShowPlates = true, GridSpacing = 30 };
    var overlay = SurfaceMesh.Combine(SurfaceMesh.Chunks(world, overlayOptions));
    Check(overlay.Lines.Length >= world.Surface.Edges.Length * 6, "Wireframe includes every dual cell edge");
    Check(overlay.LineColors.Length == overlay.Lines.Length, "Overlay colors accompany every line");
    var terrainPlates = SurfaceMesh.Combine(SurfaceMesh.Chunks(world, new ViewOptions { ShowPlates = true, GridSpacing = 0 }));
    var plateView = GlobeView.Build(world, Layer.Microplates);
    Check(terrainPlates.Colors.SequenceEqual(plateView.Colors), "Show plates colors terrain by plate");
    var climatePlates = SurfaceMesh.Combine(SurfaceMesh.Chunks(world, new ViewOptions { Layer = Layer.Climate, ShowPlates = true, GridSpacing = 0 }));
    Check(climatePlates.Colors.SequenceEqual(GlobeView.Build(world, Layer.Climate).Colors), "Plate outlines preserve climate colors");
    using (var cancelView = new CancellationTokenSource())
    {
        var enumerator = SurfaceMesh.Chunks(world, overlayOptions, cancelView.Token).GetEnumerator();
        Check(enumerator.MoveNext(), "First presentation chunk available"); cancelView.Cancel();
        try { enumerator.MoveNext(); throw new Exception("Presentation cancellation ignored"); } catch (OperationCanceledException) { assertions++; }
    }
    Check(beforeView == StateHash.Compute(world), "Views never mutate the world");
    Check(Climate.Classify(26, 25, 1800, 1700) == Koppen.Af, "Tropical rainforest");
    Check(Climate.Classify(35, 20, 10, 10) == Koppen.BWh, "Hot desert");
    Check(Climate.Classify(25, 8, 30, 500) == Koppen.Csa, "Mediterranean summer drought");
    Check(Climate.Classify(-4, -25, 50, 50) == Koppen.EF, "Ice cap");
    foreach (var entry in GoldenCorpus.Recipes())
    {
        var w = pipe.Generate(entry.Recipe); Validate(w);
        string golden = Path.Combine("tests", "golden", "v3", entry.Name + ".hashes.json"); if (!File.Exists(golden)) throw new Exception("Missing reviewed golden: " + golden); Check(WorldJson.Read<List<StageRecord>>(File.ReadAllText(golden)).Select(s => s.Hash).SequenceEqual(w.Stages.Select(s => s.Hash)), entry.Name + " golden stage hashes");
        Console.WriteLine("PASS " + entry.Name + " " + w.Stages.Last().Hash);
    }
    Console.WriteLine("PASS " + assertions + " assertions; topology, hierarchy, geology, erosion, presentation, serialization, cancellation and determinism.");
}
catch (Exception e) { Console.Error.WriteLine(e); Environment.ExitCode = 1; }
