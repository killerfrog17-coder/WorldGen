using System;
using System.Linq;
using System.Globalization;
using System.Runtime.CompilerServices;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static partial class SocietyView
    {
        static readonly ConditionalWeakTable<PrehistoryState,ChronicleEvent[]> headlineCache=new ConditionalWeakTable<PrehistoryState,ChronicleEvent[]>();
        public static string SortValue(HistoryLegendEntry entry,string sort)=>sort=="Population"?entry.Population.ToString("N0",CultureInfo.InvariantCulture):sort=="Land area"?entry.AreaKm2.ToString("N0",CultureInfo.InvariantCulture)+" km²":"";
        // Presentation selection only. The complete archive and its hash stay intact.
        public static ChronicleEvent[] Headlines(PrehistoryState state)
        {
            if(state?.Society==null)return Array.Empty<ChronicleEvent>();
            return headlineCache.GetValue(state,SelectHeadlines);
        }
        static ChronicleEvent[] SelectHeadlines(PrehistoryState state)
        {
            if(state.History==null)return state.Society.Events.GroupBy(e=>e.Category).Select(g=>g.Last()).ToArray();
            var h=state.History;var selected=new ChronicleEvent[3];int remaining=3;
            int oldest=state.Years-(h.Polities.Length>0?300:2000);
            long global=Math.Max(1,Population(state));
            for(int i=h.Archive.Count-1;i>=0&&remaining>0;i--)
            {
                var e=h.Archive[i];if(e.Year<oldest)break;int category=(int)e.Category;
                if(category<0||category>=selected.Length||selected[category]!=null||!IsHeadline(e,h,global,state))continue;
                selected[category]=new ChronicleEvent{Year=e.Year,CultureId=e.CultureId,Cell=e.Cell,Category=e.Category,Text=e.Text};remaining--;
            }
            return selected.Where(e=>e!=null).OrderBy(e=>e.Year).ThenBy(e=>e.Category).ToArray();
        }
        static bool IsHeadline(HistoricalEvent e,HistoryState h,long global,PrehistoryState state)
        {
            switch(e.Kind)
            {
                case "local-decline":case "abandonment":
                    return h.Aggregates?.Living!=null&&e.SettlementId>=0&&e.SettlementId<h.Aggregates.Living.Cities.Length&&h.Aggregates.Living.Cities[e.SettlementId].PeakPopulation>=Math.Max(10000,global/500);
                case "settlement":return e.SettlementId==0||e.SettlementId>=0&&e.SettlementId<state.Society.Settlements.Length&&state.Society.Settlements[e.SettlementId].Population>=Math.Max(5000,global/500);
                case "peace":if(h.Version=="history-5"||h.Version=="history-6"||h.Version=="history-7"||h.Version=="history-8"||h.Version=="history-9"||h.Version=="history-10"||(h.Version=="history-11"||(h.Version=="history-12"||(h.Version=="history-13"||(h.Version=="history-14"||(h.Version=="history-15"||h.Version=="history-16"))))))return false;goto case "war";
                case "frontier-retreat":case "provincial-unrest":case "provincial-resistance":case "settlement-abandoned":case "settlement-reoccupied":case "administration-retained":return false;
                case "election":case "succession":case "succession-dispute":case "contact":case "trade":case "treaty-lapse":case "battle":case "religious-contact":case "religious-community":case "sea-contact":case "government":case "incorporation":case "district-incorporated":case "incorporation-abandoned":case "district-office":case "district-office-closed":return false;
                case "great-conquest":case "great-ruler-death":case "succession-crisis":case "empire-founded":case "public-works":case "collapse":
                case "campaign-start":case "campaign-end":
                case "dependency-founded":case "dependency-ended":case "tributary-revolt":
                case "dynasty-change":
                case "regime-change":
                case "provincial-revolt":
                    return new[]{e.PolityId,e.OtherPolityId}.Any(id=>id>=0&&id<h.Polities.Length&&Math.Max(h.Polities[id].Population,h.Dynamics?.States.Length>id?h.Dynamics.States[id].PeakPopulation:0)>=Math.Max(2500,global/100));
                case "war":case "alliance":case "conquest":case "capital-moved":case "secession":
                    return new[]{e.PolityId,e.OtherPolityId}.Any(id=>id>=0&&id<h.Polities.Length&&h.Polities[id].Population>=Math.Max(2500,global/100));
                case "epidemic":return false;
                case "harvest-shock":return false;
                case "food-shortage":case "major-epidemic":
                    return e.SettlementId>=0&&e.SettlementId<state.Society.Settlements.Length&&state.Society.Settlements[e.SettlementId].Population>=Math.Max(5000,global/500);
                default:return true;
            }
        }
    }
}
