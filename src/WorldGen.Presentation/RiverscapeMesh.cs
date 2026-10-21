using System;
using System.Collections.Generic;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Explicit bounded ribbons from paired bed/water cross-sections.
    /// NEVER extrapolates a water level across a dry terrain triangle. Terrain
    /// triangle clipping only hides submerged portions; it cannot bend water.</summary>
    public static class RiverscapeMesh
    {
        struct Point
        {
            public double X,Y,Z,U,V,T;
            public static Point Between(Point a,Point b,double t)=>new Point{X=a.X+(b.X-a.X)*t,Y=a.Y+(b.Y-a.Y)*t,
                Z=a.Z+(b.Z-a.Z)*t,U=a.U+(b.U-a.U)*t,V=a.V+(b.V-a.V)*t,T=a.T+(b.T-a.T)*t};
        }
        public static void Append(LocalTerrainMesh mesh,LocalTerrainFrame frame,LocalTerrain.LocalTerrainPatch? patch,LocalTerrainSample[] samples,CancellationToken token=default)
        {
            if(patch==null)return;
            double step=mesh.SizeM/mesh.Segments;
            var spans=patch.RiverSpans(step>32?step*.1:0);if(spans.Length==0)return;
            var positions=new List<float>(mesh.WaterPositions);var colors=new List<float>(mesh.WaterColors);var indices=new List<int>(mesh.WaterTriangles);
            var buckets=new Dictionary<int,List<int>>();var groundTriangles=new List<int>();
            void GroundTriangle(int a,int b,int c)
            {
                int id=groundTriangles.Count;groundTriangles.Add(a);groundTriangles.Add(b);groundTriangles.Add(c);
                double minX=Math.Min(mesh.Positions[a*3],Math.Min(mesh.Positions[b*3],mesh.Positions[c*3]));
                double maxX=Math.Max(mesh.Positions[a*3],Math.Max(mesh.Positions[b*3],mesh.Positions[c*3]));
                double minZ=Math.Min(mesh.Positions[a*3+2],Math.Min(mesh.Positions[b*3+2],mesh.Positions[c*3+2]));
                double maxZ=Math.Max(mesh.Positions[a*3+2],Math.Max(mesh.Positions[b*3+2],mesh.Positions[c*3+2]));
                int x0=Math.Max(0,(int)Math.Floor(minX/step)),x1=Math.Min(mesh.Segments-1,(int)Math.Floor(maxX/step));
                int z0=Math.Max(0,(int)Math.Floor(minZ/step)),z1=Math.Min(mesh.Segments-1,(int)Math.Floor(maxZ/step));
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                {int key=z*mesh.Segments+x;if(!buckets.TryGetValue(key,out var list)){list=new List<int>();buckets.Add(key,list);}list.Add(id);}
            }
            if(mesh.WaterClipTriangles.Length>0)
            {var t=mesh.WaterClipTriangles;for(int i=0;i<t.Length;i+=3)GroundTriangle(t[i],t[i+1],t[i+2]);}
            else
            {int side=mesh.Segments+1;for(int z=0;z<mesh.Segments;z++)for(int x=0;x<mesh.Segments;x++){int a=z*side+x;GroundTriangle(a,a+side,a+1);GroundTriangle(a+1,a+side,a+side+1);}}

            // Reusable Sutherland-Hodgman buffers: no per-pixel/vertex noise
            // raster, physics query or dynamic water simulation is involved.
            var input=new Point[20];var output=new Point[20];
            var junctions=new Dictionary<int,(Point Center,double Radius)>();
            int Clip(int count,Func<Point,double> signed)
            {
                int n=0;for(int k=0;k<count;k++)
                {var p=input[k];var q=input[(k+1)%count];double a=signed(p),b=signed(q);if(a>=0)output[n++]=p;if((a>=0)!=(b>=0))output[n++]=Point.Between(p,q,a/(a-b));}
                var old=input;input=output;output=old;return n;
            }
            var vertices=new Dictionary<(long,long,long),int>();
            int Vertex(Point p)
            {
                var key=((long)Math.Round(p.X*10000),(long)Math.Round(p.Y*10000),(long)Math.Round(p.Z*10000));
                if(vertices.TryGetValue(key,out int id))return id;
                id=positions.Count/3;vertices.Add(key,id);positions.Add((float)p.X);positions.Add((float)p.Y);positions.Add((float)p.Z);
                colors.Add((float)p.U);colors.Add((float)p.V);colors.Add((float)p.T);colors.Add(2);return id;
            }
            void Emit(Point a,Point b,Point c)
            {
                double minX=Math.Min(a.X,Math.Min(b.X,c.X)),maxX=Math.Max(a.X,Math.Max(b.X,c.X));
                double minZ=Math.Min(a.Z,Math.Min(b.Z,c.Z)),maxZ=Math.Max(a.Z,Math.Max(b.Z,c.Z));
                int x0=Math.Max(0,(int)Math.Floor(minX/step)),x1=Math.Min(mesh.Segments-1,(int)Math.Floor(maxX/step));
                int z0=Math.Max(0,(int)Math.Floor(minZ/step)),z1=Math.Min(mesh.Segments-1,(int)Math.Floor(maxZ/step));
                var seen=new HashSet<int>();
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                {
                    if(!buckets.TryGetValue(z*mesh.Segments+x,out var candidates))continue;
                    foreach(int triangle in candidates)
                    {
                        if(!seen.Add(triangle))continue;
                        int ia=groundTriangles[triangle],ib=groundTriangles[triangle+1],ic=groundTriangles[triangle+2];
                        double ax=mesh.Positions[ia*3],az=mesh.Positions[ia*3+2],ay=mesh.Positions[ia*3+1];
                        double bx=mesh.Positions[ib*3],bz=mesh.Positions[ib*3+2],by=mesh.Positions[ib*3+1];
                        double cx=mesh.Positions[ic*3],cz=mesh.Positions[ic*3+2],cy=mesh.Positions[ic*3+1];
                        double det=(bx-ax)*(cz-az)-(bz-az)*(cx-ax);if(Math.Abs(det)<1e-10)continue;
                        double sign=det>0?1:-1;
                        input[0]=a;input[1]=b;input[2]=c;int n=3;
                        n=Clip(n,p=>sign*((bx-ax)*(p.Z-az)-(bz-az)*(p.X-ax)));if(n<3)continue;
                        n=Clip(n,p=>sign*((cx-bx)*(p.Z-bz)-(cz-bz)*(p.X-bx)));if(n<3)continue;
                        n=Clip(n,p=>sign*((ax-cx)*(p.Z-cz)-(az-cz)*(p.X-cx)));if(n<3)continue;
                        double gx=((by-ay)*(cz-az)-(cy-ay)*(bz-az))/det,gz=((bx-ax)*(cy-ay)-(cx-ax)*(by-ay))/det;
                        // Hidden 0.25m overlap is below land, never water lifted
                        // up or down to meet the shore. Finite channel bounds
                        // already exist before this intersection.
                        n=Clip(n,p=>p.Y-(ay+gx*(p.X-ax)+gz*(p.Z-az))+.25);if(n<3)continue;
                        // Do not stack a river sheet across an existing lake/sea.
                        if(ia<samples.Length&&ib<samples.Length&&ic<samples.Length)
                        {
                            var sa=samples[ia];var sb=samples[ib];var sc=samples[ic];
                            // Only ACTUAL standing water replaces the channel.
                            // A dry lake-owned triangle is not lake coverage;
                            // suppressing its ribbon leaves an abrupt dead end.
                            bool Standing(LocalTerrainSample s)=>s.Underwater&&
                                (s.WaterKind==LocalWaterKind.Lake||s.WaterKind==LocalWaterKind.Ocean);
                            if(Standing(sa)&&Standing(sb)&&Standing(sc))continue;
                        }
                        int first=Vertex(input[0]);for(int k=2;k<n;k++)
                        {
                            var p=input[0];var q=input[k-1];var r=input[k];double area=(q.X-p.X)*(r.Z-p.Z)-(q.Z-p.Z)*(r.X-p.X);
                            if(Math.Abs(area)<1e-9)continue;int second=Vertex(q),third=Vertex(r);
                            indices.Add(first);indices.Add(area<0?second:third);indices.Add(area<0?third:second);
                        }
                    }
                }
            }
            Point At(V position,double height,V flow,double speed,double turbulence)
            {
                TerrainHandoff.Coordinates(frame,position,out double x,out double z);
                return new Point{X=x-mesh.EastM,Z=z-mesh.NorthM,Y=frame.SurfaceY(position,height),
                    U=V.Dot(flow,frame.East)*speed,V=V.Dot(flow,frame.North)*speed,T=turbulence};
            }
            double HiddenBankOverlap(double width)=>width<=0?0:2;
            foreach(var span in spans)
            {
                if(span.LevelA==0&&span.LevelB==0)continue;
                V flow=(span.B-span.A).Unit;
                void Junction(int id,V at,double level,double width)
                {
                    if(id<0||width<.01)return;
                    var center=At(at,level,flow,span.Speed,span.Turbulence);
                    width+=HiddenBankOverlap(width);
                    if(!junctions.TryGetValue(id,out var previous)||previous.Radius<width)junctions[id]=(center,width);
                }
                Junction(span.From,span.A,span.LevelA,span.WidthA);Junction(span.To,span.B,span.LevelB,span.WidthB);
            }
            void Ribbon(Point a,Point b,Point c,int endpoint)
            {
                if(endpoint<0||!junctions.TryGetValue(endpoint,out var pool)){Emit(a,b,c);return;}
                // Difference against the convex junction pool: emit each
                // outside fragment once, discard the remaining inside. This
                // avoids coplanar ribbon/pool z-fighting at confluences.
                var polygon=new List<Point>{a,b,c};
                for(int edge=0;edge<16&&polygon.Count>=3;edge++)
                {
                    double angle=edge*Math.PI/8,next=(edge+1)*Math.PI/8;
                    double ax=pool.Center.X+Math.Cos(angle)*pool.Radius,az=pool.Center.Z+Math.Sin(angle)*pool.Radius;
                    double bx=pool.Center.X+Math.Cos(next)*pool.Radius,bz=pool.Center.Z+Math.Sin(next)*pool.Radius;
                    var inside=new List<Point>();var outside=new List<Point>();
                    for(int k=0;k<polygon.Count;k++)
                    {
                        var p=polygon[k];var q=polygon[(k+1)%polygon.Count];
                        double d=(bx-ax)*(p.Z-az)-(bz-az)*(p.X-ax),e=(bx-ax)*(q.Z-az)-(bz-az)*(q.X-ax);
                        if(d>=0)inside.Add(p);else outside.Add(p);
                        if((d>=0)!=(e>=0)){var hit=Point.Between(p,q,d/(d-e));inside.Add(hit);outside.Add(hit);}
                    }
                    for(int k=2;k<outside.Count;k++)Emit(outside[0],outside[k-1],outside[k]);
                    polygon=inside;
                }
            }
            foreach(var span in spans)
            {
                token.ThrowIfCancellationRequested();
                if(span.LevelA==0&&span.LevelB==0)continue;
                V flow=(span.B-span.A).Unit;
                // Bounded under-land overlap, analogous to accepted standing
                // shores. The actual drawn land still clips the surface; this
                // does not move its elevation or flood an unbounded valley.
                double wa=span.WidthA+HiddenBankOverlap(span.WidthA),wb=span.WidthB+HiddenBankOverlap(span.WidthB);
                var al=At((span.A+span.SideA*(wa/frame.RadiusM)).Unit,span.LevelA,flow,span.Speed,span.Turbulence);
                var ar=At((span.A-span.SideA*(wa/frame.RadiusM)).Unit,span.LevelA,flow,span.Speed,span.Turbulence);
                var bl=At((span.B+span.SideB*(wb/frame.RadiusM)).Unit,span.LevelB,flow,span.Speed,span.Turbulence);
                var br=At((span.B-span.SideB*(wb/frame.RadiusM)).Unit,span.LevelB,flow,span.Speed,span.Turbulence);
                int endpoint=span.From>=0?span.From:span.To;
                Ribbon(al,bl,ar,endpoint);Ribbon(ar,bl,br,endpoint);
            }
            // Only explicit source graph endpoints get junction pools. Never
            // connect neighbouring but unrelated rivers through terrain cells.
            foreach(var junction in junctions.Values)
            {
                var center=junction.Center;double width=junction.Radius;
                for(int k=0;k<16;k++)
                {
                    double angle=k*Math.PI/8,next=(k+1)*Math.PI/8;
                    var a=center;var b=center;a.X+=Math.Cos(angle)*width;a.Z+=Math.Sin(angle)*width;
                    b.X+=Math.Cos(next)*width;b.Z+=Math.Sin(next)*width;
                    // Spherical datum: a flat local fan approximation has only
                    // submillimetre error at these metre-sized junctions.
                    Emit(center,a,b);
                }
            }
            mesh.WaterPositions=positions.ToArray();mesh.WaterColors=colors.ToArray();mesh.WaterTriangles=indices.ToArray();
        }
    }
}
