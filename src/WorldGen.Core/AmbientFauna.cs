using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace WorldGen.Core
{
    public enum AmbientFaunaFamily { Cricket, Cicada, Katydid, Firefly }
    public enum AmbientFaunaRepresentation { AudioOnly, LightweightVisual }
    public enum InsectSongShape { ChirpTrain, SwellingBuzz, RaspTrain, PulsedCroak, WhistlePhrase }
    public enum InsectSeasonCue { AnnualWarmSeason, WarmMoistOpportunities }

    // Four broad authored parents, not a hardcoded list of Earth species.
    // These derived candidates do not become saved Taxa or food-web members.
    public sealed class AmbientFaunaLineage
    {
        public readonly AmbientFaunaFamily Family;
        public readonly AmbientFaunaRepresentation Representation;
        public readonly string Key,Name,AdultDiet,JuvenileDiet;
        public readonly Habitat Habitats;
        internal AmbientFaunaLineage(AmbientFaunaFamily family,AmbientFaunaRepresentation representation,
            string key,string name,Habitat habitats,string adultDiet,string juvenileDiet)
        {Family=family;Representation=representation;Key=key;Name=name;Habitats=habitats;AdultDiet=adultDiet;JuvenileDiet=juvenileDiet;}
    }
    public sealed class InsectAcousticTraits
    {
        public InsectSongShape Shape;
        public string Function="Male mate-attraction call";
        public double CarrierHz,HarmonicMix,NoiseMix,RelativeLoudness;
        public double PulseOnSeconds,PulseGapSeconds,PhraseGapSeconds,AttackSeconds,ReleaseSeconds;
        public int PulsesPerPhrase;
        public double ReferenceTemperatureC=25,RateFractionPerC,PitchCentsPerC;
        // A separate small within-species spread; not a new species per emitter.
        public double IndividualPitchSpreadCents,IndividualTimingSpreadFraction;
    }
    public sealed class InsectActivityTraits
    {
        public double StartSolarHour,DurationHours,MinTemperatureC,PreferredTemperatureC,MaxTemperatureC;
        public InsectSeasonCue SeasonCue;
        // Relative to the LOCAL warm-season peak, not fixed northern dates.
        public double SeasonPeakOffsetDays,AdultDisplayWindowDays;
        public double RainSuppressionMmPerHour,WindSuppressionMps;
    }
    public sealed class FireflyDisplayTraits
    {
        public double FlashOnSeconds,WithinBoutGapSeconds,BetweenBoutGapSeconds;
        public int FlashesPerBout;
        public double WavelengthNm,MinFlightHeightM,MaxFlightHeightM;
        public bool Synchronous=false;
    }
    public sealed class AmbientFaunaSpecies
    {
        public string Id="",Name="",FounderId="",FounderName="",ParentLineageKey="";
        public string ProfileVersion="",AdultDiet="",JuvenileDiet="";
        public int RegionId,ProvinceId;
        public AmbientFaunaFamily Family;
        public AmbientFaunaRepresentation Representation;
        public Habitat Habitats;
        public Stratum CallingStratum;
        public double MinSourceHeightM,MaxSourceHeightM,MinWarmSeasonC,MaxWarmSeasonC;
        public double MinAridityRatio,MaxSalinityPpt,MinCanopyFraction;
        public double? ReferenceSiteSuitability;
        public string OccurrenceStatus="Potential regional candidate; not a dispersal-verified range or animal census";
        public InsectAcousticTraits Acoustics;
        public InsectActivityTraits Activity;
        public FireflyDisplayTraits Display;
    }

    /// <summary>Versioned, lazy derived profiles, like procedural flora. No saved
    /// world mutation, playback, population simulation, or dynamic evolution.</summary>
    public sealed class AmbientFauna
    {
        public const string Version="ambient-fauna-1";
        static readonly ConditionalWeakTable<World,AmbientFauna> contexts=new ConditionalWeakTable<World,AmbientFauna>();
        static readonly IReadOnlyList<AmbientFaunaLineage> lineages=Array.AsReadOnly(new[]
        {
            new AmbientFaunaLineage(AmbientFaunaFamily.Cricket,AmbientFaunaRepresentation.AudioOnly,"cricket","Crickets",
                Habitat.AnyLand,"Seeds, plant material and small invertebrates; varying omnivory","Similar small omnivore niche"),
            new AmbientFaunaLineage(AmbientFaunaFamily.Cicada,AmbientFaunaRepresentation.AudioOnly,"cicada","Cicadas",
                Habitat.Forest|Habitat.Open|Habitat.Wetland|Habitat.Coastal,"Plant xylem sap","Subterranean root-xylem feeding; no simulated emergence cycle"),
            new AmbientFaunaLineage(AmbientFaunaFamily.Katydid,AmbientFaunaRepresentation.AudioOnly,"katydid","Katydids",
                Habitat.Forest|Habitat.Open|Habitat.Wetland,"Foliage and occasional small prey; varying omnivory","Similar foliage-associated niche"),
            new AmbientFaunaLineage(AmbientFaunaFamily.Firefly,AmbientFaunaRepresentation.LightweightVisual,"firefly","Fireflies",
                Habitat.Forest|Habitat.Open|Habitat.Wetland,"Limited nectar/pollen feeding or little adult feeding; not simulated","Predatory moist-litter/soil niche; not simulated")
        });
        static readonly string[] syllables={"al","bel","cor","den","el","fen","gal","hel","ir","jun","kel","lin","mor","nel","or","pel","ren","sil","tor","val"};
        readonly World world;
        readonly ConcurrentDictionary<int,IReadOnlyList<AmbientFaunaSpecies>> regions=new ConcurrentDictionary<int,IReadOnlyList<AmbientFaunaSpecies>>();
        public static IReadOnlyList<AmbientFaunaLineage> Lineages=>lineages;
        public static AmbientFauna For(World world)=>contexts.GetValue(world,w=>new AmbientFauna(w));
        AmbientFauna(World world){this.world=world;}
        public IReadOnlyList<AmbientFaunaSpecies> Region(int region)
        {
            var e=world.Ecology;
            if(e==null||region<0||region>=e.Regions.Length)return Array.Empty<AmbientFaunaSpecies>();
            return regions.GetOrAdd(region,CreateRegion);
        }
        static string Word(ref Rng r)=>syllables[(int)(r.Unit()*syllables.Length)]+syllables[(int)(r.Unit()*syllables.Length)];
        // Founder dominates; regional divergence is at most 6% of the range.
        static double Trait(ref Rng founder,ref Rng branch,double low,double high)
            =>D.Q(low+(high-low)*D.Clamp(founder.Unit()+(branch.Unit()-.5)*.12),1000000);
        IReadOnlyList<AmbientFaunaSpecies> CreateRegion(int region)
        {
            var e=world.Ecology;var r=e.Regions[region];int province=r.ProvinceId,cell=r.RepresentativeCell;
            HabitatCell h=cell>=0&&cell<e.Habitat.Length?e.Habitat[cell]:null;
            var result=new AmbientFaunaSpecies[lineages.Count];
            for(int i=0;i<result.Length;i++)
            {
                var parent=lineages[i];bool cricket=parent.Family==AmbientFaunaFamily.Cricket,cicada=parent.Family==AmbientFaunaFamily.Cicada,
                    katydid=parent.Family==AmbientFaunaFamily.Katydid,firefly=parent.Family==AmbientFaunaFamily.Firefly;
                var founder=new Rng(world.Recipe.Seed,"ambient-fauna-founder-1",unchecked(province*31+i));
                var branch=new Rng(world.Recipe.Seed,"ambient-fauna-branch-1",unchecked(region*31+i));
                var naming=new Rng(world.Recipe.Seed,"ambient-fauna-name-1",unchecked(province*31+i));
                var localName=new Rng(world.Recipe.Seed,"ambient-fauna-epithet-1",unchecked(region*31+i));
                string genus=Word(ref naming)+"a";genus=char.ToUpperInvariant(genus[0])+genus.Substring(1);
                string founderId=Version+":"+world.Recipe.Seed.ToString(CultureInfo.InvariantCulture)+":"+province.ToString(CultureInfo.InvariantCulture)+":"+parent.Key;
                bool highSource=cicada||katydid&&founder.Unit()>.4||cricket&&founder.Unit()>.8;
                var s=new AmbientFaunaSpecies{Id=founderId+":"+region.ToString(CultureInfo.InvariantCulture),Name=genus+" "+Word(ref localName)+"is",
                    FounderId=founderId,FounderName=genus+" / "+parent.Name,ParentLineageKey=parent.Key,ProfileVersion=Version,
                    RegionId=region,ProvinceId=province,Family=parent.Family,Representation=parent.Representation,Habitats=parent.Habitats,
                    AdultDiet=parent.AdultDiet,JuvenileDiet=parent.JuvenileDiet,
                    CallingStratum=firefly?Stratum.Understory:highSource?Stratum.Canopy:cricket?Stratum.Ground:Stratum.Understory,
                    MinSourceHeightM=highSource?Trait(ref founder,ref branch,1.5,4):Trait(ref founder,ref branch,.03,.25),
                    MaxSourceHeightM=highSource?Trait(ref founder,ref branch,7,18):Trait(ref founder,ref branch,.4,1.6),
                    MinWarmSeasonC=Trait(ref founder,ref branch,cicada?15:firefly?12:katydid?11:7,cicada?22:firefly?18:katydid?17:13),
                    MaxWarmSeasonC=Trait(ref founder,ref branch,38,46),
                    MinAridityRatio=Trait(ref founder,ref branch,firefly?.5:katydid?.3:cicada?.16:.08,firefly?.9:katydid?.65:cicada?.45:.3),
                    MaxSalinityPpt=Trait(ref founder,ref branch,1,4),MinCanopyFraction=highSource?Trait(ref founder,ref branch,cicada?.12:.08,cicada?.35:.25):0};
                s.Activity=new InsectActivityTraits
                {
                    StartSolarHour=Trait(ref founder,ref branch,cicada?8:17,cicada?11:20),DurationHours=Trait(ref founder,ref branch,cicada?7:8,cicada?10:12),
                    MinTemperatureC=Trait(ref founder,ref branch,cicada?16:firefly?11:7,cicada?23:firefly?17:14),
                    PreferredTemperatureC=Trait(ref founder,ref branch,24,30),MaxTemperatureC=Trait(ref founder,ref branch,35,42),
                    SeasonCue=h!=null&&h.ColdTenthsC>=180?InsectSeasonCue.WarmMoistOpportunities:InsectSeasonCue.AnnualWarmSeason,
                    SeasonPeakOffsetDays=Trait(ref founder,ref branch,-25,35),AdultDisplayWindowDays=Trait(ref founder,ref branch,firefly?14:cicada?35:50,firefly?65:cicada?110:170),
                    RainSuppressionMmPerHour=Trait(ref founder,ref branch,1,5),WindSuppressionMps=Trait(ref founder,ref branch,3,8)
                };
                if(!firefly)
                {
                    s.Acoustics=new InsectAcousticTraits
                    {
                        Shape=cicada?InsectSongShape.SwellingBuzz:katydid?InsectSongShape.RaspTrain:InsectSongShape.ChirpTrain,
                        CarrierHz=Trait(ref founder,ref branch,katydid?5500:cicada?3500:2500,katydid?10500:cicada?9000:6500),
                        HarmonicMix=Trait(ref founder,ref branch,cricket?.1:.4,cricket?.4:.85),NoiseMix=Trait(ref founder,ref branch,cricket?.02:.3,cricket?.15:.8),
                        RelativeLoudness=Trait(ref founder,ref branch,cicada?.7:.3,cicada?1:.65),
                        PulseOnSeconds=Trait(ref founder,ref branch,cicada?1.5:.025,cicada?5:.075),PulseGapSeconds=Trait(ref founder,ref branch,cicada?.15:.025,cicada?.7:.09),
                        PulsesPerPhrase=(int)Math.Round(Trait(ref founder,ref branch,cicada?1:2,cicada?3:6),MidpointRounding.AwayFromZero),
                        PhraseGapSeconds=Trait(ref founder,ref branch,cicada?.6:.25,cicada?4:1.4),AttackSeconds=Trait(ref founder,ref branch,cicada?.08:.003,cicada?.25:.012),
                        ReleaseSeconds=Trait(ref founder,ref branch,cicada?.15:.008,cicada?.5:.035),RateFractionPerC=Trait(ref founder,ref branch,cicada?0:.02,cicada?.006:.05),
                        PitchCentsPerC=Trait(ref founder,ref branch,0,cricket?3:1),IndividualPitchSpreadCents=Trait(ref founder,ref branch,3,12),
                        IndividualTimingSpreadFraction=Trait(ref founder,ref branch,.015,.06)
                    };
                }
                else s.Display=new FireflyDisplayTraits
                {
                    FlashOnSeconds=Trait(ref founder,ref branch,.12,.5),WithinBoutGapSeconds=Trait(ref founder,ref branch,.4,1.3),BetweenBoutGapSeconds=Trait(ref founder,ref branch,2,7),
                    FlashesPerBout=(int)Math.Round(Trait(ref founder,ref branch,1,4),MidpointRounding.AwayFromZero),WavelengthNm=Trait(ref founder,ref branch,555,590),
                    MinFlightHeightM=Trait(ref founder,ref branch,.15,.4),MaxFlightHeightM=Trait(ref founder,ref branch,.8,2)
                };
                s.ReferenceSiteSuitability=h==null?(double?)null:HabitatSuitability(s,h);result[i]=s;
            }
            return Array.AsReadOnly(result);
        }
        /// <summary>Constant-time abiotic potential only: no native-range, local
        /// plant-host, water-edge or live-occupancy assertion.</summary>
        public static double HabitatSuitability(AmbientFaunaSpecies s,HabitatCell h)
        {
            if(h==null||h.Biome==Biome.Ocean||h.Biome==Biome.Ice||h.Biome==Biome.Freshwater||h.Biome==Biome.SalineLake||(s.Habitats&h.Habitat)==0)return 0;
            double warmth=D.Clamp((h.WarmTenthsC*.1-s.MinWarmSeasonC)/5)*D.Clamp((s.MaxWarmSeasonC-h.WarmTenthsC*.1)/5);
            double moisture=D.Clamp((h.AridityMilli*.001-s.MinAridityRatio)/.35);
            double salt=1-D.Clamp(h.SalinityMilliPpt/(s.MaxSalinityPpt*1000));
            double woody=s.MinCanopyFraction<=0?1:D.Clamp((AnimalEcotypes.Canopy(h)*.001-s.MinCanopyFraction)/.3);
            return D.Q(D.Clamp(warmth*moisture*salt*woody),1000000);
        }
    }
}
