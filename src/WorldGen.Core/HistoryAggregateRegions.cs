using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void AdvanceAggregateRegion(PrehistoryState s,int id)
        {
            var a=s.History.Aggregates;var r=a.Regions[id];int elapsed=s.Years-r.LastYear;if(elapsed<=0)return;r.LastYear=s.Years;
            using var knowledgeScope=ProvincialHistory(s)?new ProvincialKnowledgeScope(this,s):default;
            if(LivingHistory(s))AdvanceLivingRegionalExchange(s,id,elapsed);
            if(ConnectedHistory(s))AdvanceDevelopmentVoyage(s,id,elapsed);
            long before=s.Population[id],capacity=0;
            if(!ProvincialHistory(s)||before>0||!UseProvincialCaches)
            {
                double variability=.85+.3*HistoryRoll("aggregate-region-productivity",id,s.Years/400);
                capacity=Math.Max(0,(long)(r.Capacity*variability));
                if(r.Culture>=0){var c=s.Society.Cultures[r.Culture];capacity=(long)(capacity*(1+c.Farming/80.0*r.YieldMilli/1000.0*HistoricalModernEffects.FoodMultiplier(s.History.Cultures[c.Id],s.Years)));}
                if(ProvincialHistory(s)&&r.Culture>=0)capacity=(long)(ProvincialRegionalCapacity(s,id,r.Culture)*variability);
            }
            double rate=.12;
            if(PassageHistory(s))
            {
                double room=D.Clamp(1-before/(double)Math.Max(1,capacity));
                // Colonizing an underused habitat differs from the near-zero
                // net growth of an established, resource-limited population.
                rate=.12+.68*room*room;
            }
            if(LivingHistory(s)&&r.Culture>=0&&s.Society.Cultures[r.Culture].Farming>=180)rate=.45+.35*HistoricalModernEffects.Adoption(s.History.Cultures[r.Culture],HistoricalTechnology.Sanitation,s.Years);
            if(CommunityHistory(s)&&r.Culture>=0&&s.Society.Cultures[r.Culture].Farming>=180)rate=100*CommunityRuralGrowth(s,r.Culture);
            long after=AggregateLogisticPopulation(before,capacity,rate,elapsed/100.0);
            s.Population[id]=after;a.TotalPopulation+=after-before;if(r.Culture>=0)s.Society.Cultures[r.Culture].Population=Math.Max(0,s.Society.Cultures[r.Culture].Population+after-before);
            RecordDistantResidence(s,id);
            if(ProvincialHistory(s))AdvanceProvincialDispersal(s,id,elapsed,capacity);
            if(!ProvincialHistory(s)&&after>=100&&r.Routes.Length>0)
            {
                int routeId=r.Routes[Math.Min(r.Routes.Length-1,(int)(HistoryRoll("aggregate-route",id,s.Years)*r.Routes.Length))];var route=Routes[routeId];int other=route.A==id?route.B:route.A;var destination=a.Regions[other];
                double similarity=Math.Exp(-Math.Abs(r.MeanTenthsC-destination.MeanTenthsC)/180.0-Math.Abs(r.AridityMilli-destination.AridityMilli)/1000.0);
                bool ice=LowSeaLevel(s.Years)&&s.History.Polities.Length==0;double chance=route.Sea?CrossingChance(route.DistanceM,ice&&route.Shallow):.7;
                if(route.Sea&&r.Culture>=0){var hc=s.History.Cultures[r.Culture];if(HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation))chance=Math.Max(chance,.5*Math.Exp(-route.DistanceM/2000000.0));else if(HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing))chance=Math.Max(chance,.2*Math.Exp(-route.DistanceM/300000.0));}
                double pressure=after/(double)Math.Max(1,capacity);chance*=.2+.8*similarity;chance*=.3+.7*D.Clamp(pressure);
                if(destination.Capacity>=25&&HistoryRoll("aggregate-dispersal",id,s.Years)<ElapsedChance(chance,elapsed/100.0)&&s.Years-s.ArrivalYear[id]>(route.DistanceM/1000.0+Patches[id].RadiusKm)*(.8+3*(1-similarity)))
                {
                    long people=Math.Min(after/5,Math.Min(500,Math.Max(25,(long)(after*.02))));people=Math.Min(people,Math.Max(0,destination.Capacity-s.Population[other]));
                    if(people>=25){s.Population[id]-=people;s.Population[other]+=people;if(s.ArrivalYear[other]<0)s.ArrivalYear[other]=s.Years;for(int g=0;g<8;g++)s.Ancestry[other*8+g]=(int)((s.Ancestry[other*8+g]*(s.Population[other]-people)+s.Ancestry[id*8+g]*people)/Math.Max(1,s.Population[other]));
                        if(r.Culture>=0&&destination.Culture<0&&route.CellB>=0&&HistoricalTechnologies.Has(s.History.Cultures[r.Culture],HistoricalTechnology.Sailing)&&similarity>.6&&HistoryRoll("aggregate-cultural-relay",id,s.Years)<.1){int site=route.A==id?route.CellB:route.CellA;if(site>=0&&HistoricalCells.Culture(s,site)<0)SetAggregateLayer(s,site,0,r.Culture);}
                    }
                }
            }
            if(r.Culture<0&&s.Population[id]>=400&&s.ArrivalYear[id]>=0)
            {
                double concentration=Math.Max(r.StoredMilli/1000.0,Math.Min(.7,riverSites[r.BestCell]));double occupation=HistoricalLocalEffects.RegionalTraditionOpportunity(s.Years-s.ArrivalYear[id]);
                double chance=LivingHistory(s)?LivingCommunityChance(s,id,elapsed):.0008*Math.Pow(Math.Max(.01,concentration),2)*occupation*elapsed/100.0;
                if((EnvironmentalHistory(s)?EnvironmentalOriginRoll("culture",id,s.Years):HistoryRoll("aggregate-community",id,s.Years))<chance){int site=LivingHistory(s)?LivingCommunitySite(s,id):r.BestCell;if(site>=0)FoundAggregateCulture(s,id,site);}
            }
            if(s.Population[id]>0)for(int g=0;g<8;g++){int at=id*8+g;double duration=ProvincialHistory(s)?Math.Sqrt(elapsed/100.0):1;int drift=(int)Math.Round((HistoryRoll("aggregate-drift-"+g,id,s.Years)-.5)*50*duration/Math.Sqrt(Math.Max(1,s.Population[id]/100.0)));s.Ancestry[at]=Math.Max(0,Math.Min(10000,s.Ancestry[at]+drift));}
        }
        static long AggregateLogisticPopulation(long population,long capacity,double rate,double elapsed)
        {if(population<=0)return 0;if(capacity<=0)return Math.Max(0,(long)Math.Round(population*Math.Exp(-rate*elapsed)));double result=capacity/(1+(capacity/(double)population-1)*Math.Exp(-rate*elapsed));return Math.Max(0,(long)Math.Round(result));}
        void FoundAggregateCulture(PrehistoryState s,int patch,int cell,int languageParent=-1)
        {
            var h=s.History;var a=h.Aggregates;int id=s.Society.Cultures.Length,religion=h.Religions.Length;var habitat=World.Ecology.Habitat[cell];
            var c=new CultureIdentity{Id=id,Name=HistoricalName("people",unchecked(cell+id*7919)),OriginCell=cell,FoundedYear=s.Years,StageYear=s.Years,Stage=SocietyStage.AdvancedCulture,Fishing=Patches[patch].Lifestyle==ForagerLifestyle.Coastal?400:HistoricalRiverAccess(cell)>.03?300:50,Hunting=300,Gathering=400,Resources="Native plants, seasonal game and local exchange",Subsistence="Seasonal foraging with persistent storage and exchange"};
            c.Gathering=1000-c.Fishing-c.Hunting;s.Society.Cultures=s.Society.Cultures.Concat(new[]{c}).ToArray();
            var hc=new HistoricalCulture{Id=id,ReligionId=religion,LanguageId=id,Sedentism=150,Technology=new int[HistoricalTechnologies.Names.Length],Research=new int[HistoricalTechnologies.Names.Length]};hc.Technology[0]=s.Years+1;h.Cultures=h.Cultures.Concat(new[]{hc}).ToArray();
            if(TechnicalHistory(s))InitializeTechnicalPractice(hc,s.Years);
            h.Religions=h.Religions.Concat(new[]{new HistoricalReligion{Id=religion,CultureId=id,HolyCell=cell,FoundedYear=s.Years,Name="Tradition of "+c.Name,Tradition="Ancestral and place-based tradition",Ethos="Reciprocity and stewardship",Ritual="Seasonal gatherings and shared feasts"}}).ToArray();
            var crops=new HistoricalAgriculture{CultureId=id,NativeMask=nativeCrops[cell],PracticeCell=Enumerable.Repeat(cell,6).ToArray(),WildOriginCell=Enumerable.Range(0,6).Select(k=>(nativeCrops[cell]&(1<<k))!=0?cell:-1).ToArray(),Cultivars=new HistoricalCultivar[6],HusbandryYears=new int[6]};h.Agriculture=h.Agriculture.Concat(new[]{crops}).ToArray();
            if(CommunityHistory(s)){crops.NativeMask=CommunityNativeCropMask(s,cell);for(int k=0;k<6;k++)crops.WildOriginCell[k]=(crops.NativeMask&(1<<k))!=0?cell:-1;}
            a.Cultures=a.Cultures.Concat(new[]{new HistoricalCultureAggregate{Id=id,LastYear=s.Years,Region=patch}}).ToArray();a.Regions[patch].Culture=id;a.Regions[patch].LastCultureYear=s.Years;
            AppendColonialCulture(s,patch);
            if(ProvincialHistory(s))a.Provinces.Exchanges=a.Provinces.Exchanges.Concat(new[]{new HistoricalExchangeMemory()}).ToArray();
            if(LivingHistory(s))
            {
                int teacher=a.Living.Regions[patch].Teacher;
                if(teacher>=0&&teacher<id){hc.ParentCultureId=teacher;if(ProvincialHistory(s))OfferProvincialKnowledge(s,id,teacher,a.Provinces.RegionalKnowledge[patch],a.Living.Regions[patch].LastExchangeYear,false);else LivingCultureContact(s,id,teacher);if(!LanguageHistory(s))HistoryEvent(s,"cultural-exchange",c.Name+" develops a distinct local tradition through exchange with "+s.Society.Cultures[teacher].Name+"; crops and institutions still need local support.",cell,id,category:ChronicleCategory.Society);}
                if(ColonialHistory(s)&&teacher>=0&&s.Years-s.Society.Cultures[teacher].FoundedYear<3000)
                {
                    int from=s.Society.Cultures[teacher].OriginCell;double distance=D.Angle(World.Surface.Cells[from].Position,World.Surface.Cells[cell].Position)*World.Recipe.RadiusKm;
                    double continuity=HistoricalLandscapeEffects.LivelihoodContinuity(World.Ecology.Habitat[from],World.Ecology.Habitat[from],habitat);
                    if(distance<750&&continuity>.6&&HistoryRoll("regional-cultural-lineage",id,s.Years)<.85)hc.LanguageId=h.Cultures[teacher].LanguageId;
                }
            }
            if(LanguageHistory(s)&&languageParent>=0){hc.LanguageId=h.Cultures[languageParent].LanguageId;hc.ParentCultureId=languageParent;}
            AppendLanguage(s,id);
            if(LanguageHistory(s)&&hc.ParentCultureId>=0)HistoryEvent(s,"cultural-exchange",c.Name+" develops a distinct local tradition through exchange with "+s.Society.Cultures[hc.ParentCultureId].Name+"; crops and institutions still need local support.",cell,id,category:ChronicleCategory.Society);
            SetAggregateLayer(s,cell,0,id);SetAggregateLayer(s,cell,2,religion);SetAggregateLayer(s,cell,4,1);
            c.Population=Math.Min(s.Population[patch],RuralCellPopulation(s,cell));ScheduleAggregate(s,CultureProcess,id);
            HistoryEvent(s,"culture","A persistent regional exchange and storage tradition emerges among "+c.Name+".",cell,id,religion:religion);
        }
    }
}
