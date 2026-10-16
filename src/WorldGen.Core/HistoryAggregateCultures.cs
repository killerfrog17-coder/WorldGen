using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void AdvanceAggregateCulture(PrehistoryState s,int id)
        {
            var h=s.History;var a=h.Aggregates;var c=s.Society.Cultures[id];var hc=h.Cultures[id];var ca=a.Cultures[id];int elapsed=s.Years-ca.LastYear;if(elapsed<=0)return;ca.LastYear=s.Years;
            AdvanceCulturalContinuity(s,id);
            int cell=c.OriginCell;var habitat=World.Ecology.Habitat[cell];var crops=h.Agriculture[id];
            int target=LocalFrontierTarget(s,ca.Frontier,id,"aggregate-culture-frontier",0),contact=-1;
            if(target>=0&&!LivingHistory(s))
            {
                int owner=HistoricalCells.Culture(s,target);if(owner>=0)contact=owner;
                else
                {
                    var other=World.Ecology.Habitat[target];double compatibility=Math.Exp(-Math.Abs(habitat.MeanTenthsC-other.MeanTenthsC)/170.0-Math.Abs(habitat.AridityMilli-other.AridityMilli)/900.0);
                    if(s.Population[CellPatch[target]]>=50&&HistoryRoll("aggregate-culture-spread",id,s.Years)<.12*compatibility*elapsed/40.0)
                    {SetAggregateLayer(s,target,0,id);SetAggregateLayer(s,target,2,hc.ReligionId);SetAggregateLayer(s,target,4,1);c.Population+=RuralCellPopulation(s,target);if(a.Regions[CellPatch[target]].Culture<0)a.Regions[CellPatch[target]].Culture=id;}
                }
            }
            int urbanSite=cell,governingCulture=-1;
            if(ca.Cities.Count>0)
            {
                int townId=ca.Cities.At((int)(HistoryRoll("aggregate-culture-city",id,s.Years)*ca.Cities.Count));var town=s.Society.Settlements[townId];urbanSite=town.Cell;
                var neighbors=a.Cities[townId].Neighbors;if(neighbors.Length>0){int peer=neighbors[(int)(HistoryRoll("aggregate-culture-contact",id,s.Years)*neighbors.Length)];int other=s.Society.Settlements[peer].CultureId;if(other!=id){contact=other;if(EnvironmentalHistory(s))LivingCultureContactAt(s,id,other,town.Cell,s.Society.Settlements[peer].Cell);}}
                if(LivingHistory(s))
                {
                    int ruler=a.Resolve(town.PolityId,h);
                    if(ruler>=0&&town.Population>=1200&&a.Cities[townId].Supported)
                    {if(ca.CivilizationSince<0)ca.CivilizationSince=s.Years;governingCulture=h.Polities[ruler].CultureId;if(governingCulture==id)governingCulture=-1;else if(EnvironmentalHistory(s))LivingCultureContactAt(s,id,governingCulture,town.Cell,town.Cell);else LivingCultureContact(s,id,governingCulture);}
                }
            }
            if(LivingHistory(s)){ExpandLivingCulture(s,id,elapsed);contact=LivingContact(s,id,contact);if(governingCulture>=0&&HistoryRoll("living-civic-knowledge",id,s.Years)<.5)contact=governingCulture;}
            if(ConnectedHistory(s))
            {
                StudyDevelopmentCropSites(s,id,a.Regions[ca.Region].BestCell);StudyDevelopmentCropSites(s,id,urbanSite);
                cell=DevelopmentEconomicSite(s,id,cell);habitat=World.Ecology.Habitat[cell];
            }
            if(contact>=0&&contact!=id)
            {if(!c.Contacts.Contains(contact))c.Contacts=c.Contacts.Concat(new[]{contact}).OrderBy(x=>x).ToArray();var other=s.Society.Cultures[contact];if(!other.Contacts.Contains(id))other.Contacts=other.Contacts.Concat(new[]{id}).OrderBy(x=>x).ToArray();}
            LanguageContact(s,id,contact);
            double pressure=(s.Population[CellPatch[cell]]+ca.Urban+ca.Rural)/(double)Math.Max(1,Patches[CellPatch[cell]].Capacity);
            if(LivingHistory(s))pressure=(s.Population[CellPatch[cell]]+a.Living.Regions[CellPatch[cell]].SettledPopulation)/(double)Math.Max(1,Patches[CellPatch[cell]].Capacity);
            if(HistoricalLocalEffects.AgricultureOpen(s.Years)&&s.Years-c.FoundedYear>=500)
            for(int k=0;k<6;k++)
            {
                int practiceCell=ConnectedHistory(s)?crops.PracticeCell[k]:cell;
                var practiceHabitat=World.Ecology.Habitat[practiceCell];
                if(crops.AdoptedYear[k]>0){var cultivar=crops.Cultivars?[k];if(cultivar!=null){int practiced=PassageHistory(s)?ActiveCommunityCropYears(s,id,k,elapsed,urbanSite):elapsed;int delta=Math.Min(practiced,Math.Max(0,4000-cultivar.SelectedYears));cultivar.SelectedYears+=delta;crops.HusbandryYears[k]=Math.Min(20000,crops.HusbandryYears[k]+practiced);var definition=HistoricalCropCatalog.Crops[cultivar.CropId];cultivar.YieldPermille=Math.Min(1500,650+cultivar.SelectedYears/5);cultivar.MaturityDays=Math.Max(definition.MaturityDays-35,definition.MaturityDays-cultivar.SelectedYears/100);cultivar.MinimumWarmTenthsC=Math.Max((definition.MinimumWarmC-4)*10,definition.MinimumWarmC*10-cultivar.SelectedYears/100);}continue;}
                int donor=contact>=0&&h.Agriculture[contact].AdoptedYear[k]>0&&h.Agriculture[contact].AdoptedYear[k]-1<s.Years?contact:-1;
                if(ConnectedHistory(s))donor=DevelopmentCropDonor(s,id,k,practiceCell,donor);
                if(donor<0&&(crops.NativeMask&(1<<k))==0)continue;
                var selected=donor>=0?h.Agriculture[donor].Cultivars?[k]?.Copy():AggregateCultivar(s,id,k,practiceCell);if(selected==null)continue;
                double fit=CommunityHistory(s)?CommunityTransferFit(s,practiceCell,selected):HistoricalCropCatalog.TransferFit(practiceHabitat,selected,Math.Abs(Math.Asin(World.Surface.Cells[practiceCell].Position.Z)*180/Math.PI),riverSites[practiceCell]);if(fit<.12)continue;
                double surplus=fit*HistoricalCrops.Storage[k]/1000.0;double incentive=D.Clamp((pressure-.5)*1.3)*D.Clamp(surplus*2)*HistoricalCropCatalog.LeachingPenalty(habitat,false);
                double gain=elapsed/50.0*(donor>=0?12:1.2)*incentive*fit;
                if(LivingHistory(s))
                {
                    double localAdvantage=HistoricalEmergenceEffects.CultivationIncentive(pressure,fit,landscapeSeason[cell],D.Clamp(ruralWeight[cell]/Math.Max(1,inhabitableArea[cell]*150)));
                    bool cultivationContact=contact>=0&&s.Society.Cultures[contact].Farming>=250;
                    double native=donor>=0?1:cultivationContact?.1+.9*Math.Pow(NativeDomesticability(practiceCell,k),2):.02+.98*Math.Pow(NativeDomesticability(practiceCell,k),3);
                    double clearing=Math.Max(.015,D.Clamp(landscapeYield[practiceCell]/.5));
                    gain=elapsed/50.0*(donor>=0?65:cultivationContact?60:24)*localAdvantage*fit*native*clearing;
                    if(PassageHistory(s)&&donor>=0)gain*=4;
                    if(ConnectedHistory(s))gain*=HistoricalDevelopmentEffects.GrowingSeason(practiceHabitat);
                }
                int whole=(int)gain;if(HistoryRoll("aggregate-crop-practice-"+k,id,s.Years)<gain-whole)whole++;crops.Practice[k]=Math.Min(1000,crops.Practice[k]+whole);
                if(crops.Practice[k]>=600&&HistoryRoll("aggregate-crop-adoption-"+k,id,s.Years)<.2)
                {crops.AdoptedYear[k]=s.Years+1;crops.SourceCulture[k]=donor;selected.ParentCulture=donor;crops.Cultivars[k]=selected;hc.Technology[1]=hc.Technology[1]>0?hc.Technology[1]:s.Years+1;HistoryEvent(s,"crop-domestication",c.Name+(donor<0?" establishes cultivation of ":" adopts ")+HistoricalCrops.Names[k]+(donor<0?" through local selection.":" through contact with "+s.Society.Cultures[donor].Name+"."),cell,id,category:ChronicleCategory.Discoveries);}
            }
            double crop=AggregateCropSupport(s,id,cell,false);int farmingTarget=(int)(900*crop*D.Clamp((pressure-.25)/.75));
            if(LivingHistory(s))farmingTarget=crops.AdoptedYear.Any(y=>y>0)?HistoricalEmergenceEffects.FarmingLimit(crop,HistoricalEmergenceEffects.CultivationIncentive(pressure,crop,landscapeSeason[cell],.5)):0;
            if(CommunityHistory(s)&&ca.SupportedCities>0&&crops.AdoptedYear.Any(y=>y>0))farmingTarget=Math.Max(farmingTarget,(int)((250+500*D.Clamp(ca.SupportedUrban/3000.0))*Math.Max(crop,AggregateCropSupport(s,id,urbanSite,false))));
            c.Farming=Math.Max(0,Math.Min(1000,c.Farming+(int)((farmingTarget-c.Farming)*Math.Min(.3,elapsed/600.0))));
            hc.Sedentism=Math.Min(1000,150+c.Farming/2+(int)(300*D.Clamp(settlementOpportunity[cell])));hc.Surplus=(int)Math.Min(1000,100+700*AggregateCropSupport(s,id,cell,true)+100*D.Clamp(ca.FoodCapacity/(double)Math.Max(1,ca.Urban)-.8));
            if(LivingHistory(s)&&ca.Cities.Count>0)hc.Surplus=(int)Math.Min(1000,100+700*Math.Max(AggregateCropSupport(s,id,cell,true),AggregateCropSupport(s,id,urbanSite,true))+100*D.Clamp(ca.FoodCapacity/(double)Math.Max(1,ca.Urban)-.8));
            if(ConnectedHistory(s))hc.Surplus=(int)Math.Min(1000,100+750*D.Clamp(Math.Max(LivingCropLandscape(s,id,cell,true),LivingCropLandscape(s,id,urbanSite,true))/.4)+100*D.Clamp(ca.FoodCapacity/(double)Math.Max(1,ca.Urban)-.8));
            hc.Institution=Math.Max(0,Math.Min(1000,hc.Institution+(int)((hc.Sedentism*.6+Math.Log(1+ca.Urban)*25-hc.Institution)*Math.Min(.3,elapsed/500.0))));c.Organization=hc.Institution;
            if(TechnicalHistory(s))AdvanceTechnicalResearch(s,id,elapsed,contact,urbanSite);
            else for(int k=0;k<hc.Technology.Length;k++)
            {
                if(hc.Technology[k]>0||HistoricalModernEffects.Era((HistoricalTechnology)k)>h.Modern.TechnologyCeiling)continue;
                int[] required=HistoricalTechnologies.Requirements(h.Version,k);if(required.Any(p=>hc.Technology[p]==0))continue;
                int teacher=ConnectedHistory(s)?DevelopmentTeacher(s,id,k,contact):contact;
                bool learned=teacher>=0&&h.Cultures[teacher].Technology[k]>0&&h.Cultures[teacher].Technology[k]-1<s.Years;
                int mature=required.Select(p=>hc.Technology[p]-1).DefaultIfEmpty(c.FoundedYear).Max();if(s.Years-mature<(ProvincialHistory(s)?HistoricalTechnologyDevelopment.Maturation(k,learned):learned?80:k>=27?100:k>=15?180:k>=5?300:100))continue;
                if(!AggregatePracticeSupported(s,id,k,learned))continue;
                double effort=(2+Math.Log(1+ca.SupportedUrban)/2.0)*(hc.Institution+200)/800.0*(hc.Surplus+200)/700.0*(learned?3:1)*elapsed/40.0;
                if(LivingHistory(s))effort*=learned?4:CommunityHistory(s)?CommunityTechnicalExchange(s,id,k):1+3*D.Clamp(c.Contacts.Length/4.0);
                if(ConnectedHistory(s))effort*=learned?1+Math.Log(1+HistoricalModernEffects.Communication(h.Cultures[teacher],s.Years)):.65+1.35*D.Clamp(Math.Sqrt(ca.SupportedCities)/3.0);
                if(ProvincialHistory(s))effort*=HistoricalTechnologyDevelopment.ExperimentalEffort(hc,k,s.Years,ca.SupportedCities);
                double research=effort*(.7+.6*HistoryRoll("aggregate-research-"+k,id,s.Years));
                int increment=Math.Max(1,(int)research);
                if(ProvincialHistory(s)){increment=(int)research;if(HistoryRoll("provincial-research-fraction-"+k,id,s.Years)<research-increment)increment++;}
                hc.Research[k]+=increment;
                if(hc.Research[k]>=(CommunityHistory(s)?HistoricalCommunityTechnology.Cost(k):ProvincialHistory(s)?HistoricalTechnologyDevelopment.Cost(k):ConnectedHistory(s)?HistoricalDevelopmentEffects.ResearchCost(k):120+k*30)){hc.Technology[k]=s.Years+1;HistoryEvent(s,"technology",c.Name+(learned?" adopts ":" develops ")+HistoricalTechnologies.Name("history-16",k)+(ConnectedHistory(s)&&learned?" through exchange with "+s.Society.Cultures[teacher].Name:"")+".",cell,id,category:ChronicleCategory.Discoveries);}
            }
            if(a.FirstMaximumTechnologyYear<0&&hc.Technology.All(y=>y>0))a.FirstMaximumTechnologyYear=s.Years;
            if(LivingHistory(s))TryLivingTownOpportunity(s,id,elapsed);
            else if(ca.Cities.Count==0&&c.Farming>=100&&hc.Sedentism>350&&s.Population[CellPatch[cell]]>=1200&&s.Years-c.FoundedYear>=1000&&crop>.2&&HistoryRoll("aggregate-town-origin",id,s.Years)<.08)FoundAggregateTown(s,id,cell,-1);
            else if(target>=0&&HistoricalCells.Culture(s,target)==id&&a.TownAt[target]<0&&ca.Cities.Count>0&&c.Farming>=200&&HistoricalStorageOpportunity(target)>.2&&AggregateCropSupport(s,id,target,false)>.2&&s.Population[CellPatch[target]]>=500&&HistoryRoll("aggregate-town-diffusion",id,s.Years)<.04)FoundAggregateTown(s,id,target,ca.Cities.At((int)(HistoryRoll("aggregate-town-parent",id,s.Years)*ca.Cities.Count)));
            else if(ca.Cities.Count>0&&c.Farming>=200&&HistoryRoll("aggregate-town-catchment",id,s.Years)<.06)
            {
                int parent=ca.Cities.At((int)(HistoryRoll("aggregate-town-catchment-parent",id,s.Years)*ca.Cities.Count));var candidates=World.Surface.Cells[s.Society.Settlements[parent].Cell].Neighbors;int offset=(int)(HistoryRoll("aggregate-town-catchment-edge",id,s.Years)*candidates.Length);
                for(int k=0;k<candidates.Length;k++){int site=candidates[(offset+k)%candidates.Length];if(CellPatch[site]>=0&&HistoricalCells.Culture(s,site)==id&&a.TownAt[site]<0&&s.Population[CellPatch[site]]>=500&&AggregateCropSupport(s,id,site,false)>.2){FoundAggregateTown(s,id,site,parent);break;}}
            }
            c.Stage=ca.CivilizationSince>=0?SocietyStage.Civilization:ca.Cities.Count>0?SocietyStage.EmbryonicCivilization:SocietyStage.AdvancedCulture;
            if(!s.Society.ManualStep)s.Society.SelectedStepYears=h.Polities.Length>0?1:SocietyState.DefaultStep(c.Stage);
            // A local community can adopt a portable religion through an actual
            // contact; political conquest does not overwrite local culture.
            if(contact>=0&&h.Religions[h.Cultures[contact].ReligionId].Reach!=ReligiousReach.Local&&HistoryRoll("aggregate-faith",id,s.Years)<.03){hc.ReligionId=h.Cultures[contact].ReligionId;SetAggregateLayer(s,cell,2,hc.ReligionId);}
            AdvanceAggregateHusbandry(s,id,contact,elapsed);AdvanceAggregateFaith(s,id,contact,elapsed);
        }
        bool AggregatePracticeSupported(PrehistoryState s,int id,int k,bool contact)
            =>AggregatePracticeWithMaritime(s,id,k,contact,CommunityHistory(s)&&CommunityMaritimePractice(s,id));
        bool AggregatePracticeWithMaritime(PrehistoryState s,int id,int k,bool contact,bool maritime)
        {
            var c=s.Society.Cultures[id];var hc=s.History.Cultures[id];var a=s.History.Aggregates.Cultures[id];bool water=c.Fishing>=200||riverSites[c.OriginCell]>.2;
            water|=maritime;
            if(TechnicalHistory(s)&&(k==(int)HistoricalTechnology.Navigation||k==(int)HistoricalTechnology.OceanNavigation))
            {
                int age=s.Years-c.FoundedYear;
                if(k==(int)HistoricalTechnology.Navigation)return water&&((a.SupportedUrban>=1200&&age>=500)||(c.Fishing>=300&&age>=2500));
                // A sustained craft/provisioning tradition can support ocean
                // navigation without a literate state or a steel industry.
                int provisions=LivingWorldHistory(s)?HistoricalForagerMaritime.Provisioning(hc.Surplus,ForagerMaritimeSupport(s,id,maritime)):hc.Surplus;
                return water&&provisions>=200&&((a.SupportedUrban>=1500&&age>=1500)||(c.Fishing>=300&&c.Population>=5000&&age>=4000));
            }
            if(ConnectedHistory(s)&&!contact&&DevelopmentResearchNetwork(s,id,k,water))return true;
            if(ConnectedHistory(s)&&contact&&k>=7&&k!=(int)HistoricalTechnology.Navigation&&a.CivilizationSince>=0&&a.SupportedCities>=1&&a.SupportedUrban>=1200&&hc.Institution>=400&&hc.Surplus>=150)
            {
                // Importing/operating a known practice does not require locally
                // recreating the metropolitan research network that invented it.
                if(k==(int)HistoricalTechnology.OceanNavigation)return water||c.Fishing>=150;
                if(k==(int)HistoricalTechnology.CropRotation||k==(int)HistoricalTechnology.ArtificialFertilizers||k==(int)HistoricalTechnology.MechanizedAgriculture)return c.Farming>=250||AgronomicHistory(s)&&AgronomicResearchSupported(s,id);
                return true;
            }
            if(LivingHistory(s))
            {
                if(a.Cities.Count>0){int town=a.Cities.At((int)(HistoryRoll("living-maritime-practice",id,s.Years)*a.Cities.Count));int site=s.Society.Settlements[town].Cell;water|=riverSites[site]>.2||World.Surface.Cells[site].Neighbors.Any(n=>World.Hydrology.OceanConnected[n]);}
                if(k==(int)HistoricalTechnology.Cultivation)return HistoricalLocalEffects.AgricultureOpen(s.Years)&&s.History.Agriculture[id].AdoptedYear.Any(y=>y>0);
                if(k>=7&&k!=(int)HistoricalTechnology.Navigation&&a.CivilizationSince<0)return false;
                if(k==(int)HistoricalTechnology.Navigation)return water&&((a.SupportedUrban>=1200&&s.Years-c.FoundedYear>=500)||(c.Fishing>=300&&s.Years-c.FoundedYear>=2500));
                if(k==(int)HistoricalTechnology.Writing)return a.SupportedUrban>=2500&&a.CivilizationSince>=0&&hc.Institution>=400;
                if(k>=15&&a.SupportedCities<2)return false;
            }
            if(k>=15){int centers=k>=27?4:k>=20?3:2;long people=k>=27?100000:k>=20?50000:15000;if(LivingHistory(s)&&contact){centers=2;people=people*2/5;}return a.SupportedCities>=centers&&a.SupportedUrban>=people&&hc.Institution>=550&&hc.Surplus>=350&&((HistoricalTechnology)k!=HistoricalTechnology.OceanNavigation||water)&&((HistoricalTechnology)k!=HistoricalTechnology.CropRotation&&(HistoricalTechnology)k!=HistoricalTechnology.ArtificialFertilizers&&(HistoricalTechnology)k!=HistoricalTechnology.MechanizedAgriculture||AgronomicResearchSupported(s,id));}
            switch((HistoricalTechnology)k)
            {
                case HistoricalTechnology.Cultivation:return c.Farming>0;
                case HistoricalTechnology.Pottery:return hc.Sedentism>=220;
                case HistoricalTechnology.Sailing:return water;
                case HistoricalTechnology.Irrigation:return c.Farming>=150&&hc.Institution>=180;
                case HistoricalTechnology.Copper:return a.SupportedUrban>=350&&hc.Surplus>=120;
                case HistoricalTechnology.Writing:return a.LargestCity>=2000&&a.SupportedUrban>=3000&&hc.Institution>=450&&hc.Surplus>=350&&(contact||a.CityCount>0&&s.Society.Settlements[a.Cities.At(0)].PolityId>=0);
                case HistoricalTechnology.Bronze:return a.SupportedUrban>=1500&&hc.Institution>=250;
                case HistoricalTechnology.Roads:return (a.CityCount>=2||a.LargestCity>=5000)&&hc.Institution>=350;
                case HistoricalTechnology.Iron:return a.SupportedUrban>=2500&&hc.Institution>=400;
                case HistoricalTechnology.Coinage:return a.SupportedUrban>=5000&&hc.Institution>=450;
                case HistoricalTechnology.Mills:return a.SupportedUrban>=1200&&c.Farming>=200;
                case HistoricalTechnology.Steel:return a.SupportedUrban>=4000&&hc.Surplus>=350;
                case HistoricalTechnology.Navigation:return water&&a.SupportedUrban>=1200&&s.Years-c.FoundedYear>=500;
                case HistoricalTechnology.Scholasticism:return a.SupportedUrban>=8000&&hc.Institution>=500;
                default:return true;
            }
        }
        HistoricalCultivar AggregateCultivar(PrehistoryState s,int culture,int group,int site=-1)
        {
            int cell=site>=0?site:s.Society.Cultures[culture].OriginCell;var habitat=World.Ecology.Habitat[cell];double latitude=Math.Abs(Math.Asin(World.Surface.Cells[cell].Position.Z)*180/Math.PI);HistoricalCultivar best=null;double score=0;
            if(CommunityHistory(s))habitat=CommunityAgronomicHabitat(s,cell,group);
            for(int k=0;k<HistoricalCropCatalog.Crops.Length;k++)
            {var crop=HistoricalCropCatalog.Crops[k];if(crop.Group!=group)continue;var cultivar=new HistoricalCultivar{CropId=k,OriginCulture=culture,MaturityDays=crop.MaturityDays,MinimumWarmTenthsC=crop.MinimumWarmC*10,DaylengthLatitudeTenths=(int)(latitude*10),WaterRequirementPermille=crop.MinimumWaterPermille};double fit=PassageHistory(s)?CommunityTransferFit(s,cell,cultivar):HistoricalCropCatalog.Fit(habitat,cultivar,latitude,riverSites[cell]);if(fit>score){score=fit;best=cultivar;}}
            return best;
        }
        void FoundAggregateTown(PrehistoryState s,int culture,int cell,int parent)
        {
            if(LivingHistory(s)){FoundLivingTown(s,culture,cell,parent);return;}
            var h=s.History;var a=h.Aggregates;int patch=CellPatch[cell],id=s.Society.Settlements.Length;if(a.TownAt[cell]>=0)return;long founders=Math.Min(300,s.Population[patch]/5);if(founders<100)return;
            s.Population[patch]-=founders;var town=new Settlement{Id=id,Cell=cell,CultureId=culture,PolityId=-1,FoundedYear=s.Years,Name=HistoricalName("town",unchecked(cell+id*7919)),Population=founders,FoodCapacity=1500,FoodReserve=founders,CultivatedPermille=s.Society.Cultures[culture].Farming};
            var city=new HistoricalCityAggregate{Id=id,LastYear=s.Years,LastHarvestYear=s.Years,LastPoliticalYear=s.Years,ParentCity=parent,BaseUrbanCapacity=1500,BaseFarming=s.Society.Cultures[culture].Farming,FoodYieldMilli=(int)(agriculturalYield[cell]*1000),StoredYieldMilli=(int)(storedYield[cell]*1000),FloodMilli=(int)(riverAccess[cell]*1000),SoilMilli=World.Ecology.Habitat[cell].SoilFertilityPermille,RuralCapacity=Math.Max(100,ruralWeight[cell]/1000),Neighbors=parent<0?Array.Empty<int>():a.Cities[parent].Neighbors.Concat(new[]{parent}).Distinct().Take(12).ToArray()};
            city.Rural=Math.Min(s.Population[patch]/10,Math.Max(200,city.RuralCapacity));s.Population[patch]-=city.Rural;a.Regions[patch].Capacity=Math.Max(0,a.Regions[patch].Capacity-city.RuralCapacity);city.Supported=true;city.BaseCropFactor=.4+.6*AggregateCropSupport(s,culture,cell,false);
            s.Society.Settlements=s.Society.Settlements.Concat(new[]{town}).ToArray();h.Towns=h.Towns.Concat(new[]{new HistoricalTown{Id=id,FoodBase=1500}}).ToArray();a.Cities=a.Cities.Concat(new[]{city}).ToArray();a.TownAt=a.TownAt.With(cell,id);
            var ca=a.Cultures[culture];ca.Cities.Add(id);ca.CityCount=ca.Cities.Count;ca.Urban+=founders;ca.Rural+=city.Rural;ca.SupportedUrban+=founders;ca.SupportedCities++;ca.FoodCapacity+=town.FoodCapacity;ca.LargestCity=Math.Max(ca.LargestCity,founders);a.TotalUrban+=founders;
            if(parent>=0){var p=a.Cities[parent];p.Neighbors=p.Neighbors.Concat(new[]{id}).Distinct().Take(12).ToArray();}
            SetAggregateLayer(s,cell,3,id);SetAggregateLayer(s,cell,4,2);ScheduleAggregate(s,CityProcess,id);
            int host=HistoricalCells.Polity(s,cell);if(host>=0){AssignAggregateTown(s,id,host);SetAggregateLayer(s,cell,4,3);}
            HistoryEvent(s,"settlement",town.Name+" is founded with "+founders+" residents and a local supply catchment.",cell,culture,town:id,category:ChronicleCategory.Politics);
        }
    }
}
