using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        sealed class CivicSite { public double Circumscription,Coordination; }
        readonly Dictionary<int,CivicSite> civicSites=new Dictionary<int,CivicSite>();
        CivicSite CivicContext(PrehistoryState s,int cell)
        {
            int cacheKey=cell*2+(RealismHistory(s)?1:0);if(civicSites.TryGetValue(cacheKey,out var cached))return cached;
            PrepareHistoricalEnvironment();double core=RealismHistory(s)?AgriculturalYield(s,cell):Math.Max(HistoricalAgriculturalYield(cell),riverSites[cell]*.85);
            double outside=0,weight=0;var distance=new Dictionary<int,int>{{cell,0}};var heap=new Heap();heap.Push(0,cell);
            int limit=200000+(int)Math.Sqrt(World.Surface.Cells[cell].AreaM2/Math.PI);
            while(heap.Count>0)
            {
                var item=heap.Pop();if(item.Key!=distance[item.Id])continue;var c=World.Surface.Cells[item.Id];
                if(item.Id!=cell&&!World.Hydrology.OceanConnected[item.Id])
                {
                    double w=1/(1+item.Key/100000.0),foraging=ruralWeight[item.Id]/Math.Max(1,HistoricalLandArea(item.Id)*1000)/.15;
                    outside+=w*Math.Max(AgriculturalYield(s,item.Id),D.Clamp(foraging));weight+=w;
                }
                foreach(int j in c.Neighbors){long d=item.Key+(long)(D.Angle(c.Position,World.Surface.Cells[j].Position)*World.Recipe.RadiusKm*1000);if(d<=limit&&(!distance.TryGetValue(j,out int old)||d<old)){distance[j]=(int)d;heap.Push(d,j);}}
            }
            var habitat=World.Ecology.Habitat[cell];bool coast=World.Surface.Cells[cell].Neighbors.Any(i=>World.Hydrology.OceanConnected[i]);
            cached=new CivicSite{Circumscription=D.Q(D.Clamp((core-(weight>0?outside/weight:core))/Math.Max(.1,core)),1000000),Coordination=D.Q(D.Clamp(HistoricalRiverAccess(cell)*6+(habitat.Flooding==FloodRegime.RiverSeasonal||habitat.Flooding==FloodRegime.RiverPersistent?.35:0)+(coast?.25:0)),1000000)};
            civicSites.Add(cacheKey,cached);return cached;
        }
        public double StateFoundationReadiness(PrehistoryState s,Settlement town)
        {
            var hc=s.History.Cultures[town.CultureId];var context=CivicContext(s,town.Cell);
            double concentration=D.Clamp(town.Population/6000.0),storage=D.Clamp(hc.Surplus/700.0),institutions=D.Clamp(hc.Institution/650.0);
            if(DiffusionHistory(s))storage=D.Clamp(LocalStoredSupport(s,town.CultureId,town.Cell)/.55);
            if(LocalHistory(s))return HistoricalLocalEffects.FoundationReadiness(storage,concentration,institutions,context.Coordination,context.Circumscription,LocalExchangeConcentration(s,town));
            return HistoricalInstitutionRequirements.Readiness(storage,concentration,context.Circumscription,context.Coordination,institutions);
        }
        HistoricalLeader CivicLeader(PrehistoryState s,int polity)=>s.History.Dynamics.Leaders[s.History.Dynamics.States[polity].LeaderId];
        void InitializeCivicPolity(PrehistoryState s,HistoricalPolity polity,Settlement town)
        {
            var context=CivicContext(s,town.Cell);var ds=new HistoricalStateDynamics{Id=polity.Id,FoundationPotential=(int)Math.Round(StateFoundationReadiness(s,town)*1000),LastSuccession=s.Years};
            ds.Tradition=context.Coordination>.55?"Public works and communal storage":context.Circumscription>.4?"Concentrated agrarian administration":World.Surface.Cells[town.Cell].Neighbors.Any(i=>World.Hydrology.OceanConnected[i])?"Port exchange and merchant councils":"Local defense and household alliances";
            s.History.Dynamics.States=s.History.Dynamics.States.Concat(new[]{ds}).ToArray();AppointCivicLeader(s,polity,false);
        }
        void AppointCivicLeader(PrehistoryState s,HistoricalPolity polity,bool succession,CancellationToken token=default)
        {
            var dynamics=s.History.Dynamics;var ds=dynamics.States[polity.Id];var former=ds.LeaderId>=0?dynamics.Leaders[ds.LeaderId]:null;
            if(former!=null){former.EndedYear=s.Years;former.Legacy=$"{former.Victories} victories or conquests, {former.Defeats} defeats, {former.Reforms} public-work reforms.";}
            int id=dynamics.Leaders.Length;double Roll(string trait)=>HistoryRoll("leader-"+trait,id,s.Years);
            var leader=new HistoricalLeader{Id=id,PolityId=polity.Id,StartedYear=s.Years,ExpectedEndYear=s.Years+18+(int)(32*Roll("tenure")),Military=200+(int)(600*Roll("military")),Administration=200+(int)(600*Roll("administration")),Diplomacy=200+(int)(600*Roll("diplomacy")),Ambition=150+(int)(700*Roll("ambition")),Name=HistoricalName("leader-name",unchecked(id*7919+s.Years))+" of the "+HistoricalName("leader-house",polity.Id)+" House"};
            if(Roll("exceptional")<.015){leader.Military=850+(int)(150*Roll("exceptional-military"));leader.Administration=Math.Max(leader.Administration,650);leader.Ambition=800+(int)(200*Roll("exceptional-ambition"));}
            if(ExperimentalHistory(s))ConfigureHistoricalSuccession(s,polity,ds,leader,former);
            dynamics.Leaders=dynamics.Leaders.Concat(new[]{leader}).ToArray();ds.LeaderId=id;ds.LastSuccession=s.Years;polity.Ruler=leader.Name;polity.RulerSince=s.Years;
            if(!succession)return;
            int successionCost=45+Math.Min(150,(former?.Renown??0)/5);
            if(InstitutionalHistory(s))successionCost=HistoricalInstitutionalEffects.SuccessionLegitimacyCost(HereditaryCourt(polity,ds),s.History.Cultures[polity.CultureId].Institution,leader.Diplomacy,former?.Renown??0,leader.Selection=="Acclamation of a rival court and military supporters");
            polity.Legitimacy=Math.Max(100,polity.Legitimacy-successionCost);
            if(former?.Renown>=250)polity.Cohesion=Math.Max(0,polity.Cohesion-(int)(Math.Min(200,former.Renown/4)*(.5+.5*(1-ds.PublicWorks/1000.0))));
            if(former?.Renown>=250)HistoryEvent(s,"great-ruler-death",former.Name+" dies after a consequential reign in "+polity.Name+". "+former.Legacy,s.Society.Settlements[polity.CapitalId].Cell,polity.CultureId,polity.Id,category:ChronicleCategory.Politics);
            else HistoryEvent(s,"succession",leader.Name+" takes office in "+polity.Name+".",s.Society.Settlements[polity.CapitalId].Cell,polity.CultureId,polity.Id,category:ChronicleCategory.Politics);
            if(!ExperimentalHistory(s)&&polity.Legitimacy<350&&s.Society.Settlements.Count(t=>t.PolityId==polity.Id&&t.Population>=100)>1)
                HistoryEvent(s,"succession-crisis","Rival courts contest the succession in "+polity.Name+" after "+(former?.Name??"its former ruler")+".",s.Society.Settlements[polity.CapitalId].Cell,polity.CultureId,polity.Id,category:ChronicleCategory.Politics);
            if(ExperimentalHistory(s))ContestHistoricalSuccession(s,polity,leader,former,token);
        }
        double CivicReachKm(PrehistoryState s,HistoricalPolity p,long? urbanResidents=null)
        {
            var hc=s.History.Cultures[p.CultureId];var leader=CivicLeader(s,p.Id);var ds=s.History.Dynamics.States[p.Id];
            if(LocalHistory(s))
            {
                int offices=DelegatedHistory(s)?0:s.History.Administration?.Offices.Count(o=>o.PolityId==p.Id&&o.EndedYear<0)??0;
                // Offices serve places, not a global radius bonus. Do not scan
                // the archived office roll merely to discard its count.
                long urban=urbanResidents??s.Society.Settlements.Where(t=>t.PolityId==p.Id&&t.Population>=100).Sum(t=>t.Population);
                double local=HistoricalLocalEffects.AdministrativeReachKm(urban,leader.Administration,ds.PublicWorks,HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing),HistoricalTechnologies.Has(hc,HistoricalTechnology.Roads))*(1+Math.Min(4,offices*.2));
                if(EmergenceHistory(s))local*=HistoricalEmergenceEffects.EarlyGovernmentScale(HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing),HistoricalTechnologies.Has(hc,HistoricalTechnology.Roads),HistoricalTechnologies.Has(hc,HistoricalTechnology.Bronze));
                if(RealismHistory(s))local*=Math.Sqrt(HistoricalModernEffects.AdministrativeCapacity(hc,s.Years)*HistoricalModernEffects.LandTransport(hc,s.Years));
                // Two-kilometre service districts avoid solving an entire route
                // graph again for an imperceptible resident-count fluctuation.
                return ModernHistory(s)?Math.Max(2,Math.Round(local/2)*2):D.Q(local,1000000);
            }
            if(TerritorialHistory(s))return HistoricalTerritorialEffects.ReachKm(s.Society.Settlements.Where(t=>t.PolityId==p.Id&&t.Population>=100).Sum(t=>t.Population),leader.Administration,ds.PublicWorks,HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing),HistoricalTechnologies.Has(hc,HistoricalTechnology.Roads));
            long supported=Math.Max(p.Population,s.Society.Settlements.Where(t=>t.PolityId==p.Id).Sum(t=>t.Population));
            return HistoricalLeadershipEffects.AdministrativeReachKm(supported,leader.Administration,ds.PublicWorks,HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing),HistoricalTechnologies.Has(hc,HistoricalTechnology.Roads));
        }
        void AdvanceCivicBudget(PrehistoryState s,HistoricalPolity p,Settlement[] cities,bool war,CancellationToken token=default,AdministrativeNetwork network=null)
        {
            var h=s.History;var ds=h.Dynamics.States[p.Id];var leader=CivicLeader(s,p.Id);var hc=h.Cultures[p.CultureId];long urban=cities.Sum(t=>t.Population),rural=Math.Max(0,p.Population-urban);
            double capacity=D.Clamp(hc.Surplus/650.0)*D.Clamp(hc.Institution/650.0)*(.35+.65*leader.Administration/1000.0)*(HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing)?1:.5);
            capacity=D.Q(capacity*(.75+.25*CivicContext(s,s.Society.Settlements[p.CapitalId].Cell).Circumscription+.15*ds.PublicWorks/1000.0),1000000);
            long tax=urban/(HistoricalTechnologies.Has(hc,HistoricalTechnology.Coinage)?16:20)+(long)(rural*.004*capacity);
            long treasuryBefore=p.Treasury,maintenance=p.Army/3+Math.Max(0,p.AreaKm2/2000);p.Treasury=Math.Max(0,Math.Min(1000000000,p.Treasury+tax-maintenance));
            if(LocalHistory(s)&&network!=null)
            {
                var account=network.Accounts[p.Id];rural=Math.Max(0,rural-account.DisconnectedResidents);
                tax=urban/(HistoricalTechnologies.Has(hc,HistoricalTechnology.Coinage)?16:20)+account.RuralReceipts;
                if(DelegatedHistory(s)){tax=account.UrbanReceipts+account.RuralReceipts;urban=account.TaxableUrbanResidents;}
                maintenance=p.Army/3+account.ServiceCost;
                // Use the pre-budget treasury, not the already-clamped legacy result.
                p.Treasury=Math.Max(0,Math.Min(1000000000,treasuryBefore+tax-maintenance));
            }
            int target=(int)Math.Min(500000,urban*(war?.07:.025)+rural*(war?.03:.006)*capacity);
            p.Army=Math.Max(0,p.Army+Math.Sign(target-p.Army)*Math.Min(Math.Max(50,target/20),Math.Abs(target-p.Army)));
            p.Exhaustion=Math.Max(0,Math.Min(1000,p.Exhaustion+(war?2:-6)));
            double far=cities.Max(t=>D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[s.Society.Settlements[p.CapitalId].Cell].Position)*World.Recipe.RadiusKm);
            int diversity=cities.Select(t=>t.CultureId).Distinct().Count();
            ds.Strain=HistoricalLeadershipEffects.IntegrationStrain(far,CivicReachKm(s,p),cities.Length,diversity);
            if(LocalHistory(s)&&network!=null)
            {
                var account=network.Accounts[p.Id];double distance=account.MaximumServiceDistanceM/(double)Math.Max(1,account.ReachM);
                if(DelegatedHistory(s))ds.Strain=HistoricalDelegationEffects.IntegrationStrain(account.MaximumServiceDistanceM,account.ReachM,diversity);
                ds.Strain=Math.Min(700,ds.Strain+(int)Math.Round(160*Math.Max(0,distance-1))+ (int)Math.Min(200,(account.DisconnectedResidents+(DelegatedHistory(s)?account.DisconnectedUrbanResidents:0))/50));
                if(InstitutionalHistory(s))ds.Strain=network.ResidentStrain[p.Id];
            }
            int targetCohesion=Math.Max(100,600+leader.Administration/5+hc.Institution/8-ds.Strain-p.Exhaustion/3+(p.Treasury>0?0:-150));
            int legitimacyTarget=Math.Max(100,Math.Min(1000,300+hc.Institution/3+leader.Diplomacy/4+ds.PublicWorks/10-p.Exhaustion/3-(cities.Average(t=>t.Stability)>600?0:250)));
            if(RealismHistory(s)){PoliticalCycleYear(s,p,cities,war,leader);targetCohesion-=ds.Cycle.ConquestDebt/4;legitimacyTarget-=ds.Cycle.EliteCapture/2;}
            if(ExperimentalHistory(s)&&ds.DisputedUntil>s.Years){targetCohesion=Math.Max(100,targetCohesion-160);legitimacyTarget=Math.Max(100,legitimacyTarget-170);}
            p.Cohesion=Math.Max(0,Math.Min(1000,p.Cohesion+Math.Sign(targetCohesion-p.Cohesion)*2));
            p.Legitimacy=Math.Max(0,Math.Min(1000,p.Legitimacy+Math.Sign(legitimacyTarget-p.Legitimacy)*2));
            ds.PeakPopulation=Math.Max(ds.PeakPopulation,p.Population);ds.PeakAreaKm2=Math.Max(ds.PeakAreaKm2,p.AreaKm2);
            if(s.Years>=leader.ExpectedEndYear){int beforeStates=h.Polities.Length;AppointCivicLeader(s,p,true,token);leader=CivicLeader(s,p.Id);if(ExperimentalHistory(s)&&h.Polities.Length!=beforeStates)return;}
            if(p.EndedYear>=0)return;
            if(!war&&p.Treasury>3000&&s.Years%20==0&&leader.Administration>=650&&HistoryRoll("public-work",p.Id,s.Years)<.25)
            {p.Treasury-=2000;ds.PublicWorks=Math.Min(1000,ds.PublicWorks+40);leader.Reforms++;leader.Renown=Math.Min(1000,leader.Renown+25);if(leader.Reforms==3)HistoryEvent(s,"public-works",leader.Name+" builds a durable network of public works in "+p.Name+".",s.Society.Settlements[p.CapitalId].Cell,p.CultureId,p.Id,category:ChronicleCategory.Society);}
            if(s.Years%10==0&&cities.Length>1&&(p.Cohesion<400||s.Years-ds.LastSuccession<10&&p.Legitimacy<350)&&HistoryRoll("civic-secession",p.Id,s.Years)<.025+ds.Strain/10000.0)
            {var city=cities.Where(t=>t.Id!=p.CapitalId).OrderByDescending(t=>D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[s.Society.Settlements[p.CapitalId].Cell].Position)).ThenBy(t=>t.Id).First();FoundHistoricalPolity(s,city,p.Id);}
        }
        int[] CivicInfluence(PrehistoryState s,CancellationToken token)
        {
            if(LocalHistory(s))return BuildAdministrativeNetwork(s,token).Owner;
            int n=World.Surface.Cells.Length;var owners=Enumerable.Repeat(-1,n).ToArray();var distance=Enumerable.Repeat(int.MaxValue,n).ToArray();var heap=new Heap();
            var reachByPolity=s.History.Polities.Select(p=>p.EndedYear<0?(int)(CivicReachKm(s,p)*1000):0).ToArray();
            foreach(var town in s.Society.Settlements.Where(t=>t.PolityId>=0&&t.Population>=(TerritorialHistory(s)?2500:800)).OrderBy(t=>t.Id)){owners[town.Cell]=town.PolityId;distance[town.Cell]=0;heap.Push(0,town.Cell);}
            while(heap.Count>0)
            {
                var x=heap.Pop();if(x.Key!=distance[x.Id])continue;token.ThrowIfCancellationRequested();int owner=owners[x.Id];var polity=s.History.Polities[owner];var cell=World.Surface.Cells[x.Id];int reach=reachByPolity[owner];
                for(int k=0;k<cell.Neighbors.Length;k++)
                {
                    int j=cell.Neighbors[k];if(CellPatch[j]<0||World.Hydrology.OceanConnected[j]||World.Hydrology.LakeCoverageFraction[j]>=.9||s.History.CellPolity[j]>=0&&s.History.CellPolity[j]!=owner)continue;
                    long d=x.Key+TravelCost(x.Id,j,cell.DistanceM[k],s.History.Cultures[polity.CultureId]);
                    double radius=TerritorialHistory(s)?Math.Sqrt(World.Surface.Cells[j].AreaM2/Math.PI):0;
                    if(d<=reach+radius&&(d<distance[j]||d==distance[j]&&owner<owners[j])){distance[j]=(int)d;owners[j]=owner;heap.Push(d,j);}
                }
            }
            return owners;
        }
        HistoricalCampaign ActiveCampaign(PrehistoryState s,HistoricalRelation relation)=>s.History.Dynamics.Campaigns.LastOrDefault(c=>c.A==relation.A&&c.B==relation.B&&c.EndedYear<0);
        HistoricalWarAim CivicWarAim(PrehistoryState s,HistoricalPolity attacker,HistoricalPolity defender,HistoricalLeader leader)
        {
            if(attacker.ParentId==defender.Id||defender.ParentId==attacker.Id||attacker.ParentId>=0&&attacker.ParentId==defender.ParentId)return HistoricalWarAim.Reunification;
            if(leader.Ambition>=750)return HistoricalWarAim.Conquest;
            if(defender.Treasury>0&&attacker.Treasury<900000000&&(attacker.Treasury<Math.Max(5000,attacker.Population/20)&&defender.Treasury>attacker.Treasury||leader.Ambition<550&&attacker.Army>defender.Army*1.5))return HistoricalWarAim.Tribute;
            bool Coastal(int polity)=>s.Society.Settlements.Any(t=>t.PolityId==polity&&t.Population>=100&&World.Surface.Cells[t.Cell].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]));
            if(HistoricalTechnologies.Has(s.History.Cultures[attacker.CultureId],HistoricalTechnology.Sailing)&&Coastal(attacker.Id)&&Coastal(defender.Id))return HistoricalWarAim.TradeRoute;
            return HistoricalWarAim.Frontier;
        }
        static string CivicWarObjective(HistoricalWarAim aim)=>aim==HistoricalWarAim.Conquest?"Conquest and regional hegemony":aim==HistoricalWarAim.Tribute?"Tribute and fiscal relief":aim==HistoricalWarAim.Reunification?"Reunification of a divided political realm":aim==HistoricalWarAim.TradeRoute?"Access to coastal exchange routes":aim==HistoricalWarAim.Liberation?"Release from tributary obligations and restoration of autonomy":"Frontier security and territorial control";
        void BeginCivicCampaign(PrehistoryState s,HistoricalRelation relation,int initiator=-1,HistoricalWarAim? forcedAim=null)
        {
            var h=s.History;var a=h.Polities[relation.A];var b=h.Polities[relation.B];var la=CivicLeader(s,a.Id);var lb=CivicLeader(s,b.Id);
            var attacker=initiator>=0?(initiator==a.Id?a:b):la.Ambition*D.Sqrt(Math.Max(1,a.Army))>=lb.Ambition*D.Sqrt(Math.Max(1,b.Army))?a:b;var defender=attacker==a?b:a;
            var leader=CivicLeader(s,attacker.Id);var aim=forcedAim??CivicWarAim(s,attacker,defender,leader);var c=new HistoricalCampaign{Id=h.Dynamics.Campaigns.Length,A=relation.A,B=relation.B,AttackerId=attacker.Id,LeaderId=leader.Id,StartedYear=s.Years,LastActionYear=s.Years,Name="Campaign for "+s.Society.Settlements[defender.CapitalId].Name,Aim=aim,Objective=CivicWarObjective(aim)};
            h.Dynamics.Campaigns=h.Dynamics.Campaigns.Concat(new[]{c}).ToArray();
            if(ModernHistory(s))RecordCivicRivalry(s,s.Society.Settlements[attacker.CapitalId],s.Society.Settlements[defender.CapitalId],c.Id,280);
            if(DelegatedHistory(s))c.Context=HistoricalCampaignContext(s,attacker,defender);
            HistoryEvent(s,"war",attacker.Name+" declares war on "+defender.Name+" over "+c.Objective.ToLowerInvariant()+".",s.Society.Settlements[attacker.CapitalId].Cell,attacker.CultureId,attacker.Id,defender.Id,category:ChronicleCategory.Politics);
            HistoryEvent(s,"campaign-start",leader.Name+" leads "+attacker.Name+" in the "+c.Name+" against "+defender.Name+" for "+c.Objective.ToLowerInvariant()+".",s.Society.Settlements[attacker.CapitalId].Cell,attacker.CultureId,attacker.Id,defender.Id,category:ChronicleCategory.Politics);
        }
        void EndCivicCampaign(PrehistoryState s,HistoricalRelation relation,string reason,bool negotiate=false)
        {
            var c=ActiveCampaign(s,relation);if(c==null)return;c.EndedYear=s.Years;c.Outcome=reason;
            if(negotiate&&c.Aim==HistoricalWarAim.Tribute&&(c.Conquests>0||c.Repulses>=3))
            {
                int winner=c.Conquests>0?c.AttackerId:c.AttackerId==c.A?c.B:c.A;int loser=winner==c.A?c.B:c.A;var recipient=s.History.Polities[winner];var payer=s.History.Polities[loser];
                long paid=HistoricalLeadershipEffects.TransferableFunds(payer.Treasury,recipient.Treasury,Math.Min(payer.Treasury/4,payer.Population/5));
                if(paid>0){payer.Treasury-=paid;recipient.Treasury+=paid;c.TributePaid=paid;c.TributeRecipientId=winner;c.Outcome+=" "+payer.Name+" pays "+paid.ToString("N0",CultureInfo.InvariantCulture)+" account units in a one-time settlement to "+recipient.Name+".";}
            }
            if(negotiate&&c.Aim==HistoricalWarAim.TradeRoute&&c.Conquests>0){relation.Trade=true;relation.Trust=Math.Max(0,relation.Trust);c.Outcome+=" The settlement restores access to exchange under a trade agreement.";}
            if(ExperimentalHistory(s)&&negotiate&&c.Aim==HistoricalWarAim.Liberation&&c.Repulses>=3)
            {
                var client=s.History.Polities[c.AttackerId];var patron=s.History.Polities[c.AttackerId==c.A?c.B:c.A];
                if(TryHistoricalDependency(s,patron,client,true))c.Outcome+=" After repeated defeats, "+client.Name+" renews tributary obligations to "+patron.Name+" while retaining its local court.";
            }
            HistoryEvent(s,"campaign-end",c.Name+" ends: "+c.Outcome+" "+c.Conquests+" settlements changed hands; "+(c.CasualtiesA+c.CasualtiesB).ToString("N0",CultureInfo.InvariantCulture)+" levy casualties are recorded.",s.Society.Settlements[s.History.Polities[c.AttackerId].CapitalId].Cell,polity:c.AttackerId,other:c.AttackerId==c.A?c.B:c.A,category:ChronicleCategory.Politics);
        }
        void AdvanceCivicCampaigns(PrehistoryState s,CancellationToken token)
        {
            foreach(var relation in s.History.Relations.Where(r=>r.WarSince>=0).OrderBy(r=>r.A).ThenBy(r=>r.B))
            {
                var c=ActiveCampaign(s,relation);if(c==null)continue;var a=s.History.Polities[relation.A];var b=s.History.Polities[relation.B];var leader=CivicLeader(s,c.AttackerId);
                if(a.EndedYear>=0||b.EndedYear>=0){EndCivicCampaign(s,relation,"One belligerent has lost its independent state.");relation.WarSince=-1;continue;}
                if(s.Years-c.LastActionYear>=(leader.Military>=850?2:5)){HistoricalBattle(s,relation,token);c.LastActionYear=s.Years;}
            }
        }
        void CivicCasualties(PrehistoryState s,HistoricalPolity p,int losses)
        {
            var patches=RuralPolityPatches(s,p.Id);
            var towns=s.Society.Settlements.Where(t=>t.PolityId==p.Id&&t.Population>0).OrderBy(t=>t.Id).ToArray();long total=towns.Sum(t=>t.Population)+patches.Values.Sum(),remaining=Math.Min(losses,total);
            foreach(var town in towns){long old=town.Population,take=total==0?0:(long)((decimal)remaining*old/total);town.Population-=take;remaining-=take;total-=old;}
            foreach(var pair in patches){long take=total==0?0:(long)((decimal)remaining*pair.Value/total);take=Math.Min(take,s.Population[pair.Key]);s.Population[pair.Key]-=take;remaining-=take;total-=pair.Value;}
        }
        void TransferCivicHinterland(PrehistoryState s,int marketTown,int from,int to,CancellationToken token=default)
        {
            var h=s.History;
            if(TerritorialHistory(s)){TransferAdministrativeDistrict(s,marketTown,from,to,token);return;}
            foreach(int i in PolityCells(s,from).ToArray())if(h.CellMarket[i]==marketTown)SetPoliticalOwner(s,i,to);
            // Declined sites can lack their own market while remaining in the archive.
            foreach(var t in s.Society.Settlements)if(t.PolityId==from&&h.CellPolity[t.Cell]==to){t.PolityId=to;t.Government=h.Polities[to].Government;}
        }
        void HistoricalBattleCivic(PrehistoryState s,HistoricalRelation relation,CancellationToken token)
        {
            var h=s.History;var campaign=ActiveCampaign(s,relation);if(campaign==null)return;
            var attacker=h.Polities[campaign.AttackerId];var defender=h.Polities[campaign.AttackerId==relation.A?relation.B:relation.A];var towns=s.Society.Settlements;
            // Initiating a campaign does not permanently reserve the initiative.
            // Counteroffensives keep the original declaration/aim in the archive.
            if(TerritorialHistory(s)&&campaign.Aim!=HistoricalWarAim.Tribute&&campaign.Aim!=HistoricalWarAim.Liberation&&HistoryRoll("counteroffensive",campaign.Id,s.Years)<.45)
            {var swap=attacker;attacker=defender;defender=swap;}
            var sources=towns.Where(t=>t.PolityId==attacker.Id&&t.Population>=100).ToArray();if(sources.Length==0)return;
            var candidates=towns.Where(t=>t.PolityId==defender.Id&&t.Population>=100).OrderBy(t=>sources.Min(a=>D.Angle(World.Surface.Cells[a.Cell].Position,World.Surface.Cells[t.Cell].Position))).ThenBy(t=>t.Id).ToArray();
            Settlement target=null,source=null;
            foreach(var candidate in candidates)
            {
                foreach(var departure in sources.OrderBy(t=>D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[candidate.Cell].Position)).ThenBy(t=>t.Id))
                {
                    if(!ReachableHistoricalContact(s,departure.Cell,candidate.Cell,attacker.CultureId,token))continue;
                    target=candidate;source=departure;break;
                }
                if(target!=null)break;
            }
            if(target==null)return;
            if(ModernHistory(s))RecordCivicRivalry(s,source,target,campaign.Id,300);
            double distance=D.Angle(World.Surface.Cells[source.Cell].Position,World.Surface.Cells[target.Cell].Position)*World.Recipe.RadiusKm;
            double Power(HistoricalPolity p)
            {
                var leader=CivicLeader(s,p.Id);var tech=h.Cultures[p.CultureId];int fronts=Math.Max(1,h.Relations.Count(r=>r.WarSince>=0&&(r.A==p.Id||r.B==p.Id)));
                double allies=h.Relations.Where(r=>r.Alliance&&(r.A==p.Id||r.B==p.Id)).Sum(r=>h.Polities[r.A==p.Id?r.B:r.A].Army*(ExperimentalHistory(s)&&HistoricalClientEffects.AreLinked(h.Dynamics.Dependencies,r.A,r.B)?.20:.10));
                return (p.Army/fronts+allies)*(1+(HistoricalTechnologies.Has(tech,HistoricalTechnology.Bronze)?.2:0)+(HistoricalTechnologies.Has(tech,HistoricalTechnology.Iron)?.25:0)+(HistoricalTechnologies.Has(tech,HistoricalTechnology.Steel)?.25:0))*(.5+leader.Military/700.0)*(.45+.55*p.Cohesion/1000.0)*(ModernHistory(s)?HistoricalModernEffects.Military(tech,s.Years):1);
            }
            var attackingLeader=CivicLeader(s,attacker.Id);var defendingLeader=CivicLeader(s,defender.Id);
            double supply=1/(1+distance/(400+attackingLeader.Administration*.6));
            if(ModernHistory(s))supply=1/(1+distance/((400+attackingLeader.Administration*.6)*HistoricalModernEffects.LandTransport(h.Cultures[attacker.CultureId],s.Years)));
            double defense=1.1+World.Ecology.Habitat[target.Cell].ReliefM/2500.0+h.Towns[target.Id].Walls/1400.0;
            double attackingPower=Power(attacker)*supply,defendingPower=Power(defender)*defense;
            if(TerritorialHistory(s))
            {
                // Levies are distributed across a realm, not all permanently at
                // whichever frontier town happens to be attacked this year.
                double Concentration(HistoricalPolity p,int cell)
                {
                    long total=0,near=0;foreach(var town in towns.Where(t=>t.PolityId==p.Id&&t.Population>=100))
                    {total+=town.Population;if(D.Angle(World.Surface.Cells[town.Cell].Position,World.Surface.Cells[cell].Position)*World.Recipe.RadiusKm<350)near+=town.Population;}
                    return .30+.70*near/(double)Math.Max(1,total);
                }
                attackingPower*=Concentration(attacker,source.Cell);defendingPower*=Concentration(defender,target.Cell);
            }
            bool victory=TerritorialHistory(s)?HistoryRoll("territorial-battle",campaign.Id,s.Years)<HistoricalTerritorialEffects.BattleChance(attackingPower,defendingPower):attackingPower*(.75+.5*HistoryRoll("civic-battle",campaign.Id,s.Years))>defendingPower;
            foreach(var p in new[]{attacker,defender})
            {
                int losses=(int)(p.Army*(p==attacker&&!victory?.14:p==defender&&victory?.12:.06));p.Army-=losses;CivicCasualties(s,p,losses);p.Exhaustion=Math.Min(1000,p.Exhaustion+50);
                if(p.Id==campaign.A)campaign.CasualtiesA+=losses;else campaign.CasualtiesB+=losses;
            }
            relation.LastBattle=s.Years;
            if(!victory){campaign.Repulses++;attackingLeader.Defeats++;defendingLeader.Victories++;defendingLeader.Renown=Math.Min(1000,defendingLeader.Renown+20);HistoryEvent(s,"battle",defendingLeader.Name+" repels "+attackingLeader.Name+" near "+target.Name+".",target.Cell,target.CultureId,defender.Id,attacker.Id,target.Id,category:ChronicleCategory.Politics);return;}
            if(ExperimentalHistory(s)&&campaign.Aim==HistoricalWarAim.Liberation)
            {
                attackingLeader.Victories++;attackingLeader.Renown=Math.Min(1000,attackingLeader.Renown+70);defendingLeader.Defeats++;
                EndCivicCampaign(s,relation,attacker.Name+" withstands the attempt to enforce tributary obligations and secures its autonomy without annexing "+target.Name+".");
                relation.WarSince=-1;relation.TruceUntil=s.Years+40;relation.Trust=-40;return;
            }
            if(campaign.Aim==HistoricalWarAim.Tribute)
            {
                long paid=HistoricalLeadershipEffects.TransferableFunds(defender.Treasury,attacker.Treasury,Math.Min(defender.Treasury/4,defender.Population/5));
                if(paid>0)
                {
                    defender.Treasury-=paid;attacker.Treasury+=paid;campaign.TributePaid=paid;campaign.TributeRecipientId=attacker.Id;
                    attackingLeader.Victories++;attackingLeader.Renown=Math.Min(1000,attackingLeader.Renown+40);defendingLeader.Defeats++;
                    attacker.Legitimacy=Math.Min(1000,attacker.Legitimacy+25);defender.Legitimacy=Math.Max(100,defender.Legitimacy-45);
                    bool dependent=ExperimentalHistory(s)&&TryHistoricalDependency(s,attacker,defender,true);
                    EndCivicCampaign(s,relation,defender.Name+" pays "+paid.ToString("N0",CultureInfo.InvariantCulture)+" account units to "+attacker.Name+" after defeat near "+target.Name+(dependent?" and accepts recurring tributary obligations; its local court and territory survive.":"; its court retains independence."));
                    relation.WarSince=-1;relation.TruceUntil=s.Years+40;relation.Trust=-20;return;
                }
            }
            bool capital=target.Id==defender.CapitalId;target.PolityId=attacker.Id;target.Stability=Math.Max(0,target.Stability-200);SetPoliticalOwner(s,target.Cell,attacker.Id);attacker.Legitimacy=Math.Min(1000,attacker.Legitimacy+45);
            if(TerritorialHistory(s)){h.Towns[target.Id].UnrestPolityId=attacker.Id;h.Towns[target.Id].AutonomyPressure=0;h.Towns[target.Id].LastAutonomyActionYear=s.Years;}
            long spoils=HistoricalLeadershipEffects.TransferableFunds(defender.Treasury,attacker.Treasury,Math.Min(defender.Treasury/10,target.Population/2));defender.Treasury-=spoils;attacker.Treasury+=spoils;campaign.Spoils+=spoils;
            TransferCivicHinterland(s,target.Id,defender.Id,attacker.Id,token);
            if(DelegatedHistory(s))RetainConqueredAdministration(s,target,defender,attacker,capital,token);
            attackingLeader.Victories++;attackingLeader.Renown=Math.Min(1000,attackingLeader.Renown+(capital?100:45));defendingLeader.Defeats++;campaign.Conquests++;campaign.CapturedSettlements=campaign.CapturedSettlements.Concat(new[]{target.Id}).ToArray();
            if(RealismHistory(s)){var ds=h.Dynamics.States[attacker.Id];var cycle=ds.Cycle??(ds.Cycle=new HistoricalPoliticalCycle());cycle.ConquestDebt=Math.Min(1000,cycle.ConquestDebt+(capital?160:90));cycle.LastConquestYear=s.Years;}
            HistoryEvent(s,capital||attackingLeader.Renown>=250?"great-conquest":"conquest",attackingLeader.Name+" captures "+target.Name+(capital?", the capital of ":" from ")+defender.Name+" in the "+campaign.Name+".",target.Cell,target.CultureId,attacker.Id,defender.Id,target.Id,category:ChronicleCategory.Politics);
            if(!towns.Any(t=>t.PolityId==defender.Id&&t.Population>=100)){EndCivicCampaign(s,relation,defender.Name+" loses its remaining urban centers.");EndPolity(s,defender,"Its last city falls to "+attacker.Name+".");}
            else if(towns[defender.CapitalId].PolityId!=defender.Id){defender.CapitalId=towns.Where(t=>t.PolityId==defender.Id&&t.Population>=100).OrderByDescending(t=>t.Population).ThenBy(t=>t.Id).First().Id;HistoryEvent(s,"capital-moved",defender.Name+" moves its court to "+towns[defender.CapitalId].Name+".",towns[defender.CapitalId].Cell,defender.CultureId,defender.Id,category:ChronicleCategory.Politics);}
        }
    }
}
