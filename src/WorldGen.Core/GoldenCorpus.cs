using System.Collections.Generic;
namespace WorldGen.Core
{
    public sealed class GoldenCase { public string Name; public Recipe Recipe; }
    public static class GoldenCorpus
    {
        public static IEnumerable<GoldenCase> Recipes()
        {
            yield return new GoldenCase { Name = "ordinary", Recipe = Recipe.Preset("Preview") };
            var ocean = Recipe.Preset("Ocean world"); ocean.Detail = 20000; ocean.Seed = 42; yield return new GoldenCase { Name = "ocean-world", Recipe = ocean };
            var continental = Recipe.Preset("Continental"); continental.Detail = 20000; continental.Seed = 812; yield return new GoldenCase { Name = "continental", Recipe = continental };
            var sparse = Recipe.Preset("Preview"); sparse.Seed = 1701; sparse.MicroplateCount = 4; sparse.ContinentCount = 2; yield return new GoldenCase { Name = "sparse-plates", Recipe = sparse };
            var glacial = Recipe.Preset("Glacial"); glacial.Detail = 20000; glacial.Seed = 131; yield return new GoldenCase { Name = "glacial", Recipe = glacial };
            var stress = Recipe.Preset("500 plates"); stress.Detail = 20000; stress.Seed = 500; yield return new GoldenCase { Name = "500-plates", Recipe = stress };
            var wet = Recipe.Preset("Preview"); wet.LandFraction = 0; wet.Seed = 154; yield return new GoldenCase { Name = "water-world", Recipe = wet };
            var dry = Recipe.Preset("Preview"); dry.LandFraction = 1; dry.Seed = 912; yield return new GoldenCase { Name = "land-world", Recipe = dry };
        }
    }
}
