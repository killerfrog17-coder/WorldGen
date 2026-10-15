using System;
namespace WorldGen.Core
{
    // Settlement fronts carry people and skills, not an unexplained clock
    // bonus. Geography still sets the journey and food still limits arrival.
    public static class HistoricalTechnicalMigration
    {
        public static double Mobility(HistoricalCulture culture,int year)
            =>culture?.Technical==null?1:D.Q(1+2*HistoricalModernEffects.Adoption(culture,HistoricalTechnology.Roads,year)+12*HistoricalModernEffects.NetworkUse(culture,HistoricalTechnology.Railways,year)+16*HistoricalModernEffects.NetworkUse(culture,HistoricalTechnology.CombustionEngines,year),1000000);
        public static double SettlementInvestment(HistoricalCulture culture,int year)
            =>culture?.Technical==null?1:D.Q(1+4*HistoricalModernEffects.NetworkUse(culture,HistoricalTechnology.Railways,year)+6*HistoricalModernEffects.NetworkUse(culture,HistoricalTechnology.CombustionEngines,year),1000000);
        public static double ResidencyYears(double distanceKm,double radiusKm,double similarity,HistoricalCulture culture,int year)
            =>D.Q((distanceKm+radiusKm)*(.8+3*(1-similarity))/Mobility(culture,year),1000000);
        public static int CarriedDeployment(int technology,int parentDeployment)
        {
            bool portable=technology<4||technology==(int)HistoricalTechnology.Writing||technology==(int)HistoricalTechnology.Coinage||technology==(int)HistoricalTechnology.Navigation||technology==(int)HistoricalTechnology.Scholasticism||technology==(int)HistoricalTechnology.ScientificMethod;
            return portable?Math.Min(500,Math.Max(0,parentDeployment)):0;
        }
        public static int SeaRangeM(HistoricalCulture culture,int year)
        {
            var t=culture.Technical;double range=120000;
            if(HistoricalTechnologies.Has(culture,HistoricalTechnology.Sailing))range=Math.Max(range,Math.Min(1250000,250000+125000*t.Levels[(int)HistoricalTechnology.Sailing]));
            if(HistoricalTechnologies.Has(culture,HistoricalTechnology.Navigation))range=Math.Max(range,1000000+500000*Math.Min(5,t.Levels[(int)HistoricalTechnology.Navigation]));
            if(HistoricalTechnologies.Has(culture,HistoricalTechnology.OceanNavigation))
            {
                double craft=2000000+750000*t.Levels[(int)HistoricalTechnology.OceanNavigation];
                double powered=2500000*HistoricalModernEffects.NetworkUse(culture,HistoricalTechnology.SteamPower,year)+4000000*HistoricalModernEffects.NetworkUse(culture,HistoricalTechnology.CombustionEngines,year);
                range=Math.Max(range,Math.Min(14000000,craft+powered));
            }
            return (int)(Math.Round(range/50000)*50000);
        }
    }
}
