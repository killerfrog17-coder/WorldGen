using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        readonly Dictionary<long,bool> diffusionSeaLanes=new Dictionary<long,bool>();
        readonly Queue<long> diffusionSeaLaneOrder=new Queue<long>();
        bool[] diffusionShoreMask;
        bool DiffusionShore(int cell)=>cell>=0&&cell<diffusionShoreMask.Length&&diffusionShoreMask[cell];
        bool DiffusionSeaLane(int first,int second)
        {
            long key=((long)Math.Min(first,second)<<32)|(uint)Math.Max(first,second);
            if(diffusionSeaLanes.TryGetValue(key,out bool value))return value;
            value=AggregateSeaLane(Math.Min(first,second),Math.Max(first,second));
            if(diffusionSeaLanes.Count>=32768)diffusionSeaLanes.Remove(diffusionSeaLaneOrder.Dequeue());
            diffusionSeaLanes.Add(key,value);diffusionSeaLaneOrder.Enqueue(key);return value;
        }
        double DiffusionSeaEffort(PrehistoryState s,int first,int second,int culture)
        {
            if(first==second)return 0;
            var hc=s.History.Cultures[culture];
            if(!HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing)||!DiffusionShore(first)||!DiffusionShore(second))return double.MaxValue;
            double km=D.Angle(World.Surface.Cells[first].Position,World.Surface.Cells[second].Position)*World.Recipe.RadiusKm;
            if(km>HistoricalDevelopmentEffects.CoastRangeKm(hc,s.Years,ColonialHistory(s))||!DiffusionSeaLane(first,second))return double.MaxValue;
            // A real coastal sailing route has loading/port overhead but lower
            // bulk-transport cost than carrying supplies over the same distance
            // inland. This is a logistics proxy, not a ship or ORBIS replica.
            return 30+km*(HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)?.12:.28);
        }
        void DiffusionPortContact(PrehistoryState s,Route route,int culture,int elapsed)
        {
            if(HistoryRoll("diffusion-port-network",route.A,s.Years)>1-Math.Exp(-elapsed/80.0))return;
            int FirstPort(int shore)=>NearbyLivingTowns(s,shore,180).Where(city=>DiffusionShore(s.Society.Settlements[city].Cell)&&s.Society.Settlements[city].Population>=300).DefaultIfEmpty(-1).First();
            int first=FirstPort(route.CellA),second=FirstPort(route.CellB);if(first<0||second<0||first==second)return;
            var a=s.History.Aggregates;int from=s.Society.Settlements[first].Cell,to=s.Society.Settlements[second].Cell;
            if(LivingWorldHistory(s)&&!InstitutionalRouteAllowed(s,from,to,culture))return;
            if(DiffusionSeaEffort(s,from,to,culture)==double.MaxValue)return;
            // Reserve a small share of the existing bounded contact list for a
            // successful overseas journey, rather than dropping it behind every
            // nearby village at the next local-neighbor refresh.
            foreach(var pair in new[]{(first,second),(second,first)})
            {
                var list=a.Cities[pair.Item1].Neighbors;if(list.Contains(pair.Item2))continue;
                if(list.Length<12)a.Cities[pair.Item1].Neighbors=list.Concat(new[]{pair.Item2}).ToArray();
                else{list=(int[])list.Clone();list[11]=pair.Item2;a.Cities[pair.Item1].Neighbors=list;}
            }
            LivingCultureContactAt(s,s.Society.Settlements[first].CultureId,s.Society.Settlements[second].CultureId,from,to);
        }
        double DiffusionPortAdministration(PrehistoryState s,int polity)
        {
            var a=s.History.Aggregates;var hc=s.History.Cultures[s.History.Polities[polity].CultureId];
            if(!HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation))return 1;
            var cities=a.Polities[polity].Cities;if(cities.Count<2)return 1;
            int offset=(int)(HistoryRoll("diffusion-admin-ports",polity,s.History.Polities[polity].FoundedYear)*cities.Count);
            for(int k=0;k<Math.Min(3,cities.Count);k++)
            {
                int city=cities.At((offset+k)%cities.Count);if(!DiffusionShore(s.Society.Settlements[city].Cell))continue;
                foreach(int peer in a.Cities[city].Neighbors)
                {
                    var town=s.Society.Settlements[peer];if(a.Living.Cities[peer].AbandonedYear>=0||a.Resolve(town.PolityId,s.History)!=polity||!DiffusionShore(town.Cell))continue;
                    double direct=D.Angle(World.Surface.Cells[s.Society.Settlements[city].Cell].Position,World.Surface.Cells[town.Cell].Position)*World.Recipe.RadiusKm;
                    if(direct>=150&&DiffusionSeaEffort(s,s.Society.Settlements[city].Cell,town.Cell,s.History.Polities[polity].CultureId)<direct*.5)return 2.5;
                }
            }
            return 1;
        }
    }
}
