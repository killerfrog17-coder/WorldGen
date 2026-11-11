using System;
using System.Collections.Generic;
using System.Threading;

namespace WorldGen.Core
{
    public enum RiverFlowPrimitive : byte { Calm, Turbulent, Cascade, Waterfall }

    /// <summary>A finite cross-section pair. Heights and widths belong to the
    /// drainage graph, never to neighbouring dry terrain sample potentials.</summary>
    public struct RiverscapeSpan
    {
        public V A,B,SideA,SideB;
        public double LevelA,LevelB,WidthA,WidthB,Depth,DepthA,DepthB,Speed,Turbulence,Bend;
        public double BankBendA,BankBendB,Alluvium,AlluviumA,AlluviumB,BedGravel;
        public double SurfaceGradeA,SurfaceGradeB;
        public int Reach,From,To,Receiver;
        public bool PoolA,PoolB;
        public RiverFlowPrimitive Primitive;
        internal RiverscapeBasis Basis;
    }

    public static class RiverscapeKernel
    {
        public const int JunctionSides=32;
        public static double JunctionRadius(double halfWidth)=>halfWidth<=0?0:(halfWidth+2)/Math.Cos(Math.PI/JunctionSides);
        // Two channel corridors remain joined beyond their node disk when
        // they approach at an acute angle. End their shared stage after that
        // overlap, not at an arbitrary multiple of the node width.
        public static double ConfluenceRadius(double halfWidth,double directionCosine)
        {
            double radius=JunctionRadius(halfWidth);
            // This is a centre-line limit. Keep the whole paired section on
            // the common head when one of its bank corners is still in the
            // overlap disk (triangle inequality adds one section half-width).
            return radius/Math.Sqrt(Math.Max(1e-12,(1-Math.Max(-1,Math.Min(1,directionCosine)))*.5))+halfWidth+2;
        }
        public static V JunctionPoint(V at,double radius,double angle,double planetRadius)
        {
            V east=V.Cross(Math.Abs(at.Z)<.99?new V(0,0,1):new V(0,1,0),at).Unit,north=V.Cross(at,east).Unit;
            return (at+east*(radius*Math.Cos(angle)/planetRadius)+north*(radius*Math.Sin(angle)/planetRadius)).Unit;
        }
        // PG2019 compact C2 primitive support, zero including its boundary.
        public static double Weight(double radius)
        {if(radius>=1||radius<0)return 0;double x=1-radius*radius;return x*x*x;}
        public static double Smooth(double t){t=D.Clamp(t);return t*t*t*(10+t*(-15+6*t));}
        public static double ProtectBank(double cut,double uncut,double bank,double distance)
        {
            if(distance<0||distance>=8)return cut;
            double weight=1-Smooth((distance-2.25)/5.75);
            return Math.Min(uncut,Math.Max(cut,cut+(Math.Min(uncut,bank)-cut)*weight));
        }
        public static double HalfWidth(double sourceFlow,double scale)=>RiverHydraulics.HalfWidth(sourceFlow,scale);
        public static V[] InsertJunctionCollars(V[] points,double radiusM,double startRadius,double endRadius)
        {
            if(points.Length<2||startRadius<=0&&endRadius<=0)return points;
            var result=new List<V>(points.Length+2){points[0]};var cuts=new List<V>(2);V start=points[0],end=points[points.Length-1];
            double Crossing(V a,V b,V center,double r)
            {
                if(r<=0)return -1;
                double da=(a-center).Length*radiusM-r,db=(b-center).Length*radiusM-r;
                if(da*db>=0)return -1;
                double lo=0,hi=1;
                for(int j=0;j<40;j++)
                {double t=(lo+hi)*.5,d=((a*(1-t)+b*t).Unit-center).Length*radiusM-r;if((d<0)==(da<0))lo=t;else hi=t;}
                double u=(lo+hi)*.5;return u>1e-8&&u<1-1e-8?u:-1;
            }
            for(int i=0;i<points.Length-1;i++)
            {
                V a=points[i],b=points[i+1];double x=Crossing(a,b,start,startRadius),y=Crossing(a,b,end,endRadius);
                if(y>=0&&(x<0||y<x)){double old=x;x=y;y=old;}
                if(x>=0){V cut=(a*(1-x)+b*x).Unit;result.Add(cut);cuts.Add(cut);}
                if(y>=0&&Math.Abs(y-x)>1e-8){V cut=(a*(1-y)+b*y).Unit;result.Add(cut);cuts.Add(cut);}
                result.Add(b);
            }
            // An exact boundary a metre from an old sampling station must not
            // create a metre-long artificial drop. Keep the boundary itself
            // and graph endpoints; let the adjacent full section absorb it.
            for(int i=result.Count-2;i>0;i--)
            {
                V p=result[i];if(cuts.Contains(p))continue;
                foreach(V cut in cuts)if((p-cut).Length*radiusM<4){result.RemoveAt(i);break;}
            }
            return result.ToArray();
        }
        // Prepare on the complete immutable reach, before tiles select spans.
        // Averaging mesh normals independently per tile would expose tile seams.
        public static void PrepareSurfaceGrades(RiverscapeSpan[] spans,double radiusM)
        {
            for(int station=0;station<=spans.Length;station++)
            {
                double grade=0;
                if(station>0&&station<spans.Length&&spans[station-1].Primitive!=RiverFlowPrimitive.Waterfall&&spans[station].Primitive!=RiverFlowPrimitive.Waterfall)
                {
                    var before=spans[station-1];var after=spans[station];
                    double run=((before.B-before.A).Length+(after.B-after.A).Length)*radiusM;
                    grade=Math.Max(0,Math.Min(.6,(before.LevelA-after.LevelB)/Math.Max(.001,run)));
                }
                if(station>0){var previous=spans[station-1];previous.SurfaceGradeB=grade;spans[station-1]=previous;}
                if(station<spans.Length){var next=spans[station];next.SurfaceGradeA=grade;spans[station]=next;}
            }
        }
    }

