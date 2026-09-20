using System;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public enum Koppen { Ocean, Af, Am, Aw, BWh, BWk, BSh, BSk, Csa, Csb, Csc, Cwa, Cwb, Cwc, Cfa, Cfb, Cfc, Dsa, Dsb, Dsc, Dsd, Dwa, Dwb, Dwc, Dwd, Dfa, Dfb, Dfc, Dfd, ET, EF }
    [Serializable]
    public sealed class ClimateSeason
    {
        public int[] TemperatureTenthsC = Array.Empty<int>(), PrecipitationMm = Array.Empty<int>(), PressureAnomalyPa = Array.Empty<int>();
        public int[] WindEastMilli = Array.Empty<int>(), WindNorthMilli = Array.Empty<int>(), CurrentEastMilli = Array.Empty<int>(), CurrentNorthMilli = Array.Empty<int>(), OceanWarmthMilli = Array.Empty<int>(), RainShadowPermille = Array.Empty<int>();
    }
    [Serializable]
    public sealed class ClimateResult
    {
        public ClimateSeason NorthernSummer = new ClimateSeason(), NorthernWinter = new ClimateSeason();
        public int[] ContinentalityPermille = Array.Empty<int>(); public Koppen[] Classification = Array.Empty<Koppen>();
    }
    /// <summary>Independent two-season circulation, moisture transport, temperature and climate classification.</summary>
    public static class Climate
    {
        public static void Build(World w, CancellationToken token)
        {
            int n = w.Surface.Cells.Length; var s = w.Surface;
            var oceanDistance = Graph.Distances(s, Enumerable.Range(0, n).Where(i => w.Hydrology.OceanConnected[i]), out _, token);
            var landArea = new double[Math.Max(0, w.Landmass.DefaultIfEmpty(-1).Max() + 1)];
            for (int i = 0; i < n; i++) if (w.Landmass[i] >= 0) landArea[w.Landmass[i]] += s.Cells[i].AreaM2;
            var continentality = new int[n];
            for (int i = 0; i < n; i++) if (!w.Hydrology.OceanConnected[i])
            {
                double distance = oceanDistance[i]; double size = w.Landmass[i] >= 0 ? D.Clamp(landArea[w.Landmass[i]] / 8e12) : 0;
                continentality[i] = D.M(1000 * distance / (distance + 900000) * (.2 + .8 * size));
            }
            w.Climate = new ClimateResult { ContinentalityPermille = continentality };
            w.Climate.NorthernSummer = Season(w, 1, token); w.Climate.NorthernWinter = Season(w, -1, token);
            w.Climate.Classification = new Koppen[n];
            for (int i = 0; i < n; i++)
            {
                if (w.Hydrology.OceanConnected[i]) { w.Climate.Classification[i] = Koppen.Ocean; continue; }
                bool north = s.Cells[i].Position.Z >= 0;
                var summer = north ? w.Climate.NorthernSummer : w.Climate.NorthernWinter; var winter = north ? w.Climate.NorthernWinter : w.Climate.NorthernSummer;
                w.Climate.Classification[i] = Classify(summer.TemperatureTenthsC[i] / 10.0, winter.TemperatureTenthsC[i] / 10.0, summer.PrecipitationMm[i], winter.PrecipitationMm[i]);
            }
            w.Metric("climate_classes", w.Climate.Classification.Where(k => k != Koppen.Ocean).Distinct().Count());
        }
        static ClimateSeason Season(World w, int season, CancellationToken token)
        {
            var s = w.Surface; int n = s.Cells.Length; var h = w.ElevationM;
            var output = new ClimateSeason { TemperatureTenthsC = new int[n], PrecipitationMm = new int[n], PressureAnomalyPa = new int[n], WindEastMilli = new int[n], WindNorthMilli = new int[n], CurrentEastMilli = new int[n], CurrentNorthMilli = new int[n], OceanWarmthMilli = new int[n], RainShadowPermille = new int[n] };
            var latitude = new double[n]; var pressure = new double[n]; var east = new V[n]; var north = new V[n]; var wind = new V[n]; var water = new bool[n];
            for (int i = 0; i < n; i++)
            {
                V p = s.Cells[i].Position; latitude[i] = D.Pi / 2 - D.Angle(p, new V(0, 0, 1)); water[i] = w.Hydrology.OceanConnected[i];
                east[i] = new V(-p.Y, p.X, 0).Unit; if (east[i].Length < .1) east[i] = new V(0, 1, 0); north[i] = V.Cross(p, east[i]).Unit;
                double shifted = latitude[i] - season * .14, lat = Math.Abs(shifted), continentality = w.Climate.ContinentalityPermille[i] / 1000.0;
                pressure[i] = 750 * D.Sin(lat * 6 - D.Pi / 2) - season * Math.Sign(latitude[i]) * continentality * 380 - Math.Max(0, h[i]) * .045;
            }
            for (int i = 0; i < n; i++)
            {
                if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                V gradient = new V(); var cell = s.Cells[i];
                for (int k = 0; k < cell.Neighbors.Length; k++) { int j = cell.Neighbors[k]; gradient += V.Tangent(cell.Position, s.Cells[j].Position) * ((pressure[j] - pressure[i]) / cell.DistanceM[k] * 1000); }
                gradient /= Math.Max(1, cell.Neighbors.Length);
                double shifted = latitude[i] - season * .14, lat = Math.Abs(shifted), coriolis = Math.Sign(latitude[i]);
                double zonal = lat < D.Pi / 6 ? -.85 : lat < D.Pi * .36 ? 1.0 : -.55;
                double meridional = lat < D.Pi / 6 ? -Math.Sign(shifted) * .35 : Math.Sign(shifted) * .12;
                wind[i] = east[i] * zonal + north[i] * meridional - gradient * .035 + V.Cross(cell.Position, gradient) * (coriolis * .07);
                output.WindEastMilli[i] = D.M(V.Dot(wind[i], east[i]) * 1000); output.WindNorthMilli[i] = D.M(V.Dot(wind[i], north[i]) * 1000); output.PressureAnomalyPa[i] = D.M(pressure[i]);
            }
            // Deflect coastal flow tangentially; retain its signed poleward heat-transport proxy.
            for (int i = 0; i < n; i++) if (water[i])
            {
                V towardLand = new V();
                foreach (int j in s.Cells[i].Neighbors) if (!water[j]) towardLand += V.Tangent(s.Cells[i].Position, s.Cells[j].Position);
                V current = wind[i] * .6;
                if (towardLand.Length > .1)
                {
                    towardLand = towardLand.Unit; double blocked = Math.Max(0, V.Dot(current, towardLand)); current -= towardLand * blocked;
                    current += V.Cross(s.Cells[i].Position, towardLand) * (Math.Sign(latitude[i]) * .25);
                }
                output.CurrentEastMilli[i] = D.M(V.Dot(current, east[i]) * 1000); output.CurrentNorthMilli[i] = D.M(V.Dot(current, north[i]) * 1000);
                output.OceanWarmthMilli[i] = D.M(D.Clamp(V.Dot(current, north[i]) * Math.Sign(latitude[i]), -1, 1) * 1000);
            }
            var moisture = new double[n]; var shadow = new double[n];
            for (int i = 0; i < n; i++) moisture[i] = water[i] ? 1 : .1;
            // Wind, topography and ocean evaporation are fixed during advection.
            // Cache their exact operands; keep multiply/divide order unchanged so
            // this optimization preserves the reviewed cross-host hashes.
            var offsets = new int[n + 1]; var evaporation = new double[n];
            for (int i = 0; i < n; i++)
            {
                offsets[i + 1] = offsets[i] + (water[i] ? 0 : s.Cells[i].Neighbors.Length);
                if (water[i]) evaporation[i] = .5 + .5 * D.Sin(D.Pi / 2 - latitude[i]);
            }
            var upwindWeight = new double[offsets[n]]; var attenuation = new double[offsets[n]]; var barriers = new double[offsets[n]];
            for (int i = 0; i < n; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested(); if (water[i]) continue;
                var cell = s.Cells[i];
                for (int k = 0; k < cell.Neighbors.Length; k++)
                {
                    int j = cell.Neighbors[k], index = offsets[i] + k;
                    double upwind = Math.Max(0, -V.Dot(wind[i], V.Tangent(cell.Position, s.Cells[j].Position))); upwindWeight[index] = upwind;
                    double ascent = Math.Max(0, h[i] - h[j]) / 2500.0, barrier = Math.Max(0, h[j] - h[i]) / 3000.0;
                    attenuation[index] = 1 + ascent * 2 + barrier * .2; barriers[index] = D.Clamp(barrier);
                }
            }
            var next = new double[n]; var nextShadow = new double[n];
            // Synchronous, stable neighbor order; two reusable buffer pairs.
            for (int iteration = 0; iteration < 48; iteration++)
            {
                token.ThrowIfCancellationRequested();
                for (int i = 0; i < n; i++)
                {
                    if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
                    if (water[i]) { next[i] = evaporation[i]; continue; }
                    double weighted = 0, sum = 0, obstacle = 0; var cell = s.Cells[i];
                    for (int k = 0; k < cell.Neighbors.Length; k++)
                    {
                        int j = cell.Neighbors[k], index = offsets[i] + k; double upwind = upwindWeight[index]; if (upwind <= 0) continue;
                        weighted += moisture[j] * upwind / attenuation[index]; sum += upwind;
                        obstacle += upwind * Math.Max(shadow[j] * .88, barriers[index]);
                    }
                    next[i] = D.Q(sum > 0 ? Math.Min(1, weighted / sum * .92 + .015) : moisture[i] * .92, 1e6);
                    nextShadow[i] = D.Q(sum > 0 ? obstacle / sum : 0, 1e6);
                }
                var old = moisture; moisture = next; next = old; old = shadow; shadow = nextShadow; nextShadow = old;
            }
            double moistureScale = (1 + w.Recipe.PrecipitationOffset) * D.Clamp(1 - Math.Max(0, w.Recipe.LandFraction - .4) * 1.2, .2, 1);
            for (int i = 0; i < n; i++)
            {
                double shifted = Math.Abs(latitude[i] - season * .14), continentality = w.Climate.ContinentalityPermille[i] / 1000.0;
                double tropical = D.Kernel(shifted, .5), dryBelt = D.Kernel(Math.Abs(shifted - .5), .24), storm = D.Kernel(Math.Abs(shifted - .9), .35);
                double orography = 0;
                foreach (int j in s.Cells[i].Neighbors) if (h[i] > h[j]) orography += Math.Max(0, -V.Dot(wind[i], V.Tangent(s.Cells[i].Position, s.Cells[j].Position))) * (h[i] - h[j]) / 3000.0;
                double precipitation = (45 + moisture[i] * (420 + 1250 * tropical + 600 * storm + 450 * Math.Min(1, orography))) * (1 - .72 * dryBelt) * (1 - .45 * shadow[i]) * moistureScale;
                output.PrecipitationMm[i] = D.M(Math.Max(0, precipitation)); output.RainShadowPermille[i] = D.M(D.Clamp(shadow[i]) * 1000);
                double latFraction = Math.Abs(latitude[i]) / (D.Pi / 2);
                double baseline = 29 - 55 * Math.Pow(latFraction, 1.5), seasonal = season * Math.Sign(latitude[i]) * (4 + continentality * 23) * D.Sin(Math.Abs(latitude[i]));
                double clouds = D.Clamp(precipitation / 1500), elevation = Math.Max(0, h[i]);
                double temperature = baseline + seasonal * (1 - .25 * clouds) - elevation * (.0068 - .0013 * clouds) + w.Recipe.TemperatureOffset;
                if (water[i]) temperature = baseline + seasonal * .25 + output.OceanWarmthMilli[i] * .004 + w.Recipe.TemperatureOffset;
                else
                {
                    int coastal = 0; double warmth = 0;
                    foreach (int j in s.Cells[i].Neighbors) if (water[j]) { coastal++; warmth += output.OceanWarmthMilli[j] * .003; }
                    if (coastal > 0) temperature += warmth / coastal;
                }
                output.TemperatureTenthsC[i] = D.M(D.Clamp(temperature, -90, 65) * 10);
            }
            return output;
        }
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
