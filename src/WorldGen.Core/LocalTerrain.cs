using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>Replaceable, derived local terrain. Never writes World or participates in its existing hashes.
    /// Pin Version with a world before using locations for permanent structures. Units are physical metres.</summary>
    public sealed class LocalTerrain
    {
        public const string Version = "local-terrain-preview-1";
        public readonly double RadiusM;
        readonly World world;
        readonly CellLocator locator;
        readonly LocalTerrainProfile[] profiles;
        readonly int[] faceOffsets, faces;
        readonly double maximumSupport;
        readonly int seed;

        public LocalTerrain(World world, CancellationToken token = default)
        {
            if (world == null || world.Surface.Cells.Length == 0 || world.ElevationM.Length != world.Surface.Cells.Length)
                throw new ArgumentException("A completed world heightmap is required.", nameof(world));
            this.world = world; RadiusM = world.Recipe.RadiusKm * 1000; seed = world.Recipe.Seed;
            profiles = new LocalTerrainProfile[world.Surface.Cells.Length];
            var axes = new V[profiles.Length]; var ages = new int[profiles.Length];
            // One optional generation-time index, never a recurring cell census.
            foreach (var feature in world.Features)
            {
                token.ThrowIfCancellationRequested();
                if (feature.Kind != FeatureKind.CollisionBelt && feature.Kind != FeatureKind.Cordillera && feature.Kind != FeatureKind.AncientOrogen && feature.Kind != FeatureKind.ObliqueOrogen) continue;
                foreach (int cell in feature.Cells)
                    if (cell >= 0 && cell < axes.Length && axes[cell].Length < .1) { axes[cell] = feature.Axis; ages[cell] = feature.AgeMyr; }
            }
            for (int i = 0; i < profiles.Length; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                var cell = world.Surface.Cells[i]; V center = cell.Position;
                double low = world.ElevationM[i], high = low, longest = Math.Sqrt(cell.AreaM2);
                double gx = 0, gy = 0, gz = 0;
                foreach (int j in cell.Neighbors)
                {
                    double h = world.ElevationM[j]; low = Math.Min(low, h); high = Math.Max(high, h);
                    V delta = world.Surface.Cells[j].Position - center;
                    double length2 = delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z;
                    longest = Math.Max(longest, Math.Sqrt(length2) * RadiusM);
                    double scale = (h - world.ElevationM[i]) / Math.Max(1, length2 * RadiusM);
                    gx += delta.X * scale; gy += delta.Y * scale; gz += delta.Z * scale;
                }
                var habitat = world.Ecology != null && world.Ecology.Habitat.Length > i ? world.Ecology.Habitat[i] : null;
                double plateau = At(world.PlateauPermille, i) / 1000.0;
                double fold = At(world.FoldPermille, i) / 1000.0;
                double relief = high - low;
                // Measured broad relief supplements provenance, especially Earth (which has no synthetic belts).
                // Altitude alone NEVER enables mountain detail. Neighbour relief is explicitly a coarse-scale proxy.
                double mountains = D.Clamp(Math.Max(fold * 1.35, (relief - 220) / 2300)) * (1 - .82 * plateau);
                double sediment = habitat == null ? At(world.BasinPermille, i) / 1500.0
                    : Math.Max(habitat.FloodFractionPermille / 1000.0, habitat.Soil == SoilKind.Alluvial ? .85 : 0);
                double hill = (8 + 75 * D.Clamp(relief / 1000.0)) * (1 - .85 * plateau) * (1 - .94 * sediment);
                double ridge = (90 + Math.Min(1300, relief * .46)) * mountains * (1 - .9 * sediment);
                V axis = axes[i];
                if (axis.Length < .1)
                {
                    V gradient = new V(gx, gy, gz);
                    axis = gradient.Length > .00001 ? V.Cross(center, gradient).Unit
                        : V.Tangent(center, new Rng(seed, "local-terrain-fabric-1", i).Vector());
                }
                axis = (axis - center * V.Dot(axis, center)).Unit;
                if (axis.Length < .1) axis = V.Tangent(center, Math.Abs(center.Z) < .8 ? new V(0, 0, 1) : new V(1, 0, 0));
                V across = V.Cross(center, axis).Unit;
                double support = longest * 2.15;
                maximumSupport = Math.Max(maximumSupport, support);
                profiles[i] = new LocalTerrainProfile
                {
                    Cell = i, ElevationM = world.ElevationM[i], CoarseReliefM = relief, HillHeightM = hill, MountainHeightM = ridge,
                    RidgeSpacingM = 1600 + 5800 * Rng.Value(seed, "local-terrain-spacing-1", i),
                    Sharpness = 1.25 + .7 * (1 - D.Clamp(ages[i] / 300.0)), Plateau = plateau, Sediment = sediment,
                    Glacial = world.Glaciated.Length > i && world.Glaciated[i] ? 1 : 0,
                    Biome = habitat?.Biome ?? Biome.Grassland, SupportM = support,
                    Center = center, Axis = axis, Across = across,
                    Gradient = new V(gx, gy, gz) * (2.0 / Math.Max(1, cell.Neighbors.Length)),
                    Phase = Rng.Value(seed, "local-terrain-phase-1", i) * 2 * D.Pi
                };
            }
            token.ThrowIfCancellationRequested(); locator = new CellLocator(world.Surface.Cells); token.ThrowIfCancellationRequested();
            faceOffsets = new int[profiles.Length + 1]; foreach(int id in world.Surface.Triangles) faceOffsets[id + 1]++;
            for(int i=1;i<faceOffsets.Length;i++) faceOffsets[i]+=faceOffsets[i-1];
            faces = new int[world.Surface.Triangles.Length]; var cursor=(int[])faceOffsets.Clone();
            for(int t=0;t<faces.Length;t+=3)
            { if((t&8191)==0)token.ThrowIfCancellationRequested();for(int j=0;j<3;j++)faces[cursor[world.Surface.Triangles[t+j]]++]=t; }
        }

        static int At(int[] values, int i) => i < values.Length ? values[i] : 0;
        public LocalTerrainProfile Profile(int cell)
        { if (cell < 0 || cell >= profiles.Length) throw new ArgumentOutOfRangeException(nameof(cell)); return profiles[cell]; }

        /// <summary>Shared continuous function restricted to a bounded patch. Candidate lookup happens once,
        /// not at each vertex. Independent neighbouring patches return identical overlapping point samples.</summary>
        public LocalTerrainPatch Patch(V center, double halfExtentM = 2048)
        {
            if (double.IsNaN(halfExtentM) || double.IsInfinity(halfExtentM) || halfExtentM <= 0 || halfExtentM > 100000)
                throw new ArgumentOutOfRangeException(nameof(halfExtentM));
            double norm = center.X * center.X + center.Y * center.Y + center.Z * center.Z;
            if (double.IsNaN(norm) || double.IsInfinity(norm) || norm < .25) throw new ArgumentOutOfRangeException(nameof(center));
            center = center.Unit;
            double search = Math.Min(2, (maximumSupport + halfExtentM * 1.5 + 20) / RadiusM);
            var ids = locator.Within(center, search * search);
            var sources = new LocalTerrainProfile[ids.Length]; for (int i = 0; i < ids.Length; i++) sources[i] = profiles[ids[i]];
            var triangleIds = new SortedSet<int>();
            foreach(int id in ids) for(int k=faceOffsets[id];k<faceOffsets[id+1];k++) triangleIds.Add(faces[k]);
            V east = V.Cross(Math.Abs(center.Z)<.99?new V(0,0,1):new V(0,1,0),center).Unit, north = V.Cross(center,east).Unit;
            var triangles = new List<HeightTriangle>();
            foreach(int t in triangleIds)
            {
                int a=world.Surface.Triangles[t],b=world.Surface.Triangles[t+1],c=world.Surface.Triangles[t+2];
                double minX=double.MaxValue,minY=double.MaxValue,maxX=double.MinValue,maxY=double.MinValue;
                foreach(int id in new[]{a,b,c})
                {
                    V delta=profiles[id].Center-center;double x=V.Dot(delta,east)*RadiusM,y=V.Dot(delta,north)*RadiusM;
                    minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
                }
                double bound=halfExtentM*1.6+20;
                if(minX>bound||maxX< -bound||minY>bound||maxY< -bound)continue;
                triangles.Add(new HeightTriangle(profiles[a],profiles[b],profiles[c],RadiusM));
            }
            return new LocalTerrainPatch(this, center, halfExtentM, sources, triangles.ToArray(), seed);
        }

        // Cubic triangular surface: exact source heights and shared edge curves. Bounded
        // Bezier controls prevent overshooting the triangle's source elevation range.
        internal struct HeightTriangle
        {
            readonly double ax,ay,az,bx,by,bz,cx,cy,cz,ha,hb,hc,ab,ac,ba,bc,ca,cb,mid;
            public HeightTriangle(LocalTerrainProfile a,LocalTerrainProfile b,LocalTerrainProfile c,double radius)
            {
                V p=a.Center,q=b.Center,r=c.Center;
                double det=p.X*(q.Y*r.Z-q.Z*r.Y)+p.Y*(q.Z*r.X-q.X*r.Z)+p.Z*(q.X*r.Y-q.Y*r.X);
                ax=(q.Y*r.Z-q.Z*r.Y)/det;ay=(q.Z*r.X-q.X*r.Z)/det;az=(q.X*r.Y-q.Y*r.X)/det;
                bx=(r.Y*p.Z-r.Z*p.Y)/det;by=(r.Z*p.X-r.X*p.Z)/det;bz=(r.X*p.Y-r.Y*p.X)/det;
                cx=(p.Y*q.Z-p.Z*q.Y)/det;cy=(p.Z*q.X-p.X*q.Z)/det;cz=(p.X*q.Y-p.Y*q.X)/det;
                ha=a.ElevationM;hb=b.ElevationM;hc=c.ElevationM;
                ab=Control(a,b,radius);ac=Control(a,c,radius);ba=Control(b,a,radius);bc=Control(b,c,radius);ca=Control(c,a,radius);cb=Control(c,b,radius);
                mid=(ab+ac+ba+bc+ca+cb)/6;
            }
            static double Control(LocalTerrainProfile a,LocalTerrainProfile b,double radius)
            {double h=a.ElevationM+V.Dot(a.Gradient,b.Center-a.Center)*radius/3;return D.Clamp(h,Math.Min(a.ElevationM,b.ElevationM),Math.Max(a.ElevationM,b.ElevationM));}
            public bool Height(V p,out double height)
            {
                double x=p.X*ax+p.Y*ay+p.Z*az,y=p.X*bx+p.Y*by+p.Z*bz,z=p.X*cx+p.Y*cy+p.Z*cz;
                if(Math.Min(x,Math.Min(y,z))< -.00000001){height=0;return false;}
                x=Math.Max(0,x);y=Math.Max(0,y);z=Math.Max(0,z);double sum=x+y+z;x/=sum;y/=sum;z/=sum;
                height=x*x*x*ha+y*y*y*hb+z*z*z*hc+3*(x*x*y*ab+x*x*z*ac+y*y*x*ba+y*y*z*bc+z*z*x*ca+z*z*y*cb)+6*x*y*z*mid;
                return true;
            }
        }

        public sealed class LocalTerrainPatch
        {
            readonly LocalTerrain owner; readonly LocalTerrainProfile[] sources; readonly int seed;
            readonly HeightTriangle[] triangles;
            public readonly V Center, East, North;
            public readonly double HalfExtentM;
            public int CandidateCount => sources.Length;
            internal LocalTerrainPatch(LocalTerrain owner, V center, double extent, LocalTerrainProfile[] sources, HeightTriangle[] triangles, int seed)
            {
                this.owner = owner; Center = center; HalfExtentM = extent; this.sources = sources; this.triangles=triangles;this.seed = seed;
                East = V.Cross(Math.Abs(center.Z) < .99 ? new V(0, 0, 1) : new V(0, 1, 0), center).Unit;
                North = V.Cross(center, East).Unit;
            }
            public V Position(double eastM, double northM)
            {
                if (double.IsNaN(eastM) || double.IsNaN(northM) || Math.Abs(eastM) > HalfExtentM || Math.Abs(northM) > HalfExtentM) throw new ArgumentOutOfRangeException("Coordinates outside terrain patch.");
                return (Center + East * (eastM / owner.RadiusM) + North * (northM / owner.RadiusM)).Unit;
            }
            public LocalTerrainSample Sample(double eastM, double northM) => Sample(Position(eastM, northM));
            public LocalTerrainSample Sample(V point)
            {
                double norm = point.X * point.X + point.Y * point.Y + point.Z * point.Z;
                if (double.IsNaN(norm) || Math.Abs(norm - 1) > .000001) throw new ArgumentOutOfRangeException(nameof(point), "A unit planetary position is required.");
                double dx = point.X - Center.X, dy = point.Y - Center.Y, dz = point.Z - Center.Z;
                if ((dx * dx + dy * dy + dz * dz) * owner.RadiusM * owner.RadiusM > HalfExtentM * HalfExtentM * 2.01 + 1)
                    throw new ArgumentOutOfRangeException(nameof(point), "Point outside indexed terrain patch.");
                double px = point.X * owner.RadiusM, py = point.Y * owner.RadiusM, pz = point.Z * owner.RadiusM;
                V km = new V(px / 1000, py / 1000, pz / 1000);
                double broad = SphereNoise.Sample(km, seed ^ 0x1837, .16);
                double medium = SphereNoise.Sample(km, seed ^ 0x7917, .63);
                double fine = SphereNoise.Sample(km, seed ^ 0x2573, 2.1);
                double micro = SphereNoise.Sample(km, seed ^ 0x6819, 10);
                double sum = 0, hills = 0, mountains = 0, plateau = 0, sediment = 0, glacier = 0, detail = 0, bestWeight = -1, red = 0, green = 0, blue = 0, ocean = 0;
                int cell = -1; Biome biome = Biome.Grassland;
                foreach (var f in sources)
                {
                    double x = px - f.Center.X * owner.RadiusM, y = py - f.Center.Y * owner.RadiusM, z = pz - f.Center.Z * owner.RadiusM;
                    double dist2 = (x * x + y * y + z * z) / (f.SupportM * f.SupportM);
                    if (dist2 >= 1) continue;
                    // Compact C2 kernel: derivatives vanish at support edges; no closest-N switching seams.
                    double r = Math.Sqrt(dist2), t = 1 - r, weight = t * t * t * t * (1 + 4 * r);
                    sum += weight; hills += weight * f.HillHeightM;
                    if (f.Cell < owner.world.Hydrology.OceanConnected.Length && owner.world.Hydrology.OceanConnected[f.Cell]) ocean += weight;
                    mountains += weight * f.MountainHeightM; plateau += weight * f.Plateau;
                    sediment += weight * f.Sediment; glacier += weight * f.Glacial;
                    var color = CoverColor(f.Biome); red += weight * color.X; green += weight * color.Y; blue += weight * color.Z;
                    double along = x * f.Axis.X + y * f.Axis.Y + z * f.Axis.Z;
                    double across = x * f.Across.X + y * f.Across.Y + z * f.Across.Z;
                    // Fixed source-local frames, smoothly blended in world space. Orientation changes do not
                    // rotate a planet-sized noise domain or reseed chunks. Nested waviness breaks parallel bands.
                    double phase = across * (2 * D.Pi / f.RidgeSpacingM) + .9 * medium + .8 * D.Sin(along / 5200 + f.Phase);
                    double wave = .5 + .5 * D.Sin(phase + f.Phase);
                    double crest = wave * wave; // continuous rounded valley, narrower crest
                    double sharp = f.Sharpness / 2;
                    crest = crest * sharp + wave * (1 - sharp);
                    double ridges = (crest - .375) * (1 + .38 * broad) + .17 * medium * (.2 + .8 * crest);
                    detail += weight * f.MountainHeightM * ridges;
                    if (weight > bestWeight) { bestWeight = weight; cell = f.Cell; biome = f.Biome; }
                }
                if (sum <= 0) throw new InvalidOperationException("Terrain patch has no supporting world cells.");
                double baseM = double.NaN;
                foreach(var triangle in triangles)if(triangle.Height(point,out double value)){baseM=value;break;}
                if(double.IsNaN(baseM))throw new InvalidOperationException("Terrain patch did not resolve a containing world triangle.");
                double h = hills / sum, m = mountains / sum, s = sediment / sum;
                double residual = detail / sum + h * (broad + .35 * medium + .10 * fine) + (.3 + 2.4 * (1 - s) + m * .008) * micro;
                // Sea level is a hard continuity anchor: do not manufacture new islands/coasts from high-frequency noise.
                double coast = Smooth(Math.Abs(baseM) / 100);
                if (baseM < 0) residual = (12 * broad + 4 * medium + fine) * coast;
                else residual = Math.Max(-.9 * baseM, residual * coast);
                double elevation = D.Q(baseM + residual, 1000);
                return new LocalTerrainSample { ElevationM = elevation, BaseElevationM = D.Q(baseM, 1000),
                    MountainHeightM = m, HillHeightM = h, Plateau = plateau / sum, Sediment = s,
                    GlacialInfluence = glacier / sum, Cell = cell, Biome = biome,
                    CoverColor = new V(red / sum, green / sum, blue / sum), OceanInfluence = ocean / sum,
                    WaterSurfaceM = elevation < 0 && ocean / sum > .01 ? 0 : double.NaN };
            }
            static double Smooth(double v) { v = D.Clamp(v); return v * v * (3 - 2 * v); }
        }
        static V CoverColor(Biome biome)
        {
            switch (biome)
            {
                case Biome.Desert: case Biome.SalineLake: return new V(.62, .50, .32);
                case Biome.Ice: return new V(.86, .9, .92);
                case Biome.Tundra: case Biome.Alpine: return new V(.35, .36, .27);
                case Biome.BorealForest: return new V(.22, .30, .22);
                case Biome.TropicalRainforest: case Biome.TemperateRainforest: return new V(.20, .34, .16);
                case Biome.TemperateForest: case Biome.TropicalSeasonalForest: return new V(.31, .40, .19);
                case Biome.Grassland: return new V(.43, .46, .24);
                case Biome.Savanna: return new V(.52, .46, .25);
                case Biome.Marsh: case Biome.Mangrove: case Biome.Peatland: return new V(.27, .33, .19);
                case Biome.Ocean: return new V(.36, .34, .27);
                default: return new V(.40, .42, .25);
            }
        }
    }

    public struct LocalTerrainProfile
    {
        public int Cell;
        public double ElevationM, CoarseReliefM, HillHeightM, MountainHeightM, RidgeSpacingM, Sharpness, Plateau, Sediment, Glacial;
        public Biome Biome;
        internal V Center, Axis, Across, Gradient;
        internal double SupportM, Phase;
    }
    public struct LocalTerrainSample
    {
        public double ElevationM, BaseElevationM, WaterSurfaceM, MountainHeightM, HillHeightM, Plateau, Sediment, GlacialInfluence, OceanInfluence;
        public int Cell;
        public V CoverColor;
        public Biome Biome;
        public bool Underwater => !double.IsNaN(WaterSurfaceM) && ElevationM < WaterSurfaceM;
    }
}
