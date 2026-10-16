using System;

namespace WorldGen.Core
{
    public static class HistoricalUrbanNetworks
    {
        public static long ImportThroughput(double catchmentKm2,double publicWorks,double institution,double writing,double roads,double mills,double shipping,double rail,double motors,double mechanizedFood)
        {
            // A remote rural ledger is not an unlimited loading dock in every
            // town. Throughput scales with a built market's linear extent and
            // actual handling/transport capacity, not its current population.
            double extent=Math.Sqrt(Math.Max(1,catchmentKm2));
            double handling=200+1000*D.Clamp(writing)+1400*D.Clamp(roads)+1500*D.Clamp(mills)+5000*D.Clamp(shipping)
                +15000*D.Clamp(rail)+20000*D.Clamp(motors)+30000*D.Clamp(mechanizedFood);
            double investment=(.20+.80*D.Clamp(publicWorks))*(.30+.70*D.Clamp(institution));
            return Math.Max(0,(long)(extent*handling*investment));
        }
    }
    public sealed partial class Prehistory
    {
        long LivingUrbanImportLimit(PrehistoryState s,int polity,int city)
        {
            var h=s.History;var t=s.Society.Settlements[city];var hc=h.Cultures[h.Polities[polity].CultureId];
            double deployed(HistoricalTechnology k)=>HistoricalModernEffects.Adoption(hc,k,s.Years);
            bool coast=DiffusionShore(t.Cell),river=riverAccess[t.Cell]>.02;
            double water=coast?1:river?.45:0;
            return HistoricalUrbanNetworks.ImportThroughput(h.Aggregates.Cities[city].AreaKm2,h.Dynamics.States[polity].PublicWorks/1000.0,hc.Institution/1000.0,
                deployed(HistoricalTechnology.Writing),deployed(HistoricalTechnology.Roads),deployed(HistoricalTechnology.Mills),
                water*Math.Sqrt(deployed(HistoricalTechnology.Sailing)*deployed(HistoricalTechnology.Navigation)),deployed(HistoricalTechnology.Railways),
                deployed(HistoricalTechnology.CombustionEngines),deployed(HistoricalTechnology.MechanizedAgriculture));
        }
    }
}
