using System;
namespace WorldGen.Core
{
    public static class HistoricalTerritorialContinuityEffects
    {
        // Dissent and requests for local government are not automatically a
        // demand for a new international border. These are authored game
        // priors, not an empirical coefficient or a universal state-size cap.
        public static double SecessionPressure(double demands,double bureaucracy,int inclusion,int representation,long receipts,long serviceCost,double distance,bool actualService)
        {
            if(!actualService||bureaucracy<=0||receipts<=0||double.IsNaN(distance)||double.IsInfinity(distance))return demands;
            double delivered=1/(1+Math.Pow(Math.Max(0,distance-1),2));
            double financed=D.Clamp(receipts/(double)Math.Max(1,serviceCost));
            double continuity=D.Clamp(bureaucracy)*D.Clamp(inclusion/750.0)*(.55+.45*D.Clamp(representation/1000.0))*financed*delivered;
            return Math.Max(0,demands-.45*continuity);
        }
    }
    public sealed partial class Prehistory
    {
        double CommunityTerritorialSecessionPressure(PrehistoryState s,int owner,int city,double demands,double distance)
        {
            if((s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version??0)<23)return demands;
            var h=s.History;var a=h.Aggregates;var p=h.Polities[owner];var t=s.Society.Settlements[city];
            var account=a.Polities[owner];var policy=a.Communities.Governments[owner];
            double bureaucracy=RealmBureaucracy(s,owner);if(bureaucracy<=0)return demands;
            int source=a.Empires.Territories.Sources[t.Cell];
            bool served=TerritorialNodeLive(s,owner,source)&&InstitutionalRouteAllowed(s,s.Society.Settlements[p.CapitalId].Cell,t.Cell,p.CultureId);
            return HistoricalTerritorialContinuityEffects.SecessionPressure(demands,bureaucracy,a.Communities.Cities[city].InclusionMilli,policy.RepresentationMilli,account.Receipts,account.ServiceCost,distance,served);
        }
        bool JoinRecentCommunitySuccessor(PrehistoryState s,int owner,int city)
        {
            var h=s.History;var a=h.Aggregates;var town=s.Society.Settlements[city];int rival=-1;double best=double.MaxValue;
            // Reuse the crisis path's bounded physical-neighbor convention:
            // one regional rebellion need not mint a separate flag per city.
            foreach(int peer in a.Cities[city].Neighbors)
            {
                var candidate=s.Society.Settlements[peer];if(candidate.Population<=0||a.Living.Cities[peer].AbandonedYear>=0)continue;int state=a.Resolve(candidate.PolityId,h);
                if(state<0||state==owner||h.Polities[state].ParentId!=owner||h.Polities[state].CultureId!=town.CultureId||s.Years-h.Polities[state].FoundedYear>50)continue;
                double km=LivingTravelKm(s,candidate.Cell,town.Cell,town.CultureId);
                if(km<best&&km<Math.Max(180,a.Polities[state].ReachKm*2.0)&&InstitutionalRouteAllowed(s,candidate.Cell,town.Cell,town.CultureId)){best=km;rival=state;}
            }
            if(rival<0)return false;
            ClaimAggregatePolity(s,town.Cell,rival);JoinRealmClaimantGovernment(s,owner,city,rival);
            HistoryEvent(s,"regional-realignment",town.Name+" joins the neighboring successor government of "+h.Polities[rival].Name+" during the regional contest with "+h.Polities[owner].Name+".",town.Cell,town.CultureId,rival,owner,city,category:ChronicleCategory.Politics);
            return true;
        }
    }
}
