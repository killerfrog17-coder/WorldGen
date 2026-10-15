using System;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void AdvanceProvincialDispersal(PrehistoryState s,int id,int elapsed,long capacity)
        {
            var a=s.History.Aggregates;var r=a.Regions[id];long residents=s.Population[id],budget=residents/5;
            int options=r.Routes.Length+(PassageHistory(s)?diffusionPassages[id].Length:0);
            if(residents<125||options==0)return;
            for(int trial=0;trial<Math.Min(4,options)&&budget>=25;trial++)
            {
                var route=PassageHistory(s)?DiffusionDeparture(s,id,trial):Routes[r.Routes[(int)(HistoryRoll("provincial-dispersal-route-"+trial,id,s.Years)*r.Routes.Length)]];
                int other=route.A==id?route.B:route.A;var destination=a.Regions[other];
                double similarity=Math.Exp(-Math.Abs(r.MeanTenthsC-destination.MeanTenthsC)/180.0-Math.Abs(r.AridityMilli-destination.AridityMilli)/1000.0);
                bool ice=LowSeaLevel(s.Years)&&s.History.Polities.Length==0;
                double chance=route.Sea?CrossingChance(route.DistanceM,ice&&route.Shallow):.7;
                if(route.Sea&&r.Culture>=0)
                {
                    var hc=s.History.Cultures[r.Culture];
                    if(HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation))chance=Math.Max(chance,.5*Math.Exp(-route.DistanceM/2000000.0));
                    else if(HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing))chance=Math.Max(chance,.2*Math.Exp(-route.DistanceM/300000.0));
                }
                chance*=(.2+.8*similarity)*(.3+.7*D.Clamp(residents/(double)Math.Max(1,capacity)));
                if(destination.Capacity<25||HistoryRoll("provincial-dispersal-"+trial,id,s.Years)>=ElapsedChance(chance,elapsed/100.0)
                    ||s.Years-s.ArrivalYear[id]<=(route.DistanceM/1000.0+Patches[id].RadiusKm)*(.8+3*(1-similarity)))continue;
                long room=Math.Max(0,(destination.Culture>=0?ProvincialRegionalCapacity(s,other,destination.Culture):destination.Capacity)-s.Population[other]);
                long people=Math.Min(budget,Math.Min(room,Math.Min(500,Math.Max(25,(long)(residents*.02)))));if(people<25)continue;
                long before=s.Population[other];s.Population[id]-=people;s.Population[other]+=people;budget-=people;
                if(s.ArrivalYear[other]<0)s.ArrivalYear[other]=s.Years;
                for(int g=0;g<8;g++)s.Ancestry[other*8+g]=(int)((s.Ancestry[other*8+g]*before+s.Ancestry[id*8+g]*people)/Math.Max(1,before+people));
                if(PassageHistory(s)&&r.Culture>=0&&s.Society.Cultures[r.Culture].Farming>=180)
                {
                    // Migrating households carry an available agricultural
                    // package. This creates contact, not automatic farming or
                    // a state: seeds still have to fit the destination.
                    int source=r.Culture;var local=a.Living.Regions[other];
                    local.Teacher=source;local.Exposure=Math.Min(1000,local.Exposure+(int)Math.Min(200,people));local.LastExchangeYear=s.Years;
                    a.Provinces.RegionalKnowledge[other]|=ProvincialKnowledge(s.History.Cultures[source]);
                    if(destination.Culture>=0)LivingCultureContact(s,destination.Culture,source);
                }
            }
        }
    }
}
