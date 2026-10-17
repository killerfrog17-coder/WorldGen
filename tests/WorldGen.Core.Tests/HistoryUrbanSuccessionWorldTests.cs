using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryUrbanSuccessionWorldTests
{
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));string input=world.Prehistory.Hash;var engine=new Prehistory(world);
        var basis=world.Prehistory.Copy();var h=basis.History;var a=h.Aggregates;
        int owner=Array.FindIndex(h.Polities,p=>p.EndedYear<0&&a.Polities[p.Id].CityCount>=4&&a.Polities[p.Id].Urban>20000);
        if(owner<0)throw new Exception("Use a real early multicenter court checkpoint.");
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
        Call("PrepareAggregateEnvironment",basis);Call("PrepareAggregateRelations",basis);
        var sourceQueue=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;
        PrehistoryState found=null;int year=-1;
        for(int j=0;j<300;j++)
        {
            var candidate=basis.Copy();candidate.History.Aggregates.Empires.Connections.Realms.Version=13;
            candidate.Years=basis.Years+j;candidate.Timeline.Seconds=(long)candidate.Years*SimulationTime.Year;
            sourceQueue.SetValue(engine,new SimulationEventQueue(Array.Empty<SimulationEvent>()));
            try{Call("AppointAggregateLeader",candidate,owner);}finally{sourceQueue.SetValue(engine,null);}
            if(candidate.History.Aggregates.Empires.Connections.Realms.Governments[owner].CrisisUntil<=candidate.Years)continue;
            found=candidate;year=candidate.Years;break;
        }
        if(found==null)throw new Exception("No local-center succession competition in300 controlled production rolls.");
        var preceding=basis.Copy();preceding.History.Aggregates.Empires.Connections.Realms.Version=12;preceding.Years=year;preceding.Timeline.Seconds=(long)year*SimulationTime.Year;
        sourceQueue.SetValue(engine,new SimulationEventQueue(Array.Empty<SimulationEvent>()));
        try{Call("AppointAggregateLeader",preceding,owner);}finally{sourceQueue.SetValue(engine,null);}
        check(found.History.Aggregates.Empires.Connections.Realms.Governments[owner].RegimeChanges>preceding.History.Aggregates.Empires.Connections.Realms.Governments[owner].RegimeChanges,"A real peacetime multicenter court can face competition absent from the preceding threshold-only succession path.");
        check(found.History.Polities.Length==basis.History.Polities.Length&&found.Society.Settlements.Length==basis.Society.Settlements.Length,"A succession dispute does not instantly mint states or settlements.");
        check(found.History.Aggregates.TotalPopulation==a.TotalPopulation&&found.History.Polities[owner].Treasury==h.Polities[owner].Treasury&&found.History.Polities[owner].Army==h.Polities[owner].Army,"Competition creates no treasury, army or population.");
        check(found.History.Aggregates.Empires.Connections.Realms.Governments[owner].FoundedYear==a.Empires.Connections.Realms.Governments[owner].FoundedYear,"Disputed government succession does not refound the continuing country.");
        check(found.History.Dynamics.Leaders.Length==h.Dynamics.Leaders.Length+1&&found.History.Dynamics.Leaders.Last().PolityId==owner,"The actual production appointment produces and seals a real successor ruler.");
        check(world.Prehistory.ComputeHash()==input,"The real-world succession fixture leaves its saved source intact.");
        Console.WriteLine($"Urban competition at{year}, state{owner}, {a.Polities[owner].CityCount}actual cities.");
    }
}
