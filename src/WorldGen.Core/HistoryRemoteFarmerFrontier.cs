using System;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        static bool RemoteFarmerFrontierHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=9;

        // Administering a district moves its rural people into a Remote account;
        // it does not end household migration. Probe two physical land edges,
        // never a country's holdings. Downscaled presence limits a proposal;
        // only the actual Remote ledger can pay for the emigrants.
        void AdvanceRemoteFarmerFrontier(PrehistoryState s,int region,int elapsed)
        {
            int[] links=realmLandRoutes[region];if(links.Length==0)return;
            int offset=(int)(HistoryRoll("remote-farmer-edge",region,s.Years)*links.Length);
            for(int trial=0;trial<Math.Min(2,links.Length);trial++)
                if(TryRemoteFarmerFrontier(s,region,Routes[links[(offset+trial)%links.Length]],elapsed,trial))return;
        }
        bool TryRemoteFarmerFrontier(PrehistoryState s,int region,Route route,int elapsed,int trial)
        {
            if(!RemoteFarmerFrontierHistory(s)||elapsed<=0||route.Sea||route.LandCellA<0||route.LandCellB<0)return false;
            var h=s.History;var a=h.Aggregates;int to=route.A==region?route.B:route.A;
            if(route.A!=region&&route.B!=region)return false;
            int fromCell=route.A==region?route.LandCellA:route.LandCellB,target=route.A==region?route.LandCellB:route.LandCellA;
            if(World.Landmass[fromCell]<0||World.Landmass[fromCell]!=World.Landmass[target]
                ||a.Regions[to].Culture>=0||HistoricalCells.Culture(s,target)>=0||HistoricalCells.Polity(s,target)>=0)return false;
            int owner=HistoricalCells.Polity(s,fromCell),raw=a.Cells[fromCell*5+1];
            if(owner<0||raw<0)return false;int cohort=a.PopulationCohort(raw);
            // A resolved living account is required; no abandoned predecessor
            // account or mere recognized claim can finance this journey.
            if(cohort!=owner)return false;
            var country=a.Polities[cohort];int culture=h.Polities[cohort].CultureId;var people=s.Society.Cultures[culture];
            if(country.Remote<250||people.Farming<250||country.RemoteAreaKm2<=0
                ||!InstitutionalRouteAllowed(s,fromCell,target,culture)||!PreparedFieldHistory(s)&&CarriedCropLandscape(s,culture,target,false,-1,fromCell)<.12)return false;
            long local=(long)(country.Remote*Math.Min(1,inhabitableArea[fromCell]/country.RemoteAreaKm2));
            if(local<250)return false;
            var prepared=QuoteRemoteFields(s,culture,fromCell,target,owner,Math.Min(500,Math.Min(local/20,country.Remote/100)));
            if(PreparedFieldHistory(s)&&(prepared?.Landscape??CarriedCropLandscape(s,culture,target,false,-1,fromCell))<.12)return false;
            long fields=(long)(CarriedFarmPotential(s,culture,target,-1,fromCell)*D.Clamp(people.Farming/600.0));
            if(prepared!=null)fields=(long)(fields*HistoricalPreparedFieldEffects.ClearingGain(CropHabitat(s,target),riverAccess[target]));
            long room=Math.Max(0,a.Regions[to].Capacity+Math.Min(500,fields)-s.Population[to]);
            if(FarmFrontierVacancyHistory(s))
            {
                if(prepared!=null)
                {
                    int localCulture=a.Regions[to].Culture;
                    room=HistoricalFarmFrontier.Room(room,fields,AggregateRuralCellPopulation(s,target),localCulture>=0?s.Society.Cultures[localCulture].Farming:0);
                }
                else room=CarriedFarmFrontierRoom(s,to,target,culture,room,fromCell);
            }
            long migrants=Math.Min(500,Math.Min(room,Math.Min(local/20,country.Remote/100)));
            if(migrants<50)return false;
            if(prepared!=null&&prepared.Farmers!=migrants)prepared=QuoteRemoteFields(s,culture,fromCell,target,owner,migrants);
            int source=a.Empires.Territories.Sources[fromCell];
            if(!TerritorialNodeLive(s,owner,source))return false;
            for(int depth=0;source<=-2&&depth<8;depth++)source=a.Empires.Territories.Posts[-source-2].ParentSource;
            if(source<0||source>=a.Cities.Length||!a.Cities[source].Supported||a.Living.Cities[source].AbandonedYear>=0)return false;
            var granary=s.Society.Settlements[source];
            double transport=HistoricalModernEffects.LandTransport(h.Cultures[culture],s.Years);
            double effort=LivingTravelKm(s,granary.Cell,fromCell,culture)+LivingTravelKm(s,fromCell,target,culture);
            if(double.IsInfinity(effort)||effort>=double.MaxValue)return false;
            long food=HistoricalInlandSettlement.Provisions(migrants,effort,transport)+migrants/2;
            long investment=HistoricalInlandSettlement.Investment(migrants,effort,transport);
            if(granary.FoodReserve<food+(prepared?.Food??0)+granary.Population/2||h.Polities[owner].Treasury<(investment+(prepared?.Investment??0))*2)return false;
            double transmission=EnvironmentalTransmission(s,fromCell,target,culture);
            double similarity=Math.Exp(-Math.Abs(a.Regions[region].MeanTenthsC-a.Regions[to].MeanTenthsC)/180.0
                -Math.Abs(a.Regions[region].AridityMilli-a.Regions[to].AridityMilli)/1000.0);
            double chance=.45*(.2+.8*similarity)*transmission;
            if(HistoryRoll("remote-farmer-departure-"+trial,region,s.Years)>=ElapsedChance(chance,elapsed/100.0))return false;

            long before=s.Population[to];country.Remote-=migrants;h.Polities[cohort].Population-=migrants;
            people.Population=Math.Max(0,people.Population-migrants);s.Population[to]+=migrants;
            // The world total does not change: debit and credit are simultaneous.
            ChangeRegionalCultureCount(s,to,migrants);granary.FoodReserve-=food;h.Polities[owner].Treasury-=investment;
            if(prepared!=null)CommitPreparedFields(s,prepared,-1);
            if(s.ArrivalYear[to]<0)s.ArrivalYear[to]=s.Years;
            MixHumanVariation(s,region,to,migrants,before);
            var arrival=a.Living.Regions[to];arrival.Teacher=culture;arrival.Exposure=Math.Min(1000,arrival.Exposure+(int)Math.Min(200,migrants));arrival.LastExchangeYear=s.Years;
            a.Provinces.RegionalKnowledge[to]|=ProvincialKnowledge(h.Cultures[culture]);
            int court=a.Living.CivicOrigins[culture];if(court<0)court=owner;
            int capital=s.Society.Settlements[h.Polities[court].CapitalId].Cell;
            double example=Math.Max(InstitutionalRegionalExample(s,region,court),1000*InstitutionalLocalExample(s,court,capital,fromCell));
            AccumulateInstitutionalDelivery(s,to,court,example,transmission,Math.Min(200,(int)migrants),"remote-farmer-institution-"+trial);
            if(s.Population[to]>=400)
            {
                FoundAggregateCulture(s,to,target,culture);int child=s.Society.Cultures.Length-1;
                InheritDevelopmentCommunityFrom(s,child,culture,target,fromCell,-1);
            }
            HistoryEvent(s,"migrant-frontier",migrants+" people from farming households leave "+people.Name+" for neighboring farmland, carrying viable crops and provisions from "+granary.Name+". A local farming community is not a new town or sovereign state.",target,a.Regions[to].Culture,category:ChronicleCategory.Society);
            return true;
        }
    }
}
