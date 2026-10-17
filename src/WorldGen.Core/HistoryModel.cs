using System;
using System.Linq;
using System.Collections.Generic;
namespace WorldGen.Core
{
    public enum HistoryOverlay { Civilizations, States, Cultures, Religions, Technology, Territories, Provinces, ReligiousInstitutions, Languages }
    // Stable IDs: append capabilities; never reinterpret a saved adoption date.
    public enum HistoricalTechnology { Storage, Cultivation, Pottery, Sailing, Irrigation, Copper, Writing, Bronze, Roads, Iron, Coinage, Mills, Steel, Navigation, Scholasticism,
        Printing,Gunpowder,OceanNavigation,ScientificMethod,CropRotation,SteamPower,IndustrialMetallurgy,Railways,GermTheory,Sanitation,Electricity,Telegraph,
        ArtificialFertilizers,CombustionEngines,Aviation,Radio,Vaccination,Antibiotics,MechanizedAgriculture,Electronics,Computing,Telecommunications,NuclearFission,NuclearWeapons }
    [Serializable] public sealed class HistoricalCulture
    {
        public int Id, ReligionId, LanguageId, Sedentism, Surplus, Institution;
        [System.Runtime.Serialization.OptionalField] public int ParentCultureId=-1;
        [System.Runtime.Serialization.OptionalField] public int InstitutionalExamples;
        [System.Runtime.Serialization.OptionalField] public HistoricalTechnicalPractice Technical;
        [System.Runtime.Serialization.OnDeserializing] void InitializeOptionalLineage(System.Runtime.Serialization.StreamingContext context){ParentCultureId=-1;}
        public int[] Technology=new int[15], Research=new int[15]; // adoption year + 1 (zero = absent), accumulated effort
        public HistoricalCulture Copy(){var c=(HistoricalCulture)MemberwiseClone();c.Technology=(int[])Technology.Clone();c.Research=(int[])Research.Clone();c.Technical=Technical?.Copy();return c;}
    }
    [Serializable] public sealed class HistoricalReligion
    {
        public int Id, ParentId=-1, CultureId, HolyCell, FoundedYear;
        [System.Runtime.Serialization.OptionalField] public ReligiousReach Reach;
        public string Name="", Tradition="", Ethos="", Ritual="";
        public HistoricalReligion Copy()=>(HistoricalReligion)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalPolity
    {
        [NonSerialized] internal byte[] OwnedCanonicalRecord;
        public int Id, CultureId, CapitalId, FoundedYear, EndedYear=-1, ParentId=-1, RulerSince;
        public string Name="", Government="City council", Ruler="";
        public int Legitimacy=650, Cohesion=700, ReligionId, Army, Exhaustion;
        public long Treasury, Population, AreaKm2;
        [System.Runtime.Serialization.OptionalField] public int LastEstimateYear;
        public HistoricalPolity Copy()=>(HistoricalPolity)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalRelation
    {
        [NonSerialized] internal byte[] OwnedCanonicalRecord;
        [NonSerialized] internal RelationCanonicalSnapshot OwnedCanonicalSnapshot;
        public int A,B,Trust=0,WarSince=-1,TruceUntil,LastBattle;
        public bool Trade,Alliance;
        public HistoricalRelation Copy()=>(HistoricalRelation)MemberwiseClone();
    }
    // Exact runtime guard, not saved history. Mutable reads alone are not
    // changes: compare every canonical scalar before reusing private bytes.
    internal readonly struct RelationCanonicalSnapshot
    {
        readonly int a,b,trust,warSince,truceUntil,lastBattle;
        readonly bool trade,alliance;
        internal RelationCanonicalSnapshot(HistoricalRelation r)
        {a=r.A;b=r.B;trust=r.Trust;warSince=r.WarSince;truceUntil=r.TruceUntil;lastBattle=r.LastBattle;trade=r.Trade;alliance=r.Alliance;}
        internal bool Matches(HistoricalRelation r)=>a==r.A&&b==r.B&&trust==r.Trust&&warSince==r.WarSince&&truceUntil==r.TruceUntil&&lastBattle==r.LastBattle&&trade==r.Trade&&alliance==r.Alliance;
    }
    [Serializable] public sealed class HistoricalTown
    {
        [NonSerialized] internal byte[] OwnedCanonicalRecord;
        public int Id, DiseaseUntil, ImmunityUntil, Walls;
        public long FoodBase;
        [System.Runtime.Serialization.OptionalField] public HarvestHazard LastHarvestHazard;
        [System.Runtime.Serialization.OptionalField] public int LastHarvestHazardYear,ShortageYears;
        [System.Runtime.Serialization.OptionalField] public long ImportedFood,ExportedFood,FoodTransportLoss,DiseaseDeaths;
        [System.Runtime.Serialization.OptionalField] public HistoricalDiseaseKind DiseaseKind;
        [System.Runtime.Serialization.OptionalField] public int DiseaseMortalityPermille;
        [System.Runtime.Serialization.OptionalField] public int DiseaseWaveId;
        [System.Runtime.Serialization.OptionalField] public int[] KindImmunity;
        [System.Runtime.Serialization.OptionalField] public int AutonomyPressure;
        [System.Runtime.Serialization.OptionalField] public int LastAutonomyActionYear;
        [System.Runtime.Serialization.OptionalField] public int UnrestPolityId=-1;
        [System.Runtime.Serialization.OptionalField] public int CivicPreparationYears;
        [System.Runtime.Serialization.OptionalField] public int CivicExampleExposure;
        [System.Runtime.Serialization.OptionalField] public int CivicTeacherPolityId=-1;
        [System.Runtime.Serialization.OptionalField] public HistoricalUrbanDemography Demography;
        [System.Runtime.Serialization.OptionalField] public HistoricalLocalHarvest Cropping;
        [System.Runtime.Serialization.OnDeserializing] void InitializeOptionalAutonomy(System.Runtime.Serialization.StreamingContext context){UnrestPolityId=-1;CivicTeacherPolityId=-1;}
        public HistoricalTown Copy(){var copy=(HistoricalTown)MemberwiseClone();copy.KindImmunity=KindImmunity==null?null:(int[])KindImmunity.Clone();copy.Demography=Demography?.Copy();copy.Cropping=Cropping?.Copy();return copy;}
    }
    [Serializable] public sealed class HistoricalEvent
    {
        public int Id,Year,Cell=-1,CultureId=-1,PolityId=-1,OtherPolityId=-1,ReligionId=-1,SettlementId=-1;
        public ChronicleCategory Category;public string Kind="",Text="";
        public HistoricalEvent Copy()=>(HistoricalEvent)MemberwiseClone();
    }
    [Serializable] public sealed class HistoryState
    {
        public string Version=Prehistory.CurrentHistoryVersion;
        public int[] CellPolity=Array.Empty<int>(),CellReligion=Array.Empty<int>(),CellMarket=Array.Empty<int>();
        public int[] CellStage=Array.Empty<int>();
        public HistoricalCulture[] Cultures=Array.Empty<HistoricalCulture>();
        public HistoricalReligion[] Religions=Array.Empty<HistoricalReligion>();
        public HistoricalPolity[] Polities=Array.Empty<HistoricalPolity>();
        public HistoricalRelationCollection Relations=new HistoricalRelationCollection();
        public HistoricalTown[] Towns=Array.Empty<HistoricalTown>();
        [System.Runtime.Serialization.OptionalField] public HistoricalAgriculture[] Agriculture=Array.Empty<HistoricalAgriculture>();
        [System.Runtime.Serialization.OptionalField] public HistoricalDynamics Dynamics=new HistoricalDynamics();
        [System.Runtime.Serialization.OptionalField] public HistoricalFaithState Faith;
        [System.Runtime.Serialization.OptionalField] public HistoricalRegionalLearning RegionalLearning;
        [System.Runtime.Serialization.OptionalField] public HistoricalAdministration Administration;
        [System.Runtime.Serialization.OptionalField] public HistoricalOceanSettlement[] OceanSettlements;
        [System.Runtime.Serialization.OptionalField] public HistoricalModernSettings Modern;
        [System.Runtime.Serialization.OptionalField] public HistoricalAggregateState Aggregates;
        [System.Runtime.Serialization.OptionalField] public HistoricalLanguages Languages;
        [System.Runtime.Serialization.OptionalField] public HistoricalOriginReach OriginReach;
        [System.Runtime.Serialization.OptionalField] public HistoricalInternationalSystem International;
        [System.Runtime.Serialization.OptionalField] public HistoricalNuclearHistory Nuclear;
        [System.Runtime.Serialization.OptionalField] public HistoricalReceiverFrontierRules ReceiverFrontier;
        [System.Runtime.Serialization.OptionalField] public HistoricalRegionalTraditions RegionalTraditions;
        // Lifetime archive: UI's 256-event feed must never erase campaign lore.
        public HistoricalEventArchive Archive=new HistoricalEventArchive();
        public HistoryState Copy()=>CopyRecords(false);
        internal HistoryState CopyOwned()=>CopyRecords(true);
        HistoryState CopyRecords(bool owned)=>new HistoryState{Version=Version,CellPolity=(int[])CellPolity.Clone(),CellReligion=(int[])CellReligion.Clone(),CellStage=(int[])CellStage.Clone(),CellMarket=(int[])CellMarket.Clone(),Cultures=Cultures.Select(c=>c.Copy()).ToArray(),Religions=Religions.Select(r=>r.Copy()).ToArray(),Towns=Towns.Select(t=>owned&&Aggregates?.Living?.Cities[t.Id].AbandonedYear>=0?t:t.Copy()).ToArray(),Polities=Polities.Select(p=>owned&&p.EndedYear>=0?p:p.Copy()).ToArray(),Relations=owned?Relations.ForkOwned():Relations.Fork(),Agriculture=Agriculture?.Select(a=>a.Copy()).ToArray(),Dynamics=owned?Dynamics?.CopyOwned(Polities):Dynamics?.Copy(),Faith=Faith?.Copy(),RegionalLearning=RegionalLearning?.Copy(),Administration=Administration?.Copy(),OceanSettlements=OceanSettlements?.Select(v=>v.Copy()).ToArray(),Modern=Modern?.Copy(),Aggregates=Aggregates?.Copy(),Languages=Languages?.Copy(),OriginReach=OriginReach?.Copy(),Nuclear=Nuclear?.Copy(),International=International?.Copy(),ReceiverFrontier=ReceiverFrontier?.Copy(),RegionalTraditions=RegionalTraditions?.Copy(),Archive=Archive.Fork(!owned)};
    }
    public static class HistoricalTechnologies
    {
        public static readonly string[] Names={"Food storage","Cultivation","Pottery","Coastal sailing","Irrigation","Copper working","Writing and accounts","Bronze working","Road networks","Iron working","Coinage","Water and wind mills","Steel working","Medieval navigation","Scholastic learning",
            "Printing and broad literacy","Gunpowder weapons","Ocean-going navigation","Experimental science","Crop rotations and soil management","Steam power","Industrial metallurgy","Railway networks","Germ theory","Public sanitation","Electric power","Telegraph networks",
            "Synthetic fertilizers","Internal combustion","Powered aviation","Radio communication","Vaccination","Antibiotics","Mechanized farming","Electronics","Computing","Digital telecommunications","Nuclear fission and civilian energy","Nuclear weapons capability"};
        public static readonly int[][] Prerequisites={Array.Empty<int>(),new[]{0},new[]{0},new[]{0},new[]{1,2},new[]{2},new[]{0,2},new[]{5},new[]{1},new[]{5},new[]{6,5},new[]{8,9},new[]{9},new[]{3,6},new[]{6,11},
            new[]{6,11},new[]{9,14},new[]{13,12},new[]{14,15},new[]{1,11},new[]{12,18},new[]{20,12},new[]{20,21,8},new[]{18,15},new[]{23,8},new[]{18,21},new[]{25,15},
            new[]{18,21,19},new[]{20,21},new[]{28,18},new[]{25,26},new[]{23},new[]{23,21},new[]{28,19},new[]{25,30},new[]{34,15},new[]{35,30},new[]{18,25,21},new[]{37,18,21}};
        public static int Count(string version)=>version==Prehistory.LivingWorldHistoryVersion?39:Prehistory.IsAggregateHistoryVersion(version)?37:(version=="history-13"||(version=="history-14"||(version=="history-15"||version=="history-16")))?37:15;
        static readonly int[] WayfindingPrerequisites={(int)HistoricalTechnology.Sailing};
        static readonly int[] LiteratePrintingPrerequisites={(int)HistoricalTechnology.Writing,(int)HistoricalTechnology.Mills,(int)HistoricalTechnology.Scholasticism};
        public static int[] Requirements(string version,int technology)=>(version==Prehistory.RegionalHistoryVersion||version==Prehistory.CommunityHistoryVersion||version==Prehistory.DiffusionHistoryVersion||version==Prehistory.AgronomicHistoryVersion)&&technology==(int)HistoricalTechnology.Printing?LiteratePrintingPrerequisites:LegacyRequirements(Prehistory.IsAggregateHistoryVersion(version)?"history-16":version,technology);
        static int[] LegacyRequirements(string version,int technology)=>(version=="history-6"||version=="history-7"||version=="history-8"||(version=="history-9"||(version=="history-10"||(version=="history-11"||(version=="history-12"||(version=="history-13"||(version=="history-14"||(version=="history-15"||version=="history-16"))))))))&&technology==(int)HistoricalTechnology.Navigation?WayfindingPrerequisites:Prerequisites[technology];
        public static string Name(string version,int technology)=>LegacyName(Prehistory.IsAggregateHistoryVersion(version)?"history-16":version,technology);
        static string LegacyName(string version,int technology)=>(version=="history-6"||version=="history-7"||version=="history-8"||(version=="history-9"||(version=="history-10"||(version=="history-11"||(version=="history-12"||(version=="history-13"||(version=="history-14"||(version=="history-15"||version=="history-16"))))))))&&technology==(int)HistoricalTechnology.Navigation?"Ocean wayfinding and navigation":Names[technology];
        public static bool Has(HistoricalCulture c,HistoricalTechnology t)=>(int)t<c.Technology.Length&&c.Technology[(int)t]>0&&(c.Technical==null||c.Technical.Deployment[(int)t]>=250);
    }
}
