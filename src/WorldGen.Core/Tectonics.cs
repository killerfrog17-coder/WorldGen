using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public static class Tectonics
    {
        public static BoundaryKind Classify(int normal, int shear) => Math.Abs(normal) < Math.Max(80, Math.Abs(shear) / 3) ? BoundaryKind.Transform : normal > 0 ? BoundaryKind.Convergent : BoundaryKind.Divergent;
        public static CollisionContext Context(int distanceM, int scaleM) => distanceM < scaleM / 2 ? CollisionContext.EscapeFace : distanceM > scaleM ? CollisionContext.DeadEnd : CollisionContext.Intermediate;
        public static void Build(World w, CancellationToken token)
        {
            var r = w.Recipe; var s = w.Surface; var micro = w.Microplates; int n = s.Cells.Length;
            var bounds = new List<Boundary>();
            foreach (var e in s.Edges)
            {
                if ((e.Id & 4095) == 0) token.ThrowIfCancellationRequested();
                int a = w.CellMicroplate[e.A], b = w.CellMicroplate[e.B]; if (a == b) continue;
                V at = (s.Cells[e.A].Position + s.Cells[e.B].Position).Unit;
                bounds.Add(new Boundary { Id = bounds.Count, EdgeId = e.Id, MicroA = a, MicroB = b,
                    Ghost = micro[a].ParentAffinityId == micro[b].ParentAffinityId,
                    Normal = V.Tangent(at, s.Cells[e.B].Position - s.Cells[e.A].Position) });
            }
            var links = InterfaceLinks(w, bounds, token);
            var centers = bounds.Select(b => (s.Cells[s.Edges[b.EdgeId].A].Position + s.Cells[s.Edges[b.EdgeId].B].Position).Unit).ToArray();
            SmoothNormals(w, bounds, links, centers, token);
            var density = new int[w.Parents.Length];
            foreach (var p in w.Parents)
            {
                double mass = 0; long area = 0;
                foreach (int member in p.Microplates) { mass += micro[member].DensityKgM3 * (double)micro[member].AreaM2; area += micro[member].AreaM2; }
                density[p.Id] = D.M(mass / Math.Max(1, area));
            }
            foreach (var boundary in bounds)
            {
                if ((boundary.Id & 4095) == 0) token.ThrowIfCancellationRequested();
                int a = boundary.MicroA, b = boundary.MicroB;
                var ma = micro[a]; var mb = micro[b]; var parentA = w.Parents[ma.ParentAffinityId]; var parentB = w.Parents[mb.ParentAffinityId];
                V at = centers[boundary.Id], normal = boundary.Normal, along = V.Cross(at, normal);
                bool ghost = boundary.Ghost;
                var delta = V.Cross((ghost ? ma.Motion : parentA.Motion) - (ghost ? mb.Motion : parentB.Motion), at);
                var old = V.Cross(ma.OriginalMotion - mb.OriginalMotion, at);
                var parent = V.Cross(parentA.Motion - parentB.Motion, at);
                int nv = D.M(V.Dot(delta, normal) * 1000), tv = D.M(V.Dot(delta, along) * 1000);
                int oldNormal = D.M(V.Dot(old, normal) * 1000), oldShear = D.M(V.Dot(old, along) * 1000);
                int stress = D.M((Math.Abs(V.Dot(parent, normal)) * .7 + Math.Abs(V.Dot(delta, normal)) * .5) * 1000);
                boundary.Kind = ghost ? BoundaryKind.Inactive : Classify(nv, tv); boundary.FormerKind = Classify(oldNormal, oldShear);
                boundary.NormalMotion = nv; boundary.TangentialMotion = tv; boundary.Stress = stress;
                if (boundary.Kind == BoundaryKind.Convergent)
                {
                    // Parent enclave repair may have changed crust labels after
                    // density assignment. Oceanic crust still underthrusts the
                    // continent; density only resolves ocean/ocean polarity.
                    if (ma.ContinentId < 0 && mb.ContinentId >= 0) boundary.SubductingMicroplate = a;
                    else if (mb.ContinentId < 0 && ma.ContinentId >= 0) boundary.SubductingMicroplate = b;
                    else if (ma.ContinentId < 0 && mb.ContinentId < 0)
                        boundary.SubductingMicroplate = density[parentA.Id] > density[parentB.Id] || density[parentA.Id] == density[parentB.Id] && parentA.Id < parentB.Id ? a : b;
                    if (boundary.SubductingMicroplate >= 0) boundary.OverridingMicroplate = boundary.SubductingMicroplate == a ? b : a;
                }
            }
            BuildBelts(w, bounds, links, centers, token);
            w.Boundaries = bounds.ToArray();
            w.BoundaryDistanceM = Graph.Distances(s, bounds.Where(b => !b.Ghost).SelectMany(b => new[] { s.Edges[b.EdgeId].A, s.Edges[b.EdgeId].B }), out var nearest, token);
            var local = new int[n]; foreach (var b in bounds.Where(b => !b.Ghost)) { var e = s.Edges[b.EdgeId]; local[e.A] = Math.Max(local[e.A], b.Stress); local[e.B] = Math.Max(local[e.B], b.Stress); }
            w.Stress = new int[n]; for (int i = 0; i < n; i++) w.Stress[i] = nearest[i] < 0 ? 0 : D.M(local[nearest[i]] * D.Kernel(w.BoundaryDistanceM[i], 1500000));
            w.Metric("active_boundary_segments", bounds.Count(b => !b.Ghost)); w.Metric("ghost_boundary_segments", bounds.Count(b => b.Ghost));
            token.ThrowIfCancellationRequested();
        }

        // Two dual edges are consecutive only when they share a mesh triangle.
        // Nearby but disconnected margins must never share orientation or phase.
        static int[][] InterfaceLinks(World w, List<Boundary> bounds, CancellationToken token)
        {
            var triangles = new Dictionary<int, List<int>>();
            foreach (var b in bounds)
            {
                if ((b.Id & 1023) == 0) token.ThrowIfCancellationRequested();
                if (b.Ghost) continue;
                var e = w.Surface.Edges[b.EdgeId];
                Add(e.TriangleA, b.Id); Add(e.TriangleB, b.Id);
            }
            void Add(int triangle, int id)
            {
                if (!triangles.TryGetValue(triangle, out var list)) triangles.Add(triangle, list = new List<int>(3));
                list.Add(id);
            }
            var links = new int[bounds.Count][];
            foreach (var b in bounds)
            {
                if ((b.Id & 1023) == 0) token.ThrowIfCancellationRequested();
                var adjacent = new List<int>(2);
                if (!b.Ghost)
                {
                    var e = w.Surface.Edges[b.EdgeId];
                    AddNeighbors(e.TriangleA); AddNeighbors(e.TriangleB);
                    void AddNeighbors(int triangle)
                    {
                        foreach (int id in triangles[triangle])
                            if (id != b.Id && SameParents(w, b, bounds[id]) && !adjacent.Contains(id)) adjacent.Add(id);
                    }
                }
                adjacent.Sort(); links[b.Id] = adjacent.ToArray();
            }
            return links;
        }
        static bool SameParents(World w, Boundary a, Boundary b)
        {
            int a0 = w.Microplates[a.MicroA].ParentAffinityId, a1 = w.Microplates[a.MicroB].ParentAffinityId;
            int b0 = w.Microplates[b.MicroA].ParentAffinityId, b1 = w.Microplates[b.MicroB].ParentAffinityId;
            return a0 == b0 && a1 == b1 || a0 == b1 && a1 == b0;
        }
        static int Step(World w, V a, V b) => Math.Max(1, D.M((a - b).Length * w.Recipe.RadiusKm * 1000));
        static void SmoothNormals(World w, List<Boundary> bounds, int[][] links, V[] centers, CancellationToken token)
        {
            const int width = 250000;
            var raw = bounds.Select(b => b.Normal).ToArray();
            var distance = new int[bounds.Count]; var stamp = new int[bounds.Count];
            foreach (var b in bounds)
            {
                if ((b.Id & 127) == 0) token.ThrowIfCancellationRequested();
                if (b.Ghost) continue;
                int visit = b.Id + 1, owner = w.Microplates[b.MicroA].ParentAffinityId;
                var heap = new Heap(); heap.Push(0, b.Id); stamp[b.Id] = visit; distance[b.Id] = 0;
                V sum = new V(); double weights = 0;
                while (heap.Count > 0)
                {
                    var item = heap.Pop(); if (item.Key != distance[item.Id]) continue;
                    var other = bounds[item.Id]; double sign = w.Microplates[other.MicroA].ParentAffinityId == owner ? 1 : -1;
                    double weight = w.Surface.Edges[other.EdgeId].LengthM * D.Kernel(item.Key, width);
                    sum += V.Transport(raw[item.Id] * sign, centers[item.Id], centers[b.Id]) * weight; weights += weight;
                    foreach (int j in links[item.Id])
                    {
                        int next = (int)item.Key + Step(w, centers[item.Id], centers[j]);
                        if (next >= width || stamp[j] == visit && next >= distance[j]) continue;
                        stamp[j] = visit; distance[j] = next; heap.Push(next, j);
                    }
                }
                if (weights > 0 && sum.Length / weights > .1) b.Normal = V.Tangent(centers[b.Id], sum);
            }
        }
        static void BuildBelts(World w, List<Boundary> bounds, int[][] links, V[] centers, CancellationToken token)
        {
            var visited = new bool[bounds.Count]; int count = 0;
            foreach (var seed in bounds)
            {
                if (seed.Ghost || visited[seed.Id]) continue;
                token.ThrowIfCancellationRequested(); count++;
                var queue = new Queue<int>(); var ends = new List<int>();
                queue.Enqueue(seed.Id); visited[seed.Id] = true;
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue(); if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                    var b = bounds[i]; b.BeltId = seed.Id; b.AlongM = int.MaxValue; b.EndDistanceM = int.MaxValue;
                    int degree = 0;
                    foreach (int j in links[i]) if (Compatible(b, bounds[j]))
                    { degree++; if (!visited[j]) { visited[j] = true; queue.Enqueue(j); } }
                    if (degree < 2) ends.Add(i);
                }
                ends.Sort(); int start = ends.Count > 0 ? ends[0] : seed.Id;
                Distances(new[] { start }, false);
                if (ends.Count > 0) Distances(ends, true);
                void Distances(IEnumerable<int> sources, bool terminal)
                {
                    var heap = new Heap(); foreach (int id in sources) { Set(bounds[id], 0); heap.Push(0, id); }
                    while (heap.Count > 0)
                    {
                        var item = heap.Pop(); if (item.Key != Get(bounds[item.Id])) continue;
                        if ((item.Id & 255) == 0) token.ThrowIfCancellationRequested();
                        foreach (int j in links[item.Id])
                        {
                            if (bounds[j].BeltId != seed.Id) continue;
                            int next = (int)item.Key + Step(w, centers[item.Id], centers[j]);
                            if (next >= Get(bounds[j])) continue; Set(bounds[j], next); heap.Push(next, j);
                        }
                    }
                    int Get(Boundary b) => terminal ? b.EndDistanceM : b.AlongM;
                    void Set(Boundary b, int value) { if (terminal) b.EndDistanceM = value; else b.AlongM = value; }
                }
            }
            bool Compatible(Boundary a, Boundary b) => a.Kind == b.Kind &&
                (a.SubductingMicroplate < 0 ? -1 : w.Microplates[a.SubductingMicroplate].ParentAffinityId) ==
                (b.SubductingMicroplate < 0 ? -1 : w.Microplates[b.SubductingMicroplate].ParentAffinityId);
            w.Metric("tectonic_boundary_belts", count);
        }
    }
}
