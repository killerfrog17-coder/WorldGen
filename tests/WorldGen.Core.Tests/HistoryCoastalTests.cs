using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;
public static class HistoryCoastalTests
{
    public static void Run(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;string before=source.ComputeHash();var engine=new Prehistory(world);
        var s=engine.UpgradeToCoastalHistory(source);engine.Validate(s);var coasts=s.History.Aggregates.Empires.Connections.Coasts;
        check(s.History.Version==Prehistory.CoastalHistoryVersion&&source.History.Aggregates.Empires.Connections.Coasts==null&&source.ComputeHash()==before,"Coastal setup is an explicit versioned upgrade, not a silent old-save change");
        check(s.History.Aggregates.TotalPopulation==source.History.Aggregates.TotalPopulation&&s.History.Archive.Count==source.History.Archive.Count,"A coastal index does not create people, posts or rewritten history");
        int owner=Array.FindIndex(coasts.Governments,b=>b.Count>0),cell=coasts.Governments[owner].At(0),count=coasts.Governments[owner].Count;
        check(HistoricalCells.Polity(s,cell)==owner,"The index identifies actual local control rather than sovereign claims");
        var branch=s.Copy();branch.History.Aggregates.Empires.Connections.Coasts.Governments[owner].Remove(cell);
        check(coasts.Governments[owner].Count==count,"Coastal bag branches detach their editable headers");
        bool rejected=false;try{typeof(Prehistory).GetMethod("ValidateColonialCoasts",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,new object[]{branch});}catch(TargetInvocationException ex)when(ex.InnerException is ArgumentException){rejected=true;}
        check(rejected,"Integrity checks reject a missing controlled coastline");
        var transfer=s.Copy();int recipient=Array.FindIndex(s.History.Polities,p=>p.EndedYear<0&&p.Id!=owner);
        typeof(Prehistory).GetMethod("SetAggregateLayer",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,new object[]{transfer,cell,1,recipient});
        check(transfer.History.Aggregates.Empires.Connections.Coasts.Governments[owner].Positions[cell]<0&&transfer.History.Aggregates.Empires.Connections.Coasts.Governments[recipient].Positions[cell]>=0,"A one-cell ownership delta maintains both coastal indexes without a holdings census");
        var next=engine.AdvanceYears(s,3);engine.Validate(next);var split=engine.AdvanceYears(engine.AdvanceYears(s,1),2);
        check(next.Hash==split.Hash,"Coastal opportunities remain independent of publication partitioning");
        world.Prehistory=next;var loaded=WorldFile.Read(WorldFile.Write(world));check(loaded.Prehistory.Hash==next.Hash,"Coastal control survives a saved-world round trip");
        check(new Prehistory(loaded).AdvanceYears(loaded.Prehistory,2).Hash==engine.AdvanceYears(next,2).Hash,"Saved coastal indexes resume identically");
        var legacy=engine.AdvanceYears(source,1);check(legacy.History.Version==source.History.Version&&legacy.History.Aggregates.Empires.Connections.Coasts==null,"Ordinary history26 continuation retains its original maritime model");
        check(source.ComputeHash()==before&&s.ComputeHash()==s.Hash,"Coastal processing and tests preserve both original checkpoints");
    }
}
