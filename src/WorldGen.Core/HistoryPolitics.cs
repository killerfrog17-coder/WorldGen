using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void AdvanceHistoricalYear(PrehistoryState s,CancellationToken token)
        {
            var h=s.History;var towns=s.Society.Settlements;
            if(s.Years%10==0||h.CellMarket.Length==0)using(ProfileWork("annual markets",towns.Length))HistoricalMarkets(s,token);
            // Read-only annual relation indices. Outbreak flags update in the same
            // town order as the former nested Any queries, preserving within-year exposure.
            int polityCount=h.Polities.Length;var wars=new bool[polityCount];var tradeLinks=new System.Collections.Generic.List<int>[polityCount];var infected=new bool[polityCount];
            foreach(var relation in h.Relations){if(relation.WarSince>=0){wars[relation.A]=true;wars[relation.B]=true;}if(relation.Trade){(tradeLinks[relation.A]??(tradeLinks[relation.A]=new System.Collections.Generic.List<int>())).Add(relation.B);(tradeLinks[relation.B]??(tradeLinks[relation.B]=new System.Collections.Generic.List<int>())).Add(relation.A);}}
            foreach(var town in towns)if(town.PolityId>=0&&h.Towns[town.Id].DiseaseUntil>=s.Years)infected[town.PolityId]=true;
            using var annualFoodTimer=ProfileWork("annual complete",towns.Length);
            HistoricalFoodYear annualFood;
            using(ProfileWork("annual food",towns.Length))annualFood=ExperimentalHistory(s)?HistoricalAnnualFood(s,wars,token):null;
            var diseaseSources=ExperimentalHistory(s)?DiseaseSources(s):null;
            var townNetwork=ExperimentalHistory(s)?HistoricalTownContactGraph(s,token):null;
            UrbanMigration[] migration;using(ProfileWork("annual migration planning",towns.Length))migration=DelegatedHistory(s)?PlanLocalUrbanMigration(s,token):null;
            using(ProfileWork("annual town lifecycle",towns.Length))foreach(var t in towns)
            {
                var hc=h.Cultures[t.CultureId];var ht=h.Towns[t.Id];var culture=s.Society.Cultures[t.CultureId];int patch=CellPatch[t.Cell];
                double multiplier=1+(HistoricalTechnologies.Has(hc,HistoricalTechnology.Irrigation)?.3:0)+(HistoricalTechnologies.Has(hc,HistoricalTechnology.Iron)?.2:0)+(HistoricalTechnologies.Has(hc,HistoricalTechnology.Mills)?.35:0);
                if(ModernHistory(s))multiplier*=HistoricalModernEffects.FoodMultiplier(hc,s.Years);
                t.FoodCapacity=Math.Max(RegionalHistory(s)?0:300,(long)(ht.FoodBase*multiplier));
                double weather=.70+.5*HistoryRoll("regional-harvest",patch,s.Years/5)+.1*HistoryRoll("local-harvest",t.Id,s.Years);
                bool indexed=t.PolityId>=0&&t.PolityId<polityCount;
                bool atWar=indexed&&wars[t.PolityId];if(atWar)weather*=.90;
                bool trade=indexed&&tradeLinks[t.PolityId]!=null;if(trade&&!ExperimentalHistory(s))weather+=.06;
                if(t.PolityId>=0&&HistoricalTechnologies.Has(hc,HistoricalTechnology.Iron)&&h.Polities[t.PolityId].Treasury>1000)ht.Walls=Math.Min(1000,ht.Walls+1);
                long food=annualFood==null?(long)(t.FoodCapacity*weather):annualFood.Harvest[t.Id];long available=food+t.FoodReserve;double fed=D.Clamp(available/(double)Math.Max(1,t.Population));
                t.FoodReserve=RegionalHistory(s)?HistoricalFoodEffects.RemainingReserve(t.FoodReserve,t.FoodCapacity,food,t.Population):Math.Max(0,Math.Min(t.FoodCapacity*3,available-t.Population));
                double pressure=RegionalHistory(s)?HistoricalFoodEffects.GrowthPressure(t.Population,t.FoodCapacity,annualFood?.Received[t.Id]??0):1-t.Population/(double)t.FoodCapacity;
                double growth=t.Population*.012*pressure-t.Population*(1-fed)*.08;
                if(DelegatedHistory(s))
                {
                    var demographic=ht.Demography??(ht.Demography=new HistoricalUrbanDemography());demographic.Year=s.Years;demographic.NaturalChange=demographic.RuralInflow=demographic.RuralOutflow=0;
                    int works=t.PolityId<0?0:h.Dynamics.States[t.PolityId].PublicWorks;
                    demographic.ChronicLoss=(long)Math.Floor(D.Q(t.Population*HistoricalUrbanEffects.ChronicLossRate(t.Population,works,DiseaseHabitat(t.Cell,HistoricalDiseaseKind.VectorFever)),1000000));
                    if(ModernHistory(s))demographic.ChronicLoss=(long)Math.Floor(D.Q(demographic.ChronicLoss*HistoricalModernEffects.ChronicMortalityMultiplier(hc,s.Years),1000000));
                    demographic.TotalChronicLoss+=demographic.ChronicLoss;growth-=demographic.ChronicLoss;
                }
                if(ExperimentalHistory(s))AdvanceHistoricalDisease(s,t,trade,diseaseSources,townNetwork,annualFood,token);
                else if(s.Years>ht.ImmunityUntil&&ht.DiseaseUntil<s.Years&&t.Population>1500)
                {
                    bool exposed=trade&&tradeLinks[t.PolityId].Any(other=>infected[other]);
                    double risk=exposed?.10:.0007*Math.Min(8,t.Population/3000.0)*(trade?1.8:1);
                    if(HistoryRoll("outbreak",t.Id,s.Years)<risk){ht.DiseaseUntil=s.Years+3;ht.ImmunityUntil=s.Years+25;if(indexed)infected[t.PolityId]=true;HistoryEvent(s,"epidemic","An epidemic reaches "+t.Name+", disrupting households and trade.",t.Cell,t.CultureId,t.PolityId,town:t.Id);}
                }
                if(ht.DiseaseUntil>=s.Years)
                {
                    if(ExperimentalHistory(s)){long deaths=HistoricalDiseaseEffects.AnnualDeaths(t.Population,ht.DiseaseMortalityPermille,fed);if(ModernHistory(s))deaths=(long)Math.Floor(D.Q(deaths*HistoricalModernEffects.MortalityMultiplier(hc,s.Years,ht.DiseaseKind),1000000));ht.DiseaseDeaths+=deaths;growth-=deaths;}
                    else growth-=t.Population*.025;
                }
                if(ExperimentalHistory(s)&&fed<.85){ht.ShortageYears++;if(ht.ShortageYears==1||s.Years-ht.LastHarvestHazardYear>10&&ht.ShortageYears%10==0)HistoryEvent(s,"food-shortage","Food shortfalls at "+t.Name+" draw down reserves and drive households toward other livelihoods.",t.Cell,t.CultureId,t.PolityId,town:t.Id);}
                long delta=(long)Math.Floor(growth);if(HistoryRoll("town-growth",t.Id,s.Years)<growth-delta)delta++;
                if(DelegatedHistory(s))ht.Demography.NaturalChange=Math.Max(-t.Population,delta);
                long priorPopulation=t.Population;t.Population=Math.Max(0,t.Population+delta);
                if(RegionalHistory(s)&&priorPopulation>0&&t.Population==0)HistoryEvent(s,"settlement-abandoned",t.Name+" loses its last resident households after sustained demographic or provisioning pressure. Its historical record remains, but no living settlement occupies the site.",t.Cell,t.CultureId,t.PolityId,town:t.Id);
                if(!DelegatedHistory(s)&&s.Years%5==0&&t.Population<t.FoodCapacity*.8&&fed>.95){long moved=Math.Min(150,s.Population[patch]/100);s.Population[patch]-=moved;t.Population+=moved;}
                if(fed<.85&&t.Population>100){long fleeing=t.Population/100;t.Population-=fleeing;s.Population[patch]+=fleeing;if(DelegatedHistory(s)){ht.Demography.RuralOutflow=fleeing;ht.Demography.TotalRuralOutflow+=fleeing;}}
                t.Health=(int)(800*fed)-(ht.DiseaseUntil>=s.Years?250:0);t.Stability=(int)(750*fed)-(atWar?100:0);t.CultivatedPermille=culture.Farming;t.CraftSharePermille=Math.Min(350,50+hc.Technology.Count(x=>x>0)*15);
                if(RegionalHistory(s))
                {
                    bool Example(Settlement other)=>other.PolityId>=0&&h.Polities[other.PolityId].EndedYear<0&&s.Years-h.Polities[other.PolityId].FoundedYear>=50&&other.Population>=1000&&other.FoodCapacity>=other.Population*.8;
                    if(EmergenceHistory(s))LearnLocalInstitutions(s,t,townNetwork[t.Id]);
                    else ht.CivicExampleExposure=Example(t)?1000:Math.Min(1000,townNetwork[t.Id].Count(id=>Example(towns[id]))*350);
                }
                if(DiffusionHistory(s))
                {
                    int previous=ht.CivicPreparationYears,prepared=HistoricalFormationEffects.Preparation(previous,t.Population,t.FoodCapacity,t.FoodReserve,t.Stability,hc.Institution,LocalStoredSupport(s,t.CultureId,t.Cell));
                    if(LocalHistory(s)&&(StateFoundationReadiness(s,t)<.18||LocalFiscalPotential(s,t.CultureId,t.Cell)<.22))prepared=Math.Max(0,previous-2);
                    if(RegionalHistory(s)&&prepared>previous&&HistoryRoll("administrative-practice-9",t.Id,s.Years)>=LocalAdministrativeOpportunity(s,t))prepared=previous;
                    ht.CivicPreparationYears=prepared;
                }
                bool supportedFoundation=DiffusionHistory(s)?ht.CivicPreparationYears>=150&&LocalStoredSupport(s,t.CultureId,t.Cell)>=.23:hc.Surplus>=350;
                if(t.PolityId<0&&t.Population>=(TerritorialHistory(s)?5000:2500)&&culture.Organization>=450&&supportedFoundation&&t.FoodReserve>=t.Population*(TerritorialHistory(s)?1.5:1)&&s.Years-t.FoundedYear>=(TerritorialHistory(s)?250:150))
                    if((!LocalHistory(s)||LocalFiscalPotential(s,t.CultureId,t.Cell)>=.22)&&HistoryRoll("state",t.Id,s.Years)<(LocalHistory(s)?HistoricalLocalEffects.FoundationChance(StateFoundationReadiness(s,t))*(EmergenceHistory(s)?StateOriginMultiplier(s,t):1):CivicHistory(s)?HistoricalInstitutionRequirements.FormationChance(StateFoundationReadiness(s,t)):.006))FoundHistoricalPolity(s,t,-1);
            }
            if(migration!=null)using(ProfileWork("annual migration application"))ApplyLocalUrbanMigration(s,migration);
            var citiesByPolity=towns.Where(t=>t.PolityId>=0&&t.Population>=100).GroupBy(t=>t.PolityId).ToDictionary(g=>g.Key,g=>g.ToArray());
            AdministrativeNetwork administration=null;
            if(LocalHistory(s)&&h.Polities.Length>0)using(ProfileWork("annual administration",h.Polities.Length)){administration=BuildAdministrativeNetwork(s,token);(h.Administration??(h.Administration=new HistoricalAdministration())).Accounts=administration.Accounts;}
            foreach(var p in h.Polities.Where(p=>p.EndedYear<0))
            {
                var cities=citiesByPolity.TryGetValue(p.Id,out var group)?group:Array.Empty<Settlement>();
                if(cities.Length==0){EndPolity(s,p,"Its last urban institution dissolves.");continue;}
                if(towns[p.CapitalId].PolityId!=p.Id||towns[p.CapitalId].Population<100)
                {
                    string formerCapital=towns[p.CapitalId].Name;p.CapitalId=cities.OrderByDescending(t=>t.Population).ThenBy(t=>t.Id).First().Id;
                    if(DelegatedHistory(s)){HistoryEvent(s,"capital-moved",p.Name+" relocates its court from the declining or lost center of "+formerCapital+" to "+towns[p.CapitalId].Name+".",towns[p.CapitalId].Cell,p.CultureId,p.Id,town:p.CapitalId,category:ChronicleCategory.Politics);administration=BuildAdministrativeNetwork(s,token);h.Administration.Accounts=administration.Accounts;}
                }
                long urban=cities.Sum(t=>t.Population);bool war=h.Relations.Any(r=>r.WarSince>=0&&(r.A==p.Id||r.B==p.Id));
                if(CivicHistory(s)){using(ProfileWork("annual state budgets"))AdvanceCivicBudget(s,p,cities,war,token,administration);continue;}
                var polityKnowledge=h.Cultures[p.CultureId];long tax=urban/(HistoricalTechnologies.Has(polityKnowledge,HistoricalTechnology.Coinage)?16:20),maintenance=p.Army/2+Math.Max(0,p.AreaKm2/500);p.Treasury=Math.Max(0,Math.Min(1000000000,p.Treasury+tax-maintenance));
                int target=(int)Math.Min(200000,urban*(war?.07:.025));p.Army=Math.Max(0,p.Army+Math.Sign(target-p.Army)*Math.Min(50,Math.Abs(target-p.Army)));
                p.Exhaustion=Math.Max(0,Math.Min(1000,p.Exhaustion+(war?3:-5)));
                int cohesionTarget=Math.Max(100,800-(int)Math.Min(500,p.AreaKm2/4000)-p.Exhaustion/2+(p.Treasury>0?0:-200));p.Cohesion+=Math.Sign(cohesionTarget-p.Cohesion);
                p.Legitimacy=Math.Max(0,Math.Min(1000,p.Legitimacy+(cities.Average(t=>t.Stability)>600?1:-3)));
                if(s.Years-p.RulerSince>=20&&HistoryRoll("succession",p.Id,s.Years)<.035){p.RulerSince=s.Years;p.Ruler=RulerName(p.Id,s.Years);p.Legitimacy=Math.Max(100,p.Legitimacy-120);HistoryEvent(s,"succession",p.Ruler+" takes office in "+p.Name+".",towns[p.CapitalId].Cell,p.CultureId,p.Id,category:ChronicleCategory.Politics);}
                if(s.Years%10==0&&cities.Length>1&&p.Cohesion<450&&HistoryRoll("secession",p.Id,s.Years)<.08)
                {
                    var city=cities.Where(t=>t.Id!=p.CapitalId).OrderByDescending(t=>D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[towns[p.CapitalId].Cell].Position)).ThenBy(t=>t.Id).First();
                    FoundHistoricalPolity(s,city,p.Id);
                }
            }
            if(ExperimentalHistory(s)&&s.Years%10==0)AdvanceHistoricalDependencies(s,token);
            if(TerritorialHistory(s)&&s.Years%10==0)using(ProfileWork("decadal autonomy"))AdvanceProvincialAutonomy(s,token);
            if(LocalHistory(s))using(ProfileWork("annual offices")){AdvanceIncorporations(s,token);AdvanceDistrictOffices(s,token);}
            if(CivicHistory(s))using(ProfileWork("annual campaigns"))AdvanceCivicCampaigns(s,token);
            if(s.Years%10==0)using(ProfileWork("decadal politics")){HistoricalPolitics(s,token);RefreshHistoricalStages(s);if(!s.Society.ManualStep)s.Society.SelectedStepYears=AutomaticYears(s);}
            if(s.Years%50==0)using(ProfileWork("religion")){if(StochasticCalendar(s))RefreshSocietyTotals(s);HistoricalReligion(s,token);}
        }
        string RulerName(int id,int year)
        {
            string[] a={"Aren","Sera","Tal","Neri","Oma","Kes","Ilen","Vara","Daru","Meli"};return a[(int)(HistoryRoll("ruler",id,year)*a.Length)]+" of the "+new[]{"River","Cedar","Falcon","Reed","Stone","Dawn"}[(int)(HistoryRoll("house",id,year/100)*6)]+" House";
        }
        void FoundHistoricalPolity(PrehistoryState s,Settlement town,int parent,bool successor=false)
        {
            var h=s.History;int id=h.Polities.Length;var p=new HistoricalPolity{Id=id,CultureId=town.CultureId,CapitalId=town.Id,FoundedYear=s.Years,ParentId=parent,RulerSince=s.Years,Ruler=RulerName(id,s.Years),Name=town.Name+" State",ReligionId=h.CellReligion[town.Cell],Treasury=town.Population/2,Army=(int)(town.Population/30)};
            if(successor)p.Name=HistoricalName("successor-state-16",unchecked(town.Cell+id*7919))+" State";
            if(TerritorialHistory(s)&&h.Polities.Any(other=>other.EndedYear<0&&other.Name==p.Name))
            {
                int suffix=0;do{p.Name=HistoricalName("polity-place-"+suffix++,town.Cell)+" "+town.Name+" State";}while(h.Polities.Any(other=>other.EndedYear<0&&other.Name==p.Name));
            }
            if(ExperimentalHistory(s)&&parent>=0)
            {
                var formerCourt=h.Polities[parent];long urban=s.Society.Settlements.Where(t=>t.PolityId==parent).Sum(t=>t.Population);
                p.Treasury=HistoricalSuccessionEffects.DivideFunds(formerCourt.Treasury,town.Population,urban);formerCourt.Treasury-=p.Treasury;
                p.Army=(int)HistoricalSuccessionEffects.DivideFunds(formerCourt.Army,town.Population,urban);formerCourt.Army-=p.Army;
            }
            h.Polities=h.Polities.Concat(new[]{p}).ToArray();int former=town.PolityId;town.PolityId=id;town.Government=p.Government;SetPoliticalOwner(s,town.Cell,id);
            if(DelegatedHistory(s)){p.Population=town.Population+RuralCellPopulation(s,town.Cell);p.AreaKm2=(long)Math.Round(HistoricalLandArea(town.Cell));p.LastEstimateYear=s.Years;}
            if(TerritorialHistory(s)){h.Towns[town.Id].UnrestPolityId=id;h.Towns[town.Id].AutonomyPressure=0;}
            if(CivicHistory(s))InitializeCivicPolity(s,p,town);
            if(parent>=0){if(CivicHistory(s))TransferCivicHinterland(s,town.Id,former,id);else foreach(int i in PolityCells(s,former).ToArray())if(h.CellMarket[i]==town.Id)SetPoliticalOwner(s,i,id);}
            if(ExperimentalHistory(s)&&parent>=0)RefreshSplitPolityTotals(s,parent,id);
            HistoryEvent(s,successor?"regime-change":parent<0?"state-foundation":"secession",successor?p.Name+" replaces the former ruling order of "+h.Polities[parent].Name+" at "+town.Name+"; its civic community continues.":parent<0?"Institutions at "+town.Name+" establish "+p.Name+".":p.Name+" separates from "+h.Polities[parent].Name+".",town.Cell,town.CultureId,id,parent,town.Id,category:ChronicleCategory.Politics);
            if(EmergenceHistory(s)&&parent<0)
            {
                int teacher=h.Towns[town.Id].CivicTeacherPolityId;
                HistoryEvent(s,teacher<0?"independent-institutions":"institutional-diffusion",teacher<0?p.Name+" develops centralized institutions without a recorded neighboring state example.":p.Name+" draws on institutional experience transmitted through local contact with "+h.Polities[teacher].Name+".",town.Cell,town.CultureId,id,teacher,town.Id,category:ChronicleCategory.Society);
            }
        }
        void EndPolity(PrehistoryState s,HistoricalPolity p,string reason)
        {
            if(ExperimentalHistory(s))EndHistoricalDependencies(s,p.Id);
            if(CivicHistory(s)){foreach(var relation in s.History.Relations.Where(r=>r.WarSince>=0&&(r.A==p.Id||r.B==p.Id)))EndCivicCampaign(s,relation,p.Name+" ceases to exist as an independent state.");var leader=CivicLeader(s,p.Id);if(leader.EndedYear<0){leader.EndedYear=s.Years;leader.Legacy="The state ended during this reign. "+reason;}}
            p.EndedYear=s.Years;p.Army=0;foreach(int i in PolityCells(s,p.Id).ToArray())SetPoliticalOwner(s,i,-1);
            foreach(var t in s.Society.Settlements.Where(t=>t.PolityId==p.Id)){t.PolityId=-1;if(TerritorialHistory(s)){s.History.Towns[t.Id].UnrestPolityId=-1;s.History.Towns[t.Id].AutonomyPressure=0;}}
            foreach(var r in s.History.Relations.Where(r=>r.A==p.Id||r.B==p.Id)){r.WarSince=-1;r.Trade=false;r.Alliance=false;}
            HistoryEvent(s,"collapse",p.Name+" fragments. "+reason,s.Society.Settlements[p.CapitalId].Cell,p.CultureId,p.Id,category:ChronicleCategory.Politics);
        }
        int TravelCost(int from,int to,int metres,HistoricalCulture hc)
        {
            double relief=World.Ecology.Habitat[to].ReliefM;
            return Math.Max(1,(int)(metres*(1+relief/900.0)*(HistoricalTechnologies.Has(hc,HistoricalTechnology.Roads)?.65:1)));
        }
        void HistoricalMarkets(PrehistoryState s,CancellationToken token)
        {
            if(RevisedHistory(s)){HistoricalMarketsRevised(s,token);return;}
            var h=s.History;int n=World.Surface.Cells.Length;var distance=Enumerable.Repeat(int.MaxValue,n).ToArray();h.CellMarket=Enumerable.Repeat(-1,n).ToArray();var heap=new Heap();
            foreach(var t in s.Society.Settlements.Where(t=>t.Population>=100)){distance[t.Cell]=0;h.CellMarket[t.Cell]=t.Id;heap.Push(0,t.Cell);h.Towns[t.Id].FoodBase=0;}
            while(heap.Count>0)
            {
                var item=heap.Pop();int i=item.Id;if(item.Key!=distance[i])continue;token.ThrowIfCancellationRequested();int town=h.CellMarket[i];var hc=h.Cultures[s.Society.Settlements[town].CultureId];
                var cell=World.Surface.Cells[i];for(int k=0;k<cell.Neighbors.Length;k++)
                {
                    int j=cell.Neighbors[k];if(CellPatch[j]<0||World.Hydrology.OceanConnected[j]||World.Hydrology.LakeCoverageFraction[j]>=.9)continue;
                    int originPolity=s.Society.Settlements[town].PolityId,otherPolity=h.CellPolity[j];
                    if(originPolity>=0&&otherPolity>=0&&originPolity!=otherPolity&&!h.Relations.Any(r=>r.Trade&&r.WarSince<0&&(r.A==originPolity&&r.B==otherPolity||r.B==originPolity&&r.A==otherPolity)))continue;
                    long d=(long)distance[i]+TravelCost(i,j,cell.DistanceM[k],hc);if(d>(HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing)?240000:180000))continue;
                    if(d<distance[j]||d==distance[j]&&town<h.CellMarket[j]){distance[j]=(int)d;h.CellMarket[j]=town;heap.Push(d,j);}
                }
            }
            for(int i=0;i<n;i++)if(h.CellMarket[i]>=0)
            {
                var t=s.Society.Settlements[h.CellMarket[i]];var c=s.Society.Cultures[t.CultureId];
                double area=Math.Min(8000,World.Surface.Cells[i].AreaM2/1e6)*(1-World.Hydrology.LakeCoverageFraction[i]);
                // Surplus from a disjoint local catchment, never the whole culture's land divided among cities.
                double productive=area*FarmingSuitability(i)*c.Farming/850.0;
                h.Towns[t.Id].FoodBase+=(long)(productive*2.8+area*.25*(c.Fishing/450.0));
            }
        }
        void HistoricalPolitics(PrehistoryState s,CancellationToken token)
        {
            var h=s.History;var towns=s.Society.Settlements;var claims=new SortedDictionary<int,int>();var influence=CivicHistory(s)?CivicInfluence(s,token):null;
            var urbanCells=towns.Where(t=>t.Population>=100).ToDictionary(t=>t.Cell,t=>t);
            var civicSites=CivicHistory(s)?towns.ToDictionary(t=>t.Cell,t=>t):null;
            if(TerritorialHistory(s))WithdrawUnsupportedTerritory(s,influence,token);
            if(LocalHistory(s))PlanSupportedFrontiers(s,influence,token);
            // One frontier per decade, with physical logistical distance, populated targets and no culture-wide annexation.
            foreach(int i in PoliticalCells(s))
            {
                if(LocalHistory(s))break; // Incorporation projects replace free frontier painting.
                if((i&2047)==0)token.ThrowIfCancellationRequested();var p=h.Polities[h.CellPolity[i]];var hc=h.Cultures[p.CultureId];
                foreach(int j in World.Surface.Cells[i].Neighbors)
                {
                    if(h.CellPolity[j]>=0||World.Hydrology.OceanConnected[j]||CellPatch[j]<0||LocalDensity(s,j)<.015||World.Hydrology.LakeCoverageFraction[j]>=.9||urbanCells.ContainsKey(j)&&urbanCells[j].PolityId>=0)continue;
                    int market=h.CellMarket[j];if(CivicHistory(s)){if(influence[j]!=p.Id)continue;}else if(market<0||towns[market].PolityId!=p.Id)continue;
                    double chance=.15*D.Clamp(p.Cohesion/700.0);
                    if(TerritorialHistory(s))
                    {
                        // Incorporation needs a governable surplus, and established
                        // foreign communities do not automatically surrender to a
                        // nearby sphere of administrative reach.
                        double density=LocalDensity(s,j),support=D.Clamp(density/.25);
                        int localCulture=s.Society.CellCulture[j];
                        double resistance=localCulture>=0&&localCulture!=p.CultureId?.35+.65*(1-s.Society.Cultures[localCulture].Organization/1000.0):1;
                        chance*=support*resistance*D.Clamp(p.Legitimacy/650.0)*(p.Treasury>0?1:.25);
                    }
                    if(HistoryPairRoll(s,"frontier",j,p.Id)<chance&&!claims.ContainsKey(j))claims[j]=p.Id;
                }
            }
            foreach(var claim in claims)
            {
                SetPoliticalOwner(s,claim.Key,claim.Value);
                if(civicSites!=null&&civicSites.TryGetValue(claim.Key,out var localTown)&&localTown.PolityId<0)
                {
                    localTown.PolityId=claim.Value;localTown.Government=h.Polities[claim.Value].Government;
                    if(TerritorialHistory(s)){h.Towns[localTown.Id].UnrestPolityId=claim.Value;h.Towns[localTown.Id].AutonomyPressure=0;}
                    if(localTown.Population>=100)HistoryEvent(s,"incorporation","The local councils of "+localTown.Name+" are incorporated into "+h.Polities[claim.Value].Name+"; their cultural identity remains distinct.",localTown.Cell,localTown.CultureId,claim.Value,town:localTown.Id,category:ChronicleCategory.Politics);
                }
            }
            foreach(var p in h.Polities){if(DelegatedHistory(s)&&p.EndedYear>=0)continue;p.Population=0;p.AreaKm2=0;if(DelegatedHistory(s))p.LastEstimateYear=s.Years;}
            bool indexedTotals=AddIndexedPoliticalTotals(s);
            var pairs=new SortedSet<(int,int)>();
            foreach(int i in PoliticalFrontierCells(s)){var p=h.Polities[h.CellPolity[i]];if(!indexedTotals){p.Population+=RuralCellPopulation(s,i);p.AreaKm2+=(long)Math.Round(RevisedHistory(s)?HistoricalLandArea(i):World.Surface.Cells[i].AreaM2/1e6);}foreach(int j in World.Surface.Cells[i].Neighbors){int b=h.CellPolity[j];if(b>=0&&b!=p.Id)pairs.Add((Math.Min(p.Id,b),Math.Max(p.Id,b)));}}
            foreach(var t in towns)if(t.PolityId>=0)h.Polities[t.PolityId].Population+=t.Population;
            // Nearby capitals can trade or raid without their coarse cell borders already touching.
            var active=h.Polities.Where(p=>p.EndedYear<0).ToArray();
            if(CivicHistory(s))CivicPoliticalContacts(s,pairs,token);
            else for(int a=0;a<active.Length;a++)for(int b=a+1;b<active.Length;b++)
            {
                var pa=active[a];var pb=active[b];int ca=towns[pa.CapitalId].Cell,cb=towns[pb.CapitalId].Cell;
                if(D.Angle(World.Surface.Cells[ca].Position,World.Surface.Cells[cb].Position)*World.Recipe.RadiusKm<500&&ReachableHistoricalContact(s,ca,cb,pa.CultureId,token))pairs.Add((pa.Id,pb.Id));
            }
            foreach(var pair in pairs)if(!h.Relations.Any(r=>r.A==pair.Item1&&r.B==pair.Item2))h.Relations=h.Relations.Concat(new[]{new HistoricalRelation{A=pair.Item1,B=pair.Item2}}).ToArray();
            foreach(var r in h.Relations.OrderBy(r=>r.A).ThenBy(r=>r.B))
            {
                var a=h.Polities[r.A];var b=h.Polities[r.B];if(a.EndedYear>=0||b.EndedYear>=0)continue;
                bool accessible=pairs.Contains((r.A,r.B));
                if(r.WarSince>=0)
                {
                    if(accessible&&!CivicHistory(s))HistoricalBattle(s,r,token);
                    if(a.EndedYear>=0||b.EndedYear>=0)continue;
                    if(!accessible||s.Years-r.WarSince>=(CivicHistory(s)?25+Math.Max(CivicLeader(s,a.Id).Ambition,CivicLeader(s,b.Id).Ambition)/20:40)||a.Exhaustion>650||b.Exhaustion>650){if(CivicHistory(s))EndCivicCampaign(s,r,!accessible?"Contact and supply are lost.":"The belligerents accept peace after exhaustion or a prolonged campaign.",accessible);r.WarSince=-1;r.TruceUntil=s.Years+40;HistoryEvent(s,"peace",a.Name+" and "+b.Name+" agree to peace.",towns[a.CapitalId].Cell,a.CultureId,a.Id,b.Id,category:ChronicleCategory.Politics);}
                    continue;
                }
                if(!accessible){r.Trade=false;r.Alliance=false;continue;}
                if(ExperimentalHistory(s)&&HistoricalClientEffects.AreLinked(h.Dynamics.Dependencies,r.A,r.B)){r.Trade=true;r.Alliance=true;r.Trust=Math.Max(35,r.Trust);continue;}
                int affinity=a.CultureId==b.CultureId?15:0;affinity+=a.ReligionId==b.ReligionId?10:0;
                if(ModernHistory(s))affinity-=CivicRivalry(s,a.CapitalId,b.CapitalId)/30;
                if(CivicHistory(s)&&h.Polities.Any(p=>p.EndedYear<0&&p.Id!=a.Id&&p.Id!=b.Id&&p.Army>(a.Army+b.Army)*1.3&&pairs.Contains((Math.Min(p.Id,a.Id),Math.Max(p.Id,a.Id)))&&pairs.Contains((Math.Min(p.Id,b.Id),Math.Max(p.Id,b.Id)))))affinity+=20;
                r.Trust=Math.Max(-100,Math.Min(100,r.Trust+(int)(HistoryPairRoll(s,"diplomacy",r.A,r.B)*17)-8+(r.Trade?2:0)));
                if(CivicHistory(s))r.Trust=Math.Max(-100,Math.Min(100,r.Trust+HistoricalLeadershipEffects.TreatyTrustBonus(CivicLeader(s,a.Id).Diplomacy,CivicLeader(s,b.Id).Diplomacy)));
                if(RevisedHistory(s))
                {
                    bool shortage=towns.Any(t=>(t.PolityId==a.Id||t.PolityId==b.Id)&&t.Population>t.FoodCapacity*.95&&t.FoodReserve<t.Population);
                    if(shortage)r.Trust=Math.Max(-100,r.Trust-4);
                    bool succession=s.Years-a.RulerSince<10||s.Years-b.RulerSince<10;
                    double failure=CivicHistory(s)?HistoricalLeadershipEffects.TreatyFailureChance(CivicLeader(s,a.Id).Diplomacy,CivicLeader(s,b.Id).Diplomacy):.15;
                    if(r.Alliance&&(r.Trust<20||succession&&HistoryPairRoll(s,"treaty-renewal",r.A,r.B)<failure))
                    {r.Alliance=false;r.Trust=Math.Min(r.Trust,35);HistoryEvent(s,"treaty-lapse",a.Name+" and "+b.Name+" do not renew their defensive pact.",towns[a.CapitalId].Cell,a.CultureId,a.Id,b.Id,category:ChronicleCategory.Politics);}
                }
                if(!r.Trade&&r.Trust+affinity>0&&HistoryPairRoll(s,"trade",r.A,r.B)<.3){r.Trade=true;HistoryEvent(s,"trade",a.Name+" opens a trade agreement with "+b.Name+".",towns[a.CapitalId].Cell,a.CultureId,a.Id,b.Id,category:ChronicleCategory.Politics);}
                if(!r.Alliance&&r.Trust>55){r.Alliance=true;HistoryEvent(s,"alliance",a.Name+" and "+b.Name+" swear a defensive alliance.",towns[a.CapitalId].Cell,a.CultureId,a.Id,b.Id,category:ChronicleCategory.Politics);}
                if(ExperimentalHistory(s)&&r.Trust>=0)
                {
                    var patron=a.Army>=b.Army?a:b;var client=patron==a?b:a;
                    bool threatened=h.Relations.Any(x=>x.WarSince>=0&&(x.A==client.Id||x.B==client.Id))||client.Cohesion<450;
                    if(threatened&&patron.Army>client.Army*2&&patron.Exhaustion<400&&HistoryRoll("protection-offer",client.Id,s.Years)<.025*CivicLeader(s,patron.Id).Diplomacy/1000.0&&TryHistoricalDependency(s,patron,client,false))continue;
                }
                if(DelegatedHistory(s)){TryStrategicHistoricalWar(s,r);continue;}
                double pressure=(a.Treasury==0||b.Treasury==0)?.08:.025;
                if(CivicHistory(s))
                {
                    var la=CivicLeader(s,a.Id);var lb=CivicLeader(s,b.Id);pressure=.008+.045*Math.Max(la.Ambition,lb.Ambition)/1000.0;
                    if(a.Treasury==0||b.Treasury==0)pressure*=1.3;
                    if(r.Alliance&&((la.Ambition>=800&&a.Army>b.Army*1.5)||(lb.Ambition>=800&&b.Army>a.Army*1.5))&&HistoryPairRoll(s,"ambitious-treaty",r.A,r.B)<.08){r.Alliance=false;r.Trust-=45;HistoryEvent(s,"treaty-lapse","Expansionist ambitions undermine the pact between "+a.Name+" and "+b.Name+".",towns[a.CapitalId].Cell,a.CultureId,a.Id,b.Id,category:ChronicleCategory.Politics);}
                }
                if(!r.Alliance&&s.Years>=r.TruceUntil&&a.Army>30&&b.Army>30&&HistoryPairRoll(s,"war",r.A,r.B)<pressure*(r.Trade?.3:1))
                {r.WarSince=s.Years;r.Trade=false;r.Trust=-60;if(CivicHistory(s))BeginCivicCampaign(s,r);else HistoryEvent(s,"war",a.Name+" and "+b.Name+" go to war over access to frontier land and tribute.",towns[a.CapitalId].Cell,a.CultureId,a.Id,b.Id,category:ChronicleCategory.Politics);}
            }
            foreach(var p in h.Polities.Where(p=>p.EndedYear<0))
            {
                var cities=towns.Where(t=>t.PolityId==p.Id).ToArray();bool empire=cities.Select(t=>t.CultureId).Distinct().Count()>1&&cities.Length>=3;
                string government=empire?"Tributary empire":cities.Length>=3?"Regional kingdom":"City council";
                if(CivicHistory(s)&&!empire){var ds=h.Dynamics.States[p.Id];var leader=CivicLeader(s,p.Id);government=ds.Tradition=="Port exchange and merchant councils"?(cities.Length>=3?"Maritime league":"Merchant council"):leader.Ambition>=750&&cities.Length>=3?"Expansionist kingdom":ds.Tradition=="Concentrated agrarian administration"?"Agrarian monarchy":ds.Tradition=="Public works and communal storage"?"Storage and irrigation council":government;}
                if(ExperimentalHistory(s))government=h.Dynamics.Dependencies.Any(d=>d.EndedYear<0&&d.PatronId==p.Id)?"Tributary hegemony":h.Dynamics.Dependencies.Any(d=>d.EndedYear<0&&d.ClientId==p.Id)?"Client kingdom":empire?"Territorial empire":government;
                if(p.Government!=government){p.Government=government;HistoryEvent(s,CivicHistory(s)&&empire?"empire-founded":"government",p.Name+" is reorganized as a "+government.ToLowerInvariant()+".",towns[p.CapitalId].Cell,p.CultureId,p.Id,category:ChronicleCategory.Politics);}
                foreach(var t in cities)t.Government=p.Government;
            }
        }
        bool ReachableHistoricalContact(PrehistoryState s,int start,int end,int culture,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(ModernShippingContact(s,start,end,culture))return true;
            var hc=s.History.Cultures[culture];bool civic=CivicHistory(s);
            int movement=RealismHistory(s)?Math.Max(4,(int)Math.Round(HistoricalModernEffects.LandTransport(hc,s.Years)*4)):0;
            var key=(start,end,HistoricalTechnologies.Has(hc,HistoricalTechnology.Roads),HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing),HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation),EmergenceHistory(s)?World.Ecology.Habitat[s.Society.Cultures[culture].OriginCell].AridityMilli<400?2:1:0,movement);
            if(civic&&UseHistoryIndexes)return CachedPhysicalContact(start,end,key.Item3,key.Item4,key.Item5,key.Item6,movement,token);
            var distance=new Dictionary<int,int>{{start,0}};var heap=new Heap();heap.Push(0,start);int limit=EmergenceHistory(s)?HistoricalTechnologies.Has(hc,HistoricalTechnology.Roads)?900000:600000:civic?1200000:600000;
            while(heap.Count>0)
            {
                var x=heap.Pop();if(x.Key!=distance[x.Id])continue;if(x.Id==end)return true;token.ThrowIfCancellationRequested();var c=World.Surface.Cells[x.Id];
                for(int k=0;k<c.Neighbors.Length;k++)
                {
                    int j=c.Neighbors[k];bool sea=World.Hydrology.OceanConnected[j];if(sea&&!HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing))continue;
                    if(sea&&!HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)&&!World.Surface.Cells[j].Neighbors.Any(n=>!World.Hydrology.OceanConnected[n]))continue;
                    long edge=sea?c.DistanceM[k]:TravelCost(x.Id,j,c.DistanceM[k],hc);
                    if(EmergenceHistory(s)&&!sea)edge=(long)Math.Round(edge*HistoricalCropDiffusionEffects.EnvironmentalCost(World.Ecology.Habitat[x.Id],World.Ecology.Habitat[j],key.Item6==2));
                    if(RealismHistory(s)&&!sea)edge=Math.Max(1,(long)Math.Round(edge/(movement/4.0)));
                    long d=x.Key+edge;if(d<=limit&&(!distance.TryGetValue(j,out int old)||d<old)){distance[j]=(int)d;heap.Push(d,j);}
                }
            }
            return false;
        }
        void HistoricalBattle(PrehistoryState s,HistoricalRelation relation,CancellationToken token)
        {
            if(CivicHistory(s)){HistoricalBattleCivic(s,relation,token);return;}
            var h=s.History;var a=h.Polities[relation.A];var b=h.Polities[relation.B];var towns=s.Society.Settlements;
            double Power(HistoricalPolity p){var c=h.Cultures[p.CultureId];double allies=h.Relations.Where(r=>r.Alliance&&(r.A==p.Id||r.B==p.Id)).Sum(r=>h.Polities[r.A==p.Id?r.B:r.A].Army*.15);return(p.Army+allies)*(1+(HistoricalTechnologies.Has(c,HistoricalTechnology.Bronze)?.20:0)+(HistoricalTechnologies.Has(c,HistoricalTechnology.Iron)?.25:0)+(HistoricalTechnologies.Has(c,HistoricalTechnology.Steel)?.25:0))*(.4+.6*p.Cohesion/1000.0);}
            bool attackA=HistoryPairRoll(s,"initiative",a.Id,b.Id)<.5;var attacker=attackA?a:b;var defender=attackA?b:a;
            var target=towns.Where(t=>t.PolityId==defender.Id).OrderBy(t=>D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[towns[attacker.CapitalId].Cell].Position)).ThenBy(t=>t.Id).FirstOrDefault();if(target==null)return;
            var source=towns.Where(t=>t.PolityId==attacker.Id).OrderBy(t=>D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[target.Cell].Position)).ThenBy(t=>t.Id).First();
            if(!ReachableHistoricalContact(s,source.Cell,target.Cell,attacker.CultureId,token))return;
            int targetCell=target.Cell;double defense=1.2+World.Ecology.Habitat[targetCell].ReliefM/2000.0+h.Towns[target.Id].Walls/1000.0;
            bool victory=Power(attacker)*(.7+.6*HistoryPairRoll(s,"battle",attacker.Id,defender.Id))>Power(defender)*defense;
            foreach(var p in new[]{attacker,defender}){int loss=(int)(p.Army*(p==attacker&&!victory?.18:.09));p.Army-=loss;if(RevisedHistory(s))HistoricalCasualties(s,p,loss);else {var home=towns[p.CapitalId];home.Population=Math.Max(0,home.Population-loss);}p.Exhaustion=Math.Min(1000,p.Exhaustion+90);}
            relation.LastBattle=s.Years;
            if(victory)
            {
                target.PolityId=attacker.Id;SetPoliticalOwner(s,targetCell,attacker.Id);target.Stability=Math.Max(0,target.Stability-250);attacker.Legitimacy=Math.Min(1000,attacker.Legitimacy+60);
                foreach(int i in PolityCells(s,defender.Id).ToArray())if(h.CellMarket[i]==target.Id)SetPoliticalOwner(s,i,attacker.Id);
                HistoryEvent(s,"conquest",attacker.Name+" captures "+target.Name+" from "+defender.Name+"; local cultural identity survives.",target.Cell,target.CultureId,attacker.Id,defender.Id,target.Id,category:ChronicleCategory.Politics);
                if(!towns.Any(t=>t.PolityId==defender.Id))EndPolity(s,defender,"Its last city falls to "+attacker.Name+".");
                else if(RevisedHistory(s)&&towns[defender.CapitalId].PolityId!=defender.Id)
                {
                    defender.CapitalId=towns.Where(t=>t.PolityId==defender.Id).OrderByDescending(t=>t.Population).ThenBy(t=>t.Id).First().Id;
                    HistoryEvent(s,"capital-moved",defender.Name+" moves its surviving court to "+towns[defender.CapitalId].Name+".",towns[defender.CapitalId].Cell,defender.CultureId,defender.Id,category:ChronicleCategory.Politics);
                }
            }
            else HistoryEvent(s,"battle",defender.Name+" repels an attack by "+attacker.Name+" near "+target.Name+".",target.Cell,target.CultureId,defender.Id,attacker.Id,target.Id,category:ChronicleCategory.Politics);
        }
        void HistoricalReligion(PrehistoryState s,CancellationToken token=default)
        {
            if(ExperimentalHistory(s)){HistoricalReligionNetworks(s,token);return;}
            var h=s.History;var before=(int[])h.CellReligion.Clone();
            if(RevisedHistory(s))foreach(var relation in h.Relations.Where(r=>r.Trade&&r.WarSince<0).OrderBy(r=>r.A).ThenBy(r=>r.B))
            {
                var a=h.Polities[relation.A];var b=h.Polities[relation.B];if(a.EndedYear>=0||b.EndedYear>=0)continue;
                int ca=s.Society.Settlements[a.CapitalId].Cell,cb=s.Society.Settlements[b.CapitalId].Cell;
                int source=HistoryPairRoll(s,"religious-travel-direction",a.Id,b.Id)<.5?ca:cb,target=source==ca?cb:ca;
                if(before[source]>=0&&before[source]!=before[target]&&HistoryPairRoll(s,"religious-travel",a.Id,b.Id)<.025)
                {h.CellReligion[target]=before[source];HistoryEvent(s,"religious-contact","Travelers and merchants establish "+h.Religions[before[source]].Name+" at "+s.Society.Settlements[(target==ca?a:b).CapitalId].Name+".",target,polity:(target==ca?a:b).Id,religion:before[source]);}
            }
            for(int i=0;i<before.Length;i++)if(s.Society.CellCulture[i]>=0)
            {
                int p=h.CellPolity[i];if(p>=0&&HistoryRoll("patronage",i,s.Years)<.06)h.CellReligion[i]=h.Polities[p].ReligionId;
                else foreach(int j in World.Surface.Cells[i].Neighbors)if(before[j]>=0&&before[j]!=before[i]&&HistoryPairRoll(s,"belief-contact",i,j)<.015){h.CellReligion[i]=before[j];break;}
            }
            foreach(var town in s.Society.Settlements.Where(t=>t.Population>5000))
            {
                var hc=h.Cultures[town.CultureId];if(!HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing)||HistoryRoll("reform",town.Id,s.Years)>.002)continue;
                int parent=h.CellReligion[town.Cell];if(parent<0)continue;int id=h.Religions.Length;var r=h.Religions[parent].Copy();r.Id=id;r.ParentId=parent;r.CultureId=town.CultureId;r.FoundedYear=s.Years;r.HolyCell=town.Cell;r.Name="Way of "+town.Name+" "+(id+1);r.Tradition="Reformed teaching of "+h.Religions[parent].Name;
                h.Religions=h.Religions.Concat(new[]{r}).ToArray();h.CellReligion[town.Cell]=id;if(town.PolityId>=0)h.Polities[town.PolityId].ReligionId=id;
                HistoryEvent(s,"religious-reform",r.Name+" emerges at "+town.Name+", retaining and reinterpreting earlier traditions.",town.Cell,town.CultureId,town.PolityId,town:town.Id,religion:id);
            }
        }
    }
}
