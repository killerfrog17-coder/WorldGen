using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Near water, cut existing terrain triangles along the channel banks
    /// and thalweg. LOD requests/resolutions/streaming are untouched. This avoids
    /// rounding metre-wide streams up to a grid cell or losing them between vertices.</summary>
    public static class LocalChannelMesh
    {
        struct Point {public double X,Z;public Point(double x,double z){X=x;Z=z;}}
        struct Cut {public double X,Z,DX,DZ,Length,Half;}
        public static LocalTerrainSample[] Refine(LocalTerrainMesh mesh,LocalTerrain terrain,LocalTerrainFrame frame,LocalTerrain.LocalTerrainPatch? patch,LocalTerrainSample[] original,System.Threading.CancellationToken token=default)
        {
            double step=mesh.SizeM/mesh.Segments;if(step>32||patch==null)return original;
            var source=patch.ChannelPieces(Math.Max(16,step*2));if(source.Length==0)return original;
            int side=mesh.Segments+1;var cuts=new List<Cut>();var buckets=new Dictionary<int,List<int>>();
            foreach(var p in source)
            {
                token.ThrowIfCancellationRequested();
                TerrainHandoff.Coordinates(frame,p.A,out double ax,out double az);TerrainHandoff.Coordinates(frame,p.B,out double bx,out double bz);
                ax-=mesh.EastM;bx-=mesh.EastM;az-=mesh.NorthM;bz-=mesh.NorthM;
                double dx=bx-ax,dz=bz-az,length=Math.Sqrt(dx*dx+dz*dz);if(length<.001)continue;
                double pad=p.HalfWidthM+1;
                int minX=Math.Max(0,(int)Math.Floor((Math.Min(ax,bx)-pad)/step)),maxX=Math.Min(mesh.Segments-1,(int)Math.Floor((Math.Max(ax,bx)+pad)/step));
                int minZ=Math.Max(0,(int)Math.Floor((Math.Min(az,bz)-pad)/step)),maxZ=Math.Min(mesh.Segments-1,(int)Math.Floor((Math.Max(az,bz)+pad)/step));
                if(minX>maxX||minZ>maxZ)continue;
                int id=cuts.Count;cuts.Add(new Cut{X=ax,Z=az,DX=dx/length,DZ=dz/length,Length=length,Half=p.HalfWidthM});
                for(int z=minZ;z<=maxZ;z++)for(int x=minX;x<=maxX;x++)
                {
                    double cx=(x+.5)*step-ax,cz=(z+.5)*step-az;
                    if(Math.Abs(cx*(-dz/length)+cz*dx/length)>pad+step*.72)continue;
                    int key=z*mesh.Segments+x;if(!buckets.TryGetValue(key,out var list)){list=new List<int>();buckets.Add(key,list);}list.Add(id);
                }
            }
            if(cuts.Count==0)return original;
            var positions=new List<float>(mesh.Positions);var normals=new List<float>(mesh.Normals);var colors=new List<float>(mesh.Colors);
            var weights=new List<float>(mesh.SurfaceWeights);var groundB=new List<float>(mesh.GroundWeightsB);var rocks=new List<float>(mesh.Lithology);
            var samples=new List<LocalTerrainSample>(original);while(samples.Count<mesh.Positions.Length/3)samples.Add(new LocalTerrainSample{WaterSurfaceM=double.NaN,WaterMaskM=-1});
            var indices=new List<int>();var shared=new Dictionary<(long,long),int>();
            static (long,long) Key(double x,double z)=>((long)Math.Round(x*1000000),(long)Math.Round(z*1000000));
            for(int i=0;i<original.Length;i++)shared[Key(positions[i*3],positions[i*3+2])]=i;
            int Vertex(Point p)
            {
                token.ThrowIfCancellationRequested();
                var key=Key(p.X,p.Z);if(shared.TryGetValue(key,out int found))return found;
                V at=frame.Position(mesh.EastM+p.X,mesh.NorthM+p.Z);var s=patch.Sample(at);
                double Height(double x,double z){var v=frame.Position(mesh.EastM+x,mesh.NorthM+z);return frame.SurfaceY(v,patch.Sample(v).ElevationM);}
                double here=frame.SurfaceY(at,s.ElevationM);
                double nx=(here-Height(p.X+1,p.Z)),nz=(here-Height(p.X,p.Z+1));
                double length=Math.Sqrt(nx*nx+1+nz*nz),up=1/length;
                var surface=LocalGeology.Surface(s.Ground,at,terrain.SourceSeed,s.ElevationM,up,s.OceanInfluence,s.Biome==Biome.Ice?D.Clamp((up-.55)/.4):0,terrain.RadiusM*frame.HorizontalScale);
                int id=samples.Count;samples.Add(s);shared.Add(key,id);
                double height=frame.SurfaceY(at,s.ElevationM);
                // Keep the existing collapsed tile perimeter exactly linear;
                // channel refinement cannot open cracks against another LOD.
                if(Math.Abs(p.X)<1e-7||Math.Abs(p.X-mesh.SizeM)<1e-7||Math.Abs(p.Z)<1e-7||Math.Abs(p.Z-mesh.SizeM)<1e-7)
                {
                    bool vertical=Math.Abs(p.X)<1e-7||Math.Abs(p.X-mesh.SizeM)<1e-7;
                    double along=(vertical?p.Z:p.X)/step;int low=Math.Max(0,Math.Min(mesh.Segments-1,(int)Math.Floor(along)));double t=along-low;
                    int first=vertical?low*side+(p.X>step?mesh.Segments:0):(p.Z>step?mesh.Segments*side:0)+low;
                    int next=first+(vertical?side:1);height=positions[first*3+1]*(1-t)+positions[next*3+1]*t;
                }
                positions.Add((float)p.X);positions.Add((float)height);positions.Add((float)p.Z);
                normals.Add((float)(nx/length));normals.Add((float)up);normals.Add((float)(nz/length));
                colors.Add((float)surface.Color.X);colors.Add((float)surface.Color.Y);colors.Add((float)surface.Color.Z);colors.Add(1);
                for(int k=0;k<4;k++){weights.Add((float)surface.Weight(k));groundB.Add((float)surface.Weight(k+4));}
                for(int k=0;k<6;k++)rocks.Add((float)s.Ground.RockWeight(k));return id;
            }
            void Triangle(int a,int b,int c,List<int> relevant)
            {
                if(relevant==null){indices.Add(a);indices.Add(b);indices.Add(c);return;}
                var polygons=new List<List<Point>>{new List<Point>{new Point(positions[a*3],positions[a*3+2]),new Point(positions[b*3],positions[b*3+2]),new Point(positions[c*3],positions[c*3+2])}};
                foreach(int id in relevant)
                {
                    var cut=cuts[id];
                    foreach(double offset in new[]{-cut.Half,0,cut.Half})
                    {
                        var next=new List<List<Point>>();
                        foreach(var polygon in polygons)
                        {
                            var left=new List<Point>();var right=new List<Point>();
                            for(int k=0;k<polygon.Count;k++)
                            {
                                var p=polygon[k];var q=polygon[(k+1)%polygon.Count];
                                double d=(p.X-cut.X)*(-cut.DZ)+(p.Z-cut.Z)*cut.DX-offset,e=(q.X-cut.X)*(-cut.DZ)+(q.Z-cut.Z)*cut.DX-offset;
                                if(d>=-1e-8)left.Add(p);if(d<=1e-8)right.Add(p);
                                if(d*e< -1e-14){double t=d/(d-e);var v=new Point(p.X+(q.X-p.X)*t,p.Z+(q.Z-p.Z)*t);left.Add(v);right.Add(v);}
                            }
                            if(left.Count>=3)next.Add(left);if(right.Count>=3)next.Add(right);
                        }
                        polygons=next;
                    }
                    // At a confluence only a few strips overlap. A hard geometry
                    // budget prevents pathological intersections from exploding.
                    if(polygons.Count>32)break;
                }
                foreach(var polygon in polygons)
                {
                    int first=Vertex(polygon[0]),previous=Vertex(polygon[1]);
                    for(int k=2;k<polygon.Count;k++){int current=Vertex(polygon[k]);if(first!=previous&&previous!=current&&first!=current){indices.Add(first);indices.Add(previous);indices.Add(current);}previous=current;}
                }
            }
            for(int z=0;z<mesh.Segments;z++)for(int x=0;x<mesh.Segments;x++)
            {
                token.ThrowIfCancellationRequested();
                buckets.TryGetValue(z*mesh.Segments+x,out var relevant);int a=z*side+x;
                Triangle(a,a+side,a+1,relevant);Triangle(a+1,a+side,a+side+1,relevant);
            }
            // Adjacent cells can see different finite channel pieces. Their
            // infinite cutting lines may leave T-junctions on the ORIGINAL
            // grid edges/diagonals. Share every such boundary point and fan
            // the affected triangle so land AND water use the same edge.
            var lines=new Dictionary<(int,int),List<int>>();
            int before=samples.Count;
            for(int id=0;id<before;id++)
            {
                if(id>=original.Length&&id<mesh.Positions.Length/3)continue; // skirts
                double x=positions[id*3],z=positions[id*3+2];
                for(int axis=0;axis<3;axis++)
                {
                    double value=(axis==0?x:axis==1?z:x+z)/step;int line=(int)Math.Round(value);
                    if(Math.Abs(value-line)>1e-6)continue;
                    var key=(axis,line);if(!lines.TryGetValue(key,out var list)){list=new List<int>();lines.Add(key,list);}list.Add(id);
                }
            }
            var stitched=new List<int>(indices.Count);
            for(int tri=0;tri<indices.Count;tri+=3)
            {
                var polygon=new List<int>();
                for(int k=0;k<3;k++)
                {
                    int a=indices[tri+k],b=indices[tri+(k+1)%3];polygon.Add(a);
                    double ax=positions[a*3],az=positions[a*3+2],bx=positions[b*3],bz=positions[b*3+2];
                    int axis=Math.Abs(ax-bx)<1e-5?0:Math.Abs(az-bz)<1e-5?1:Math.Abs(ax+az-bx-bz)<1e-5?2:-1;
                    if(axis<0)continue;double val=(axis==0?ax:axis==1?az:ax+az)/step;int line=(int)Math.Round(val);
                    if(Math.Abs(val-line)>1e-6||!lines.TryGetValue((axis,line),out var list))continue;
                    double dx=bx-ax,dz=bz-az,len=dx*dx+dz*dz;if(len<1e-12)continue;
                    var inside=new List<(double,int)>();
                    foreach(int id in list)
                    {
                        if(id==a||id==b)continue;
                        double x=positions[id*3]-ax,z=positions[id*3+2]-az,t=(x*dx+z*dz)/len;
                        if(t>1e-6&&t<1-1e-6&&Math.Abs(x*dz-z*dx)<1e-4*Math.Sqrt(len))inside.Add((t,id));
                    }
                    inside.Sort((p,q)=>p.Item1.CompareTo(q.Item1));foreach(var p in inside)polygon.Add(p.Item2);
                }
                if(polygon.Count==3){stitched.AddRange(polygon);continue;}
                double cx=0,cz=0;for(int k=0;k<3;k++){cx+=positions[indices[tri+k]*3]/3d;cz+=positions[indices[tri+k]*3+2]/3d;}
                int centre=Vertex(new Point(cx,cz));
                for(int k=0;k<polygon.Count;k++){stitched.Add(centre);stitched.Add(polygon[k]);stitched.Add(polygon[(k+1)%polygon.Count]);}
            }
            indices=stitched;
            mesh.WaterClipTriangles=indices.ToArray();indices.AddRange(mesh.Triangles.Skip(mesh.Segments*mesh.Segments*6));
            mesh.Triangles=indices.ToArray();mesh.Positions=positions.ToArray();mesh.Normals=normals.ToArray();mesh.Colors=colors.ToArray();
            mesh.SurfaceWeights=weights.ToArray();mesh.GroundWeightsB=groundB.ToArray();mesh.Lithology=rocks.ToArray();return samples.ToArray();
        }
    }
}
