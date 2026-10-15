using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryLivingTests
{
    public static void Run(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var original=world.Prehistory;string hash=original.Hash;
        var source=engine.UpgradeToCurrentHistory(original);engine.Validate(source);
        check(source.History.Version==Prehistory.CurrentHistoryVersion&&source.History.Aggregates.Living!=null,"Living rules are an explicit, saved version upgrade");
        check(original.ComputeHash()==hash&&SocietyView.Population(original)==SocietyView.Population(source),"Updating rules preserves the previous save and total population");
        var session=new SimulationSession(engine,source);var metrics=new List<SimulationWorkMetric>();engine.WorkObserver=metrics.Add;
        session.AdvanceSeconds(20*SimulationTime.Year,int.MaxValue);var expected=session.Snapshot();engine.Validate(expected);
        var partition=new SimulationSession(new Prehistory(world),source);long target=partition.Seconds+20*SimulationTime.Year;
        while(partition.Seconds<target)partition.AdvanceSeconds(target-partition.Seconds,97,maximumGlobalWork:64);
        check(partition.Hash==expected.Hash,"Living exchanges, retirement and political succession are partition deterministic");
        check(metrics.All(m=>m.Stage=="publication copy"||(m.Stage=="aggregate checkpoint copy"||m.Stage=="aggregate checkpoint hash")&&m.Items==0||m.Stage.StartsWith("aggregate event ",StringComparison.Ordinal)),"New historical mechanisms do not restore any global cell turn");
        int localBound=source.History.Version==Prehistory.CommunityHistoryVersion?64000:16000;
        check(metrics.Where(m=>m.Stage.StartsWith("aggregate event ")).All(m=>m.Items<localBound),"Events retain bounded terrain access, including capped physical-area campaigns");
        var isolated=source.Copy();int city=Array.FindIndex(isolated.Society.Settlements,t=>t.Population>0&&isolated.History.Aggregates.Resolve(t.PolityId,isolated.History)<0);
        if(city<0)city=0;
        var life=isolated.History.Aggregates.Living.Cities[city];life.Productivity=.1;life.DisruptedUntil=isolated.Years+200;life.DeclineYears=79;isolated.Seal();
        var decline=new SimulationSession(new Prehistory(world),isolated);decline.AdvanceSeconds(3*SimulationTime.Year,int.MaxValue);var retired=decline.Snapshot();engine.Validate(retired);
        check(retired.History.Aggregates.Living.Cities[city].AbandonedYear>=0&&retired.Society.Settlements[city].Population==0,"An unsupported center can actually cease to exist");
        check(retired.History.Aggregates.TownAt[retired.Society.Settlements[city].Cell]!=city&&!retired.Timeline.Events.Any(e=>e.Process==21&&e.Entity==city),"Ruins stop generating annual work and no longer reserve their former site");
        check(retired.History.Archive.Any(e=>e.Kind=="abandonment"&&e.SettlementId==city),"Abandonment retains dated narrative history");
        check(!retired.History.Aggregates.Cultures[retired.Society.Settlements[city].CultureId].Cities.Items.Fingerprint().Equals(source.History.Aggregates.Cultures[source.Society.Settlements[city].CultureId].Cities.Items.Fingerprint()),"Live city membership is updated without erasing old records");
        var withoutState=source.Copy();int culture=withoutState.Society.Settlements[0].CultureId;withoutState.History.Aggregates.Cultures[culture].CivilizationSince=-1;
        var supports=typeof(Prehistory).GetMethod("AggregatePracticeSupported",BindingFlags.Instance|BindingFlags.NonPublic)!;
        check(!(bool)supports.Invoke(engine,new object[]{withoutState,culture,16,true})!,"A contact does not give gunpowder research to a society with no state-level institutions");
        var noCrops=source.Copy();Array.Clear(noCrops.History.Agriculture[culture].AdoptedYear);
        check(!(bool)supports.Invoke(engine,new object[]{noCrops,culture,1,false})!,"Cultivation is not manufactured from the nonzero floor of a farming score");
        var relay=source.Copy();int routeId=Array.FindIndex(engine.Routes,r=>!r.Sea);var route=engine.Routes[routeId];int destination=route.A,intermediate=route.B;
        relay.History.Aggregates.Regions[destination].Routes=new[]{routeId};relay.History.Aggregates.Regions[destination].Culture=-1;relay.History.Aggregates.Regions[intermediate].Culture=-1;
        relay.Population[destination]=relay.Population[intermediate]=1000;relay.History.Aggregates.Living.Regions[intermediate].Teacher=culture;relay.History.Aggregates.Living.Regions[intermediate].Exposure=1000;
        relay.History.Aggregates.Living.Regions[destination].Teacher=-1;int beforeTowns=relay.Society.Settlements.Length;
        typeof(Prehistory).GetMethod("AdvanceLivingRegionalExchange",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,new object[]{relay,destination,100000});
        check(relay.History.Aggregates.Living.Regions[destination].Teacher==culture,"Exchange can relay through an inhabited region without its own colored advanced culture");
        check(relay.Society.Settlements.Length==beforeTowns&&relay.History.Aggregates.Regions[destination].Culture<0,"An exchange relay does not automatically grant a city, culture or state");
        relay.Population[destination]=relay.Population[intermediate]=0;relay.History.Aggregates.Living.Regions[destination].SettledPopulation=1000;relay.History.Aggregates.Living.Regions[intermediate].SettledPopulation=1000;relay.History.Aggregates.Living.Regions[destination].Teacher=-1;
        typeof(Prehistory).GetMethod("AdvanceLivingRegionalExchange",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,new object[]{relay,destination,100000});
        check(relay.History.Aggregates.Living.Regions[destination].Teacher==culture,"A settled region is not treated as empty when its unallocated forager population is zero");
        var canFound=typeof(Prehistory).GetMethod("CanFoundLivingTown",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var town=source.Society.Settlements[0];check(!(bool)canFound.Invoke(engine,new object[]{source,town.CultureId,town.Cell,-1})!,"An occupied center cannot be duplicated");
        var chain=new HistoricalAggregateState{Successors=new HistoricalCellTree(32)};chain.Successors=chain.Successors.With(0,1).With(1,2);var record=new HistoryState{Polities=new[]{new HistoricalPolity{Id=0,EndedYear=100},new HistoricalPolity{Id=1,EndedYear=200},new HistoricalPolity{Id=2}}};
        check(chain.Resolve(0,record)==2&&chain.PopulationCohort(0)==2,"Political and demographic ownership follow successor governments");record.Polities[2].EndedYear=300;
        check(chain.Resolve(0,record)==-1&&chain.PopulationCohort(0)==2,"Communities survive a final court collapse without duplicating or losing their people");
        var cancelEngine=new Prehistory(world);var cancelled=new SimulationSession(cancelEngine,source);using var token=new CancellationTokenSource();int calls=0;cancelEngine.YearWorkObserver=(_,_)=>{if(++calls==7)token.Cancel();};bool stopped=false;
        try{cancelled.AdvanceSeconds(20*SimulationTime.Year,int.MaxValue,token.Token);}catch(OperationCanceledException){stopped=true;}
        check(stopped&&cancelled.Hash==source.Hash,"A cancelled living-history transaction cannot leak contact or political edits");cancelEngine.YearWorkObserver=null;cancelled.AdvanceSeconds(20*SimulationTime.Year,int.MaxValue);check(cancelled.Hash==expected.Hash,"Cancellation/retry preserves exchanges and succession");
        world.Prehistory=expected;var loaded=WorldFile.Read(WorldFile.Write(world));check(loaded.Prehistory.Hash==expected.Hash,"New city life cycles, account transfers and contact indices survive save/load");
        var resume=new SimulationSession(new Prehistory(loaded),loaded.Prehistory);resume.AdvanceSeconds(10*SimulationTime.Year,int.MaxValue);session.AdvanceSeconds(10*SimulationTime.Year,int.MaxValue);check(resume.Hash==session.Hash,"Reloaded living history continues identically");
        var longRun=new SimulationSession(new Prehistory(world),source);longRun.AdvanceSeconds(1000*SimulationTime.Year,int.MaxValue);var final=longRun.Snapshot();engine.Validate(final);
        check(final.History.Archive.Skip(source.History.Archive.Count).Any(e=>e.Kind=="regime-change"),"Long playback contains governmental succession, not only immortal state IDs or wholesale collapse");
        check(original.ComputeHash()==hash,"All long-run tests leave the original world untouched");
    }
}
