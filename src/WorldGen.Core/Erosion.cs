using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public static class Erosion
    {
        public static void Apply(World w, CancellationToken token)
        {
            var r = w.Recipe; var s = w.Surface; int n = s.Cells.Length; var h = w.ElevationM; var glacier = new bool[n];
            for (int iteration = 0; iteration < 5; iteration++)
            {
                token.ThrowIfCancellationRequested(); var delta = new double[n]; var receiver = Enumerable.Repeat(-1, n).ToArray(); var area = s.Cells.Select(c => (double)c.AreaM2).ToArray();
                for (int i = 0; i < n; i++)
                {
                    double best = 0; var c = s.Cells[i]; for (int k = 0; k < c.Neighbors.Length; k++) { int j = c.Neighbors[k]; double slope = (h[i] - h[j]) / (double)c.DistanceM[k]; if (slope > best) { best = slope; receiver[i] = j; } }
                }
                foreach (int i in Enumerable.Range(0, n).OrderByDescending(i => h[i]).ThenBy(i => i)) if (receiver[i] >= 0) area[receiver[i]] += area[i];
                for (int i = 0; i < n; i++)
                {
                    if (w.ProtectedInlandSea[i]) continue; int j = receiver[i]; if (j < 0) continue; double drop = h[i] - h[j];
                    if (h[i] > 0)
                    { double cut = Math.Min(drop * .1, r.Hydraulic * 18 * D.Sqrt(area[i] / 1e10)); delta[i] -= cut; if (!w.ProtectedInlandSea[j]) delta[j] += cut * .3 * s.Cells[i].AreaM2 / s.Cells[j].AreaM2; }
                    double talus = Math.Max(0, drop - 550); double transfer = talus * r.Thermal * .05 * (1 - .65 * r.RidgePreservation); delta[i] -= transfer; if (!w.ProtectedInlandSea[j]) delta[j] += transfer * s.Cells[i].AreaM2 / s.Cells[j].AreaM2;
                    double ice = D.Clamp((Math.Abs(s.Cells[i].Position.Z) - .5) * 2) + D.Clamp((h[i] - 2400) / 3000.0);
                    if (ice > 0 && h[i] > -300 && area[i] > s.Cells[i].AreaM2 * 1.5)
                    { double cut = r.Glacial * ice * (20 + Math.Min(130, drop * .15)); delta[i] -= cut; glacier[i] = cut > 10; }
                }
                for (int i = 0; i < n; i++) h[i] += D.M(delta[i]);
            }
            var feats = w.Features.ToList(); var labels = Graph.Components(s, i => glacier[i]);
            foreach (int id in labels.Where(i => i >= 0).Distinct().OrderBy(i => i))
            { var cells = Enumerable.Range(0, n).Where(i => labels[i] == id).ToArray(); feats.Add(new Feature { Id = feats.Count, Name = "Glacial basin " + id, Kind = FeatureKind.GlacialBasin, Center = s.Cells[cells[0]].Position, Cells = cells }); }
            w.Features = feats.ToArray(); w.Landmass = Graph.Components(s, i => h[i] >= 0);
            w.Metric("erosion_mean_absolute_delta", Enumerable.Range(0, n).Average(i => Math.Abs(h[i] - w.PreErosionM[i])), "m");
        }
    }
}
