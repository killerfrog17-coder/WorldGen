using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    public enum CampKind { Fire,Bedding,Stake }
    [Serializable] public sealed class CampObject
    {
        public string Id="camp-"+Guid.NewGuid().ToString("N");public CampKind Kind;public V Position;
        public double Heading,EmbedM,SoilDepthM,SoilFirmness=1,Wetness,EmberSeconds,LastSimulatedSeconds;
        public bool Burning;public double AshKg;
        public List<Possession> Materials=new List<Possession>();
        public double MassKg=>Materials.Sum(p=>p.MassKg);
        public double FuelKg=>Materials.Where(CampRules.Fuel).Sum(p=>p.MassKg);
        public double BeddingDepthM=>Kind==CampKind.Bedding?Math.Min(.4,MassKg/32/1.8):0;
        public double Stability=>Kind!=CampKind.Stake||Materials.Count==0?0:D.Clamp(EmbedM/Math.Max(.08,Materials[0].HeightM*.2)*Math.Sqrt(Materials[0].WidthM/.03)*SoilFirmness);
        public double FirePowerKw=>!Burning?EmberSeconds>0?.5:0:Math.Min(8,2+Math.Sqrt(FuelKg)*2)*(1-Wetness*.7);
        public CampObject Copy(){var c=(CampObject)MemberwiseClone();c.Materials=Materials.Select(p=>p.Copy()).ToList();return c;}
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
                if(c==null||string.IsNullOrEmpty(c.Id)||!ids.Add(c.Id)||!Enum.IsDefined(typeof(CampKind),c.Kind)||!Finite(c.Position.Length)||Math.Abs(c.Position.Length-1)>1e-5||!Finite(c.Heading)||!Finite(c.EmbedM)||c.EmbedM<0||!Finite(c.SoilDepthM)||c.SoilDepthM<0||!Finite(c.SoilFirmness)||c.SoilFirmness<=0||!Finite(c.Wetness)||c.Wetness<0||c.Wetness>1||!Finite(c.EmberSeconds)||c.EmberSeconds<0||!Finite(c.AshKg)||c.AshKg<0||!Finite(c.LastSimulatedSeconds)||c.LastSimulatedSeconds<0||c.Materials==null)throw new ArgumentException("Invalid placed camp object.");
                foreach(var p in c.Materials)if(p==null||string.IsNullOrEmpty(p.Id)||!materials.Add(p.Id)||!Finite(p.MassKg)||p.MassKg<=0||!Finite(p.HeightM)||p.HeightM<=0||!Finite(p.WidthM)||p.WidthM<=0)throw new ArgumentException("Invalid camp material.");
                if(c.Kind==CampKind.Stake&&(c.Materials.Count!=1||c.EmbedM>c.Materials[0].HeightM*.7+.001))throw new ArgumentException("Invalid planted stick.");
            }
        }
    }
    public static class CampRules
    {
        public static bool Tinder(Possession p)=>p!=null&&(p.Kind==PossessionKind.TinderBundle||p.Kind==PossessionKind.DryGrass||p.Kind==PossessionKind.PreparedFibre||p.Kind==PossessionKind.BarkStrips);
        public static bool Kindling(Possession p)=>p!=null&&(p.Kind==PossessionKind.KindlingBundle||WoodCrafting.Wood(p)&&p.WidthM<=.025);
        public static bool Fuel(Possession p)=>p!=null&&(Tinder(p)||Kindling(p)||WoodCrafting.Wood(p)||p.Kind==PossessionKind.BarkSheet||p.Kind==PossessionKind.LeafLitter||p.Kind==PossessionKind.ReedBundle);
        public static bool Bedding(Possession p)=>p!=null&&(p.Kind==PossessionKind.DryGrass||p.Kind==PossessionKind.LeafLitter||p.Kind==PossessionKind.ReedBundle||p.Kind==PossessionKind.RawHide||p.Kind==PossessionKind.ScrapedHide||p.Kind==PossessionKind.WorkedHide);
        public static bool Plantable(Possession p)=>WoodCrafting.Wood(p)&&p.HeightM>=.35&&p.WidthM<=.09&&p.WoodSoundness>=.35;
        public static string IgniteReason(CampObject fire,bool drillAvailable)
        {
            if(fire==null||fire.Kind!=CampKind.Fire)return "Choose a fire lay.";
            if(fire.Burning)return "The fire is already burning.";
            if(fire.EmberSeconds<=0&&!drillAvailable)return "Requires a hand-drill set, or live embers.";
            if(fire.Wetness>.45)return "The tinder is too wet; let it dry.";
            if(fire.Materials.Where(Tinder).Sum(p=>p.MassKg)<.025)return "Add fine dry tinder.";
            if(fire.Materials.Where(Kindling).Sum(p=>p.MassKg)<.1)return "Add thin kindling before larger fuel.";
            return "";
        }
        public static void Add(CampObject c,Possession material)
        {
            if(material==null||material.MassKg<=0||c.Materials.Any(p=>p.Id==material.Id)||!(c.Kind==CampKind.Fire?Fuel(material):c.Kind==CampKind.Bedding?Bedding(material):Plantable(material))||c.Kind==CampKind.Stake&&c.Materials.Count>0)throw new ArgumentException("Unsuitable placed material.");
            c.Materials.Add(material.Copy());
        }
        public static void Ignite(CampObject c,bool drill)
        {string reason=IgniteReason(c,drill);if(reason.Length>0)throw new InvalidOperationException(reason);c.Burning=true;c.EmberSeconds=900;}
        public static void Advance(CampObject c,double seconds,double rain,double humidity,double wind,double sun)
        {
            if(!double.IsFinite(seconds)||seconds<0||seconds>1e12)throw new ArgumentOutOfRangeException(nameof(seconds));
            if(c.Kind==CampKind.Stake)return;
            // Analytic environmental relaxation; streaming catch-up is O(materials),
            // not a replay of every elapsed simulation frame.
            double wetTarget=rain>.1?D.Clamp(.35+rain*.12):D.Clamp(humidity*.25-sun*.2);
            double rate=rain>.1?1/900d:(1+Math.Max(0,wind)*.15+sun*3+(c.Burning?15:0))/14400;
            c.Wetness=wetTarget+(c.Wetness-wetTarget)*Math.Exp(-seconds*rate);
            if(c.Kind!=CampKind.Fire)return;
            if(c.Burning&&(c.FuelKg<=.002||c.Wetness>.75||rain>8)){c.Burning=false;}
            double remaining=seconds;
            if(c.Burning)
            {
                double burnRate=(.6+.12*Math.Min(8,c.FirePowerKw))/3600*(1+Math.Min(1,Math.Max(0,wind)/15));
                double needed=remaining*burnRate,burned=0;
                foreach(var p in c.Materials.OrderBy(p=>Tinder(p)?0:Kindling(p)?1:2).ToArray())
                {double take=Math.Min(needed,p.MassKg);p.MassKg-=take;needed-=take;burned+=take;if(p.MassKg<.000001)c.Materials.Remove(p);if(needed<=0)break;}
                c.AshKg+=burned*.04;c.EmberSeconds=900;
                if(needed>0||c.FuelKg<.002){c.Burning=false;remaining=Math.Max(0,seconds-burned/burnRate);}else remaining=0;
            }
            if(!c.Burning)c.EmberSeconds=Math.Max(0,c.EmberSeconds-remaining*(rain>.1?3:1));
        }
        public static double RadiantWarmth(CampObject c,double distance)=>c.FirePowerKw*10/(.8+distance*distance);
        public static double ContactDamagePerSecond(CampObject c,double distance,double feetHeight)
        {
            if(c==null||c.Kind!=CampKind.Fire||feetHeight<-.25||feetHeight>1.1||distance>=.55)return 0;
            if(c.Burning)return 10*D.Clamp((.55-distance)/.2);
            return c.EmberSeconds>0&&feetHeight<.18?3*D.Clamp((.35-distance)/.15):0;
        }
        public static double BedComfort(CampObject c)=>c==null?.38:D.Clamp(.4+Math.Min(.55,c.BeddingDepthM*8)-c.Wetness*.45);
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
        public static void Advance(PlayerSurvival s,double simulatedSeconds,bool asleep,double comfort=1)
        {if(s.Dead||simulatedSeconds<=0)return;s.Tiredness=Math.Max(0,Math.Min(100,s.Tiredness+simulatedSeconds/3600*(asleep?-14*Math.Max(.1,Math.Min(1,comfort)):4.4)));}
    }
}
