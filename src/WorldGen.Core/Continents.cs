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
            var r = w.Recipe; var plates = w.Microplates; long total = plates.Sum(p => p.AreaM2);
            // LandFraction is the user-facing final coverage target. The previous
            // generator mapped it to too much final land, so invert the measured
            // sweep fit before allocating whole continental plates.
            double structuralFraction = D.Clamp((r.LandFraction - .0589175484) / 1.0526993813);
            double target = total * structuralFraction;
            int count = structuralFraction <= 0 ? 0 : Math.Min(r.ContinentCount, Math.Max(1, D.M(plates.Length * structuralFraction)));
            var seeds = count == 0 ? Array.Empty<int>() : Graph.SeparatedSeeds(plates.Select(p => p.Centroid).ToArray(), count, r.Seed, "continent-seeds");
            var areas = new double[count]; var weights = new double[count]; double weightSum = 0; long land = 0;
            for (int g = 0; g < count; g++) { double v = Rng.Value(r.Seed, "continent-size", g); weights[g] = (1 - r.ContinentVariation) + r.ContinentVariation * (.15 + 3 * v * v); weightSum += weights[g]; }
            for (int g = 0; g < count; g++) { var p = plates[seeds[g]]; p.ContinentId = g; areas[g] = p.AreaM2; land += p.AreaM2; }
            while (land < target)
            {
                token.ThrowIfCancellationRequested(); long best = long.MinValue; int winner = -1, identity = -1;
                foreach (var p in plates) if (p.ContinentId < 0)
                {
                    foreach (int g in p.Neighbors.Select(i => plates[i].ContinentId).Where(i => i >= 0).Distinct().OrderBy(i => i))
                    {
                        double deficit = 1 - areas[g] / Math.Max(1, target * weights[g] / weightSum);
                        int other = p.Neighbors.Count(i => plates[i].ContinentId >= 0 && plates[i].ContinentId != g);
                        int contact = p.Neighbors.Count(i => plates[i].ContinentId == g);
                        double score = 3 * deficit + .35 * contact - 4 * other + .6 * Rng.Value(r.Seed, "continent-frontier", p.Id * 11 + g);
                        long key = (long)(score * 1e6);
                        if (key > best) { best = key; winner = p.Id; identity = g; }
                    }
                }
                if (winner < 0) break;
                // Whole plates are indivisible; retain whichever side of the target is closer.
                if (land > 0 && target < total && Math.Abs(land + plates[winner].AreaM2 - target) > Math.Abs(land - target)) break;
                plates[winner].ContinentId = identity; areas[identity] += plates[winner].AreaM2; land += plates[winner].AreaM2;
            }
            foreach (var p in plates) p.DensityKgM3 = (p.ContinentId >= 0 ? 2400 : 3000) + D.M(500 * Rng.Value(r.Seed, "plate-density", p.Id));
            w.Continent = w.CellMicroplate.Select(i => plates[i].ContinentId).ToArray();
            w.Metric("continent_identity_count", count); w.Metric("continental_crust_fraction", land / (double)total);
        }
    }
}
