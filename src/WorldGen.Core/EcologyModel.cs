using System;

namespace WorldGen.Core
{
    public enum Biome { Ocean, Ice, Tundra, BorealForest, TemperateForest, TemperateRainforest, MediterraneanWoodland, Grassland, Desert, TropicalRainforest, TropicalSeasonalForest, Savanna, Alpine, Floodplain, Marsh, Peatland, Mangrove, SaltMarsh, Freshwater, SalineLake }
    public enum SoilKind { Rock, Sand, Loam, Alluvial, Leached, Organic, Frozen, Saline }
    public enum FloodRegime { None, RiverSeasonal, RiverPersistent, Tidal, LakeMargin }
    [Flags] public enum Habitat { None=0, Forest=1, Open=2, Desert=4, Tundra=8, Wetland=16, Freshwater=32, Coastal=64, Alpine=128, AnyLand=223 }
    [Flags] public enum Food { None=0, Grass=1, Browse=2, Fruit=4, Seed=8, Nectar=16, Detritus=32, Invertebrate=64, Vertebrate=128, Fish=256 }
    [Flags] public enum Stratum { None=0, Ground=1, Understory=2, Canopy=4, Air=8, Water=16, Soil=32 }
    public enum TaxonRank { Species, Subspecies, FunctionalGuild, Lineage }
    // A lineage is an authored animal archetype, not a claim that a generated
    // regional form is one of Earth's named species. Sources describe ancestry;
    // they do not impose modern Earth coordinates on procedural worlds.
    [Serializable] public sealed class AnimalNativeSource
    {
        public string Realm="";
        public double Latitude,Longitude;
        public int OriginCell=-1;
    }
    [Serializable] public sealed class AnimalLineage
    {
        public string Key="";
        public AnimalNativeSource[] Sources=Array.Empty<AnimalNativeSource>();
        public bool RequiresCoast, RequiresFreshwater, Ectotherm, Aquatic, MarineFeeder, AdjacentSeaOnly;
        public int MinCanopyPermille,MaxCanopyPermille=1000;
        public int MinSalinityMilliPpt;
        public int EarVariationPermille,AppendageVariationPermille,BodyVariationPermille=200;
        public int ThermalSizeSensitivityPermille=200;
        public int RidingSuitabilityPermille;
        public string DisplayTrait="",Pattern="plain";
    }
    [Serializable] public sealed class AnimalEcotype
    {
        public string LineageKey="",HabitatForm="",CoatPattern="";
        public int SampleCell;
        public int BodySizePermille,EarSizePermille,AppendageSizePermille,DisplaySizePermille;
        public string DisplayTrait="";
    }
    // Authored comparative priors, not a fitted domestication probability.
    [Serializable] public sealed class AnimalTraits
    {
        public int DietFlexibility, CaptiveBreeding, Docility, PanicResistance, SocialTolerance, MaturityMonths;
        public bool Milk, Fiber, Traction, SeasonalWhite;
        public int InsulationPermille=500, CoatLightnessPermille=450;
        public AnimalTraits Copy()=>(AnimalTraits)MemberwiseClone();
    }
    [Serializable] public sealed class RegionalAnimalVariant
    {
        public string Name="";
        public double MassKg;
        public int InsulationPermille,CoatLightnessPermille,SeasonalWhiteningPermille;
        public int Docility,PanicResistance;
        [System.Runtime.Serialization.OptionalField] public AnimalEcotype Ecotype;
    }
    [Serializable] public sealed class HabitatCell
    {
        public int PetMm, AridityMilli, WaterDeficitMm, GrowingDays, ColdTenthsC, WarmTenthsC, MeanTenthsC, WarmSeasonDryPermille;
        public int WaterTableCm, PermafrostPermille, FloodFractionPermille, FloodDaysPerYear, FloodPulsesPerYear;
        public int SalinityMilliPpt, SoilFertilityPermille, FirePotentialPermille, ProductivityGCM2Year, ReliefM;
        public SoilKind Soil; public FloodRegime Flooding; public Biome Biome; public Habitat Habitat;
    }
    [Serializable] public sealed class Ecoregion
    {
        public int Id, ProvinceId, RepresentativeCell, PatchCount;
        public string Name=""; public Biome Biome; public long AreaM2;
        public int[] Cells=Array.Empty<int>(), Neighbors=Array.Empty<int>();
        public SpeciesPresence[] Species=Array.Empty<SpeciesPresence>();
        public FoodLink[] FoodWeb=Array.Empty<FoodLink>();
    }
    [Serializable] public sealed class SpeciesPresence
    {
        public int TaxonId, SuitableAreaPermille, OriginCell;
        public bool UsedHistoricalBridge;
        [System.Runtime.Serialization.OptionalField] public RegionalAnimalVariant Variant;
    }
    [Serializable] public sealed class FoodLink
    {
        public int ConsumerId, ResourceId; public Food Resource;
        // Plausible feeding opportunity, not a measured flux or guaranteed encounter.
        public int OverlapPermille;
    }
    [Serializable] public sealed class Taxon
    {
        public int Id; public string Name="", ScientificName="", ParentSpecies="", OriginGroup="";
        public TaxonRank Rank; public bool Producer, BasalResource;
        public string RequiredResource="";
        public Habitat Habitats; public Food Eats, Provides; public Stratum Occupies, Hunts;
        public double MassKg, MinPreyKg, MaxPreyKg;
        public int MinColdTenthsC, MaxWarmTenthsC, MinWarmTenthsC, MinAridityMilli, MaxAridityMilli=20000;
        public int MaxColdTenthsC=600;
        public bool HistoricalBridgeAllowed;
        public int PassageColdTenthsC, PassageMaxWarmTenthsC=500, MaxElevationM=6000;
        public int MaxSalinityMilliPpt=1000, MaxFloodDays=365;
        public int MaxGapKm=250, OceanCrossingKm, SpreadBudgetKm=18000;
        public double EarthLatitude, EarthLongitude;
        [System.Runtime.Serialization.OptionalField] public AnimalTraits Traits;
        [System.Runtime.Serialization.OptionalField] public AnimalLineage Lineage;
    }
    [Serializable] public sealed class EcologyResult
    {
        public string PackVersion="earth-starter-1";
        public HabitatCell[] Habitat=Array.Empty<HabitatCell>();
        public int[] Region=Array.Empty<int>(), Province=Array.Empty<int>();
        public Ecoregion[] Regions=Array.Empty<Ecoregion>();
        public Taxon[] Taxa=Array.Empty<Taxon>();
        public int[] Origins=Array.Empty<int>();
        public int[] SpeciesBits=Array.Empty<int>();
        public double[] WaterSalinityPpt=Array.Empty<double>();
    }
}
