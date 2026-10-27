using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public struct SurvivalHudValues
    {
        public float Health,Energy,Stamina,Hydration,Fullness,FeelsLikeC,BodyStress;
        static float Unit(double n)=>(float)Math.Max(0,Math.Min(1,n));
        public static SurvivalHudValues From(PlayerSurvival s,double feelsLike)
        {
            float cold=Unit((36.5-s.CoreTemperatureC)/(36.5-SurvivalRules.ColdDeathC));
            float hot=Unit((s.CoreTemperatureC-37.5)/(SurvivalRules.HeatCollapseC-37.5));
            return new SurvivalHudValues{Health=Unit(s.Health/100),Energy=Unit(s.Energy/100),Stamina=Unit(Math.Min(s.Stamina,s.Energy)/100),
                Hydration=Unit(1-s.Thirst/100),Fullness=Unit(1-s.Hunger/100),FeelsLikeC=(float)feelsLike,BodyStress=hot>0?hot:-cold};
        }
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
