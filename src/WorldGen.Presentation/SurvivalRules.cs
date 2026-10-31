using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public struct SurvivalEnvironment
    {
        public double AirTemperatureC,Humidity,WindMps,RainMmPerHour,Sunlight,Insulation,RadiantHeatC,SleepComfort,LocalHour,EffortLoad,GroundGrade,SwimEffort;
        public bool Immersed,Sleeping,Swimming;
    }
    /// <summary>Approximate gameplay physiology, not a clinical thermoregulation model.</summary>
    public static class SurvivalRules
    {
        static double Clamp(double n,double lo=0,double hi=1)=>Math.Max(lo,Math.Min(hi,n));
        public const double ColdDeathC=30,HeatCollapseC=40.5;
        public const double JumpStaminaCost=2;
        static void PrepareEffort(PlayerSurvival s)
        {
            if(s.Exertion==null)s.Exertion=new ExertionState{Burst=Clamp(s.Stamina/Math.Max(1,s.MaximumStamina))};
            if(ExertionRules.Upgrade(s.Exertion,ExertionProfile.Human()))s.Stamina=s.MaximumStamina*s.Exertion.Burst;
            s.Exertion.Burst=Math.Min(s.Exertion.Burst,Clamp(s.Stamina/Math.Max(1,s.MaximumStamina)));
        }
        public static void SpendStamina(PlayerSurvival s,double amount)
        {
            PrepareEffort(s);
            ExertionRules.Impulse(s.Exertion,Math.Max(0,amount)/Math.Max(1,s.MaximumStamina));
            s.Stamina=s.MaximumStamina*s.Exertion.Burst;
        }
        static bool ResolveFatalCold(PlayerSurvival state)
        {
            // No rescue/rewarming gameplay exists yet. Also resolve a saved
            // cold knockout from the preceding prototype instead of stranding it.
            if(state.CoreTemperatureC>ColdDeathC&&!(state.Unconscious&&state.CoreTemperatureC<32))return false;
            state.Health=0;state.Unconscious=false;state.DeathCause=PlayerDeathCause.Cold;return true;
        }
        // Perceived exposure is deliberately fast, unlike accumulated core heat.
        public static double FeelsLike(SurvivalEnvironment e,double wetness,double speed)
        {
            double air=e.AirTemperatureC,wind=Clamp(e.WindMps,0,60),activity=Clamp(speed/TerrainMotion.RunSpeedMps);
            return air-Math.Max(0,22-air)*wind*.04*(1-Clamp(e.Insulation)*.8)
                -Clamp(wetness)*Math.Max(0,30-air)*.35+Clamp(e.Sunlight)*4+activity*4
                +Math.Max(0,air-24)*Clamp(e.Humidity)*.18+e.RadiantHeatC*.65;
        }
        public static bool TryMode(string value,out GameMode mode)
        {
            switch(value.ToLowerInvariant())
            {case "survival":mode=GameMode.Survival;return true;case "creative":mode=GameMode.Creative;return true;case "spectator":mode=GameMode.Spectator;return true;default:mode=GameMode.Creative;return false;}
        }
        public static void Advance(PlayerSurvival state,double seconds,double minutesPerDay,SurvivalEnvironment environment,double speed,bool sprinting)
        {
            if(!double.IsFinite(seconds)||seconds<0||seconds>86400||!double.IsFinite(minutesPerDay)||minutesPerDay<1||minutesPerDay>10080)throw new ArgumentOutOfRangeException(nameof(seconds));
            if(!double.IsFinite(speed)||speed<0)throw new ArgumentOutOfRangeException(nameof(speed));
            if(!double.IsFinite(environment.SwimEffort)||environment.SwimEffort<0||environment.SwimEffort>1)throw new ArgumentException("Invalid swimming effort.");
            if(!double.IsFinite(environment.AirTemperatureC)||!double.IsFinite(environment.Humidity)||!double.IsFinite(environment.WindMps)||!double.IsFinite(environment.RainMmPerHour)||!double.IsFinite(environment.Sunlight)||!double.IsFinite(environment.Insulation)||!double.IsFinite(environment.RadiantHeatC)||!double.IsFinite(environment.SleepComfort))throw new ArgumentException("Invalid survival environment.");
            if(state.Dead)return;
            if(seconds>0&&ResolveFatalCold(state))return;
            // Fixed substeps keep low-FPS frames and saved deterministic probes comparable.
            while(seconds>1e-9&&!state.Dead)
            {double dt=Math.Min(.25,seconds);Step(state,dt,minutesPerDay,environment,speed,sprinting);seconds-=dt;}
        }
        static void Step(PlayerSurvival s,double dt,double minutesPerDay,SurvivalEnvironment e,double speed,bool sprinting)
        {
            double day=dt/(minutesPerDay*60),thermal=dt*60/minutesPerDay;
            Foraging.AdvancePoison(s,day*86400);if(s.Dead)return;
            WaterDrinking.AdvanceIllness(s,day*86400);
            SleepRules.Advance(s,day*86400,e.Sleeping,e.SleepComfort,e.LocalHour,e.Sunlight);
            double activity=s.Unconscious?0:e.Swimming?Clamp(e.SwimEffort):Math.Max(Clamp(speed/TerrainMotion.RunSpeedMps),sprinting&&speed>.1?.9:0),humidity=Clamp(e.Humidity),wind=Clamp(e.WindMps,0,60),sun=Clamp(e.Sunlight);
            double sweat=Clamp((e.AirTemperatureC-20)/12+activity*1.2+sun*.5,0,4);
            if(e.Immersed)s.Wetness=1;
            else s.Wetness=Clamp(s.Wetness+thermal*(Math.Max(0,e.RainMmPerHour)*.003+sweat*.003-(.002+wind*.0003)*(1-humidity)));
            double cold=Math.Max(0,18-e.AirTemperatureC)*(1+wind*.05+s.Wetness*.9)*(1-Clamp(e.Insulation)*.9);
            if(e.Immersed)cold+=Math.Max(0,30-e.AirTemperatureC)*1.5;
            double coldLoad=Math.Max(0,cold-5-activity*8-e.RadiantHeatC);
            coldLoad*=1+(1-s.BodyCondition)*.8;
            double evaporation=3*(1-humidity*.75)*(1+wind*.03)*(1-Clamp((s.Thirst-45)/55)*.8);
            double heatLoad=Math.Max(0,Math.Max(0,e.AirTemperatureC-28)*.28+Math.Max(0,e.AirTemperatureC-35)*humidity*.35+activity*1.5+sun*1.5+Math.Max(0,e.RadiantHeatC-12)*.2-evaporation);
            // Uncompensated heat loss/gain accumulates. A fixed cold equilibrium
            // could otherwise leave a naked person at subfreezing temperatures
            // indefinitely without progressing beyond mild hypothermia.
            if(coldLoad>0||heatLoad>0)s.CoreTemperatureC=Clamp(s.CoreTemperatureC+(heatLoad*.0015-coldLoad*.0008)*thermal,26,43.5);
            else s.CoreTemperatureC+=(37-s.CoreTemperatureC)*(1-Math.Exp(-thermal/150));
            if(ResolveFatalCold(s))return;
            if(s.CoreTemperatureC>=HeatCollapseC)s.Unconscious=true;
            else if(s.Unconscious&&s.CoreTemperatureC>=32&&s.CoreTemperatureC<=39.5)s.Unconscious=false;
            PlayerMetabolism.Advance(s,day,activity,coldLoad,2.4+Math.Max(0,e.AirTemperatureC-22)*.09+activity*3+sweat*.6);
            bool resting=speed<.1&&(!e.Swimming||e.SwimEffort<.3);
            bool comfortable=s.CoreTemperatureC>=36&&s.CoreTemperatureC<=38;
            double energyLimit=100*s.BodyCondition*(s.Hunger>90?.9:1)*(1-.25*Clamp((s.Thirst-60)/40))*(1-.18*WaterDrinking.SymptomSeverity(s));
            if(resting&&s.Thirst<85&&comfortable)s.Energy=Clamp(s.Energy+day*(e.Sleeping?180*Math.Max(.25,e.SleepComfort):65),0,energyLimit);
            else s.Energy=Clamp(s.Energy-day*(8+activity*70+coldLoad*.5+heatLoad*4),0,100);
            PrepareEffort(s);
            // Sleep accelerates calendar needs, not the physical rate of a sprint.
            double effort=e.Swimming?Clamp(e.SwimEffort):resting?0:sprinting?1:Math.Pow(Clamp(speed/TerrainMotion.RunSpeedMps),1.25);
            ExertionRules.Advance(s.Exertion,ExertionProfile.Human(),dt,effort,e.AirTemperatureC,Math.Max(1,e.EffortLoad),e.GroundGrade);
            s.Stamina=s.MaximumStamina*s.Exertion.Burst;s.Exhausted=s.Exertion.Burst<.08;
            // Critical exposure can incapacitate before it proves fatal. Mild
            // hypothermia causes impairment, not automatic rapid health loss.
            double coldDamage=Math.Max(0,32-s.CoreTemperatureC)*.12,heatDamage=Math.Max(0,s.CoreTemperatureC-39.5)*.25;
            double thirstDamage=PlayerMetabolism.DehydrationDamagePerDay(s)/3600,hungerDamage=PlayerMetabolism.StarvationDamagePerDay(s)/3600;
            double damage=coldDamage+heatDamage+thirstDamage+hungerDamage;
            if(damage>0)
            {
                s.Health=Clamp(s.Health-damage*thermal,0,100);
                if(s.Dead)s.DeathCause=coldDamage>=Math.Max(heatDamage,Math.Max(thirstDamage,hungerDamage))?PlayerDeathCause.Cold:heatDamage>=Math.Max(thirstDamage,hungerDamage)?PlayerDeathCause.Heat:thirstDamage>=hungerDamage?PlayerDeathCause.Dehydration:PlayerDeathCause.Starvation;
            }
            else if(comfortable&&s.Hunger<70&&s.Thirst<50)s.Health=Clamp(s.Health+day*30*s.BodyCondition,0,100);
        }
    }
}
