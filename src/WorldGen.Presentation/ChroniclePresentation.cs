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
        static readonly ConditionalWeakTable<Prehistory,ChronicleFeed> feeds=new ConditionalWeakTable<Prehistory,ChronicleFeed>();
        public static ChronicleEvent[] Headlines(PrehistoryState state,Prehistory engine){var feed=feeds.GetValue(engine,_=>new ChronicleFeed());lock(feed)return feed.Read(state);}
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
            // One-off snapshot inspection. Live hosts use the engine-keyed
            // overload above so subsequent publications read only new records.
            return new ChronicleFeed().Read(state);
        }
    }
}
