using System;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void TransferRealmCenterTitles(PrehistoryState s,int city,int previous,int next)
        {
            if(!RealmHistory(s)||next<0||previous==next)return;
            var c=s.History.Aggregates.Empires.Connections;var routes=c.Realms.ClaimRoutes;if(routes==null)return;
            var town=s.Society.Settlements[city];int home=CellPatch[town.Cell];if(home<0||town.Population<1200||!s.History.Aggregates.Cities[city].Supported)return;
            if(routes.SourceHeads!=null)
            {routes.PendingHeads=routes.PendingHeads.With(town.Cell,routes.SourceHeads[town.Cell]);ReviewRealmCenterTitles(s,city);return;}
            if(previous<0)return;
            // The local legal authority follows its real administrative center.
            // Only this district and at most eight recorded neighbors are
            // addressed, never all titles or cells in the departing empire.
            void Consider(int region)
            {
                int title=c.TitleAt[region];if(title<0||routes.Sources[region]!=town.Cell||s.History.Aggregates.Resolve(c.Titles[title].Holder,s.History)!=previous)return;
                TransferRecognizedTitle(s,title,next,0);
            }
            Consider(home);var neighbors=s.History.Aggregates.Regions[home].Routes;
            int offset=neighbors.Length==0?0:(int)(HistoryRoll("realm-local-title-succession",city,s.Years)*neighbors.Length);
            for(int k=0;k<Math.Min(8,neighbors.Length);k++){var route=Routes[neighbors[(offset+k)%neighbors.Length]];if(!route.Sea)Consider(route.A==home?route.B:route.A);}
        }
        void ReviewRealmCenterTitles(PrehistoryState s,int city)
        {
            if(!RealmHistory(s)||!s.History.Aggregates.Cities[city].Supported)return;
            var town=s.Society.Settlements[city];ReviewRealmSourceTitles(s,town.Cell,s.History.Aggregates.Resolve(town.PolityId,s.History));
        }
        void QueueRealmSourceTitles(PrehistoryState s,int cell)
        {
            if(!RealmHistory(s))return;var r=s.History.Aggregates.Empires.Connections.Realms.ClaimRoutes;
            if(r?.SourceHeads!=null)r.PendingHeads=r.PendingHeads.With(cell,r.SourceHeads[cell]);
        }
        void ReviewRealmSourceTitles(PrehistoryState s,int cell,int owner)
        {
            if(!RealmHistory(s))return;var a=s.History.Aggregates;var c=a.Empires.Connections;var r=c.Realms.ClaimRoutes;if(r?.PendingHeads==null||owner<0)return;
            int entry=r.PendingHeads[cell];if(entry<0||HistoricalCells.Polity(s,cell)!=owner)return;
            // Explicit legal transactions, at most eight documents per local
            // review. No territory, ecoregion or empire membership enumeration.
            for(int k=0;k<8&&entry>=0;k++)
            {
                int title=r.EntryTitles[entry];var document=c.Titles[title];
                if(r.Sources[document.Region]==cell&&a.Resolve(document.Holder,s.History)!=owner)TransferRecognizedTitle(s,title,owner,0);
                entry=r.EntryNext[entry];
            }
            r.PendingHeads=r.PendingHeads.With(cell,entry);
        }
    }
}
