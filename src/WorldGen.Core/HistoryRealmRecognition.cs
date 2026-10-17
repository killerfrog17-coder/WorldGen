using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        static long RealmTitleUpkeep(long area)=>40+Math.Max(0,area)/2000;
        int RealmRecognitionPartner(PrehistoryState s,int polity)
        {
            var h=s.History;var a=h.Aggregates;var relations=h.International.Accounts[polity].Relations;if(relations.Count==0)return -1;
            int offset=(int)(HistoryRoll("realm-known-recognizer-1",polity,s.Years)*relations.Count);
            for(int k=0;k<Math.Min(6,relations.Count);k++)
            {
                var relation=h.Relations[relations.At((offset+k)%relations.Count)];int peer=InternationalContinuingParty(s,relation.A==polity?relation.B:relation.A);
                if(peer<0||peer==polity||h.Polities[peer].EndedYear>=0||HistoricalImperialHierarchy.Sovereign(s,peer)==HistoricalImperialHierarchy.Sovereign(s,polity))continue;
                if(relation.WarSince<0&&relation.Trade&&relation.Trust>=20)return peer;
            }
            return -1;
        }
        void ChangeRealmTitleAccount(PrehistoryState s,int polity,long area,int direction)
        {
            if(!RealmHistory(s)||polity<0)return;var record=s.History.Aggregates.Empires.Connections.Realms.Governments[polity];
            record.RecognizedAreaKm2=Math.Max(0,record.RecognizedAreaKm2+direction*area);
            record.RecognitionUpkeep=Math.Max(0,record.RecognitionUpkeep+direction*RealmTitleUpkeep(area));
        }
        void TryRecognizeRealmDistrict(PrehistoryState s,int polity,int partner)
        {
            var h=s.History;var a=h.Aggregates;var c=a.Empires.Connections;var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];var finances=a.Polities[polity];
            if(p.Cohesion<450||p.Treasury<3000||finances.Receipts<=finances.ServiceCost)return;
            int source=s.Society.Settlements[p.CapitalId].Cell;bool established=s.Years-HistoricalRealmLifecycles.Founded(s,polity)>=25;
            var posts=a.Empires.Governments[polity].AdministrativePosts;
            if(posts.Count>0&&HistoryRoll("realm-treaty-office",polity,s.Years)<.7)
            {
                int id=posts.At((int)(HistoryRoll("realm-treaty-office-index",polity,s.Years)*posts.Count));var post=a.Empires.Territories.Posts[id];
                source=post.Cell;established=post.ClosedYear<0&&s.Years-post.FoundedYear>=10&&TerritorialNodeLive(s,polity,-2-id);
            }
            if(!established||HistoricalCells.Polity(s,source)!=polity)return;int home=CellPatch[source];if(home<0)return;
            double roads=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Roads,s.Years),rail=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Railways,s.Years),engines=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years);
            bool adjoining=roads>=.2&&(HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years)>=.2||rail>=.2);
            double reach=HistoricalProvincialEffects.LocalServiceKm(hc,s.Years,h.Dynamics.States[polity].PublicWorks)*(1+2*roads+4*rail+2*engines);
            int[] routes=a.Regions[home].Routes;int offset=routes.Length==0?0:(int)(HistoryRoll("realm-treaty-district",polity,s.Years)*routes.Length);
            int peerCell=s.Society.Settlements[h.Polities[partner].CapitalId].Cell;var peer=h.Cultures[h.Polities[partner].CultureId];
            for(int trial=0;trial<1+(adjoining?Math.Min(8,routes.Length):0);trial++)
            {
                int region=home;
                if(trial>0){var route=Routes[routes[(offset+trial-1)%routes.Length]];region=route.A==home?route.B:route.A;}
                int existing=c.TitleAt[region];if(existing>=0&&a.Resolve(c.Titles[existing].Holder,h)>=0)continue;
                int target=region==home?source:a.Regions[region].BestCell;
                if(World.Ecology.Habitat[target].GrowingDays<30||World.Hydrology.OceanConnected[target])continue;
                int local=HistoricalCells.Polity(s,target);if(local>=0&&HistoricalImperialHierarchy.Sovereign(s,local)!=HistoricalImperialHierarchy.Sovereign(s,polity))continue;
                if(region!=home)
                {
                    if(World.Landmass[source]<0||World.Landmass[source]!=World.Landmass[target]||LivingTravelKm(s,source,target,p.CultureId)>reach||!InstitutionalRouteAllowed(s,source,target,p.CultureId))continue;
                }
                bool informed=World.Landmass[peerCell]>=0&&World.Landmass[peerCell]==World.Landmass[target]&&LivingTravelKm(s,peerCell,target,h.Polities[partner].CultureId)<=Math.Max(300,a.Polities[partner].ReachKm*3.0)
                    ||c.Cultures[h.Polities[partner].CultureId].Charts.Any(x=>x.Region==region)
                    ||GeographicHistory(s)&&c.Geography.Governments[partner].Grade[home]>=2&&HistoricalModernEffects.Adoption(peer,HistoricalTechnology.Telegraph,s.Years)>=.2;
                if(!informed)continue;
                long upkeep=RealmTitleUpkeep(c.RegionAreas[region]),cost=Math.Max(1000,upkeep*20);
                if(p.Treasury<cost+upkeep*20||finances.Receipts-finances.ServiceCost<upkeep*2)continue;
                int titleId=existing>=0?existing:c.Titles.Length;HistoricalRecognizedTitle title;
                if(existing>=0)
                {
                    title=c.Titles[existing];int old=a.PopulationCohort(title.Holder);c.Governments[old].Titles.Remove(titleId);ChangeRealmTitleAccount(s,old,title.AreaKm2,-1);
                    title.PreviousHolder=title.Holder;title.Holder=polity;title.Recognizer=partner;title.LastTransferYear=s.Years;title.LastPrice=0;
                }
                else
                {
                    title=new HistoricalRecognizedTitle{Id=titleId,Region=region,Holder=polity,Recognizer=partner,EstablishedYear=s.Years,LastTransferYear=s.Years,AreaKm2=c.RegionAreas[region]};
                    c.Titles=c.Titles.Concat(new[]{title}).ToArray();c.TitleAt=c.TitleAt.With(region,titleId);
                }
                title.Basis=region==home?"Established local administration and an informed bilateral recognition agreement":"A funded neighboring administration, physically reachable hinterland and an informed territorial agreement; local communities retain their institutions";
                c.Governments[polity].Titles.Add(titleId);ChangeRealmTitleAccount(s,polity,title.AreaKm2,1);p.Treasury-=cost;
                h.Polities[partner].Treasury=Math.Min(1000000000,h.Polities[partner].Treasury+cost);
                HistoryEvent(s,"territorial-recognition",h.Polities[partner].Name+" recognizes "+p.Name+"'s territorial agreement covering "+ColonialRegionName(region)+". Its funded service network supports a claim to external sovereignty, not a census of settlers or uniform occupation.",target,p.CultureId,polity,partner,category:ChronicleCategory.Politics);
                return;
            }
        }
    }
}
