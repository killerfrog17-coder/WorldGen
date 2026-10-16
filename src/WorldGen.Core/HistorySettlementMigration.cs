using System;

namespace WorldGen.Core
{
    public readonly struct HistoricalFounderTransfer
    {
        public readonly long Urban,Suburban,Rural,Remote;
        public long Total=>Urban+Suburban+Rural+Remote;
        public HistoricalFounderTransfer(long urban,long suburban,long rural,long remote){Urban=urban;Suburban=suburban;Rural=rural;Remote=remote;}
    }
    public static class HistoricalSettlementMigration
    {
        public static long Available(long urban,long suburban,long rural,long remote,int cityCount)
            =>Math.Max(0,rural)+Math.Max(0,urban-1000)/4+Math.Max(0,suburban)/4+Math.Max(0,remote)/Math.Max(4,4L*cityCount);
        public static HistoricalFounderTransfer Allocate(long wanted,long urban,long suburban,long rural,long remote,int cityCount)
        {
            long remaining=Math.Max(0,wanted),r=Math.Min(remaining,Math.Max(0,rural));remaining-=r;
            long s=Math.Min(remaining,Math.Max(0,suburban)/4);remaining-=s;
            long u=Math.Min(remaining,Math.Max(0,urban-1000)/4);remaining-=u;
            long p=Math.Min(remaining,Math.Max(0,remote)/Math.Max(4,4L*cityCount));
            return new HistoricalFounderTransfer(u,s,r,p);
        }
    }
    public sealed partial class Prehistory
    {
        long LivingFounderAvailability(PrehistoryState s,int parent)
        {
            var t=s.Society.Settlements[parent];var a=s.History.Aggregates;var city=a.Cities[parent];int owner=a.Resolve(t.PolityId,s.History);
            return HistoricalSettlementMigration.Available(t.Population,city.Suburban,city.Rural,owner<0||CoastalLocalHouseholds(s,parent)?0:a.Polities[owner].Remote,owner<0?1:a.Polities[owner].CityCount);
        }
        void TransferLivingFounders(PrehistoryState s,int parent,long people,int recipient)
        {
            var h=s.History;var a=h.Aggregates;var t=s.Society.Settlements[parent];var city=a.Cities[parent];int owner=a.Resolve(t.PolityId,h);
            var transfer=HistoricalSettlementMigration.Allocate(people,t.Population,city.Suburban,city.Rural,owner<0||CoastalLocalHouseholds(s,parent)?0:a.Polities[owner].Remote,owner<0?1:a.Polities[owner].CityCount);
            if(transfer.Total!=people)throw new InvalidOperationException("Settlement founders exceed transferable households.");
            ChangeAggregateCityPopulation(s,parent,t.Population-transfer.Urban,city.Suburban-transfer.Suburban,city.Rural-transfer.Rural);
            if(transfer.Remote>0)
            {
                a.Polities[owner].Remote-=transfer.Remote;h.Polities[owner].Population-=transfer.Remote;a.TotalPopulation-=transfer.Remote;
                int source=h.Polities[owner].CultureId;s.Society.Cultures[source].Population=Math.Max(0,s.Society.Cultures[source].Population-transfer.Remote);
            }
            a.TotalPopulation+=people;s.Society.Cultures[recipient].Population+=people;
            t.FoodReserve=Math.Max(0,t.FoodReserve-people);
        }
    }
}
