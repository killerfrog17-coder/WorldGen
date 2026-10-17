using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Explicit offline, bounded camera-export window chosen from actual chronology.
public static class HistoryPoliticalWindowAudit
{
    public static void Run(string source,string output,int start,int years,int interval)
    {
        if(start<0||years<1||years>500||interval<1||interval>years)throw new ArgumentOutOfRangeException();
        if(Directory.Exists(output))throw new IOException("Use a new political window folder.");
        Directory.CreateDirectory(output);var world=WorldFile.Read(File.ReadAllBytes(source));world.Prehistory=null;
        var engine=new Prehistory(world);var session=new SimulationSession(engine,engine.Start(engine.Prepare()));
        while(session.LastSnapshot.Years<start)
        {
            int step=session.LastSnapshot.History.Polities.Length==0?1000:100;
            session.AdvanceSeconds(Math.Min(step,start-session.LastSnapshot.Years)*SimulationTime.Year,int.MaxValue);
            if(session.LastSnapshot.Years%20000==0)Console.WriteLine("Political window replay "+session.LastSnapshot.Years);
        }
        var rows=new List<object>();int end=start+years;
        while(true)
        {
            var s=session.Snapshot();engine.Validate(s);world.Prehistory=s;var h=s.History;var a=h.Aggregates;
            string hash=s.ComputeHash(),folder=Path.Combine(output,"year-"+s.Years);Directory.CreateDirectory(folder);
            HistoryAudit.Snapshot(world,engine,s,folder,new[]{HistoryOverlay.States},compressed:true,backgroundOpacity:0,overlayOpacity:1,includeLabels:false);
            var courts=h.Polities.Where(p=>p.EndedYear<0).Select(p=>new{p.Id,p.Name,p.FoundedYear,p.ParentId,p.CapitalId,p.Population,p.Cohesion,p.Exhaustion,p.AreaKm2,Cities=a.Polities[p.Id].CityCount}).ToArray();
            var towns=s.Society.Settlements.Where(t=>t.Population>0&&a.Living.Cities[t.Id].AbandonedYear<0).Select(t=>new{t.Id,t.Cell,t.Population,Polity=HistoricalCells.Polity(s,t.Cell)}).ToArray();
            var events=h.Archive.Where(e=>e.Year>s.Years-interval&&e.Year<=s.Years&&(e.Kind=="war"||e.Kind=="peace"||e.Kind=="field-battle"||e.Kind=="realm-succession-crisis"||e.Kind=="secession"||e.Kind=="conquest"||e.Kind=="independence"))
                .Select(e=>new{e.Id,e.Year,e.Kind,e.PolityId,e.OtherPolityId,e.Cell,e.Text}).ToArray();
            if(s.ComputeHash()!=hash)throw new Exception("Political export changed its source snapshot.");
            rows.Add(new{Year=s.Years,Era=WorldEraView.Of(s),Hash=hash,Population=a.TotalPopulation,Courts=courts,Towns=towns,Events=events});
            string assembly=typeof(Prehistory).Assembly.Location;
            File.WriteAllText(Path.Combine(output,"episode.json"),JsonSerializer.Serialize(new{Source=Path.GetFullPath(source),RequestedStart=start,Start=start,Interval=interval,
                CoreAssembly=assembly,CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),Rows=rows,
                Notes=new[]{"Explicit offline exact-year political window, selected from an actual prior chronology; fresh replay, no Core rule or input-world edits.",
                    "Opaque States over neutral background; fill identifies ruling culture, not one court. Court IDs and adjacent-frame events accompany the actual border exports.",
                    "Town IDs are active snapshot observations, not proof of every unsaved annual status. No extra duplicate final.world is written."}
            },new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"Political window {s.Years}: {courts.Length} courts, {towns.Length} towns, {events.Length} recent events, {hash}");
            if(s.Years>=end)break;
            session.AdvanceSeconds(Math.Min(interval,end-s.Years)*SimulationTime.Year,int.MaxValue);
        }
    }
}
