using System;
using System.Linq;
namespace WorldGen.Core
{
    public enum HistoricalWarAim { Frontier, Conquest, Tribute, Reunification, TradeRoute, Liberation }
    [Serializable] public sealed class HistoricalLeader
    {
        public int Id,PolityId,StartedYear,EndedYear=-1,ExpectedEndYear,Military,Administration,Diplomacy,Ambition,Renown,Victories,Defeats,Reforms;
        public string Name="",Legacy="";
        [System.Runtime.Serialization.OptionalField] public int PredecessorId=-1;
        [System.Runtime.Serialization.OptionalField] public string Selection="";
        [System.Runtime.Serialization.OnDeserializing] void InitializeOptionalSuccession(System.Runtime.Serialization.StreamingContext context){PredecessorId=-1;Selection="";}
        public HistoricalLeader Copy()=>(HistoricalLeader)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalStateDynamics
    {
        [NonSerialized] internal byte[] OwnedCanonicalRecord;
        public int Id,LeaderId=-1,FoundationPotential,Strain,LastSuccession,PublicWorks;
        public long PeakPopulation,PeakAreaKm2;
        public string Tradition="";
        [System.Runtime.Serialization.OptionalField] public int HouseSerial,DisputedUntil;
        [System.Runtime.Serialization.OptionalField] public HistoricalPoliticalCycle Cycle;
        public HistoricalStateDynamics Copy(){var c=(HistoricalStateDynamics)MemberwiseClone();c.Cycle=Cycle?.Copy();return c;}
    }
    [Serializable] public sealed class HistoricalCampaign
    {
        [NonSerialized] internal byte[] OwnedCanonicalRecord;
        public int Id,A,B,AttackerId,LeaderId,StartedYear,EndedYear=-1,LastActionYear,Conquests,Repulses,TributeRecipientId=-1;
        public long CasualtiesA,CasualtiesB,TributePaid,Spoils;
        public HistoricalWarAim Aim;
        public string Name="",Objective="",Outcome="Ongoing";
        [System.Runtime.Serialization.OptionalField] public string Context;
        public int[] CapturedSettlements=Array.Empty<int>();
        public HistoricalCampaign Copy(){var c=(HistoricalCampaign)MemberwiseClone();c.CapturedSettlements=(int[])CapturedSettlements.Clone();return c;}
    }
    [Serializable] public sealed class HistoricalDynamics
    {
        public HistoricalStateDynamics[] States=Array.Empty<HistoricalStateDynamics>();
        public HistoricalLeader[] Leaders=Array.Empty<HistoricalLeader>();
        public HistoricalCampaign[] Campaigns=Array.Empty<HistoricalCampaign>();
        [System.Runtime.Serialization.OptionalField] public HistoricalDependency[] Dependencies=Array.Empty<HistoricalDependency>();
        [System.Runtime.Serialization.OnDeserializing] void InitializeOptionalDependencies(System.Runtime.Serialization.StreamingContext context){Dependencies=Array.Empty<HistoricalDependency>();}
        public HistoricalDynamics Copy()=>new HistoricalDynamics{States=States.Select(s=>s.Copy()).ToArray(),Leaders=Leaders.Select(s=>s.Copy()).ToArray(),Campaigns=Campaigns.Select(s=>s.Copy()).ToArray(),Dependencies=Dependencies?.Select(s=>s.Copy()).ToArray()};
        internal HistoricalDynamics CopyOwned(HistoricalPolity[] polities)=>new HistoricalDynamics{States=States.Select(s=>polities[s.Id].EndedYear>=0?s:s.Copy()).ToArray(),Leaders=Leaders.Select(s=>s.EndedYear>=0?s:s.Copy()).ToArray(),Campaigns=Campaigns.Select(s=>s.EndedYear>=0?s:s.Copy()).ToArray(),Dependencies=Dependencies?.Select(s=>s.Copy()).ToArray()};
    }
    public static class HistoricalInstitutionRequirements
    {
        // Bounded game priors. Productive concentrated cores, shared infrastructure,
        // accountable storage and restricted exit can support different state paths.
        public static double Readiness(double storedSurplus,double concentration,double circumscription,double coordination,double institutionalSupport)
            =>D.Q(D.Clamp(storedSurplus)*D.Clamp(institutionalSupport)*(.35+.25*D.Clamp(concentration)+.20*D.Clamp(circumscription)+.20*D.Clamp(coordination)),1000000);
        public static double FormationChance(double readiness)=>readiness<.18?0:D.Q(.0006+.004*readiness*readiness,1000000000);
    }
    public static class HistoricalLeadershipEffects
    {
        public static double AdministrativeReachKm(long supportedPeople,int administration,int publicWorks,bool writing,bool roads)
            =>D.Q((90+.45*administration+.2*publicWorks+(writing?150:0)+(roads?200:0))*(.4+.6*D.Clamp(D.Sqrt(Math.Max(0,supportedPeople)/200000.0))),1000000);
        public static int TreatyTrustBonus(int diplomacyA,int diplomacyB)=>(diplomacyA+diplomacyB-1000)/300;
        public static double TreatyFailureChance(int diplomacyA,int diplomacyB)=>D.Q(.25-.20*(diplomacyA+diplomacyB)/2000.0,1000000);
        public static int IntegrationStrain(double farthestCityKm,double administrativeReachKm,int cityCount,int culturalCenters)
        {
            double relay=D.Q(1+.30*Math.Log(1+Math.Max(1,cityCount)),1000000);
            return (int)Math.Min(700,300*Math.Max(0,farthestCityKm/Math.Max(1,administrativeReachKm*relay)-.7)+Math.Max(0,culturalCenters-1)*25);
        }
        public static long TransferableFunds(long payer,long recipient,long demand)=>Math.Max(0,Math.Min(Math.Max(0,demand),Math.Min(Math.Max(0,payer),1000000000-Math.Max(0,Math.Min(1000000000,recipient)))));
    }
}
