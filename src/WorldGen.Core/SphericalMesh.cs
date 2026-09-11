using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public static class SphericalMesh
    {
        public const int MinCellsPerMicroplate = 16;
        public static Surface Build(Recipe r, CancellationToken token)
        {
            double phi = (1 + D.Sqrt(5)) / 2;
            var v = new List<V> { new V(-1,phi,0).Unit,new V(1,phi,0).Unit,new V(-1,-phi,0).Unit,new V(1,-phi,0).Unit,
                new V(0,-1,phi).Unit,new V(0,1,phi).Unit,new V(0,-1,-phi).Unit,new V(0,1,-phi).Unit,
                new V(phi,0,-1).Unit,new V(phi,0,1).Unit,new V(-phi,0,-1).Unit,new V(-phi,0,1).Unit };
            var tris = new List<int> { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            int required = Math.Max(r.MacroCellCount, r.MicroplateCount * MinCellsPerMicroplate);
            while (v.Count < required)
            {
                token.ThrowIfCancellationRequested(); var mid = new Dictionary<long, int>(); var next = new List<int>();
                int Mid(int a, int b) { long key = Key(a, b); if (mid.TryGetValue(key, out int id)) return id; id = v.Count; v.Add((v[a] + v[b]).Unit); mid.Add(key, id); return id; }
                for (int t = 0; t < tris.Count; t += 3) { int a = tris[t], b = tris[t + 1], c = tris[t + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a); next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca }); }
                tris = next;
            }
            // Sphere-native irregularity without a projection seam; each vertex stays in its local neighborhood.
            double jitter = .22 * D.Sqrt(4 * D.Pi / v.Count);
            for (int i = 0; i < v.Count; i++) { var rng = new Rng(r.Seed, "mesh-jitter", i); v[i] = (v[i] + V.Tangent(v[i], rng.Vector()) * (jitter * (.3 + .7 * rng.Unit()))).Unit; }
            var surface = new Surface { Triangles = tris.ToArray(), Cells = new Cell[v.Count], Corners = new V[tris.Count / 3] };
            var area = new double[v.Count]; var adjacency = new SortedSet<int>[v.Count]; for (int i = 0; i < v.Count; i++) adjacency[i] = new SortedSet<int>();
            var edges = new SortedDictionary<long, Edge>();
            for (int t = 0; t < tris.Count; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2]; var center = (v[a] + v[b] + v[c]).Unit; surface.Corners[t / 3] = center;
                int[] ids = { a, b, c };
                for (int k = 0; k < 3; k++)
                {
                    int x = ids[k], y = ids[(k + 1) % 3], z = ids[(k + 2) % 3];
                    area[x] += TriangleArea(v[x], (v[x] + v[y]).Unit, center) + TriangleArea(v[x], center, (v[x] + v[z]).Unit);
                    adjacency[x].Add(y); adjacency[y].Add(x); long key = Key(x, y);
                    if (edges.TryGetValue(key, out var edge)) edge.TriangleB = t / 3;
                    else edges.Add(key, new Edge { A = Math.Min(x, y), B = Math.Max(x, y), TriangleA = t / 3, TriangleB = -1 });
                }
            }
            int radiusM = r.RadiusKm * 1000;
            for (int i = 0; i < v.Count; i++)
            {
                var n = adjacency[i].ToArray(); surface.Cells[i] = new Cell { Id = i, Position = v[i], AreaM2 = (long)Math.Round(area[i] * radiusM * radiusM), Neighbors = n, DistanceM = n.Select(j => Math.Max(1, D.M(D.Angle(v[i], v[j]) * radiusM))).ToArray() };
            }
            surface.Edges = edges.Values.ToArray();
            for (int i = 0; i < surface.Edges.Length; i++) { var e = surface.Edges[i]; if (e.TriangleB < 0) throw new InvalidOperationException("Open sphere."); e.Id = i; e.LengthM = Math.Max(1, D.M(D.Angle(surface.Corners[e.TriangleA], surface.Corners[e.TriangleB]) * radiusM)); }
            return surface;
        }
        static long Key(int a, int b) => ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
        static double TriangleArea(V a, V b, V c) => 2 * D.Atan(Math.Abs(V.Dot(a, V.Cross(b, c))) / Math.Max(1e-15, 1 + V.Dot(a, b) + V.Dot(b, c) + V.Dot(c, a)));
    }
    public static class Graph
    {
        public static int[] Components(Surface s, Func<int, bool> included)
        {
            var labels = Enumerable.Repeat(-1, s.Cells.Length).ToArray(); int id = 0; var q = new Queue<int>();
            for (int i = 0; i < labels.Length; i++) if (labels[i] < 0 && included(i))
            { labels[i] = id; q.Enqueue(i); while (q.Count > 0) { int c = q.Dequeue(); foreach (int n in s.Cells[c].Neighbors) if (labels[n] < 0 && included(n)) { labels[n] = id; q.Enqueue(n); } } id++; }
            return labels;
        }
        public static int[] Distances(Surface s, IEnumerable<int> seeds, out int[] nearest)
        {
            var dist = Enumerable.Repeat(int.MaxValue, s.Cells.Length).ToArray(); nearest = Enumerable.Repeat(-1, s.Cells.Length).ToArray(); var heap = new Heap();
            foreach (int i in seeds.OrderBy(i => i)) { dist[i] = 0; nearest[i] = i; heap.Push(0, i, i); }
            while (heap.Count > 0)
            {
                var item = heap.Pop(); int c = item.Id; if (item.Key != dist[c] || item.From != nearest[c]) continue;
                var cell = s.Cells[c]; for (int k = 0; k < cell.Neighbors.Length; k++) { int n = cell.Neighbors[k]; long d = item.Key + cell.DistanceM[k]; if (d < dist[n] || d == dist[n] && nearest[c] < nearest[n]) { dist[n] = (int)d; nearest[n] = nearest[c]; heap.Push(d, n, nearest[n]); } }
            }
            return dist;
        }
        public static int LargestComponent(Surface s, int[] labels)
        {
            var areas = new SortedDictionary<int, long>(); for (int i = 0; i < labels.Length; i++) if (labels[i] >= 0) { if (!areas.ContainsKey(labels[i])) areas[labels[i]] = 0; areas[labels[i]] += s.Cells[i].AreaM2; }
            return areas.Count == 0 ? -1 : areas.OrderByDescending(p => p.Value).ThenBy(p => p.Key).First().Key;
        }
        public static int[] SeparatedSeeds(V[] positions, int count, int seed, string stream)
        {
            var seeds = new List<int>(); var best = Enumerable.Repeat(4.0, positions.Length).ToArray(); int current = (int)(new Rng(seed, stream).Next() % (uint)positions.Length);
            for (int p = 0; p < count; p++)
            {
                seeds.Add(current); for (int i = 0; i < positions.Length; i++) best[i] = Math.Min(best[i], D.Q(1 - V.Dot(positions[i], positions[current]), 1e6));
                double max = -1; for (int i = 0; i < positions.Length; i++) { double score = best[i] * (.95 + .05 * Rng.Value(seed, stream + "-score", i)); if (score > max) { max = score; current = i; } }
            }
            return seeds.ToArray();
        }
    }
}
