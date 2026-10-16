using System;
namespace WorldGen.Core
{
    public static class HistoricalInlandSettlement
    {
        // A settlement proposal is not a free new transport network. Only
        // deployed transport supported by a funded state's works broadens it.
        public static double ReachFactor(double landTransport,int publicWorks,bool funded)
            =>funded?D.Q(1+(Math.Max(1,Math.Min(4.5,landTransport))-1)*(.25+.75*D.Clamp(publicWorks/1000.0)),1000000):1;
        public static double ProbeScale(double reach,int trial)=>D.Q(1+(Math.Max(1,Math.Min(4.5,reach))-1)*Math.Max(0,Math.Min(2,trial))/2,1000000);
        // Effective family/carriage travel, including terrain and staging;
        // these are authored logistics priors, not measured train schedules.
        public static double TravelDays(double effortKm,double landTransport)
            =>D.Q(7+Math.Max(0,effortKm)/(15*Math.Max(1,Math.Min(4.5,landTransport))),1000000);
        public static long Provisions(long people,double effortKm,double landTransport)
            =>people<=0?0:(long)Math.Min(1000000000000,Math.Ceiling(D.Q(people*(TravelDays(effortKm,landTransport)/365)*1.5,1000000)));
        public static long Investment(long people,double effortKm,double landTransport)
            =>people<=0?0:(long)Math.Min(1000000000000,Math.Max(100,people/5)+Math.Ceiling(D.Q(people*(Math.Max(0,effortKm)/(250*Math.Max(1,Math.Min(4.5,landTransport)))),1000000)));
    }
    public sealed partial class Prehistory
    {
        double LivingSettlementProbeReach(PrehistoryState s,int parent)
        {
            if(!LivingWorldHistory(s))return 1;var h=s.History;var a=h.Aggregates;var town=s.Society.Settlements[parent];int owner=a.Resolve(town.PolityId,h);
            if(owner<0)return 1;var p=h.Polities[owner];bool funded=p.EndedYear<0&&p.Treasury>=1000&&a.Polities[owner].Receipts>a.Polities[owner].ServiceCost;
            return HistoricalInlandSettlement.ReachFactor(HistoricalModernEffects.LandTransport(h.Cultures[town.CultureId],s.Years),h.Dynamics.States[owner].PublicWorks,funded);
        }
        void LivingInlandSettlementCosts(PrehistoryState s,int parent,int target,long people,out long provisions,out long investment)
        {
            if(!LivingWorldHistory(s)){provisions=0;investment=Math.Max(100,people/5);return;}
            var town=s.Society.Settlements[parent];double transport=HistoricalModernEffects.LandTransport(s.History.Cultures[town.CultureId],s.Years),effort=LivingTravelKm(s,town.Cell,target,town.CultureId);
            provisions=HistoricalInlandSettlement.Provisions(people,effort,transport);investment=HistoricalInlandSettlement.Investment(people,effort,transport);
        }
    }
}
