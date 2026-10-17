using System;
namespace WorldGen.Core
{
    public static class HistoricalFarmFrontier
    {
        // A full foraging economy need not have filled the available cultivated
        // plots. Reserve existing local farmers' share before admitting a small
        // real incoming cohort; this number creates neither people nor farms.
        public static long Room(long ordinaryRoom,long viablePlotCapacity,long estimatedLocalResidents,int localFarming)
        {
            long used=(long)(Math.Max(0,estimatedLocalResidents)*D.Clamp(localFarming/600.0));
            long plots=Math.Max(0,viablePlotCapacity-used);
            return Math.Max(Math.Max(0,ordinaryRoom),Math.Min(500,plots));
        }
    }
    public sealed partial class Prehistory
    {
        static bool FarmFrontierVacancyHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=12;
        long FarmFrontierRoom(PrehistoryState s,int region,int cell,int farmer,long ordinary)
        {
            if(!FarmFrontierVacancyHistory(s)||HistoricalCells.Polity(s,cell)>=0||s.Society.Cultures[farmer].Farming<250
                ||LivingCropLandscape(s,farmer,cell,false)<.12)return Math.Max(0,ordinary);
            long fields=(long)(ProvincialFarmPotential(s,cell,farmer)*D.Clamp(s.Society.Cultures[farmer].Farming/600.0));
            int local=s.History.Aggregates.Regions[region].Culture;
            return HistoricalFarmFrontier.Room(ordinary,fields,AggregateRuralCellPopulation(s,cell),local>=0?s.Society.Cultures[local].Farming:0);
        }
    }
}