    public sealed partial class HyperAmplification
    {
        // Local channel coordinates include metre-scale squared angular lengths.
        // V.Dot rounds to 1e-9 for the world simulator: a 12m span on a
        // 637km planet has squared length below that quantum. Preserve double
        // precision here, without changing canonical world-generation math.
        static double RiverDot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        double[] riverDatums,riverNodeWidths,riverNodeDepths,riverCollarRadii;
        int[] riverIncidentOffsets,riverIncidents;
        int[] riverReceivers,riverFaceOffsets,riverFaces,riverPrevious;
        bool[] riverActive;
        int[] riverIncoming;
        readonly object riverGate=new object();
        readonly System.Collections.Concurrent.ConcurrentDictionary<int,WeakReference<RiverscapeSpan[]>> riverCache=new System.Collections.Concurrent.ConcurrentDictionary<int,WeakReference<RiverscapeSpan[]>>();
        readonly Queue<RiverscapeSpan[]> riverRetained=new Queue<RiverscapeSpan[]>();
        readonly Dictionary<int,V[]> riverPlans=new Dictionary<int,V[]>();
        readonly Queue<int> riverPlanOrder=new Queue<int>();
        public int RiverscapeReachCount {get;private set;}
        public double RiverscapeFittingMilliseconds {get;private set;}
        public bool UsedCachedRiverDatums {get;private set;}
        public double[] ExportRiverDatums()=>(double[])riverDatums.Clone();
        public int CachedRiverscapeReaches {get{lock(riverGate)return riverRetained.Count;}}
        // Exact drainage-graph profile for inspection and render validation.
        // Return a copy so callers cannot mutate the cached world derivation.
        public RiverscapeSpan[] RiverReach(int source)
            =>source<0||source>=riverActive.Length||!riverActive[source]?Array.Empty<RiverscapeSpan>():(RiverscapeSpan[])PrepareRiverscape(source).Clone();

