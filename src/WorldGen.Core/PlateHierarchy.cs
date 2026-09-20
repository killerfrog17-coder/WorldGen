using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public static class PlateHierarchy
    {
        public static void Microplates(World w, CancellationToken token)
        {
            var s = w.Surface; int n = s.Cells.Length, count = w.Recipe.MicroplateCount, seed = w.Recipe.Seed;
            int[] seeds = Graph.SeparatedSeeds(s.Cells.Select(c => c.Position).ToArray(), count, seed, "plate-seeds");
            var labels = Enumerable.Repeat(-1, n).ToArray(); var fronts = new Heap[count]; var sizes = new int[count]; var weights = new double[count]; var axes = new V[count];
            var schedule = new Heap();
            for (int p = 0; p < count; p++)
            {
                var random = new Rng(seed, "plate-growth", p); weights[p] = .3 + 1.7 * random.Unit(); axes[p] = V.Tangent(s.Cells[seeds[p]].Position, random.Vector());
                fronts[p] = new Heap(); labels[seeds[p]] = p; sizes[p] = 1;
            }
            void Expand(int p, int cell, long distance)
            {
                var at = s.Cells[cell];
                var direction = V.Transport(axes[p], s.Cells[seeds[p]].Position, at.Position);
                for (int k = 0; k < at.Neighbors.Length; k++)
                {
                    int next = at.Neighbors[k]; if (labels[next] >= 0) continue;
                    double aligned = V.Dot(direction, V.Tangent(at.Position, s.Cells[next].Position));
                    double rough = Rng.Value(seed, "plate-frontier", unchecked(cell * 73856093 ^ next * 19349663 ^ p));
                    long cost = distance + (long)(at.DistanceM[k] * (1.3 - .45 * aligned + .8 * rough));
                    fronts[p].Push(cost, next);
                }
            }
            for (int p = 0; p < count; p++) { Expand(p, seeds[p], 0); schedule.Push((long)(1e6 / weights[p]), p); }
            int remaining = n - count;
            while (schedule.Count > 0)
            {
                if ((remaining & 255) == 0) token.ThrowIfCancellationRequested();
                int p = schedule.Pop().Id; var front = fronts[p];
                while (front.Count > 0)
                {
                    var candidate = front.Pop(); int cell = candidate.Id; if (labels[cell] >= 0) continue;
                    labels[cell] = p; sizes[p]++; remaining--; Expand(p, cell, candidate.Key); break;
                }
                if (front.Count > 0) schedule.Push((long)(sizes[p] * 1e6 / weights[p]), p);
            }
            if (remaining != 0) throw new InvalidOperationException("Plate growth left unclassified cells.");
            w.CellMicroplate = labels; w.Microplates = new Microplate[count];
            var members = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
            for (int i = 0; i < n; i++) members[labels[i]].Add(i);
            for (int p = 0; p < count; p++)
            {
                long area = 0; V center = new V();
                foreach (int i in members[p]) { area += s.Cells[i].AreaM2; center += s.Cells[i].Position * (s.Cells[i].AreaM2 / 1e12); }
                var random = new Rng(seed, "plate-rotation", p); var motion = random.Vector() * (.4 + .8 * random.Unit());
                w.Microplates[p] = new Microplate { Id = p, SeedCell = seeds[p], Cells = members[p].ToArray(), AreaM2 = area, Centroid = center.Unit, OriginalMotion = motion, Motion = motion, ActiveActorId = p };
            }
            var adjacency = Enumerable.Range(0, count).Select(_ => new SortedDictionary<int, long>()).ToArray();
            var boundaries = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
            foreach (var edge in s.Edges)
            {
                int a = labels[edge.A], b = labels[edge.B]; if (a == b) continue;
                if (!adjacency[a].ContainsKey(b)) { adjacency[a][b] = 0; adjacency[b][a] = 0; }
                adjacency[a][b] += edge.LengthM; adjacency[b][a] += edge.LengthM; boundaries[a].Add(edge.Id); boundaries[b].Add(edge.Id);
            }
            for (int p = 0; p < count; p++)
            {
                w.Microplates[p].Neighbors = adjacency[p].Keys.ToArray(); w.Microplates[p].SharedBoundaryM = adjacency[p].Values.ToArray(); w.Microplates[p].BoundaryEdges = boundaries[p].ToArray();
            }
            w.Metric("minimum_plate_cells", sizes.Min()); w.Metric("plates", count);
        }

        // Connected groups of like crust supplement, rather than replace, fine plate motion.
        public static void Parents(World w, CancellationToken token)
        {
            var plates = w.Microplates; var groups = new List<ParentPlate>(); int targetMembers = Math.Max(1, D.M(D.Sqrt(plates.Length) / 2));
            foreach (var seed in plates.OrderByDescending(p => p.AreaM2).ThenBy(p => p.Id))
            {
                token.ThrowIfCancellationRequested(); if (seed.ParentAffinityId >= 0) continue;
                int id = groups.Count; var members = new List<int> { seed.Id }; seed.ParentAffinityId = id;
                while (members.Count < targetMembers)
                {
                    var candidates = new SortedDictionary<int, long>();
                    foreach (int p in members) for (int k = 0; k < plates[p].Neighbors.Length; k++)
                    {
                        int next = plates[p].Neighbors[k]; var other = plates[next];
                        if (other.ParentAffinityId >= 0 || (other.ContinentId >= 0) != (seed.ContinentId >= 0)) continue;
                        if (!candidates.ContainsKey(next)) candidates[next] = 0;
                        candidates[next] += plates[p].SharedBoundaryM[k];
                    }
                    if (candidates.Count == 0) break;
                    int winner = candidates.OrderByDescending(p => p.Value).ThenBy(p => p.Key).First().Key; plates[winner].ParentAffinityId = id; members.Add(winner);
                }
                long area = members.Sum(i => plates[i].AreaM2); V motion = new V();
                foreach (int i in members) motion += plates[i].Motion * (plates[i].AreaM2 / (double)area);
                groups.Add(new ParentPlate { Id = id, SeedMicroplate = seed.Id, AreaM2 = area, TargetAreaM2 = area, Microplates = members.OrderBy(i => i).ToArray(), Motion = motion, ContinentalFraction = seed.ContinentId < 0 ? 0 : 1 });
            }
            w.Parents = groups.ToArray(); w.Metric("superplates", groups.Count);
        }
    }
}
