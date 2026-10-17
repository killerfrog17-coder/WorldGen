using System.Reflection;
using System.Runtime.CompilerServices;
using WorldGen.Core;

public static class HistoryPreparedLedgerTests
{
    static object Call(Prehistory engine,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
    // The three tested ledger methods have no geographical dependency. Build a
    // two-cell actor fixture, not another world generator or an expensive load.
    static (Prehistory Engine,PrehistoryState State) Fixture(int revision)
    {
        var engine=(Prehistory)RuntimeHelpers.GetUninitializedObject(typeof(Prehistory));
        typeof(Prehistory).GetField("CellPatch")!.SetValue(engine,new[]{0,0});typeof(Prehistory).GetField("Patches")!.SetValue(engine,new[]{new Prehistory.Patch()});
        var plots=new HistoricalPreparedPlots{Version=revision>=17?2:1,SinceYear=100,Cells=new HistoricalCellTree(12),RegionalCapacity=new HistoricalCellTree(revision>=17?2:1),RegionalEpoch=new HistoricalCellTree(1)};
        var state=new PrehistoryState{Years=100,Population=new long[]{500},Society=new SocietyState{Settlements=new[]{new Settlement{Id=0,Cell=0}}},History=new HistoryState{Aggregates=new HistoricalAggregateState{Empires=new HistoricalImperialSystem{Connections=new HistoricalColonialConnections{Realms=new HistoricalRealmLifecycles{Version=revision},PreparedPlots=plots}}}}};
        return(engine,state);
    }
    static bool Invalid(Prehistory engine,PrehistoryState state,out string message)
    {try{Call(engine,"ValidatePreparedPlots",state);message="valid";return false;}catch(TargetInvocationException e)when(e.InnerException is ArgumentException){message=e.InnerException.Message;return true;}}
    public static void Run(Action<bool,string> check)
    {
        var legacy=Fixture(16);Call(legacy.Engine,"RecordPreparedField",legacy.State,0,500L,-1);Call(legacy.Engine,"VacatePreparedTownFields",legacy.State,0);legacy.State.Years+=20;
        long effective=legacy.Engine.PreparedFarmersAt(legacy.State,0);Call(legacy.Engine,"RecordPreparedField",legacy.State,0,effective+50,-1);
        var old=legacy.State.History.Aggregates.Empires.Connections.PreparedPlots;
        bool reproduced=Invalid(legacy.Engine,legacy.State,out var message);
        check(effective==303&&old.Cells[0]==353&&old.RegionalCapacity[0]==550&&reproduced,"Frozen16 reproduces the exact197-person residual: a town vacates another rural holding, then replacement subtracts decayed rather than raw credit.");
        Console.WriteLine("Literal16 decayed/raw reproducer: "+message);
        var safe=Fixture(17);Call(safe.Engine,"RecordPreparedField",safe.State,0,500L,-1);Call(safe.Engine,"VacatePreparedTownFields",safe.State,0);
        check(safe.State.History.Aggregates.Empires.Connections.PreparedPlots.Cells[3]<0,"Revision17 abandonment cannot vacate a different rural holder sharing the physical cell.");
        // A valid preceding16 snapshot can already carry a rural vacancy date;
        // prospective17 replacement must debit its raw regional contribution.
        var inherited=Fixture(16);Call(inherited.Engine,"RecordPreparedField",inherited.State,0,500L,-1);Call(inherited.Engine,"VacatePreparedTownFields",inherited.State,0);inherited.State.Years+=20;inherited.State.History.Aggregates.Empires.Connections.Realms.Version=17;
        Call(inherited.Engine,"InitializePreparedPlots",inherited.State);
        Call(inherited.Engine,"RecordPreparedField",inherited.State,0,353L,-1);
        check(inherited.Engine.PreparedRegionalFarmersAt(inherited.State,0)==353&&!Invalid(inherited.Engine,inherited.State,out _),"Revision17 replacement removes the complete old raw rural contribution even when its usable field has decayed.");
        foreach(int revision in new[]{16,17})
        {
            var f=Fixture(revision);Call(f.Engine,"RecordPreparedField",f.State,0,100L,-1);f.State.Population[0]=0;Call(f.Engine,"PreparedRegionalCapacity",f.State,0,0L);f.State.Population[0]=50;Call(f.Engine,"RecordPreparedField",f.State,1,50L,-1);f.State.Population[0]=0;Call(f.Engine,"PreparedRegionalCapacity",f.State,0,0L);
            bool invalid=Invalid(f.Engine,f.State,out var detail);check(revision==16?invalid:!invalid,"Two empty/refill cycles in one calendar year retain the literal16 failure but use distinct17 reset generations.");
            Console.WriteLine("Epoch reproducer revision"+revision+": "+detail);
        }
        foreach(int revision in new[]{16,17})
        {
            var f=Fixture(revision);Call(f.Engine,"RecordPreparedField",f.State,0,(long)int.MaxValue,-1);Call(f.Engine,"RecordPreparedField",f.State,1,(long)int.MaxValue,-1);
            if(revision>=17)check(f.Engine.PreparedRegionalFarmersAt(f.State,0)==2L*int.MaxValue,"Version2's two31-bit words retain both fully paid plot capacities beyond int range without clipping either cohort.");
            Call(f.Engine,"RecordPreparedField",f.State,0,(long)int.MaxValue,0);
            bool invalid=Invalid(f.Engine,f.State,out var detail);check(revision==16?invalid:!invalid,"The saved int rural-credit bound preserves literal16 saturation behavior but cannot silently saturate and then under-debit17 remaining plots.");
            Console.WriteLine("Integer-bound reproducer revision"+revision+": "+detail);
        }
        var upgrade=Fixture(16);Call(upgrade.Engine,"RecordPreparedField",upgrade.State,0,(long)int.MaxValue,-1);Call(upgrade.Engine,"RecordPreparedField",upgrade.State,1,(long)int.MaxValue,-1);
        check(!Invalid(upgrade.Engine,upgrade.State,out _),"An old saturated-but-valid16 ledger is accepted before explicit migration.");
        upgrade.State.History.Aggregates.Empires.Connections.Realms.Version=17;Call(upgrade.Engine,"InitializePreparedPlots",upgrade.State);
        check(upgrade.State.History.Aggregates.Empires.Connections.PreparedPlots.Version==2&&upgrade.Engine.PreparedRegionalFarmersAt(upgrade.State,0)==2L*int.MaxValue&&!Invalid(upgrade.Engine,upgrade.State,out _),"Explicit17 sparse migration recovers the exact raw paid total from a valid saturated16 ledger without deleting fields or inventing work.");
    }
}
