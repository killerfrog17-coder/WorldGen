using WorldGen.Core;
using WorldGen.Serialization;

static class EarthTests
{
    // Geography is used only in this measured-relief regression, never in the solver.
    public static void LegacyForcingLakeSanity(World earth, Action<bool, string> check)
    {
        check(earth.Recipe.LakeAmountPercent == 5, "Historical Earth size ceilings are tested at the previous baseline forcing, now5%");
        var lakes = earth.Hydrology.Waters.Where(b => b.Kind != WaterKind.Ocean).ToArray();
        // Four-season rainfall changes this deliberately coarse 20k fixture.
        // This guards million-km2 regressions, not observed shoreline accuracy:
        // its ~443k Caspian remains oversized and is recorded in VALIDATION.md.
        // The owner-selected20x default has a424k Amazon basin on this coarse
        // fixture. Do not silently loosen the old30k ceiling to call that realistic.
        check(lakes.Length > 0 && lakes.Max(b => b.AreaM2) < 500_000_000_000L, "Coarse Earth lake size stays below the reviewed regression ceiling");
        foreach (var region in new[] { (Name: "Amazon", MinLat: -15.0, MaxLat: 5.0, MinLon: -80.0, MaxLon: -45.0), (Name: "Congo", MinLat: -10.0, MaxLat: 8.0, MinLon: 10.0, MaxLon: 35.0) })
        {
            var regional = lakes.Where(b =>
            {
                var p = earth.Surface.Cells[earth.Hydrology.Depressions[b.DepressionId].PitCell].Position;
                double lat = Math.Asin(p.Z) * 180 / Math.PI, lon = Math.Atan2(p.Y, p.X) * 180 / Math.PI;
                return lat >= region.MinLat && lat <= region.MaxLat && lon >= region.MinLon && lon <= region.MaxLon;
            });
            check(regional.All(b => b.AreaM2 < 30_000_000_000L), "Earth " + region.Name + " actual lake area stays below the reviewed coarse-grid regression ceiling");
        }
    }

