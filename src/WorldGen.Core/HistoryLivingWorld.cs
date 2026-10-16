using System;

namespace WorldGen.Core
{
    public static class HistoricalLivingWorldOrigins
    {
        public static double IndependentStateHazard(double core,double environment,double staples,double homeland)
        {
            // Core already multiplies fiscal surplus, concentration, institutions,
            // exchange and coordination. Requiring eight simultaneous copies of
            // those tests is incompatible with finite-lived early communities.
            double support=D.Clamp(core),crop=.01+.99*D.Clamp(staples);
            return Math.Max(.000000000000001,D.Q((.000000002+.00025*Math.Pow(support,4))*environment*crop*crop*homeland,1000000000000000));
        }
    }
    public sealed partial class Prehistory
    {
        public const string LivingWorldHistoryVersion="history-32";
        static bool LivingWorldHistory(PrehistoryState s)=>s?.History?.Version==LivingWorldHistoryVersion;
        public PrehistoryState UpgradeToLivingWorldHistory(PrehistoryState source)
        {
            Validate(source);if(LivingWorldHistory(source))return source;
            source=UpgradeToEnvironmentalHistory(source);var s=source.Copy();
            s.History.Version=LivingWorldHistoryVersion;ExpandNuclearCapabilities(s);InitializeFreshHumanVariation(s);s.Seal();return s;
        }
    }
}
