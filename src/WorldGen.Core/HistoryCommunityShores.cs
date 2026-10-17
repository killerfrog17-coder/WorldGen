using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        Route[][] communityVoyages;
        int[] communityRasterShore;
        void PrepareCommunityShores()
        {
            if(communityVoyages!=null)return;
            int width=OceanRasterWidth,height=OceanRasterHeight,count=width*height;
            var distances=Enumerable.Repeat(long.MaxValue,count).ToArray();var owners=Enumerable.Repeat(-1,count).ToArray();var shores=Enumerable.Repeat(-1,count).ToArray();
            var positions=new V[count];var heap=new Heap();
            int Node(V point)
            {int x=Math.Max(0,Math.Min(width-1,(int)((Math.Atan2(point.Y,point.X)/(2*Math.PI)+.5)*width))),y=Math.Max(0,Math.Min(height-1,(int)((.5-Math.Asin(point.Z)/Math.PI)*height)));return y*width+x;}
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {double lat=Math.PI*(.5-(y+.5)/height),lon=2*Math.PI*((x+.5)/width-.5);positions[y*width+x]=new V(Math.Cos(lat)*Math.Cos(lon),Math.Cos(lat)*Math.Sin(lon),Math.Sin(lat));}
            // Setup only. Every usable shore can seed the fixed ocean raster;
            // a large non-convex region is not represented by just one beach.
            foreach(var patch in Patches)
            {
                int region=CellPatch[patch.Cell];if(region<0||patch.Capacity<25)continue;
                foreach(int cell in patch.Cells)
                {
                    if(!patch.Island&&!World.Surface.Cells[cell].Neighbors.Any(n=>World.Hydrology.OceanConnected[n]))continue;
                    var point=World.Surface.Cells[cell].Position;int node=Node(point),x=node%width,y=node/width,best=-1;long meters=long.MaxValue;
                    for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
                    {int ny=y+dy;if(ny<0||ny>=height)continue;int candidate=ny*width+(x+dx+width)%width;if(!aggregateOceanRaster[candidate])continue;long distance=(long)Math.Round(D.Angle(point,positions[candidate])*World.Recipe.RadiusKm*1000);if(distance<meters||distance==meters&&candidate<best){meters=distance;best=candidate;}}
                    if(best<0||meters>distances[best]||meters==distances[best]&&(region>owners[best]||region==owners[best]&&cell>=shores[best]))continue;
                    owners[best]=region;shores[best]=cell;distances[best]=meters;heap.Push(meters,best,region);
                }
            }
            int[] dxs={-1,0,1,-1,1,-1,0,1},dys={-1,-1,-1,0,0,1,1,1};
            long Edge(int first,int second)=>(long)Math.Max(1,Math.Round(D.Angle(positions[first],positions[second])*World.Recipe.RadiusKm*1000));
            while(heap.Count>0)
            {
                var item=heap.Pop();int node=item.Id;if(item.Key!=distances[node]||item.From!=owners[node])continue;
                int x=node%width,y=node/width;
                for(int k=0;k<8;k++)
                {
                    int ny=y+dys[k];if(ny<0||ny>=height)continue;int next=ny*width+(x+dxs[k]+width)%width;if(!aggregateOceanRaster[next])continue;
                    long distance=item.Key+Edge(node,next);int owner=owners[node];
                    if(distance>distances[next]||distance==distances[next]&&(owner>owners[next]||owner==owners[next]&&shores[node]>=shores[next]))continue;
                    distances[next]=distance;owners[next]=owner;shores[next]=shores[node];heap.Push(distance,next,owner);
                }
            }
            var links=new SortedDictionary<(int,int),Route>();
            void Add(int first,int second,int cellA,int cellB,long meters)
            {
                if(first<0||second<0||first==second||meters>14000000)return;
                if(first>second){(first,second)=(second,first);(cellA,cellB)=(cellB,cellA);}
                var key=(first,second);int distance=(int)Math.Max(1000,meters);
                if(links.TryGetValue(key,out var old)&&(old.DistanceM<distance||old.DistanceM==distance&&(old.CellA<cellA||old.CellA==cellA&&old.CellB<=cellB)))return;
                links[key]=new Route{A=first,B=second,CellA=cellA,CellB=cellB,Sea=true,Coastal=true,DistanceM=distance};
            }
            for(int node=0;node<count;node++)
            {
                if(owners[node]<0)continue;int x=node%width,y=node/width;
                for(int k=0;k<8;k++){int ny=y+dys[k];if(ny<0||ny>=height)continue;int next=ny*width+(x+dxs[k]+width)%width;if(next<=node||owners[next]<0||owners[node]==owners[next])continue;Add(owners[node],owners[next],shores[node],shores[next],distances[node]+distances[next]+Edge(node,next));}
            }
            // Keep physically checked longer voyages, but never prune the local
            // sea graph to an arbitrary degree cap that can isolate an island.
            foreach(var routes in developmentVoyages)foreach(var route in routes)Add(route.A,route.B,route.CellA,route.CellB,route.DistanceM);
            var lists=Enumerable.Range(0,Patches.Length).Select(_=>new List<Route>()).ToArray();
            foreach(var route in links.Values)
            {lists[route.A].Add(route);lists[route.B].Add(new Route{A=route.B,B=route.A,CellA=route.CellB,CellB=route.CellA,DistanceM=route.DistanceM,Sea=true,Coastal=route.Coastal});}
            communityVoyages=lists.Select(list=>list.OrderBy(r=>r.DistanceM).ThenBy(r=>r.B).ThenBy(r=>r.CellA).ToArray()).ToArray();
            communityRasterShore=shores;
        }
    }
}
