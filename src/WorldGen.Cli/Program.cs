using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

try
{
    string Arg(string name, string fallback = "") { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
    if(args.Contains("--terrain-worker")){await TerrainWorker.Run(int.Parse(Arg("--terrain-worker")),int.Parse(Arg("--parent")),Arg("--terrain-token"));return;}
    if(args.Contains("--list-presets")){foreach(string name in WorldPresets.Names)Console.WriteLine(name);return;}
    if (args.Contains("--help")) { Console.WriteLine("WorldGen --preset Earth|Preview|\"Orogen defaults\"|\"Ocean world\"|Continental|Glacial|\"500 plates\" --seed N --recipe file.json --output directory --verify --corpus directory\nWorld snapshots: --save-world file.world; --world file.world (or legacy result JSON)\nTerrain controls: --lake-amount 0..100000; --mountain-strength 0..100000 (percent, default100; fractional values supported)\nPlate selection: --continents 6 --interconnectedness 1..50\nVisual review: --map-preview file.svg --layer Continents|Terrain --rivers --summary\nOutputs recipe, complete result, cells.csv, heightmap.pgm, stage hashes. Ctrl+C cancels."); return; }
    using var cancel = new CancellationTokenSource(); Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
    var pipeline = new WorldPipeline();
    if (args.Contains("--corpus"))
    {
        string folder = Arg("--corpus", "artifacts/corpus"); Directory.CreateDirectory(folder);
        foreach (var entry in GoldenCorpus.Recipes())
        {
            var watch = System.Diagnostics.Stopwatch.StartNew(); var world = pipeline.Generate(entry.Recipe, cancellation: cancel.Token); string stem = Path.Combine(folder, entry.Name);
            File.WriteAllText(stem + ".recipe.json", WorldJson.Write(world.Recipe)); File.WriteAllText(stem + ".hashes.json", WorldJson.Write(world.Stages));
            File.WriteAllText(stem + ".metrics.json", WorldJson.Write(world.Metrics)); Console.WriteLine(entry.Name + " " + world.Stages.Last().Hash + " " + watch.ElapsedMilliseconds + "ms");
        }
        return;
    }
    var recipe = args.Contains("--recipe") ? WorldJson.ReadRecipe(File.ReadAllText(Arg("--recipe"))) : Recipe.Preset(Arg("--preset", "Preview"));
    if (args.Contains("--complexes") || args.Contains("--composition") || args.Contains("--world-type")) throw new ArgumentException("Use --continents and --interconnectedness for plate selection; complex and world-type controls are not supported.");
    if (args.Contains("--seed")) recipe.Seed = int.Parse(Arg("--seed"));
    if(!args.Contains("--recipe"))WorldPresets.Apply(Arg("--preset","Preview"),recipe);
    if (args.Contains("--detail")) recipe.Detail = int.Parse(Arg("--detail"));
    if (args.Contains("--lake-amount")) recipe.LakeAmountPercent = double.Parse(Arg("--lake-amount"), System.Globalization.CultureInfo.InvariantCulture);
    if (args.Contains("--mountain-strength")) { recipe.MountainStrengthPercent = double.Parse(Arg("--mountain-strength"), System.Globalization.CultureInfo.InvariantCulture); ClimateCalibration.Upgrade(recipe); }
    if (args.Contains("--continents")) recipe.ContinentCount = int.Parse(Arg("--continents"));
    if (args.Contains("--interconnectedness")) recipe.Interconnectedness = int.Parse(Arg("--interconnectedness"));
    if (args.Contains("--world") && new[] { "--seed", "--detail", "--lake-amount", "--mountain-strength", "--recipe", "--preset", "--continents", "--interconnectedness" }.Any(args.Contains)) throw new ArgumentException("Loading a world preserves its saved settings; generation overrides cannot accompany --world.");
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var w = args.Contains("--world") ? (Path.GetExtension(Arg("--world")).Equals(".world", StringComparison.OrdinalIgnoreCase) ? WorldFile.Read(File.ReadAllBytes(Arg("--world")), cancel.Token) : WorldJson.ReadWorld(File.ReadAllText(Arg("--world")))) : pipeline.Generate(recipe, p => Console.Error.WriteLine($"{p.Percent,3}% {p.Stage} [{sw.ElapsedMilliseconds} ms]"), cancel.Token);
    recipe = w.Recipe;
    if(args.Contains("--hominins"))
    {
        var engine=new Prehistory(w);var state=w.Prehistory?.DeepPast?.Active==true?w.Prehistory:engine.Start(engine.PrepareHominins());
        int years=int.Parse(Arg("--hominin-years","20000"));var timer=System.Diagnostics.Stopwatch.StartNew();
        state=engine.AdvanceYears(state,years,cancel.Token);engine.Validate(state);w.Prehistory=state;
        Console.WriteLine(HomininView.Status(state));
        Console.WriteLine("Hominin simulation: "+timer.Elapsed.TotalSeconds.ToString("F3")+" seconds; "+state.DeepPast.Populations.Length+" cohorts; "+state.DeepPast.Events.Length+" events");
        string report=Arg("--hominin-report");if(report.Length>0){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);File.WriteAllText(report,WorldJson.Write(state));}
    }
    foreach (var stage in w.Stages) Console.WriteLine(stage.Name + " " + stage.Hash);
    foreach (var warning in w.Warnings) Console.Error.WriteLine("Note: " + warning);
    foreach (var metric in w.Metrics.Where(m => !m.Name.StartsWith("parent_"))) Console.WriteLine($"{metric.Name}: {metric.Value} {metric.Unit}");
    Console.WriteLine("Elapsed: " + sw.ElapsedMilliseconds + "ms");
    if (args.Contains("--verify"))
    {
        var expected = w.Stages.Select(s => s.Hash).ToArray();
        foreach (var again in new[] { pipeline.Generate(recipe), pipeline.Reapply(recipe), pipeline.Generate(WorldJson.ReadRecipe(WorldJson.Write(recipe))), pipeline.Reconstruct(WorldJson.ReadWorld(WorldJson.Write(w))) })
            if (!expected.SequenceEqual(again.Stages.Select(s => s.Hash))) throw new Exception("Reconstruction hash mismatch.");
        Console.WriteLine("PASS repeat, reapply, recipe and cached-result reconstruction: all stage hashes identical.");
    }
    if (args.Contains("--render-check"))
    {
        foreach (var projection in new[] { Projection.Globe, Projection.Map })
        {
            long vertices = 0; int chunks = 0; var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (var view in SurfaceMesh.Chunks(w, new ViewOptions { Projection = projection, ShowPlates = true, Wireframe = true, ShowRivers = args.Contains("--rivers") }, cancel.Token)) { vertices += view.Positions.Length / 3; chunks++; }
            Console.WriteLine($"{projection}: {chunks} chunks, {vertices} vertices, {watch.ElapsedMilliseconds}ms");
        }
    }
    if (args.Contains("--map-preview"))
    {
        var pieces = SurfaceMesh.Chunks(w, new ViewOptions { Projection = Projection.Map, Layer = Enum.Parse<Layer>(Arg("--layer", "Continents")), GridSpacing = 0, ShowRivers = args.Contains("--rivers") }, cancel.Token).ToArray();
        int surfaceIndices = pieces.Take((w.Surface.Edges.Length + SurfaceMesh.EdgesPerChunk - 1) / SurfaceMesh.EdgesPerChunk).Sum(p => p.Triangles.Length);
        var view = SurfaceMesh.Combine(pieces);
        var svg = new System.Text.StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1440\" height=\"720\" viewBox=\"0 0 1440 720\"><rect width=\"1440\" height=\"720\" fill=\"#0b1824\"/>");
        string F(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        for (int t = 0; t < view.Triangles.Length; t += 3)
        {
            int first = view.Triangles[t] * 3;
            string color = "rgb(" + D.M(view.Colors[first] * 255) + "," + D.M(view.Colors[first + 1] * 255) + "," + D.M(view.Colors[first + 2] * 255) + ")";
            svg.Append("<path fill=\"").Append(color).Append("\" stroke=\"").Append(color).Append("\" stroke-width=\"").Append(t < surfaceIndices ? ".5" : "0").Append("\" d=\"");
            for (int k = 0; k < 3; k++) { int v = view.Triangles[t + k] * 3; svg.Append(k == 0 ? "M" : "L").Append(F((view.Positions[v] + 2) * 360)).Append(' ').Append(F((1 - view.Positions[v + 1]) * 360)); }
            svg.Append("Z\"/>");
        }
        svg.Append("</svg>"); string target = Path.GetFullPath(Arg("--map-preview")); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllText(target, svg.ToString());
    }
    if (args.Contains("--save-world"))
    {
        string file = Path.GetFullPath(Arg("--save-world")); Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, WorldFile.Write(w, cancel.Token));
        Console.WriteLine("Saved compact world: " + file);
        if (args.Contains("--install-earth-library"))
        {
            if (w.Recipe.TerrainSource != Recipe.Preset("Earth").TerrainSource) throw new ArgumentException("Only measured Earth can seed the bundled Earth entry.");
            new WorldLibrary(Arg("--install-earth-library")).InstallEarth(file, cancel.Token);
        }
    }
    if (args.Contains("--summary")) return;
    string output = Arg("--output", "artifacts/latest"); Directory.CreateDirectory(output);
    File.WriteAllText(Path.Combine(output, "recipe.json"), WorldJson.Write(w.Recipe)); File.WriteAllText(Path.Combine(output, "world.json"), WorldJson.Write(w)); File.WriteAllText(Path.Combine(output, "hashes.json"), WorldJson.Write(w.Stages)); File.WriteAllText(Path.Combine(output, "cells.csv"), WorldJson.Csv(w)); File.WriteAllBytes(Path.Combine(output, "heightmap.pgm"), WorldJson.Heightmap(w));
}
catch (OperationCanceledException) { Console.Error.WriteLine("Generation cancelled."); Environment.ExitCode = 130; }
catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
