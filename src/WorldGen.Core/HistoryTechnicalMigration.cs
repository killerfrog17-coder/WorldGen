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
    }
}
