using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DelaunatorSharp;

namespace WorldGen.Core
{
    public static class SphericalMesh
    {
        public const int CanonicalCellCount = 20000;
        public const int MinCellsPerMicroplate = 16;
        public static Surface Build(Recipe r, CancellationToken token) => Build(r, r.Detail, r.Irregularity, "detail-geometry", token);
        public static Surface BuildCoarse(Recipe r, CancellationToken token) => Build(r, CanonicalCellCount, .75, "canonical-geometry", token);
        static Surface Build(Recipe r, int count, double irregularity, string stream, CancellationToken token)
        {
            var geometry = Fibonacci(count, irregularity, r.Seed, stream, token);
            var v = geometry.Positions; var tris = geometry.Triangles;
            var surface = new Surface { Triangles = tris, Cells = new Cell[v.Length], Corners = new V[tris.Length / 3] };
            var area = new double[v.Length]; var degree = new int[v.Length];
            // Sorted packed edge keys replace millions of tree nodes and per-cell sets.
            var keys = new long[tris.Length]; var faces = new int[tris.Length];
            for (int t = 0; t < tris.Length; t += 3)
            {
                if ((t & 4095) == 0) token.ThrowIfCancellationRequested();
                int a = tris[t], b = tris[t + 1], c = tris[t + 2]; var center = (v[a] + v[b] + v[c]).Unit; surface.Corners[t / 3] = center;
                for (int k = 0; k < 3; k++)
                {
                    int x = tris[t + k], y = tris[t + (k + 1) % 3], z = tris[t + (k + 2) % 3];
                    area[x] += TriangleArea(v[x], (v[x] + v[y]).Unit, center) + TriangleArea(v[x], center, (v[x] + v[z]).Unit);
                    keys[t + k] = Key(x, y); faces[t + k] = t / 3;
                }
            }
            Array.Sort(keys, faces); token.ThrowIfCancellationRequested();
            surface.Edges = new Edge[keys.Length / 2]; int radiusM = r.RadiusKm * 1000;
            for (int k = 0; k < keys.Length; k += 2)
            {
                if ((k & 4095) == 0) token.ThrowIfCancellationRequested();
                if (k + 1 >= keys.Length || keys[k] != keys[k + 1]) throw new InvalidOperationException("Open sphere.");
                int a = (int)(keys[k] >> 32), b = (int)keys[k], ta = Math.Min(faces[k], faces[k + 1]), tb = Math.Max(faces[k], faces[k + 1]);
                surface.Edges[k / 2] = new Edge { Id = k / 2, A = a, B = b, TriangleA = ta, TriangleB = tb,
                    LengthM = Math.Max(1, D.M(D.Angle(surface.Corners[ta], surface.Corners[tb]) * radiusM)) };
                degree[a]++; degree[b]++;
            }
            for (int i = 0; i < v.Length; i++)
                surface.Cells[i] = new Cell { Id = i, Position = v[i], AreaM2 = Math.Max(1, (long)Math.Round(area[i] * radiusM * radiusM)), Neighbors = new int[degree[i]], DistanceM = new int[degree[i]] };
            Array.Clear(degree, 0, degree.Length);
            foreach (var e in surface.Edges)
            {
                int distance = Math.Max(1, D.M(D.Angle(v[e.A], v[e.B]) * radiusM));
                int a = degree[e.A]++, b = degree[e.B]++;
                surface.Cells[e.A].Neighbors[a] = e.B; surface.Cells[e.A].DistanceM[a] = distance;
                surface.Cells[e.B].Neighbors[b] = e.A; surface.Cells[e.B].DistanceM[b] = distance;
            }
            for (int i = 0; i < v.Length; i++) Array.Sort(surface.Cells[i].Neighbors, surface.Cells[i].DistanceM);
            token.ThrowIfCancellationRequested(); return surface;
        }
        public sealed class Geometry { public V[] Positions; public int[] Triangles; }
        // Independently constructed Fibonacci sampling and stereographic triangulation.
        // Count includes the north-pole closure vertex; the coarse plate mesh always has exactly 20,000 cells.
        public static Geometry Fibonacci(int count, double irregularity, int seed, string stream, CancellationToken token = default)
        {
            if (count < 4 || count > 2560000) throw new ArgumentOutOfRangeException(nameof(count));
            var positions = new V[count]; var flat = new IPoint[count - 1];
            double turn = D.Pi * (3 - D.Sqrt(5)), spacing = D.Sqrt(4 * D.Pi / count);
            var rng = new Rng(seed, stream);
            for (int i = 0; i < count - 1; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                double z = -1 + 2 * (i + .5) / (count - 1), radial = D.Sqrt(1 - z * z), longitude = i * turn;
                var p = new V(radial * D.Sin(longitude + D.Pi / 2), radial * D.Sin(longitude), z);
                var east = V.Tangent(p, new V(-p.Y, p.X, 0)); var north = V.Cross(p, east);
                p = (p + (east * (rng.Unit() - rng.Unit()) + north * (rng.Unit() - rng.Unit())) * (spacing * irregularity * .4)).Unit;
                positions[i] = p; double denominator = Math.Max(1e-12, 1 - p.Z);
                flat[i] = new Point(D.Q(p.X / denominator, 1e9), D.Q(p.Y / denominator, 1e9));
            }
            positions[count - 1] = new V(0, 0, 1);
            var delaunay = new Delaunator(flat); token.ThrowIfCancellationRequested();
            var faces = new List<int>(delaunay.Triangles.Length + delaunay.Hull.Length * 3);
            void Face(int a, int b, int c)
            {
                if (V.Dot(V.Cross(positions[b] - positions[a], positions[c] - positions[a]), positions[a]) < 0) { int swap = b; b = c; c = swap; }
                faces.Add(a); faces.Add(b); faces.Add(c);
            }
            for (int i = 0; i < delaunay.Triangles.Length; i += 3) Face(delaunay.Triangles[i], delaunay.Triangles[i + 1], delaunay.Triangles[i + 2]);
            for (int i = 0; i < delaunay.Hull.Length; i++) Face(delaunay.Hull[i], delaunay.Hull[(i + 1) % delaunay.Hull.Length], count - 1);
            return new Geometry { Positions = positions, Triangles = faces.ToArray() };
        }
        static long Key(int a, int b) => ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
        static double TriangleArea(V a, V b, V c) => 2 * D.Atan(Math.Abs(V.Dot(a, V.Cross(b, c))) / Math.Max(1e-15, 1 + V.Dot(a, b) + V.Dot(b, c) + V.Dot(c, a)));
    }
    public static class Graph
    {
        public static int[] Components(Surface s, Func<int, bool> included, CancellationToken token = default)
        {
            var labels = Enumerable.Repeat(-1, s.Cells.Length).ToArray(); int id = 0; var q = new Queue<int>();
            for (int i = 0; i < labels.Length; i++) if (labels[i] < 0 && included(i))
            { token.ThrowIfCancellationRequested(); labels[i] = id; q.Enqueue(i); while (q.Count > 0) { int c = q.Dequeue(); if ((c & 1023) == 0) token.ThrowIfCancellationRequested(); foreach (int n in s.Cells[c].Neighbors) if (labels[n] < 0 && included(n)) { labels[n] = id; q.Enqueue(n); } } id++; }
            return labels;
        }
        public static int[] Distances(Surface s, IEnumerable<int> seeds, out int[] nearest, CancellationToken token = default)
        {
            var dist = Enumerable.Repeat(int.MaxValue, s.Cells.Length).ToArray(); nearest = Enumerable.Repeat(-1, s.Cells.Length).ToArray(); var heap = new Heap();
            foreach (int i in seeds.OrderBy(i => i)) { dist[i] = 0; nearest[i] = i; heap.Push(0, i, i); }
            while (heap.Count > 0)
            {
                if ((heap.Count & 1023) == 0) token.ThrowIfCancellationRequested();
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
