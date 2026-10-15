using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace WorldGen.Core
{
    public sealed class HistoricalSeaPath
    {
        public int DistanceM,Expanded;
        public int[] WaterCells=Array.Empty<int>();
    }
    // A bounded navigation query on the accepted ocean graph, not a terrain
    // generator or tactical boat simulation. Failure grants no connection.
    public static class HistoricalOceanPaths
    {
        public static HistoricalSeaPath Find(Surface surface,bool[] ocean,int from,int to,double radiusKm,int maximumDistanceM,int maximumExpanded=5000,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();int n=surface.Cells.Length;
            if(ocean.Length!=n||from<0||to<0||from>=n||to>=n||radiusKm<=0||maximumDistanceM<=0||maximumExpanded<1||maximumExpanded>20000)throw new ArgumentException("Invalid ocean path request.");
            var result=new HistoricalSeaPath();var goals=new Dictionary<int,int>();
            if(ocean[to])goals[to]=0;
            else for(int k=0;k<surface.Cells[to].Neighbors.Length;k++){int next=surface.Cells[to].Neighbors[k];if(ocean[next])goals[next]=surface.Cells[to].DistanceM[k];}
            if(goals.Count==0)return result;
            var distance=new Dictionary<int,int>();var parent=new Dictionary<int,int>();var closed=new HashSet<int>();var heap=new Heap();
            var estimates=new Dictionary<int,int>();
            int Estimate(int cell)
            {
                if(!estimates.TryGetValue(cell,out int value)){value=(int)Math.Max(0,Math.Floor(D.Angle(surface.Cells[cell].Position,surface.Cells[to].Position)*radiusKm*1000)-100);estimates.Add(cell,value);}return value;
            }
            void Seed(int cell,int cost){if(cost<=maximumDistanceM){distance[cell]=cost;parent[cell]=-1;heap.Push((long)cost+Estimate(cell),cell);}}
            if(ocean[from])Seed(from,0);
            else for(int k=0;k<surface.Cells[from].Neighbors.Length;k++){int next=surface.Cells[from].Neighbors[k];if(ocean[next])Seed(next,surface.Cells[from].DistanceM[k]);}
            while(heap.Count>0&&result.Expanded<maximumExpanded)
            {
                if((result.Expanded&63)==0)token.ThrowIfCancellationRequested();var item=heap.Pop();int cell=item.Id;
                if(!distance.TryGetValue(cell,out int cost)||item.Key!=(long)cost+Estimate(cell)||!closed.Add(cell))continue;result.Expanded++;
                if(goals.TryGetValue(cell,out int landing)&&cost+landing<=maximumDistanceM)
                {
                    var cells=new List<int>();for(int at=cell;at>=0;at=parent[at])cells.Add(at);cells.Reverse();result.WaterCells=cells.ToArray();result.DistanceM=cost+landing;return result;
                }
                var current=surface.Cells[cell];
                for(int k=0;k<current.Neighbors.Length;k++)
                {
                    int next=current.Neighbors[k];if(!ocean[next]||closed.Contains(next))continue;long candidate=(long)cost+current.DistanceM[k];
                    if(candidate>maximumDistanceM||candidate+Estimate(next)>maximumDistanceM)continue;
                    if(!distance.TryGetValue(next,out int old)||candidate<old||candidate==old&&cell<parent[next])
                    {distance[next]=(int)candidate;parent[next]=cell;heap.Push(candidate+Estimate(next),next);}
                }
            }
            return result;
        }
    }
    public sealed partial class Prehistory
    {
        readonly Dictionary<(int,int,int),int> modernSeaDistances=new Dictionary<(int,int,int),int>();
        int ModernSeaDistance(int from,int to,int maximumDistanceM,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();int a=Math.Min(from,to),b=Math.Max(from,to);var key=(a,b,maximumDistanceM);
            if(modernSeaDistances.TryGetValue(key,out int known))return known;
            int distance=(int)Math.Round(D.Angle(World.Surface.Cells[a].Position,World.Surface.Cells[b].Position)*World.Recipe.RadiusKm*1000);
            if(distance>maximumDistanceM)return 0;
            if(!OpenOceanPassage(a,b,token))
            {
                using(ProfileWork("bounded ocean path"))distance=HistoricalOceanPaths.Find(World.Surface,World.Hydrology.OceanConnected,a,b,World.Recipe.RadiusKm,maximumDistanceM,5000,token).DistanceM;
            }
            token.ThrowIfCancellationRequested();if(modernSeaDistances.Count>=4096)modernSeaDistances.Clear();modernSeaDistances[key]=distance;return distance;
        }
        public HistoricalSeaPath AssessOceanPath(int from,int to,int maximumDistanceM=9000000,CancellationToken token=default)
            =>HistoricalOceanPaths.Find(World.Surface,World.Hydrology.OceanConnected,from,to,World.Recipe.RadiusKm,maximumDistanceM,5000,token);
    }
}
