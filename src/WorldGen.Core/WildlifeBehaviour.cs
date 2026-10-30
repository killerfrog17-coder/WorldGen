using System;

namespace WorldGen.Core
{
    public enum WildlifeTrackingTier { Fleeting,Local,Resident,Notable }
    public enum WildlifeIntent { Rest,Forage,Travel,Hunt,Drink,Flee,Migrate,Shelter,Rut }
    public sealed class WildlifeBehaviourProfile
    {
        public WildlifeTrackingTier Tier;public bool Predator,PackHunter,Herd,Defensive,Hibernates,Migratory,Perches,Crepuscular;
        public bool HasNaturalHistory,SmallBudget,CanHunt,SocialAlarm,SeasonalRut,ThreatensPrey;
        public WildlifeNaturalHistoryProfile NaturalHistory;
        public double NoticeM,FlightM,DefendM,TrackingM;public int GroupMin=1,GroupMax=1;
    }
    /// <summary>Broad natural-history priors for prototype behaviour. They do
    /// not replace the native habitat/taxon distribution or simulate breeding.</summary>
    public static class WildlifeBehaviour
    {
        public static bool IsNocturnal(string slug,bool catalogued=false)
            =>WildlifeNaturalHistory.TryGet(slug,out var profile)?profile.Diel==WildlifeDiel.Nocturnal:catalogued||slug=="civet"||slug=="badger"||slug=="hedgehog"||slug=="porcupine";
        public static WildlifeBehaviourProfile Describe(string slug,double mass,Food eats,bool flying=false,bool tiny=false,string nativeName="")
        {
            slug=(slug??"").ToLowerInvariant();bool Has(params string[] words){foreach(var word in words)if(slug.Contains(word))return true;return false;}
            bool predator=(eats&Food.Vertebrate)!=0&&!Has("boar","pig","bear","rat","mouse","chicken","duck");
            var p=new WildlifeBehaviourProfile{Predator=predator,Tier=tiny?WildlifeTrackingTier.Fleeting:mass<2?WildlifeTrackingTier.Local:mass<20?WildlifeTrackingTier.Resident:WildlifeTrackingTier.Notable,
                NoticeM=tiny?2:mass<2?12:predator?65:75,FlightM=tiny?1:mass<2?6:predator?20:35,TrackingM=tiny?35:mass<2?220:mass<20?900:2200};
            if(slug=="butterflies-and-moths-guild"||slug=="fireflies-derived-lineage"){p.Tier=WildlifeTrackingTier.Local;p.TrackingM=220;}
            if(Has("wolf","hunting-dog","dhole","hyena")||Has("lion")&&!Has("sea-lion","sea lion","mountain-lion","mountain lion")){p.PackHunter=true;p.GroupMin=3;p.GroupMax=8;p.Tier=WildlifeTrackingTier.Notable;p.TrackingM=2400;}
            if(Has("deer","reindeer","elk","zebra","antelope","gazelle","wildebeest","buffalo","bison","sheep","goat","horse","ibex","musk-ox","musk ox","cattle","mouflon","guanaco","kangaroo","wallaby")){p.Herd=true;p.GroupMin=3;p.GroupMax=Has("zebra","wildebeest","reindeer")?18:8;p.Crepuscular=true;}
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
            p.CanHunt=p.ThreatensPrey=p.Predator;p.SocialAlarm=p.Herd||p.PackHunter;p.SeasonalRut=p.Herd;
            if(WildlifeNaturalHistory.TryGet(slug,out _))
            {
                var n=WildlifeNaturalHistory.Native(slug,nativeName);p.HasNaturalHistory=true;p.NaturalHistory=n;
                p.GroupMin=n.GroupMin;p.GroupMax=n.GroupMax;p.SmallBudget=n.SmallBudget;
                p.PackHunter=n.Social==WildlifeSocial.Pack||slug=="orca";
                p.Herd=n.Social==WildlifeSocial.Herd||n.Social==WildlifeSocial.Family||n.Social==WildlifeSocial.Loose||n.Social==WildlifeSocial.Troop;
                p.SocialAlarm=n.Social!=WildlifeSocial.Solitary;p.SeasonalRut=n.SeasonalRut;
                p.Defensive=n.DefendM>0;p.DefendM=n.DefendM;p.Hibernates=n.Hibernates;p.Crepuscular=n.Diel==WildlifeDiel.Crepuscular;
                bool carnivorous=(eats&(Food.Vertebrate|Food.Fish))!=0;
                p.Predator=carnivorous&&(n.Hunt==WildlifeHuntStyle.Chase||n.Hunt==WildlifeHuntStyle.Stalk||n.Hunt==WildlifeHuntStyle.Ambush||n.Hunt==WildlifeHuntStyle.Aquatic);
                p.CanHunt=carnivorous&&n.Hunt!=WildlifeHuntStyle.None&&n.Hunt!=WildlifeHuntStyle.Scavenge;
                p.ThreatensPrey=carnivorous&&n.Hunt!=WildlifeHuntStyle.None;
                if(n.SmallBudget&&slug!="butterflies-and-moths-guild"&&slug!="fireflies-derived-lineage")p.Tier=WildlifeTrackingTier.Fleeting;
                if(p.PackHunter){p.Tier=WildlifeTrackingTier.Notable;p.TrackingM=Math.Max(p.TrackingM,2400);}
            }
            return p;
        }
        public static double Winter(double year,double latitude)=>Math.Cos((year-Math.Floor(year))*Math.PI*2)*(latitude>=0?1:-1);
        // This terrain sample does not carry lake salinity; freshwater here
        // means river/lake water, never ocean or a dry influence footprint.
        public static bool DrinkingWater(LocalTerrainSample sample)
            =>(sample.WaterKind==LocalWaterKind.River||sample.WaterKind==LocalWaterKind.Lake)&&
              ItemUseRules.Finite(sample.ElevationM)&&ItemUseRules.Finite(sample.WaterSurfaceM)&&
              ItemUseRules.Finite(sample.WaterMaskM)&&sample.Underwater;
        public static bool EdiblePrey(double predatorMass,bool pack,double minimum,double maximum,double preyMass)
        {
            if(!ItemUseRules.Finite(predatorMass)||predatorMass<=0||!ItemUseRules.Finite(preyMass)||preyMass<=0||
                !ItemUseRules.Finite(minimum)||minimum<0||!ItemUseRules.Finite(maximum)||maximum<0)return false;
            // Native ecological limits are actual bounds, as in Biogeography.
            // Estimate from body/pack size only when no upper limit is supplied.
            double upper=maximum>0?maximum:predatorMass*(pack?8:1.5);
            return preyMass>=minimum&&preyMass<=upper;
        }
        public static bool Dormant(WildlifeBehaviourProfile p,double year,double latitude,double nativeColdC,double temperatureC)
            =>p.Hibernates&&Math.Abs(latitude)>25&&nativeColdC<4&&temperatureC<8&&Winter(year,latitude)>.25;
        public static bool SeasonallyPresent(WildlifeBehaviourProfile p,double year,double latitude,double nativeColdC)
            =>!p.Migratory||Math.Abs(latitude)<28||nativeColdC>2||Winter(year,latitude)<.45;
        public static bool Rut(double year,double latitude)
        {double season=(year+(latitude<0?.5:0))%1;return season>.65&&season<.91;}
        public static bool Rut(WildlifeBehaviourProfile profile,double year,double latitude)
        {
            if(!profile.SeasonalRut||Math.Abs(latitude)<=20)return false;
            if(!profile.HasNaturalHistory)return Rut(year,latitude);
            double season=(year+(latitude<0?.5:0))%1;
            return season>profile.NaturalHistory.RutStart&&season<profile.NaturalHistory.RutEnd;
        }
        public static double MigrationHeading(double year,double latitude)
        {
            // Follow the change of season, not whether today's season is cold.
            // Otherwise a flock reverses direction halfway through spring/fall.
            bool north=latitude>=0,poleward=Math.Sin(year*Math.PI*2)*(north?1:-1)>0;
            return poleward==north?0:Math.PI;
        }
        public static double Activity(WildlifeBehaviourProfile p,double hour,bool nocturnal,double rain,double wind,double daylight=double.NaN)
        {
            double light=double.IsNaN(daylight)?(hour>=6&&hour<18?1:0):Math.Max(0,Math.Min(1,daylight)),activity=nocturnal?1-light:light;
            if(p.HasNaturalHistory)
            {
                switch(p.NaturalHistory.Diel)
                {
                    case WildlifeDiel.Cathemeral:activity=.6+.2*Math.Cos(hour*Math.PI/3);break;
                    case WildlifeDiel.Nocturnal:activity=.1+.9*(1-light);break;
                    case WildlifeDiel.Diurnal:activity=.12+.88*light;break;
                    case WildlifeDiel.Crepuscular:activity=double.IsNaN(daylight)?.25+.75*Math.Max(Math.Exp(-Math.Pow((hour-6)/2,2)),Math.Exp(-Math.Pow((hour-18)/2,2))):.25+.75*Math.Exp(-Math.Pow((light-.2)/.25,2));break;
                }
            }
            else if(p.Crepuscular)activity=.3+.7*Math.Max(Math.Exp(-Math.Pow((hour-6)/2,2)),Math.Exp(-Math.Pow((hour-18)/2,2)));
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
