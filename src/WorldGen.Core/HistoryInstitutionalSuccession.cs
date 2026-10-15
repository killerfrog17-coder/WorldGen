using System;
namespace WorldGen.Core
{
    // Explicit game priors: a recognized institutional handover is not the
    // same political shock as replacing a personal dynasty or a military coup.
    public static class HistoricalInstitutionalEffects
    {
        public static int SuccessionLegitimacyCost(bool hereditary,int institutions,int diplomacy,int formerRenown,bool contested)
        {
            double continuity=D.Clamp(institutions/1000.0),negotiation=D.Clamp(diplomacy/1000.0);
            double routine=hereditary?30+20*(1-continuity):6+10*(1-continuity)+8*(1-negotiation);
            double personal=Math.Min(150,Math.Max(0,formerRenown)/5.0)*(hereditary?1:.25);
            return (int)Math.Round(D.Q(routine+personal+(contested?60:0),1000000));
        }
        public static int ServiceBurden(int distanceM,int localReachM,bool supported)
            =>supported?Math.Min(700,(int)Math.Round(D.Q(300*Math.Max(0,distanceM/(double)Math.Max(1,localReachM)-.7),1000000))):700;
        public static int RealmStrain(long weightedBurden,long residents,long disconnected,int culturalCenters)
            =>Math.Min(700,(int)(residents<=0?0:Math.Round((decimal)Math.Max(0,weightedBurden)/residents+200m*Math.Min(Math.Max(0,disconnected),residents)/residents))+Math.Max(0,culturalCenters-1)*25);
    }
    public sealed partial class Prehistory
    {
        static bool InstitutionalHistory(PrehistoryState s)=>IsAggregateHistoryVersion(s.History?.Version)||(s.History?.Version=="history-12"||(s.History?.Version=="history-13"||(s.History?.Version=="history-14"||(s.History?.Version=="history-15"||s.History?.Version=="history-16"))));
        public PrehistoryState UpgradeToInstitutionalHistory(PrehistoryState source)
        {
            Validate(source);if(InstitutionalHistory(source))return source;
            if(source.History?.Version!="history-11")throw new InvalidOperationException("Update this history to delegated administration first.");
            var s=source.Copy();s.History.Version="history-12";s.Seal();return UpgradeScheduling(s);
        }
    }
}
