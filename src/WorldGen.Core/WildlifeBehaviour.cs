using System;

namespace WorldGen.Core
{
    public enum WildlifeTrackingTier { Fleeting,Local,Resident,Notable }
    public enum WildlifeIntent { Rest,Forage,Travel,Hunt,Drink,Flee,Migrate,Shelter,Rut }
    public sealed class WildlifeBehaviourProfile
    {
        public WildlifeTrackingTier Tier;public bool Predator,PackHunter,Herd,Defensive,Hibernates,Migratory,Perches,Crepuscular;
        public double NoticeM,FlightM,DefendM,TrackingM;public int GroupMin=1,GroupMax=1;
    }
    /// <summary>Broad natural-history priors for prototype behaviour. They do
    /// not replace the native habitat/taxon distribution or simulate breeding.</summary>
    public static class WildlifeBehaviour
    {
        public static WildlifeBehaviourProfile Describe(string slug,double mass,Food eats,bool flying=false,bool tiny=false)
        {
            slug=(slug??"").ToLowerInvariant();bool Has(params string[] words){foreach(var word in words)if(slug.Contains(word))return true;return false;}
            bool predator=(eats&Food.Vertebrate)!=0&&!Has("boar","pig","bear","rat","mouse","chicken","duck");
            var p=new WildlifeBehaviourProfile{Predator=predator,Tier=tiny?WildlifeTrackingTier.Fleeting:mass<2?WildlifeTrackingTier.Local:mass<20?WildlifeTrackingTier.Resident:WildlifeTrackingTier.Notable,
                NoticeM=tiny?2:mass<2?12:predator?65:75,FlightM=tiny?1:mass<2?6:predator?20:35,TrackingM=tiny?35:mass<2?220:mass<20?900:2200};
            if(Has("wolf","hunting-dog","dhole","hyena")||Has("lion")&&!Has("sea-lion","sea lion","mountain-lion","mountain lion")){p.PackHunter=true;p.GroupMin=3;p.GroupMax=8;p.Tier=WildlifeTrackingTier.Notable;p.TrackingM=2400;}
            if(Has("deer","reindeer","elk","zebra","antelope","gazelle","wildebeest","buffalo","bison","sheep","goat","horse","ibex","musk-ox","musk ox","cattle","mouflon","guanaco")){p.Herd=true;p.GroupMin=3;p.GroupMax=Has("zebra","wildebeest","reindeer")?18:8;p.Crepuscular=true;}
            if(Has("moose")){p.Herd=false;p.GroupMin=p.GroupMax=1;p.Defensive=true;p.DefendM=5;}
            if(Has("elk","bison","buffalo","boar","warthog","rhinoceros","hippopotamus","elephant")){p.Defensive=true;p.DefendM=Has("hippopotamus","rhinoceros","elephant")?9:4;}
            if(Has("bear")){p.Predator=Has("polar");p.Defensive=true;p.DefendM=6;p.Hibernates=Has("brown","black")||slug.StartsWith("bear");}
            if(Has("crocodile","alligator")){p.Defensive=true;p.DefendM=3;}
            if(flying&&!tiny)
            {
                p.Perches=!Has("swift","albatross","frigate","goose","duck","swan","flamingo","pelican","crane","bat","flying-fox");
                p.Migratory=Has("goose","swan","crane","stork","swallow","tern");
                if(p.Migratory){p.GroupMin=4;p.GroupMax=12;p.TrackingM=2000;p.Tier=WildlifeTrackingTier.Resident;}
            }
            return p;
        }
        public static double Winter(double year,double latitude)=>Math.Cos((year-Math.Floor(year))*Math.PI*2)*(latitude>=0?1:-1);
        public static bool EdiblePrey(double predatorMass,bool pack,double minimum,double maximum,double preyMass)
            =>preyMass>Math.Max(.001,minimum)&&preyMass<Math.Max(predatorMass*(pack?8:1.5),maximum);
        public static bool Dormant(WildlifeBehaviourProfile p,double year,double latitude,double nativeColdC,double temperatureC)
            =>p.Hibernates&&Math.Abs(latitude)>25&&nativeColdC<4&&temperatureC<8&&Winter(year,latitude)>.25;
        public static bool SeasonallyPresent(WildlifeBehaviourProfile p,double year,double latitude,double nativeColdC)
            =>!p.Migratory||Math.Abs(latitude)<28||nativeColdC>2||Winter(year,latitude)<.45;
        public static bool Rut(double year,double latitude)
        {double season=(year+(latitude<0?.5:0))%1;return season>.65&&season<.91;}
        public static double Activity(WildlifeBehaviourProfile p,double hour,bool nocturnal,double rain,double wind)
        {
            double light=hour>=6&&hour<18?1:.25,activity=nocturnal?1.25-light:light;
            if(p.Crepuscular)activity=.3+.7*Math.Max(Math.Exp(-Math.Pow((hour-6)/2,2)),Math.Exp(-Math.Pow((hour-18)/2,2)));
            return Math.Max(.1,activity/(1+Math.Max(0,rain)*.04+Math.Max(0,wind-8)*.025));
        }
    }
    [Serializable] public sealed class WildlifeGroup
    {
        public ulong Id;public string Species="";public V Home,Position,Goal;public WildlifeIntent Intent;
        public double Heading,LastSolarDays,NextDecisionDays,Hunger,Thirst;public int Decision,Members=1;
        [System.Runtime.Serialization.OptionalField] public int Taxon=-1;
        [System.Runtime.Serialization.OptionalField] public int Sex=-1;
        public WildlifeGroup Copy()=>(WildlifeGroup)MemberwiseClone();
        public void Validate()
        {
            bool Unit(V v)=>ItemUseRules.Finite(v.Length)&&Math.Abs(v.Length-1)<1e-5;
            if(Id==0||string.IsNullOrEmpty(Species)||!Unit(Home)||!Unit(Position)||!Unit(Goal)||!ItemUseRules.Finite(Heading)||!ItemUseRules.Finite(LastSolarDays)||LastSolarDays<0||!ItemUseRules.Finite(NextDecisionDays)||NextDecisionDays<0||!ItemUseRules.Finite(Hunger)||Hunger<0||Hunger>1||!ItemUseRules.Finite(Thirst)||Thirst<0||Thirst>1||Decision<0||Members<1||Sex< -1||Sex>1||!Enum.IsDefined(typeof(WildlifeIntent),Intent))throw new ArgumentException("Invalid wildlife group.");
        }
    }
}
