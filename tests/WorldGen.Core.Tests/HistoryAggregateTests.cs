using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryAggregateTests
{
    public static void Fixture(string input,string output)
    {
        if(File.Exists(output))throw new IOException("Refusing to overwrite an existing world: "+output);
        var world=WorldFile.Read(File.ReadAllBytes(input));var engine=new Prehistory(world);world.Prehistory=engine.UpgradeToAggregateHistory(world.Prehistory);engine.Validate(world.Prehistory);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllBytes(output,WorldFile.Write(world));Console.WriteLine("Prepared aggregate benchmark fixture "+world.Prehistory.Hash);
    }
    public static void Run(string path,Action<bool,string> check)
    {
        var tree=HistoricalCellTree.From(200000,i=>i%31==0?i:-1);string root=tree.Fingerprint();var changed=tree.With(199999,42);
        check(tree[199999]==-1&&changed[199999]==42,"Persistent spatial edits preserve prior snapshots");
        check(tree.Fingerprint()==root&&changed.Fingerprint()!=root,"Spatial Merkle hashes change only along the edited path");
        check(tree.Fingerprint(true)==root,"Full spatial integrity validation matches cached roots");
        var empty=new HistoricalCellTree(200000);check(empty.With(199999,42).With(199999,-1).Fingerprint()==empty.Fingerprint(),"Sparse spatial hashes are canonical after deletion, not dependent on page materialization");
        var bag=new HistoricalLocalBag();for(int i=0;i<1000;i++)bag.Add(i);bag.Add(17);var old=bag.Copy();bag.Remove(17);
        check(bag.Count==999&&old.Count==1000&&old.Positions[17]>=0&&bag.Positions[17]==-1,"Frontier removal is local, unique, and copy isolated");
        for(int i=0;i<bag.Count;i++)check(bag.Positions[bag.At(i)]==i,"Frontier swap index "+i);
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string originalHash=original.Hash;var engine=new Prehistory(world);
        var source=engine.UpgradeToAggregateHistory(original);engine.Validate(source);
        string expectedVersion=Prehistory.IsAggregateHistoryVersion(original.History.Version)?original.History.Version:Prehistory.AggregateHistoryVersion;
        check(source.History.Version==expectedVersion&&source.History.Aggregates!=null,"Explicit aggregate migration never downgrades a newer actor model");
        check(source.History.CellPolity.Length==0&&source.History.CellReligion.Length==0&&source.History.CellMarket.Length==0&&source.Society.CellCulture.Length==0,"Routine state contains no dense mutable cell arrays");
        check(original.ComputeHash()==originalHash,"Upgrading leaves original geography and historical snapshot unchanged");
        check(SocietyView.Population(source)==SocietyView.Population(original),"Upgrade conserves the world's complete human population");
        var metrics=new List<SimulationWorkMetric>();engine.WorkObserver=metrics.Add;
        var direct=new SimulationSession(engine,source);direct.AdvanceSeconds(20*SimulationTime.Year,int.MaxValue);var expected=direct.Snapshot();engine.Validate(expected);
        check(metrics.Any(m=>m.Stage=="aggregate event 21"),"City events actually execute");
        check(metrics.All(m=>m.Stage=="publication copy"||(m.Stage=="aggregate checkpoint copy"||m.Stage=="aggregate checkpoint hash")&&m.Items==0||m.Stage.StartsWith("aggregate event ",StringComparison.Ordinal)),"No legacy global yearly, religious, cultural, routing or market pass executes");
        int localBound=source.History.Version==Prehistory.CommunityHistoryVersion||source.History.Version==Prehistory.DiffusionHistoryVersion||source.History.Version==Prehistory.AgronomicHistoryVersion||source.History.Version==Prehistory.TechnicalHistoryVersion||source.History.Version==Prehistory.ProvincialFrontierHistoryVersion||source.History.Version==Prehistory.ColonialHistoryVersion||source.History.Version==Prehistory.CoastalHistoryVersion||source.History.Version==Prehistory.TerritorialHistoryVersion?64000:source.History.Version==Prehistory.RegionalHistoryVersion?16000:2000;
        Console.WriteLine("Maximum local cell reads per event: "+metrics.Where(m=>m.Stage.StartsWith("aggregate event ")).Max(m=>m.Items)+"; supported model bound "+localBound);
        check(metrics.Where(m=>m.Stage.StartsWith("aggregate event ")).All(m=>m.Items<localBound),"Every individual event has bounded local cell access, independent of territorial membership");
        var partition=new SimulationSession(new Prehistory(world),source);long target=partition.Seconds+20*SimulationTime.Year;
        while(partition.Seconds<target)partition.AdvanceSeconds(target-partition.Seconds,97,maximumGlobalWork:64);
        check(partition.Hash==expected.Hash,"Event budgets and publication partitioning preserve the exact result");
        var isolation=new SimulationSession(new Prehistory(world),source);var exposed=isolation.AdvanceSeconds(5*SimulationTime.Year,int.MaxValue);
        exposed.Society.Settlements[0].Population+=10000;exposed.History.Archive[0].Text="Edited public record";exposed.History.Aggregates.Cells=exposed.History.Aggregates.Cells.With(0,123);
        isolation.AdvanceSeconds(15*SimulationTime.Year,int.MaxValue);check(isolation.Hash==expected.Hash,"Public cell roots, actors and lifetime archive cannot mutate private playback");
        var cancelledEngine=new Prehistory(world);var cancelled=new SimulationSession(cancelledEngine,source);using var cancellation=new CancellationTokenSource();int calls=0;
        cancelledEngine.YearWorkObserver=(_,_)=>{if(++calls==5)cancellation.Cancel();};bool stopped=false;
        try{cancelled.AdvanceSeconds(20*SimulationTime.Year,int.MaxValue,cancellation.Token);}catch(OperationCanceledException){stopped=true;}
        check(stopped&&cancelled.Hash==source.Hash,"Cancellation inside a sequence rolls the entire transaction back");cancelledEngine.YearWorkObserver=null;
        cancelled.AdvanceSeconds(20*SimulationTime.Year,int.MaxValue);check(cancelled.Hash==expected.Hash,"Abandoned relation indexes and private edits cannot leak into resumed playback");
        world.Prehistory=expected;var bytes=WorldFile.Write(world);var loaded=WorldFile.Read(bytes);check(loaded.Prehistory.Hash==expected.Hash,"Persistent geography and accounts survive real binary world serialization");
        var continued=new SimulationSession(new Prehistory(loaded),loaded.Prehistory);continued.AdvanceSeconds(3*SimulationTime.Year,int.MaxValue);direct.AdvanceSeconds(3*SimulationTime.Year,int.MaxValue);
        check(continued.Hash==direct.Hash,"Reloaded queues, spatial roots and static environment preparation preserve continuation");
        var tampered=expected.Copy();tampered.History.Archive[0]=tampered.History.Archive[0].Copy();tampered.History.Archive[0].Text="Corrupt frozen prefix";bool rejected=false;try{engine.Validate(tampered);}catch(ArgumentException){rejected=true;}check(rejected,"Cached append-only digests do not bypass untrusted save validation");
        check(SocietyView.OverlayId(expected,expected.Society.Settlements[0].Cell,HistoryOverlay.Cultures)==expected.Society.Settlements[0].CultureId,"Map inspection uses authoritative persistent local culture");
        check(original.ComputeHash()==originalHash,"All simulation branches preserve the original save");
        var freshWorld=new World();foreach(var field in typeof(World).GetFields())field.SetValue(freshWorld,field.GetValue(world));freshWorld.Prehistory=null;
        var freshEngine=new Prehistory(freshWorld);var prepared=freshEngine.Prepare();var started=freshEngine.Start(prepared);freshEngine.Validate(started);
        check(prepared.History.Version==Prehistory.CurrentHistoryVersion&&prepared.History.Aggregates.TotalPopulation==0&&started.History.Aggregates.TotalPopulation==200,"Default new worlds use the actor model and conserve the founding band");
        var early=new SimulationSession(freshEngine,started);early.AdvanceSeconds(1000*SimulationTime.Year,int.MaxValue);freshEngine.Validate(early.Snapshot());
        check(early.LastSnapshot.Society.Settlements.Length==0&&early.LastSnapshot.History.Polities.Length==0&&SocietyView.Population(early.LastSnapshot)>=200,"Forager-only playback grows and disperses without manufacturing early cities or states");
    }
}
