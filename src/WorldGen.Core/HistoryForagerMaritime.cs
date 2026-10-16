using System;
namespace WorldGen.Core
{
    public static class HistoricalForagerMaritime
    {
        // Recognizable forager traditions are not pristine state institutions.
        // Keep the state-origin prior separate, including its saved provenance.
        public static double RecognitionHomeland(double stateOriginPrior)=>1;
        public static long CatchmentResidents(long cellResidents,long cellAreaM2,long regionalResidents)
        {
            if(cellResidents<=0||cellAreaM2<=0||regionalResidents<=0)return 0;
            const long area=31415926536L; // A fixed ~100km-radius local catchment.
            decimal density=(decimal)cellResidents/cellAreaM2;
            if(density>=(decimal)regionalResidents/area)return regionalResidents;
            return Math.Min(regionalResidents,(long)(density*area));
        }
        public static double Support(double marineFood,int fishing,long localResidents,bool maritime)
        {
            if(!maritime||localResidents<200)return 0;
            return D.Q(D.Clamp((marineFood-.2)/.8)*D.Clamp((fishing-200)/300.0)*D.Clamp(localResidents/5000.0),1000000);
        }
        public static bool Portable(int technology)=>technology==(int)HistoricalTechnology.Storage||HistoricalTechnicalGraph.Maritime(technology);
        public static double DeploymentCapital(int technology,double ordinary,double support)
            =>Portable(technology)&&support>0?Math.Max(ordinary,.4+.4*D.Clamp(support)):ordinary;
        // A maritime-only provisioning opportunity, not fiscal grain surplus.
        public static int Provisioning(int ordinary,double support)=>Math.Max(ordinary,100+(int)Math.Floor(200*D.Clamp(support)));
    }
    public sealed partial class Prehistory
    {
        long ForagerMaritimeResidents(PrehistoryState s,int culture)
        {
            if(!LivingWorldHistory(s))return 0;
            int cell=s.Society.Cultures[culture].OriginCell;
            if(CellPatch[cell]<0||HistoricalCells.Culture(s,cell)!=culture)return 0;
            return HistoricalForagerMaritime.CatchmentResidents(AggregateRuralCellPopulation(s,cell),World.Surface.Cells[cell].AreaM2,
                ProvincialContactResidents(s,CellPatch[cell]));
        }
        double ForagerMaritimeSupport(PrehistoryState s,int culture,bool maritime)
        {
            if(!LivingWorldHistory(s)||!maritime)return 0;
            var c=s.Society.Cultures[culture];int cell=c.OriginCell;
            if(HistoricalCells.Culture(s,cell)!=culture)return 0;
            // Food support already requires actual adjacent marine taxa,
            // drinking-water opportunity and a non-permanent-ice coast. The
            // local resident cohort is cached actor accounting, not a census.
            double food=MarineForagerSupport(s,cell);
            if(food<=.2)return 0;
            return HistoricalForagerMaritime.Support(food,c.Fishing,ForagerMaritimeResidents(s,culture),true);
        }
    }
}
