using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>Replaceable, derived local terrain. Never writes World or participates in its existing hashes.
    /// Pin Version with a world before using locations for permanent structures. Units are physical metres.</summary>
    public sealed class LocalTerrain
    {
        public const string Version = "local-terrain-hyper-11";
        public readonly double RadiusM;
        public readonly double HorizontalScale;
        readonly World world;
        readonly CellLocator locator;
        readonly LocalTerrainProfile[] profiles;
        readonly int[] faceOffsets, faces;
        readonly double maximumSupport;
        readonly int seed;
        public readonly LocalHydrology Hydrology;
        public readonly GeographicScaffold Scaffold;
        public readonly HyperAmplification Amplification;
        internal World SourceWorld=>world;
        public int SourceSeed=>seed;

        public LocalTerrain(World world, CancellationToken token = default,double horizontalScale=1)
        {
            if(double.IsNaN(horizontalScale)||horizontalScale<.01||horizontalScale>2)throw new ArgumentOutOfRangeException(nameof(horizontalScale));
            HorizontalScale=horizontalScale;
            if (world == null || world.Surface.Cells.Length == 0 || world.ElevationM.Length != world.Surface.Cells.Length)
                throw new ArgumentException("A completed world heightmap is required.", nameof(world));
            this.world = world; RadiusM = world.Recipe.RadiusKm * 1000; seed = world.Recipe.Seed;
            var groundEnvironment=GroundEnvironment.For(world);
            profiles = new LocalTerrainProfile[world.Surface.Cells.Length];
            var axes = new V[profiles.Length]; var ages = new int[profiles.Length];
            var fabricOrigins=new V[profiles.Length];var fabricKeys=new int[profiles.Length];
            // One optional generation-time index, never a recurring cell census.
            foreach (var feature in world.Features)
            {
                token.ThrowIfCancellationRequested();
                if (feature.Kind != FeatureKind.CollisionBelt && feature.Kind != FeatureKind.Cordillera && feature.Kind != FeatureKind.AncientOrogen && feature.Kind != FeatureKind.ObliqueOrogen) continue;
                foreach (int cell in feature.Cells)
                    if (cell >= 0 && cell < axes.Length && axes[cell].Length < .1)
                    { axes[cell] = feature.Axis; ages[cell] = feature.AgeMyr;fabricOrigins[cell]=feature.Center.Length>.1?feature.Center.Unit:world.Surface.Cells[cell].Position;fabricKeys[cell]=feature.Id; }
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
                double relief = high - low;
                // High, low-relief interiors are plateaus, not Himalayan peaks.
                // On measured Earth this is an inference, not a named-region override.
                double sediment = habitat == null ? At(world.BasinPermille, i) / 1500.0
                    : Math.Max(habitat.FloodFractionPermille / 1000.0, habitat.Soil == SoilKind.Alluvial ? .85 : 0);
                var landform=new TerrainRelief(world.ElevationM[i],relief,At(world.FoldPermille,i)/1000.0,At(world.PlateauPermille,i)/1000.0,sediment);
                double plateau=landform.Plateau,hill=landform.HillsM,ridge=landform.MountainsM;
                V axis = axes[i];
                if (axis.Length < .1)
                {
                    V gradient = new V(gx, gy, gz);
                    axis = gradient.Length > .00001 ? V.Cross(center, gradient).Unit
                        : V.Tangent(center, new Rng(seed, "local-terrain-fabric-1", i).Vector());
                }
                bool sharedFabric=fabricOrigins[i].Length>.1;V fabricOrigin=sharedFabric?fabricOrigins[i]:center;
                axis = (axis - fabricOrigin * V.Dot(axis, fabricOrigin)).Unit;
                if (axis.Length < .1) axis = V.Tangent(center, Math.Abs(center.Z) < .8 ? new V(0, 0, 1) : new V(1, 0, 0));
                V across = V.Cross(fabricOrigin, axis).Unit;
                double support = longest * 1.6;
                maximumSupport = Math.Max(maximumSupport, support);
                profiles[i] = new LocalTerrainProfile
                {
                    Cell = i, Ground=groundEnvironment.TerrainCell(i), ElevationM = world.ElevationM[i], CoarseReliefM = relief, HillHeightM = hill, MountainHeightM = ridge,
                    RidgeSpacingM = 4000 + 9000 * Rng.Value(seed, "local-terrain-spacing-2", sharedFabric?fabricKeys[i]:i),
                    Sharpness = 1.05 + .9 * (1 - D.Clamp(ages[i] / 300.0)), Plateau = plateau, Sediment = sediment,
                    Glacial = world.Glaciated.Length > i && world.Glaciated[i] ? 1 : 0,
                    Biome = habitat?.Biome ?? Biome.Grassland, SupportM = support,
                    Center = center, FabricOrigin=fabricOrigin, Axis = axis, Across = across,
                    Gradient = new V(gx, gy, gz) * (2.0 / Math.Max(1, cell.Neighbors.Length)),
                    Phase = Rng.Value(seed, "local-terrain-phase-2", sharedFabric?fabricKeys[i]:i) * 2 * D.Pi
                };
            }
            token.ThrowIfCancellationRequested(); locator = new CellLocator(world.Surface.Cells); token.ThrowIfCancellationRequested();
            faceOffsets = new int[profiles.Length + 1]; foreach(int id in world.Surface.Triangles) faceOffsets[id + 1]++;
            for(int i=1;i<faceOffsets.Length;i++) faceOffsets[i]+=faceOffsets[i-1];
            faces = new int[world.Surface.Triangles.Length]; var cursor=(int[])faceOffsets.Clone();
            for(int t=0;t<faces.Length;t+=3)
            { if((t&8191)==0)token.ThrowIfCancellationRequested();for(int j=0;j<3;j++)faces[cursor[world.Surface.Triangles[t+j]]++]=t; }
            // Completed worlds get one shared geographic control mesh. Partial
            // preview/test fixtures with no complete drainage retain the small
            // interpolation-only fallback; no fine map is serialized into World.
            if(world.Hydrology.Receiver.Length==profiles.Length&&world.Hydrology.VisitOrder.Length==profiles.Length&&world.Surface.Edges.Length>0)
                Amplification=new HyperAmplification(world,profiles,horizontalScale,token);
            // Kept as coarse diagnostic/source reach metadata. Amplified terrain
            // and water are generated together; do not fit legacy strips over it.
            Hydrology=new LocalHydrology(world,horizontalScale,token);
        }

        static int At(int[] values, int i) => i < values.Length ? values[i] : 0;
        public LocalTerrainProfile Profile(int cell)
        { if (cell < 0 || cell >= profiles.Length) throw new ArgumentOutOfRangeException(nameof(cell)); return profiles[cell]; }
        public int NearestCell(V point)=>locator.Nearest(point);
        internal CellLocator WeatherLocator=>locator;

        /// <summary>Shared continuous function restricted to a bounded patch. Candidate lookup happens once,
        /// not at each vertex. Independent neighbouring patches return identical overlapping point samples.</summary>
        public LocalTerrainPatch Patch(V center, double halfExtentM = 2048)=>BuildPatch(center,halfExtentM,false);
        internal LocalTerrainPatch DryPatch(V center,double halfExtentM)=>BuildPatch(center,halfExtentM,true);
        LocalTerrainPatch BuildPatch(V center,double halfExtentM,bool dry)
        {
            if (double.IsNaN(halfExtentM) || double.IsInfinity(halfExtentM) || halfExtentM <= 0 || halfExtentM > 250000)
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
            var geography=new List<HeightTriangle>();
            foreach(int t in triangleIds)
            {
                int a=world.Surface.Triangles[t],b=world.Surface.Triangles[t+1],c=world.Surface.Triangles[t+2];
                double minX=double.MaxValue,minY=double.MaxValue,maxX=double.MinValue,maxY=double.MinValue;
                foreach(int id in new[]{a,b,c})
                {
                    V delta=profiles[id].Center-center;double x=V.Dot(delta,east)*RadiusM,y=V.Dot(delta,north)*RadiusM;
                    minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
                }
                double bound=halfExtentM*1.6+20+maximumSupport*.15;
                if(minX>bound||maxX< -bound||minY>bound||maxY< -bound)continue;
                triangles.Add(new HeightTriangle(profiles[a],profiles[b],profiles[c],RadiusM));
                if(Scaffold!=null)
                {
                    int start=Scaffold.Refined?t*4:t,count=Scaffold.Refined?4:1;
                    for(int k=0;k<count;k++)
                    {
                        int at=start+k*3,aa=Scaffold.Triangles[at],bb=Scaffold.Triangles[at+1],cc=Scaffold.Triangles[at+2];
                        geography.Add(new HeightTriangle(Scaffold.Vertex(aa),Scaffold.Vertex(bb),Scaffold.Vertex(cc),RadiusM,
                            Scaffold.Ridge(aa,bb),Scaffold.Ridge(bb,cc),Scaffold.Ridge(cc,aa)));
                    }
                }
            }
            var rootIds=new int[triangleIds.Count];triangleIds.CopyTo(rootIds);
            return new LocalTerrainPatch(this, center, halfExtentM, sources, triangles.ToArray(), seed,geography.ToArray(),dry,
                Amplification?.Region(rootIds));
        }

        // Cubic triangular surface: exact source heights and shared edge curves. Bounded
        // Bezier controls prevent overshooting the triangle's source elevation range.
        internal readonly struct HeightTriangle
        {
            readonly double ax,ay,az,bx,by,bz,cx,cy,cz,ha,hb,hc,ab,ac,ba,bc,ca,cb,mid;
            readonly double ridgeAB,ridgeBC,ridgeCA;
            public HeightTriangle(LocalTerrainProfile a,LocalTerrainProfile b,LocalTerrainProfile c,double radius,double ridgeAB=0,double ridgeBC=0,double ridgeCA=0)
            {
                this.ridgeAB=ridgeAB;this.ridgeBC=ridgeBC;this.ridgeCA=ridgeCA;
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
                // Shared, non-river edges carry divides. Ridges are connected
                // to the drainage graph, not arbitrary periodic noise contours.
                double xy=4*x*y,yz=4*y*z,zx=4*z*x;
                height+=xy*xy*ridgeAB+yz*yz*ridgeBC+zx*zx*ridgeCA;
                return true;
            }
        }

        public sealed class LocalTerrainPatch
        {
            readonly LocalTerrain owner; readonly LocalTerrainProfile[] sources; readonly int seed;
            readonly HeightTriangle[] triangles;
            readonly HeightTriangle[] geography;
            readonly LocalHydrology.Patch water;
            readonly HyperAmplification.Patch amplified;
            readonly bool dry;
            public readonly V Center, East, North;
            public readonly double HalfExtentM;
            public int CandidateCount => sources.Length;
            public LocalHydrology.Piece[] ChannelPieces(double spacingM=16)=>amplified?.RiverscapePieces(Center,HalfExtentM*owner.HorizontalScale)??water?.Pieces()??Array.Empty<LocalHydrology.Piece>();
            public RiverscapeSpan[] RiverSpans(double minimumWidthM=0)=>amplified?.RiverscapeSpans(Center,HalfExtentM*owner.HorizontalScale,minimumWidthM)??Array.Empty<RiverscapeSpan>();
            internal LocalTerrainPatch(LocalTerrain owner, V center, double extent, LocalTerrainProfile[] sources, HeightTriangle[] triangles, int seed,HeightTriangle[] geography,bool dry,HyperAmplification.Patch amplified)
            {
                this.owner = owner; Center = center; HalfExtentM = extent; this.sources = sources; this.triangles=triangles;this.seed = seed;
                this.geography=geography;
                this.amplified=amplified;this.dry=dry;
                amplified?.SetRiverRegion(center,extent*owner.HorizontalScale);
                East = V.Cross(Math.Abs(center.Z) < .99 ? new V(0, 0, 1) : new V(0, 1, 0), center).Unit;
                North = V.Cross(center, East).Unit;
                water=dry||amplified!=null?null:owner.Hydrology.Region(center,extent);
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
                // Compress macro geography, not metre-sized local landforms. The same physical
                // ridge/noise wavelengths survive on a smaller playable planet; base anchors stay exact.
                V km = new V(px / 1000, py / 1000, pz / 1000)*owner.HorizontalScale;
                double broad = amplified==null?SphereNoise.Sample(km, seed ^ 0x1837, .16):0;
                double medium = amplified==null?SphereNoise.Sample(km, seed ^ 0x7917, .63):0;
                double fine = amplified==null?SphereNoise.Sample(km, seed ^ 0x2573, 2.1):0;
                double micro = amplified==null?SphereNoise.Sample(km, seed ^ 0x6819, 10):0;
                double sum = 0, hills = 0, mountains = 0, plateau = 0, sediment = 0, glacier = 0, sharpness = 0, bestWeight = -1, red = 0, green = 0, blue = 0, ocean = 0;
                bool heightOnly=dry;double rockA=0,rockB=0;
                int cell = -1; Biome biome = Biome.Grassland;var ground=new GroundProfile();
                for(int sourceIndex=0;sourceIndex<sources.Length;sourceIndex++)
                {
                    ref readonly var f=ref sources[sourceIndex];
                    double x = px - f.Center.X * owner.RadiusM, y = py - f.Center.Y * owner.RadiusM, z = pz - f.Center.Z * owner.RadiusM;
                    double dist2 = (x * x + y * y + z * z) / (f.SupportM * f.SupportM);
                    if (dist2 >= 1) continue;
                    // Compact C2 kernel: derivatives vanish at support edges; no closest-N switching seams.
                    double r = Math.Sqrt(dist2), t = 1 - r, weight = t * t * t * t * (1 + 4 * r);
                    sum += weight; hills += weight * f.HillHeightM;
                    sharpness+=weight*f.Sharpness;
                    if(!heightOnly)ground.Add(f.Ground,weight);
                    else
                    {
                        // Match the quantized two lithology components used by
                        // ledge height, without blending thirteen soil fields,
                        // three colours and four unused rock components.
                        rockA=D.Q(rockA+D.Q(f.Ground.RockMixA.Z*weight));
                        rockB=D.Q(rockB+D.Q(f.Ground.RockMixB.X*weight));
                    }
                    if (f.Cell < owner.world.Hydrology.OceanConnected.Length && owner.world.Hydrology.OceanConnected[f.Cell]) ocean += weight;
                    mountains += weight * f.MountainHeightM; plateau += weight * f.Plateau;
                    sediment += weight * f.Sediment; glacier += weight * f.Glacial;
                    if(!heightOnly){var color = CoverColor(f.Biome); red += weight * color.X; green += weight * color.Y; blue += weight * color.Z;}
                    if (weight > bestWeight) { bestWeight = weight; cell = f.Cell; biome = f.Biome;ground.Bedrock=f.Ground.Bedrock;ground.Deposit=f.Ground.Deposit; }
                }
                if (sum <= 0) throw new InvalidOperationException("Terrain patch has no supporting world cells.");
                if(amplified!=null)
                {
                    var surface=amplified.Sample(point,dry);
                    if(!heightOnly)ground.Divide(sum);
                    return new LocalTerrainSample{ElevationM=D.Q(surface.Height,1000),PreWaterElevationM=D.Q(surface.UnshapedHeight,1000),
                        BaseElevationM=D.Q(surface.UnshapedHeight,1000),MountainHeightM=mountains/sum,HillHeightM=hills/sum,
                        Plateau=plateau/sum,Sediment=sediment/sum,GlacialInfluence=glacier/sum,Cell=cell,Biome=biome,Ground=ground,
                        CoverColor=new V(red/sum,green/sum,blue/sum),OceanInfluence=ocean/sum,
                        WaterSurfaceM=surface.Water,WaterMaskM=surface.Mask,RiverInfluence=surface.RiverInfluence,
                        WaterKind=surface.Kind,WaterBodyKey=surface.BodyKey,WaterGradient=surface.Gradient,WaterFootprintM=surface.Footprint,
                        WaterFlow=surface.Flow,WaterSpeedMPerS=surface.Speed,WaterTurbulence=surface.Turbulence,ContinuousWaterSurface=surface.Kind==LocalWaterKind.River,
                        WaterMinimumM=surface.Minimum,WaterMaximumM=surface.Maximum};
                }
                double baseM = double.NaN;
                for(int i=0;i<triangles.Length;i++)if(triangles[i].Height(point,out double value)){baseM=value;break;}
                if(double.IsNaN(baseM))throw new InvalidOperationException("Terrain patch did not resolve a containing world triangle.");
                double geographicM=baseM;
                if(geography.Length>0)
                {
                    bool found=false;for(int i=0;i<geography.Length;i++)if(geography[i].Height(point,out double value)){geographicM=value;found=true;break;}
                    if(!found)throw new InvalidOperationException("Terrain patch did not resolve its geographic scaffold triangle.");
                }
                double h = hills / sum, m = mountains / sum, s = sediment / sum;
                // Six fixed octaves: coherent masses and subordinate spurs,
                // rather than periodic bands/phasor waves. Drainage subsequently
                // supplies the actual valley floor, banks and water as one field.
                double mountainShape=geography.Length==0&&m>0?TerrainFractal.Mountains(km*1000,seed^0x7135,sharpness/sum,broad):0;
                // Separate hill-scale relief from mountain masses. A restrained,
                // lithology-weighted cut adds ledges, not global stair terraces.
                double rounded=SphereNoise.Sample(km,seed^0x4b13,1.25);
                double hillShape=.55*medium+.38*rounded+.09*fine;
                // Sub-grid roughness is subordinate to geographic divides and
                // broad eroded slopes, never kilometre-tall independent spikes.
                // Keep its physical wavelength when horizontal scale changes.
                double detailScale=geography.Length>0?Math.Min(280,m*.18)*(1-.8*s):0;
                double rough=geography.Length>0?SphereNoise.Sample(km,seed^0x1a53,.42):0;
                double residual = m*mountainShape + (geography.Length>0?Math.Min(h,35):h) * hillShape + detailScale*(.7*rough+.18*fine+.025*micro)+(.12+.18*(1-s))*micro;
                // Sea level is a hard continuity anchor: do not manufacture new islands/coasts from high-frequency noise.
                double coast = Smooth(Math.Abs(baseM) / 100);
                if (baseM < 0) residual = (12 * broad + 4 * medium + fine) * coast;
                else residual = Math.Max(-.9 * baseM, residual * coast);
                if(!heightOnly)ground.Divide(sum);
                // Zero-height noise used to lock every coast to the coarse
                // triangle contour. A bounded continuous shoreline band permits
                // inlets/headlands on flat cells without ocean-cell borders.
                double oceanFraction=ocean/sum;
                double shoreBand=(1-Smooth(Math.Abs(baseM)/65))*OrogenMorphology.Smooth(.001,.08,oceanFraction);
                residual+=shoreBand*(3.5*medium+1.2*fine);
                double strata=heightOnly?D.Q(rockA*(1/sum))+D.Q(rockB*(1/sum)):ground.RockWeight((int)RockFamily.Sandstone)+ground.RockWeight((int)RockFamily.Limestone);
                double ledge=strata*Math.Min(9,m*.012+h*.06)*(1-s)*Math.Sin((baseM+residual)/24+medium*.4);
                double elevation=geographicM+residual+ledge;
                double level=oceanFraction>.001?0:double.NaN,mask=oceanFraction>.001?-elevation:-1000000;
                double preWater=elevation,river=0;
                water?.Apply(point,ref elevation,ref level,ref mask,out river);
                elevation=D.Q(elevation,1000);
                return new LocalTerrainSample { ElevationM = elevation, PreWaterElevationM=preWater, BaseElevationM = D.Q(baseM, 1000),
                    MountainHeightM = m, HillHeightM = h, Plateau = plateau / sum, Sediment = s,
                    GlacialInfluence = glacier / sum, Cell = cell, Biome = biome,Ground=ground,
                    CoverColor = new V(red / sum, green / sum, blue / sum), OceanInfluence = oceanFraction,
                    WaterSurfaceM = level,WaterMaskM=mask,RiverInfluence=river };
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
        public GroundProfile Ground;
        internal V Center, FabricOrigin, Axis, Across, Gradient;
        internal double SupportM, Phase;
    }
    public enum LocalWaterKind : byte { None, River, Lake, Ocean }
    public struct LocalTerrainSample
    {
        public double ElevationM, PreWaterElevationM, BaseElevationM, WaterSurfaceM, WaterMaskM, RiverInfluence, MountainHeightM, HillHeightM, Plateau, Sediment, GlacialInfluence, OceanInfluence;
        public int Cell;
        public V CoverColor;
        public Biome Biome;
        public GroundProfile Ground;
        // Derived local geometry, not simulation ownership or saved world data.
        // Gradient is metres of surface elevation per unit planetary direction.
        // Footprint excludes depth: the renderer clips against its actual land
        // triangles separately rather than mixing two different zero crossings.
        public LocalWaterKind WaterKind;
        public ulong WaterBodyKey;
        public V WaterGradient;
        public V WaterFlow;
        public double WaterSpeedMPerS,WaterTurbulence;
        public double WaterFootprintM,WaterMinimumM,WaterMaximumM;
        public bool ContinuousWaterSurface;
        public bool Underwater => !double.IsNaN(WaterSurfaceM) && WaterMaskM>=0 && ElevationM < WaterSurfaceM;
    }
}
