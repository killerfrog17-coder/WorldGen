using System;

namespace WorldGen.Core
{
    /// <summary>Shared physical effort. Seconds are action time, not calendar-compressed days.</summary>
    [Serializable] public sealed class ExertionState
    {
        public double Burst=1,Breath,Fatigue,Heat;
        [System.Runtime.Serialization.OptionalField] public int ModelVersion=1;
        public ExertionState Copy()=>(ExertionState)MemberwiseClone();
        public void Validate()
        {if(ModelVersion<0||ModelVersion>1)throw new ArgumentException("Invalid exertion model.");foreach(double n in new[]{Burst,Breath,Fatigue,Heat})if(!ItemUseRules.Finite(n)||n<0||n>1)throw new ArgumentException("Invalid exertion state.");}
        public double PowerScale=>Math.Max(.18,(.45+.55*Math.Sqrt(Burst))*(1-.42*Fatigue)*(1-.38*Heat));
    }
    public sealed class ExertionProfile
    {
        public double Aerobic=.52,BurstSeconds=25,RecoverySeconds=100,FatigueSeconds=1600,RestSeconds=900,HeatTolerance=1;
        public double LegacyBurstSeconds=25;
        // This reserve represents sustained usable effort, not just muscle PCr.
        // A 6 m/s game sprint is not a maximal Olympic acceleration effort.
        static readonly ExertionProfile human=new ExertionProfile{Aerobic=.62,BurstSeconds=240,LegacyBurstSeconds=24,RecoverySeconds=32,FatigueSeconds=1800,RestSeconds=1200,HeatTolerance=1.4};
        public static ExertionProfile Human()=>human;
        public static ExertionProfile Animal(string slug,double mass,string medium)
        {
            slug=(slug??"").ToLowerInvariant();bool Has(params string[] words){foreach(var w in words)if(slug.Contains(w))return true;return false;}
            var p=new ExertionProfile{BurstSeconds=65+Math.Min(65,Math.Sqrt(Math.Max(0,mass))*4),FatigueSeconds=1600,HeatTolerance=.85,RecoverySeconds=50};
            p.LegacyBurstSeconds=22+Math.Min(25,Math.Sqrt(Math.Max(0,mass)));
            if(Has("wolf","dog","hyena","horse","wildebeest","reindeer")){p.Aerobic=.68;p.BurstSeconds=210;p.FatigueSeconds=2600;}
            if(Has("cheetah")){p.Aerobic=.28;p.BurstSeconds=35;p.RecoverySeconds=220;p.HeatTolerance=.55;}
            if(Has("kangaroo","wallaby")){p.Aerobic=.74;p.BurstSeconds=180;p.FatigueSeconds=2600;p.HeatTolerance=1.2;}
            if(Has("crocodile","alligator","tortoise","turtle")){p.Aerobic=.28;p.BurstSeconds=14;p.RecoverySeconds=170;}
            if(medium=="air"){p.Aerobic=.72;p.BurstSeconds=150;p.FatigueSeconds=3600;p.HeatTolerance=1.1;}
            if(medium=="water"){p.Aerobic=.7;p.BurstSeconds=150;p.FatigueSeconds=2800;p.HeatTolerance=2;}
            if(slug=="pronghorn"){p.Aerobic=.76;p.BurstSeconds=300;p.FatigueSeconds=3600;}
            if(slug=="camel"||slug=="oryx"||slug=="wild-ass")p.HeatTolerance=1.6;
            if(slug=="polar-bear")p.HeatTolerance=.45;
            if(slug=="reindeer"||slug=="musk-ox")p.HeatTolerance=.65;
            if(Has("wolf","dog","hyena","horse","wildebeest","reindeer"))p.LegacyBurstSeconds=55;
            if(Has("cheetah"))p.LegacyBurstSeconds=18;
            if(Has("kangaroo","wallaby"))p.LegacyBurstSeconds=45;
            if(Has("crocodile","alligator","tortoise","turtle"))p.LegacyBurstSeconds=14;
            if(medium=="air")p.LegacyBurstSeconds=45;if(medium=="water")p.LegacyBurstSeconds=38;if(slug=="pronghorn")p.LegacyBurstSeconds=80;
            return p;
        }
    }
    public static class ExertionRules
    {
        static double Unit(double x)=>Math.Max(0,Math.Min(1,x));
        public static bool Upgrade(ExertionState s,ExertionProfile p)
        {
            if(s.ModelVersion!=0)return false;
            // Preserve the seconds of work already spent, rather than carrying
            // a depleted 24-second budget over as a depleted four-minute budget.
            s.Burst=Unit(1-(1-s.Burst)*Math.Min(1,p.LegacyBurstSeconds/p.BurstSeconds));s.ModelVersion=1;return true;
        }
        public static void Advance(ExertionState s,ExertionProfile p,double seconds,double effort,double heatC=20,double load=1,double grade=0)
        {
            if(!ItemUseRules.Finite(seconds)||seconds<0||seconds>86400||!ItemUseRules.Finite(effort)||!ItemUseRules.Finite(heatC)||!ItemUseRules.Finite(load)||!ItemUseRules.Finite(grade))throw new ArgumentException("Invalid exertion input.");
            effort=Unit(effort*Math.Max(1,load)*(1+Math.Max(0,grade)*.75+Math.Max(0,-grade)*.12));
            Upgrade(s,p);
            // Bounded substeps retain consistent recovery during streaming catch-up.
            while(seconds>1e-9)
            {
                double dt=Math.Min(1,seconds),over=Math.Max(0,effort-p.Aerobic)/(1-p.Aerobic);
                if(over>0)s.Burst=Unit(s.Burst-dt*over/p.BurstSeconds);
                else s.Burst=Unit(s.Burst+dt*(1-effort/p.Aerobic)/(p.RecoverySeconds*(1+s.Breath*2+s.Fatigue*4+s.Heat*2)));
                double breath=Unit(effort*effort+s.Heat*.25);
                s.Breath+=(breath-s.Breath)*(1-Math.Exp(-dt/(breath>s.Breath?7:35)));
                double strain=(over*.8+effort*.04)/p.FatigueSeconds;
                s.Fatigue=Unit(s.Fatigue+dt*(effort<.2?-(1-effort/.2)/p.RestSeconds:strain));
                double heat=Unit((Math.Max(0,heatC-24)/22*effort+over*.25)/p.HeatTolerance);
                s.Heat+=(heat-s.Heat)*(1-Math.Exp(-dt/(heat>s.Heat?240:110)));
                seconds-=dt;
            }
        }
        public static void Impulse(ExertionState s,double fraction)
        {s.Burst=Unit(s.Burst-Math.Max(0,fraction));s.Breath=Unit(s.Breath+Math.Max(0,fraction)*.5);}
    }
}
