using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistorySeaServiceTests
{
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalSeaServiceEffects.Capacity(0,0,1,1,1,1,1)==0,"Technology and money without local crew communities create no shipping capacity.");
        long regional=HistoricalSeaServiceEffects.Capacity(5000,3000,1,0,.5,0,0),ocean=HistoricalSeaServiceEffects.Capacity(5000,3000,1,1,1,0,0),industrial=HistoricalSeaServiceEffects.Capacity(50000,30000,1,1,1,1,1);
        check(regional>0&&ocean>regional&&industrial>ocean*20,"Funded large industrial ports scale real throughput rather than receiving a global colony slowdown.");
        check(HistoricalSeaServiceEffects.Capacity(500,1000000000,1,1,1,0,0)==HistoricalSeaServiceEffects.Capacity(500,2000,1,1,1,0,0),"A tiny harbor cannot treat an arbitrarily large inland farm catchment as coastal shipyards.");
        check(HistoricalSeaServiceEffects.RoundTripDays(700,0,0,0)<60,"Nearby ancient maritime service remains a short recurring operation.");
        check(HistoricalSeaServiceEffects.RoundTripDays(7000,1,1,1)<HistoricalSeaServiceEffects.RoundTripDays(7000,1,0,0)/2,"Powered fleets can rapidly turn around genuinely distant cargo routes.");
        check(HistoricalSeaServiceEffects.Work(100,0,200)==20000&&HistoricalSeaServiceEffects.Work(100,100,200)==25000,"Passengers and grain consume the same finite cargo-day budget without creating people.");
        check(HistoricalSeaServiceEffects.Credit(0,100,100000)==36500,"Unused centuries cannot accumulate unlimited voyage credit.");
        check(HistoricalSeaServiceEffects.Credit(0,100,20)==HistoricalSeaServiceEffects.Credit(HistoricalSeaServiceEffects.Credit(0,100,8),100,12),"Unchanged capacity mints the same work allowance across partitioned time.");
        var ledger=new HistoricalSeaService{Ports=new[]{new HistoricalSeaServicePort{Key=2,TownId=1,Capacity=100,CargoDayCredit=200}}};ledger.BySource=ledger.BySource.With(2,0);var copy=ledger.Copy();copy.Ports[0].CargoDayCredit=0;copy.BySource=copy.BySource.With(2,-1);
        check(ledger.Ports[0].CargoDayCredit==200&&ledger.BySource[2]==0,"Public checkpoint copies detach craft capital and source indices.");
        check(StateHash.Compute(new HistoricalColonialConnections())=="3e6f409c4cdc77e11d2119ec182e566258a65f5eaa5265d0ebc2bd8ea6d094ea","Absent sea-service preserves released optional-connection canonical bytes.");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var original=w.Prehistory;string originalHash=original.ComputeHash();var e=new Prehistory(w);var s=e.UpgradeRealmLifecycles(original).Copy();
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);var a=s.History.Aggregates;
        var town=s.Society.Settlements.Where(t=>t.Population>=1000&&a.Living.Cities[t.Id].AbandonedYear<0&&a.Cities[t.Id].Supported&&a.Resolve(t.PolityId,s.History)>=0&&HistoricalCells.Polity(s,t.Cell)==a.Resolve(t.PolityId,s.History)&&(bool)Call("DiffusionShore",t.Cell)).OrderByDescending(t=>t.Population).First();
        int owner=a.Resolve(town.PolityId,s.History),target=s.Society.Settlements.Where(t=>t.Id!=town.Id&&(bool)Call("DiffusionShore",t.Cell)).OrderBy(t=>Math.Abs(D.Angle(w.Surface.Cells[town.Cell].Position,w.Surface.Cells[t.Cell].Position)*w.Recipe.RadiusKm-3000)).First().Cell;
        s.History.Polities[owner].Treasury=1000000000;town.FoodReserve=Math.Max(town.FoodReserve,10000000);a.Empires.Connections.SeaService=new HistoricalSeaService();
        object Quote(PrehistoryState state,long cargo,long people=0)=>Call("QuoteSeaService",state,owner,town.Id,town.Cell,target,cargo,people,0L,0L,true,0d);
        bool Allowed(object q)=>(bool)q.GetType().GetField("Allowed")!.GetValue(q)!;
        bool Commit(PrehistoryState state,object q)=>(bool)Call("CommitSeaService",state,q);
        var q=Quote(s,100,20);string before=s.ComputeHash();var stale=Quote(s,100,20);
        check(Allowed(q)&&s.ComputeHash()==before,"A real funded coastal port can quote a distant voyage without mutating state.");
        int other=Enumerable.Range(0,a.Polities[owner].Cities.Count).Select(k=>a.Polities[owner].Cities.At(k)).FirstOrDefault(id=>id!=town.Id&&a.Cities[id].Supported&&a.Living.Cities[id].AbandonedYear<0,-1);
        if(other<0)throw new Exception("Sea-service fixture needs two grain suppliers under one court.");
        var alternate=Call("QuoteSeaService",s,owner,other,town.Cell,target,100L,0L,0L,0L,true,0d);
        check((int)alternate.GetType().GetField("Key")!.GetValue(alternate)! == (int)q.GetType().GetField("Key")!.GetValue(q)!,"Different granaries using the same physical port cannot create separate craft fleets.");
        long peopleBefore=a.TotalPopulation,money=s.History.Polities[owner].Treasury,food=town.FoodReserve;
        check(Commit(s,q),"A finite paid charter can be committed at an actual source port.");
        check(a.TotalPopulation==peopleBefore&&s.History.Polities[owner].Treasury<money&&town.FoodReserve<food,"Crew provisions and charter capital are paid without adding or removing demographic cohorts.");
        check(!Commit(s,stale),"Two simultaneous bookings cannot spend the same quoted source credit.");
        int bookings=0;for(;bookings<64;bookings++){var next=Quote(s,10000000);if(!Allowed(next))break;if(!Commit(s,next))throw new Exception("Fresh sequential sea quote failed.");}
        check(bookings<64&&a.Empires.Connections.SeaService.Ports.Length==1,"Concurrent freight, passenger and military-equivalent requests exhaust one shared source, not independent fleets per consumer.");
        var abandoned=s.Copy();abandoned.History.Aggregates.Living.Cities[town.Id].AbandonedYear=abandoned.Years;
        check(!Allowed(Quote(abandoned,10)),"An abandoned supplier cannot field an old saved craft allocation.");
        var lost=s.Copy();lost.Society.Settlements[town.Id].PolityId=-1;
        check(!Allowed(Quote(lost,10)),"Losing the actual source invalidates its former owner's supply and credit.");
        Call("ValidateSeaService",s);check(original.ComputeHash()==originalHash,"Shipping experiments preserve the loaded old-rules world.");
        // The ordinary upgraded world, not the controlled finance experiment,
        // verifies saved continuation and optional-model canonical handling.
        var normal=e.UpgradeRealmLifecycles(original);w.Prehistory=normal;var coldWorld=WorldFile.Read(WorldFile.Write(w));var restored=coldWorld.Prehistory;
        check(restored.Hash==normal.Hash,"The new sparse service extension survives an exact compact save/load.");
        var whole=e.AdvanceYears(normal,12);var cold=new Prehistory(coldWorld);var parts=cold.AdvanceYears(cold.AdvanceYears(restored,5),7);e.Validate(whole);cold.Validate(parts);
        check(whole.Hash==parts.Hash,"Service capital and work replay identically across warm twelve and cold five-plus-seven years.");
        var used=whole.Copy();var service=used.History.Aggregates.Empires.Connections.SeaService;if(service.Ports.Length==0)throw new Exception("Continuation fixture did not use its service ledger.");string usedHash=used.ComputeHash();
        service.Ports[0].CharterSpending++;check(used.ComputeHash()!=usedHash,"Public mutation of a used craft account is detected by authoritative hashing.");service.Ports[0].CharterSpending--;
        service.BySource=service.BySource.With(service.Ports[0].Key,-1);check(used.ComputeHash()!=usedHash,"Public mutation of the source lookup tree is detected even after cached fingerprinting.");
        w.Prehistory=whole;var usedWorld=WorldFile.Read(WorldFile.Write(w));check(usedWorld.Prehistory.Hash==whole.Hash&&usedWorld.Prehistory.History.Aggregates.Empires.Connections.SeaService.Ports.Length==service.Ports.Length,"Used craft capital, paid charter accounts and source indices survive a cold compact save exactly.");
        Console.WriteLine("Sea service: source="+town.Id+", bookings="+bookings+", ports="+whole.History.Aggregates.Empires.Connections.SeaService.Ports.Length);
    }
}
