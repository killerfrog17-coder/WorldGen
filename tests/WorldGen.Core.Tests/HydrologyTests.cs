using WorldGen.Core;

public static class HydrologyTests
{
    sealed class FixedRunoff : ISeasonalRunoffProvider
    {
        readonly double summer, winter;
        public FixedRunoff(double summer, double winter) { this.summer = summer; this.winter = winter; }
        public double[] RunoffDepthMetres(World world, int seasonIndex) => Enumerable.Repeat(seasonIndex < 2 ? summer * .5 : winter * .5, world.Surface.Cells.Length).ToArray();
    }

    public static void Run(Action<bool, string> check)
    {
        var wet = Fixture(); int[] originalHeights = (int[])wet.ElevationM.Clone(); bool[] originalOcean = (bool[])wet.Hydrology.OceanConnected.Clone();
        Hydrology.Build(wet, default, new FixedRunoff(.5, .1));
        Validate(wet, check);
        check(wet.ElevationM.SequenceEqual(originalHeights), "Hydrology does not alter terrain elevation");
        check(wet.Hydrology.OceanConnected.SequenceEqual(originalOcean), "Hydrology does not alter ocean topology");
        var lake = wet.Hydrology.Waters.Single(x => x.Kind == WaterKind.Lake);
        check(lake.SurfaceElevationM < 20 && lake.SpillM <= 100 && lake.Cells.SequenceEqual(new[] { 3 }), "Humid coarse bowl retains flow-limited or incised storage instead of filling an enormous bucket");
        check(lake.Cells.All(i => wet.ElevationM[i] < lake.SurfaceM) && lake.VolumeM3 > 0, "Lake has positive common-level storage");
        check(lake.OutletCell == 1 && lake.DownstreamCell == 0 && !lake.Endorheic, "Spilling lake retains one deterministic basin-boundary outlet");
        check(wet.Hydrology.NorthernSummerDischargeM3PerS[1] > wet.Hydrology.NorthernWinterDischargeM3PerS[1], "Seasonal runoff remains seasonal after accumulation");
        check(wet.Hydrology.Rivers.All(r => r.WidthM > 0 && r.NorthernSummerDischargeM3PerS >= r.NorthernWinterDischargeM3PerS), "River reaches retain physical width and seasonal discharge");
        check(!lake.SubgridOutlet || Math.Abs(lake.VolumeM3 / lake.ResidenceTimeYears / (365.2425 * 86400) - (lake.MeanInflowM3PerS - lake.EvaporationM3PerS)) < .01, "Subgrid throughflow has an explicit volume/residence water budget");

        var repeated = Fixture(); Hydrology.Build(repeated, default, new FixedRunoff(.5, .1));
        check(StateHash.Compute(wet.Hydrology) == StateHash.Compute(repeated.Hydrology), "Persistent hydrology is deterministic");

        var dry = Fixture();
        Array.Fill(dry.Climate.NorthernSpring.PrecipitationMm, 0); Array.Fill(dry.Climate.NorthernSummer.PrecipitationMm, 0); Array.Fill(dry.Climate.NorthernAutumn.PrecipitationMm, 0); Array.Fill(dry.Climate.NorthernWinter.PrecipitationMm, 0);
        Array.Fill(dry.Climate.NorthernSpring.TemperatureTenthsC, 400); Array.Fill(dry.Climate.NorthernSummer.TemperatureTenthsC, 400); Array.Fill(dry.Climate.NorthernAutumn.TemperatureTenthsC, 300); Array.Fill(dry.Climate.NorthernWinter.TemperatureTenthsC, 300);
        Hydrology.Build(dry, default, new FixedRunoff(0, 0)); Validate(dry, check);
        check(!dry.Hydrology.Waters.Any(x => x.Kind != WaterKind.Ocean), "Arid bowl with no inflow remains a dry closed basin");
        var closed = dry.Hydrology.Depressions.Single();
        check(closed.OutletCell < 0 && dry.Hydrology.Receiver[closed.PitCell] < 0, "Dry endorheic basin terminates at its pit rather than artificial spill");

        var glacial = Fixture(); glacial.Glaciated[2] = glacial.Glaciated[3] = true;
        Hydrology.Build(glacial, default, new FixedRunoff(.5, .1));
        check(glacial.Hydrology.Waters.Any(x => x.Kind == WaterKind.GlacialLake), "Glacially carved bowl retains lake provenance");

        var frozen = Fixture();
        Array.Fill(frozen.Climate.NorthernSpring.PrecipitationMm, 600); Array.Fill(frozen.Climate.NorthernSummer.PrecipitationMm, 600); Array.Fill(frozen.Climate.NorthernAutumn.PrecipitationMm, 600); Array.Fill(frozen.Climate.NorthernWinter.PrecipitationMm, 600);
        Array.Fill(frozen.Climate.NorthernSpring.TemperatureTenthsC, -100); Array.Fill(frozen.Climate.NorthernSummer.TemperatureTenthsC, -100); Array.Fill(frozen.Climate.NorthernAutumn.TemperatureTenthsC, -200); Array.Fill(frozen.Climate.NorthernWinter.TemperatureTenthsC, -200);
        Hydrology.Build(frozen, default); Validate(frozen, check);
        check(!frozen.Hydrology.Waters.Any(x => x.Kind != WaterKind.Ocean), "Permanent-freeze snowfall does not create liquid surface lakes");
        check(frozen.Hydrology.NorthernSummerDischargeM3PerS.All(x => x == 0) && frozen.Hydrology.NorthernWinterDischargeM3PerS.All(x => x == 0), "Permanent snow storage does not become liquid runoff");

        var melt = Fixture();
        Array.Fill(melt.Climate.NorthernSpring.PrecipitationMm, 600); Array.Fill(melt.Climate.NorthernSummer.PrecipitationMm, 600); Array.Fill(melt.Climate.NorthernAutumn.PrecipitationMm, 0); Array.Fill(melt.Climate.NorthernWinter.PrecipitationMm, 0);
        Array.Fill(melt.Climate.NorthernSpring.TemperatureTenthsC, -100); Array.Fill(melt.Climate.NorthernSummer.TemperatureTenthsC, -100); Array.Fill(melt.Climate.NorthernAutumn.TemperatureTenthsC, 100); Array.Fill(melt.Climate.NorthernWinter.TemperatureTenthsC, 100);
        Hydrology.Build(melt, default); Validate(melt, check);
        check(melt.Hydrology.NorthernAutumnDischargeM3PerS[1] > melt.Hydrology.NorthernSummerDischargeM3PerS[1], "Seasonal warming transfers stored snowfall into the next warm quarter's discharge");

        var cascade = CascadeFixture(); Hydrology.Build(cascade, default, new FixedRunoff(.5, .1)); Validate(cascade, check);
        var cascadeLakes = cascade.Hydrology.Waters.Where(x => x.Kind != WaterKind.Ocean).OrderBy(x => x.SurfaceM).ToArray();
        check(cascadeLakes.Length == 2 && cascadeLakes.All(x => !x.Endorheic), "Two wet depressions form a spilling lake cascade");
        foreach (var body in cascadeLakes)
        {
            double annualIn = body.MeanInflowM3PerS * 2 * 365.2425 * 86400 / 2;
            double annualEvap = body.EvaporationM3PerS * 2 * 365.2425 * 86400 / 2;
            double annualOut = MeanDischarge(cascade.Hydrology, body.OutletCell) * 365.2425 * 86400;
            check(Math.Abs(annualIn - annualEvap - annualOut) <= Math.Max(1, annualIn) * 1e-5, "Spilling lake cascade conserves annual inflow, evaporation, and outflow");
        }
        check(cascadeLakes[0].MeanInflowM3PerS > cascadeLakes[1].MeanInflowM3PerS, "Downstream lake inflow includes upstream lake outflow");

        var tectonic = Fixture(); foreach (var cell in tectonic.Surface.Cells) cell.AreaM2 = 100_000_000_000;
        tectonic.Features = new[] { new Feature { Kind = FeatureKind.MarineBasin, Cells = new[] { 2, 3 } } };
        Hydrology.Build(tectonic, default, new FixedRunoff(.5, .1));
        check(tectonic.Hydrology.Waters.Any(x => x.Kind != WaterKind.Ocean), "Large tectonic basin retains water after bounded outlet conditioning");

        var unresolved = Fixture(); foreach (var cell in unresolved.Surface.Cells) cell.AreaM2 = 100_000_000_000;
        Hydrology.Build(unresolved, default, new FixedRunoff(.5, .1)); Validate(unresolved, check);
        check(unresolved.Hydrology.Depressions.Single().UnresolvedSpillway && unresolved.Hydrology.Depressions.Single().SpillwayIncisionM <= 200, "Narrow obstruction receives bounded inspectable hydraulic conditioning");
        check(unresolved.ElevationM.SequenceEqual(originalHeights) && unresolved.Hydrology.Waters.Any(x => x.Kind != WaterKind.Ocean), "Conditioning lowers lake storage without deleting basin or changing terrain");

        var partial = Fixture();
        Array.Fill(partial.Climate.NorthernSpring.PrecipitationMm, 0); Array.Fill(partial.Climate.NorthernSummer.PrecipitationMm, 0); Array.Fill(partial.Climate.NorthernAutumn.PrecipitationMm, 0); Array.Fill(partial.Climate.NorthernWinter.PrecipitationMm, 0);
        Array.Fill(partial.Climate.NorthernSpring.TemperatureTenthsC, 300); Array.Fill(partial.Climate.NorthernSummer.TemperatureTenthsC, 300); Array.Fill(partial.Climate.NorthernAutumn.TemperatureTenthsC, 300); Array.Fill(partial.Climate.NorthernWinter.TemperatureTenthsC, 300);
        // Keep this deliberately tiny pond's storage forcing fixed when the
        // reviewed global ordinary-lake retention calibration changes.
        Hydrology.Build(partial, default, new FixedRunoff(.002 / Hydrology.DefaultLakeRetentionScale, .002 / Hydrology.DefaultLakeRetentionScale)); Validate(partial, check);
        var partialLake = partial.Hydrology.Waters.Single(x => x.Kind != WaterKind.Ocean);
        check(partialLake.SubgridOutlet && partialLake.Cells.Length == 1 && partial.Hydrology.LakeCoverageFraction[partialLake.Cells[0]] < .02, "Small lake retains fractional area instead of rounding up a whole cell");
        check(partialLake.AreaM2 > 0 && partialLake.AreaM2 < partial.Surface.Cells[partialLake.Cells[0]].AreaM2 && partialLake.VolumeM3 > 0, "Sub-cell water area and volume are retained");
        check(partialLake.MeanInflowM3PerS > partialLake.EvaporationM3PerS && partialLake.VolumeM3 < lake.VolumeM3, "Drier basin stores less actual water while routing residual flow onward");

        var terminal = Fixture(); terminal.ElevationM[3] = -900;
        foreach (var cell in terminal.Surface.Cells) cell.AreaM2 = 150_000_000_000;
        Array.Fill(terminal.Climate.NorthernSpring.PrecipitationMm, 0); Array.Fill(terminal.Climate.NorthernSummer.PrecipitationMm, 0); Array.Fill(terminal.Climate.NorthernAutumn.PrecipitationMm, 0); Array.Fill(terminal.Climate.NorthernWinter.PrecipitationMm, 0);
        Hydrology.Build(terminal, default, new FixedRunoff(.04, .01)); Validate(terminal, check);
        var terminalLake = terminal.Hydrology.Waters.Single(b => b.Kind != WaterKind.Ocean);
        check(terminalLake.Endorheic && !terminalLake.SubgridOutlet && terminalLake.CatchmentTransmissionLossM3PerS > 0, "Deep terminal basin keeps closed drainage and explicit catchment transmission losses");
        check(Math.Abs(terminalLake.MeanInflowM3PerS - terminalLake.EvaporationM3PerS) < 1e-5, "Terminal sea balances delivered water and evaporation without an uphill river");
        double terminalAnnualRunoff = terminal.Surface.Cells.Skip(1).Sum(c => c.AreaM2 * .05);
        double replacedRunoff = terminalLake.Cells.Sum(i => terminal.Surface.Cells[i].AreaM2 * .05 * terminal.Hydrology.LakeCoverageFraction[i]);
        double terminalAnnualLoss = (terminalLake.EvaporationM3PerS + terminalLake.CatchmentTransmissionLossM3PerS) * (365.2425 * 86400);
        check(Math.Abs(terminalAnnualRunoff - replacedRunoff - terminalAnnualLoss) < terminalAnnualRunoff * 1e-5, "Terminal lake conserves original runoff minus inundated land runoff as evaporation plus explicit transmission loss");
        Array.Fill(terminal.Climate.NorthernSpring.TemperatureTenthsC, -100); Array.Fill(terminal.Climate.NorthernSummer.TemperatureTenthsC, -100); Array.Fill(terminal.Climate.NorthernAutumn.TemperatureTenthsC, -200); Array.Fill(terminal.Climate.NorthernWinter.TemperatureTenthsC, -200);
        Array.Fill(terminal.Climate.NorthernSpring.PrecipitationMm, 600); Array.Fill(terminal.Climate.NorthernSummer.PrecipitationMm, 600); Array.Fill(terminal.Climate.NorthernAutumn.PrecipitationMm, 600); Array.Fill(terminal.Climate.NorthernWinter.PrecipitationMm, 600);
        Hydrology.Build(terminal, default);
        check(!terminal.Hydrology.Waters.Any(b => b.Kind != WaterKind.Ocean), "Deep frozen terminal basin does not turn snow into liquid lake water");

        var aridHighBasin = Fixture(); aridHighBasin.ElevationM = new[] { -100, 700, 300, 250, 900 };
        Array.Fill(aridHighBasin.Climate.NorthernSpring.PrecipitationMm, 0); Array.Fill(aridHighBasin.Climate.NorthernSummer.PrecipitationMm, 0); Array.Fill(aridHighBasin.Climate.NorthernAutumn.PrecipitationMm, 0); Array.Fill(aridHighBasin.Climate.NorthernWinter.PrecipitationMm, 0);
        foreach (var cell in aridHighBasin.Surface.Cells) { cell.AreaM2 = 20_000_000_000; Array.Fill(cell.DistanceM, 800000); }
        Hydrology.Build(aridHighBasin, default, new FixedRunoff(.01, .005)); Validate(aridHighBasin, check);
        var highLake = aridHighBasin.Hydrology.Waters.Single(b => b.Kind != WaterKind.Ocean);
        check(highLake.Endorheic && !highLake.SubgridOutlet && highLake.OutletCell < 0 && aridHighBasin.Hydrology.Receiver[aridHighBasin.Hydrology.Depressions.Single().PitCell] < 0,
            "Arid strongly confined above-sea basin retains genuine closed drainage");

        var broadBarrier = Fixture(); foreach (var cell in broadBarrier.Surface.Cells) { cell.AreaM2 = 100_000_000_000; Array.Fill(cell.DistanceM, 800000); }
        Hydrology.Build(broadBarrier, default, new FixedRunoff(.5, .1));
        check(!broadBarrier.Hydrology.Depressions.Single().UnresolvedSpillway, "Wide barrier receives no explicit bounded hydraulic incision");
        check(broadBarrier.Hydrology.Waters.Any(b => b.SubgridOutlet), "Unresolved coarse humid drainage is separately tagged from proved hydraulic incision");

        var twoExits = TwoExitFixture(); Hydrology.Build(twoExits, default, new FixedRunoff(.5, .1)); Validate(twoExits, check);
        check(Enumerable.Range(1, twoExits.Surface.Cells.Length - 1).Count(i => twoExits.Hydrology.Receiver[i] == 0) == 1, "Equal-level competing spillways converge on exactly one basin outlet");
        var twoExitLake = twoExits.Hydrology.Waters.Single(b => b.Kind != WaterKind.Ocean);
        double oceanMean = MeanDischarge(twoExits.Hydrology, 0);
        check(Math.Abs(oceanMean - (twoExitLake.MeanInflowM3PerS - twoExitLake.EvaporationM3PerS)) < 1e-5, "Competing spill routes cannot double-count basin outflow");

        double previousArea = -1, previousVolume = -1;
        foreach (int amount in new[] { 0, 5, 25, 100, 400, 1000 })
        {
            var adjustable = CascadeFixture(); adjustable.Recipe.LakeAmountPercent = amount;
            Hydrology.Build(adjustable, default, new FixedRunoff(.5, .1)); Validate(adjustable, check);
            double area = adjustable.Hydrology.Waters.Where(b => b.Kind != WaterKind.Ocean).Sum(b => (double)b.AreaM2);
            double volume = adjustable.Hydrology.LakeWaterVolumeM3.Sum();
            check(area >= previousArea && volume >= previousVolume, "Increasing lake amount increases actual cascade storage and area, not only display");
            if (amount == 0)
            {
                check(area == 0 && volume == 0 && adjustable.Hydrology.LakeCoverageFraction.All(x => x == 0), "Zero lake amount has no persistent inland water");
                check(MeanDischarge(adjustable.Hydrology, 0) > 0, "No-lakes mode still routes runoff to the ocean");
            }
            previousArea = area; previousVolume = volume;
        }
        foreach (int amount in new[] { 5, 100, 1000 })
        {
            var scaled = Fixture(); scaled.Recipe.LakeAmountPercent = amount;
            Hydrology.Build(scaled, default, new FixedRunoff(.5, .1)); Validate(scaled, check);
            var body = scaled.Hydrology.Waters.Single(b => b.Kind != WaterKind.Ocean);
            double expectedScale = amount == 5 ? 1 : amount == 100 ? 20 : 200;
            check(Math.Abs(body.ResidenceTimeYears - lake.ResidenceTimeYears * expectedScale) < 1e-6,
                "New 5/100/1000% settings retain exactly 1/20/200 times the previous default residence");
            double normalAnnualEvaporation = 2 * .012 * (20 + 5) + 2 * .012 * (5 + 5);
            double expectedEvaporation = body.AreaM2 * normalAnnualEvaporation / expectedScale / (365.2425 * 86400);
            check(Math.Abs(body.EvaporationM3PerS - expectedEvaporation) < Math.Max(1, expectedEvaporation) * 1e-5,
                "Rebased amount applies reciprocally to effective open-water loss, including the previous-default setting");

            var scaledTerminal = Fixture(); scaledTerminal.Recipe.LakeAmountPercent = amount; scaledTerminal.ElevationM[3] = -900;
            foreach (var cell in scaledTerminal.Surface.Cells) cell.AreaM2 = 150_000_000_000;
            foreach (var season in new[] { scaledTerminal.Climate.NorthernSpring, scaledTerminal.Climate.NorthernSummer, scaledTerminal.Climate.NorthernAutumn, scaledTerminal.Climate.NorthernWinter })
                Array.Fill(season.PrecipitationMm, 0);
            Hydrology.Build(scaledTerminal, default, new FixedRunoff(.001, .001)); Validate(scaledTerminal, check);
            var terminalBody = scaledTerminal.Hydrology.Waters.Single(b => b.Kind != WaterKind.Ocean);
            double dryArea = scaledTerminal.Surface.Cells.Skip(1).Sum(c => (double)c.AreaM2) - terminalBody.AreaM2;
            double expectedLoss = dryArea * .002 * (1 - Math.Min(1, .02 * expectedScale)) / (365.2425 * 86400);
            check(Math.Abs(terminalBody.CatchmentTransmissionLossM3PerS - expectedLoss) < Math.Max(1, expectedLoss) * 1e-5,
                "Rebased terminal delivery is 2/40/100 percent at 5/100/1000 slider values, saturating rather than creating water");
        }
        var zeroTerminal = Fixture(); zeroTerminal.Recipe.LakeAmountPercent = 0; zeroTerminal.ElevationM[3] = -900;
        foreach (var cell in zeroTerminal.Surface.Cells) cell.AreaM2 = 150_000_000_000;
        Hydrology.Build(zeroTerminal, default, new FixedRunoff(.04, .01)); Validate(zeroTerminal, check);
        check(zeroTerminal.Hydrology.Waters.All(b => b.Kind == WaterKind.Ocean) && zeroTerminal.Hydrology.DryBasinTransmissionLossM3PerS > 0,
            "No-lakes terminal water budget records dry transmission loss rather than discarding water silently");

        double rainFedPrevious = -1, rainFedDefault = 0, rainFedHalf = 0;
        foreach (double amount in new[] { .05, 2.5, 5, 100, 1000 })
        {
            var rainFed = Fixture(); rainFed.Recipe.LakeAmountPercent = amount; rainFed.ElevationM[3] = -900;
            foreach (var cell in rainFed.Surface.Cells) cell.AreaM2 = 150_000_000_000;
            foreach (var season in new[] { rainFed.Climate.NorthernSpring, rainFed.Climate.NorthernSummer, rainFed.Climate.NorthernAutumn, rainFed.Climate.NorthernWinter })
            { Array.Fill(season.PrecipitationMm, 450); Array.Fill(season.TemperatureTenthsC, 200); }
            Hydrology.Build(rainFed, default, new FixedRunoff(0, 0)); Validate(rainFed, check);
            double area = rainFed.Hydrology.Waters.Where(b => b.Kind != WaterKind.Ocean).Sum(b => (double)b.AreaM2);
            check(area >= rainFedPrevious, "Lake slider is monotonic for direct-rainfall terminal lakes with zero catchment runoff");
            if (amount == 2.5) rainFedHalf = area;
            if (amount == 5) rainFedDefault = area;
            foreach (var body in rainFed.Hydrology.Waters.Where(b => b.Kind != WaterKind.Ocean))
            {
                double directRainQ = body.AreaM2 * 1.8 / (365.2425 * 86400);
                double outQ = body.OutletCell < 0 ? 0 : MeanDischarge(rainFed.Hydrology, body.OutletCell);
                check(Math.Abs(directRainQ - body.EvaporationM3PerS - outQ) < Math.Max(1, directRainQ) * 1e-5,
                    "Rain-fed lake conserves direct precipitation, amount-biased losses and routed outflow");
            }
            rainFedPrevious = area;
        }
        check(rainFedDefault > 0 && rainFedHalf < rainFedDefault, "Half the previous default shrinks a rainfall-fed basin even when precipitation exceeds normal evaporation");

        var springMelt = Fixture();
        var four = new[] { springMelt.Climate.NorthernSpring, springMelt.Climate.NorthernSummer, springMelt.Climate.NorthernAutumn, springMelt.Climate.NorthernWinter };
        foreach (var season in four) { Array.Fill(season.PrecipitationMm, 0); Array.Fill(season.TemperatureTenthsC, 100); }
        Array.Fill(four[3].PrecipitationMm, 400); Array.Fill(four[3].TemperatureTenthsC, -100);
        Hydrology.Build(springMelt, default); Validate(springMelt, check);
        check(springMelt.Hydrology.LocalNorthernSpringRunoffM3PerS[4] > 0 && springMelt.Hydrology.LocalNorthernSummerRunoffM3PerS[4] == 0
            && springMelt.Hydrology.LocalNorthernWinterRunoffM3PerS[4] == 0, "Winter snow melts in the next warm spring, not automatically the opposite summer");

        try { Hydrology.Build(Fixture(), default, new InvalidRunoff()); check(false, "Invalid runoff provider rejected"); }
        catch (ArgumentException) { check(true, "Invalid runoff provider rejected"); }
        try { Hydrology.Build(Fixture(), default, new FixedRunoff(double.PositiveInfinity, 0)); check(false, "Infinite runoff provider rejected"); }
        catch (ArgumentException) { check(true, "Infinite runoff provider rejected"); }
    }

