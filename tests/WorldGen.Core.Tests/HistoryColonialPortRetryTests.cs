using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryColonialPortRetryTests
{
    static object Call(Prehistory engine,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
    public static void Run(Action<bool,string> check)
    {
        check(StateHash.Compute(new HistoricalColonialConnections())=="3e6f409c4cdc77e11d2119ec182e566258a65f5eaa5265d0ebc2bd8ea6d094ea","An absent port retry extension preserves the independently measured frozen-O canonical record hash");
        var g=new HistoricalPendingPortGovernment();for(int i=0;i<1000;i++)g.Pending.Add(i);
        var visited=new HashSet<int>();for(int i=0;i<125;i++){var batch=g.Take();check(batch.Length==8,"Each pending-port review has a fixed eight-office maximum");foreach(int post in batch)check(visited.Add(post),"Stable round-robin visits a pending office once before repeating any");}
        check(visited.Count==1000&&g.Cursor==0,"A thousand pending offices cannot starve behind settled or randomly sampled posts");
        var copy=g.Copy();copy.Remove(0);copy.Pending.Add(1001);
        check(g.Pending.Positions[0]>=0&&g.Pending.Positions[1001]<0&&copy.Pending.Positions[0]<0&&copy.Pending.Positions[1001]>=0,"Persistent pending-bag branches do not mutate their source");
        var sparse=new HistoricalPendingPortGovernment();sparse.Pending.Add(7);sparse.Pending.Add(11);sparse.Take();sparse.Remove(7);sparse.Remove(11);
        check(sparse.Take().Length==0&&sparse.Cursor==0,"Removing the last office leaves a valid empty cursor");
        check(HistoricalColonialPortRetryEffects.OpportunityYears(1500,67,-1)==40,"A 1,433-year-old never-tested office earns at most forty years of opportunity, not fourteen centuries of invented continuous supply");
        check(HistoricalColonialPortRetryEffects.OpportunityYears(1500,67,1493)==7&&HistoricalColonialPortRetryEffects.OpportunityYears(1500,1498,-1)==2,"Elapsed credit follows that actual office's review or founding date, not the latest state tick");
        check(HistoricalColonialPortRetryEffects.OpportunityYears(1500,67,1500)==0&&HistoricalColonialPortRetryEffects.OpportunityYears(1500,67,1501)==0,"A same-date or future-date review cannot accrue another founding opportunity");
        var record=new HistoricalColonialPortSettlements{Governments=new[]{g}};record.LastTestYear=record.LastTestYear.With(7,1493);var branch=record.Copy();branch.LastTestYear=branch.LastTestYear.With(7,1500);branch.Governments[0].Remove(7);
        check(record.LastTestYear[7]==1493&&record.Governments[0].Pending.Positions[7]>=0,"Saved opportunity dates and government queues copy without aliasing mutations");
        g.Take();g.Pending.Add(1002);var dynamicVisited=new HashSet<int>();for(int i=0;i<126;i++)foreach(int post in g.Take())dynamicVisited.Add(post);
        check(dynamicVisited.Count==1001&&dynamicVisited.Contains(1002),"A newly appended office remains reachable as the cursor wraps");
        var swapping=new HistoricalPendingPortGovernment();for(int i=0;i<35;i++)swapping.Pending.Add(i);
        var first=swapping.Take();foreach(int id in first)swapping.Remove(id);
        var survivors=new HashSet<int>();for(int i=0;i<5;i++)foreach(int id in swapping.Take())survivors.Add(id);
        check(survivors.SetEquals(Enumerable.Range(8,27)),"Removing a selected batch via swap-back does not strand the offices moved before the cursor");
    }
    sealed class Ready
    {
        public int Polity,Post,Port;public Prehistory.Route Route;public long People,Provisions,Investment;
    }
    static Ready FindReady(Prehistory engine,PrehistoryState s,int polity,int postId,int[] departures)
    {
        var a=s.History.Aggregates;var p=s.History.Polities[polity];var post=a.Empires.Territories.Posts[postId];
        if(!(bool)Call(engine,"PendingColonialPort",s,polity,post)||p.Cohesion<500||!(bool)Call(engine,"LivingTerritorialInterest",s,polity,post.Cell)||!(bool)Call(engine,"TerritorialNodeLive",s,polity,-2-postId))return null;
        foreach(int from in departures)
        {
            if(from==post.Cell)continue;
            var route=new Prehistory.Route{A=engine.CellPatch[from],B=engine.CellPatch[post.Cell],CellA=from,CellB=post.Cell,Sea=true,DistanceM=(int)Math.Round(D.Angle(engine.World.Surface.Cells[from].Position,engine.World.Surface.Cells[post.Cell].Position)*engine.World.Recipe.RadiusKm*1000)};
            if(!(bool)Call(engine,"InstitutionalRouteAllowed",s,from,post.Cell,p.CultureId))continue;
            object[] args={s,polity,route,a.TownAt[from],-1,0L};if(!(bool)Call(engine,"MaritimeCharterLogistics",args))continue;
            int port=(int)args[4];var source=s.Society.Settlements[port];if(!(bool)Call(engine,"CanFoundLivingTown",s,source.CultureId,post.Cell,port))continue;
            long available=(long)Call(engine,"LivingFounderAvailability",s,port),founders=Math.Min(250,available/12),farms=Math.Min(available/3,Math.Max(800,founders*8)),people=founders+farms;
            double staging=D.Angle(engine.World.Surface.Cells[source.Cell].Position,engine.World.Surface.Cells[from].Position)*engine.World.Recipe.RadiusKm;
            long provisions=HistoricalVoyageEffects.Provisioning(people,(int)Math.Min(int.MaxValue,route.DistanceM+staging*1000)),investment=HistoricalMaritimeIntegrationEffects.CouncilInvestment(people,route.DistanceM);
            if(p.Treasury>=investment*2&&source.FoodReserve>=people+provisions+source.Population/4)return new Ready{Polity=polity,Post=postId,Port=port,Route=route,People=people,Provisions=provisions,Investment=investment};
        }
        return null;
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string oldHash=original.ComputeHash();var engine=new Prehistory(world);
        var current=engine.UpgradeToLivingWorldHistory(original);var s=engine.UpgradeColonialPortRetries(current);var a=s.History.Aggregates;var h=s.History;
        check(original.ComputeHash()==oldHash,"Explicit retry upgrades leave the loaded old-null checkpoint unchanged");
        check(s.History.Aggregates.Empires.Connections.PortSettlements!=null,"Explicit setup creates the saved pending index for old candidate32 checkpoints");
        check(ReferenceEquals(engine.UpgradeColonialPortRetries(s),s),"An already upgraded retry index is not rebuilt or re-dated");
        engine.Validate(s);var retry=a.Empires.Connections.PortSettlements;
        var ready=new List<Ready>();
        // Offline fixture discovery only. Production retries never enumerate
        // posts, and each review tries no more than eight pending offices.
        foreach(var p in h.Polities.Where(p=>p.EndedYear<0))
        {
            int[] departures=(int[])Call(engine,"LivingColonialDepartures",s,p.Id);var pending=retry.Governments[p.Id].Pending;
            for(int i=0;i<pending.Count;i++){var found=FindReady(engine,s,p.Id,pending.At(i),departures);if(found!=null)ready.Add(found);}
        }
        check(ready.Count>0,"Saved fixture contains genuinely provisioned, crop-compatible, funded unsettled offices without altering any gate");
        var oldest=ready.OrderByDescending(x=>s.Years-a.Empires.Territories.Posts[x.Post].FoundedYear).First();
        int age=s.Years-a.Empires.Territories.Posts[oldest.Post].FoundedYear;
        if(world.Recipe.Seed==73421&&s.Years==131700)
            check(age>=1433&&ready.Any(x=>x.Post==205&&a.Empires.Territories.Posts[x.Post].Cell==160132),"Actual O Earth reproduces the1,433-year-ready cell160132/source-town517 office, not a fabricated permissive target");
        var review=s.Copy();var r=review.History.Aggregates.Empires.Connections.PortSettlements;int[] sources=(int[])Call(engine,"LivingColonialDepartures",review,oldest.Polity);
        var queueField=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var queue=new SimulationEventQueue(review.Timeline.Events);queueField.SetValue(engine,queue);
        int initial=r.Governments[oldest.Polity].Pending.Count;
        for(int i=0;i<initial/8+3&&r.LastTestYear[oldest.Post]<0;i++)
        {
            Call(engine,"RetryLivingColonialPorts",review,oldest.Polity,1,sources);
            check(r.Governments[oldest.Polity].Pending.Count<=initial,"Retry attempts do not manufacture administrative offices");
        }
        check(r.LastTestYear[oldest.Post]==s.Years,"The oldest ready port receives a bounded fair review despite hundreds of other offices");
        review.Timeline.Events=queue.Ordered();queueField.SetValue(engine,null);review.Seal();engine.Validate(review);
        var success=ready.FirstOrDefault(x=>(double)Call(engine,"HistoryRoll","living-sponsored-port-"+x.Route.CellB,x.Polity,s.Years)<1-Math.Exp(-40/40.0));
        check(success!=null,"Fixture has an actual eligible stochastic opportunity within the capped forty-year window");
        var positive=s.Copy();var pa=positive.History.Aggregates;var sourceTown=positive.Society.Settlements[success.Port];var sponsor=positive.History.Polities[success.Polity];
        long population=pa.TotalPopulation,food=sourceTown.FoodReserve,treasury=sponsor.Treasury;int cityCount=pa.Cities.Length;
        var q=new SimulationEventQueue(positive.Timeline.Events);queueField.SetValue(engine,q);
        bool founded=(bool)Call(engine,"TryFoundLivingColonialPort",positive,success.Polity,success.Route,40);
        check(founded&&pa.Cities.Length==cityCount+1&&pa.TownAt[success.Route.CellB]==cityCount,"A real funded compatible office can establish its sponsored town with no gate waiver");
        check(pa.TotalPopulation==population&&positive.Society.Settlements[cityCount].Population+pa.Cities[cityCount].Rural==success.People,"The new port transfers exactly its existing urban/rural founders and invents no world population");
        check(sourceTown.FoodReserve==food-success.People-success.Provisions&&sponsor.Treasury==treasury-success.Investment,"Real startup food, additional voyage rations and court investment are each charged once");
        check(pa.Empires.Connections.PortSettlements.Governments[success.Polity].Pending.Positions[success.Post]<0,"Actual town founding incrementally removes the office from the unsettled index");
        positive.Timeline.Events=q.Ordered();queueField.SetValue(engine,null);positive.Seal();engine.Validate(positive);
        var abandoned=positive.Copy();Call(engine,"AbandonLivingTown",abandoned,cityCount,"Controlled retry lifecycle fixture.");
        check(abandoned.History.Aggregates.Empires.Connections.PortSettlements.Governments[success.Polity].Pending.Positions[success.Post]>=0&&abandoned.History.Aggregates.Empires.Connections.PortSettlements.LastTestYear[success.Post]==s.Years,"Actual abandonment returns the surviving maritime office to the unsettled index without banking its occupied years as new settlement opportunity");
        abandoned.Seal();engine.Validate(abandoned);
        Call(engine,"TrackPendingColonialPort",abandoned,success.Route.CellB,false);int pendingCount=abandoned.History.Aggregates.Empires.Connections.PortSettlements.Governments[success.Polity].Pending.Count;
        Call(engine,"TrackPendingColonialPort",abandoned,success.Route.CellB,false);
        check(abandoned.History.Aggregates.Empires.Connections.PortSettlements.Governments[success.Polity].Pending.Count==pendingCount,"Repeated abandonment/ownership notifications cannot duplicate a pending office");
        var closed=s.Copy();Call(engine,"ClaimAggregatePolity",closed,success.Route.CellB,-1);
        check(closed.History.Aggregates.Empires.Connections.PortSettlements.Governments[success.Polity].Pending.Positions[success.Post]<0,"Actual withdrawal of ownership removes the now-closed colonial office incrementally");
        closed.Seal();engine.Validate(closed);
        var negative=s.Copy();var np=negative.History.Polities[success.Polity];var nt=negative.Society.Settlements[success.Port];long nfood=nt.FoodReserve;np.Treasury=0;long total=negative.History.Aggregates.TotalPopulation;int count=negative.Society.Settlements.Length;
        check(!(bool)Call(engine,"TryFoundLivingColonialPort",negative,success.Polity,success.Route,40)&&negative.History.Aggregates.TotalPopulation==total&&negative.Society.Settlements.Length==count&&nt.FoodReserve==nfood,"No treasury means no settlers, food debit or population creation despite a very old ready office");
        np.Treasury=treasury;foreach(var town in negative.Society.Settlements.Where(t=>negative.History.Aggregates.Resolve(t.PolityId,negative.History)==success.Polity))town.FoodReserve=0;
        check(!(bool)Call(engine,"TryFoundLivingColonialPort",negative,success.Polity,success.Route,40)&&negative.History.Aggregates.TotalPopulation==total&&negative.Society.Settlements.Length==count&&np.Treasury==treasury,"No real provisions means no city or money debit");
        nt.FoodReserve=nfood;negative.History.Aggregates.Empires.Territories.Posts[success.Post].Maritime=false;
        check(!(bool)Call(engine,"TryFoundLivingColonialPort",negative,success.Polity,success.Route,40)&&negative.History.Aggregates.TotalPopulation==total,"A non-maritime office cannot bypass the actual shipping/service requirement");
        var future=engine.AdvanceYears(s,3);var split=engine.AdvanceYears(engine.AdvanceYears(s,1),2);
        check(future.Hash==split.Hash,"Saved office dates and cursors remain publication-partition deterministic");
        world.Prehistory=future;var loaded=WorldFile.Read(WorldFile.Write(world));
        check(loaded.Prehistory.Hash==future.Hash&&loaded.Prehistory.History.Aggregates.Empires.Connections.PortSettlements!=null,"Queue membership, sparse last-tested dates and integrity survive real snapshot serialization");
        check(new Prehistory(loaded).AdvanceYears(loaded.Prehistory,2).Hash==engine.AdvanceYears(future,2).Hash,"A saved retry-bearing world continues identically after reload");
        check(original.ComputeHash()==oldHash,"All diagnosis and retry fixtures leave the original checkpoint untouched");
    }
}
