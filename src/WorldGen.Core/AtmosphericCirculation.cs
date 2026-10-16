using System;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>
    /// Smooth land/ocean pressure contrasts perturb the zonal circulation without
    /// stamping climates or referring to geographical regions. A dual-corner
    /// streamfunction makes basin transport exactly nondivergent on the mesh.
    /// </summary>
    internal static class AtmosphericCirculation
    {
        internal static V[] BasinWind(Surface s, double[] oceanFraction, double season,
            CancellationToken token, out double[] anomalyPressure, out double[] streamFunction,double[] thermalPressureField=null)
        {
            int n = s.Cells.Length;
            var smooth = new double[n]; var next = new double[n]; var latitude = new double[n];
            var east = new V[n]; var north = new V[n]; var diffusion = new double[n];
            for (int i = 0; i < n; i++)
            {
                var cell = s.Cells[i]; var p = cell.Position;
                smooth[i] = D.Clamp(oceanFraction[i]); latitude[i] = D.Pi / 2 - D.Angle(p, new V(0, 0, 1));
                east[i] = new V(-p.Y, p.X, 0).Unit;
                if (east[i].Length < .1) east[i] = new V(0, 1, 0);
                north[i] = V.Cross(p, east[i]).Unit;
                double meanDistance = 0;
                foreach (int distance in cell.DistanceM) meanDistance += distance;
                meanDistance /= Math.Max(1, cell.DistanceM.Length);
                double ratio = 160000 / Math.Max(1, meanDistance);
                diffusion[i] = .5 * D.Clamp(ratio * ratio);
            }
            // Fixed stable Jacobi diffusion has approximately an 1800 km support on
            // the bounded atmospheric mesh. The physical-distance factor prevents
            // coarser preview meshes acquiring a proportionally wider basin forcing.
            for (int pass = 0; pass < 240; pass++)
            {
                token.ThrowIfCancellationRequested();
                for (int i = 0; i < n; i++)
                {
                    var cell = s.Cells[i]; double weighted = 0, weights = 0;
                    for (int k = 0; k < cell.Neighbors.Length; k++)
                    {
                        double weight = 160000.0 / Math.Max(1, cell.DistanceM[k]);
                        weighted += weight * smooth[cell.Neighbors[k]]; weights += weight;
                    }
                    next[i] = D.Q(weights > 0 ? smooth[i] + diffusion[i] * (weighted / weights - smooth[i]) : smooth[i]);
                }
                var swap = smooth; smooth = next; next = swap;
            }
            const int bins = 36;
            var means = new double[bins]; var areas = new double[bins];
            for (int i = 0; i < n; i++)
            {
                int bin = Math.Max(0, Math.Min(bins - 1, (int)((latitude[i] + D.Pi / 2) * bins / D.Pi)));
                double area = Math.Max(1, s.Cells[i].AreaM2);
                means[bin] += smooth[i] * area; areas[bin] += area;
            }
            for (int b = 0; b < bins; b++) if (areas[b] > 0) means[b] /= areas[b];
            // Graph-only fixtures may not cover every latitude bin.
            for (int b = 0; b < bins; b++) if (areas[b] == 0)
            {
                int closest = -1;
                for (int j = 0; j < bins; j++) if (areas[j] > 0 && (closest < 0 || Math.Abs(j - b) < Math.Abs(closest - b))) closest = j;
                means[b] = closest < 0 ? 0 : means[closest];
            }
            anomalyPressure = new double[n]; streamFunction = new double[s.Corners.Length];
            var psi = new double[n];
            if (s.Edges.Length == 0 || s.Triangles.Length == 0) return new V[n];
            for (int i = 0; i < n; i++)
            {
                // Interpolation prevents five-degree stair-steps in the forcing.
                double bin = D.Clamp((latitude[i] + D.Pi / 2) * bins / D.Pi - .5, 0, bins - 1);
                int lower = (int)bin, upper = Math.Min(bins - 1, lower + 1);
                double zonal = means[lower] + (means[upper] - means[lower]) * (bin - lower);
                double contrast = smooth[i] - zonal;
                double high = D.Kernel(Math.Abs(Math.Abs(latitude[i] - season * .10) - .5), .4);
                double degrees = Math.Abs(latitude[i]) * 180 / D.Pi;
                double equatorialTaper = 1 - D.Kernel(Math.Max(0, degrees - 10), 10);
                double polarTaper = D.Kernel(Math.Max(0, degrees - 60), 10);
                double sine = D.Sin(latitude[i]);
                double thermal = season * D.Clamp(2 * sine, -1, 1);
                double gyrePressure = contrast * 900 * high * equatorialTaper * polarTaper;
                double thermalPressure = contrast * 1100 * thermal * polarTaper;
                if(thermalPressureField!=null)thermalPressureField[i]=D.Q(thermalPressure,1000);
                anomalyPressure[i] = D.Q(gyrePressure + thermalPressure, 1000);
                double coriolis = .000146 * sine;
                double boundedF = Math.Max(.000025, Math.Abs(coriolis)) * (latitude[i] < 0 ? -1 : 1);
                // The thermal pressure is proportional to sin(latitude), as is f.
                // Their ratio has a finite, same-sign equatorial limit. Evaluating
                // that limit directly permits cross-equatorial monsoon transport
                // instead of deleting it with the subtropical gyre's latitude taper.
                double thermalRatio = Math.Abs(sine) <= .5 ? 2 : 1 / Math.Abs(sine);
                double thermalPsi = contrast * 1100 * season * thermalRatio * polarTaper / (1.2 * .000146);
                psi[i] = D.Q(gyrePressure / (1.2 * boundedF) + thermalPsi, 1000);
            }
            var result = new V[n];
            for (int i = 0; i < n; i++)
            {
                if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                var cell = s.Cells[i]; double xx = 0, xy = 0, yy = 0, gx = 0, gy = 0;
                for (int k = 0; k < cell.Neighbors.Length; k++)
                {
                    int j = cell.Neighbors[k]; V direction = V.Tangent(cell.Position, s.Cells[j].Position);
                    double x = V.Dot(direction, east[i]), y = V.Dot(direction, north[i]);
                    double derivative = (psi[j] - psi[i]) / Math.Max(1, cell.DistanceM[k]);
                    xx += x * x; xy += x * y; yy += y * y; gx += x * derivative; gy += y * derivative;
                }
                // Small regularization also handles one-dimensional transect fixtures.
                xx += .000001; yy += .000001; double determinant = xx * yy - xy * xy;
                double gradientEast = (gx * yy - gy * xy) / determinant;
                double gradientNorth = (gy * xx - gx * xy) / determinant;
                result[i] = east[i] * (-gradientNorth / 5) + north[i] * (gradientEast / 5);
            }
            // A GLOBAL limit preserves closed streamlines. Independently clipping
            // edge transports would reintroduce the very divergence we removed.
            double maximumSpeed = 0;
            for (int i = 0; i < n; i++) maximumSpeed = Math.Max(maximumSpeed, result[i].Length * 5);
            double scale = maximumSpeed > 12 ? 12 / maximumSpeed : 1;
            for (int i = 0; i < n; i++) result[i] *= scale;
            for (int t = 0; t < s.Triangles.Length; t += 3)
                streamFunction[t / 3] = D.Q(scale * (psi[s.Triangles[t]] + psi[s.Triangles[t + 1]] + psi[s.Triangles[t + 2]]) / 3, 1000);
            return result;
        }
    }
}
