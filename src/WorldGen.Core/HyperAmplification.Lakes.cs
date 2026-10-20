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
