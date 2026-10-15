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
            var elongation = new double[count]; var fabricSeeds = new int[count];
            double fabricFrequency = Math.Max(1.5, D.Sqrt(count) * .42);
            var schedule = new Heap();
            for (int p = 0; p < count; p++)
            {
                var random = new Rng(seed, "plate-growth", p); weights[p] = .3 + 1.7 * random.Unit(); axes[p] = V.Tangent(s.Cells[seeds[p]].Position, random.Vector());
                var fabric = new Rng(seed, "plate-fabric", p);
                elongation[p] = .8 + 3.2 * fabric.Unit(); fabricSeeds[p] = unchecked((int)fabric.Next());
                fronts[p] = new Heap(); labels[seeds[p]] = p; sizes[p] = 1;
            }
            void Expand(int p, int cell, long distance)
            {
                var at = s.Cells[cell];
                var direction = V.Transport(axes[p], s.Cells[seeds[p]].Position, at.Position);
                // A linear directional bias merely displaces a round front.
                // Axial resistance stretches it; coherent bends/barriers create
                // lobes and embayments at plate scale rather than cell-scale fuzz.
                double bend = 1.6 * SphereNoise.Sample(at.Position, fabricSeeds[p], fabricFrequency);
                direction = (direction + V.Cross(at.Position, direction) * bend).Unit;
                double resistance = D.Clamp(1 + 1.2 * SphereNoise.Sample(at.Position, fabricSeeds[p] ^ 19331, fabricFrequency * 1.6)
                    + .35 * SphereNoise.Sample(at.Position, fabricSeeds[p] ^ 43717, fabricFrequency * 3.5), .4, 2);
                for (int k = 0; k < at.Neighbors.Length; k++)
                {
                    int next = at.Neighbors[k]; if (labels[next] >= 0) continue;
                    double aligned = V.Dot(direction, V.Tangent(at.Position, s.Cells[next].Position));
                    double rough = Rng.Value(seed, "plate-frontier", unchecked(cell * 73856093 ^ next * 19349663 ^ p));
                    double crossing = .65 + elongation[p] * (1 - aligned * aligned) - .16 * aligned;
                    long entry = Math.Max(1, D.M(at.DistanceM[k] * (crossing * resistance + .18 * rough)));
                    // Retain the strongest encountered barrier, plus a smaller
                    // positive travel cost. Pure summed distance inflates circles
                    // around seeds; barrier fronts follow lobed weak zones while
                    // travel still bounds long corridors. All claims remain adjacent.
                    long cost = Math.Max(distance, entry * 6) + Math.Max(1, entry * 22 / 100);
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
            groups.Add(new ParentPlate { Id = id, SeedMicroplate = seed.Id, AreaM2 = area, TargetAreaM2 = area, Microplates = members.OrderBy(i => i).ToArray(), Motion = motion, ContinentalFraction = members.Sum(i => plates[i].ContinentId >= 0 ? plates[i].AreaM2 : 0) / (double)area });
            }
            int enclosedMerges = RepairEnclosedGroups(w, groups);
            w.Parents = groups.ToArray(); w.Metric("superplates", groups.Count); w.Metric("enclosed_superplate_merges", enclosedMerges);
        }

        // Any group completely surrounded by one other group is an artificial
        // enclave at this hierarchy level. Merge it into the surrounding group
        // regardless of crust type while preserving all microplates.
        private static int RepairEnclosedGroups(World w, List<ParentPlate> groups)
        {
            var plates = w.Microplates;
            int merges = 0;
            bool changed;
            do
            {
                changed = false;
                for (int source = 0; source < groups.Count; source++)
                {
                    var members = new HashSet<int>(groups[source].Microplates); var neighbors = new HashSet<int>();
                    foreach (int p in members) foreach (int n in plates[p].Neighbors)
                        if (plates[n].ParentAffinityId != source) neighbors.Add(plates[n].ParentAffinityId);
                    if (neighbors.Count != 1) continue;
                    int target = neighbors.First(); if (target < 0 || target >= groups.Count) continue;
                    bool targetLand = groups[target].ContinentalFraction >= .5;
                    int targetContinent = targetLand ? LargestContinentId(groups[target], plates) : -1;
                    foreach (int p in groups[source].Microplates)
                    {
                        plates[p].ContinentId = targetContinent;
                        foreach (int cell in plates[p].Cells) w.Continent[cell] = targetContinent;
                    }
                    var merged = groups[target].Microplates.Concat(groups[source].Microplates).OrderBy(i => i).ToArray();
                    groups[target].Microplates = merged; groups[target].AreaM2 += groups[source].AreaM2; groups[target].TargetAreaM2 = groups[target].AreaM2;
                    groups[target].ContinentalFraction = merged.Sum(i => plates[i].ContinentId >= 0 ? plates[i].AreaM2 : 0) / (double)groups[target].AreaM2;
                    groups.RemoveAt(source);
                    for (int id = 0; id < groups.Count; id++)
                    {
                        groups[id].Id = id;
                        foreach (int p in groups[id].Microplates) plates[p].ParentAffinityId = id;
                    }
                    merges++; changed = true; break;
                }
            } while (changed);
            return merges;
        }

        private static int LargestContinentId(ParentPlate group, Microplate[] plates)
        {
            return group.Microplates.Where(i => plates[i].ContinentId >= 0).GroupBy(i => plates[i].ContinentId)
                .OrderByDescending(g => g.Sum(i => plates[i].AreaM2)).ThenBy(g => g.Key).Select(g => g.Key).FirstOrDefault();
        }
    }
}
