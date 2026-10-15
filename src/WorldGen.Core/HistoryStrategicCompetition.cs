using System;
using System.Linq;
namespace WorldGen.Core
{
    public static class HistoricalStrategicEffects
    {
        public static double DecadalWarChance(int ambition,int military,int cohesion,int exhaustion,long treasury,int army,double opponentPower,double frontierPressure,bool restoration,bool trading,int trust)
        {
            if(army<=30||treasury<army||exhaustion>=650||cohesion<350)return 0;
            double strength=army*(.5+military/700.0),opportunity=D.Clamp((strength/Math.Max(1,opponentPower)-.7)/1.8);
            double readiness=D.Clamp(cohesion/700.0)*D.Clamp(1-exhaustion/750.0)*D.Clamp(treasury/(double)Math.Max(1,army*4L));
            double desire=.006+.13*Math.Pow(D.Clamp(ambition/1000.0),2)*opportunity+.035*D.Clamp(frontierPressure)+(restoration?.065:0);
            double restraint=(trading?.5:1)*(1-.45*D.Clamp((trust+100)/200.0));
            return D.Q(D.Clamp(desire*readiness*restraint,0,.25),1000000000);
        }
    }
    public sealed partial class Prehistory
    {
        bool RelatedCourts(HistoricalPolity a,HistoricalPolity b)
            =>a.ParentId==b.Id||b.ParentId==a.Id||a.ParentId>=0&&a.ParentId==b.ParentId;
        double DirectedHistoricalWarChance(PrehistoryState s,HistoricalPolity actor,HistoricalPolity target,HistoricalRelation relation)
        {
            var leader=CivicLeader(s,actor.Id);var other=CivicLeader(s,target.Id);
            int fronts=s.History.Relations.Count(r=>r.WarSince>=0&&(r.A==actor.Id||r.B==actor.Id));if(fronts>=2)return 0;
            double defense=target.Army*(.5+other.Military/700.0);
            foreach(var pact in s.History.Relations.Where(r=>r.Alliance&&(r.A==target.Id||r.B==target.Id)))
            {int ally=pact.A==target.Id?pact.B:pact.A;if(ally!=actor.Id&&s.History.Polities[ally].EndedYear<0)defense+=s.History.Polities[ally].Army*.25;}
            // Existing kin claims, shortages and military opportunities are
            // different interests; trading treaties and allies remain restraints.
            double shortage=s.Society.Settlements.Where(t=>t.PolityId==actor.Id&&t.Population>=100).Select(t=>D.Clamp(1-t.FoodReserve/(double)Math.Max(1,t.Population))).DefaultIfEmpty(0).Average();
            double pressure=shortage+(actor.Treasury<actor.Army*2L?.25:0);
            if(relation.Alliance&&(leader.Ambition<800||actor.Army*(.5+leader.Military/700.0)<=defense*1.4))return 0;
            double chance=HistoricalStrategicEffects.DecadalWarChance(leader.Ambition,leader.Military,actor.Cohesion,actor.Exhaustion,actor.Treasury,actor.Army,defense,pressure,RelatedCourts(actor,target),relation.Trade,relation.Trust);
            return D.Q(chance/(1+fronts)*(relation.Alliance?.5:1),1000000000);
        }
        string HistoricalCampaignContext(PrehistoryState s,HistoricalPolity actor,HistoricalPolity target)
        {
            var leader=CivicLeader(s,actor.Id);var other=CivicLeader(s,target.Id);
            if(RelatedCourts(actor,target))return "The two courts descend from a divided political realm; their rulers contest its reunification.";
            bool shortage=s.Society.Settlements.Any(t=>t.PolityId==actor.Id&&t.Population>=100&&t.FoodReserve<t.Population/2);
            if(shortage)return "Depleted reserves in its settlements intensify the court's interest in neighboring resources and tribute. Its existing funds and forces permit a campaign, but victory is uncertain.";
            if(leader.Ambition>=750&&actor.Army*(.5+leader.Military/700.0)>target.Army*(.5+other.Military/700.0)*1.4)
                return leader.Name+" pursues regional dominance while judging the current military balance favorable. Alliances, provisioning and succession may change that opportunity.";
            return "Competing courts dispute frontier security, exchange access and fiscal obligations. The campaign reflects their current leadership and resources, not an inevitable cultural clash.";
        }
        bool TryStrategicHistoricalWar(PrehistoryState s,HistoricalRelation relation)
        {
            if(s.Years<relation.TruceUntil)return false;var a=s.History.Polities[relation.A];var b=s.History.Polities[relation.B];
            double ca=DirectedHistoricalWarChance(s,a,b,relation),cb=DirectedHistoricalWarChance(s,b,a,relation);
            double chance=D.Q(1-(1-ca)*(1-cb),1000000000);if(chance<=0||HistoryRoll("strategic-war-11-"+relation.A,relation.B,s.Years)>=chance)return false;
            int initiator=HistoryRoll("strategic-initiator-11-"+relation.A,relation.B,s.Years)<ca/(ca+cb)?a.Id:b.Id;
            relation.WarSince=s.Years;relation.Trade=false;relation.Alliance=false;relation.Trust=-60;BeginCivicCampaign(s,relation,initiator);return true;
        }
    }
}
