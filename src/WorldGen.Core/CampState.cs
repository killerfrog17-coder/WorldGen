using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    public enum CampKind { Fire,Bedding,Stake,FrameWood,Blueprint }
    [Serializable] public sealed class CampObject
    {
        public string Id="camp-"+Guid.NewGuid().ToString("N");public CampKind Kind;public V Position;
        [System.Runtime.Serialization.OptionalField] public long NpcGroup,NpcSite,NpcHousehold;
        [System.Runtime.Serialization.OptionalField] public bool NpcNeedsInitialStock;
        [System.Runtime.Serialization.OptionalField] public double WeatheredThroughCalendarDay=-1;
        [System.Runtime.Serialization.OptionalField] public double ClearedWidthM,ClearedDepthM;
        public double Heading,EmbedM,SoilDepthM,SoilFirmness=1,Wetness,EmberSeconds,LastSimulatedSeconds;
        public bool Burning;public double AshKg;
        [System.Runtime.Serialization.OptionalField] public double StoneHeatJ;
        public List<Possession> Materials=new List<Possession>();
        [System.Runtime.Serialization.OptionalField] public List<Possession> CookingItems=new List<Possession>();
        // Axis components are east/up/north, not renderer or tile coordinates.
        [System.Runtime.Serialization.OptionalField] public V FrameAxis;
        [System.Runtime.Serialization.OptionalField] public double FrameHeightM,FrameRoll;
        [System.Runtime.Serialization.OptionalField] public bool FrameGrounded;
        [System.Runtime.Serialization.OptionalField] public List<FrameJoint> FrameJoints=new List<FrameJoint>();
        [System.Runtime.Serialization.OptionalField] public CampBlueprint Blueprint;
        public double MassKg=>Materials.Sum(p=>p.MassKg)+(FrameJoints??new List<FrameJoint>()).Sum(j=>(j.Bindings??new List<Possession>()).Sum(p=>p.MassKg))+(Blueprint?.Parts?.Sum(s=>s.Materials.Sum(p=>p.MassKg))??0);
        public double FuelKg=>Materials.Where(CampRules.Fuel).Sum(p=>p.MassKg);
        public double BeddingDepthM=>Kind==CampKind.Bedding?Math.Min(.4,Materials.Sum(p=>p.MassKg/(p.Kind==PossessionKind.LeafLitter?45:p.Kind==PossessionKind.ReedBundle?40:p.Kind==PossessionKind.WovenMat?75:CampRules.Hide(p)?650:32))/1.8):0;
        public double Stability=>Kind!=CampKind.Stake||Materials.Count==0?0:D.Clamp(EmbedM/Math.Max(.08,Materials[0].HeightM*.2)*Math.Sqrt(Materials[0].WidthM/.03)*SoilFirmness);
        public double FuelEnergyKg {get{double sum=0;foreach(var p in Materials)if(CampRules.Fuel(p))sum+=p.MassKg*CampRules.FuelEnergy(p);return sum;}}
        public double FirePowerKw=>!Burning?EmberSeconds>0?.8*D.Clamp(EmberSeconds/1800):0:8*(1-Math.Exp(-FuelEnergyKg/2))*(1-Wetness*.7);
        public IEnumerable<Possession> OwnedMaterials()=>Materials.Concat(CookingItems??Enumerable.Empty<Possession>()).Concat((FrameJoints??new List<FrameJoint>()).SelectMany(j=>j.Bindings??new List<Possession>())).Concat(Blueprint?.Parts.SelectMany(p=>p.Materials)??Enumerable.Empty<Possession>());
        public CampObject Copy(){var c=(CampObject)MemberwiseClone();c.Materials=Materials.Select(p=>p.Copy()).ToList();c.CookingItems=(CookingItems??new List<Possession>()).Select(p=>p.Copy()).ToList();c.FrameJoints=(FrameJoints??new List<FrameJoint>()).Select(j=>j.Copy()).ToList();c.Blueprint=Blueprint?.Copy();return c;}
    }
    [Serializable] public sealed class CampState
    {
        public double SimulatedSeconds;
        public List<CampObject> Objects=new List<CampObject>();
        public CampState Copy()=>new CampState{SimulatedSeconds=SimulatedSeconds,Objects=Objects.Select(c=>c.Copy()).ToList()};
        public void Validate()
        {
            if(Objects==null||!double.IsFinite(SimulatedSeconds)||SimulatedSeconds<0)throw new ArgumentException("Invalid camp clock or placed materials.");var ids=new HashSet<string>();var materials=new HashSet<string>();
            foreach(var c in Objects)
            {
                bool Finite(double n)=>double.IsFinite(n);
                if(c==null||string.IsNullOrEmpty(c.Id)||!ids.Add(c.Id)||!Enum.IsDefined(typeof(CampKind),c.Kind)||!Finite(c.Position.Length)||Math.Abs(c.Position.Length-1)>1e-5||!Finite(c.Heading)||!Finite(c.EmbedM)||c.EmbedM<0||!Finite(c.SoilDepthM)||c.SoilDepthM<0||!Finite(c.SoilFirmness)||c.SoilFirmness<=0||!Finite(c.Wetness)||c.Wetness<0||c.Wetness>1||!Finite(c.EmberSeconds)||c.EmberSeconds<0||!Finite(c.StoneHeatJ)||c.StoneHeatJ<0||!Finite(c.AshKg)||c.AshKg<0||!Finite(c.WeatheredThroughCalendarDay)||c.WeatheredThroughCalendarDay< -1||!Finite(c.LastSimulatedSeconds)||c.LastSimulatedSeconds<0||c.Materials==null)throw new ArgumentException("Invalid placed camp object.");
                if(c.CookingItems==null)c.CookingItems=new List<Possession>();
                if(!Finite(c.ClearedWidthM)||!Finite(c.ClearedDepthM)||c.ClearedWidthM<0||c.ClearedDepthM<0||c.ClearedWidthM>100||c.ClearedDepthM>100)throw new ArgumentException("Invalid camp ground footprint.");
                if(c.FrameJoints==null)c.FrameJoints=new List<FrameJoint>();
                if(c.FrameJoints.Any(j=>j==null||j.Bindings==null))throw new ArgumentException("Invalid frame joint.");
                CampBlueprints.Validate(c);
                foreach(var p in c.OwnedMaterials())if(p==null||string.IsNullOrEmpty(p.Id)||!materials.Add(p.Id)||!Finite(p.MassKg)||p.MassKg<=0||!Finite(p.HeightM)||p.HeightM<=0||!Finite(p.WidthM)||p.WidthM<=0)throw new ArgumentException("Invalid camp material.");
                foreach(var p in c.CookingItems)PaleolithicWater.Validate(p);
                if(c.CookingItems.Any(p=>!CampRules.HeatItem(p)||!Finite(p.CookingSeconds)||p.CookingSeconds<0))throw new ArgumentException("Invalid camp food.");
                if(c.Kind==CampKind.Stake&&(c.Materials.Count!=1||c.EmbedM>c.Materials[0].HeightM*.7+.001))throw new ArgumentException("Invalid planted stick.");
            }
            FrameRules.Validate(this);
        }
    }
    public static class CampRules
    {
        public static bool FireStone(Possession p)=>StoneCrafting.Rock(p)&&p.MassKg>=.1&&p.MassKg<=12;
        // The same dry materials can make a fire lay or catch from hot coals.
        // Wetness is checked on the actual fire, not inferred from the item name.
        public static bool Tinder(Possession p)=>p!=null&&(p.Kind==PossessionKind.TinderBundle||p.Kind==PossessionKind.DryGrass||p.Kind==PossessionKind.PreparedFibre||p.Kind==PossessionKind.RawFibre||p.Kind==PossessionKind.BarkStrips||p.Kind==PossessionKind.LeafLitter||p.Kind==PossessionKind.ReedBundle);
        public static bool Kindling(Possession p)=>p!=null&&(p.Kind==PossessionKind.KindlingBundle||WoodCrafting.Wood(p)&&p.WidthM<=.025);
        public static bool Fuel(Possession p)=>p!=null&&(Tinder(p)||Kindling(p)||WoodCrafting.Wood(p)||p.Kind==PossessionKind.BarkSheet||p.Kind==PossessionKind.LeafLitter||p.Kind==PossessionKind.ReedBundle);
        public static double FuelEnergy(Possession p)=>WoodCrafting.Wood(p)||p.Kind==PossessionKind.KindlingBundle?1:p.Kind==PossessionKind.BarkSheet?.85:p.Kind==PossessionKind.ReedBundle?.4:.22;
        public static bool Cookable(Possession p)=>p!=null&&(p.Kind==PossessionKind.RawMeat||p.Kind==PossessionKind.RawFish||p.Kind==PossessionKind.EdibleRoot||p.Kind==PossessionKind.Mushroom||p.Kind==PossessionKind.PlantCake);
        public static double CookingDuration(Possession p)=>Math.Max(240,(p.Kind==PossessionKind.EdibleRoot?1200:600)*Math.Pow(Math.Max(.03,p.MassKg)/.25,.35));
        public static bool HeatItem(Possession p)=>Cookable(p)||PaleolithicClay.Green(p)||CampWaterHeating.DirectVessel(p);
        public static bool NeedsHeat(Possession p)=>PaleolithicClay.Green(p)?p.Dried:CampWaterHeating.DirectVessel(p)?p.WaterLitres>0&&!p.WaterBoiled:Cookable(p)&&!p.Cooked&&!p.Burnt;
        public static string HeatLabel(Possession p)=>PaleolithicClay.Green(p)?"Fire ":CampWaterHeating.DirectVessel(p)?"Boil water in ":"Cook ";
        public static double HeatProgress(Possession p)=>PaleolithicClay.Green(p)?p.CookingSeconds/PaleolithicClay.FiringSeconds:CampWaterHeating.DirectVessel(p)?p.WaterBoiled?1:p.CookingSeconds/(120+240*p.WaterLitres):p.CookingSeconds/CookingDuration(p);
        public static bool Bedding(Possession p)=>p!=null&&(p.Kind==PossessionKind.WovenMat||p.Kind==PossessionKind.DryGrass||p.Kind==PossessionKind.LeafLitter||p.Kind==PossessionKind.ReedBundle||p.Kind==PossessionKind.RawHide||p.Kind==PossessionKind.ScrapedHide||p.Kind==PossessionKind.WorkedHide);
        public static bool Hide(Possession p)=>p!=null&&(p.Kind==PossessionKind.RawHide||p.Kind==PossessionKind.ScrapedHide||p.Kind==PossessionKind.WorkedHide);
        public static bool Plantable(Possession p)=>WoodCrafting.Wood(p)&&p.HeightM>=.35&&p.WidthM<=.09&&p.WoodSoundness>=.35;
        public static string IgniteReason(CampObject fire,bool drillAvailable)
        {
            if(fire==null||fire.Kind!=CampKind.Fire)return "Choose a fire lay.";
            if(fire.Burning)return "The fire is already burning.";
            if(fire.EmberSeconds<=0&&!drillAvailable)return "Requires a hand-drill set, or live embers.";
            if(fire.Wetness>.45)return "The tinder is too wet; let it dry.";
            if(fire.EmberSeconds>0&&fire.Materials.Where(Tinder).Sum(p=>p.MassKg)>=.005)return "";
            if(fire.EmberSeconds>0&&fire.Materials.Where(Kindling).Sum(p=>p.MassKg)>=.01)return "";
            if(fire.EmberSeconds>=300&&fire.Materials.Where(p=>WoodCrafting.Wood(p)&&p.WidthM<=.045).Sum(p=>p.MassKg)>=.03)return "";
            if(fire.Materials.Where(Tinder).Sum(p=>p.MassKg)<.025)return "Add fine dry tinder.";
            if(fire.Materials.Where(Kindling).Sum(p=>p.MassKg)<.1)return "Add thin kindling before larger fuel.";
            return "";
        }
        public static void Add(CampObject c,Possession material)
        {
            if(material==null||material.MassKg<=0||c.Materials.Any(p=>p.Id==material.Id)||!(c.Kind==CampKind.Fire?Fuel(material)||FireStone(material):c.Kind==CampKind.Bedding?Bedding(material):c.Kind==CampKind.FrameWood?FrameRules.Material(material):c.Kind==CampKind.Stake&&Plantable(material))||(c.Kind==CampKind.Stake||c.Kind==CampKind.FrameWood)&&c.Materials.Count>0)throw new ArgumentException("Unsuitable placed material.");
            var placed=material.Copy();placed.Place=PossessionPlace.Ground;placed.ContainerId="";placed.GroundMoving=false;placed.InFlight=false;placed.GroundVelocity=placed.GroundSpin=new V();
            if(c.Kind==CampKind.Fire&&WoodCrafting.Wood(placed)&&placed.HeightM>.5)
            {
                int count=(int)Math.Ceiling(placed.HeightM/.42);
                for(int i=0;i<count;i++){var piece=placed.Copy();piece.Id=placed.Id+(i==0?"":"-fuel-"+i);piece.NaturalId=0;piece.HeightM/=count;piece.MassKg/=count;piece.CellsHigh=Math.Max(1,(int)Math.Ceiling(piece.HeightM/.1));c.Materials.Add(piece);}
            }
            else c.Materials.Add(placed);
            // Feeding usable coals is the relighting action. Requiring the
            // already-burned starter bundle plus another menu ignition made
            // apparently live fires impossible to revive with fine dry fuel.
            if(c.Kind==CampKind.Fire&&!c.Burning&&c.EmberSeconds>0&&IgniteReason(c,false).Length==0)Ignite(c,false);
        }
        public static void Ignite(CampObject c,bool drill)
        {string reason=IgniteReason(c,drill);if(reason.Length>0)throw new InvalidOperationException(reason);c.Burning=true;c.EmberSeconds=Math.Max(c.EmberSeconds,1800);}
        public static void Advance(CampObject c,double seconds,double rain,double humidity,double wind,double sun)
        {
            if(!double.IsFinite(seconds)||seconds<0||seconds>1e12)throw new ArgumentOutOfRangeException(nameof(seconds));
            if(c.Kind==CampKind.Stake||c.Kind==CampKind.FrameWood||c.Kind==CampKind.Blueprint&&c.Blueprint?.Design!=CampDesign.RaisedBed)return;
            // Analytic environmental relaxation; streaming catch-up is O(materials),
            // not a replay of every elapsed simulation frame.
            double wetTarget=rain>.1?D.Clamp(.35+rain*.12):D.Clamp(humidity*.25-sun*.2);
            double rate=rain>.1?1/900d:(1+Math.Max(0,wind)*.15+sun*3+(c.Burning?15:0))/14400;
            c.Wetness=wetTarget+(c.Wetness-wetTarget)*Math.Exp(-seconds*rate);
            if(c.Kind!=CampKind.Fire)return;
            if(c.Burning&&(c.FuelKg<=.002||c.Wetness>.75||rain>8)){c.Burning=false;}
            double remaining=seconds,heated=0,power=c.FirePowerKw;
            if(c.Burning)
            {
                double fine=c.Materials.Where(Tinder).Sum(p=>p.MassKg*FuelEnergy(p))/Math.Max(.001,c.FuelEnergyKg);
                double wood=c.Materials.Where(WoodCrafting.Wood).Sum(p=>p.MassKg*FuelEnergy(p))/Math.Max(.001,c.FuelEnergyKg);
                // Longer tending intervals on the compressed day clock: roughly
                // twice the former duration for fine fuel, three times for wood.
                double burnRate=(.25+.12*Math.Min(8,power))/3600*(1+Math.Min(1,Math.Max(0,wind)/15))*(1+fine*2)*.5/(1+wood*.5);
                double needed=remaining*burnRate,burned=0,burnedEnergy=0;
                foreach(var p in c.Materials.Where(Fuel).OrderBy(p=>Tinder(p)?0:Kindling(p)?1:2).ToArray())
                {double take=Math.Min(needed/FuelEnergy(p),p.MassKg);p.MassKg-=take;needed-=take*FuelEnergy(p);burned+=take;burnedEnergy+=take*FuelEnergy(p);if(p.MassKg<.000001)c.Materials.Remove(p);if(needed<=0)break;}
                c.AshKg+=burned*.04;c.EmberSeconds=1200+7200*(1-Math.Exp(-c.AshKg*6));
                heated=Math.Min(seconds,burnedEnergy/burnRate);
                if(needed>0||c.FuelKg<.002){c.Burning=false;remaining=Math.Max(0,seconds-heated);}else remaining=0;
            }
            double heatDose=heated*Math.Min(2,power/3);
            if(!c.Burning){heatDose+=Math.Min(remaining,c.EmberSeconds/(rain>.1?3:1))*.22*D.Clamp(c.EmberSeconds/1200);c.EmberSeconds=Math.Max(0,c.EmberSeconds-remaining*(rain>.1?3:1));}
            CampWaterHeating.AdvanceStones(c,heated,remaining,power,c.EmberSeconds>0);
            foreach(var p in c.CookingItems??Enumerable.Empty<Possession>())
            {
                if(PaleolithicClay.Green(p)){if(heated>0&&power>=3)PaleolithicClay.Fire(p,heated*Math.Min(2,power/3));}
                else if(CampWaterHeating.DirectVessel(p))CampWaterHeating.HeatPot(p,heatDose);
                else {p.CookingSeconds+=heatDose;double duration=CookingDuration(p);if(p.CookingSeconds>=duration)p.Cooked=true;if(p.CookingSeconds>=duration*3)p.Burnt=true;}
            }
        }
        public static double RadiantWarmth(CampObject c,double distance)=>c.FirePowerKw*10/(.8+distance*distance);
        public static double ContactDamagePerSecond(CampObject c,double distance,double feetHeight)
        {
            if(c==null||c.Kind!=CampKind.Fire||feetHeight<-.25||feetHeight>1.1||distance>=.55)return 0;
            if(c.Burning)return 10*D.Clamp((.55-distance)/.2);
            return c.EmberSeconds>0&&feetHeight<.18?3*D.Clamp((.35-distance)/.15):0;
        }
        public static double BedComfort(CampObject c)=>c==null?.38:c.Blueprint?.Design==CampDesign.RaisedBed?D.Clamp(.55+.2*CampBlueprints.CoverFraction(c.Blueprint)-c.Wetness*.3):D.Clamp(.38+.42*(1-Math.Exp(-c.BeddingDepthM/.08))+(c.Materials.Any(Hide)?.08:0)-c.Wetness*.45);
        public static double BedInsulation(CampObject c)=>c==null?0:c.Blueprint?.Design==CampDesign.RaisedBed?Math.Min(.7,.25+.3*CampBlueprints.CoverFraction(c.Blueprint))*(1-c.Wetness*.7):Math.Min(.75,c.BeddingDepthM*6+(c.Materials.Any(Hide)?.18:0))*(1-c.Wetness*.7);
        public static double Drive(CampObject stake,Possession rock)
        {
            if(stake==null||stake.Kind!=CampKind.Stake||stake.Materials.Count!=1||!StoneCrafting.Rock(rock)||rock.MassKg<.1)return 0;
            var p=stake.Materials[0];double maximum=Math.Min(stake.SoilDepthM,p.HeightM*.65);
            double step=.014*Math.Sqrt(rock.MassKg/.5)*Math.Pow(.03/Math.Max(.01,p.WidthM),1.3)/stake.SoilFirmness;
            double old=stake.EmbedM;stake.EmbedM=Math.Min(maximum,old+Math.Max(.002,Math.Min(.04,step)));return stake.EmbedM-old;
        }
    }
    public static class SleepRules
    {
        public static double WakeDrive(PlayerSurvival s,double localHour)
            =>.5+.5*Math.Cos((localHour-(s.SleepPhaseHour+16))*Math.PI/12);
        public static double Sleepiness(PlayerSurvival s,double localHour)=>Math.Max(0,Math.Min(100,s.Tiredness+(1-WakeDrive(s,localHour))*14));
        public static void Advance(PlayerSurvival s,double simulatedSeconds,bool asleep,double comfort=1,double localHour=0,double sunlight=0)
        {
            if(s.Dead||simulatedSeconds<=0)return;PlayerMetabolism.Initialize(s);
            double hours=simulatedSeconds/3600,drive=WakeDrive(s,localHour);
            s.Tiredness=Math.Max(0,Math.Min(100,s.Tiredness+hours*(asleep?-11*(.7+.3*Math.Max(0,Math.Min(1,comfort)))*(1-.25*drive):4.4)));
            s.SleepBoutSeconds=asleep?s.SleepBoutSeconds+simulatedSeconds:0;
            // Daylight gently entrains the clock; it does not force everyone
            // awake at an exact sunrise or make daytime sleep impossible.
            if(sunlight>.2){double difference=23-s.SleepPhaseHour;if(difference>12)difference-=24;if(difference< -12)difference+=24;s.SleepPhaseHour=(s.SleepPhaseHour+difference*(1-Math.Exp(-hours/48))+24)%24;}
        }
        public static bool WakeNaturally(PlayerSurvival s,double localHour,double sunlight)
        {
            if(s.SleepBoutSeconds<1200)return false;
            if(s.Tiredness<=8)return true;
            double drive=WakeDrive(s,localHour);
            if(sunlight>.12&&drive>.2&&s.Tiredness<40&&s.SleepBoutSeconds>4*3600)return true;
            return drive>.75&&s.SleepBoutSeconds>(1+s.Tiredness/40)*3600;
        }
    }
}
