using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryProvincialFrontierTests
{
    public static void Run(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;
        var engine=new Prehistory(world);var before=source.ComputeHash();
        var state=engine.UpgradeToProvincialFrontierHistory(source);engine.Validate(state);
        check(source.ComputeHash()==before&&state.History.Version==Prehistory.ProvincialFrontierHistoryVersion,"Provincial upgrade is explicit and preserves the old checkpoint");
        check(source.History.Archive.Count==state.History.Archive.Count&&source.History.Aggregates.TotalPopulation==state.History.Aggregates.TotalPopulation,"An administrative queue does not found people or rewrite recorded history");
        check(source.History.Cultures.SelectMany(c=>c.Technology).SequenceEqual(state.History.Cultures.SelectMany(c=>c.Technology)),"Provincial frontier setup does not invent technology");
        var e=state.History.Aggregates.Empires;var fronts=e.Frontiers.Posts;
        check(fronts.Length==e.Territories.Posts.Length&&fronts.Any(f=>f.Count>0),"Existing funded districts gain local frontier queues on explicit setup");
        int id=Array.FindIndex(fronts,f=>f.Count>0),cell=fronts[id].At(0),prior=fronts[id].Count;
        var isolated=state.Copy();isolated.History.Aggregates.Empires.Frontiers.Posts[id].Remove(cell);
        check(fronts[id].Count==prior&&fronts[id].At(0)==cell,"Public branches detach provincial bag headers and share only immutable paths");
        bool rejected=false;try{typeof(Prehistory).GetMethod("ValidateProvincialFrontiers",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,new object[]{isolated});}catch(TargetInvocationException ex)when(ex.InnerException is ArgumentException){rejected=true;}
        check(rejected,"Integrity checks reject a missing eligible district frontier");
        var politicalChange=state.Copy();int owner=politicalChange.History.Aggregates.Cells[cell*5+1];
        politicalChange.History.Aggregates.Cells=politicalChange.History.Aggregates.Cells.With(cell*5+1,-1);
        typeof(Prehistory).GetMethod("RefreshProvincialFrontier",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,new object[]{politicalChange,cell});
        check(politicalChange.History.Aggregates.Empires.Frontiers.Posts[id].Positions[cell]>=0,"A court ending cannot erase the underlying district edge before a successor can use it");
        politicalChange.History.Aggregates.Cells=politicalChange.History.Aggregates.Cells.With(cell*5+1,owner);
        var three=engine.AdvanceYears(state,3);engine.Validate(three);
        var partitioned=engine.AdvanceYears(engine.AdvanceYears(state,1),2);
        check(three.Hash==partitioned.Hash,"Provincial frontiers preserve publication-partition determinism");
        check(state.ComputeHash()==state.Hash&&source.ComputeHash()==before,"Provincial continuation leaves both source generations untouched");
        world.Prehistory=three;var loaded=WorldFile.Read(WorldFile.Write(world));
        check(loaded.Prehistory.Hash==three.Hash,"Provincial frontier records survive saved-world round trips");
        var resumed=new Prehistory(loaded).AdvanceYears(loaded.Prehistory,2);var direct=engine.AdvanceYears(three,2);
        check(resumed.Hash==direct.Hash,"Saved provincial queues continue identically");
        var old=engine.AdvanceYears(source,1);
        check(old.History.Version==source.History.Version&&old.History.Aggregates.Empires.Frontiers==null,"Ordinary old-save continuation never silently enables the new queues");
    }
}
