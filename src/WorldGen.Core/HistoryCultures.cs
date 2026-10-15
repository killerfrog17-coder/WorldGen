using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        double HistoryRoll(string stream,int id,int year)=>CachedHistoricalRoll(stream,id,year);
        double HistoryPairRoll(PrehistoryState s,string stream,int first,int second)=>ModernHistory(s)?HistoryRoll(stream+"-13-actor-"+first,second,s.Years):HistoryRoll(stream,unchecked(first*31+second),s.Years);
        double LocalDensity(PrehistoryState s,int cell)=>RuralCellPopulation(s,cell)/Math.Max(1,World.Surface.Cells[cell].AreaM2/1e6);
        double StorageOpportunity(int cell)
        {
            var a=World.Ecology.Habitat[cell];double fishing=a.Flooding==FloodRegime.RiverSeasonal?.60:0;
            if(World.Surface.Cells[cell].Neighbors.Any(i=>World.Hydrology.OceanConnected[i]))fishing=.55;
            // Resource concentration permits non-farming settled communities too; neither cold nor rainforest is an absolute veto.
            return D.Q(Math.Max(FarmingSuitability(cell),fishing)*D.Clamp((a.GrowingDays+90)/270.0)*D.Clamp(1-a.ReliefM/4000.0),1000000);
        }
        void HistoryEvent(PrehistoryState s,string kind,string text,int cell,int culture=-1,int polity=-1,int other=-1,int town=-1,int religion=-1,ChronicleCategory category=ChronicleCategory.Society)
        {
            var h=s.History;var e=new HistoricalEvent{Id=h.Archive.Count,Year=s.Years,Kind=kind,Text=text,Cell=cell,CultureId=culture,PolityId=polity,OtherPolityId=other,SettlementId=town,ReligionId=religion,Category=category};
            h.Archive.Add(e);
            if(h.Aggregates!=null)h.Aggregates.ArchiveHash=StateHash.Compute(new object[]{h.Aggregates.ArchiveHash,e});
            s.Society.Events=s.Society.Events.Concat(new[]{new ChronicleEvent{Year=e.Year,CultureId=culture,Cell=cell,Category=category,Text=text}}).Skip(Math.Max(0,s.Society.Events.Length+1-256)).ToArray();
        }
        void InitializeHistoricalCulture(PrehistoryState s,CultureIdentity c)
        {
            var h=s.History;int religion=h.Religions.Length;
            if(RevisedHistory(s)){if(c.Herding>0){c.Gathering+=c.Herding;c.Herding=0;c.Subsistence="Reindeer hunting and seasonal gathering";}string name=HistoricalName("people",c.OriginCell);if(s.Society.Cultures.Any(x=>x.Id!=c.Id&&x.Name==name))name+=" "+HistoricalName("people-place",c.OriginCell);c.Name=name;}
            string[] rituals={"seasonal gatherings and ancestor offerings","river festivals and shared feasts","household shrines and pilgrimage","oral recitation and sacred groves","communal rites at sacred peaks"};
            string[] ethos={"hospitality and reciprocal obligations","stewardship and ancestral duty","oaths, justice and communal service","ritual generosity and reconciliation"};
            h.Religions=h.Religions.Concat(new[]{new HistoricalReligion{Id=religion,CultureId=c.Id,HolyCell=c.OriginCell,FoundedYear=s.Years,Name="Tradition of "+c.Name,Tradition="Local ancestral and place-based traditions",Ethos=ethos[(int)(HistoryRoll("ethos",c.Id,0)*ethos.Length)],Ritual=rituals[(int)(HistoryRoll("rites",c.Id,0)*rituals.Length)]}}).ToArray();
            var hc=new HistoricalCulture{Id=c.Id,ReligionId=religion,LanguageId=c.Id,Sedentism=150};hc.Technology[0]=s.Years+1;
            if(ModernHistory(s)){Array.Resize(ref hc.Technology,HistoricalTechnologies.Names.Length);Array.Resize(ref hc.Research,HistoricalTechnologies.Names.Length);}
            h.Cultures=h.Cultures.Concat(new[]{hc}).ToArray();
            if(EraHistory(s))InitializeHistoricalAgriculture(s,c);
            if(h.CellPolity.Length==0){int n=World.Surface.Cells.Length;h.CellPolity=Enumerable.Repeat(-1,n).ToArray();h.CellReligion=Enumerable.Repeat(-1,n).ToArray();h.CellStage=new int[n];}
            h.CellReligion[c.OriginCell]=religion;h.CellStage[c.OriginCell]=1;
            if(ExperimentalHistory(s)){InitializeFaithDynamics(s);h.Religions[religion].Reach=c.Fishing>=300?ReligiousReach.Portable:ReligiousReach.Local;}
            HistoryEvent(s,"culture","A persistent regional exchange and storage tradition emerges among "+c.Name+".",c.OriginCell,c.Id,religion:religion);
        }
        void AdvanceHistoricalCultures(PrehistoryState s,CancellationToken token,double elapsedYears=50)
        {
            var h=s.Society;var history=s.History;
            if(ModernHistory(s))ResetFiscalCaches();
            double scale=StochasticCalendar(s)?elapsedYears/50:1,emergenceScale=StochasticCalendar(s)?elapsedYears/100:1;
            if(RegionalHistory(s))using(ProfileWork("regional learning"))AdvanceRegionalLearning(s,token,scale);
            if(DelegatedHistory(s))using(ProfileWork("ocean settlements"))AdvanceRemoteOceanSettlements(s,token,elapsedYears);
            if(StochasticCalendar(s)||s.Years%100==0)
            {
                if(RegionalHistory(s))RegionalFarmingCommunities(s,token,emergenceScale);
                if(ExperimentalHistory(s))MaritimeFoundingCommunities(s,token,emergenceScale);
                if(EraHistory(s))FarmingFrontiers(s,token,emergenceScale);
                var established=new bool[Patches.Length];foreach(var c in h.Cultures)established[CellPatch[c.OriginCell]]=true;
                for(int p=0;p<Patches.Length;p++)
                {
                    if((p&255)==0)token.ThrowIfCancellationRequested();
                    if(Patches[p].Island&&!RevisedHistory(s)||established[p]||s.Population[p]<600||!LocalHistory(s)&&h.Knowledge[p]<4500)continue;
                    int cell=FindCultureEmergenceSite(s,p);
                    if(cell<0)continue;double opportunity=CultureOpportunity(s,cell),density=HistoricalSiteDensity(s,cell);
                    // Rich mobile subsistence need not become sedentary simply because time passes.
                    if(opportunity<(LocalHistory(s)?.15:DiffusionHistory(s)?HistoricalFormationEffects.EmergenceFloor(h.Knowledge[p]):RevisedHistory(s)?.34:.30)||density<.022||!LocalHistory(s)&&s.Population[p]<Patches[p].Capacity*.4)continue;
                    // A large occupied ecological patch represents more local communities,
                    // not the same single opportunity as a tiny refuge. Sublinear and capped
                    // exposure avoids multiplying directly by tessellation cell count.
                    double exposure=FrontierHistory(s)?RegionalEmergenceExposure(s.Population[p]):1;
                    double chance=LocalHistory(s)?LocalCommunityChance(s,p,cell):.012*opportunity*D.Clamp(density/.08)*exposure;
                    if(HistoryRoll("culture",p,s.Years)<ElapsedChance(chance,emergenceScale))InitializeHistoricalCulture(s,FoundCulture(s,p,cell));
                }
            }
            if(h.Cultures.Length==0)return;
            var claims=new SortedDictionary<int,int>();var contacts=new SortedSet<(int,int)>();
            foreach(int cell in CulturalFrontierCells(s))
            {
                if((cell&2047)==0)token.ThrowIfCancellationRequested();int owner=h.CellCulture[cell];if(owner<0)continue;
                foreach(int next in World.Surface.Cells[cell].Neighbors)
                {
                    int other=h.CellCulture[next],patch=CellPatch[next];if(other>=0){if(other!=owner)contacts.Add((Math.Min(owner,other),Math.Max(owner,other)));continue;}
                    if(patch<0||World.Hydrology.OceanConnected[next]||World.Hydrology.LakeCoverageFraction[next]>=.5||HistoricalSiteDensity(s,next)<.012||CultureOpportunity(s,next)<(RevisedHistory(s)?.15:.20))continue;
                    double distance=D.Angle(World.Surface.Cells[h.Cultures[owner].OriginCell].Position,World.Surface.Cells[next].Position)*World.Recipe.RadiusKm;
                    double transport=ModernHistory(s)?HistoricalModernEffects.LandTransport(history.Cultures[owner],s.Years):1;
                    double chance=.20*HabitatSimilarity(CellPatch[cell],patch)/(1+Math.Pow(distance/(350*transport),3));
                    if(RealismHistory(s))chance*=HistoricalLandscapeEffects.LivelihoodContinuity(World.Ecology.Habitat[h.Cultures[owner].OriginCell],World.Ecology.Habitat[cell],World.Ecology.Habitat[next]);
                    // More usable movement/contact opportunities, not free
                    // population or automatically viable imported agriculture.
                    if(HistoryPairRoll(s,"identity",cell,next)<ElapsedChance(chance,scale*transport)&&(!claims.ContainsKey(next)||owner<claims[next]))claims[next]=owner;
                }
            }
            if(RevisedHistory(s))HistoricalMaritimeCultures(s,claims,contacts,token,scale);
            foreach(var claim in claims){SetCultureOwner(s,claim.Key,claim.Value);history.CellReligion[claim.Key]=history.Cultures[claim.Value].ReligionId;history.CellStage[claim.Key]=1;}
            // Shipping sustains exchange across straits without requiring a land cultural frontier.
            var patchCulture=Enumerable.Repeat(-1,Patches.Length).ToArray();
            // Modern/local contacts use actual route endpoints, not a patch's
            // first owner. Older rules retain that owner in stable cell order.
            if(!DiffusionHistory(s))foreach(int cell in CulturalCells(s)){int patch=CellPatch[cell];if(patch>=0&&patchCulture[patch]<0)patchCulture[patch]=h.CellCulture[cell];}
            foreach(var route in Routes)
            {
                // A large ecoregion is not a single exchange station. History-8
                // land contacts use inhabited local paths, not its first culture.
                if(DiffusionHistory(s)&&!route.Sea)continue;
                int a=patchCulture[route.A],b=patchCulture[route.B];if(RevisedHistory(s)&&route.Sea){a=route.CellA<0?-1:h.CellCulture[route.CellA];b=route.CellB<0?-1:h.CellCulture[route.CellB];}if(a<0||b<0||a==b||s.Population[route.A]<100||s.Population[route.B]<100)continue;
                bool sailing=HistoricalTechnologies.Has(history.Cultures[a],HistoricalTechnology.Sailing)||HistoricalTechnologies.Has(history.Cultures[b],HistoricalTechnology.Sailing);
                bool navigation=HistoricalTechnologies.Has(history.Cultures[a],HistoricalTechnology.Navigation)||HistoricalTechnologies.Has(history.Cultures[b],HistoricalTechnology.Navigation);
                if(!route.Sea||sailing&&route.DistanceM<(navigation?700000:200000))contacts.Add((Math.Min(a,b),Math.Max(a,b)));
            }
            if(DiffusionHistory(s))using(ProfileWork("culture exchange paths",h.Cultures.Length))HistoricalExchangeContacts(s,contacts,token);
            if(ModernHistory(s))LocalCropTeachingContacts(s,contacts);
            if(ModernHistory(s))using(ProfileWork("ocean exchange"))ModernOceanExchange(s,contacts,token,elapsedYears);
            foreach(var pair in contacts){var a=h.Cultures[pair.Item1];var b=h.Cultures[pair.Item2];if(!a.Contacts.Contains(b.Id)){a.Contacts=a.Contacts.Concat(new[]{b.Id}).OrderBy(i=>i).ToArray();b.Contacts=b.Contacts.Concat(new[]{a.Id}).OrderBy(i=>i).ToArray();HistoryEvent(s,"contact",a.Name+" opens exchange with "+b.Name+".",a.OriginCell,a.Id,category:ChronicleCategory.Politics);}}
            RefreshSocietyTotals(s);
            if(DelegatedHistory(s))AdaptHistoricalLivelihoods(s,token,elapsedYears);
            if(DelegatedHistory(s))PrepareLocalNavigationSupport(s,token);
            if(RegionalHistory(s))RefreshInstitutionalExamples(s,contacts);
            if(RegionalHistory(s))PrepareRegionalCropSites(s,token);
            foreach(var c in h.Cultures)
            {
                var hc=history.Cultures[c.Id];int economicCell=RegionalHistory(s)?HistoricalEconomicCenter(s,c):c.OriginCell;
                int patch=CellPatch[economicCell];double suitability=CultureAgriculture(s,economicCell),opportunity=CultureOpportunity(s,economicCell);
                bool cultivatedContact=c.Contacts.Any(i=>h.Cultures[i].Farming>200 && (!RevisedHistory(s) || (contacts.Contains((Math.Min(c.Id,i),Math.Max(c.Id,i))) && CropCompatibility(c.OriginCell,h.Cultures[i].OriginCell)>.35)));
                if(EraHistory(s))AdvanceHistoricalAgriculture(s,c,contacts,scale);
                if(ModernHistory(s))SelectLocalCrops(s,c,elapsedYears);
                if(RealismHistory(s))AdvanceAnimalHusbandry(s,c,contacts,elapsedYears);
                if(!EraHistory(s)&&c.Farming==0&&h.Knowledge[patch]>=6500&&s.Years-c.FoundedYear>500&&suitability>(RevisedHistory(s)?.16:.25))
                {
                    double pressure=D.Clamp(s.Population[patch]/(double)Math.Max(1,Patches[patch].Capacity));
                    double chance=(cultivatedContact?.10:.004)*suitability*(.3+.7*pressure)*(1-.5*(RevisedHistory(s)&&!UsesGlacialDispersal(s)?0:GlacialIntensity(s.Years)));
                    if(RevisedHistory(s))chance*=.5+CultureStoredOpportunity(s,c.OriginCell);
                    if(HistoryRoll("cultivation",c.Id,s.Years)<ElapsedChance(chance,scale)){c.Farming=50;hc.Technology[1]=s.Years+1;HistoryEvent(s,"domestication",c.Name+" begins sustained cultivation of local food plants.",c.OriginCell,c.Id,category:ChronicleCategory.Discoveries);}
                }
                int farmingLimit=TerritorialHistory(s)?850:(int)(850*D.Clamp(suitability*1.6));
                if(EmergenceHistory(s))
                {
                    double fit=CropYieldFactor(s,c.Id,economicCell,false);
                    farmingLimit=HistoricalEmergenceEffects.FarmingLimit(fit,CultivationIncentive(s,economicCell,fit));
                }
                int uptake=cultivatedContact?8:4;
                if(TerritorialHistory(s))
                {
                    double pressure=D.Clamp(s.Population[patch]/(double)Math.Max(1,Patches[patch].Capacity));
                    // Crop practice already models the protracted domestication
                    // interval. Established methods can spread through households
                    // without imposing another universal 5–10 millennia of delay.
                    uptake=(int)Math.Round((cultivatedContact?36:18)*(.4+.6*pressure));
                }
                if(c.Farming>0)
                {
                    int change=(int)Math.Round(uptake*scale);
                    c.Farming=EmergenceHistory(s)?Math.Max(0,c.Farming+Math.Sign(farmingLimit-c.Farming)*Math.Min(change,Math.Abs(farmingLimit-c.Farming))):Math.Max(0,Math.Min(farmingLimit,c.Farming+change));
                }
                if(ModernHistory(s))InvalidateFiscalCache(c.Id);
                hc.Surplus=(int)(1000*opportunity*(Math.Max(.15,c.Fishing/700.0)+.75*c.Farming/850.0));
                if(RevisedHistory(s))hc.Surplus=(int)Math.Min(1000,1000*(opportunity*c.Fishing/1100.0+CultureStoredOpportunity(s,c.OriginCell)*c.Farming/850.0));
                if(EraHistory(s))hc.Surplus=(int)Math.Min(1000,1000*(opportunity*c.Fishing/1100.0+CultureStoredOpportunity(s,c.OriginCell)*c.Farming/850.0*CropYieldFactor(s,c.Id,c.OriginCell,true)));
                int sedentaryLimit=(int)(900*opportunity)+c.Farming/3;hc.Sedentism=Math.Min(1000,Math.Max(0,hc.Sedentism+Math.Sign(sedentaryLimit-hc.Sedentism)*Math.Min((int)Math.Round(8*scale),Math.Abs(sedentaryLimit-hc.Sedentism))));
                var towns=h.Settlements.Where(t=>t.CultureId==c.Id&&t.Population>=100).ToArray();
                if(DiffusionHistory(s))hc.Surplus=(int)Math.Round(1000*towns.Select(t=>LocalStoredSupport(s,c.Id,t.Cell)).Concat(new[]{LocalStoredSupport(s,c.Id,economicCell)}).Max());
                int institutionTarget=Math.Min(950,hc.Surplus+(towns.Length>0?200:0)+Math.Min(150,(RevisedHistory(s)?c.Contacts.Count(i=>contacts.Contains((Math.Min(c.Id,i),Math.Max(c.Id,i)))):c.Contacts.Length)*25));
                hc.Institution=Math.Max(0,hc.Institution+Math.Sign(institutionTarget-hc.Institution)*Math.Min((int)Math.Round(5*scale),Math.Abs(institutionTarget-hc.Institution)));c.Organization=hc.Institution;
                if(c.Farming>0)c.Subsistence=RevisedHistory(s)?(CultureStoredOpportunity(s,c.OriginCell)>suitability*.65?"Seasonal grain cultivation":"Mixed gardens and tree crops")+" with "+(c.Fishing>300?"fishing":"hunting and gathering"):"Mixed cultivation with "+(c.Fishing>300?"fishing":"hunting and gathering");
                if(RegionalHistory(s)&&c.Farming>0)c.Subsistence="Crop cultivation with "+(c.Fishing>300?"fishing and wild-food gathering":"hunting and wild-food gathering");
                if(!StochasticCalendar(s))AdvanceSettlementOpportunity(s,c.Id,token,economicCell);
                AdvanceTechnology(s,c,hc,towns,contacts,scale);
                if(ModernHistory(s))InvalidateFiscalCache(c.Id);
                if(ModernHistory(s))SurveyTownCrops(s,c.Id);
            }
            RefreshHistoricalStages(s);if(!h.ManualStep)h.SelectedStepYears=AutomaticYears(s);
        }
        void AdvanceSettlementOpportunity(PrehistoryState s,int culture,CancellationToken token,int foundingCell=-1)
        {
            var h=s.Society;var c=h.Cultures[culture];var hc=s.History.Cultures[culture];
            var towns=h.Settlements.Where(t=>t.CultureId==culture&&t.Population>=100).ToArray();
            if(towns.Length==0&&hc.Sedentism>400&&hc.Surplus>230&&c.Population>1800)FoundHistoricalSettlement(s,c,foundingCell>=0?foundingCell:RegionalHistory(s)?HistoricalEconomicCenter(s,c):c.OriginCell);
            if(towns.Length==0||!StochasticCalendar(s)&&s.Years%500!=0||hc.Surplus<=320||towns.Length>=32)return;
            int candidate=-1;double best=0;
            foreach(int i in CultureCells(s,culture))
            {
                if((i&2047)==0)token.ThrowIfCancellationRequested();
                if(h.CellCulture[i]!=culture||s.Population[CellPatch[i]]<=1200||CultureOpportunity(s,i)<=.3)continue;
                if(SettlementWithin100Km(s,i))continue;
                double distance=towns.Min(t=>D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[i].Position)*World.Recipe.RadiusKm);
                double q=CultureOpportunity(s,i)/(1+distance/100);if(distance<=400&&q>best){best=q;candidate=i;}
            }
            if(candidate>=0&&HistoryRoll("town",culture,s.Years)<.35)FoundHistoricalSettlement(s,c,candidate);
        }
        void AdvanceTechnology(PrehistoryState s,CultureIdentity c,HistoricalCulture hc,Settlement[] towns,SortedSet<(int,int)> activeContacts,double scale=1)
        {
            for(int k=2;k<hc.Technology.Length;k++)
            {
                if(ModernHistory(s)&&HistoricalModernEffects.Era((HistoricalTechnology)k)>s.History.Modern.TechnologyCeiling)continue;
                if(hc.Technology[k]>0||HistoricalTechnologies.Requirements(s.History.Version,k).Any(p=>hc.Technology[p]==0))continue;
                if(k!=2&&k!=3&&!(ExperimentalHistory(s)&&k==(int)HistoricalTechnology.Navigation)&&towns.Length==0)continue;
                bool coastal=World.Surface.Cells[c.OriginCell].Neighbors.Any(i=>World.Hydrology.OceanConnected[i]);if(k==3&&!coastal&&c.Fishing<300)continue;
                if(k==4&&World.Ecology.Habitat[c.OriginCell].Flooding==FloodRegime.None&&World.Ecology.Habitat[c.OriginCell].AridityMilli>700)continue;
                bool contact=c.Contacts.Any(i=>s.Society.Cultures[i].Population>100&&s.History.Cultures[i].Technology[k]>0&&(!DiffusionHistory(s)||s.History.Cultures[i].Technology[k]-1<s.Years)&&(!RevisedHistory(s)||activeContacts.Contains((Math.Min(c.Id,i),Math.Max(c.Id,i)))));
                bool carried=ModernHistory(s)&&hc.ParentCultureId>=0&&HistoricalModernEffects.CarriedAtFounding(s.History.Cultures[hc.ParentCultureId],k,c.FoundedYear);contact|=carried;
                if(EraHistory(s)&&!SupportsHistoricalPractice(s,c,hc,towns,k,contact))continue;
                // Settlers can retain a known method without reinventing it.
                // This follows, never bypasses, local support/prerequisite gates.
                if(carried)hc.Research[k]=Math.Max(hc.Research[k],120+k*30-1);
                int effort=(HistoricalTechnologies.Has(hc,HistoricalTechnology.Scholasticism)?6:2)+Math.Min(12,(int)(towns.Sum(t=>t.Population)/2500))+hc.Surplus/150+(contact?25:0);
                if(ModernHistory(s)&&k>=15)effort*=2+(HistoricalTechnologies.Has(hc,HistoricalTechnology.Printing)?1:0)+(HistoricalTechnologies.Has(hc,HistoricalTechnology.ScientificMethod)?2:0)+(HistoricalTechnologies.Has(hc,HistoricalTechnology.Computing)?2:0);
                hc.Research[k]+=Math.Max(0,(int)(effort*(.7+.6*HistoryPairRoll(s,"research",c.Id,k))*scale));
                if(hc.Research[k]<120+k*30)continue;
                hc.Technology[k]=s.Years+1;HistoryEvent(s,"technology",c.Name+" "+(contact?"adopts ":"develops ")+HistoricalTechnologies.Name(s.History.Version,k)+".",c.OriginCell,c.Id,category:ChronicleCategory.Discoveries);
            }
        }
        void FoundHistoricalSettlement(PrehistoryState s,CultureIdentity c,int cell,long arrivingFounders=0)
        {
            long founderLimit=RegionalHistory(s)?Math.Min(arrivingFounders>0?arrivingFounders:400,(long)(RegionalFoundingFood(s,c,cell)*.8)):400;if(founderLimit<100)return;
            if(RegionalHistory(s))
            {
                var old=s.Society.Settlements.FirstOrDefault(t=>t.Cell==cell);
                if(old!=null)
                {
                    if(old.Population>=100)return;int patch=CellPatch[cell];long settlers=Math.Min(founderLimit,arrivingFounders>0?s.Population[patch]:s.Population[patch]/5);if(settlers<100)return;
                    s.Population[patch]-=settlers;old.Population+=settlers;old.FoodReserve+=settlers;old.CultureId=c.Id;
                    HistoryEvent(s,"settlement-reoccupied",c.Name+" reoccupies the recorded site of "+old.Name+", bringing new resident households and food reserves. Its earlier history remains attached to the same place.",cell,c.Id,old.PolityId,town:old.Id,category:ChronicleCategory.Society);return;
                }
            }
            int before=s.Society.Settlements.Length;FoundSettlement(s,c,cell,founderLimit,arrivingFounders>0);if(before==s.Society.Settlements.Length)return;
            var town=s.Society.Settlements[before];if(RevisedHistory(s))town.Name=HistoricalName("town",cell);town.PolityId=-1;s.History.Towns=s.History.Towns.Concat(new[]{new HistoricalTown{Id=town.Id,FoodBase=town.Population*3}}).ToArray();
            HistoryEvent(s,"settlement",town.Name+" is founded as a permanent settlement"+(RealismHistory(s)?" with "+town.Population.ToString(System.Globalization.CultureInfo.InvariantCulture)+" residents.":"."),cell,c.Id,town:town.Id,category:ChronicleCategory.Politics);
            if(CivicHistory(s)&&s.History.CellPolity[cell]>=0)
            {
                var host=s.History.Polities[s.History.CellPolity[cell]];town.PolityId=host.Id;town.Government=host.Government;
                HistoryEvent(s,"incorporation",town.Name+" grows within the administration of "+host.Name+".",cell,c.Id,host.Id,town:town.Id,category:ChronicleCategory.Politics);return;
            }
            if(RevisedHistory(s))
            {
                var sponsor=s.Society.Settlements.Where(t=>t.Id!=town.Id&&t.CultureId==c.Id&&t.PolityId>=0&&s.History.Polities[t.PolityId].EndedYear<0&&s.History.Polities[t.PolityId].Cohesion>=550)
                    .OrderBy(t=>D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[cell].Position)).ThenBy(t=>t.Id).FirstOrDefault();
                if(sponsor!=null)
                {
                    var p=s.History.Polities[sponsor.PolityId];double distance=D.Angle(World.Surface.Cells[sponsor.Cell].Position,World.Surface.Cells[cell].Position)*World.Recipe.RadiusKm;
                    if(LocalHistory(s))
                    {
                        if(HistoryRoll("settlement-patron",town.Id,s.Years)<.65&&BeginSupportedIncorporation(s,p,cell,BuildAdministrativeNetwork(s,default)))
                            HistoryEvent(s,"settlement-sponsorship",p.Name+" funds an effort to incorporate the new community at "+town.Name+"; political control is not yet established.",cell,c.Id,p.Id,town:town.Id);
                        return;
                    }
                    if(distance<300&&p.Treasury>=town.Population&&HistoryRoll("settlement-patron",town.Id,s.Years)<.65&&ReachableHistoricalContact(s,sponsor.Cell,cell,c.Id,default))
                    {town.PolityId=p.Id;town.Government=p.Government;p.Treasury-=town.Population;SetPoliticalOwner(s,cell,p.Id);HistoryEvent(s,"sponsored-settlement",p.Name+" sponsors and administers the new community at "+town.Name+".",cell,c.Id,p.Id,town:town.Id,category:ChronicleCategory.Politics);}
                }
            }
        }
        void RefreshHistoricalStages(PrehistoryState s)
        {
            var h=s.History;var society=s.Society;
            var index=WorkIndex(s);
            using var stageTimer=ProfileWork("local stage bookkeeping",index==null?h.CellStage.Length:index.StageOverrides.Count+society.Cultures.Length+society.Settlements.Length);
            if(index==null)for(int i=0;i<h.CellStage.Length;i++)h.CellStage[i]=(byte)(society.CellCulture[i]>=0?1:0);
            else {foreach(int cell in index.StageOverrides)h.CellStage[cell]=h.CellPolity[cell]>=0?3:society.CellCulture[cell]>=0?1:0;index.StageOverrides.Clear();}
            var byCulture=society.Settlements.Where(t=>t.Population>=800).GroupBy(t=>t.CultureId).ToDictionary(g=>g.Key,g=>g.ToArray());
            foreach(var c in society.Cultures)
            {
                var old=c.Stage;var towns=byCulture.TryGetValue(c.Id,out var local)?local:Array.Empty<Settlement>();
                c.Stage=towns.Any(t=>t.PolityId>=0&&h.Polities[t.PolityId].EndedYear<0)?SocietyStage.Civilization:towns.Length>0&&c.Organization>=300?SocietyStage.EmbryonicCivilization:SocietyStage.AdvancedCulture;
                if(old!=c.Stage){c.StageYear=s.Years;HistoryEvent(s,"transition",c.Name+" now has "+(c.Stage==SocietyStage.Civilization?"city-based state institutions":c.Stage==SocietyStage.EmbryonicCivilization?"regional settlement institutions":"decentralized communities")+".",c.OriginCell,c.Id);}
                foreach(var t in towns){h.CellStage[t.Cell]=(byte)(h.CellPolity[t.Cell]>=0||t.PolityId>=0&&h.Polities[t.PolityId].EndedYear<0?3:2);index?.StageOverrides.Add(t.Cell);}
            }
            if(index==null)foreach(int cell in PoliticalCells(s))h.CellStage[cell]=3;
            else index.StagesVerified=true;
        }
    }
}
