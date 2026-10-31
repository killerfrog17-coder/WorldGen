using System;

namespace WorldGen.Core
{
    public enum WildlifeAwareness { Relaxed,Suspicious,Alert,Fleeing,Searching }
    public enum WildlifeSense { None,Sight,Sound,Scent,Social,Injury }
    public sealed class WildlifeSenses
    {
        public double VisionM=85,FieldDegrees=300,StaticDetail=.22,NightVision=.4,HearingM=45,SmellM=110,MemorySeconds=65,ConfirmSeconds=2.5;
        public double Caution=.5;
        public void Individual(uint identity)
        {Caution=Rng.Mix(identity^0x6e13b7u)/(double)uint.MaxValue;ConfirmSeconds*=1.2-.4*Caution;}
        public static WildlifeSenses Describe(string slug,double mass,bool predator,bool flying,bool tiny,string nativeName="")
        {
            slug=(slug??"").ToLowerInvariant();bool Has(params string[] words){foreach(var w in words)if(slug.Contains(w))return true;return false;}
            var p=new WildlifeSenses{VisionM=predator?95:85,FieldDegrees=predator?220:310,StaticDetail=predator?.4:.22,HearingM=predator?65:45,SmellM=predator?160:110,NightVision=predator?.7:.45};
            if(Has("kangaroo","wallaby")){p.StaticDetail=.12;p.VisionM=65;p.HearingM=60;p.SmellM=85;p.FieldDegrees=310;}
            if(Has("deer","elk","moose","antelope","gazelle")){p.HearingM=65;p.SmellM=150;p.NightVision=.7;p.FieldDegrees=320;}
            if(Has("wolf","dog","bear","boar")){p.SmellM=240;p.HearingM=85;}
            if(Has("owl","cat","lion","leopard","tiger"))p.NightVision=.95;
            if(flying){p.VisionM=Has("eagle","hawk","falcon")?220:100;p.StaticDetail=.55;p.SmellM=Has("vulture")?180:12;p.FieldDegrees=290;}
            if(WildlifeNaturalHistory.TryGet(slug,out var history))
            {
                void Set(double sight,double field,double detail,double night,double hearing,double smell,double memory=65,double confirm=2)
                {p.VisionM=sight;p.FieldDegrees=field;p.StaticDetail=detail;p.NightVision=night;p.HearingM=hearing;p.SmellM=smell;p.MemorySeconds=memory;p.ConfirmSeconds=confirm;}
                switch(history.Sensory)
                {
                    case WildlifeSensoryKind.Ungulate:Set(95,320,.28,.7,65,150,90);break;
                    case WildlifeSensoryKind.Macropod:Set(75,310,.18,.65,65,100,65,2.6);break;
                    case WildlifeSensoryKind.Canid:Set(110,240,.45,.8,90,240,120,1.8);break;
                    case WildlifeSensoryKind.SmallCarnivore:Set(60,240,.35,.75,55,100);break;
                    case WildlifeSensoryKind.LargeCat:Set(100,240,.5,.95,80,110,110,1.8);break;
                    case WildlifeSensoryKind.Bear:Set(65,280,.25,.65,75,280,120,2.5);break;
                    case WildlifeSensoryKind.SmallMammal:Set(25,300,.2,.7,30,40,35,1.4);break;
                    case WildlifeSensoryKind.Primate:Set(100,220,.65,.3,65,25,90,1.5);break;
                    case WildlifeSensoryKind.Elephant:Set(55,280,.3,.7,110,250,150);break;
                    case WildlifeSensoryKind.Rhino:Set(45,280,.14,.45,85,200,75,2.5);break;
                    case WildlifeSensoryKind.Bird:Set(100,300,.55,.22,40,8,30,1.2);break;
                    case WildlifeSensoryKind.Raptor:Set(240,280,.75,.25,70,10,60,1.3);break;
                    case WildlifeSensoryKind.Owl:Set(110,250,.55,.99,110,5,70,1.5);break;
                    case WildlifeSensoryKind.Bat:Set(45,270,.55,.85,100,35,35,1.5);break;
                    case WildlifeSensoryKind.Reptile:Set(35,290,.2,.5,18,45,40,2.2);break;
                    case WildlifeSensoryKind.Snake:Set(20,240,.12,.55,12,60,45,2.2);break;
                    case WildlifeSensoryKind.Fish:Set(mass<.1?12:35,300,.3,.5,35,80,25,1.5);break;
                    case WildlifeSensoryKind.Marine:Set(65,270,.5,.6,140,5,90);break;
                    case WildlifeSensoryKind.Invertebrate:Set(4,320,.08,.4,2,3,6,.7);break;
                }
                if(slug=="crocodile")Set(90,290,.45,.9,50,80,100,2.4);
                if(slug=="monitor-lizard")Set(60,290,.35,.4,25,60);
                if(slug=="vulture")p.SmellM=(nativeName??"").IndexOf("turkey",StringComparison.OrdinalIgnoreCase)>=0?180:20;
                if(slug=="kiwi"){p.VisionM=12;p.SmellM=70;p.NightVision=.75;}
            }
            if(tiny){p.VisionM=5;p.HearingM=3;p.SmellM=4;p.MemorySeconds=8;p.ConfirmSeconds=.6;}
            return p;
        }
    }
    [Serializable] public sealed class WildlifeAwarenessState
    {
        public double Suspicion,MemorySeconds,LastX,LastZ,LastCueSeconds;
        [System.Runtime.Serialization.OptionalField] public double ThreatSeconds,FlightSeconds;
        public WildlifeAwareness Mode;public WildlifeSense Sense;
        [System.Runtime.Serialization.OptionalField] public bool ThreatIsPlayer;
        public WildlifeAwarenessState Copy()=>(WildlifeAwarenessState)MemberwiseClone();
        public void Validate()
        {if(!ItemUseRules.Finite(Suspicion)||Suspicion<0||Suspicion>1||!ItemUseRules.Finite(MemorySeconds)||MemorySeconds<0||!ItemUseRules.Finite(LastX)||!ItemUseRules.Finite(LastZ)||!ItemUseRules.Finite(LastCueSeconds)||LastCueSeconds<0||!ItemUseRules.Finite(ThreatSeconds)||ThreatSeconds<0||ThreatSeconds>120||!ItemUseRules.Finite(FlightSeconds)||FlightSeconds<0||FlightSeconds>120||!Enum.IsDefined(typeof(WildlifeAwareness),Mode)||!Enum.IsDefined(typeof(WildlifeSense),Sense))throw new ArgumentException("Invalid animal awareness.");}
    }
    public static class WildlifePerception
    {
        static double Unit(double x)=>Math.Max(0,Math.Min(1,x));
        public static double Vision(WildlifeSenses p,double distance,double facingDot,double speed,double posture,double cover,double light,bool clear,bool feeding=false,bool sleeping=false)
        {
            if(sleeping||!clear||distance>p.VisionM||facingDot<Math.Cos(p.FieldDegrees*Math.PI/360))return 0;
            double motion=Unit(speed/2.5),detail=p.StaticDetail+(1-p.StaticDetail)*motion;
            double illumination=Math.Pow(Unit(light),1-.65*p.NightVision);
            double near=Unit((12-distance)/10);
            return Unit((.12+1.55*Math.Pow(Unit(1-distance/p.VisionM),1.4))*(detail+near*.6)*posture*Math.Pow(1-Unit(cover),1.5)*illumination*(feeding?.55:1));
        }
        public static double GrassDepth(GroundCoverProfile cover,double aboveGround,double metres,double admission=1)
        {
            double Block(double weight,double height)
            {height*=cover.Stature;double top=Math.Min(3.2,height*1.25),bottom=Math.Min(3.2,height*.70);return Math.Max(0,weight)*Unit((top-aboveGround)/Math.Max(.08,top-bottom));}
            // Same growth-form heights and stature as the grass shader; the
            // transition accounts for its .70..1.25 individual blade variation.
            double density=Block(cover.Grasses.X,.58)+Block(cover.Grasses.Y,.90)+Block(cover.Grasses.Z,.40)+Block(cover.Herbs.X,.85);
            return Math.Max(0,metres)*density*Unit(admission)*1.4;
        }
        public static double BodyCover(GroundCoverProfile cover,double height,double admission=1)
            =>Unit(1-Math.Exp(-GrassDepth(cover,Math.Max(.05,height*.55),2,admission)));
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
        public static double ScentHeight(double verticalSeparation)
            =>Math.Exp(-Math.Max(0,Math.Abs(verticalSeparation)-2)/6);
        public static void Observe(WildlifeAwarenessState s,WildlifeSenses p,double dt,double sight,double sound,double scent,double x,double z,double distance=double.PositiveInfinity,double flightDistance=25,double closingSpeed=0)
        {
            if(!ItemUseRules.Finite(dt)||dt<0||dt>86400)throw new ArgumentException("Invalid perception interval.");
            double evidence=Math.Max(sight,Math.Max(sound*.65,scent*.6));
            s.MemorySeconds=Math.Max(0,s.MemorySeconds-dt);s.LastCueSeconds+=dt;
            s.FlightSeconds=Math.Max(0,s.FlightSeconds-dt);
            // Weak clues converge to weak suspicion: waiting cannot turn a
            // barely audible/visible shape into certain identification.
            double confidence=Unit((evidence-.04)/.45);
            s.Suspicion+=(confidence-s.Suspicion)*(1-Math.Exp(-dt/Math.Max(.2,p.ConfirmSeconds)));
            if(evidence>.055)
            {
                s.LastCueSeconds=0;
                s.Sense=sight>=Math.Max(sound*.65,scent*.6)?WildlifeSense.Sight:sound*.65>=scent*.6?WildlifeSense.Sound:WildlifeSense.Scent;
                // Even an uncertain sound has an approximate direction. Do not
                // turn a weak cue into a watch/search aimed at the map origin.
                s.LastX=x;s.LastZ=z;
                if(s.Suspicion>=.65)s.MemorySeconds=Math.Max(s.MemorySeconds,p.MemorySeconds);
            }
            bool danger=distance<=flightDistance*(.75+.6*p.Caution)&&(sight>.24||sound>.85||scent>.5);
            s.ThreatSeconds=Math.Max(0,Math.Min(120,s.ThreatSeconds+dt*(danger?1:-2)));
            double reaction=1.5+p.ConfirmSeconds*(1.4-.6*p.Caution);
            bool startle=distance<2.5&&evidence>.55||closingSpeed>4&&distance<flightDistance*.5&&(sight>.65||sound>.95)&&s.ThreatSeconds>.5;
            if(startle||danger&&s.Suspicion>.68&&s.ThreatSeconds>=reaction)
            {s.FlightSeconds=Math.Max(s.FlightSeconds,3+p.Caution*3);s.MemorySeconds=Math.Max(s.MemorySeconds,p.MemorySeconds);}
            s.Mode=s.FlightSeconds>0?WildlifeAwareness.Fleeing:s.Suspicion>=.45?WildlifeAwareness.Alert:s.Suspicion>=.12?WildlifeAwareness.Suspicious:s.MemorySeconds>0?WildlifeAwareness.Searching:WildlifeAwareness.Relaxed;
        }
        public static void Alarm(WildlifeAwarenessState s,double x,double z,bool injury=false)
        {s.Suspicion=1;s.FlightSeconds=Math.Max(s.FlightSeconds,injury?8:4);s.MemorySeconds=Math.Max(s.MemorySeconds,injury?120:65);s.LastX=x;s.LastZ=z;s.LastCueSeconds=0;s.Sense=injury?WildlifeSense.Injury:WildlifeSense.Social;s.Mode=WildlifeAwareness.Fleeing;}
        public static bool RelayAlarm(WildlifeAwarenessState receiver,WildlifeAwarenessState source)
        {
            if(source.Mode!=WildlifeAwareness.Fleeing||source.LastCueSeconds>10||receiver.Mode==WildlifeAwareness.Fleeing)return false;
            receiver.Suspicion=Math.Min(1,Math.Max(.4,receiver.Suspicion)+.16);
            receiver.Mode=receiver.Suspicion>=.78?WildlifeAwareness.Fleeing:WildlifeAwareness.Alert;receiver.Sense=WildlifeSense.Social;
            if(receiver.Mode==WildlifeAwareness.Fleeing)receiver.FlightSeconds=Math.Max(receiver.FlightSeconds,4);
            receiver.LastX=source.LastX;receiver.LastZ=source.LastZ;
            receiver.ThreatIsPlayer=source.ThreatIsPlayer;
            receiver.MemorySeconds=Math.Max(receiver.MemorySeconds,source.MemorySeconds);
            // Preserve the original cue's age: neighbours cannot refresh one
            // another forever after the real threat has gone quiet.
            receiver.LastCueSeconds=source.LastCueSeconds+.5;
            return true;
        }
    }
}
