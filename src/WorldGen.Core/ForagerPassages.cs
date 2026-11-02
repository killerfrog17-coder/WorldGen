using System;
namespace WorldGen.Core
{
    public static class ForagerPassages
    {
        public static bool Open(int seed,ForagerPassage pass,ForagerDistrict from,ForagerEnvironment a,ForagerEnvironment b,ForagerCohort people,bool sharedProducts=false)
        {
            if(a.IceCover>.86||b.IceCover>.86)return false;
            double Prepared(int k)=>people!=null&&people.Skill.Length>k?(sharedProducts?ForagerTechnology.MakerAccess(people.Adults,people.Skill[k]):people.Skill[k])*D.Clamp(ForagerTechnology.Materials(from,a,k)*3):0;
            if(pass.MountainBarrier>.85&&Math.Min(a.SummerC,b.SummerC)<4&&Prepared(14)<.5)return false;
            if(!pass.Sea)return true;
            var climate=PaleolithicRules.Climate(seed,a.Years,from.Center.Z);
            if(pass.Shelf)return pass.SeaFallM>0&&pass.SeaFallM<=climate.SeaFallM;
            int distance=pass.WaterDistanceM>0?pass.WaterDistanceM:pass.DistanceM;
            return distance<=20000&&Prepared(20)>.5||distance<=120000&&Prepared(30)>.65;
        }
        public static double TravelCostKm(ForagerPassage pass)=>pass.DistanceM/1000.0*(1+2.5*pass.MountainBarrier+1.4*pass.WaterCost+(pass.Sea&&!pass.Shelf?3:0));
        public static double TravelCostKm(ForagerPassage pass,ForagerEnvironment from,ForagerEnvironment to)=>pass.DistanceM/1000.0*(1+2.5*pass.MountainBarrier+1.4*D.Clamp(1-(from.Water+to.Water)*.5)+(pass.Sea&&!pass.Shelf?3:0));
        public static double FrontierKmPerYear(ForagerCohort c,ForagerCulture culture,ForagerDistrict destination,ForagerEnvironment environment,bool sharedProducts=false)
        {
            double Gear(int k)=>sharedProducts?ForagerTechnology.ProductCoverage(c.Skill[k]):c.Skill[k];
            double winter=D.Clamp((environment.WinterC+15+8*Gear(8)+12*Gear(13)+16*Gear(14))/35);
            return (.2+.7*culture.ExchangeOpenness+.3*D.Clamp(c.Connectivity))*(.35+.65*winter)*(.55+.45*environment.Water)/(1+destination.Relief/2200);
        }
    }
}
