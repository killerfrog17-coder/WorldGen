using System;
using System.Text.Json.Nodes;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class SurvivalHudTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string s){n++;if(!ok)throw new Exception(s);}
        var v=SurvivalHudValues.From(new PlayerSurvival{Health=90,Energy=90,Stamina=85,Thirst=25,Hunger=35},22);
        Check(Math.Abs(v.Health-.9)<1e-6&&Math.Abs(v.Hydration-(.9-.15/.65*.4))<1e-6&&Math.Abs(v.Fullness-.925)<1e-6,"Early appetite/thirst remain distinct from starvation or severe dehydration");
        var need=new PlayerSurvival();need.Restore();PlayerMetabolism.Advance(need,1,0,0,2.4);
        var hungry=SurvivalHudValues.From(need,22);Check(Math.Abs(hungry.Fullness-.5)<1e-6&&hungry.FoodAttention>0,"One comfortable day without food shows half a stomach and intermittent attention");
        PlayerMetabolism.Advance(need,2,0,0,0);Check(SurvivalHudValues.From(need,22).Fullness>.28,"Several fasting days do not display an empty icon while ordinary reserves remain");
        need.ReserveKcal=0;Check(SurvivalHudValues.From(need,22).Fullness<=.06,"True reserve depletion leaves only a critical sliver");
        need.Health=0;Check(SurvivalHudValues.From(need,22).Fullness==0,"Lethal starvation fully empties the food icon");need.Health=100;
        need.WaterDeficitLitres=2.2;var parched=SurvivalHudValues.From(need,22);need.WaterDeficitLitres=7.4;
        Check(parched.Hydration>.35&&SurvivalHudValues.From(need,22).Hydration<.02,"Parched and critically dehydrated remain visibly distinct");
        Check(Math.Abs(SurvivalHudValues.FeltHydration(1)-.5)<1e-6,"A one-litre deficit is the quite-thirsty half-icon tuning point");
        foreach(double minutes in new[]{15d,60d,1440d})
        {var day=new PlayerSurvival();day.Restore();SurvivalRules.Advance(day,minutes*60,minutes,new SurvivalEnvironment{AirTemperatureC=24,Humidity=.5},0,false);Check(Math.Abs(SurvivalHudValues.From(day,22).Fullness-.5)<.001,"Food feeling follows solar days at day length "+minutes);}
        var fed=new PlayerSurvival();fed.Restore();PlayerMetabolism.Advance(fed,1,0,0,0);double daysBefore=fed.FoodNeedDays;
        PlayerMetabolism.Eat(fed,.02,50,0);Check(fed.FoodNeedDays>daysBefore-.03,"A few berries do not reset a whole day of hunger");
        var feelingSave=WorldJson.Read<PlayerSurvival>(WorldJson.Write(fed));feelingSave.Validate();Check(feelingSave.FoodNeedDays==fed.FoodNeedDays,"Felt food deficit persists through saves");
        for(double water=0;water<=8;water+=.05)Check(SurvivalHudValues.FeltHydration(water)>=SurvivalHudValues.FeltHydration(water+.05),"Felt hydration decreases continuously as the deficit grows");
        need.FoodExposures=new System.Collections.Generic.List<FoodExposure>{new FoodExposure{DelaySeconds=3600,RemainingDamage=30,RemainingSeconds=10800}};
        Check(SurvivalHudValues.From(need,22).Sickness==0,"Delayed poison cannot be detected through the HUD before symptoms");
        need.FoodExposures[0].DelaySeconds=0;Check(SurvivalHudValues.From(need,22).Sickness==1,"Active nausea colours the stomach");
        need.FoodExposures.Clear();need.WaterSicknessSeconds=3600;need.WaterSicknessSeverity=.7;need.WaterSicknessDelaySeconds=120;
        Check(SurvivalHudValues.From(need,22).Sickness==0,"Delayed waterborne illness also stays hidden");need.WaterSicknessDelaySeconds=0;
        Check(Math.Abs(SurvivalHudValues.From(need,22).Sickness-.7)<1e-6,"Active waterborne sickness contributes felt nausea");
        Check(Math.Abs(SurvivalHudValues.ThermalWarning(.5f)-.85f)<.0001&&SurvivalHudValues.ThermalWarning(0)==0&&SurvivalHudValues.ThermalWarning(1)==1,"Thermal warning shows the requested early 50-to85 response with exact endpoints");
        foreach(float temp in new[]{19f,22f,24f}){v.FeelsLikeC=temp;Check(v.SnowOpacity==0&&v.SunOpacity==0,"Comfort has no symbol");}
        v.FeelsLikeC=13;Check(v.SnowOpacity>0&&v.SnowOpacity<1&&v.SunOpacity==0,"Chilly snowflake fades in alone");
        v.FeelsLikeC=8;Check(v.SnowOpacity==1&&v.SunOpacity==0,"Snowflake fully visible at8C");
        v.FeelsLikeC=31;Check(v.SunOpacity>0&&v.SunOpacity<1&&v.SnowOpacity==0,"Warm sun fades in alone");
        v.FeelsLikeC=33;Check(v.SunOpacity==1&&v.SnowOpacity==0,"Sun fully visible at33C");
        for(int t=-60;t<65;t++){v.FeelsLikeC=t;Check(v.SunOpacity==0||v.SnowOpacity==0,"Temperature symbols never crossfade over each other");}
        foreach(float d in new[]{0f,.25f,.5f,.75f,1f})
        {
            Check(SurvivalHudValues.GlowOpacity(0,d)==0,"Zero stress has no glow");
            float a=SurvivalHudValues.GlowOpacity(.99f,d),b=SurvivalHudValues.GlowOpacity(1,d);
            Check(b>=a&&b-a<.02,"99-to100 stress remains continuous across the full radius");
        }
        var s=new PlayerSurvival{Energy=25,Stamina=100};
        v=SurvivalHudValues.From(s,22);Check(v.Stamina==v.Energy&&s.MaximumStamina==s.Energy,"Energy is the exact stamina cap");
        var cold=new SurvivalEnvironment{AirTemperatureC=-10,Humidity=.4,WindMps=10};
        double felt=SurvivalRules.FeelsLike(cold,0,0);
        Check(felt<cold.AirTemperatureC,"Wind worsens perceived cold");
        Check(SurvivalRules.FeelsLike(cold,0,6)>felt,"Running feels warmer than standing");
        Check(SurvivalRules.FeelsLike(cold,1,0)<felt,"Existing wetness hook worsens perceived cold");
        var fresh=new PlayerSurvival();var before=SurvivalHudValues.From(fresh,felt);
        Check(before.SnowOpacity==1&&before.BodyStress==0,"New cold exposure changes the icon before core stress");
        SurvivalRules.Advance(fresh,1,60,cold,0,false);
        Check(fresh.Health==100&&Math.Abs(fresh.CoreTemperatureC-37)<.2,"Freezing weather does not instantly damage a warm body");
        var neutral=new SurvivalEnvironment{AirTemperatureC=24,Humidity=.5};
        var collapse=new PlayerSurvival{CoreTemperatureC=29.9};SurvivalRules.Advance(collapse,.25,60,neutral,0,false);
        Check(collapse.Dead&&!collapse.Unconscious&&collapse.DeathCause==PlayerDeathCause.Cold&&collapse.MovementScale==0&&!collapse.CanSprint,"Critical cold causes actual death instead of unconsciousness");
        var oldColdKnockout=new PlayerSurvival{CoreTemperatureC=31,Unconscious=true};SurvivalRules.Advance(oldColdKnockout,.1,60,neutral,0,false);
        Check(oldColdKnockout.Dead&&!oldColdKnockout.Unconscious&&oldColdKnockout.DeathCause==PlayerDeathCause.Cold,"Existing saved cold knockouts resolve to death on resuming survival");
        Check(SurvivalHudValues.From(collapse,22).BodyStress== -1,"Full cold glow marks critical core temperature even in comfortable air");
        var hot=new PlayerSurvival{CoreTemperatureC=40.6};var dangerous=new SurvivalEnvironment{AirTemperatureC=50,Humidity=.95,Sunlight=1};
        SurvivalRules.Advance(hot,.25,60,dangerous,0,false);
        Check(hot.Unconscious&&!hot.Dead&&SurvivalHudValues.From(hot,22).BodyStress==1,"Full heat glow marks heat-related incapacitation, not automatic death");
        var saved=WorldJson.Read<PlayerSurvival>(WorldJson.Write(hot));saved.Validate();Check(saved.Unconscious,"Incapacitation persists in saves");
        var old=JsonNode.Parse(WorldJson.Write(hot));old.AsObject().Remove("Unconscious");
        Check(!WorldJson.Read<PlayerSurvival>(old.ToJsonString()).Unconscious,"Old survival states tolerate missing unconsciousness flag");
        hot.CoreTemperatureC=40.4;SurvivalRules.Advance(hot,.1,60,neutral,0,false);Check(hot.Unconscious,"Recovery hysteresis prevents threshold flutter");
        hot.CoreTemperatureC=39.4;SurvivalRules.Advance(hot,.1,60,neutral,0,false);Check(!hot.Unconscious&&!hot.Dead,"Cooling can restore consciousness");
        var fatal=new PlayerSurvival{CoreTemperatureC=40.6};SurvivalRules.Advance(fatal,600,60,dangerous,0,false);
        Check(fatal.Dead&&fatal.DeathCause==PlayerDeathCause.Heat,"Sustained severe hyperthermia remains potentially fatal");
        var freezing=new PlayerSurvival();SurvivalRules.Advance(freezing,800,60,new SurvivalEnvironment{AirTemperatureC=0,WindMps=5,Humidity=.5},0,false);
        Check(!freezing.Unconscious&&freezing.Dead&&freezing.DeathCause==PlayerDeathCause.Cold,"Naked freezing exposure accumulates over hours and ends in death");
        collapse.Restore();Check(!collapse.Unconscious&&!collapse.Incapacitated,"Creative reset clears incapacitation");
        Console.WriteLine("PASS "+n+" HUD bindings/thermal feedback/incapacitation assertions.");
    }
}
