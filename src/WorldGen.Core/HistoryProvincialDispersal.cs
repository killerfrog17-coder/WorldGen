using System;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void AdvanceProvincialDispersal(PrehistoryState s,int id,int elapsed,long capacity)
        {
            var a=s.History.Aggregates;var r=a.Regions[id];long residents=s.Population[id],budget=residents/5;
            // Raw regional foragers and administered rural farmers are separate
            // real accounts. The presence of the former cannot cancel a paid
            // departure from the latter. Each branch keeps its own real debit.
            if(RemoteFarmerFrontierHistory(s)&&(residents<125||a.Empires.Connections.Realms.Version>=15))AdvanceRemoteFarmerFrontier(s,id,elapsed);
            var knowledge=TechnicalHistory(s)&&r.Culture>=0?s.History.Cultures[r.Culture]:null;
            double mobility=HistoricalTechnicalMigration.Mobility(knowledge,s.Years);
            int options=r.Routes.Length+(PassageHistory(s)?diffusionPassages[id].Length:0);
            if(residents<125||options==0)return;
            for(int trial=0;trial<Math.Min(4,options)&&budget>=25;trial++)
            {
                var route=PassageHistory(s)?DiffusionDeparture(s,id,trial):Routes[r.Routes[(int)(HistoryRoll("provincial-dispersal-route-"+trial,id,s.Years)*r.Routes.Length)]];
                int other=route.A==id?route.B:route.A;var destination=a.Regions[other];
                bool migrantLand=MigrantBorderHistory(s)&&!route.Sea;
                int departure=migrantLand?MigrantContactCell(s,route,id):r.BestCell;
                int arrival=migrantLand?MigrantContactCell(s,route,other):destination.BestCell;
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
                if(EnvironmentalHistory(s)&&knowledge!=null&&s.Society.Cultures[r.Culture].Farming>=180)chance*=EnvironmentalTransmission(s,departure,arrival,r.Culture,route.Sea);
                double residence=(route.DistanceM/1000.0+Patches[id].RadiusKm)*(.8+3*(1-similarity));
                if(knowledge!=null&&!route.Sea){residence=HistoricalTechnicalMigration.ResidencyYears(route.DistanceM/1000.0,Patches[id].RadiusKm,similarity,knowledge,s.Years);chance=Math.Min(.98,chance*Math.Sqrt(mobility));}
                long farmRoom=migrantLand&&knowledge!=null?CarriedFarmFrontierRoom(s,other,arrival,r.Culture,0,departure):0;
                if(destination.Capacity<25&&farmRoom<25||HistoryRoll("provincial-dispersal-"+trial,id,s.Years)>=ElapsedChance(chance,elapsed/100.0)
                    ||s.Years-s.ArrivalYear[id]<=residence)continue;
                long receivingCapacity=destination.Culture>=0?ProvincialRegionalCapacity(s,other,destination.Culture):destination.Capacity;
                if(migrantLand&&knowledge!=null&&destination.Culture<0&&s.Society.Cultures[r.Culture].Farming>=250&&CarriedCropLandscape(s,r.Culture,arrival,false,-1,departure)>=.12)
                {
                    // A district at its foraging limit is not necessarily full
                    // for a real incoming farming household. Count only the
                    // viable free border-cell fields, not a fictitious regional
                    // farm economy before any households arrive and practice.
                    long borderFields=(long)(CarriedFarmPotential(s,r.Culture,arrival,-1,departure)*D.Clamp(s.Society.Cultures[r.Culture].Farming/600.0));
                    receivingCapacity=Math.Max(receivingCapacity,destination.Capacity+Math.Min(500,borderFields));
                }
                long room=Math.Max(0,receivingCapacity-s.Population[other]);
                if(farmRoom>room)room=farmRoom;
                long limit=knowledge!=null&&!route.Sea?(long)(500*Math.Sqrt(mobility)):500;
                long people=Math.Min(budget,Math.Min(room,Math.Min(limit,Math.Max(25,(long)(residents*.02)))));if(people<25)continue;
                long before=s.Population[other];s.Population[id]-=people;s.Population[other]+=people;budget-=people;
                if(MigrantDemographyHistory(s))TransferRegionalCultureCounts(s,id,other,people);
                if(s.ArrivalYear[other]<0)s.ArrivalYear[other]=s.Years;
                if(LivingWorldHistory(s))MixHumanVariation(s,id,other,people,before);
                else for(int g=0;g<8;g++)s.Ancestry[other*8+g]=(int)((s.Ancestry[other*8+g]*before+s.Ancestry[id*8+g]*people)/Math.Max(1,before+people));
                if(PassageHistory(s)&&r.Culture>=0&&s.Society.Cultures[r.Culture].Farming>=180)
                {
                    int sourceCell=route.A==id?route.CellA:route.CellB,targetCell=route.A==id?route.CellB:route.CellA;
                    if(sourceCell<0)sourceCell=route.Sea&&developmentCoast[id]>=0?developmentCoast[id]:r.BestCell;
                    if(targetCell<0)targetCell=route.Sea&&developmentCoast[other]>=0?developmentCoast[other]:destination.BestCell;
                    if(migrantLand){sourceCell=departure;targetCell=arrival;}
                    // A rare primitive crossing can move people without a
                    // reliable farm/institution-carrying supply connection.
                    if(LivingWorldHistory(s)&&!InstitutionalRouteAllowed(s,sourceCell,targetCell,r.Culture))continue;
                    // Migrating households carry an available agricultural
                    // package. This creates contact, not automatic farming or
                    // a state: seeds still have to fit the destination.
                    int source=r.Culture;var local=a.Living.Regions[other];
                    local.Teacher=source;local.Exposure=Math.Min(1000,local.Exposure+(int)Math.Min(200,people));local.LastExchangeYear=s.Years;
                    a.Provinces.RegionalKnowledge[other]|=ProvincialKnowledge(s.History.Cultures[source]);
                    if(knowledge!=null)
                    {
                        int court=a.Living.CivicOrigins[source];if(court<0)court=a.Living.Regions[id].CivicTeacher;
                        if(court>=0)
                        {
                            if(!LivingWorldHistory(s)){local.CivicTeacher=court;local.CivicExposure=Math.Min(1000,local.CivicExposure+(int)Math.Min(400,people));}
                            else
                            {
                                int capital=s.Society.Settlements[s.History.Polities[court].CapitalId].Cell;
                                double sourceExample=Math.Max(InstitutionalRegionalExample(s,id,court),1000*InstitutionalLocalExample(s,court,capital,sourceCell));
                                double transmission=EnvironmentalTransmission(s,sourceCell,targetCell,source,route.Sea);
                                AccumulateInstitutionalDelivery(s,other,court,sourceExample,transmission,Math.Min(200,(int)people),"institutional-migrant-lesson-"+trial);
                            }
                        }
                    }
                    if(destination.Culture>=0)LivingCultureContactAt(s,destination.Culture,source,migrantLand?targetCell:destination.BestCell,migrantLand?sourceCell:destination.BestCell);
                    if(knowledge!=null&&destination.Culture<0&&s.Population[other]>=400)
                    {
                        int site=route.A==id?route.CellB:route.CellA;
                        if(migrantLand)site=targetCell;
                        if(site>=0&&HistoricalCells.Culture(s,site)<0&&CarriedCropLandscape(s,source,site,false,-1,sourceCell)>=.12)
                        {
                            // Descendant households do not forget their crops,
                            // language and technical knowledge merely because
                            // they crossed a demographic-region boundary.
                            int child=FoundMigrantAggregateCulture(s,other,site,source,sourceCell,-1,before);
                            HistoryEvent(s,"migrant-community",s.Society.Cultures[source].Name+" households establish "+s.Society.Cultures[child].Name+" as a local descendant community, bringing crops and skills suited to the new landscape.",site,child,category:ChronicleCategory.Society);
                        }
                    }
                }
            }
        }
    }
}
