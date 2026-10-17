using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalTerritorialServiceRecovery
    {
        public int Version=1,PendingCursor;
        public long ReassignedCells,WithdrawnCells,WithdrawnKm2;
        public HistoricalLocalBag PendingSources=new HistoricalLocalBag();
        [System.Runtime.Serialization.OptionalField] public HistoricalLocalBag RetiredGovernments;
        public HistoricalLocalBag[] CityEdges=Array.Empty<HistoricalLocalBag>();
        public HistoricalCellTree Cursor=new HistoricalCellTree(33554432),Reviewed=new HistoricalCellTree(33554432),CreditKm2=new HistoricalCellTree(33554432),LastReviewYear=new HistoricalCellTree(33554432);
        [System.Runtime.Serialization.OptionalField] public HistoricalCellTree GovernmentCursor;
        public HistoricalTerritorialServiceRecovery Copy()=>new HistoricalTerritorialServiceRecovery{Version=Version,PendingCursor=PendingCursor,ReassignedCells=ReassignedCells,WithdrawnCells=WithdrawnCells,WithdrawnKm2=WithdrawnKm2,PendingSources=PendingSources.Copy(),RetiredGovernments=RetiredGovernments?.Copy(),GovernmentCursor=GovernmentCursor,CityEdges=CityEdges.Select(b=>b.Copy()).ToArray(),Cursor=Cursor,Reviewed=Reviewed,CreditKm2=CreditKm2,LastReviewYear=LastReviewYear};
    }
    public static class HistoricalServiceRecoveryEffects
    {
        public static int Key(int source)=>source>=0?checked(source*2):source<=-2?checked((-source-2)*2+1):-1;
        public static int Source(int key)=>key%2==0?key/2:-2-key/2;
        // A physical-area envelope, not a number of mesh cells. Review frequency
        // can speed detection but cannot accelerate the district's retreat.
        public static int Credit(int previous,int elapsed,long area)=> (int)Math.Min(int.MaxValue,Math.Max(0,previous)+(double)Math.Max(0,elapsed)*Math.Max(5000,area/20.0));
    }
    public sealed partial class Prehistory
    {
        static bool ServiceRecoveryHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.ServiceRecovery!=null;
        void EnsureServiceRecoveryCities(PrehistoryState s)
        {
            var r=s.History.Aggregates.Empires.Connections.ServiceRecovery;if(r==null)return;
            int old=r.CityEdges.Length;if(old>=s.History.Aggregates.Cities.Length)return;
            Array.Resize(ref r.CityEdges,s.History.Aggregates.Cities.Length);for(int i=old;i<r.CityEdges.Length;i++)r.CityEdges[i]=new HistoricalLocalBag();
        }
        bool ServiceDistrictBoundary(PrehistoryState s,int cell,int source)
        {
            int owner=HistoricalCells.Polity(s,cell);var t=s.History.Aggregates.Empires.Territories;
            foreach(int next in World.Surface.Cells[cell].Neighbors)
                if(PoliticalFrontierCell(s,next,1)&&World.Hydrology.LakeCoverageFraction[next]<.9&&(t.Sources[next]!=source||HistoricalCells.Polity(s,next)!=owner))return true;
            return false;
        }
        void RefreshServiceRecoveryEdge(PrehistoryState s,int cell)
        {
            if(!ServiceRecoveryHistory(s))return;EnsureServiceRecoveryCities(s);var a=s.History.Aggregates;int source=a.Empires.Territories.Sources[cell];if(source<0)return;
            var bag=a.Empires.Connections.ServiceRecovery.CityEdges[source];if(ServiceDistrictBoundary(s,cell,source))bag.Add(cell);else bag.Remove(cell);
        }
        void RefreshServiceRecoveryNeighborhood(PrehistoryState s,int cell)
        {
            if(!ServiceRecoveryHistory(s))return;RefreshServiceRecoveryEdge(s,cell);RefreshProvincialFrontier(s,cell);
            foreach(int next in World.Surface.Cells[cell].Neighbors){RefreshServiceRecoveryEdge(s,next);RefreshProvincialFrontier(s,next);}
        }
        // Structural ownership/closure only. An inhabited town does not lose
        // its district merely because this year's harvest cannot support it.
        bool ServiceSourceStructurallyLive(PrehistoryState s,int owner,int source)
        {
            if(owner<0)return false;var a=s.History.Aggregates;var t=a.Empires.Territories;
            for(int depth=0;depth<8;depth++)
            {
                if(source>=0)return source<a.Cities.Length&&a.Living.Cities[source].AbandonedYear<0&&a.Resolve(s.Society.Settlements[source].PolityId,s.History)==owner;
                if(source== -1||-source-2>=t.Posts.Length)return false;var post=t.Posts[-source-2];
                if(post.ClosedYear>=0||a.Resolve(post.PolityId,s.History)!=owner||HistoricalCells.Polity(s,post.Cell)!=owner)return false;source=post.ParentSource;
            }
            return false;
        }
        void InitializeTerritorialServiceRecovery(PrehistoryState s)
        {
            var a=s.History.Aggregates;var c=a.Empires.Connections;if(c.ServiceRecovery!=null)return;
            c.ServiceRecovery=new HistoricalTerritorialServiceRecovery{Version=2,RetiredGovernments=new HistoricalLocalBag(),GovernmentCursor=new HistoricalCellTree(33554432),CityEdges=a.Cities.Select(_=>new HistoricalLocalBag()).ToArray()};
            // Explicit fresh setup/upgrade only. No event or actor enumerates
            // terrain membership. Structural status is shared per source/owner.
            var status=new Dictionary<long,bool>();
            for(int cell=0;cell<CellPatch.Length;cell++)
            {
                int source=a.Empires.Territories.Sources[cell];if(source== -1)continue;
                RefreshServiceRecoveryEdge(s,cell);RefreshProvincialFrontier(s,cell);
                int owner=HistoricalCells.Polity(s,cell);long pair=((long)owner<<32)|(uint)source;
                if(!status.TryGetValue(pair,out bool live)){live=ServiceSourceStructurallyLive(s,owner,source);status.Add(pair,live);}
                if(!live)QueueTerritorialServiceRecovery(s,source);
            }
            for(int polity=0;polity<a.Polities.Length;polity++)if(s.History.Polities[polity].EndedYear>=0)QueueRetiredServiceGovernment(s,polity);
        }
        void UpgradeTerritorialServiceRecovery(PrehistoryState s)
        {
            InitializeTerritorialServiceRecovery(s);var r=s.History.Aggregates.Empires.Connections.ServiceRecovery;if(r.Version>=2)return;
            r.Version=2;r.RetiredGovernments=new HistoricalLocalBag();r.GovernmentCursor=new HistoricalCellTree(33554432);
            for(int polity=0;polity<s.History.Polities.Length;polity++)if(s.History.Polities[polity].EndedYear>=0)QueueRetiredServiceGovernment(s,polity);
        }
        void QueueTerritorialServiceRecovery(PrehistoryState s,int source)
        {
            if(!ServiceRecoveryHistory(s)||source== -1)return;var r=s.History.Aggregates.Empires.Connections.ServiceRecovery;int key=HistoricalServiceRecoveryEffects.Key(source);
            r.Reviewed=r.Reviewed.With(key,0);if(r.PendingSources.Positions[key]>=0)return;
            r.PendingSources.Add(key);r.Cursor=r.Cursor.With(key,0);r.LastReviewYear=r.LastReviewYear.With(key,s.Years);r.CreditKm2=r.CreditKm2.With(key,0);
        }
        HistoricalLocalBag ServiceRecoveryEdges(PrehistoryState s,int source)=>source>=0?s.History.Aggregates.Empires.Connections.ServiceRecovery.CityEdges[source]:s.History.Aggregates.Empires.Frontiers.Posts[-source-2];
        void QueueRetiredServiceGovernment(PrehistoryState s,int polity)
        {
            if(!ServiceRecoveryHistory(s))return;var r=s.History.Aggregates.Empires.Connections.ServiceRecovery;if(r.Version<2)return;
            if(s.History.Aggregates.Empires.Governments[polity].AdministrativePosts.Count==0)return;
            if(r.RetiredGovernments.Positions[polity]>=0)return;r.RetiredGovernments.Add(polity);r.GovernmentCursor=r.GovernmentCursor.With(polity,0);
        }
        void RecoverRetiredServiceGovernments(PrehistoryState s)
        {
            var a=s.History.Aggregates;var r=a.Empires.Connections.ServiceRecovery;
            if(r.Version<2)return;
            // The tree's final slot stores the queue cursor (not a government
            // ID). Explicit positions prevent a large court starving its peers.
            int visits=Math.Min(2,r.RetiredGovernments.Count);
            for(int i=0;i<visits&&r.RetiredGovernments.Count>0;i++)
            {
                int at=Math.Max(0,r.GovernmentCursor[33554431])%r.RetiredGovernments.Count,polity=r.RetiredGovernments.At(at);var g=a.Empires.Governments[polity];int cursor=Math.Max(0,r.GovernmentCursor[polity]);
                for(int j=0;j<8&&cursor<g.AdministrativePosts.Count;j++)
                {
                    int id=g.AdministrativePosts.At(cursor++);var post=a.Empires.Territories.Posts[id];QueueTerritorialServiceRecovery(s,-2-id);
                    if(post.ClosedYear<0&&a.Resolve(post.PolityId,s.History)<0)
                    {
                        post.ClosedYear=s.Years;g.AdministrativePosts.Remove(id);cursor--;g.AdministrativePostCost=Math.Max(0,g.AdministrativePostCost-post.AnnualCost);
                        if(a.Empires.Territories.PostAt[post.Cell]==id)a.Empires.Territories.PostAt=a.Empires.Territories.PostAt.With(post.Cell,-1);
                    }
                }
                r.GovernmentCursor=r.GovernmentCursor.With(polity,cursor);
                if(cursor>=g.AdministrativePosts.Count)r.RetiredGovernments.Remove(polity);else at++;
                r.GovernmentCursor=r.GovernmentCursor.With(33554431,at);
            }
        }
        void AdvanceTerritorialServiceRecovery(PrehistoryState s,bool background=false)
        {
            if(!ServiceRecoveryHistory(s))return;var a=s.History.Aggregates;var r=a.Empires.Connections.ServiceRecovery;EnsureServiceRecoveryCities(s);
            RecoverRetiredServiceGovernments(s);
            int sources=Math.Min(background?2:8,r.PendingSources.Count);
            for(int trial=0;trial<sources&&r.PendingSources.Count>0;trial++)
            {
                if(r.PendingCursor>=r.PendingSources.Count)r.PendingCursor=0;int key=r.PendingSources.At(r.PendingCursor),source=HistoricalServiceRecoveryEffects.Source(key);var bag=ServiceRecoveryEdges(s,source);
                long area=source>=0?a.Empires.Territories.DistrictAreas[source]:a.Empires.Territories.Posts[-source-2].AreaKm2;
                int credit=HistoricalServiceRecoveryEffects.Credit(r.CreditKm2[key],s.Years-Math.Max(0,r.LastReviewYear[key]),area);
                r.LastReviewYear=r.LastReviewYear.With(key,s.Years);r.CreditKm2=r.CreditKm2.With(key,credit);
                if(bag.Count==0&&area>0){int anchor=TerritorialNodeCell(s,source);if(a.Empires.Territories.Sources[anchor]==source)bag.Add(anchor);}
                int visits=Math.Min(background?2:8,bag.Count);
                // No nested service operation reads this cursor: queuing an
                // already-pending source only resets Reviewed. Keep each
                // modulo/removal decision identical, but publish the final
                // value once instead of cloning a radix path for every cell.
                // Finally also preserves the old completed prefix on failure.
                int cursorValue=r.Cursor[key];bool cursorTouched=false;
                try
                {
                    for(int k=0;k<visits&&bag.Count>0;k++)
                    {
                        int cursor=Math.Max(0,cursorValue)%bag.Count,cell=bag.At(cursor);cursorValue=cursor+1;cursorTouched=true;
                        if(a.Empires.Territories.Sources[cell]!=source){bag.Remove(cell);r.Reviewed=r.Reviewed.With(key,0);continue;}
                        int owner=HistoricalCells.Polity(s,cell);
                        if(ServiceSourceStructurallyLive(s,owner,source)){r.Reviewed=r.Reviewed.With(key,Math.Max(0,r.Reviewed[key])+1);continue;}
                        r.Reviewed=r.Reviewed.With(key,0);int cost=(int)Math.Min(int.MaxValue,Math.Max(1,Math.Round(TerritorialPoliticalArea(cell))));
                        if(credit<cost)continue;
                        int replacement=-1,town=a.TownAt[cell];
                        if(owner>=0)
                        {
                            if(town>=0&&a.Living.Cities[town].AbandonedYear<0&&a.Resolve(s.Society.Settlements[town].PolityId,s.History)==owner)replacement=town;
                            else replacement=TerritorialServiceSource(s,owner,cell);
                        }
                        credit-=cost;r.CreditKm2=r.CreditKm2.With(key,credit);
                        if(replacement!= -1){AssignTerritorialSource(s,cell,replacement);r.ReassignedCells++;}
                        else
                        {
                            // Claims and cultures are independent ledgers. Releasing
                            // control only transfers the real rural cohort locally.
                            if(owner>=0)ClaimAggregatePolity(s,cell,-1);else AssignTerritorialSource(s,cell,-1);
                            r.WithdrawnCells++;r.WithdrawnKm2+=cost;
                        }
                    }
                }
                finally {if(cursorTouched)r.Cursor=r.Cursor.With(key,cursorValue);}
                if(bag.Count==0||r.Reviewed[key]>=bag.Count){r.PendingSources.Remove(key);r.Reviewed=r.Reviewed.With(key,0);}
                else r.PendingCursor++;
            }
        }
        void ValidateTerritorialServiceRecovery(PrehistoryState s)
        {
            if(!ServiceRecoveryHistory(s))return;var a=s.History.Aggregates;var r=a.Empires.Connections.ServiceRecovery;
            // New independent towns have no territorial source yet. Their
            // trailing empty bags are allocated lazily by the next service
            // event/source assignment, not by taking a checkpoint. Accept that
            // existing representation without changing saved bytes or timing.
            if(r.Version<1||r.Version>2||r.CityEdges==null||r.CityEdges.Length>a.Cities.Length||r.PendingSources==null||r.PendingCursor<0||r.ReassignedCells<0||r.WithdrawnCells<0||r.WithdrawnKm2<0)throw new ArgumentException("Invalid territorial service recovery.");
            foreach(var tree in new[]{r.Cursor,r.Reviewed,r.CreditKm2,r.LastReviewYear})if(tree?.Length!=33554432)throw new ArgumentException("Service recovery tree alignment.");
            if(r.Version>=2)
            {
                if(r.GovernmentCursor?.Length!=33554432||r.RetiredGovernments==null)throw new ArgumentException("Retired government service alignment.");
                for(int k=0;k<r.RetiredGovernments.Count;k++){int id=r.RetiredGovernments.At(k);if(id<0||id>=a.Polities.Length||r.RetiredGovernments.Positions[id]!=k||s.History.Polities[id].EndedYear<0||r.GovernmentCursor[id]<0)throw new ArgumentException("Invalid retired service government.");}
            }
            for(int k=0;k<r.PendingSources.Count;k++)
            {
                int key=r.PendingSources.At(k),source=HistoricalServiceRecoveryEffects.Source(key);
                if(key<0||r.PendingSources.Positions[key]!=k||source>=a.Cities.Length||source<-a.Empires.Territories.Posts.Length-1||r.Cursor[key]<0||r.Reviewed[key]<0||r.CreditKm2[key]<0||r.LastReviewYear[key]<0||r.LastReviewYear[key]>s.Years)throw new ArgumentException("Invalid pending territorial service source.");
            }
            for(int city=0;city<r.CityEdges.Length;city++)
            {
                var bag=r.CityEdges[city];if(bag==null||bag.Count<0||bag.Count>CellPatch.Length)throw new ArgumentException("Invalid city service boundary.");
                for(int k=0;k<bag.Count;k++){int cell=bag.At(k);if(cell<0||cell>=CellPatch.Length||bag.Positions[cell]!=k||a.Empires.Territories.Sources[cell]!=city)throw new ArgumentException("Invalid city service boundary reference.");}
            }
            for(int cell=0;cell<CellPatch.Length;cell++)
            {
                int source=a.Empires.Territories.Sources[cell];
                if(source>=0&&(source>=r.CityEdges.Length||ServiceDistrictBoundary(s,cell,source)&&r.CityEdges[source].Positions[cell]<0))throw new ArgumentException("Missing city service boundary.");
            }
        }
    }
}
