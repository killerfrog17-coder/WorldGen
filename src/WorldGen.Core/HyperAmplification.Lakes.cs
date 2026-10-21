using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace WorldGen.Core
{
    public sealed partial class HyperAmplification
    {
        int[] lakeFaces;
        double[] lakeLevels,rimLevels;
        WaterBody[] lakeBodies;
        int[] lakeOutletOwners;
        public int ConditionedLakeSpills {get;private set;}
        public int LakeBasinCount {get;private set;}
        public int RejectedLakeBasins {get;private set;}
        public int LakeBasinFaces {get;private set;}
        public double LakeBasinPreparationMilliseconds {get;private set;}

        // Once per derived terrain, not per history tick or streamed vertex.
        // Connected sublevel components use actual control beds. Dry perimeter
        // edges become refinement constraints; designated outlets remain river
        // interfaces, not arbitrary cuts through the body of a lake.
        void BuildLakeBasins(World world,CancellationToken token)
        {
            LakeBasinCount=RejectedLakeBasins=LakeBasinFaces=0;
            var watch=Stopwatch.StartNew();var waters=lakeBodies=world.Hydrology.Waters;
            lakeFaces=new int[roots.Length];rimLevels=new double[roots.Length];lakeLevels=new double[waters.Length];
            for(int i=0;i<roots.Length;i++){lakeFaces[i]=-1;rimLevels[i]=double.NaN;}
            var seeds=new List<int>[waters.Length];
            for(int i=0;i<waters.Length;i++)lakeLevels[i]=waters[i].SurfaceElevationM;
            for(int face=0;face<roots.Length;face++)
            {
                var f=roots[face];int first=-1,second=-1;
                void Seed(int node)
                {
                    var v=rootNodes[node];if(v.Type!=Kind.Lake||v.LakeKey==0||v.LakeKey>(ulong)waters.Length)return;
                    int id=(int)v.LakeKey-1;if(id==first||id==second)return;
                    if(first<0)first=id;else second=id;
                    if(seeds[id]==null)seeds[id]=new List<int>();seeds[id].Add(face);
                }
                Seed(f.A);Seed(f.B);Seed(f.C);
            }
            var order=new int[waters.Length];for(int i=0;i<order.Length;i++)order[i]=i;
            Array.Sort(order,(a,b)=>{int c=lakeLevels[a].CompareTo(lakeLevels[b]);return c!=0?c:a.CompareTo(b);});
            foreach(int id in order)
            {
                token.ThrowIfCancellationRequested();if(seeds[id]==null)continue;
                var visited=new HashSet<int>();var queue=new List<int>();bool open=false;
                foreach(int face in seeds[id])if(visited.Add(face))queue.Add(face);
                for(int next=0;next<queue.Count&&!open;next++)
                {
                    if((next&255)==0)token.ThrowIfCancellationRequested();
                    int face=queue[next];var f=roots[face];
                    if(rootNodes[f.A].Type==Kind.Ocean||rootNodes[f.B].Type==Kind.Ocean||rootNodes[f.C].Type==Kind.Ocean){open=true;break;}
                    if(lakeFaces[face]>=0&&lakeFaces[face]!=id)
                    {
                        // A lower standing body is an escape, not a surface
                        // to interpolate upwards into the current lake.
                        if(Math.Abs(lakeLevels[lakeFaces[face]]-lakeLevels[id])>.001)open=true;
                        continue;
                    }
                    void Cross(int edgeId)
                    {
                        var edge=coarseEdges[edgeId];
                        if(IsLakeOutlet(edge,id,waters))return;
                        if(Math.Min(rootNodes[edge.A].Height,rootNodes[edge.B].Height)>=lakeLevels[id])return;
                        int neighbour=edge.Left==face?edge.Right:edge.Left;
                        if(neighbour<0){open=true;return;}
                        if(visited.Add(neighbour))queue.Add(neighbour);
                    }
                    Cross(f.AB);Cross(f.BC);Cross(f.CA);
                }
                if(open){RejectedLakeBasins++;continue;}
                foreach(int face in queue)if(lakeFaces[face]<0){lakeFaces[face]=id;LakeBasinFaces++;}
                LakeBasinCount++;
            }
            // Preserve actual dry divides over all later refinements. A river
            // outlet is explicitly exempt; rivers are not solved by this pass.
            foreach(var edge in coarseEdges)
            {
                if(edge.Right<0)continue;int a=lakeFaces[edge.Left],b=lakeFaces[edge.Right];if(a==b)continue;
                void Rim(int id)
                {
                    if(id<0||IsLakeOutlet(edge,id,waters))return;
                    double level=lakeLevels[id];
                    if(Math.Min(rootNodes[edge.A].Height,rootNodes[edge.B].Height)<level)return;
                    void Set(int face){rimLevels[face]=double.IsNaN(rimLevels[face])?level:Math.Max(rimLevels[face],level);}
                    Set(edge.Left);Set(edge.Right);
                }
                Rim(a);Rim(b);
            }
            LakeBasinPreparationMilliseconds=watch.Elapsed.TotalMilliseconds;
        }

        // Constrain the INITIAL incision substrate at an accepted lake's
        // physical spill control. The old channel lowering pass could excavate
        // it tens of metres below the saved lake while leaving its surface
        // unchanged. Restore the spill bed BELOW the unincised source terrain,
        // before any local terrain is amplified; never warp a water surface or
        // erect banks around an already generated valley.
        void ConditionLakeSpills(World world,CancellationToken token)
        {
            lakeOutletOwners=new int[rootNodes.Length];for(int i=0;i<lakeOutletOwners.Length;i++)lakeOutletOwners[i]=-1;
            var accepted=new bool[lakeBodies.Length];foreach(int id in lakeFaces)if(id>=0)accepted[id]=true;
            for(int id=0;id<lakeBodies.Length;id++)
            {
                if(!accepted[id])continue;var body=lakeBodies[id];int cell=body.OutletCell;
                if(body.Endorheic||cell<0||cell>=rootNodes.Length||body.DownstreamCell<0)continue;
                var node=rootNodes[cell];if(node.Type==Kind.Ocean)continue;
                if(node.Type==Kind.Lake&&node.LakeKey!=(ulong)(uint)(id+1))continue;
                if(lakeOutletOwners[cell]>=0&&Math.Abs(rootNodes[cell].Water-body.SurfaceElevationM)>.001)continue;
                lakeOutletOwners[cell]=id;
                node.Water=body.SurfaceElevationM;
                // This is the pre-incision spill lip. The local paired river
                // section excavates its actual scaled depth ONCE afterwards.
                // Subtracting the old unscaled channel depth here leaves a
                // broad submerged hole outside the much narrower new river.
                node.Height=node.RiverBed=Math.Min(node.Crust,node.Water);
                ConditionedLakeSpills++;
            }
            var order=world.Hydrology.VisitOrder;
            // Correct only upstream controls over-excavated beneath these spill
            // datums. This is a bounded graph pass, not a fine flood or tick.
            for(int k=order.Length-1;k>=0;k--)
            {
                if((k&4095)==0)token.ThrowIfCancellationRequested();
                int i=order[k],next=world.Hydrology.Receiver[i];if(next<0||rootNodes[i].Type!=Kind.River)continue;
                int body=lakeOutletOwners[next];if(body<0||rootNodes[i].Water>=rootNodes[next].Water)continue;
                var node=rootNodes[i];node.Water=rootNodes[next].Water;
                node.Height=node.RiverBed=Math.Min(node.Crust,node.Water);
                lakeOutletOwners[i]=body;
                if(node.Crust<node.Water)
                {node.Type=Kind.Lake;node.LakeKey=(ulong)(uint)(body+1);node.LakeLevel=node.Water;node.LakeWeight=1;}
            }
            // Keep accepted body ownership: an incision correction is not a
            // second basin catalog generation. Re-seeding from newly inundated
            // river controls can spuriously reject the original lake instead
            // of repairing its outlet.
        }

        void CutLakeSpillMouth(V point,ref Surface surface)
        {
            int id=(int)surface.BodyKey-1;if(id<0||id>=lakeBodies.Length)return;
            var body=lakeBodies[id];int cell=body.OutletCell;
            if(body.Endorheic||cell<0||lakeOutletOwners[cell]!=id)return;
            var node=rootNodes[cell];double physical=radius*scale;
            double width=RiverscapeKernel.HalfWidth(node.Flow,scale);
            double distance=(point-node.Position).Length*physical;if(distance>=width)return;
            double depth=Math.Min(Math.Min(Depth(node.Flow),width*.65),.25+Math.Pow(node.Flow*scale*scale,.27)*.4);
            double q=distance/width;
            // Excavate the real, finite opening AFTER preserving the initial
            // spill lip. Lake water retains its fixed datum through the mouth.
            surface.Height=Math.Min(surface.Height,surface.Water-depth*(1-q*q));
            surface.Mask=surface.Water-surface.Height;
        }

        static bool IsLakeOutlet(CoarseEdge edge,int id,WaterBody[] waters)
        {
            var w=waters[id];if(w.Endorheic||w.OutletCell<0)return false;
            return edge.A==w.OutletCell||edge.B==w.OutletCell||edge.A==w.DownstreamCell||edge.B==w.DownstreamCell;
        }

        double LakeDivideLevel(CoarseEdge edge)
        {
            if(edge.Right<0)return double.NaN;int a=lakeFaces[edge.Left],b=lakeFaces[edge.Right];
            if(a==b)return double.NaN;double result=double.NaN;
            void Add(int id)
            {
                if(id<0||IsLakeOutlet(edge,id,lakeBodies))return;double level=lakeLevels[id];
                if(Math.Min(rootNodes[edge.A].Height,rootNodes[edge.B].Height)<level)return;
                result=double.IsNaN(result)?level:Math.Max(result,level);
            }
            Add(a);Add(b);return result;
        }

        void PreserveLakeRim(Vertex node,Face left,Face right)
        {
            // Limit the amplitude of NEW refinement relative to the coarse
            // dry divide. Do not erect a flat water-height levee after carving.
            // Applying the same smooth lower envelope throughout the adjacent
            // coarse faces avoids a one-vertex-wide artificial ridge.
            void Limit(Face face)
            {
                if(face==null)return;int root=face.Root;if(double.IsNaN(rimLevels[root]))return;
                var f=roots[root];f.Coordinates(node.Position,out double a,out double b,out double c);
                a=Math.Max(0,a);b=Math.Max(0,b);c=Math.Max(0,c);double sum=a+b+c;
                double baseline=(a*rootNodes[f.A].Height+b*rootNodes[f.B].Height+c*rootNodes[f.C].Height)/sum;
                void Apply(int id)
                {
                    if(id<0)return;double level=lakeLevels[id];if(baseline<=level)return;
                    node.BasinFloor=Math.Max(node.BasinFloor,level+.2*(baseline-level));
                }
                Apply(lakeFaces[root]);
                void Neighbour(int edgeId){var e=coarseEdges[edgeId];int next=e.Left==root?e.Right:e.Left;if(next>=0)Apply(lakeFaces[next]);}
                Neighbour(f.AB);Neighbour(f.BC);Neighbour(f.CA);
            }
            Limit(left);Limit(right);
            node.Height=Math.Max(node.Height,node.BasinFloor);
        }
    }
}
