using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public struct SurvivalHudValues
    {
        public float Health,Energy,Stamina,Hydration,Fullness,FeelsLikeC,BodyStress;
        public float FoodAttention,WaterAttention,Sickness;
        public WindHudValues Wind;
        static float Unit(double n)=>(float)Math.Max(0,Math.Min(1,n));
        public static SurvivalHudValues From(PlayerSurvival s,double feelsLike)
        {
            float cold=Unit((36.5-s.CoreTemperatureC)/(36.5-SurvivalRules.ColdDeathC));
            float hot=Unit((s.CoreTemperatureC-37.5)/(SurvivalRules.HeatCollapseC-37.5));
            // An empty digestive tract feels hungry well before body reserves run out.
            double reserves=s.MetabolismVersion==0?1:s.ReserveKcal/PlayerMetabolism.NormalReserveKcal;
            double floor=.14*Unit(reserves/.25),water=s.MetabolismVersion==0?s.Thirst/100*2:s.WaterDeficitLitres;
            float sick=0;
            if(s.FoodExposures!=null)foreach(var e in s.FoodExposures)
                if(e.DelaySeconds<=0&&e.RemainingSeconds>0)sick=Math.Max(sick,Unit(e.RemainingDamage/12));
            sick=Math.Max(sick,Unit(WaterDrinking.SymptomSeverity(s)));
            return new SurvivalHudValues{Health=Unit(s.Health/100),Energy=Unit(s.Energy/100),Stamina=Unit(Math.Min(s.Stamina,s.Energy)/100),
                Hydration=FeltHydration(water),Fullness=Unit(floor+(1-floor)*(1-s.Hunger/100)),
                FoodAttention=Unit((s.Hunger-72)/23),WaterAttention=Unit((water-1.25)/1.25),Sickness=sick,
                FeelsLikeC=(float)feelsLike,BodyStress=hot>0?ThermalWarning(hot):-ThermalWarning(cold)};
        }
        public static float FeltHydration(double deficit)
        {
            if(deficit<=1)return Unit(1-deficit*.25);
            if(deficit<=2.2)return Unit(.75-(deficit-1)/1.2*.35);
            if(deficit<=4)return Unit(.4-(deficit-2.2)/1.8*.22);
            return Unit(.18-(deficit-4)/3.5*.18);
        }
        public static float ThermalWarning(float stress)=>(float)(1-Math.Pow(1-Unit(stress),2.736965594166206));
        public static float Smooth(float v){v=Math.Max(0,Math.Min(1,v));return v*v*(3-2*v);}
        public float SnowOpacity=>FeelsLikeC<19?Smooth((19-FeelsLikeC)/11):0;
        public float SunOpacity=>FeelsLikeC>24?Smooth((FeelsLikeC-24)/9):0;
        public static float GlowOpacity(float stress,float radiusFraction)
        {
            double p=Math.Max(0,Math.Min(1,Math.Abs(stress))),d=Math.Max(0,Math.Min(1,radiusFraction));
            return (float)(.99*Math.Pow(p,.72)*Math.Exp(-13*Math.Pow(1-p,1.7)*d*d)*(1-.06*d*d));
        }
    }
}
