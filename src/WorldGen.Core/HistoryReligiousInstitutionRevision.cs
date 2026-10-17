using System;

namespace WorldGen.Core
{
    public enum ReligiousVocation { SacredPlaces, HouseholdTradition, TravelingTeaching }
    public static class HistoricalReligiousInstitutionEffects
    {
        // Scenario priors for institutional choices, not quotas for the world.
        public static ReligiousVocation Vocation(double roll,ReligiousReach recorded)
        {
            if(recorded==ReligiousReach.Missionary)return ReligiousVocation.TravelingTeaching;
            if(recorded==ReligiousReach.Portable)return roll>=.82?ReligiousVocation.TravelingTeaching:ReligiousVocation.HouseholdTradition;
            return roll<.45?ReligiousVocation.SacredPlaces:roll<.82?ReligiousVocation.HouseholdTradition:ReligiousVocation.TravelingTeaching;
        }
        public static bool Portable(ReligiousVocation vocation,int support,bool funded,bool actualContact)
            =>vocation!=ReligiousVocation.SacredPlaces&&support>=400&&funded&&actualContact;
        public static double PolicyReformChance(int support,int age,int representation,int former,int exhaustion,int legitimacy)
        {
            double constitutional=former==2?D.Clamp((representation-500)/400.0):0;
            double crisis=Math.Max(D.Clamp(exhaustion/1000.0),D.Clamp((400-legitimacy)/400.0));
            return D.Q(Math.Min(.45,(.025+.12*(1-D.Clamp(support/1000.0))+.20*constitutional+.10*crisis)*(.6+.4*(1-D.Clamp(age/300.0)))),1000000000);
        }
        public static bool OrdinaryCarrier(bool war,bool disrupted,int sourceStability,int targetStability,bool foreign,double sourceReserve)
            =>!war&&!disrupted&&sourceReserve>=.25&&(!foreign||sourceStability>400&&targetStability>400);
    }
    public sealed partial class Prehistory
    {
        static bool ReligiousInstitutionHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Religion?.Version>=2;
        void UpgradeReligiousNetworkRules(PrehistoryState s)
        {InitializeReligiousNetworks(s);s.History.Aggregates.Empires.Connections.Religion.Version=2;}
        ReligiousVocation ReligiousInstitutionVocation(PrehistoryState s,int religion)
            =>HistoricalReligiousInstitutionEffects.Vocation(HistoryRoll("religion-institution-vocation-2",religion,0),s.History.Religions[religion].Reach);
        bool ReligiousTownCarrierAllowed(PrehistoryState s,int source,int target)
        {
            var h=s.History;var a=h.Aggregates;var from=s.Society.Settlements[source];var to=s.Society.Settlements[target];
            int first=a.Resolve(from.PolityId,h),second=a.Resolve(to.PolityId,h);bool foreign=first>=0&&second>=0&&first!=second;
            bool war=foreign&&aggregateRelations.TryGetValue((Math.Min(first,second),Math.Max(first,second)),out int relation)&&h.Relations[relation].WarSince>=0;
            bool disrupted=a.Living.Cities[source].DisruptedUntil>s.Years||a.Living.Cities[target].DisruptedUntil>s.Years;
            if(!HistoricalReligiousInstitutionEffects.OrdinaryCarrier(war,disrupted,from.Stability,to.Stability,foreign,from.FoodReserve/Math.Max(1.0,from.Population)))return false;
            var hc=h.Cultures[from.CultureId];double km=D.Angle(World.Surface.Cells[from.Cell].Position,World.Surface.Cells[to.Cell].Position)*World.Recipe.RadiusKm;
            // Ordinary household/merchant movement follows the same presently
            // usable distances as economic town exchange, not an old graph edge.
            return km<200*HistoricalModernEffects.LandTransport(hc,s.Years)||HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)&&km*1000<=HistoricalModernVoyageEffects.RangeM(hc,s.Years,ColonialHistory(s))&&AggregateSeaLane(from.Cell,to.Cell);
        }
        void EnsureEndowedReligiousPolicy(PrehistoryState s,int polity)
        {
            var h=s.History;var r=h.Aggregates.Empires.Connections.Religion;int key=polity*4,leader=h.Dynamics.States[polity].LeaderId,former=r.Policies[key+1];
            if(r.Policies[key]==leader&&former>=0)return;
            var person=leader>=0?h.Dynamics.Leaders[leader]:null;var p=h.Polities[polity];var constitution=h.Aggregates.Communities.Governments[polity];
            double disposition=HistoryRoll("religion-institutional-policy-2",polity,0);
            double openness=.20+.45*constitution.RepresentationMilli/1000.0+.15*(person?.Diplomacy??500)/1000.0;
            int proposed=disposition<openness?0:disposition>.88&&constitution.RepresentationMilli<550&&(person?.Ambition??500)>550?2:1;
            r.Policies=r.Policies.With(key,leader);
            if(former==proposed)return;
            int capital=s.Society.Settlements[p.CapitalId].Cell,support=Math.Max(0,r.Read(capital,FaithInstitution));
            int since=Math.Max(r.SinceYear,r.Policies[key+2]);
            double chance=HistoricalReligiousInstitutionEffects.PolicyReformChance(support,s.Years-since,constitution.RepresentationMilli,former,p.Exhaustion,p.Legitimacy);
            if(former>=0&&HistoryRoll("religion-endowed-policy-reform-2",polity,person?.StartedYear??s.Years)>=chance)return;
            r.Policies=r.Policies.With(key+1,proposed).With(key+2,s.Years);
            if(former>=0)HistoryEvent(s,"religious-policy",p.Name+" revises its established religious settlement to "+ReligiousPolicyName(proposed).ToLowerInvariant()+". Local endowments and household rites continue through the change of ruler.",capital,p.CultureId,polity,religion:p.ReligionId);
        }
    }
}
