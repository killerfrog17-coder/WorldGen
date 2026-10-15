using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class TerrainControlTests
{
    public static void Run(Action<bool, string> check)
    {
        foreach (double amount in new[] { 0, .0001, .125, 100, 1234.5678, 100000 })
        {
            var r = Recipe.Preset("Preview"); r.Detail = 5000; r.MountainStrengthPercent = amount; r.LakeAmountPercent = amount; r.Validate();
            var restored = WorldJson.ReadRecipe(WorldJson.Write(r));
            check(restored.MountainStrengthPercent == amount && restored.LakeAmountPercent == amount, "Extreme percentages roundtrip exactly");
            check(Math.Abs(PercentageControl.Value(PercentageControl.Position(amount)) - amount) < .0001, "Log slider and number input agree");
        }
        var pipeline = new WorldPipeline(); var recipe = Recipe.Preset("Preview"); recipe.Detail = 5000;
        var baseline = pipeline.Generate(recipe);
        var legacyRecipe = recipe.Clone(); legacyRecipe.AlgorithmVersion = ClimateCalibration.AggressiveAlgorithm;
        var legacy = pipeline.Generate(legacyRecipe);
        check(!baseline.ElevationM.SequenceEqual(legacy.ElevationM), "New mountain baseline and belt variability change relief");
        var oldEcologyRecipe=recipe.Clone();oldEcologyRecipe.AlgorithmVersion=Recipe.EcologyAlgorithm;
        var oldEcology=pipeline.Generate(oldEcologyRecipe);
        check(oldEcology.ElevationM.SequenceEqual(legacy.ElevationM) && StateHash.Compute(oldEcology.Climate)==StateHash.Compute(legacy.Climate),"Old 3.28 terrain and climate remain compatible");
        check(Hydrology.LakeScale(recipe)==Hydrology.LakeScale(new Recipe{AlgorithmVersion=Recipe.EcologyAlgorithm,LakeAmountPercent=20}),"100 percent lakes equals old 20 percent forcing");
        var upgrade=oldEcologyRecipe.Clone();upgrade.MountainStrengthPercent=600;upgrade.LakeAmountPercent=20;ClimateCalibration.Upgrade(upgrade);
        check(upgrade.MountainStrengthPercent==100&&upgrade.LakeAmountPercent==100,"Old control values convert to new baselines");
        foreach(int seed in new[]{73421,42})
        {
            var varied=recipe.Clone();varied.Seed=seed;var terrain=pipeline.Generate(varied);
            var amplitudes=terrain.Features.Where(f=>f.Kind==FeatureKind.Cordillera||f.Kind==FeatureKind.CollisionBelt||f.Kind==FeatureKind.ObliqueOrogen).Select(f=>f.AmplitudeM).OrderBy(v=>v).ToArray();
            check(amplitudes.Length>2 && amplitudes.Last()>amplitudes.First()*3,"Mountains retain a wide within-world severity distribution");
            Console.WriteLine($"Mountain seed {seed}: {amplitudes.Length} belts, amplitude {amplitudes.First()}–{amplitudes.Last()}m, median {amplitudes[amplitudes.Length/2]}m; final peak {terrain.ElevationM.Max()}m");
        }
        foreach (double value in new[] { 0, .0001, 400, 100000 })
        {
            var r = recipe.Clone(); r.MountainStrengthPercent = value; r.LakeAmountPercent = value;
            var w = pipeline.Generate(r);
            check(w.CoarseHash == baseline.CoarseHash, "Mountain slider preserves coarse plates");
            check(!w.TectonicM.SequenceEqual(baseline.TectonicM), "Mountain strength actually changes relief");
            if (value > 100) check(w.TectonicM.Max() > baseline.TectonicM.Max(), "Higher mountain strength raises tectonic peaks");
            check(w.Hydrology.LakeCoverageFraction.All(x => double.IsFinite(x) && x >= 0 && x <= 1), "Extreme lake setting stays numerically valid");
            if (value == 0) check(w.Hydrology.LakeCoverageFraction.All(x => x == 0), "Zero still disables lakes");
            check(WorldFile.Read(WorldFile.Write(w)).Stages.Last().Hash == w.Stages.Last().Hash, "Extreme control snapshot roundtrip");
            if (value == 400)
            {
                foreach (var again in new[] { pipeline.Reapply(r), pipeline.Generate(WorldJson.ReadRecipe(WorldJson.Write(r))), pipeline.Reconstruct(w) })
                    check(w.Stages.Select(s => s.Hash).SequenceEqual(again.Stages.Select(s => s.Hash)), "Changed controls reconstruct all stages identically");
            }
        }
        var earthRecipe = Recipe.Preset("Earth"); earthRecipe.Detail = 5000;
        var earth = pipeline.Generate(earthRecipe); earthRecipe.MountainStrengthPercent = 100000;
        check(earth.ElevationM.SequenceEqual(pipeline.Generate(earthRecipe).ElevationM), "Measured Earth is not rescaled by mountain strength");
    }
}
