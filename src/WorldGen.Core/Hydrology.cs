using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>Persistent, climate-driven drainage over the immutable final terrain.</summary>
    public static class Hydrology
    {
        const double YearSeconds = 365.2425 * 86400;
        const double SeasonSeconds = YearSeconds / 4;
        // Uniform coarse-map priors, not measured aquifer coefficients. The
        // terminal correction is deliberately independent of storage calibration
        // so raising ordinary-lake retention cannot flood enclosed inland seas.
        public const double DefaultLakeRetentionScale = 1;
        public const double DefaultTerminalDeliveryFraction = .02;
        // Owner-selected 3.22 baseline: 5% now reproduces the 3.21 100%
        // hydrological forcing. This rescales water retention, not lake area.
        public const double DefaultLakeAmountScale = 20;
        public static double LakeScale(Recipe recipe) => ((((recipe.AlgorithmVersion == Recipe.CurrentAlgorithm || recipe.AlgorithmVersion == Recipe.PresetAlgorithm) || recipe.AlgorithmVersion == Recipe.SubcellAlgorithm) || recipe.AlgorithmVersion==Recipe.FeatureBlendAlgorithm || recipe.AlgorithmVersion==Recipe.RangeControlsAlgorithm)?4:DefaultLakeAmountScale)*recipe.LakeAmountPercent/100.0;

        public static void Build(World w, CancellationToken token, ISeasonalRunoffProvider provider = null)
        {
            var s = w.Surface; int n = s.Cells.Length; var h = w.ElevationM;
            if (h.Length != n || w.Hydrology.OceanConnected.Length != n) throw new InvalidOperationException("Hydrology requires final terrain and ocean connectivity.");
            bool[] ocean = (bool[])w.Hydrology.OceanConnected.Clone();
            PriorityFlood(s, h, ocean, token, out var receiver, out var floodLevel, out var floodOrder);
            var liquid = LiquidPrecipitation(w, token);
            var depth = BuildRunoff(w, provider, liquid, token);
            var local = Seasons(n);
            for (int i = 0; i < n; i++) if (!ocean[i])
                for (int season = 0; season < 4; season++) local[season][i] = D.Q(depth[season][i] * s.Cells[i].AreaM2 / SeasonSeconds, 1e6);

            int[] depressionLabel = DepressionComponents(s, h, floodLevel, ocean, token, out var components);
            var depressions = new List<Depression>(components.Count); var waters = new List<WaterBody>();
            var waterBody = Enumerable.Repeat(-1, n).ToArray();
            var coverage = new double[n]; var waterVolume = new double[n];
            AddOceans(w, ocean, waterBody, waters, token);

            var catchment = Enumerable.Repeat(-1, n).ToArray();
            foreach (int i in floodOrder)
                catchment[i] = depressionLabel[i] >= 0 ? depressionLabel[i] : receiver[i] >= 0 ? catchment[receiver[i]] : -1;
            var direct = Seasons(components.Count); var directArea = new double[components.Count];
            for (int i = 0; i < n; i++) if (catchment[i] >= 0)
            {
                for (int season = 0; season < 4; season++) direct[season][catchment[i]] += local[season][i] * SeasonSeconds;
                directArea[catchment[i]] += s.Cells[i].AreaM2;
            }
            var tectonicBasin = new bool[n];
            foreach (var feature in w.Features.Where(f => f.Kind == FeatureKind.MarineBasin || f.Kind == FeatureKind.BackArcBasin || f.Kind == FeatureKind.ForelandBasin))
                foreach (int cell in feature.Cells) if (cell >= 0 && cell < n) tectonicBasin[cell] = true;
            var pits = new int[components.Count]; var spills = new int[components.Count]; var effectiveSpills = new int[components.Count];
            var outlets = new int[components.Count]; var downstream = new int[components.Count];
            var downstreamDepression = Enumerable.Repeat(-1, components.Count).ToArray(); var incoming = new int[components.Count];
            for (int id = 0; id < components.Count; id++)
            {
                token.ThrowIfCancellationRequested();
                FindSpill(s, h, floodLevel, receiver, depressionLabel, id, components[id], out pits[id], out spills[id], out outlets[id], out downstream[id]);
                effectiveSpills[id] = spills[id];
                depressions.Add(new Depression { Id = id, PitCell = pits[id], SpillM = spills[id], OutletCell = outlets[id], EffectiveSpillElevationM = spills[id] });
            }
            foreach (int id in Enumerable.Range(0, components.Count).OrderBy(i => spills[i]).ThenBy(i => i))
            {
                long fullArea = components[id].Where(i => h[i] < spills[id]).Sum(i => s.Cells[i].AreaM2);
                bool deepEnclosedSea = fullArea >= 2e11 && h[pits[id]] < -200;
                int[] spillwayCells = Array.Empty<int>();
                // Small coastal bowls also deserve a narrow-outlet search:
                // the old 10,000 km2 gate ignored their resolvable connections.
                effectiveSpills[id] = fullArea >= 1e9 && SumAt(direct, id) > 0 && !deepEnclosedSea
                    ? ConditionSpillway(s, h, receiver, floodLevel, depressionLabel, effectiveSpills, id, pits[id], spills[id], ref outlets[id], ref downstream[id], out spillwayCells, token) : spills[id];
                depressions[id].OutletCell = outlets[id]; depressions[id].EffectiveSpillElevationM = effectiveSpills[id];
                depressions[id].SpillwayIncisionM = spills[id] - effectiveSpills[id]; depressions[id].UnresolvedSpillway = effectiveSpills[id] < spills[id]; depressions[id].SpillwayCells = spillwayCells;
            }
            for (int id = 0; id < components.Count; id++)
            {
                int next = downstream[id] >= 0 ? catchment[downstream[id]] : -1;
                if (next == id) throw new InvalidOperationException("Depression drains to itself.");
                downstreamDepression[id] = next; if (next >= 0) incoming[next]++;
            }

            double candidateLakeArea = 0, retainedLakeArea = 0, dryTransmissionLoss = 0;
            int maximumLakeDepth = 0, closed = 0, processed = 0;
            var cascade = Seasons(components.Count); var outletFlow = Seasons(n);
            for (int season = 0; season < 4; season++) for (int i = 0; i < n; i++) outletFlow[season][i] = double.NaN;
            // This is a storage/delivery multiplier, not a rendered-area quota.
            double amount = LakeScale(w.Recipe);
            double lakeScale = DefaultLakeRetentionScale * amount;
            var depressionHeap = new Heap(); for (int id = 0; id < components.Count; id++) if (incoming[id] == 0) depressionHeap.Push(0, id);
            while (depressionHeap.Count > 0)
            {
                token.ThrowIfCancellationRequested(); int id = depressionHeap.Pop().Id; processed++;
                var cells = components[id]; int pit = pits[id], spill = spills[id], outlet = outlets[id], effectiveSpill = effectiveSpills[id];
                var basis = new double[4];
                for (int season = 0; season < 4; season++) basis[season] = direct[season][id] + cascade[season][id];
                long fullArea = cells.Where(i => h[i] < spill).Sum(i => s.Cells[i].AreaM2); candidateLakeArea += fullArea;
                bool terminalBasin = fullArea >= 2e11 && h[pit] < -200
                    || fullArea >= 1e10 && effectiveSpill - h[pit] >= 200 && SumAt(direct, id) / Math.Max(1, directArea[id]) < .05;
                double delivery = terminalBasin ? D.Clamp(DefaultTerminalDeliveryFraction * amount) : 1;
                double transmissionLoss = basis.Sum() * (1 - delivery);
                var delivered = basis.Select(x => x * delivery).ToArray();
                double confinement = D.Clamp((effectiveSpill - h[pit] - 40) / 300.0);
                double residenceYears = (.025 + 2 * confinement * confinement) * lakeScale;
                if (cells.Count(i => w.Glaciated.Length == n && w.Glaciated[i]) * 4 >= cells.Length) residenceYears *= 4;
                if (terminalBasin) residenceYears = 0;
                double surface; int[] lakeCells; bool doesSpill, subgridOutlet; double[] balance; double evaporationVolume;
                if (lakeScale == 0)
                {
                    surface = h[pit]; lakeCells = Array.Empty<int>(); subgridOutlet = false; evaporationVolume = 0;
                    // No-lakes mode forwards ordinary drainage unchanged. A real
                    // terminal basin remains terminal and records its dry loss.
                    doesSpill = !terminalBasin && basis.Sum() > 0; balance = terminalBasin ? new double[4] : basis;
                }
                else SelectLake(w, depressionLabel, id, cells, pit, effectiveSpill, depth, liquid, delivered, residenceYears, delivery, token,
                    coverage, waterVolume, out surface, out lakeCells, out doesSpill, out subgridOutlet, out balance, out evaporationVolume);
                foreach (int cell in lakeCells) transmissionLoss -= (1 - delivery) * SumAt(depth, cell) * s.Cells[cell].AreaM2 * coverage[cell];
                transmissionLoss = Math.Max(0, transmissionLoss);
                long selectedArea = (long)Math.Round(lakeCells.Sum(i => s.Cells[i].AreaM2 * coverage[i]), MidpointRounding.AwayFromZero);
                int selectedDepth = lakeCells.Length == 0 ? 0 : D.M(lakeCells.Max(i => surface - h[i]));
                bool glacialProvenance = lakeCells.Length > 0 && lakeCells.Count(i => w.Glaciated.Length == n && w.Glaciated[i]) * 4 >= lakeCells.Length;
                bool tectonicProvenance = lakeCells.Length > 0 && lakeCells.Count(i => tectonicBasin[i]) * 4 >= lakeCells.Length;
                bool measuredEnclosedSea = w.Recipe.TerrainSource == "Earth" && selectedArea >= 2e11 && h[pit] < 0;
                var outflow = new double[4]; double annual = balance.Sum();
                if (doesSpill && annual > 0)
                {
                    RerouteToPit(s, h, depressionLabel, id, cells, outlet, receiver, token, depressions[id].SpillwayCells);
                    receiver[outlet] = downstream[id];
                    double positive = balance.Sum(x => Math.Max(0, x));
                    for (int season = 0; season < 4; season++)
                    {
                        outflow[season] = annual * Math.Max(0, balance[season]) / positive;
                        if (outlet >= 0) outletFlow[season][outlet] = outflow[season] / SeasonSeconds;
                    }
                }
                else
                {
                    closed++; depressions[id].OutletCell = -1;
                    RerouteToPit(s, h, depressionLabel, id, cells, pit, receiver, token);
                    // A dry terminal pit has no unresolved surface discharge.
                    if (lakeScale == 0 && terminalBasin)
                        for (int season = 0; season < 4; season++) outletFlow[season][pit] = 0;
                }
                if (lakeCells.Length > 0)
                {
                    bool largeTectonic = selectedArea >= 2e11 && h[pit] < 0 && (tectonicProvenance || measuredEnclosedSea);
                    WaterKind kind = largeTectonic ? WaterKind.TectonicInlandSea : glacialProvenance ? WaterKind.GlacialLake : !doesSpill ? WaterKind.EndorheicLake : WaterKind.Lake;
                    int bodyId = waters.Count; foreach (int i in lakeCells) waterBody[i] = bodyId;
                    waters.Add(new WaterBody { Id = bodyId, Kind = kind, Endorheic = !doesSpill, SurfaceM = D.M(surface), SurfaceElevationM = surface, SpillM = effectiveSpill,
                        OutletCell = doesSpill ? outlet : -1, DownstreamCell = doesSpill ? downstream[id] : -1, DepressionId = id,
                        SubgridOutlet = subgridOutlet, ResidenceTimeYears = residenceYears, CatchmentTransmissionLossM3PerS = D.Q(transmissionLoss / YearSeconds, 1e6),
                        AreaM2 = selectedArea, VolumeM3 = D.Q(lakeCells.Sum(i => waterVolume[i]), 1e3),
                        MeanInflowM3PerS = D.Q((annual + evaporationVolume) / YearSeconds, 1e6), EvaporationM3PerS = D.Q(evaporationVolume / YearSeconds, 1e6), Cells = lakeCells });
                    retainedLakeArea += selectedArea; maximumLakeDepth = Math.Max(maximumLakeDepth, selectedDepth);
                }
                else dryTransmissionLoss += transmissionLoss / YearSeconds;
                int down = downstreamDepression[id]; if (down >= 0)
                {
                    for (int season = 0; season < 4; season++) cascade[season][down] += outflow[season];
                    if (--incoming[down] == 0) depressionHeap.Push(0, down);
                }
            }
            if (processed != components.Count) throw new InvalidOperationException("Depression drainage graph contains a cycle.");
            Topology(s, ocean, receiver, local, outletFlow, token, out var order, out var contributing, out var discharge, out var streamOrder, out var watershed);
            var rivers = new List<RiverReach>();
            for (int i = 0; i < n; i++)
            {
                int to = receiver[i]; if (ocean[i] || to < 0 || coverage[i] >= .9) continue;
                double mean = SumAt(discharge, i) * .25;
                if (contributing[i] < 8e10 || mean < 60) continue;
                double peak = 0; for (int season = 0; season < 4; season++) peak = Math.Max(peak, discharge[season][i]);
                double width = D.Clamp(4.8 * D.Sqrt(peak), 3, 1200);
                rivers.Add(new RiverReach { Id = rivers.Count, From = i, To = to, WatershedId = watershed[i], Order = streamOrder[i],
                    ContributingAreaM2 = contributing[i], DischargePotentialM3PerS = D.Q(mean, 1e6),
                    NorthernSpringDischargeM3PerS = discharge[0][i], NorthernSummerDischargeM3PerS = discharge[1][i],
                    NorthernAutumnDischargeM3PerS = discharge[2][i], NorthernWinterDischargeM3PerS = discharge[3][i], WidthM = D.Q(width, 1e3) });
            }
            w.Hydrology = new HydrologyResult { OceanConnected = ocean, Receiver = receiver, Watershed = watershed, WaterBody = waterBody,
                FloodLevelM = floodLevel, VisitOrder = order, RiverOrder = streamOrder, ContributingAreaM2 = contributing,
                LocalNorthernSpringRunoffM3PerS = local[0], LocalNorthernSummerRunoffM3PerS = local[1], LocalNorthernAutumnRunoffM3PerS = local[2], LocalNorthernWinterRunoffM3PerS = local[3],
                NorthernSpringDischargeM3PerS = discharge[0], NorthernSummerDischargeM3PerS = discharge[1], NorthernAutumnDischargeM3PerS = discharge[2], NorthernWinterDischargeM3PerS = discharge[3],
                LakeCoverageFraction = coverage, LakeWaterVolumeM3 = waterVolume,
                DryBasinTransmissionLossM3PerS = D.Q(dryTransmissionLoss, 1e6),
                Waters = waters.ToArray(), Depressions = depressions.ToArray(), Rivers = rivers.ToArray() };
            double landArea = Enumerable.Range(0, n).Where(i => !ocean[i]).Sum(i => (double)s.Cells[i].AreaM2);
            w.Metric("hydrology_depressions", depressions.Count); w.Metric("hydrology_closed_basins", closed);
            w.Metric("hydrology_unresolved_spillways", depressions.Count(x => x.UnresolvedSpillway));
            w.Metric("hydrology_lakes", waters.Count(x => x.Kind != WaterKind.Ocean)); w.Metric("hydrology_rivers", rivers.Count);
            w.Metric("hydrology_subgrid_throughflow_lakes", waters.Count(x => x.SubgridOutlet));
            w.Metric("hydrology_catchment_transmission_loss", waters.Sum(x => x.CatchmentTransmissionLossM3PerS) + dryTransmissionLoss, "m3/s");
            w.Metric("hydrology_candidate_lake_area_fraction", candidateLakeArea / Math.Max(1, landArea));
            w.Metric("hydrology_lake_amount_percent", w.Recipe.LakeAmountPercent, "%");
            w.Metric("hydrology_lake_retention_multiplier", lakeScale);
            w.Metric("hydrology_lake_area_fraction", retainedLakeArea / Math.Max(1, landArea));
            w.Metric("hydrology_displayed_lake_area_fraction", Enumerable.Range(0, n).Where(i => coverage[i] >= .9).Sum(i => (double)s.Cells[i].AreaM2) / Math.Max(1, landArea));
            var reachesOcean = new bool[n];
            for (int k = order.Length - 1; k >= 0; k--) { int i = order[k]; reachesOcean[i] = ocean[i] || receiver[i] >= 0 && reachesOcean[receiver[i]]; }
            w.Metric("hydrology_ocean_draining_land_fraction", Enumerable.Range(0, n).Where(i => !ocean[i] && reachesOcean[i]).Sum(i => (double)s.Cells[i].AreaM2) / Math.Max(1, landArea));
            w.Metric("hydrology_maximum_lake_depth", maximumLakeDepth, "m");
            w.Metric("hydrology_large_lake_area_fraction", waters.Where(x => x.Kind != WaterKind.Ocean && x.AreaM2 >= 1e10).Sum(x => (double)x.AreaM2) / Math.Max(1, landArea));
            w.Metric("hydrology_deep_lake_area_fraction", waters.Where(x => x.Kind != WaterKind.Ocean && x.Cells.Any(i => x.SurfaceM - h[i] > 250)).Sum(x => (double)x.AreaM2) / Math.Max(1, landArea));
            w.Metric("hydrology_glacial_lake_area_fraction", waters.Where(x => x.Kind == WaterKind.GlacialLake).Sum(x => (double)x.AreaM2) / Math.Max(1, landArea));
            w.Metric("hydrology_tectonic_sea_area_fraction", waters.Where(x => x.Kind == WaterKind.TectonicInlandSea).Sum(x => (double)x.AreaM2) / Math.Max(1, landArea));
        }

        static double[][] Seasons(int count) => Enumerable.Range(0, 4).Select(_ => new double[count]).ToArray();
        static double SumAt(double[][] values, int i) => values[0][i] + values[1][i] + values[2][i] + values[3][i];
        static ClimateSeason[] ClimateSeasons(World w) => new[] { w.Climate.NorthernSpring, w.Climate.NorthernSummer, w.Climate.NorthernAutumn, w.Climate.NorthernWinter };

        static void PriorityFlood(Surface s, int[] h, bool[] ocean, CancellationToken token, out int[] receiver, out int[] level, out int[] order)
        {
            int n = h.Length; receiver = Enumerable.Repeat(-1, n).ToArray(); level = new int[n]; var seen = new bool[n]; var heap = new Heap();
            for (int i = 0; i < n; i++) if (ocean[i]) { seen[i] = true; level[i] = h[i]; heap.Push(h[i], i); }
            if (heap.Count == 0) { int pit = Enumerable.Range(0, n).OrderBy(i => h[i]).ThenBy(i => i).First(); seen[pit] = true; level[pit] = h[pit]; heap.Push(h[pit], pit); }
            var list = new List<int>(n);
            while (heap.Count > 0)
            {
                if ((list.Count & 1023) == 0) token.ThrowIfCancellationRequested(); var item = heap.Pop(); int i = item.Id; list.Add(i);
                foreach (int j in s.Cells[i].Neighbors) if (!seen[j])
                { seen[j] = true; receiver[j] = i; level[j] = Math.Max(h[j], level[i]); heap.Push(level[j], j); }
            }
            order = list.ToArray();
        }

        static int[] DepressionComponents(Surface s, int[] h, int[] level, bool[] ocean, CancellationToken token, out List<int[]> components)
        {
            var labels = Enumerable.Repeat(-1, h.Length).ToArray(); components = new List<int[]>(); var queue = new Queue<int>();
            for (int start = 0; start < h.Length; start++) if (!ocean[start] && level[start] > h[start] + 1 && labels[start] < 0)
            {
                token.ThrowIfCancellationRequested(); int id = components.Count; var cells = new List<int>(); labels[start] = id; queue.Enqueue(start);
                int spill = level[start];
                // Include the zero-depth spill corridor in the component.  A
                // Priority-Flood receiver may cross such a saddle and re-enter
                // drowned cells at the same water level; splitting at the
                // saddle incorrectly makes that depression drain to itself.
                while (queue.Count > 0) { int i = queue.Dequeue(); cells.Add(i); foreach (int j in s.Cells[i].Neighbors) if (!ocean[j] && level[j] == spill && h[j] <= spill && labels[j] < 0) { labels[j] = id; queue.Enqueue(j); } }
                components.Add(cells.OrderBy(i => i).ToArray());
            }
            return labels;
        }

        static void FindSpill(Surface s, int[] h, int[] level, int[] receiver, int[] labels, int id, int[] cells, out int pit, out int spill, out int outlet, out int downstream)
        {
            pit = cells.OrderBy(i => h[i]).ThenBy(i => i).First(); spill = level[cells[0]]; outlet = -1; downstream = -1;
            int expectedSpill = spill;
            if (cells.Any(i => level[i] != expectedSpill)) throw new InvalidOperationException("Depression component has multiple spill levels.");
            foreach (int i in cells)
            {
                int j = receiver[i]; if (j < 0 || labels[j] == id) continue;
                if (outlet < 0 || i < outlet || i == outlet && j < downstream) { outlet = i; downstream = j; }
            }
            if (outlet < 0) throw new InvalidOperationException("Depression has no priority-flood outlet.");
        }

        static int ConditionSpillway(Surface s, int[] h, int[] receiver, int[] floodLevel, int[] labels, int[] effectiveSpills, int id, int pit, int spill, ref int outlet, ref int downstream, out int[] corridor, CancellationToken token)
        {
            corridor = Array.Empty<int>();
            // Coarse ridge samples miss narrow river notches. Lower only the
            // hydraulic sill, never the terrain, and only when its existing
            // downstream course crosses a cheap obstruction into lower ground.
            // Bound vertical incision, integrated cut, and total course length;
            // wide/deep closed basins cannot be erased by a provenance label.
            int maximumCut = Math.Min(200, Math.Max(0, spill - h[pit] - 2));
            // Binary-search bounded candidate cuts (at most eight probes).
            // Integer integrated
            // excavation cost (metres * metres) prefers a short deep notch or
            // a longer shallow river course over a long mountain crossing.
            int low = 5, high = maximumCut, best = spill;
            while (low <= high)
            {
                token.ThrowIfCancellationRequested(); int cut = (low + high) / 2, target = spill - cut; bool found = false;
                var heap = new Heap(); var cost = new Dictionary<int, long> { [pit] = 0 }; var length = new Dictionary<int, long> { [pit] = 0 };
                var previous = new Dictionary<int, int>(); heap.Push(0, pit); int visits = 0;
                while (heap.Count > 0)
                {
                    if ((visits++ & 255) == 0) token.ThrowIfCancellationRequested(); var item = heap.Pop(); int i = item.Id;
                    if (item.Key != cost[i]) continue;
                    if (labels[i] != id && (labels[i] >= 0 ? effectiveSpills[labels[i]] : floodLevel[i]) <= target)
                    {
                        int firstOutside = i; while (labels[previous[firstOutside]] != id) firstOutside = previous[firstOutside];
                        outlet = previous[firstOutside]; downstream = firstOutside;
                        var path = new List<int>(); int cell = i;
                        while (true) { path.Add(cell); if (cell == pit) break; cell = previous[cell]; }
                        path.Reverse(); corridor = path.ToArray(); best = target; found = true; break;
                    }
                    for (int edge = 0; edge < s.Cells[i].Neighbors.Length; edge++)
                    {
                        int j = s.Cells[i].Neighbors[edge];
                        // Outside the basin, inspect only the existing strictly
                        // downstream course; do not invent another basin route.
                        if (labels[i] != id && j != receiver[i]) continue;
                        if (labels[j] != id && floodLevel[j] >= spill) continue;
                        if (labels[j] >= 0 && labels[j] != id && effectiveSpills[labels[j]] > target) continue;
                        long distance = s.Cells[i].DistanceM[edge], nextLength = length[i] + distance;
                        int incision = Math.Max(0, Math.Max(h[i], h[j]) - target);
                        // Twice the trapezoidal integral keeps cost ties in
                        // integers without treating one sampled peak as a flat
                        // ridge extending across the entire graph edge.
                        long nextCost = cost[i] + distance * (2L + Math.Max(0, h[i] - target) + Math.Max(0, h[j] - target));
                        if (incision > 200 || nextLength > 1500000 || nextCost > 100000000) continue;
                        if (cost.TryGetValue(j, out long old) && nextCost >= old) continue;
                        cost[j] = nextCost; length[j] = nextLength; previous[j] = i; heap.Push(nextCost, j);
                    }
                }
                if (found) low = cut + 1; else high = cut - 1;
            }
            return best;
        }

        static void SelectLake(World w, int[] labels, int id, int[] component, int pit, int spill, double[][] runoff, double[][] liquid, double[] basis, double residenceYears, double deliveryFraction, CancellationToken token, double[] coverage, double[] waterVolume, out double surface, out int[] lakeCells, out bool spills, out bool subgridOutlet,
            out double[] balance, out double evaporationVolume)
        {
            var s = w.Surface; var h = w.ElevationM; var seen = new HashSet<int> { pit }; var heap = new Heap(); heap.Push(h[pit], pit);
            var ordered = new List<int>(component.Length); var keys = new List<int>(component.Length);
            while (heap.Count > 0)
            {
                if ((ordered.Count & 1023) == 0) token.ThrowIfCancellationRequested(); var item = heap.Pop(); int i = item.Id;
                ordered.Add(i); keys.Add((int)item.Key);
                foreach (int j in s.Cells[i].Neighbors) if (labels[j] == id && seen.Add(j)) heap.Push(Math.Max(item.Key, h[j]), j);
            }
            if (ordered.Count != component.Length) throw new InvalidOperationException("Disconnected depression component.");
            // A centre height is not a flat tile. Approximate unresolved relief
            // by a continuous uniform hypsometric distribution, with support
            // from the local neighbour relief. The integral gives water volume.
            // Access keys prevent filling a remote pocket through a dry saddle.
            var lower = new double[ordered.Count]; var span = new double[ordered.Count];
            var loss = Seasons(ordered.Count); var evaporativeLoss = new double[ordered.Count]; var seasons = ClimateSeasons(w);
            var events = new List<LakeEvent>(ordered.Count * 2);
            for (int k = 0; k < ordered.Count; k++)
            {
                    int cell = ordered[k]; double area = s.Cells[cell].AreaM2;
                    int minimum = h[cell], maximum = h[cell];
                    foreach (int j in s.Cells[cell].Neighbors) { minimum = Math.Min(minimum, h[j]); maximum = Math.Max(maximum, h[j]); }
                    lower[k] = Math.Max(h[cell], keys[k]); span[k] = Math.Max(5, (maximum - minimum) * .5);
                    for (int season = 0; season < 4; season++)
                    {
                        // The user-controlled lake amount is a water-retention
                        // bias, not an observed evaporation calibration. Apply
                        // its reciprocal to open-water losses as well as changing
                        // residence/delivery, so even a rain-fed terminal basin
                        // shrinks toward dry as the amount approaches zero.
                        double evaporation = PotentialEvaporationM(seasons[season].TemperatureTenthsC[cell]) / LakeScale(w.Recipe);
                        loss[season][k] = area * (liquid[season][cell] - deliveryFraction * runoff[season][cell] - evaporation);
                        evaporativeLoss[k] += area * evaporation;
                    }
                    events.Add(new LakeEvent { Level = lower[k], Index = k, Sign = 1 });
                    events.Add(new LakeEvent { Level = lower[k] + span[k], Index = k, Sign = -1 });
            }
            events.Sort((a, b) => { int c = a.Level.CompareTo(b.Level); if (c != 0) return c; c = a.Index.CompareTo(b.Index); return c != 0 ? c : a.Sign.CompareTo(b.Sign); });
            double budget = basis.Sum(), slope = 0, level = h[pit]; int e = 0;
            // A wetting pit can grow from direct rainfall even when a custom
            // runoff provider supplies zero catchment runoff. Without this
            // positive-slope check, an exactly empty budget was a false fixed
            // point despite precipitation exceeding open-water losses.
            bool wettingPit = false;
            for (int k = 0; k < ordered.Count; k++) if (lower[k] <= level && SumAt(loss, k) > 0) { wettingPit = true; break; }
            bool equilibrium = budget <= 0 && !wettingPit;
            while (!equilibrium && level < spill)
            {
                token.ThrowIfCancellationRequested();
                while (e < events.Count && events[e].Level <= level)
                { var change = events[e++]; slope += change.Sign * SumAt(loss, change.Index) / span[change.Index]; }
                double next = Math.Min(spill, e < events.Count ? events[e].Level : spill);
                double nextBudget = budget + slope * (next - level);
                if (slope < 0 && nextBudget <= 0) { level += budget / -slope; equilibrium = true; }
                else { budget = nextBudget; level = next; }
            }
            // A sampled depression is not an impermeable bucket which has
            // accumulated water forever. Represent unresolved river channels
            // as a linear reservoir: annual net supply = stored volume / tau.
            // This constrains actual water level, area AND volume; unretained
            // supply continues downstream once, through the basin outlet.
            // Deep terminal seas and arid confined basins keep their closed
            // balance above. Subgrid outlets are a drainage prior, NOT proof
            // of a <=200m notch: unlike ConditionSpillway they can cross wider
            // unresolved barriers without modifying the saved heightmap.
            subgridOutlet = false;
            if (residenceYears > 0 && basis.Sum() > 0 && level > h[pit])
            {
                double NetAfterThroughflow(double candidate)
                {
                    double net = basis.Sum();
                    for (int k = 0; k < ordered.Count; k++)
                    {
                        double depth = Math.Max(0, candidate - lower[k]), fraction = D.Clamp(depth / span[k]);
                        double volume = s.Cells[ordered[k]].AreaM2 * (depth < span[k] ? depth * depth / (2 * span[k]) : depth - span[k] * .5);
                        net += SumAt(loss, k) * fraction - volume / residenceYears;
                    }
                    return net;
                }
                if (NetAfterThroughflow(level) < 0)
                {
                    double low = h[pit], high = level;
                    for (int step = 0; step < 36; step++)
                    { token.ThrowIfCancellationRequested(); double middle = (low + high) * .5; if (NetAfterThroughflow(middle) > 0) low = middle; else high = middle; }
                    level = (low + high) * .5; subgridOutlet = true; equilibrium = false;
                }
            }
            surface = D.Q(level, 1e6); spills = subgridOutlet || !equilibrium && surface >= spill;
            balance = (double[])basis.Clone(); evaporationVolume = 0;
            var selected = new List<int>();
            for (int k = 0; k < ordered.Count; k++)
            {
                int cell = ordered[k]; double depth = Math.Max(0, surface - lower[k]);
                double fraction = D.Q(D.Clamp(depth / span[k]), 1e6);
                if (fraction <= 0) continue;
                coverage[cell] = fraction;
                waterVolume[cell] = D.Q(s.Cells[cell].AreaM2 * (depth < span[k] ? depth * depth / (2 * span[k]) : depth - span[k] * .5), 1e3);
                selected.Add(cell); for (int season = 0; season < 4; season++) balance[season] += loss[season][k] * fraction;
                evaporationVolume += evaporativeLoss[k] * fraction;
            }
            // Quantizing the exact equilibrium can leave a few litres/year of
            // numerical imbalance in a continent-scale basin. Assign that
            // residual to storage (not fictitious downstream discharge).
            if (equilibrium && selected.Count > 0) balance[3] = -(balance[0] + balance[1] + balance[2]);
            lakeCells = selected.OrderBy(i => i).ToArray();
        }

        struct LakeEvent { public double Level; public int Index, Sign; }

        static void RerouteToPit(Surface s, int[] h, int[] labels, int id, int[] cells, int pit, int[] receiver, CancellationToken token, int[] corridor = null)
        {
            var seen = new HashSet<int> { pit }; var heap = new Heap(); heap.Push(h[pit], pit); receiver[pit] = -1; int count = 0;
            if (corridor != null) for (int k = 0; k + 1 < corridor.Length; k++)
            { int i = corridor[k]; if (labels[i] != id) break; receiver[i] = corridor[k + 1]; if (seen.Add(i)) heap.Push(h[i], i); }
            while (heap.Count > 0)
            {
                if ((count++ & 1023) == 0) token.ThrowIfCancellationRequested(); int i = heap.Pop().Id;
                foreach (int j in s.Cells[i].Neighbors) if (labels[j] == id && seen.Add(j)) { receiver[j] = i; heap.Push(Math.Max(h[j], h[i]), j); }
            }
            if (seen.Count != cells.Length) throw new InvalidOperationException("Disconnected depression component.");
        }

        static void Topology(Surface s, bool[] ocean, int[] receiver, double[][] local, double[][] outletFlow, CancellationToken token,
            out int[] order, out long[] area, out double[][] discharge, out int[] streamOrder, out int[] watershed)
        {
            int n = receiver.Length; var upstream = new int[n]; area = s.Cells.Select(c => c.AreaM2).ToArray();
            discharge = local.Select(a => (double[])a.Clone()).ToArray(); streamOrder = new int[n];
            for (int i = 0; i < n; i++) if (receiver[i] >= 0) upstream[receiver[i]]++;
            var heap = new Heap(); for (int i = 0; i < n; i++) if (upstream[i] == 0) heap.Push(0, i);
            var list = new List<int>(n); var maximum = new int[n]; var maximumCount = new int[n];
            while (heap.Count > 0)
            {
                if ((list.Count & 1023) == 0) token.ThrowIfCancellationRequested(); int i = heap.Pop().Id; list.Add(i);
                for (int season = 0; season < 4; season++) if (!double.IsNaN(outletFlow[season][i])) discharge[season][i] = outletFlow[season][i];
                int current = maximum[i] == 0 ? 1 : maximum[i] + (maximumCount[i] >= 2 ? 1 : 0); streamOrder[i] = current;
                int j = receiver[i]; if (j < 0) continue;
                area[j] += area[i]; for (int season = 0; season < 4; season++) discharge[season][j] += discharge[season][i];
                if (current > maximum[j]) { maximum[j] = current; maximumCount[j] = 1; } else if (current == maximum[j]) maximumCount[j]++;
                if (--upstream[j] == 0) heap.Push(0, j);
            }
            if (list.Count != n) throw new InvalidOperationException("Hydrology receiver graph contains a cycle.");
            order = list.ToArray(); watershed = Enumerable.Repeat(-1, n).ToArray(); int next = 0;
            for (int k = order.Length - 1; k >= 0; k--)
            {
                int i = order[k]; if (ocean[i]) continue; int j = receiver[i];
                watershed[i] = j < 0 || ocean[j] ? next++ : watershed[j];
            }
            for (int i = 0; i < n; i++) for (int season = 0; season < 4; season++) discharge[season][i] = D.Q(discharge[season][i], 1e6);
        }

        // Northern calendar order is spring, summer, autumn, winter. Each
        // precipitation/runoff value is a quarter-year total, not an annual rate.
        static double[][] LiquidPrecipitation(World w, CancellationToken token)
        {
            int n = w.Surface.Cells.Length; var seasons = ClimateSeasons(w); var liquid = Seasons(n);
            for (int season = 0; season < 4; season++)
                if (seasons[season].PrecipitationMm.Length != n || seasons[season].TemperatureTenthsC.Length != n)
                    throw new InvalidOperationException("Hydrology requires all four climate seasons.");
            for (int i = 0; i < n; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                for (int season = 0; season < 4; season++)
                {
                    double precipitation = seasons[season].PrecipitationMm[i] / 1000.0;
                    if (seasons[season].TemperatureTenthsC[i] >= 0) liquid[season][i] += precipitation;
                    else for (int offset = 1; offset <= 4; offset++)
                    {
                        int melt = (season + offset) % 4;
                        if (seasons[melt].TemperatureTenthsC[i] <= 20) continue;
                        liquid[melt][i] += precipitation; break;
                    }
                }
            }
            return liquid;
        }

        static double[][] BuildRunoff(World w, ISeasonalRunoffProvider provider, double[][] liquid, CancellationToken token)
        {
            int n = w.Surface.Cells.Length; var runoff = Seasons(n);
            if (provider != null)
            {
                for (int season = 0; season < 4; season++)
                {
                    var values = provider.RunoffDepthMetres(w, season);
                    if (values == null || values.Length != n || values.Any(x => x < 0 || double.IsNaN(x) || double.IsInfinity(x)))
                        throw new ArgumentException("Seasonal runoff provider returned invalid quarter-year depths.");
                    runoff[season] = (double[])values.Clone();
                }
                return runoff;
            }
            for (int i = 0; i < n; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested(); if (w.Hydrology.OceanConnected[i]) continue;
                double slope = 0; var cell = w.Surface.Cells[i];
                for (int k = 0; k < cell.Neighbors.Length; k++) slope = Math.Max(slope, Math.Max(0, w.ElevationM[i] - w.ElevationM[cell.Neighbors[k]]) / (double)cell.DistanceM[k]);
                double annual = SumAt(liquid, i);
                double coefficient = D.Clamp(.10 + .28 * annual / (annual + .35) + .22 * D.Clamp(slope / .02), .08, .68);
                for (int season = 0; season < 4; season++) runoff[season][i] = D.Q(liquid[season][i] * coefficient, 1e9);
            }
            return runoff;
        }

        static double PotentialEvaporationM(int temperatureTenthsC)
        {
            double c = temperatureTenthsC / 10.0;
            return D.Clamp((c + 5) * .012, 0, .54);
        }

        static void AddOceans(World w, bool[] ocean, int[] waterBody, List<WaterBody> waters, CancellationToken token)
        {
            int[] labels = Graph.Components(w.Surface, i => ocean[i], token); int count = labels.DefaultIfEmpty(-1).Max() + 1;
            var members = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
            for (int i = 0; i < labels.Length; i++) if (labels[i] >= 0) members[labels[i]].Add(i);
            for (int id = 0; id < count; id++)
            {
                var cells = members[id].ToArray(); long area = cells.Sum(i => w.Surface.Cells[i].AreaM2); double volume = cells.Sum(i => Math.Max(0, -w.ElevationM[i]) * (double)w.Surface.Cells[i].AreaM2);
                int body = waters.Count; foreach (int i in cells) waterBody[i] = body;
                waters.Add(new WaterBody { Id = body, Kind = WaterKind.Ocean, SurfaceM = 0, SpillM = 0, AreaM2 = area, VolumeM3 = D.Q(volume, 1e3), Cells = cells });
            }
        }
    }
}
