using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        Route[][] diffusionPassages;
        void PrepareDiffusionPassages()
        {
            if(diffusionPassages!=null)return;
            diffusionShoreMask=new bool[CellPatch.Length];
            for(int cell=0;cell<CellPatch.Length;cell++)if(CellPatch[cell]>=0)
                diffusionShoreMask[cell]=Patches[CellPatch[cell]].Island||World.Surface.Cells[cell].Neighbors.Any(n=>World.Hydrology.OceanConnected[n]);
            var neighbors=Enumerable.Range(0,Patches.Length).Select(_=>new List<Route>()).ToArray();
            foreach(var route in Routes){neighbors[route.A].Add(route);neighbors[route.B].Add(route);}
            diffusionPassages=new Route[Patches.Length][];
            // Static regional graph contraction, not an event-time cell search.
            // Small resource patches need not sustain a permanent 125-person
            // colony before a traveling band can continue into the next valley.
            for(int source=0;source<Patches.Length;source++)
            {
                var found=new SortedDictionary<int,Route>();var distances=new Dictionary<int,int>{{source,0}};
                var open=new SortedSet<(int Distance,int Region)>{(0,source)};int visited=0;
                while(open.Count>0&&visited++<32)
                {
                    var item=open.Min;open.Remove(item);
                    foreach(var edge in neighbors[item.Region])
                    {
                        if(edge.Sea)continue;int next=edge.A==item.Region?edge.B:edge.A;if(next==source)continue;
                        var habitat=World.Ecology.Habitat[Patches[next].Cell];
                        if(habitat.Biome==Biome.Ice||World.ElevationM[Patches[next].Cell]>4500)continue;
                        int distance=item.Distance+edge.DistanceM+(item.Region==source?0:(int)Math.Round(Patches[item.Region].RadiusKm*1000));
                        if(distance>500000||distances.TryGetValue(next,out int prior)&&prior<=distance)continue;
                        if(distances.ContainsKey(next))open.Remove((distances[next],next));distances[next]=distance;
                        if(Patches[next].Capacity>=125)
                        {
                            if(item.Region!=source)found[next]=new Route{A=source,B=next,DistanceM=distance};
                        }
                        else open.Add((distance,next));
                    }
                }
                diffusionPassages[source]=found.Values.OrderBy(r=>r.DistanceM).ThenBy(r=>r.B).Take(12).ToArray();
            }
        }
        Route DiffusionDeparture(PrehistoryState s,int region,int trial)
        {
            var ordinary=s.History.Aggregates.Regions[region].Routes;var passages=diffusionPassages[region];
            int total=ordinary.Length+passages.Length;
            int selected=(int)(HistoryRoll("provincial-dispersal-route-"+trial,region,s.Years)*total);
            return selected<ordinary.Length?Routes[ordinary[selected]]:passages[selected-ordinary.Length];
        }
    }
}
