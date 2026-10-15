using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.ClimateTraining;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
bool aggressive = args.Contains("--aggressive");
string algorithm = aggressive ? ClimateCalibration.AggressiveAlgorithm : ClimateCalibration.Algorithm;
string Arg(string flag, string fallback = "") { int i = Array.IndexOf(args, flag); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
void Write(string path, object value)
{
    string temporary = path + ".tmp";
    File.WriteAllText(temporary, JsonSerializer.Serialize(value, value.GetType(), json));
    File.Move(temporary, path, true);
}
Recipe RecipeFor(double[] weights)
{
    var r = Recipe.Preset("Earth"); r.Detail = 20000;
    r.AlgorithmVersion = algorithm; r.ClimateWeights = (double[])weights.Clone(); r.Validate(); return r;
}
if (args.Contains("--self-test"))
{
    // No Earth generation, observations download or optimization run in this test mode.
    void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    var defaults = new Recipe(); defaults.Validate();
    Check(defaults.ClimateWeights.SequenceEqual(ClimateCalibration.DefaultWeights()), "New worlds use approved weights");
    var another = new Recipe(); defaults.ClimateWeights[0] = 0;
    Check(another.ClimateWeights[0] == -0.687057, "Default recipes own separate arrays");
    foreach (var preset in new[] { "Preview", "Earth", "Continental", "Glacial", "Ocean world" })
        Check(Recipe.Preset(preset).ClimateWeights.SequenceEqual(ClimateCalibration.DefaultWeights()), "Presets use approved weights");
    string approvedPath = "artifacts/climate-learning/wild-20261002-174123-921/selected.recipe.json";
    if (File.Exists(approvedPath)) Check(WorldJson.ReadRecipe(File.ReadAllText(approvedPath)).ClimateWeights.SequenceEqual(another.ClimateWeights), "Defaults exactly match approved iteration277");
    var r = RecipeFor(ClimateCalibration.Baseline); var copy = r.Clone(); copy.ClimateWeights[0] = 2;
    ClimateCalibration.Upgrade(r); Check(r.AlgorithmVersion == Recipe.CurrentAlgorithm, "Reapply upgrades controls while preserving weights");
    if (aggressive)
    {
        RecipeFor(ClimateCalibration.AggressiveMinimum).Validate(); RecipeFor(ClimateCalibration.AggressiveMaximum).Validate();
        Check(Enumerable.Range(0, 1000).Any(i => Calibration.Propose(r.ClimateWeights, i, true)[4] > 10), "Aggressive search actually explores beyond conservative bounds");
    }
    Check(r.ClimateWeights[0] == 0, "Recipe clone must own its weights");
    Check(StateHash.Compute(r) != StateHash.Compute(copy), "Weights must affect recipe hash");
    Check(WorldJson.Write(WorldJson.ReadRecipe(WorldJson.Write(r))) == WorldJson.Write(r), "Recipe roundtrip");
    var legacy = new Recipe(); ClimateCalibration.Reset(legacy); string oldJson = WorldJson.Write(legacy).Replace("\"ClimateWeights\":null,", "");
    Check(WorldJson.ReadRecipe(oldJson).ClimateWeights == null, "Legacy recipe import");
    foreach (double bad in new[] { double.NaN, double.PositiveInfinity, -100, 101 })
    {
        var invalid = r.Clone(); invalid.ClimateWeights[4] = bad; bool rejected = false;
        try { invalid.Validate(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Unsafe weights rejected");
    }
    for (int i = 0; i < 1000; i++)
    {
        var proposal = Calibration.Propose(r.ClimateWeights, i, aggressive); RecipeFor(proposal).Validate();
        Check(proposal.SequenceEqual(Calibration.Propose(r.ClimateWeights, i, aggressive)), "Deterministic proposals");
    }
    Check(Calibration.Penalty(ClimateCalibration.Baseline) == 0, "Baseline penalty");
    Check(Calibration.Penalty(copy.ClimateWeights) > 0, "Regularization");
    ClimateCalibration.Reset(copy); copy.Validate(); Check(copy.ClimateWeights == null, "Rollback");
    Check(!Calibration.NoMaterialRegression(new Score(10, .1, 1, 1, 100), new Score(1, 1, 1, 1, 100)), "Temperature regression gate");
    ClimateTests.Run(Check); // Tiny synthetic transects; no Earth fitting.
    var fixture = new World { Surface = new Surface { Cells = new Cell[20000] }, ElevationM = new int[20000], Hydrology = new HydrologyResult { OceanConnected = new bool[20000] } };
    fixture.Climate.AtmosphericMonths = Enumerable.Range(0, 12).Select(m => new ClimateMonth { TemperatureTenthsC = Enumerable.Repeat(((m + 2) % 12) * 10, 20000).ToArray(), PrecipitationMm = Enumerable.Repeat(((m + 2) % 12) + 1, 20000).ToArray() }).ToArray();
    var synthetic = new List<Observation>();
    for (int i = 0; i < 20000; i++)
    {
        int slot = i % 540, y = slot / 30 * 5 + 2, x = slot % 30 * 6 + 2;
        double lat = (89 - y * 2) * Math.PI / 180, lon = (-179 + x * 2) * Math.PI / 180;
        fixture.Surface.Cells[i] = new Cell { Id = i, AreaM2 = 1000, Position = new V(Math.Cos(lat) * Math.Cos(lon), Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat)) };
        if (i < 540) synthetic.Add(new Observation(y * 180 + x, new[] { "train", "validation", "test" }[slot % 3], y / 15 * 6 + x / 30, 1, 0, Enumerable.Range(0, 12).Select(m => (double)m).ToArray(), Enumerable.Range(1, 12).Select(m => (double)m).ToArray()));
    }
    Check(Calibration.Evaluate(fixture, synthetic.ToArray(), true).Values.All(s => s.Loss == 0), "Geographic and Jan/March month alignment");
    foreach (var observation in synthetic.Where(o => o.Split == "test")) Array.Fill(observation.T, 50);
    Check(Calibration.Evaluate(fixture, synthetic.ToArray()).Values.All(s => s.Loss == 0), "Held-out data cannot affect training/validation objective");
    Check(Calibration.Evaluate(fixture, synthetic.ToArray(), true)["test"].Loss > 0, "Held-out regression is measured separately");
    string dataCheck = Arg("--check-data");
    if (dataCheck.Length > 0) Console.WriteLine($"Parsed {Calibration.ReadObservations(dataCheck).Length} observation blocks (no model evaluation).");
    // Optional existing snapshot verification, without regenerating it.
    string snapshot = Arg("--check-snapshot");
    if (snapshot.Length > 0) WorldFile.Read(File.ReadAllBytes(snapshot));
    string comparison = Arg("--compare-snapshot");
    if (comparison.Length > 0)
    {
        var first = WorldFile.Read(File.ReadAllBytes(snapshot)); var second = WorldFile.Read(File.ReadAllBytes(comparison));
        Check(first.Stages.Select(s => s.Hash).SequenceEqual(second.Stages.Select(s => s.Hash)), "Untrained all-stage compatibility with previous executable");
    }
    if (args.Contains("--pipeline-check"))
    {
        var probe = RecipeFor(ClimateCalibration.Baseline); probe.Detail = 5000; probe.TerrainSource = "Procedural"; probe.ClimateWeights[4] = 1.1;
        var pipeline = new WorldPipeline(); var first = pipeline.Generate(probe);
        foreach (var other in new[] { pipeline.Reapply(probe), pipeline.Generate(WorldJson.ReadRecipe(WorldJson.Write(probe))), pipeline.Reconstruct(WorldFile.Read(WorldFile.Write(first))) })
            Check(first.Stages.Select(s => s.Hash).SequenceEqual(other.Stages.Select(s => s.Hash)), "Nonneutral profile fresh/reapply/recipe/snapshot all-stage equality");
        var reset = probe.Clone(); ClimateCalibration.Reset(reset); var untrained = pipeline.Generate(reset);
        Check(first.CoarseHash == untrained.CoarseHash && first.ElevationM.SequenceEqual(untrained.ElevationM) && first.Hydrology.OceanConnected.SequenceEqual(untrained.Hydrology.OceanConnected), "Calibration leaves terrain and ocean mask unchanged");
        Check(StateHash.Compute(first.Climate) != StateHash.Compute(untrained.Climate), "A nonneutral process coefficient actually changes climate");
    }
    Console.WriteLine("PASS: calibration bounds, cloning, serialization, hashing, proposal determinism, regularization, rollback and regression gates. No training run.");
    return;
}
if (args.Contains("--help") || args.Length == 0)
{
    Console.WriteLine("Add --aggressive for wide-range3.26 fitting: no regularization, joint training+validation objective, full requested budget. Start a new run; conservative checkpoints remain separate.");
    Console.WriteLine("Earth climate calibration (ten bounded simulation weights; no classifier fitting).\n--data observations.csv --output NEW_FOLDER --iterations 100 --library world\n--resume RUN_FOLDER --data observations.csv --iterations 100 --library world\nIterations are additional proposals on resume. Ctrl+C checkpoints completed work; rerun with --resume.\n--self-test [--check-snapshot FILE] runs only contract tests, never training.");
    return;
}
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); Console.WriteLine("Cancelling; last completed iteration is checkpointed."); };
try
{
    int iterations = int.Parse(Arg("--iterations", "100"));
    if (iterations < 1 || iterations > 10000) throw new ArgumentException("Choose 1-10000 iterations.");
    string data = Path.GetFullPath(Arg("--data")), resume = Arg("--resume");
    string output = Path.GetFullPath(resume.Length > 0 ? resume : Arg("--output"));
    string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(data))).ToLowerInvariant();
    var observations = Calibration.ReadObservations(data);
    Checkpoint state;
    if (resume.Length > 0)
    {
        state = JsonSerializer.Deserialize<Checkpoint>(File.ReadAllText(Path.Combine(output, "checkpoint.json")), json);
        if (state == null || state.Algorithm != algorithm || state.DataHash != hash) throw new InvalidDataException("Checkpoint mode/algorithm/data mismatch. Start a new aggressive run rather than resuming a conservative checkpoint.");
        RecipeFor(state.Current); RecipeFor(state.Selected);
        if (state.Completed < 0 || state.Completed > 100000 || !double.IsFinite(state.CurrentLoss) || !double.IsFinite(state.SelectedValidation) || state.BaselineTrain == null || state.BaselineValidation == null) throw new InvalidDataException("Invalid checkpoint.");
        state.StaleChecks = 0; // Explicit continuation permits another bounded search burst.
    }
    else
    {
        if (Directory.Exists(output)) throw new IOException("Use a new output directory or --resume; existing runs are never overwritten.");
        Directory.CreateDirectory(output);
        File.Copy(data, Path.Combine(output, "observations.csv"));
        string provenance = Path.Combine(Path.GetDirectoryName(data), "source.json");
        if (File.Exists(provenance)) File.Copy(provenance, Path.Combine(output, "source.json"));
        state = new Checkpoint { DataHash = hash, Algorithm = algorithm };
    }
    var pipeline = new WorldPipeline();
    var elapsed = Stopwatch.StartNew(); int evaluations = 0;
    World Generate(double[] weights)
    {
        cancel.Token.ThrowIfCancellationRequested();
        var watch = Stopwatch.StartNew();
        var world = pipeline.Generate(RecipeFor(weights), cancellation: cancel.Token); evaluations++;
        Console.WriteLine($"  Simulation {watch.Elapsed.TotalSeconds:F1}s; monthly water residual {world.Climate.AtmosphericMonths.Max(m => m.MoistureBudgetResidualPpm)} ppm.");
        return world;
    }
    Console.WriteLine("Only global bounded mechanism weights are learned. Classification, circulation geometry and terrain cannot be edited by this optimizer.");
    Console.WriteLine("20k Earth grid; 12 monthly solves per proposal. No defaults or existing worlds will be replaced.");
    if (aggressive) Console.WriteLine("AGGRESSIVE: wide log-scale exploration, no regularization/component guards/early stop. Training+validation jointly fitted; only test remains held out. Numerical water-budget guard retained.");
    if (resume.Length == 0)
    {
        Console.WriteLine("Measuring unchanged baseline...");
        var baseline = Generate(ClimateCalibration.Baseline); var scores = Calibration.Evaluate(baseline, observations);
        state.BaselineTrain = scores["train"]; state.BaselineValidation = scores["validation"];
        state.CurrentLoss = aggressive ? (state.BaselineTrain.Loss + state.BaselineValidation.Loss) / 2 : state.BaselineTrain.Loss;
        state.SelectedValidation = aggressive ? state.CurrentLoss : state.BaselineValidation.Loss;
        state.BaselineResidual = baseline.Climate.AtmosphericMonths.Max(m => m.MoistureBudgetResidualPpm);
        Write(Path.Combine(output, "checkpoint.json"), state);
        File.WriteAllText(Path.Combine(output, "baseline.recipe.json"), WorldJson.Write(RecipeFor(ClimateCalibration.Baseline)));
        File.WriteAllText(Path.Combine(output, "iterations.csv"), "iteration,seconds,train_loss,validation_loss,temp_mae_c,rain_log_mae,penalty,accepted,selected,water_ok\n");
        Console.WriteLine($"Baseline evaluation: {elapsed.Elapsed.TotalSeconds:F1}s. Rough remaining compute estimate: {elapsed.Elapsed.TotalMinutes * (iterations + 3):F1} minutes (plus saving). ");
    }
    int end = checked(state.Completed + iterations);
    for (int iteration = state.Completed; iteration < end; iteration++)
    {
        var candidate = Calibration.Propose(state.Current, iteration, aggressive);
        Console.WriteLine($"Proposal {iteration + 1}/{end}"); var world = Generate(candidate);
        var scores = Calibration.Evaluate(world, observations); var train = scores["train"]; var validation = scores["validation"];
        double penalty = aggressive ? 0 : Calibration.Penalty(candidate);
        double loss = aggressive ? (train.Loss + validation.Loss) / 2 : train.Loss + penalty;
        bool waterOk = world.Climate.AtmosphericMonths.Max(m => m.MoistureBudgetResidualPpm) <= Math.Max(100, state.BaselineResidual * 1.2 + 50);
        bool accepted = waterOk && double.IsFinite(loss) && loss < state.CurrentLoss && (aggressive || Calibration.NoMaterialRegression(train, state.BaselineTrain));
        bool selected = false;
        if (accepted) { state.Current = candidate; state.CurrentLoss = loss; }
        // Conservative mode uses validation only for checkpoint selection.
        // Aggressive mode deliberately fits both sets; only test remains held out.
        if (accepted && (aggressive || (validation.Loss + penalty < state.SelectedValidation && Calibration.NoMaterialRegression(validation, state.BaselineValidation))))
        {
            state.Selected = (double[])candidate.Clone(); state.SelectedValidation = aggressive ? loss : validation.Loss + penalty;
            state.SelectedIteration = iteration + 1; state.StaleChecks = 0; selected = true;
            File.WriteAllText(Path.Combine(output, "selected.recipe.json"), WorldJson.Write(RecipeFor(candidate)));
        }
        else state.StaleChecks++;
        state.Completed = iteration + 1;
        Write(Path.Combine(output, "checkpoint.json"), state);
        File.AppendAllText(Path.Combine(output, "iterations.csv"), $"{state.Completed},{elapsed.Elapsed.TotalSeconds:F2},{train.Loss:F6},{validation.Loss:F6},{train.TemperatureMaeC:F4},{train.RainLogMae:F4},{penalty:F6},{accepted},{selected},{waterOk}\n");
        Console.WriteLine($"  Train {train.Loss:F4}; validation {validation.Loss:F4}; temperature MAE {train.TemperatureMaeC:F2} C; rain log-MAE {train.RainLogMae:F3}. {(selected ? (aggressive ? "BEST JOINT FIT" : "BEST VALIDATED") : accepted ? "training improved" : "kept previous")}");
        Console.WriteLine($"  Estimate remaining: {elapsed.Elapsed.TotalMinutes / evaluations * (end - state.Completed + 2):F1} min. Checkpoint saved.");
        if (!aggressive && state.StaleChecks >= 60) { Console.WriteLine("Early stop: 60 proposals without a validation improvement. Explicit resume remains possible."); break; }
    }
    Console.WriteLine("Final held-out evaluation (not used for proposals or checkpoint ranking)...");
    var finalBaseline = Generate(ClimateCalibration.Baseline); var selectedWorld = Generate(state.Selected);
    var baselineAll = Calibration.Evaluate(finalBaseline, observations, true); var selectedAll = Calibration.Evaluate(selectedWorld, observations, true);
    bool holdoutPassed = selectedAll["test"].Loss < baselineAll["test"].Loss && Calibration.NoMaterialRegression(selectedAll["test"], baselineAll["test"]);
    var report = new { state.Completed, state.SelectedIteration, Aggressive = aggressive, HoldoutPassed = holdoutPassed,
        Warning = "Earth-only fitting cannot prove generalization to other continents or planetary climates. Repeated runs inspected against this same test set weaken its independence. No automatic promotion.",
        DataHash = hash, Algorithm = algorithm, Detail = 20000, Baseline = baselineAll, Selected = selectedAll,
        Weights = ClimateCalibration.Names.Select((name, i) => new { Name = name, Baseline = ClimateCalibration.Baseline[i], Selected = state.Selected[i], Min = (aggressive ? ClimateCalibration.AggressiveMinimum : ClimateCalibration.Minimum)[i], Max = (aggressive ? ClimateCalibration.AggressiveMaximum : ClimateCalibration.Maximum)[i] }).ToArray() };
    Write(Path.Combine(output, "report.json"), report);
    File.WriteAllText(Path.Combine(output, "selected.recipe.json"), WorldJson.Write(selectedWorld.Recipe));
    File.WriteAllBytes(Path.Combine(output, "selected.world"), WorldFile.Write(selectedWorld, cancel.Token));
    File.WriteAllBytes(Path.Combine(output, "baseline.world"), WorldFile.Write(finalBaseline, cancel.Token));
    string libraryPath = Arg("--library");
    if (libraryPath.Length > 0)
    {
        var library = new WorldLibrary(libraryPath); string name = Path.GetFileName(output);
        var baselineEntry = library.SaveNew("Earth baseline " + name, finalBaseline, cancel.Token);
        var selectedEntry = library.SaveNew((holdoutPassed ? "Earth learned " : "Earth experiment CHECK ") + name, selectedWorld, cancel.Token);
        Write(Path.Combine(output, "library-entries.json"), new[] { baselineEntry, selectedEntry });
    }
    Console.WriteLine($"Done. Held-out check: {(holdoutPassed ? "improved" : "NOT improved / inspect before using")}. Defaults unchanged.\nOpen Your worlds and Refresh for the baseline/learned copies.\nReport and reversible recipe: {output}");
}
catch (OperationCanceledException) { Console.WriteLine("Stopped. Resume the run to continue from its last completed checkpoint."); Environment.ExitCode = 2; }
catch (Exception error) { Console.Error.WriteLine(error.ToString()); Environment.ExitCode = 1; }
