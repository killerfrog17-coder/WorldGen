using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Enhanced offline observations of ordinary frozen-core SimulationSessions.
// Never installed in the simulation loop; no policy, RNG or physics override.
public static class HistoryPoliticalEpisodeEvidence
{
    static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    public static void Run(string source,string output,int era,int years,int interval)
    {
        if(era<5||era>10||years<1||years>1000||interval<1||interval>years)throw new ArgumentOutOfRangeException();
        if(Directory.Exists(output))throw new IOException("Use a new episode folder.");
        var world=WorldFile.Read(File.ReadAllBytes(source));world.Prehistory=null;
        var engine=new Prehistory(world);var session=new SimulationSession(engine,engine.Start(engine.Prepare()));
        Directory.CreateDirectory(output);PrehistoryState lastPrestate=null;object foundation=null;
        while(WorldEraView.Of(session.LastSnapshot)<era&&session.LastSnapshot.Years<350000)
        {
            bool prestate=session.LastSnapshot.History.Polities.Length==0;
            if(prestate)lastPrestate=session.LastSnapshot; // detached publication, never the private current state
            session.AdvanceSeconds((prestate?1000:25)*SimulationTime.Year,int.MaxValue);
            if(foundation==null&&session.LastSnapshot.History.Polities.Length>0)
                foundation=FoundationBranch(world,session,lastPrestate,source,output);
        }
        if(WorldEraView.Of(session.LastSnapshot)<era)throw new InvalidOperationException("Requested era not reached.");
        int start=session.LastSnapshot.Years,end=start+years;var rows=new List<object>();int[] previous=null;
        while(true)
        {
            var s=session.Snapshot();world.Prehistory=s;
            rows.Add(Frame(world,engine,s,Path.Combine(output,"year-"+s.Years),s.Years-interval,ref previous));
            File.WriteAllText(Path.Combine(output,"episode.json"),JsonSerializer.Serialize(new
            {
                Source=Path.GetFullPath(source),RequestedEra=era,Start=start,Interval=interval,
                RealmRules=s.History.Aggregates.Empires.Connections.Realms.Version,
                CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Prehistory).Assembly.Location))),
                FoundationObservations=foundation,Rows=rows,
                Notes=new[]{"Ordinary SimulationSession and frozen authoritative physics; no rules upgrade or handler intervention.",
                    "Prehistory advances1000yr until first state, then25yr until the requested leading era, matching the preceding episode harness. The subsequent observation cadence is explicit; no hash identity with differently published full-era replays is claimed.",
                    "Events at interval boundaries can appear twice; deduplicate immutable IDs. Archive field-battle headlines are not the complete battle census. Campaign victory/casualty/capture counters and interval-intersecting finished campaigns are exported separately.",
                    "Controller transition counts are offline fine-cell observations, not physical area, population loss, or proof that every change was a conquest. A declared war is not automatically a battle.",
                    "States includes sovereign-level recognized-title fallback and ruling-culture tint. Territories uses actual resolved court controllers. Controller transition counts exclude recognized claims.",
                    "SVG States/Territories exports are diagnostic images, not native Unity captures. Country/culture colors alone do not identify sovereigns; actual court and bounded patron IDs are exported per frame."
                }
            },Json));
            Console.WriteLine($"Enhanced episode era{era}: {s.Years}; {s.History.Polities.Count(p=>p.EndedYear<0)} living courts; {s.Hash}");
            if(s.Years>=end)break;
            session.AdvanceSeconds(Math.Min(interval,end-s.Years)*SimulationTime.Year,int.MaxValue);
        }
        File.WriteAllBytes(Path.Combine(output,"final.world"),WorldFile.Write(world));
    }

    static object FoundationBranch(World world,SimulationSession main,PrehistoryState previous,string source,string output)
    {
        var principal=main.LastSnapshot;string principalHash=principal.ComputeHash(),mainContinuationHash=main.Snapshot().ComputeHash(),checkpointHash=previous.ComputeHash();
        var recordedFirst=principal.History.Polities.OrderBy(p=>p.FoundedYear).ThenBy(p=>p.Id).First();int first=recordedFirst.FoundedYear;
        string Fingerprint(PrehistoryState state)=>state.History.Archive.Where(e=>e.PolityId==recordedFirst.Id&&e.Year==first&&e.SettlementId>=0)
            .OrderBy(e=>e.Id).Select(e=>$"{e.Kind}|{e.Cell}|{e.SettlementId}|{e.CultureId}").FirstOrDefault();
        string recordedFingerprint=Fingerprint(principal);
        var copy=previous.Copy();if(copy.ComputeHash()!=checkpointHash)throw new Exception("Foundation checkpoint copy changed.");
        object result;
        try
        {
            world.Prehistory=copy;var branchEngine=new Prehistory(world);var branch=new SimulationSession(branchEngine,copy);
            branch.AdvanceSeconds(((long)first+1-previous.Years)*SimulationTime.Year,int.MaxValue);
            var at=branch.Snapshot();bool same=at.History.Polities.Length>0&&at.History.Polities.Min(p=>p.FoundedYear)==first
                &&at.History.Polities.Any(p=>p.Id==recordedFirst.Id&&p.CultureId==recordedFirst.CultureId&&p.FoundedYear==first)
                &&recordedFingerprint!=null&&Fingerprint(at)==recordedFingerprint;
            var observations=new List<object>();int[] branchPrevious=null;
            if(same)
            {
                string folder=Path.Combine(output,"foundation-observations");Directory.CreateDirectory(folder);
                world.Prehistory=at;
                observations.Add(Frame(world,branchEngine,at,Path.Combine(folder,"year-"+at.Years),previous.Years,ref branchPrevious));
                branch.AdvanceSeconds(500*SimulationTime.Year,int.MaxValue);at=branch.Snapshot();world.Prehistory=at;
                observations.Add(Frame(world,branchEngine,at,Path.Combine(folder,"year-"+at.Years),first+1,ref branchPrevious));
                File.WriteAllText(Path.Combine(folder,"episode.json"),JsonSerializer.Serialize(new
                {
                    Source=Path.GetFullPath(source),ParentEpisode=Path.GetFullPath(output),RequestedEra=4,Start=first+1,Interval=500,Rows=observations,
                    Notes=new[]{"Private bounded continuation from the last detached pre-state checkpoint, to the end of the recorded first-state year and500yr later. Main episode continuation is untouched.",
                        "The private branch reproduces the recorded first foundation YEAR, not an invented subannual event time. Its later history need not equal a differently published main replay."
                    }
                },Json));
            }
            result=new{RecordedFoundationYear=first,MainFirstObservedYear=principal.Years,LastPrestateCheckpointYear=previous.Years,
                CheckpointHash=checkpointHash,DetachedCopyHash=copy.ComputeHash(),MainContinuationHash=mainContinuationHash,RecordedFoundationFingerprint=recordedFingerprint,
                PrivateBranchFoundationMatched=same,
                PrivateFirstObservationYear=same?first+1:-1,PrivateSecondObservationYear=same?first+501:-1,
                Note=same?"Private ordinary-session branch observes within1yr of foundation, then500yr later; main continuation preserved.":"Private publication cadence did not reproduce the recorded foundation year. No exact-foundation map is asserted; main first-observed date remains authoritative."};
        }
        finally{world.Prehistory=principal;}
        if(principal.ComputeHash()!=principalHash||main.LastSnapshot.ComputeHash()!=principalHash||main.Snapshot().ComputeHash()!=mainContinuationHash||previous.ComputeHash()!=checkpointHash)
            throw new Exception("Private foundation branch changed main/checkpoint state.");
        return result;
    }

    static object Frame(World world,Prehistory engine,PrehistoryState s,string folder,int since,ref int[] previous)
    {
        engine.Validate(s);string hash=s.ComputeHash();var h=s.History;var a=h.Aggregates;
        Directory.CreateDirectory(folder);
        HistoryAudit.Snapshot(world,engine,s,folder,new[]{HistoryOverlay.States,HistoryOverlay.Territories},compressed:true,backgroundOpacity:0,overlayOpacity:1,includeLabels:false);
        object Place(int cell)=>cell<0?null:new{Cell=cell,Latitude=Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI,Longitude=Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI};
        var owners=new int[world.Surface.Cells.Length];var counts=new Dictionary<int,int>();var transitions=new Dictionary<(int From,int To),List<int>>();int changed=0;
        for(int cell=0;cell<owners.Length;cell++)
        {
            owners[cell]=world.Hydrology.OceanConnected[cell]?-1:HistoricalCells.Polity(s,cell);
            if(owners[cell]>=0){counts.TryGetValue(owners[cell],out int count);counts[owners[cell]]=count+1;}
            if(previous==null||owners[cell]==previous[cell])continue;changed++;
            var key=(previous[cell],owners[cell]);if(!transitions.TryGetValue(key,out var samples))transitions[key]=samples=new List<int>();samples.Add(cell);
        }
        previous=owners;
        var courts=h.Polities.Where(p=>p.EndedYear<0).Select(p=>new
        {
            p.Id,p.Name,p.FoundedYear,p.ParentId,p.CapitalId,p.CultureId,p.Population,p.Cohesion,p.Legitimacy,p.Exhaustion,p.AreaKm2,
            Cities=a.Polities[p.Id].CityCount,Capital=Place(s.Society.Settlements[p.CapitalId].Cell),
            Patron=HistoricalImperialHierarchy.Patron(s,p.Id),Sovereign=HistoricalImperialHierarchy.Sovereign(s,p.Id),
            CountryFounded=HistoricalRealmLifecycles.Founded(s,p.Id),ActualControlledFineCells=counts.TryGetValue(p.Id,out int count)?count:0,
            a.Empires.Connections.Realms.Governments[p.Id].RegimeSince,a.Empires.Connections.Realms.Governments[p.Id].RegimeChanges,
            a.Empires.Connections.Realms.Governments[p.Id].RegionalSeparations,h.Dynamics.States[p.Id].LastSuccession
        }).ToArray();
        var kinds=new HashSet<string>{"war","peace","field-battle","realm-succession-crisis","secession","conquest","independence","regional-realignment","regime-change","succession","election"};
        var events=h.Archive.Where(e=>e.Year>=since&&e.Year<=s.Years&&kinds.Contains(e.Kind)).Select(e=>new{e.Id,e.Year,e.Kind,e.PolityId,e.OtherPolityId,e.SettlementId,e.Cell,e.Text}).ToArray();
        var campaigns=h.Dynamics.Campaigns.Where(c=>c.StartedYear<=s.Years&&(c.EndedYear<0||c.EndedYear>=since)).Select(c=>
        {
            var op=a.Empires.Connections.Military.Campaigns[c.Id];
            return new{c.Id,c.Name,c.A,c.B,c.AttackerId,c.StartedYear,c.EndedYear,Aim=c.Aim.ToString(),c.CasualtiesA,c.CasualtiesB,c.Conquests,c.Repulses,c.CapturedSettlements,
                Theater=a.Provinces.Theaters[c.Id].Cell,op.CommittedA,op.CommittedB,op.SiegeTarget,op.SiegeDays,op.VictoriesA,op.VictoriesB,op.CorePressureA,op.CorePressureB,op.OriginalCapitalA,op.OriginalCapitalB,op.Closed};
        }).ToArray();
        var leaders=h.Dynamics.Leaders.Where(l=>l.StartedYear<=s.Years&&(l.EndedYear<0||l.EndedYear>=since)).Select(l=>new{l.Id,l.Name,l.PolityId,l.StartedYear,l.EndedYear,l.Victories,l.Defeats,l.Legacy}).ToArray();
        if(s.ComputeHash()!=hash)throw new Exception("Episode observations changed published checkpoint.");
        return new{Year=s.Years,Era=WorldEraView.Of(s),Hash=hash,Population=a.TotalPopulation,
            Sovereigns=courts.Count(c=>c.Patron<0),Courts=courts,Campaigns=campaigns,Events=events,Leaders=leaders,
            IntervalSince=since,ChangedControllerFineCells=changed,
            ControllerTransitions=transitions.OrderBy(pair=>pair.Key.From).ThenBy(pair=>pair.Key.To)
                .Select(pair=>new{From=pair.Key.From,To=pair.Key.To,FineCells=pair.Value.Count,SampleCells=pair.Value.Take(8).ToArray()}).ToArray()};
    }
}
