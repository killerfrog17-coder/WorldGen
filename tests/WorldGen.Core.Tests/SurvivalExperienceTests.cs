using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
using System.Text.Json.Nodes;

static class SurvivalExperienceTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var mild=new SurvivalEnvironment{AirTemperatureC=22,Humidity=.5,LocalHour=12};
        double Lifetime(double minutes,bool feed,bool drink,double speed=0)
        {
            var s=new PlayerSurvival();double hours=0;
            while(!s.Dead&&hours<60*24)
            {
                if(drink)PlayerMetabolism.Hydrate(s,.25);
                if(feed&&hours%24==0)PlayerMetabolism.Eat(s,1,3000,0);
                SurvivalRules.Advance(s,minutes*60/24,minutes,mild,speed,false);hours++;
            }
            Check(s.Dead&&s.DeathCause==(drink?PlayerDeathCause.Starvation:PlayerDeathCause.Dehydration),"The depleted physiological store determines death, not an empty feeling bar");
            return hours/24;
        }
        double rest=Lifetime(60,false,true),active=Lifetime(60,false,true,3.4),dry=Lifetime(60,true,false);
        Check(rest>26&&rest<45&&active<rest&&active>12,"Starvation with water takes weeks; sustained activity consumes reserves faster");
        Check(dry>2&&dry<4,"Comfortable unwatered survival is roughly three physiological days");
        Check(Math.Abs(Lifetime(15,true,false)-dry)<.1&&Math.Abs(Lifetime(120,true,false)-dry)<.1,"Changing day duration automatically preserves physiological dehydration time");
        var hungry=new PlayerSurvival();for(int i=0;i<24;i++){PlayerMetabolism.Hydrate(hungry,.25);SurvivalRules.Advance(hungry,150,60,mild,0,false);}
        Check(hungry.Hunger==100&&hungry.Health==100&&hungry.ReserveKcal>60000,"An empty stomach after a day is uncomfortable without immediately starving a healthy body");
        double reserve=hungry.ReserveKcal;PlayerMetabolism.Eat(hungry,.3,900,.2);
        Check(hungry.Hunger<50&&hungry.DigestingKcal>=900&&hungry.ReserveKcal==reserve,"A meal fills the stomach immediately and enters digestion instead of instantly creating body fat");
        double water=hungry.WaterDeficitLitres;PlayerMetabolism.Hydrate(hungry,.25);Check(hungry.WaterDeficitLitres<=water,"Water replaces an actual accumulated fluid deficit");
        var saved=WorldJson.Read<PlayerSurvival>(WorldJson.Write(hungry));saved.Validate();Check(saved.ReserveKcal==hungry.ReserveKcal&&saved.DigestingKcal==hungry.DigestingKcal,"Nutritional stores persist exactly through real save serialization");
        var old=JsonNode.Parse(WorldJson.Write(new PlayerSurvival{Hunger=100}));foreach(string key in new[]{"MetabolismVersion","DigestingKcal","ReserveKcal","WaterDeficitLitres","SleepPhaseHour","SleepBoutSeconds","LastSleepHour"})old.AsObject().Remove(key);
        var migrated=WorldJson.Read<PlayerSurvival>(old.ToJsonString());migrated.Validate();Check(migrated.ReserveKcal==PlayerMetabolism.NormalReserveKcal&&migrated.Hunger==100,"Legacy hungry saves do not acquire a retroactive starvation death sentence");
        V at=new V(1,0,0);var fire=new CampObject{Kind=CampKind.Fire,Position=at};
        CampRules.Add(fire,PortableTechnology.Create(PossessionKind.TinderBundle,at));CampRules.Add(fire,PortableTechnology.Create(PossessionKind.KindlingBundle,at));
        var branch=WoodCrafting.Branch(new ScatterInstance{Kind=ScatterKind.Branch,WidthM=1.8,HeightM=.055,Position=at});double originalMass=branch.MassKg;
        CampRules.Add(fire,branch);var wood=fire.Materials.Where(WoodCrafting.Wood).ToArray();
        Check(wood.Length>1&&wood.All(p=>p.HeightM<=.5)&&Math.Abs(wood.Sum(p=>p.MassKg)-originalMass)<1e-9,"Adding a long branch splits it into short fuel pieces without inventing or losing wood");
        Check(wood.Select(p=>p.Id).Distinct().Count()==wood.Length,"Broken firewood pieces have distinct persistent ownership IDs");
        CampRules.Ignite(fire,true);double initialPower=fire.FirePowerKw,elapsed=0;
        var meat=PortableTechnology.Create(PossessionKind.RawMeat,at);fire.CookingItems.Add(meat);
        while(fire.Burning&&elapsed<24*3600){CampRules.Advance(fire,60,0,.3,0,0);elapsed+=60;}
        Check(elapsed>2*3600&&elapsed<12*3600,"A substantial real wood load sustains a campfire for hours, not a fixed short countdown");
        Check(!fire.Burning&&fire.EmberSeconds>0&&fire.FirePowerKw<initialPower,"Flames diminish into a lower-power ember stage instead of abruptly going cold");
        Check(meat.Cooked&&meat.Burnt&&meat.Name.Contains("Charred"),"Food cooks over actual burning time and chars if abandoned on the fire");
        var session=new PlaySession{Camp=new CampState{Objects={fire}}};session.Validate();var copy=WorldJson.Read<PlaySession>(WorldJson.Write(session));copy.Validate();
        Check(copy.Camp.Objects[0].CookingItems.Single().Burnt,"Cooking state and food ownership survive saves");
        bool rejected=false;try{session.Inventory.Items.Add(meat.Copy());session.Validate();}catch(ArgumentException){rejected=true;}Check(rejected,"Food cannot simultaneously exist over a campfire and in an inventory");
        var bed=new CampObject{Kind=CampKind.Bedding,Position=at};double bare=CampRules.BedComfort(null);CampRules.Add(bed,PortableTechnology.Create(PossessionKind.DryGrass,at));double thin=CampRules.BedComfort(bed);
        for(int i=0;i<7;i++)CampRules.Add(bed,PortableTechnology.Create(PossessionKind.DryGrass,at));
        Check(thin>bare&&thin-bare<.1&&CampRules.BedComfort(bed)>thin+.15,"One grass bundle helps a little; a layered bed helps substantially more");
        var sleeper=new PlayerSurvival{Tiredness=30,SleepBoutSeconds=3*3600,MetabolismVersion=1,ReserveKcal=65000};
        Check(SleepRules.WakeDrive(sleeper,15)>SleepRules.WakeDrive(sleeper,3),"The body clock promotes afternoon wakefulness and night-time sleep");
        Check(SleepRules.WakeNaturally(sleeper,15,1)&&!SleepRules.WakeNaturally(sleeper,3,0),"Daytime naps can end in shorter bouts while the same tired sleeper remains asleep at night");
        sleeper.SleepBoutSeconds=5*3600;Check(SleepRules.WakeNaturally(sleeper,7,1)&&!SleepRules.WakeNaturally(sleeper,7,0),"Morning light increases natural waking without imposing an exact alarm time");
        Console.WriteLine($"PASS {checks} physiological hunger, timescale, fire, cooking and circadian checks; starvation {rest:0.0} days resting/{active:0.0} active; dehydration {dry:0.0} days");
    }
}
