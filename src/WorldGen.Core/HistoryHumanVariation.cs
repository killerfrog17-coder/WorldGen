using System;

namespace WorldGen.Core
{
    public enum HumanAppearanceTrait
    { SkinPigmentation,HairPigmentation,IrisPigmentation,HairCurl,BodyCompactness,FacialVariation,NasalVariation,StaturePotential }

    // A future species pack can supply another profile, but no other sapient
    // species is implemented or silently projected onto human trait meanings.
    public sealed class SapientAppearanceProfile
    {
        public string Species { get; }
        public int TraitCount=>8;
        public double GenerationYears { get; }
        public double SkinAdaptationYears { get; }
        public double BodyAdaptationYears { get; }
        internal SapientAppearanceProfile(string species,double generation,double skin,double body)
        {Species=species;GenerationYears=generation;SkinAdaptationYears=skin;BodyAdaptationYears=body;}
    }
    public sealed class HumanAppearance
    {
        public bool Available;
        public string Species="Homo sapiens",Basis="";
        public int Region=-1,SampleCell=-1;
        public int SkinPigmentation,HairPigmentation,IrisPigmentation,HairCurl,BodyCompactness,FacialVariation,NasalVariation,StaturePotential;
        public V SkinColor,HairColor,IrisColor;
        public int Value(HumanAppearanceTrait trait)
        {
            switch(trait)
            {
                case HumanAppearanceTrait.SkinPigmentation:return SkinPigmentation;
                case HumanAppearanceTrait.HairPigmentation:return HairPigmentation;
                case HumanAppearanceTrait.IrisPigmentation:return IrisPigmentation;
                case HumanAppearanceTrait.HairCurl:return HairCurl;
                case HumanAppearanceTrait.BodyCompactness:return BodyCompactness;
                case HumanAppearanceTrait.FacialVariation:return FacialVariation;
                case HumanAppearanceTrait.NasalVariation:return NasalVariation;
                default:return StaturePotential;
            }
        }
    }
    public static class HistoryHumanVariation
    {
        // Bounded procedural model parameters, not a facial reconstruction or
        // a diagnostic of ethnicity. Future NPCs can persist the sampled result.
        public static HumanAppearance Sample(HumanAppearance mean,int seed,int individual)
        {
            if(mean==null||!mean.Available)return new HumanAppearance{Basis="No resident appearance estimate."};
            int SampleTrait(HumanAppearanceTrait trait)
            {
                int key=unchecked(individual*397+mean.Region*104729);
                double noise=0;for(int k=0;k<4;k++)noise+=Rng.Value(seed,"npc-appearance-36-"+(int)trait+"-"+k,key)-.5;
                int spread=trait==HumanAppearanceTrait.SkinPigmentation?1200:trait==HumanAppearanceTrait.HairPigmentation||trait==HumanAppearanceTrait.IrisPigmentation?1800:2400;
                return Math.Max(0,Math.Min(10000,mean.Value(trait)+(int)Math.Round(noise*spread)));
            }
            var p=new HumanAppearance{Available=true,Region=mean.Region,SampleCell=mean.SampleCell,Species=mean.Species,Basis="Seeded individual from a regional distribution, not an ethnic template.",
                SkinPigmentation=SampleTrait(HumanAppearanceTrait.SkinPigmentation),HairPigmentation=SampleTrait(HumanAppearanceTrait.HairPigmentation),IrisPigmentation=SampleTrait(HumanAppearanceTrait.IrisPigmentation),
                HairCurl=SampleTrait(HumanAppearanceTrait.HairCurl),BodyCompactness=SampleTrait(HumanAppearanceTrait.BodyCompactness),FacialVariation=SampleTrait(HumanAppearanceTrait.FacialVariation),NasalVariation=SampleTrait(HumanAppearanceTrait.NasalVariation),StaturePotential=SampleTrait(HumanAppearanceTrait.StaturePotential)};
            p.SkinColor=SkinSwatch(p.SkinPigmentation);p.HairColor=HairSwatch(p.HairPigmentation);p.IrisColor=IrisSwatch(p.IrisPigmentation);return p;
        }
        static readonly SapientAppearanceProfile Human=new SapientAppearanceProfile("Homo sapiens",28,18000,65000);
        public static SapientAppearanceProfile Profile(string species)
        {if(species!="Homo sapiens")throw new ArgumentException("No appearance profile is implemented for this sapient species.",nameof(species));return Human;}
        public static string TraitName(HumanAppearanceTrait trait)
        {
            switch(trait)
            {
                case HumanAppearanceTrait.SkinPigmentation:return "Skin pigmentation";
                case HumanAppearanceTrait.HairPigmentation:return "Hair pigmentation";
                case HumanAppearanceTrait.IrisPigmentation:return "Iris pigmentation";
                case HumanAppearanceTrait.HairCurl:return "Hair curl";
                case HumanAppearanceTrait.BodyCompactness:return "Body compactness";
                case HumanAppearanceTrait.FacialVariation:return "Facial variation";
                case HumanAppearanceTrait.NasalVariation:return "Nasal variation";
                default:return "Stature potential";
            }
        }
        // Unitless, annual exposure proxy, not UVI, measured UVB or a skin-type
        // diagnosis. Temperature is deliberately not a pigmentation input.
        public static double UvExposure(double sineLatitude,int elevationM,int annualPrecipitationMm,int canopyPermille)
        {
            double latitude=1-D.Clamp(sineLatitude*sineLatitude);
            double cloud=.12*D.Clamp(annualPrecipitationMm/3000.0),shade=.10*D.Clamp(canopyPermille/1000.0);
            double altitude=1+.12*D.Clamp(elevationM/1000.0,0,4);
            return D.Q(D.Clamp((.04+.96*latitude)*(1-cloud)*(1-shade)*altitude),1000000);
        }
        public static int SkinTarget(double exposure)
            =>Math.Max(0,Math.Min(10000,(int)Math.Round(1100+7700*Math.Pow(D.Clamp(exposure),.8))));
        public static int BodyTarget(int meanTenthsC)
            =>(int)Math.Round(5000+1100*D.Clamp((130-meanTenthsC)/220.0,-1,1));
        public static int FounderValue(int seed,int origin,HumanAppearanceTrait trait,double uv,int meanTenthsC)
        {
            int baseline=trait==HumanAppearanceTrait.SkinPigmentation?SkinTarget(uv):trait==HumanAppearanceTrait.HairPigmentation?8700:trait==HumanAppearanceTrait.IrisPigmentation?8200:trait==HumanAppearanceTrait.BodyCompactness?BodyTarget(meanTenthsC):5000;
            int spread=trait==HumanAppearanceTrait.SkinPigmentation?500:trait==HumanAppearanceTrait.HairPigmentation||trait==HumanAppearanceTrait.IrisPigmentation?800:1800;
            return Math.Max(0,Math.Min(10000,baseline+(int)Math.Round((Rng.Value(seed,"human-founder-appearance-32-"+(int)trait,origin)-.5)*spread)));
        }
        public static int MixValue(int existing,long previousResidents,int incoming,long migrants)
        {
            if(migrants<=0)return existing;if(previousResidents<=0)return incoming;
            return (int)(((decimal)existing*previousResidents+(decimal)incoming*migrants)/((decimal)previousResidents+migrants));
        }
        public static int Evolve(int seed,int region,int year,HumanAppearanceTrait trait,int previous,int elapsed,long residents,double uv,int meanTenthsC,double culturalBuffer=0)
        {
            if(previous<0||previous>10000)throw new ArgumentOutOfRangeException(nameof(previous));
            if(elapsed<=0||residents<=0)return previous;
            double value=previous,buffer=1+4*D.Clamp(culturalBuffer);
            if(trait==HumanAppearanceTrait.SkinPigmentation)value+=(SkinTarget(uv)-value)*(1-Math.Exp(-elapsed/(Human.SkinAdaptationYears*buffer)));
            else if(trait==HumanAppearanceTrait.BodyCompactness)value+=(BodyTarget(meanTenthsC)-value)*(1-Math.Exp(-elapsed/(Human.BodyAdaptationYears*buffer)));
            // Regional phenotype means, not named racial boxes or a literal
            // single-locus allele simulation. Population size controls drift;
            // mutation/polygenic variance prevents artificial absorbing endpoints.
            double ne=D.Clamp(residents*.1,60,10000),p=previous/10000.0;
            double sigma=1300*Math.Sqrt((elapsed/Human.GenerationYears)*(.02+.98*p*(1-p))/(2*ne));
            int key=unchecked(year*397+region*104729);
            double noise=(Rng.Value(seed,"human-appearance-drift-a-32-"+(int)trait,key)+Rng.Value(seed,"human-appearance-drift-b-32-"+(int)trait,key)+Rng.Value(seed,"human-appearance-drift-c-32-"+(int)trait,key)-1.5)*2;
            value=D.Q(D.Clamp(value+noise*sigma,0,10000),1000000);
            int whole=(int)value;
            if(Rng.Value(seed,"human-appearance-round-32-"+(int)trait,key)<value-whole)whole++;
            return Math.Max(0,Math.Min(10000,whole));
        }
        public static V SkinSwatch(int pigment)
        {
            double x=D.Clamp(pigment/10000.0);return new V(.95-.72*x,.79-.66*x,.65-.57*x);
        }
        public static V HairSwatch(int pigment)
        {
            double x=D.Clamp(pigment/10000.0);return new V(.85-.79*x,.65-.61*x,.31-.28*x);
        }
        public static V IrisSwatch(int pigment)
        {
            double x=D.Clamp(pigment/10000.0);var pale=new V(.38,.54,.62);var hazel=new V(.43,.38,.21);var dark=new V(.16,.105,.055);
            return x<.5?pale*(1-x*2)+hazel*(x*2):hazel*(2-x*2)+dark*(x*2-1);
        }
        internal static HumanAppearance Read(PrehistoryState s,int region,int sampleCell,string basis)
        {
            if(s?.History?.Version!=Prehistory.LivingWorldHistoryVersion||s.Species!="Homo sapiens"||region<0||region>=s.Population.Length)return new HumanAppearance{Basis="Appearance traits are unavailable under the saved historical rules."};
            int at=region*8;var a=s.Ancestry;
            var result=new HumanAppearance{Available=true,Region=region,SampleCell=sampleCell,Basis=basis,SkinPigmentation=a[at],HairPigmentation=a[at+1],IrisPigmentation=a[at+2],HairCurl=a[at+3],BodyCompactness=a[at+4],FacialVariation=a[at+5],NasalVariation=a[at+6],StaturePotential=a[at+7]};
            result.SkinColor=SkinSwatch(result.SkinPigmentation);result.HairColor=HairSwatch(result.HairPigmentation);result.IrisColor=IrisSwatch(result.IrisPigmentation);return result;
        }
    }
    public sealed partial class Prehistory
    {
        void HumanAppearanceEnvironment(int region,out int cell,out double uv,out int meanTemperature)
        {
            cell=Patches[region].Cell;var h=World.Ecology.Habitat[cell];
            int rain=World.Climate.NorthernSpring.PrecipitationMm[cell]+World.Climate.NorthernSummer.PrecipitationMm[cell]+World.Climate.NorthernAutumn.PrecipitationMm[cell]+World.Climate.NorthernWinter.PrecipitationMm[cell];
            uv=HistoryHumanVariation.UvExposure(World.Surface.Cells[cell].Position.Z,World.ElevationM[cell],rain,AnimalEcotypes.Canopy(h));meanTemperature=h.MeanTenthsC;
        }
        void InitializeFreshHumanVariation(PrehistoryState s)
        {
            if(!LivingWorldHistory(s)||s.Species!="Homo sapiens"||s.Started||s.Years!=0||s.History.Aggregates.TotalPopulation!=0||s.Population[s.Origin]!=0)return;
            HumanAppearanceEnvironment(s.Origin,out _,out double uv,out int mean);
            for(int k=0;k<8;k++)s.Ancestry[s.Origin*8+k]=HistoryHumanVariation.FounderValue(World.Recipe.Seed,s.Origin,(HumanAppearanceTrait)k,uv,mean);
        }
        void UpdateHumanVariation(PrehistoryState s,int region,int elapsed)
        {
            long residents=ProvincialContactResidents(s,region);if(residents<=0||elapsed<=0)return;
            HumanAppearanceEnvironment(region,out _,out double uv,out int mean);
            int culture=s.History.Aggregates.Regions[region].Culture;double buffer=0;
            if(culture>=0)
            {
                var hc=s.History.Cultures[culture];
                // Clothing, diet and shelter mediate exposure. Installed modern
                // networks are only a weak cultural-buffer proxy, not genetics.
                buffer=.4*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Sanitation,s.Years)+.3*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Electricity,s.Years)+.3*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years);
            }
            for(int k=0;k<8;k++){int at=region*8+k;s.Ancestry[at]=HistoryHumanVariation.Evolve(World.Recipe.Seed,region,s.Years,(HumanAppearanceTrait)k,s.Ancestry[at],elapsed,residents,uv,mean,buffer);}
        }
        void MixHumanVariation(PrehistoryState s,int from,int to,long migrants,long previousRemoteResidents)
        {
            CarryRegionalHeritage(s,from,to,migrants,previousRemoteResidents);
            long settled=s.History.Aggregates.Living.Regions[to].SettledPopulation;
            long residents=Math.Max(0,previousRemoteResidents)+Math.Max(0,settled);
            for(int k=0;k<8;k++)s.Ancestry[to*8+k]=HistoryHumanVariation.MixValue(s.Ancestry[to*8+k],residents,s.Ancestry[from*8+k],migrants);
        }
        public HumanAppearance RegionalAppearance(PrehistoryState s,int region)
        {
            if(s==null||region<0||region>=Patches.Length)return new HumanAppearance{Basis="No occupied regional appearance estimate."};
            long residents=LivingWorldHistory(s)?ProvincialContactResidents(s,region,false):s.Population[region]+(s.History?.Aggregates?.Living?.Regions[region].SettledPopulation??0);
            if(residents<=0)return new HumanAppearance{Region=region,Basis="No resident regional population is recorded."};
            return HistoryHumanVariation.Read(s,region,Patches[region].Cell,"Regional inherited appearance averages; individuals vary. Not a racial category or a household census.");
        }
        public HumanAppearance SampleNpcAppearance(PrehistoryState s,int cell,int individualSeed)
        {
            if(cell<0||cell>=CellPatch.Length)return new HumanAppearance{Basis="No valid birthplace."};
            return HistoryHumanVariation.Sample(RegionalAppearance(s,CellPatch[cell]),World.Recipe.Seed,individualSeed);
        }
        public HumanAppearance CultureAppearance(PrehistoryState s,int culture)
        {
            if(s?.Society==null||culture<0||culture>=s.Society.Cultures.Length)return new HumanAppearance{Basis="No cultural appearance sample."};
            var c=s.Society.Cultures[culture];int cell=c.OriginCell;
            if(s.History?.Aggregates!=null){var cities=s.History.Aggregates.Cultures[culture].Cities;if(cities.Count>0)cell=s.Society.Settlements[cities.At(0)].Cell;}
            int region=CellPatch[cell];
            return HistoryHumanVariation.Read(s,region,cell,"A local ancestry sample, not an assertion that every member of this culture looks alike. Individuals and other regions vary.");
        }
    }
}
