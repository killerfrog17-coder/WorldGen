using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public static class PlateProjection
    {
        // A fixed coarse partition supplies identities; fine cells retain their own
        // neighborhoods, boundary geometry, terrain and erosion state.
        public static void Apply(World w, CancellationToken token)
        {
            var coarse = w.CoarseSurface; var fine = w.Surface; int n = fine.Cells.Length, count = w.Microplates.Length;
            var locator = new CellLocator(coarse.Cells); var desired = new int[n];
            var seeds = Enumerable.Repeat(-1, count).ToArray(); var scores = Enumerable.Repeat(double.NegativeInfinity, count).ToArray();
            for (int i = 0; i < n; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                V p = fine.Cells[i].Position;
                V perturb = new V(Terrain.Noise(p, w.Recipe.Seed ^ 1291, 12), Terrain.Noise(p, w.Recipe.Seed ^ 4371, 12), Terrain.Noise(p, w.Recipe.Seed ^ 8317, 12));
                int plate = w.CoarsePlate[locator.Nearest((p + perturb * .013).Unit)]; desired[i] = plate;
                double score = V.Dot(p, coarse.Cells[w.Microplates[plate].SeedCell].Position);
                if (score > scores[plate]) { scores[plate] = score; seeds[plate] = i; }
            }
            var labels = Enumerable.Repeat(-1, n).ToArray(); var costs = Enumerable.Repeat(long.MaxValue, n).ToArray(); var heap = new Heap();
            // Even very small coarse plates must retain a fine seed at minimum detail.
            for (int plate = 0; plate < count; plate++)
            {
                int seed = seeds[plate];
                if (seed < 0 || labels[seed] >= 0)
                {
                    double best = double.NegativeInfinity;
                    for (int i = 0; i < n; i++) if (labels[i] < 0)
                    { double dot = V.Dot(fine.Cells[i].Position, w.Microplates[plate].Centroid); if (dot > best) { best = dot; seed = i; } }
                }
                labels[seed] = plate; seeds[plate] = seed; costs[seed] = 0; heap.Push(0, seed, plate);
            }
            var settled = new bool[n];
            while (heap.Count > 0)
            {
                var next = heap.Pop(); int i = next.Id, plate = next.From;
                if (settled[i] || next.Key != costs[i] || labels[i] != plate) continue;
                settled[i] = true; if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                var cell = fine.Cells[i];
                for (int k = 0; k < cell.Neighbors.Length; k++)
                {
                    int j = cell.Neighbors[k]; if (settled[j] || costs[j] == 0) continue;
                    long cost = next.Key + cell.DistanceM[k] * (desired[j] == plate ? 1L : 256L);
                    if (cost < costs[j] || cost == costs[j] && plate < labels[j])
                    { costs[j] = cost; labels[j] = plate; heap.Push(cost, j, plate); }
                }
            }
            w.CellMicroplate = labels; w.Continent = labels.Select(p => w.Microplates[p].ContinentId).ToArray();
            var members = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
            var neighbors = Enumerable.Range(0, count).Select(_ => new SortedDictionary<int, long>()).ToArray();
            var boundaries = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
            for (int i = 0; i < n; i++) members[labels[i]].Add(i);
            foreach (var e in fine.Edges)
            {
                int a = labels[e.A], b = labels[e.B]; if (a == b) continue;
                if (!neighbors[a].ContainsKey(b)) { neighbors[a][b] = 0; neighbors[b][a] = 0; }
                neighbors[a][b] += e.LengthM; neighbors[b][a] += e.LengthM; boundaries[a].Add(e.Id); boundaries[b].Add(e.Id);
            }
            for (int p = 0; p < count; p++)
            {
                var plate = w.Microplates[p]; plate.SeedCell = seeds[p]; plate.Cells = members[p].ToArray(); plate.AreaM2 = 0; V center = new V();
                foreach (int i in members[p]) { var c = fine.Cells[i]; plate.AreaM2 += c.AreaM2; center += c.Position * (c.AreaM2 / 1e12); }
                plate.Centroid = center.Unit; plate.Neighbors = neighbors[p].Keys.ToArray(); plate.SharedBoundaryM = neighbors[p].Values.ToArray(); plate.BoundaryEdges = boundaries[p].ToArray();
            }
            w.Metric("minimum_plate_cells", members.Min(p => p.Count)); w.Metric("plates", count);
        }
    }
}
