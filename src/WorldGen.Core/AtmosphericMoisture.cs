using System;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>
    /// Bounded atmospheric water-column transport, in mm and days. This is a steady
    /// climatological moisture budget, not a resolved weather/circulation model.
    /// Every interior face uses the same volumetric flux in both cell budgets.
    /// Ocean evaporation is the only external source; recycling returns at most
    /// 75 percent of land precipitation, additionally limited by surface humidity
    /// deficit. Condensation consumes transported water.
    /// </summary>
    internal static class AtmosphericMoisture
    {
        internal static double Solve(Surface s, double[] height, double[] oceanFraction,
            V[] wind, double[] temperatureC, double season, CancellationToken token,
            out double[] rainMmDay, out double[] humidity, out double[] shadow,
            double[] basinStreamFunction = null, V[] basinWind = null, double[] maritimeInstability = null, Recipe recipe = null)
        {
            int n = s.Cells.Length;
            rainMmDay = new double[n]; humidity = new double[n]; shadow = new double[n];
            var area = new double[n]; var capacity = new double[n]; var column = new double[n];
            var incoming = new double[n][]; var outgoing = new double[n];
            var convergence = new double[n]; var ascent = new double[n]; var descent = new double[n];
            var stormMixing = new double[n];
            bool anyOcean = false;
            for (int i = 0; i < n; i++)
            {
                var cell = s.Cells[i]; incoming[i] = new double[cell.Neighbors.Length];
                double meanDistance = 0;
                for (int k = 0; k < cell.DistanceM.Length; k++) meanDistance += cell.DistanceM[k];
                meanDistance /= Math.Max(1, cell.DistanceM.Length);
                area[i] = cell.AreaM2 > 0 ? cell.AreaM2 : Math.Max(1, .866 * meanDistance * meanDistance);
                // A deterministic rational-power Clausius-Clapeyron approximation:
                // roughly 5/11/21/40/57 mm at -10/0/10/20/26 Celsius.
                double x = D.Clamp(1 + temperatureC[i] * .0048, .45, 1.28);
                double x2 = x * x, x4 = x2 * x2, x8 = x4 * x4;
                capacity[i] = D.Q(11 * x8 * x4 * x2);
                double latitude = D.Pi / 2 - D.Angle(cell.Position, new V(0, 0, 1));
                double shifted = Math.Abs(latitude - season * .14);
                stormMixing[i] = D.Kernel(Math.Abs(shifted - .95), .60);
                if (oceanFraction[i] > 0) anyOcean = true;
            }
            if (!anyOcean) return 0;
            var faceWidth = new double[n][]; var faceNormal = new V[n][]; var basinFlux = new double[n][];
            for (int i = 0; i < n; i++)
            {
                faceWidth[i] = new double[s.Cells[i].Neighbors.Length];
                faceNormal[i] = new V[s.Cells[i].Neighbors.Length];
                basinFlux[i] = new double[s.Cells[i].Neighbors.Length];
            }
            // These are barycentric dual faces, not Voronoi faces: their normal
            // generally is NOT the direction between the two cell centers.
            // Using center-to-center directions creates spurious alternating
            // convergence/divergence on an irregular mesh, and spotty rainfall.
            foreach (var edge in s.Edges)
            {
                int k = Array.IndexOf(s.Cells[edge.A].Neighbors, edge.B);
                if (k < 0 || edge.TriangleA < 0 || edge.TriangleB < 0 ||
                    edge.TriangleA >= s.Corners.Length || edge.TriangleB >= s.Corners.Length) continue;
                V normal = V.Cross(s.Corners[edge.TriangleA], s.Corners[edge.TriangleB]).Unit;
                double orientation = V.Dot(normal, s.Cells[edge.B].Position - s.Cells[edge.A].Position) < 0 ? -1 : 1;
                normal *= orientation;
                faceWidth[edge.A][k] = edge.LengthM; faceNormal[edge.A][k] = normal;
                int back = Array.IndexOf(s.Cells[edge.B].Neighbors, edge.A);
                if (back >= 0) { faceWidth[edge.B][back] = edge.LengthM; faceNormal[edge.B][back] = normal * -1; }
                if (basinStreamFunction != null && basinStreamFunction.Length == s.Corners.Length)
                {
                    // For u = surfaceNormal cross grad(psi), the A->B volume
                    // transport is the oriented endpoint difference of psi [m2/s].
                    // Endpoint differences telescope to zero around every cell.
                    double flux = orientation * (basinStreamFunction[edge.TriangleB] - basinStreamFunction[edge.TriangleA]);
                    basinFlux[edge.A][k] = flux;
                    if (back >= 0) basinFlux[edge.B][back] = -flux;
                }
            }
            for (int i = 0; i < n; i++)
            {
                if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                var cell = s.Cells[i];
                for (int k = 0; k < cell.Neighbors.Length; k++)
                {
                    int j = cell.Neighbors[k]; if (j <= i) continue;
                    int back = Array.IndexOf(s.Cells[j].Neighbors, i); if (back < 0) continue;
                    double distance = Math.Max(1, cell.DistanceM[k]);
                    // A symmetric dual-face estimate also supports graph-only fixtures.
                    double width = faceWidth[i][k] > 0 ? faceWidth[i][k] : (area[i] / Math.Max(1, cell.Neighbors.Length) +
                        area[j] / Math.Max(1, s.Cells[j].Neighbors.Length)) / distance;
                    bool exactBasin = basinStreamFunction != null && basinWind != null && basinWind.Length == n;
                    V fromWind = exactBasin ? wind[i] - basinWind[i] : wind[i];
                    V toWind = exactBasin ? wind[j] - basinWind[j] : wind[j];
                    double velocity = faceWidth[i][k] > 0
                        ? 5 * V.Dot((fromWind + toWind) * .5, faceNormal[i][k])
                        : 5 * .5 * (V.Dot(fromWind, V.Tangent(cell.Position, s.Cells[j].Position)) -
                            V.Dot(toWind, V.Tangent(s.Cells[j].Position, cell.Position)));
                    double advect = (velocity * width + basinFlux[i][k]) * 86400;
                    // Synoptic weather transports moisture across the mean wind.
                    // Storm-belt eddies exchange existing columns symmetrically;
                    // they do not supply water or bypass mountain condensation.
                    // Stronger transient-storm exchange in temperate/boreal air,
                    // weaker cross-wind leakage through stable subtropical highs.
                    double diffusivity = 250000 + 3000000 * .5 * (stormMixing[i] + stormMixing[j]) * ClimateCalibration.Weight(recipe, 5);
                    double mix = diffusivity * 86400 * width / distance;
                    double forward = Math.Max(0, advect) + mix, reverse = Math.Max(0, -advect) + mix;
                    outgoing[i] += forward / area[i]; outgoing[j] += reverse / area[j];
                    incoming[j][back] = forward / area[j]; incoming[i][k] = reverse / area[i];
                    convergence[i] -= advect / area[i]; convergence[j] += advect / area[j];
                    if (advect != 0)
                    {
                        int from = advect > 0 ? i : j, to = advect > 0 ? j : i;
                        double climb = (Math.Max(0, height[to]) - Math.Max(0, height[from])) / 2200;
                        ascent[to] += Math.Abs(advect) / area[to] * Math.Max(0, climb);
                        descent[to] += Math.Abs(advect) / area[to] * Math.Max(0, -climb);
                    }
                }
            }
            var evaporation = new double[n]; var basicLoss = new double[n];
            var convection = new double[n]; var threshold = new double[n]; var retained = new double[n];
            var landExchange = new double[n];
            for (int i = 0; i < n; i++)
            {
                double latitude = D.Pi / 2 - D.Angle(s.Cells[i].Position, new V(0, 0, 1));
                double shifted = Math.Abs(latitude - season * .14);
                double subsidence = D.Kernel(Math.Abs(shifted - .48), .23);
                double stormActivity = stormMixing[i];
                double warmth = D.Clamp((temperatureC[i] - 8) / 17);
                evaporation[i] = .16 * D.Clamp(oceanFraction[i]) * (.65 + .35 * D.Clamp(wind[i].Length)) * ClimateCalibration.Weight(recipe, 4);
                // Smooth fractional rainout avoids a hard humidity cutoff leaving
                // inland vapor stranded below a deep-convection threshold forever.
                // Positive air convergence exits upward, condensing its vapor; negative
                // convergence is balanced by dry upper-air descent, not invented water.
                basicLoss[i] = .006 + .025 * stormActivity * ClimateCalibration.Weight(recipe, 6) + Math.Max(0, convergence[i]) + ascent[i] * ClimateCalibration.Weight(recipe, 7);
                double maritimeLift = maritimeInstability == null ? 0 : maritimeInstability[i];
                // Heated land supports stronger convective overturning than the
                // ocean mixed layer. Both consume the transported finite column;
                // lower marine rainout permits ocean vapor to reach humid land.
                double stability = 1 - .90 * subsidence * (1 - .5 * maritimeLift);
                double summerHeating = D.Clamp(.4 + .6 * season * D.Clamp(2 * D.Sin(latitude), -1, 1));
                double landConvectiveRate = .05 + .22 * D.Kernel(shifted, .30) + .20 * maritimeLift * summerHeating;
                double convectiveRate = landConvectiveRate * (1 - D.Clamp(oceanFraction[i])) + .05 * D.Clamp(oceanFraction[i]);
                convection[i] = convectiveRate * warmth * stability * ClimateCalibration.Weight(recipe, 8);
                threshold[i] = 0;
                // Immediate local recycling is strongest over warm lowlands. Cold
                // rain/snow and forced mountain condensation mostly leave the air
                // column as drainage/storage, rather than evaporating on the same face.
                double warmRecycling = D.Clamp(temperatureC[i] / 10);
                double majorRelief = D.Clamp((height[i] - 500) / 1500);
                double ascentSuppression = 1 / (1 + ascent[i] * 20 * majorRelief);
                retained[i] = 1 - (.10 + .65 * warmRecycling * ascentSuppression * stability) * (1 - D.Clamp(oceanFraction[i]));
                // Bulk surface exchange: evaporation falls as air approaches saturation.
                // The land resistance is larger than the ocean's; available precipitation
                // still bounds this return flux, so dry land cannot invent water. This is
                // a climatological closure, not a soil reservoir or full energy balance.
                landExchange[i] = .04 * (1 - D.Clamp(oceanFraction[i])) *
                    (.65 + .35 * D.Clamp(wind[i].Length)) * warmRecycling * ClimateCalibration.Weight(recipe, 9);
            }
            // Fixed alternating sweeps avoid tolerance/platform-dependent stop rules.
            // At convergence: ocean E + land return + in = out + rain.
            // Starting dry ensures unconnected source-free components stay dry.
            for (int pass = 0; pass < 320; pass++)
            {
                token.ThrowIfCancellationRequested();
                for (int index = 0; index < n; index++)
                {
                    int i = (pass & 1) == 0 ? index : n - 1 - index;
                    double source = evaporation[i] * capacity[i];
                    var neighbors = s.Cells[i].Neighbors;
                    for (int k = 0; k < neighbors.Length; k++) source += incoming[i][k] * column[neighbors[k]];
                    double transportSource = source;
                    double denominator = outgoing[i] + evaporation[i] + retained[i] * basicLoss[i];
                    double q = source / Math.Max(1e-12, denominator);
                    if (q > threshold[i])
                    {
                        double rate = retained[i] * convection[i];
                        source += rate * threshold[i]; denominator += rate;
                        q = source / Math.Max(1e-12, denominator);
                    }
                    if (q > capacity[i])
                    {
                        double rate = retained[i] * 8;
                        q = (source + rate * capacity[i]) / (denominator + rate);
                    }
                    double rain = (basicLoss[i] + convection[i]) * q + 8 * Math.Max(0, q - capacity[i]);
                    if ((1 - retained[i]) * rain > landExchange[i] * Math.Max(0, capacity[i] - q))
                    {
                        // Exact other branch of min(available water, bulk evaporation).
                        // Below saturation its source is k*(capacity-q); above saturation
                        // land evaporation is zero, not negative or an unbounded fraction.
                        double loss = outgoing[i] + evaporation[i] + basicLoss[i] + convection[i];
                        q = (transportSource + landExchange[i] * capacity[i]) / Math.Max(1e-12, loss + landExchange[i]);
                        if (q > capacity[i]) q = (transportSource + 8 * capacity[i]) / (loss + 8);
                    }
                    column[i] = D.Q(Math.Max(0, q), 10000000);
                }
            }
            double absoluteResidual = 0, positiveEvaporation = 0;
            for (int i = 0; i < n; i++)
            {
                double q = column[i];
                rainMmDay[i] = D.Q(basicLoss[i] * q + convection[i] * Math.Max(0, q - threshold[i]) +
                    8 * Math.Max(0, q - capacity[i]), 1000000);
                humidity[i] = D.Q(D.Clamp(q / capacity[i]), 1000000);
                shadow[i] = D.Q(D.Clamp((1 - humidity[i]) * descent[i] / (.1 + descent[i])), 1000000);
                double evaporationMmDay = evaporation[i] * (capacity[i] - q);
                double landReturn = Math.Min((1 - retained[i]) * rainMmDay[i], landExchange[i] * Math.Max(0, capacity[i] - q));
                double residualMmDay = evaporationMmDay + landReturn - outgoing[i] * q - rainMmDay[i];
                for (int k = 0; k < incoming[i].Length; k++)
                    residualMmDay += incoming[i][k] * column[s.Cells[i].Neighbors[k]];
                absoluteResidual += Math.Abs(residualMmDay) * area[i];
                positiveEvaporation += Math.Max(0, evaporationMmDay) * area[i];
            }
            // Relative L1 local steady-budget residual, not merely the global
            // signed imbalance (which can conceal compensating local errors).
            return D.Q(absoluteResidual / Math.Max(1, positiveEvaporation));
        }
    }
}
