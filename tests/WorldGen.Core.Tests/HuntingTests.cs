using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class HuntingTests
{
    public static void Run()
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
        var human=ExertionProfile.Human();var tired=new ExertionState();ExertionRules.Advance(tired,human,300,1);
        Check(tired.Burst==0&&tired.PowerScale>.2&&tired.PowerScale<.5,"Exhaustion progressively reduces available speed instead of toggling a sprint permission");
        double power=tired.PowerScale;ExertionRules.Advance(tired,human,8,.4);Check(tired.Burst<.15,"Eight seconds of jogging cannot refill maximal sprint capacity");
        ExertionRules.Advance(tired,human,5,1);Check(tired.PowerScale<.7,"Another burst after brief jogging cannot reproduce a fresh sprint");
        var intervals=new ExertionState();for(int i=0;i<60;i++){ExertionRules.Advance(intervals,human,8,1);ExertionRules.Advance(intervals,human,8,.4);}
        Console.WriteLine($"Repeated-effort sample: reserve={intervals.Burst:F3}, fatigue={intervals.Fatigue:F3}, power={intervals.PowerScale:F3}");
        Check(intervals.Fatigue>.1&&intervals.PowerScale<.85,"Repeated efforts accumulate fatigue and cannot reproduce fresh power indefinitely");
        ExertionRules.Advance(intervals,human,600,0);Check(intervals.Burst>.95&&intervals.Breath<.01,"Substantial rest restores burst capacity and breathing");
        var jog=new ExertionState();ExertionRules.Advance(jog,human,300,.45);Check(jog.Burst>.99&&jog.Fatigue<.03,"A sustainable jog is fundamentally cheaper than maximal sprinting");
        var heavy=new ExertionState();ExertionRules.Advance(heavy,human,90,.45,20,1.8,.3);Check(heavy.Burst<jog.Burst,"Load and climbing increase physical demand");
        var hot=new ExertionState();ExertionRules.Advance(hot,human,600,.75,42);var temperate=new ExertionState();ExertionRules.Advance(temperate,human,600,.75,20);Check(hot.Heat>temperate.Heat+.25,"Hot-weather exertion accumulates substantially more heat strain");
        var flap=new ExertionState();var glide=new ExertionState();var bird=ExertionProfile.Animal("eagle",4,"air");ExertionRules.Advance(flap,bird,300,.95);ExertionRules.Advance(glide,bird,300,.08);Check(glide.Burst>flap.Burst&&glide.Fatigue<flap.Fatigue,"Flapping escape and gliding have distinct costs");
        var s=WildlifeSenses.Describe("red-kangaroo",50,false,false,false);
        double moving=WildlifePerception.Vision(s,25,1,3,1,0,1,true),still=WildlifePerception.Vision(s,25,1,0,1,0,1,true);
        Check(WildlifePerception.Vision(s,2,1,3,1,0,1,true,false,true)==0&&WildlifePerception.Hearing(s,2,1,0,0)>0,"Sleeping animals can be disturbed by sound without seeing through closed eyes");
        Check(moving>still*3,"Movement is substantially more conspicuous than a stationary form");
        double hidden=WildlifePerception.Vision(s,25,1,.5,.55,.8,.2,true);Check(hidden<still,"Crouching, cover and darkness compound concealment");
        Check(WildlifePerception.Vision(s,10,1,3,1,0,1,false)==0,"Hard line-of-sight obstruction blocks vision");
        Check(WildlifePerception.Vision(s,20,-1,3,1,0,1,true)==0,"A genuine blind direction does not see through the back of the head");
        double down=WildlifePerception.Scent(s,0,35,0,3,0),up=WildlifePerception.Scent(s,0,-35,0,3,0),cross=WildlifePerception.Scent(s,35,0,0,3,0);
        Check(down>up*20&&down>cross*20,"Airflow carries scent from an upwind hunter toward a downwind animal");
        Check(WildlifePerception.Hearing(s,20,1,0,0)>WildlifePerception.Hearing(s,20,1,8,4),"Wind and rain mask movement sounds");
        Check(WildlifePerception.Hearing(s,20,0,0,0)==0,"A motionless silent character does not create movement sound");
        var aware=new WildlifeAwarenessState();WildlifePerception.Observe(aware,s,.3,.4,0,0,10,20);Check(aware.Suspicion<.1,"First noticing something is not instant complete identification");
        for(int i=0;i<40;i++)WildlifePerception.Observe(aware,s,.3,.4,0,0,10,20,15,30);
        Check(aware.Mode==WildlifeAwareness.Fleeing&&aware.MemorySeconds>0,"Sustained evidence confirms a threat");
        double x=aware.LastX,z=aware.LastZ;WildlifePerception.Observe(aware,s,5,0,0,0,999,999);Check(aware.LastX==x&&aware.LastZ==z&&aware.MemorySeconds>0,"Losing contact preserves last-known position, without following an unseen player");
        var individual=new WildlifeIndividual{Id=7,Species="red-kangaroo",Position=new V(1,0,0),MaximumHealth=100,Health=100,Exertion=hot.Copy(),Awareness=aware.Copy(),ThreatPosition=new V(1,0,0)};
        var loaded=WorldJson.Read<WildlifeIndividual>(WorldJson.Write(individual));loaded.Validate();Check(loaded.Exertion.Burst==hot.Burst&&loaded.Awareness.LastX==x,"Awareness and exertion survive the actual save serializer");
        var copy=individual.Copy();copy.Exertion.Burst=1;copy.Awareness.Suspicion=0;Check(individual.Awareness.Suspicion>0&&individual.Exertion.Burst!=1,"Saved animal copies do not share mutable effort or awareness");
        var back=WindHudValues.From(0,3,0);var front=WindHudValues.From(0,-3,0);Check(Math.Abs(back.Angle-Math.PI/2)<.001&&Math.Abs(front.Angle+Math.PI/2)<.001,"Wind source behind is at the HUD bottom; source ahead is at the top");
        Check(WindHudValues.From(.2,0,0).Strength==0&&WindHudValues.From(4,0,0).Strength>.9,"Weak indistinct airflow fades out, stronger wind gives a clear cue");
        var catalogue=WorldJson.Read<FaunaPrototypeCatalogue>(File.ReadAllText("unity/WorldGen.Unity/Assets/WorldGen/Resources/WorldGenFaunaCatalogue.json")).animals;
        Check(catalogue.Length==177&&WildlifeNaturalHistory.Profiles.Count==catalogue.Length,"Natural-history review covers every one of the 177 model families");
        foreach(var actor in catalogue)
        {
            Check(WildlifeNaturalHistory.TryGet(actor.slug,out var natural),"Explicit natural history: "+actor.slug);
            var behaviour=WildlifeBehaviour.Describe(actor.slug,Math.Max(.001,Math.Pow(actor.lengthM,3)),Food.Vertebrate|Food.Fish,actor.medium=="air",actor.tiny);
            var senses=WildlifeSenses.Describe(actor.slug,1,behaviour.Predator,actor.medium=="air",actor.tiny);
            Check(behaviour.HasNaturalHistory&&natural.GroupMin>=1&&natural.GroupMax>=natural.GroupMin&&natural.GroupMax<=64,"Bounded group and explicit profile: "+actor.slug);
            Check(senses.VisionM>0&&senses.HearingM>0&&senses.SmellM>0&&senses.FieldDegrees>0&&senses.FieldDegrees<=360&&senses.ConfirmSeconds>0,"Finite useful senses: "+actor.slug);
            if(actor.medium=="ground"&&!actor.tiny)
                Check(WildlifeLocomotion.TryGround(actor.slug,out var pace)&&pace.WalkMps>0&&pace.BurstMps>pace.WalkMps&&pace.BurstMps<=25,"Explicit physical ground pace: "+actor.slug);
        }
        double Burst(string slug){WildlifeLocomotion.TryGround(slug,out var pace);return pace.BurstMps;}
        Check(Burst("hare")>12&&Burst("wolf")>12&&Burst("rabbit")>8,"Fast small prey and predators cannot be casually out-jogged because of short model length");
        Check(Burst("tortoise")<.3&&Burst("sloth")<.2&&Burst("python")<2,"Slow walkers and a long snake cannot inherit absurd body-length running speeds");
        Check(Burst("elephant")<9&&Burst("pronghorn")>20,"A giant body does not automatically outrun specialized cursorial prey");
        var paced=catalogue.First(a=>a.slug=="wolf");double sourcePace=paced.runMps;paced.PrepareLocomotion();paced.PrepareLocomotion();
        Check(paced.runMps>12&&paced.ClipSpeed("run")==sourcePace,"Repeated profile preparation preserves the unchanged animation's nominal stride reference");
        Check(WildlifeLocomotion.ObstacleLookahead(14,14,.35,.3)>7&&WildlifeLocomotion.ObstacleLookahead(14,14,1.2,.3)>18&&WildlifeLocomotion.ObstacleLookahead(100,1,3,1)<=40,"Fast obstacle lookahead spans the scheduled decision interval and braking with a bounded ray length");
        Check(WildlifeNaturalHistory.Native("deer","Muntjac").Social==WildlifeSocial.Solitary&&WildlifeNaturalHistory.Native("deer","Chital").GroupMax>8,"Different native deer can share a model without sharing herd organization");
        var kangaroo=WildlifeBehaviour.Describe("red-kangaroo",60,Food.Grass);
        Check(!kangaroo.SeasonalRut&&!WildlifeBehaviour.Rut(kangaroo,.8,-35),"Kangaroo mobs do not inherit northern ungulate autumn rut");
        var bison=WildlifeBehaviour.Describe("bison",700,Food.Grass);Check(WildlifeBehaviour.Rut(bison,.6,45)&&!WildlifeBehaviour.Rut(bison,.85,45)&&WildlifeBehaviour.Rut(bison,.1,-45),"Bison rut in local summer rather than inheriting the deer autumn window");
        Check(!WildlifeBehaviour.Defend(bison,7,false,false,false)&&WildlifeBehaviour.Defend(bison,7,false,false,true),"A rutting male protects a wider personal space without changing the normal encounter rule");
        var snowy=WildlifeBehaviour.Describe("snowy-owl",2,Food.Vertebrate,true);
        var vulture=WildlifeBehaviour.Describe("vulture",6,Food.Vertebrate,true);Check(vulture.CanScavenge&&!vulture.CanHunt&&!vulture.ThreatensPrey,"A carrion specialist seeks carcasses without automatically hunting live nearby animals");
        foreach(string opportunist in new[]{"raven","gull"})
        {var p=WildlifeBehaviour.Describe(opportunist,1,Food.Vertebrate|Food.Fish,true);Check(p.CanScavenge&&p.CanHunt&&!p.Predator,"An opportunistic "+opportunist+" can take small live prey and carrion without acquiring apex-predator behavior");}
        Check(WildlifeBehaviour.Describe("goose",4,Food.Grass,true).VFormation&&!WildlifeBehaviour.Describe("tern",.2,Food.Fish,true).VFormation,"Migration does not force terns and other small flocking birds into a goose V");
        Check(!WildlifeBehaviour.IsNocturnal("snowy-owl")&&WildlifeBehaviour.Activity(snowy,12,false,0,0,1)>.3,"Snowy owls retain daytime hunting activity");
        var wolf=WildlifeNaturalHistory.Native("wolf","");
        Check(!WildlifeNaturalHistory.HuntPerson(wolf,.7,1,true,false)&&!WildlifeNaturalHistory.HuntPerson(wolf,1,.9,true,false),"Ordinary hungry wolves do not automatically hunt people because they have a pack");
        Check(WildlifeNaturalHistory.HuntPerson(WildlifeNaturalHistory.Native("polar-bear",""),.7,.8,false,false),"Polar bears retain a distinct plausible predatory response");
        Check(!WildlifeNaturalHistory.HuntPerson(WildlifeNaturalHistory.Native("sea-lion",""),1,1,true,true),"Sea lions never acquire big-cat predation from a substring");
        Check(!WildlifeNaturalHistory.HuntPerson(WildlifeNaturalHistory.Native("crocodile",""),1,1,true,false),"Water ambush requires a reachable waterside victim");
        Check(!WildlifeNaturalHistory.CompatiblePrey(new Taxon{Hunts=Stratum.Ground},new Taxon{Occupies=Stratum.Water})&&WildlifeNaturalHistory.CompatiblePrey(new Taxon{Hunts=Stratum.Water},new Taxon{Occupies=Stratum.Water}),"Predation respects hunting strata, including fish");
        var faint=new WildlifeAwarenessState();WildlifePerception.Observe(faint,s,.3,.15,0,0,47,-21);
        Check(faint.LastX==47&&faint.LastZ== -21,"An uncertain initial cue has a direction instead of pointing to the world origin");
        var leader=new WildlifeAwarenessState{ThreatIsPlayer=true};WildlifePerception.Alarm(leader,47,-21);leader.LastCueSeconds=9.6;
        var follower=new WildlifeAwarenessState();Check(WildlifePerception.RelayAlarm(follower,leader)&&follower.ThreatIsPlayer&&follower.LastCueSeconds>10,"A social alarm retains its source and ages rather than renewing indefinitely");
        Check(!WildlifePerception.RelayAlarm(new WildlifeAwarenessState(),follower),"An old social cue cannot keep circulating through a herd");
        Check(WildlifePerception.ScentHeight(25)<.025&&WildlifePerception.ScentHeight(0)==1,"A hunter on a high cliff is not smelled as though beside the animal");
        Check(WildlifeNaturalHistory.ThermalMobility(WildlifeNaturalHistory.Native("native-small-lizards-guild",""),0,false)<.2&&WildlifeNaturalHistory.ThermalMobility(WildlifeNaturalHistory.Native("small-native-freshwater-fish-guild",""),-20,true)==1,"Cold air slows terrestrial ectotherms without directly chilling deep-water fish");
        Check(WildlifeNaturalHistory.ThermalMobility(WildlifeNaturalHistory.Native("freshwater-turtle",""),-10,true)==1,"A submerged reptile also avoids using air temperature as its water temperature");
        Check(ExertionProfile.Animal("red-kangaroo",60,"ground").Aerobic>ExertionProfile.Human().Aerobic&&ExertionProfile.Animal("pronghorn",50,"ground").BurstSeconds>60,"Efficient hopping and pronghorn endurance retain distinct exertion priors");
        var desert=new ExertionState();var arctic=new ExertionState();ExertionRules.Advance(desert,ExertionProfile.Animal("camel",500,"ground"),300,.8,38);ExertionRules.Advance(arctic,ExertionProfile.Animal("polar-bear",500,"ground"),300,.8,38);
        Check(desert.Heat<arctic.Heat,"Desert adaptations and an Arctic bear's heat sensitivity do not share one heat tolerance");
        desert=new ExertionState();arctic=new ExertionState();ExertionRules.Advance(desert,ExertionProfile.Animal("camel",500,"ground"),300,1,-10);ExertionRules.Advance(arctic,ExertionProfile.Animal("polar-bear",500,"ground"),300,1,-10);
        Check(arctic.Heat>desert.Heat*2,"Species heat tolerance also applies to internal exercise heat in cold weather");
        var diurnal=WildlifeBehaviour.Describe("squirrel",.5,Food.Seed);Check(WildlifeBehaviour.Activity(diurnal,12,false,0,0,0)<WildlifeBehaviour.Activity(diurnal,12,false,0,0,1),"Diel activity follows actual illumination during polar day/night");
        individual.HuntTargetId=9;individual.HuntMemorySeconds=12;individual.HuntLastPosition=new V(0,1,0);
        loaded=WorldJson.Read<WildlifeIndividual>(WorldJson.Write(individual));loaded.Validate();Check(loaded.HuntMemorySeconds==12&&loaded.HuntLastPosition.Y==1,"A predator's last seen prey location survives serialization");
        Console.WriteLine("PASS "+count+" hunting senses, memory, wind and shared exertion checks");
    }
}
