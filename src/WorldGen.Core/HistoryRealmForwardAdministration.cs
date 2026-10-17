using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        bool FundRealmForwardAdministration(PrehistoryState s,int polity,int region,int origin,int effortM)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];var t=a.Empires.Territories;var g=a.Empires.Governments[polity];
            if(RealmBureaucracy(s,polity)<.25||h.Dynamics.States[polity].PublicWorks<200||HistoricalCells.Polity(s,origin)!=polity)return false;
            int target=a.Regions[region].BestCell;if(target==origin||t.PostAt[target]>=0||a.TownAt[target]>=0)return false;
            int owner=HistoricalCells.Polity(s,target);if(owner>=0&&owner!=polity)return false;
            int parent=t.Sources[origin];if(!TerritorialNodeLive(s,polity,parent))return false;
            int depth=parent>=0?0:t.Posts[-parent-2].Depth;if(depth>=7)return false;
            double radius=TerritorialServiceRadius(s,polity,target),distance=LivingTravelKm(s,origin,target,p.CultureId);
            if(effortM<radius*550||distance>radius*1.15||!InstitutionalRouteAllowed(s,origin,target,p.CultureId))return false;
            var account=a.Polities[polity];int maximum=HistoricalMaritimeIntegrationEffects.PostBudget(account.Urban,HistoricalModernEffects.AdministrativeCapacity(hc,s.Years),HistoricalModernEffects.Communication(hc,s.Years));
            if(g.AdministrativePosts.Count>=maximum)return false;
            double parentEffort=parent>=0?0:t.Posts[-parent-2].MessageEffortKm;
            long annual=Math.Max(60,(long)(60*(1+(parentEffort+distance)/(400*Math.Sqrt(HistoricalModernEffects.Communication(hc,s.Years)*HistoricalModernEffects.LandTransport(hc,s.Years)))))),setup=annual*30;
            if(p.Treasury<setup+annual*50||account.Receipts-account.ServiceCost<annual*2)return false;
            // A paid real administrative node, not a settlement or free global
            // sovereignty paint. This advances one documented land corridor.
            int id=t.Posts.Length;p.Treasury-=setup;
            if(owner<0)ClaimAggregatePolity(s,target,polity);
            var post=new HistoricalAdministrativePost{Id=id,PolityId=polity,Cell=target,ParentSource=parent,Depth=depth+1,FoundedYear=s.Years,AnnualCost=annual,
                MessageEffortKm=D.Q(parentEffort+distance,1000000),AutonomyMilli=(int)(700/Math.Sqrt(HistoricalModernEffects.Communication(hc,s.Years))),
                Name=LanguageName(s,p.CultureId,LanguageNameKind.Region,unchecked(target+id*7919))};
            t.Posts=t.Posts.Concat(new[]{post}).ToArray();t.PostAt=t.PostAt.With(target,id);g.AdministrativePosts.Add(id);g.AdministrativePostCost+=annual;
            AppendProvincialFrontier(s);AssignTerritorialSource(s,target,-2-id);RecordRealmClaimRoute(s,region,target,0);
            HistoryEvent(s,"administrative-post",p.Name+" funds a regional office at "+post.Name+" along its recognized land corridor. Its treasury pays construction and ongoing service; this creates no town or additional inhabitants.",target,p.CultureId,polity,category:ChronicleCategory.Politics);
            return true;
        }
    }
}
