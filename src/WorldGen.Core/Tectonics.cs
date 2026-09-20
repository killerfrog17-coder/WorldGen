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
                var ma = micro[a]; var mb = micro[b]; var parentA = w.Parents[ma.ParentAffinityId]; var parentB = w.Parents[mb.ParentAffinityId];
                V at = (s.Cells[e.A].Position + s.Cells[e.B].Position).Unit, normal = V.Tangent(at, s.Cells[e.B].Position - s.Cells[e.A].Position), along = V.Cross(at, normal);
                bool ghost = ma.ParentAffinityId == mb.ParentAffinityId;
                var delta = V.Cross((ghost ? ma.Motion : parentA.Motion) - (ghost ? mb.Motion : parentB.Motion), at);
                var old = V.Cross(ma.OriginalMotion - mb.OriginalMotion, at);
                var parent = V.Cross(parentA.Motion - parentB.Motion, at);
                int nv = D.M(V.Dot(delta, normal) * 1000), tv = D.M(V.Dot(delta, along) * 1000);
                int oldNormal = D.M(V.Dot(old, normal) * 1000), oldShear = D.M(V.Dot(old, along) * 1000);
                int stress = D.M((Math.Abs(V.Dot(parent, normal)) * .7 + Math.Abs(V.Dot(delta, normal)) * .5) * 1000);
                var boundary = new Boundary { Id = bounds.Count, EdgeId = e.Id, MicroA = a, MicroB = b, Ghost = ghost, Kind = ghost ? BoundaryKind.Inactive : Classify(nv, tv), FormerKind = Classify(oldNormal, oldShear), NormalMotion = nv, TangentialMotion = tv, Stress = stress };
                if (boundary.Kind == BoundaryKind.Convergent)
                {
                    if (ma.ContinentId < 0 || mb.ContinentId < 0)
                        boundary.SubductingMicroplate = ma.DensityKgM3 >= mb.DensityKgM3 ? a : b;
                }
                bounds.Add(boundary);
            }
            w.Boundaries = bounds.ToArray();
            w.BoundaryDistanceM = Graph.Distances(s, bounds.Where(b => !b.Ghost).SelectMany(b => new[] { s.Edges[b.EdgeId].A, s.Edges[b.EdgeId].B }), out var nearest, token);
            var local = new int[n]; foreach (var b in bounds.Where(b => !b.Ghost)) { var e = s.Edges[b.EdgeId]; local[e.A] = Math.Max(local[e.A], b.Stress); local[e.B] = Math.Max(local[e.B], b.Stress); }
            w.Stress = new int[n]; for (int i = 0; i < n; i++) w.Stress[i] = nearest[i] < 0 ? 0 : D.M(local[nearest[i]] * D.Kernel(w.BoundaryDistanceM[i], 1500000));
            w.Metric("active_boundary_segments", bounds.Count(b => !b.Ghost)); w.Metric("ghost_boundary_segments", bounds.Count(b => b.Ghost));
            token.ThrowIfCancellationRequested();
        }
    }
}
