using System;
using System.Linq;
namespace WorldGen.Core
{
    // Separate nullable extension: existing history-17..20 checkpoints keep
    // both their hashes and their rules until the owner explicitly upgrades.
    [Serializable] public sealed class HistoricalCommunityDynamics
    {
        public HistoricalLocalCommunity[] Cities=Array.Empty<HistoricalLocalCommunity>();
        public HistoricalGovernmentPolicy[] Governments=Array.Empty<HistoricalGovernmentPolicy>();
        public HistoricalCampaignAdvanceCollection Campaigns=new HistoricalCampaignAdvanceCollection();
        public HistoricalCommunityDynamics Copy()=>new HistoricalCommunityDynamics{Cities=Cities.Select(c=>c.Copy()).ToArray(),Governments=Governments.Select(g=>g.Copy()).ToArray(),Campaigns=Campaigns.Copy()};
    }
    [Serializable] public sealed class HistoricalLocalCommunity
    {
        public int RulerId=-1,SinceYear,InclusionMilli;
        public HistoricalLocalCommunity Copy()=>(HistoricalLocalCommunity)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalGovernmentPolicy
    {
        public int RepresentationMilli=100,CentralizationMilli=600,LastReformYear;
        public HistoricalGovernmentPolicy Copy()=>(HistoricalGovernmentPolicy)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalCampaignAdvance
    {
        public long AttackerAreaKm2,DefenderAreaKm2;
        public HistoricalCampaignAdvance Copy()=>(HistoricalCampaignAdvance)MemberwiseClone();
    }
    public sealed partial class Prehistory
    {
        public const string CommunityHistoryVersion="history-21";
        public const string DiffusionHistoryVersion="history-22";
        public const string AgronomicHistoryVersion="history-23";
        static bool AgronomicHistory(PrehistoryState s)=>LivingWorldHistory(s)||s?.History?.Version==AgronomicHistoryVersion||TechnicalHistory(s);
        static bool PassageHistory(PrehistoryState s)=>LivingWorldHistory(s)||s?.History?.Version==DiffusionHistoryVersion||AgronomicHistory(s);
        static bool CommunityHistory(PrehistoryState s)=>LivingWorldHistory(s)||s?.History?.Version==CommunityHistoryVersion||PassageHistory(s);
        public PrehistoryState UpgradeToAgronomicHistory(PrehistoryState source)
        {
            Validate(source);if(AgronomicHistory(source))return source;
            if(!PassageHistory(source))source=UpgradeToDiffusionHistory(source);
            var s=source.Copy();s.History.Version=AgronomicHistoryVersion;s.Seal();return s;
        }
        public PrehistoryState UpgradeToDiffusionHistory(PrehistoryState source)
        {
            Validate(source);if(PassageHistory(source))return source;
            if(!CommunityHistory(source))source=UpgradeToCommunityHistory(source);
            var s=source.Copy();s.History.Version=DiffusionHistoryVersion;
            PrepareDiffusionPassages();s.Seal();return s;
        }
        public PrehistoryState UpgradeToCommunityHistory(PrehistoryState source)
        {
            Validate(source);if(CommunityHistory(source))return source;
            if(!ProvincialHistory(source))source=UpgradeToRegionalHistory(source);
            var s=source.Copy();s.History.Version=CommunityHistoryVersion;var a=s.History.Aggregates;
            a.Communities=new HistoricalCommunityDynamics{
                Cities=s.Society.Settlements.Select(t=>new HistoricalLocalCommunity{RulerId=a.Resolve(t.PolityId,s.History),SinceYear=s.Years,InclusionMilli=400}).ToArray(),
                Governments=s.History.Polities.Select(p=>new HistoricalGovernmentPolicy{LastReformYear=s.Years}).ToArray(),
                Campaigns=s.History.Dynamics.Campaigns.ReadOnlyValues().Select(c=>new HistoricalCampaignAdvance()).ToArray()};
            PrepareCommunityShores();
            s.Seal();return s;
        }
    }
}
