using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    public enum RiverFlowPrimitive : byte { Calm, Turbulent, Cascade }

    /// <summary>A finite cross-section pair. Heights and widths belong to the
    /// drainage graph, never to neighbouring dry terrain sample potentials.</summary>
    public struct RiverscapeSpan
    {
        public V A,B,SideA,SideB;
        public double LevelA,LevelB,WidthA,WidthB,Depth,Speed,Turbulence,Bend;
        public int Reach,From,To;
        public RiverFlowPrimitive Primitive;
    }

    public static class RiverscapeKernel
    {
        // PG2019 compact C2 primitive support, zero including its boundary.
        public static double Weight(double radius)
        {if(radius>=1||radius<0)return 0;double x=1-radius*radius;return x*x*x;}
        public static double Smooth(double t){t=D.Clamp(t);return t*t*t*(10+t*(-15+6*t));}
        public static double HalfWidth(double sourceFlow,double scale)=>Math.Max(.6,Math.Min(600,2.4*Math.Sqrt(Math.Max(0,sourceFlow)))*scale);
    }

    public sealed partial class HyperAmplification
    {
        double[] riverDatums,riverNodeWidths;
        int[] riverReceivers,riverFaceOffsets,riverFaces;
        bool[] riverActive;
        int[] riverIncoming;
        readonly object riverGate=new object();
        readonly Dictionary<int,WeakReference<RiverscapeSpan[]>> riverCache=new Dictionary<int,WeakReference<RiverscapeSpan[]>>();
        readonly Queue<RiverscapeSpan[]> riverRetained=new Queue<RiverscapeSpan[]>();
        public int RiverscapeReachCount {get;private set;}
        public int CachedRiverscapeReaches {get{lock(riverGate)return riverRetained.Count;}}

        void BuildRiverscapes(World world,CancellationToken token)
        {
            int count=rootNodes.Length;riverReceivers=world.Hydrology.Receiver;
            riverDatums=new double[count];riverNodeWidths=new double[count];riverActive=new bool[count];riverIncoming=new int[count];
            riverFaceOffsets=new int[count+1];
            foreach(var f in roots){riverFaceOffsets[f.A+1]++;riverFaceOffsets[f.B+1]++;riverFaceOffsets[f.C+1]++;}
            for(int i=1;i<=count;i++)riverFaceOffsets[i]+=riverFaceOffsets[i-1];
            riverFaces=new int[roots.Length*3];var cursor=(int[])riverFaceOffsets.Clone();
            for(int f=0;f<roots.Length;f++){var t=roots[f];riverFaces[cursor[t.A]++]=f;riverFaces[cursor[t.B]++]=f;riverFaces[cursor[t.C]++]=f;}
            for(int i=0;i<count;i++)
            {
                if((i&4095)==0)token.ThrowIfCancellationRequested();
                var node=rootNodes[i];int next=riverReceivers[i];
                riverDatums[i]=node.Type==Kind.Ocean?0:node.Type==Kind.Lake?node.LakeLevel:node.Height-.15;
                if(lakeOutletOwners[i]>=0)riverDatums[i]=lakeLevels[lakeOutletOwners[i]];
                // Discharge represents a source catchment area. Shrinking both
                // horizontal axes shrinks its visual discharge by scale².
                riverActive[i]=next>=0&&nodeChannels[i]>=0&&coarseEdges[nodeChannels[i]].Type==Kind.River&&
                    node.Type!=Kind.Ocean&&node.Flow*scale*scale>=.02;
                if(!riverActive[i])continue;
                riverIncoming[next]++;
                RiverscapeReachCount++;
                double width=RiverscapeKernel.HalfWidth(node.Flow,scale);
                riverNodeWidths[i]=Math.Max(riverNodeWidths[i],width);
                riverNodeWidths[next]=Math.Max(riverNodeWidths[next],width);
            }
            // Reconcile ONLY actual graph junctions, once. Standing receiving
            // bodies retain their exact accepted datum. No terrain raising.
            var order=world.Hydrology.VisitOrder;
            for(int k=0;k<order.Length;k++)
            {
                int i=order[k],next=riverReceivers[i];if(!riverActive[i]||next<0)continue;
                if(rootNodes[next].Type!=Kind.Lake&&rootNodes[next].Type!=Kind.Ocean)
                    riverDatums[next]=Math.Min(riverDatums[next],riverDatums[i]);
            }
            for(int k=order.Length-1;k>=0;k--)
            {int i=order[k],next=riverReceivers[i];if(riverActive[i]&&next>=0)riverDatums[i]=Math.Max(riverDatums[i],riverDatums[next]);}
        }

        RiverscapeSpan[] PrepareRiverscape(int source)
        {
            lock(riverGate)
            {
                if(riverCache.TryGetValue(source,out var weak)&&weak.TryGetTarget(out var cached))return cached;
                int target=riverReceivers[source];var a=rootNodes[source];var b=rootNodes[target];
                double length=(a.Position-b.Position).Length*radius*scale;
                double width=RiverscapeKernel.HalfWidth(a.Flow,scale);
                int n=Math.Max(4,Math.Min(256,(int)Math.Ceiling(length/Math.Max(24,width*1.5))));
                var points=new V[n+1];var levels=new double[n+1];var widths=new double[n+1];
                var faceSet=new SortedSet<int>();
                void Include(int node){for(int k=riverFaceOffsets[node];k<riverFaceOffsets[node+1];k++)faceSet.Add(riverFaces[k]*3);}
                Include(source);Include(target);
                var ids=new int[faceSet.Count];faceSet.CopyTo(ids);var bare=new Patch(this,ids);
                double start=riverDatums[source],end=riverDatums[target];
                for(int i=0;i<=n;i++)
                {
                    double t=i/(double)n;points[i]=i==0?a.Position:i==n?b.Position:RiverPoint(source,t);
                    double ground=bare.Sample(points[i],true).Height;
                    V tangent=i==n?(points[i]-points[i-1]).Unit:(RiverPoint(source,Math.Min(1,(i+.25)/n))-points[i]).Unit;
                    V side=V.Cross(points[i],tangent).Unit;
                    // Fit the bounded water profile below BOTH prospective
                    // banks as well as the thalweg. A centre-only fit can leave
                    // an exposed lateral ribbon edge on sloping valley floors.
                    double left=bare.Sample((points[i]+side*(width*1.25/(radius*scale))).Unit,true).Height;
                    double right=bare.Sample((points[i]-side*(width*1.25/(radius*scale))).Unit,true).Height;
                    ground=Math.Min(ground,Math.Min(left,right));
                    // The final water profile is BELOW the sampled valley, not
                    // a sheet draped along the surrounding hill elevations.
                    double ceiling=ground-.15;
                    levels[i]=Math.Max(end,Math.Min(start,ceiling));
                    double taper=Math.Min(1,Math.Min(i,n-i)/2d);
                    widths[i]=width*taper+(i<n/2?riverNodeWidths[source]:riverNodeWidths[target])*(1-taper);
                }
                levels[0]=start;levels[n]=end;
                // Downstream minimum propagation is graph-consistent. Pools
                // may be flat; no cross-stream side is ever tilted to terrain.
                for(int i=1;i<=n;i++)levels[i]=Math.Min(levels[i-1],levels[i]);
                // A confluence is a shared local pool, not two sloping ribbons
                // crossing at independent levels. Flat endpoint collars are
                // wider than their explicit bounded junction primitive.
                levels[1]=levels[0];levels[n-1]=levels[n];
                for(int i=2;i<n;i++)levels[i]=Math.Min(levels[i-1],levels[i]);
                if(riverIncoming[source]==0){widths[0]=0;widths[1]*=.5;}
                var result=new RiverscapeSpan[n];
                for(int i=0;i<n;i++)
                {
                    V tangentA=(points[Math.Min(n,i+1)]-points[Math.Max(0,i-1)]).Unit;
                    V tangentB=(points[Math.Min(n,i+2)]-points[i]).Unit;
                    double distance=(points[i+1]-points[i]).Length*radius*scale;
                    double slope=(levels[i]-levels[i+1])/Math.Max(.001,distance);
                    double depth=Math.Min(Math.Min(Depth(a.Flow),width*.65),.25+Math.Pow(a.Flow*scale*scale,.27)*.4);
                    var section=new RiverSection(levels[i],width,depth,a.Flow*scale*scale,slope,a.Sediment);
                    result[i]=new RiverscapeSpan{A=points[i],B=points[i+1],SideA=V.Cross(points[i],tangentA).Unit,SideB=V.Cross(points[i+1],tangentB).Unit,
                        LevelA=levels[i],LevelB=levels[i+1],WidthA=widths[i],WidthB=widths[i+1],Depth=depth,
                        Speed=section.Speed,Turbulence=section.Turbulence,Bend=D.Clamp(V.Dot(tangentB-tangentA,V.Cross(points[i],tangentA))*.8,-.6,.6),
                        Reach=source,From=i==0?source:-1,To=i==n-1?target:-1,
                        Primitive=slope>.08?RiverFlowPrimitive.Cascade:slope>.004?RiverFlowPrimitive.Turbulent:RiverFlowPrimitive.Calm};
                }
                // Longitudinal replacement primitive: steep bedrock reaches
                // receive a compact C2 shelf/drop template. Water AND bed use
                // these same additional stations. This is a heightfield cascade
                // (the paper also excludes free-falling 3D waterfall sheets).
                var refined=new List<RiverscapeSpan>(n*2);
                foreach(var span in result)
                {
                    if(span.Primitive!=RiverFlowPrimitive.Cascade){refined.Add(span);continue;}
                    for(int j=0;j<4;j++)
                    {
                        double t=j/4d,u=(j+1)/4d;
                        var s=span;s.A=(span.A*(1-t)+span.B*t).Unit;s.B=(span.A*(1-u)+span.B*u).Unit;
                        s.SideA=(span.SideA*(1-t)+span.SideB*t).Unit;s.SideB=(span.SideA*(1-u)+span.SideB*u).Unit;
                        // A replacement may lower an existing downhill profile,
                        // never lift a shelf above its available valley walls.
                        double shape(double x)=>Math.Max(x,x*.25+RiverscapeKernel.Smooth(x)*.75);
                        s.LevelA=span.LevelA+(span.LevelB-span.LevelA)*shape(t);s.LevelB=span.LevelA+(span.LevelB-span.LevelA)*shape(u);
                        s.WidthA=span.WidthA+(span.WidthB-span.WidthA)*t;s.WidthB=span.WidthA+(span.WidthB-span.WidthA)*u;
                        s.From=j==0?span.From:-1;s.To=j==3?span.To:-1;refined.Add(s);
                    }
                }
                result=refined.ToArray();
                // Re-condition AFTER replacement/refinement. A smooth cascade
                // template fitted only at its old endpoints can otherwise sit
                // above a small bank hollow between them. Shared station levels
                // are lowered together; graph endpoint datums remain fixed.
                var ceilings=new double[result.Length+1];for(int i=0;i<ceilings.Length;i++)ceilings[i]=double.PositiveInfinity;
                double BankCeiling(V p,V side,double half)
                {
                    double h=bare.Sample(p,true).Height;
                    h=Math.Min(h,bare.Sample((p+side*(half/(radius*scale))).Unit,true).Height);
                    h=Math.Min(h,bare.Sample((p-side*(half/(radius*scale))).Unit,true).Height);
                    // Include the finite render underlap, not just the nominal
                    // wet bank, in the available-wall/clearance constraint.
                    h=Math.Min(h,bare.Sample((p+side*((half+2)/(radius*scale))).Unit,true).Height);
                    return Math.Min(h,bare.Sample((p-side*((half+2)/(radius*scale))).Unit,true).Height)-.15;
                }
                for(int i=0;i<result.Length;i++)
                {
                    var s=result[i];ceilings[i]=Math.Min(ceilings[i],BankCeiling(s.A,s.SideA,s.WidthA));
                    ceilings[i+1]=Math.Min(ceilings[i+1],BankCeiling(s.B,s.SideB,s.WidthB));
                    foreach(double t in new[]{.25,.5,.75})
                    {
                        double middle=BankCeiling((s.A*(1-t)+s.B*t).Unit,(s.SideA*(1-t)+s.SideB*t).Unit,s.WidthA*(1-t)+s.WidthB*t);
                        double excess=s.LevelA*(1-t)+s.LevelB*t-middle;
                        if(excess>0){ceilings[i]=Math.Min(ceilings[i],s.LevelA-excess);ceilings[i+1]=Math.Min(ceilings[i+1],s.LevelB-excess);}
                    }
                }
                double previous=start;
                for(int i=0;i<result.Length;i++)
                {
                    var s=result[i];double level=i==0?start:Math.Max(end,Math.Min(previous,Math.Min(s.LevelA,ceilings[i])));
                    s.LevelA=level;if(i>0){var old=result[i-1];old.LevelB=level;result[i-1]=old;}
                    previous=level;s.LevelB=Math.Max(end,Math.Min(level,Math.Min(s.LevelB,ceilings[i+1])));result[i]=s;
                }
                if(result.Length>0){var last=result[result.Length-1];last.LevelB=end;result[result.Length-1]=last;}
                riverCache[source]=new WeakReference<RiverscapeSpan[]>(result);riverRetained.Enqueue(result);
                while(riverRetained.Count>128)riverRetained.Dequeue();
                if(riverCache.Count>512){var expired=new List<int>();foreach(var item in riverCache)if(!item.Value.TryGetTarget(out _))expired.Add(item.Key);foreach(int id in expired)riverCache.Remove(id);}
                return result;
            }
        }

        internal sealed partial class Patch
        {
            int[] riverscapeCandidates;
            RiverscapeSpan[] localRiverscapes;
            V riverRegionCenter;
            double riverRegionExtent;
            Dictionary<(int,int),List<int>> riverBins;
            V riverEast,riverNorth;
            double riverBinSize;
            internal void SetRiverRegion(V center,double extent){riverRegionCenter=center;riverRegionExtent=extent;}
            void SelectRiverscapes(V center,double extent)
            {
                var candidates=new SortedSet<int>();double physical=owner.radius*owner.scale;
                void Candidate(int node)
                {
                    if(!owner.riverActive[node])return;
                    int next=owner.riverReceivers[node];V a=owner.rootNodes[node].Position,b=owner.rootNodes[next].Position,d=b-a,p=center-a;
                    double u=D.Clamp(V.Dot(p,d)/Math.Max(1e-30,V.Dot(d,d)));
                    double distance=(p-d*u).Length*physical,length=d.Length*physical;
                    if(distance<=extent*1.5+length*.4+owner.riverNodeWidths[node]*8+20)candidates.Add(node);
                }
                foreach(int id in faces){var f=owner.roots[id];Candidate(f.A);Candidate(f.B);Candidate(f.C);}
                riverscapeCandidates=new int[candidates.Count];candidates.CopyTo(riverscapeCandidates);
            }
            public RiverscapeSpan[] RiverscapeSpans(V center,double extent,double minimumWidth=0)
            {
                if(localRiverscapes!=null&&minimumWidth<=0)return localRiverscapes;
                SelectRiverscapes(center,extent);var list=new List<RiverscapeSpan>();double physical=owner.radius*owner.scale;
                foreach(int id in riverscapeCandidates)
                {
                    double width=Math.Max(owner.riverNodeWidths[id],owner.riverNodeWidths[owner.riverReceivers[id]]);
                    if(width<minimumWidth||!owner.NearRiverscape(center,owner.RootEdge(owner.nodeChannels[id]),extent*1.5+width*8+20))continue;
                    foreach(var span in owner.PrepareRiverscape(id))
                    {
                        V d=span.B-span.A,p=center-span.A;double u=D.Clamp(V.Dot(p,d)/Math.Max(1e-30,V.Dot(d,d)));
                        if((p-d*u).Length*physical<=extent*1.5+Math.Max(span.WidthA,span.WidthB)*8+20)list.Add(span);
                    }
                }
                if(minimumWidth>0)return list.ToArray();
                localRiverscapes=list.ToArray();
                riverRegionCenter=center;
                riverEast=V.Cross(Math.Abs(center.Z)<.99?new V(0,0,1):new V(0,1,0),center).Unit;
                riverNorth=V.Cross(center,riverEast).Unit;
                riverBinSize=Math.Max(64,Math.Min(2048,extent*.08));
                foreach(var span in localRiverscapes)riverBinSize=Math.Max(riverBinSize,Math.Max(span.WidthA,span.WidthB)*6+8);
                riverBins=new Dictionary<(int,int),List<int>>();
                for(int i=0;i<localRiverscapes.Length;i++)
                {
                    var span=localRiverscapes[i];V a=span.A-center,b=span.B-center;
                    double ax=V.Dot(a,riverEast)*physical,az=V.Dot(a,riverNorth)*physical,bx=V.Dot(b,riverEast)*physical,bz=V.Dot(b,riverNorth)*physical;
                    double pad=Math.Max(span.WidthA,span.WidthB)*5+5;
                    int x0=(int)Math.Floor((Math.Min(ax,bx)-pad)/riverBinSize),x1=(int)Math.Floor((Math.Max(ax,bx)+pad)/riverBinSize);
                    int z0=(int)Math.Floor((Math.Min(az,bz)-pad)/riverBinSize),z1=(int)Math.Floor((Math.Max(az,bz)+pad)/riverBinSize);
                    for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                    {if(!riverBins.TryGetValue((x,z),out var bucket)){bucket=new List<int>();riverBins.Add((x,z),bucket);}bucket.Add(i);}
                }
                return localRiverscapes;
            }
            public LocalHydrology.Piece[] RiverscapePieces(V center,double extent)
            {
                var spans=RiverscapeSpans(center,extent);var result=new LocalHydrology.Piece[spans.Length];
                for(int i=0;i<result.Length;i++)result[i]=new LocalHydrology.Piece{A=spans[i].A,B=spans[i].B,HalfWidthM=Math.Max(spans[i].WidthA,spans[i].WidthB)};
                return result;
            }
            void ApplyRiverscape(V point,ref Surface surface)
            {
                // Lake ownership includes a dry collar, not just actual water.
                // Carve paired channels through that collar until they meet
                // submerged lake terrain; the coarse owner is NOT a shoreline.
                bool lake=surface.Kind==LocalWaterKind.Lake;
                double lakeLevel=surface.Water;ulong lakeKey=surface.BodyKey;
                if(lake)
                {
                    owner.CutLakeSpillMouth(point,ref surface);
                    if(surface.Height<lakeLevel)return;
                }
                if(surface.Kind==LocalWaterKind.Ocean)return;
                double physical=owner.radius*owner.scale,best=double.PositiveInfinity;
                void Apply(RiverscapeSpan span,ref Surface current)
                {
                    V d=span.B-span.A,p=point-span.A;double dd=V.Dot(d,d),raw=V.Dot(p,d)/Math.Max(1e-30,dd),u=D.Clamp(raw);
                    double distance=(p-d*u).Length*physical,width=span.WidthA+(span.WidthB-span.WidthA)*u;
                    double support=width*5+4;if(distance>=support)return;
                    V ta=V.Cross(span.SideA,span.A).Unit,tb=V.Cross(span.SideB,span.B).Unit;
                    bool before=V.Dot(p,ta)<-1e-10,after=V.Dot(point-span.B,tb)>1e-10;
                    // Adjacent paired cross-sections partition longitudinal
                    // space. Round caps at EVERY station used to let a lower
                    // cascade carve underneath the preceding higher ribbon.
                    if((before&&span.From<0)||(after&&span.To<0))return;
                    double level=span.LevelA+(span.LevelB-span.LevelA)*u,bankDistance=width-distance;
                    bool inside=bankDistance>=0;
                    if(!before&&!after&&span.WidthA+span.WidthB>.0001)
                    {
                        V axis=(d-span.A*V.Dot(d,span.A)).Unit,side=V.Cross(span.A,axis).Unit;
                        double x=V.Dot(p,axis)*physical,z=V.Dot(p,side)*physical,bx=V.Dot(d,axis)*physical,bz=V.Dot(d,side)*physical;
                        double lax=V.Dot(span.SideA,axis)*span.WidthA,laz=V.Dot(span.SideA,side)*span.WidthA;
                        double lbx=bx+V.Dot(span.SideB,axis)*span.WidthB,lbz=bz+V.Dot(span.SideB,side)*span.WidthB;
                        double rax=-lax,raz=-laz,rbx=2*bx-lbx,rbz=2*bz-lbz;
                        double Plane(double ax,double az,double qx,double qz,double rx,double rz,double ah,double qh,double rh,out double value)
                        {
                            double det=(qx-ax)*(rz-az)-(qz-az)*(rx-ax);
                            if(Math.Abs(det)<1e-12){value=level;return double.NegativeInfinity;}
                            double s=((x-ax)*(rz-az)-(z-az)*(rx-ax))/det,t=((qx-ax)*(z-az)-(qz-az)*(x-ax))/det;
                            value=ah+(qh-ah)*s+(rh-ah)*t;return Math.Min(1-s-t,Math.Min(s,t));
                        }
                        double first=Plane(lax,laz,lbx,lbz,rax,raz,span.LevelA,span.LevelB,span.LevelA,out double h1);
                        double second=Plane(rax,raz,lbx,lbz,rbx,rbz,span.LevelA,span.LevelB,span.LevelB,out double h2);
                        inside=Math.Max(first,second)>=-1e-7;
                        level=Math.Max(span.LevelB,Math.Min(span.LevelA,first>=second?h1:h2));
                        double EdgeDistance(double ax,double az,double qx,double qz)
                        {double ex=qx-ax,ez=qz-az,t=D.Clamp(((x-ax)*ex+(z-az)*ez)/Math.Max(1e-20,ex*ex+ez*ez));return Math.Sqrt(Math.Pow(x-ax-ex*t,2)+Math.Pow(z-az-ez*t,2));}
                        bankDistance=Math.Min(EdgeDistance(lax,laz,lbx,lbz),EdgeDistance(rax,raz,rbx,rbz))*(inside?1:-1);
                    }
                    // Paired unit cross-section: low bed, rising submerged
                    // banks, then compact cut-only retirement into the valley.
                    double relative=1-bankDistance/Math.Max(.01,width);
                    double signed=V.Dot(p-d*u,span.SideA)*physical/Math.Max(.01,width);
                    // C-type bend cross-section: deeper outer bank and a
                    // shallower point bar, without tilting the water surface.
                    double bed=level-span.Depth*(1-Math.Min(1,relative*relative))*(1+span.Bend*signed);
                    if(!inside)bed=level-bankDistance*.35;
                    double weight=inside?1:RiverscapeKernel.Weight(-bankDistance/(support-width));
                    current.DetailProtection=Math.Min(current.DetailProtection,1-weight);
                    current.Height=Math.Min(current.Height,current.Height+(Math.Min(current.Height,bed)-current.Height)*weight);
                    if(current.Height<0&&span.LevelA==0&&span.LevelB==0)
                    {
                        current.Kind=LocalWaterKind.Ocean;current.BodyKey=1;current.Water=0;current.Mask=-current.Height;
                        current.Footprint=double.PositiveInfinity;current.Minimum=current.Maximum=0;current.Gradient=default;
                    }
                    if(!inside||relative>=best)return;
                    best=relative;current.Kind=LocalWaterKind.River;current.BodyKey=(ulong)(uint)(span.Reach+1);
                    current.Water=level;current.Mask=level-current.Height;current.Footprint=bankDistance;
                    current.Minimum=Math.Min(span.LevelA,span.LevelB);current.Maximum=Math.Max(span.LevelA,span.LevelB);
                    current.Gradient=d*((span.LevelB-span.LevelA)/Math.Max(1e-30,dd));current.Flow=d.Unit;
                    current.Speed=span.Speed;current.Turbulence=span.Turbulence;current.RiverInfluence=1;
                }
                if(localRiverscapes!=null)
                {
                    V offset=point-riverRegionCenter;
                    var bin=((int)Math.Floor(V.Dot(offset,riverEast)*physical/riverBinSize),(int)Math.Floor(V.Dot(offset,riverNorth)*physical/riverBinSize));
                    if(riverBins.TryGetValue(bin,out var nearby))foreach(int index in nearby)Apply(localRiverscapes[index],ref surface);
                }
                else
                {
                    if(riverscapeCandidates==null)SelectRiverscapes(riverRegionExtent>0?riverRegionCenter:point,riverRegionExtent>0?riverRegionExtent:1);
                    foreach(int id in riverscapeCandidates)
                    {
                        double width=Math.Max(owner.riverNodeWidths[id],owner.riverNodeWidths[owner.riverReceivers[id]]);
                        if(!owner.NearRiverscape(point,owner.RootEdge(owner.nodeChannels[id]),width*5+4))continue;
                        foreach(var span in owner.PrepareRiverscape(id))Apply(span,ref surface);
                    }
                }
                if(lake)
                {
                    // A cut at/below the receiving datum is part of the flat
                    // lake, not a separate submerged or uphill river sheet.
                    // Dry collar cuts must also refresh their depth mask.
                    if(surface.Kind!=LocalWaterKind.River||surface.Water<=lakeLevel+1e-6)
                    {
                        surface.Kind=LocalWaterKind.Lake;surface.BodyKey=lakeKey;surface.Water=lakeLevel;
                        surface.Mask=lakeLevel-surface.Height;surface.Footprint=double.PositiveInfinity;
                        surface.Minimum=surface.Maximum=lakeLevel;surface.Gradient=default;
                        surface.Flow=default;surface.Speed=surface.Turbulence=0;
                    }
                }
                // A source-connected river mouth at the sea datum belongs to
                // the sea surface. It must not replace zero-level shelf water
                // with a separate, finitely truncated river patch.
                if(surface.Kind==LocalWaterKind.River&&Math.Abs(surface.Water)<1e-9)
                {surface.Kind=LocalWaterKind.Ocean;surface.BodyKey=1;surface.Footprint=double.PositiveInfinity;surface.Gradient=default;}
            }
        }

        bool NearRiverscape(V point,Edge edge,double support,int depth=0)
        {
            double physical=radius*scale,length=(edge.B.Position-edge.A.Position).Length*physical;
            double distance=Math.Sqrt(SegmentDistance2(point,edge,out _))*physical;
            if(distance-length*.4>support)return false;
            if(length<=Math.Max(16,support*.2)||edge.Depth>=terminalDepth||depth>=20)return distance<=support+length*.4;
            var split=Split(edge);return NearRiverscape(point,split.First,support,depth+1)||NearRiverscape(point,split.Last,support,depth+1);
        }
    }
}
