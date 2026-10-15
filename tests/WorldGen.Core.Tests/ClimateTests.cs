using WorldGen.Core;

public static class ClimateTests
{
    public static void Run(Action<bool, string> check)
    {
        var flat = Transect(45, false); Climate.Build(flat, default);
        var tropicalControl = Transect(5, false, false, 250000); Climate.Build(tropicalControl, default);
        var tropicalCold = Transect(5, false, false, 250000); tropicalCold.Recipe.TemperatureOffset = -8; Climate.Build(tropicalCold, default);
        check(tropicalCold.Climate.AtmosphericMonths.Sum(m => m.TemperatureTenthsC.Sum()) < tropicalControl.Climate.AtmosphericMonths.Sum(m => m.TemperatureTenthsC.Sum()),
            "Global cooling affects simulated monthly temperatures before classification");
        check(tropicalCold.Climate.AtmosphericMonths.Sum(m => m.PrecipitationMm.Sum()) < tropicalControl.Climate.AtmosphericMonths.Sum(m => m.PrecipitationMm.Sum()),
            "Global cooling reduces atmospheric moisture supply, not just displayed temperature");
        check(tropicalControl.Climate.AtmosphericMonths.Concat(tropicalCold.Climate.AtmosphericMonths).All(m => m.MoistureBudgetResidualPpm < 10000),
            "Humidity-limited land recycling closes tropical and cooled monthly water budgets");
        var mountainous = Transect(45, true); Climate.Build(mountainous, default);
        ClimateSeason[] Seasons(World w) => new[] { w.Climate.NorthernSpring, w.Climate.NorthernSummer, w.Climate.NorthernAutumn, w.Climate.NorthernWinter };
        foreach (var season in Seasons(flat))
        {
            check(season.PrecipitationMm.Length == flat.Surface.Cells.Length && season.MoisturePermille.Length == flat.Surface.Cells.Length, "All four seasonal climate fields retain full cell coverage");
            check(season.PrecipitationMm.All(p => p >= 0) && season.TemperatureTenthsC.All(t => t >= -900 && t <= 650), "Climate physical fields remain finite and bounded");
            check(season.PrecipitationMm[1] > season.PrecipitationMm[14], "Rainfall declines over a long continent rather than receiving a latitude-only moisture floor");
            check(season.MoistureTransportKm[14] > 9000, "Moisture transport reaches distant cells in physical kilometres without a fixed hop horizon");
        }
        check(mountainous.Climate.NorthernSummer.PrecipitationMm[7] < flat.Climate.NorthernSummer.PrecipitationMm[7] * .7, "A controlled mountain barrier depletes leeward rain relative to the identical flat transect");
        check(mountainous.Climate.NorthernSummer.PrecipitationMm[4] > mountainous.Climate.NorthernSummer.PrecipitationMm[5], "Controlled windward uplift is wetter than the immediate lee");
        check(mountainous.Climate.NorthernSummer.RainShadowPermille[5] > 0, "Orographic shadow provenance persists downstream");
        check(flat.Climate.NorthernSummer.TemperatureTenthsC[7] > flat.Climate.NorthernWinter.TemperatureTenthsC[7], "Northern summer is warmer than northern winter");
        check(flat.Climate.NorthernSpring.TemperatureTenthsC[7] != flat.Climate.NorthernAutumn.TemperatureTenthsC[7], "Shoulder seasons retain thermal lag instead of duplicating a two-season midpoint");
        check(flat.Climate.NorthernSpring.PrecipitationMm.Where((p, i) => p != flat.Climate.NorthernAutumn.PrecipitationMm[i]).Any(), "Shoulder-season circulation is independently solved");
        var south = Transect(-45, false); Climate.Build(south, default);
        check(south.Climate.NorthernWinter.TemperatureTenthsC[7] > south.Climate.NorthernSummer.TemperatureTenthsC[7], "Southern hemisphere seasonality is reversed");
        var boreal = Transect(65, false); Climate.Build(boreal, default);
        check(boreal.Climate.NorthernSummer.TemperatureTenthsC[2] > 100 && boreal.Climate.Classification[2] != Koppen.ET && boreal.Climate.Classification[2] != Koppen.EF, "Lowland continental 65-degree summers support non-polar climates");
        foreach (double latitude in new[] { -85d, -75d, -70d, 70d, 75d, 85d })
        {
            var iceSheet = Transect(latitude, false, false, 250000);
            for (int i = 1; i < iceSheet.ElevationM.Length; i++) iceSheet.ElevationM[i] = 3000;
            Climate.Build(iceSheet, default);
            check(Seasons(iceSheet).All(q => q.TemperatureTenthsC[1] < 0) && iceSheet.Climate.Classification[1] == Koppen.EF,
                $"Polar3000m coastal plateau remains ice-cap rather than maritime forest at latitude{latitude}");
            check(Seasons(iceSheet).All(q => q.TemperatureTenthsC[0] >= -18 && q.TemperatureTenthsC[0] < 100),
                $"Polar ocean respects seawater freezing buffer without unrealistically warm summer water at latitude{latitude}");
        }
        var polarLowland = Transect(85, false, false, 250000); Climate.Build(polarLowland, default);
        check(polarLowland.Climate.Classification[1] == Koppen.ET || polarLowland.Climate.Classification[1] == Koppen.EF,
            "Ocean-adjacent85N lowland remains polar without turning all65N lowland into tundra");
        check(polarLowland.Climate.NorthernWinter.TemperatureTenthsC[1] < -100,
            "Air over ice-adjacent polar land is not warmed to the seawater freezing-point floor");
        var connectedHeat = HeatBasins(true); Climate.Build(connectedHeat, default);
        var noHeatSource = HeatBasins(false); Climate.Build(noHeatSource, default);
        check(connectedHeat.Climate.NorthernSummer.OceanWarmthMilli[3] > noHeatSource.Climate.NorthernSummer.OceanWarmthMilli[3],
            "Warm western-boundary source carries heat through connected poleward ocean cells");
        check(Seasons(connectedHeat).Zip(Seasons(noHeatSource), (warm, control) => warm.OceanWarmthMilli[6] == control.OceanWarmthMilli[6]).All(equal => equal),
            "Ocean current heat cannot jump a land barrier into a disconnected basin");
        var heatRepeat = HeatBasins(true); Climate.Build(heatRepeat, default);
        check(StateHash.Compute(connectedHeat.Climate) == StateHash.Compute(heatRepeat.Climate), "Directed ocean heat transport remains deterministic");
        var westernMargin = Transect(35, false, false, 250000); Climate.Build(westernMargin, default);
        var easternMargin = Transect(35, false, true, 250000); Climate.Build(easternMargin, default);
        check(easternMargin.Climate.NorthernSummer.PrecipitationMm[1] > westernMargin.Climate.NorthernSummer.PrecipitationMm[1] * 1.5,
            "Mirrored subtropical warm eastern margin receives more summer rain than the cool western margin");
        check(westernMargin.Climate.NorthernWinter.PrecipitationMm[1] > westernMargin.Climate.NorthernSummer.PrecipitationMm[1],
            "Western subtropical margin can retain Mediterranean winter-wet seasonality");
        check(easternMargin.Climate.Classification[1] != Koppen.Csa && easternMargin.Climate.Classification[1] != Koppen.Csb && easternMargin.Climate.Classification[1] != Koppen.Csc,
            "Subtropical summer-wet eastern margin is not stamped with a Mediterranean climate");
        check(easternMargin.Climate.NorthernSummer.OceanWarmthMilli[0] > 0 && westernMargin.Climate.NorthernSummer.OceanWarmthMilli[0] < 0,
            "Opposite coast orientations distinguish poleward warm and equatorward cool current influence");
        var tropical = Transect(10, false, true, 250000); Climate.Build(tropical, default);
        var tropicalHighland = Transect(10, false, true, 250000); tropicalHighland.ElevationM[1] = 1800; Climate.Build(tropicalHighland, default);
        check(Seasons(tropical).All(q => q.TemperatureTenthsC[1] >= 180), "Ocean-accessible lowland10N stays tropical in every quarter");
        check(tropical.Climate.Classification[1] == Koppen.Af || tropical.Climate.Classification[1] == Koppen.Am || tropical.Climate.Classification[1] == Koppen.Aw,
            "Moist lowland10N does not become a spurious temperate climate");
        check((int)tropicalHighland.Climate.Classification[1] >= (int)Koppen.Csa && (int)tropicalHighland.Climate.Classification[1] <= (int)Koppen.Cfc,
            "Tropical1800m upland may legitimately have a temperate C climate without latitude-specific exclusion");
        check(Seasons(tropical).Zip(Seasons(tropicalHighland), (low, high) => low.TemperatureTenthsC[1] - high.TemperatureTenthsC[1]).All(delta => delta > 70),
            "Tropical highland cooling follows a substantial physical lapse rate in every season");
        check(flat.Climate.NorthernWinter.TemperatureTenthsC[12] < flat.Climate.NorthernWinter.TemperatureTenthsC[1] - 30,
            "Midlatitude deep continental winter is colder than maritime winter without changing latitude");
        int coastalRange = flat.Climate.NorthernSummer.TemperatureTenthsC[1] - flat.Climate.NorthernWinter.TemperatureTenthsC[1];
        int inlandRange = flat.Climate.NorthernSummer.TemperatureTenthsC[12] - flat.Climate.NorthernWinter.TemperatureTenthsC[12];
        check(inlandRange > coastalRange + 50, "Midlatitude inland continentality increases seasonal range rather than uniformly cooling the year");
        var dry = Transect(0, false); Array.Fill(dry.Hydrology.OceanConnected, false); Climate.Build(dry, default);
        foreach (var season in Seasons(dry))
        {
            check(season.PrecipitationMm.All(p => p == 0), "A genuinely ocean-free equatorial planet has no atmospheric water source");
            check(season.MoistureTransportKm.All(d => d == -1), "Dry-world diagnostics do not invent ocean origins");
        }
        var repeat = Transect(45, false); Climate.Build(repeat, default);
        check(StateHash.Compute(flat.Climate) == StateHash.Compute(repeat.Climate), "Four-season climate is repeatable");
        var profiled = Transect(45, false);
        profiled.Recipe.AlgorithmVersion = ClimateCalibration.Algorithm;
        profiled.Recipe.ClimateWeights = (double[])ClimateCalibration.Baseline.Clone();
        Climate.Build(profiled, default);
        check(StateHash.Compute(flat.Climate) == StateHash.Compute(profiled.Climate), "Neutral calibration weights preserve all climate outputs exactly");
        foreach (var extreme in new[] { ClimateCalibration.Minimum, ClimateCalibration.Maximum })
        {
            var noOcean = Transect(0, false); Array.Fill(noOcean.Hydrology.OceanConnected, false);
            noOcean.Recipe.ClimateWeights = (double[])extreme.Clone(); noOcean.Recipe.AlgorithmVersion = ClimateCalibration.Algorithm;
            Climate.Build(noOcean, default);
            check(noOcean.Climate.AtmosphericMonths.All(m => m.PrecipitationMm.All(p => p == 0)), "Even extreme allowed learned weights cannot create rain without water");
        }
        check(Climate.ClassifySeasons(new[] { 25d, 26d, 25d, 24d }, new[] { 600d, 600d, 600d, 600d }, true) == Koppen.Af, "Four wet tropical quarters classify as rainforest");
        check(Climate.ClassifySeasons(new[] { 25d, 30d, 27d, 20d }, new[] { 10d, 10d, 10d, 10d }, true) == Koppen.BWh, "Four dry hot quarters classify as desert");
        check(Climate.ClassifySeasons(new[] { 12d, 25d, 15d, 7d }, new[] { 150d, 15d, 180d, 240d }, true) == Koppen.Csa, "Monthly shoulder reconstruction recognizes Mediterranean dry summers");
        check(Climate.ClassifySeasons(new[] { 3d, 17d, 5d, -18d }, new[] { 160d, 220d, 160d, 110d }, true) == Koppen.Dfc, "Quarter-only compatibility classification does not invent additional warm months");
        var monthlyRain = Enumerable.Repeat(60d, 12).ToArray();
        check(Climate.ClassifyMonths(new[] { -2d, 5d, 12d, 17d, 19d, 16d, 11d, 5d, -3d, -10d, -16d, -12d }, monthlyRain, true) == Koppen.Dfb, "Actual monthly samples distinguish five warm months from a short boreal summer");
        check(Climate.ClassifyMonths(Enumerable.Repeat(0d, 12).ToArray(), monthlyRain, true) == Koppen.EF, "Warmest month exactly0C is icecap");
        check(Climate.ClassifyMonths(Enumerable.Repeat(10d, 12).ToArray(), monthlyRain, true) == Koppen.ET, "Warmest month exactly10C is tundra");
        check(Climate.ClassifyMonths(Enumerable.Repeat(10d, 12).ToArray(), new double[12], true) == Koppen.BWk, "Aridity takes precedence over thermal classes per published definitions");
        check(flat.Climate.AtmosphericMonths.Length == 12 && flat.Climate.AtmosphericMonths.All(m => m.TemperatureTenthsC.Length == flat.Surface.Cells.Length), "Twelve actually simulated atmospheric monthly fields remain inspectable");
        check(flat.Climate.AtmosphericMonths.All(m => m.MoistureBudgetResidualPpm < 10000), "Monthly local moisture budgets converge to less than one percent residual");
        for (int q = 0; q < 4; q++) for (int i = 0; i < flat.Surface.Cells.Length; i++)
            check(Seasons(flat)[q].PrecipitationMm[i] == flat.Climate.AtmosphericMonths.Skip(q * 3).Take(3).Sum(m => m.PrecipitationMm[i]), "Quarter rainfall is the sum of its simulated months");
    }

