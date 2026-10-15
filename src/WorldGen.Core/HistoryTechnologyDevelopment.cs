using System;
namespace WorldGen.Core
{
    public static class HistoricalTechnologyDevelopment
    {
        // Different technical problems have different experimental/material
        // burdens. Dates are never selected from the calendar or Earth geography.
        public static int Cost(int technology)
        {
            switch((HistoricalTechnology)technology)
            {
                case HistoricalTechnology.Bronze:return 400;
                case HistoricalTechnology.Iron:return 1150;
                case HistoricalTechnology.Steel:return 950;
                case HistoricalTechnology.Scholasticism:return 1300;
                case HistoricalTechnology.Printing:return 1200;
                case HistoricalTechnology.ScientificMethod:return 1500;
                default:return HistoricalDevelopmentEffects.ResearchCost(technology);
            }
        }
        public static int Maturation(int technology,bool learned)
        {
            if(learned)return technology>=20?15:technology>=15?35:80;
            switch((HistoricalTechnology)technology)
            {
                case HistoricalTechnology.Bronze:return 350;
                case HistoricalTechnology.Iron:return 650;
                case HistoricalTechnology.Scholasticism:return 400;
                case HistoricalTechnology.Printing:return 250;
                case HistoricalTechnology.ScientificMethod:return 200;
                case HistoricalTechnology.SteamPower:return 250;
                default:return technology>=34?20:technology>=27?35:technology>=20?65:technology>=15?160:technology>=5?300:100;
            }
        }
        public static double ExperimentalEffort(HistoricalCulture c,int technology,int year,int centers)
        {
            if(technology<20)return 1;
            double practice=HistoricalModernEffects.Adoption(c,HistoricalTechnology.ScientificMethod,year);
            double communication=HistoricalModernEffects.Communication(c,year);
            return D.Q(1+practice*(1+Math.Min(2,Math.Log(1+Math.Max(0,centers))/2)+Math.Log(communication)),1000000);
        }
    }
}
