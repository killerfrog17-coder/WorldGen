using System.Collections.Generic;
namespace WorldGen.Core
{
    public sealed class GoldenCase { public string Name; public Recipe Recipe; }
    public static class GoldenCorpus
    {
        public static IEnumerable<GoldenCase> Recipes()
        {
            var ordinary = Recipe.Preset("Preview"); yield return new GoldenCase { Name = "ordinary", Recipe = ordinary };
            var connected = Recipe.Preset("Connected"); connected.MacroCellCount = 2562; connected.Seed = 42; yield return new GoldenCase { Name = "connected", Recipe = connected };
            var isolated = Recipe.Preset("Isolated"); isolated.MacroCellCount = 2562; isolated.Seed = 912; yield return new GoldenCase { Name = "isolated", Recipe = isolated };
            var escape = Recipe.Preset("Preview"); escape.Seed = 1701; escape.EscapeDistanceKm = 5000; escape.CollisionFrequency = 1; yield return new GoldenCase { Name = "escape-face", Recipe = escape };
            var dead = Recipe.Preset("Connected"); dead.MacroCellCount = 2562; dead.Seed = 812; dead.EscapeDistanceKm = 200; dead.CollisionFrequency = 1; dead.LandFraction = .65; yield return new GoldenCase { Name = "dead-end", Recipe = dead };
            var inland = dead.Clone(); inland.Seed = 154; inland.LandFraction = .65; inland.InlandSeaMinAreaKm2 = 1000; inland.InlandSeaMinDepthM = 10; yield return new GoldenCase { Name = "inland-sea", Recipe = inland };
            var glacial = Recipe.Preset("Glacial"); glacial.MacroCellCount = 2562; glacial.Seed = 131; yield return new GoldenCase { Name = "glacial-basin", Recipe = glacial };
            var stress = Recipe.Preset("500 microplates"); stress.Seed = 500; yield return new GoldenCase { Name = "500-microplates", Recipe = stress };
        }
    }
}
