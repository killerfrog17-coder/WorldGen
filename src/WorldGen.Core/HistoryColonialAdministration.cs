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
            if(polity<0)return;var p=h.Polities[polity];var government=a.Empires.Governments[polity];var finances=a.Polities[polity];
            int target=route.CellB,other=HistoricalCells.Polity(s,target);
            if(other>=0)
            {
                if(other!=polity&&p.Cohesion>=600&&HistoricalModernEffects.Military(hc,s.Years)>HistoricalModernEffects.Military(h.Cultures[h.Polities[other].CultureId],s.Years)*1.5)
                    TryImperialSubmission(s,polity,other,false,elapsed,target);
                return;
            }
            if(!ocean&&World.Landmass[route.CellA]==World.Landmass[target])return;
            if(p.Cohesion<550||finances.Receipts<=finances.ServiceCost||government.AdministrativePosts.Count>=128||territory.PostAt[target]>=0||a.TownAt[target]>=0)return;
            int depth=source>=0?1:territory.Posts[-source-2].Depth+1;if(depth>7)return;
            double distance=LivingTravelKm(s,TerritorialNodeCell(s,source),target,p.CultureId);if(distance==double.MaxValue)return;
            long annual=Math.Max(80,(long)(80+route.DistanceM/18000.0)),setup=annual*(ocean?60:100);
            if(p.Treasury<setup+annual*40||finances.Receipts-finances.ServiceCost<annual*2)return;
            int recruitment=source>=0?source:p.CapitalId;var home=s.Society.Settlements[recruitment];var catchment=a.Cities[recruitment];
            long migrants=Math.Min(250,catchment.Rural/30);if(migrants<80)return;
            double opportunity=.3+.7*D.Clamp(ProvincialPhysicalYield(s,culture,target)*3);
            if(HistoryRoll("territorial-sea-charter-"+target,polity,s.Years)>1-Math.Exp(-elapsed*(ocean?.025:.005)*opportunity))return;
            // Transfer actual households. The global account loses and regains
            // precisely the same migrants; a charter cannot create colonists.
            ChangeAggregateCityPopulation(s,recruitment,home.Population,catchment.Suburban,catchment.Rural-migrants);
            a.TotalPopulation+=migrants;a.Polities[polity].Remote+=migrants;p.Population+=migrants;s.Society.Cultures[home.CultureId].Population+=migrants;
            int id=territory.Posts.Length;double parentEffort=source>=0?LivingTravelKm(s,s.Society.Settlements[p.CapitalId].Cell,TerritorialNodeCell(s,source),p.CultureId):territory.Posts[-source-2].MessageEffortKm;
            var post=new HistoricalAdministrativePost{Id=id,PolityId=polity,Cell=target,ParentSource=source,Depth=depth,FoundedYear=s.Years,AnnualCost=annual,MessageEffortKm=D.Q(parentEffort+distance,1000000),AutonomyMilli=ocean?700:850,Maritime=true,Founders=(int)migrants,LegDistanceM=route.DistanceM,Name=HistoricalName("overseas-province",unchecked(target+id*7919))+" Charter"};
            territory.Posts=territory.Posts.Concat(new[]{post}).ToArray();territory.PostAt=territory.PostAt.With(target,id);government.AdministrativePosts.Add(id);government.AdministrativePostCost+=annual;p.Treasury-=setup;
            ClaimAggregatePolity(s,target,polity);AssignTerritorialSource(s,target,-2-id);
            if(s.ArrivalYear[route.B]<0)s.ArrivalYear[route.B]=s.Years;
            int local=HistoricalCells.Culture(s,target);if(local>=0&&local!=culture)LivingCultureContact(s,culture,local);
            HistoryEvent(s,"overseas-charter",p.Name+" establishes "+post.Name+" across a sea route of approximately "+route.DistanceM/1000+" km, funded from its port network and settled by "+migrants+" existing households. Local peoples retain their own cultures.",target,culture,polity,category:ChronicleCategory.Politics);
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
