using WorldGen.Core;

static class TerrainScaleTests
{
    public static void Run(Action<bool, string> check)
    {
        var recipe = Recipe.Preset("Preview");
        recipe.Detail = 5000;
        recipe.Seed = 73421;
        var world = new WorldPipeline().Generate(recipe);

        var active = world.Features.Where(f => f.Kind == FeatureKind.Hotspot || f.Kind == FeatureKind.RidgeHotspot).ToArray();
        check(active.Length > 0, "Hotspot scale fixture contains active centres");
        check(active.Where(f => f.Kind == FeatureKind.Hotspot).All(f => f.WidthM >= 38250 && f.WidthM <= 97750), "Ordinary hotspot supports stay at the smaller 3.30 edifice scale");
        check(active.Where(f => f.Kind == FeatureKind.RidgeHotspot).All(f => f.WidthM >= 76500 && f.WidthM <= 195500), "Ridge-hotspot supports use their distinct smaller scale");
        check(world.Features.Where(f => f.Kind == FeatureKind.IslandChain).All(f => f.WidthM > 0 && f.WidthM <= 260000), "Trail edifices are not widened to coarse-cell size");
        check(world.Features.Where(f => f.Kind == FeatureKind.LargeIgneousProvince).All(f => f.WidthM >= 250000 && f.WidthM <= 850000), "Broad igneous provinces remain separate from hotspot edifices");

        foreach (var hot in active)
        {
            var trail = world.Features.Skip(hot.Id + 1).TakeWhile(f => f.Kind == FeatureKind.IslandChain).ToArray();
            if (trail.Length == 0) continue;
            double lengthM = D.Angle(hot.Center, trail[trail.Length - 1].Center) * recipe.RadiusKm * 1000;
            check(lengthM >= 40000 && lengthM <= 3400000, "Hotspot trails use bounded Earth-scale lengths");
        }

        var inherited = world.Features.Where(f => f.Kind == FeatureKind.AncientOrogen && f.BoundaryId < 0 && f.AgeMyr >= 180).ToArray();
        check(inherited.Length > 0, "Continental interiors receive synthetic ancient orogens");
        bool elongated = false;
        foreach (var range in inherited)
        {
            check(range.WidthM >= 140000 && range.WidthM <= 300000, "Ancient orogen width is physically bounded");
            check(range.AmplitudeM >= 200 && range.AmplitudeM <= 1250, "Ancient orogen relief is subdued");
            check(range.Cells.All(i => world.Continent[i] >= 0), "Ancient orogens remain on continental terrain");
            check(range.Cells.All(i => world.Microplates[world.CellMicroplate[i]].ParentAffinityId == range.OwnerParent), "Ancient orogen provenance retains one owner parent");
            check(range.Cells.All(i => world.BoundaryDistanceM[i] >= 420000), "Synthetic ancient orogens are independent of current plate boundaries");
            elongated |= range.Cells.Any(i => D.Angle(range.Center, world.Surface.Cells[i].Position) * recipe.RadiusKm * 1000 > range.WidthM * 1.5);
        }
        check(elongated, "Ancient orogen support forms an elongated belt rather than one circular stamp");
    }
}
