using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryPerformanceTests
{
    public static void Run(string source,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(source));var original=world.Prehistory;string originalHash=original.Hash;
        var literal=new Prehistory(world){UseHistoryIndexes=false};
        var expected=literal.AdvanceYears(original,20);
        var engine=new Prehistory(world);var metrics=new List<SimulationWorkMetric>();engine.WorkObserver=metrics.Add;
        var direct=engine.AdvanceYears(original,20);
        check(direct.Hash==expected.Hash,"Sparse accounting, catchments and route geometry preserve literal history");
        var accounts=metrics.Where(m=>m.Stage=="administrative accounts").ToArray();
        check(accounts.Length>0&&accounts.All(m=>m.Items<world.Surface.Cells.Length),"Administrative accounts visit owned cells rather than the full mesh");
        var session=new SimulationSession(engine,original);metrics.Clear();long target=session.Seconds+20*SimulationTime.Year;int publications=0;
        while(session.Seconds<target)
        {
            var slice=SimulationClockView.Publication(session.Seconds,target-session.Seconds,1000*SimulationTime.Year);
            session.AdvanceSeconds(slice.Seconds,slice.MaximumEvents,maximumGlobalWork:slice.MaximumGlobalWork);publications++;
        }
        check(session.Hash==expected.Hash,"Larger playback batches preserve every event and its exact history");
        check(publications<20,"Fast populated playback does not copy/hash a checkpoint every year");
        check(metrics.Count(m=>m.Stage=="migration catchment index")<=3,"Migration geometry survives publications and rebuilds only with markets");
        var first=engine.AdvanceYears(original,7);var second=engine.AdvanceYears(first,13);
        check(second.Hash==expected.Hash,"Public branch copies retain exact sparse-cache outcomes");
        var isolated=new SimulationSession(new Prehistory(world),original);var exposed=isolated.AdvanceSeconds(2*SimulationTime.Year,int.MaxValue);
        exposed.Society.Settlements[0].Population+=1000;
        if(exposed.History.Archive.Count>0)exposed.History.Archive[0].Text="Public caller mutation";
        isolated.AdvanceSeconds(18*SimulationTime.Year,int.MaxValue);
        check(isolated.Hash==expected.Hash,"Published actors and archive remain isolated from the private session");
        var townCells=original.Society.Settlements.Select(t=>t.Cell).ToHashSet();
        int rural=Enumerable.Range(0,original.History.CellPolity.Length).FirstOrDefault(i=>original.History.CellPolity[i]>=0&&!townCells.Contains(i),-1);
        if(rural>=0)
        {
            var branch=original.Copy();branch.History.CellPolity[rural]=-1;branch.Seal();
            var reference=new Prehistory(world){UseHistoryIndexes=false}.AdvanceYears(branch,3);
            check(engine.AdvanceYears(branch,3).Hash==reference.Hash,"An independently edited ownership branch cannot reuse another branch's revision key");
        }
        int market=Enumerable.Range(0,original.History.CellMarket.Length).FirstOrDefault(i=>original.History.CellMarket[i]>=0&&!townCells.Contains(i),-1);
        if(market>=0)
        {
            var branch=original.Copy();branch.History.CellMarket[market]=-1;branch.Seal();
            var reference=new Prehistory(world){UseHistoryIndexes=false}.AdvanceYears(branch,3);
            check(engine.AdvanceYears(branch,3).Hash==reference.Hash,"An independently edited market branch rebuilds its catchment index");
        }
        var cancelled=new SimulationSession(new Prehistory(world),original);using var cancellation=new CancellationTokenSource();cancellation.Cancel();bool stopped=false;
        try{cancelled.AdvanceSeconds(SimulationTime.Year,token:cancellation.Token);}catch(OperationCanceledException){stopped=true;}
        check(stopped&&cancelled.Hash==originalHash,"Cancellation preserves the private checkpoint");
        cancelled.AdvanceSeconds(20*SimulationTime.Year,int.MaxValue);
        check(cancelled.Hash==expected.Hash,"Cancelled work can be resumed without stale route dependencies");
        check(original.ComputeHash()==originalHash,"Optimization checks leave the saved source untouched");
        Console.WriteLine("REFERENCE "+expected.Hash+"; "+publications+" publications");
    }
}
