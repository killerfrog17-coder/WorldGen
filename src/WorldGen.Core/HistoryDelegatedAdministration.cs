using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    // Transport-effort priors, not measured travel times. Local service and
    // capital communication are deliberately different constraints.
    public static class HistoricalDelegationEffects
    {
        public static int CommunicationReachM(int localReach,bool writing,bool roads,int publicWorks)
            =>(int)Math.Round(D.Q(localReach*(writing?4:1)*(roads?2:1)*(1+.35*D.Clamp(publicWorks/1000.0)),1000000));
        public static int RelayOverheadM(int capitalDistance)
            =>Math.Max(0,(int)Math.Round(D.Q(capitalDistance*.08,1000000)));
        public static int IntegrationStrain(int maximumServiceDistance,int localReach,int culturalCenters)
            =>Math.Min(700,(int)Math.Round(300*Math.Max(0,maximumServiceDistance/(double)Math.Max(1,localReach)-.7))+Math.Max(0,culturalCenters-1)*25);
    }
    public sealed partial class Prehistory
    {
        int[] delegatedNetworkSignature;AdministrativeNetwork delegatedConnectivity;
        HistoricalWorkIndex delegatedIndexedSource;
        double[] administrativeTerrainCosts,administrativeCellRadii;
        void PrepareAdministrativeGeometry()
        {
            if(administrativeTerrainCosts!=null)return;
            int count=World.Surface.Cells.Length;var costs=new double[count];var radii=new double[count];
            for(int i=0;i<count;i++)
            {
                var habitat=World.Ecology.Habitat[i];double cost=1+habitat.ReliefM/900.0;
                if((habitat.Habitat&Habitat.Forest)!=0)cost*=1.35;
                costs[i]=cost;radii[i]=Math.Sqrt(World.Surface.Cells[i].AreaM2/Math.PI);
            }
            administrativeTerrainCosts=costs;administrativeCellRadii=radii;
        }
        // Independent searches depend on the actor's transport capabilities and
        // the shared control/barrier graph, not on another actor's changing
        // budget or ruler. Keep immutable sorted search results between calls.
        int[] delegatedTopologySignature,delegatedControlSnapshot;
        Dictionary<int,int[]> delegatedShippingSnapshot=new Dictionary<int,int[]>();
        readonly Dictionary<int,DelegatedSearchCache> delegatedSearches=new Dictionary<int,DelegatedSearchCache>();
        int maximumWorkers=4;
        // Execution preference only, never part of a recipe or model decision.
        public int MaximumWorkers {get=>maximumWorkers;set{if(value<1||value>4)throw new ArgumentOutOfRangeException(nameof(value));maximumWorkers=value;}}
        readonly System.Collections.Concurrent.ConcurrentBag<DelegatedWorkspace> delegatedWorkspaces=new System.Collections.Concurrent.ConcurrentBag<DelegatedWorkspace>();
        sealed class DelegatedWorkspace
        {
            public readonly int[] Distance,Visit,Connected,MessageDistance,DependencyVisit;public int Serial;
            public readonly Heap Frontier=new Heap();
            public readonly List<int> Visited=new List<int>(),Dependencies=new List<int>();
            public readonly List<(int Cell,long Cost)> Blocked=new List<(int,long)>();
            public DelegatedWorkspace(int count){Distance=new int[count];Visit=new int[count];Connected=new int[count];MessageDistance=new int[count];DependencyVisit=new int[count];}
        }
        sealed class DelegatedWorkspaceLease:IDisposable
        {
            readonly System.Collections.Concurrent.ConcurrentBag<DelegatedWorkspace> pool;public readonly DelegatedWorkspace Work;
            public DelegatedWorkspaceLease(System.Collections.Concurrent.ConcurrentBag<DelegatedWorkspace> pool,int count){this.pool=pool;Work=pool.TryTake(out var work)?work:new DelegatedWorkspace(count);}
            public void Dispose()=>pool.Add(Work);
        }
        sealed class DelegatedSearchCache
        {
            public int[] Key,MessageKey,ServiceOriginKey,MessageCells,Dependencies,Allies;public KeyValuePair<int,int>[] Services,Messages;
            public KeyValuePair<int,long>[] MessageBoundary,ServiceBoundary;
            public int MinimumMessageLimit,MaximumMessageLimit,MinimumServiceLimit,MaximumServiceLimit;
        }
        int DelegatedCommunicationReach(PrehistoryState s,HistoricalPolity p,int reach)
        {
            var c=s.History.Cultures[p.CultureId];int baseline=HistoricalDelegationEffects.CommunicationReachM(reach,HistoricalTechnologies.Has(c,HistoricalTechnology.Writing),HistoricalTechnologies.Has(c,HistoricalTechnology.Roads),s.History.Dynamics.States[p.Id].PublicWorks);
            return ModernHistory(s)?(int)Math.Round(baseline*HistoricalModernEffects.Communication(c,s.Years)):baseline;
        }
        AdministrativeNetwork BuildDelegatedAdministrativeNetwork(PrehistoryState s,CancellationToken token)
        {
            PrepareHistoricalEnvironment();PrepareAdministrativeSeaRoutes();PrepareAdministrativeGeometry();int n=World.Surface.Cells.Length;var h=s.History;
            var urbanByPolity=s.Society.Settlements.Where(t=>t.PolityId>=0&&t.Population>=100).GroupBy(t=>t.PolityId).ToDictionary(g=>g.Key,g=>g.Sum(t=>t.Population));
            var officesByPolity=(h.Administration?.Offices??Array.Empty<HistoricalDistrictOffice>()).GroupBy(o=>o.PolityId).ToDictionary(g=>g.Key,g=>g.ToArray());
            var alliesByPolity=h.Relations.Where(r=>r.Alliance&&r.WarSince<0).SelectMany(r=>new[]{(Owner:r.A,Ally:r.B),(Owner:r.B,Ally:r.A)}).GroupBy(x=>x.Owner).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Ally).OrderBy(id=>id).ToArray());
            long Urban(int polity)=>urbanByPolity.TryGetValue(polity,out long population)?population:0;
            var shippingByCell=new Dictionary<int,List<(int Cell,int Distance)>>();
            if(ModernHistory(s))foreach(var link in h.Modern.Shipping)
            {
                var a=s.Society.Settlements[link.PortA];var b=s.Society.Settlements[link.PortB];if(a.Population<100||b.Population<100)continue;
                void Add(int from,int to){if(!shippingByCell.TryGetValue(from,out var list)){list=new List<(int,int)>();shippingByCell.Add(from,list);}list.Add((to,link.DistanceM));}
                Add(a.Cell,b.Cell);Add(b.Cell,a.Cell);
            }
            token.ThrowIfCancellationRequested();var index=WorkIndex(s);
            // A validated branch rebuilds the work index. Never trust revision
            // numbers from a different branch, even when they happen to match.
            if(!ReferenceEquals(index,delegatedIndexedSource))
            {delegatedIndexedSource=index;delegatedNetworkSignature=null;delegatedTopologySignature=null;}
            var signature=new List<int>((index==null?n:2)+h.Polities.Length*8){h.Polities.Length};if(EmergenceHistory(s))signature.Add(15);
            if(index==null)signature.AddRange(h.CellPolity);
            else {signature.Add(unchecked((int)index.PoliticalRevision));signature.Add((int)(index.PoliticalRevision>>32));}
            foreach(var p in h.Polities)
            {
                int reach=p.EndedYear<0?(int)Math.Round(CivicReachKm(s,p,Urban(p.Id))*1000):0;var c=h.Cultures[p.CultureId];
                signature.Add(p.EndedYear<0?1:0);signature.Add(s.Society.Settlements[p.CapitalId].Cell);signature.Add(reach);
                signature.Add(DelegatedCommunicationReach(s,p,reach));
                if(ModernHistory(s))signature.Add((int)Math.Round(HistoricalModernEffects.LandTransport(c,s.Years)*1000000));
                signature.Add((HistoricalTechnologies.Has(c,HistoricalTechnology.Writing)?1:0)|(HistoricalTechnologies.Has(c,HistoricalTechnology.Roads)?2:0)|(HistoricalTechnologies.Has(c,HistoricalTechnology.Sailing)?4:0)|(HistoricalTechnologies.Has(c,HistoricalTechnology.Navigation)?8:0)|(HistoricalTechnologies.Has(c,HistoricalTechnology.OceanNavigation)?16:0));
            }
            signature.Add(h.Administration?.Offices.Length??0);
            foreach(var office in h.Administration?.Offices??Array.Empty<HistoricalDistrictOffice>())
            {
                var town=s.Society.Settlements[office.TownId];var polity=h.Polities[office.PolityId];
                signature.Add(office.PolityId);signature.Add(town.Cell);signature.Add(office.EndedYear<0&&town.PolityId==polity.Id&&town.Population>=3000&&polity.Treasury>=office.AnnualCost?1:0);
            }
            signature.Add(h.Relations.Length);foreach(var relation in h.Relations.OrderBy(r=>r.A).ThenBy(r=>r.B)){signature.Add(relation.A);signature.Add(relation.B);signature.Add(relation.Alliance&&relation.WarSince<0?1:0);}
            var key=signature.ToArray();
            if(ModernHistory(s)){var extra=shippingByCell.OrderBy(x=>x.Key).SelectMany(x=>new[]{x.Key,x.Value.Count}.Concat(x.Value.SelectMany(route=>new[]{route.Cell,route.Distance}))).ToArray();key=key.Concat(extra).ToArray();}
            if(delegatedNetworkSignature!=null&&key.SequenceEqual(delegatedNetworkSignature))
            {
                var cached=new AdministrativeNetwork{Owner=delegatedConnectivity.Owner,Distance=delegatedConnectivity.Distance,CommunicationOwner=delegatedConnectivity.CommunicationOwner,Accounts=delegatedConnectivity.Accounts.Select(a=>new HistoricalAdministrativeAccount{PolityId=a.PolityId,Year=s.Years,ReachM=a.ReachM}).ToArray()};
                FinishDelegatedAccounts(s,cached,token);return cached;
            }
            var topologyKey=new[]{EmergenceHistory(s)?15:ModernHistory(s)?13:12};
            var shippingSnapshot=shippingByCell.ToDictionary(row=>row.Key,row=>row.Value.SelectMany(route=>new[]{route.Cell,route.Distance}).ToArray());
            if(delegatedTopologySignature==null||!topologyKey.SequenceEqual(delegatedTopologySignature))
            {delegatedSearches.Clear();delegatedTopologySignature=topologyKey;delegatedControlSnapshot=(int[])h.CellPolity.Clone();index?.AdministrativeChanges.Clear();}
            else
            {
                var changed=index==null?new List<int>():index.AdministrativeChanges.ToList();
                if(index==null)
                {for(int cell=0;cell<n;cell++)if(h.CellPolity[cell]!=delegatedControlSnapshot[cell])changed.Add(cell);}
                else index.AdministrativeChanges.Clear();
                // A changed sea edge can affect only searches that reached or
                // offered one of its endpoint rows. Preserve unrelated actors;
                // removing a route and changing its distance matter too.
                var changedPorts=new HashSet<int>();
                foreach(var row in shippingSnapshot)if(!delegatedShippingSnapshot.TryGetValue(row.Key,out var previous)||!row.Value.SequenceEqual(previous)){changed.Add(row.Key);changedPorts.Add(row.Key);}
                foreach(int port in delegatedShippingSnapshot.Keys)if(!shippingSnapshot.ContainsKey(port)){changed.Add(port);changedPorts.Add(port);}
                if(changed.Count>0)
                {
                    var changedSet=changed.Count<32?null:new HashSet<int>(changed);
                    bool Affected(KeyValuePair<int,DelegatedSearchCache> actor)
                    {
                        // A conquest between two unrelated foreign controllers
                        // does not change this court's blocked edge. Conversely,
                        // neutral, own and allied transit have distinct costs.
                        int Access(int owner)=>owner<0?0:owner==actor.Key?1:Array.BinarySearch(actor.Value.Allies,owner)>=0?2:3;
                        bool Changed(int cell)=>changedPorts.Contains(cell)||Access(delegatedControlSnapshot[cell])!=Access(h.CellPolity[cell]);
                        return changedSet==null?changed.Any(cell=>Array.BinarySearch(actor.Value.Dependencies,cell)>=0&&Changed(cell)):actor.Value.Dependencies.Any(cell=>changedSet.Contains(cell)&&Changed(cell));
                    }
                    var invalid=delegatedSearches.Where(Affected).Select(pair=>pair.Key).ToArray();
                    WorkObserver?.Invoke(new SimulationWorkMetric{Stage="administrative topology invalidations",Items=invalid.Length});
                    foreach(int id in invalid)delegatedSearches.Remove(id);
                    foreach(int cell in changed)delegatedControlSnapshot[cell]=h.CellPolity[cell];
                }
            }
            delegatedShippingSnapshot=shippingSnapshot;
            foreach(var ended in h.Polities.Where(p=>p.EndedYear>=0))delegatedSearches.Remove(ended.Id);
            var result=new AdministrativeNetwork{
                Owner=Enumerable.Repeat(-1,n).ToArray(),Distance=Enumerable.Repeat(int.MaxValue,n).ToArray(),
                CommunicationOwner=Enumerable.Repeat(-1,n).ToArray(),Accounts=new HistoricalAdministrativeAccount[h.Polities.Length]};
            var actorResults=new DelegatedSearchCache[h.Polities.Length];var cacheHits=new bool[h.Polities.Length];var searchTimes=WorkObserver==null?null:new double[h.Polities.Length];
            var messageTimes=WorkObserver==null?null:new double[h.Polities.Length];var serviceTimes=WorkObserver==null?null:new double[h.Polities.Length];
            void SolveActor(int polity)
            {
                var p=h.Polities[polity];token.ThrowIfCancellationRequested();var account=new HistoricalAdministrativeAccount{PolityId=p.Id,Year=s.Years};result.Accounts[p.Id]=account;if(p.EndedYear>=0)return;
                var culture=h.Cultures[p.CultureId];bool writing=HistoricalTechnologies.Has(culture,HistoricalTechnology.Writing),roads=HistoricalTechnologies.Has(culture,HistoricalTechnology.Roads);
                bool modern=ModernHistory(s),emergence=EmergenceHistory(s),sailing=HistoricalTechnologies.Has(culture,HistoricalTechnology.Sailing),navigation=HistoricalTechnologies.Has(culture,HistoricalTechnology.Navigation),oceanNavigation=HistoricalTechnologies.Has(culture,HistoricalTechnology.OceanNavigation);
                double landTransport=modern?HistoricalModernEffects.LandTransport(culture,s.Years):1;
                account.ReachM=(int)Math.Round(CivicReachKm(s,p,Urban(p.Id))*1000);
                int communicationReach=DelegatedCommunicationReach(s,p,account.ReachM);
                int capital=s.Society.Settlements[p.CapitalId].Cell;
                var actorKey=new List<int>{capital,account.ReachM,communicationReach,(writing?1:0)|(roads?2:0)|(HistoricalTechnologies.Has(culture,HistoricalTechnology.Sailing)?4:0)|(HistoricalTechnologies.Has(culture,HistoricalTechnology.Navigation)?8:0)|(HistoricalTechnologies.Has(culture,HistoricalTechnology.OceanNavigation)?16:0)};
                if(ModernHistory(s))actorKey.Add((int)Math.Round(HistoricalModernEffects.LandTransport(culture,s.Years)*1000000));
                var actorOffices=officesByPolity.TryGetValue(p.Id,out var localOffices)?localOffices:Array.Empty<HistoricalDistrictOffice>();actorKey.Add(actorOffices.Length);
                foreach(var office in actorOffices)
                {var town=s.Society.Settlements[office.TownId];actorKey.Add(town.Cell);actorKey.Add(office.EndedYear<0&&town.PolityId==p.Id&&town.Population>=3000&&p.Treasury>=office.AnnualCost?1:0);}
                var allies=alliesByPolity.TryGetValue(p.Id,out var localAllies)?localAllies:Array.Empty<int>();actorKey.Add(allies.Length);actorKey.AddRange(allies);
                var actorSignature=actorKey.ToArray();
                if(delegatedSearches.TryGetValue(p.Id,out var prior)&&actorSignature.SequenceEqual(prior.Key))
                {cacheHits[p.Id]=true;actorResults[p.Id]=prior;return;}
                using var workspaceLease=new DelegatedWorkspaceLease(delegatedWorkspaces,n);var workspace=workspaceLease.Work;
                if(workspace.Serial>int.MaxValue-4){Array.Clear(workspace.Visit,0,n);Array.Clear(workspace.Connected,0,n);Array.Clear(workspace.DependencyVisit,0,n);workspace.Serial=0;}
                int actorStamp=++workspace.Serial;var dependencies=workspace.Dependencies;dependencies.Clear();
                void Dependency(int cell){if(workspace.DependencyVisit[cell]!=actorStamp){workspace.DependencyVisit[cell]=actorStamp;dependencies.Add(cell);}}
                var messageKey=new List<int>{capital,actorSignature[3]};if(modern)messageKey.Add((int)Math.Round(landTransport*1000000));messageKey.AddRange(allies);
                var messageSignature=messageKey.ToArray();
                bool sameMessageGraph=UseHistoryIndexes&&prior!=null&&messageSignature.SequenceEqual(prior.MessageKey);
                bool reuseMessages=sameMessageGraph&&communicationReach>=prior.MinimumMessageLimit&&communicationReach<=prior.MaximumMessageLimit;
                var permittedControllers=new bool[h.Polities.Length];permittedControllers[p.Id]=true;
                foreach(int ally in allies)permittedControllers[ally]=true;
                int EdgeCost(int from,int to,int metres)
                {
                    if(!UseHistoryIndexes)return AdministrativeEdgeCost(s,from,to,metres,culture);
                    double cost=administrativeTerrainCosts[to];if(roads)cost*=.7;if(modern)cost/=landTransport;
                    bool river=(World.Hydrology.Receiver[from]==to||World.Hydrology.Receiver[to]==from)&&HistoricalRiverAccess(from)>.01&&HistoricalRiverAccess(to)>.01;
                    if(river)cost*=emergence?sailing?.35:.65:.22;
                    if(h.CellPolity[to]<0)cost*=1.25;
                    return Math.Max(1,(int)Math.Round(metres*cost));
                }
                // A paid office never creates its own unconnected empire. First
                // establish the usable capital route, including foreign barriers.
                (List<int> Cells,int Minimum,int Maximum,KeyValuePair<int,long>[] Boundary) Search(Dictionary<int,int> origins,int limit,bool connected=false,KeyValuePair<int,int>[] resume=null,KeyValuePair<int,long>[] boundary=null)
                {
                    var watch=searchTimes==null?null:System.Diagnostics.Stopwatch.StartNew();
                    int stamp=++workspace.Serial;var visited=workspace.Visited;visited.Clear();var blocked=workspace.Blocked;blocked.Clear();var heap=workspace.Frontier;heap.Clear();
                    if(resume==null)foreach(var origin in origins.OrderBy(x=>x.Key)){Dependency(origin.Key);workspace.Visit[origin.Key]=stamp;workspace.Distance[origin.Key]=origin.Value;visited.Add(origin.Key);heap.Push(origin.Value,origin.Key);}
                    else foreach(var pair in resume){workspace.Visit[pair.Key]=stamp;workspace.Distance[pair.Key]=pair.Value;visited.Add(pair.Key);}
                    void Offer(int cell,long cost)
                    {
                        if(cell<0||CellPatch[cell]<0||World.Hydrology.LakeCoverageFraction[cell]>=.9)return;Dependency(cell);
                        if(h.CellPolity[cell]>=0&&!permittedControllers[h.CellPolity[cell]])return;
                        if(connected&&workspace.Connected[cell]!=actorStamp){blocked.Add((cell,cost));return;}
                        double radius=administrativeCellRadii[cell];
                        bool known=workspace.Visit[cell]==stamp;if(known&&cost>=workspace.Distance[cell])return;
                        if(cost>limit+radius){blocked.Add((cell,cost));return;}
                        if(!known){visited.Add(cell);workspace.Visit[cell]=stamp;}workspace.Distance[cell]=(int)cost;heap.Push(cost,cell);
                    }
                    // With unchanged edge costs/barriers, growing the horizon
                    // can resume at its old rejected boundary. Old settled
                    // distances remain valid; new paths can still improve them.
                    if(boundary!=null)foreach(var edge in boundary)Offer(edge.Key,edge.Value);
                    while(heap.Count>0)
                    {
                        token.ThrowIfCancellationRequested();var item=heap.Pop();if(workspace.Distance[item.Id]!=item.Key)continue;var geo=World.Surface.Cells[item.Id];
                        for(int k=0;k<geo.Neighbors.Length;k++){int to=geo.Neighbors[k];if(!World.Hydrology.OceanConnected[to]){int cost=EdgeCost(item.Id,to,geo.DistanceM[k]);if(h.CellPolity[to]>=0&&h.CellPolity[to]!=p.Id)cost=(int)Math.Round(cost*1.5);Offer(to,item.Key+cost);}}
                        if(sailing&&administrativeSeaRoutes.TryGetValue(item.Id,out var routes))foreach(var route in routes)
                        {
                            if(route.DistanceM>(navigation?700000:220000))continue;
                            Offer(route.CellA==item.Id?route.CellB:route.CellA,item.Key+(long)Math.Round(route.DistanceM*(navigation?.12:.25))+12000);
                        }
                        if(modern&&oceanNavigation&&shippingByCell.TryGetValue(item.Id,out var shipping))foreach(var route in shipping)Offer(route.Cell,item.Key+(long)Math.Round(route.Distance*.06)+30000);
                    }
                    // Exact stability interval, NOT rounding the model's reach.
                    // All settled paths remain admissible above the lower bound;
                    // no excluded boundary node can enter below the upper bound.
                    // A one-metre margin makes floating-point bounds conservative.
                    int minimum=0,maximum=int.MaxValue;
                    foreach(int cell in visited)minimum=Math.Max(minimum,(int)Math.Ceiling(workspace.Distance[cell]-administrativeCellRadii[cell])+1);
                    var frontier=new Dictionary<int,long>();
                    foreach(var edge in blocked)if(workspace.Visit[edge.Cell]!=stamp)
                    {
                        if(!connected||workspace.Connected[edge.Cell]==actorStamp)maximum=Math.Min(maximum,(int)Math.Min(int.MaxValue,Math.Floor(edge.Cost-administrativeCellRadii[edge.Cell])-1));
                        if(!frontier.TryGetValue(edge.Cell,out long old)||edge.Cost<old)frontier[edge.Cell]=edge.Cost;
                    }
                    var remaining=frontier.OrderBy(x=>x.Key).ToArray();
                    if(watch!=null){double elapsed=watch.Elapsed.TotalMilliseconds;searchTimes[p.Id]+=elapsed;if(connected)serviceTimes[p.Id]+=elapsed;else messageTimes[p.Id]+=elapsed;}return(visited,minimum,maximum,remaining);
                }
                bool growingMessages=sameMessageGraph&&communicationReach>=prior.Key[2];
                KeyValuePair<int,int>[] messages;KeyValuePair<int,long>[] messageBoundary;int minimumMessage,maximumMessage;
                if(reuseMessages)
                {
                    messages=prior.Messages;messageBoundary=prior.MessageBoundary;minimumMessage=prior.MinimumMessageLimit;maximumMessage=prior.MaximumMessageLimit;
                    foreach(int cell in prior.Dependencies)Dependency(cell);
                }
                else
                {
                    bool resume=growingMessages;
                    if(resume)foreach(int cell in prior.Dependencies)Dependency(cell);
                    var searched=Search(new Dictionary<int,int>{{capital,0}},communicationReach,false,resume?prior.Messages:null,resume?prior.MessageBoundary:null);minimumMessage=searched.Minimum;maximumMessage=searched.Maximum;messageBoundary=searched.Boundary;
                    messages=searched.Cells.Select(cell=>new KeyValuePair<int,int>(cell,workspace.Distance[cell])).ToArray();
                }
                foreach(var pair in messages){workspace.Connected[pair.Key]=actorStamp;workspace.MessageDistance[pair.Key]=pair.Value;}
                var origins=new Dictionary<int,int>{{capital,0}};
                if(writing&&h.Administration!=null)foreach(var office in actorOffices.Where(o=>o.EndedYear<0).OrderBy(o=>o.Id))
                {
                    var town=s.Society.Settlements[office.TownId];
                    if(town.PolityId!=p.Id||town.Population<3000||p.Treasury<office.AnnualCost||workspace.Connected[town.Cell]!=actorStamp)continue;
                    int messageCost=workspace.MessageDistance[town.Cell];
                    int overhead=HistoricalDelegationEffects.RelayOverheadM(messageCost);if(overhead<=account.ReachM)origins[town.Cell]=overhead;
                }
                var originKey=origins.OrderBy(x=>x.Key).SelectMany(x=>new[]{x.Key,x.Value}).ToArray();
                KeyValuePair<int,int>[] services;KeyValuePair<int,long>[] serviceBoundary;int minimumService,maximumService;
                if(reuseMessages&&originKey.SequenceEqual(prior.ServiceOriginKey)&&account.ReachM>=prior.MinimumServiceLimit&&account.ReachM<=prior.MaximumServiceLimit)
                {services=prior.Services;serviceBoundary=prior.ServiceBoundary;minimumService=prior.MinimumServiceLimit;maximumService=prior.MaximumServiceLimit;}
                else
                {
                    bool resume=(reuseMessages||growingMessages)&&originKey.SequenceEqual(prior.ServiceOriginKey)&&account.ReachM>=prior.Key[1];
                    var searched=Search(origins,account.ReachM,true,resume?prior.Services:null,resume?prior.ServiceBoundary:null);minimumService=searched.Minimum;maximumService=searched.Maximum;serviceBoundary=searched.Boundary;searched.Cells.Sort();
                    services=searched.Cells.Select(cell=>new KeyValuePair<int,int>(cell,workspace.Distance[cell])).ToArray();
                }
                dependencies.Sort();
                var computed=new DelegatedSearchCache{Key=actorSignature,MessageKey=messageSignature,ServiceOriginKey=originKey,Messages=messages,MessageBoundary=messageBoundary,ServiceBoundary=serviceBoundary,MinimumMessageLimit=minimumMessage,MaximumMessageLimit=maximumMessage,MinimumServiceLimit=minimumService,MaximumServiceLimit=maximumService,MessageCells=messages.Select(x=>x.Key).Where(i=>h.CellPolity[i]==p.Id).OrderBy(i=>i).ToArray(),Services=services,Dependencies=dependencies.ToArray(),Allies=allies};
                actorResults[p.Id]=computed;
            }
            // Only independent, read-only actor searches run concurrently.
            // Cache installation and overlapping territorial reductions remain
            // ordered by stable polity ID, on the calling worker.
            int workers=n>=80000?Math.Min(MaximumWorkers,Math.Max(1,Environment.ProcessorCount)):1;
            if(workers>1&&h.Polities.Length>=4)System.Threading.Tasks.Parallel.For(0,h.Polities.Length,new System.Threading.Tasks.ParallelOptions{CancellationToken=token,MaxDegreeOfParallelism=workers},SolveActor);
            else for(int actor=0;actor<h.Polities.Length;actor++)SolveActor(actor);
            token.ThrowIfCancellationRequested();
            foreach(var p in h.Polities)
            {
                if(p.EndedYear>=0)continue;var search=actorResults[p.Id];delegatedSearches[p.Id]=search;
                WorkObserver?.Invoke(new SimulationWorkMetric{Stage=cacheHits[p.Id]?"route cache hits":"route cache misses"});
                if(!cacheHits[p.Id]&&searchTimes!=null)WorkObserver?.Invoke(new SimulationWorkMetric{Stage="administrative actor searches",Milliseconds=searchTimes[p.Id],Items=2});
                if(messageTimes!=null&&messageTimes[p.Id]>0)WorkObserver?.Invoke(new SimulationWorkMetric{Stage="administrative message searches",Milliseconds=messageTimes[p.Id]});
                if(serviceTimes!=null&&serviceTimes[p.Id]>0)WorkObserver?.Invoke(new SimulationWorkMetric{Stage="administrative service searches",Milliseconds=serviceTimes[p.Id]});
                foreach(int cell in search.MessageCells)result.CommunicationOwner[cell]=p.Id;
                foreach(var pair in search.Services)
                {
                    int cell=pair.Key;if(h.CellPolity[cell]!=p.Id&&(h.CellPolity[cell]>=0||pair.Value>result.Distance[cell]||pair.Value==result.Distance[cell]&&result.Owner[cell]>=0&&p.Id>result.Owner[cell]))continue;
                    result.Owner[cell]=p.Id;result.Distance[cell]=pair.Value;
                }
            }
            FinishDelegatedAccounts(s,result,token);
            // Only immutable connectivity is reused. Budgets, rural residents,
            // fiscal practice and annual accounts are recalculated each call.
            delegatedNetworkSignature=key;delegatedConnectivity=new AdministrativeNetwork{Owner=result.Owner,Distance=result.Distance,CommunicationOwner=result.CommunicationOwner,Accounts=result.Accounts.Select(a=>new HistoricalAdministrativeAccount{PolityId=a.PolityId,ReachM=a.ReachM}).ToArray()};return result;
        }
        void FinishDelegatedAccounts(PrehistoryState s,AdministrativeNetwork result,CancellationToken token)
        {
            using var accountMeasurement=ProfileWork("administrative accounts",WorkIndex(s)?.PoliticalCells.Count??World.Surface.Cells.Length);
            token.ThrowIfCancellationRequested();var h=s.History;int visited=0;
            bool institutional=InstitutionalHistory(s);var residents=institutional?new long[h.Polities.Length]:null;var burden=institutional?new long[h.Polities.Length]:null;
            foreach(int cell in PoliticalCells(s))
            {
                if((visited++&2047)==0)token.ThrowIfCancellationRequested();int owner=h.CellPolity[cell];if(owner<0||h.Polities[owner].EndedYear>=0)continue;
                var account=result.Accounts[owner];bool supported=result.Owner[cell]==owner;int distance=supported?result.Distance[cell]:account.ReachM*3;
                long people=RuralCellPopulation(s,cell);
                account.MaximumServiceDistanceM=Math.Max(account.MaximumServiceDistanceM,distance);
                if(!supported)account.DisconnectedResidents+=people;
                account.RuralReceipts+=supported?DistrictReceipts(s,owner,cell,distance,account.ReachM,people):0;
                account.ServiceCost+=DistrictServiceCost(s,owner,cell,distance,account.ReachM,people);
                if(institutional){residents[owner]+=people;burden[owner]+=people*HistoricalInstitutionalEffects.ServiceBurden(distance,account.ReachM,supported);}
            }
            if(h.Administration!=null)foreach(var office in h.Administration.Offices.Where(o=>o.EndedYear<0))result.Accounts[office.PolityId].ServiceCost+=office.AnnualCost;
            foreach(var town in s.Society.Settlements.Where(t=>t.PolityId>=0&&t.Population>=100))
            {
                var account=result.Accounts[town.PolityId];bool supported=result.Owner[town.Cell]==town.PolityId;
                if(institutional){residents[town.PolityId]+=town.Population;burden[town.PolityId]+=town.Population*HistoricalInstitutionalEffects.ServiceBurden(supported?result.Distance[town.Cell]:0,account.ReachM,supported);}
                if(!supported){account.DisconnectedUrbanResidents+=town.Population;continue;}
                account.TaxableUrbanResidents+=town.Population;account.UrbanReceipts+=town.Population/(HistoricalTechnologies.Has(h.Cultures[h.Polities[town.PolityId].CultureId],HistoricalTechnology.Coinage)?16:20);
            }
            if(institutional){result.ResidentStrain=new int[h.Polities.Length];foreach(var p in h.Polities.Where(p=>p.EndedYear<0)){var account=result.Accounts[p.Id];int diversity=s.Society.Settlements.Where(t=>t.PolityId==p.Id&&t.Population>=100).Select(t=>t.CultureId).Distinct().Count();result.ResidentStrain[p.Id]=HistoricalInstitutionalEffects.RealmStrain(burden[p.Id],residents[p.Id],account.DisconnectedResidents+account.DisconnectedUrbanResidents,diversity);}}
        }
    }
}
