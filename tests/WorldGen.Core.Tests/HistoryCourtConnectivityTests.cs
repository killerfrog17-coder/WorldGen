using System.Reflection;
using System.Runtime.CompilerServices;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryCourtConnectivityTests
{
    public static void Run(Action<bool,string> check)
    {
        int[][] graph={new[]{1},new[]{0,2},new[]{1,3},new[]{2,4},new[]{3,5},new[]{4}};
        HistoricalCourtConnection Probe(Func<int,bool> pass,int budget=96)=>HistoricalCourtConnectivity.Probe(0,5,i=>graph[i],pass,i=>5-i,budget);
        check(Probe(_=>true)==HistoricalCourtConnection.Connected,"A continuous accessible corridor connects the court.");
        check(Probe(i=>i!=2)==HistoricalCourtConnection.Blocked,"Foreign control of the only corridor isolates an enclave.");
        check(Probe(_=>true,1)==HistoricalCourtConnection.Unknown,"A bounded probe cannot label an unfinished search disconnected.");
        int reads=0;HistoricalCourtConnectivity.Probe(0,1000000,i=>{reads++;return new[]{i+1};},_=>true,i=>-i,96);
        check(reads==96,"Court route work is bounded independently of world or empire size.");
        double H(double ratio=1,HistoricalCourtConnection connection=HistoricalCourtConnection.Connected,double bureaucracy=0,double strength=1,double legitimacy=.6)
            =>HistoricalCourtConnectivity.IndependenceHazard(ratio,connection,bureaucracy,strength,legitimacy);
        check(H()==0,"A connected local city-state gains no generic new dissolution lottery.");
        check(H(connection:HistoricalCourtConnection.Unknown)==0,"Uncertain route results alone cause no independence pressure.");
        check(H(connection:HistoricalCourtConnection.Blocked)>.02,"A genuinely cut-off organized center can separate on a decadal rather than millennial scale.");
        check(H(4)>H(1.2)&&H(1.2)>0,"Overextension increases local independence pressure continuously.");
        check(H(4,bureaucracy:1)<H(4)&&H(connection:HistoricalCourtConnection.Blocked,bureaucracy:1)>0,"Modern administration reduces distance pressure but does not make a true enclave invulnerable.");
        check(H(connection:HistoricalCourtConnection.Blocked,strength:.1)<H(connection:HistoricalCourtConnection.Blocked),"Weak settlements are less able to organize an independent court.");
        check(HistoricalCourtConnectivity.CoreRadius(100,0,0,0)==100&&HistoricalCourtConnectivity.CoreRadius(100,1,1,0)>300,"Early direct administration remains local while installed writing and roads extend it.");
        var engine=(Prehistory)RuntimeHelpers.GetUninitializedObject(typeof(Prehistory));
        var guard=typeof(Prehistory).GetMethod("ReviewDisconnectedCourt",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var legacy=new PrehistoryState{History=new HistoryState{Aggregates=new HistoricalAggregateState{Empires=new HistoricalImperialSystem{Connections=new HistoricalColonialConnections{Realms=new HistoricalRealmLifecycles{Version=29}}}}}};
        check(!(bool)guard.Invoke(engine,new object[]{legacy,0,0,10})!,"Revision29 takes its exact old dispatch without inspecting missing world data.");
        var cells=Enumerable.Range(0,6).Select(i=>new Cell{Position=new V(1,i*.01,0).Unit,Neighbors=graph[i]}).ToArray();
        var world=new World{Surface=new Surface{Cells=cells},Hydrology=new HydrologyResult{OceanConnected=new bool[6],LakeCoverageFraction=new double[6]}};
        typeof(Prehistory).GetField("World")!.SetValue(engine,world);
        var aggregate=new HistoricalAggregateState{Cells=HistoricalCellTree.From(30,i=>i%5==1?(i/5==0||i/5==5?0:1):-1),Polities=new[]{new HistoricalPolityAggregate(),new HistoricalPolityAggregate()},Empires=new HistoricalImperialSystem{Governments=new[]{new HistoricalImperialGovernment(),new HistoricalImperialGovernment()}}};
        var s=new PrehistoryState{History=new HistoryState{Aggregates=aggregate,Polities=new[]{new HistoricalPolity{Id=0},new HistoricalPolity{Id=1}}}};
        var route=typeof(Prehistory).GetMethod("CourtLandConnection",BindingFlags.Instance|BindingFlags.NonPublic)!;
        HistoricalCourtConnection Connection()=>(HistoricalCourtConnection)route.Invoke(engine,new object[]{s,0,0,5})!;
        check(Connection()==HistoricalCourtConnection.Blocked,"Actual political ownership blocks the Core administrative route, not just the pure graph fixture.");
        for(int i=1;i<5;i++)aggregate.Cells=aggregate.Cells.With(i*5+1,0);
        check(Connection()==HistoricalCourtConnection.Connected,"Actual own territory restores the connection without changing distance.");
        world.Hydrology.LakeCoverageFraction[3]=1;
        check(Connection()==HistoricalCourtConnection.Blocked,"The land control probe does not walk a whole-cell lake.");
        world.Hydrology.LakeCoverageFraction[3]=0;
        aggregate.Empires.Governments[1].Patron=0;for(int i=1;i<5;i++)aggregate.Cells=aggregate.Cells.With(i*5+1,1);
        check(Connection()==HistoricalCourtConnection.Connected,"Actual constituent administration inside the same realm is a usable corridor.");
        HistoryMapSelectionTests.Run(check);
    }
    public static void World(string path,string output,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var old=world.Prehistory;engine.Validate(old);
        int preceding=old.History.Aggregates.Empires.Connections.Realms.Version;string hash=old.Hash;
        var next=engine.UpgradeRealmLifecycles(old);engine.Validate(next);
        check(next.History.Aggregates.Empires.Connections.Realms.Version==30&&old.History.Aggregates.Empires.Connections.Realms.Version==preceding&&old.ComputeHash()==hash,"Explicit30 upgrade preserves the old checkpoint and its rule marker.");
        var one=engine.AdvanceYears(next,10);var parts=engine.AdvanceYears(engine.AdvanceYears(next,3),7);engine.Validate(one);
        check(one.Hash==parts.Hash,"Actual200k revision30 continuation is publication-partition independent.");
        world.Prehistory=one;var bytes=WorldFile.Write(world);var loaded=WorldFile.Read(bytes);new Prehistory(loaded).Validate(loaded.Prehistory);
        check(loaded.Prehistory.Hash==one.Hash,"New rules and authoritative ledgers survive an actual world-file roundtrip.");
        check(new Prehistory(loaded).AdvanceYears(loaded.Prehistory,2).Hash==engine.AdvanceYears(one,2).Hash,"Reloaded new rules retain exact future event decisions.");
        File.WriteAllBytes(output,bytes);
    }
}