    public static void Run(Action<bool, string> check)
    {
        check(EarthTerrain.SampleMetres(0, -150) < -3000, "Earth measured deep Pacific bathymetry");
        check(EarthTerrain.SampleMetres(28, 87) > 3000, "Earth measured Himalayan relief");
        check(EarthTerrain.SampleMetres(-89, 0) > 1000, "Earth ice surface, not Antarctic subglacial bed");
        check(EarthTerrain.SampleMetres(0, -150) == EarthTerrain.SampleMetres(0, 210), "Earth longitude wraps");
        var recipe = Recipe.Preset("Earth"); recipe.Detail = 5000;
        var pipeline = new WorldPipeline(); var earth = pipeline.Generate(recipe);
        check(earth.Recipe.TerrainSource == "Earth", "Bundled Earth source retained");
        double land = earth.Surface.Cells.Where(c => !earth.Hydrology.OceanConnected[c.Id]).Sum(c => (double)c.AreaM2) / earth.Surface.Cells.Sum(c => (double)c.AreaM2);
        check(land > .27 && land < .32, "Earth global land area plausible without remapping");
        check(earth.PreErosionM.SequenceEqual(earth.ElevationM) && earth.TectonicM.SequenceEqual(earth.ElevationM), "Imported relief not eroded or rescaled");
        check(earth.Climate.Classification.Length == recipe.Detail && earth.Hydrology.Receiver.Length == recipe.Detail, "Earth downstream simulations run");
        var restored = WorldJson.ReadWorld(WorldJson.Write(earth));
        var packed = WorldFile.Write(earth);
        check(packed.Length < WorldJson.Write(earth).Length / 2, "Compact world avoids enormous JSON payloads");
        var compactRestored = WorldFile.Read(packed);
        check(StateHash.Compute(earth) == StateHash.Compute(compactRestored), "Compact Earth snapshot preserves all state exactly");
        foreach (var saved in new[] { restored, compactRestored })
        {
            check(saved.Climate.AtmosphericMonths.Length == 12 && StateHash.Compute(saved.Climate.AtmosphericMonths) == StateHash.Compute(earth.Climate.AtmosphericMonths), "Actual monthly atmospheric fields and budget diagnostics survive snapshots");
            var observedSeasons = new[] { earth.Climate.NorthernSpring, earth.Climate.NorthernSummer, earth.Climate.NorthernAutumn, earth.Climate.NorthernWinter };
            var savedSeasons = new[] { saved.Climate.NorthernSpring, saved.Climate.NorthernSummer, saved.Climate.NorthernAutumn, saved.Climate.NorthernWinter };
            for (int season = 0; season < 4; season++)
                check(observedSeasons[season].TemperatureTenthsC.SequenceEqual(savedSeasons[season].TemperatureTenthsC) && observedSeasons[season].PrecipitationMm.SequenceEqual(savedSeasons[season].PrecipitationMm), "All four climate seasons survive snapshots");
            check(saved.Hydrology.LakeCoverageFraction.SequenceEqual(earth.Hydrology.LakeCoverageFraction), "Snapshot retains fractional lake coverage, including undisplayed water");
            check(saved.Hydrology.LakeWaterVolumeM3.SequenceEqual(earth.Hydrology.LakeWaterVolumeM3), "Snapshot retains subcell lake storage");
            check(saved.Hydrology.Waters.Select(x => x.SurfaceElevationM).SequenceEqual(earth.Hydrology.Waters.Select(x => x.SurfaceElevationM)), "Snapshot retains continuous lake surfaces");
            check(saved.Hydrology.Waters.Select(x => (x.SubgridOutlet, x.ResidenceTimeYears, x.CatchmentTransmissionLossM3PerS)).SequenceEqual(earth.Hydrology.Waters.Select(x => (x.SubgridOutlet, x.ResidenceTimeYears, x.CatchmentTransmissionLossM3PerS))), "Snapshot retains subgrid outlet and catchment-loss provenance");
        }
        string hydroHash = StateHash.Compute(earth.Hydrology);
        double originalCoverage = earth.Hydrology.LakeCoverageFraction[0];
        earth.Hydrology.LakeCoverageFraction[0] = .123456;
        check(StateHash.Compute(earth.Hydrology) != hydroHash, "Fractional lake coverage participates in authoritative hash");
        earth.Hydrology.LakeCoverageFraction[0] = originalCoverage;
        double originalVolume = earth.Hydrology.LakeWaterVolumeM3[0];
        earth.Hydrology.LakeWaterVolumeM3[0] += 1;
        check(StateHash.Compute(earth.Hydrology) != hydroHash, "Subcell lake storage participates in authoritative hash");
        earth.Hydrology.LakeWaterVolumeM3[0] = originalVolume;
        var diagnosticBody = earth.Hydrology.Waters[0];
        bool originalOutlet = diagnosticBody.SubgridOutlet;
        diagnosticBody.SubgridOutlet = !originalOutlet;
        check(StateHash.Compute(earth.Hydrology) != hydroHash, "Subgrid outlet participates in authoritative hash");
        diagnosticBody.SubgridOutlet = originalOutlet;
        double originalResidence = diagnosticBody.ResidenceTimeYears;
        diagnosticBody.ResidenceTimeYears += 1;
        check(StateHash.Compute(earth.Hydrology) != hydroHash, "Lake retention participates in authoritative hash");
        diagnosticBody.ResidenceTimeYears = originalResidence;
        double originalLoss = diagnosticBody.CatchmentTransmissionLossM3PerS;
        diagnosticBody.CatchmentTransmissionLossM3PerS += 1;
        check(StateHash.Compute(earth.Hydrology) != hydroHash, "Catchment transmission loss participates in authoritative hash");
        diagnosticBody.CatchmentTransmissionLossM3PerS = originalLoss;
        string csvHeader = WorldJson.Csv(earth).Split('\n')[0];
        check(csvHeader.Contains("spring_temp_c,summer_temp_c,autumn_temp_c,winter_temp_c") && csvHeader.Contains("spring_precip_mm,summer_precip_mm,autumn_precip_mm,winter_precip_mm"), "CSV exposes four seasonal temperatures and quarter precipitation totals");
        check(csvHeader.Contains("lake_fraction,lake_area_m2,lake_volume_m3"), "Cell export retains subcell lake coverage and storage alongside later ecology columns");
        earth.ElevationM[0]++;
        var tampered = WorldFile.Write(earth);
        earth.ElevationM[0]--;
        try { WorldFile.Read(tampered); check(false, "Modified compact snapshot rejected by full state hash"); } catch (ArgumentException) { check(true, "Modified compact snapshot rejected by full state hash"); }
        packed[0] ^= 1;
        try { WorldFile.Read(packed); check(false, "Corrupt compact header rejected"); } catch (ArgumentException) { check(true, "Corrupt compact header rejected"); }
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try { WorldFile.Write(earth, cancelled.Token); check(false, "Compact save cancellation"); } catch (OperationCanceledException) { check(true, "Compact save cancellation"); }
        }
        check(earth.Stages.Select(s => s.Hash).SequenceEqual(pipeline.Reconstruct(restored).Stages.Select(s => s.Hash)), "Earth saved snapshot reconstructs through pipeline");
        var changed = recipe.Clone(); changed.TerrainWarp = 0; changed.Glacial = 0; changed.Hydraulic = 0;
        var again = pipeline.Generate(changed);
        check(earth.ElevationM.SequenceEqual(again.ElevationM), "Scuplting settings cannot corrupt measured Earth terrain");
        check(earth.CoarseHash == again.CoarseHash, "Imported Earth preserves coarse contract");
        HydrologyTests.Validate(earth, check);
    }
}
