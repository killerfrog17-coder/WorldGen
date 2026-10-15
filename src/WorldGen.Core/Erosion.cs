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
            var s = w.Surface; var recipe = w.Recipe; int n = s.Cells.Length; var h = w.ElevationM;
            // World Orogen resolves stranded continental depressions before
            // postprocessing. Keep their physical elevation, but preserve the
            // resulting land classification for the postprocessing stages.
            var oceanCrust = w.Continent.Select(c => c < 0).ToArray();
            var topologyConnected = OceanTopology(s, h, oceanCrust, token);
            var strandedLand = new bool[n];
            for (int i = 0; i < n; i++) strandedLand[i] = w.Continent[i] >= 0 && h[i] <= 0 && !topologyConnected[i];
            var warped = (int[])h.Clone(); var volcanic = new bool[n];
            var intentionalOceanIsland = new bool[n];
            foreach (var feature in w.Features.Where(f => f.Kind == FeatureKind.Hotspot || f.Kind == FeatureKind.IslandChain || f.Kind == FeatureKind.IslandArc || f.Kind == FeatureKind.VolcanicArc || f.Kind == FeatureKind.RidgeHotspot || f.Kind == FeatureKind.RelictArc || f.Kind == FeatureKind.AccretionaryWedge))
                foreach (int cell in feature.Cells)
                    if (feature.OwnerParent < 0 || w.Microplates[w.CellMicroplate[cell]].ParentAffinityId == feature.OwnerParent) volcanic[cell] = true;
            // A shallow arc/accretion ridge can emerge without an 800 m summit.
            // Only actual, owner-qualified tectonic features permit ocean islands;
            // ordinary ridges and numerical coast overshoots remain submarine.
            for (int i = 0; i < n; i++) if (oceanCrust[i] && h[i] >= 40 && volcanic[i]) intentionalOceanIsland[i] = true;
            void ClampOrdinaryOcean()
            {
                for (int i = 0; i < n; i++)
                    if (oceanCrust[i] && !intentionalOceanIsland[i] && h[i] > 0) h[i] = -1;
            }
            for (int i = 0; i < n; i++)
            {
                if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                if (recipe.TerrainWarp <= 0 || w.ProtectedInlandSea[i]) continue;
                V p = s.Cells[i].Position;
                V offset = new V(Terrain.Noise(p, recipe.Seed ^ 2921, 5), Terrain.Noise(p, recipe.Seed ^ 9419, 5), Terrain.Noise(p, recipe.Seed ^ 5219, 5));
                V target = (p + offset * (recipe.TerrainWarp * .055 * (volcanic[i] ? .2 : 1))).Unit;
                int near = Nearest(s, target, i); double sum = 0, weight = 0;
                foreach (int j in s.Cells[near].Neighbors.Concat(new[] { near }))
                { double distance = Math.Max(1e-7, 1 - V.Dot(target, s.Cells[j].Position)); double v = 1 / (distance * distance); sum += h[j] * v; weight += v; }
                // A warp perturbs the existing structural surface; it must not
                // replace it outright or it turns small texture changes into coasts.
                double blend = .25 + .5 * recipe.TerrainWarp;
                warped[i] = D.M(h[i] + (sum / weight - h[i]) * blend);
            }
            h = warped; ClampOrdinaryOcean(); w.PreErosionM = (int[])h.Clone();
            var ocean = Enumerable.Range(0, n).Select(i => h[i] <= 0 && !strandedLand[i]).ToArray();
            var lockedCoast = StructuralCoastLocks(s, h, w.Continent);
            void Smooth(double amount, bool bilateral)
            {
                var next = (int[])h.Clone();
                for (int i = 0; i < n; i++)
                {
                    if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                    if (lockedCoast[i]) continue;
                    double sum = 0, weights = 0;
                    foreach (int j in s.Cells[i].Neighbors)
                    {
                        double weight = bilateral ? 1 / (1 + Math.Abs(h[i] - h[j]) / 400.0) : 1;
                        sum += h[j] * weight; weights += weight;
                    }
                    if (weights > 0) next[i] = D.M(h[i] + (sum / weights - h[i]) * amount);
                }
                h = next;
                ClampOrdinaryOcean();
            }
            if (recipe.Smoothing > 0) for (int pass = 0; pass < D.M(1 + recipe.Smoothing * 4); pass++) Smooth(.2 + recipe.Smoothing * .5, true);
            // Zero-mean, elevation-gated detail texture. Low terrain stays stable
            // near sea level so texture does not manufacture new coastlines.
            for (int i = 0; i < n; i++) if (!ocean[i] && !w.ProtectedInlandSea[i])
            {
                V p = s.Cells[i].Position; double damp = 1 - .8 * Math.Max(w.CratonPermille[i], w.BasinPermille[i]) / 1000.0;
                double detail = 65 * Terrain.Noise(p, recipe.Seed ^ 6421, 31) + 35 * Terrain.Noise(p, recipe.Seed ^ 9181, 57);
                double elevationGate = D.Clamp(h[i] / 300.0);
                h[i] += D.M(detail * damp * elevationGate * D.Clamp(w.Stress[i] / 1000.0, .15, 1));
            }
            ClampOrdinaryOcean();
            int hydraulicPasses = D.M(recipe.Hydraulic * 20), thermalPasses = D.M(recipe.Thermal * 10), glacialPasses = D.M(recipe.Glacial * 10);
            var glaciated = new bool[n];
            for (int pass = 0; pass < Math.Max(hydraulicPasses, Math.Max(thermalPasses, glacialPasses)); pass++)
            {
                token.ThrowIfCancellationRequested(); Route(s, h, w.ProtectedInlandSea, out var receiver, out var order, token);
                var area = s.Cells.Select(c => (double)c.AreaM2).ToArray();
                for (int k = order.Length - 1; k >= 0; k--) { int i = order[k]; if (receiver[i] >= 0) area[receiver[i]] += area[i]; }
                var delta = new double[n];
                foreach (int i in order)
                {
                    int j = receiver[i]; if (j < 0 || w.ProtectedInlandSea[i]) continue;
                    double drop = Math.Max(0, h[i] - h[j]), ratio = s.Cells[i].AreaM2 / (double)s.Cells[j].AreaM2;
                    int edge = Array.IndexOf(s.Cells[i].Neighbors, j); double distance = s.Cells[i].DistanceM[edge];
                    if (pass < hydraulicPasses && !ocean[i] && drop > 0)
                    {
                        double k = recipe.Hydraulic * .07 * D.Sqrt(area[i] / 1e10) * 150000 / distance;
                        double cut = Math.Min(drop * .25, drop * k / (1 + k));
                        delta[i] -= cut;
                        if (!w.ProtectedInlandSea[j]) delta[j] += cut * .18 * ratio * D.Clamp(1 - drop / 800);
                    }
                    if (pass < thermalPasses && drop > distance * .009)
                    {
                        double moved = (drop - distance * .009) * recipe.Thermal * .12;
                        if (!w.ProtectedInlandSea[j]) { delta[i] -= moved; delta[j] += moved * ratio; }
                    }
                    double ice = D.Clamp((Math.Abs(s.Cells[i].Position.Z) - .55) / .35 + (h[i] - 2000) / 4000.0);
                    if (pass < glacialPasses && ice > 0 && h[i] > -400 && area[i] > s.Cells[i].AreaM2 * 1.2)
                    {
                        double cut = recipe.Glacial * ice * Math.Min(150, 25 + D.Sqrt(area[i] / 1e10) * 12 + drop * .05);
                        delta[i] -= cut; glaciated[i] = true;
                        foreach (int nb in s.Cells[i].Neighbors) if (!w.ProtectedInlandSea[nb] && h[nb] > h[i]) delta[nb] -= cut * .1;
                    }
                }
                for (int i = 0; i < n; i++) if (!w.ProtectedInlandSea[i]) h[i] += D.M(delta[i]);
                ClampOrdinaryOcean();
            }
            if (recipe.RidgePreservation > 0) for (int pass = 0; pass < D.M(1 + recipe.RidgePreservation * 3); pass++)
            {
                var next = (int[])h.Clone();
                for (int i = 0; i < n; i++) if (!ocean[i] && !w.ProtectedInlandSea[i])
                {
                    double mean = s.Cells[i].Neighbors.Average(j => h[j]);
                    next[i] += D.M(Math.Max(0, h[i] - mean) * recipe.RidgePreservation * .08);
                }
                h = next;
                ClampOrdinaryOcean();
            }
            w.Glaciated = glaciated;
            for (int pass = 0; pass < 3; pass++) Smooth(.1125, true);
            ShapeContinentalHypsometry(w, h, token);
            ApplyContinentalInteriorFloor(w, h);
            ClampOrdinaryOcean();
            int breached = BreachCoastalBarriers(s, h, w.ProtectedInlandSea, glaciated, w.CoastDistanceM, D.M(recipe.Hydraulic * 60), token);
            w.Metric("erosion_breached_coastal_cells", breached);
            w.ElevationM = h;
            // Recompute after every height-changing pass so new sub-sea-level
            // continental cells receive the same final topology treatment.
            var finalTopologyConnected = OceanTopology(s, h, oceanCrust, token);
            var finalStrandedLand = new bool[n];
            for (int i = 0; i < n; i++) finalStrandedLand[i] = !oceanCrust[i] && h[i] <= 0 && !finalTopologyConnected[i];
            w.Hydrology.OceanConnected = OceanConnectivity(s, h, oceanCrust, finalStrandedLand, token);
            w.Landmass = Graph.Components(s, i => !w.Hydrology.OceanConnected[i]);
            w.Metric("erosion_mean_absolute_delta", Enumerable.Range(0, n).Average(i => Math.Abs(h[i] - w.PreErosionM[i])), "m");
        }
        // Limited physical incision before climate: remove shallow coastal dams,
        // not mountain divides. Earth bypasses this entire sculpting stage.
        public static int BreachCoastalBarriers(Surface s, int[] h, bool[] protect, bool[] glaciated, int[] coastDistanceM, int maxCutM, CancellationToken token)
        {
            if (maxCutM <= 0) return 0;
            Route(s, h, protect, out var receiver, out _, token);
            var original = (int[])h.Clone();
            var changed = new bool[h.Length]; int count = 0;
            for (int pit = 0; pit < h.Length; pit++)
            {
                if ((pit & 1023) == 0) token.ThrowIfCancellationRequested();
                if (h[pit] < 3 || coastDistanceM[pit] > 500000 || protect[pit] || glaciated[pit]) continue;
                bool minimum = true;
                foreach (int neighbor in s.Cells[pit].Neighbors)
                    if (h[neighbor] < h[pit] || h[neighbor] == h[pit] && neighbor < pit) { minimum = false; break; }
                if (!minimum) continue;
                var path = new List<int>(); var levels = new List<int>();
                int cursor = pit, level = h[pit], distance = 0; bool outlet = false;
                for (int step = 0; step < 12; step++)
                {
                    int next = receiver[cursor]; if (next < 0) break;
                    int edge = Array.IndexOf(s.Cells[cursor].Neighbors, next);
                    distance += s.Cells[cursor].DistanceM[edge]; level--;
                    if (distance > 350000 || level < 1 || protect[next] || glaciated[next]) break;
                    if (h[next] < level) { outlet = true; break; }
                    if (original[next] - level > maxCutM) break;
                    path.Add(next); levels.Add(level); cursor = next;
                }
                if (!outlet) continue;
                for (int k = 0; k < path.Count; k++)
                {
                    int cell = path[k]; if (h[cell] <= levels[k]) continue;
                    h[cell] = levels[k]; if (!changed[cell]) { changed[cell] = true; count++; }
                }
            }
            return count;
        }

        static bool[] StructuralCoastLocks(Surface s, int[] h, int[] continent)
        {
            var locked = new bool[h.Length];
            for (int i = 0; i < h.Length; i++)
            {
                bool oceanCrust = continent[i] < 0;
                // Islands and continental crust directly bordering oceanic crust
                // define the structural coast and are not blurred by erosion smoothing.
                locked[i] = oceanCrust && h[i] > 0 || !oceanCrust && s.Cells[i].Neighbors.Any(j => continent[j] < 0);
            }
            return locked;
        }
        static void ShapeContinentalHypsometry(World w, int[] h, CancellationToken token)
        {
            var land = Enumerable.Range(0, h.Length).Where(i => w.Continent[i] >= 0).ToArray();
            var ancientOrogen = new bool[h.Length];
            foreach (var feature in w.Features.Where(f => f.Kind == FeatureKind.AncientOrogen))
                foreach (int cell in feature.Cells) ancientOrogen[cell] = true;
            Array.Sort(land, (a, b) => h[a] != h[b] ? h[a].CompareTo(h[b]) : a.CompareTo(b));
            if (land.Length == 0) return;
            double totalArea = land.Sum(i => (double)w.Surface.Cells[i].AreaM2), accumulatedArea = 0;
            for (int rank = 0; rank < land.Length; rank++)
            {
                if ((rank & 1023) == 0) token.ThrowIfCancellationRequested();
                int i = land[rank]; double area = w.Surface.Cells[i].AreaM2;
                double q = (accumulatedArea + area * .5) / totalArea; accumulatedArea += area;
                // The target is a broad continental basin-to-upland distribution.
                // Its low tail remains below sea level, but is intentionally small:
                // broad structural crust controls the coastline, not local texture.
                double target;
                if (q < .018) target = -260 + 300 * Math.Pow(q / .018, .9);
                else if (q < .64) target = 40 + 530 * Math.Pow((q - .018) / .622, 1.42);
                else if (q < .91) target = 570 + 1450 * Math.Pow((q - .64) / .27, 1.18);
                else target = 2020 + 3600 * Math.Pow((q - .91) / .09, 1.7);
                double conditioned = h[i] < -400 ? -400 + (h[i] + 400) * .1 : h[i];
                if (conditioned > 6500) conditioned = 6500 + (conditioned - 6500) * .2;
                // Preserve the distinct cross-sections of genuine orogenic
                // fronts and plateaus instead of forcing them into a global rank curve.
                double blend = h[i] < 0 ? .9 : h[i] > 1200 && w.FoldPermille[i] + w.PlateauPermille[i] > 450 ? .12 : ancientOrogen[i] ? .2 : .34;
                h[i] = D.M(conditioned + (target - conditioned) * blend);
            }
        }
        static void ApplyContinentalInteriorFloor(World w, int[] h)
        {
            var rift = new bool[h.Length];
            foreach (var feature in w.Features.Where(f => f.Kind == FeatureKind.Rift || f.Kind == FeatureKind.AncientRift))
                foreach (int cell in feature.Cells) rift[cell] = true;
            for (int i = 0; i < h.Length; i++)
            {
                if (w.Continent[i] < 0 || rift[i] || w.CoastDistanceM[i] < 450000 || h[i] >= 0) continue;
                double interior = D.Clamp((w.CoastDistanceM[i] - 450000) / 1200000.0);
                h[i] = Math.Max(h[i], D.M(8 + 32 * interior));
            }
        }
        // All ocean-plate components seed the flood. This supports worlds whose
        // continents separate multiple independent ocean basins.
        static bool[] OceanTopology(Surface s, int[] h, bool[] oceanCrust, CancellationToken token)
        {
            int n = h.Length; var connected = new bool[n]; var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
                if (oceanCrust[i]) { connected[i] = true; queue.Enqueue(i); }
            while (queue.Count > 0)
            {
                if ((queue.Count & 1023) == 0) token.ThrowIfCancellationRequested();
                int i = queue.Dequeue();
                foreach (int j in s.Cells[i].Neighbors)
                    if (!connected[j] && h[j] <= 0) { connected[j] = true; queue.Enqueue(j); }
            }
            return connected;
        }
        static bool[] OceanConnectivity(Surface s, int[] h, bool[] oceanCrust, bool[] strandedLand, CancellationToken token)
        {
            int n = h.Length; var connected = new bool[n]; var visited = new bool[n]; var queue = new Queue<int>();
            for (int i = 0; i < n; i++) if (oceanCrust[i]) { visited[i] = true; queue.Enqueue(i); }
            while (queue.Count > 0)
            {
                if ((queue.Count & 1023) == 0) token.ThrowIfCancellationRequested();
                int i = queue.Dequeue();
                foreach (int j in s.Cells[i].Neighbors)
                    if (!visited[j] && h[j] < 0 && !strandedLand[j]) { visited[j] = true; queue.Enqueue(j); }
            }
            for (int i = 0; i < n; i++) connected[i] = h[i] < 0 && visited[i] && !strandedLand[i];
            return connected;
        }
        public static int Nearest(Surface s, V p, int start = 0)
        {
            int current = start;
            while (true)
            {
                int next = current; double best = V.Dot(p, s.Cells[current].Position);
                foreach (int j in s.Cells[current].Neighbors) { double dot = V.Dot(p, s.Cells[j].Position); if (dot > best || dot == best && j < next) { best = dot; next = j; } }
                if (next == current) return current; current = next;
            }
        }
        static void Route(Surface s, int[] h, bool[] protect, out int[] receiver, out int[] order, CancellationToken token)
        {
            int n = h.Length; receiver = Enumerable.Repeat(-1, n).ToArray(); var visited = new bool[n]; var heap = new Heap(); var ordered = new List<int>(n);
            for (int i = 0; i < n; i++) if (h[i] <= 0 || protect[i]) { visited[i] = true; heap.Push(h[i], i); }
            if (heap.Count == 0) { int minimum = Enumerable.Range(0, n).OrderBy(i => h[i]).ThenBy(i => i).First(); visited[minimum] = true; heap.Push(h[minimum], minimum); }
            while (heap.Count > 0)
            {
                if ((ordered.Count & 1023) == 0) token.ThrowIfCancellationRequested();
                var item = heap.Pop(); ordered.Add(item.Id);
                foreach (int j in s.Cells[item.Id].Neighbors) if (!visited[j])
                { visited[j] = true; receiver[j] = item.Id; heap.Push(Math.Max(h[j], item.Key), j); }
            }
            order = ordered.ToArray();
        }
    }
}
