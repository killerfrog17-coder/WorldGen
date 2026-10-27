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
        Check(Math.Abs(v.Health-.9)<1e-6&&Math.Abs(v.Hydration-.75)<1e-6&&Math.Abs(v.Fullness-.65)<1e-6,"Resource bindings use remaining water/food, not depletion");
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