        void BuildRiverscapes(World world,LocalTerrainProfile[] profiles,CancellationToken token,double[] cachedDatums)
        {
            int count=rootNodes.Length;riverReceivers=world.Hydrology.Receiver;
            riverDatums=new double[count];riverNodeWidths=new double[count];riverNodeDepths=new double[count];riverActive=new bool[count];riverIncoming=new int[count];
            riverPrevious=new int[count];
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
                // Inundated glacial main-stem stations are receiving ocean,
                // including narrow channels that bypass ordinary bank fitting.
                // Their old inland datums must not emit floating river ribbons.
                if(Fjords.OceanNode(i))riverDatums[i]=0;
                // Admission controls encounter density on the compressed globe.
                // Once admitted, the channel retains its source-globe dimensions.
                riverActive[i]=next>=0&&nodeChannels[i]>=0&&coarseEdges[nodeChannels[i]].Type==Kind.River&&
                    node.Type!=Kind.Ocean&&node.Flow*scale*scale>=RiverHydraulics.MinimumLocalDischarge;
                if(!riverActive[i])continue;
                riverIncoming[next]++;
                riverPrevious[next]=i;
                RiverscapeReachCount++;
                double width=RiverscapeKernel.HalfWidth(node.Flow,scale);
                riverNodeWidths[i]=Math.Max(riverNodeWidths[i],width);
                riverNodeWidths[next]=Math.Max(riverNodeWidths[next],width);
                double depth=RiverHydraulics.Depth(node.Flow,scale,width);riverNodeDepths[i]=Math.Max(riverNodeDepths[i],depth);riverNodeDepths[next]=Math.Max(riverNodeDepths[next],depth);
            }
            for(int i=0;i<count;i++)if(riverNodeWidths[i]>0)
                riverNodeDepths[i]=Math.Min(riverNodeDepths[i],riverNodeWidths[i]*.75);
            riverIncidentOffsets=new int[count+1];
            for(int i=0;i<count;i++)if(riverActive[i]){riverIncidentOffsets[i+1]++;riverIncidentOffsets[riverReceivers[i]+1]++;}
            for(int i=1;i<=count;i++)riverIncidentOffsets[i]+=riverIncidentOffsets[i-1];
            riverIncidents=new int[riverIncidentOffsets[count]];cursor=(int[])riverIncidentOffsets.Clone();
            for(int i=0;i<count;i++)if(riverActive[i]){int next=riverReceivers[i];riverIncidents[cursor[i]++]=next;riverIncidents[cursor[next]++]=i;}
            riverCollarRadii=new double[count];
            for(int node=0;node<count;node++)riverCollarRadii[node]=RiverscapeKernel.JunctionRadius(riverNodeWidths[node]);
            // Construct the inherited troughs BEFORE bank fitting. Cached and
            // freshly derived water levels then sample exactly the same ground.
            // Final reconciled levels remain the shared trough-floor reference.
            Glaciers=new GlacialValleys(world,profiles,scale,riverDatums,riverNodeWidths,token);
            // Wide junctions must fit their available valley walls BEFORE the
            // graph levels are reconciled. Fitting the ribbons afterwards cannot
            // repair a fixed node datum that stands above an outer bank hollow.
            var fitting=System.Diagnostics.Stopwatch.StartNew();
            bool cached=cachedDatums!=null&&cachedDatums.Length==count;
            if(cached)for(int node=0;node<count;node++)
            {
                if(!double.IsFinite(cachedDatums[node])){cached=false;break;}
            }
            UsedCachedRiverDatums=cached;
            if(cached)
            {
                // The provider checks source/model identity and the complete
                // array checksum. These are FINAL graph-reconciled levels;
                // comparing them to the provisional pre-fit lake datums would
                // reject valid cached graph corrections and repeat the work.
                Array.Copy(cachedDatums,riverDatums,count);RiverscapeFittingMilliseconds=fitting.Elapsed.TotalMilliseconds;return;
            }
            for(int node=0;node<count;node++)
            {if((node&1023)==0)token.ThrowIfCancellationRequested();if(NeedsRiverFit(node))riverDatums[node]=FitRiverNode(node);}
            RiverscapeFittingMilliseconds=fitting.Elapsed.TotalMilliseconds;
            // Reconcile ONLY actual graph junctions, once. Standing receiving
            // bodies retain their exact accepted datum. No terrain raising.
            var order=world.Hydrology.VisitOrder;
            for(int k=0;k<order.Length;k++)
            {
                int i=order[k],next=riverReceivers[i];if(!riverActive[i]||next<0)continue;
                if(rootNodes[next].Type!=Kind.Lake&&rootNodes[next].Type!=Kind.Ocean&&lakeOutletOwners[next]<0)
                    riverDatums[next]=Math.Min(riverDatums[next],riverDatums[i]);
            }
            for(int k=order.Length-1;k>=0;k--)
            {int i=order[k],next=riverReceivers[i];if(riverActive[i]&&next>=0)riverDatums[i]=Math.Max(riverDatums[i],riverDatums[next]);}
        }

        bool ThroughRiverNode(int node)
        {
            if(riverIncoming[node]!=1||!riverActive[node]||rootNodes[node].Type==Kind.Lake||Fjords.OceanNode(node))return false;
            V at=rootNodes[node].Position;
            return RiverDot((at-rootNodes[riverPrevious[node]].Position).Unit,(rootNodes[riverReceivers[node]].Position-at).Unit)>-.65;
        }
        bool RiverHasPool(int node)=>riverIncoming[node]>0&&!ThroughRiverNode(node)&&rootNodes[node].Type!=Kind.Lake&&rootNodes[node].Type!=Kind.Ocean&&lakeOutletOwners[node]<0&&!Fjords.OceanNode(node);
        V RiverNodeTangent(int node)
        {
            V at=rootNodes[node].Position;
            V tangent=((at-rootNodes[riverPrevious[node]].Position).Unit+(rootNodes[riverReceivers[node]].Position-at).Unit).Unit;
            return (tangent-at*RiverDot(tangent,at)).Unit;
        }
        bool NeedsRiverFit(int node)=>riverIncoming[node]>0&&!ThroughRiverNode(node)&&riverNodeWidths[node]>=16&&rootNodes[node].Type!=Kind.Lake&&rootNodes[node].Type!=Kind.Ocean&&lakeOutletOwners[node]<0&&!Fjords.OceanNode(node);
        double FitRiverNode(int node)
        {
            var faceSet=new SortedSet<int>();
            for(int k=riverFaceOffsets[node];k<riverFaceOffsets[node+1];k++)
            {
                var f=roots[riverFaces[k]];
                foreach(int v in new[]{f.A,f.B,f.C})for(int j=riverFaceOffsets[v];j<riverFaceOffsets[v+1];j++)faceSet.Add(riverFaces[j]*3);
            }
            var ids=new int[faceSet.Count];faceSet.CopyTo(ids);var bare=new Patch(this,ids);double ceiling=riverDatums[node];
            double width=riverNodeWidths[node];
            // Collars now stop exactly at the pool, rather than extending to
            // 2.5 radii plus an entire coarse station. Fit that actual footprint
            // and its square corners, not an unrelated distant floodplain ring.
            foreach(double r in new[]{RiverscapeKernel.JunctionRadius(width)})
            for(int direction=0;direction<16;direction++)
            {
                var p=RiverscapeKernel.JunctionPoint(rootNodes[node].Position,r,direction*Math.PI/8,radius*scale);
                ceiling=Math.Min(ceiling,bare.Sample(p,true).Height-RiverHydraulics.BankFreeboard(width));
            }
            return ceiling;
        }

