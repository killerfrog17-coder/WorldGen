using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    /// <summary>Derived gameplay yields for the actual placed regional species.
    /// Not an identification guide or a claim about an Earth species.</summary>
    public sealed class ForageProduct
    {
        public string Key,Species;public PossessionKind Kind;public double MassKg,KcalPerKg,WaterPerKg,Toxicity,DelayHours,SeasonCentre,SeasonWidth;public V Tint;
        public bool Available(double year,double latitude,double temperature)
        {
            if(Kind==PossessionKind.ReedBundle||Kind==PossessionKind.RawFibre||Kind==PossessionKind.LeafLitter)return true;
            if(temperature< -3)return false;
            double phase=year+(latitude<0?.5:0);phase-=Math.Floor(phase);
            double d=Math.Abs(phase-SeasonCentre);d=Math.Min(d,1-d);
            return Math.Abs(latitude)<23?d<.43:d<SeasonWidth;
        }
    }
    public static class Foraging
    {
        public static bool Food(Possession p)=>p!=null&&p.Kind>=PossessionKind.Berries&&p.Kind<=PossessionKind.AnimalFat;
        public static string Name(Possession p)
        {
            if(!Food(p))return null;
            string name=PortableTechnology.Item(p.Kind)?.Name??p.Kind.ToString();
            if(p.Cooked)name=name.Replace("Raw ","").Replace("raw ","");
            string prefix=p.Burnt?"Charred ":p.Cooked?"Cooked ":"";
            if(p.Kind==PossessionKind.RawMeat||p.Kind==PossessionKind.RawFish||p.Kind==PossessionKind.AnimalFat)return prefix+(string.IsNullOrEmpty(p.AppearanceSpecies)?name:PortableTechnology.SpeciesName(p.AppearanceSpecies)+" "+name.ToLowerInvariant());
            return prefix+(string.IsNullOrEmpty(p.FoodSpecies)?name:p.FoodSpecies+" "+name.ToLowerInvariant());
        }
        static ForageProduct Product(string species,int code,PossessionKind kind,double kg,double kcal,double water,double toxin,V tint,string part="food")
        {
            var r=new Rng(code,"forage-products-1");
            return new ForageProduct{Key=code+"/"+part,Species=species,Kind=kind,MassKg=kg,KcalPerKg=kcal,WaterPerKg=water,Toxicity=toxin,DelayHours=kind==PossessionKind.Mushroom?6+12*r.Unit():.5+3*r.Unit(),Tint=tint,
                SeasonCentre=kind==PossessionKind.Nuts?.74:kind==PossessionKind.Mushroom?.68:.51+.16*r.Unit(),SeasonWidth=kind==PossessionKind.EdibleRoot?.36:.10+.10*r.Unit()};
        }
        public static ForageProduct[] Products(MidPlantSpecies s,double width)
        {
            var m=s.Morphology;var r=m.Reproduction;var result=new List<ForageProduct>();double size=Math.Max(.25,Math.Min(2,width));
            if(s.Form==MidGrowthForm.ReedStand)
                result.Add(Product(s.Name,s.MorphologyCode,PossessionKind.ReedBundle,.3,0,0,0,m.Leaf,"reeds"));
            else if(s.Form==MidGrowthForm.LeafyBush||s.Form==MidGrowthForm.OpenScrub||s.Form==MidGrowthForm.ArchingCane)
                result.Add(Product(s.Name,s.MorphologyCode,PossessionKind.RawFibre,.18,0,0,0,m.Leaf,"fibre"));
            if(r.Fruit==MidFruitForm.Berry||r.Fruit==MidFruitForm.FleshyDrupe)
                result.Add(Product(s.Name,s.MorphologyCode,PossessionKind.Berries,.08+.12*size,350+400*r.FruitNutrition,.82,r.FleshToxicity,m.Fruit));
            return result.ToArray();
        }
        public static ForageProduct[] Products(TreeSpecies s)
        {
            var r=s.Reproduction;
            var leaves=Product(s.Name,s.MorphologyCode,PossessionKind.LeafLitter,.4,0,0,0,s.Morphology.Leaf,"leaf-litter");
            if(r.Fruit!=TreeFruitForm.FleshyFruit&&r.Fruit!=TreeFruitForm.Nut)return new[]{leaves};
            bool nut=r.Fruit==TreeFruitForm.Nut;
            return new[]{leaves,Product(s.Name,s.MorphologyCode,nut?PossessionKind.Nuts:PossessionKind.Fruit,nut?.16:.28,nut?4800+1200*r.SeedNutrition:450+250*r.SeedNutrition,nut?.06:.8,nut?r.SeedToxicity:r.FleshToxicity,s.Morphology.Fruit)};
        }
        public static ForageProduct[] Products(GroundSpecialist s)
        {
            var r=new Rng(s.MorphologyCode&~15,"ground-food-1");
            if(s.Form==SpecialistForm.Fungus)
                return new[]{Product(s.Name,s.MorphologyCode,PossessionKind.Mushroom,.035+.065*r.Unit(),220+120*r.Unit(),.89,r.Unit()<.55?0:.2+.8*r.Unit(),s.Morphology.Blossom)};
            if(s.Form==SpecialistForm.Rosette)
                return new[]{Product(s.Name,s.MorphologyCode,PossessionKind.RawFibre,.018+s.HeightM*.025,0,0,0,s.Morphology.Leaf,"fibre"),Product(s.Name,s.MorphologyCode,PossessionKind.EdibleRoot,.12+.12*r.Unit(),650+450*r.Unit(),.73,r.Unit()<.65?0:.15+.75*r.Unit(),new V(.64,.49,.29),"root")};
            if(s.Form==SpecialistForm.Spike)return new[]{Product(s.Name,s.MorphologyCode,PossessionKind.RawFibre,.018+s.HeightM*.025,0,0,0,s.Morphology.Leaf,"fibre")};
            return Array.Empty<ForageProduct>();
        }
        public static Possession Gather(ForageProduct product,V position,double fraction=1)
        {
            var p=PortableTechnology.Create(product.Kind,position);p.FoodSpecies=product.Species;p.FoodKcalPerKg=product.KcalPerKg;p.FoodWaterPerKg=product.WaterPerKg;p.FoodToxicity=product.Toxicity;p.FoodDelayHours=product.DelayHours;p.Tint=product.Tint;
            Resize(p,product.MassKg*fraction);return p;
        }
        public static void Resize(Possession p,double mass)
        {
            double scale=Math.Pow(Math.Max(.00001,mass)/p.MassKg,1d/3);p.WidthM*=scale;p.HeightM*=scale;p.DepthM*=scale;p.MassKg=Math.Max(.00001,mass);
            p.CellsWide=Math.Max(1,(int)Math.Ceiling(p.WidthM/.1));p.CellsHigh=Math.Max(1,(int)Math.Ceiling(p.HeightM/.1));
        }
        public static double Consume(PlayerSurvival state,Possession p,bool sample)
        {
            if(!Food(p)||state.Incapacitated)return 0;
            double mass=Math.Min(p.MassKg,sample?.005:p.Kind==PossessionKind.Nuts?.06:p.Kind==PossessionKind.AnimalFat?.06:.25);
            double kcal=mass*p.FoodKcalPerKg*(p.Burnt?.5:1);
            PlayerMetabolism.Eat(state,mass,kcal,mass*p.FoodWaterPerKg);
            state.Energy=Math.Min(100,state.Energy+kcal/150);state.Stamina=Math.Min(state.Stamina,state.MaximumStamina);
            if(p.FoodToxicity>0)
            {
                state.FoodExposures=state.FoodExposures??new List<FoodExposure>();
                state.FoodExposures.Add(new FoodExposure{Source=p.FoodSpecies??p.Name,DelaySeconds=p.FoodDelayHours*3600,RemainingDamage=mass*p.FoodToxicity*(p.Kind==PossessionKind.Mushroom?1200:200),RemainingSeconds=10800});
            }
            p.MassKg=Math.Max(0,p.MassKg-mass);return mass;
        }
        public static void AdvancePoison(PlayerSurvival state,double simulatedSeconds)
        {
            if(simulatedSeconds<=0||state.Dead||state.FoodExposures==null)return;
            foreach(var e in state.FoodExposures)
            {
                double active=Math.Max(0,simulatedSeconds-e.DelaySeconds);e.DelaySeconds=Math.Max(0,e.DelaySeconds-simulatedSeconds);
                if(active<=0||e.RemainingSeconds<=0)continue;
                double dt=Math.Min(active,e.RemainingSeconds),damage=e.RemainingDamage*dt/e.RemainingSeconds;e.RemainingDamage-=damage;e.RemainingSeconds-=dt;
                state.Health=Math.Max(0,state.Health-damage);PlayerMetabolism.Hydrate(state,-damage*.0077);state.Energy=Math.Max(0,state.Energy-damage*.25);
                if(state.Dead){state.DeathCause=PlayerDeathCause.Poison;state.Unconscious=false;}
            }
            state.FoodExposures.RemoveAll(e=>e.RemainingSeconds<=0||e.RemainingDamage<1e-9);
        }
        public static void ValidateFood(Possession p)
        {
            if(!ItemUseRules.Finite(p.CookingSeconds)||p.CookingSeconds<0||p.CookingSeconds>1e9)throw new ArgumentException("Invalid cooking state.");
            if(p.FoodSpecies!=null&&p.FoodSpecies.Length>160||!ItemUseRules.Finite(p.FoodKcalPerKg)||p.FoodKcalPerKg<0||p.FoodKcalPerKg>10000||!ItemUseRules.Finite(p.FoodWaterPerKg)||p.FoodWaterPerKg<0||p.FoodWaterPerKg>1||!ItemUseRules.Finite(p.FoodToxicity)||p.FoodToxicity<0||p.FoodToxicity>1||!ItemUseRules.Finite(p.FoodDelayHours)||p.FoodDelayHours<0||p.FoodDelayHours>72)throw new ArgumentException("Invalid food state.");
        }
        public static List<Possession> AnimalLoot(ulong id,string species,string medium,double mass,V position,int coat,int seed,bool bird,bool mammal,bool antlers)
        {
            var result=new List<Possession>();mass=Math.Max(.00001,mass);
            Possession Item(PossessionKind kind,double kg)
            {
                var p=PortableTechnology.Create(kind,position,species,coat,seed,id);Resize(p,kg);p.Place=PossessionPlace.Corpse;p.CorpseOwner=id;result.Add(p);return p;
            }
            // Edible tissue, not total dressed carcass weight; portions remain
            // physical separate objects. Hide/bone yields scale with body mass.
            double meat=mass*(medium=="water"?.4:.32),portion=Math.Min(Math.Max(.75,mass*.005),Math.Max(.005,meat));
            while(meat>1e-8)
            {double kg=Math.Min(portion,meat);var p=Item(medium=="water"?PossessionKind.RawFish:PossessionKind.RawMeat,kg);p.FoodKcalPerKg=medium=="water"?1100:1200;p.FoodWaterPerKg=.7;meat-=kg;}
            if(mass>=.2&&mammal)Item(PossessionKind.RawHide,Math.Min(8,mass*.06));
            if(mass>=.1){Item(PossessionKind.Bone,Math.Min(1,mass*.025));if(mammal)Item(PossessionKind.Sinew,Math.Min(.3,mass*.003));}
            if(bird)Item(PossessionKind.Feather,Math.Min(.08,mass*.03));
            if(antlers&&mass>=10)Item(PossessionKind.Antler,Math.Min(3,mass*.012));
            if(mass>=5){var fat=Item(PossessionKind.AnimalFat,mass*.015);fat.FoodKcalPerKg=8500;fat.FoodWaterPerKg=.05;}
            return result;
        }
    }
    [Serializable] public sealed class FoodExposure
    {
        public string Source="";public double DelaySeconds,RemainingDamage,RemainingSeconds;
        [System.Runtime.Serialization.OptionalField] public bool SymptomsNotified;
        public FoodExposure Copy()=>(FoodExposure)MemberwiseClone();
        public void Validate(){if(Source==null||Source.Length>200||new[]{DelaySeconds,RemainingDamage,RemainingSeconds}.Any(v=>!ItemUseRules.Finite(v)||v<0||v>1e8))throw new ArgumentException("Invalid food exposure.");}
    }
}
