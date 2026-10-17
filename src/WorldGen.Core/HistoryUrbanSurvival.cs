using System;
namespace WorldGen.Core
{
    public readonly struct HistoricalUrbanFoodBalance
    {
        public readonly long Reserve,Produced,Consumed,Overflow;
        public readonly double Supply;
        public HistoricalUrbanFoodBalance(long reserve,long produced,long consumed,long overflow,double supply)
        {Reserve=reserve;Produced=produced;Consumed=consumed;Overflow=overflow;Supply=supply;}
    }
    public static class HistoricalUrbanSurvivalEffects
    {
        static long Add(long first,long second)=>first>long.MaxValue-second?long.MaxValue:first+second;
        static long Multiply(long amount,int years)=>amount>long.MaxValue/Math.Max(1,years)?long.MaxValue:amount*Math.Max(0,years);
        public static HistoricalUrbanFoodBalance Food(long oldReserve,long annualHarvest,long residents,long capacity,int elapsed)
        {
            long stored=Math.Max(0,oldReserve),produced=Multiply(Math.Max(0,annualHarvest),elapsed),demand=Multiply(Math.Max(0,residents),elapsed);
            long available=Add(stored,produced),consumed=Math.Min(available,demand),remaining=available-consumed;
            // A loss of fields or tax imports is not destruction of already
            // stored grain. Existing warehouse contents survive that shock;
            // excess NEW harvest can still exceed present handling capacity.
            long ceiling=Math.Max(stored,Multiply(Math.Max(0,capacity),3)),reserve=Math.Min(remaining,ceiling);
            return new HistoricalUrbanFoodBalance(reserve,produced,consumed,remaining-reserve,demand==0?1:D.Q(consumed/(double)demand,1000000000));
        }
        public static long NaturalChange(long residents,long capacity,double ordinaryRate,int elapsed,double supply)
        {
            if(residents<=0||elapsed<=0)return 0;
            double room=capacity<=0?0:Math.Max(0,1-residents/(double)capacity),scarcity=1-D.Clamp(supply);
            // Crowding can remove net growth; it cannot make an arbitrary
            // negative quadratic birth rate erase millions of fed people.
            double rate=Math.Max(0,ordinaryRate)*room-.06*scarcity*scarcity;
            double change=residents*(Math.Exp(Math.Max(-700,Math.Min(1,rate*elapsed)))-1);
            return (long)Math.Round(Math.Max(-residents,Math.Min(long.MaxValue-residents,change)));
        }
        public static long RuralChange(long residents,long capacity,double ordinaryRate,int elapsed)
        {
            if(residents<=0||elapsed<=0)return 0;
            double ratio=capacity<=0?0:capacity/(double)residents;
            double rate=ratio>=1?Math.Max(0,ordinaryRate)*(1-1/ratio):-.035*(1-Math.Max(0,ratio));
            double change=residents*(Math.Exp(Math.Max(-700,Math.Min(1,rate*elapsed)))-1);
            return (long)Math.Round(Math.Max(-residents,Math.Min(long.MaxValue-residents,change)));
        }
        public static int Distress(int previous,int elapsed,double supply,long residents,long capacity)
            =>supply<.75||residents<100&&capacity<100?Math.Min(int.MaxValue-Math.Max(0,elapsed),Math.Max(0,previous))+Math.Max(0,elapsed):Math.Max(0,previous-2*Math.Max(0,elapsed));
        public static bool Retire(long residents,long capacity,long reserve,int distress,double supply)
            =>residents<25||distress>=30&&residents<150&&capacity<150||distress>=80&&supply<.25&&reserve==0&&capacity<Math.Max(100,residents*.10);
    }
    public sealed partial class Prehistory
    {
        static bool RevisedUrbanSurvival(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=5;
    }
}
