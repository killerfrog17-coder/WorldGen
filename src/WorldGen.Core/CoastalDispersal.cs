using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // Near-shore travel need not settle an uninhabitable mountain patch first.
        // A route must stay on the immediate coastal band; open-ocean rules are unchanged.
        Route[] CoastalDetours(CancellationToken token)
        {
            var cells=World.Surface.Cells;int n=cells.Length;var ocean=World.Hydrology.OceanConnected;
            var band=new bool[n];for(int i=0;i<n;i++)band[i]=cells[i].Neighbors.Any(j=>ocean[i]!=ocean[j]);
            var distances=Enumerable.Repeat(int.MaxValue,n).ToArray();var owners=Enumerable.Repeat(-1,n).ToArray();var heap=new Heap();
            for(int i=0;i<n;i++)if(band[i]&&CellPatch[i]>=0&&Patches[CellPatch[i]].Capacity>=25){distances[i]=0;owners[i]=CellPatch[i];heap.Push(0,i);}
            var found=new SortedDictionary<(int,int),Route>();
            while(heap.Count>0)
            {
                token.ThrowIfCancellationRequested();var item=heap.Pop();int i=item.Id;if(item.Key!=distances[i])continue;
                for(int k=0;k<cells[i].Neighbors.Length;k++)
                {
                    int j=cells[i].Neighbors[k];if(!band[j])continue;
                    // Never treat a step between two deep ocean cells as coastal navigation.
                    if(ocean[i]&&ocean[j]&&!cells[i].Neighbors.Any(a=>!ocean[a]&&Array.IndexOf(cells[j].Neighbors,a)>=0))continue;
                    long distance=(long)distances[i]+cells[i].DistanceM[k];if(distance>900000)continue;
                    if(owners[j]>=0&&owners[j]!=owners[i]&&distance+distances[j]<=900000)
                    {
                        int a=Math.Min(owners[i],owners[j]),b=Math.Max(owners[i],owners[j]);var key=(a,b);int length=(int)(distance+distances[j]);
                        if(!found.TryGetValue(key,out var previous)||length<previous.DistanceM)found[key]=new Route{A=a,B=b,Sea=true,Coastal=true,DistanceM=length};
                    }
                    if(distance<distances[j]||distance==distances[j]&&owners[i]<owners[j]){distances[j]=(int)distance;owners[j]=owners[i];heap.Push(distance,j);}
                }
            }
            return found.Values.ToArray();
        }
    }
}
