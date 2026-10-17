using System;
namespace WorldGen.Core
{
    // Encounter opportunities, not a population account or a prerequisite for
    // agriculture/state formation. A small inhabited district is not a wall.
    public static class HistoricalSparseExchange
    {
        public static double Encounter(long residents)
            =>residents<=0?0:residents>=100?1:D.Q(Math.Sqrt(residents/100.0),1000000);
        public static double Contact(long first,long second)
            =>D.Q(Encounter(first)*Encounter(second),1000000);
    }
    public sealed partial class Prehistory
    {
        static bool SparseExchangeHistory(PrehistoryState s)
            =>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=18;
    }
}
