using System;
using System.Linq;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class CampTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception(why);}
        V at=new V(1,0,0);Possession Item(PossessionKind k)=>PortableTechnology.Create(k,at);
        CampObject Fire()=>new CampObject{Position=at,Kind=CampKind.Fire};
        var fire=Fire();Check(CampRules.IgniteReason(fire,false).Contains("hand-drill"),"Friction ignition needs an actual fire tool");
        CampRules.Add(fire,Item(PossessionKind.TinderBundle));Check(CampRules.IgniteReason(fire,true).Contains("kindling"),"Tinder alone cannot light a sustainable fire");
        CampRules.Add(fire,Item(PossessionKind.KindlingBundle));fire.Wetness=.8;Check(CampRules.IgniteReason(fire,true).Contains("wet"),"Wet tinder cannot be friction-lit");
        fire.Wetness=0;Check(CampRules.IgniteReason(fire,true)=="","Dry tinder plus kindling and drill can ignite");
        double mass=fire.MassKg;CampRules.Ignite(fire,true);Check(fire.Burning&&fire.FirePowerKw>0,"Ignition makes real heat");
        Check(CampRules.ContactDamagePerSecond(fire,.1,0)>0,"Standing in flames causes contact burns");
        Check(CampRules.ContactDamagePerSecond(fire,.8,0)==0&&CampRules.ContactDamagePerSecond(fire,.1,2)==0,"Nearby warmth and jumping above the flames are not contact burns");
        CampRules.Advance(fire,600,0,.3,0,1);Check(fire.MassKg<mass&&fire.AshKg>0,"Burning consumes physical fuel into ash");
        Check(Math.Abs((mass-fire.MassKg)*.04-fire.AshKg)<1e-9,"Fuel and ash accounting is conserved");
        CampRules.Advance(fire,86400,0,.3,0,1);Check(!fire.Burning&&fire.MassKg<.001&&fire.EmberSeconds==0,"A fire expires and cools without fuel");
        Check(CampRules.ContactDamagePerSecond(fire,.1,0)==0,"Cold ash does not burn the player");
        fire.EmberSeconds=30;Check(CampRules.ContactDamagePerSecond(fire,.1,0)>0&&CampRules.ContactDamagePerSecond(fire,.1,.5)==0,"Live embers burn bare feet only close to the ground");
        var rain=Fire();CampRules.Add(rain,Item(PossessionKind.TinderBundle));CampRules.Add(rain,Item(PossessionKind.KindlingBundle));CampRules.Ignite(rain,true);CampRules.Advance(rain,1,20,.9,5,0);Check(!rain.Burning,"Heavy rain can extinguish an exposed small fire");
        var bed=new CampObject{Kind=CampKind.Bedding,Position=at};double bare=CampRules.BedComfort(null);
        var packedGrass=Item(PossessionKind.DryGrass);packedGrass.Place=PossessionPlace.Container;packedGrass.ContainerId="worn-pack";packedGrass.GroundMoving=true;packedGrass.GroundVelocity=new V(2,0,0);
        var looseBed=new CampObject{Kind=CampKind.Bedding,Position=at};CampRules.Add(looseBed,packedGrass);var placedGrass=looseBed.Materials.Single();
        Check(placedGrass.Place==PossessionPlace.Ground&&placedGrass.ContainerId==""&&!placedGrass.GroundMoving&&placedGrass.GroundVelocity.Length==0,"Placed materials shed prior container ownership and projectile motion");
        Check(packedGrass.Place==PossessionPlace.Container&&packedGrass.ContainerId=="worn-pack"&&packedGrass.GroundMoving,"Placing copies material state without secretly mutating the inventory input");
        CampRules.Add(bed,Item(PossessionKind.DryGrass));double thin=CampRules.BedComfort(bed);for(int i=0;i<7;i++)CampRules.Add(bed,Item(PossessionKind.DryGrass));
        Check(thin>bare&&CampRules.BedComfort(bed)>thin&&bed.BeddingDepthM>.08,"Adding loose grass thickens and improves bedding");
        double dryComfort=CampRules.BedComfort(bed);bed.Wetness=1;Check(CampRules.BedComfort(bed)<dryComfort-.3,"Wet bedding loses comfort and insulation");CampRules.Advance(bed,86400,0,.2,4,1);Check(bed.Wetness<.1,"Sun and wind dry placed bedding over time");
        var hideBed=new CampObject{Kind=CampKind.Bedding,Position=at};CampRules.Add(hideBed,Item(PossessionKind.RawHide));Check(hideBed.BeddingDepthM<.02&&CampRules.BedComfort(hideBed)>bare,"A hide is a thin comfortable layer, not a straw-density mattress");
        double insulation=CampRules.BedInsulation(hideBed);hideBed.Wetness=1;Check(insulation>.1&&CampRules.BedInsulation(hideBed)<insulation,"A hide adds insulation separately from its small thickness, reduced when wet");
        var branch=WoodCrafting.Branch(new ScatterInstance{Position=at,HeightM=.03,WidthM=1.8,Kind=ScatterKind.Branch,Variant=0});
        var stake=new CampObject{Kind=CampKind.Stake,Position=at,EmbedM=.03,SoilDepthM=.25};CampRules.Add(stake,branch);
        var rock=new Possession{Kind=PossessionKind.SmallRock,MassKg=.7,WidthM=.08,HeightM=.07};double stability=stake.Stability;
        for(int i=0;i<100;i++)CampRules.Drive(stake,rock);Check(Math.Abs(stake.EmbedM-.25)<1e-9&&stake.Stability>stability,"Hammering deepens a stake only as far as available soil");
        Check(stake.Materials[0].HeightM==1.8,"Hammering changes burial, never the actual stick length");
        Check(CampRules.Drive(stake,rock)==0,"Fully driven stake stops moving");
        var deep=new CampObject{Kind=CampKind.Stake,Position=at,SoilDepthM=5};CampRules.Add(deep,branch);for(int i=0;i<300;i++)CampRules.Drive(deep,rock);Check(deep.EmbedM<=1.8*.65+1e-9,"A stick cannot be driven beyond its usable length");
        var sleeper=new PlayerSurvival{Tiredness=80,Energy=95};SleepRules.Advance(sleeper,8*3600,true,1);Check(sleeper.Tiredness<10&&sleeper.Energy==95,"Sleep pressure is independent of energy");
        sleeper=new PlayerSurvival{Tiredness=0,Energy=5};SleepRules.Advance(sleeper,3600,false);Check(sleeper.Tiredness>0&&sleeper.Energy==5,"Low energy does not imply sleepiness");
        var fatigue=new PlayerSurvival();SleepRules.Advance(fatigue,16*3600,false);Check(fatigue.Tiredness>65&&fatigue.Tiredness<80,"Normal awake day builds sleep pressure");
        var e=new SurvivalEnvironment{AirTemperatureC=20,Humidity=.4,Sleeping=true,SleepComfort=.9};var body=new PlayerSurvival{Tiredness=85,Energy=35};
        SurvivalRules.Advance(body,300,60,e,0,false);Check(body.Tiredness<65&&body.Energy>35,"Accelerated sleep still runs survival recovery");
        var warm=new PlayerSurvival();var cold=warm.Copy();var exposure=new SurvivalEnvironment{AirTemperatureC=3,WindMps=3,Humidity=.4};
        SurvivalRules.Advance(cold,60,60,exposure,0,false);exposure.RadiantHeatC=25;SurvivalRules.Advance(warm,60,60,exposure,0,false);Check(warm.CoreTemperatureC>cold.CoreTemperatureC,"A nearby fire reduces real thermal loss");
        Check(CampRules.RadiantWarmth(rain,1)>=CampRules.RadiantWarmth(rain,5),"Fire warmth attenuates with distance");
        var session=new PlaySession{Camp=new CampState{Objects={stake,bed}},Survival=body};session.Validate();var copy=session.Copy();copy.Camp.Objects[0].EmbedM=.04;
        Check(session.Camp.Objects[0].EmbedM==.25,"Camp save snapshots have independent deep material copies");
        var restored=WorldJson.Read<PlaySession>(WorldJson.Write(session));restored.Validate();Check(restored.Camp.Objects.Count==2&&restored.Camp.Objects[0].EmbedM==.25&&restored.Survival.Tiredness==body.Tiredness,"Real save format retains stakes, material layers and independent tiredness");
        var oldJson=System.Text.Json.Nodes.JsonNode.Parse(WorldJson.Write(new PlaySession()));oldJson.AsObject().Remove("Camp");oldJson["Survival"].AsObject().Remove("Tiredness");
        var old=WorldJson.Read<PlaySession>(oldJson.ToJsonString());old.Validate();Check(old.Camp!=null&&old.Camp.Objects.Count==0&&old.Survival.Tiredness==0,"Pre-camp saves acquire empty camp and rested defaults");
        bool rejected=false;try{session.Inventory.Items.Add(stake.Materials[0].Copy());session.Validate();}catch(ArgumentException){rejected=true;}Check(rejected,"Saved material cannot exist both planted and carried");
        foreach(var kind in new[]{PossessionKind.DryGrass,PossessionKind.LeafLitter,PossessionKind.TinderBundle,PossessionKind.KindlingBundle,PossessionKind.HandDrillSet})Check(Item(kind).MassKg>0,"New craft/gather item definition "+kind);
        Console.WriteLine("PASS "+n+" campfire, material, sleep, thermal, stake and save checks");
    }
}
