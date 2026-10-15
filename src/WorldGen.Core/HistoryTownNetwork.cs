using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        int[] townNetworkSignature;int[][] townNetwork;
        int[] townNeighborSignature;int[][] townNeighbors;
        int[][] HistoricalTownNeighbors(Settlement[] inhabited,int townCount,CancellationToken token)
        {
            var key=inhabited.SelectMany(t=>new[]{t.Id,t.Cell}).ToArray();
            if(townNeighborSignature!=null&&key.SequenceEqual(townNeighborSignature))return townNeighbors;
            using var measurement=ProfileWork("town neighbor geometry",inhabited.Length);
            var neighbors=new int[townCount][];
            foreach(var town in inhabited)
            {
                token.ThrowIfCancellationRequested();var position=World.Surface.Cells[town.Cell].Position;
                neighbors[town.Id]=inhabited.Where(t=>t.Id!=town.Id).OrderByDescending(t=>V.Dot(World.Surface.Cells[t.Cell].Position,position)).ThenBy(t=>t.Id).Take(32).Select(t=>t.Id).ToArray();
            }
            townNeighborSignature=key;townNeighbors=neighbors;return neighbors;
        }
        public int[][] HistoricalTownContacts(PrehistoryState state,CancellationToken token=default)
            =>HistoricalTownContactGraph(state,token).Select(row=>(int[])row.Clone()).ToArray();
        int[][] HistoricalTownContactGraph(PrehistoryState s,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();var towns=s.Society.Settlements;var h=s.History;
            var signature=new List<int>{towns.Length,h.Relations.Length};
            if(EmergenceHistory(s))signature.Add(15);
            if(RealismHistory(s))signature.Add(16);
            if(ModernHistory(s))
            {
                signature.Add(h.Modern.Shipping.Length);
                foreach(var link in h.Modern.Shipping){signature.Add(link.PortA);signature.Add(link.PortB);}
            }
            foreach(var town in towns)
            {
                var hc=h.Cultures[town.CultureId];int skills=(HistoricalTechnologies.Has(hc,HistoricalTechnology.Roads)?1:0)|(HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing)?2:0)|(HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)?4:0);
                if(ModernHistory(s)&&HistoricalTechnologies.Has(hc,HistoricalTechnology.OceanNavigation))skills|=8;
                signature.Add(town.Cell);signature.Add(town.CultureId);signature.Add(town.PolityId);signature.Add(town.Population<100?0:town.Population>=12000?2:1);signature.Add(skills);
                if(RealismHistory(s))signature.Add((int)Math.Round(HistoricalModernEffects.LandTransport(hc,s.Years)*4));
            }
            foreach(var relation in h.Relations.OrderBy(r=>r.A).ThenBy(r=>r.B)){signature.Add(relation.A);signature.Add(relation.B);signature.Add(relation.Trade?1:0);signature.Add(relation.WarSince>=0?1:0);}
            var key=signature.ToArray();if(townNetworkSignature!=null&&key.SequenceEqual(townNetworkSignature))return townNetwork;
            var rows=Enumerable.Range(0,towns.Length).Select(_=>new SortedSet<int>()).ToArray();
            bool Link(Settlement a,Settlement b)
            {
                token.ThrowIfCancellationRequested();if(a.Id==b.Id)return false;
                double distance=D.Angle(World.Surface.Cells[a.Cell].Position,World.Surface.Cells[b.Cell].Position)*World.Recipe.RadiusKm;
                double transport=RealismHistory(s)?Math.Max(HistoricalModernEffects.LandTransport(h.Cultures[a.CultureId],s.Years),HistoricalModernEffects.LandTransport(h.Cultures[b.CultureId],s.Years)):1;
                if(distance>1000*transport||!FoodExchangeAllowed(s,a,b))return false;
                if(!ReachableHistoricalContact(s,a.Cell,b.Cell,a.CultureId,token)&&!ReachableHistoricalContact(s,b.Cell,a.Cell,b.CultureId,token))return false;
                rows[a.Id].Add(b.Id);rows[b.Id].Add(a.Id);return true;
            }
            var inhabited=towns.Where(t=>t.Population>=100).OrderBy(t=>t.Id).ToArray();
            var nearest=UseHistoryIndexes?HistoricalTownNeighbors(inhabited,towns.Length,token):null;
            foreach(var town in inhabited)
            {
                int target=town.Population>=12000&&HistoricalTechnologies.Has(h.Cultures[town.CultureId],HistoricalTechnology.Roads)?4:3,found=0;
                foreach(var neighbor in nearest==null?inhabited.Where(t=>t.Id!=town.Id).OrderByDescending(t=>V.Dot(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[town.Cell].Position)).ThenBy(t=>t.Id).Take(32):nearest[town.Id].Select(id=>towns[id]))
                {if(Link(town,neighbor))found++;if(found>=target)break;}
            }
            // Formal exchange adds a reachable bridge between courts, not every pair of their towns.
            foreach(var relation in h.Relations.Where(r=>r.Trade&&r.WarSince<0).OrderBy(r=>r.A).ThenBy(r=>r.B))
            {
                var a=inhabited.Where(t=>t.PolityId==relation.A).ToArray();var b=inhabited.Where(t=>t.PolityId==relation.B).ToArray();
                var pairs=(from x in a from y in b select (x,y)).OrderByDescending(p=>V.Dot(World.Surface.Cells[p.x.Cell].Position,World.Surface.Cells[p.y.Cell].Position)).ThenBy(p=>p.x.Id).ThenBy(p=>p.y.Id);
                foreach(var pair in pairs)if(Link(pair.x,pair.y))break;
            }
            if(ModernHistory(s))foreach(var link in h.Modern.Shipping)
            {var a=towns[link.PortA];var b=towns[link.PortB];if(a.Population>=100&&b.Population>=100&&FoodExchangeAllowed(s,a,b)){rows[a.Id].Add(b.Id);rows[b.Id].Add(a.Id);}}
            var graph=rows.Select(row=>row.ToArray()).ToArray();townNetworkSignature=key;townNetwork=graph;return graph;
        }
    }
}
