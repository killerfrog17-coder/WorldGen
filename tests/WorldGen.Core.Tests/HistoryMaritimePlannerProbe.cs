using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Explicit detached counterfactual, never an authoritative actor or valid save.
public static class HistoryMaritimePlannerProbe
{
    public static void Run(string path,string output,int court=21)
    {
        if(File.Exists(output))throw new IOException("Use a new planner-probe output.");
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string input=original.ComputeHash();long reads=original.History.Aggregates.CellReads;
        var engine=new Prehistory(world);var reports=new List<object>();
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,args)!;
        object Point(int cell){var p=world.Surface.Cells[cell].Position;return new{Cell=cell,Latitude=Math.Asin(p.Z)*180/Math.PI,Longitude=Math.Atan2(p.Y,p.X)*180/Math.PI};}
        foreach(var variant in new[]{(Name:"actual-scheduler-baseline",Due:false,Cohesion:-1),(Name:"review-due-only",Due:true,Cohesion:-1),(Name:"review-due-cohesion550",Due:true,Cohesion:550),(Name:"review-due-cohesion600",Due:true,Cohesion:600)})
        {
            var s=original.Copy();var a=s.History.Aggregates;var p=s.History.Polities[court];var map=a.Empires.Connections.Geography.Governments[court];long now=SimulationTime.Seconds(s);
            int actualCohesion=p.Cohesion;if(variant.Cohesion>=0)p.Cohesion=variant.Cohesion;
            bool schedulerWouldWait=map.ScheduledDue>0||now<map.NextPlan;long oldPlan=map.NextPlan;int beforeMissions=map.Missions.Length;long treasury=p.Treasury,total=a.TotalPopulation;
            var food=Enumerable.Range(0,a.Cities.Length).Select(i=>s.Society.Settlements[i].FoodReserve).ToArray();
            var reasons=new SortedDictionary<string,int>();engine.SeaVentureObserver=(id,reason)=>{if(id==court)reasons[reason]=reasons.TryGetValue(reason,out int n)?n+1:1;};
            if(variant.Due||!schedulerWouldWait)
            {
                if(variant.Due)map.NextPlan=now;
                Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
                // The bounded real planner is invoked without scheduling its
                // counterfactual missions. No past arrival or valid save claim.
                Call("PlanSeaVentures",s,court);
            }
            engine.SeaVentureObserver=null;
            var debits=Enumerable.Range(0,a.Cities.Length).Where(i=>food[i]!=s.Society.Settlements[i].FoodReserve).Select(i=>new{Town=i,s.Society.Settlements[i].Name,Before=food[i],After=s.Society.Settlements[i].FoodReserve,Spent=food[i]-s.Society.Settlements[i].FoodReserve}).ToArray();
            var missions=map.Missions.Skip(beforeMissions).Select(m=>new{m.Kind,Departure=Point(m.Source),Target=Point(m.Target),m.Region,m.DistanceM,m.Investment,Departed=m.Departed/(double)SimulationTime.Year,Due=m.Due/(double)SimulationTime.Year,RememberedHost=map.Host[m.Region],RememberedGrade=map.Grade[m.Region],ActualOwner=HistoricalCells.Polity(s,m.Target)}).ToArray();
            if(a.TotalPopulation!=total)throw new Exception("A planning-only probe changed actual people.");
            reports.Add(new{variant.Name,variant.Due,ActualCohesion=actualCohesion,TestedCohesion=p.Cohesion,SchedulerWouldWait=schedulerWouldWait,OriginalNextPlan=oldPlan/(double)SimulationTime.Year,NextPlan=map.NextPlan/(double)SimulationTime.Year,Reasons=reasons,TreasuryBefore=treasury,TreasuryAfter=p.Treasury,CashSpent=treasury-p.Treasury,FoodDebits=debits,NewMissions=missions,TotalPeopleUnchanged=true});
        }
        original.History.Aggregates.CellReads=reads;if(original.ComputeHash()!=input)throw new Exception("Detached planner probes changed the actual source.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Input=path,Hash=input,Year=original.Years,Court=court,CoreSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(Prehistory).Assembly.Location))).ToLowerInvariant(),Warning="Actual-scheduler baseline honors the stored due date. Other variants explicitly bypass only review timing and optionally set cohesion; no free resources/ships/people, no past historical outcomes and no counterfactual valid save are claimed. New missions are funded actual planner proposals, not successful landfalls. Original source unchanged.",Reports=reports},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("PASS detached maritime planner probe source unchanged "+input);
    }
}
