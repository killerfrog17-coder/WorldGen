using System;
namespace WorldGen.Core
{
    public static class ForagerLifeHistory
    {
        public static double Survival(double age)=>age<=0?1:age<5?Math.Exp(-.085*age):age<15?.654*Math.Exp(-.012*(age-5)):age<50?.58*Math.Exp(-.011*(age-15)):.395*Math.Exp(-.035*(Math.Exp((age-50)/8)-1));
        public static double Lifetime(int seed,long id,bool adultFounder=false)
        {
            double draw=Rng.Value(seed,"individual-lifetime",unchecked((int)id^(int)(id>>32)));
            // A person introduced specifically as an adult parent is sampled
            // conditional on reaching adulthood. Children use the full life table.
            if(adultFounder)draw*=Survival(18);
            double lo=adultFounder?18:0,hi=100;
            for(int i=0;i<32;i++){double mid=(lo+hi)*.5;if(Survival(mid)>draw)lo=mid;else hi=mid;}
            return (lo+hi)*.5;
        }
    }
}
