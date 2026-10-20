using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WorldGen.Core
{
    public static class Terrain
    {
        public static double Noise(V p, int seed, int frequency) => SphereNoise.Sample(p, seed, frequency);

        // Propagation stays on its source actor. Separate fields prevent an
        // unrelated nearby margin from supplying uplift on the wrong plate.
        sealed class Field
        {
            public int[] Distance, Source;
            public Field(World w, Func<Boundary, int, bool> eligible, int limit, CancellationToken token)
            {
                int n = w.Surface.Cells.Length;
                Distance = Enumerable.Repeat(int.MaxValue, n).ToArray(); Source = Enumerable.Repeat(-1, n).ToArray();
                var heap = new Heap();
                foreach (var b in w.Boundaries)
                {
                    var edge = w.Surface.Edges[b.EdgeId];
                    foreach (int i in new[] { edge.A, edge.B })
                    {
                        if (!eligible(b, i)) continue;
                        int d = D.M(D.Angle(w.Surface.Cells[edge.A].Position, w.Surface.Cells[edge.B].Position) * w.Recipe.RadiusKm * 500);
                        if (d < Distance[i] || d == Distance[i] && (Source[i] < 0 || b.Id < Source[i]))
                        { Distance[i] = d; Source[i] = b.Id; heap.Push(d, i, b.Id); }
                    }
                }
                int steps = 0;
                while (heap.Count > 0)
                {
                    if ((steps++ & 1023) == 0) token.ThrowIfCancellationRequested();
                    var item = heap.Pop(); int i = item.Id;
                    if (item.Key != Distance[i] || item.From != Source[i]) continue;
                    int owner = w.Microplates[w.CellMicroplate[i]].ParentAffinityId; var c = w.Surface.Cells[i];
                    for (int k = 0; k < c.Neighbors.Length; k++)
                    {
                        int j = c.Neighbors[k]; if (w.Microplates[w.CellMicroplate[j]].ParentAffinityId != owner) continue;
                        long d = item.Key + c.DistanceM[k]; if (d > limit) continue;
                        if (d < Distance[j] || d == Distance[j] && item.From < Source[j])
                        { Distance[j] = (int)d; Source[j] = item.From; heap.Push(d, j, item.From); }
                    }
                }
            }
        }

        public static void Build(World w, CancellationToken token)
        {
            var s = w.Surface; var r = w.Recipe; int n = s.Cells.Length;
            int Parent(int micro) => w.Microplates[micro].ParentAffinityId;
            int Owner(int cell) => Parent(w.CellMicroplate[cell]);
            bool Continental(Boundary b) { var e = s.Edges[b.EdgeId]; return w.Continent[e.A] >= 0 && w.Continent[e.B] >= 0; }
            bool Subduction(Boundary b) => !b.Ghost && b.Kind == BoundaryKind.Convergent && b.SubductingMicroplate >= 0 && b.OverridingMicroplate >= 0;
            var coast = Enumerable.Range(0, n).Where(i => s.Cells[i].Neighbors.Any(j => (w.Continent[i] >= 0) != (w.Continent[j] >= 0))).ToArray();
            w.CoastDistanceM = Graph.Distances(s, coast, out _, token);
            Field collision=null,upper=null,lower=null,spreading=null,oblique=null,inherited=null;
            Action[] fields={
                () => collision = new Field(w, (b, i) => !b.Ghost && b.Kind == BoundaryKind.Convergent && Continental(b), 1900000, token),
                () => upper = new Field(w, (b, i) => Subduction(b) && Owner(i) == Parent(b.OverridingMicroplate), 1500000, token),
                () => lower = new Field(w, (b, i) => Subduction(b) && Owner(i) == Parent(b.SubductingMicroplate), 500000, token),
                () => spreading = new Field(w, (b, i) => !b.Ghost && b.Kind == BoundaryKind.Divergent, 1100000, token),
                () => oblique = new Field(w, (b, i) => !b.Ghost && b.Kind == BoundaryKind.Transform && b.NormalMotion > 30, 500000, token),
                () => inherited = new Field(w, (b, i) => b.Ghost && b.FormerKind == BoundaryKind.Convergent && Rng.Value(r.Seed, "inherited-belt", Parent(b.MicroA)) < .24, 650000, token)
            };
            // Independent heaps; bounded concurrency limits peak working memory.
            if(Environment.ProcessorCount>1 && n>=20000)Parallel.Invoke(new ParallelOptions{CancellationToken=token,MaxDegreeOfParallelism=Math.Min(3,Environment.ProcessorCount)},fields);
            else foreach(var build in fields)build();
            w.FoldPermille = new int[n]; w.CratonPermille = new int[n]; w.BasinPermille = new int[n]; w.PlateauPermille = new int[n];
            var heights = new double[n]; var background = (((r.AlgorithmVersion == Recipe.CurrentAlgorithm || r.AlgorithmVersion == Recipe.PresetAlgorithm) || r.AlgorithmVersion == Recipe.SubcellAlgorithm) || r.AlgorithmVersion==Recipe.FeatureBlendAlgorithm) ? new double[n] : null; var features = new List<Feature>();
            var records = new SortedDictionary<long, Feature>(); var members = new SortedDictionary<long, List<int>>();
            void Add(FeatureKind kind, Boundary b, int i, double relief, int width, int age = 0)
            {
                if (Math.Abs(relief) < 35) return;
                int owner = Owner(i), belt = b.BeltId >= 0 ? b.BeltId : b.Id;
                long key = ((long)(int)kind << 48) | ((long)(uint)belt << 16) | (uint)owner;
                if (!records.TryGetValue(key, out var f))
                {
                    var e = s.Edges[b.EdgeId]; V center = (s.Cells[e.A].Position + s.Cells[e.B].Position).Unit;
                    f = new Feature { BoundaryId = b.Id, BeltId = b.BeltId, OwnerParent = owner, OwnerMicroplate = w.CellMicroplate[i], Kind = kind, Center = center, Axis = V.Cross(center, b.Normal), WidthM = width, AgeMyr = age };
                    records.Add(key, f); members.Add(key, new List<int>());
                }
                if (Math.Abs(relief) > Math.Abs(f.AmplitudeM)) f.AmplitudeM = D.M(relief);
                f.WidthM = Math.Max(f.WidthM, width);
                members[key].Add(i);
            }
            double Strength(Boundary b) => D.Clamp(Math.Max(0, b.NormalMotion) / 1100.0, 0, 1.35);
            bool variedBelts=(((r.AlgorithmVersion == Recipe.CurrentAlgorithm || r.AlgorithmVersion == Recipe.PresetAlgorithm) || r.AlgorithmVersion == Recipe.SubcellAlgorithm) || r.AlgorithmVersion==Recipe.FeatureBlendAlgorithm) || r.AlgorithmVersion==Recipe.RangeControlsAlgorithm;
            double MountainScale(Boundary b,V p)
            {
                double scale=r.MountainStrengthPercent/100;
                if(!variedBelts)return scale;
                int belt=b.BeltId>=0?b.BeltId:b.Id;
                double history=Rng.Value(r.Seed,"orogen-lifetime-329",belt);
                if(r.AlgorithmVersion==Recipe.CurrentAlgorithm)
                    return scale*D.Q(OrogenMorphology.Retention(history,.5+.85*Noise(p,r.Seed^37291,11)),1000000);
                double retention=.08+1.60*history*history;
                double segment=.35+1.05*D.Clamp(.5+.85*Noise(p,r.Seed^37291,11));
                return scale*6*D.Q(retention*segment,1000000);
            }
            double BeltWidth(Boundary b) => variedBelts ? .65+.65*Rng.Value(r.Seed,"orogen-width-329",b.BeltId>=0?b.BeltId:b.Id) : 1;
            // Spatial phase avoids resets at graph branches/short transform
            // interruptions; the field still follows the curved physical margin.
            double StrikeTexture(V p, int salt, int frequency) => .5 + .5 * Noise(p, r.Seed ^ salt, frequency);
            for (int i = 0; i < n; i++)
            {
                if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                V p = s.Cells[i].Position; bool land = w.Continent[i] >= 0;
                double shore = w.CoastDistanceM[i], broad = Noise(p, r.Seed, 3), province = Noise(p, r.Seed ^ 7613, 7), detail = Noise(p, r.Seed ^ 19277, 28);
                double fold = Math.Max(D.Kernel(collision.Distance[i],r.AlgorithmVersion==Recipe.CurrentAlgorithm?400000:700000), D.Kernel(upper.Distance[i],r.AlgorithmVersion==Recipe.CurrentAlgorithm?450000:650000)) * D.Clamp(w.Stress[i] / 700.0);
                double craton = (1 - fold) * (1 - D.Kernel(w.BoundaryDistanceM[i], 1200000));
                double basin = (1 - fold) * D.Clamp(-province * 1.7), plateauProvince = 0;
                double coastMask = D.Kernel(shore, 450000), h;
                if (land)
                {
                    h = 70 + 750 * (1 - D.Kernel(shore, 1800000));
                    double interior = D.Clamp((shore - 700000) / 1800000);
                    h += 700 * interior * (.65 + .35 * craton);
                    double noiseGate = D.Clamp(h / 400.0) * (.35 + .65 * interior);
                    h += (broad * 180 + province * 110) * noiseGate * (1 - .6 * craton) - basin * 420;
                }
                else
                {
                    // Broad uniform coast-distance ramps turn even irregular
                    // marine plates into concentric bowls. Margin character and
                    // coherent structural variation set separate shelf/slope widths.
                    double activeMargin = Math.Max(D.Kernel(lower.Distance[i], 600000), D.Kernel(upper.Distance[i], 600000));
                    double shelfWidth = (70000 + 360000 * StrikeTexture(p, 42617, 9)) * (1 - .55 * activeMargin);
                    double slopeWidth = 160000 + 320000 * StrikeTexture(p, 83129, 13);
                    double t = D.Clamp((shore - shelfWidth * .25) / (shelfWidth + slopeWidth));
                    h = -90 - 4800 * t * t * (3 - 2 * t) + (350 * broad + 150 * province) * t + 65 * detail;
                }
                if(background!=null)background[i]=h;
                if (land && collision.Source[i] >= 0)
                {
                    var b = w.Boundaries[collision.Source[i]]; double d = collision.Distance[i];
                    int a = Parent(b.MicroA), z = Parent(b.MicroB), hinterland = w.Parents[a].AreaM2 >= w.Parents[z].AreaM2 ? a : z;
                    int pair = Math.Min(a, z) * w.Parents.Length + Math.Max(a, z);
                    // Thickened continental crust records accumulated shortening,
                    // not just today's slow collision velocity. Maturity is a
                    // bounded, explicitly synthetic history proxy per margin.
                    double history = Rng.Value(r.Seed, "collision-maturity", pair);
                    double maturity = .40 + .85 * history * history;
                    double orthogonal = D.Clamp(b.NormalMotion / (Math.Abs(b.TangentialMotion) + 250.0));
                    double power = D.Clamp(D.Sqrt(Math.Max(0, b.NormalMotion - 60) / 300.0) * maturity * (.45 + .55 * orthogonal), 0, 1.4);
                    double grain = .5 + .5 * D.Sin(d / 260000 * 2 * D.Pi + province * 1.4);
                    double passes = .32 + .68 * StrikeTexture(p, 1933, 18);
                    bool natural=r.AlgorithmVersion==Recipe.CurrentAlgorithm;
                    double frontWidth=natural?OrogenMorphology.CollisionWidth(BeltWidth(b)):520000*BeltWidth(b);
                    double frontAmplitude=5200*power*MountainScale(b,p);
                    if(natural)frontAmplitude=OrogenMorphology.Uplift(frontAmplitude,6500,r.MountainStrengthPercent/100);
                    double front = natural?frontAmplitude * D.Kernel(d, frontWidth) * (.62 + .38 * grain * passes):5200 * power * D.Kernel(d, frontWidth) * (.62 + .38 * grain * passes) * MountainScale(b,p);
                    h += front; Add(FeatureKind.CollisionBelt, b, i, front, D.M(frontWidth));
                    if (Owner(i) == hinterland)
                    {
                        double plateauWidth = (550000 + 500000 * history)*BeltWidth(b);
                        double platform = natural?OrogenMorphology.PlateauEnvelope(d,plateauWidth):D.Kernel(Math.Abs(d - 430000), plateauWidth);
                        double plateauAmplitude=3500*power*MountainScale(b,p);
                        if(natural)plateauAmplitude=OrogenMorphology.Uplift(plateauAmplitude,4500,r.MountainStrengthPercent/100);
                        double relief = natural?plateauAmplitude * platform * (.90 + .10 * province):3500 * power * platform * (.90 + .10 * province) * MountainScale(b,p);
                        h += relief; plateauProvince = platform * D.Clamp(power); Add(FeatureKind.Plateau, b, i, relief, D.M(plateauWidth));
                    }
                    else
                    {
                        double foreland = -850 * power * D.Kernel(Math.Abs(d - 570000), 330000);
                        h += foreland; Add(FeatureKind.ForelandBasin, b, i, foreland, 330000);
                    }
                }
                if (!land && lower.Source[i] >= 0)
                {
                    var b = w.Boundaries[lower.Source[i]]; double trench = -2600 * Strength(b) * D.Kernel(lower.Distance[i], 320000);
                    h += trench; Add(FeatureKind.Trench, b, i, trench, 320000);
                }
                if (upper.Source[i] >= 0)
                {
                    var b = w.Boundaries[upper.Source[i]]; double d = upper.Distance[i], power = Strength(b);
                    double maturity = D.Clamp(.60 + .85 * Noise(p, r.Seed ^ 31937, 5));
                    double termination = .40 + .60 * D.Clamp(b.EndDistanceM / 280000.0);
                    double peak = D.Clamp((StrikeTexture(p, 25147, 42) - .36) / .48); peak *= peak;
                    double arcOffset = 150000 + 200000 * StrikeTexture(p, 15347, 8);
                    double arcWidth = 190000 + 170000 * StrikeTexture(p, 49739, 12);
                    double envelope = D.Kernel(Math.Abs(d - arcOffset), arcWidth);
                    int pair = Math.Min(Parent(b.MicroA), Parent(b.MicroB)) * w.Parents.Length + Math.Max(Parent(b.MicroA), Parent(b.MicroB));
                    if (land)
                    {
                        // Subduction does not imply an Andes-class cordillera.
                        // Pair-level coupling/history is mostly modest, with a
                        // long upper tail for mature shortening margins. This
                        // affects continental relief only, not island arcs.
                        double history = Rng.Value(r.Seed, "cordillera-maturity", pair);
                        double coupling = .16 + .94 * history * history * history;
                        double orthogonal = D.Clamp(b.NormalMotion / (Math.Abs(b.TangentialMotion) + 300.0));
                        double shortening = D.Clamp(D.Sqrt(Math.Max(0, b.NormalMotion - 80) / 950.0), 0, 1.3) * coupling * (.4 + .6 * orthogonal);
                        bool flatSlab = history > .65 && Rng.Value(r.Seed, "flat-slab", pair) < .27;
                        double grain = .5 + .5 * D.Sin(d / 230000 * 2 * D.Pi + province);
                        double rangeWidth = (r.AlgorithmVersion==Recipe.CurrentAlgorithm?150000+160000*history:270000+230000*history)*BeltWidth(b);
                        double passes = .32 + .68 * StrikeTexture(p, 69239, 14);
                        double rangeAmplitude=4800*shortening*MountainScale(b,p);
                        if(r.AlgorithmVersion==Recipe.CurrentAlgorithm)rangeAmplitude=OrogenMorphology.Uplift(rangeAmplitude,5800,r.MountainStrengthPercent/100);
                        double range = r.AlgorithmVersion==Recipe.CurrentAlgorithm?rangeAmplitude * D.Kernel(Math.Abs(d - 190000), rangeWidth) * (.5 + .5 * grain) * passes:4800 * shortening * D.Kernel(Math.Abs(d - 190000), rangeWidth) * (.5 + .5 * grain) * passes * MountainScale(b,p);
                        h += range; Add(FeatureKind.Cordillera, b, i, range, D.M(rangeWidth));
                        double volcanic = (flatSlab ? 180 : 1100) * power * envelope * peak;
                        h += volcanic; Add(FeatureKind.VolcanicArc, b, i, volcanic, D.M(arcWidth));
                        if (flatSlab)
                        {
                            double inland = 2300 * shortening * D.Kernel(Math.Abs(d - 900000), 430000) * (.7 + .3 * grain) * MountainScale(b,p);
                            h += inland; Add(FeatureKind.AncientOrogen, b, i, inland, 430000, 55);
                        }
                        double depression = -450 * shortening * D.Kernel(Math.Abs(d - (flatSlab ? 580000 : 780000)), 260000);
                        h += depression; Add(FeatureKind.ForelandBasin, b, i, depression, 260000);
                    }
                    else
                    {
                        double magmatism = D.Clamp(D.Sqrt(Math.Max(0, b.NormalMotion) / 800.0), 0, 1.15);
                        double arc = (2400 + 4300 * maturity + 2800 * peak) * magmatism * envelope * termination;
                        h += arc; Add(FeatureKind.IslandArc, b, i, arc, D.M(arcWidth));
                        double wedge = 750 * power * D.Kernel(d, 250000) * D.Kernel(shore, 650000) * maturity;
                        h += wedge; Add(FeatureKind.AccretionaryWedge, b, i, wedge, 250000);
                        double rollback = Rng.Value(r.Seed, "backarc", pair) < .58 ? 1 : 0;
                        double backarcOffset = arcOffset + 260000 + 260000 * StrikeTexture(p, 71933, 7);
                        double backarc = -1400 * power * rollback * D.Kernel(Math.Abs(d - backarcOffset), 340000);
                        h += backarc; Add(FeatureKind.BackArcBasin, b, i, backarc, 340000);
                        if (shore < 1100000) Add(FeatureKind.MarineBasin, b, i, backarc, 340000);
                    }
                }
                if (spreading.Source[i] >= 0)
                {
                    var b = w.Boundaries[spreading.Source[i]]; double power = D.Clamp(-b.NormalMotion / 1000.0, 0, 1.4), d = spreading.Distance[i];
                    double relief = (land ? -1050 : 950) * power * D.Kernel(d, land ? 380000 : 850000);
                    h += relief; Add(land ? FeatureKind.Rift : FeatureKind.MidOceanRidge, b, i, relief, land ? 380000 : 850000);
                }
                if (land && oblique.Source[i] >= 0)
                {
                    var b = w.Boundaries[oblique.Source[i]]; double compression = D.Clamp(b.NormalMotion / 350.0), shear = D.Clamp(Math.Abs(b.TangentialMotion) / 1000.0);
                    double relief = 2700 * compression * shear * D.Kernel(oblique.Distance[i], 360000) * (.7 + .3 * StrikeTexture(p, 7893, 18)) * MountainScale(b,p);
                    h += relief; Add(FeatureKind.ObliqueOrogen, b, i, relief, 360000);
                }
                if (inherited.Source[i] >= 0)
                {
                    var b = w.Boundaries[inherited.Source[i]]; int age = 40 + (int)(Rng.Value(r.Seed, "inherited-age", Owner(i)) * 180);
                    double survival = 1 - age / 320.0, d = inherited.Distance[i];
                    double relief = (land ? 1100 : 3900) * survival * D.Kernel(d, land ? 550000 : 300000) * (.65 + .35 * province);
                    if (!land) relief *= D.Kernel(shore, 650000);
                    h += relief; Add(land ? FeatureKind.AncientOrogen : FeatureKind.RelictArc, b, i, relief, land ? 550000 : 300000, age);
                }
                h += r.Relief / .4 * ((land ? 260 : 65) * detail * (1 - .7 * craton) + 100 * province * coastMask);
                h += w.MantlePermille[i] * (land ? .5 : .25);
                if (land) h -= coastMask * Math.Max(0, -province) * 120;
                heights[i] = h;
                double total = Math.Max(.001, fold + craton + basin + plateauProvince);
                w.FoldPermille[i] = (int)(1000 * fold / total); w.BasinPermille[i] = (int)(1000 * basin / total); w.PlateauPermille[i] = (int)(1000 * plateauProvince / total);
                w.CratonPermille[i] = 1000 - w.FoldPermille[i] - w.BasinPermille[i] - w.PlateauPermille[i];
            }
            foreach (var entry in records)
            { var f = entry.Value; f.Id = features.Count; f.Name = f.Kind + " " + f.Id; f.Cells = members[entry.Key].ToArray(); features.Add(f); }
            if(background!=null)
            {
                // Blend only added relief, not the accepted large-scale continental base.
                // A short stencil softens nearest-belt ownership seams without flattening ranges.
                var relief=new double[n];for(int i=0;i<n;i++)relief[i]=heights[i]-background[i];
                for(int i=0;i<n;i++)
                {
                    if((i&4095)==0)token.ThrowIfCancellationRequested();
                    double sum=0;int count=0;
                    foreach(int j in s.Cells[i].Neighbors)if((w.Continent[i]>=0)==(w.Continent[j]>=0)){sum+=relief[j];count++;}
                    if(count>0)heights[i]=background[i]+.8*relief[i]+.2*sum/count;
                }
            }
            AncientOrogens(w, heights, features, token);
            Hotspots(w, heights, spreading.Distance, features, token);
            w.TectonicM = heights.Select(h => D.M(r.AlgorithmVersion==Recipe.CurrentAlgorithm?OrogenMorphology.ContinentalHeight(h):h > 8500 ? 8500 + (h - 8500) * .25 : h)).ToArray(); w.Features = features.ToArray();
            w.ProtectedInlandSea = new bool[n]; w.PreErosionM = (int[])w.TectonicM.Clone(); w.ElevationM = (int[])w.TectonicM.Clone();
            foreach (FeatureKind kind in Enum.GetValues(typeof(FeatureKind))) w.Metric("tectonic_" + kind.ToString().ToLowerInvariant() + "_features", features.Count(f => f.Kind == kind));
            w.Metric("tectonic_ocean_emergent_cells", Enumerable.Range(0, n).Count(i => w.Continent[i] < 0 && w.TectonicM[i] > 0));
        }

        // Synthetic inherited belts are deliberately independent of today's
        // plate interfaces. Their age is provenance, not a simulated history.
        // Fixed physical dimensions allow them to disappear below coarse mesh
        // resolution instead of being inflated to one cell.
        static void AncientOrogens(World w, double[] heights, List<Feature> features, CancellationToken token)
        {
            var s = w.Surface; var r = w.Recipe;
            var eligible = Enumerable.Range(0, heights.Length)
                .Where(i => w.Continent[i] >= 0 && w.BoundaryDistanceM[i] >= 650000 && w.CoastDistanceM[i] >= 180000)
                .ToArray();
            if (eligible.Length == 0) return;
            int target = Math.Min(5, Math.Max(2, w.Parents.Count(p => p.ContinentalFraction > .5) / 3));
            var accepted = new List<V>();
            for (int candidate = 0; candidate < target * 18 && accepted.Count < target; candidate++)
            {
                if ((candidate & 7) == 0) token.ThrowIfCancellationRequested();
                var random = new Rng(r.Seed, "ancient-orogens", candidate);
                int seedCell = eligible[Math.Min(eligible.Length - 1, D.M(random.Unit() * (eligible.Length - 1)))];
                V start = s.Cells[seedCell].Position;
                if (accepted.Any(p => D.Angle(p, start) * r.RadiusKm * 1000 < 1200000)) continue;
                int owner = w.Microplates[w.CellMicroplate[seedCell]].ParentAffinityId;
                int continent = w.Continent[seedCell];
                V direction = V.Tangent(start, random.Vector());
                if (direction.Length < .5) continue;
                int age = 180 + D.M(random.Unit() * 260);
                int width = 140000 + D.M(random.Unit() * 160000);
                int length = 1000000 + D.M(random.Unit() * 2000000);
                // Dense overlapping centres form one belt rather than a row of
                // circular stamps. Spacing is always below half the support width.
                int nodes = Math.Max(8, (int)Math.Ceiling(length / (width * .45)) + 1);
                double step = length / (double)(nodes - 1);
                double bend = (random.Unit() * 2 - 1) * (.035 + .075 * random.Unit());
                var centers = new V[nodes]; centers[0] = start;
                V cursor = start, axis = direction;
                for (int node = 1; node < nodes; node++)
                {
                    V next = (cursor + axis * (step / (r.RadiusKm * 1000))).Unit;
                    V transported = V.Transport(axis, cursor, next);
                    V side = V.Cross(next, transported).Unit;
                    axis = (transported + side * bend).Unit;
                    centers[node] = next; cursor = next;
                }
                double survival = .38 + .42 * (1 - (age - 180) / 260.0);
                double amplitude = (650 + 850 * random.Unit()) * survival;
                var cells = new List<int>();
                for (int i = 0; i < heights.Length; i++)
                {
                    if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
                    if (w.Continent[i] != continent || w.Microplates[w.CellMicroplate[i]].ParentAffinityId != owner || w.BoundaryDistanceM[i] < 420000) continue;
                    V p = s.Cells[i].Position; double nearest = double.MaxValue; int nearestNode = -1;
                    for (int node = 0; node < centers.Length; node++)
                    {
                        double chord = 1 - V.Dot(p, centers[node]);
                        double loose = width / (r.RadiusKm * 1000.0);
                        if (chord > loose * loose) continue;
                        double distance = D.Angle(p, centers[node]) * r.RadiusKm * 1000;
                        if (distance < nearest) { nearest = distance; nearestNode = node; }
                    }
                    if (nearest >= width) continue;
                    double texture = .58 + .42 * (.5 + .5 * Noise(p, r.Seed ^ (candidate * 7919 + 173), 18));
                    double endpoint = D.Clamp(Math.Min(nearestNode + 1, centers.Length - nearestNode) / 3.0);
                    double relief = amplitude * D.Kernel(nearest, width) * texture * endpoint;
                    if((((r.AlgorithmVersion == Recipe.CurrentAlgorithm || r.AlgorithmVersion == Recipe.PresetAlgorithm) || r.AlgorithmVersion == Recipe.SubcellAlgorithm) || r.AlgorithmVersion==Recipe.FeatureBlendAlgorithm))relief*=1-D.Kernel(w.BoundaryDistanceM[i]-420000,230000);
                    if (relief < 35) continue;
                    heights[i] += relief; cells.Add(i);
                }
                if (cells.Count == 0) continue;
                accepted.Add(start);
                features.Add(new Feature
                {
                    Id = features.Count, Name = "Ancient orogen " + accepted.Count, Kind = FeatureKind.AncientOrogen,
                    OwnerParent = owner, OwnerMicroplate = w.CellMicroplate[seedCell], Center = centers[nodes / 2], Axis = direction,
                    AmplitudeM = D.M(amplitude), WidthM = width, AgeMyr = age, Cells = cells.ToArray()
                });
            }
        }

        static void Hotspots(World w, double[] heights, int[] ridgeDistance, List<Feature> features, CancellationToken token)
        {
            if((((w.Recipe.AlgorithmVersion == Recipe.CurrentAlgorithm || w.Recipe.AlgorithmVersion == Recipe.PresetAlgorithm) || w.Recipe.AlgorithmVersion == Recipe.SubcellAlgorithm) || w.Recipe.AlgorithmVersion==Recipe.FeatureBlendAlgorithm)){Hotspots330(w,heights,ridgeDistance,features,token);return;}
            var s = w.Surface; var r = w.Recipe;
            for (int k = 0; k < 10; k++)
            {
                var random = new Rng(r.Seed, "hotspots", k); V at = random.Vector().Unit; int cell = Erosion.Nearest(s, at);
                int owner = w.Microplates[w.CellMicroplate[cell]].ParentAffinityId;
                V drift = V.Cross(w.Parents[owner].Motion, at).Unit;
                bool continental = w.Continent[cell] >= 0, ridge = !continental && ridgeDistance[cell] < 420000;
                // A shallow shelf requires less construction than an abyssal
                // basin. The same fixed abyssal uplift must not produce an
                // 8-km volcano just because the plume happens to meet a coast.
                double initialAmplitude = continental ? 1200 + 1200 * random.Unit() : Math.Min(ridge ? 7600 : 7100, (ridge ? 1300 : 2500) - Math.Min(0, heights[cell]));
                int activeWidth = ridge ? 130000 + D.M(random.Unit() * 130000) : 50000 + D.M(random.Unit() * 80000);
                int trailCount = 4 + D.M(random.Unit() * 6);
                int trailLength = 500000 + D.M(random.Unit() * 2700000);
                int ageStep = 6 + D.M(random.Unit() * 9);
                // A stationary plate cannot transport a trail: do not stack
                // multiple old volcanoes onto exactly the active centre.
                if (drift.Length < .5) trailCount = 1;
                V center = at, trailDirection = drift;
                // Old edifices move WITH the plate away from a stationary plume.
                for (int age = 0; age < trailCount; age++)
                {
                    if (age > 0)
                    {
                        V next = (center + trailDirection * (trailLength / (double)(trailCount - 1) / (r.RadiusKm * 1000))).Unit;
                        trailDirection = V.Transport(trailDirection, center, next); center = next;
                    }
                    int centerCell = Erosion.Nearest(s, center, cell);
                    if (w.Microplates[w.CellMicroplate[centerCell]].ParentAffinityId != owner) break;
                    int width = Math.Min(activeWidth, D.M(activeWidth * (1 - age * .055) * (.82 + .36 * random.Unit())));
                    double amplitude = initialAmplitude * (1 - age / (double)(trailCount + 1)) * (age == 0 ? 1 : .82 + .14 * random.Unit());
                    var cells = new List<int>();
                    for (int i = 0; i < heights.Length; i++)
                    {
                        if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
                        double loose = width / (r.RadiusKm * 1000.0);
                        if (w.Microplates[w.CellMicroplate[i]].ParentAffinityId != owner || 1 - V.Dot(s.Cells[i].Position, center) > loose * loose) continue;
                        double distance = D.Angle(s.Cells[i].Position, center) * r.RadiusKm * 1000, relief = amplitude * D.Kernel(distance, width);
                        if (relief < 35) continue; heights[i] += relief; cells.Add(i);
                    }
                    features.Add(new Feature { Id = features.Count, Name = (age == 0 ? "Hotspot " : "Volcanic trail ") + k + "/" + age, Kind = age == 0 ? ridge ? FeatureKind.RidgeHotspot : FeatureKind.Hotspot : FeatureKind.IslandChain, OwnerParent = owner, OwnerMicroplate = w.CellMicroplate[cell], Center = center, Axis = drift, AmplitudeM = D.M(amplitude), WidthM = width, AgeMyr = age * ageStep, Cells = cells.ToArray() });
                }
                if (ridge || k % 4 == 0)
                {
                    var cells = new List<int>(); int width = ridge ? 450000 + D.M(random.Unit() * 400000) : 250000 + D.M(random.Unit() * 270000); double amplitude = ridge ? 650 + 450 * random.Unit() : 260 + 300 * random.Unit();
                    for (int i = 0; i < heights.Length; i++)
                    {
                        if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
                        double loose = width / (r.RadiusKm * 1000.0);
                        if (w.Microplates[w.CellMicroplate[i]].ParentAffinityId != owner || 1 - V.Dot(s.Cells[i].Position, at) > loose * loose) continue;
                        double distance = D.Angle(s.Cells[i].Position, at) * r.RadiusKm * 1000, relief = amplitude * D.Kernel(distance, width);
                        if (relief < 35) continue; heights[i] += relief; cells.Add(i);
                    }
                    features.Add(new Feature { Id = features.Count, Name = "Igneous province " + k, Kind = FeatureKind.LargeIgneousProvince, OwnerParent = owner, Center = at, WidthM = width, AmplitudeM = D.M(amplitude), Cells = cells.ToArray() });
                }
            }
        }

        static void Hotspots330(World w,double[] heights,int[] ridgeDistance,List<Feature> features,CancellationToken token)
        {
            var s=w.Surface;var r=w.Recipe;double radius=r.RadiusKm*1000;
            var locator=new CellLocator(s.Cells);
            for(int k=0;k<10;k++)
            {
                token.ThrowIfCancellationRequested();var random=new Rng(r.Seed,"hotspots-330",k);
                V center=random.Vector().Unit;int cell=locator.Nearest(center);
                int owner=w.Microplates[w.CellMicroplate[cell]].ParentAffinityId;
                V direction=V.Cross(w.Parents[owner].Motion,center).Unit;
                bool continental=w.Continent[cell]>=0,ridge=!continental && ridgeDistance[cell]<420000;
                int count=direction.Length<.5?1:8+D.M(random.Unit()*8);
                if(ridge || k%4==0)
                {
                    double swellWidth=ridge?350000:250000, swellHeight=ridge?600:300;
                    double angle=swellWidth/radius;var swellCells=new List<int>();
                    foreach(int i in locator.Within(center,2-2*Math.Cos(angle)))
                    {
                        token.ThrowIfCancellationRequested();
                        double uplift=swellHeight*D.Kernel(D.Angle(s.Cells[i].Position,center)*radius,swellWidth);
                        if(uplift<.5)continue;heights[i]+=uplift;swellCells.Add(i);
                    }
                    features.Add(new Feature{Id=features.Count,Name="Igneous province "+k,Kind=FeatureKind.LargeIgneousProvince,
                        OwnerParent=owner,Center=center,WidthM=D.M(swellWidth),AmplitudeM=D.M(swellHeight),Cells=swellCells.ToArray()});
                }
                double width=ridge?90000+random.Unit()*80000:45000+random.Unit()*40000;
                double spacing=90000+random.Unit()*130000;
                double curve=(random.Unit()*2-1)*.12,bend=(random.Unit()<.5?-1:1)*(.45+.65*random.Unit());
                int bendAt=3+D.M(random.Unit()*4);
                for(int age=0;age<count;age++)
                {
                    if(age>0)
                    {
                        V side=V.Cross(center,direction).Unit;double turn=curve+(age==bendAt?bend:0);
                        direction=(direction*Math.Cos(turn)+side*Math.Sin(turn)).Unit;
                        V next=(center+direction*(spacing/radius)).Unit;
                        direction=V.Transport(direction,center,next);center=next;
                    }
                    int nearest=locator.Nearest(center);
                    if(w.Microplates[w.CellMicroplate[nearest]].ParentAffinityId!=owner)break;
                    double support=width*(.85+.3*random.Unit());
                    // Summit height decays, not the entire construction measured from the abyssal floor.
                    // Several young edifices can emerge; old ones become submarine seamounts.
                    double summit=(ridge?1600:2300)*Math.Exp(-age/3.0)-age*90;
                    double amplitude=continental?(1000+random.Unit()*900)*Math.Exp(-age/4.0):Math.Min(8500,Math.Max(0,summit-Math.Min(0,heights[nearest])));
                    double angle=support*1.2/radius;
                    var cells=new List<int>();V sideAxis=V.Cross(center,direction).Unit;
                    foreach(int i in locator.Within(center,2-2*Math.Cos(angle)))
                    {
                        token.ThrowIfCancellationRequested();V p=s.Cells[i].Position;
                        double along=V.Dot(p,direction)*radius,across=V.Dot(p,sideAxis)*radius;
                        double distance=direction.Length<.5?D.Angle(p,center)*radius:Math.Sqrt(along*along/.81+across*across*1.21);
                        double shape=1+.12*Noise(p,r.Seed^(k*191+age*37),55);
                        double relief=amplitude*D.Kernel(distance,support*shape);
                        // No hard plate-mask cut at the foot of a volcano.
                        if(relief<.5)continue;heights[i]+=relief;cells.Add(i);
                    }
                    features.Add(new Feature{Id=features.Count,Name=(age==0?"Hotspot ":"Volcanic trail ")+k+"/"+age,
                        Kind=age==0?(ridge?FeatureKind.RidgeHotspot:FeatureKind.Hotspot):FeatureKind.IslandChain,
                        OwnerParent=owner,OwnerMicroplate=w.CellMicroplate[cell],Center=center,Axis=direction,AmplitudeM=D.M(amplitude),WidthM=D.M(support),AgeMyr=age*3,Cells=cells.ToArray()});
                }
            }
        }
    }
}