    public static void Validate(World world, Action<bool, string> check)
    {
        var h = world.Hydrology; int n = world.Surface.Cells.Length;
        check(h.Receiver.Length == n && h.Watershed.Length == n && h.WaterBody.Length == n && h.FloodLevelM.Length == n, "Hydrology cell fields cover the fine mesh");
        check(h.VisitOrder.Length == n && h.VisitOrder.Distinct().Count() == n, "Hydrology retains a complete topological visit order");
        check(h.ContributingAreaM2.Length == n && h.NorthernSummerDischargeM3PerS.Length == n && h.NorthernWinterDischargeM3PerS.Length == n, "Hydrology accumulation fields cover the fine mesh");
        check(h.NorthernSpringDischargeM3PerS.Length == n && h.NorthernAutumnDischargeM3PerS.Length == n && h.LocalNorthernSpringRunoffM3PerS.Length == n && h.LocalNorthernAutumnRunoffM3PerS.Length == n,
            "All four seasonal runoff and discharge fields cover the fine mesh");
        check(h.LakeCoverageFraction.Length == n && h.LakeWaterVolumeM3.Length == n && h.LakeCoverageFraction.All(f => f >= 0 && f <= 1) && h.LakeWaterVolumeM3.All(v => v >= 0), "Fractional lake fields cover fine mesh with bounded physical values");
        var position = new int[n]; for (int i = 0; i < n; i++) position[h.VisitOrder[i]] = i;
        check(Enumerable.Range(0, n).All(i => h.Receiver[i] < 0 || world.Surface.Cells[i].Neighbors.Contains(h.Receiver[i])), "Every receiver follows a surface edge");
        check(Enumerable.Range(0, n).All(i => h.Receiver[i] < 0 || position[i] < position[h.Receiver[i]]), "Receiver graph is acyclic and topologically ordered");
        check(Enumerable.Range(0, n).All(i => h.Receiver[i] < 0 || h.ContributingAreaM2[h.Receiver[i]] >= h.ContributingAreaM2[i]), "Contributing area never decreases downstream");
        check(h.Waters.All(b => b.Cells.All(i => h.WaterBody[i] == b.Id) && b.Cells.All(i => b.Kind == WaterKind.Ocean || world.ElevationM[i] < b.SurfaceElevationM)), "Water-body membership has one common physical surface");
        check(h.Waters.Where(b => b.Kind != WaterKind.Ocean).All(b => Math.Abs(b.AreaM2 - b.Cells.Sum(i => world.Surface.Cells[i].AreaM2 * h.LakeCoverageFraction[i])) <= 1 && Math.Abs(b.VolumeM3 - b.Cells.Sum(i => h.LakeWaterVolumeM3[i])) <= .01), "Lake totals equal retained fractional area and volume");
        check(h.Waters.All(b => Connected(world.Surface, b.Cells)), "Every water body is one connected pool");
        check(h.Waters.Where(b => b.Kind != WaterKind.Ocean && !b.Endorheic).All(b => b.OutletCell >= 0 && b.DownstreamCell == h.Receiver[b.OutletCell] && !b.Cells.Contains(b.DownstreamCell)), "Every spilling lake outlet leaves its water body");
        check(h.Rivers.All(r => r.From >= 0 && r.To == h.Receiver[r.From] && r.ContributingAreaM2 == h.ContributingAreaM2[r.From] && r.WidthM > 0), "River records correspond to accumulated receiver edges");
        check(h.NorthernSummerDischargeM3PerS.All(q => q >= 0 && double.IsFinite(q)) && h.NorthernWinterDischargeM3PerS.All(q => q >= 0 && double.IsFinite(q)), "Seasonal discharge is finite and nonnegative");
        foreach (var depression in h.Depressions.Where(d => d.UnresolvedSpillway))
        {
            int[] corridor = depression.SpillwayCells;
            check(depression.SpillwayIncisionM > 0 && depression.SpillwayIncisionM <= 200 && depression.EffectiveSpillElevationM == depression.SpillM - depression.SpillwayIncisionM, "Virtual incision has bounded inspectable hydraulic depth");
            check(corridor.Length >= 2 && corridor[0] == depression.PitCell && corridor.All(i => world.ElevationM[i] - depression.EffectiveSpillElevationM <= 200), "Stored spillway proves a pit-connected bounded-depth course");
            check(Enumerable.Range(0, corridor.Length - 1).All(k => world.Surface.Cells[corridor[k]].Neighbors.Contains(corridor[k + 1])), "Stored spillway follows actual graph edges");
            double doubledCost = 0;
            for (int k = 0; k + 1 < corridor.Length; k++)
            {
                int i = corridor[k], j = corridor[k + 1], edge = Array.IndexOf(world.Surface.Cells[i].Neighbors, j);
                doubledCost += world.Surface.Cells[i].DistanceM[edge] * (2 + Math.Max(0, world.ElevationM[i] - depression.EffectiveSpillElevationM) + Math.Max(0, world.ElevationM[j] - depression.EffectiveSpillElevationM));
            }
            check(doubledCost <= 100000000, "Stored spillway remains inside integrated excavation budget");
        }
    }

