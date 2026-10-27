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
            double sizeM = 2048, int segments = 64, CancellationToken token = default,double[]? edgeSteps=null,TerrainSampleCache? sampleCache=null)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            if (segments < 4 || segments > 128 || sizeM <= 0 || sizeM > 131072 || double.IsNaN(sizeM)) throw new ArgumentOutOfRangeException(nameof(segments));
            var mesh = new LocalTerrainMesh { EastM = eastM, NorthM = northM, SizeM = sizeM, Segments = segments, MinHeightM = double.MaxValue, MaxHeightM = double.MinValue };
            double step = sizeM / segments;
            double extent=(sizeM*1.1+step*2)/frame.HorizontalScale;
            var patch = extent<=250000?terrain.Patch(frame.Position(eastM + sizeM / 2, northM + sizeM / 2),extent):null;
            var patchCache=new System.Collections.Generic.Dictionary<(int,int),LocalTerrain.LocalTerrainPatch>();
            LocalTerrainSample Sample(double x,double z,V p)
            {
                if(sampleCache!=null&&sampleCache.TryGet(p,out var cachedSample))return cachedSample;
                // LOD changes tessellation, never the value of a shared point.
                // Stopping the height hierarchy at a tile-dependent depth made
                // adjacent detail levels disagree and exposed their skirts.
                if(patch!=null){var value=patch.Sample(p);sampleCache?.Store(p,value);return value;}
                // A far tile on a shrunken planet spans a large geographical area.
                // Query bounded subpatches instead of constructing a planet-sized candidate bag.
                double box=100000*frame.HorizontalScale;int a=(int)Math.Floor(x/box),b=(int)Math.Floor(z/box);
                if(!patchCache.TryGetValue((a,b),out var local)){local=terrain.Patch(frame.Position((a+.5)*box,(b+.5)*box),150000);patchCache.Add((a,b),local);}var sampled=local.Sample(p);sampleCache?.Store(p,sampled);return sampled;
            }
            int side = segments + 1, halo = side + 2, ground = side * side;
            mesh.GroundVertices = ground;
            // Every terrain LOD needs the grass/moss/lichen material mixture.
            // Only nearby tiles create geometry from this compact regular grid.
            mesh.GroundCover=new float[ground*16];
            mesh.MidCover=new float[ground*6];
            mesh.ForestCover=new float[ground*8];
            var heights = new double[halo * halo]; var samples = new LocalTerrainSample[ground];
            for (int z = -1; z <= side; z++)
            {
                token.ThrowIfCancellationRequested();
                for (int x = -1; x <= side; x++)
                {
                    var position = frame.Position(eastM + x * step, northM + z * step); var sample = Sample(eastM+x*step,northM+z*step,position);
                    double height = frame.SurfaceY(position, sample.ElevationM); heights[(z + 1) * halo + x + 1] = height;
                    if (x >= 0 && x < side && z >= 0 && z < side) samples[z * side + x] = sample;
                }
            }
            // Presentation-only edge collapse: the finer boundary follows the adjacent coarse triangle edge.
            // This prevents exposed vertical skirts/sawteeth; the authoritative height function is unchanged.
            if(edgeSteps!=null)
            {
                if(edgeSteps.Length!=4)throw new ArgumentException("Four edge sample spacings required.",nameof(edgeSteps));
                for(int boundary=0;boundary<4;boundary++)
                {
                    double coarse=edgeSteps[boundary];if(coarse<=step*1.001)continue;
                    for(int i=0;i<side;i++)
                    {
                        int x=boundary==0?0:boundary==1?segments:i,z=boundary==2?0:boundary==3?segments:i;
                        double along=(boundary<2?northM:eastM)+i*step,lo=Math.Floor(along/coarse)*coarse,t=(along-lo)/coarse;
                        double Height(double a){double xx=boundary<2?eastM+x*step:a,zz=boundary<2?a:northM+z*step;var p=frame.Position(xx,zz);return frame.SurfaceY(p,Sample(xx,zz,p).ElevationM);}
                        heights[(z+1)*halo+x+1]=Height(lo)*(1-t)+Height(lo+coarse)*t;
                    }
                }
            }
            mesh.Positions = new float[ground * 3]; mesh.Normals = new float[mesh.Positions.Length]; mesh.Colors = new float[ground * 4];
            mesh.SurfaceWeights=new float[mesh.Colors.Length];
            mesh.GroundWeightsB=new float[mesh.Colors.Length];
            mesh.Lithology=new float[ground*6];
            mesh.WaterPositions = new float[ground * 3]; mesh.WaterColors = new float[ground * 4];
            for (int z = 0; z < side; z++) for (int x = 0; x < side; x++)
            {
                int i = z * side + x, at = (z + 1) * halo + x + 1; var sample = samples[i]; double y = heights[at];
                mesh.MinHeightM = Math.Min(mesh.MinHeightM, y); mesh.MaxHeightM = Math.Max(mesh.MaxHeightM, y);
                mesh.Positions[i * 3] = (float)(x * step); mesh.Positions[i * 3 + 1] = (float)y; mesh.Positions[i * 3 + 2] = (float)(z * step);
                mesh.WaterPositions[i * 3] = (float)(x * step); mesh.WaterPositions[i * 3 + 1] = (float)frame.SurfaceY(frame.Position(eastM + x * step, northM + z * step), 0); mesh.WaterPositions[i * 3 + 2] = (float)(z * step);
                mesh.WaterColors[i * 4] = .13f; mesh.WaterColors[i * 4 + 1] = .37f; mesh.WaterColors[i * 4 + 2] = .46f; mesh.WaterColors[i * 4 + 3] = (float)sample.OceanInfluence;
                double nx = (heights[at - 1] - heights[at + 1]) / (2 * step), nz = (heights[at - halo] - heights[at + halo]) / (2 * step);
                double length = Math.Sqrt(nx * nx + 1 + nz * nz); double up = 1 / length;
                mesh.Normals[i * 3] = (float)(nx / length); mesh.Normals[i * 3 + 1] = (float)up; mesh.Normals[i * 3 + 2] = (float)(nz / length);
                double snow=sample.Biome==Biome.Ice?D.Clamp((up-.55)/.4):0;
                var groundSurface=LocalGeology.Surface(sample.Ground,frame.Position(eastM+x*step,northM+z*step),terrain.SourceSeed,
                    sample.ElevationM,up,sample.OceanInfluence,snow,terrain.RadiusM*frame.HorizontalScale);
                for(int layer=0;layer<4;layer++){mesh.SurfaceWeights[i*4+layer]=(float)groundSurface.Weight(layer);mesh.GroundWeightsB[i*4+layer]=(float)groundSurface.Weight(layer+4);}
                for(int layer=0;layer<6;layer++)mesh.Lithology[i*6+layer]=(float)sample.Ground.RockWeight(layer);
                V color = groundSurface.Color;
                double canopyShade=sample.Underwater?0:TreeFlora.CanopyShade(sample.Forest,frame.Position(eastM+x*step,northM+z*step),terrain.SourceSeed,terrain.RadiusM*frame.HorizontalScale)*TreeFlora.SlopeAdmission(up);
                var cover=GroundFlora.Surface(sample.Cover,frame.Position(eastM+x*step,northM+z*step),terrain.SourceSeed,
                    terrain.RadiusM*frame.HorizontalScale,sample.Ground,sample.ElevationM,sample.BaseElevationM,up,sample.Underwater,snow,sample.Biome,canopyShade);
                double coverage=D.Clamp(cover.Coverage);
                var mid=sample.MidCover;double midAmount=sample.Underwater||sample.ElevationM<2?0:
                    mid.Coverage*MidFlora.StandDensity(frame.Position(eastM+x*step,northM+z*step),terrain.SourceSeed,terrain.RadiusM*frame.HorizontalScale)*D.Clamp((up-.65)/.25);
                int midAt=i*6;mesh.MidCover[midAt]=(float)mid.Color.X;mesh.MidCover[midAt+1]=(float)mid.Color.Y;mesh.MidCover[midAt+2]=(float)mid.Color.Z;
                mesh.MidCover[midAt+3]=(float)midAmount;mesh.MidCover[midAt+4]=(float)mid.HeightM;mesh.MidCover[midAt+5]=(float)mid.WidthM;
                var forest=TreeFlora.Surface(sample.Forest,frame.Position(eastM+x*step,northM+z*step),terrain.SourceSeed,terrain.RadiusM*frame.HorizontalScale,sample.ElevationM,up,sample.Underwater);
                int forestAt=i*8;mesh.ForestCover[forestAt]=(float)forest.Color.X;mesh.ForestCover[forestAt+1]=(float)forest.Color.Y;mesh.ForestCover[forestAt+2]=(float)forest.Color.Z;
                mesh.ForestCover[forestAt+3]=(float)forest.Coverage;mesh.ForestCover[forestAt+4]=(float)forest.HeightM;mesh.ForestCover[forestAt+5]=(float)forest.WidthM;mesh.ForestCover[forestAt+6]=(float)forest.Openness;
                // Discrete representative anatomy in the existing spare word;
                // preserve bits, never interpolate this as a numeric float.
                mesh.ForestCover[forestAt+7]=BitConverter.Int32BitsToSingle(forest.MorphologyCode);
                double visibleCover=D.Clamp(coverage/.45);
                color=color*(1-visibleCover)+cover.Color*(.90*visibleCover);
                if(mesh.GroundCover.Length>0)
                {
                    int coverAt=i*16;mesh.GroundCover[coverAt]=(float)cover.Color.X;mesh.GroundCover[coverAt+1]=(float)cover.Color.Y;
                    mesh.GroundCover[coverAt+2]=(float)cover.Color.Z;mesh.GroundCover[coverAt+3]=(float)coverage;
                    for(int form=0;form<9;form++)mesh.GroundCover[coverAt+4+form]=(float)cover.Weight(form);
                    mesh.GroundCover[coverAt+13]=(float)cover.Stature;mesh.GroundCover[coverAt+14]=(float)cover.LeafWidth;mesh.GroundCover[coverAt+15]=(float)y;
                }
                mesh.Colors[i * 4] = (float)color.X; mesh.Colors[i * 4 + 1] = (float)color.Y; mesh.Colors[i * 4 + 2] = (float)color.Z; mesh.Colors[i * 4 + 3] = (float)coverage;
            }
            mesh.Triangles=SurfaceTriangles(segments);
            // Planned neighbors already share collapsed triangle edges above.
            // The old >=80m perimeter skirts also appeared on INTERIOR fallback
            // boundaries, making manufactured cliffs through loaded terrain.
            // Keep the sampled land surface; never extrude those opaque walls.
            var waterSamples=LocalChannelMesh.Refine(mesh,terrain,frame,patch,samples,token);
            LocalWaterMesh.Build(mesh,frame,waterSamples,terrain.Amplification!=null);
            if(terrain.Amplification!=null)RiverscapeMesh.Append(mesh,frame,patch,waterSamples,token);
            GroundCoverGrid.ExcludeWater(mesh);
            // Distant vegetation is now a separately culled baked-tree field.
            // Do not bake unrelated statistical ovals into terrain products.
            mesh.BuildMilliseconds = watch.Elapsed.TotalMilliseconds; return mesh;
        }
    }
}
