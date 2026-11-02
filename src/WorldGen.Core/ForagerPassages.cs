using System;
namespace WorldGen.Core
{
    public static class ForagerPassages
    {
        public static bool Open(int seed,ForagerPassage pass,ForagerDistrict from,ForagerEnvironment a,ForagerEnvironment b,ForagerCohort people)
        {
            if(a.IceCover>.86||b.IceCover>.86)return false;
            double Use(int k)=>people!=null&&people.Use.Length>k?people.Use[k]:0;
            if(pass.MountainBarrier>.85&&Math.Min(a.SummerC,b.SummerC)<4&&Use(14)<.5)return false;
            if(!pass.Sea)return true;
            var climate=PaleolithicRules.Climate(seed,a.Years,from.Center.Z);
            if(pass.Shelf)return pass.SeaFallM>0&&pass.SeaFallM<=climate.SeaFallM;
            int distance=pass.WaterDistanceM>0?pass.WaterDistanceM:pass.DistanceM;
            return distance<=20000&&Use(20)>.5||distance<=120000&&Use(30)>.65;
        }
        public static double TravelCostKm(ForagerPassage pass)=>pass.DistanceM/1000.0*(1+2.5*pass.MountainBarrier+1.4*pass.WaterCost+(pass.Sea&&!pass.Shelf?3:0));
        public static double TravelCostKm(ForagerPassage pass,ForagerEnvironment from,ForagerEnvironment to)=>pass.DistanceM/1000.0*(1+2.5*pass.MountainBarrier+1.4*D.Clamp(1-(from.Water+to.Water)*.5)+(pass.Sea&&!pass.Shelf?3:0));
        public static double FrontierKmPerYear(ForagerCohort c,ForagerCulture culture,ForagerDistrict destination,ForagerEnvironment environment)
        {
            double winter=D.Clamp((environment.WinterC+15+8*c.Use[8]+12*c.Use[13]+16*c.Use[14])/35);
            return (.2+.7*culture.ExchangeOpenness+.3*D.Clamp(c.Connectivity))*(.35+.65*winter)*(.55+.45*environment.Water)/(1+destination.Relief/2200);
        }
    }
}
