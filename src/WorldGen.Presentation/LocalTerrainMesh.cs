using System;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Double-precision planet anchor; local meshes are disposable, metre-scale render products.</summary>
    public sealed class LocalTerrainFrame
    {
        public readonly V Origin, East, North;
        public readonly double RadiusM, AltitudeM,HorizontalScale;
        public LocalTerrainFrame(V origin, double radiusM, double altitudeM,double horizontalScale=1)
        {
            if(horizontalScale<.01||horizontalScale>2||double.IsNaN(horizontalScale))throw new ArgumentOutOfRangeException(nameof(horizontalScale));
            Origin = origin.Unit; RadiusM = radiusM*horizontalScale; AltitudeM = altitudeM;HorizontalScale=horizontalScale;
            East = V.Cross(Math.Abs(Origin.Z) < .99 ? new V(0, 0, 1) : new V(0, 1, 0), Origin).Unit;
            North = V.Cross(Origin, East).Unit;
        }
        LocalTerrainFrame(V origin,V east,V north,double radius,double altitude,double scale)
        {Origin=origin;East=east;North=north;RadiusM=radius;AltitudeM=altitude;HorizontalScale=scale;}
        public static LocalTerrainFrame FromExactBasis(V origin,V east,V north,double radius,double altitude,double scale)
        {
            if(double.IsNaN(radius)||double.IsInfinity(radius)||radius<=0||scale<.01||scale>2||double.IsNaN(scale)||double.IsNaN(altitude)||double.IsInfinity(altitude)
                ||Math.Abs(origin.Length-1)>.00001||Math.Abs(east.Length-1)>.00001||Math.Abs(north.Length-1)>.00001)throw new ArgumentException("Invalid terrain frame basis.");
            return new LocalTerrainFrame(origin,east,north,radius,altitude,scale);
        }
        public V Position(double x, double z) => (Origin + East * (x / RadiusM) + North * (z / RadiusM)).Unit;
        public double SurfaceY(V position, double heightM) => (RadiusM + heightM) *
            (position.X * Origin.X + position.Y * Origin.Y + position.Z * Origin.Z) - RadiusM - AltitudeM;
    }

    public sealed class LocalTerrainMesh
    {
        internal TerrainUploadBufferPool? UploadPool;
        internal TerrainCoverBuffers? CoverBuffers;
        public TerrainCoverBuffers? TakeCoverBuffers()
        {
            var owned=Interlocked.Exchange(ref CoverBuffers,null);
            if(owned!=null)GroundCover=MidCover=ForestCover=Array.Empty<float>();
            return owned;
        }
        /// <summary>Release transport attributes and any cover lease not transferred
        /// to the upload/tile. Transferred resident grids remain with their owner.</summary>
        public void ReleaseUploadBuffers()
        {
            var abandoned=TakeCoverBuffers();abandoned?.Dispose();
            var pool=System.Threading.Interlocked.Exchange(ref UploadPool,null);if(pool==null)return;
            pool.Return(Positions);pool.Return(Normals);pool.Return(Colors);pool.Return(SurfaceWeights);pool.Return(GroundWeightsB);pool.Return(Lithology);pool.Return(WaterPositions);pool.Return(WaterColors);pool.Return(Triangles);pool.Return(WaterTriangles);
            Positions=Normals=Colors=SurfaceWeights=GroundWeightsB=Lithology=WaterPositions=WaterColors=Array.Empty<float>();Triangles=WaterTriangles=Array.Empty<int>();
        }
        public float[] Positions = Array.Empty<float>(), Normals = Array.Empty<float>(), Colors = Array.Empty<float>();
        public float[] WaterPositions = Array.Empty<float>(), WaterColors = Array.Empty<float>();
        // UV1: soil/sand/gravel/clay; UV2: rock/organic/salt/ice. Independent from geometry.
        public float[] SurfaceWeights = Array.Empty<float>();
        public float[] GroundWeightsB = Array.Empty<float>();
        // Six continuous lithology weights, independent of soil and render LOD.
        public float[] Lithology = Array.Empty<float>();
        // Regular-grid ground flora: RGB/cover,9 growth-form weights,stature,
        // leaf width and rendered height. Local GPU cover reuses this surface.
        public float[] GroundCover = Array.Empty<float>();
        // Distant mid-height cover: RGB,projected cover fraction,height,width.
        public float[] MidCover = Array.Empty<float>();
        public float[] ForestCover = Array.Empty<float>();
        public int[] Triangles = Array.Empty<int>();
        public int[] WaterTriangles = Array.Empty<int>();
        public int[] WaterClipTriangles = Array.Empty<int>();
        public double EastM, NorthM, SizeM, MinHeightM, MaxHeightM, BuildMilliseconds;
        public int Segments, GroundVertices;
        public static int[] SurfaceTriangles(int segments)
        {
            if(segments<4||segments>128)throw new ArgumentOutOfRangeException(nameof(segments));
            int side=segments+1,cursor=0;var indices=new int[segments*segments*6];
            for(int z=0;z<segments;z++)for(int x=0;x<segments;x++)
            {int a=z*side+x;indices[cursor++]=a;indices[cursor++]=a+side;indices[cursor++]=a+1;indices[cursor++]=a+1;indices[cursor++]=a+side;indices[cursor++]=a+side+1;}
            return indices;
        }
        public static LocalTerrainMesh Build(LocalTerrain terrain, LocalTerrainFrame frame, double eastM, double northM,
            double sizeM = 2048, int segments = 64, CancellationToken token = default,double[]? edgeSteps=null,TerrainSampleCache? sampleCache=null,TerrainBuildTiming? timing=null,bool coverageOnly=false)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            if (segments < 4 || segments > 128 || sizeM <= 0 || sizeM > 131072 || double.IsNaN(sizeM)) throw new ArgumentOutOfRangeException(nameof(segments));
            var mesh = new LocalTerrainMesh { EastM = eastM, NorthM = northM, SizeM = sizeM, Segments = segments, MinHeightM = double.MaxValue, MaxHeightM = double.MinValue };
            double step = sizeM / segments;
            int normalStride=terrain.Amplification==null?1:Math.Max(1,(int)Math.Ceiling(HyperAmplification.FinestSpacingM/step));
            double normalRadius=normalStride*step;
            bool distant=terrain.Amplification!=null&&step>=128;
            bool coarseGeometry=terrain.Amplification!=null&&step>256;
            double extent=(sizeM*1.1+step*2)/frame.HorizontalScale;
            bool nearCoverage=coverageOnly&&sizeM<=4096&&extent<=250000;
            var patch = (!coverageOnly||nearCoverage)&&extent<=250000?terrain.Patch(frame.Position(eastM + sizeM / 2, northM + sizeM / 2),extent):null;
            // The same index was previously prepared only by channel refinement,
            // after the regular grid had already scanned every river station.
            token.ThrowIfCancellationRequested();
            // Sparse distant grids query only a few hundred points across many
            // kilometres. Eagerly constructing EVERY small river in that whole
            // region costs more than the grid. Keep exact point admission there;
            // dense near grids still amortize the shared finite-span index.
            if((!coverageOnly&&!coarseGeometry||nearCoverage)&&(segments>=32||sizeM<=4096))patch?.RiverSpans();
            timing?.Mark(0);
            var patchCache=new System.Collections.Generic.Dictionary<(int,int),LocalTerrain.LocalTerrainPatch>();
            var horizon=coverageOnly||distant?terrain.CreateMapHeightCursor():null;
            var horizonMaterials=coverageOnly||distant?new System.Collections.Generic.List<int>():null;
            LocalTerrainSample Sample(double x,double z,V p,bool geometryOnly=false,LocalTerrainSample? knownGeometry=null)
            {
                // Near fallback is still coarse in materials, but its actual
                // land must contain the same bed as its explicit water ribbon.
                // The far horizon remains the fast approximate height product.
                if(nearCoverage)
                {
                    LocalTerrainSample geometry;
                    if(knownGeometry.HasValue)geometry=knownGeometry.Value;
                    else if(sampleCache!=null&&sampleCache.TryGetGeometry(p,out var cached))geometry=cached;
                    else{geometry=patch!.Geometry(p);sampleCache?.StoreGeometry(p,geometry);if(timing!=null&&geometryOnly)timing.GeometryQueries++;}
                    if(geometryOnly)return geometry;
                    if(timing!=null)timing.FullSurfaceQueries++;
                    var ecology=horizon!.Sample(p,Math.Max(256,step));terrain.FillMapMaterials(p,ref ecology,horizonMaterials!);
                    return LocalTerrain.LocalTerrainPatch.BlendEcology(ecology,ecology,ecology,ecology,0,0,geometry);
                }
                // Coverage is an explicitly approximate distant product, never
                // a requested-quality mesh or a source for exact sample caches.
                // Its footprint must stop the hierarchy before metre-scale
                // river fitting over an entire hundred-kilometre horizon.
                if(coverageOnly||coarseGeometry)
                {
                    // A distant render product samples the existing hierarchy
                    // at half its grid spacing. It never reads or writes the
                    // canonical cache; visible major-river beds are restored
                    // explicitly by the same bank refiner used below.
                    var coarse=horizon!.Sample(p,coverageOnly?Math.Max(256,step):Math.Max(256,step*.5));
                    if(!geometryOnly)terrain.FillMapMaterials(p,ref coarse,horizonMaterials!);
                    if(timing!=null){if(geometryOnly)timing.GeometryQueries++;else timing.FullSurfaceQueries++;}
                    return coarse;
                }
                if(sampleCache!=null&&(geometryOnly?sampleCache.TryGetGeometry(p,out var cachedSample):sampleCache.TryGet(p,out cachedSample)))return cachedSample;
                if(timing!=null){if(geometryOnly)timing.GeometryQueries++;else timing.FullSurfaceQueries++;}
                if(distant)
                {
                    // Zero footprint retains canonical height and hydrology.
                    // Reuse bounded local face stars instead of constructing
                    // many full ecology patches across a distant work unit.
                    var value=horizon!.Sample(p,0);sampleCache?.StoreGeometry(p,value);
                    if(!geometryOnly)terrain.FillMapMaterials(p,ref value,horizonMaterials!);
                    return value;
                }
                // Near/intermediate products retain canonical geometry.
                // Distant shared edges use their common sampling footprint
                // below, and resident transitions conform the actual edges.
                if(patch!=null){var value=geometryOnly?patch.Geometry(p):knownGeometry.HasValue?patch.Ecology(p,knownGeometry.Value):patch.Sample(p);if(geometryOnly)sampleCache?.StoreGeometry(p,value);else sampleCache?.Store(p,value);return value;}
                // A far tile on a shrunken planet spans a large geographical area.
                // Query bounded subpatches instead of constructing a planet-sized candidate bag.
                double box=10000*frame.HorizontalScale;int a=(int)Math.Floor(x/box),b=(int)Math.Floor(z/box);
                // A backdrop visits only a handful of points in each large
                // subpatch. Priming every river across that whole area can
                // take longer than sampling the complete coarse tile.
                // Keep the ordinary exact point query here; dense regular
                // tiles still prime their shared batch index above.
                if(!patchCache.TryGetValue((a,b),out var local)){local=terrain.Patch(frame.Position((a+.5)*box,(b+.5)*box),15000);token.ThrowIfCancellationRequested();patchCache.Add((a,b),local);}var sampled=geometryOnly?local.Geometry(p):local.Sample(p);if(geometryOnly)sampleCache?.StoreGeometry(p,sampled);else sampleCache?.Store(p,sampled);return sampled;
            }
            // Material resolution is independent of tessellation. This chart-
            // anchored grid is shared by adjacent tiles, not restarted per tile.
            // Grouped horizon products keep the old 32-segment product's
            // material lattice as well as its height spacing.
            double materialStep=coverageOnly?Math.Max(128,step*4):distant?step*2:32;
            bool sparseMaterials=terrain.Amplification!=null&&(coverageOnly||distant||step<16);
            var materials=new System.Collections.Generic.Dictionary<(int,int,double),LocalTerrainSample>();
            LocalTerrainSample MaterialNode(int x,int z,double nodeStep=0)
            {
                if(nodeStep==0)nodeStep=materialStep;
                if(materials.TryGetValue((x,z,nodeStep),out var found))return found;
                double xx=x*nodeStep,zz=z*nodeStep;var point=frame.Position(xx,zz);
                bool coarseMaterial=distant||nodeStep>=256;
                var value=coarseMaterial?horizon!.Sample(point,Math.Max(256,nodeStep)):Sample(xx,zz,point);
                if(coarseMaterial)terrain.FillMapMaterials(point,ref value,horizonMaterials!);
                materials.Add((x,z,nodeStep),value);return value;
            }
            LocalTerrainSample SurfaceSample(double x,double z,LocalTerrainSample geometry)
            {
                // Narrow channels and wet boundaries retain full local material
                // queries; do not blur river gravel or a shoreline across land.
                if(!sparseMaterials||!distant&&(geometry.Underwater||geometry.WaterKind==LocalWaterKind.River&&geometry.WaterFootprintM> -materialStep))
                {if(timing!=null)timing.SurfaceFallbacks++;return Sample(x,z,frame.Position(x,z),knownGeometry:geometry);}
                int ix=(int)Math.Floor(x/materialStep),iz=(int)Math.Floor(z/materialStep);
                var a=MaterialNode(ix,iz);var b=MaterialNode(ix+1,iz);var c=MaterialNode(ix,iz+1);var d=MaterialNode(ix+1,iz+1);
                if(!distant&&(a.Biome!=b.Biome||a.Biome!=c.Biome||a.Biome!=d.Biome||a.Underwater||b.Underwater||c.Underwater||d.Underwater))
                {if(timing!=null)timing.SurfaceFallbacks++;return Sample(x,z,frame.Position(x,z),knownGeometry:geometry);}
                return LocalTerrain.LocalTerrainPatch.BlendEcology(a,b,c,d,x/materialStep-ix,z/materialStep-iz,geometry);
            }
            int side = segments + 1, halo = side + normalStride*2, ground = side * side;
            mesh.GroundVertices = ground;
            // Every terrain LOD needs the grass/moss/lichen material mixture.
            // Only nearby tiles create geometry from this compact regular grid.
            mesh.GroundCover=new float[ground*16];
            mesh.MidCover=new float[ground*6];
            mesh.ForestCover=new float[ground*8];
            var heights = new double[halo * halo]; var samples = new LocalTerrainSample[ground];
            void SampleHeight(int x,int z,bool interior)
            {
                var position=frame.Position(eastM+x*step,northM+z*step);
                var sample=Sample(eastM+x*step,northM+z*step,position,sparseMaterials||!interior);
                if(timing!=null)timing.GeometrySamples++;
                heights[(z+normalStride)*halo+x+normalStride]=frame.SurfaceY(position,sample.ElevationM);
                if(interior)samples[z*side+x]=sample;
            }
            // Keep the original physical grid/one-cell halo and its sampling
            // order. Only extra axial strips serve the wider shading derivative.
            for (int z = -1; z <= side; z++)
            {
                token.ThrowIfCancellationRequested();
                for (int x = -1; x <= side; x++)
                    SampleHeight(x,z,x>=0&&x<side&&z>=0&&z<side);
            }
            for(int offset=2;offset<=normalStride;offset++)
            {
                token.ThrowIfCancellationRequested();
                for(int at=0;at<side;at++)
                {
                    SampleHeight(-offset,at,false);SampleHeight(segments+offset,at,false);
                    SampleHeight(at,-offset,false);SampleHeight(at,segments+offset,false);
                }
            }
            mesh.Positions = new float[ground * 3]; mesh.Normals = new float[mesh.Positions.Length]; mesh.Colors = new float[ground * 4];
            timing?.Mark(1);
            mesh.SurfaceWeights=new float[mesh.Colors.Length];
            mesh.GroundWeightsB=new float[mesh.Colors.Length];
            mesh.Lithology=new float[ground*6];
            mesh.WaterPositions = new float[ground * 3]; mesh.WaterColors = new float[ground * 4];
            for (int z = 0; z < side; z++) for (int x = 0; x < side; x++)
            {
                int i = z * side + x, at = (z + normalStride) * halo + x + normalStride; var sample = sparseMaterials?SurfaceSample(eastM+x*step,northM+z*step,samples[i]):samples[i]; double y = heights[at];
                // Distant bank vertices reuse this ecological grid. Retain the
                // enriched sample rather than the first pass's geometry only.
                if(distant)samples[i]=sample;
                mesh.MinHeightM = Math.Min(mesh.MinHeightM, y); mesh.MaxHeightM = Math.Max(mesh.MaxHeightM, y);
                mesh.Positions[i * 3] = (float)(x * step); mesh.Positions[i * 3 + 1] = (float)y; mesh.Positions[i * 3 + 2] = (float)(z * step);
                mesh.WaterPositions[i * 3] = (float)(x * step); mesh.WaterPositions[i * 3 + 1] = (float)frame.SurfaceY(frame.Position(eastM + x * step, northM + z * step), 0); mesh.WaterPositions[i * 3 + 2] = (float)(z * step);
                mesh.WaterColors[i * 4] = .13f; mesh.WaterColors[i * 4 + 1] = .37f; mesh.WaterColors[i * 4 + 2] = .46f; mesh.WaterColors[i * 4 + 3] = (float)sample.OceanInfluence;
                double nx = (heights[at - 1] - heights[at + 1]) / (2 * step), nz = (heights[at - halo] - heights[at + halo]) / (2 * step);
                var vertex=TerrainSurfaceVertex.Evaluate(terrain,frame,frame.Position(eastM+x*step,northM+z*step),sample,nx,nz);
                if(normalStride>1)
                {
                    // Fine mesh spacing must not turn each underlying eight-
                    // metre height triangle into a separately lit patch. Keep
                    // ecological/physical slopes above literal; filter shading only.
                    double sx=(heights[at-normalStride]-heights[at+normalStride])/(2*normalRadius);
                    double sz=(heights[at-normalStride*halo]-heights[at+normalStride*halo])/(2*normalRadius);
                    vertex.Normal=new V(sx,1,sz)/Math.Sqrt(sx*sx+1+sz*sz);
                }
                if(timing!=null)timing.MaterialSamples++;
                TerrainSurfaceVertex.Write(mesh,i,vertex,vertex,0);
            }
            double Height(double x,double z)
            {
                double fx=(x-eastM)/step,fz=(z-northM)/step;int ix=(int)Math.Round(fx),iz=(int)Math.Round(fz);
                if(ix>=-1&&ix<=side&&iz>=-1&&iz<=side&&Math.Abs(fx-ix)<1e-7&&Math.Abs(fz-iz)<1e-7)return heights[(iz+normalStride)*halo+ix+normalStride];
                var p=frame.Position(x,z);
                return frame.SurfaceY(p,coarseGeometry?Sample(x,z,p,true).ElevationM:patch!=null?patch.Elevation(p):Sample(x,z,p).ElevationM);
            }
            double ShadingHeight(double x,double z)
            {
                double fx=(x-eastM)/step,fz=(z-northM)/step;int ix=(int)Math.Round(fx),iz=(int)Math.Round(fz);
                bool axial=ix>=0&&ix<side&&iz>=-normalStride&&iz<side+normalStride
                    ||iz>=0&&iz<side&&ix>=-normalStride&&ix<side+normalStride;
                if(axial&&Math.Abs(fx-ix)<1e-7&&Math.Abs(fz-iz)<1e-7)return heights[(iz+normalStride)*halo+ix+normalStride];
                return Height(x,z);
            }
            TerrainSurfaceVertex Boundary(double x,double z,double h)
            {
                double fx=(x-eastM)/step,fz=(z-northM)/step;int ix=(int)Math.Round(fx),iz=(int)Math.Round(fz);
                var p=frame.Position(x,z);
                if(terrain.Amplification!=null&&h>256)
                {
                    // Both planned neighbours use the coarser edge's footprint,
                    // independent of their own interior sampling resolution.
                    var cursor=horizon??(horizon=terrain.CreateMapHeightCursor());
                    var coarse=cursor.Sample(p,Math.Max(256,h*.5));
                    horizonMaterials??=new System.Collections.Generic.List<int>();
                    double material=h*2;int mx=(int)Math.Floor(x/material),mz=(int)Math.Floor(z/material);
                    coarse=LocalTerrain.LocalTerrainPatch.BlendEcology(MaterialNode(mx,mz,material),MaterialNode(mx+1,mz,material),MaterialNode(mx,mz+1,material),MaterialNode(mx+1,mz+1,material),x/material-mx,z/material-mz,coarse);
                    double At(double xx,double zz){var at=frame.Position(xx,zz);return frame.SurfaceY(at,cursor.Sample(at,Math.Max(256,h*.5)).ElevationM);}
                    return TerrainSurfaceVertex.Evaluate(terrain,frame,p,coarse,(At(x-h,z)-At(x+h,z))/(2*h),(At(x,z-h)-At(x,z+h))/(2*h));
                }
                var sample=h<16&&sparseMaterials?SurfaceSample(x,z,Sample(x,z,p,true)):Sample(x,z,p);
                var vertex=TerrainSurfaceVertex.Evaluate(terrain,frame,p,sample,(Height(x-h,z)-Height(x+h,z))/(2*h),(Height(x,z-h)-Height(x,z+h))/(2*h));
                if(terrain.Amplification!=null&&h<HyperAmplification.FinestSpacingM)
                {
                    double radius=h*Math.Ceiling(HyperAmplification.FinestSpacingM/h);
                    double sx=(ShadingHeight(x-radius,z)-ShadingHeight(x+radius,z))/(2*radius);
                    double sz=(ShadingHeight(x,z-radius)-ShadingHeight(x,z+radius))/(2*radius);
                    vertex.Normal=new V(sx,1,sz)/Math.Sqrt(sx*sx+1+sz*sz);
                }
                return vertex;
            }
            timing?.Mark(2);TerrainSurfaceEdges.Apply(mesh,edgeSteps,Boundary,token);timing?.Mark(3);
            mesh.Triangles=SurfaceTriangles(segments);
            // Planned neighbors already share collapsed triangle edges above.
            // The old >=80m perimeter skirts also appeared on INTERIOR fallback
            // boundaries, making manufactured cliffs through loaded terrain.
            // Keep the sampled land surface; never extrude those opaque walls.
            var waterSamples=coverageOnly&&!nearCoverage?samples:LocalChannelMesh.Refine(mesh,terrain,frame,patch,samples,token,timing,edgeSteps);
            timing?.Mark(4);
            LocalWaterMesh.Build(mesh,frame,waterSamples,terrain.Amplification!=null,patch==null?null:new Func<ulong,V,SmallLake>(patch.LocalLakeAt));
            if((!coverageOnly||nearCoverage)&&terrain.Amplification!=null)RiverscapeMesh.Append(mesh,frame,patch,waterSamples,token);
            timing?.Mark(5);
            GroundCoverGrid.ExcludeWater(mesh);
            timing?.Mark(6);
            // Distant vegetation is now a separately culled baked-tree field.
            // Do not bake unrelated statistical ovals into terrain products.
            mesh.BuildMilliseconds = watch.Elapsed.TotalMilliseconds; return mesh;
        }
    }
}
