using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // Optional read-only diagnostic hook; never participates in decisions.
        public Action<int,int,string> RealmRecognitionObserver;
        static long RealmTitleUpkeep(long area)=>40+Math.Max(0,area)/2000;
        int RealmRecognitionPartner(PrehistoryState s,int polity)
        {
            var h=s.History;var a=h.Aggregates;var relations=h.International.Accounts[polity].Relations;if(relations.Count==0)return -1;
            int offset=(int)(HistoryRoll("realm-known-recognizer-1",polity,s.Years)*relations.Count);
            for(int k=0;k<Math.Min(6,relations.Count);k++)
            {
                var relation=h.Relations[relations.At((offset+k)%relations.Count)];int peer=InternationalContinuingParty(s,relation.A==polity?relation.B:relation.A);
                if(peer<0||peer==polity||h.Polities[peer].EndedYear>=0)continue;
                // A sovereign can grant a locally administered colonial charter
                // to an existing constituent court. Sibling/self assertions do
                // not thereby become external recognition.
                if(HistoricalImperialHierarchy.Sovereign(s,peer)==HistoricalImperialHierarchy.Sovereign(s,polity)&&HistoricalImperialHierarchy.Patron(s,polity)!=peer)continue;
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
        bool FundRealmRecognition(PrehistoryState s,int polity,long required)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];if(p.Treasury>=required)return true;
            int sponsor=a.Resolve(a.Empires.Connections.Governments[polity].Sponsor,h);
            if(sponsor<0||HistoricalImperialHierarchy.Patron(s,polity)!=sponsor)return false;
            var patron=h.Polities[sponsor];var account=a.Polities[sponsor];long grant=required-p.Treasury;
            if(grant<=0||patron.Cohesion<500||patron.Exhaustion>=400||account.Receipts<=account.ServiceCost||patron.Treasury<grant*4||required>1000000000)return false;
            patron.Treasury-=grant;p.Treasury+=grant;
            HistoryEvent(s,"colonial-administration-grant",patron.Name+" contributes "+grant+" treasury units to the regional administration of "+p.Name+". The grant pays real costs; it creates neither inhabitants nor farmland.",s.Society.Settlements[p.CapitalId].Cell,p.CultureId,polity,sponsor,category:ChronicleCategory.Politics);
            return true;
        }
        void TryRecognizeRealmDistrict(PrehistoryState s,int polity,int partner)
        {
            var h=s.History;var a=h.Aggregates;var c=a.Empires.Connections;var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];var finances=a.Polities[polity];
            // A modern ministry does not lose its ability to conclude a funded
            // boundary agreement whenever a cabinet has middling cohesion.
            // Weak premodern courts still need substantially stronger unity.
            if(p.Cohesion<450-200*RealmBureaucracy(s,polity)){RealmRecognitionObserver?.Invoke(polity,-1,"court-cohesion");return;}
            int source=s.Society.Settlements[p.CapitalId].Cell;bool established=s.Years-HistoricalRealmLifecycles.Founded(s,polity)>=25;
            var posts=a.Empires.Governments[polity].AdministrativePosts;
            if(posts.Count>0&&HistoryRoll("realm-treaty-office",polity,s.Years)<.7)
            {
                var pending=c.PortSettlements?.Governments[polity].Pending;var offices=pending!=null&&pending.Count>0&&HistoryRoll("realm-treaty-overseas-office",polity,s.Years)<.6?pending:posts;
                int id=offices.At((int)(HistoryRoll("realm-treaty-office-index",polity,s.Years)*offices.Count));var post=a.Empires.Territories.Posts[id];
                source=post.Cell;established=post.ClosedYear<0&&s.Years-post.FoundedYear>=10&&TerritorialNodeLive(s,polity,-2-id);
            }
            if(!established||HistoricalCells.Polity(s,source)!=polity){RealmRecognitionObserver?.Invoke(polity,-1,"no-live-center");return;}int home=CellPatch[source];if(home<0)return;
            double roads=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Roads,s.Years),rail=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Railways,s.Years),engines=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years);
            bool adjoining=roads>=.2&&(HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years)>=.2||rail>=.2);
            double reach=HistoricalProvincialEffects.LocalServiceKm(hc,s.Years,h.Dynamics.States[polity].PublicWorks)*(1+2*roads+4*rail+2*engines);
            int district=home,cumulativeM=0;var documents=c.Governments[polity].Titles;var record=c.Realms.Governments[polity];var serviceRoutes=c.Realms.ClaimRoutes;
            // A territorial agreement can address the next contiguous district,
            // not only the first ring around a beach. Its real service source
            // remains the capital/office above: paper cannot extend physical reach.
            for(int review=0;adjoining&&review<Math.Min(3,documents.Count);review++)
            {
                int at=record.RecognitionCursor%documents.Count,titleId=documents.At(at);record.RecognitionCursor=(at+1)%documents.Count;
                var document=c.Titles[titleId];int anchor=a.Regions[document.Region].BestCell;
                if(serviceRoutes!=null)
                {
                    int origin=serviceRoutes.Sources[document.Region],meters=serviceRoutes.EffortM[document.Region];
                    if(origin<0||meters<0||meters>reach*1000||a.Resolve(document.Holder,h)!=polity||!RealmClaimSourceLive(s,polity,origin))continue;
                    source=origin;home=CellPatch[source];district=document.Region;cumulativeM=meters;break;
                }
                if(a.Resolve(document.Holder,h)==polity&&World.Landmass[source]>=0&&World.Landmass[source]==World.Landmass[anchor]&&LivingTravelKm(s,source,anchor,p.CultureId)<=reach)
                {district=document.Region;break;}
            }
            int[] routes=realmLandRoutes[district];int offset=routes.Length==0?0:(int)(HistoryRoll("realm-treaty-district",polity,s.Years)*routes.Length);
            int peerCell=s.Society.Settlements[h.Polities[partner].CapitalId].Cell;var peer=h.Cultures[h.Polities[partner].CultureId];
            int signed=0,lastTarget=source,lastRegion=home;bool forwardOffice=false;
            int documentsPerAgreement=Math.Min(8,1+(int)(2*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years)+3*rail+2*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Telegraph,s.Years)+2*engines));
            long priorObligations=record.RecognitionUpkeep;
            for(int trial=0;trial<1+(adjoining?Math.Min(8,routes.Length):0);trial++)
            {
                int region=home;Route connection=null;
                if(trial>0){connection=Routes[routes[(offset+trial-1)%routes.Length]];region=connection.A==district?connection.B:connection.A;}
                int existing=c.TitleAt[region];
                if(existing>=0&&a.Resolve(c.Titles[existing].Holder,h)>=0)
                {if(region==home&&a.Resolve(c.Titles[existing].Holder,h)==polity)RecordRealmClaimRoute(s,region,source,0);continue;}
                int target=region==home?source:a.Regions[region].BestCell;
                if(World.Ecology.Habitat[target].Biome==Biome.Ice||World.Ecology.Habitat[target].GrowingDays<30&&engines<.2||World.Hydrology.OceanConnected[target]){RealmRecognitionObserver?.Invoke(polity,region,"inhospitable");continue;}
                int local=HistoricalCells.Polity(s,target);if(local>=0&&HistoricalImperialHierarchy.Sovereign(s,local)!=HistoricalImperialHierarchy.Sovereign(s,polity)){RealmRecognitionObserver?.Invoke(polity,region,"foreign-control");continue;}
                int proposedM=0;
                if(region!=home)
                {
                    if(World.Landmass[source]<0||World.Landmass[source]!=World.Landmass[target]){RealmRecognitionObserver?.Invoke(polity,region,"different-landmass");continue;}
                    if(serviceRoutes!=null)
                    {
                        if(connection==null||connection.Sea){RealmRecognitionObserver?.Invoke(polity,region,"no-land-corridor");continue;}
                        int anchor=district==home?source:a.Regions[district].BestCell;
                        double straight=D.Angle(World.Surface.Cells[anchor].Position,World.Surface.Cells[target].Position)*World.Recipe.RadiusKm*1000;
                        double terrain=1+(World.Ecology.Habitat[anchor].ReliefM+World.Ecology.Habitat[target].ReliefM)/4000.0;
                        double meters=cumulativeM+Math.Max(connection.DistanceM,straight)*terrain;
                        if(meters>reach*1000||meters>int.MaxValue){RealmRecognitionObserver?.Invoke(polity,region,"service-reach");continue;}proposedM=(int)Math.Round(meters);
                    }
                    else if(LivingTravelKm(s,source,target,p.CultureId)>reach||!InstitutionalRouteAllowed(s,source,target,p.CultureId))continue;
                }
                bool informed=World.Landmass[peerCell]>=0&&World.Landmass[peerCell]==World.Landmass[target]&&LivingTravelKm(s,peerCell,target,h.Polities[partner].CultureId)<=Math.Max(300,a.Polities[partner].ReachKm*3.0)
                    ||c.Cultures[h.Polities[partner].CultureId].Charts.Any(x=>x.Region==region)
                    ||GeographicHistory(s)&&c.Geography.Governments[partner].Grade[home]>=2&&HistoricalModernEffects.Adoption(peer,HistoricalTechnology.Telegraph,s.Years)>=.2;
                if(!informed){RealmRecognitionObserver?.Invoke(polity,region,"uninformed-recognizer");continue;}
                long upkeep=RealmTitleUpkeep(c.RegionAreas[region]),cost=Math.Max(1000,upkeep*20);
                long annualDeficit=Math.Max(0,finances.ServiceCost+(record.RecognitionUpkeep-priorObligations)+upkeep-finances.Receipts);
                long required=Math.Max(cost+upkeep*20,cost+annualDeficit*50);
                if(!FundRealmRecognition(s,polity,required)){RealmRecognitionObserver?.Invoke(polity,region,"administration-funding");continue;}
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
                RecordRealmClaimRoute(s,region,source,proposedM);
                if(!forwardOffice&&region!=home)forwardOffice=FundRealmForwardAdministration(s,polity,region,source,proposedM);
                h.Polities[partner].Treasury=Math.Min(1000000000,h.Polities[partner].Treasury+cost);
                RealmRecognitionObserver?.Invoke(polity,region,"recognized");signed++;lastTarget=target;lastRegion=region;if(signed>=documentsPerAgreement)break;
            }
            if(signed>0)HistoryEvent(s,"territorial-recognition",h.Polities[partner].Name+" recognizes "+p.Name+"'s territorial agreement covering "+(signed==1?ColonialRegionName(lastRegion):signed+" adjoining districts")+". Its funded service network supports a claim to external sovereignty, not a census of settlers or uniform occupation.",lastTarget,p.CultureId,polity,partner,category:ChronicleCategory.Politics);
        }
    }
}
