using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public static class Continents
    {
        public static void Build(World w, CancellationToken token)
        {
            var r = w.Recipe; var m = w.Microplates; int count = r.ContinentCount;
            var seeds = Graph.SeparatedSeeds(m.Select(p => p.Centroid).ToArray(), count, r.Seed, "continent-seeds");
            // Move some identity seeds toward the first seed while keeping distinct regions.
            if (r.Connectedness > .5)
                for (int k = 1; k < count; k++)
                { var candidates = Enumerable.Range(0, m.Length).Where(i => !seeds.Take(k).Contains(i)).OrderBy(i => D.M((1 - V.Dot(m[i].Centroid, m[seeds[0]].Centroid)) * 1e6)).ThenBy(i => i).ToArray(); int index = Math.Min(candidates.Length - 1, D.M((1 - r.Connectedness) * candidates.Length * .5) + k); seeds[k] = candidates[index]; }
            var areas = new long[count]; var targets = new double[count]; long total = m.Sum(p => p.AreaM2); double sum = 0;
            for (int k = 0; k < count; k++) { targets[k] = 1 + r.ContinentVariation * (Rng.Value(r.Seed, "continent-size", k) * 3 - 1); sum += targets[k]; m[seeds[k]].ContinentId = k; areas[k] = m[seeds[k]].AreaM2; }
            for (int k = 0; k < count; k++) targets[k] = targets[k] / sum * total * r.LandFraction;
            long land = areas.Sum();
            while (land < total * r.LandFraction)
            {
                token.ThrowIfCancellationRequested(); long best = long.MinValue; int chosen = -1, group = -1;
                foreach (var p in m) if (p.ContinentId < 0)
                {
                    foreach (int g in p.Neighbors.Select(n => m[n].ContinentId).Where(g => g >= 0).Distinct().OrderBy(g => g))
                    {
                        int other = p.Neighbors.Count(n => m[n].ContinentId >= 0 && m[n].ContinentId != g);
                        double score = 3 * (1 - areas[g] / targets[g]) - (1 - r.Connectedness) * other * 5 + (r.Connectedness * 2 - 1) * p.Neighbors.Count(n => m[n].ContinentId >= 0) + .5 * Rng.Value(r.Seed, "continent-growth", p.Id * 13 + g);
                        long q = (long)(score * 1e6); if (q > best) { best = q; chosen = p.Id; group = g; }
                    }
                }
                if (chosen < 0) break; m[chosen].ContinentId = group; areas[group] += m[chosen].AreaM2; land += m[chosen].AreaM2;
            }
            foreach (var p in m) p.DensityKgM3 = (p.ContinentId >= 0 ? 2650 : 3150) + (int)(Rng.Value(r.Seed, "density", p.Id) * 250);
            foreach (var p in w.Parents) p.ContinentalFraction = D.Q(p.Microplates.Where(i => m[i].ContinentId >= 0).Sum(i => m[i].AreaM2) / (double)p.AreaM2, 1e6);
            w.Continent = w.CellMicroplate.Select(i => m[i].ContinentId).ToArray();
            w.Metric("continent_identity_count", m.Where(p => p.ContinentId >= 0).Select(p => p.ContinentId).Distinct().Count());
            w.Metric("continental_crust_fraction", land / (double)total);
        }
    }
}
