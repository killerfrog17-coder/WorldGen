using System;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        static bool MigrantDemographyHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=8;
        void ChangeRegionalCultureCount(PrehistoryState s,int region,long delta)
        {
            int culture=s.History.Aggregates.Regions[region].Culture;
            if(culture>=0)s.Society.Cultures[culture].Population=Math.Max(0,s.Society.Cultures[culture].Population+delta);
        }
        void TransferRegionalCultureCounts(PrehistoryState s,int from,int to,long people)
        {ChangeRegionalCultureCount(s,from,-people);ChangeRegionalCultureCount(s,to,people);}
    }
}
