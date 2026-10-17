using System;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalRealmIndexCount {public int Value;}
    [Serializable] public sealed class HistoricalRealmClaimRoutes
    {
        public HistoricalCellTree Sources,EffortM;
        [System.Runtime.Serialization.OptionalField] public HistoricalCellTree SourceHeads,PendingHeads,EntryTitles,EntryNext;
        [System.Runtime.Serialization.OptionalField] public HistoricalRealmIndexCount IndexCount;
        public HistoricalRealmClaimRoutes Copy(){var r=(HistoricalRealmClaimRoutes)MemberwiseClone();if(IndexCount!=null)r.IndexCount=new HistoricalRealmIndexCount{Value=IndexCount.Value};return r;}
    }
    public sealed partial class Prehistory
    {
        bool RealmClaimSourceLive(PrehistoryState s,int polity,int source)
        {
            int owner=HistoricalCells.Polity(s,source);return owner>=0&&HistoricalImperialHierarchy.Sovereign(s,owner)==HistoricalImperialHierarchy.Sovereign(s,polity)
                &&TerritorialNodeLive(s,owner,s.History.Aggregates.Empires.Territories.Sources[source]);
        }
        void InitializeRealmClaimRoutes(PrehistoryState s)
        {
            var c=s.History.Aggregates.Empires.Connections;var r=c.Realms;
            bool fresh=r.ClaimRoutes==null;
            if(fresh)r.ClaimRoutes=new HistoricalRealmClaimRoutes{Sources=new HistoricalCellTree(Patches.Length),EffortM=new HistoricalCellTree(Patches.Length)};
            var routes=r.ClaimRoutes;if(routes.SourceHeads!=null)return;
            routes.SourceHeads=new HistoricalCellTree(CellPatch.Length);routes.PendingHeads=new HistoricalCellTree(CellPatch.Length);
            routes.IndexCount=new HistoricalRealmIndexCount();
            routes.EntryTitles=new HistoricalCellTree(33554432);routes.EntryNext=new HistoricalCellTree(33554432);
            // Explicit setup only. A previous paper title does not invent an
            // overseas supply route. Existing local capitals can witness theirs.
            foreach(var title in c.Titles)
            {
                if(!fresh){int source=routes.Sources[title.Region];if(source>=0)IndexRealmClaimSource(s,title.Region,source);continue;}
                int owner=s.History.Aggregates.Resolve(title.Holder,s.History);if(owner<0)continue;
                int cell=s.Society.Settlements[s.History.Polities[owner].CapitalId].Cell;
                if(CellPatch[cell]==title.Region)RecordRealmClaimRoute(s,title.Region,cell,0);
            }
            if(!fresh)foreach(var title in c.Titles)
            {
                int source=routes.Sources[title.Region];if(source<0)continue;int owner=HistoricalCells.Polity(s,source);
                if(owner>=0&&owner!=s.History.Aggregates.Resolve(title.Holder,s.History))routes.PendingHeads=routes.PendingHeads.With(source,routes.SourceHeads[source]);
            }
        }
        void RecordRealmClaimRoute(PrehistoryState s,int region,int source,int effortM)
        {
            var routes=s.History.Aggregates.Empires.Connections.Realms.ClaimRoutes;if(routes==null)return;
            bool changed=routes.Sources[region]!=source;
            routes.Sources=routes.Sources.With(region,source);routes.EffortM=routes.EffortM.With(region,effortM);
            if(changed&&routes.SourceHeads!=null)IndexRealmClaimSource(s,region,source);
        }
        void IndexRealmClaimSource(PrehistoryState s,int region,int source)
        {
            var c=s.History.Aggregates.Empires.Connections;var r=c.Realms.ClaimRoutes;int title=c.TitleAt[region];if(title<0)return;
            int entry=r.IndexCount.Value++;if(entry>=33554432)throw new InvalidOperationException("Territorial service archive limit reached.");
            r.EntryTitles=r.EntryTitles.With(entry,title);r.EntryNext=r.EntryNext.With(entry,r.SourceHeads[source]);r.SourceHeads=r.SourceHeads.With(source,entry);
        }
        void ValidateRealmClaimRoutes(PrehistoryState s)
        {
            var c=s.History.Aggregates.Empires.Connections;var r=c.Realms.ClaimRoutes;if(r==null)return;
            if(r.Sources?.Length!=Patches.Length||r.EffortM?.Length!=Patches.Length||!r.Sources.AllPresentValues((region,source)=>source>=0&&source<CellPatch.Length&&r.EffortM[region]>=0&&c.TitleAt[region]>=0)
                ||!r.EffortM.AllPresentValues((region,meters)=>meters>=0&&r.Sources[region]>=0))throw new ArgumentException("Invalid witnessed territorial service routes.");
            if(r.SourceHeads!=null&&(r.SourceHeads.Length!=CellPatch.Length||r.PendingHeads?.Length!=CellPatch.Length||r.EntryTitles?.Length!=33554432||r.EntryNext?.Length!=33554432||r.IndexCount==null||r.IndexCount.Value<0||r.IndexCount.Value>33554432
                ||!r.SourceHeads.AllPresentValues((cell,entry)=>entry>=0&&entry<r.IndexCount.Value)||!r.PendingHeads.AllPresentValues((cell,entry)=>entry>=0&&entry<r.IndexCount.Value)
                ||!r.EntryTitles.AllPresentValues((entry,title)=>entry<r.IndexCount.Value&&title>=0&&title<c.Titles.Length)||!r.EntryNext.AllPresentValues((entry,next)=>entry<r.IndexCount.Value&&next>=0&&next<entry)))throw new ArgumentException("Invalid bounded territorial succession index.");
        }
    }
}
