using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Deliberate offline censuses/exports. No ordinary simulation turn calls this.
public static class HistoryPoliticalEpisodes
{
    public static void Run(string source,string output,int era,int years,int interval)
    {
        if(era<4||era>10||years<1||years>1000||interval<1||interval>years)throw new ArgumentOutOfRangeException();
        if(Directory.Exists(output))throw new IOException("Use a new episode folder.");
        Directory.CreateDirectory(output);var world=WorldFile.Read(File.ReadAllBytes(source));world.Prehistory=null;
        var engine=new Prehistory(world);var session=new SimulationSession(engine,engine.Start(engine.Prepare()));
        while(WorldEraView.Of(session.LastSnapshot)<era&&session.LastSnapshot.Years<350000)
            session.AdvanceSeconds((session.LastSnapshot.History.Polities.Length==0?1000:25)*SimulationTime.Year,int.MaxValue);
        if(WorldEraView.Of(session.LastSnapshot)<era)throw new InvalidOperationException("Requested era not reached.");
        int start=session.LastSnapshot.Years,end=start+years;var rows=new List<object>();
        while(true)
        {
            var s=session.Snapshot();world.Prehistory=s;
            try{engine.Validate(s);}catch(ArgumentException)
            {
                var recovery=s.History.Aggregates.Empires.Connections.ServiceRecovery;
                Console.Error.WriteLine($"Episode validation at{s.Years}: service version={recovery?.Version}, cityedges={recovery?.CityEdges?.Length}, cities={s.History.Aggregates.Cities.Length}, cursor={recovery?.PendingCursor}, reassigned={recovery?.ReassignedCells}, withdrawn={recovery?.WithdrawnCells}, withdrawnKm2={recovery?.WithdrawnKm2}.");
                throw;
            }
            string folder=Path.Combine(output,"year-"+s.Years);Directory.CreateDirectory(folder);
            HistoryAudit.Snapshot(world,engine,s,folder,new[]{HistoryOverlay.States,HistoryOverlay.Territories},compressed:true,backgroundOpacity:0,overlayOpacity:1,includeLabels:false);
            var h=s.History;var a=h.Aggregates;
            var courts=h.Polities.Where(p=>p.EndedYear<0).Select(p=>new{p.Id,p.Name,p.FoundedYear,p.ParentId,p.CapitalId,p.CultureId,p.Population,p.Cohesion,p.Exhaustion,p.AreaKm2,Cities=a.Polities[p.Id].CityCount,Patron=HistoricalImperialHierarchy.Patron(s,p.Id),Sovereign=HistoricalImperialHierarchy.Sovereign(s,p.Id),CountryFounded=HistoricalRealmLifecycles.Founded(s,p.Id)}).ToArray();
            var recent=h.Archive.Where(e=>e.Year>=s.Years-interval&&e.Year<=s.Years&&(e.Kind=="war"||e.Kind=="peace"||e.Kind=="field-battle"||e.Kind=="realm-succession-crisis"||e.Kind=="secession"||e.Kind=="conquest"||e.Kind=="independence"||e.Kind=="regional-realignment")).Select(e=>new{e.Id,e.Year,e.Kind,e.PolityId,e.OtherPolityId,e.SettlementId,e.Cell,e.Text}).ToArray();
            var campaigns=h.Dynamics.Campaigns.Where(c=>c.EndedYear<0).Select(c=>{var operation=a.Empires.Connections.Military?.Campaigns[c.Id];return new{c.Id,c.Name,c.A,c.B,c.AttackerId,c.StartedYear,Aim=c.Aim.ToString(),c.CasualtiesA,c.CasualtiesB,c.Conquests,Theater=a.Provinces.Theaters[c.Id].Cell,CommittedA=operation?.CommittedA??0,CommittedB=operation?.CommittedB??0,SiegeTarget=operation?.SiegeTarget??-1,SiegeDays=operation?.SiegeDays??0};}).ToArray();
            rows.Add(new{Year=s.Years,Era=WorldEraView.Of(s),Hash=s.Hash,Population=a.TotalPopulation,Sovereigns=courts.Count(c=>c.Patron<0),Courts=courts,Campaigns=campaigns,Events=recent});
            File.WriteAllText(Path.Combine(output,"episode.json"),JsonSerializer.Serialize(new{Source=Path.GetFullPath(source),RequestedEra=era,Start=start,Interval=interval,Rows=rows},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"Episode era{era}: {s.Years}, {courts.Length} courts, {recent.Length} recent events, {s.Hash}");
            if(s.Years>=end)break;
            session.AdvanceSeconds(Math.Min(interval,end-s.Years)*SimulationTime.Year,int.MaxValue);
        }
        File.WriteAllBytes(Path.Combine(output,"final.world"),WorldFile.Write(world));
    }
}
