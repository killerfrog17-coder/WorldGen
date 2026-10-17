using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryMaritimeMissionTests
{
    public static void Run(Action<bool,string> check)
    {
        var map=new HistoricalGeographicMap(4);map.Record(0,3,100,800,4,10);
        string initial=StateHash.Compute(map);
        check(!map.Record(0,2,120,900,5,11)&&StateHash.Compute(map)==initial,"Legacy report merge remains byte-exact and rejects lower-grade updates.");
        check(HistoricalMaritimeMissionEffects.RecordWitnessed(map,0,2,120,900,5,11),"A newer witnessed copied account can update obsolete coastal facts.");
        check(map.Grade[0]==3&&map.ReportedYear[0]==120&&map.Host[0]==5&&map.Shore[0]==11,"Copied political facts do not erase a completed survey's navigation grade.");
        string current=StateHash.Compute(map);
        check(!HistoricalMaritimeMissionEffects.RecordWitnessed(map,0,2,110,700,6,12)&&StateHash.Compute(map)==current,"Stale witnessed accounts cannot overwrite newer political observations.");
        check(!HistoricalMaritimeMissionEffects.RecordWitnessed(map,0,1,150,0,-1,13)&&StateHash.Compute(map)==current,"Newer hearsay cannot erase witnessed facts or their date.");
        check(!HistoricalMaritimeMissionEffects.RecordWitnessed(map,0,3,120,1000,7,14),"Equal-date facts do not oscillate with delivery order.");
        var receiver=new HistoricalGeographicMap(4);receiver.Record(0,3,100,800,4,10);
        check(HistoricalMaritimeMissionEffects.ShareWitnessed(map,receiver,0,2),"Existing navigation does not suppress a physically delivered newer host report.");
        check(!HistoricalMaritimeMissionEffects.ShareWitnessed(map,receiver,0,1),"An untrusted hearsay copy cannot deliver hidden host facts.");
        HistoricalMaritimeMissionEffects.RecordWitnessed(receiver,0,2,120,900,5,11);
        check(!HistoricalMaritimeMissionEffects.ShareWitnessed(map,receiver,0,2),"Already delivered facts do not generate repeated report traffic.");
        map.Record(1,2,200,300,8,20);
        check(HistoricalMaritimeMissionEffects.RecordWitnessed(map,1,3,180,500,7,21)&&map.Grade[1]==3&&map.Host[1]==8&&map.ReportedYear[1]==200,"An older accurate survey improves navigation without replacing newer observed ownership.");
        map.Record(2,1,300,0,-1,25);
        check(HistoricalMaritimeMissionEffects.RecordWitnessed(map,2,2,250,500,9,26)&&map.Host[2]==9&&map.ReportedYear[2]==250,"Actual older witness replaces a later rumor, not the reverse.");
        var copy=map.Copy();HistoricalMaritimeMissionEffects.RecordWitnessed(copy,0,2,140,900,10,12);
        check(map.Host[0]==5&&copy.Host[0]==10,"Witnessed chart updates preserve public snapshot isolation.");
        check(!HistoricalMaritimeMissionEffects.ExpeditionReadiness(439,.2,0,20000,900,100000000,100000,5000),"A wealthy but poorly integrated court does not automatically finance a coherent coercive mission.");
        check(HistoricalMaritimeMissionEffects.ExpeditionReadiness(589,.8,0,20000,900,100000000,100000,5000),"A functioning bureaucratic funded fleet is not rejected by the obsolete fixed600 cohesion veto.");
        check(!HistoricalMaritimeMissionEffects.ExpeditionReadiness(900,1,0,0,900,100000000,100000,5000),"Already committed troops cannot be promised to another overseas operation.");
        check(!HistoricalMaritimeMissionEffects.ExpeditionReadiness(900,1,0,20000,900,1000,100000,5000),"Physical comparative force does not waive real fitting-out costs.");
        check(!HistoricalMaritimeMissionEffects.ExpeditionReadiness(900,1,0,20000,500,100000000,100000,5000),"A chart is not permission to launch a war without an expansionist political actor.");
    }
    public static void World(string path,Action<bool,string> check,bool continuation=true)
    {
        var w=WorldFile.Read(File.ReadAllBytes(path));var original=w.Prehistory;string originalHash=original.ComputeHash();var e=new Prehistory(w);var s=e.UpgradeRealmLifecycles(original);
        if(s.History.Aggregates.Empires.Connections.Realms.Version<11)throw new Exception("Actual mission fixture requires the explicit revision11 upgrade.");
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
        // Cold serialization and split playback use unmodified real history.
        string replayHash="not-run";
        if(continuation)
        {
            w.Prehistory=s;var restored=WorldFile.Read(WorldFile.Write(w));var whole=e.AdvanceYears(s,12);var cold=new Prehistory(restored);
            var parts=cold.AdvanceYears(cold.AdvanceYears(restored.Prehistory,5),7);e.Validate(whole);cold.Validate(parts);
            check(whole.Hash==parts.Hash,"Revision11 actual-world continuation agrees across cold12 versus5+7 years.");replayHash=whole.Hash;
        }
        var branch=s.Copy();Call("PrepareAggregateEnvironment",branch);Call("PrepareAggregateRelations",branch);var h=branch.History;var a=h.Aggregates;
        var town=branch.Society.Settlements.Where(t=>a.Resolve(t.PolityId,h)>=0&&HistoricalCells.Polity(branch,t.Cell)==a.Resolve(t.PolityId,h)&&a.Living.Cities[t.Id].AbandonedYear<0&&a.Cities[t.Id].Supported&&t.Population>=1000&&(bool)Call("DiffusionShore",t.Cell)).OrderByDescending(t=>t.Population).First();
        int owner=a.Resolve(town.PolityId,h);var p=h.Polities[owner];var hc=h.Cultures[p.CultureId];
        var peerTown=branch.Society.Settlements.Where(t=>a.Resolve(t.PolityId,h)>=0&&HistoricalImperialHierarchy.Sovereign(branch,a.Resolve(t.PolityId,h))!=HistoricalImperialHierarchy.Sovereign(branch,owner)&&a.Living.Cities[t.Id].AbandonedYear<0&&a.Cities[t.Id].Supported&&(bool)Call("DiffusionShore",t.Cell))
            .OrderBy(t=>D.Angle(w.Surface.Cells[town.Cell].Position,w.Surface.Cells[t.Cell].Position)).First(t=>(bool)Call("InstitutionalRouteAllowed",branch,town.Cell,t.Cell,p.CultureId));
        int peer=a.Resolve(peerTown.PolityId,h);var other=h.Polities[peer];p.CapitalId=town.Id;other.CapitalId=peerTown.Id;p.Army=50000;p.Cohesion=900;p.Exhaustion=0;p.Treasury=1000000000;
        a.Empires.Connections.Military.Governments[owner].Reserved=0;town.FoodReserve=100000000;peerTown.FoodReserve=100000000;
        Call("AssignTerritorialSource",branch,town.Cell,town.Id);
        var route=new Prehistory.Route{CellA=town.Cell,CellB=peerTown.Cell,A=e.CellPatch[town.Cell],B=e.CellPatch[peerTown.Cell],Sea=true,
            DistanceM=(int)Math.Round(D.Angle(w.Surface.Cells[town.Cell].Position,w.Surface.Cells[peerTown.Cell].Position)*w.Recipe.RadiusKm*1000)};
        var map=a.Empires.Connections.Geography.Governments[owner];map.Record(route.B,3,branch.Years-1,500,peer,route.CellB);
        string before=branch.ComputeHash();double power=(double)Call("QuotedMaritimeFieldPower",branch,owner,route.CellB,town.Id,5000L);
        check(power>0&&branch.ComputeHash()==before,"An actual funded coast quotes a nonzero field force without charging, reserving or mutating source state.");
        h.Dynamics.Leaders[h.Dynamics.States[owner].LeaderId].Ambition=900;
        a.Empires.Connections.Military.Governments[owner].Reserved=p.Army;
        check((double)Call("QuotedMaritimeFieldPower",branch,owner,route.CellB,town.Id,5000L)==0,"An actual simultaneous front consuming all troops leaves no second promised expedition.");
        a.Empires.Connections.Military.Governments[owner].Reserved=0;long savedFood=town.FoodReserve;town.FoodReserve=0;
        check((int)Call("QuotedMaritimeMen",branch,owner,route.CellB,town.Id,5000L)==0,"A rich real port without expedition provisions cannot quote transportable troops.");town.FoodReserve=savedFood;
        int acrossSea=branch.Society.Settlements.First(t=>w.Landmass[t.Cell]!=w.Landmass[town.Cell]&&(bool)Call("DiffusionShore",t.Cell)).Cell;
        var previousService=a.Empires.Connections.SeaService;int key=town.Id*2;
        a.Empires.Connections.SeaService=new HistoricalSeaService{Ports=new[]{new HistoricalSeaServicePort{Key=key,TownId=town.Id,Cell=town.Cell,OwnerId=owner,LastSeconds=SimulationTime.Seconds(branch),Capacity=1000000000}}};
        a.Empires.Connections.SeaService.BySource=a.Empires.Connections.SeaService.BySource.With(key,0);
        check((int)Call("QuotedMaritimeMen",branch,owner,acrossSea,town.Id,5000L)==0,"A real source fleet already fully booked cannot promise a second overseas field force merely because treasury is rich.");
        a.Empires.Connections.SeaService=previousService;
        p.Cohesion=100;
        check(!(bool)Call("ReadyKnownMaritimeMission",branch,owner,route,town.Id),"Known foreign shores are not convoy targets for a court unable to organize its own political aim.");p.Cohesion=900;
        // Equal technology but a genuine local force advantage can support a
        // fast expedition. A campaign, not immediate whole-country submission,
        // is the positive outcome. Test-controlled actors retain real ports.
        other.CultureId=p.CultureId;other.Army=25;other.Treasury=1000000000;other.Exhaustion=0;
        a.Empires.Connections.Military.Governments[peer].Reserved=0;h.Nuclear.States[peer].Stockpile=0;
        var relation=(HistoricalRelation)Call("AggregateRelation",branch,owner,peer);relation.WarSince=-1;relation.TruceUntil=-1;relation.Alliance=false;
        a.Polities[owner].Receipts=a.Polities[owner].ServiceCost+1000000;
        var campaignQueue=new SimulationEventQueue(Array.Empty<SimulationEvent>());typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(e,campaignQueue);
        int campaigns=h.Dynamics.Campaigns.Length;
        check((bool)Call("CommitMaritimeStateExpedition",branch,owner,peer,route,1,true)&&h.Dynamics.Campaigns.Length==campaigns+1,"An equal-technology materially superior supplied force can promptly fund a real overseas campaign.");
        check(HistoricalImperialHierarchy.Sovereign(branch,peer)!=HistoricalImperialHierarchy.Sovereign(branch,owner),"Opening that funded campaign does not annex or subordinate the remote government.");
        // Direct chart delivery regression: lower-grade newer facts cross the
        // normal due-dated report branch, not a test-only direct owner oracle.
        var receiver=a.Empires.Connections.Geography.Governments[peer];int region=route.A;receiver.Record(region,3,branch.Years-2,500,peer,route.CellA);
        receiver.Missions=new[]{new HistoricalSeaMission{Kind=HistoricalSeaMission.Report,Source=route.CellA,Target=route.CellA,Region=region,Grade=2,Host=owner,Opportunity=700,ReportedYear=branch.Years-1,Due=SimulationTime.Seconds(branch),Departed=SimulationTime.Seconds(branch)-SimulationTime.Day}};
        other.Cohesion=0;var queue=new SimulationEventQueue(Array.Empty<SimulationEvent>());typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(e,queue);
        Call("AdvanceSeaMissions",branch,peer);
        check(receiver.Grade[region]==3&&receiver.Host[region]==owner&&receiver.ReportedYear[region]==branch.Years-1,"The actual dated report-arrival branch refreshes political facts while retaining survey-grade navigation.");
        var legacy=branch.Copy();legacy.History.Aggregates.Empires.Connections.Realms.Version=10;var legacyMap=new HistoricalGeographicMap(e.Patches.Length);legacyMap.Record(region,3,100,500,1,route.CellA);
        check(!(bool)Call("RecordGeographicFacts",legacy,legacyMap,region,2,120,700,2,route.CellA)&&legacyMap.Host[region]==1,"Actual realm10 dispatch still selects the untouched legacy merge.");
        check(original.ComputeHash()==originalHash,"Upgrade, actual source quotes, dated delivery and cold continuation leave the original save unchanged.");
        Console.WriteLine("Maritime mission world12 hash="+replayHash+"; quoted field="+power);
    }
}
