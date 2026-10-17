using System.Diagnostics;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Explicit review command, not a renderer, generation path or simulation turn.
// Reuse one immutable world load instead of several simultaneous large readers.
public static class HistoryOvernightEvidenceBundle
{
    public static void Run(string source,string output,int profileYears=0,int colonialReplayStart=-1)
    {
        if(Directory.Exists(output))throw new IOException("Use a new evidence directory.");
        if(profileYears<0||profileYears>100)throw new ArgumentOutOfRangeException(nameof(profileYears));
        string input=Path.GetFullPath(source),folder=Path.GetFullPath(output);
        var watch=Stopwatch.StartNew();
        var world=WorldFile.Read(File.ReadAllBytes(input));
        var state=world.Prehistory??throw new ArgumentException("A historical saved world is required.");
        new Prehistory(world).Validate(state);
        string before=state.ComputeHash();
        Directory.CreateDirectory(folder);

        HistoryReligionEvidenceAudit.RunLoaded(world,input,Path.Combine(folder,"religion"));
        HistoryPoliticalIdentityAudit.RunLoaded(world,input,Path.Combine(folder,"political-identities"));
        HistoryColonialThroughputAudit.RunLoaded(world,input,Path.Combine(folder,"colonial-throughput.json"));
        bool earth=world.Recipe.TerrainSource=="Earth";
        if(earth)
        {
            string first=Path.Combine(folder,"first-town-opportunities.json");
            HistoryFirstTownOpportunityAudit.RunLoaded(world,input,first);
            HistoryFarmerFrontierOpportunityAudit.RunLoaded(world,input,first,Path.Combine(folder,"farmer-frontier-opportunities.json"));
        }

        string review=Path.Combine(Path.GetDirectoryName(input)!,"review.json");
        bool continuity=File.Exists(review)&&state.History.Polities.Any(p=>p.EndedYear<0);
        if(continuity)
        {
            int oldest=state.History.Polities.Where(p=>p.EndedYear<0).OrderBy(p=>p.FoundedYear).ThenBy(p=>p.Id).First().Id;
            HistoryStateContinuityAudit.RunLoaded(world,input,Path.Combine(folder,"state-continuity-oldest.json"),review,oldest);
        }
        if(profileYears>0)
            HistoryColonialThroughputAudit.ProfileLoaded(world,input,Path.Combine(folder,"private-profile.json"),profileYears);
        if(colonialReplayStart>=0)
            HistoryColonialThroughputAudit.RunLoaded(world,input,Path.Combine(folder,"colonial-earlier.json"),colonialReplayStart,0,
                state.History.Aggregates.Empires.Connections.Realms.Version,Path.Combine(folder,"earlier-replay.world"));

        if(!ReferenceEquals(world.Prehistory,state)||state.ComputeHash()!=before)
            throw new Exception("An offline evidence bundle changed the loaded historical checkpoint.");
        File.WriteAllText(Path.Combine(folder,"bundle.json"),JsonSerializer.Serialize(new
        {
            Source=input,Year=state.Years,Hash=before,Realm=state.History.Aggregates.Empires.Connections.Realms.Version,
            EarthFieldAnchors=earth,HistoricalContinuity=continuity,PrivateProfileYears=profileYears,EarlierColonialReplayYear=colonialReplayStart,UnchangedCheckpoint=true,
            WallSeconds=watch.Elapsed.TotalSeconds,
            Notes=new[]{"One saved-world load; no change or rules upgrade to the loaded historical checkpoint.",
                "Optional profiling advances a detached private branch only; its future results are labeled separately.",
                "An optional earlier colonial query is a fresh ordinary-calendar replay, not present stocks backdated into the past.",
                "Current stocks, routes and readiness are not backdated to an earlier era.",
                "SVG exports are not native Unity screenshots. Cell counts are not physical areas or adherent counts.",
                "Opaque court/sovereign control is distinct from public culture colors and recognized claims."}
        },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("PASS unchanged single-load evidence bundle "+before);
    }
}
