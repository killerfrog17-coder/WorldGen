using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        bool CommunityMaritimePractice(PrehistoryState s,int culture)
        {
            if(LivingWorldHistory(s))return LivingCommunityMaritimePractice(s,culture);
            bool Shore(int cell)=>Patches[CellPatch[cell]].Island||World.Surface.Cells[cell].Neighbors.Any(n=>World.Hydrology.OceanConnected[n]);
            int origin=s.Society.Cultures[culture].OriginCell;if(Shore(origin))return true;
            if(ColonialHistory(s)&&ColonialCoastalPractice(s,culture,s.History.Aggregates.Living.CivicOrigins[culture]))return true;
            var cities=s.History.Aggregates.Cultures[culture].Cities;
            int offset=cities.Count==0?0:(int)(HistoryRoll("community-maritime-centers",culture,s.Years)*cities.Count);
            for(int k=0;k<Math.Min(3,cities.Count);k++)
            {
                var town=s.Society.Settlements[cities.At((offset+k)%cities.Count)];if(Shore(town.Cell))return true;
                if(ColonialHistory(s)&&ColonialCoastalPractice(s,culture,town.PolityId))return true;
                if(ColonialHistory(s))
                {
                    int court=s.History.Aggregates.Resolve(town.PolityId,s.History);if(court<0||s.History.Polities[court].CultureId!=culture)continue;
                    var posts=s.History.Aggregates.Empires.Governments[court].AdministrativePosts;
                    for(int p=0;p<Math.Min(2,posts.Count);p++){int post=posts.At((int)(HistoryRoll("colonial-maritime-practice-"+p,court,s.Years)*posts.Count));var port=s.History.Aggregates.Empires.Territories.Posts[post];if(DiffusionShore(port.Cell)&&TerritorialNodeLive(s,court,-2-post))return true;}
                }
            }
            return false;
        }
        void AdvanceCommunityVoyage(PrehistoryState s,int region,int elapsed)
        {
            var h=s.History;var a=h.Aggregates;var options=communityVoyages[region];if(options.Length==0)return;
            int offset=(int)(HistoryRoll("community-expedition",region,s.Years)*options.Length);
            for(int trial=0;trial<Math.Min(4,options.Length);trial++)
            {
                var route=options[(offset+trial)%options.Length];int sourceState=HistoricalCells.Polity(s,route.CellA),culture=HistoricalCells.Culture(s,route.CellA);
                if(culture<0)culture=a.Regions[region].Culture;
                if(sourceState>=0)culture=h.Polities[sourceState].CultureId;
                long residents=sourceState>=0?a.Polities[sourceState].Remote:s.Population[region];int sourceTown=-1;
                if(TechnicalHistory(s)&&(culture<0||residents<250))
                {
                    // A populated port does not become unable to sail merely
                    // because its households left the unallocated region pool.
                    foreach(int port in NearbyLivingTowns(s,route.CellA,180))
                    {
                        var town=s.Society.Settlements[port];var farms=a.Cities[port];
                        if(!DiffusionShore(town.Cell)||!farms.Supported||town.Population<300||farms.Rural<250)continue;
                        sourceTown=port;sourceState=a.Resolve(town.PolityId,h);culture=ColonialHistory(s)&&sourceState>=0?h.Polities[sourceState].CultureId:town.CultureId;
                        residents=farms.Rural;break;
                    }
                }
                if(culture<0||residents<250)continue;
                var hc=h.Cultures[culture];if(!HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing))continue;
                double range=HistoricalDevelopmentEffects.CoastRangeKm(hc,s.Years,ColonialHistory(s));if(route.DistanceM>range*1000)continue;
                if(LivingWorldHistory(s)&&!InstitutionalRouteAllowed(s,route.CellA,route.CellB,culture))continue;
                int to=route.B,destinationState=HistoricalCells.Polity(s,route.CellB);if(sourceState>=0&&sourceState==destinationState)continue;
                double ships=HistoricalTechnologies.Has(hc,HistoricalTechnology.OceanNavigation)?1:HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)?.35:.08;
                double chance=ships*Math.Exp(-route.DistanceM/(range*700))*Math.Min(3,Math.Sqrt(residents/1000.0));
                if(HistoryRoll("community-sea-contact-"+trial,region,s.Years)>1-Math.Exp(-chance*elapsed/50.0))continue;
                var target=a.Living.Regions[to];int peer=a.Regions[to].Culture;if(destinationState>=0)peer=h.Polities[destinationState].CultureId;
                bool reliableDelivery=!LivingWorldHistory(s)||InstitutionalVoyageDelivery(s,route,culture,sourceState,sourceTown);
                if(reliableDelivery&&peer>=0&&peer!=culture)
                {if(LivingWorldHistory(s))LivingCultureContactAt(s,culture,peer,route.CellA,route.CellB);else LivingCultureContact(s,culture,peer);}
                if(reliableDelivery&&PassageHistory(s))DiffusionPortContact(s,route,culture,elapsed);
                if(!GeographicHistory(s))TryTerritorialSeaCharter(s,route,culture,elapsed);
                if(reliableDelivery)RecordStateVoyageReport(s,sourceState,route.CellB);
                if(ColonialHistory(s)){RecordCoastChart(s,culture,to,culture);destinationState=HistoricalCells.Polity(s,route.CellB);}
                if(reliableDelivery)
                {
                    if(target.Teacher<0||CurrentProvincialKnowledgeCount(s,target.Teacher)<=CurrentProvincialKnowledgeCount(s,culture))target.Teacher=culture;
                    target.Exposure=Math.Min(1000,target.Exposure+Math.Max(100,elapsed*2));target.LastExchangeYear=s.Years;a.Provinces.RegionalKnowledge[to]|=ProvincialKnowledge(hc);
                }
                int court=sourceState>=0?sourceState:a.Living.CivicOrigins[culture];
                if(court>=0&&reliableDelivery)
                {
                    if(!LivingWorldHistory(s)){target.CivicTeacher=court;target.CivicExposure=Math.Min(1000,target.CivicExposure+elapsed*2);}
                    else
                    {
                        int capital=s.Society.Settlements[h.Polities[court].CapitalId].Cell;
                        double source=sourceState>=0?1000:1000*InstitutionalLocalExample(s,court,capital,route.CellA);
                        double transmission=EnvironmentalTransmission(s,route.CellA,route.CellB,culture,true);
                        AccumulateInstitutionalDelivery(s,to,court,source,transmission,elapsed,"institutional-voyage-lesson-"+trial);
                    }
                }
                // Stateful long-distance settlement is an organized, supplied
                // venture, not unlocated remote people appearing as independent
                // primitive coastal cultures. Independent human voyaging remains.
                if(GeographicHistory(s)&&sourceState>=0&&(route.DistanceM>120000||HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years)>=.2))continue;
                if(GeographicHistory(s)&&!reliableDelivery)continue;
                long carrying=Patches[to].Capacity;
                if(s.Society.Cultures[culture].Farming>=180)carrying=(long)(carrying*(1+s.Society.Cultures[culture].Farming/80.0*CarriedCropLandscape(s,culture,route.CellB,false,sourceTown,route.CellA)*HistoricalModernEffects.FoodMultiplier(hc,s.Years)));
                long destination=destinationState>=0?a.Polities[destinationState].Remote:s.Population[to];
                if(destinationState>=0)carrying=ProvincialRemoteCapacity(s,destinationState);
                long migrants=Math.Min(residents/20,Math.Min(1000,Math.Max(0,carrying-destination)));if(migrants<50)continue;
                if(sourceTown>=0)
                {var town=s.Society.Settlements[sourceTown];var farms=a.Cities[sourceTown];ChangeAggregateCityPopulation(s,sourceTown,town.Population,farms.Suburban,farms.Rural-migrants);a.TotalPopulation+=migrants;}
                else if(sourceState>=0){a.Polities[sourceState].Remote-=migrants;h.Polities[sourceState].Population-=migrants;s.Society.Cultures[h.Polities[sourceState].CultureId].Population=Math.Max(0,s.Society.Cultures[h.Polities[sourceState].CultureId].Population-migrants);}
                else {s.Population[region]-=migrants;if(MigrantDemographyHistory(s))ChangeRegionalCultureCount(s,region,-migrants);}
                if(destinationState>=0){a.Polities[destinationState].Remote+=migrants;h.Polities[destinationState].Population+=migrants;s.Society.Cultures[h.Polities[destinationState].CultureId].Population+=migrants;}
                else {s.Population[to]+=migrants;if(MigrantDemographyHistory(s))ChangeRegionalCultureCount(s,to,migrants);}
                if(s.ArrivalYear[to]<0)s.ArrivalYear[to]=s.Years;
                if(LivingWorldHistory(s))MixHumanVariation(s,sourceTown>=0?CellPatch[s.Society.Settlements[sourceTown].Cell]:region,to,migrants,destination);
                else for(int g=0;g<8;g++)s.Ancestry[to*8+g]=(int)((s.Ancestry[to*8+g]*destination+s.Ancestry[region*8+g]*migrants)/Math.Max(1,destination+migrants));
                if(a.Regions[to].Culture<0&&HistoricalCells.Culture(s,route.CellB)<0&&s.Population[to]>=400)
                {
                    FoundAggregateCulture(s,to,route.CellB,culture);int child=s.Society.Cultures.Length-1;InheritDevelopmentCommunityFrom(s,child,culture,route.CellB,route.CellA,sourceTown);
                    if(ColonialHistory(s)&&sourceState>=0){var continuity=a.Empires.Connections.Cultures[child];continuity.ColonialSponsor=sourceState;continuity.CivilizationRoot=HistoricalRecognizedClaims.Civilization(s,culture);}
                    HistoryEvent(s,"overseas-community",s.Society.Cultures[culture].Name+" voyagers establish a community along a sea route of approximately "+route.DistanceM/1000+" km, bringing crops and practical knowledge that must work in their new environment.",route.CellB,child,category:ChronicleCategory.Society);
                }
                break;
            }
        }
    }
}