        V[] RiverPlanPoints(int source)
        {
            if(riverPlans.TryGetValue(source,out var existing))return existing;
            int target=riverReceivers[source];var a=rootNodes[source];var b=rootNodes[target];
            double length=(a.Position-b.Position).Length*radius*scale,width=RiverscapeKernel.HalfWidth(a.Flow,scale);
            int n=Math.Max(16,Math.Min(256,(int)Math.Ceiling(length/Math.Max(24,width*1.5))));
            // Planform is geomorphic. Subsequent stage fitting must not move
            // its banks after the shared junction footprint was measured.
            var points=RiverPlanform.Build(t=>RiverPoint(source,t),n,length,width,Math.Max(0,(a.Water-b.Water)/Math.Max(1,length)),
                a.Sediment,a.Hardness,radius*scale,a.Key,ThroughRiverNode(source)?RiverNodeTangent(source):(V?)null,ThroughRiverNode(target)?RiverNodeTangent(target):(V?)null);
            points[0]=a.Position;points[n]=b.Position;riverPlans.Add(source,points);riverPlanOrder.Enqueue(source);
            while(riverPlanOrder.Count>128)riverPlans.Remove(riverPlanOrder.Dequeue());
            return points;
        }

        RiverscapeSpan[] PrepareRiverscape(int source)
        {
            // Published spans are immutable. A resident river must not wait
            // behind another thread fitting an unrelated, newly visited reach.
            // Only misses, construction and bounded retention need riverGate.
            if(riverCache.TryGetValue(source,out var weak)&&weak.TryGetTarget(out var cached))return cached;
            lock(riverGate)
            {
                if(riverCache.TryGetValue(source,out weak)&&weak.TryGetTarget(out cached))return cached;
                int target=riverReceivers[source];var a=rootNodes[source];var b=rootNodes[target];
                double length=(a.Position-b.Position).Length*radius*scale;
                double width=RiverscapeKernel.HalfWidth(a.Flow,scale);
                var points=RiverPlanPoints(source);int n=points.Length-1;
                var sourceNode=ActualRiverNode(source);var targetNode=ActualRiverNode(target);
                double start=sourceNode.Level,end=targetNode.Level;
                bool throughA=ThroughRiverNode(source),throughB=ThroughRiverNode(target);
                // A standing body already supplies the receiving surface.
                // Extending a circular confluence collar outside its actual
                // basin can suspend a flat lake-level ribbon over its outlet.
                bool poolA=RiverHasPool(source),poolB=RiverHasPool(target);
                double collarA=poolA?sourceNode.Radius:0,collarB=poolB?targetNode.Radius:0;
                points=RiverscapeKernel.InsertJunctionCollars(points,radius*scale,collarA,collarB);n=points.Length-1;
                var levels=new double[n+1];RiverDimensions(source,points,out var widths,out var depths);
                var faceSet=new SortedSet<int>();
                void Include(int node){for(int k=riverFaceOffsets[node];k<riverFaceOffsets[node+1];k++)faceSet.Add(riverFaces[k]*3);}
                Include(source);Include(target);
                var ids=new int[faceSet.Count];faceSet.CopyTo(ids);var bare=new Patch(this,ids);
                for(int i=0;i<=n;i++)
                {
                    double t=i/(double)n;
                    double ground=bare.Sample(points[i],true).Height;
                    V tangent=(points[Math.Min(n,i+1)]-points[Math.Max(0,i-1)]).Unit;
                    V side=V.Cross(points[i],tangent).Unit;
                    // Fit the bounded water profile below BOTH prospective
                    // banks as well as the thalweg. A centre-only fit can leave
                    // an exposed lateral ribbon edge on sloping valley floors.
                    double left=bare.Sample((points[i]+side*(widths[i]*1.25/(radius*scale))).Unit,true).Height;
                    double right=bare.Sample((points[i]-side*(widths[i]*1.25/(radius*scale))).Unit,true).Height;
                    ground=Math.Min(ground,Math.Min(left,right));
                    // The final water profile is BELOW the sampled valley, not
                    // a sheet draped along the surrounding hill elevations.
                    double ceiling=ground-.15;
                    levels[i]=Math.Max(end,Math.Min(start,ceiling));
                }
                levels[0]=start;levels[n]=end;
                // Downstream minimum propagation is graph-consistent. Pools
                // may be flat; no cross-stream side is ever tilted to terrain.
                for(int i=1;i<=n;i++)levels[i]=Math.Min(levels[i-1],levels[i]);
                // A confluence is a shared local pool, not two sloping ribbons
                // crossing at independent levels. Flat endpoint collars are
                // wider than their explicit bounded junction primitive.
                for(int i=2;i<n;i++)levels[i]=Math.Min(levels[i-1],levels[i]);
                if(riverIncoming[source]==0){widths[0]=depths[0]=0;widths[1]*=.5;depths[1]=Math.Min(depths[1],widths[1]*.75);}
                double alluvium=RiverAlluvium.Strength(width,(start-end)/Math.Max(1,length),a.Sediment,a.Hardness);
                double bedGravel=D.Clamp(.1+.65*a.Hardness+.2*(1-a.Sediment));
                var result=new RiverscapeSpan[n];
                for(int i=0;i<n;i++)
                {
                    V tangentA=(points[Math.Min(n,i+1)]-points[Math.Max(0,i-1)]).Unit;
                    V tangentB=(points[Math.Min(n,i+2)]-points[i]).Unit;
                    double distance=(points[i+1]-points[i]).Length*radius*scale;
                    double slope=(levels[i]-levels[i+1])/Math.Max(.001,distance);
                    double depth=(depths[i]+depths[i+1])*.5;
                    var section=new RiverSection(levels[i],(widths[i]+widths[i+1])*.5,depth,RiverHydraulics.Discharge(a.Flow,scale),slope,a.Sediment);
                    result[i]=new RiverscapeSpan{A=points[i],B=points[i+1],SideA=V.Cross(points[i],i==0&&throughA?RiverNodeTangent(source):tangentA).Unit,SideB=V.Cross(points[i+1],i==n-1&&throughB?RiverNodeTangent(target):tangentB).Unit,
                        LevelA=levels[i],LevelB=levels[i+1],WidthA=widths[i],WidthB=widths[i+1],Depth=depth,DepthA=depths[i],DepthB=depths[i+1],
                        Speed=section.Speed,Turbulence=section.Turbulence,Bend=D.Clamp(RiverDot(tangentB-tangentA,V.Cross(points[i],tangentA))*.8,-.6,.6),
                        BedGravel=bedGravel,
                        Reach=source,Receiver=target,From=i==0?source:-1,To=i==n-1?target:-1,
                        PoolA=poolA,PoolB=poolB,
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
                        s.DepthA=span.DepthA+(span.DepthB-span.DepthA)*t;s.DepthB=span.DepthA+(span.DepthB-span.DepthA)*u;s.Depth=(s.DepthA+s.DepthB)*.5;
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
                if(result.Length>0)
                {
                    // Bank fitting must not undo the shared junction collar.
                    // The render pool replaces this end of the ribbon; a slope
                    // here produces a vertical crack along its polygon boundary.
                    // Keep both ends of each collar on the graph datum and
                    // propagate the adjacent station too (no disconnected step).
                    int firstOutside=0,lastOutside=result.Length;
                    if(poolA)for(int i=0;i<result.Length;i++)if((result[i].B-a.Position).Length*radius*scale<=collarA+.01)firstOutside=Math.Max(firstOutside,i+1);
                    if(poolB)for(int i=result.Length-1;i>=0;i--)if((result[i].A-b.Position).Length*radius*scale<=collarB+.01)lastOutside=Math.Min(lastOutside,i);
                    firstOutside=Math.Min(result.Length-1,firstOutside);lastOutside=Math.Max(firstOutside,lastOutside);
                    for(int i=0;i<result.Length;i++)
                    {
                        var s=result[i];if(i<firstOutside)s.LevelA=s.LevelB=start;else if(i==firstOutside)s.LevelA=start;
                        if(i>=lastOutside)s.LevelA=s.LevelB=end;else if(i==lastOutside-1)s.LevelB=end;result[i]=s;
                    }
                }
                RefreshRiverFlow(result,radius*scale,RiverHydraulics.Discharge(a.Flow,scale),a.Sediment);
                result=WaterfallProfiles.Apply(result,radius*scale,a.Resistance,source,seed);
                if(result.Length>0)
                {
                    var first=result[0];first.A=a.Position;
                    if(throughA){first.SideA=V.Cross(a.Position,RiverNodeTangent(source)).Unit;first.WidthA=riverNodeWidths[source];first.DepthA=riverNodeDepths[source];first.LevelA=start;first.Depth=(first.DepthA+first.DepthB)*.5;}
                    result[0]=first;
                    int last=result.Length-1;var final=result[last];final.B=b.Position;
                    if(throughB){final.SideB=V.Cross(b.Position,RiverNodeTangent(target)).Unit;final.WidthB=riverNodeWidths[target];final.DepthB=riverNodeDepths[target];final.LevelB=end;final.Depth=(final.DepthA+final.DepthB)*.5;}
                    result[last]=final;
                }
                RiverAlluvium.PrepareBanks(result,radius*scale,alluvium);
                RiverscapeKernel.PrepareSurfaceGrades(result,radius*scale);
                for(int i=0;i<result.Length;i++)result[i].Basis=new RiverscapeBasis(result[i],radius*scale);
                riverCache[source]=new WeakReference<RiverscapeSpan[]>(result);riverRetained.Enqueue(result);
                while(riverRetained.Count>128)riverRetained.Dequeue();
                if(riverCache.Count>512){var expired=new List<int>();foreach(var item in riverCache)if(!item.Value.TryGetTarget(out _))expired.Add(item.Key);foreach(int id in expired)riverCache.TryRemove(id,out _);}
                return result;
            }
        }

        internal sealed partial class Patch
        {
            struct BankGuard {public double Height,Distance;}
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
                    double u=D.Clamp(RiverDot(p,d)/Math.Max(1e-30,RiverDot(d,d)));
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
                        V d=span.B-span.A,p=center-span.A;double u=D.Clamp(RiverDot(p,d)/Math.Max(1e-30,RiverDot(d,d)));
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
                    double ax=RiverDot(a,riverEast)*physical,az=RiverDot(a,riverNorth)*physical,bx=RiverDot(b,riverEast)*physical,bz=RiverDot(b,riverNorth)*physical;
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
                // Ocean ownership extends inland through the interpolated
                // coastline. A dry owned point is not already submerged: cut
                // the river through it just as through a dry lake collar.
                if(surface.Kind==LocalWaterKind.Ocean&&surface.Height<surface.Water)return;
                // Sample the same finite, paired spans used by the renderer.
                // A separate distance-to-fractal-curve rejection can miss the
                // chord between two stations. It also made samples change once
                // RiverSpans() populated this patch during mesh refinement.
                double physical=owner.radius*owner.scale,best=double.PositiveInfinity,uncutHeight=surface.Height,cutHeight=uncutHeight;
                Span<BankGuard> bankGuards=stackalloc BankGuard[16];int guardCount=0;bool guardOverflow=false;
                double insideHeight=uncutHeight;bool hasInside=false;
                void Guard(double bank,double distance,ref Surface current,Span<BankGuard> guards,ref int count,ref bool overflow,bool applying)
                {
                    if(distance<0||distance>=8)return;
                    if(applying){current.Height=Math.Max(current.Height,RiverscapeKernel.ProtectBank(cutHeight,uncutHeight,bank,distance));return;}
                    if(count<guards.Length)guards[count++]=new BankGuard{Height=bank,Distance=distance};else overflow=true;
                }
                void Pool(int node,V at,double width,bool enabled,ref Surface current,Span<BankGuard> guards,ref int count,ref bool overflow,bool applying)
                {
                    if(!enabled||node<0||width<=.01)return;double distance=(point-at).Length*physical,support=width*3+4;if(distance>=support)return;
                    double level=owner.ActualRiverNode(node).Level,relative=distance/width;bool inside=relative<1;
                    if(!inside)Guard(level+(distance-width)*.35,Math.Max(0,distance-RiverscapeKernel.JunctionRadius(width)+2),ref current,guards,ref count,ref overflow,applying);
                    if(applying)return;
                    double bed=inside?level-owner.riverNodeDepths[node]*(1-relative*relative):level+(distance-width)*.35;
                    if(inside){hasInside=true;insideHeight=Math.Min(insideHeight,Math.Min(uncutHeight,bed));}
                    double weight=inside?1:RiverscapeKernel.Weight((distance-width)/(support-width));
                    current.DetailProtection=Math.Min(current.DetailProtection,1-weight);current.Height=Math.Min(current.Height,current.Height+(Math.Min(current.Height,bed)-current.Height)*weight);
                    if(!inside||relative>=best)return;best=relative;
                    current.Kind=LocalWaterKind.River;current.BodyKey=(ulong)(uint)(node+1);current.Water=level;current.Mask=level-current.Height;current.Footprint=width-distance;
                    current.Minimum=current.Maximum=level;current.Gradient=default;current.RiverInfluence=1;
                    int next=owner.riverReceivers[node];if(next>=0){current.Flow=(owner.rootNodes[next].Position-at).Unit;var section=new RiverSection(level,width,owner.riverNodeDepths[node],RiverHydraulics.Discharge(owner.rootNodes[node].Flow,owner.scale),0,owner.rootNodes[node].Sediment);current.Speed=section.Speed;current.Turbulence=section.Turbulence;}
                }
                void Apply(in RiverscapeSpan span,ref Surface current,Span<BankGuard> guards,ref int count,ref bool overflow,bool applying)
                {
                    ref readonly var basis=ref span.Basis;
                    V d=basis.D,p=point-span.A;double dd=basis.DD,raw=RiverDot(p,d)/Math.Max(1e-30,dd),u=D.Clamp(raw);
                    double distance=(p-d*u).Length*physical,width=span.WidthA+(span.WidthB-span.WidthA)*u;
                    double support=width*5+4;if(distance>=support)return;
                    V ta=basis.TangentA,tb=basis.TangentB;
                    bool before=RiverDot(p,ta)<-1e-10,after=RiverDot(point-span.B,tb)>1e-10;
                    // Adjacent paired cross-sections partition longitudinal
                    // space. Round caps at EVERY station used to let a lower
                    // cascade carve underneath the preceding higher ribbon.
                    double level=span.LevelA+(span.LevelB-span.LevelA)*u,bankDistance=width-distance;
                    bool inside=bankDistance>=0;
                    if(span.WidthA+span.WidthB>.0001)
                    {
                        double x=RiverDot(p,basis.Axis)*physical,z=RiverDot(p,basis.Side)*physical;
                        double lax=basis.LAX,laz=basis.LAZ,lbx=basis.LBX,lbz=basis.LBZ,rax=basis.RAX,raz=basis.RAZ,rbx=basis.RBX,rbz=basis.RBZ;
                        double Plane(double ax,double az,double qx,double qz,double rx,double rz,double ah,double qh,double rh,out double value,out double bs,out double bt)
                        {
                            double det=(qx-ax)*(rz-az)-(qz-az)*(rx-ax);
                            if(Math.Abs(det)<1e-12){value=level;bs=bt=0;return double.NegativeInfinity;}
                            double s=((x-ax)*(rz-az)-(z-az)*(rx-ax))/det,t=((qx-ax)*(z-az)-(qz-az)*(x-ax))/det;
                            bs=s;bt=t;
                            value=ah+(qh-ah)*s+(rh-ah)*t;return Math.Min(1-s-t,Math.Min(s,t));
                        }
                        double first=Plane(lax,laz,lbx,lbz,rax,raz,span.LevelA,span.LevelB,span.LevelA,out double h1,out double s1,out _);
                        double second=Plane(rax,raz,lbx,lbz,rbx,rbz,span.LevelA,span.LevelB,span.LevelB,out double h2,out double s2,out double t2);
                        inside=Math.Max(first,second)>=-1e-7;
                        level=Math.Max(span.LevelB,Math.Min(span.LevelA,first>=second?h1:h2));
                        // Depth, bend and bank material must use the SAME paired
                        // cross-section coordinates as the water elevation.
                        // Centre-line projection disagrees at an angled station.
                        u=D.Clamp(first>=second?s1:s2+t2);
                        width=span.WidthA+(span.WidthB-span.WidthA)*u;
                        double EdgeDistance(double ax,double az,double qx,double qz)
                        {double ex=qx-ax,ez=qz-az,t=D.Clamp(((x-ax)*ex+(z-az)*ez)/Math.Max(1e-20,ex*ex+ez*ez));return Math.Sqrt(Math.Pow(x-ax-ex*t,2)+Math.Pow(z-az-ez*t,2));}
                        bankDistance=Math.Min(EdgeDistance(lax,laz,lbx,lbz),EdgeDistance(rax,raz,rbx,rbz))*(inside?1:-1);
                    }
                    // At a bend the finite quad can cross an averaged end
                    // tangent's half-plane. Its actual interior still owns its
                    // bed; end-plane retirement applies only outside the quad.
                    if(!inside&&((before&&(span.From<0||!span.PoolA))||(after&&(span.To<0||!span.PoolB))))return;
                    if(!inside)Guard(level-bankDistance*.35,-bankDistance,ref current,guards,ref count,ref overflow,applying);
                    if(applying)return;
                    // Paired unit cross-section: low bed, rising submerged
                    // banks, then compact cut-only retirement into the valley.
                    double relative=1-bankDistance/Math.Max(.01,width);
                    V across=(span.SideA*(1-u)+span.SideB*u).Unit;
                    double signed=RiverDot(p-d*u,across)*physical/Math.Max(.01,width);
                    // C-type bend cross-section: deeper outer bank and a
                    // shallower point bar, without tilting the water surface.
                    double depth=span.DepthA+(span.DepthB-span.DepthA)*u;
                    double bed=level-depth*(1-Math.Min(1,relative*relative))*(1+span.Bend*signed);
                    double alluvium=span.AlluviumA+(span.AlluviumB-span.AlluviumA)*u;
                    double bankBend=span.BankBendA+(span.BankBendB-span.BankBendA)*u;
                    if(inside&&alluvium>0)
                        bed=RiverAlluvium.Bed(bed,level,depth,relative,signed,bankBend,alluvium);
                    if(!inside)bed=level-bankDistance*.35;
                    else{hasInside=true;insideHeight=Math.Min(insideHeight,Math.Min(uncutHeight,bed));}
                    double weight=inside?1:RiverscapeKernel.Weight(-bankDistance/(support-width));
                    current.DetailProtection=Math.Min(current.DetailProtection,1-weight);
                    current.Height=Math.Min(current.Height,current.Height+(Math.Min(current.Height,bed)-current.Height)*weight);
                    if(alluvium>0)
                    {
                        double deposit=RiverAlluvium.Deposit(level,current.Height,depth,relative,alluvium,signed,bankBend);
                        current.RiverSand=Math.Max(current.RiverSand,deposit*(1-span.BedGravel));
                        current.RiverGravel=Math.Max(current.RiverGravel,deposit*span.BedGravel);
                    }
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
                void Visit(ref Surface current,Span<BankGuard> guards,ref int count,ref bool overflow,bool applying)
                {
                if(localRiverscapes!=null)
                {
                    V offset=point-riverRegionCenter;
                    var bin=((int)Math.Floor(RiverDot(offset,riverEast)*physical/riverBinSize),(int)Math.Floor(RiverDot(offset,riverNorth)*physical/riverBinSize));
                    if(riverBins.TryGetValue(bin,out var nearby))foreach(int index in nearby)
                    {ref readonly var s=ref localRiverscapes[index];Apply(in s,ref current,guards,ref count,ref overflow,applying);Pool(s.From,s.A,s.WidthA,s.PoolA,ref current,guards,ref count,ref overflow,applying);Pool(s.To,s.B,s.WidthB,s.PoolB,ref current,guards,ref count,ref overflow,applying);}
                }
                else
                {
                    if(riverscapeCandidates==null)SelectRiverscapes(riverRegionExtent>0?riverRegionCenter:point,riverRegionExtent>0?riverRegionExtent:1);
                    foreach(int id in riverscapeCandidates)
                    {
                        // A conservative root-chord envelope includes every
                        // displaced station and the chords connecting them.
                        // Do not prepare every distant reach in a large patch,
                        // or reject a valid chord using the finer fractal line.
                        int next=owner.riverReceivers[id];V a=owner.rootNodes[id].Position,b=owner.rootNodes[next].Position,d=b-a,p=point-a;
                        double t=D.Clamp(RiverDot(p,d)/Math.Max(1e-30,RiverDot(d,d)));
                        double width=Math.Max(owner.riverNodeWidths[id],owner.riverNodeWidths[next]);
                        if((p-d*t).Length*physical>d.Length*physical*.4+width*5+4)continue;
                        var reach=owner.PrepareRiverscape(id);
                        for(int i=0;i<reach.Length;i++){ref readonly var span=ref reach[i];Apply(in span,ref current,guards,ref count,ref overflow,applying);Pool(span.From,span.A,span.WidthA,span.PoolA,ref current,guards,ref count,ref overflow,applying);Pool(span.To,span.B,span.WidthB,span.PoolB,ref current,guards,ref count,ref overflow,applying);}
                    }
                }
                }
                Visit(ref surface,bankGuards,ref guardCount,ref guardOverflow,false);
                // A channel bed outranks another channel's dry valley support.
                // Only actual overlapping wet footprints combine their beds.
                if(hasInside)surface.Height=insideHeight;
                cutHeight=surface.Height;
                if(!hasInside)for(int i=0;i<guardCount;i++)surface.Height=Math.Max(surface.Height,RiverscapeKernel.ProtectBank(cutHeight,uncutHeight,bankGuards[i].Height,bankGuards[i].Distance));
                // Dense junctions fall back to another bounded candidate pass,
                // without allocating a managed list or dropping constraints.
                if(!hasInside&&guardOverflow)Visit(ref surface,Span<BankGuard>.Empty,ref guardCount,ref guardOverflow,true);
                if(surface.Kind!=LocalWaterKind.None)surface.Mask=surface.Water-surface.Height;
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
