using System;

namespace WorldGen.Core
{
    /// <summary>Global bounded process coefficients, never an Earth lookup or a class override.</summary>
    public static class ClimateCalibration
    {
        public const string Algorithm = "worldgen-3.25.0";
        public const string UntrainedAlgorithm = "worldgen-3.24.0";
        // Owner-approved wild-20261002-174123-921, selected iteration277. Fresh array per recipe.
        public static double[] DefaultWeights() => new[] { -0.687057, 0.827853, 0.801046, 0.801431, 0.700332, 3.990525, 79.873156, 0.621944, 1.107598, 2.571496 };
        public static void UseDefault(Recipe recipe) { recipe.ClimateWeights = DefaultWeights(); recipe.AlgorithmVersion = Recipe.CurrentAlgorithm; }
        public const string AggressiveAlgorithm = "worldgen-3.26.0";
        public static readonly string[] Names = { "ThermalBiasC", "SeasonalAmplitude", "ContinentalCooling", "MarineModeration", "OceanEvaporation", "StormExchange", "FrontalRainout", "OrographicRainout", "Convection", "LandExchange" };
        public static readonly double[] Baseline = { 0, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
        public static readonly double[] Minimum = { -3, .75, .5, .7, .65, .5, .5, .65, .5, .5 };
        public static readonly double[] Maximum = { 3, 1.2, 1.5, 1.3, 1.4, 1.7, 1.7, 1.5, 1.4, 1.5 };
        public static readonly double[] AggressiveMinimum = { -20, .01, .001, .001, .001, .001, .001, .001, .001, .001 };
        // Marine moderation is a mixing fraction: .7 * multiplier must not exceed one.
        public static readonly double[] AggressiveMaximum = { 20, 5, 10, 1.428571, 100, 100, 100, 100, 100, 100 };
        public static double Weight(Recipe recipe, int index) => recipe?.ClimateWeights == null ? Baseline[index] : recipe.ClimateWeights[index];
        public static void Validate(Recipe recipe)
        {
            var values = recipe.ClimateWeights;
            if (values == null)
            {
                if (recipe.AlgorithmVersion == Algorithm || recipe.AlgorithmVersion == AggressiveAlgorithm) throw new ArgumentException("Calibration recipe is missing its weights.");
                return;
            }
            bool aggressive = recipe.AlgorithmVersion == AggressiveAlgorithm || ((recipe.AlgorithmVersion == Recipe.CurrentAlgorithm || recipe.AlgorithmVersion == Recipe.SubcellAlgorithm) || recipe.AlgorithmVersion == Recipe.FeatureBlendAlgorithm || recipe.AlgorithmVersion == Recipe.RangeControlsAlgorithm || recipe.AlgorithmVersion == Recipe.EcologyAlgorithm) || recipe.AlgorithmVersion == Recipe.TerrainControlsAlgorithm;
            if ((!aggressive && recipe.AlgorithmVersion != Algorithm) || values.Length != Names.Length) throw new ArgumentException("Unsupported climate calibration contract.");
            var minimum = aggressive ? AggressiveMinimum : Minimum; var maximum = aggressive ? AggressiveMaximum : Maximum;
            for (int i = 0; i < values.Length; i++)
            {
                if (double.IsNaN(values[i]) || double.IsInfinity(values[i]) || values[i] < minimum[i] || values[i] > maximum[i])
                    throw new ArgumentException("Climate calibration outside safe bounds: " + Names[i]);
                values[i] = D.Q(values[i], 1000000);
            }
        }
        public static void Reset(Recipe recipe) { recipe.ClimateWeights = null; recipe.AlgorithmVersion = Recipe.CurrentAlgorithm; }
        public static void Upgrade(Recipe recipe)
        {
            if(recipe.AlgorithmVersion != Recipe.CurrentAlgorithm && recipe.AlgorithmVersion != Recipe.SubcellAlgorithm && recipe.AlgorithmVersion!=Recipe.FeatureBlendAlgorithm && recipe.AlgorithmVersion!=Recipe.RangeControlsAlgorithm){recipe.MountainStrengthPercent=D.Q(recipe.MountainStrengthPercent/6,1000000);recipe.LakeAmountPercent=Math.Min(100000,recipe.LakeAmountPercent*5);}
            recipe.AlgorithmVersion=Recipe.CurrentAlgorithm;
        }
    }
}
