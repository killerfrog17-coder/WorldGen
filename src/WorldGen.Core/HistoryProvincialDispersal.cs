using System;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void AdvanceProvincialDispersal(PrehistoryState s,int id,int elapsed,long capacity)
        {
            var a=s.History.Aggregates;var r=a.Regions[id];long residents=s.Population[id],budget=residents/5;
            var knowledge=TechnicalHistory(s)&&r.Culture>=0?s.History.Cultures[r.Culture]:null;
            double mobility=HistoricalTechnicalMigration.Mobility(knowledge,s.Years);
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
                double residence=(route.DistanceM/1000.0+Patches[id].RadiusKm)*(.8+3*(1-similarity));
                if(knowledge!=null&&!route.Sea){residence=HistoricalTechnicalMigration.ResidencyYears(route.DistanceM/1000.0,Patches[id].RadiusKm,similarity,knowledge,s.Years);chance=Math.Min(.98,chance*Math.Sqrt(mobility));}
                if(destination.Capacity<25||HistoryRoll("provincial-dispersal-"+trial,id,s.Years)>=ElapsedChance(chance,elapsed/100.0)
                    ||s.Years-s.ArrivalYear[id]<=residence)continue;
                long room=Math.Max(0,(destination.Culture>=0?ProvincialRegionalCapacity(s,other,destination.Culture):destination.Capacity)-s.Population[other]);
                long limit=knowledge!=null&&!route.Sea?(long)(500*Math.Sqrt(mobility)):500;
                long people=Math.Min(budget,Math.Min(room,Math.Min(limit,Math.Max(25,(long)(residents*.02)))));if(people<25)continue;
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
                    if(knowledge!=null)
                    {
                        int court=a.Living.CivicOrigins[source];if(court<0)court=a.Living.Regions[id].CivicTeacher;
                        if(court>=0){local.CivicTeacher=court;local.CivicExposure=Math.Min(1000,local.CivicExposure+(int)Math.Min(400,people));}
                    }
                    if(destination.Culture>=0)LivingCultureContact(s,destination.Culture,source);
                    if(knowledge!=null&&destination.Culture<0&&s.Population[other]>=400)
                    {
                        int site=route.A==id?route.CellB:route.CellA;
                        if(site>=0&&HistoricalCells.Culture(s,site)<0&&LivingCropLandscape(s,source,site,false)>=.12)
                        {
                            // Descendant households do not forget their crops,
                            // language and technical knowledge merely because
                            // they crossed a demographic-region boundary.
                            FoundAggregateCulture(s,other,site,source);int child=s.Society.Cultures.Length-1;
                            InheritDevelopmentCommunity(s,child,source,site);
                            HistoryEvent(s,"migrant-community",s.Society.Cultures[source].Name+" households establish "+s.Society.Cultures[child].Name+" as a local descendant community, bringing crops and skills suited to the new landscape.",site,child,category:ChronicleCategory.Society);
                        }
                    }
                }
            }
        }
    }
}
