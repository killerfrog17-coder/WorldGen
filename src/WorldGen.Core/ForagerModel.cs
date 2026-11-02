using System;
using System.Linq;
namespace WorldGen.Core
{
    public enum ForagerFood { Plant,SmallGame,LargeGame,Freshwater,Marine }
    public enum ForagerResidence { ResidentialMobility,SeasonalBaseCamps,CollectorCamps,AquaticResidence }
    public enum ForagerEventKind { Settlement,Departure,Refuge,Decline,Recovery,Extinction,Contact,Innovation,KnowledgeLoss,LanguageBranch,BeliefBranch,Outbreak,Conflict,Admixture }

    // New-world rules live in a separate ledger. The rejected Paleolithic v1/v2
    // records remain unchanged for old-save replay, never silently reinterpreted.
    [Serializable] public sealed class ForagerState
    {
        public string Version="foragers-4";
        public double InitialPopulation;
        public long Seconds,BoundarySeconds,NextReviewSeconds;
        public int NextCohort,NextCulture,NextLanguage,NextBelief,NextFamily,NextPeople;
        public int[] CellDistrict=Array.Empty<int>();
        public ForagerDistrict[] Districts=Array.Empty<ForagerDistrict>();
        public ForagerResources[] Resources=Array.Empty<ForagerResources>();
        public ForagerBiotaDistrict[] Biota=Array.Empty<ForagerBiotaDistrict>();
        public HomininLineage[] Lineages=Array.Empty<HomininLineage>();
        public ForagerCohort[] Cohorts=Array.Empty<ForagerCohort>();
        public ForagerCulture[] Cultures=Array.Empty<ForagerCulture>();
        public ForagerPeopleIdentity[] Peoples=Array.Empty<ForagerPeopleIdentity>();
        public ForagerLanguage[] Languages=Array.Empty<ForagerLanguage>();
        public ForagerLanguageFamily[] LanguageFamilies=Array.Empty<ForagerLanguageFamily>();
        public ForagerBelief[] Beliefs=Array.Empty<ForagerBelief>();
        public ForagerExchange[] Exchanges=Array.Empty<ForagerExchange>();
        public ForagerEvent[] Events=Array.Empty<ForagerEvent>();
        public ForagerDiscovery[] Discoveries=Array.Empty<ForagerDiscovery>();
        public ForagerState Copy()=>new ForagerState{Version=Version,InitialPopulation=InitialPopulation,Seconds=Seconds,BoundarySeconds=BoundarySeconds,NextReviewSeconds=NextReviewSeconds,NextCohort=NextCohort,NextCulture=NextCulture,NextLanguage=NextLanguage,NextBelief=NextBelief,NextFamily=NextFamily,NextPeople=NextPeople,Discoveries=Discoveries.Select(x=>x.Copy()).ToArray(),
            CellDistrict=(int[])CellDistrict.Clone(),Districts=Districts.Select(x=>x.Copy()).ToArray(),Resources=Resources.Select(x=>x.Copy()).ToArray(),Biota=Biota.Select(x=>x.Copy()).ToArray(),Lineages=Lineages.Select(x=>x.Copy()).ToArray(),Cohorts=Cohorts.Select(x=>x.Copy()).ToArray(),Cultures=Cultures.Select(x=>x.Copy()).ToArray(),Peoples=Peoples.Select(x=>x.Copy()).ToArray(),Languages=Languages.Select(x=>x.Copy()).ToArray(),LanguageFamilies=LanguageFamilies.Select(x=>x.Copy()).ToArray(),Beliefs=Beliefs.Select(x=>x.Copy()).ToArray(),Exchanges=Exchanges.Select(x=>x.Copy()).ToArray(),Events=Events.Select(x=>x.Copy()).ToArray()};
    }
    [Serializable] public sealed class ForagerDiscovery
    {
        public int Id,Technique,Cohort,District,Year;
        public ForagerDiscovery Copy()=>(ForagerDiscovery)MemberwiseClone();
    }
    [Serializable] public sealed class ForagerBiotaDistrict
    {
        public int District,ReviewedYear;
        public int[] Taxa=Array.Empty<int>();
        public double[] Abundance=Array.Empty<double>(),BaselineFood=new double[5],FoodAvailability=new[]{1d,1d,1d,1d,1d};
        public double ForestCover;
        public ForagerBiotaDistrict Copy(){var c=(ForagerBiotaDistrict)MemberwiseClone();c.Taxa=(int[])Taxa.Clone();c.Abundance=(double[])Abundance.Clone();c.BaselineFood=(double[])BaselineFood.Clone();c.FoodAvailability=(double[])FoodAvailability.Clone();return c;}
        public double Presence(int taxon){int k=Array.BinarySearch(Taxa,taxon);return k<0?0:Abundance[k];}
    }
    [Serializable] public sealed class ForagerResources
    {
        public int District,LastReviewYear;
        public double[] Stock=new[]{1d,1d,1d,1d,1d};
        public double HarvestPressure,HumanDensity,BurnSeverity;
        public int BurnUntilYear;
        public ForagerResources Copy(){var c=(ForagerResources)MemberwiseClone();c.Stock=(double[])Stock.Clone();return c;}
    }
    [Serializable] public sealed class ForagerDistrict
    {
        public int Id,Patch,Province,Realm,Cell;
        public int[] Cells=Array.Empty<int>();
        public ForagerPassage[] Passages=Array.Empty<ForagerPassage>();
        public V Center;
        public double AreaKm2,RadiusKm,Productivity,WinterC,SummerC,MeanC,Aridity,GrowingDays,Water,Coast,River,Relief,Canopy,Stone,Fiber,Wood,Resin,Pathogens;
        public double PetMm,AnnualRainMm,OceanMoistureAccess;
        public double[] Food=new double[5],Seasonality=new double[5];
        public ForagerDistrict Copy(){var c=(ForagerDistrict)MemberwiseClone();c.Cells=(int[])Cells.Clone();c.Passages=Passages.Select(x=>x.Copy()).ToArray();c.Food=(double[])Food.Clone();c.Seasonality=(double[])Seasonality.Clone();return c;}
    }
    [Serializable] public sealed class ForagerPassage
    {
        public int To,FromCell,ToCell,DistanceM,SeaFallM,WaterDistanceM;
        public bool Sea,Shelf;
        public double Exposure,MountainBarrier,WaterCost;
        public ForagerPassage Copy()=>(ForagerPassage)MemberwiseClone();
    }
    [Serializable] public sealed class HomininLineage
    {
        public int Id,Parent=-1,OriginDistrict,AncestorOrigin,SeparationYears;
        public string Name="",ScientificName="",Basis="";
        public bool PlayerSpecies,Sapient;
        // Explicit fictional-world capacities, not diagnoses inferred from fossil
        // skulls. More than one lineage may be designated sapient in later presets.
        public double CulturalLearning=1,SymbolicPotential=1;
        public double GenerationYears=27,AdultMassKg=65,StatureM=1.68,Compactness=.5,LimbRatio=.5,FaceProjection=.5,BrowRidge=.5,JawBreadth=.5,NoseForm=.5;
        public double[] Ancestry=new double[8];
        public int[] AncestralRoute=Array.Empty<int>();
        public HomininLineage Copy(){var c=(HomininLineage)MemberwiseClone();c.Ancestry=(double[])Ancestry.Clone();c.AncestralRoute=(int[])AncestralRoute.Clone();return c;}
    }
    [Serializable] public sealed class ForagerAffiliation
    {
        public int Id; public double Share;
        public ForagerAffiliation Copy()=>(ForagerAffiliation)MemberwiseClone();
    }
    [Serializable] public sealed class ForagerCohort
    {
        public int Id,District,Lineage,Culture,Identity,Language,Belief,Parent=-1,FoundedYear,LastMigrationYear,LastContactYear,LastLanguageReviewYear;
        // Canonical distributions retain every represented resident community.
        // Primary/runner-up fields below are derived observer caches only.
        public ForagerAffiliation[] Speech=Array.Empty<ForagerAffiliation>(),Traditions=Array.Empty<ForagerAffiliation>();
        public int MinorityLanguage=-1,MinorityBelief=-1;
        public double MinorityLanguageShare,MinorityBeliefShare,BilingualShare;
        public double MatingContacts;
        public double Children,Adults,Elders,Capacity,Occupation,BirthRate,AdultMortality,ChildMortality,ElderMortality;
        public double[] Ages=new double[5];
        public double FoodSecurity,WinterSecurity,Health,ResourcePressure,ConflictPressure,Isolation,Connectivity,CarryingPractice;
        public double Births,Deaths,Immigration,Emigration;
        public double[] Knowledge=Array.Empty<double>(),Skill=Array.Empty<double>(),Use=Array.Empty<double>(),Teaching=Array.Empty<double>(),Innovation=Array.Empty<double>();
        public int[] FirstKnownYear=Array.Empty<int>(),FirstSkilledYear=Array.Empty<int>(),LastLossYear=Array.Empty<int>();
        // Saved per-technique expertise/access blocks; see TeacherContext.
        public double[] TeachingFrom=Array.Empty<double>();public double ContactTeaching,TeachingAdults;
        public double[] Diet=new double[5],Appearance=new double[8],Admixture=Array.Empty<double>();
        public long ShockStart,ShockEnd;
        public double ShockSeverity,ShockRecovery;
        public ForagerEventKind ShockKind;
        public int HomeCell,FrontierCell;
        public double FrontierKm;
        public double Population=>Children+Adults+Elders;
        public ForagerCohort Copy(){var c=(ForagerCohort)MemberwiseClone();c.Speech=Speech.Select(x=>x.Copy()).ToArray();c.Traditions=Traditions.Select(x=>x.Copy()).ToArray();c.Ages=(double[])Ages.Clone();c.Knowledge=(double[])Knowledge.Clone();c.Skill=(double[])Skill.Clone();c.Use=(double[])Use.Clone();c.Teaching=(double[])Teaching.Clone();c.Innovation=(double[])Innovation.Clone();c.FirstKnownYear=(int[])FirstKnownYear.Clone();c.FirstSkilledYear=(int[])FirstSkilledYear.Clone();c.LastLossYear=(int[])LastLossYear.Clone();c.TeachingFrom=(double[])TeachingFrom.Clone();c.Diet=(double[])Diet.Clone();c.Appearance=(double[])Appearance.Clone();c.Admixture=(double[])Admixture.Clone();return c;}
    }
    [Serializable] public sealed class ForagerPeopleIdentity
    {
        public int Id,Parent=-1,OriginDistrict,FoundedYear;
        public string Name="",HeritageStory="";
        public int[] Affinities=Array.Empty<int>();
        public ForagerPeopleIdentity Copy(){var c=(ForagerPeopleIdentity)MemberwiseClone();c.Affinities=(int[])Affinities.Clone();return c;}
    }
    [Serializable] public sealed class ForagerCulture
    {
        public int Id,Parent=-1,OriginDistrict,FoundedYear,Seed;
        public string Name="";
        public ForagerResidence Residence;
        public double FoodSharing,GuestAccess,KinObligation,ExchangeOpenness,PrestigeCompetition,ConflictAvoidance,MarriageMobility,ChildcareCooperation;
        public double[] FoodPreference=new double[5],TechniquePreference=Array.Empty<double>();
        public ForagerCulture Copy(){var c=(ForagerCulture)MemberwiseClone();c.FoodPreference=(double[])FoodPreference.Clone();c.TechniquePreference=(double[])TechniquePreference.Clone();return c;}
    }
    [Serializable] public sealed class ForagerLanguage
    {
        public int Id,Parent=-1,Family,OriginDistrict,FoundedYear,LastChangeYear,Seed;
        public string Name="";
        public LanguageProfile Sounds;
        // Each meaning retains a root ancestry identity independently of sound
        // changes. Contact can borrow a root without changing someone's species.
        public int[] Roots=Array.Empty<int>(),RootYears=Array.Empty<int>();
        public double[] Rates=Array.Empty<double>(),Grammar=Array.Empty<double>();
        public double NarrativePractice,TechnicalRegister,RitualRegister;
        public ForagerLanguage Copy(){var c=(ForagerLanguage)MemberwiseClone();c.Sounds=Sounds.Copy();c.Roots=(int[])Roots.Clone();c.RootYears=(int[])RootYears.Clone();c.Rates=(double[])Rates.Clone();c.Grammar=(double[])Grammar.Clone();return c;}
    }
    [Serializable] public sealed class ForagerLanguageFamily
    {
        public int Id,Parent=-1,FounderLanguage,FoundedYear,OriginDistrict;
        public string Name="";
        public int[] ReferenceRoots=Array.Empty<int>();
        public ForagerLanguageFamily Copy(){var c=(ForagerLanguageFamily)MemberwiseClone();c.ReferenceRoots=(int[])ReferenceRoots.Clone();return c;}
    }
    [Serializable] public sealed class ForagerBelief
    {
        public int Id,Parent=-1,Family,OriginDistrict,FoundedYear,Seed;
        public string Name="",SacredLandscape="",SacredAnimal="",SeasonalRite="",MortuaryPractice="";
        public double Animacy,AncestorPresence,Afterlife,RitualSpecialists,HealingRites,CollectiveRites,ReciprocityWithGame,CreatorNarrative;
        public int[] SacredCells=Array.Empty<int>(),FoodTaboos=Array.Empty<int>();
        public ForagerBelief Copy(){var c=(ForagerBelief)MemberwiseClone();c.SacredCells=(int[])SacredCells.Clone();c.FoodTaboos=(int[])FoodTaboos.Clone();return c;}
    }
    [Serializable] public sealed class ForagerExchange
    {
        public int A,B,SinceYear,LastMeetingYear;
        public double Strength,Kinship,Hospitality,Trade,Bilingualism,Hostility;
        public ForagerExchange Copy()=>(ForagerExchange)MemberwiseClone();
    }
    [Serializable] public sealed class ForagerEvent
    {
        public long Seconds;public int Cohort=-1,District=-1,Other=-1;
        public ForagerEventKind Kind;public string Text="";
        public ForagerEvent Copy()=>(ForagerEvent)MemberwiseClone();
    }
    // Query products, not one permanent agent per generated inhabitant. An NPC
    // host persists identities/edits for bands it actually instantiates.
    public sealed class ForagerBandSample
    {
        public long Id;public int Cohort,District,Lineage,Culture,Language,Belief;
        public string Name="";public V Home,Position;public int People;
        public double HomeRangeKm,MoveFrequencyPerYear,Aggregation,FoodSecurity,Hospitality;
        public ForagerResidence Residence;
        public bool[] Techniques=Array.Empty<bool>();
        // Capability, ordinary practice and prepared winter equipment are distinct.
        public bool[] RoutineTechniques=Array.Empty<bool>(),WinterTechniques=Array.Empty<bool>();
        public ForagerPersonSample[] Members=Array.Empty<ForagerPersonSample>();
        public ForagerPersonRecord[] FamilyRecords=Array.Empty<ForagerPersonRecord>();
        public ForagerCampSample[] SeasonalCamps=Array.Empty<ForagerCampSample>();
        public long[] ContactBands=Array.Empty<long>();
    }
    public sealed class ForagerPersonRecord
    {
        public long Id,Household,Mother=-1,Father=-1,Partner=-1;
        public bool Female;public double BirthYear,DeathYear;
    }
    public sealed class ForagerLanguageKnowledge
    {
        public int Language;public double Fluency;public bool Native;
    }
    public sealed class ForagerPersonSample
    {
        public long Id,Household,Mother=-1,Father=-1,Partner=-1;
        public string Name="",Role="";public bool Female;public double AgeYears,BirthYear,DeathYear;
        public double[] Appearance=Array.Empty<double>(),Knowledge=Array.Empty<double>(),Skills=Array.Empty<double>();
        public ForagerLanguageKnowledge[] Languages=Array.Empty<ForagerLanguageKnowledge>();
    }
    public sealed class ForagerCampSample
    {
        public long Id;public V Position;public int Cell,Season;
        public string Purpose="";public double DurationDays,WaterAccess,Exposure,FoodOpportunity;
        public bool RequiresLocalGroundValidation=true,ClimateSuitable=true;
    }
}
