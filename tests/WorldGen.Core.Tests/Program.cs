using WorldGen.Core;
using WorldGen.Serialization;

int assertions = 0;
void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
void Connected(int[] members, Func<int, int[]> neighbors, string name)
{ var set = members.ToHashSet(); var visited = new HashSet<int> { members[0] }; var queue = new Queue<int>(); queue.Enqueue(members[0]); while (queue.Count > 0) foreach (int n in neighbors(queue.Dequeue())) if (set.Contains(n) && visited.Add(n)) queue.Enqueue(n); Check(visited.Count == members.Length, name + " disconnected"); }
void Validate(World w)
{
    var s = w.Surface; int n = s.Cells.Length; Check(n - s.Edges.Length + s.Triangles.Length / 3 == 2, "Euler characteristic");
    double expected = 4 * Math.PI * Math.Pow(w.Recipe.RadiusKm * 1000.0, 2); Check(Math.Abs(s.Cells.Sum(c => (double)c.AreaM2) / expected - 1) < .00001, "Spherical area");
    foreach (var c in s.Cells) { Check(Math.Abs(c.Position.Length - 1) < .000001, "Unit sphere"); Check(c.AreaM2 > 0, "Positive area"); foreach (int nb in c.Neighbors) Check(s.Cells[nb].Neighbors.Contains(c.Id), "Symmetric adjacency"); }
    Check(w.Microplates.Length == w.Recipe.MicroplateCount, "Microplate count");
    foreach (var p in w.Microplates)
    { Check(p.Cells.Length >= 4, "Useful microplate"); Connected(p.Cells, i => s.Cells[i].Neighbors, "microplate"); Check(p.Cells.All(c => w.CellMicroplate[c] == p.Id), "Membership provenance"); Check(p.ParentAffinityId >= 0, "Parent affinity"); Check(p.Cells.Sum(c => s.Cells[c].AreaM2) == p.AreaM2, "Microplate area"); if (p.Preserved) Check(p.ContestDegree >= 2 && p.Neighbors.Any(j => w.Microplates[j].ParentAffinityId != p.ParentAffinityId), "Only contested interface actors preserved"); else Check(p.ActiveActorId == p.ParentAffinityId, "Fused active identity"); }
    foreach (var p in w.Parents) Connected(p.Microplates, i => w.Microplates[i].Neighbors, "parent");
    foreach (var b in w.Boundaries) { Check(b.Ghost == (w.Microplates[b.MicroA].ActiveActorId == w.Microplates[b.MicroB].ActiveActorId), "Ghost identity"); if (b.Ghost) Check(b.Kind == BoundaryKind.Inactive, "Inactive seam"); }
    Check(w.Features.All(f => f.Cells.All(c => c >= 0 && c < n)), "Feature membership");
    for (int i = 0; i < n; i++) Check(w.FoldPermille[i] >= 0 && w.CratonPermille[i] >= 0 && w.BasinPermille[i] >= 0 && w.PlateauPermille[i] >= 0 && w.FoldPermille[i] + w.CratonPermille[i] + w.BasinPermille[i] + w.PlateauPermille[i] == 1000, "Continuous province partition");
    Hydro(w.Surface, w.ElevationM, w.Hydrology);
    for (int i = 0; i < n; i++) if (w.ProtectedInlandSea[i]) Check(w.ElevationM[i] == w.TectonicM[i] && w.ElevationM[i] < 0, "Protected sea bathymetry");
}
void Hydro(Surface s, int[] h, HydrologyResult water)
{
    int n = h.Length; var rank = new int[n]; for (int k = 0; k < n; k++) rank[water.VisitOrder[k]] = k;
    for (int i = 0; i < n; i++) if (water.Receiver[i] >= 0) { int j = water.Receiver[i]; Check(s.Cells[i].Neighbors.Contains(j), "Receiver adjacency"); Check(rank[j] < rank[i], "Acyclic receiver"); Check(water.FloodLevelM[j] <= water.FloodLevelM[i], "Nonascending conditioned flow"); Check(water.Watershed[j] == water.Watershed[i], "Watershed continuity"); }
    Check(Enumerable.Range(0, n).Where(i => water.Receiver[i] < 0).Sum(i => water.ContributingAreaM2[i]) == s.Cells.Sum(c => c.AreaM2), "Contributing area conservation");
    foreach (var lake in water.Waters.Where(b => b.Kind != WaterKind.Ocean))
    { Check(lake.Cells.All(i => water.FloodLevelM[i] == lake.SurfaceM), "Common lake level"); Check(lake.Cells.All(i => h[i] <= lake.SurfaceM), "Water above bed"); if (!lake.Endorheic) { Check(lake.OutletCell >= 0, "Lake outlet"); Check(water.Receiver[lake.OutletCell] == lake.DownstreamCell, "Spill receiver"); Check(lake.Cells.Count(i => water.Receiver[i] >= 0 && !lake.Cells.Contains(water.Receiver[i])) == 1, "One lake outlet"); } else Check(lake.Cells.Count(i => water.Receiver[i] < 0) == 1, "Terminal endorheic sink"); }
}
Surface Chain(int count)
{ return new Surface { Cells = Enumerable.Range(0, count).Select(i => new Cell { Id = i, AreaM2 = 200000000, Neighbors = Enumerable.Range(Math.Max(0, i - 1), Math.Min(count - 1, i + 1) - Math.Max(0, i - 1) + 1).Where(j => j != i).ToArray(), DistanceM = Enumerable.Repeat(1000, i == 0 || i == count - 1 ? 1 : 2).ToArray() }).ToArray() }; }
try
{
    var recipe = Recipe.Preset("Preview"); var pipe = new WorldPipeline(); var world = pipe.Generate(recipe); Validate(world);
    string[] hashes = world.Stages.Select(s => s.Hash).ToArray();
    foreach (var repeat in new[] { pipe.Generate(recipe), pipe.Reapply(recipe), pipe.Generate(WorldJson.ReadRecipe(WorldJson.Write(recipe))), pipe.Reconstruct(WorldJson.ReadWorld(WorldJson.Write(world))) }) Check(hashes.SequenceEqual(repeat.Stages.Select(s => s.Hash)), "All-stage reconstruction equivalence");
    Check(StateHash.Compute(world) == StateHash.Compute(WorldJson.ReadWorld(WorldJson.Write(world))), "Result roundtrip");
    var invalid = recipe.Clone(); invalid.AlgorithmVersion = "future"; try { pipe.Generate(invalid); throw new Exception("Version accepted"); } catch (ArgumentException) { assertions++; }
    try { WorldJson.ReadRecipe(WorldJson.Write(recipe).Replace("\"Hydraulic\":0.35,", "")); throw new Exception("Incomplete recipe accepted"); } catch (ArgumentException) { assertions++; }
    var minimum = recipe.Clone(); minimum.MicroplateCount = 4; minimum.ParentCount = 2; minimum.ContinentCount = 2; minimum.MacroCellCount = 642; Validate(pipe.Generate(minimum));
    using (var cancel = new CancellationTokenSource()) { cancel.Cancel(); try { pipe.Generate(recipe, cancellation: cancel.Token); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { assertions++; } }
    var hr = recipe.Clone(); hr.MinLakeAreaKm2 = 100; hr.Endorheic = 0; int[] h = { -10, 200, 0, 100, -20, 30, 250 }; var chain = Chain(h.Length); var hydro = Hydrology.Solve(chain, h, hr, new bool[h.Length], Array.Empty<Feature>(), default); Hydro(chain, h, hydro); Check(hydro.Waters.Any(b => b.Kind == WaterKind.Lake && b.SurfaceM == 200), "Nested bowl common spill"); Check(hydro.Depressions.Any(d => d.Children.Length >= 2), "Depression hierarchy merge");
    Check(StateHash.Compute(hydro) == StateHash.Compute(Hydrology.Solve(chain, h, hr, new bool[h.Length], Array.Empty<Feature>(), default)), "Hydrology stable ties");
    bool[] protect = { false, false, true, true, false, false, false }; int[] inlandH = { -20, 100, -50, -100, 200, 300, 400 }; var inlandWater = Hydrology.Solve(chain, inlandH, hr, protect, Array.Empty<Feature>(), default); Hydro(chain, inlandH, inlandWater); Check(inlandWater.Waters.Any(b => b.Kind == WaterKind.TectonicInlandSea && b.Endorheic && b.SurfaceM == 0), "Protected inland sea is terminal");
    var protectedWorld = new World { Recipe = hr, Surface = chain, TectonicM = inlandH, Continent = new[] { -1, 1, -1, -1, 1, 1, 1 }, Stress = Enumerable.Repeat(500, h.Length).ToArray() }; hr.InlandSeaMinAreaKm2 = 1000; chain.Cells[2].AreaM2 = 2000000000; chain.Cells[3].AreaM2 = 2000000000; chain.Cells[0].AreaM2 = 10000000000; Terrain.ProtectInlandSeas(protectedWorld); Check(protectedWorld.ProtectedInlandSea[2] && protectedWorld.ProtectedInlandSea[3], "Pre-fill marine block detection");
    Check(Tectonics.Context(100000, 1000000) == CollisionContext.EscapeFace && Tectonics.Context(750000, 1000000) == CollisionContext.Intermediate && Tectonics.Context(2000000, 1000000) == CollisionContext.DeadEnd, "Collision contexts");
    var from = new V(1, 0, 0); var to = new V(0, 1, 0); var direction = new V(0, 1, 0); var transported = V.Transport(direction, from, to); Check(Math.Abs(V.Dot(transported, to)) < .000001 && V.Dot(transported, new V(-1, 0, 0)) > .99999, "Great-circle transport");
    foreach (var entry in GoldenCorpus.Recipes())
    {
        var w = pipe.Generate(entry.Recipe); Validate(w);
        if (entry.Name == "escape-face") Check(w.Boundaries.Any(b => b.Context == CollisionContext.EscapeFace), "Escape corpus coverage");
        if (entry.Name == "dead-end") Check(w.Boundaries.Any(b => b.Context == CollisionContext.DeadEnd), "Dead-end corpus coverage");
        if (entry.Name == "inland-sea") Check(w.Hydrology.Waters.Any(b => b.Kind == WaterKind.TectonicInlandSea), "Inland-sea corpus coverage");
        if (entry.Name == "glacial-basin") Check(w.Features.Any(f => f.Kind == FeatureKind.GlacialBasin), "Glacial corpus coverage");
        string golden = Path.Combine("tests", "golden", entry.Name + ".hashes.json"); if (File.Exists(golden)) Check(WorldJson.Read<List<StageRecord>>(File.ReadAllText(golden)).Select(s => s.Hash).SequenceEqual(w.Stages.Select(s => s.Hash)), entry.Name + " golden stage hashes");
        Console.WriteLine("PASS " + entry.Name + " " + w.Stages.Last().Hash);
    }
    Console.WriteLine("PASS " + assertions + " assertions; topology, hierarchy, geology, erosion, hydrology, serialization, cancellation and determinism.");
}
catch (Exception e) { Console.Error.WriteLine(e); Environment.ExitCode = 1; }
