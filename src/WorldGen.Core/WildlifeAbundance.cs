using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    /// <summary>Local resource and shelter fractions, independent of graphics quality.
    /// Numerical capacities are prototype ecological priors, not surveyed biomass.</summary>
    public struct WildlifeHabitat
    {
        public double Productivity,Grass,Browse,Fruit,Seed,Nectar,Invertebrates,Detritus;
        public double Canopy,Shrubs,Wetland,Coast,Freshwater,Water,Slope;
        public Biome Biome;
        static double Unit(double x)=>ItemUseRules.Finite(x)?Math.Max(0,Math.Min(1,x)):0;
        public static WildlifeHabitat From(LocalTerrainSample s,HabitatCell climate,double canopy=double.NaN,double slope=0,double temperatureC=double.NaN)
        {
            double trees=Unit(double.IsNaN(canopy)?TreeFlora.HabitatAdmission(s):canopy),shrubs=Unit(s.MidCover.Coverage);
            double grass=Unit(s.Cover.Grasses.X+s.Cover.Grasses.Y+s.Cover.Grasses.Z),herbs=Unit(s.Cover.Herbs.X+s.Cover.Herbs.Y+s.Cover.Herbs.Z);
            bool wet=s.Underwater;double coast=Unit(s.OceanInfluence),fresh=s.WaterKind==LocalWaterKind.River||s.WaterKind==LocalWaterKind.Lake?1:Unit(s.RiverInfluence);
            double steep=Unit((slope-.55)/.65),growth=Unit((climate.WarmTenthsC/10.0+5)/20);
            double season=double.IsNaN(temperatureC)?1:Unit((temperatureC+3)/15);
            return new WildlifeHabitat{Productivity=Math.Max(0,Math.Min(3,climate.ProductivityGCM2Year/600.0)),
                Canopy=trees,Shrubs=shrubs,Water=wet?1:0,Coast=s.WaterKind==LocalWaterKind.Ocean?1:coast,Freshwater=fresh,
                Wetland=Math.Max(fresh,Unit(climate.FloodFractionPermille/1000.0)),Slope=Math.Max(0,slope),Biome=s.Biome,
                Grass=wet?0:Unit((grass+herbs*.35)*(1-steep)*growth*(.15+.85*season)),
                Browse=wet?0:Unit((shrubs*.8+trees*.28+herbs*.15)*(1-steep*.65)*growth*(.45+.55*season)),
                Fruit=wet?0:Unit((trees*.65+shrubs*.55)*growth*(.05+.95*season)),Seed=wet?0:Unit((grass*.7+herbs*.3+trees*.25)*growth*(.4+.6*season)),
                Nectar=wet?0:Unit((herbs+shrubs*.3+grass*.08)*growth*season),
                Invertebrates=Unit((trees*.55+shrubs*.4+grass*.5+herbs*.4+fresh*.35+(wet?.7:0))*growth*(.1+.9*season)),
                Detritus=Unit(trees*.65+shrubs*.25+grass*.25+(wet?.7:0))};
        }
        public void Add(WildlifeHabitat b,double weight)
        {
            Productivity+=b.Productivity*weight;Grass+=b.Grass*weight;Browse+=b.Browse*weight;Fruit+=b.Fruit*weight;
            Seed+=b.Seed*weight;Nectar+=b.Nectar*weight;Invertebrates+=b.Invertebrates*weight;Detritus+=b.Detritus*weight;
            Canopy+=b.Canopy*weight;Shrubs+=b.Shrubs*weight;Wetland+=b.Wetland*weight;Coast+=b.Coast*weight;
            Freshwater+=b.Freshwater*weight;Water+=b.Water*weight;Slope+=b.Slope*weight;
        }
        public double Fit(Taxon t)
        {
            double fit=0;var h=t.Habitats;
            if((h&Habitat.Forest)!=0)fit=Math.Max(fit,Unit(Canopy+Shrubs*.4));
            if((h&Habitat.Open)!=0)fit=Math.Max(fit,Unit(1-Canopy)*Math.Max(Grass,Math.Max(Shrubs,.1)));
            if((h&Habitat.Desert)!=0&&Biome==Biome.Desert)fit=1;
            if((h&Habitat.Tundra)!=0&&(Biome==Biome.Tundra||Biome==Biome.Alpine))fit=1;
            if((h&Habitat.Alpine)!=0&&(Biome==Biome.Alpine||Slope>.5))fit=1;
            if((h&Habitat.Wetland)!=0)fit=Math.Max(fit,Wetland);
            if((h&Habitat.Freshwater)!=0)fit=Math.Max(fit,Freshwater);
            if((h&Habitat.Coastal)!=0)fit=Math.Max(fit,Coast);
            if(t.Lineage?.RequiresCoast==true)fit*=Coast;
            if(t.Lineage?.RequiresFreshwater==true)fit*=Freshwater;
            if(t.Lineage!=null)
            {
                double low=t.Lineage.MinCanopyPermille/1000.0,high=t.Lineage.MaxCanopyPermille/1000.0;
                if(low>0)fit*=Unit((Canopy-low+.15)/.15);
                if(high<1)fit*=Unit((high-Canopy+.15)/.15);
            }
            if((t.Occupies&Stratum.Water)!=0&&(t.Occupies&(Stratum.Ground|Stratum.Air|Stratum.Canopy))==0)fit*=Water;
            else if(Water>.5&&(t.Occupies&(Stratum.Water|Stratum.Air))==0)fit=0;
            return Unit(fit);
        }
    }

    public sealed class WildlifeCommunity
    {
        public readonly Dictionary<int,double> DensityPerKm2=new Dictionary<int,double>();
        public double PrimaryCapacity,PrimaryAllocated,PreyBiomassKg,PredatorSupportedBiomassKg;
        public double Density(int id)=>DensityPerKm2.TryGetValue(id,out var n)?n:0;
    }

    public static class WildlifeAbundance
    {
        public const int Version=2;
        static readonly Food[] PrimaryFoods={Food.Grass,Food.Browse,Food.Fruit,Food.Seed,Food.Nectar,Food.Detritus,Food.Invertebrate};
        public static double EffectiveDayMinutes(double actualMinutes)
            =>ItemUseRules.Finite(actualMinutes)?Math.Max(15,Math.Min(1440,actualMinutes)):1440;
        // Compress distant travel sublinearly, never instantaneous population.
        public static double EncounterPace(double actualMinutes)=>Math.Sqrt(1440/EffectiveDayMinutes(actualMinutes));
        public static double TravelPace(double actualMinutes,double distance,double visibleRadius)
        {
            double start=Math.Max(120,visibleRadius*1.5),end=start+Math.Max(180,visibleRadius);
            double t=Math.Max(0,Math.Min(1,(distance-start)/(end-start)));t=t*t*(3-2*t);
            return 1+(EncounterPace(actualMinutes)-1)*t;
        }
        public static double Demand(double mass)=>Math.Pow(Math.Max(.000001,mass),.75);
        static int DietCount(Food food)
        {int count=0;for(int bit=1;bit<=256;bit<<=1)if(((int)food&bit)!=0)count++;return Math.Max(1,count);}
        public static WildlifeCommunity Allocate(IEnumerable<Taxon> population,WildlifeHabitat habitat)
        {
            var taxa=population.Where(t=>t!=null&&!t.Producer&&!t.BasalResource&&ItemUseRules.Finite(t.MassKg)&&t.MassKg>0).GroupBy(t=>t.Id).Select(g=>g.First()).OrderBy(t=>t.Id).ToArray();
            var result=new WildlifeCommunity();int n=taxa.Length;var fit=new double[n];var density=new double[n];
            for(int i=0;i<n;i++){fit[i]=habitat.Fit(taxa[i]);result.DensityPerKm2[taxa[i].Id]=0;}
            double p=ItemUseRules.Finite(habitat.Productivity)?Math.Max(0,Math.Min(3,habitat.Productivity)):0;
            double Resource(double x)=>ItemUseRules.Finite(x)?Math.Max(0,Math.Min(1,x)):0;
            // Separate accessible foods: canopy biomass is not all edible browse.
            double[] budgets={260*Resource(habitat.Grass)*p,220*Resource(habitat.Browse)*p,12*Resource(habitat.Fruit)*p,16*Resource(habitat.Seed)*p,
                8*Resource(habitat.Nectar)*p,20*Resource(habitat.Detritus)*p,35*Resource(habitat.Invertebrates)*p};
            for(int food=0;food<PrimaryFoods.Length;food++)
            {
                double total=0;for(int i=0;i<n;i++)if((taxa[i].Eats&PrimaryFoods[food])!=0)total+=fit[i]/DietCount(taxa[i].Eats);
                double budget=Math.Max(0,budgets[food]);result.PrimaryCapacity+=budget;if(total<=0)continue;
                for(int i=0;i<n;i++)if((taxa[i].Eats&PrimaryFoods[food])!=0&&fit[i]>0)
                {double share=budget*(fit[i]/DietCount(taxa[i].Eats))/total;density[i]+=share/Demand(taxa[i].MassKg);result.PrimaryAllocated+=share;}
            }
            // Distribute a small supported predator biomass from each eligible
            // prey pool ONCE across competitors, including fish-only diets.
            var predatorDensity=new double[n];
            for(int prey=0;prey<n;prey++)
            {
                double biomass=density[prey]*taxa[prey].MassKg;if(biomass<=0)continue;
                result.PreyBiomassKg+=biomass;double total=0;
                for(int hunter=0;hunter<n;hunter++)if(fit[hunter]>0&&(taxa[hunter].Eats&(Food.Vertebrate|Food.Fish))!=0&&Biogeography.CanEat(taxa[hunter],taxa[prey]))total+=fit[hunter]/DietCount(taxa[hunter].Eats);
                if(total<=0)continue;
                double supported=biomass*.009;result.PredatorSupportedBiomassKg+=supported;
                for(int hunter=0;hunter<n;hunter++)if(fit[hunter]>0&&(taxa[hunter].Eats&(Food.Vertebrate|Food.Fish))!=0&&Biogeography.CanEat(taxa[hunter],taxa[prey]))
                    predatorDensity[hunter]+=supported*(fit[hunter]/DietCount(taxa[hunter].Eats))/total/taxa[hunter].MassKg;
            }
            for(int i=0;i<n;i++)result.DensityPerKm2[taxa[i].Id]=density[i]+predatorDensity[i];
            return result;
        }
        /// <summary>Exposed individuals, not a claim all birds are airborne.
        /// Concealed/resting residents remain implicit in the community budget.</summary>
        public static double ExposedFraction(double mass,bool flying,bool tiny)
            =>tiny?(mass<.00001?.002:.2):flying?(mass>=1?.45:.30):mass<20?.3:1;
    }
}
