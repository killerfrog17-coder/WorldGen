using System;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class PlayerDeathTests
{
    public static void Run()
    {
        int count=0;void Check(bool ok,string text){count++;if(!ok)throw new Exception(text);}
        foreach(double dt in new[]{1d/240,1d/60,1d/20,.25})
        foreach(double height in new[]{.45,2d,3,4,5,6,8,10,25})
        {
            var motor=new TerrainMotion{Y=TerrainMotion.EyeHeightM+height};double impact=0;
            for(int i=0;i<20000&&!motor.Grounded;i++){motor.Move(dt,0,0,0,0,0,false,false,(x,z)=>0);impact=Math.Max(impact,motor.LandingSpeedMps);}
            Check(motor.Grounded&&Math.Abs(PlayerDamageRules.EquivalentFallMeters(impact)-height)<1e-8,"Frame-independent contact speed at "+height+" / "+dt);
            var s=new PlayerSurvival();PlayerDamageRules.Apply(s,GameMode.Survival,PlayerDamageRules.FallDamage(impact),PlayerDeathCause.Fall);
            Check(height<=3?s.Health>99.999:height>=10?s.Health<1e-8:s.Health>0&&s.Health<100,"Fall balance "+height);
            motor.Move(dt,0,0,0,0,0,false,false,(x,z)=>0);Check(motor.LandingSpeedMps==0,"Stationary contact not repeated");
        }
        var grounded=new TerrainMotion();grounded.Place(0,0,0);grounded.Move(.1,0,0,0,0,0,false,false,(x,z)=>100);
        Check(grounded.LandingSpeedMps==0,"Terrain refinement cannot invent fall energy");
        var jump=new TerrainMotion();jump.Place(0,0,0);jump.Move(.02,0,0,0,0,0,false,true,(x,z)=>0);
        double jumpImpact=0;for(int i=0;i<200&&!jump.Grounded;i++){jump.Move(.02,0,0,0,0,0,false,false,(x,z)=>0);jumpImpact=Math.Max(jumpImpact,jump.LandingSpeedMps);}
        Check(jump.Grounded&&PlayerDamageRules.FallDamage(jumpImpact)==0,"Ordinary jump is harmless");
        foreach(var mode in new[]{GameMode.Creative,GameMode.Spectator}){var s=new PlayerSurvival();PlayerDamageRules.Apply(s,mode,1000,PlayerDeathCause.Fall);Check(s.Health==100,"Mode fall immunity "+mode);}
        var dead=new PlayerSurvival();PlayerDamageRules.Apply(dead,GameMode.Survival,100,PlayerDeathCause.Fall);
        Check(dead.Dead&&dead.DeathCause==PlayerDeathCause.Fall&&!dead.Unconscious,"Fatal impact is actual death");
        var restored=WorldJson.Read<PlayerSurvival>(WorldJson.Write(dead));restored.Validate();Check(restored.Dead&&restored.DeathCause==PlayerDeathCause.Fall,"Dead fall save round trip");
        dead.Restore();Check(!dead.Incapacitated&&dead.DeathCause==PlayerDeathCause.None&&dead.Health==100&&dead.Hunger==0&&dead.Thirst==0,"Respawn reset");
        var mild=new SurvivalEnvironment{AirTemperatureC=22,Humidity=.5};
        foreach(bool thirst in new[]{true,false})
        {
            var s=new PlayerSurvival{Health=1,Hunger=thirst?0:100,Thirst=thirst?100:0};
            SurvivalRules.Advance(s,thirst?30:200,60,mild,0,false);
            Check(s.Dead&&s.DeathCause==(thirst?PlayerDeathCause.Dehydration:PlayerDeathCause.Starvation),"Depleted need kills: "+thirst);
        }
        Console.WriteLine("PASS "+count+" player fall/death checks");
    }
}
