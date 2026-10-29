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
            public double X,Y,Z,U,V,T,H;
            public static Point Between(Point a,Point b,double t)=>new Point{X=a.X+(b.X-a.X)*t,Y=a.Y+(b.Y-a.Y)*t,
                Z=a.Z+(b.Z-a.Z)*t,U=a.U+(b.U-a.U)*t,V=a.V+(b.V-a.V)*t,T=a.T+(b.T-a.T)*t,H=a.H+(b.H-a.H)*t};
        }
        sealed class JunctionPool {public Point Center;public Point[] Ring=Array.Empty<Point>();public double Radius;public bool Outgoing;}
        public static void Append(LocalTerrainMesh mesh,LocalTerrainFrame frame,LocalTerrain.LocalTerrainPatch? patch,LocalTerrainSample[] samples,CancellationToken token=default)
        {
            if(patch==null)return;
            double step=mesh.SizeM/mesh.Segments;
            var spans=patch.RiverSpans(step>32?step*.1:0);if(spans.Length==0)return;
            var positions=new List<float>(mesh.WaterPositions);var colors=new List<float>(mesh.WaterColors);var indices=new List<int>(mesh.WaterTriangles);
            // Reusable Sutherland-Hodgman buffers: no per-pixel/vertex noise
            // raster, physics query or dynamic water simulation is involved.
            var input=new Point[20];var output=new Point[20];
            var junctions=new Dictionary<int,JunctionPool>();
            int Clip(int count,Func<Point,double> signed)
            {
                int n=0;for(int k=0;k<count;k++)
                {var p=input[k];var q=input[(k+1)%count];double a=signed(p),b=signed(q);if(a>=0)output[n++]=p;if((a>=0)!=(b>=0))output[n++]=Point.Between(p,q,a/(a-b));}
                var old=input;input=output;output=old;return n;
            }
            var vertices=new Dictionary<(long,long,long),List<int>>();
            int Vertex(Point p)
            {
                // A kilometre-wide river cannot be approximated by one chord
                // across the spherical datum. Interpolate its elevation, then
                // project each final (also clipped) vertex back onto that datum.
                p.Y=frame.SurfaceY(frame.Position(mesh.EastM+p.X,mesh.NorthM+p.Z),p.H);
                // Weld the coordinates actually delivered to the GPU. At high
                // elevations different doubles can become the same float on
                // opposite rounding-bin boundaries, leaving false open edges.
                var key=((long)Math.Floor(p.X*500),(long)Math.Floor(p.Y*500),(long)Math.Floor(p.Z*500));
                for(int dz=-1;dz<=1;dz++)for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    if(vertices.TryGetValue((key.Item1+dx,key.Item2+dy,key.Item3+dz),out var nearby))foreach(int found in nearby)
                    {double ex=positions[found*3]-p.X,ey=positions[found*3+1]-p.Y,ez=positions[found*3+2]-p.Z;if(ex*ex+ey*ey+ez*ez<.000004)return found;}
                int id=positions.Count/3;if(!vertices.TryGetValue(key,out var list)){list=new List<int>();vertices.Add(key,list);}list.Add(id);positions.Add((float)p.X);positions.Add((float)p.Y);positions.Add((float)p.Z);
                colors.Add((float)p.U);colors.Add((float)p.V);colors.Add((float)p.T);colors.Add(2);return id;
            }
            void Final(Point a,Point b,Point c,int depth=0)
            {
                double Square(Point p,Point q)=>Math.Pow(p.X-q.X,2)+Math.Pow(p.Z-q.Z,2);
                // Bound spherical chord sag to about a millimetre, including
                // coarse tiles. A tile-size-dependent limit left centimetre
                // steps where a junction fan met its clipped ribbon.
                double ab=Square(a,b),bc=Square(b,c),ca=Square(c,a),limit=Math.Min(128,Math.Max(4,Math.Sqrt(frame.RadiusM*.008)));
                if(depth<18&&Math.Max(ab,Math.Max(bc,ca))>limit*limit)
                {
                    if(ab>=bc&&ab>=ca){var m=Point.Between(a,b,.5);Final(a,m,c,depth+1);Final(m,b,c,depth+1);}
                    else if(bc>=ca){var m=Point.Between(b,c,.5);Final(a,b,m,depth+1);Final(a,m,c,depth+1);}
                    else{var m=Point.Between(c,a,.5);Final(a,b,m,depth+1);Final(m,b,c,depth+1);}return;
                }
                double area=(b.X-a.X)*(c.Z-a.Z)-(b.Z-a.Z)*(c.X-a.X);if(Math.Abs(area)<1e-9)return;
                int first=Vertex(a),second=Vertex(b),third=Vertex(c);if(first==second||first==third||second==third)return;
                indices.Add(first);indices.Add(area<0?second:third);indices.Add(area<0?third:second);
            }
            void Emit(Point a,Point b,Point c)
            {
                // A finite channel ribbon can simply continue UNDER its banks:
                // the depth buffer hides it correctly. Re-clipping it against
                // thousands of near-degenerate bank triangles introduced holes
                // and protrusions at their discontinuous interpolation planes.
                // Tile bounds still prevent duplicate surfaces across LOD tiles.
                input[0]=a;input[1]=b;input[2]=c;int n=3;
                n=Clip(n,p=>p.X);if(n<3)return;
                n=Clip(n,p=>mesh.SizeM-p.X);if(n<3)return;
                n=Clip(n,p=>p.Z);if(n<3)return;
                n=Clip(n,p=>mesh.SizeM-p.Z);if(n<3)return;
                for(int k=2;k<n;k++)
                {
                    Final(input[0],input[k-1],input[k]);
                }
            }
            Point At(V position,double height,V flow,double speed,double turbulence)
            {
                TerrainHandoff.Coordinates(frame,position,out double x,out double z);
                return new Point{X=x-mesh.EastM,Z=z-mesh.NorthM,Y=frame.SurfaceY(position,height),
                    U=V.Dot(flow,frame.East)*speed,V=V.Dot(flow,frame.North)*speed,T=turbulence,H=height};
            }
            // The terrain refiner includes these same outer bank edges. The
            // bounded underlap reaches the actual raised bank, not the cut bed.
            double HiddenBankOverlap(double width)=>width<=0?0:2;
            foreach(var span in spans)
            {
                if(span.LevelA==0&&span.LevelB==0)continue; // paired cut is covered by the connected ocean mesh
                V flow=(span.B-span.A).Unit;
                void Junction(int id,V at,double level,double width)
                {
                    if(id<0||width<.01)return;
                    var center=At(at,level,flow,span.Speed,span.Turbulence);
                    width=RiverscapeKernel.JunctionRadius(width);bool outgoing=span.From==id;
                    if(junctions.TryGetValue(id,out var previous)&&previous.Radius>=width&&(!outgoing||previous.Outgoing))return;
                    var ring=new Point[RiverscapeKernel.JunctionSides];for(int i=0;i<ring.Length;i++)ring[i]=At(RiverscapeKernel.JunctionPoint(at,width,i*Math.PI*2/ring.Length,frame.RadiusM),level,flow,span.Speed,span.Turbulence);
                    junctions[id]=new JunctionPool{Center=center,Radius=width,Ring=ring,Outgoing=outgoing};
                }
                Junction(span.From,span.A,span.LevelA,span.WidthA);Junction(span.To,span.B,span.LevelB,span.WidthB);
            }
            void Ribbon(Point a,Point b,Point c,int endpoint,int other)
            {
                if(endpoint<0||!junctions.TryGetValue(endpoint,out var pool)){if(other>=0)Ribbon(a,b,c,other,-1);else Emit(a,b,c);return;}
                // Difference against the convex junction pool: emit each
                // outside fragment once, discard the remaining inside. This
                // avoids coplanar ribbon/pool z-fighting at confluences.
                var polygon=new List<Point>{a,b,c};
                for(int edge=0;edge<pool.Ring.Length&&polygon.Count>=3;edge++)
                {
                    var ra=pool.Ring[edge];var rb=pool.Ring[(edge+1)%pool.Ring.Length];double ax=ra.X,az=ra.Z,bx=rb.X,bz=rb.Z;
                    var inside=new List<Point>();var outside=new List<Point>();
                    for(int k=0;k<polygon.Count;k++)
                    {
                        var p=polygon[k];var q=polygon[(k+1)%polygon.Count];
                        double d=(bx-ax)*(p.Z-az)-(bz-az)*(p.X-ax),e=(bx-ax)*(q.Z-az)-(bz-az)*(q.X-ax);
                        if(d>=0)inside.Add(p);else outside.Add(p);
                        if((d>=0)!=(e>=0)){var hit=Point.Between(p,q,d/(d-e));inside.Add(hit);outside.Add(hit);}
                    }
                    for(int k=2;k<outside.Count;k++){if(other>=0)Ribbon(outside[0],outside[k-1],outside[k],other,-1);else Emit(outside[0],outside[k-1],outside[k]);}
                    polygon=inside;
                }
            }
            foreach(var span in spans)
            {
                if(span.LevelA==0&&span.LevelB==0)continue;
                token.ThrowIfCancellationRequested();
                V flow=(span.B-span.A).Unit;
                // Bounded under-land overlap, analogous to accepted standing
                // shores. The actual drawn land still clips the surface; this
                // does not move its elevation or flood an unbounded valley.
                double wa=span.WidthA+HiddenBankOverlap(span.WidthA),wb=span.WidthB+HiddenBankOverlap(span.WidthB);
                var al=At((span.A+span.SideA*(wa/frame.RadiusM)).Unit,span.LevelA,flow,span.Speed,span.Turbulence);
                var ar=At((span.A-span.SideA*(wa/frame.RadiusM)).Unit,span.LevelA,flow,span.Speed,span.Turbulence);
                var bl=At((span.B+span.SideB*(wb/frame.RadiusM)).Unit,span.LevelB,flow,span.Speed,span.Turbulence);
                var br=At((span.B-span.SideB*(wb/frame.RadiusM)).Unit,span.LevelB,flow,span.Speed,span.Turbulence);
                Ribbon(al,bl,ar,span.Reach,span.Receiver);Ribbon(ar,bl,br,span.Reach,span.Receiver);
            }
            // Only explicit source graph endpoints get junction pools. Never
            // connect neighbouring but unrelated rivers through terrain cells.
            foreach(var junction in junctions.Values)
            {
                var center=junction.Center;
                for(int k=0;k<junction.Ring.Length;k++)
                {
                    Emit(center,junction.Ring[k],junction.Ring[(k+1)%junction.Ring.Length]);
                }
            }
            mesh.WaterPositions=positions.ToArray();mesh.WaterColors=colors.ToArray();mesh.WaterTriangles=indices.ToArray();
        }
    }
}
