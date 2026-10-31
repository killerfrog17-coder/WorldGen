using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class EffortCoverRevisionTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception(why);}
        var mild=new SurvivalEnvironment{AirTemperatureC=24,Humidity=.4};
        foreach(double speed in new[]{0d,1.5,3.4})
        {
            var p=new PlayerSurvival();p.Restore();SurvivalRules.SpendStamina(p,SurvivalRules.JumpStaminaCost);
            Check(p.Stamina>=97.9,"One jump is a small effort at speed "+speed);
            double t=0;while(p.Exertion.Burst<.999&&t<10){SurvivalRules.Advance(p,.05,60,mild,speed,false);t+=.05;}
            Console.WriteLine($"Jump recovery at {speed} m/s: {t:F2}s, reserve {p.Exertion.Burst:F4}");
            Check(t<(speed>3?8:2),"One jump recovers in seconds, including during ordinary travel");
        }
        var sprint=new PlayerSurvival();sprint.Restore();SurvivalRules.Advance(sprint,10,60,mild,6,true);
        Check(sprint.Exertion.Burst>.94&&sprint.SprintPower>.95,"Ten seconds of running cannot wipe out a healthy person");
        SurvivalRules.Advance(sprint,50,60,mild,6,true);Check(sprint.Exertion.Burst>.7&&sprint.SprintPower>.8,"A minute of hard running retains substantial capacity");
        var quick=sprint.Exertion.Copy();ExertionRules.Advance(quick,ExertionProfile.Human(),3,0);
        Check(quick.Burst<.9,"A short pause does not reset a sustained hard effort");
        var swim=new PlayerSurvival();swim.Restore();var water=mild;water.Swimming=true;water.SwimEffort=.5;
        SurvivalRules.Advance(swim,10,60,water,1.45,false);Check(swim.Stamina>99&&swim.Exertion.Burst>.99,"Ten seconds of normal swimming barely changes the reserve");
        SurvivalRules.Advance(swim,590,60,water,1.45,false);Check(swim.Stamina>80&&swim.Exertion.Burst>.99,"Ten minutes of steady swimming is sustainable for the prototype's fit human");
        swim.Restore();water.SwimEffort=.85;SurvivalRules.Advance(swim,60,60,water,2.7,true);
        Check(swim.Exertion.Burst>.8,"Hard swimming costs effort without ten-second exhaustion");
        var motion=new TerrainMotion{Y=.12,Water=(x,z)=>new MotionWater(0,1,0)};
        for(int i=0;i<60;i++)motion.Move(1d/60,0,0,0,0,0,false,false,(x,z)=>-6);
        Check(motion.Swimming&&motion.X>.8&&motion.SwimEffort<.3,"A current carries a floating swimmer without counting as swimming strokes");
        motion.Move(.1,1,0,0,0,0,false,false,(x,z)=>-6);Check(Math.Abs(motion.SwimEffort-.5)<1e-9,"Ordinary strokes feed the common exertion system");
        var senses=WildlifeSenses.Describe("deer",80,false,false,false);
        var weak=new WildlifeAwarenessState();for(int i=0;i<1200;i++)WildlifePerception.Observe(weak,senses,.1,.12,0,0,0,0,15,35);
        Check(weak.Suspicion<.2&&weak.Mode!=WildlifeAwareness.Fleeing,"Two minutes of a weak clue never become certain flight");
        var far=new WildlifeAwarenessState();for(int i=0;i<300;i++)WildlifePerception.Observe(far,senses,.1,.8,0,0,0,0,70,35);
        Check(far.Mode==WildlifeAwareness.Alert,"A clearly seen distant stationary person can be watched without automatic full-speed escape");
        var close=new WildlifeAwarenessState();WildlifePerception.Observe(close,senses,.3,1,0,0,0,0,20,35);
        Check(close.Mode!=WildlifeAwareness.Fleeing,"First seeing a non-contact threat is not instant panic");
        for(int i=0;i<80;i++)WildlifePerception.Observe(close,senses,.1,1,0,0,0,0,20,35);
        Check(close.Mode==WildlifeAwareness.Fleeing,"Sustained credible nearby danger still produces escape");
        var injured=new WildlifeAwarenessState();WildlifePerception.Alarm(injured,0,0,true);
        Check(injured.Mode==WildlifeAwareness.Fleeing&&injured.FlightSeconds>=8,"An actual injury can trigger immediate committed escape");
        var mate=new WildlifeAwarenessState();WildlifePerception.RelayAlarm(mate,injured);
        Check(mate.Mode==WildlifeAwareness.Alert,"A herd mate first reacts alertly to a newly observed alarm");
        WildlifePerception.RelayAlarm(mate,injured);WildlifePerception.RelayAlarm(mate,injured);
        Check(mate.Mode==WildlifeAwareness.Fleeing,"Repeated credible herd alarm can recruit escape");
        var grass=new GroundCoverProfile{Grasses=new V(0,.8,0),Stature=3};
        double depth=WildlifePerception.GrassDepth(grass,1.65,50);
        Check(Math.Exp(-depth)<.001,"Tall dense grass across a 50m sight line is opaque at head height");
        Check(WildlifePerception.GrassDepth(grass,4,50)==0,"The same grass does not hide a line of sight above its canopy");
        Check(WildlifePerception.GrassDepth(grass,1,50,0)==0,"Grass cannot provide concealment on a rejected cliff surface");
        double hidden=WildlifePerception.Vision(senses,50,1,0,1,WildlifePerception.BodyCover(grass,1.7),1,true)*Math.Exp(-depth);
        var unseen=new WildlifeAwarenessState();for(int i=0;i<300;i++)WildlifePerception.Observe(unseen,senses,.1,hidden,0,0,0,0,50,35);
        Check(unseen.Mode==WildlifeAwareness.Relaxed,"A silent still person hidden by tall grass cannot be visually identified at 50m");
        Check(WildlifePerception.Vision(senses,20,1,3,1,0,0,true)==0,"Night adaptation is not vision in zero light");
        var qualities=new HashSet<double>();for(uint i=0;i<20;i++){var s=WildlifeSenses.Describe("deer",80,false,false,false);s.Individual(i);qualities.Add(s.ConfirmSeconds);}
        Check(qualities.Count>15,"Stable individual caution varies reaction delays within a species");
        var saved=WorldJson.Read<WildlifeAwarenessState>(WorldJson.Write(close));saved.Validate();
        Check(saved.ThreatSeconds==close.ThreatSeconds&&saved.FlightSeconds==close.FlightSeconds,"Reaction timing and committed flight survive saving");
        var cat=WorldJson.Read<FaunaPrototypeCatalogue>(File.ReadAllText("unity/WorldGen.Unity/Assets/WorldGen/Resources/WorldGenFaunaCatalogue.json")).animals;
        Check(cat.First(p=>p.slug=="deer").VisibleRadiusM>=1000&&cat.First(p=>p.slug=="elephant").VisibleRadiusM>=1500,"Large silhouettes have long draw distances");
        Check(cat.First(p=>p.slug=="duck").VisibleRadiusM>=600&&cat.First(p=>p.slug=="raven").VisibleRadiusM>=1000,"Sky birds have a distant render band even below 0.8m model length");
        Check(cat.First(p=>p.slug=="mouse").VisibleRadiusM<=80,"A small ground animal stays in its local render band");
        foreach(var p in cat.Where(p=>p.tiny&&!p.derivedFirefly&&p.slug!="butterflies-and-moths-guild"))Check(p.VisibleRadiusM==p.radiusM,"Do not expand dense tiny wildlife render radius: "+p.slug);
        Check(cat.All(p=>p.PreloadRadiusM>p.VisibleRadiusM),"Models can begin loading before crossing their draw boundary");
        var old=new PlayerSurvival();old.Restore();old.Stamina=0;old.Exertion=new ExertionState{Burst=0,Fatigue=.2,Breath=.5,ModelVersion=0};
        SurvivalRules.Advance(old,.01,60,mild,0,false);
        Check(old.Exertion.ModelVersion==1&&old.Exertion.Burst>=.9&&old.Exertion.Fatigue>.19,"Old depleted short-budget saves retain work/fatigue without inheriting false total exhaustion");
        var migrated=WorldJson.Read<PlayerSurvival>(WorldJson.Write(old));double reserve=migrated.Exertion.Burst;
        Check(!ExertionRules.Upgrade(migrated.Exertion,ExertionProfile.Human())&&migrated.Exertion.Burst==reserve,"Saving the migrated reserve cannot apply the conversion twice");
        Console.WriteLine("PASS "+n+" reported jump/swimming, weak-cue, tall-grass, reaction and draw-band regressions");
    }
}
