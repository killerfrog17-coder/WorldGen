using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void ChangeAggregateCityPopulation(PrehistoryState s,int id,long urban,long suburban,long rural)
        {
            var a=s.History.Aggregates;var t=s.Society.Settlements[id];var city=a.Cities[id];var c=a.Cultures[t.CultureId];
            long du=urban-t.Population,ds=suburban-city.Suburban,dr=rural-city.Rural,total=du+ds+dr;
            if(LivingHistory(s))a.Living.Regions[CellPatch[t.Cell]].SettledPopulation+=total;
            c.Urban+=du;c.Rural+=ds+dr;c.LargestCity=Math.Max(c.LargestCity,urban);s.Society.Cultures[t.CultureId].Population=Math.Max(0,s.Society.Cultures[t.CultureId].Population+total);
            if(city.Supported)c.SupportedUrban+=du;
            int owner=a.Resolve(t.PolityId,s.History);if(owner>=0){var p=a.Polities[owner];p.Urban+=du;p.Suburban+=ds;p.Rural+=dr;s.History.Polities[owner].Population=Math.Max(0,s.History.Polities[owner].Population+total);}
            a.TotalPopulation+=total;a.TotalUrban+=du;t.Population=urban;city.Suburban=suburban;city.Rural=rural;
        }
        void AdvanceAggregateCity(PrehistoryState s,int id)
        {
            if(LivingHistory(s)&&s.History.Aggregates.Living.Cities[id].AbandonedYear>=0)return;
            var h=s.History;var a=h.Aggregates;var city=a.Cities[id];var t=s.Society.Settlements[id];int elapsed=s.Years-city.LastYear;if(elapsed<=0)return;city.LastYear=s.Years;
            if(LivingHistory(s))AdvanceLivingCityCondition(s,id,elapsed);
            int owner=a.Resolve(t.PolityId,h);if(owner!=t.PolityId){t.PolityId=owner;if(owner>=0){a.Polities[owner].Cities.Add(id);a.Polities[owner].CityCount=a.Polities[owner].Cities.Count;}}
            var culture=s.Society.Cultures[t.CultureId];var hc=h.Cultures[t.CultureId];var account=a.Cultures[t.CultureId];var record=h.Towns[id];
            if(LivingHistory(s)&&owner>=0){record.CivicTeacherPolityId=owner;record.CivicExampleExposure=1000;}
            bool wasSupported=t.FoodCapacity>=t.Population*.8&&t.Stability>=450;long oldCapacity=t.FoodCapacity,oldPopulation=t.Population;
            // Community capacity replaces, rather than adjusts, the legacy
            // estimates below. Do not evaluate those discarded crop/animal/
            // climate calculations for every city actor. The rural production
            // context is unchanged until this actor's population commit.
            var provisioned=ProvisionedPort(s,id);
            bool community=CommunityHistory(s);long communityRural=community&&provisioned==null?CommunityRuralCapacity(s,id):0;
            double farm=0,modern=0,yield=0;
            if(!community)
            {
            double cropSupport=AggregateCropSupport(s,t.CultureId,t.Cell,false);
            farm=(100.0+culture.Farming)/(100+city.BaseFarming);modern=HistoricalModernEffects.FoodMultiplier(hc,s.Years)/city.BaseFoodMultiplier;
            yield=(.4+.6*cropSupport)/city.BaseCropFactor;t.FoodCapacity=Math.Max(100,(long)Math.Round(city.BaseUrbanCapacity*farm*modern*yield));
            if(LivingHistory(s))
            {
                double urbanShare=.10+.05*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Roads,s.Years)+.05*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Printing,s.Years)+.20*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Railways,s.Years)+.15*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Sanitation,s.Years)+.10*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.MechanizedAgriculture,s.Years);
                t.FoodCapacity=Math.Max(0,(long)(city.RuralCapacity*HistoricalModernEffects.FoodMultiplier(hc,s.Years)*urbanShare*farm*yield*a.Living.Cities[id].Productivity));
                if(ProvincialHistory(s))
                {
                    urbanShare=HistoricalProvincialEffects.UrbanShare(hc,s.Years);
                    t.FoodCapacity=Math.Max(0,(long)(city.RuralCapacity*HistoricalModernEffects.FoodMultiplier(hc,s.Years)*urbanShare/1.15*farm*yield*a.Living.Cities[id].Productivity));
                    if(owner>=0)t.FoodCapacity+=ProvincialUrbanSupply(s,owner);
                }
            }
            }
            else if(provisioned==null)t.FoodCapacity=CommunityUrbanCapacity(s,id,communityRural);
            if(LivingWorldHistory(s))t.FoodCapacity=LivingProtocentreCapacity(s,id,t.FoodCapacity);
            if(provisioned!=null)t.FoodCapacity=provisioned.MaximumResidents;
            double weather=.82+.36*HistoryRoll("aggregate-harvest",id,s.Years),provisionedSupply=0;long harvest=(long)Math.Round(t.FoodCapacity*weather);
            record.LastHarvestHazard=HarvestHazard.None;record.ImportedFood=record.ExportedFood=record.FoodTransportLoss=0;
            if(provisioned==null&&HistoryRoll("aggregate-harvest-shock",id,s.Years)<.018*elapsed){harvest=(long)(harvest*(.3+.4*HistoryRoll("aggregate-harvest-severity",id,s.Years)));record.LastHarvestHazard=HarvestHazard.Drought;record.LastHarvestHazardYear=s.Years;}
            if(provisioned==null)t.FoodReserve=Math.Max(0,Math.Min(t.FoodCapacity*3,t.FoodReserve+(harvest-t.Population)*elapsed));
            else
            {
                // A delivered ration is stock, not a new local harvest as well.
                // Account all consumption, but never invent centuries of cargo
                // when a stale actor is reviewed with today's route/resources.
                long delivered=SupplyProvisionedPort(s,id,elapsed,provisioned);
                provisionedSupply=Math.Min(1,(t.FoodReserve+delivered)/Math.Max(1.0,t.Population*(double)elapsed));
                harvest=delivered/elapsed;t.FoodReserve=Math.Max(0,Math.Min(t.FoodCapacity*3,t.FoodReserve+delivered-t.Population*elapsed));
            }
            if(provisioned==null&&city.Neighbors.Length>0)
            {
                int peer=city.Neighbors[(int)(HistoryRoll("aggregate-trade-partner",id,s.Years)*city.Neighbors.Length)];var other=s.Society.Settlements[peer];double km=D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[other.Cell].Position)*World.Recipe.RadiusKm;
                bool compatible=(!LivingHistory(s)||a.Living.Cities[peer].AbandonedYear<0)&&(owner<0||other.PolityId<0||owner==a.Resolve(other.PolityId,h)||t.Stability>400&&other.Stability>400);
                int peerOwner=a.Resolve(other.PolityId,h);if(owner>=0&&peerOwner>=0&&owner!=peerOwner&&aggregateRelations.TryGetValue((Math.Min(owner,peerOwner),Math.Max(owner,peerOwner)),out int relation))compatible&=h.Relations[relation].WarSince<0;
                bool sea=km>200*HistoricalModernEffects.LandTransport(hc,s.Years)&&HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)&&km*1000<=HistoricalModernVoyageEffects.RangeM(hc,s.Years,ColonialHistory(s))&&AggregateSeaLane(t.Cell,other.Cell);
                if(compatible&&(km<200*HistoricalModernEffects.LandTransport(hc,s.Years)||sea))
                {
                    long surplus=Math.Max(0,t.FoodReserve-t.Population),need=Math.Max(0,other.Population-other.FoodReserve),export=Math.Min(surplus/5,Math.Min(need,t.Population/8));
                    if(export>0){t.FoodReserve-=export;long loss=(long)(export*Math.Min(.8,km/((sea?15000:1000)*HistoricalModernEffects.LandTransport(hc,s.Years))));other.FoodReserve+=export-loss;record.ExportedFood=export;record.FoodTransportLoss=loss;h.Towns[peer].ImportedFood+=export-loss;}
                }
            }
            double supply=provisioned==null?(harvest+Math.Min(t.FoodReserve,t.Population*.3))/Math.Max(1.0,t.Population):provisionedSupply,medical=HistoricalModernEffects.ChronicMortalityMultiplier(hc,s.Years);
            long chronic=(long)(t.Population*HistoricalUrbanEffects.ChronicLossRate(t.Population,owner>=0?h.Dynamics.States[owner].PublicWorks:0,World.Ecology.Habitat[t.Cell].AridityMilli/2000.0)*medical*elapsed);
            long natural=(long)Math.Round(t.Population*(.009+.005*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Sanitation,s.Years))*elapsed*(1-t.Population/(double)Math.Max(1,t.FoodCapacity)));
            if(supply<.8)natural-=(long)(t.Population*(.8-supply)*.025*elapsed);
            long disease=0;
            if(record.ImmunityUntil<s.Years&&HistoryRoll("aggregate-outbreak",id,s.Years)<.003*elapsed*(1+Math.Log(1+t.Population/3000.0)))
            {
                record.DiseaseKind=(HistoricalDiseaseKind)(1+(int)(HistoryRoll("aggregate-pathogen",id,s.Years)*3));record.DiseaseUntil=s.Years+2;record.ImmunityUntil=s.Years+20;
                disease=(long)(t.Population*(.02+.08*HistoryRoll("aggregate-disease-loss",id,s.Years))*HistoricalModernEffects.MortalityMultiplier(hc,s.Years,record.DiseaseKind));record.DiseaseDeaths+=disease;
                if(disease>=Math.Max(100,t.Population/20))HistoryEvent(s,"epidemic","An epidemic strikes "+t.Name+", with approximately "+disease.ToString("N0",System.Globalization.CultureInfo.InvariantCulture)+" deaths.",t.Cell,t.CultureId,owner,town:id,category:ChronicleCategory.Society);
            }
            long ruralCapacity=communityRural;
            if(!community)
            {
                ruralCapacity=Math.Max(city.Rural,(long)(city.RuralCapacity*(1+culture.Farming/90.0*city.FoodYieldMilli/1000.0)*modern));
                if(LivingHistory(s))ruralCapacity=Math.Max(1,(long)(city.RuralCapacity*modern*a.Living.Cities[id].Productivity));
                if(ProvincialHistory(s))ruralCapacity=(long)(ruralCapacity*(1-HistoricalProvincialEffects.UrbanShare(hc,s.Years)));
            }
            double ruralRate=CommunityHistory(s)?CommunityRuralGrowth(s,t.CultureId,t.FoundedYear):.006;
            long rural= Math.Max(0,city.Rural+(long)Math.Round(city.Rural*ruralRate*elapsed*(1-city.Rural/(double)Math.Max(1,ruralCapacity))));
            long migrants=Math.Min(rural/200,Math.Max(0,(t.FoodCapacity-t.Population)/30));if(supply<.9)migrants=0;rural-=migrants;
            long urban=Math.Max(0,t.Population+natural-chronic-disease+migrants);long suburban=provisioned==null?Math.Max(0,city.Suburban+(long)((urban*.15-city.Suburban)*Math.Min(1,.02*elapsed))):0;
            if(provisioned==null&&ProvincialHistory(s)&&owner>=0&&supply>=.9&&urban<t.FoodCapacity)
            {
                var country=a.Polities[owner];double connection=D.Clamp((HistoricalModernEffects.LandTransport(h.Cultures[h.Polities[owner].CultureId],s.Years)-1)/3.5);
                long arrivals=Math.Min(country.Remote/Math.Max(1,country.CityCount),(long)Math.Min((t.FoodCapacity-urban)/20.0,country.Remote*.006*connection*elapsed/Math.Max(1,country.CityCount)));
                country.Remote-=arrivals;h.Polities[owner].Population-=arrivals;a.TotalPopulation-=arrivals;s.Society.Cultures[h.Polities[owner].CultureId].Population=Math.Max(0,s.Society.Cultures[h.Polities[owner].CultureId].Population-arrivals);urban+=arrivals;
            }
            long suburbanChange=suburban-city.Suburban;if(suburbanChange>0){suburbanChange=Math.Min(suburbanChange,rural);suburban=city.Suburban+suburbanChange;rural-=suburbanChange;}else rural-=suburbanChange;
            ChangeAggregateCityPopulation(s,id,urban,suburban,rural);
            if(LivingHistory(s))
            {
                account.FoodCapacity+=t.FoodCapacity-oldCapacity;
                var life=a.Living.Cities[id];if(urban>life.PeakPopulation){life.PeakPopulation=urban;life.PeakYear=s.Years;}
                bool declining=supply<.75||urban<Math.Max(80,life.PeakPopulation*.2)||life.Productivity<.4;
                life.DeclineYears=declining?life.DeclineYears+elapsed:Math.Max(0,life.DeclineYears-elapsed*2);
                if(life.DeclineYears>=30&&urban<Math.Max(150,life.PeakPopulation*.35)||life.DeclineYears>=80||urban<25)
                {AbandonLivingTown(s,id,"Prolonged insecurity or loss of its provisioning base makes the old center untenable.");return;}
                if(LivingWorldHistory(s)&&LivingProtocentreDispersal(s,id,elapsed))return;
            }
            t.Health=(int)Math.Max(100,Math.Min(950,600+200*D.Clamp(supply)-disease*1000/Math.Max(1,oldPopulation)));t.Stability=Math.Max(100,Math.Min(950,t.Stability+(supply>=.9?3:-12)*elapsed));t.CultivatedPermille=provisioned==null?culture.Farming:0;
            bool supported=t.FoodCapacity>=t.Population*.8&&t.Stability>=450&&(provisioned==null||supply>=.8);if(supported!=city.Supported){account.SupportedCities+=supported?1:-1;account.SupportedUrban+=supported?t.Population:-t.Population;city.Supported=supported;}if(!LivingHistory(s))account.FoodCapacity+=t.FoodCapacity-oldCapacity;
            if(provisioned!=null)ExpandProvisionedPort(s,id,provisioned,supply);
            var demography=record.Demography??(record.Demography=new HistoricalUrbanDemography());demography.Year=s.Years;demography.NaturalChange=natural;demography.ChronicLoss=chronic;demography.RuralInflow=migrants;demography.TotalRuralInflow+=migrants;demography.TotalChronicLoss+=chronic;
            if(owner<0&&t.Population>=800&&t.Stability>=550&&s.Years-city.LastPoliticalYear>=10)
            {
                if(LivingHistory(s)){TryLivingInstitutions(s,id);city.LastPoliticalYear=s.Years;}
                else
                {
                city.LastPoliticalYear=s.Years;double fiscal=AggregateCropSupport(s,t.CultureId,t.Cell,true);double coordination=Math.Max(city.FloodMilli/200.0,city.Neighbors.Length/8.0);double circumscription=D.Clamp(1-a.Regions[CellPatch[t.Cell]].StoredMilli/Math.Max(1.0,city.StoredYieldMilli));double exchange=D.Clamp(city.Neighbors.Length/4.0);
                double readiness=HistoricalLocalEffects.FoundationReadiness(D.Clamp(fiscal/.55),D.Clamp(t.Population/6000.0),D.Clamp(hc.Institution/650.0),coordination,circumscription,exchange);
                bool example=false;foreach(int neighbor in city.Neighbors)if(a.Resolve(s.Society.Settlements[neighbor].PolityId,h)>=0){example=true;record.CivicTeacherPolityId=a.Resolve(s.Society.Settlements[neighbor].PolityId,h);break;}
                record.CivicPreparationYears+=10;if(example)record.CivicExampleExposure=Math.Min(1000,record.CivicExampleExposure+15);
                double origin=HistoricalEmergenceEffects.OriginMultiplier(fiscal,fiscal,coordination,circumscription,exchange,example?record.CivicExampleExposure:0);double hazard=HistoricalLocalEffects.FoundationChance(readiness)*origin*10;
                if(culture.Farming>=100&&record.CivicPreparationYears>=100&&HistoryRoll("aggregate-state-foundation",id,s.Years)<hazard)FoundAggregateState(s,id,-1);
                }
            }
            if(!LivingHistory(s)&&owner<0&&t.PolityId<0&&a.Cells[t.Cell*5+1]>=0&&s.Years-city.LastPoliticalYear>20&&t.Population>=800){city.LastPoliticalYear=s.Years;if(HistoryRoll("aggregate-successor-city",id,s.Years)<.15)FoundAggregateState(s,id,a.Cells[t.Cell*5+1]);}
            if(s.Years-city.LastVoyageYear>=10){city.LastVoyageYear=s.Years;AdvanceAggregateVoyage(s,id);}
            if(provisioned==null&&s.Years-city.LastHarvestYear>=20){city.LastHarvestYear=s.Years;SurveyAggregateCityCrops(s,id);}
            if(ProvincialHistory(s))AdvanceRegionalCityPolitics(s,id);
            if(provisioned==null&&LivingWorldHistory(s))TryLivingLocalTownOpportunity(s,id,elapsed);
        }
        double AggregateCropSupport(PrehistoryState s,int culture,int cell,bool stored)
        {
            var a=s.History.Agriculture[culture];var habitat=CropHabitat(s,cell);double best=0;double latitude=Math.Abs(Math.Asin(World.Surface.Cells[cell].Position.Z)*180/Math.PI);
            for(int k=0;k<6;k++)if(a.AdoptedYear[k]>0)
            {
                double fit=CommunityHistory(s)?(a.Cultivars?[k]==null?CommunityWildCropFit(s,cell,k):CommunityTransferFit(s,cell,a.Cultivars[k],HistoricalTechnologies.Has(s.History.Cultures[culture],HistoricalTechnology.CropRotation))):a.Cultivars?[k]==null?HistoricalCrops.Suitability(habitat,k,riverSites[cell]):HistoricalCropCatalog.TransferFit(habitat,a.Cultivars[k],latitude,riverSites[cell],HistoricalTechnologies.Has(s.History.Cultures[culture],HistoricalTechnology.CropRotation));
                double local=fit*(stored?HistoricalHusbandryEffects.StoragePermille(k,a.Cultivars?[k],HistoricalTechnologies.Has(s.History.Cultures[culture],HistoricalTechnology.Pottery))/1000.0:1);best=Math.Max(best,local);
            }
            return best;
        }
        void SurveyAggregateCityCrops(PrehistoryState s,int id)
        {
            var town=s.Society.Settlements[id];var crops=s.History.Agriculture[town.CultureId];var habitat=CropHabitat(s,town.Cell);double latitude=Math.Abs(Math.Asin(World.Surface.Cells[town.Cell].Position.Z)*180/Math.PI);var ids=new System.Collections.Generic.List<int>();var weights=new System.Collections.Generic.List<double>();
            for(int k=0;k<6;k++)if(crops.AdoptedYear[k]>0&&crops.Cultivars?[k]!=null){bool managed=HistoricalTechnologies.Has(s.History.Cultures[town.CultureId],HistoricalTechnology.CropRotation);double fit=PassageHistory(s)?CommunityTransferFit(s,town.Cell,crops.Cultivars[k],managed):HistoricalCropCatalog.TransferFit(habitat,crops.Cultivars[k],latitude,riverSites[town.Cell],managed);if(fit>=.02){ids.Add(crops.Cultivars[k].CropId);weights.Add(fit);}}
            int[] shares=new int[ids.Count];double sum=weights.Sum();int assigned=0;for(int k=0;k<shares.Length;k++){shares[k]=k==shares.Length-1?1000-assigned:(int)(weights[k]/sum*1000);assigned+=shares[k];}
            s.History.Towns[id].Cropping=new HistoricalLocalHarvest{Year=s.Years,CultureId=town.CultureId,CropIds=ids.ToArray(),SharesPermille=shares};
        }
    }
}
