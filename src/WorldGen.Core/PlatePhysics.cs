using System;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public static class PlatePhysics
    {
        public static void Apply(World w, CancellationToken token)
        {
            var plates = w.Microplates; double meanArea = plates.Average(p => (double)p.AreaM2);
            var torque = new V[plates.Length]; var length = new double[plates.Length];
            foreach (var boundary in w.Surface.Edges)
            {
                int a = w.CellMicroplate[boundary.A], b = w.CellMicroplate[boundary.B]; if (a == b) continue;
                V at = (w.Surface.Cells[boundary.A].Position + w.Surface.Cells[boundary.B].Position).Unit;
                V normal = V.Tangent(at, w.Surface.Cells[boundary.B].Position - w.Surface.Cells[boundary.A].Position);
                double closing = V.Dot(V.Cross(plates[a].OriginalMotion - plates[b].OriginalMotion, at), normal);
                double pullA = closing > 0 && plates[a].ContinentId < 0 && plates[a].DensityKgM3 > plates[b].DensityKgM3 ? .35 * closing : 0;
                double pullB = closing > 0 && plates[b].ContinentId < 0 && plates[b].DensityKgM3 > plates[a].DensityKgM3 ? .35 * closing : 0;
                double ridge = closing < 0 ? closing * .12 : 0;
                torque[a] += V.Cross(at, normal) * ((pullA + ridge) * boundary.LengthM / 1e6);
                torque[b] -= V.Cross(at, normal) * ((pullB + ridge) * boundary.LengthM / 1e6);
                length[a] += boundary.LengthM / 1e6; length[b] += boundary.LengthM / 1e6;
            }
            foreach (var p in plates)
            {
                token.ThrowIfCancellationRequested();
                double drag = p.ContinentId >= 0 ? .6 : 1;
                double size = D.Clamp(D.Sqrt(meanArea / p.AreaM2), .5, 1.6);
                V mantle = new V(Terrain.Noise(p.Centroid, w.Recipe.Seed ^ 1541, 2), Terrain.Noise(p.Centroid, w.Recipe.Seed ^ 3187, 2), Terrain.Noise(p.Centroid, w.Recipe.Seed ^ 7253, 2));
                p.Motion = p.OriginalMotion * (drag * size) + torque[p.Id] / Math.Max(1, length[p.Id]) + mantle * .25;
            }
            w.MantlePermille = w.Surface.Cells.Select(c => D.M(Terrain.Noise(c.Position, w.Recipe.Seed ^ 41533, 2) * 1000)).ToArray();
        }
    }
}
