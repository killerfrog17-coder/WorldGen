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
            public double X,Y,Z,U,V,T,H,W;
            public static Point Between(Point a,Point b,double t)=>new Point{X=a.X+(b.X-a.X)*t,Y=a.Y+(b.Y-a.Y)*t,
                Z=a.Z+(b.Z-a.Z)*t,U=a.U+(b.U-a.U)*t,V=a.V+(b.V-a.V)*t,T=a.T+(b.T-a.T)*t,H=a.H+(b.H-a.H)*t,W=a.W+(b.W-a.W)*t};
        }
        sealed class JunctionPool {public Point Center;public Point[] Ring=Array.Empty<Point>();public double Radius;public bool Outgoing;}
        sealed class SurfaceUnion
        {
            sealed class Footprint
            {
                public Point[] Ring;public double MinX,MaxX,MinZ,MaxZ,Sign;
                public Footprint(Point[] ring)
                {
                    Ring=ring;MinX=MinZ=double.PositiveInfinity;MaxX=MaxZ=double.NegativeInfinity;double area=0;
                    for(int i=0;i<ring.Length;i++){var p=ring[i];var q=ring[(i+1)%ring.Length];MinX=Math.Min(MinX,p.X);MaxX=Math.Max(MaxX,p.X);MinZ=Math.Min(MinZ,p.Z);MaxZ=Math.Max(MaxZ,p.Z);area+=p.X*q.Z-p.Z*q.X;}
                    Sign=area>=0?1:-1;
                }
            }
            readonly List<Footprint> shapes=new List<Footprint>();
            readonly Dictionary<(int,int),List<int>> bins=new Dictionary<(int,int),List<int>>();
            const double Bin=128;
            public void Add(Point[] polygon)
            {
                var shape=new Footprint(polygon);int id=shapes.Count;shapes.Add(shape);
                for(int z=(int)Math.Floor(shape.MinZ/Bin);z<=(int)Math.Floor(shape.MaxZ/Bin);z++)
                for(int x=(int)Math.Floor(shape.MinX/Bin);x<=(int)Math.Floor(shape.MaxX/Bin);x++)
                {if(!bins.TryGetValue((x,z),out var list)){list=new List<int>();bins.Add((x,z),list);}list.Add(id);}
            }
            static bool Overlap(Footprint a,Footprint b)
            {
                if(a.MinX>=b.MaxX-1e-7||a.MaxX<=b.MinX+1e-7||a.MinZ>=b.MaxZ-1e-7||a.MaxZ<=b.MinZ+1e-7)return false;
                bool Separated(Footprint f,Footprint g)
                {
                    for(int i=0;i<f.Ring.Length;i++)
                    {
                        var p=f.Ring[i];var q=f.Ring[(i+1)%f.Ring.Length];double largest=double.NegativeInfinity;
                        foreach(var v in g.Ring)largest=Math.Max(largest,f.Sign*((q.X-p.X)*(v.Z-p.Z)-(q.Z-p.Z)*(v.X-p.X)));
                        if(largest<=1e-7)return true;
                    }
                    return false;
                }
                return !Separated(a,b)&&!Separated(b,a);
            }
            static void Split(List<Point> polygon,Footprint clip,bool retainInside,List<List<Point>> result)
            {
                var inside=polygon;
                for(int edge=0;edge<clip.Ring.Length&&inside.Count>=3;edge++)
                {
                    var a=clip.Ring[edge];var b=clip.Ring[(edge+1)%clip.Ring.Length];var next=new List<Point>();var outside=new List<Point>();
                    for(int i=0;i<inside.Count;i++)
                    {
                        var p=inside[i];var q=inside[(i+1)%inside.Count];
                        double d=clip.Sign*((b.X-a.X)*(p.Z-a.Z)-(b.Z-a.Z)*(p.X-a.X));
                        double e=clip.Sign*((b.X-a.X)*(q.Z-a.Z)-(b.Z-a.Z)*(q.X-a.X));
                        if(d>=0)next.Add(p);else outside.Add(p);
                        if((d>=0)!=(e>=0)){var v=Point.Between(p,q,d/(d-e));next.Add(v);outside.Add(v);}
                    }
                    if(outside.Count>=3)result.Add(outside);inside=next;
                }
                if(retainInside&&inside.Count>=3)result.Add(inside);
            }
            public void Emit(Action<Point,Point,Point> emit,System.Threading.CancellationToken token)
            {
                var seen=new HashSet<int>();var candidates=new List<int>();
                for(int id=0;id<shapes.Count;id++)
                {
                    token.ThrowIfCancellationRequested();var shape=shapes[id];seen.Clear();candidates.Clear();
                    for(int z=(int)Math.Floor(shape.MinZ/Bin);z<=(int)Math.Floor(shape.MaxZ/Bin);z++)
                    for(int x=(int)Math.Floor(shape.MinX/Bin);x<=(int)Math.Floor(shape.MaxX/Bin);x++)
                        if(bins.TryGetValue((x,z),out var list))foreach(int other in list)if(other!=id&&seen.Add(other)&&Overlap(shape,shapes[other]))candidates.Add(other);
                    candidates.Sort();var polygons=new List<List<Point>>{new List<Point>(shape.Ring)};
                    foreach(int other in candidates)
                    {
                        var next=new List<List<Point>>();foreach(var polygon in polygons)Split(polygon,shapes[other],other>id,next);polygons=next;
                        if(polygons.Count==0)break;
                    }
                    foreach(var polygon in polygons)for(int i=2;i<polygon.Count;i++)emit(polygon[0],polygon[i-1],polygon[i]);
                }
            }
        }
        public static void Append(LocalTerrainMesh mesh,LocalTerrainFrame frame,LocalTerrain.LocalTerrainPatch? patch,LocalTerrainSample[] samples,CancellationToken token=default)
        {
            if(patch==null)return;
            double step=mesh.SizeM/mesh.Segments;
            var spans=patch.RiverSpans(step>32?step*.1:0);if(spans.Length==0)return;
            var positions=new List<float>(mesh.WaterPositions);var colors=new List<float>(mesh.WaterColors);var indices=new List<int>(mesh.WaterTriangles);
            int firstRiverIndex=indices.Count;
            // Reusable Sutherland-Hodgman buffers: no per-pixel/vertex noise
            // raster, physics query or dynamic water simulation is involved.
            var input=new Point[20];var output=new Point[20];
            var junctions=new Dictionary<int,JunctionPool>();
            var surfaceUnion=new SurfaceUnion();
            var waterQueries=new Dictionary<(long,long),RiverWaterState>();
            RiverWaterState Water(Point p)
            {
                var key=((long)Math.Round(p.X*10000),(long)Math.Round(p.Z*10000));
                if(waterQueries.TryGetValue(key,out var water))return water;
                water=patch.RiverWater(frame.Position(mesh.EastM+p.X,mesh.NorthM+p.Z));waterQueries.Add(key,water);return water;
            }
            double Head(Point p)=>p.W>2.05?p.H:Water(p).Present?Water(p).Level:p.H;
            int Clip(int count,Func<Point,double> signed)
            {
                int n=0;for(int k=0;k<count;k++)
                {var p=input[k];var q=input[(k+1)%count];double a=signed(p),b=signed(q);if(a>=0)output[n++]=p;if((a>=0)!=(b>=0))output[n++]=Point.Between(p,q,a/(a-b));}
                var old=input;input=output;output=old;return n;
            }
            var vertices=new Dictionary<(long,long,long),List<int>>();
            int Vertex(Point p)
            {
                p.X=(float)p.X;p.Z=(float)p.Z;
                if(p.W<=2.05)
                {
                    var water=Water(p);if(water.Present){p.H=water.Level;p.U=V.Dot(water.Flow,frame.East)*water.Speed;p.V=V.Dot(water.Flow,frame.North)*water.Speed;p.T=water.Turbulence;}
                }
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
                    {
                        bool fall=p.W>2.05,otherFall=colors[found*4+3]>2.05;
                        if(fall!=otherFall||fall&&(Math.Abs(colors[found*4+2]-p.T)>.0001||Math.Abs(colors[found*4+3]-p.W)>.0001))continue;
                        double ex=positions[found*3]-p.X,ey=positions[found*3+1]-p.Y,ez=positions[found*3+2]-p.Z;if(ex*ex+ey*ey+ez*ez<.000004)return found;
                    }
                int id=positions.Count/3;if(!vertices.TryGetValue(key,out var list)){list=new List<int>();vertices.Add(key,list);}list.Add(id);positions.Add((float)p.X);positions.Add((float)p.Y);positions.Add((float)p.Z);
                colors.Add((float)p.U);colors.Add((float)p.V);colors.Add((float)p.T);colors.Add((float)p.W);return id;
            }
            void Final(Point a,Point b,Point c,int depth=0)
            {
                double Square(Point p,Point q)=>Math.Pow(p.X-q.X,2)+Math.Pow(p.Z-q.Z,2);
                // Bound spherical chord sag to about a millimetre, including
                // coarse tiles. A tile-size-dependent limit left centimetre
                // steps where a junction fan met its clipped ribbon.
                double ab=Square(a,b),bc=Square(b,c),ca=Square(c,a),limit=Math.Min(128,Math.Max(4,Math.Sqrt(frame.RadiusM*.008)));
                var mab=Point.Between(a,b,.5);var mbc=Point.Between(b,c,.5);var mca=Point.Between(c,a,.5);
                double error=0;int split=0;
                if(a.W<=2.05&&b.W<=2.05&&c.W<=2.05)
                {
                    double ea=Math.Abs(Head(mab)-(Head(a)+Head(b))*.5),eb=Math.Abs(Head(mbc)-(Head(b)+Head(c))*.5),ec=Math.Abs(Head(mca)-(Head(c)+Head(a))*.5);
                    error=Math.Max(ea,Math.Max(eb,ec));split=ea>=eb&&ea>=ec?0:eb>=ec?1:2;
                }
                if(depth<18&&(Math.Max(ab,Math.Max(bc,ca))>limit*limit||error>.012&&Math.Max(ab,Math.Max(bc,ca))>1))
                {
                    if(error<=.012)split=ab>=bc&&ab>=ca?0:bc>=ca?1:2;
                    if(split==0){Final(a,mab,c,depth+1);Final(mab,b,c,depth+1);}
                    else if(split==1){Final(a,b,mbc,depth+1);Final(a,mbc,c,depth+1);}
                    else{Final(a,b,mca,depth+1);Final(mca,b,c,depth+1);}return;
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
                if(a.W>2.05||b.W>2.05||c.W>2.05){for(int k=2;k<n;k++)Final(input[0],input[k-1],input[k]);return;}
                var polygon=new Point[n];Array.Copy(input,polygon,n);surfaceUnion.Add(polygon);
            }
            Point At(V position,double height,V flow,double speed,double turbulence)
            {
                TerrainHandoff.Coordinates(frame,position,out double x,out double z);
                return new Point{X=x-mesh.EastM,Z=z-mesh.NorthM,Y=frame.SurfaceY(position,height),
                    U=V.Dot(flow,frame.East)*speed,V=V.Dot(flow,frame.North)*speed,T=turbulence,H=height,W=2};
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
                    // Zero-datum channels already belong to the connected sea
                    // surface, just like the skipped flat ribbons above. A
                    // second junction fan would overlap that ocean geometry.
                    if(id<0||width<.01||level==0)return;
                    var center=At(at,level,flow,span.Speed,span.Turbulence);
                    width=RiverscapeKernel.JunctionRadius(width);bool outgoing=span.From==id;
                    if(junctions.TryGetValue(id,out var previous)&&previous.Radius>=width&&(!outgoing||previous.Outgoing))return;
                    var ring=new Point[RiverscapeKernel.JunctionSides];for(int i=0;i<ring.Length;i++)ring[i]=At(RiverscapeKernel.JunctionPoint(at,width,i*Math.PI*2/ring.Length,frame.RadiusM),level,flow,span.Speed,span.Turbulence);
                    junctions[id]=new JunctionPool{Center=center,Radius=width,Ring=ring,Outgoing=outgoing};
                }
                if(span.PoolA)Junction(span.From,span.A,span.LevelA,span.WidthA);if(span.PoolB)Junction(span.To,span.B,span.LevelB,span.WidthB);
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
                // Bounded under-land overlap, analogous to accepted standing
                // shores. The actual drawn land still clips the surface; this
                // does not move its elevation or flood an unbounded valley.
                double wa=span.WidthA+HiddenBankOverlap(span.WidthA),wb=span.WidthB+HiddenBankOverlap(span.WidthB);
                V flowA=V.Cross(span.SideA,span.A).Unit,flowB=V.Cross(span.SideB,span.B).Unit;
                var al=At((span.A+span.SideA*(wa/frame.RadiusM)).Unit,span.LevelA,flowA,span.Speed,span.Turbulence);
                var ar=At((span.A-span.SideA*(wa/frame.RadiusM)).Unit,span.LevelA,flowA,span.Speed,span.Turbulence);
                var bl=At((span.B+span.SideB*(wb/frame.RadiusM)).Unit,span.LevelB,flowB,span.Speed,span.Turbulence);
                var br=At((span.B-span.SideB*(wb/frame.RadiusM)).Unit,span.LevelB,flowB,span.Speed,span.Turbulence);
                // River alpha 1.7..2 carries a bounded continuous surface grade;
                // 2.1..2.2 remains reserved for actual falling-sheet progress.
                // Reuse the existing payload: no extra vertex buffer or pass.
                al.W=ar.W=2-span.SurfaceGradeA*.5;
                bl.W=br.W=2-span.SurfaceGradeB*.5;
                if(span.Primitive==RiverFlowPrimitive.Waterfall)
                {
                    // The existing river colour payload carries signed lateral
                    // position and descent, without adding a mesh buffer/pass.
                    // Alpha remains inside the river-type interval 1.5..2.5.
                    al.T=-wa/Math.Max(.01,span.WidthA);ar.T=-al.T;
                    bl.T=-wb/Math.Max(.01,span.WidthB);br.T=-bl.T;
                    al.W=ar.W=2.1;bl.W=br.W=2.2;
                }
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
            surfaceUnion.Emit((a,b,c)=>Final(a,b,c),token);
            // Adaptive height fitting must also share the edge subdivision.
            // Otherwise one curved face interpolates over a neighbour's middle
            // vertex, opening a crack despite both sampling the same water.
            const double stitchBin=32;double tolerance=Math.Max(.002,mesh.SizeM*2e-7);
            var waterBins=new Dictionary<(int,int),List<int>>();int beforeStitch=positions.Count/3;
            for(int id=0;id<beforeStitch;id++)
            {
                if(colors[id*4+3]<1.5||colors[id*4+3]>2.05)continue;
                var key=((int)Math.Floor(positions[id*3]/stitchBin),(int)Math.Floor(positions[id*3+2]/stitchBin));
                if(!waterBins.TryGetValue(key,out var list)){list=new List<int>();waterBins.Add(key,list);}list.Add(id);
            }
            var stitched=new List<int>(indices.Count);for(int i=0;i<firstRiverIndex;i++)stitched.Add(indices[i]);
            for(int tri=firstRiverIndex;tri<indices.Count;tri+=3)
            {
                if(colors[indices[tri]*4+3]>2.05||colors[indices[tri+1]*4+3]>2.05||colors[indices[tri+2]*4+3]>2.05)
                {stitched.Add(indices[tri]);stitched.Add(indices[tri+1]);stitched.Add(indices[tri+2]);continue;}
                var polygon=new List<int>();
                for(int k=0;k<3;k++)
                {
                    int a=indices[tri+k],b=indices[tri+(k+1)%3];polygon.Add(a);
                    double ax=positions[a*3],az=positions[a*3+2],bx=positions[b*3],bz=positions[b*3+2],dx=bx-ax,dz=bz-az,len=dx*dx+dz*dz;
                    if(len<1e-12)continue;var inside=new List<(double,int)>();
                    void Candidates(int x,int z)
                    {
                        if(!waterBins.TryGetValue((x,z),out var list))return;
                        foreach(int id in list)
                        {
                            if(id==a||id==b)continue;double px=positions[id*3]-ax,pz=positions[id*3+2]-az,t=(px*dx+pz*dz)/len;
                            if(t>1e-6&&t<1-1e-6&&Math.Abs(px*dz-pz*dx)<tolerance*Math.Sqrt(len))inside.Add((t,id));
                        }
                    }
                    bool alongX=Math.Abs(dx)>=Math.Abs(dz);double begin=alongX?ax:az,end=alongX?bx:bz,delta=end-begin;
                    int low=(int)Math.Floor((Math.Min(begin,end)-tolerance)/stitchBin),high=(int)Math.Floor((Math.Max(begin,end)+tolerance)/stitchBin);
                    for(int major=low;major<=high;major++)
                    {
                        double t0=D.Clamp((major*stitchBin-tolerance-begin)/delta),t1=D.Clamp(((major+1)*stitchBin+tolerance-begin)/delta);
                        double v0=(alongX?az:ax)+(alongX?dz:dx)*t0,v1=(alongX?az:ax)+(alongX?dz:dx)*t1;
                        for(int minor=(int)Math.Floor((Math.Min(v0,v1)-tolerance)/stitchBin);minor<=(int)Math.Floor((Math.Max(v0,v1)+tolerance)/stitchBin);minor++)Candidates(alongX?major:minor,alongX?minor:major);
                    }
                    inside.Sort((p,q)=>p.Item1.CompareTo(q.Item1));double previous=-1;
                    foreach(var p in inside){if(previous>=0&&(p.Item1-previous)*Math.Sqrt(len)<.001)continue;polygon.Add(p.Item2);previous=p.Item1;}
                }
                if(polygon.Count==3){stitched.AddRange(polygon);continue;}
                // Clip ears using existing boundary vertices. A newly sampled
                // centroid can itself land on a thin neighbouring edge and
                // introduce another T-junction after this pass has indexed it.
                double Area(int a,int b,int c)
                {double ax=positions[a*3],az=positions[a*3+2],bx=positions[b*3],bz=positions[b*3+2],cx=positions[c*3],cz=positions[c*3+2];return (bx-ax)*(cz-az)-(bz-az)*(cx-ax);}
                while(polygon.Count>3)
                {
                    int ear=-1;double largest=1e-8;
                    for(int i=0;i<polygon.Count;i++)
                    {
                        int a=polygon[(i+polygon.Count-1)%polygon.Count],b=polygon[i],c=polygon[(i+1)%polygon.Count];double area=-Area(a,b,c);
                        if(area<=largest)continue;bool blocked=false;
                        foreach(int p in polygon)
                        {
                            if(p==a||p==b||p==c)continue;
                            // Include points on the proposed diagonal: clipping
                            // the opposite corner first would otherwise skip
                            // every collinear vertex on that shared edge.
                            if(Area(a,b,p)<=1e-7&&Area(b,c,p)<=1e-7&&Area(c,a,p)<=1e-7){blocked=true;break;}
                        }
                        if(!blocked){ear=i;largest=area;}
                    }
                    if(ear<0)break;
                    stitched.Add(polygon[(ear+polygon.Count-1)%polygon.Count]);stitched.Add(polygon[ear]);stitched.Add(polygon[(ear+1)%polygon.Count]);polygon.RemoveAt(ear);
                }
                if(polygon.Count==3&&Math.Abs(Area(polygon[0],polygon[1],polygon[2]))>1e-8)stitched.AddRange(polygon);
            }
            indices=stitched;
            mesh.WaterPositions=positions.ToArray();mesh.WaterColors=colors.ToArray();mesh.WaterTriangles=indices.ToArray();
        }
    }
}
