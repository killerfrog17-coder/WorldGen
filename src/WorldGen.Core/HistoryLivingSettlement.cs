using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void TryLivingTownOpportunity(PrehistoryState s,int culture,int elapsed)
        {
            var a=s.History.Aggregates;var ca=a.Cultures[culture];var c=s.Society.Cultures[culture];
            int maturation=ConnectedHistory(s)&&s.History.Cultures[culture].ParentCultureId>=0?100:1000;
            double investment=TechnicalHistory(s)?HistoricalTechnicalMigration.SettlementInvestment(s.History.Cultures[culture],s.Years):1;
            if(TechnicalHistory(s))maturation=Math.Max(5,(int)(maturation/investment));
            if(c.Farming<180||s.Years-c.FoundedYear<maturation)return;
            if(ca.Cities.Count==0)
            {if(HistoryRoll("living-first-town",culture,s.Years)<1-Math.Exp(-elapsed*investment/600.0))FoundLivingTown(s,culture,ConnectedHistory(s)?DevelopmentEconomicSite(s,culture,c.OriginCell):c.OriginCell,-1);return;}
            // Secondary neighborhoods belong to their actual local town actor.
            // An overseas port must not compete with every metropolitan city
            // for the culture's single sampled parent opportunity.
            if(LivingWorldHistory(s))return;
            double mobility=ConnectedHistory(s)?Math.Sqrt(HistoricalModernEffects.LandTransport(s.History.Cultures[culture],s.Years))*(1+.4*HistoricalModernEffects.Adoption(s.History.Cultures[culture],HistoricalTechnology.Roads,s.Years)):1;
            if(ProvincialHistory(s))mobility*=Math.Min(8,Math.Max(1,Math.Sqrt(ca.Rural/20000.0)));
            if(HistoryRoll("living-secondary-town",culture,s.Years)>1-Math.Exp(-elapsed*Math.Sqrt(ca.Cities.Count)*mobility/600.0))return;
            int parent=ca.Cities.At((int)(HistoryRoll("living-town-source",culture,s.Years)*ca.Cities.Count)),from=s.Society.Settlements[parent].Cell;
            for(int trial=0;trial<3;trial++)
            {
                int site=from;
                for(int step=0;step<4+trial;step++)
                {var ns=World.Surface.Cells[site].Neighbors;int offset=(int)(HistoryRoll("living-town-walk-"+trial+"-"+step,culture,s.Years)*ns.Length);int next=-1;for(int k=0;k<ns.Length;k++){int n=ns[(offset+k)%ns.Length];if(CellPatch[n]>=0&&World.Landmass[n]==World.Landmass[from]&&World.Ecology.Habitat[n].ReliefM<2000){next=n;break;}}if(next<0)break;site=next;}
                int owner=HistoricalCells.Culture(s,site);if(owner>=0&&owner!=culture&&!ProvincialHistory(s))continue;
                int recipient=ProvincialHistory(s)&&owner>=0?owner:culture;
                if(recipient!=culture)LivingCultureContactAt(s,culture,recipient,from,site);
                double continuity=HistoricalLandscapeEffects.LivelihoodContinuity(World.Ecology.Habitat[ConnectedHistory(s)?from:c.OriginCell],World.Ecology.Habitat[from],World.Ecology.Habitat[site]);
                if(HistoryRoll("living-town-adaptation-"+trial,culture,s.Years)>Math.Max(.03,continuity))continue;
                if(EnvironmentalHistory(s)&&HistoryRoll("environment-settlement-migration-"+trial,culture,s.Years)>EnvironmentalTransmission(s,from,site,culture))continue;
                if(CanFoundLivingTown(s,recipient,site,parent)){FoundLivingTown(s,culture==recipient?culture:recipient,site,parent);return;}
            }
        }
        double LivingCropLandscape(PrehistoryState s,int culture,int cell,bool storage)
        {
            if(CommunityHistory(s))return CommunityCropLandscape(s,culture,cell,storage);
            var h=World.Ecology.Habitat[cell];var hc=s.History.Cultures[culture];bool managed=HistoricalTechnologies.Has(hc,HistoricalTechnology.CropRotation);
            double landscape=storage?(ConnectedHistory(s)?developmentStored[cell]:landscapeStored[cell]):landscapeYield[cell]*(ConnectedHistory(s)?HistoricalDevelopmentEffects.GrowingSeason(h):1);
            // Irrigated desert strips can support a court without pretending the
            // whole cell is irrigable. Evergreen clearing/soil costs still apply.
            double fraction=Math.Min(1,Math.Max(.08,D.Clamp((h.AridityMilli-100)/550.0)+riverAccess[cell]));
            double site=landscape/fraction;
            if(managed&&HistoricalLandscapeEffects.EverWet(h))site=Math.Max(site,.35*HistoricalCropCatalog.LeachingPenalty(h,true));
            if(HistoricalLandscapeEffects.EverWet(h))
            {
                // Local fallow/soil practice can mature without already owning a
                // state or medieval crop-rotation technology: no jungle deadlock.
                double husbandry=D.Clamp((s.History.Agriculture[culture].HusbandryYears?.Max()??0)/15000.0);
                site=Math.Max(site,.30*husbandry*HistoricalCropCatalog.LeachingPenalty(h,true));
            }
            return D.Clamp(site)*AggregateCropSupport(s,culture,cell,storage);
        }
        bool CanFoundLivingTown(PrehistoryState s,int culture,int cell,int parent)
        {
            var a=s.History.Aggregates;int region=cell>=0&&cell<CellPatch.Length?CellPatch[cell]:-1;if(region<0||a.TownAt[cell]>=0||World.Hydrology.LakeCoverageFraction[cell]>=.9)return false;
            double localYield=LivingCropLandscape(s,culture,cell,false);int practiced=s.History.Agriculture[culture].HusbandryYears?.Max()??0;
            int host=HistoricalCells.Polity(s,cell);bool remoteHouseholds=ProvincialHistory(s)&&host>=0&&a.Polities[host].Remote>=1600;
            if(parent<0&&s.Population[region]<800&&!remoteHouseholds||s.Society.Cultures[culture].Farming<180||localYield<.10&&(localYield<.02||practiced<10000))return false;
            if(LivingWorldHistory(s))
            {
                var habitat=CropHabitat(s,cell);
                // A procedural maritime arrival cannot conjure viable fields on
                // an icecap. Heated/import-dependent stations need later supply.
                bool modernSupply=HistoricalModernEffects.Adoption(s.History.Cultures[culture],HistoricalTechnology.CombustionEngines,s.Years)>.3;
                if((habitat.GrowingDays<90||habitat.WarmTenthsC<80||habitat.PermafrostPermille>700)&&!modernSupply)return false;
                if(parent<0&&HistoricalProtocentreEffects.MaximumResidents(EnvironmentalEstablishment(s,cell,culture),LivingCropLandscape(s,culture,cell,true),s.History.Cultures[culture].Institution,0)<120)return false;
            }
            int bucket=LivingTownBucket(World.Surface.Cells[cell].Position);if(a.Living.BucketCounts[bucket]>=16)return false;
            double modern=HistoricalModernEffects.Adoption(s.History.Cultures[culture],HistoricalTechnology.Railways,s.Years);
            double spacing=(100-45*modern)*Math.Sqrt(Math.Min(4,Math.Max(1,.25/Math.Max(.02,localYield))));var nearby=NearbyLivingTowns(s,cell,spacing);
            if(nearby.Length>0)return false;
            if(parent>=0)
            {
                var town=s.Society.Settlements[parent];var source=a.Cities[parent];
                if(a.Living.Cities[parent].AbandonedYear>=0||town.Population<1000||town.FoodReserve<town.Population*.5||(LivingWorldHistory(s)?LivingFounderAvailability(s,parent):source.Rural)<1600)return false;
            }
            return a.Living.Regions[region].CommittedAreaKm2<a.Regions[region].AreaKm2*.65;
        }
        void FoundLivingTown(PrehistoryState s,int culture,int cell,int parent)
            =>FoundLivingTownFunded(s,culture,cell,parent,-1);
        void FoundLivingTownFunded(PrehistoryState s,int culture,int cell,int parent,int fundingPolity)
            =>FoundLivingTownBasis(s,culture,cell,parent,fundingPolity,null);
        void FoundLivingTownBasis(PrehistoryState s,int culture,int cell,int parent,int fundingPolity,ProvisionedPortPlan provisioned)
            =>FoundLivingTownPrepared(s,culture,cell,parent,fundingPolity,provisioned,null);
        void FoundLivingTownPrepared(PrehistoryState s,int culture,int cell,int parent,int fundingPolity,ProvisionedPortPlan provisioned,CoastalFarmPlan coastal)
        {
            if(coastal!=null?!ValidCoastalFarmPlan(s,culture,cell,parent,coastal):provisioned==null?!CanFoundLivingTown(s,culture,cell,parent):!ValidProvisionedPortPlan(s,culture,cell,parent,provisioned))return;
            var h=s.History;var a=h.Aggregates;var c=s.Society.Cultures[culture];int patch=CellPatch[cell],id=s.Society.Settlements.Length;
            int previousFaith=ReligiousNetworkHistory(s)&&parent>=0?HistoricalCells.Religion(s,cell):-1;long previousResidents=ReligiousNetworkHistory(s)&&parent>=0?RuralCellPopulation(s,cell):0;
            int ruralSource=ProvincialHistory(s)&&parent<0&&s.Population[patch]<800?HistoricalCells.Polity(s,cell):-1;
            long available=parent>=0?(LivingWorldHistory(s)?LivingFounderAvailability(s,parent):a.Cities[parent].Rural):ruralSource>=0?a.Polities[ruralSource].Remote:s.Population[patch];
            long founders=Math.Min(250,available/12),rural=Math.Min(available/3,Math.Max(800,founders*8));
            if(provisioned!=null){founders=provisioned.Founders;rural=0;}
            if(coastal!=null){founders=coastal.Urban;rural=coastal.People-founders;}
            // Reserve a genuine share of the region's land and forager capacity.
            // Later centers must compete for that land, rather than each gaining
            // an independent 1,500-person food source for free.
            double localYield=LivingCropLandscape(s,culture,cell,false);
            long area=Math.Min(Math.Max(500,(long)(Math.PI*55*55*Math.Min(4,Math.Max(1,.25/Math.Max(.02,localYield))))),Math.Max(0,a.Regions[patch].AreaKm2-a.Living.Regions[patch].CommittedAreaKm2));
            if(provisioned!=null)area=10;
            if(coastal!=null)area=coastal.Area;
            if(area<(provisioned!=null?10:coastal!=null?50:500))return;long baseRural=Math.Min(a.Regions[patch].Capacity,(long)(Patches[patch].Capacity*area/Math.Max(1.0,a.Regions[patch].AreaKm2)));
            long capacity=Math.Max(800,(long)(area*(.8+10*localYield)*(.3+.7*c.Farming/600.0)));
            if(ProvincialHistory(s))capacity=Math.Max(800,(long)(area*HistoricalProvincialEffects.FarmingDensity(ProvincialPhysicalYield(s,culture,cell),c.Farming)));
            long urbanCapacity=Math.Max(500,(long)(capacity*(.035+.08*c.Farming/1000.0)));
            if(provisioned!=null){capacity=0;urbanCapacity=HistoricalProvisionedPortEffects.ResidentLimit;}
            var town=new Settlement{Id=id,Cell=cell,CultureId=culture,PolityId=-1,FoundedYear=s.Years,Name=LanguageHistory(s)?LanguageName(s,culture,LanguageNameKind.Settlement,unchecked(cell+id*7919),cell):HistoricalName("town",unchecked(cell+id*7919)),Population=founders,FoodCapacity=urbanCapacity,FoodReserve=founders,CultivatedPermille=c.Farming};
            if(provisioned!=null){town.FoodReserve=founders*3;town.CultivatedPermille=0;}
            var city=new HistoricalCityAggregate{Id=id,LastYear=s.Years,LastHarvestYear=s.Years,LastPoliticalYear=s.Years,LastVoyageYear=s.Years,ParentCity=parent,BaseUrbanCapacity=urbanCapacity,BaseFarming=c.Farming,BaseFoodMultiplier=HistoricalModernEffects.FoodMultiplier(h.Cultures[culture],s.Years),BaseCropFactor=.4+.6*AggregateCropSupport(s,culture,cell,false),FoodYieldMilli=(int)(landscapeYield[cell]*1000),StoredYieldMilli=(int)(landscapeStored[cell]*1000),FloodMilli=(int)(riverAccess[cell]*1000),SoilMilli=World.Ecology.Habitat[cell].SoilFertilityPermille,Rural=rural,RuralCapacity=capacity,AreaKm2=area,Supported=true};
            if(coastal!=null)TransferCoastalFarmFounders(s,coastal);
            else if(provisioned!=null)TransferProvisionedPortFounders(s,provisioned);
            else if(parent>=0){if(LivingWorldHistory(s))TransferLivingFounders(s,parent,founders+rural,culture);else{var source=s.Society.Settlements[parent];var supply=a.Cities[parent];ChangeAggregateCityPopulation(s,parent,source.Population,supply.Suburban,supply.Rural-founders-rural);a.TotalPopulation+=founders+rural;c.Population+=founders+rural;}}
            else if(ruralSource>=0)
            {
                a.Polities[ruralSource].Remote-=founders+rural;h.Polities[ruralSource].Population-=founders+rural;
                int sourceCulture=h.Polities[ruralSource].CultureId;s.Society.Cultures[sourceCulture].Population=Math.Max(0,s.Society.Cultures[sourceCulture].Population-founders-rural);c.Population+=founders+rural;
            }
            else s.Population[patch]-=founders+rural;
            a.Regions[patch].Capacity-=baseRural;a.Living.Regions[patch].CommittedAreaKm2+=area;
            if(ProvincialHistory(s))
            {
                int hostState=HistoricalCells.Polity(s,cell);
                if(hostState>=0)
                {
                    double share=D.Clamp(a.Provinces.ClaimedAreaKm2[patch]/(double)Math.Max(1,a.Regions[patch].AreaKm2));
                    if(provisioned==null)a.Provinces.FarmPotential[hostState]=Math.Max(0,a.Provinces.FarmPotential[hostState]-(long)(area*share*65*ProvincialPhysicalYield(s,culture,cell)));
                }
                RefreshProvincialRegionalCapacity(s,patch);
            }
            if(LivingWorldHistory(s)&&parent>=0)MixHumanVariation(s,CellPatch[s.Society.Settlements[parent].Cell],patch,founders+rural,s.Population[patch]);
            a.Living.Regions[patch].SettledPopulation+=founders+rural;
            s.Society.Settlements=s.Society.Settlements.Concat(new[]{town}).ToArray();h.Towns=h.Towns.Concat(new[]{new HistoricalTown{Id=id,FoodBase=urbanCapacity}}).ToArray();a.Cities=a.Cities.Concat(new[]{city}).ToArray();
            if(TechnicalHistory(s))Array.Resize(ref a.Empires.Territories.DistrictAreas,a.Cities.Length);
            a.Living.Cities=a.Living.Cities.Concat(new[]{new HistoricalLivingCity{PeakPopulation=founders,PeakYear=s.Years}}).ToArray();a.TownAt=a.TownAt.With(cell,id);RegisterLivingTown(s,id);
            if(provisioned!=null)RegisterProvisionedPort(s,id,provisioned);
            RegisterCoastalFarmFamily(s,id,parent);
            TrackPendingColonialPort(s,cell);
            if(CommunityHistory(s))a.Communities.Cities=a.Communities.Cities.Concat(new[]{new HistoricalLocalCommunity{SinceYear=s.Years}}).ToArray();
            city.Neighbors=NearbyLivingTowns(s,cell,280).Where(n=>n!=id&&LivingTownConnection(s,id,n)).Take(12).ToArray();
            foreach(int neighbor in city.Neighbors)LinkLivingTowns(s,id,neighbor);
            var ca=a.Cultures[culture];ca.Cities.Add(id);ca.CityCount=ca.Cities.Count;ca.Urban+=founders;ca.Rural+=rural;ca.SupportedUrban+=founders;ca.SupportedCities++;ca.FoodCapacity+=urbanCapacity;ca.LargestCity=Math.Max(ca.LargestCity,founders);a.TotalUrban+=founders;
            SetAggregateLayer(s,cell,0,culture);SetAggregateLayer(s,cell,2,h.Cultures[culture].ReligionId);SetAggregateLayer(s,cell,3,id);SetAggregateLayer(s,cell,4,2);ScheduleAggregate(s,CityProcess,id);
            if(ReligiousNetworkHistory(s)&&parent>=0)InheritReligiousHouseholds(s,cell,s.Society.Settlements[parent].Cell,previousFaith,previousResidents);
            int host=HistoricalCells.Polity(s,cell);
            if(host<0&&fundingPolity>=0){ClaimAggregatePolity(s,cell,fundingPolity);host=fundingPolity;}
            if(host>=0)AssignAggregateTown(s,id,host);
            if(host<0)ConnectColonialTown(s,id);
            if(coastal!=null)SurveyAggregateCityCrops(s,id);
            if(coastal!=null)HistoryEvent(s,"coastal-farms",town.Name+" begins with "+coastal.People+" actual emigrants from "+s.Society.Settlements[parent].Name+", establishing fields from their carried crops across a paid "+area+" km² local farm catchment. "+rural+" rations provision the farmers' first growing season; "+founders+" remain in the urban warehouse. The existing harbor remains dependent on its own supply route.",cell,culture,coastal.Polity,town:id,category:ChronicleCategory.Society);
            else if(provisioned==null)HistoryEvent(s,"settlement",town.Name+" begins as a community of "+founders+" people supported by neighboring farms.",cell,culture,town:id,category:ChronicleCategory.Society);
            else HistoryEvent(s,"provisioned-port",h.Polities[provisioned.Polity].Name+" finances a small harbor at "+town.Name+". "+founders+" residents arrive from "+s.Society.Settlements[parent].Name+"; its warehouses and paid shipping must continue to supply food. No local fields or farming households are assumed.",cell,culture,provisioned.Polity,town:id,category:ChronicleCategory.Society);
        }
        void AbandonLivingTown(PrehistoryState s,int id,string reason)
        {
            var h=s.History;var a=h.Aggregates;var t=s.Society.Settlements[id];var city=a.Cities[id];var life=a.Living.Cities[id];if(life.AbandonedYear>=0)return;
            int patch=CellPatch[t.Cell];long refugees=t.Population+city.Suburban+city.Rural;int oldOwner=a.Resolve(t.PolityId,h);
            AssignAggregateTown(s,id,-1);QueueTerritorialServiceRecovery(s,id);ChangeAggregateCityPopulation(s,id,0,0,0);s.Population[patch]+=refugees;a.TotalPopulation+=refugees;s.Society.Cultures[t.CultureId].Population+=refugees;
            var ca=a.Cultures[t.CultureId];if(city.Supported){ca.SupportedCities--;city.Supported=false;}ca.FoodCapacity-=t.FoodCapacity;ca.Cities.Remove(id);ca.CityCount=ca.Cities.Count;
            life.AbandonedYear=s.Years;t.FoodCapacity=t.FoodReserve=0;UnregisterLivingTown(s,id);if(a.TownAt[t.Cell]==id)a.TownAt=a.TownAt.With(t.Cell,-1);
            if(RegionalTraditions(s))s.History.RegionalTraditions.Qualified=s.History.RegionalTraditions.Qualified.With(id,0);
            TrackPendingColonialPort(s,t.Cell,true);
            a.Living.Regions[patch].CommittedAreaKm2=Math.Max(0,a.Living.Regions[patch].CommittedAreaKm2-city.AreaKm2);
            a.Regions[patch].Capacity+=(long)(Patches[patch].Capacity*city.AreaKm2/Math.Max(1.0,a.Regions[patch].AreaKm2));
            if(ProvincialHistory(s))
            {
                if(oldOwner>=0&&ProvisionedPort(s,id)==null){double share=D.Clamp(a.Provinces.ClaimedAreaKm2[patch]/(double)Math.Max(1,a.Regions[patch].AreaKm2));a.Provinces.FarmPotential[oldOwner]+=(long)(city.AreaKm2*share*65*ProvincialPhysicalYield(s,t.CultureId,t.Cell));}
                RefreshProvincialRegionalCapacity(s,patch);
            }
            if(HistoricalCells.Market(s,t.Cell)==id)SetAggregateLayer(s,t.Cell,3,-1);SetAggregateLayer(s,t.Cell,4,HistoricalCells.Polity(s,t.Cell)>=0?3:1);
            HistoryEvent(s,"abandonment",t.Name+" is abandoned. "+reason+" Surviving households disperse into the surrounding countryside; the former site remains in the historical record.",t.Cell,t.CultureId,oldOwner,town:id,category:ChronicleCategory.Society);
        }
        bool LivingTownConnection(PrehistoryState s,int first,int second)
        {
            int from=s.Society.Settlements[first].Cell,to=s.Society.Settlements[second].Cell;
            if(LivingWorldHistory(s)&&!InstitutionalRouteAllowed(s,from,to,s.Society.Settlements[first].CultureId))return false;
            // History32 uses the shared physical600-unit institutional reach
            // test above. Keep31's stricter360/transmission rule for old saves,
            // rather than applying two differently calibrated reach models.
            if(EnvironmentalHistory(s)&&!LivingWorldHistory(s))
            {
                int culture=s.Society.Settlements[first].CultureId;
                bool sea=DiffusionShore(from)&&DiffusionShore(to)&&DiffusionSeaEffort(s,from,to,culture)!=double.MaxValue;
                double km=D.Angle(World.Surface.Cells[from].Position,World.Surface.Cells[to].Position)*World.Recipe.RadiusKm;
                if(!sea&&km/EnvironmentalTransmission(s,from,to,culture)>360*HistoricalModernEffects.LandTransport(s.History.Cultures[culture],s.Years))return false;
            }
            if(World.Landmass[from]>=0&&World.Landmass[from]==World.Landmass[to])
            {var a=World.Ecology.Habitat[from];var b=World.Ecology.Habitat[to];return Math.Max(a.ReliefM,b.ReliefM)<2000||HistoricalTechnologies.Has(s.History.Cultures[s.Society.Settlements[first].CultureId],HistoricalTechnology.Roads);}
            return HistoricalTechnologies.Has(s.History.Cultures[s.Society.Settlements[first].CultureId],HistoricalTechnology.Sailing)&&AggregateSeaLane(from,to);
        }
        void LinkLivingTowns(PrehistoryState s,int first,int second)
        {
            if(PassageHistory(s))
            {
                foreach(var pair in new[]{(first,second),(second,first)})
                {
                    var c=s.History.Aggregates.Cities[pair.Item1];int from=s.Society.Settlements[pair.Item1].Cell;
                    double Km(int city)=>D.Angle(World.Surface.Cells[from].Position,World.Surface.Cells[s.Society.Settlements[city].Cell].Position)*World.Recipe.RadiusKm;
                    var candidates=c.Neighbors.Concat(new[]{pair.Item2}).Where(n=>s.History.Aggregates.Living.Cities[n].AbandonedYear<0).Distinct().ToArray();
                    var ports=candidates.Where(n=>Km(n)>280&&DiffusionShore(from)&&DiffusionShore(s.Society.Settlements[n].Cell)).OrderBy(Km).ThenBy(n=>n).Take(2).ToArray();
                    c.Neighbors=candidates.Except(ports).OrderBy(Km).ThenBy(n=>n).Take(12-ports.Length).Concat(ports).ToArray();
                }
                return;
            }
            foreach(var pair in new[]{(first,second),(second,first)})
            {var c=s.History.Aggregates.Cities[pair.Item1];c.Neighbors=c.Neighbors.Concat(new[]{pair.Item2}).Where(n=>s.History.Aggregates.Living.Cities[n].AbandonedYear<0).Distinct().OrderBy(n=>D.Angle(World.Surface.Cells[s.Society.Settlements[pair.Item1].Cell].Position,World.Surface.Cells[s.Society.Settlements[n].Cell].Position)).ThenBy(n=>n).Take(12).ToArray();}
        }
    }
}
