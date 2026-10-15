using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        CellLocator shippingContactLocator;
        readonly System.Collections.Generic.Dictionary<int,int[]> shippingContactNeighbors=new System.Collections.Generic.Dictionary<int,int[]>();
        PrehistoryState shippingContactState;string shippingContactHash;HistoricalShippingLink[] shippingContactRecords;
        readonly System.Collections.Generic.Dictionary<int,System.Collections.Generic.List<HistoricalShippingLink>> shippingContactRows=new System.Collections.Generic.Dictionary<int,System.Collections.Generic.List<HistoricalShippingLink>>();
        void ModernOceanExchange(PrehistoryState s,SortedSet<(int,int)> contacts,CancellationToken token,double elapsedYears)
        {
            var h=s.History;var ports=s.Society.Settlements.Where(t=>t.Population>=1000&&t.FoodReserve>=t.Population/2&&(World.Hydrology.OceanConnected[t.Cell]||World.Surface.Cells[t.Cell].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]))).OrderBy(t=>t.Id).ToArray();
            foreach(var group in ports.GroupBy(t=>t.CultureId).OrderBy(g=>g.Key))
            {
                var hc=h.Cultures[group.Key];if(!HistoricalTechnologies.Has(hc,HistoricalTechnology.OceanNavigation))continue;
                int limit=HistoricalModernVoyageEffects.RangeM(hc,s.Years);
                foreach(var port in group.OrderByDescending(t=>t.Population).ThenBy(t=>t.Id).Take(3))
                {
                    token.ThrowIfCancellationRequested();int attempts=0,detours=0;
                    foreach(var destination in ports.Where(t=>t.CultureId!=port.CultureId&&VoyageLandmass(t.Cell)!=VoyageLandmass(port.Cell)&&!h.Modern.Shipping.Any(r=>r.PortA==port.Id&&r.PortB==t.Id||r.PortB==port.Id&&r.PortA==t.Id)).OrderByDescending(t=>V.Dot(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[port.Cell].Position)).ThenBy(t=>t.Id).Take(16))
                    {
                        if(attempts>=3)break;
                        int distance=(int)Math.Round(D.Angle(World.Surface.Cells[port.Cell].Position,World.Surface.Cells[destination.Cell].Position)*World.Recipe.RadiusKm*1000);
                        if(distance<900000||distance>limit)continue;
                        if(!OpenOceanPassage(port.Cell,destination.Cell,token)&&detours++>=4)continue;
                        distance=ModernSeaDistance(port.Cell,destination.Cell,limit,token);if(distance<=0)continue;
                        attempts++;double chance=.22*Math.Exp(-distance/(double)limit)*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years);
                        if(HistoryRoll("ocean-exchange-13-port-"+port.Id,destination.Id,s.Years)>=ElapsedChance(chance,elapsedYears/50))continue;
                        long provisions=HistoricalVoyageEffects.Provisioning(50,distance);if(port.FoodReserve<port.Population/2+provisions)continue;port.FoodReserve-=provisions;
                        int a=Math.Min(port.Id,destination.Id),b=Math.Max(port.Id,destination.Id);
                        var link=new HistoricalShippingLink{Id=h.Modern.Shipping.Length,EstablishedYear=s.Years,PortA=a,PortB=b,CultureA=s.Society.Settlements[a].CultureId,CultureB=s.Society.Settlements[b].CultureId,DistanceM=distance};
                        h.Modern.Shipping=h.Modern.Shipping.Concat(new[]{link}).ToArray();
                        HistoryEvent(s,"ocean-exchange","Provisioned ocean-going voyages establish contact between "+port.Name+" and "+destination.Name+". Plants and techniques can now be exchanged, but still need to suit the receiving environment.",port.Cell,port.CultureId,port.PolityId,destination.PolityId,port.Id,category:ChronicleCategory.Discoveries);
                    }
                }
            }
            foreach(var link in h.Modern.Shipping)
            {
                var a=s.Society.Settlements[link.PortA];var b=s.Society.Settlements[link.PortB];if(a.Population<100||b.Population<100||a.CultureId==b.CultureId)continue;
                contacts.Add((Math.Min(a.CultureId,b.CultureId),Math.Max(a.CultureId,b.CultureId)));
            }
        }
        readonly Dictionary<(int,int),bool> openOceanPassages=new Dictionary<(int,int),bool>();
        bool OpenOceanPassage(int from,int to,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();if(from>to){int swap=from;from=to;to=swap;}
            var key=(from,to);if(openOceanPassages.TryGetValue(key,out bool known))return known;
            if(voyageWorldLocator==null)voyageWorldLocator=new CellLocator(World.Surface.Cells);
            var a=World.Surface.Cells[from].Position;var b=World.Surface.Cells[to].Position;int steps=Math.Max(12,(int)Math.Ceiling(D.Angle(a,b)*World.Recipe.RadiusKm/80));
            for(int k=1;k<steps;k++){token.ThrowIfCancellationRequested();int cell=voyageWorldLocator.Nearest((a*(1-k/(double)steps)+b*(k/(double)steps)).Unit);if(!World.Hydrology.OceanConnected[cell]&&k>2&&k<steps-2){openOceanPassages[key]=false;return false;}}
            openOceanPassages[key]=true;return true;
        }
        bool ModernShippingContact(PrehistoryState s,int from,int to,int culture)
        {
            if(!ModernHistory(s)||!HistoricalTechnologies.Has(s.History.Cultures[culture],HistoricalTechnology.OceanNavigation))return false;
            var records=s.History.Modern.Shipping;if(records.Length==0)return false;
            if(!ReferenceEquals(s,shippingContactState)||s.Hash!=shippingContactHash||!ReferenceEquals(records,shippingContactRecords))
            {
                shippingContactRows.Clear();
                void Add(int cell,HistoricalShippingLink link){if(!shippingContactRows.TryGetValue(cell,out var row)){row=new System.Collections.Generic.List<HistoricalShippingLink>();shippingContactRows.Add(cell,row);}row.Add(link);}
                foreach(var link in records){Add(s.Society.Settlements[link.PortA].Cell,link);Add(s.Society.Settlements[link.PortB].Cell,link);}
                shippingContactState=s;shippingContactHash=s.Hash;shippingContactRecords=records;
            }
            if(shippingContactLocator==null)shippingContactLocator=new CellLocator(World.Surface.Cells);
            if(!shippingContactNeighbors.TryGetValue(from,out var nearby))
            {
                // Conservative broad phase; the original150km spherical test
                // below remains the only authoritative distance decision.
                double chord=2*D.Sin(151.0/(2*World.Recipe.RadiusKm));nearby=shippingContactLocator.Within(World.Surface.Cells[from].Position,chord*chord).ToArray();
                if(shippingContactNeighbors.Count>=8192)shippingContactNeighbors.Clear();shippingContactNeighbors.Add(from,nearby);
            }
            bool Near(int x,int y)=>D.Angle(World.Surface.Cells[x].Position,World.Surface.Cells[y].Position)*World.Recipe.RadiusKm<=150;
            foreach(int port in nearby)if(shippingContactRows.TryGetValue(port,out var links))foreach(var link in links)
            {var a=s.Society.Settlements[link.PortA];var b=s.Society.Settlements[link.PortB];if(a.Population<100||b.Population<100)continue;if(Near(from,a.Cell)&&Near(to,b.Cell)||Near(from,b.Cell)&&Near(to,a.Cell))return true;}
            return false;
        }
    }
}
