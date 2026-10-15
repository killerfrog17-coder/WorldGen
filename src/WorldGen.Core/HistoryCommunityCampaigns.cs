using System;
namespace WorldGen.Core
{
    public static partial class HistoricalCommunityCampaignEffects
    {
        // Physical area, not cells: the same army must not conquer ten times
        // more slowly simply because the player chose a finer terrain mesh.
        public static long AnnualAdvanceKm2(double fieldPower,double transport,int military,int ambition)
        {
            double exceptional=1+3*D.Clamp((military-800)/150.0)*D.Clamp((ambition-800)/150.0);
            return (long)Math.Round((5000+250*Math.Sqrt(Math.Max(0,fieldPower)))*Math.Sqrt(Math.Max(1,transport))*exceptional);
        }
    }
    public sealed partial class Prehistory
    {
        int CommunityCampaignActions(PrehistoryState s,int campaign,int winner,bool attacker,double power,int elapsed)
        {
            var h=s.History;var progress=h.Aggregates.Communities.Campaigns[campaign];var hc=h.Cultures[h.Polities[winner].CultureId];
            var leader=h.Dynamics.Leaders[h.Dynamics.States[winner].LeaderId];
            long annual=HistoricalCommunityCampaignEffects.AnnualAdvanceKm2(power,HistoricalModernEffects.LandTransport(hc,s.Years),leader.Military,leader.Ambition);
            ref long budget=ref (attacker?ref progress.AttackerAreaKm2:ref progress.DefenderAreaKm2);
            budget=Math.Min(annual*3,budget+annual*elapsed);return 64;
        }
        bool SpendCommunityCampaignArea(PrehistoryState s,int campaign,bool attacker,int target)
        {
            var progress=s.History.Aggregates.Communities.Campaigns[campaign];
            ref long budget=ref (attacker?ref progress.AttackerAreaKm2:ref progress.DefenderAreaKm2);
            long cost=Math.Max(1,(long)Math.Round((TechnicalHistory(s)?TerritorialPoliticalArea(target):inhabitableArea[target])*(1+World.Ecology.Habitat[target].ReliefM/1200.0)));
            if(budget<cost)return false;budget-=cost;return true;
        }
    }
}
