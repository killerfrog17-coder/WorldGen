using System;

namespace WorldGen.Core
{
    public static class HistoricalLivingWorldOrigins
    {
        public static double IndependentStateHazard(double core,double environment,double staples,double homeland)
        {
            // Core already multiplies fiscal surplus, concentration, institutions,
            // exchange and coordination. A fourth power of that product still
            // made genuinely provisioned agrotowns wait millions of years per
            // site. The conditional response is quadratic, like the learned
            // route; actual ecology, viable staples and homeland remain separate
            // strong barriers, not waived because a village has survived.
            double support=D.Clamp(core),crop=.01+.99*D.Clamp(staples);
            return Math.Max(.000000000000001,D.Q((.000000002+.00025*support*support)*environment*crop*crop*homeland,1000000000000000));
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
            s.History.Version=LivingWorldHistoryVersion;ExpandNuclearCapabilities(s);InitializeFreshHumanVariation(s);InitializeInternational(s);InitializeColonialPortRetries(s);InitializeProvisionedPorts(s);InitializeCoastalFarming(s);InitializeReceiverFrontierLearning(s);InitializeRegionalTraditions(s);InitializeGeographicKnowledge(s);InitializeRealmLifecycles(s);s.Seal();return s;
        }
    }
}
