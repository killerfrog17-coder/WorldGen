using System;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public static class Continents
    {
        public static void Build(World w, CancellationToken token)
        {
            if(w.Recipe.LandShape!=null){MapShapes.AssignPlates(w,token);return;}
            var r = w.Recipe; var plates = w.Microplates;
            long total = plates.Sum(p => p.AreaM2);
            // Original whole-plate land budget and growth; only assignments are searched.
            double fraction = D.Clamp((r.LandFraction - .0589175484) / 1.0526993813);
            double target = total * fraction;
            int count = fraction <= 0 ? 0 : Math.Min(r.ContinentCount, Math.Max(1, D.M(plates.Length * fraction)));
            var scores = new long[r.Interconnectedness]; int[] winner = null; long best = long.MinValue; int selected = 0;
            int stride = unchecked((int)(new Rng(r.Seed, "continent-candidate").Next() | 1u));
            for (int iteration = 0; iteration < r.Interconnectedness; iteration++)
            {
                token.ThrowIfCancellationRequested();
                int seed = unchecked(r.Seed + iteration * stride); // distinct seeds, stable prefix
                var labels = Assign(plates, r, count, total, target, seed, token);
                scores[iteration] = Score(w, labels, count, target, total, token);
                if (scores[iteration] > best) { best = scores[iteration]; winner = labels; selected = iteration; }
            }
            long land = 0;
            foreach (var p in plates)
            {
                p.ContinentId = winner[p.Id]; if (p.ContinentId >= 0) land += p.AreaM2;
                p.DensityKgM3 = (p.ContinentId >= 0 ? 2400 : 3000) + D.M(500 * Rng.Value(r.Seed, "plate-density", p.Id));
            }
            w.Continent = w.CellMicroplate.Select(i => plates[i].ContinentId).ToArray();
            w.ContinentalCandidate = selected; w.ContinentalScore = best; w.ContinentalCandidateScores = scores;
            w.Metric("continent_identity_count", count); w.Metric("continental_crust_fraction", land / (double)total);
            w.Metric("continental_candidates_evaluated", scores.Length); w.Metric("continental_selected_candidate", selected);
            w.Metric("continental_interconnectedness_score", best);
            if (land < target - total * .02) w.Warnings.Add("Whole-plate allocation limited the requested land budget.");
        }

        private static int[] Assign(Microplate[] plates, Recipe r, int count, long total, double target, int seed, CancellationToken token)
        {
            var labels = Enumerable.Repeat(-1, plates.Length).ToArray();
            // Preserve the original default stream and separated seed ordering.
            var seeds = count == 0 ? Array.Empty<int>() : Graph.SeparatedSeeds(plates.Select(p => p.Centroid).ToArray(), count, seed, "continent-complex-seeds");
            var areas = new double[count]; var weights = new double[count]; double sum = 0; long land = 0;
            for (int g = 0; g < count; g++) { double v = Rng.Value(seed, "continent-size", g); weights[g] = 1 - r.ContinentVariation + r.ContinentVariation * (.15 + 3 * v * v); sum += weights[g]; }
            for (int g = 0; g < count; g++) { var p = plates[seeds[g]]; labels[p.Id] = g; areas[g] = p.AreaM2; land += p.AreaM2; }
            while (land < target)
            {
                token.ThrowIfCancellationRequested(); long best = long.MinValue; int winner = -1, identity = -1;
                foreach (var p in plates) if (labels[p.Id] < 0)
                foreach (int g in p.Neighbors.Select(i => labels[i]).Where(i => i >= 0).Distinct().OrderBy(i => i))
                {
                    double deficit = 1 - areas[g] / Math.Max(1, target * weights[g] / sum);
                    int contact = p.Neighbors.Count(i => labels[i] == g), other = p.Neighbors.Count(i => labels[i] >= 0 && labels[i] != g);
                    // Keep the original operation order for candidate-zero reproduction.
                    double score = 3 * deficit + .35 * contact + 2.25 * contact - 8 * other + .6 * Rng.Value(seed, "continent-frontier", p.Id * 11 + g);
                    long key = (long)(score * 1e6);
                    if (key > best) { best = key; winner = p.Id; identity = g; }
                }
                if (winner < 0) break;
                if (land > 0 && target < total && Math.Abs(land + plates[winner].AreaM2 - target) > Math.Abs(land - target)) break;
                labels[winner] = identity; areas[identity] += plates[winner].AreaM2; land += plates[winner].AreaM2;
            }
            return labels;
        }

        // Score actual coarse shoreline gaps without changing any geometry.
        private static long Score(World w, int[] labels, int count, double target, long total, CancellationToken token)
        {
            var plates = w.Microplates;
            var areas = new long[count]; var contact = new long[count, count]; long land = 0;
            foreach (var p in plates) if (labels[p.Id] >= 0)
            {
                int g = labels[p.Id]; areas[g] += p.AreaM2; land += p.AreaM2;
                for (int k = 0; k < p.Neighbors.Length; k++)
                { int other = labels[p.Neighbors[k]]; if (other >= 0 && other != g) contact[g, other] += p.SharedBoundaryM[k]; }
            }
            double score = -10000 * Math.Abs(land - target) / total;
            if (count < 2) return (long)Math.Round(score * 1000, MidpointRounding.AwayFromZero);
            var cells = w.CellMicroplate.Select(p => labels[p]).ToArray();
            var distance = Graph.Distances(w.Surface, Enumerable.Range(0, cells.Length).Where(i => cells[i] >= 0), out var nearest, token);
            var gaps = new int[count, count];
            for (int g = 0; g < count; g++) for (int h = 0; h < count; h++) gaps[g, h] = int.MaxValue;
            foreach (var cell in w.Surface.Cells)
            {
                if ((cell.Id & 1023) == 0) token.ThrowIfCancellationRequested();
                int g = cells[nearest[cell.Id]];
                for (int k = 0; k < cell.Neighbors.Length; k++)
                {
                    int j = cell.Neighbors[k]; if (j <= cell.Id) continue;
                    int h = cells[nearest[j]]; if (g == h) continue;
                    int gap = distance[cell.Id] + cell.DistanceM[k] + distance[j];
                    if (gap < gaps[g, h]) gaps[g, h] = gaps[h, g] = gap;
                }
            }
            for (int g = 0; g < count; g++)
            {
                token.ThrowIfCancellationRequested();
                for (int h = g + 1; h < count; h++)
                {
                    if (contact[g, h] > 0)
                    {
                        double width = contact[g, h] / Math.Max(1, D.Sqrt(Math.Min(areas[g], areas[h])));
                        score += width <= .35 ? 1000 * (1 - width) : -5000 * Math.Min(3, width - .35);
                    }
                    else
                    {
                        score += 600 * D.Clamp(1 - gaps[g, h] / 1500000.0);
                    }
                }
            }
            return (long)Math.Round(score * 1000, MidpointRounding.AwayFromZero);
        }
    }
}
