using System;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public enum Koppen { Ocean, Af, Am, Aw, BWh, BWk, BSh, BSk, Csa, Csb, Csc, Cwa, Cwb, Cwc, Cfa, Cfb, Cfc, Dsa, Dsb, Dsc, Dsd, Dwa, Dwb, Dwc, Dwd, Dfa, Dfb, Dfc, Dfd, ET, EF }
    [Serializable]
    public sealed class ClimateSeason
    {
        public int MoistureBudgetResidualPpm;
        public int[] TemperatureTenthsC = Array.Empty<int>(), PrecipitationMm = Array.Empty<int>(), PressureAnomalyPa = Array.Empty<int>();
        public int[] WindEastMilli = Array.Empty<int>(), WindNorthMilli = Array.Empty<int>(), CurrentEastMilli = Array.Empty<int>(), CurrentNorthMilli = Array.Empty<int>(), OceanWarmthMilli = Array.Empty<int>(), RainShadowPermille = Array.Empty<int>();
        public int[] MoisturePermille = Array.Empty<int>(), MoistureTransportKm = Array.Empty<int>();
    }
    [Serializable]
    public sealed class ClimateMonth
    {
        public int MoistureBudgetResidualPpm;
        public int[] TemperatureTenthsC = Array.Empty<int>(), PrecipitationMm = Array.Empty<int>();
    }
    [Serializable]
    public sealed class ClimateResult
    {
        public ClimateSeason NorthernSpring = new ClimateSeason(), NorthernSummer = new ClimateSeason(), NorthernAutumn = new ClimateSeason(), NorthernWinter = new ClimateSeason();
        public int[] ContinentalityPermille = Array.Empty<int>(); public Koppen[] Classification = Array.Empty<Koppen>();
        // March through February on the atmospheric mesh (fine surface up to20k, otherwise CoarseSurface).
        public ClimateMonth[] AtmosphericMonths = Array.Empty<ClimateMonth>();
        public int[] AtmosphericElevationM = Array.Empty<int>(); public int[] AtmosphericOceanPermille = Array.Empty<int>();
    }
    /// <summary>Twelve-month empirical circulation, ocean-sourced transport and temperature with four-quarter output; not a general circulation model.</summary>
    public static class Climate
    {
        public static void Build(World w, CancellationToken token)
        {
            int n = w.Surface.Cells.Length;
            // A bounded atmospheric mesh keeps physical travel lengths independent of terrain
            // detail. Its relief and ocean fractions come from THIS fine terrain, not plate flags.
            var s = n > 20000 && w.CoarseSurface.Cells.Length > 0 ? w.CoarseSurface : w.Surface;
            int a = s.Cells.Length; var height = new double[a]; var area = new double[a]; var sea = new double[a];
            var locator = new CellLocator(s.Cells); var mapping = new int[n];
            for (int i = 0; i < n; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                int j = ReferenceEquals(s, w.Surface) ? i : locator.Nearest(w.Surface.Cells[i].Position); mapping[i] = j;
                double weight = Math.Max(1, w.Surface.Cells[i].AreaM2); area[j] += weight;
                height[j] += Math.Max(0, w.ElevationM[i]) * weight;
                if (w.Hydrology.OceanConnected[i]) sea[j] += weight;
            }
            CellLocator fineLocator = null;
            var atmosphere = new World { Recipe = w.Recipe, Surface = s, ElevationM = new int[a],
                Hydrology = new HydrologyResult { OceanConnected = new bool[a] } };
            for (int j = 0; j < a; j++)
            {
                if (area[j] == 0)
                {
                    if (fineLocator == null) fineLocator = new CellLocator(w.Surface.Cells);
                    int i = fineLocator.Nearest(s.Cells[j].Position); area[j] = 1;
                    height[j] = Math.Max(0, w.ElevationM[i]); sea[j] = w.Hydrology.OceanConnected[i] ? 1 : 0;
                }
                height[j] /= area[j]; sea[j] /= area[j];
                atmosphere.ElevationM[j] = D.M(height[j]); atmosphere.Hydrology.OceanConnected[j] = sea[j] >= .5;
            }
            var distance = Graph.Distances(s, Enumerable.Range(0, a).Where(i => sea[i] >= .5), out var nearest, token);
            atmosphere.Climate.ContinentalityPermille = new int[a];
            for (int j = 0; j < a; j++) atmosphere.Climate.ContinentalityPermille[j] = sea[j] >= .5 ? 0 : D.M(1000.0 * distance[j] / (distance[j] + 1800000.0));
            var heat = OceanHeatDrift(atmosphere, token); var months = new ClimateSeason[12];
            var thermalHeight = atmosphere.ElevationM.Select((z, i) => sea[i] >= .5 ? 0 : z).ToArray();
            for (int m = 0; m < 12; m++)
            {
                double circulation = D.Sin(2 * D.Pi * (m - .1) / 12);
                double thermal = D.Sin(2 * D.Pi * (m - 1.35) / 12);
                months[m] = Season(atmosphere, circulation, thermal, nearest, heat, sea, token);
            }
            var result = new ClimateResult { ContinentalityPermille = new int[n], Classification = new Koppen[n],
                AtmosphericMonths = new ClimateMonth[12], AtmosphericElevationM = atmosphere.ElevationM,
                AtmosphericOceanPermille = sea.Select(x => D.M(x * 1000)).ToArray() };
            for (int m = 0; m < 12; m++) result.AtmosphericMonths[m] = new ClimateMonth {
                TemperatureTenthsC = months[m].TemperatureTenthsC, PrecipitationMm = months[m].PrecipitationMm,
                MoistureBudgetResidualPpm = months[m].MoistureBudgetResidualPpm };
            var quarters = new[] { NewSeason(n), NewSeason(n), NewSeason(n), NewSeason(n) };
            result.NorthernSpring = quarters[0]; result.NorthernSummer = quarters[1];
            result.NorthernAutumn = quarters[2]; result.NorthernWinter = quarters[3];
            for (int q = 0; q < 4; q++) quarters[q].MoistureBudgetResidualPpm = months.Skip(q * 3).Take(3).Max(m => m.MoistureBudgetResidualPpm);
            var t = new double[12]; var p = new double[12];
            for (int i = 0; i < n; i++)
            {
                if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                int center = mapping[i]; var neighbors = s.Cells[center].Neighbors;
                // Local inverse-distance interpolation avoids atmospheric Voronoi blocks.
                var ids = new int[neighbors.Length + 1]; var weights = new double[ids.Length]; ids[0] = center;
                for (int k = 0; k < neighbors.Length; k++) ids[k + 1] = neighbors[k];
                double sum = 0;
                for (int k = 0; k < ids.Length; k++)
                {
                    var delta = w.Surface.Cells[i].Position - s.Cells[ids[k]].Position;
                    double d2 = V.Dot(delta, delta); weights[k] = 1 / Math.Max(1e-12, d2); sum += weights[k];
                }
                for (int k = 0; k < ids.Length; k++) weights[k] /= sum;
                double Sample(int[] values) { double x = 0; for (int k = 0; k < ids.Length; k++) x += values[ids[k]] * weights[k]; return x; }
                double baseHeight = Sample(thermalHeight), fineHeight = Math.Max(0, w.ElevationM[i]);
                result.ContinentalityPermille[i] = w.Hydrology.OceanConnected[i] ? 0 : D.M(Sample(atmosphere.Climate.ContinentalityPermille));
                for (int m = 0; m < 12; m++)
                {
                    var month = months[m]; var q = quarters[m / 3];
                    double humidity = Sample(month.MoisturePermille) / 1000;
                    double lapse = .0065 - .001 * humidity;
                    t[m] = D.Q(D.Clamp(Sample(month.TemperatureTenthsC) / 10 - (fineHeight - baseHeight) * lapse, -90, 65), 10);
                    if (w.Hydrology.OceanConnected[i])
                    {
                        double lat = D.Pi / 2 - D.Angle(w.Surface.Cells[i].Position, new V(0, 0, 1));
                        t[m] = D.Q(SeaTemperature(lat, D.Sin(2 * D.Pi * (m - 1.35) / 12), D.M(Sample(month.OceanWarmthMilli))) + ClimateCalibration.Weight(w.Recipe, 0) + w.Recipe.TemperatureOffset, 10);
                    }
                    p[m] = Math.Max(0, D.M(Sample(month.PrecipitationMm)));
                    q.TemperatureTenthsC[i] += D.M(t[m] * 10); q.PrecipitationMm[i] += D.M(p[m]);
                    q.PressureAnomalyPa[i] += D.M(Sample(month.PressureAnomalyPa));
                    q.WindEastMilli[i] += D.M(Sample(month.WindEastMilli)); q.WindNorthMilli[i] += D.M(Sample(month.WindNorthMilli));
                    q.CurrentEastMilli[i] += D.M(Sample(month.CurrentEastMilli)); q.CurrentNorthMilli[i] += D.M(Sample(month.CurrentNorthMilli));
                    q.OceanWarmthMilli[i] += D.M(Sample(month.OceanWarmthMilli));
                    q.RainShadowPermille[i] += D.M(Sample(month.RainShadowPermille)); q.MoisturePermille[i] += D.M(Sample(month.MoisturePermille));
                    q.MoistureTransportKm[i] += D.M(Sample(month.MoistureTransportKm));
                }
                foreach (var q in quarters)
                {
                    q.TemperatureTenthsC[i] = D.M(q.TemperatureTenthsC[i] / 3.0);
                    q.PressureAnomalyPa[i] = D.M(q.PressureAnomalyPa[i] / 3.0);
                    q.WindEastMilli[i] = D.M(q.WindEastMilli[i] / 3.0); q.WindNorthMilli[i] = D.M(q.WindNorthMilli[i] / 3.0);
                    q.CurrentEastMilli[i] = D.M(q.CurrentEastMilli[i] / 3.0); q.CurrentNorthMilli[i] = D.M(q.CurrentNorthMilli[i] / 3.0);
                    q.OceanWarmthMilli[i] = D.M(q.OceanWarmthMilli[i] / 3.0); q.RainShadowPermille[i] = D.M(q.RainShadowPermille[i] / 3.0);
                    q.MoisturePermille[i] = D.M(q.MoisturePermille[i] / 3.0); q.MoistureTransportKm[i] = D.M(q.MoistureTransportKm[i] / 3.0);
                }
                result.Classification[i] = w.Hydrology.OceanConnected[i] ? Koppen.Ocean : ClassifyMonths(t, p, w.Surface.Cells[i].Position.Z >= 0);
            }
            w.Climate = result;
            w.Metric("climate_classes", result.Classification.Where(k => k != Koppen.Ocean).Distinct().Count());
            w.Metric("climate_seasons", 4); w.Metric("climate_simulated_months", 12); w.Metric("atmospheric_cells", a);
            w.Metric("climate_moisture_budget_residual", result.AtmosphericMonths.Max(m => m.MoistureBudgetResidualPpm), "ppm");
        }
        static ClimateSeason NewSeason(int n) => new ClimateSeason {
            TemperatureTenthsC = new int[n], PrecipitationMm = new int[n], PressureAnomalyPa = new int[n],
            WindEastMilli = new int[n], WindNorthMilli = new int[n], CurrentEastMilli = new int[n], CurrentNorthMilli = new int[n],
            OceanWarmthMilli = new int[n], RainShadowPermille = new int[n], MoisturePermille = new int[n], MoistureTransportKm = new int[n] };
        // Basin-connected warm-current reach. Sources are poleward currents on the
        // western ocean boundary, never geographic coordinates. Directed ocean-only
        // travel carries their heat toward the midlatitude eastward return flow.
        static int[] OceanHeatDrift(World w, CancellationToken token)
        {
            var s = w.Surface; int n = s.Cells.Length; var water = w.Hydrology.OceanConnected;
            var cost = Enumerable.Repeat(long.MaxValue, n).ToArray(); var drift = new V[n]; var heap = new Heap();
            for (int i = 0; i < n; i++) if (water[i])
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                var p = s.Cells[i].Position; double lat = D.Pi / 2 - D.Angle(p, new V(0, 0, 1));
                var east = new V(-p.Y, p.X, 0).Unit; var north = V.Cross(p, east).Unit;
                drift[i] = (east * D.Clamp((Math.Abs(lat) - .35) / .35) + north * (Math.Sign(lat) * .65)).Unit;
                V towardLand = new V(); foreach (int j in s.Cells[i].Neighbors) if (!water[j]) towardLand += V.Tangent(p, s.Cells[j].Position);
                double strength = towardLand.Length < .1 ? 0 : Math.Max(0, -V.Dot(towardLand.Unit, east)) * D.Kernel(Math.Abs(Math.Abs(lat) - .5), .32);
                if (strength <= .15) continue;
                cost[i] = D.M((1 - strength) * 4000000); heap.Push(cost[i], i);
            }
            while (heap.Count > 0)
            {
                var item = heap.Pop(); int i = item.Id; if (item.Key != cost[i]) continue;
                if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                var cell = s.Cells[i];
                for (int k = 0; k < cell.Neighbors.Length; k++)
                {
                    int j = cell.Neighbors[k]; if (!water[j]) continue;
                    double align = D.Clamp(V.Dot(drift[i], V.Tangent(cell.Position, s.Cells[j].Position)), -1, 1);
                    long candidate = cost[i] + Math.Max(1, D.M(cell.DistanceM[k] * (1 + 2.2 * (1 - align) * (1 - align))));
                    if (candidate >= 12000000 || candidate >= cost[j]) continue;
                    cost[j] = candidate; heap.Push(candidate, j);
                }
            }
            var heat = new int[n];
            for (int i = 0; i < n; i++) if (water[i] && cost[i] != long.MaxValue) heat[i] = D.M(1000 * D.Kernel(cost[i], 12000000));
            return heat;
        }
        static ClimateSeason Season(World w, double season, double thermalSeason, int[] nearestOcean, int[] oceanHeat, double[] oceanFraction, CancellationToken token)
        {
            var s = w.Surface; int n = s.Cells.Length; var h = w.ElevationM;
            var output = new ClimateSeason { TemperatureTenthsC = new int[n], PrecipitationMm = new int[n], PressureAnomalyPa = new int[n], WindEastMilli = new int[n], WindNorthMilli = new int[n], CurrentEastMilli = new int[n], CurrentNorthMilli = new int[n], OceanWarmthMilli = new int[n], RainShadowPermille = new int[n] };
            output.MoisturePermille = new int[n]; output.MoistureTransportKm = new int[n];
            var latitude = new double[n]; var pressure = new double[n]; var east = new V[n]; var north = new V[n]; var wind = new V[n]; var water = new bool[n];
            for (int i = 0; i < n; i++)
            {
                V p = s.Cells[i].Position; latitude[i] = D.Pi / 2 - D.Angle(p, new V(0, 0, 1)); water[i] = w.Hydrology.OceanConnected[i];
                east[i] = new V(-p.Y, p.X, 0).Unit; if (east[i].Length < .1) east[i] = new V(0, 1, 0); north[i] = V.Cross(p, east[i]).Unit;
                double shifted = latitude[i] - season * .14, lat = Math.Abs(shifted), continentality = w.Climate.ContinentalityPermille[i] / 1000.0;
                pressure[i] = 750 * D.Sin(lat * 6 - D.Pi / 2) - season * D.Sin(latitude[i]) * continentality * 1300;
            }
            var thermalPressure=w.Recipe.AlgorithmVersion==Recipe.CurrentAlgorithm?new double[n]:null;
            var basinWind = AtmosphericCirculation.BasinWind(s, oceanFraction, season, token, out var basinPressure, out var basinStream,thermalPressure);
            var boundaryInflow=thermalPressure==null?null:AtmosphericBoundaryInflow.Solve(s,thermalPressure,w.Recipe.BoundaryInflow,token);
            for (int i = 0; i < n; i++)
            {
                if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                double shifted = latitude[i] - season * .14, lat = Math.Abs(shifted);
                double zonal = -.85 + 1.95 * D.Kernel(Math.Abs(lat - .87), .43);
                double hadley = 1 - D.Clamp((lat - .35) / .25);
                double meridional = -.30 * D.Clamp(shifted / .22, -1, 1) * hadley + Math.Sign(shifted) * .10 * (1 - hadley);
                wind[i] = east[i] * zonal + north[i] * meridional;
                output.PressureAnomalyPa[i] = D.M(pressure[i] + basinPressure[i]);
            }
            // Pressure/nearest-coast gradients on an irregular mesh must describe a
            // synoptic flow, not independent eddies at every graph cell.
            for (int pass = 0; pass < 2; pass++)
            {
                var smooth = new V[n];
                for (int i = 0; i < n; i++)
                {
                    var cell = s.Cells[i]; V average = new V(); double weight = 0;
                    for (int k = 0; k < cell.Neighbors.Length; k++)
                    {
                        int j = cell.Neighbors[k]; double f = 1.0 / Math.Max(1, cell.DistanceM[k]);
                        average += (wind[j] - cell.Position * V.Dot(wind[j], cell.Position)) * f; weight += f;
                    }
                    smooth[i] = weight > 0 ? wind[i] * .5 + average * (.5 / weight) : wind[i];
                }
                wind = smooth;
            }
            for (int i = 0; i < n; i++)
            {
                wind[i] += basinWind[i];
                if(boundaryInflow!=null)wind[i]+=boundaryInflow[i];
                output.WindEastMilli[i] = D.M(V.Dot(wind[i], east[i]) * 1000);
                output.WindNorthMilli[i] = D.M(V.Dot(wind[i], north[i]) * 1000);
            }
            // Deflect coastal flow tangentially; retain its signed poleward heat-transport proxy.
            for (int i = 0; i < n; i++) if (water[i])
            {
                V towardLand = new V();
                foreach (int j in s.Cells[i].Neighbors) if (!water[j]) towardLand += V.Tangent(s.Cells[i].Position, s.Cells[j].Position);
                double easternBoundary = towardLand.Length > .1 ? Math.Max(0, V.Dot(towardLand.Unit, east[i])) : 0;
                V current = wind[i] * .6;
                if (towardLand.Length > .1)
                {
                    towardLand = towardLand.Unit; double blocked = Math.Max(0, V.Dot(current, towardLand)); current -= towardLand * blocked;
                    // Land west of ocean: warm poleward western-boundary flow;
                    // land east of ocean: cool equatorward eastern-boundary flow.
                    current -= V.Cross(s.Cells[i].Position, towardLand) * (Math.Sign(latitude[i]) * .65 * D.Kernel(Math.Abs(Math.Abs(latitude[i]) - .45), .6));
                }
                output.CurrentEastMilli[i] = D.M(V.Dot(current, east[i]) * 1000); output.CurrentNorthMilli[i] = D.M(V.Dot(current, north[i]) * 1000);
                // Cool subtropical eastern boundaries interrupt warm return water;
                // south-facing gulfs and other warm coasts are not cold boundaries.
                double returnFlow = 1 - easternBoundary * D.Kernel(Math.Abs(Math.Abs(latitude[i]) - .42), .4);
                // Ekman transport is to the wind's right in the north, left in the
                // south. Offshore transport replaces surface water with cold water,
                // including monsoon-driven upwelling on western ocean boundaries.
                double offshore = towardLand.Length > .1 ? Math.Max(0, -V.Dot(V.Cross(wind[i], s.Cells[i].Position) * Math.Sign(latitude[i]), towardLand.Unit)) : 0;
                double upwelling = 3 * offshore * D.Clamp(wind[i].Length, 0, 2) * D.Clamp(Math.Abs(latitude[i]) / .12)
                    * D.Clamp((.9 - Math.Abs(latitude[i])) / .4);
                output.OceanWarmthMilli[i] = D.M(D.Clamp(V.Dot(current, north[i]) * Math.Sign(latitude[i]) + returnFlow * oceanHeat[i] / 1000.0 - upwelling, -1, 1) * 1000);
            }
            // Representative ocean-access routes for thermal moderation, instability and
            // inspection only. These are not the mixed moisture trajectories: rainfall
            // below comes from AtmosphericMoisture's finite-volume water budget.
            var cost = Enumerable.Repeat(long.MaxValue, n).ToArray(); var source = Enumerable.Repeat(-1, n).ToArray();
            var barrier = new double[n]; var distance = new long[n]; var airCost = new long[n]; var queue = new Heap();
            var unitWind = new V[n]; for (int i = 0; i < n; i++) unitWind[i] = wind[i].Unit;
            for (int i = 0; i < n; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                if (!water[i]) continue;
                cost[i] = 0; source[i] = i;
                // Interior ocean cells are already source terminals and never relax another ocean.
                // Enqueue only coastal sources; this is numerically identical and bounds the initial heap.
                foreach (int j in s.Cells[i].Neighbors) if (!water[j]) { queue.Push(0, i); break; }
            }
            while (queue.Count > 0)
            {
                var item = queue.Pop(); int i = item.Id; if (item.Key != cost[i]) continue;
                if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                var cell = s.Cells[i];
                for (int k = 0; k < cell.Neighbors.Length; k++)
                {
                    int j = cell.Neighbors[k]; if (water[j]) continue;
                    double aligned = D.Clamp(-V.Dot(unitWind[j], V.Tangent(s.Cells[j].Position, cell.Position)), -1, 1);
                    double travel = Math.Max(1, cell.DistanceM[k]); double ascent = Math.Max(0, h[j] - Math.Max(0, h[i]));
                    // Low rolling relief mixes/recycles moisture; major ranges remove much more
                    // through cooling into the free troposphere. Do not count every 100 m hill as a range.
                    double rainout = 550 + 1900 * D.Clamp((h[j] - 1000) / 2000.0);
                    double transport = travel * (.65 + 2 * (1 - aligned) * (1 - aligned));
                    long step = Math.Max(1L, (long)Math.Round(transport + ascent * rainout, MidpointRounding.AwayFromZero));
                    long candidate = cost[i] + step;
                    if (candidate >= cost[j]) continue;
                    cost[j] = candidate; source[j] = source[i]; distance[j] = distance[i] + (long)travel;
                    // Losing humidity over a range is not losing all marine heat.
                    airCost[j] = airCost[i] + Math.Max(1L, (long)Math.Round(transport + ascent * 150, MidpointRounding.AwayFromZero));
                    barrier[j] = D.Q(barrier[i] / (1 + travel / 1200000) + ascent, 1000);
                    queue.Push(candidate, j, i);
                }
            }
            var provisional = new double[n];
            double Temperature(int i, double clouds)
            {
                double sine = D.Sin(Math.Abs(latitude[i])), sine2 = sine * sine;
                double polar = D.Kernel(D.Pi / 2 - Math.Abs(latitude[i]), .4);
                double baseline = 29 - 32 * sine2 - 7 * sine2 * sine2 * sine2 * sine2 - 12 * polar;
                double airContinentality = source[i] < 0 ? 1 : airCost[i] / (airCost[i] + 1400000.0);
                double c = Math.Max(w.Climate.ContinentalityPermille[i] / 1000.0, airContinentality);
                double sourceSea = source[i] < 0 ? -1.8 : SeaTemperature(latitude[source[i]], thermalSeason, output.OceanWarmthMilli[source[i]]);
                double sourceIce = source[i] < 0 ? 0 : D.Clamp(-sourceSea / 1.8);
                c = Math.Max(c, .65 * sourceIce);
                baseline -= 6 * c * sine2 * ClimateCalibration.Weight(w.Recipe, 2);
                // Insolation seasonality tends to zero at the equator; dry land increases
                // the seasonal range, not an arbitrary constant winter cooling.
                double amplitude = (12 * sine + c * (8 * sine + 27 * sine2)) * ClimateCalibration.Weight(w.Recipe, 1);
                double lapse = Math.Max(0, h[i]) * (.0065 - .001 * clouds);
                double snowCooling = 12 * D.Clamp((-(baseline - lapse) - 2) / 18);
                double temperature = baseline + thermalSeason * Math.Sign(latitude[i]) * amplitude * (1 - .15 * clouds)
                    - lapse - Math.Max(0, thermalSeason * Math.Sign(latitude[i])) * snowCooling;
                if (water[i]) temperature = SeaTemperature(latitude[i], thermalSeason, output.OceanWarmthMilli[i]);
                else if (source[i] >= 0)
                {
                    double onshore = source[i] == i ? 1 : Math.Max(0, -V.Dot(wind[i].Unit, V.Tangent(s.Cells[i].Position, s.Cells[source[i]].Position)));
                    double marine = .7 * (.15 + .85 * onshore) / (1 + airCost[i] / 700000.0) * ClimateCalibration.Weight(w.Recipe, 3);
                    // Freezing WATER is not freezing AIR. An ice cover insulates
                    // the ocean; Arctic winter air can remain far below -1.8C.
                    double sourceSine = D.Sin(Math.Abs(latitude[source[i]])), sourceS2 = sourceSine * sourceSine;
                    double coldAir = 29 - 32 * sourceS2 - 7 * sourceS2 * sourceS2 * sourceS2 * sourceS2
                        - 12 * D.Kernel(D.Pi / 2 - Math.Abs(latitude[source[i]]), .4)
                        + thermalSeason * Math.Sign(latitude[source[i]]) * 12 * sourceSine;
                    double seaAir = sourceSea * (1 - sourceIce) + Math.Min(sourceSea, coldAir) * sourceIce - lapse;
                    temperature = temperature * (1 - marine) + seaAir * marine;
                }
                return D.Clamp(temperature + ClimateCalibration.Weight(w.Recipe, 0) + w.Recipe.TemperatureOffset, -90, 65);
            }
            var instability = new double[n];
            for (int i = 0; i < n; i++)
            {
                provisional[i] = Temperature(i, .5);
                if (!water[i] && source[i] >= 0)
                {
                    double seaTemperature = SeaTemperature(latitude[source[i]], thermalSeason, output.OceanWarmthMilli[source[i]]);
                    double onshore = Math.Max(0, -V.Dot(wind[i].Unit, V.Tangent(s.Cells[i].Position, s.Cells[source[i]].Position)));
                    instability[i] = D.Clamp((seaTemperature - 20) / 7) * onshore / (1 + airCost[i] / 1200000.0);
                }
            }
            double budgetResidual = AtmosphericMoisture.Solve(s, h.Select(x => (double)x).ToArray(), oceanFraction, wind, provisional, season, token,
                out var rain, out var humidity, out var shadow, basinStream, basinWind, instability, w.Recipe);
            output.MoistureBudgetResidualPpm = D.M(budgetResidual * 1000000);
            double moistureScale = Math.Max(0, 1 + w.Recipe.PrecipitationOffset);
            for (int i = 0; i < n; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                // Explicit scenario water inventory: a bootstrap for oceanless wet worlds,
                // not ocean transport or a biome/classification override. Monthly input.
                double inland=w.Recipe.InteriorRainMm<=0?0:w.Recipe.InteriorRainMm/12 * (.55+.45*D.Clamp((provisional[i]+15)/40)) * (.75+.5*D.Clamp(.5+Terrain.Noise(s.Cells[i].Position,w.Recipe.Seed^17631,5)));
                output.PrecipitationMm[i] = D.M(Math.Max(0, (rain[i] * (365.2425 / 12)+inland) * moistureScale));
                output.MoisturePermille[i] = D.M(D.Clamp(humidity[i]) * 1000);
                output.MoistureTransportKm[i] = source[i] < 0 ? -1 : D.M(distance[i] / 1000.0);
                output.RainShadowPermille[i] = D.M(D.Clamp(Math.Max(shadow[i], barrier[i] / 2400)) * 1000);
                output.TemperatureTenthsC[i] = D.M(Temperature(i, D.Clamp(humidity[i])) * 10);
            }
            return output;
        }
        static double SeaTemperature(double latitude, double season, int warmthMilli)
        {
            double sine = D.Sin(Math.Abs(latitude)), s2 = sine * sine, s8 = s2 * s2 * s2 * s2;
            // Seasonal sea-ice buffering suppresses the spurious warm polar ocean
            // that otherwise warms adjacent ice-sheet cells above forest thresholds.
            double ice = D.Kernel(D.Pi / 2 - Math.Abs(latitude), .25);
            double heatContrast = 1 + 2 * D.Clamp((Math.Abs(latitude) - .6) / .5);
            double currentTemperature = warmthMilli * (warmthMilli < 0 ? .004 * (1 + D.Kernel(Math.Abs(latitude), .6)) : .004 * heatContrast);
            return Math.Max(-1.8, 29 - 30 * s2 - s8 - 12 * ice + season * Math.Sign(latitude) * sine * 6 * (1 - .8 * ice) + currentTemperature);
        }
        /// <summary>Quarter totals/means ordered northern spring, summer, autumn, winter.</summary>
        public static Koppen ClassifySeasons(double[] temperatureC, double[] precipitationMm, bool northernHemisphere)
        {
            if (temperatureC.Length != 4 || precipitationMm.Length != 4) throw new ArgumentException("Four quarter-year samples are required.");
            var t = new double[12]; var p = new double[12];
            for (int q = 0; q < 4; q++) for (int m = 0; m < 3; m++) { t[q * 3 + m] = temperatureC[q]; p[q * 3 + m] = precipitationMm[q] / 3; }
            return ClassifyMonths(t, p, northernHemisphere);
        }
        /// <summary>Actual monthly means/totals, March through February; Beck2018 0C C/D convention.</summary>
        public static Koppen ClassifyMonths(double[] temperatureC, double[] precipitationMm, bool northernHemisphere)
        {
            if (temperatureC.Length != 12 || precipitationMm.Length != 12) throw new ArgumentException("Twelve monthly samples are required.");
            double annual = 0, mean = 0, hot = -100, cold = 100, dry = double.MaxValue;
            double summer = 0, sDry = double.MaxValue, wDry = double.MaxValue, sWet = 0, wWet = 0; int warm = 0;
            double aprilSeptember = 0, octoberMarch = 0;
            for (int m = 0; m < 12; m++) { if (m >= 1 && m <= 6) aprilSeptember += temperatureC[m]; else octoberMarch += temperatureC[m]; }
            bool northernWarmHalf = aprilSeptember == octoberMarch ? northernHemisphere : aprilSeptember > octoberMarch;
            for (int m = 0; m < 12; m++)
            {
                double t = temperatureC[m], p = Math.Max(0, precipitationMm[m]);
                bool localSummer = m >= 1 && m <= 6; if (!northernWarmHalf) localSummer = !localSummer;
                annual += p; mean += t / 12; hot = Math.Max(hot, t); cold = Math.Min(cold, t); dry = Math.Min(dry, p); if (t > 10) warm++;
                if (localSummer) { summer += p; sDry = Math.Min(sDry, p); sWet = Math.Max(sWet, p); }
                else { wDry = Math.Min(wDry, p); wWet = Math.Max(wWet, p); }
            }
            double summerShare = annual > 0 ? summer / annual : .5;
            double threshold = Math.Max(0, 20 * mean + (summerShare > .7 ? 280 : summerShare < .3 ? 0 : 140));
            if (annual < threshold * .5) return mean >= 18 ? Koppen.BWh : Koppen.BWk;
            if (annual < threshold) return mean >= 18 ? Koppen.BSh : Koppen.BSk;
            if (hot <= 0) return Koppen.EF; if (hot <= 10) return Koppen.ET;
            if (cold >= 18) return dry >= 60 ? Koppen.Af : dry >= 100 - annual / 25 ? Koppen.Am : Koppen.Aw;
            bool drySummer = sDry < wWet / 3 && sDry < 40, dryWinter = wDry < sWet / 10;
            if (drySummer && dryWinter) { drySummer = summer < annual - summer; dryWinter = !drySummer; }
            int heat = hot >= 22 ? 0 : warm >= 4 ? 1 : 2;
            if (cold > 0) return (Koppen)((int)(drySummer ? Koppen.Csa : dryWinter ? Koppen.Cwa : Koppen.Cfa) + heat);
            if (cold < -38 && heat == 2) heat = 3;
            return (Koppen)((int)(drySummer ? Koppen.Dsa : dryWinter ? Koppen.Dwa : Koppen.Dfa) + heat);
        }
        // Historical two-half-year API; new generated worlds classify twelve actual months.
        public static Koppen Classify(double summerC, double winterC, double summerMm, double winterMm)
        {
            double hot = Math.Max(summerC, winterC), cold = Math.Min(summerC, winterC), annual = summerMm + winterMm, mean = (summerC + winterC) / 2;
            if (hot < 0) return Koppen.EF;
            if (hot < 10) return Koppen.ET;
            double summerShare = annual > 0 ? summerMm / annual : .5;
            double dryThreshold = Math.Max(0, 20 * mean + (summerShare >= .7 ? 280 : summerShare <= .3 ? 0 : 140));
            if (annual < dryThreshold * .5) return mean >= 18 ? Koppen.BWh : Koppen.BWk;
            if (annual < dryThreshold) return mean >= 18 ? Koppen.BSh : Koppen.BSk;
            double dryMonth = Math.Min(summerMm, winterMm) / 6;
            if (cold >= 18) return dryMonth >= 60 ? Koppen.Af : dryMonth >= 100 - annual / 25 ? Koppen.Am : Koppen.Aw;
            bool drySummer = summerMm < winterMm / 3 && summerMm / 6 < 40, dryWinter = winterMm < summerMm / 10;
            // Infer the warm-month count from a two-season sinusoidal climatology.
            int warmMonths = 0; for (int m = 0; m < 12; m++) if (mean + (hot - cold) / 2 * D.Sin(2 * D.Pi * m / 12) > 10) warmMonths++;
            int heat = hot >= 22 ? 0 : warmMonths >= 4 ? 1 : 2;
            if (cold > 0) return (Koppen)((int)(drySummer ? Koppen.Csa : dryWinter ? Koppen.Cwa : Koppen.Cfa) + heat);
            if (cold < -38 && heat == 2) heat = 3;
            return (Koppen)((int)(drySummer ? Koppen.Dsa : dryWinter ? Koppen.Dwa : Koppen.Dfa) + heat);
        }
    }
}
