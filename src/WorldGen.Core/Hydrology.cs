using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public static class Hydrology
    {
        // Ascending union tree records every local depression and the saddles where basins merge.
        public static Depression[] BuildHierarchy(Surface s, int[] h)
        {
            int n = h.Length; var uf = Enumerable.Repeat(-1, n).ToArray(); var node = new int[n]; var list = new List<Depression>();
            int Find(int i) { int root = i; while (uf[root] != root) root = uf[root]; while (i != root) { int next = uf[i]; uf[i] = root; i = next; } return root; }
            foreach (int i in Enumerable.Range(0, n).OrderBy(i => h[i]).ThenBy(i => i))
            {
                var roots = s.Cells[i].Neighbors.Where(j => uf[j] >= 0).Select(Find).Distinct().OrderBy(j => j).ToArray(); uf[i] = i;
                if (roots.Length == 0) { node[i] = list.Count; list.Add(new Depression { Id = list.Count, PitCell = i, SpillM = h[i] }); }
                else if (roots.Length == 1) { node[i] = node[roots[0]]; uf[roots[0]] = i; }
                else
                {
                    int id = list.Count; var children = roots.Select(j => node[j]).OrderBy(j => j).ToArray(); int pit = children.Select(c => list[c].PitCell).OrderBy(c => h[c]).ThenBy(c => c).First();
                    list.Add(new Depression { Id = id, PitCell = pit, SpillM = h[i], OutletCell = i, Children = children });
                    foreach (int child in children) { list[child].ParentId = id; list[child].SpillM = h[i]; list[child].OutletCell = i; }
                    node[i] = id; foreach (int root in roots) uf[root] = i;
                }
            }
            return list.ToArray();
        }
        public static HydrologyResult Solve(Surface s, int[] h, Recipe r, bool[] protectedSea, Feature[] features, CancellationToken token)
        {
            int n = h.Length; var result = new HydrologyResult { Receiver = Enumerable.Repeat(-1, n).ToArray(), Watershed = Enumerable.Repeat(-1, n).ToArray(), WaterBody = Enumerable.Repeat(-1, n).ToArray(), FloodLevelM = new int[n], VisitOrder = new int[n], ContributingAreaM2 = s.Cells.Select(c => c.AreaM2).ToArray(), OceanConnected = new bool[n], RiverOrder = new int[n] };
            result.Depressions = BuildHierarchy(s, h); var marine = Graph.Components(s, i => h[i] < 0 && (protectedSea == null || !protectedSea[i])); int main = Graph.LargestComponent(s, marine); var heap = new Heap(); var seen = new bool[n]; var order = new List<int>(); var rootKind = Enumerable.Repeat(-1, n).ToArray(); var waters = new List<WaterBody>();
            for (int i = 0; i < n; i++) if (main >= 0 && marine[i] == main) { result.OceanConnected[i] = true; seen[i] = true; result.FloodLevelM[i] = 0; result.Watershed[i] = i; heap.Push(0, i); rootKind[i] = 0; }
            // Endorheic reservoirs are seeded below their spill. They remain terminal drainage sinks.
            var fixedBodies = new List<(int[] cells, int level, WaterKind kind, int depression)>();
            var seaLabels = Graph.Components(s, i => protectedSea != null && protectedSea[i] && !result.OceanConnected[i]);
            foreach (int id in seaLabels.Where(i => i >= 0).Distinct().OrderBy(i => i)) fixedBodies.Add((Enumerable.Range(0, n).Where(i => seaLabels[i] == id).ToArray(), 0, WaterKind.TectonicInlandSea, -1));
            var reserved = new bool[n]; foreach (var b in fixedBodies) foreach (int c in b.cells) reserved[c] = true;
            foreach (var d in result.Depressions.Where(d => d.Children.Length == 0 && d.ParentId >= 0).OrderBy(d => d.Id))
            {
                int pit = d.PitCell; if (result.OceanConnected[pit] || reserved[pit] || d.SpillM - h[pit] < 80 || Rng.Value(r.Seed, "endorheic", pit) >= r.Endorheic) continue;
                int level = h[pit] + Math.Max(1, (d.SpillM - h[pit]) * 2 / 3); var cells = new List<int>(); var queue = new Queue<int>(); var visited = new HashSet<int> { pit }; queue.Enqueue(pit); bool invalid = false;
                while (queue.Count > 0) { int c = queue.Dequeue(); if (seen[c] || reserved[c]) { invalid = true; break; } cells.Add(c); foreach (int nb in s.Cells[c].Neighbors) if (h[nb] < level && visited.Add(nb)) queue.Enqueue(nb); }
                if (invalid || cells.Sum(i => s.Cells[i].AreaM2) < r.MinLakeAreaKm2 * 1e6) continue;
                var sorted = cells.OrderBy(i => i).ToArray(); fixedBodies.Add((sorted, level, WaterKind.EndorheicLake, d.Id)); foreach (int c in sorted) reserved[c] = true;
            }
            foreach (var b in fixedBodies)
            {
                int pit = b.cells.OrderBy(i => h[i]).ThenBy(i => i).First(); var members = new HashSet<int>(b.cells); var q = new Queue<int>(); q.Enqueue(pit); seen[pit] = true; result.Watershed[pit] = pit; result.FloodLevelM[pit] = b.level; rootKind[pit] = 1; heap.Push(b.level, pit);
                while (q.Count > 0) { int c = q.Dequeue(); foreach (int nb in s.Cells[c].Neighbors) if (members.Contains(nb) && !seen[nb]) { seen[nb] = true; result.Receiver[nb] = c; result.FloodLevelM[nb] = b.level; result.Watershed[nb] = pit; rootKind[nb] = 1; q.Enqueue(nb); heap.Push(b.level, nb); } }
                var body = new WaterBody { Id = waters.Count, Kind = b.kind, Endorheic = true, Cells = b.cells, SurfaceM = b.level, SpillM = b.depression >= 0 ? result.Depressions[b.depression].SpillM : FindSpill(s, h, b.cells), DepressionId = b.depression }; waters.Add(body); foreach (int c in b.cells) result.WaterBody[c] = body.Id;
            }
            if (heap.Count == 0) { int pit = Enumerable.Range(0, n).OrderBy(i => h[i]).ThenBy(i => i).First(); seen[pit] = true; result.FloodLevelM[pit] = h[pit]; result.Watershed[pit] = pit; rootKind[pit] = 1; heap.Push(h[pit], pit); }
            while (heap.Count > 0)
            {
                var item = heap.Pop(); int c = item.Id; order.Add(c); if ((order.Count & 255) == 0) token.ThrowIfCancellationRequested();
                foreach (int nb in s.Cells[c].Neighbors) if (!seen[nb])
                { seen[nb] = true; result.Receiver[nb] = c; result.FloodLevelM[nb] = Math.Max(h[nb], result.FloodLevelM[c]); result.Watershed[nb] = result.Watershed[c]; rootKind[nb] = rootKind[c]; heap.Push(result.FloodLevelM[nb], nb); }
            }
            // Receiver topology is ordered explicitly, including seeded multi-cell reservoirs.
            var donors = new List<int>[n]; var pending = new int[n]; for (int i = 0; i < n; i++) donors[i] = new List<int>();
            for (int i = 0; i < n; i++) if (result.Receiver[i] >= 0) { donors[result.Receiver[i]].Add(i); pending[result.Receiver[i]]++; }
            var topo = new List<int>(); var ready = new SortedSet<int>(Enumerable.Range(0, n).Where(i => pending[i] == 0));
            while (ready.Count > 0) { int c = ready.Min; ready.Remove(c); topo.Add(c); int rec = result.Receiver[c]; if (rec >= 0 && --pending[rec] == 0) ready.Add(rec); }
            if (topo.Count != n) throw new InvalidOperationException("Hydrology receiver cycle.");
            result.VisitOrder = topo.AsEnumerable().Reverse().ToArray();
            foreach (int c in topo)
            {
                int max = donors[c].Count == 0 ? 1 : donors[c].Max(j => result.RiverOrder[j]); int count = donors[c].Count(j => result.RiverOrder[j] == max); result.RiverOrder[c] = max + (count >= 2 ? 1 : 0);
                int rec = result.Receiver[c]; if (rec >= 0) result.ContributingAreaM2[rec] += result.ContributingAreaM2[c];
            }
            // Wet connected cells at the same fill level form one lake with one physical surface.
            var initialRank = new int[n]; for (int k = 0; k < n; k++) initialRank[result.VisitOrder[k]] = k;
            var handled = new bool[n]; var glacial = new bool[n]; foreach (var f in features.Where(f => f.Kind == FeatureKind.GlacialBasin)) foreach (int c in f.Cells) glacial[c] = true;
            for (int i = 0; i < n; i++)
            {
                if (handled[i] || result.OceanConnected[i] || result.WaterBody[i] >= 0 || result.FloodLevelM[i] <= h[i]) continue;
                int level = result.FloodLevelM[i]; var cells = new List<int>(); var q = new Queue<int>(); q.Enqueue(i); handled[i] = true;
                while (q.Count > 0) { int c = q.Dequeue(); cells.Add(c); foreach (int nb in s.Cells[c].Neighbors) if (!handled[nb] && !result.OceanConnected[nb] && result.WaterBody[nb] < 0 && result.FloodLevelM[nb] == level && h[nb] < level) { handled[nb] = true; q.Enqueue(nb); } }
                cells.Sort(); if (cells.Sum(c => s.Cells[c].AreaM2) < r.MinLakeAreaKm2 * 1e6) continue; var membership = new HashSet<int>(cells);
                int outlet = cells.Where(c => result.Receiver[c] >= 0 && !membership.Contains(result.Receiver[c])).OrderBy(c => initialRank[c]).ThenBy(c => c).DefaultIfEmpty(-1).First();
                int pit = cells.OrderBy(c => h[c]).ThenBy(c => c).First(); int dep = Array.FindIndex(result.Depressions, d => d.PitCell == pit && d.Children.Length == 0);
                var body = new WaterBody { Id = waters.Count, Kind = cells.Any(c => glacial[c]) ? WaterKind.GlacialLake : WaterKind.Lake, Cells = cells.ToArray(), SurfaceM = level, SpillM = level, OutletCell = outlet, DownstreamCell = outlet < 0 ? -1 : result.Receiver[outlet], DepressionId = dep }; waters.Add(body); foreach (int c in cells) result.WaterBody[c] = body.Id;
                // Collapse the wet component to one stable outlet, with an acyclic lake-internal tree.
                if (outlet >= 0)
                { var routed = new HashSet<int> { outlet }; var lakeQueue = new Queue<int>(); lakeQueue.Enqueue(outlet); while (lakeQueue.Count > 0) { int c = lakeQueue.Dequeue(); foreach (int nb in s.Cells[c].Neighbors) if (membership.Contains(nb) && routed.Add(nb)) { result.Receiver[nb] = c; lakeQueue.Enqueue(nb); } } }
            }
            if (main >= 0) { var ocean = new WaterBody { Id = waters.Count, Kind = WaterKind.Ocean, SurfaceM = 0, SpillM = 0, Cells = Enumerable.Range(0, n).Where(i => result.OceanConnected[i]).ToArray() }; waters.Add(ocean); foreach (int c in ocean.Cells) result.WaterBody[c] = ocean.Id; }
            foreach (var body in waters) { body.AreaM2 = body.Cells.Sum(c => s.Cells[c].AreaM2); body.VolumeM3 = body.Cells.Sum(c => (double)s.Cells[c].AreaM2 * Math.Max(0, body.SurfaceM - h[c])); }
            result.Waters = waters.ToArray();
            // Recompute all drainage products after lake outlet consolidation.
            for (int i = 0; i < n; i++) { donors[i].Clear(); pending[i] = 0; result.ContributingAreaM2[i] = s.Cells[i].AreaM2; }
            for (int i = 0; i < n; i++) if (result.Receiver[i] >= 0) { donors[result.Receiver[i]].Add(i); pending[result.Receiver[i]]++; }
            topo.Clear(); ready = new SortedSet<int>(Enumerable.Range(0, n).Where(i => pending[i] == 0));
            while (ready.Count > 0) { int c = ready.Min; ready.Remove(c); topo.Add(c); int rec = result.Receiver[c]; if (rec >= 0 && --pending[rec] == 0) ready.Add(rec); }
            if (topo.Count != n) throw new InvalidOperationException("Lake outlet consolidation created a receiver cycle.");
            foreach (int c in topo) { int max = donors[c].Count == 0 ? 1 : donors[c].Max(j => result.RiverOrder[j]); result.RiverOrder[c] = max + (donors[c].Count(j => result.RiverOrder[j] == max) >= 2 ? 1 : 0); int rec = result.Receiver[c]; if (rec >= 0) result.ContributingAreaM2[rec] += result.ContributingAreaM2[c]; }
            result.VisitOrder = topo.AsEnumerable().Reverse().ToArray(); foreach (int c in result.VisitOrder) result.Watershed[c] = result.Receiver[c] < 0 ? c : result.Watershed[result.Receiver[c]];
            var rivers = new List<RiverReach>();
            for (int i = 0; i < n; i++) if (result.Receiver[i] >= 0 && result.WaterBody[i] < 0 && result.ContributingAreaM2[i] >= r.RiverThresholdKm2 * 1e6)
            { rivers.Add(new RiverReach { Id = rivers.Count, From = i, To = result.Receiver[i], WatershedId = result.Watershed[i], Order = result.RiverOrder[i], ContributingAreaM2 = result.ContributingAreaM2[i], DischargePotentialM3PerS = D.Q(result.ContributingAreaM2[i] / 1000.0 / 86400, 1e6) }); }
            result.Rivers = rivers.ToArray(); return result;
        }
        static int FindSpill(Surface s, int[] h, int[] cells)
        { var set = new HashSet<int>(cells); return cells.SelectMany(c => s.Cells[c].Neighbors).Where(c => !set.Contains(c)).Select(c => h[c]).DefaultIfEmpty(cells.Max(c => h[c])).Min(); }
    }
}
