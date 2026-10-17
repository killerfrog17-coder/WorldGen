using WorldGen.Core;
public static class HistorySparseExchangeTests
{
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalSparseExchange.Encounter(0)==0&&HistoricalSparseExchange.Encounter(-1)==0,"An empty district cannot carry contact.");
        check(HistoricalSparseExchange.Contact(0,10000)==0&&HistoricalSparseExchange.Contact(10000,0)==0,"A remembered distant teacher does not populate an empty route endpoint.");
        check(HistoricalSparseExchange.Encounter(1)>0&&HistoricalSparseExchange.Encounter(75)>HistoricalSparseExchange.Encounter(1),"Sparse inhabited districts retain proportionately fewer encounters, not an absolute blockade.");
        check(HistoricalSparseExchange.Encounter(100)==1&&HistoricalSparseExchange.Encounter(long.MaxValue)==1,"At least100 actual residents retain the preceding encounter multiplier exactly.");
        check(HistoricalSparseExchange.Contact(75,10000)==HistoricalSparseExchange.Contact(10000,75),"Contact opportunities are symmetric in actual endpoint population.");
        check(HistoricalSparseExchange.Contact(75,75)<HistoricalSparseExchange.Contact(75,10000),"Two sparse endpoints reduce encounters rather than creating a free relay.");
        double previous=0;
        for(int people=0;people<=10000;people++)
        {double current=HistoricalSparseExchange.Encounter(people);check(current>=previous&&current<=1,"Encounter opportunity is bounded and monotone at population"+people);previous=current;}
    }
}