    static double MeanDischarge(HydrologyResult h, int i) => (h.NorthernSpringDischargeM3PerS[i] + h.NorthernSummerDischargeM3PerS[i] + h.NorthernAutumnDischargeM3PerS[i] + h.NorthernWinterDischargeM3PerS[i]) * .25;

    static World Fixture()
    {
        // ocean(-100) -- sill(100) -- bowl(20) -- pit(-20) -- upland(220)
        int[] elevation = { -100, 100, 20, -20, 220 };
        int[][] neighbors = { new[] { 1 }, new[] { 0, 2 }, new[] { 1, 3 }, new[] { 2, 4 }, new[] { 3 } };
        var cells = new Cell[elevation.Length];
        for (int i = 0; i < cells.Length; i++) cells[i] = new Cell { Id = i, Position = new V(1, i * .01, 0).Unit, AreaM2 = 4_000_000_000, Neighbors = neighbors[i], DistanceM = Enumerable.Repeat(100000, neighbors[i].Length).ToArray() };
        ClimateSeason Season(int precipitation, int temperature) => new ClimateSeason { PrecipitationMm = Enumerable.Repeat(precipitation, cells.Length).ToArray(), TemperatureTenthsC = Enumerable.Repeat(temperature, cells.Length).ToArray() };
        // Preserve the old synthetic physical forcing after the user-requested
        // 20x baseline change; separate slider cases explicitly test the new range.
        // Pin the historical 3.24 fixture, which uses the 3.22 scale (5% = unit forcing).
        // Current control rebasing is independently covered by TerrainControlTests.
        var recipe = Recipe.Preset("Preview"); recipe.AlgorithmVersion = ClimateCalibration.UntrainedAlgorithm; recipe.LakeAmountPercent = 5;
        return new World
        {
            Recipe = recipe, Surface = new Surface { Cells = cells }, ElevationM = elevation, PreErosionM = new[] { -100, 100, 20, 80, 220 },
            Continent = new[] { -1, 0, 0, 0, 0 }, Glaciated = new bool[cells.Length], Features = Array.Empty<Feature>(),
            Hydrology = new HydrologyResult { OceanConnected = new[] { true, false, false, false, false } },
            Climate = new ClimateResult { NorthernSpring = Season(300, 200), NorthernSummer = Season(300, 200), NorthernAutumn = Season(150, 50), NorthernWinter = Season(150, 50), Classification = new Koppen[cells.Length], ContinentalityPermille = new int[cells.Length] }
        };
    }

