using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

try
{
    string Arg(string name, string fallback = "") { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
    if (args.Contains("--help")) { Console.WriteLine("WorldGen --preset Preview|\"Orogen defaults\"|\"Ocean world\"|Continental|Glacial|\"500 plates\" --seed N --recipe file.json --output directory --verify --corpus directory\nOutputs recipe, complete result, cells.csv, heightmap.pgm, stage hashes. Ctrl+C cancels."); return; }
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
    if (args.Contains("--seed")) recipe.Seed = int.Parse(Arg("--seed"));
    if (args.Contains("--detail")) recipe.Detail = int.Parse(Arg("--detail"));
    var sw = System.Diagnostics.Stopwatch.StartNew(); var w = pipeline.Generate(recipe, p => Console.Error.WriteLine($"{p.Percent,3}% {p.Stage} [{sw.ElapsedMilliseconds} ms]"), cancel.Token);
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
            foreach (var view in SurfaceMesh.Chunks(w, new ViewOptions { Projection = projection, ShowPlates = true, Wireframe = true }, cancel.Token)) { vertices += view.Positions.Length / 3; chunks++; }
            Console.WriteLine($"{projection}: {chunks} chunks, {vertices} vertices, {watch.ElapsedMilliseconds}ms");
        }
    }
    if (args.Contains("--summary")) return;
    string output = Arg("--output", "artifacts/latest"); Directory.CreateDirectory(output);
    File.WriteAllText(Path.Combine(output, "recipe.json"), WorldJson.Write(w.Recipe)); File.WriteAllText(Path.Combine(output, "world.json"), WorldJson.Write(w)); File.WriteAllText(Path.Combine(output, "hashes.json"), WorldJson.Write(w.Stages)); File.WriteAllText(Path.Combine(output, "cells.csv"), WorldJson.Csv(w)); File.WriteAllBytes(Path.Combine(output, "heightmap.pgm"), WorldJson.Heightmap(w));
}
catch (OperationCanceledException) { Console.Error.WriteLine("Generation cancelled."); Environment.ExitCode = 130; }
catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
