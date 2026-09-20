using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public static class Terrain
    {
        public static double Noise(V p, int seed, int frequency) => SphereNoise.Sample(p, seed, frequency);
        public static void Build(World w, CancellationToken token)
        {
            var s = w.Surface; var r = w.Recipe; int n = s.Cells.Length;
            var coast = Enumerable.Range(0, n).Where(i => s.Cells[i].Neighbors.Any(j => (w.Continent[i] >= 0) != (w.Continent[j] >= 0))).ToArray();
            w.CoastDistanceM = Graph.Distances(s, coast, out _, token);
            var collision = new SortedDictionary<int, Boundary>(); var spreading = new SortedDictionary<int, Boundary>(); var transforms = new SortedDictionary<int, Boundary>();
            void Seed(SortedDictionary<int, Boundary> sources, int cell, Boundary b)
            { if (!sources.TryGetValue(cell, out var old) || b.Stress > old.Stress || b.Stress == old.Stress && b.Id < old.Id) sources[cell] = b; }
            foreach (var b in w.Boundaries)
            {
                if (b.Ghost) continue;
                var edge = s.Edges[b.EdgeId]; var target = b.Kind == BoundaryKind.Convergent ? collision : b.Kind == BoundaryKind.Divergent ? spreading : transforms;
                Seed(target, edge.A, b); Seed(target, edge.B, b);
            }
            var convergenceDistance = Graph.Distances(s, collision.Keys, out var convergenceSource, token);
            var divergenceDistance = Graph.Distances(s, spreading.Keys, out var divergenceSource, token);
            var transformDistance = Graph.Distances(s, transforms.Keys, out _, token);
            w.FoldPermille = new int[n]; w.CratonPermille = new int[n]; w.BasinPermille = new int[n]; w.PlateauPermille = new int[n];
            var heights = new double[n]; var features = new List<Feature>();
            var featureCells = new SortedDictionary<long, List<int>>();
            var featureRecords = new SortedDictionary<long, Feature>();
            void FeatureCell(FeatureKind kind, Boundary b, int cell, double amplitude, int width)
            {
                // Retain one feature per plate pair and kind, in stable first-cell order.
                int parentA = w.Microplates[b.MicroA].ParentAffinityId, parentB = w.Microplates[b.MicroB].ParentAffinityId;
                if (parentA > parentB) { int swap = parentA; parentA = parentB; parentB = swap; }
                long key = ((long)(int)kind << 40) | ((long)parentA << 20) | (uint)parentB;
                if (!featureCells.ContainsKey(key))
                {
                    var edge = s.Edges[b.EdgeId]; var center = (s.Cells[edge.A].Position + s.Cells[edge.B].Position).Unit;
                    featureCells[key] = new List<int>();
                    featureRecords[key] = new Feature { BoundaryId = b.Id, Kind = kind, Center = center, Axis = V.Cross(center, V.Tangent(center, s.Cells[edge.B].Position - s.Cells[edge.A].Position)), AmplitudeM = D.M(amplitude), WidthM = width };
                }
                featureCells[key].Add(cell);
            }
            for (int i = 0; i < n; i++)
            {
                if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                V p = s.Cells[i].Position; bool land = w.Continent[i] >= 0;
                double shore = w.CoastDistanceM[i], compression = convergenceDistance[i], extension = divergenceDistance[i];
                double broad = Noise(p, r.Seed, 3), province = Noise(p, r.Seed ^ 7613, 7), detail = Noise(p, r.Seed ^ 19277, 28);
                double fold = D.Kernel(compression, 1300000) * D.Clamp(w.Stress[i] / 700.0);
                double craton = (1 - fold) * (1 - D.Kernel(w.BoundaryDistanceM[i], 1200000));
                double basin = (1 - fold) * D.Clamp(-province * 1.7);
                double plateau = fold * D.Clamp(shore / 1700000);
                double total = Math.Max(.001, fold + craton + basin + plateau);
                w.FoldPermille[i] = (int)(1000 * fold / total); w.BasinPermille[i] = (int)(1000 * basin / total); w.PlateauPermille[i] = (int)(1000 * plateau / total);
                w.CratonPermille[i] = 1000 - w.FoldPermille[i] - w.BasinPermille[i] - w.PlateauPermille[i];
                double coastMask = D.Kernel(shore, 450000);
                double h;
                if (land)
                {
                    h = 70 + 750 * (1 - D.Kernel(shore, 1800000));
                    // Continental crust has a coherent buoyant basement before
                    // localized rifts, basins and texture are applied. This keeps
                    // continent shape controlled by the structural plate field.
                    double interior = D.Clamp((shore - 700000) / 1800000);
                    h += 700 * interior * (.65 + .35 * craton);
                    double noiseGate = D.Clamp(h / 400.0) * (.35 + .65 * interior);
                    h += (broad * 180 + province * 110) * noiseGate * (1 - .6 * craton) - basin * 420;
                }
                else
                {
                    double shelfWidth = 180000 + 100000 * (province + 1);
                    double t = D.Clamp((shore - shelfWidth * .3) / (shelfWidth + 650000));
                    h = -90 - 4800 * t * t * (3 - 2 * t) + 170 * broad + 65 * detail;
                }
                if (convergenceSource[i] >= 0)
                {
                    var b = collision[convergenceSource[i]]; var edge = s.Edges[b.EdgeId];
                    bool bothLand = w.Continent[edge.A] >= 0 && w.Continent[edge.B] >= 0;
                    V at = (s.Cells[edge.A].Position + s.Cells[edge.B].Position).Unit;
                    V normal = V.Tangent(at, s.Cells[edge.B].Position - s.Cells[edge.A].Position), along = V.Cross(at, normal);
                    double cross = V.Dot(p - at, normal) * r.RadiusKm * 1000, strike = V.Dot(p - at, along) * r.RadiusKm * 1000;
                    double power = D.Clamp(b.Stress / 950.0, .12, 1.6);
                    double phase = cross / 230000 * 2 * D.Pi + Noise(p, r.Seed ^ 3541, 11) * 3;
                    double ridge = .5 + .5 * D.Sin(phase), passes = .65 + .35 * (.5 + .5 * D.Sin(strike / 170000 + broad * 5));
                    if (bothLand && land)
                    {
                        double envelope = D.Kernel(compression, 1450000), uplift = 4700 * power * envelope;
                        double orogen = uplift * (.36 + .64 * ridge * ridge * passes);
                        h += orogen + 1600 * power * D.Kernel(compression, 2200000) * (1 - .65 * b.EscapePermille / 1000.0);
                        double foreland = D.Kernel(Math.Abs(compression - 1050000), 420000) * 550 * power;
                        h -= foreland;
                        if (envelope > .01) FeatureCell(FeatureKind.CollisionBelt, b, i, uplift, 1450000);
                        if (foreland > 40) FeatureCell(FeatureKind.ForelandBasin, b, i, -foreland, 420000);
                    }
                    else if (!bothLand)
                    {
                        bool underthrust = w.CellMicroplate[i] == b.SubductingMicroplate;
                        if (underthrust && !land)
                        {
                            double trench = 2100 * power * D.Kernel(compression, 360000); h -= trench;
                            if (trench > 30) FeatureCell(FeatureKind.Trench, b, i, -trench, 360000);
                        }
                        else
                        {
                            double arcEnvelope = D.Kernel(Math.Abs(compression - 250000), 530000);
                            double volcanoes = .35 + .65 * Math.Pow(.5 + .5 * D.Sin(strike / 160000 + province * 4), 2);
                            double arc = (land ? 3100 : 5800) * Math.Min(1.3, power) * arcEnvelope * volcanoes;
                            h += arc;
                            if (arcEnvelope > .01) FeatureCell(land ? FeatureKind.VolcanicArc : FeatureKind.IslandArc, b, i, arc, 530000);
                        }
                    }
                }
                if (divergenceSource[i] >= 0)
                {
                    var b = spreading[divergenceSource[i]]; double power = D.Clamp(b.Stress / 700.0, .1, 1.5), envelope = D.Kernel(extension, land ? 550000 : 1100000);
                    double relief = (land ? -700 : 2400) * power * envelope; h += relief;
                    if (envelope > .01) FeatureCell(land ? FeatureKind.Rift : FeatureKind.MidOceanRidge, b, i, relief, land ? 550000 : 1100000);
                }
                h -= 130 * D.Kernel(transformDistance[i], 200000);
                h += r.Relief / .4 * ((land ? 340 : 85) * detail * (1 - .7 * craton) + 120 * province * coastMask);
                h += w.MantlePermille[i] * (land ? .5 : .25);
                // Crust labels are distinct from coastlines; roughness may submerge a continental coast.
                if (land) h -= coastMask * Math.Max(0, -province) * 120;
                heights[i] = h;
            }
            foreach (var entry in featureRecords)
            { var f = entry.Value; f.Id = features.Count; f.Name = f.Kind + " " + f.Id; f.Cells = featureCells[entry.Key].ToArray(); features.Add(f); }
            int hotspots = 10;
            for (int k = 0; k < hotspots; k++)
            {
                var random = new Rng(r.Seed, "hotspots", k); V at = random.Vector().Unit; int cell = Erosion.Nearest(s, at);
                V drift = V.Cross(w.Microplates[w.CellMicroplate[cell]].Motion, at).Unit;
                for (int age = 0; age < 5; age++)
                {
                    V center = (at - drift * (age * .04)).Unit; int width = age == 0 ? 430000 : 300000;
                    double amplitude = (w.Continent[cell] >= 0 ? 2100 : 6300) * (1 - age * .16);
                    var cells = new List<int>();
                    for (int i = 0; i < n; i++)
                    {
                        if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
                        double chord = 1 - V.Dot(s.Cells[i].Position, center); if (chord > .01) continue;
                        double distance = D.Angle(s.Cells[i].Position, center) * r.RadiusKm * 1000;
                        if (distance >= width) continue;
                        heights[i] += amplitude * D.Kernel(distance, width); cells.Add(i);
                    }
                    features.Add(new Feature { Id = features.Count, Name = (age == 0 ? "Hotspot " : "Volcanic trail ") + k + "/" + age, Kind = age == 0 ? FeatureKind.Hotspot : FeatureKind.IslandChain, Center = center, Axis = drift, AmplitudeM = D.M(amplitude), WidthM = width, AgeMyr = age * 10, Cells = cells.ToArray() });
                }
                if (k % 4 == 0)
                {
                    var cells = new List<int>();
                    for (int i = 0; i < n; i++)
                    {
                        if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
                        if (1 - V.Dot(s.Cells[i].Position, at) > .025) continue;
                        double distance = D.Angle(s.Cells[i].Position, at) * r.RadiusKm * 1000;
                        if (distance < 1100000) { heights[i] += 650 * D.Kernel(distance, 1100000); cells.Add(i); }
                    }
                    features.Add(new Feature { Id = features.Count, Name = "Igneous province " + k, Kind = FeatureKind.LargeIgneousProvince, Center = at, WidthM = 1100000, AmplitudeM = 650, Cells = cells.ToArray() });
                }
            }
            w.TectonicM = heights.Select(h => D.M(h > 6000 ? 6000 + (h - 6000) * .18 : h)).ToArray(); w.Features = features.ToArray();
            w.ProtectedInlandSea = new bool[n];
            w.PreErosionM = (int[])w.TectonicM.Clone(); w.ElevationM = (int[])w.TectonicM.Clone();
        }
    }
}
