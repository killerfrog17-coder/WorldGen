using System;
using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public enum SettlementMarkerMode { Off, CapitalsOnly, All, CitiesOnly, LargeCitiesOnly }
    public sealed class SettlementMarker
    {
        public int Id,Cell;public string Name="";public long Population;public bool Capital,City,Large;
    }
    public static class SettlementMarkers
    {
        public static string Name(SettlementMarkerMode mode)=>mode==SettlementMarkerMode.CapitalsOnly?"Capitals only":mode==SettlementMarkerMode.CitiesOnly?"Cities only":mode==SettlementMarkerMode.LargeCitiesOnly?"Large cities only":mode.ToString();
        public static SettlementMarkerMode Next(SettlementMarkerMode mode)=>(SettlementMarkerMode)(((int)mode+1)%5);
        // Renderer publication only, not a simulation census. Filtered arrays are
        // reused between frames, so Off/Capitals do not visit every old settlement.
        public static SettlementMarker[][] Build(PrehistoryState s)
        {
            var result=Enumerable.Range(0,5).Select(_=>Array.Empty<SettlementMarker>()).ToArray();if(s?.Society==null)return result;
            var h=s.History;var a=h?.Aggregates;
            var live=s.Society.Settlements.Where(t=>t.Population>=100&&(a?.Living==null||a.Living.Cities[t.Id].AbandonedYear<0)).Select(t=>
            {
                int polity=a!=null?a.Resolve(t.PolityId,h):t.PolityId;
                bool capital=polity>=0&&h!=null&&h.Polities[polity].EndedYear<0&&h.Polities[polity].CapitalId==t.Id;
                bool proto=h?.RegionalTraditions!=null&&HistoricalRegionalTraditions.Proto(s,t.Id);
                return new SettlementMarker{Id=t.Id,Cell=t.Cell,Name=t.Name,Population=t.Population,Capital=capital,City=capital||proto||t.Population>=2000};
            }).OrderByDescending(t=>t.Capital).ThenByDescending(t=>t.Population).ThenBy(t=>t.Id).ToArray();
            var cities=live.Where(t=>t.City).OrderByDescending(t=>t.Population).ThenBy(t=>t.Id).ToArray();
            int limit=Math.Min(20,Math.Max(1,(cities.Length+9)/10));long threshold=cities.Length==0?long.MaxValue:Math.Max(1000,(long)(cities[0].Population*.2));
            var largest=cities.Take(limit).Where(t=>t.Population>=threshold).Select(t=>t.Id).ToHashSet();
            foreach(var mark in live)mark.Large=largest.Contains(mark.Id);
            result[1]=live.Where(t=>t.Capital).ToArray();result[2]=live;result[3]=live.Where(t=>t.City).ToArray();result[4]=live.Where(t=>t.Large).Take(20).ToArray();return result;
        }
    }
}