    static World CascadeFixture()
    {
        // ocean -- sill A -- pit A -- sill B -- pit B -- upland
        var world = Fixture(); int[] elevation = { -100, 100, 0, 200, 50, 300 };
        int[][] neighbors = { new[] { 1 }, new[] { 0, 2 }, new[] { 1, 3 }, new[] { 2, 4 }, new[] { 3, 5 }, new[] { 4 } };
        var cells = new Cell[elevation.Length];
        for (int i = 0; i < cells.Length; i++) cells[i] = new Cell { Id = i, Position = new V(1, i * .01, 0).Unit, AreaM2 = 4_000_000_000, Neighbors = neighbors[i], DistanceM = Enumerable.Repeat(100000, neighbors[i].Length).ToArray() };
        ClimateSeason Season(int precipitation, int temperature) => new ClimateSeason { PrecipitationMm = Enumerable.Repeat(precipitation, cells.Length).ToArray(), TemperatureTenthsC = Enumerable.Repeat(temperature, cells.Length).ToArray() };
        world.Surface = new Surface { Cells = cells }; world.ElevationM = elevation; world.PreErosionM = (int[])elevation.Clone(); world.Continent = new[] { -1, 0, 0, 0, 0, 0 };
        world.Glaciated = new bool[cells.Length]; world.Hydrology = new HydrologyResult { OceanConnected = new[] { true, false, false, false, false, false } };
        world.Climate = new ClimateResult { NorthernSpring = Season(300, 200), NorthernSummer = Season(300, 200), NorthernAutumn = Season(150, 50), NorthernWinter = Season(150, 50), Classification = new Koppen[cells.Length], ContinentalityPermille = new int[cells.Length] };
        return world;
    }

