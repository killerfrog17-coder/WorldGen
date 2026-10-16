using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Diagnostic replay: exact same model/seed/origin, sparse era snapshots, and a
// final hash comparison. No training, geography edits or forced era unlocks.
public static class HistoryEraReview
{
    public static void Run(string[] args)
    {
        int at=Array.IndexOf(args,"--era-review");var world=WorldFile.Read(File.ReadAllBytes(args[at+1]));var saved=world.Prehistory;
        string output=Path.GetFullPath(args[at+2]);Directory.CreateDirectory(output);
        var first=WorldEraView.FirstYears(saved);var samples=new List<(int Era,int Year)>();
        for(int era=2;era<=10;era++)
        {
            if(first[era]<0||Enumerable.Range(era+1,10-era).Any(later=>first[later]>=0&&first[later]<=first[era]))continue;
            int next=Enumerable.Range(era+1,10-era).Where(later=>first[later]>first[era]).Select(later=>first[later]).DefaultIfEmpty(saved.Years).Min();
            samples.Add((era,era==10?saved.Years:first[era]+Math.Max(0,(next-first[era])/2)));
        }
        int firstState=saved.History.Polities.Select(p=>p.FoundedYear).DefaultIfEmpty(-1).Min();
        int relativeAt=Array.IndexOf(args,"--civilization-years");
        if(relativeAt>=0)
        {
            samples.Clear();foreach(int relative in args[relativeAt+1].Split(',').Select(int.Parse))
            {int year=firstState+relative+1;if(firstState>=0&&year<=saved.Years)samples.Add((0,year));}
            if(samples.All(s=>s.Year!=saved.Years))samples.Add((0,saved.Years));
        }
        world.Prehistory=new PrehistoryState{Version=saved.Version,History=new HistoryState{Version=saved.History.Version,Modern=new HistoricalModernSettings{TechnologyCeiling=saved.History.Modern.TechnologyCeiling}}};
        var engine=new Prehistory(world);var session=new SimulationSession(engine,engine.Start(engine.Prepare(saved.Origin)));var rows=new List<object>();
        foreach(var sample in samples.OrderBy(v=>v.Year).ThenBy(v=>v.Era))
        {
            long target=sample.Year*SimulationTime.Year;if(target>session.Seconds)session.AdvanceSeconds(target-session.Seconds,int.MaxValue);
            var s=session.LastSnapshot;var h=s.History;var towns=s.Society.Settlements.Where(t=>t.Population>0).ToArray();var live=h.Polities.Where(p=>p.EndedYear<0).ToArray();
            int observedEra=WorldEraView.Of(s);string caption=sample.Era==0?"Year +"+(s.Years-firstState)+" - "+WorldEraView.Name(observedEra):WorldEraView.Name(sample.Era);
            HistoryAudit.Snapshot(world,engine,s,output,new[]{HistoryOverlay.States,HistoryOverlay.Technology,HistoryOverlay.Territories},compressed:true);
            var communities=towns.Select(t=>new{t.Id,t.Name,t.Cell,t.CultureId,t.PolityId,t.FoundedYear,t.Population,t.FoodCapacity,t.FoodReserve,t.Stability,Rural=h.Aggregates.Cities[t.Id].Rural,RuralCapacity=h.Aggregates.Cities[t.Id].RuralCapacity,Productivity=h.Aggregates.Living.Cities[t.Id].Productivity,Farming=s.Society.Cultures[t.CultureId].Farming,Climate=world.Climate.Classification[t.Cell].ToString(),Latitude=Math.Asin(world.Surface.Cells[t.Cell].Position.Z)*180/Math.PI});
            File.WriteAllText(Path.Combine(output,"communities-era-"+sample.Era+".json"),JsonSerializer.Serialize(communities,new JsonSerializerOptions{WriteIndented=true}));
            var coverage=HistoricalTechnologyCoverage.Measure(s);
            var row=new{Era=observedEra,Name=caption,Began=first[observedEra],Year=s.Years,YearsSinceFirstState=s.Years-firstState,Population=h.Aggregates.TotalPopulation,Urban=h.Aggregates.TotalUrban,
                Cultures=s.Society.Cultures.Length,Towns=towns.Length,States=live.Length,LargestStateKm2=live.Select(p=>p.AreaKm2).DefaultIfEmpty().Max(),
                SovereignStates=live.Count(p=>HistoricalImperialHierarchy.Patron(s,p.Id)<0),ConstituentStates=live.Count(p=>HistoricalImperialHierarchy.Patron(s,p.Id)>=0),LargestEmpireKm2=live.Select(p=>HistoricalImperialHierarchy.AreaKm2(s,p.Id)).DefaultIfEmpty().Max(),
                Technology=Enumerable.Range(0,HistoricalTechnologies.Count(h.Version)).Select(k=>new{Name=HistoricalTechnologies.Names[k],Cultures=h.Cultures.Count(c=>c.Technology[k]>0),InstalledCultures=h.Cultures.Count(c=>HistoricalTechnologies.Has(c,(HistoricalTechnology)k)),Population=coverage[k],Urban=towns.Where(t=>h.Cultures[t.CultureId].Technology[k]>0).Sum(t=>t.Population),States=live.Count(p=>h.Cultures[p.CultureId].Technology[k]>0)})};
            rows.Add(row);Console.WriteLine(caption+" at "+sample.Year+": "+live.Length+" states, "+towns.Length+" centers");
        }
        if(session.Seconds<saved.Years*SimulationTime.Year)session.AdvanceSeconds(saved.Years*SimulationTime.Year-session.Seconds,int.MaxValue);
        // Saved reviews end on an integer-year boundary, not a guessed epoch.
        if(session.Hash!=saved.Hash)throw new InvalidOperationException("Era replay changed the final historical hash: "+session.Hash+" != "+saved.Hash);
        engine.Validate(session.LastSnapshot);
        File.WriteAllText(Path.Combine(output,"era-review.json"),JsonSerializer.Serialize(new{Rule=saved.History.Version,FinalHash=session.Hash,Verified=true,Eras=rows},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("VERIFIED ERA REPLAY "+session.Hash);
    }
}