    static World Transect(double latitudeDegrees, bool mountain, bool oceanEast = false, int edgeDistanceM = 700000)
    {
        const int count = 16; double lat = latitudeDegrees * Math.PI / 180;
        var cells = new Cell[count]; var heights = Enumerable.Repeat(100, count).ToArray(); heights[0] = -100;
        if (mountain) heights[4] = 3000;
        for (int i = 0; i < count; i++)
        {
            double lon = i * .14 * (oceanEast ? -1 : 1);
            int[] neighbors = i == 0 ? new[] { 1 } : i == count - 1 ? new[] { i - 1 } : new[] { i - 1, i + 1 };
            cells[i] = new Cell { Id = i, Position = new V(Math.Cos(lat) * Math.Cos(lon), Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat)), AreaM2 = 500_000_000_000, Neighbors = neighbors, DistanceM = Enumerable.Repeat(edgeDistanceM, neighbors.Length).ToArray() };
        }
        var ocean = new bool[count]; ocean[0] = true;
        return new World { Recipe = UntrainedRecipe(), Surface = new Surface { Cells = cells }, ElevationM = heights, Landmass = Enumerable.Repeat(0, count).ToArray(), Hydrology = new HydrologyResult { OceanConnected = ocean } };
    }

    static World HeatBasins(bool warmSource)
    {
        // Western boundary at35N, connected poleward sea, land barrier, isolated sea.
        double[] latitudes = { 35, 35, 43, 50, 50, 50, 55 };
        double[] longitudes = { -.1, 0, .1, .2, .3, .4, .5 };
        var cells = new Cell[latitudes.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            double lat = latitudes[i] * Math.PI / 180, lon = longitudes[i];
            int[] neighbors = i == 0 ? new[] { 1 } : i == cells.Length - 1 ? new[] { i - 1 } : new[] { i - 1, i + 1 };
            cells[i] = new Cell { Id = i, Position = new V(Math.Cos(lat) * Math.Cos(lon), Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat)), AreaM2 = 500_000_000_000, Neighbors = neighbors, DistanceM = Enumerable.Repeat(700000, neighbors.Length).ToArray() };
        }
        bool[] ocean = { !warmSource, true, true, true, false, true, true };
        return new World { Recipe = UntrainedRecipe(), Surface = new Surface { Cells = cells }, ElevationM = ocean.Select(water => water ? -100 : 100).ToArray(), Landmass = new int[cells.Length], Hydrology = new HydrologyResult { OceanConnected = ocean } };
    }
    // Historical mechanism fixtures intentionally exercise the unchanged untrained model.
    static Recipe UntrainedRecipe() { var r = Recipe.Preset("Preview"); ClimateCalibration.Reset(r); return r; }
}
