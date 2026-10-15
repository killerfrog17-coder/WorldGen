using System.Diagnostics;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Longitudinal observations, separate from mechanical invariant tests.
public static class HistoryLivingReview
{
    public static void Run(string[] args)
    {
        int at=Array.IndexOf(args,"--living-review");string input=args[at+1],output=Path.GetFullPath(args[at+2]);int years=int.Parse(args[at+3]);
        Directory.CreateDirectory(output);var world=WorldFile.Read(File.ReadAllBytes(input));
        if(args.Contains("--fresh"))world.Prehistory=null;
        var engine=new Prehistory(world);var state=world.Prehistory??engine.Start(engine.Prepare());
        if(args.Contains("--upgrade"))state=engine.UpgradeToCurrentHistory(state);
        engine.Validate(state);var session=new SimulationSession(engine,state);var watch=Stopwatch.StartNew();
        int target=state.Years+years,lastMap=-10000,lastStateCount=0,lastEra=0;var rows=new List<object>();int afterAt=Array.IndexOf(args,"--after-first-state"),after=afterAt>=0?int.Parse(args[afterAt+1]):-1;
        while(session.LastSnapshot.Years<target)
        {
            long chunk=args.Contains("--era-maps")&&session.LastSnapshot.History.Polities.Length>0?250:1000;
            session.AdvanceSeconds(Math.Min(chunk*SimulationTime.Year,target*SimulationTime.Year-session.Seconds),int.MaxValue);
            state=session.LastSnapshot;var h=state.History;int first=h.Polities.Select(p=>p.FoundedYear).DefaultIfEmpty(-1).Min();
            var towns=state.Society.Settlements.Where(t=>t.Population>0).ToArray();var live=h.Polities.Where(p=>p.EndedYear<0).ToArray();
            var row=new {Year=state.Years,Population=SocietyView.Population(state),Occupied=state.Population.Count(p=>p>0),Cultures=state.Society.Cultures.Length,
                Towns=towns.Length,RecordedTowns=state.Society.Settlements.Length,Urban=towns.Sum(t=>t.Population),States=live.Length,RecordedStates=h.Polities.Length,
                FirstState=first,MaxTech=h.Cultures.Select(c=>c.Technology.Count(y=>y>0)).DefaultIfEmpty().Max(),MaxTechYear=h.Aggregates.FirstMaximumTechnologyYear,
                MeanActiveStateAge=live.Select(p=>state.Years-p.FoundedYear).DefaultIfEmpty().Average(),Seconds=watch.Elapsed.TotalSeconds};
            rows.Add(row);Console.WriteLine(JsonSerializer.Serialize(row));File.WriteAllText(Path.Combine(output,"timeline.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
            bool map=first>=0&&(lastStateCount==0||state.Years-lastMap>=2000)&&args.Contains("--maps");
            if(map){HistoryAudit.Snapshot(world,engine,state,output);lastMap=state.Years;}
            int era=WorldEraView.Of(state);if(args.Contains("--era-maps")&&era!=lastEra&&era>=4){HistoryAudit.Snapshot(world,engine,state,output,new[]{HistoryOverlay.States});lastEra=era;}
            lastStateCount=live.Length;
            if(h.Aggregates.FirstMaximumTechnologyYear>=0&&state.Years>=h.Aggregates.FirstMaximumTechnologyYear+1000)break;
            if(after>=0&&first>=0&&state.Years>=first+after)break;
        }
        state=session.Snapshot();world.Prehistory=state;
        File.WriteAllText(Path.Combine(output,"events.json"),JsonSerializer.Serialize(state.History.Archive,new JsonSerializerOptions{IncludeFields=true}));
        File.WriteAllText(Path.Combine(output,"towns.json"),JsonSerializer.Serialize(state.Society.Settlements,new JsonSerializerOptions{IncludeFields=true}));
        File.WriteAllText(Path.Combine(output,"cultures.json"),JsonSerializer.Serialize(state.Society.Cultures.Select(c=>new{c.Id,c.Name,c.FoundedYear,c.OriginCell,c.Farming,c.Population,Habitat=world.Ecology.Habitat[c.OriginCell],History=state.History.Cultures[c.Id]}),new JsonSerializerOptions{IncludeFields=true}));
        File.WriteAllText(Path.Combine(output,"accounts.json"),JsonSerializer.Serialize(new{States=state.History.Polities,StateAccounts=state.History.Aggregates.Polities,Cultures=state.History.Aggregates.Cultures.Select(c=>new{c.Id,c.CityCount,c.SupportedCities,c.Urban,c.SupportedUrban,c.CivilizationSince}),Cities=state.History.Aggregates.Cities.Select(c=>new{c.Id,c.AreaKm2,c.Rural,c.RuralCapacity,c.Supported}),LivingCities=state.History.Aggregates.Living?.Cities},new JsonSerializerOptions{IncludeFields=true}));
        if(state.History.Aggregates.Living!=null)
        {
            var fiscal=typeof(Prehistory).GetMethod("LivingCropLandscape",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
            File.WriteAllText(Path.Combine(output,"site-opportunities.json"),JsonSerializer.Serialize(state.Society.Settlements.Where(t=>t.Population>0).Select(t=>new{t.Id,t.Cell,t.CultureId,t.PolityId,t.Population,t.FoodCapacity,Fiscal=(double)fiscal.Invoke(engine,new object[]{state,t.CultureId,t.Cell,true})!,LocalExample=state.History.Towns[t.Id].CivicExampleExposure,Region=state.History.Aggregates.Living.Regions[engine.CellPatch[t.Cell]],Latitude=Math.Asin(world.Surface.Cells[t.Cell].Position.Z)*180/Math.PI,Longitude=Math.Atan2(world.Surface.Cells[t.Cell].Position.Y,world.Surface.Cells[t.Cell].Position.X)*180/Math.PI}),new JsonSerializerOptions{IncludeFields=true}));
        }
        engine.Validate(state);
        var active=state.History.Polities.Where(p=>p.EndedYear<0).ToArray();
        var habitatGroups=state.History.Archive.Where(e=>e.Kind=="state"||e.Kind=="independent-institutions").GroupBy(e=>world.Climate.Classification[e.Cell]).OrderByDescending(g=>g.Count()).Select(g=>new{Climate=g.Key.ToString(),Foundations=g.Count()}).ToArray();
        var land=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>!world.Hydrology.OceanConnected[i]).GroupBy(i=>world.Landmass[i]).Select(group=>new{
            Landmass=group.Key,AreaKm2=group.Sum(i=>world.Surface.Cells[i].AreaM2/1e6),
            InhabitedKm2=group.Where(i=>engine.CellPatch[i]>=0&&(state.Population[engine.CellPatch[i]]+state.History.Aggregates.Living.Regions[engine.CellPatch[i]].SettledPopulation>=25||HistoricalCells.Polity(state,i)>=0)).Sum(i=>world.Surface.Cells[i].AreaM2/1e6),
            StateKm2=group.Where(i=>HistoricalCells.Polity(state,i)>=0).Sum(i=>world.Surface.Cells[i].AreaM2/1e6),
            States=active.Count(p=>world.Landmass[state.Society.Settlements[p.CapitalId].Cell]==group.Key)
        }).OrderByDescending(g=>g.AreaKm2).Take(12).ToArray();
        var summary=new{Year=state.Years,Rule=state.History.Version,Landmasses=land,FoundationClimates=habitatGroups,
            Origins=state.History.Archive.Where(e=>e.Kind=="independent-institutions").Select(e=>new{e.Year,e.Cell,Climate=world.Climate.Classification[e.Cell].ToString(),Latitude=Math.Asin(world.Surface.Cells[e.Cell].Position.Z)*180/Math.PI}),
            Technology=Enumerable.Range(0,HistoricalTechnologies.Names.Length).Select(k=>new{Name=HistoricalTechnologies.Names[k],Cultures=state.History.Cultures.Count(c=>c.Technology[k]>0),First=state.History.Cultures.Where(c=>c.Technology[k]>0).Select(c=>c.Technology[k]-1).DefaultIfEmpty(-1).Min()}),
            LargestStates=active.OrderByDescending(p=>p.AreaKm2).Take(8).Select(p=>new{p.Name,p.AreaKm2,p.Population,Age=state.Years-p.FoundedYear}),
            SeaCommunities=state.History.Archive.Count(e=>e.Kind=="overseas-community"||e.Kind=="ocean-settlement"),Contacts=state.Society.Cultures.Sum(c=>c.Contacts.Length)/2};
        File.WriteAllText(Path.Combine(output,"development-summary.json"),JsonSerializer.Serialize(summary,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        if(args.Contains("--save"))File.WriteAllBytes(Path.Combine(output,"final.world"),WorldFile.Write(world));
        if(args.Contains("--maps")||args.Contains("--final-maps"))HistoryAudit.Snapshot(world,engine,state,output);
        File.WriteAllText(Path.Combine(output,"final.hash"),state.Hash);Console.WriteLine("VALIDATED "+state.Hash);
    }
}
