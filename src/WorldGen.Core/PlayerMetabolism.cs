using System;

namespace WorldGen.Core
{
    /// <summary>Physiological days are solar days, independent of the historical calendar.</summary>
    public static class PlayerMetabolism
    {
        public const double NormalReserveKcal=65000;
        static double Clamp(double x,double lo,double hi)=>Math.Max(lo,Math.Min(hi,x));
        public static void Initialize(PlayerSurvival s)
        {
            if(s.MetabolismVersion!=0)return;
            // Legacy empty stomachs are not retroactively treated as starvation.
            s.MetabolismVersion=1;s.ReserveKcal=NormalReserveKcal;
            s.DigestingKcal=Math.Max(0,1000*(1-s.Hunger/100));s.WaterDeficitLitres=s.Thirst/100*2;
            s.SleepPhaseHour=23;
        }
        public static void Eat(PlayerSurvival s,double kg,double kcal,double waterLitres)
        {
            Initialize(s);s.DigestingKcal=Clamp(s.DigestingKcal+Math.Max(0,kcal),0,30000);
            s.Hunger=Clamp(s.Hunger-Math.Max(0,kg)/.65*100-Math.Max(0,kcal)/40,0,100);
            Hydrate(s,Math.Max(0,waterLitres));
        }
        public static void Hydrate(PlayerSurvival s,double litres)
        {Initialize(s);s.WaterDeficitLitres=Clamp(s.WaterDeficitLitres-litres,0,20);s.Thirst=Clamp(s.WaterDeficitLitres/2.2*100,0,100);}
        public static void Advance(PlayerSurvival s,double days,double activity,double coldLoad,double waterLitresPerDay)
        {
            Initialize(s);if(days<=0||s.Dead)return;
            double kcal=(2200+activity*2200+Math.Min(1500,coldLoad*18))*days;
            double absorbed=Math.Min(s.DigestingKcal,days*24*300);s.DigestingKcal-=absorbed;
            s.ReserveKcal=Clamp(s.ReserveKcal+absorbed-kcal,0,130000);
            s.Hunger=Clamp(s.Hunger+days*(280+activity*50),0,100);
            Hydrate(s,-Math.Max(0,waterLitresPerDay)*days);
        }
        public static double DehydrationDamagePerDay(PlayerSurvival s)=>Math.Max(0,s.WaterDeficitLitres-4)*35;
        public static double StarvationDamagePerDay(PlayerSurvival s)=>s.ReserveKcal<=0?25:0;
    }
}
