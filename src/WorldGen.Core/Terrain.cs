using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public static class Terrain
    {
        public static double Noise(V p, int seed, int frequency)
        {
            double x = (p.X + 2) * frequency, y = (p.Y + 2) * frequency, z = (p.Z + 2) * frequency; int ix = (int)x, iy = (int)y, iz = (int)z;
            double fx = x - ix, fy = y - iy, fz = z - iz; fx *= fx * (3 - 2 * fx); fy *= fy * (3 - 2 * fy); fz *= fz * (3 - 2 * fz); double value = 0;
            for (int a = 0; a < 2; a++) for (int b = 0; b < 2; b++) for (int c = 0; c < 2; c++)
            { uint h = Rng.Mix(unchecked((uint)(seed ^ (ix + a) * 73856093 ^ (iy + b) * 19349663 ^ (iz + c) * 83492791))); value += (h / 4294967295.0 * 2 - 1) * (a == 0 ? 1 - fx : fx) * (b == 0 ? 1 - fy : fy) * (c == 0 ? 1 - fz : fz); }
            return D.Q(value, 1e6);
        }
        public static void Build(World w, CancellationToken token)
        {
            var r = w.Recipe; var s = w.Surface; int n = s.Cells.Length; var features = new List<Feature>(); var height = new double[n];
            var coast = Enumerable.Range(0, n).Where(i => s.Cells[i].Neighbors.Any(j => (w.Continent[i] >= 0) != (w.Continent[j] >= 0))).ToArray();
            w.CoastDistanceM = Graph.Distances(s, coast, out _);
            w.FoldPermille = new int[n]; w.CratonPermille = new int[n]; w.BasinPermille = new int[n]; w.PlateauPermille = new int[n];
            var deadDistance = Graph.Distances(s, w.Boundaries.Where(b => b.Context == CollisionContext.DeadEnd).SelectMany(b => new[] { s.Edges[b.EdgeId].A, s.Edges[b.EdgeId].B }), out _);
            for (int i = 0; i < n; i++)
            {
                var p = s.Cells[i].Position; double distance = w.CoastDistanceM[i]; double broad = Noise(p, r.Seed, 6), detail = Noise(p, r.Seed ^ 57291, 30);
                double coastNoise = Noise(p, r.Seed ^ 741, 16) * r.Relief * 500;
                double fold = D.Clamp(w.Stress[i] / 1700.0), plateau = D.Kernel(deadDistance[i], 1600000) * r.CollisionStrength, basin = D.Clamp(-broad) * (.8 - .5 * fold), craton = (1 - fold) * (1 - D.Kernel(w.BoundaryDistanceM[i], 800000));
                double sum = Math.Max(.001, fold + plateau + basin + craton); w.FoldPermille[i] = (int)(fold / sum * 1000); w.PlateauPermille[i] = (int)(plateau / sum * 1000); w.BasinPermille[i] = (int)(basin / sum * 1000); w.CratonPermille[i] = 1000 - w.FoldPermille[i] - w.PlateauPermille[i] - w.BasinPermille[i];
                if (w.Continent[i] >= 0) height[i] = 120 + Math.Min(700, distance * .0006) + r.Relief * (broad * 550 + detail * 180) + coastNoise * D.Kernel(distance, 350000);
                else
                { double shelf = 170000 + 450000 * r.NearStrait; double slope = D.Clamp((distance - shelf * .35) / (shelf + 500000)); height[i] = -120 - 4400 * slope * slope * (3 - 2 * slope) + r.OceanDetail * (broad * 400 + detail * 100) + coastNoise * D.Kernel(distance, 300000); }
                if (w.Continent[i] >= 0) height[i] += w.CratonPermille[i] * .18 - w.BasinPermille[i] * .2 + w.PlateauPermille[i] * .35;
            }
            Feature Add(FeatureKind kind, Boundary b, V center, V axis, int amp, int width, int owner = -1)
            { var f = new Feature { Id = features.Count, Name = kind + " " + features.Count, Kind = kind, BoundaryId = b?.Id ?? -1, Center = center, Axis = axis, AmplitudeM = amp, WidthM = width, OwnerMicroplate = owner, AgeMyr = b?.AgeMyr ?? 0 }; features.Add(f); return f; }
            // One semantic record per boundary kernel. The same record supplies terrain and inspection.
            foreach (var b in w.Boundaries)
            {
                var e = s.Edges[b.EdgeId]; var at = (s.Cells[e.A].Position + s.Cells[e.B].Position).Unit; var normal = V.Tangent(at, s.Cells[e.B].Position - s.Cells[e.A].Position); var axis = V.Cross(at, normal);
                double stress = D.Clamp(b.Stress / 1000.0, 0, 2); bool bothLand = w.Continent[e.A] >= 0 && w.Continent[e.B] >= 0;
                if (b.Ghost)
                {
                    if (Rng.Value(r.Seed, "ghost-feature", b.MicroA * 503 + b.MicroB) > r.GhostFrequency) continue;
                    int amplitude = D.M((b.FormerKind == BoundaryKind.Convergent ? 1500 : b.FormerKind == BoundaryKind.Divergent ? -350 : 100) * stress * r.GhostStrength * (1 - .8 * r.GhostAge));
                    Add(b.FormerKind == BoundaryKind.Convergent ? FeatureKind.AncientOrogen : b.FormerKind == BoundaryKind.Divergent ? FeatureKind.AncientRift : FeatureKind.TransformCorridor, b, at, axis, amplitude, r.GhostWidthKm * 1000); continue;
                }
                if (b.Kind == BoundaryKind.Convergent && bothLand)
                {
                    double special = Rng.Value(r.Seed, "collision-feature", b.MicroA * 503 + b.MicroB) < r.CollisionFrequency ? r.CollisionStrength : 0;
                    double dead = 1 - b.EscapePermille / 1000.0; int amp = D.M((1900 + 3200 * dead * special) * stress); int width = D.M(600000 + 500000 * dead * special);
                    Add(FeatureKind.CollisionBelt, b, at, axis, amp, width);
                    if (special > 0) Add(b.Context == CollisionContext.EscapeFace ? FeatureKind.EscapeBlocks : FeatureKind.Plateau, b, at, axis, D.M(1100 * dead * special * stress), width * 2);
                    // Flexural foreland lies on the denser underthrust continental side.
                    double side = w.Microplates[b.MicroA].DensityKgM3 > w.Microplates[b.MicroB].DensityKgM3 ? -1 : 1;
                    Add(FeatureKind.ForelandBasin, b, (at + normal * (side * .11)).Unit, axis, -D.M(1800 * stress * r.ForelandStrength), 450000, side < 0 ? b.MicroA : b.MicroB);
                }
                else if (b.Kind == BoundaryKind.Convergent)
                {
                    double side = b.SubductingMicroplate == b.MicroA ? -1 : 1;
                    Add(FeatureKind.Trench, b, (at + normal * (side * .025)).Unit, axis, -D.M(2400 * stress), 220000, b.SubductingMicroplate);
                    Add(w.Continent[e.A] < 0 && w.Continent[e.B] < 0 ? FeatureKind.IslandArc : FeatureKind.VolcanicArc, b, (at - normal * (side * .045)).Unit, axis, D.M((3400 + stress * 1400) * r.Volcanism), 340000, b.SubductingMicroplate == b.MicroA ? b.MicroB : b.MicroA);
                }
                else if (b.Kind == BoundaryKind.Divergent) Add(bothLand ? FeatureKind.Rift : FeatureKind.MidOceanRidge, b, at, axis, D.M((bothLand ? -1400 : 2300) * stress), 650000);
                else Add(FeatureKind.TransformCorridor, b, at, axis, -D.M(180 * r.TectonicIntensity), 170000);
            }
            // Sample compact kernels grouped by kind: weighted averages avoid uplift scaling with mesh edge density.
            foreach (var group in features.GroupBy(f => f.Kind).ToArray())
            {
                token.ThrowIfCancellationRequested(); var sum = new double[n]; var weights = new double[n]; var affected = group.ToDictionary(f => f.Id, f => new List<int>());
                foreach (var f in group)
                {
                    double angular = f.WidthM / (double)(r.RadiusKm * 1000); double chordLimit = angular * angular * .65;
                    for (int i = 0; i < n; i++)
                    {
                        var p = s.Cells[i].Position; double dot = 1 - V.Dot(p, f.Center); if (dot > chordLimit) continue;
                        if (f.OwnerMicroplate >= 0 && w.CellMicroplate[i] != f.OwnerMicroplate) continue;
                        double distance = D.Angle(p, f.Center) * r.RadiusKm * 1000; if (distance >= f.WidthM) continue;
                        double envelope = D.Kernel(distance, f.WidthM); double value = f.AmplitudeM;
                        if (f.Kind == FeatureKind.CollisionBelt)
                        {
                            var across = V.Cross(f.Center, f.Axis); double cross = V.Dot(p - f.Center, across), along = V.Dot(p - f.Center, f.Axis);
                            double warp = Noise(p, r.Seed, 14) * .012; double ridge = .5 + .5 * D.Sin((cross + warp) * 180 + Noise(p, r.Seed, 8) * 2);
                            double passes = .65 + .35 * (.5 + .5 * D.Sin(along * 90 + f.BoundaryId * .017)); value *= .55 + .7 * ridge * ridge * passes;
                        }
                        else if (f.Kind == FeatureKind.EscapeBlocks) value -= 1200 * (.5 + .5 * Noise(p, r.Seed, 23));
                        else if (f.Kind == FeatureKind.Plateau) value *= .8 + .2 * Noise(p, r.Seed, 11);
                        else if (f.Kind == FeatureKind.AncientOrogen) value *= .8 + .2 * Noise(p, r.Seed, 6);
                        sum[i] += value * envelope; weights[i] += envelope; affected[f.Id].Add(i);
                    }
                }
                for (int i = 0; i < n; i++) if (weights[i] > 0) height[i] += sum[i] / Math.Max(1, weights[i]);
                foreach (var f in group) f.Cells = affected[f.Id].ToArray();
            }
            for (int k = 0; k < D.M(r.Volcanism * 14); k++)
            {
                var rng = new Rng(r.Seed, "hotspot", k); int cell = (int)(rng.Next() % (uint)n); var at = s.Cells[cell].Position; var motion = V.Cross(w.Microplates[w.CellMicroplate[cell]].Motion, at).Unit;
                for (int age = 0; age < 5; age++)
                {
                    var center = (at - motion * (age * .055)).Unit; int amplitude = D.M((height[cell] < 0 ? 6500 : 2200) * r.Volcanism * (1 - age * .13));
                    var f = Add(age == 0 ? FeatureKind.Hotspot : FeatureKind.IslandChain, null, center, motion, amplitude, age == 0 ? 450000 : 280000); f.AgeMyr = age * 12; var cells = new List<int>();
                    for (int i = 0; i < n; i++) { double d = D.Angle(s.Cells[i].Position, center) * r.RadiusKm * 1000; if (d < f.WidthM) { height[i] += f.AmplitudeM * D.Kernel(d, f.WidthM); cells.Add(i); } }
                    f.Cells = cells.ToArray();
                }
                if (k % 4 == 0) { var f = Add(FeatureKind.LargeIgneousProvince, null, at, motion, D.M(850 * r.Volcanism), 1100000); var cells = new List<int>(); for (int i = 0; i < n; i++) { double d = D.Angle(s.Cells[i].Position, at) * r.RadiusKm * 1000; if (d < f.WidthM) { height[i] += f.AmplitudeM * D.Kernel(d, f.WidthM); cells.Add(i); } } f.Cells = cells.ToArray(); }
            }
            // Stable shallow sills between nearby distinct continental identities.
            var bridges = new List<int>();
            for (int i = 0; i < n; i++) if (w.Continent[i] < 0 && w.CoastDistanceM[i] < 800000)
            {
                var identities = s.Cells[i].Neighbors.Select(j => w.Continent[j]).Where(c => c >= 0).Distinct().ToArray();
                if (identities.Length >= 2)
                { double u = Rng.Value(r.Seed, "land-bridge", i); if (u < r.Isthmus * r.Connectedness) { height[i] = Math.Max(height[i], 80 + 120 * u); bridges.Add(i); } else if (u < r.NearStrait) { height[i] = Math.Max(height[i], -80 - 160 * u); bridges.Add(i); } }
            }
            if (bridges.Count > 0) Add(FeatureKind.LandBridge, null, s.Cells[bridges[0]].Position, new V(), 0, 0).Cells = bridges.ToArray();
            var shelves = Enumerable.Range(0, n).Where(i => w.Continent[i] < 0 && height[i] >= -350).ToArray(); if (shelves.Length > 0) Add(FeatureKind.Shelf, null, s.Cells[shelves[0]].Position, new V(), 0, 0).Cells = shelves;
            var slopes = Enumerable.Range(0, n).Where(i => w.Continent[i] < 0 && height[i] < -350 && height[i] > -4000).ToArray(); if (slopes.Length > 0) Add(FeatureKind.Slope, null, s.Cells[slopes[0]].Position, new V(), 0, 0).Cells = slopes;
            w.TectonicM = height.Select(D.M).ToArray(); w.Features = features.ToArray(); ProtectInlandSeas(w);
            // A bounded tangent-space warp blends neighboring heights; authoritative and retained before erosion.
            w.PreErosionM = (int[])w.TectonicM.Clone();
            for (int i = 0; i < n; i++) if (!w.ProtectedInlandSea[i])
            { var p = s.Cells[i].Position; V dir = V.Tangent(p, new V(Noise(p, r.Seed, 9), Noise(p, r.Seed ^ 123, 9), Noise(p, r.Seed ^ 731, 9))); int nb = s.Cells[i].Neighbors.OrderByDescending(j => V.Dot(V.Tangent(p, s.Cells[j].Position), dir)).ThenBy(j => j).First(); w.PreErosionM[i] = D.M(w.TectonicM[i] * (1 - r.Relief * .12) + w.TectonicM[nb] * r.Relief * .12); }
            w.ElevationM = (int[])w.PreErosionM.Clone();
        }
        public static void ProtectInlandSeas(World w)
        {
            var s = w.Surface; var r = w.Recipe; int n = s.Cells.Length; w.ProtectedInlandSea = new bool[n]; if (r.PreserveInlandSeas == 0) return;
            var labels = Graph.Components(s, i => w.TectonicM[i] < 0); int main = Graph.LargestComponent(s, labels); var list = w.Features.ToList();
            foreach (int id in labels.Where(i => i >= 0 && i != main).Distinct().OrderBy(i => i))
            {
                var cells = Enumerable.Range(0, n).Where(i => labels[i] == id).ToArray(); long area = cells.Sum(i => s.Cells[i].AreaM2); bool marine = cells.Any(i => w.Continent[i] < 0);
                bool enclosed = cells.SelectMany(i => s.Cells[i].Neighbors).Any(i => w.Continent[i] >= 0 && w.Stress[i] > 100);
                if (!marine || !enclosed || area < r.InlandSeaMinAreaKm2 * 1e6 || -cells.Min(i => w.TectonicM[i]) < r.InlandSeaMinDepthM) continue;
                foreach (int c in cells) w.ProtectedInlandSea[c] = true;
                list.Add(new Feature { Id = list.Count, Name = "Tectonic inland sea " + id, Kind = FeatureKind.InlandSea, Center = s.Cells[cells[0]].Position, Cells = cells });
            }
            w.Features = list.ToArray();
        }
    }
}
