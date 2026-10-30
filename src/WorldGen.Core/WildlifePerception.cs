using System;

namespace WorldGen.Core
{
    public enum WildlifeAwareness { Relaxed,Suspicious,Alert,Fleeing,Searching }
    public enum WildlifeSense { None,Sight,Sound,Scent,Social,Injury }
    public sealed class WildlifeSenses
    {
        public double VisionM=85,FieldDegrees=300,StaticDetail=.22,NightVision=.4,HearingM=45,SmellM=110,MemorySeconds=65,ConfirmSeconds=2.5;
        public static WildlifeSenses Describe(string slug,double mass,bool predator,bool flying,bool tiny)
        {
            slug=(slug??"").ToLowerInvariant();bool Has(params string[] words){foreach(var w in words)if(slug.Contains(w))return true;return false;}
            var p=new WildlifeSenses{VisionM=predator?95:85,FieldDegrees=predator?220:310,StaticDetail=predator?.4:.22,HearingM=predator?65:45,SmellM=predator?160:110,NightVision=predator?.7:.45};
            if(Has("kangaroo","wallaby")){p.StaticDetail=.12;p.VisionM=65;p.HearingM=60;p.SmellM=85;p.FieldDegrees=310;}
            if(Has("deer","elk","moose","antelope","gazelle")){p.HearingM=65;p.SmellM=150;p.NightVision=.7;p.FieldDegrees=320;}
            if(Has("wolf","dog","bear","boar")){p.SmellM=240;p.HearingM=85;}
            if(Has("owl","cat","lion","leopard","tiger"))p.NightVision=.95;
            if(flying){p.VisionM=Has("eagle","hawk","falcon")?220:100;p.StaticDetail=.55;p.SmellM=Has("vulture")?180:12;p.FieldDegrees=290;}
            if(tiny){p.VisionM=5;p.HearingM=3;p.SmellM=4;p.MemorySeconds=8;p.ConfirmSeconds=.6;}
            return p;
        }
    }
    [Serializable] public sealed class WildlifeAwarenessState
    {
        public double Suspicion,MemorySeconds,LastX,LastZ,LastCueSeconds;
        public WildlifeAwareness Mode;public WildlifeSense Sense;
        public WildlifeAwarenessState Copy()=>(WildlifeAwarenessState)MemberwiseClone();
        public void Validate()
        {if(!ItemUseRules.Finite(Suspicion)||Suspicion<0||Suspicion>1||!ItemUseRules.Finite(MemorySeconds)||MemorySeconds<0||!ItemUseRules.Finite(LastX)||!ItemUseRules.Finite(LastZ)||!ItemUseRules.Finite(LastCueSeconds)||LastCueSeconds<0||!Enum.IsDefined(typeof(WildlifeAwareness),Mode)||!Enum.IsDefined(typeof(WildlifeSense),Sense))throw new ArgumentException("Invalid animal awareness.");}
    }
    public static class WildlifePerception
    {
        static double Unit(double x)=>Math.Max(0,Math.Min(1,x));
        public static double Vision(WildlifeSenses p,double distance,double facingDot,double speed,double posture,double cover,double light,bool clear,bool feeding=false)
        {
            if(!clear||distance>p.VisionM||facingDot<Math.Cos(p.FieldDegrees*Math.PI/360))return 0;
            double motion=Unit(speed/2.5),detail=p.StaticDetail+(1-p.StaticDetail)*motion;
            double illumination=p.NightVision+(1-p.NightVision)*Unit(light);
            double near=Unit((12-distance)/10);
            return Unit((.12+1.55*Math.Pow(Unit(1-distance/p.VisionM),1.4))*(detail+near*.6)*posture*(1-Unit(cover)*.9)*illumination*(feeding?.55:1));
        }
        public static double Hearing(WildlifeSenses p,double distance,double noise,double wind,double rain)
            =>Unit(noise*(1-distance/Math.Max(1,p.HearingM))/(1+wind*.065+rain*.1));
        /// <summary>Wind vector points where air travels. A positive alignment puts the observer downwind.</summary>
        public static double Scent(WildlifeSenses p,double dx,double dz,double windX,double windZ,double rain)
        {
            double distance=Math.Sqrt(dx*dx+dz*dz),speed=Math.Sqrt(windX*windX+windZ*windZ);
            if(distance<1)return 1;
            double alignment=speed>.05?(dx*windX+dz*windZ)/(distance*speed):0;
            double local=Math.Exp(-distance/5)*.35;
            double plume=speed>.2&&alignment>.2?Math.Pow(alignment,7)*Math.Exp(-distance/(p.SmellM*Math.Min(1.5,.3+speed*.22)))*Math.Min(1,speed/1.5):0;
            return Unit((local+plume)/(1+rain*.1));
        }
        public static void Observe(WildlifeAwarenessState s,WildlifeSenses p,double dt,double sight,double sound,double scent,double x,double z)
        {
            if(!ItemUseRules.Finite(dt)||dt<0||dt>86400)throw new ArgumentException("Invalid perception interval.");
            double evidence=Math.Max(sight,Math.Max(sound*.8,scent*.75));
            s.MemorySeconds=Math.Max(0,s.MemorySeconds-dt);s.LastCueSeconds+=dt;
            if(evidence>.055)
            {
                s.Suspicion=Unit(s.Suspicion+dt*(evidence-.045)/p.ConfirmSeconds);s.LastCueSeconds=0;
                s.Sense=sight>=Math.Max(sound*.8,scent*.75)?WildlifeSense.Sight:sound*.8>=scent*.75?WildlifeSense.Sound:WildlifeSense.Scent;
                if(evidence>.18){s.LastX=x;s.LastZ=z;}
                if(s.Suspicion>=.65)s.MemorySeconds=Math.Max(s.MemorySeconds,p.MemorySeconds);
            }
            else s.Suspicion=Unit(s.Suspicion-dt/(s.MemorySeconds>0?150:25));
            s.Mode=s.Suspicion>=.78?WildlifeAwareness.Fleeing:s.Suspicion>=.5?WildlifeAwareness.Alert:s.Suspicion>=.15?WildlifeAwareness.Suspicious:s.MemorySeconds>0?WildlifeAwareness.Searching:WildlifeAwareness.Relaxed;
        }
        public static void Alarm(WildlifeAwarenessState s,double x,double z,bool injury=false)
        {s.Suspicion=1;s.MemorySeconds=Math.Max(s.MemorySeconds,injury?120:65);s.LastX=x;s.LastZ=z;s.LastCueSeconds=0;s.Sense=injury?WildlifeSense.Injury:WildlifeSense.Social;s.Mode=WildlifeAwareness.Fleeing;}
    }
}