    static World TwoExitFixture()
    {
        var world = Fixture(); int[] elevation = { -100, 100, 20, -20, 20, 100, 220 };
        int[][] neighbors = { new[] { 1, 5 }, new[] { 0, 2 }, new[] { 1, 3 }, new[] { 2, 4, 6 }, new[] { 3, 5 }, new[] { 0, 4 }, new[] { 3 } };
        world.Surface = new Surface { Cells = Enumerable.Range(0, elevation.Length).Select(i => new Cell { Id = i, Position = new V(1, i * .01, 0).Unit, AreaM2 = 4_000_000_000, Neighbors = neighbors[i], DistanceM = Enumerable.Repeat(100000, neighbors[i].Length).ToArray() }).ToArray() };
        world.ElevationM = elevation; world.PreErosionM = (int[])elevation.Clone(); world.Glaciated = new bool[elevation.Length];
        world.Hydrology = new HydrologyResult { OceanConnected = new[] { true, false, false, false, false, false, false } };
        ClimateSeason Season(int precipitation, int temperature) => new ClimateSeason { PrecipitationMm = Enumerable.Repeat(precipitation, elevation.Length).ToArray(), TemperatureTenthsC = Enumerable.Repeat(temperature, elevation.Length).ToArray() };
        world.Climate = new ClimateResult { NorthernSpring = Season(300, 200), NorthernSummer = Season(300, 200), NorthernAutumn = Season(150, 50), NorthernWinter = Season(150, 50) };
        return world;
    }

    static bool Connected(Surface surface, int[] cells)
    {
        if (cells.Length == 0) return false; var members = cells.ToHashSet(); var seen = new HashSet<int> { cells[0] }; var queue = new Queue<int>(); queue.Enqueue(cells[0]);
        while (queue.Count > 0) foreach (int next in surface.Cells[queue.Dequeue()].Neighbors) if (members.Contains(next) && seen.Add(next)) queue.Enqueue(next);
        return seen.Count == cells.Length;
    }

    sealed class InvalidRunoff : ISeasonalRunoffProvider
    {
        public double[] RunoffDepthMetres(World world, int seasonIndex) => new[] { -1.0 };
    }
}
