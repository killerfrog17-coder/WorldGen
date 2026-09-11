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
            var r = w.Recipe; var s = w.Surface; int n = s.Cells.Length; var seeds = Graph.SeparatedSeeds(s.Cells.Select(c => c.Position).ToArray(), r.MicroplateCount, r.Seed, "micro-seeds");
            var label = Enumerable.Repeat(-1, n).ToArray(); var cost = Enumerable.Repeat(long.MaxValue, n).ToArray(); var heap = new Heap();
            for (int p = 0; p < seeds.Length; p++) { label[seeds[p]] = p; cost[seeds[p]] = 0; heap.Push(0, seeds[p], p); }
            var done = new bool[n];
            while (heap.Count > 0)
            {
                var item = heap.Pop(); int c = item.Id; if (done[c] || item.Key != cost[c] || label[c] != item.From) continue; done[c] = true;
                if ((c & 255) == 0) token.ThrowIfCancellationRequested(); var cell = s.Cells[c];
                for (int k = 0; k < cell.Neighbors.Length; k++)
                {
                    int nb = cell.Neighbors[k]; if (done[nb]) continue;
                    double noise = Rng.Value(r.Seed, "micro-growth", unchecked(Math.Min(c, nb) * 73856093 ^ Math.Max(c, nb) * 19349663));
                    long next = item.Key + (long)(cell.DistanceM[k] * (1 + r.MicroplateIrregularity * noise * 2 + r.BoundaryRoughness * noise * noise));
                    if (next < cost[nb] || next == cost[nb] && item.From < label[nb]) { cost[nb] = next; label[nb] = item.From; heap.Push(next, nb, item.From); }
                }
            }
            w.CellMicroplate = label; w.Microplates = new Microplate[seeds.Length];
            for (int p = 0; p < seeds.Length; p++)
            {
                var cells = Enumerable.Range(0, n).Where(i => label[i] == p).ToArray(); long area = 0; V center = new V(); foreach (int c in cells) { area += s.Cells[c].AreaM2; center += s.Cells[c].Position * (s.Cells[c].AreaM2 / 1e12); }
                var rng = new Rng(r.Seed, "micro-motion", p); w.Microplates[p] = new Microplate { Id = p, SeedCell = seeds[p], Cells = cells, AreaM2 = area, Centroid = center.Unit, OriginalMotion = rng.Vector() * (.4 + rng.Unit()), ActiveActorId = p };
            }
            var adjacency = new SortedDictionary<int, long>[seeds.Length]; var boundaries = new List<int>[seeds.Length]; for (int p = 0; p < seeds.Length; p++) { adjacency[p] = new SortedDictionary<int, long>(); boundaries[p] = new List<int>(); }
            foreach (var e in s.Edges)
            { int a = label[e.A], b = label[e.B]; if (a == b) continue; if (!adjacency[a].ContainsKey(b)) { adjacency[a][b] = 0; adjacency[b][a] = 0; } adjacency[a][b] += e.LengthM; adjacency[b][a] += e.LengthM; boundaries[a].Add(e.Id); boundaries[b].Add(e.Id); }
            for (int p = 0; p < seeds.Length; p++) { w.Microplates[p].Neighbors = adjacency[p].Keys.ToArray(); w.Microplates[p].SharedBoundaryM = adjacency[p].Values.ToArray(); w.Microplates[p].BoundaryEdges = boundaries[p].ToArray(); }
            w.Metric("minimum_microplate_cells", w.Microplates.Min(p => p.Cells.Length));
            if (w.Microplates.Any(p => p.Cells.Length < 4)) throw new InvalidOperationException("Degenerate microplate: increase canonical cell count.");
        }
        public static void Parents(World w, CancellationToken token)
        {
            var r = w.Recipe; var m = w.Microplates; var seeds = Graph.SeparatedSeeds(m.Select(p => p.Centroid).ToArray(), r.ParentCount, r.Seed, "parent-seeds");
            long total = m.Sum(p => p.AreaM2); var weights = new double[seeds.Length]; double sum = 0;
            for (int p = 0; p < weights.Length; p++) { double u = Rng.Value(r.Seed, "parent-size", p); weights[p] = (1 - r.ParentInequality) + r.ParentInequality * (.08 + 8 * u * u * u * u); sum += weights[p]; }
            w.Parents = new ParentPlate[seeds.Length];
            for (int p = 0; p < seeds.Length; p++)
            { var rng = new Rng(r.Seed, "parent-motion", p); var dir = V.Tangent(m[seeds[p]].Centroid, rng.Vector()); w.Parents[p] = new ParentPlate { Id = p, SeedMicroplate = seeds[p], TargetAreaM2 = (long)(total * weights[p] / sum), AreaM2 = m[seeds[p]].AreaM2, Motion = rng.Vector() * (.3 + .7 * rng.Unit()), GrowthDirection = dir, LastDirection = dir }; m[seeds[p]].ParentAffinityId = p; }
            int remaining = m.Length - seeds.Length;
            while (remaining > 0)
            {
                token.ThrowIfCancellationRequested(); long bestScore = long.MinValue; int winner = -1, parent = -1, source = -1;
                for (int i = 0; i < m.Length; i++) if (m[i].ParentAffinityId < 0)
                {
                    var contenders = m[i].Neighbors.Select(j => m[j].ParentAffinityId).Where(p => p >= 0).Distinct().OrderBy(p => p).ToArray(); m[i].ContestDegree = Math.Max(m[i].ContestDegree, contenders.Length);
                    foreach (int p in contenders)
                    {
                        var plate = w.Parents[p]; var origin = m[plate.SeedMicroplate].Centroid; var at = m[i].Centroid;
                        var transported = V.Transport(plate.GrowthDirection, origin, at); var curved = (transported + V.Cross(at, transported) * (r.ParentCurvature * D.Angle(origin, at))).Unit;
                        int prev = m[i].Neighbors.Where(j => m[j].ParentAffinityId == p).OrderBy(j => j).First(); var step = V.Tangent(at, at - m[prev].Centroid);
                        double alignment = V.Dot(curved, step), continuity = V.Dot(V.Transport(plate.LastDirection, origin, at), step);
                        long shared = 0, perimeter = 0; for (int k = 0; k < m[i].Neighbors.Length; k++) { perimeter += m[i].SharedBoundaryM[k]; if (m[m[i].Neighbors[k]].ParentAffinityId == p) shared += m[i].SharedBoundaryM[k]; }
                        double fill = plate.AreaM2 / (double)Math.Max(1, plate.TargetAreaM2);
                        double score = 4 * (1 - fill) + r.ParentInertia * (alignment + .5 * continuity) + 1.2 * shared / Math.Max(1.0, perimeter) + r.MicroplateIrregularity * Rng.Value(r.Seed, "parent-frontier", i * 41 + p);
                        long quant = (long)(score * 1000000);
                        if (quant > bestScore) { bestScore = quant; winner = i; parent = p; source = prev; }
                    }
                }
                if (winner < 0) throw new InvalidOperationException("Disconnected microplate graph.");
                m[winner].ParentAffinityId = parent; var wp = w.Parents[parent]; wp.AreaM2 += m[winner].AreaM2;
                wp.LastDirection = V.Transport(V.Tangent(m[winner].Centroid, m[winner].Centroid - m[source].Centroid), m[winner].Centroid, m[wp.SeedMicroplate].Centroid); remaining--;
            }
            // Deterministic look-ahead eligibility: a member touches another final parent domain.
            foreach (var plate in m) if (plate.Neighbors.Any(j => m[j].ParentAffinityId != plate.ParentAffinityId)) plate.ContestDegree = Math.Max(2, plate.ContestDegree);
            double Score(Microplate p)
            { long length = 0; double contrast = 0; for (int k = 0; k < p.Neighbors.Length; k++) { var other = m[p.Neighbors[k]]; if (other.ParentAffinityId == p.ParentAffinityId) continue; length += p.SharedBoundaryM[k]; contrast += (p.OriginalMotion - other.OriginalMotion).Length; } return p.ContestDegree * 2 + length / 1e6 + contrast + Rng.Value(r.Seed, "contested", p.Id); }
            int requested = D.M(m.Length * r.PreservedFraction); var eligible = m.Where(p => p.ContestDegree >= 2 && p.Neighbors.Any(j => m[j].ParentAffinityId != p.ParentAffinityId)).OrderByDescending(p => (long)(Score(p) * 1000000)).ThenBy(p => p.Id).ToArray();
            foreach (var p in eligible.Take(requested)) p.Preserved = true;
            if (eligible.Length < requested) w.Warnings.Add("Only " + eligible.Length + " genuinely contested microplates eligible for " + requested + " requested independent actors.");
            foreach (var p in m) { p.ActiveActorId = p.Preserved ? w.Parents.Length + p.Id : p.ParentAffinityId; p.Motion = p.Preserved ? (p.OriginalMotion * .7 + w.Parents[p.ParentAffinityId].Motion * .3) : w.Parents[p.ParentAffinityId].Motion; }
            foreach (var p in w.Parents) { p.Microplates = m.Where(x => x.ParentAffinityId == p.Id).Select(x => x.Id).ToArray(); w.Metric("parent_" + p.Id + "_requested_area", p.TargetAreaM2 / 1e6, "km²"); w.Metric("parent_" + p.Id + "_achieved_area", p.AreaM2 / 1e6, "km²"); }
            w.Metric("contested_eligible", eligible.Length); w.Metric("preserved_microplates", m.Count(p => p.Preserved));
        }
    }
}
