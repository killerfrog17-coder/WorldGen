using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalAdministrativeAccount
    {
        public int PolityId,Year,ReachM,MaximumServiceDistanceM;
        public long RuralReceipts,ServiceCost,DisconnectedResidents;
        [System.Runtime.Serialization.OptionalField] public long UrbanReceipts,TaxableUrbanResidents,DisconnectedUrbanResidents;
        public HistoricalAdministrativeAccount Copy()=>(HistoricalAdministrativeAccount)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalIncorporation
    {
        public int Id,PolityId,Cell,StartedYear,DueYear,EndedYear=-1;
        public long SetupCost,AnnualCost;
        public string Outcome="Ongoing local negotiations and administrative establishment";
        [System.Runtime.Serialization.OptionalField] public int[] Cells;
        public HistoricalIncorporation Copy(){var copy=(HistoricalIncorporation)MemberwiseClone();copy.Cells=Cells==null?null:(int[])Cells.Clone();return copy;}
    }
    [Serializable] public sealed class HistoricalAdministration
    {
        public HistoricalAdministrativeAccount[] Accounts=Array.Empty<HistoricalAdministrativeAccount>();
        public HistoricalIncorporation[] Projects=Array.Empty<HistoricalIncorporation>();
        public HistoricalDistrictOffice[] Offices=Array.Empty<HistoricalDistrictOffice>();
        public HistoricalAdministration Copy()=>new HistoricalAdministration{Accounts=Accounts.Select(a=>a.Copy()).ToArray(),Projects=Projects.Select(p=>p.Copy()).ToArray(),Offices=Offices.Select(o=>o.Copy()).ToArray()};
    }
    [Serializable] public sealed class HistoricalDistrictOffice
    {
        public int Id,PolityId,TownId,FoundedYear,EndedYear=-1;
        public long AnnualCost;
        [System.Runtime.Serialization.OptionalField] public int PredecessorOfficeId=-1,PredecessorPolityId=-1;
        [System.Runtime.Serialization.OnDeserializing] void InitializeInheritance(System.Runtime.Serialization.StreamingContext context){PredecessorOfficeId=-1;PredecessorPolityId=-1;}
        public HistoricalDistrictOffice Copy()=>(HistoricalDistrictOffice)MemberwiseClone();
    }
    public sealed partial class Prehistory
    {
        sealed class AdministrativeNetwork
        {
            public int[] Owner,Distance,CommunicationOwner,ResidentStrain;public HistoricalAdministrativeAccount[] Accounts;
        }
        Dictionary<int,List<Route>> administrativeSeaRoutes;
        void PrepareAdministrativeSeaRoutes()
        {
            if(administrativeSeaRoutes!=null)return;administrativeSeaRoutes=new Dictionary<int,List<Route>>();
            foreach(var r in Routes)if(r.Sea&&(!r.Shallow||r.Coastal)&&r.CellA>=0&&r.CellB>=0)
            {
                foreach(int cell in new[]{r.CellA,r.CellB}){if(!administrativeSeaRoutes.TryGetValue(cell,out var list)){list=new List<Route>();administrativeSeaRoutes.Add(cell,list);}list.Add(r);}
            }
        }
        int AdministrativeEdgeCost(PrehistoryState s,int from,int to,int metres,HistoricalCulture culture)
        {
            var a=World.Ecology.Habitat[to];double cost=1+a.ReliefM/900.0;
            if(a.Habitat.HasFlag(Habitat.Forest))cost*=1.35;
            if(HistoricalTechnologies.Has(culture,HistoricalTechnology.Roads))cost*=.7;
            if(ModernHistory(s))cost/=HistoricalModernEffects.LandTransport(culture,s.Years);
            bool river=(World.Hydrology.Receiver[from]==to||World.Hydrology.Receiver[to]==from)&&HistoricalRiverAccess(from)>.01&&HistoricalRiverAccess(to)>.01;
            if(river)cost*=EmergenceHistory(s)?HistoricalTechnologies.Has(culture,HistoricalTechnology.Sailing)?.35:.65:.22;
            if(s.History.CellPolity[to]<0)cost*=1.25; // Unadministered transit is less secure.
            return Math.Max(1,(int)Math.Round(metres*cost));
        }
        AdministrativeNetwork BuildAdministrativeNetwork(PrehistoryState s,CancellationToken token)
        {
            if(DelegatedHistory(s))return BuildDelegatedAdministrativeNetwork(s,token);
            PrepareHistoricalEnvironment();PrepareAdministrativeSeaRoutes();int n=World.Surface.Cells.Length;
            var h=s.History;var result=new AdministrativeNetwork{Owner=Enumerable.Repeat(-1,n).ToArray(),Distance=Enumerable.Repeat(int.MaxValue,n).ToArray(),Accounts=new HistoricalAdministrativeAccount[h.Polities.Length]};
            foreach(var p in h.Polities)
            {
                var account=new HistoricalAdministrativeAccount{PolityId=p.Id,Year=s.Years};result.Accounts[p.Id]=account;if(p.EndedYear>=0)continue;
                account.ReachM=(int)Math.Round(CivicReachKm(s,p)*1000);var hc=h.Cultures[p.CultureId];int origin=s.Society.Settlements[p.CapitalId].Cell;
                var distance=new Dictionary<int,int>{{origin,0}};var heap=new Heap();heap.Push(0,origin);
                void Offer(int cell,long d)
                {
                    if(cell<0||CellPatch[cell]<0||World.Hydrology.LakeCoverageFraction[cell]>=.9||h.CellPolity[cell]>=0&&h.CellPolity[cell]!=p.Id)return;
                    double radius=Math.Sqrt(World.Surface.Cells[cell].AreaM2/Math.PI);
                    if(d>account.ReachM+radius||distance.TryGetValue(cell,out int prior)&&d>=prior)return;
                    distance[cell]=(int)d;heap.Push(d,cell);
                }
                while(heap.Count>0)
                {
                    token.ThrowIfCancellationRequested();var item=heap.Pop();if(distance[item.Id]!=item.Key)continue;int cell=item.Id;
                    if(h.CellPolity[cell]==p.Id||h.CellPolity[cell]<0&&(item.Key<result.Distance[cell]||item.Key==result.Distance[cell]&&(result.Owner[cell]<0||p.Id<result.Owner[cell])))
                    {result.Owner[cell]=p.Id;result.Distance[cell]=(int)item.Key;}
                    var geo=World.Surface.Cells[cell];
                    for(int k=0;k<geo.Neighbors.Length;k++){int to=geo.Neighbors[k];if(World.Hydrology.OceanConnected[to])continue;Offer(to,item.Key+AdministrativeEdgeCost(s,cell,to,geo.DistanceM[k],hc));}
                    if(HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing)&&administrativeSeaRoutes.TryGetValue(cell,out var routes))foreach(var route in routes)
                    {
                        bool navigation=HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation);if(route.DistanceM>(navigation?700000:220000))continue;
                        int to=route.CellA==cell?route.CellB:route.CellA;Offer(to,item.Key+(long)Math.Round(route.DistanceM*(navigation?.12:.25))+12000);
                    }
                }
            }
            for(int cell=0;cell<n;cell++)
            {
                if((cell&2047)==0)token.ThrowIfCancellationRequested();int owner=h.CellPolity[cell];if(owner<0||h.Polities[owner].EndedYear>=0)continue;
                var account=result.Accounts[owner];long people=RuralCellPopulation(s,cell);bool supported=result.Owner[cell]==owner;
                int distance=supported?result.Distance[cell]:account.ReachM*3;
                account.MaximumServiceDistanceM=Math.Max(account.MaximumServiceDistanceM,distance);
                if(!supported)account.DisconnectedResidents+=people;
                account.RuralReceipts+=supported?DistrictReceipts(s,owner,cell,distance,account.ReachM):0;
                account.ServiceCost+=DistrictServiceCost(s,owner,cell,distance,account.ReachM);
            }
            if(h.Administration!=null)foreach(var office in h.Administration.Offices.Where(o=>o.EndedYear<0))result.Accounts[office.PolityId].ServiceCost+=office.AnnualCost;
            return result;
        }
        void AdvanceDistrictOffices(PrehistoryState s,CancellationToken token)
        {
            var h=s.History;if(h.Polities.Length==0)return;var a=h.Administration??(h.Administration=new HistoricalAdministration());
            var delegated=DelegatedHistory(s)?BuildAdministrativeNetwork(s,token):null;
            foreach(var office in a.Offices.Where(o=>o.EndedYear<0))
            {
                var p=h.Polities[office.PolityId];var t=s.Society.Settlements[office.TownId];
                if(p.EndedYear>=0||t.PolityId!=p.Id||t.Population<3000||p.Treasury<office.AnnualCost||delegated!=null&&delegated.CommunicationOwner[t.Cell]!=p.Id){office.EndedYear=s.Years;HistoryEvent(s,"district-office-closed",p.Name+" can no longer sustain its regional administrative office at "+t.Name+".",t.Cell,t.CultureId,p.Id,town:t.Id);}
            }
            if(s.Years%10!=0)return;var network=BuildAdministrativeNetwork(s,token);
            foreach(var p in h.Polities.Where(p=>p.EndedYear<0&&p.Cohesion>=550))
            {
                token.ThrowIfCancellationRequested();if(!HistoricalTechnologies.Has(h.Cultures[p.CultureId],HistoricalTechnology.Writing))continue;
                var candidates=s.Society.Settlements.Where(t=>t.PolityId==p.Id&&t.Id!=p.CapitalId&&t.Population>=5000&&s.Years-t.FoundedYear>=150&&(delegated!=null?network.CommunicationOwner[t.Cell]==p.Id:network.Owner[t.Cell]==p.Id)&&!a.Offices.Any(o=>o.TownId==t.Id&&o.EndedYear<0));
                if(DelegatedHistory(s))candidates=candidates.Where(t=>s.Years-DistrictHeldSince(s,p,t)>=75&&p.Treasury>=400+t.Population/30+(25+t.Population/200)*10);
                var town=candidates.OrderByDescending(t=>t.Population).ThenBy(t=>t.Id).FirstOrDefault();
                if(town==null)continue;
                int heldSince=a.Projects.Where(x=>x.Cell==town.Cell&&x.PolityId==p.Id&&x.Outcome=="Local administration established").Select(x=>x.EndedYear).DefaultIfEmpty(Math.Max(p.FoundedYear,h.Towns[town.Id].LastAutonomyActionYear)).Max();
                if(DelegatedHistory(s))heldSince=Math.Max(heldSince,Math.Max(p.FoundedYear,h.Towns[town.Id].LastAutonomyActionYear));
                long setup=400+town.Population/30,cost=25+town.Population/200;
                if(s.Years-heldSince<75||p.Treasury<setup+cost*10||HistoryRoll("district-office-10",town.Id,s.Years)>.2)continue;
                p.Treasury-=setup;a.Offices=a.Offices.Concat(new[]{new HistoricalDistrictOffice{Id=a.Offices.Length,PolityId=p.Id,TownId=town.Id,FoundedYear=s.Years,AnnualCost=cost}}).ToArray();
                HistoryEvent(s,"district-office",p.Name+" establishes a staffed regional office at "+town.Name+", extending its administrative network through accounts and delegated officials.",town.Cell,town.CultureId,p.Id,town:town.Id,category:ChronicleCategory.Society);
            }
        }
        int DistrictHeldSince(PrehistoryState s,HistoricalPolity p,Settlement town)
            =>Math.Max(Math.Max(p.FoundedYear,s.History.Towns[town.Id].LastAutonomyActionYear),s.History.Administration.Projects.Where(x=>(x.Cells?.Contains(town.Cell)??x.Cell==town.Cell)&&x.PolityId==p.Id&&x.Outcome=="Local administration established").Select(x=>x.EndedYear).DefaultIfEmpty(p.FoundedYear).Max());
        long DistrictReceipts(PrehistoryState s,int owner,int cell,int distance,int reach,long ruralResidents=-1)
        {
            int local=s.Society.CellCulture[cell];if(local<0)return 0;
            double fiscal=LocalFiscalPotential(s,local,cell),travel=Math.Exp(-distance/(double)Math.Max(1,reach));
            double administration=.35+.65*CivicLeader(s,owner).Administration/1000.0;
            double rate=ModernHistory(s)?HistoricalModernEffects.RuralReceiptRate(s.History.Cultures[s.History.Polities[owner].CultureId],s.Years):.004;
            // Established farms finance local service as well as their towns.
            // The former low fraction made even many reachable agrarian
            // hinterlands a permanent net loss. Foragers gain no such bonus.
            if(RealismHistory(s))rate*=1+2*D.Clamp(s.Society.Cultures[local].Farming/850.0);
            return Math.Max(0,(long)Math.Floor(D.Q((ruralResidents<0?RuralCellPopulation(s,cell):ruralResidents)*rate*fiscal*administration*travel,1000000)));
        }
        long DistrictServiceCost(PrehistoryState s,int owner,int cell,int distance,int reach,long ruralResidents=-1)
        {
            double travel=distance/(double)Math.Max(1,reach);int local=s.Society.CellCulture[cell];
            double diversity=local>=0&&local!=s.History.Polities[owner].CultureId?.7:0;
            double landCost=DelegatedHistory(s)?HistoricalDistrictEffects.LandServiceCost(HistoricalLandArea(cell),travel):.35*Math.Sqrt(HistoricalLandArea(cell))*(1+.5*travel);
            double cost=(ruralResidents<0?RuralCellPopulation(s,cell):ruralResidents)*.001*(.25+2.5*travel*travel+diversity)+landCost;
            return Math.Max(1,(long)Math.Ceiling(D.Q(cost,1000000)));
        }
        double DistrictBenefit(PrehistoryState s,int owner,int cell,int distance,int reach,long? urbanResidents=null)
        {
            int local=s.Society.CellCulture[cell];if(local<0)return DelegatedHistory(s)?-DistrictServiceCost(s,owner,cell,distance,reach):0;
            double crop=RegionalFoodDensity(s,s.Society.Cultures[local],cell);
            double spare=Math.Max(0,HistoricalLandArea(cell)*crop-RuralCellPopulation(s,cell));
            // Nearby food surpluses and taxable residents are distinct benefits.
            // A distant productive field is not automatically a usable food source.
            double provisioning=distance<80000?Math.Min(150,spare*.01)*Math.Exp(-distance/80000.0):0;
            long urban=urbanResidents??s.Society.Settlements.Where(t=>t.Cell==cell&&t.Population>=100).Sum(t=>t.Population);
            return DistrictReceipts(s,owner,cell,distance,reach)+urban/20.0+provisioning-DistrictServiceCost(s,owner,cell,distance,reach);
        }
        void AdvanceIncorporations(PrehistoryState s,CancellationToken token)
        {
            if(DelegatedHistory(s)){AdvanceDistrictIncorporations(s,token);return;}
            var a=s.History.Administration;if(a==null||!a.Projects.Any(p=>p.EndedYear<0))return;
            var network=BuildAdministrativeNetwork(s,token);
            foreach(var project in a.Projects.Where(p=>p.EndedYear<0).OrderBy(p=>p.Id))
            {
                token.ThrowIfCancellationRequested();var polity=s.History.Polities[project.PolityId];
                bool viable=polity.EndedYear<0&&s.History.CellPolity[project.Cell]<0&&network.Owner[project.Cell]==polity.Id&&polity.Treasury>=project.AnnualCost&&polity.Cohesion>=350;
                if(!viable){project.EndedYear=s.Years;project.Outcome="Abandoned when political access, funds or administrative support failed";HistoryEvent(s,"incorporation-abandoned",polity.Name+" abandons its effort to establish administration in an outlying district.",project.Cell,polity.CultureId,polity.Id);continue;}
                polity.Treasury-=project.AnnualCost;if(s.Years<project.DueYear)continue;
                project.EndedYear=s.Years;project.Outcome="Local administration established";SetPoliticalOwner(s,project.Cell,polity.Id);
                var town=s.Society.Settlements.FirstOrDefault(t=>t.Cell==project.Cell);
                if(town!=null){town.PolityId=polity.Id;town.Government=polity.Government;s.History.Towns[town.Id].UnrestPolityId=polity.Id;s.History.Towns[town.Id].AutonomyPressure=0;}
                HistoryEvent(s,"district-incorporated",polity.Name+" establishes district administration after years of local negotiation, provisioning and enforcement"+(town==null?".":" around "+town.Name+"."),project.Cell,polity.CultureId,polity.Id,town:town?.Id??-1,category:ChronicleCategory.Politics);
            }
        }
        bool BeginSupportedIncorporation(PrehistoryState s,HistoricalPolity p,int cell,AdministrativeNetwork network)
        {
            if(DelegatedHistory(s))return BeginDistrictIncorporation(s,p,cell,network);
            var h=s.History;var a=h.Administration??(h.Administration=new HistoricalAdministration());
            if(p.EndedYear>=0||h.CellPolity[cell]>=0||network.Owner[cell]!=p.Id||a.Projects.Any(x=>x.Cell==cell&&x.EndedYear<0)||p.Cohesion<500||h.Dynamics.States[p.Id].Strain>350)return false;
            long urban=s.Society.Settlements.Where(t=>t.PolityId==p.Id&&t.Population>=100).Sum(t=>t.Population);var hc=h.Cultures[p.CultureId];
            int workforce=Math.Max(1,(int)Math.Min(8,urban/8000+(HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing)?1:0)));
            if(a.Projects.Count(x=>x.PolityId==p.Id&&x.EndedYear<0)>=workforce)return false;
            int local=s.Society.CellCulture[cell];double resistance=local>=0&&local!=p.CultureId?s.Society.Cultures[local].Organization/1000.0:0;
            long annual=DistrictServiceCost(s,p.Id,cell,network.Distance[cell],network.Accounts[p.Id].ReachM)+5+(long)(20*resistance);
            long setup=150+network.Distance[cell]/500+(long)(250*resistance);
            if(p.Treasury<setup+annual*10||DistrictBenefit(s,p.Id,cell,network.Distance[cell],network.Accounts[p.Id].ReachM)<=0)return false;
            int id=a.Projects.Length,duration=15+(int)(30*HistoryRoll("incorporation-duration-10",id,s.Years))+(int)(30*resistance);
            var project=new HistoricalIncorporation{Id=id,PolityId=p.Id,Cell=cell,StartedYear=s.Years,DueYear=s.Years+duration,SetupCost=setup,AnnualCost=annual};p.Treasury-=setup;
            a.Projects=a.Projects.Concat(new[]{project}).ToArray();return true;
        }
        void PlanSupportedFrontiers(PrehistoryState s,int[] influence,CancellationToken token)
        {
            if(DelegatedHistory(s)){PlanAdministrativeDistricts(s,token);return;}
            var h=s.History;var a=h.Administration??(h.Administration=new HistoricalAdministration());var network=BuildAdministrativeNetwork(s,token);
            var frontier=new SortedSet<int>();foreach(int cell in PoliticalCells(s))foreach(int neighbor in World.Surface.Cells[cell].Neighbors)if(h.CellPolity[neighbor]<0)frontier.Add(neighbor);
            foreach(var p in h.Polities.Where(p=>p.EndedYear<0))
            {
                token.ThrowIfCancellationRequested();var hc=h.Cultures[p.CultureId];
                if(HistoricalTechnologies.Has(hc,HistoricalTechnology.Sailing))foreach(var town in s.Society.Settlements.Where(t=>t.PolityId==p.Id&&t.Population>=100))if(administrativeSeaRoutes.TryGetValue(town.Cell,out var routes))foreach(var route in routes)frontier.Add(route.CellA==town.Cell?route.CellB:route.CellA);
                long urban=s.Society.Settlements.Where(t=>t.PolityId==p.Id&&t.Population>=100).Sum(t=>t.Population);
                int workforce=Math.Max(1,(int)Math.Min(8,urban/8000+(HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing)?1:0)));
                if(a.Projects.Count(x=>x.PolityId==p.Id&&x.EndedYear<0)>=workforce||p.Cohesion<500||h.Dynamics.States[p.Id].Strain>350)continue;
                int best=-1;double bestValue=0;
                foreach(int cell in frontier)
                {
                    if(network.Owner[cell]!=p.Id||CellPatch[cell]<0||h.CellPolity[cell]>=0||World.Hydrology.LakeCoverageFraction[cell]>=.9||a.Projects.Any(x=>x.Cell==cell&&x.EndedYear<0)||RuralCellPopulation(s,cell)<100)continue;
                    double value=DistrictBenefit(s,p.Id,cell,network.Distance[cell],network.Accounts[p.Id].ReachM);
                    if(value>bestValue){best=cell;bestValue=value;}
                }
                if(best<0)continue;int local=s.Society.CellCulture[best];double resistance=local>=0&&local!=p.CultureId?s.Society.Cultures[local].Organization/1000.0:0;
                if(HistoryRoll("supported-incorporation-10",p.Id,s.Years)>.15*D.Clamp(bestValue/80)*D.Clamp(p.Legitimacy/700.0)*(1-.7*resistance))continue;
                BeginSupportedIncorporation(s,p,best,network);
            }
        }
    }
}
