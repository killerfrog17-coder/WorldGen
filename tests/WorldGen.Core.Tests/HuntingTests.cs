using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class HuntingTests
{
    public static void Run()
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
        var human=ExertionProfile.Human();var tired=new ExertionState();ExertionRules.Advance(tired,human,60,1);
        Check(tired.Burst==0&&tired.PowerScale>.2&&tired.PowerScale<.5,"Exhaustion progressively reduces available speed instead of toggling a sprint permission");
        double power=tired.PowerScale;ExertionRules.Advance(tired,human,8,.4);Check(tired.Burst<.15,"Eight seconds of jogging cannot refill maximal sprint capacity");
        ExertionRules.Advance(tired,human,5,1);Check(tired.PowerScale<.7,"Another burst after brief jogging cannot reproduce a fresh sprint");
        var intervals=new ExertionState();for(int i=0;i<12;i++){ExertionRules.Advance(intervals,human,8,1);ExertionRules.Advance(intervals,human,8,.4);}
        Check(intervals.Fatigue>0&&intervals.PowerScale<.7,"Repeated short bursts accumulate fatigue across the recovery intervals");
        ExertionRules.Advance(intervals,human,600,0);Check(intervals.Burst>.95&&intervals.Breath<.01,"Substantial rest restores burst capacity and breathing");
        var jog=new ExertionState();ExertionRules.Advance(jog,human,300,.45);Check(jog.Burst>.99&&jog.Fatigue<.03,"A sustainable jog is fundamentally cheaper than maximal sprinting");
        var heavy=new ExertionState();ExertionRules.Advance(heavy,human,90,.45,20,1.8,.3);Check(heavy.Burst<jog.Burst,"Load and climbing increase physical demand");
        var hot=new ExertionState();ExertionRules.Advance(hot,human,600,.75,42);var temperate=new ExertionState();ExertionRules.Advance(temperate,human,600,.75,20);Check(hot.Heat>temperate.Heat+.25,"Hot-weather exertion accumulates substantially more heat strain");
        var flap=new ExertionState();var glide=new ExertionState();var bird=ExertionProfile.Animal("eagle",4,"air");ExertionRules.Advance(flap,bird,300,.95);ExertionRules.Advance(glide,bird,300,.08);Check(glide.Burst>flap.Burst&&glide.Fatigue<flap.Fatigue,"Flapping escape and gliding have distinct costs");
        var s=WildlifeSenses.Describe("red-kangaroo",50,false,false,false);
        double moving=WildlifePerception.Vision(s,25,1,3,1,0,1,true),still=WildlifePerception.Vision(s,25,1,0,1,0,1,true);
        Check(moving>still*3,"Movement is substantially more conspicuous than a stationary form");
        double hidden=WildlifePerception.Vision(s,25,1,.5,.55,.8,.2,true);Check(hidden<still,"Crouching, cover and darkness compound concealment");
        Check(WildlifePerception.Vision(s,10,1,3,1,0,1,false)==0,"Hard line-of-sight obstruction blocks vision");
        Check(WildlifePerception.Vision(s,20,-1,3,1,0,1,true)==0,"A genuine blind direction does not see through the back of the head");
        double down=WildlifePerception.Scent(s,0,35,0,3,0),up=WildlifePerception.Scent(s,0,-35,0,3,0),cross=WildlifePerception.Scent(s,35,0,0,3,0);
        Check(down>up*20&&down>cross*20,"Airflow carries scent from an upwind hunter toward a downwind animal");
        Check(WildlifePerception.Hearing(s,20,1,0,0)>WildlifePerception.Hearing(s,20,1,8,4),"Wind and rain mask movement sounds");
        Check(WildlifePerception.Hearing(s,20,0,0,0)==0,"A motionless silent character does not create movement sound");
        var aware=new WildlifeAwarenessState();WildlifePerception.Observe(aware,s,.3,.4,0,0,10,20);Check(aware.Suspicion<.1,"First noticing something is not instant complete identification");
        for(int i=0;i<30;i++)WildlifePerception.Observe(aware,s,.3,.4,0,0,10,20);
        Check(aware.Mode==WildlifeAwareness.Fleeing&&aware.MemorySeconds>0,"Sustained evidence confirms a threat");
        double x=aware.LastX,z=aware.LastZ;WildlifePerception.Observe(aware,s,5,0,0,0,999,999);Check(aware.LastX==x&&aware.LastZ==z&&aware.MemorySeconds>0,"Losing contact preserves last-known position, without following an unseen player");
        var individual=new WildlifeIndividual{Id=7,Species="red-kangaroo",Position=new V(1,0,0),MaximumHealth=100,Health=100,Exertion=hot.Copy(),Awareness=aware.Copy(),ThreatPosition=new V(1,0,0)};
        var loaded=WorldJson.Read<WildlifeIndividual>(WorldJson.Write(individual));loaded.Validate();Check(loaded.Exertion.Burst==hot.Burst&&loaded.Awareness.LastX==x,"Awareness and exertion survive the actual save serializer");
        var copy=individual.Copy();copy.Exertion.Burst=1;copy.Awareness.Suspicion=0;Check(individual.Awareness.Suspicion>0&&individual.Exertion.Burst!=1,"Saved animal copies do not share mutable effort or awareness");
        var back=WindHudValues.From(0,3,0);var front=WindHudValues.From(0,-3,0);Check(Math.Abs(back.Angle-Math.PI/2)<.001&&Math.Abs(front.Angle+Math.PI/2)<.001,"Wind source behind is at the HUD bottom; source ahead is at the top");
        Check(WindHudValues.From(.2,0,0).Strength==0&&WindHudValues.From(4,0,0).Strength>.9,"Weak indistinct airflow fades out, stronger wind gives a clear cue");
        Console.WriteLine("PASS "+count+" hunting senses, memory, wind and shared exertion checks");
    }
}
