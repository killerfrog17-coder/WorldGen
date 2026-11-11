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
        struct Cut {public double X,Z,DX,DZ,Length,Half;public bool Bank;}
        public static LocalTerrainSample[] Refine(LocalTerrainMesh mesh,LocalTerrain terrain,LocalTerrainFrame frame,LocalTerrain.LocalTerrainPatch? patch,LocalTerrainSample[] original,System.Threading.CancellationToken token=default,TerrainBuildTiming? timing=null,double[]? edgeSteps=null)
        {
            var timer=timing!=null?System.Diagnostics.Stopwatch.StartNew():null;
            double step=mesh.SizeM/mesh.Segments;if(patch==null)return original;
            if(step>32&&terrain.Amplification==null)return original;
            var bankEdges=new HashSet<(V,V)>();
            var source=terrain.Amplification==null?patch.ChannelPieces(Math.Max(16,step*2)):Array.Empty<LocalHydrology.Piece>();
            if(terrain.Amplification!=null)
            {
                // Constrain the ACTUAL paired water polygon, not a parallel-
                // bank approximation of its centre line. Those disagree at
                // bends, tapered widths and cascade cross-sections.
                var exact=new List<LocalHydrology.Piece>();
                var pools=new HashSet<int>();
                // Every river wide enough to render at this LOD needs its bed
                // in the same mesh. Otherwise coarse faces bridge the channel
                // while the water is correctly drawn underneath solid ground.
                foreach(var s in patch.RiverSpans(step>32?step*.1:0))
                {
                    V al=(s.A+s.SideA*(s.WidthA/frame.RadiusM)).Unit,ar=(s.A-s.SideA*(s.WidthA/frame.RadiusM)).Unit;
                    V bl=(s.B+s.SideB*(s.WidthB/frame.RadiusM)).Unit,br=(s.B-s.SideB*(s.WidthB/frame.RadiusM)).Unit;
                    void Edge(V a,V b,bool bank=false){exact.Add(new LocalHydrology.Piece{A=a,B=b,HalfWidthM=0});if(bank)bankEdges.Add((a,b));}
                    void Pool(int id,V at,double width)
                    {
                        if(id<0||width<=0||!pools.Add(id))return;
                        foreach(double r in step>256?new[]{RiverscapeKernel.JunctionRadius(width)}:new[]{width,RiverscapeKernel.JunctionRadius(width)})
                        for(int i=0;i<RiverscapeKernel.JunctionSides;i++)Edge(RiverscapeKernel.JunctionPoint(at,r,i*Math.PI*2/RiverscapeKernel.JunctionSides,frame.RadiusM),RiverscapeKernel.JunctionPoint(at,r,(i+1)*Math.PI*2/RiverscapeKernel.JunctionSides,frame.RadiusM),r>width);
                    }
                    if(s.PoolA)Pool(s.From,s.A,s.WidthA);if(s.PoolB)Pool(s.To,s.B,s.WidthB);
                    Edge(s.A,s.B);
                    // Distant bathymetry needs the thalweg and the exact outer
                    // shoreline, not a second almost-coincident bank ring2m
                    // inside it. Close meshes retain both complete sections.
                    if(step<=256){Edge(al,bl);Edge(ar,br);Edge(al,ar);Edge(bl,br);}
                    // Match the water ribbon's two-metre hidden bank overlap.
                    // Without these cuts, its outer strip crossed coarse faces
                    // and appeared as isolated floating triangles on steep banks.
                    if(s.WidthA>0&&s.WidthB>0)
                    {
                        V ol=(s.A+s.SideA*((s.WidthA+2)/frame.RadiusM)).Unit,orr=(s.A-s.SideA*((s.WidthA+2)/frame.RadiusM)).Unit;
                        V pl=(s.B+s.SideB*((s.WidthB+2)/frame.RadiusM)).Unit,pr=(s.B-s.SideB*((s.WidthB+2)/frame.RadiusM)).Unit;
                        Edge(ol,pl,true);Edge(orr,pr,true);Edge(ol,orr);Edge(pl,pr);
                    }
                }
                // A known pond also needs its basin in the drawn land. A coarse
                // triangle otherwise bridges the hollow, or stays submerged
                // beyond the finite water outline. Refine only encountered ponds
                // that are undersampled at this LOD; no neighbourhood survey.
                var pondIds=new HashSet<ulong>();
                for(int index=0;index<original.Length;index++)
                {
                    var sample=original[index];
                    if(!sample.Underwater||sample.WaterKind!=LocalWaterKind.Lake||!SmallWaterBodies.IsLocal(sample.WaterBodyKey)||!pondIds.Add(sample.WaterBodyKey))continue;
                    var point=frame.Position(mesh.EastM+mesh.Positions[index*3],mesh.NorthM+mesh.Positions[index*3+2]);
                    var lake=patch.LocalLakeAt(sample.WaterBodyKey,point);
                    if(lake==null||step<=lake.Radius*.2)continue;
                    V Ring(double angle,double radius)
                    {
                        double edge=1+(lake.Hummocks?.115:.07)*Math.Sin(angle*3+lake.Phase)+(lake.Hummocks?.065:.04)*Math.Cos(angle*7-lake.Phase);
                        double a=Math.Cos(angle)*radius*lake.Radius*edge*lake.Aspect,b=Math.Sin(angle)*radius*lake.Radius*edge;
                        double c=Math.Cos(lake.Heading),s=Math.Sin(lake.Heading),x=a*c+b*s,z=-a*s+b*c;
                        return (lake.Center+lake.East*(x/frame.RadiusM)+lake.North*(z/frame.RadiusM)).Unit;
                    }
                    void Edge(V a,V b)=>exact.Add(new LocalHydrology.Piece{A=a,B=b,HalfWidthM=0});
                    // Tiny distant ponds need a closed basin, not the same
                    // ring arrangement as a well-resolved shore. Extra finite
                    // lines otherwise create many intersections in one cell.
                    bool distant=step>lake.Radius*.8;
                    int sectors=distant?8:12;
                    foreach(double radius in distant?new[]{1.55}:new[]{.65,1.55})
                        for(int i=0;i<sectors;i++)Edge(Ring(i*Math.PI*2/sectors,radius),Ring((i+1)*Math.PI*2/sectors,radius));
                    for(int i=0;i<2;i++)Edge(Ring(i*Math.PI/2,1.55),Ring(i*Math.PI/2+Math.PI,1.55));
                }
                source=exact.ToArray();
            }
            if(source.Length==0)return original;
            int side=mesh.Segments+1;var cuts=new List<Cut>();var buckets=new Dictionary<int,List<int>>();
            var seenCuts=new HashSet<(long,long,long,long)>();
            foreach(var p in source)
            {
                token.ThrowIfCancellationRequested();
                TerrainHandoff.Coordinates(frame,p.A,out double ax,out double az);TerrainHandoff.Coordinates(frame,p.B,out double bx,out double bz);
                ax-=mesh.EastM;bx-=mesh.EastM;az-=mesh.NorthM;bz-=mesh.NorthM;
                double dx=bx-ax,dz=bz-az,length=Math.Sqrt(dx*dx+dz*dz);if(length<.001)continue;
                long kax=(long)Math.Round(ax*10000),kaz=(long)Math.Round(az*10000),kbx=(long)Math.Round(bx*10000),kbz=(long)Math.Round(bz*10000);
                var cutKey=kax<kbx||kax==kbx&&kaz<kbz?(kax,kaz,kbx,kbz):(kbx,kbz,kax,kaz);
                if(!seenCuts.Add(cutKey))continue;
                double pad=p.HalfWidthM+1;
                int minX=Math.Max(0,(int)Math.Floor((Math.Min(ax,bx)-pad)/step)),maxX=Math.Min(mesh.Segments-1,(int)Math.Floor((Math.Max(ax,bx)+pad)/step));
                int minZ=Math.Max(0,(int)Math.Floor((Math.Min(az,bz)-pad)/step)),maxZ=Math.Min(mesh.Segments-1,(int)Math.Floor((Math.Max(az,bz)+pad)/step));
                if(minX>maxX||minZ>maxZ)continue;
                int id=cuts.Count;cuts.Add(new Cut{X=ax,Z=az,DX=dx/length,DZ=dz/length,Length=length,Half=p.HalfWidthM,Bank=bankEdges.Contains((p.A,p.B))});
                for(int z=minZ;z<=maxZ;z++)for(int x=minX;x<=maxX;x++)
                {
                    double cx=(x+.5)*step-ax,cz=(z+.5)*step-az;
                    if(Math.Abs(cx*(-dz/length)+cz*dx/length)>pad+step*.72)continue;
                    int key=z*mesh.Segments+x;if(!buckets.TryGetValue(key,out var list)){list=new List<int>();buckets.Add(key,list);}list.Add(id);
                }
            }
            if(cuts.Count==0)return original;
            if(timing!=null)timing.ChannelConstraintsMs=timer!.Elapsed.TotalMilliseconds;
            // A sparse base grid can become a dense mesh at a river. Prime the
            // shared exact-query index only once we KNOW refinement is needed.
            // Otherwise every added bank vertex rescans the broad river region.
            if(terrain.Amplification!=null)patch.RiverSpans();
            if(timing!=null)timing.ChannelIndexMs=timer!.Elapsed.TotalMilliseconds-timing.ChannelConstraintsMs;
            var positions=new List<float>(mesh.Positions);var normals=new List<float>(mesh.Normals);var colors=new List<float>(mesh.Colors);
            var weights=new List<float>(mesh.SurfaceWeights);var groundB=new List<float>(mesh.GroundWeightsB);var rocks=new List<float>(mesh.Lithology);
            var samples=new List<LocalTerrainSample>(original);while(samples.Count<mesh.Positions.Length/3)samples.Add(new LocalTerrainSample{WaterSurfaceM=double.NaN,WaterMaskM=-1});
            // Collapsing a tile edge to a coarse neighbour may bridge a narrow
            // channel. Both neighbours constrain the same river cross-sections;
            // retain canonical submerged bed vertices on that shared boundary.
            // Any difference from an extra fine vertex is below the water, while
            // dry land still follows the existing linear LOD seam policy.
            for(int i=0;i<original.Length;i++)
            {
                int x=i%side,z=i/side;if(x!=0&&z!=0&&x!=mesh.Segments&&z!=mesh.Segments)continue;
                if(original[i].Underwater)positions[i*3+1]=(float)frame.SurfaceY(frame.Position(mesh.EastM+positions[i*3],mesh.NorthM+positions[i*3+2]),original[i].ElevationM);
            }
            var indices=new List<int>();var shared=new Dictionary<(long,long),List<int>>();
            static (long,long) Key(double x,double z)=>((long)Math.Floor(x*1000),(long)Math.Floor(z*1000));
            void Remember((long,long) key,int id){if(!shared.TryGetValue(key,out var list)){list=new List<int>();shared.Add(key,list);}list.Add(id);}
            for(int i=0;i<original.Length;i++)Remember(Key(positions[i*3],positions[i*3+2]),i);
            bool Perimeter(Point p)=>Math.Abs(p.X)<1e-7||Math.Abs(p.X-mesh.SizeM)<1e-7||Math.Abs(p.Z)<1e-7||Math.Abs(p.Z-mesh.SizeM)<1e-7;
            bool Bank(Point p)
            {
                int x=Math.Max(0,Math.Min(mesh.Segments-1,(int)Math.Floor(p.X/step))),z=Math.Max(0,Math.Min(mesh.Segments-1,(int)Math.Floor(p.Z/step)));
                if(!buckets.TryGetValue(z*mesh.Segments+x,out var nearby))return false;
                double tolerance=Math.Max(.001,mesh.SizeM*2e-7);
                foreach(int id in nearby)
                {
                    var cut=cuts[id];if(!cut.Bank)continue;double px=p.X-cut.X,pz=p.Z-cut.Z,along=px*cut.DX+pz*cut.DZ;
                    if(along>=-tolerance&&along<=cut.Length+tolerance&&Math.Abs(px*cut.DZ-pz*cut.DX)<tolerance)return true;
                }
                return false;
            }
            int Vertex(Point p)
            {
                token.ThrowIfCancellationRequested();
                var key=Key(p.X,p.Z);
                // Independently intersected bank/grid lines differ by fractions
                // of a millimetre. Sampling those as separate terrain features
                // can put opposite channel-side heights on coincident GPU
                // vertices, creating needle triangles and holes in water clips.
                for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                    if(shared.TryGetValue((key.Item1+dx,key.Item2+dz),out var nearby))foreach(int found in nearby)
                    {
                        double ex=positions[found*3]-p.X,ez=positions[found*3+2]-p.Z;if(ex*ex+ez*ez>=.000001)continue;
                        if(Perimeter(p)&&Bank(p)){V bankPoint=frame.Position(mesh.EastM+p.X,mesh.NorthM+p.Z);positions[found*3+1]=(float)frame.SurfaceY(bankPoint,patch.Elevation(bankPoint));}
                        return found;
                    }
                double vertexStart=timer?.Elapsed.TotalMilliseconds??0;
                V at=frame.Position(mesh.EastM+p.X,mesh.NorthM+p.Z);LocalTerrainSample s;double nx,nz;
                if(step>256)
                {
                    // Keep river heights/water exact, while distant soil,
                    // vegetation and shading use the existing material grid.
                    // Tens of thousands of bank intersections do not each need
                    // a full ecological query plus two metre-scale normals.
                    int x=Math.Max(0,Math.Min(mesh.Segments-1,(int)Math.Floor(p.X/step))),z=Math.Max(0,Math.Min(mesh.Segments-1,(int)Math.Floor(p.Z/step)));
                    double tx=D.Clamp(p.X/step-x),tz=D.Clamp(p.Z/step-z);int a=z*side+x,b=a+1,c=a+side,d=c+1;
                    s=LocalTerrain.LocalTerrainPatch.BlendEcology(original[a],original[b],original[c],original[d],tx,tz,patch.Geometry(at));
                    RiverAlluvium.Ground(s.RiverSand,s.RiverGravel,ref s.Ground,ref s.Cover,ref s.MidCover,ref s.Forest);
                    double Normal(int component)=>normals[a*3+component]*(1-tx)*(1-tz)+normals[b*3+component]*tx*(1-tz)+normals[c*3+component]*(1-tx)*tz+normals[d*3+component]*tx*tz;
                    double y=Math.Max(.02,Normal(1));nx=Normal(0)/y;nz=Normal(2)/y;
                }
                else
                {
                    s=patch.Sample(at);
                    double Height(double x,double z){var v=frame.Position(mesh.EastM+x,mesh.NorthM+z);return frame.SurfaceY(v,patch.Elevation(v));}
                    double here=frame.SurfaceY(at,s.ElevationM);nx=here-Height(p.X+1,p.Z);nz=here-Height(p.X,p.Z+1);
                }
                double length=Math.Sqrt(nx*nx+1+nz*nz),up=1/length;
                var surface=LocalGeology.Surface(s.Ground,at,terrain.SourceSeed,s.ElevationM,up,s.OceanInfluence,s.Biome==Biome.Ice?D.Clamp((up-.55)/.4):0,terrain.RadiusM*frame.HorizontalScale,
                    s.Province.ExposedRock*(1-D.Clamp(s.RiverSand+s.RiverGravel)));
                int id=samples.Count;samples.Add(s);Remember(key,id);
                double height=frame.SurfaceY(at,s.ElevationM);
                // Both neighbours refine these same finite bank constraints.
                // Preserve their canonical dry-bank height as well as the wet
                // bed. Flattening a bank onto the old coarse grid can put it
                // metres below its water and expose the ribbon at tile borders.
                if(!s.Underwater&&Perimeter(p)&&!Bank(p))
                {
                    bool vertical=Math.Abs(p.X)<1e-7||Math.Abs(p.X-mesh.SizeM)<1e-7;
                    double along=(vertical?p.Z:p.X)/step;int low=Math.Max(0,Math.Min(mesh.Segments-1,(int)Math.Floor(along)));double t=along-low;
                    int first=vertical?low*side+(p.X>step?mesh.Segments:0):(p.Z>step?mesh.Segments*side:0)+low;
                    int next=first+(vertical?side:1);height=positions[first*3+1]*(1-t)+positions[next*3+1]*t;
                }
                positions.Add((float)p.X);positions.Add((float)height);positions.Add((float)p.Z);
                normals.Add((float)(nx/length));normals.Add((float)up);normals.Add((float)(nz/length));
                var cover=GroundFlora.Surface(s.Cover,at,terrain.SourceSeed,terrain.RadiusM*frame.HorizontalScale,
                    s.Ground,s.ElevationM,s.BaseElevationM,up,s.Underwater,s.Biome==Biome.Ice?1:0,s.Biome,
                    TreeFlora.CanopyShade(s.Forest,at,terrain.SourceSeed,terrain.RadiusM*frame.HorizontalScale)*TreeFlora.SlopeAdmission(up));
                double coverAmount=D.Clamp(cover.Coverage),visibleCover=D.Clamp(coverAmount/.45);
                V groundColor=surface.Color*(1-visibleCover)+cover.Color*(.90*visibleCover);
                colors.Add((float)groundColor.X);colors.Add((float)groundColor.Y);colors.Add((float)groundColor.Z);colors.Add((float)coverAmount);
                for(int k=0;k<4;k++){weights.Add((float)surface.Weight(k));groundB.Add((float)surface.Weight(k+4));}
                for(int k=0;k<6;k++)rocks.Add((float)s.Ground.RockWeight(k));
                if(timing!=null){timing.ChannelVerticesMs+=timer!.Elapsed.TotalMilliseconds-vertexStart;timing.ChannelNewVertices++;}return id;
            }
            void Triangle(int a,int b,int c,List<int> relevant)
            {
                if(relevant==null){indices.Add(a);indices.Add(b);indices.Add(c);return;}
                // A far grid cell may contain many short bank pieces. Applying
                // every supporting line to one enormous triangle creates a
                // dense arrangement of intersections far beyond those finite
                // pieces. Partition only overloaded triangles first, then keep
                // the exact same bank constraints in their local children.
                if(step>256&&relevant.Count>8)
                {
                    double ax=positions[a*3],az=positions[a*3+2],bx=positions[b*3],bz=positions[b*3+2],cx=positions[c*3],cz=positions[c*3+2];
                    double minX=Math.Min(ax,Math.Min(bx,cx)),maxX=Math.Max(ax,Math.Max(bx,cx)),minZ=Math.Min(az,Math.Min(bz,cz)),maxZ=Math.Max(az,Math.Max(bz,cz));
                    var local=new List<int>();
                    foreach(int id in relevant)
                    {
                        var cut=cuts[id];double ex=cut.X+cut.DX*cut.Length,ez=cut.Z+cut.DZ*cut.Length,pad=cut.Half+.01;
                        if(Math.Max(cut.X,ex)+pad<minX||Math.Min(cut.X,ex)-pad>maxX||Math.Max(cut.Z,ez)+pad<minZ||Math.Min(cut.Z,ez)-pad>maxZ)continue;
                        double sa=(ax-cut.X)*(-cut.DZ)+(az-cut.Z)*cut.DX,sb=(bx-cut.X)*(-cut.DZ)+(bz-cut.Z)*cut.DX,sc=(cx-cut.X)*(-cut.DZ)+(cz-cut.Z)*cut.DX;
                        if(Math.Min(sa,Math.Min(sb,sc))>pad||Math.Max(sa,Math.Max(sb,sc))< -pad)continue;
                        local.Add(id);
                    }
                    if(local.Count==0){indices.Add(a);indices.Add(b);indices.Add(c);return;}
                    relevant=local;
                    double ab=(bx-ax)*(bx-ax)+(bz-az)*(bz-az),bc=(cx-bx)*(cx-bx)+(cz-bz)*(cz-bz),ca=(ax-cx)*(ax-cx)+(az-cz)*(az-cz);
                    if(local.Count>8&&Math.Max(ab,Math.Max(bc,ca))>256*256)
                    {
                        int abm=Vertex(new Point((ax+bx)*.5,(az+bz)*.5)),bcm=Vertex(new Point((bx+cx)*.5,(bz+cz)*.5)),cam=Vertex(new Point((cx+ax)*.5,(cz+az)*.5));
                        Triangle(a,abm,cam,relevant);Triangle(abm,b,bcm,relevant);Triangle(cam,bcm,c,relevant);Triangle(abm,bcm,cam,relevant);return;
                    }
                }
                var polygons=new List<List<Point>>{new List<Point>{new Point(positions[a*3],positions[a*3+2]),new Point(positions[b*3],positions[b*3+2]),new Point(positions[c*3],positions[c*3+2])}};
                foreach(int id in relevant)
                {
                    var cut=cuts[id];
                    foreach(double offset in cut.Half<.0001?new[]{0d}:new[]{-cut.Half,0,cut.Half})
                    {
                        var next=new List<List<Point>>();
                        foreach(var polygon in polygons)
                        {
                            double minAlong=double.PositiveInfinity,maxAlong=double.NegativeInfinity,minSigned=double.PositiveInfinity,maxSigned=double.NegativeInfinity;
                            foreach(var p in polygon)
                            {
                                double along=(p.X-cut.X)*cut.DX+(p.Z-cut.Z)*cut.DZ,signed=(p.X-cut.X)*(-cut.DZ)+(p.Z-cut.Z)*cut.DX-offset;
                                minAlong=Math.Min(minAlong,along);maxAlong=Math.Max(maxAlong,along);minSigned=Math.Min(minSigned,signed);maxSigned=Math.Max(maxSigned,signed);
                            }
                            if(maxAlong<-.001||minAlong>cut.Length+.001){next.Add(polygon);continue;}
                            // A boundary touch is not a split. Repeated finite
                            // pieces can share one cutting line; cloning a
                            // polygon onto both sides of a coincident line
                            // causes exponential degenerate subdivision.
                            if(minSigned>=-1e-8||maxSigned<=1e-8){next.Add(polygon);continue;}
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
                    // Do not discard later constraints at a busy junction: that
                    // leaves uncut ground triangles crossing the water. Finite
                    // segment rejection above limits unrelated subdivisions.
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
            double clippingEnd=timer?.Elapsed.TotalMilliseconds??0;
            if(timing!=null)timing.ChannelClippingMs=clippingEnd-timing.ChannelConstraintsMs-timing.ChannelIndexMs;
            if(terrain.Amplification!=null)RiverBoundaryCurves.Apply(mesh,terrain,frame,patch,edgeSteps,positions,(x,z)=>Vertex(new Point(x,z)),token);
            // Finite cuts can leave T-junctions on grid edges OR prior cut
            // lines. Join every coincident edge using the points in its local
            // grid cells, rather than recognizing just three grid directions.
            // Coordinates are floats now, so compare distances in metres at
            // their stored precision. A dimensionless 1e-6 grid test skipped
            // valid shared points and left the two sides at different heights.
            var pointBins=new Dictionary<(int,int),List<int>>();
            double stitchTolerance=Math.Max(.00001,mesh.SizeM*2e-7);
            // A coarse terrain cell can contain thousands of refined bank
            // vertices. Scanning all of them for every triangle edge made
            // distant river tiles quadratic. Index at a bounded physical scale.
            double stitchBin=Math.Min(step,32);
            int before=samples.Count;
            for(int id=0;id<before;id++)
            {
                double x=positions[id*3],z=positions[id*3+2];
                var key=((int)Math.Floor(x/stitchBin),(int)Math.Floor(z/stitchBin));
                if(!pointBins.TryGetValue(key,out var list)){list=new List<int>();pointBins.Add(key,list);}list.Add(id);
            }
            var stitched=new List<int>(indices.Count);
            for(int tri=0;tri<indices.Count;tri+=3)
            {
                var polygon=new List<int>();
                for(int k=0;k<3;k++)
                {
                    int a=indices[tri+k],b=indices[tri+(k+1)%3];polygon.Add(a);
                    double ax=positions[a*3],az=positions[a*3+2],bx=positions[b*3],bz=positions[b*3+2];
                    double dx=bx-ax,dz=bz-az,len=dx*dx+dz*dz;if(len<1e-12)continue;
                    var inside=new List<(double,int)>();
                    void Candidates(int x,int z)
                    {
                        if(!pointBins.TryGetValue((x,z),out var list))return;
                        foreach(int id in list)
                        {
                            if(id==a||id==b)continue;
                            double px=positions[id*3]-ax,pz=positions[id*3+2]-az,t=(px*dx+pz*dz)/len;
                            if(t>1e-6&&t<1-1e-6&&Math.Abs(px*dz-pz*dx)<stitchTolerance*Math.Sqrt(len))inside.Add((t,id));
                        }
                    }
                    // Traverse the thin segment corridor, not the potentially
                    // enormous rectangle around a diagonal. Each bin is visited
                    // once; the existing exact distance predicate still decides.
                    bool alongX=Math.Abs(dx)>=Math.Abs(dz);double begin=alongX?ax:az,end=alongX?bx:bz,delta=end-begin;
                    int low=(int)Math.Floor((Math.Min(begin,end)-stitchTolerance)/stitchBin),high=(int)Math.Floor((Math.Max(begin,end)+stitchTolerance)/stitchBin);
                    for(int major=low;major<=high;major++)
                    {
                        double t0=D.Clamp((major*stitchBin-stitchTolerance-begin)/delta),t1=D.Clamp(((major+1)*stitchBin+stitchTolerance-begin)/delta);
                        double v0=(alongX?az:ax)+(alongX?dz:dx)*t0,v1=(alongX?az:ax)+(alongX?dz:dx)*t1;
                        int first=(int)Math.Floor((Math.Min(v0,v1)-stitchTolerance)/stitchBin),last=(int)Math.Floor((Math.Max(v0,v1)+stitchTolerance)/stitchBin);
                        for(int minor=first;minor<=last;minor++)Candidates(alongX?major:minor,alongX?minor:major);
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
            mesh.SurfaceWeights=weights.ToArray();mesh.GroundWeightsB=groundB.ToArray();mesh.Lithology=rocks.ToArray();
            if(timing!=null)timing.ChannelStitchingMs=timer!.Elapsed.TotalMilliseconds-clippingEnd;return samples.ToArray();
        }
    }
}
