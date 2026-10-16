using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void TryTerritorialSeaCharter(PrehistoryState s,Route route,int culture,int elapsed)
        {
            if(!TechnicalHistory(s))return;var h=s.History;var a=h.Aggregates;var hc=h.Cultures[culture];var territory=a.Empires.Territories;
            bool ocean=HistoricalTechnologies.Has(hc,HistoricalTechnology.OceanNavigation);
            if(!ocean&&(!HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)||route.DistanceM>700000))return;
            if(LivingWorldHistory(s)&&!InstitutionalRouteAllowed(s,route.CellA,route.CellB,culture))return;
            int source=-1,polity=-1;
            int postAt=territory.PostAt[route.CellA];
            if(postAt>=0)
            {int owner=a.Resolve(territory.Posts[postAt].PolityId,h);if(owner>=0&&TerritorialNodeLive(s,owner,-2-postAt)){source=-2-postAt;polity=owner;}}
            if(polity<0)foreach(int port in NearbyLivingTowns(s,route.CellA,180).Take(12))
            {
                var town=s.Society.Settlements[port];int owner=a.Resolve(town.PolityId,h);
                if(owner<0||!DiffusionShore(town.Cell)||town.Population<500||!a.Cities[port].Supported||h.Polities[owner].CultureId!=culture)continue;
                source=port;polity=owner;break;
            }
            if(polity<0&&a.Empires.Connections?.Coasts!=null)
            {
                int owner=HistoricalCells.Polity(s,route.CellA),harborSource=a.Empires.Territories.Sources[route.CellA];
                if(owner>=0&&h.Polities[owner].CultureId==culture&&DiffusionShore(route.CellA)&&TerritorialNodeLive(s,owner,harborSource)){source=harborSource;polity=owner;}
            }
            if(polity<0)return;var p=h.Polities[polity];var government=a.Empires.Governments[polity];var finances=a.Polities[polity];
            int provisionPort=-1;long provisions=0;
            if(LivingWorldHistory(s)&&!MaritimeCharterLogistics(s,polity,route,source,out provisionPort,out provisions))return;
            int target=route.CellB,other=HistoricalCells.Polity(s,target);
            if(LivingWorldHistory(s)&&!LivingTerritorialInterest(s,polity,target))return;
            if(ColonialHistory(s)&&(RespectRecognizedClaim(s,polity,target)||World.Ecology.Habitat[target].GrowingDays<60&&!HistoricalTechnologies.Has(hc,HistoricalTechnology.CombustionEngines)))return;
            if(other>=0)
            {
                if(other!=polity&&p.Cohesion>=600&&HistoricalModernEffects.Military(hc,s.Years)>HistoricalModernEffects.Military(h.Cultures[h.Polities[other].CultureId],s.Years)*1.5)
                {
                    bool submitted=TryImperialSubmission(s,polity,other,false,elapsed,target);
                    bool expedition=!submitted&&LivingWorldHistory(s)&&TryMaritimeStateExpedition(s,polity,other,route,elapsed);
                    if(LivingWorldHistory(s)&&(submitted||expedition))s.Society.Settlements[provisionPort].FoodReserve-=provisions;
                }
                return;
            }
            if(LivingWorldHistory(s)&&a.TownAt[target]>=0)
            {
                if(TryMaritimeCouncil(s,polity,a.TownAt[target],route,elapsed))s.Society.Settlements[provisionPort].FoodReserve-=provisions;return;
            }
            if(!ocean&&World.Landmass[route.CellA]==World.Landmass[target])return;
            int postBudget=LivingWorldHistory(s)?HistoricalMaritimeIntegrationEffects.PostBudget(finances.Urban,HistoricalModernEffects.AdministrativeCapacity(hc,s.Years),HistoricalModernEffects.Communication(hc,s.Years)):128;
            if(p.Cohesion<550||finances.Receipts<=finances.ServiceCost||government.AdministrativePosts.Count>=postBudget||territory.PostAt[target]>=0||a.TownAt[target]>=0)return;
            int depth=source>=0?1:territory.Posts[-source-2].Depth+1;if(depth>7)return;
            double distance=LivingTravelKm(s,TerritorialNodeCell(s,source),target,p.CultureId);if(distance==double.MaxValue)return;
            if(a.Empires.Connections?.Coasts!=null){distance=LivingTravelKm(s,TerritorialNodeCell(s,source),route.CellA,p.CultureId)+DiffusionSeaEffort(s,route.CellA,target,p.CultureId);if(double.IsInfinity(distance)||distance>=double.MaxValue)return;}
            long annual=Math.Max(80,(long)(80+route.DistanceM/18000.0)),setup=annual*(ocean?60:100);
            if(p.Treasury<setup+annual*40||finances.Receipts-finances.ServiceCost<annual*2)return;
            int recruitment=source>=0?source:p.CapitalId;var home=s.Society.Settlements[recruitment];var catchment=a.Cities[recruitment];
            bool remoteRecruitment=ColonialHistory(s)&&finances.Remote>=2400&&(source<0||catchment.Rural<2400);
            long migrants=LivingWorldHistory(s)&&remoteRecruitment?0:Math.Min(250,remoteRecruitment?finances.Remote/300:catchment.Rural/30);
            if(migrants<80&&!(LivingWorldHistory(s)&&remoteRecruitment))return;
            double opportunity=.3+.7*D.Clamp(ProvincialPhysicalYield(s,culture,target)*3);
            if(HistoryRoll("territorial-sea-charter-"+target,polity,s.Years)>1-Math.Exp(-elapsed*(ocean?.025:.005)*opportunity))return;
            if(LivingWorldHistory(s))s.Society.Settlements[provisionPort].FoodReserve-=provisions;
            // Transfer actual households. The global account loses and regains
            // precisely the same migrants; a charter cannot create colonists.
            if(!remoteRecruitment)
            {
                ChangeAggregateCityPopulation(s,recruitment,home.Population,catchment.Suburban,catchment.Rural-migrants);
                a.TotalPopulation+=migrants;a.Polities[polity].Remote+=migrants;p.Population+=migrants;s.Society.Cultures[home.CultureId].Population+=migrants;
            }
            // In32 an office staffed from the aggregate remote service ledger
            // records zero emigrating founders. It is not250 new colonists at
            // every post, and cannot seed a new settler culture on that basis.
            // Positive founders require the real city-rural transfer above.
            int id=territory.Posts.Length;double parentEffort=source>=0?LivingTravelKm(s,s.Society.Settlements[p.CapitalId].Cell,TerritorialNodeCell(s,source),p.CultureId):territory.Posts[-source-2].MessageEffortKm;
            var post=new HistoricalAdministrativePost{Id=id,PolityId=polity,Cell=target,ParentSource=source,Depth=depth,FoundedYear=s.Years,AnnualCost=annual,MessageEffortKm=D.Q(parentEffort+distance,1000000),AutonomyMilli=ocean?700:850,Maritime=true,Founders=(int)migrants,LegDistanceM=route.DistanceM,Name=HistoricalName("overseas-province",unchecked(target+id*7919))+" Charter"};
            if(LanguageHistory(s))post.Name=LanguageName(s,p.CultureId,LanguageNameKind.Region,unchecked(target+id*7919));
            territory.Posts=territory.Posts.Concat(new[]{post}).ToArray();territory.PostAt=territory.PostAt.With(target,id);government.AdministrativePosts.Add(id);government.AdministrativePostCost+=annual;p.Treasury-=setup;
            AppendProvincialFrontier(s);
            ClaimAggregatePolity(s,target,polity);AssignTerritorialSource(s,target,-2-id);
            if(s.ArrivalYear[route.B]<0)s.ArrivalYear[route.B]=s.Years;
            int local=HistoricalCells.Culture(s,target);if(local>=0&&local!=culture)LivingCultureContact(s,culture,local);
            HistoryEvent(s,"overseas-charter",p.Name+" establishes "+post.Name+" across a sea route of approximately "+route.DistanceM/1000+" km, funded from its port network"+(LivingWorldHistory(s)&&migrants==0?" as an administrative and shipping office, not a new migrant population.":" and settled by "+migrants+" existing households.")+" Local peoples retain their own cultures.",target,culture,polity,category:ChronicleCategory.Politics);
        }
        void TransferTerritorialPost(PrehistoryState s,int cell,int polity)
        {
            if(!TechnicalHistory(s))return;var h=s.History;var a=h.Aggregates;var t=a.Empires.Territories;int id=t.PostAt[cell];if(id<0)return;var post=t.Posts[id];if(post.ClosedYear>=0)return;
            int previous=a.Resolve(post.PolityId,h);if(previous==polity)return;
            int cohort=a.PopulationCohort(post.PolityId);var old=a.Empires.Governments[cohort];old.AdministrativePosts.Remove(id);old.AdministrativePostCost=Math.Max(0,old.AdministrativePostCost-post.AnnualCost);
            if(polity<0){post.ClosedYear=s.Years;t.PostAt=t.PostAt.With(cell,-1);return;}
            int parent=TerritorialServiceSource(s,polity,cell);
            if(parent== -1||parent<0&&t.Posts[-parent-2].Depth>=7){post.ClosedYear=s.Years;t.PostAt=t.PostAt.With(cell,-1);return;}
            post.PolityId=polity;post.ParentSource=parent;post.Depth=parent>=0?1:t.Posts[-parent-2].Depth+1;post.Maritime=false;post.LegDistanceM=0;
            post.MessageEffortKm=D.Q(LivingTravelKm(s,TerritorialNodeCell(s,parent),cell,h.Polities[polity].CultureId)+(parent<0?t.Posts[-parent-2].MessageEffortKm:0),1000000);
            var next=a.Empires.Governments[polity];next.AdministrativePosts.Add(id);next.AdministrativePostCost+=post.AnnualCost;
            AssignTerritorialSource(s,cell,-2-id);
        }
    }
}
