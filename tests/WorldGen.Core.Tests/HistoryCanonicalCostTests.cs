using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryCanonicalCostTests
{
    static readonly Type Costs=typeof(StateHash).Assembly.GetType("WorldGen.Core.CanonicalHashCosts")!;
    static readonly MethodInfo BeginMethod=Costs.GetMethod("Begin",BindingFlags.NonPublic|BindingFlags.Static)!;
    static readonly MethodInfo OwnedMethod=typeof(StateHash).GetMethod("ComputeOwnedHistorical",BindingFlags.NonPublic|BindingFlags.Static)!;
    static readonly MethodInfo FreezeMethod=typeof(StateHash).GetMethod("FreezeHistoricalRecord",BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(object)},null)!;
    static IDisposable Begin(Action<CanonicalHashMetric> observer,int year)=>(IDisposable)BeginMethod.Invoke(null,new object[]{observer,year})!;
    static void Complete(IDisposable scope)=>scope.GetType().GetMethod("Complete",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scope,null);
    static string Owned(object value,CancellationToken token=default)=>(string)OwnedMethod.Invoke(null,new object[]{value,new HistoryState(),token})!;
    public static void Run(Action<bool,string> check)
    {
        check(Begin(null,0)==null,"Inactive diagnostic scope performs no allocation or callback.");
        var rows=new List<CanonicalHashMetric>();var tree=new HistoricalCellTree(73).With(17,29);string expected=new HistoricalCellTree(73).With(17,29).Fingerprint();
        using(var scope=Begin(rows.Add,7))
        {
            check(tree.Fingerprint()==expected,"Active diagnostic counters do not change immutable-tree hashes.");
            tree.Fingerprint(); // Already cached: do not count the tree twice.
            FreezeMethod.Invoke(null,new object[]{new HistoricalCoalitionEntry{AllyId=13}});
            check(Owned(new[]{1,2,3})==StateHash.Compute(new[]{1,2,3}),"Root timing does not change canonical outcome bytes.");Complete(scope);
        }
        var metric=rows.Single();check(metric.Completed&&metric.Year==7,"One completed seal produces one dated metric.");
        check(metric.UncachedTreeNodes==5&&metric.TreeFingerprints==1&&metric.TreePayloadBytes==4*1025+129+36,"Counters report exactly five new radix nodes plus one tree-root payload, not cached reads.");
        check(metric.NewFrozenRecords==1&&metric.NewFrozenBytes>0&&metric.SealMilliseconds>=metric.RootMilliseconds&&metric.RootMilliseconds>=0,"New frozen bytes and non-authoritative root/total timing are counted once.");
        rows.Clear();
        using(var outer=Begin(rows.Add,11))
        {
            new HistoricalCellTree(11).With(3,7).Fingerprint();
            using(var inner=Begin(rows.Add,12)){new HistoricalCellTree(13).With(5,11).Fingerprint();Complete(inner);}
            new HistoricalCellTree(17).With(7,13).Fingerprint();Complete(outer);
        }
        check(rows.Count==2&&rows[0].Year==12&&rows[0].TreeFingerprints==1&&rows[1].Year==11&&rows[1].TreeFingerprints==2,"Nested scopes isolate counters and restore the outer scope in stack order.");
        rows.Clear();using(var cancellation=new CancellationTokenSource())
        {
            cancellation.Cancel();bool cancelled=false;
            using(var scope=Begin(rows.Add,17))try{Owned(new[]{17},cancellation.Token);Complete(scope);}catch(TargetInvocationException ex)when(ex.InnerException is OperationCanceledException){cancelled=true;}
            check(cancelled&&rows.Count==1&&!rows[0].Completed,"Cancellation emits one incomplete diagnostic without committing a result.");
        }
        rows.Clear();bool observerFailed=false;
        try{using(var scope=Begin(_=>throw new InvalidOperationException("Test observer"),19)){Complete(scope);}}catch(InvalidOperationException){observerFailed=true;}
        using(var scope=Begin(rows.Add,23)){new HistoricalCellTree(29).With(2,3).Fingerprint();Complete(scope);}
        check(observerFailed&&rows.Single().Year==23&&rows[0].TreeFingerprints==1,"Observer exceptions restore thread-local context before invoking user code.");
        var outOfOrder=Begin(rows.Add,29);var nested=Begin(rows.Add,31);bool rejected=false;
        try{outOfOrder.Dispose();}catch(InvalidOperationException){rejected=true;}finally{nested.Dispose();outOfOrder.Dispose();}
        check(rejected,"Out-of-order disposal cannot silently corrupt a diagnostic scope stack.");
        var concurrent=new CanonicalHashMetric[32];var results=new bool[32];Parallel.For(0,32,i=>
        {
            using(var scope=Begin(m=>concurrent[i]=m,i)){string hash=new HistoricalCellTree(37).With(i,i*7919).Fingerprint();results[i]=hash==new HistoricalCellTree(37).With(i,i*7919).Fingerprint();Complete(scope);}
        });
        for(int i=0;i<32;i++)check(results[i]&&concurrent[i].Year==i&&concurrent[i].TreeFingerprints==2&&concurrent[i].UncachedTreeNodes==10,"Concurrent scopes never share counters, tree digests or outcomes.");
        check(Begin(null,37)==null,"Nested, cancelled and failed observers leave ordinary diagnostics inactive.");
    }
    public static void World(string source,string output,int steps=20)
    {
        if(File.Exists(output)||File.Exists(output+".json"))throw new IOException("Do not overwrite cost evidence.");
        var world=WorldFile.Read(File.ReadAllBytes(source));var initial=world.Prehistory;string original=initial.Hash;var engine=new Prehistory(world);var session=new SimulationSession(engine,initial);
        var costs=new List<CanonicalHashMetric>();var work=new List<SimulationWorkMetric>();engine.CanonicalHashObserver=costs.Add;engine.WorkObserver=work.Add;
        for(int step=0;step<steps;step++)
        {var next=session.AdvanceSeconds(5*SimulationTime.Year,int.MaxValue);Console.WriteLine(JsonSerializer.Serialize(new{Step=step+1,Year=next.Years,Hash=next.Hash,Cost=costs.Last()}));}
        if(initial.ComputeHash()!=original||initial.Hash!=original)throw new Exception("Scoped diagnostic playback mutated its public input.");
        var lines=new List<string>{"year,completed,sealMilliseconds,rootMilliseconds,uncachedTreeNodes,treePayloadBytes,treeFingerprints,newFrozenRecords,newFrozenBytes"};
        lines.AddRange(costs.Select(m=>FormattableString.Invariant($"{m.Year},{m.Completed},{m.SealMilliseconds:F6},{m.RootMilliseconds:F6},{m.UncachedTreeNodes},{m.TreePayloadBytes},{m.TreeFingerprints},{m.NewFrozenRecords},{m.NewFrozenBytes}")));
        File.WriteAllLines(output,lines);File.WriteAllText(output+".json",JsonSerializer.Serialize(new{InitialHash=original,FinalHash=session.Hash,Years=steps*5,Costs=costs,Work=work.GroupBy(m=>m.Stage).Select(g=>new{Stage=g.Key,Count=g.Count(),Milliseconds=g.Sum(m=>m.Milliseconds),AllocatedBytes=g.Sum(m=>m.AllocatedBytes)})},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine("HASH "+session.Hash);
    }
}
