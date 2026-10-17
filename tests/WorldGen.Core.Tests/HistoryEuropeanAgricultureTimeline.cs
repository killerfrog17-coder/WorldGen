using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// One fresh frozen-engine replay, sparse metadata only. No saved replay worlds.
public static class HistoryEuropeanAgricultureTimeline
{
    public static void Run(string source,string folder,int start=171000,int end=179800,Func<World,PrehistoryState,Prehistory,object> extraProbe=null)
    {
        if(Directory.Exists(folder))throw new IOException("Use a new European timeline folder.");
        Directory.CreateDirectory(folder);var world=WorldFile.Read(File.ReadAllBytes(source));
        string expectedFinal=world.Prehistory.ComputeHash();int expectedYear=world.Prehistory.Years;string acceptedWorldHash=world.Prehistory.WorldHash;
        world.Prehistory=null;var engine=new Prehistory(world);var initial=engine.Start(engine.Prepare());
        if(initial.History.Aggregates.Empires.Connections.Realms.Version!=17)throw new Exception("Timeline requires frozen source17.");
        var session=new SimulationSession(engine,initial);var watch=Stopwatch.StartNew();
        var options=new JsonSerializerOptions{IncludeFields=true};
        object Place(int cell)=>new{Cell=cell,Latitude=Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI,Longitude=Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI};
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(engine,values);
        int[] sites={176034,163381,177775,179194,172937};string[] names={"France accepted field","Iberia accepted field","Sanala accepted origin","Mesikani accepted origin","Retongkiting accepted origin"};
        string match170000=null;int rows=0;long totalDiagnosticReads=0;
        using var writer=new StreamWriter(Path.Combine(folder,"timeline.jsonl"));
        while(session.LastSnapshot.Years<end)
        {
            // Match the reviewed driver's1000yr prestate/100yr poststate
            // publication scheme. Focus cultures269/517/617 do not exist before
            // the first100yr reviewed interval, so no early target rows are lost.
            bool hasState=session.LastSnapshot.History.Polities.Length>0;
            int step=hasState?100:1000;
            session.AdvanceSeconds(Math.Min(step,end-session.LastSnapshot.Years)*SimulationTime.Year,int.MaxValue);
            var published=session.LastSnapshot;
            if(published.Years==170000)match170000=published.ComputeHash();
            if(published.Years<start)
            {if(published.Years%10000==0)Console.WriteLine("European replay "+published.Years+" "+watch.Elapsed.TotalSeconds.ToString("F1")+"s");continue;}
            var probe=published.Copy();var a=probe.History.Aggregates;long reads=a.CellReads,writes=a.CellWrites,events=a.ProcessedLocalEvents;
            Call("PrepareAggregateEnvironment",probe);Call("PrepareAggregateRelations",probe);
            var fieldRows=sites.Select((cell,index)=>
            {
                int region=engine.CellPatch[cell],culture=a.Regions[region].Culture,actual=HistoricalCells.Culture(probe,cell),owner=HistoricalCells.Polity(probe,cell);
                return new{Name=names[index],Place=Place(cell),Region=region,RegionalCulture=culture,CellCulture=actual,Owner=owner,
                    RegionalFarming=culture>=0?probe.Society.Cultures[culture].Farming:0,RegionalPopulation=probe.Population[region],
                    ContactResidents=(long)Call("ProvincialContactResidents",probe,region,false),LocalTeacher=a.Living.Regions[region].Teacher,
                    LastExchangeYear=a.Living.Regions[region].LastExchangeYear,Exposure=a.Living.Regions[region].Exposure};
            }).ToArray();
            int[] cultures=new[]{269,517,617}.Concat(fieldRows.Select(field=>field.RegionalCulture)).Where(id=>id>=0&&id<probe.Society.Cultures.Length&&a.Cultures[id].Cities.Count==0).Distinct().ToArray();
            var arithmetic=HistorySeedAdoptionArithmeticAudit.Inspect(world,probe,engine,cultures);
            var additionalEvidence=extraProbe?.Invoke(world,probe,engine);
            long delta=a.CellReads-reads;a.CellReads=reads;totalDiagnosticReads+=delta;
            string after=probe.ComputeHash();
            if(after!=published.Hash||a.CellWrites!=writes||a.ProcessedLocalEvents!=events)throw new Exception("Timeline probe changed authoritative state at "+published.Years+" actual="+after+" expected="+published.Hash);
            writer.WriteLine(JsonSerializer.Serialize(new{Year=published.Years,Hash=published.Hash,DiagnosticReadDelta=delta,ProbeHashPreserved=true,Fields=fieldRows,Cultures=arithmetic,AdditionalEvidence=additionalEvidence},options));writer.Flush();rows++;
            if(published.Years%500==0)Console.WriteLine("European timeline "+published.Years+" rows"+rows+" "+watch.Elapsed.TotalSeconds.ToString("F1")+"s");
        }
        var final=session.LastSnapshot;string finalHash=final.ComputeHash();string validationError=null;
        try{engine.Validate(final);}catch(Exception error){validationError=error.Message;}
        string assembly=typeof(Prehistory).Assembly.Location;
        File.WriteAllText(Path.Combine(folder,"summary.json"),JsonSerializer.Serialize(new{Source=Path.GetFullPath(source),AcceptedWorldHash=acceptedWorldHash,
            FrozenAssembly=assembly,CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),FirstState=final.History.Polities.Min(p=>p.FoundedYear),
            Year170000Hash=match170000,Year170000HasExternalMatchedCheckpoint=false,FinalYear=final.Years,FinalHash=finalHash,
            ExpectedSavedYear=expectedYear,ExpectedSavedHash=expectedFinal,ExactFinalMatch=final.Years==expectedYear&&finalHash==expectedFinal,
            Rows=rows,TotalDiagnosticReadCounterDelta=totalDiagnosticReads,ValidationPassed=validationError==null,ValidationError=validationError,WallSeconds=watch.Elapsed.TotalSeconds,
            Notes=new[]{"Fresh replay through ordinary Prepare/Start on the accepted source terrain, frozen Q17 Core. No generator route, rules upgrade, seed physics edit, replay save or map export.",
                "Uses original reviewed1000yr-prestate/100yr-poststate publication scheme.171000 is observed, then172000 before100yr steps; focus269/517/617 do not yet exist during the skipped early centuries.",
                "Every observation is a detached probe; only instrumented CellReads is restored on that probe, then its full hash must equal the published snapshot. No adoption/delivery handler is called.",
                "Current-snapshot gain estimates remain conditional, not future guaranteed adoption dates. Native best fit is offered only when the actual native mask admits that group.",
                "Exact final hash comparison establishes equivalence only when true. The170000 hash is recorded without inventing an unavailable external matching checkpoint."}},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine("European replay finished "+final.Years+" hash"+finalHash+" exact="+(final.Years==expectedYear&&finalHash==expectedFinal));
    }
}
