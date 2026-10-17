using System.Diagnostics;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

// Explicit diagnostics may census/export geometry. Ordinary simulation may not.
public static partial class HistoryWorldRealismReview
{
    public static void FinalMaps(string path,string output)
    {
        if(Directory.Exists(output))throw new IOException("Use a new diagnostic folder.");
        Directory.CreateDirectory(output);var world=WorldFile.Read(File.ReadAllBytes(path));var s=world.Prehistory;var engine=new Prehistory(world);engine.Validate(s);
        HistoryAudit.Snapshot(world,engine,s,output,new[]{HistoryOverlay.States,HistoryOverlay.Religions},compressed:true,backgroundOpacity:0,overlayOpacity:1,includeLabels:false);
        // Explicit offline inspection, never used by ordinary simulation turns.
        var towns=s.Society.Settlements.Where(t=>t.Population>0&&s.History.Aggregates.Living.Cities[t.Id].AbandonedYear<0).OrderBy(t=>t.Id).ToArray();
        File.WriteAllText(Path.Combine(output,"religions.json"),JsonSerializer.Serialize(new{Year=s.Years,Religions=s.History.Religions.Select(r=>new{r.Id,r.Name,r.FoundedYear,r.ParentId,r.CultureId,Reach=r.Reach.ToString(),r.Ethos,r.Ritual}),Towns=towns.Select(t=>new{t.Id,t.Name,t.Cell,t.Population,Public=HistoricalCells.Religion(s,t.Cell),Community=Prehistory.ReligiousCommunityAt(s,t.Cell)})},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        FinalPoliticalChronology(world,output);
        Console.WriteLine("Exported opaque state/religious maps and actual town observations at "+s.Years);
    }
    public static void Inspect(string path)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var s=world.Prehistory;var engine=new Prehistory(world);engine.Validate(s);
        object Call(string method,params object[] values)=>typeof(Prehistory).GetMethod(method,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(engine,values)!;
        foreach(var t in s.Society.Settlements.Where(t=>t.Population>0).OrderByDescending(t=>t.Population).Take(20))
            Console.WriteLine(JsonSerializer.Serialize(new{t.Name,t.Id,t.Population,t.FoodCapacity,Age=s.Years-t.FoundedYear,Farming=s.Society.Cultures[t.CultureId].Farming,Institution=s.History.Cultures[t.CultureId].Institution,Climate=world.Climate.Classification[t.Cell].ToString(),Environment=Call("EnvironmentalEstablishment",s,t.Cell,t.CultureId),Storage=Call("LivingCropLandscape",s,t.CultureId,t.Cell,true),Links=s.History.Aggregates.Cities[t.Id].Neighbors.Length,Rural=s.History.Aggregates.Cities[t.Id].Rural,RuralCapacity=s.History.Aggregates.Cities[t.Id].RuralCapacity,Uncapped=Call("CommunityUrbanCapacity",s,t.Id),Staples=Call("EnvironmentalStaples",s,t.CultureId,t.Cell),Homeland=Call("HomelandOriginFactor",s,t.Cell),Civic=s.History.Towns[t.Id].CivicPreparationYears,Reserve=t.FoodReserve}));
        Console.WriteLine("Active:"+s.Society.Settlements.Count(t=>t.Population>0)+" abandoned:"+s.History.Aggregates.Living.Cities.Count(t=>t.AbandonedYear>=0));
    }
    public static void Run(string preset,int seed,int detail,string output,int safetyYear,string profile="earth-native-3")
    {
        if(preset=="ContinentsPlus")preset="Continents plus";
        if(!WorldPresets.Names.Contains(preset))throw new ArgumentException("Use an existing preset.");
        var setup=new WorldSetup();var recipe=setup.Resolve(setup.Preset(preset,new Recipe{Seed=seed,Detail=detail,EcologyProfile=profile}));recipe.EcologyProfile=profile;
        RunWorld(new WorldPipeline().Generate(recipe),preset,seed,output,safetyYear);
    }
    public static void Replay(string source,string output,int safetyYear)
    {
        if(Path.GetFullPath(source).Equals(Path.GetFullPath(Path.Combine(output,"final.world")),StringComparison.OrdinalIgnoreCase))throw new ArgumentException("A replay must not overwrite its source world.");
        // A controlled history experiment, not another generation route. Keep
        // every accepted terrain/climate/ecology value and reset only the loaded
        // copy's history through the ordinary public preparation/start API.
        var world=WorldFile.Read(File.ReadAllBytes(source));world.Prehistory=null;
        RunWorld(world,"Saved-world history replay",world.Recipe.Seed,output,safetyYear,Path.GetFullPath(source));
    }
    static void RunWorld(World world,string preset,int seed,string output,int safetyYear,string source=null)
    {
        if(File.Exists(Path.Combine(output,"final.world")))throw new IOException("Refusing to overwrite a completed review world.");
        Directory.CreateDirectory(output);var recipe=world.Recipe;var engine=new Prehistory(world);var session=new SimulationSession(engine,engine.Start(engine.Prepare()));
        var watch=Stopwatch.StartNew();var rows=new List<object>();var seen=new HashSet<int>();int first=-1,tail=-1,lastMap=-1,workYear=-1,civilianTail=-1;double worst=0,yearWork=0;
        engine.YearWorkObserver=(year,ms)=>{if(year!=workYear){workYear=year;yearWork=0;}yearWork+=ms;worst=Math.Max(worst,yearWork);};
        while(session.LastSnapshot.Years<safetyYear)
        {
            int step=first<0?1000:100;
            session.AdvanceSeconds(Math.Min(step,safetyYear-session.LastSnapshot.Years)*SimulationTime.Year,int.MaxValue);
            var s=session.LastSnapshot;first=s.History.Polities.Select(p=>p.FoundedYear).DefaultIfEmpty(-1).Min();
            int era=WorldEraView.Of(s);bool milestone=seen.Add(era);
            if(era>=10&&civilianTail<0)civilianTail=s.Years;
            int maximumTechnology=s.History.Aggregates?.FirstMaximumTechnologyYear??-1;
            if(maximumTechnology>=0&&tail<0)tail=maximumTechnology;
            bool observe=milestone||first>=0&&(lastMap<0||s.Years-lastMap>=(era>=7?300:1000))||first<0&&(lastMap<0||s.Years-lastMap>=20000)||s.Years==100000;
            if(observe)
            {
                string directory=Path.Combine(output,"year-"+s.Years);Directory.CreateDirectory(directory);
                // Keep all four views for era comparisons; intermediate political
                // frames need only States. Metadata remains complete at every
                // observation, without accumulating four near-identical geometry
                // exports throughout a long late-world tail.
                var views=milestone?new[]{HistoryOverlay.States,HistoryOverlay.Technology,HistoryOverlay.Cultures,HistoryOverlay.Religions}:new[]{HistoryOverlay.States};
                HistoryAudit.Snapshot(world,engine,s,directory,views,compressed:true,backgroundOpacity:1,overlayOpacity:.58,includeLabels:false);
                var towns=s.Society.Settlements.Where(t=>t.Population>0).ToArray();
                var row=new{Year=s.Years,Era=era,EraName=WorldEraView.Name(era),FirstState=first,Population=s.History.Aggregates.TotalPopulation,Cultures=s.Society.Cultures.Length,
                    ActiveTowns=towns.Length,AbandonedTowns=s.History.Aggregates.Living.Cities.Count(c=>c.AbandonedYear>=0),
                    VillagePopulation=towns.Where(t=>s.History.Aggregates.Resolve(t.PolityId,s.History)<0).Sum(t=>t.Population),
                    States=s.History.Polities.Count(p=>p.EndedYear<0),IndependentOrigins=s.History.Archive.Count(e=>e.Kind=="independent-institutions"),
                    OldestStateAge=s.History.Polities.Where(p=>p.EndedYear<0).Select(p=>s.Years-p.FoundedYear).DefaultIfEmpty(0).Max(),LargestCity=towns.Select(t=>t.Population).DefaultIfEmpty(0).Max(),
                    ColonialCouncils=s.History.Archive.Count(e=>e.Kind=="colonial-council"),ColonialProtectorates=s.History.Archive.Count(e=>e.Kind=="colonial-protectorate"),SponsoredPorts=s.History.Archive.Count(e=>e.Kind=="sponsored-port"),Charters=s.History.Aggregates.Empires.Territories.Posts.Count(p=>p.ClosedYear<0),
                    ActiveOrganizations=s.History.International?.Organizations.Count(o=>o.EndedYear<0)??0,CoalitionEntries=s.History.International?.CoalitionEntries.Length??0,
                    NuclearArsenals=s.History.Nuclear?.States.Count(n=>n.Stockpile>0)??0,NuclearUses=s.History.Nuclear?.States.Sum(n=>n.Used)??0,
                    FirstMaximumTechnologyYear=maximumTechnology,WorstYearMs=worst,WallSeconds=watch.Elapsed.TotalSeconds};
                rows.Add(row);File.WriteAllText(Path.Combine(output,"review.json"),JsonSerializer.Serialize(new{Preset=preset,Seed=seed,Recipe=recipe,SourceWorld=source,Rules=s.History.Version,Rows=rows},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
                File.WriteAllText(Path.Combine(directory,"towns.json"),JsonSerializer.Serialize(towns.Select(t=>new{t.Id,t.Name,t.Cell,t.CultureId,t.PolityId,t.Population,Age=s.Years-t.FoundedYear,Latitude=Math.Asin(world.Surface.Cells[t.Cell].Position.Z)*180/Math.PI,Climate=world.Climate.Classification[t.Cell].ToString(),world.Ecology.Habitat[t.Cell].Biome}),new JsonSerializerOptions{WriteIndented=true}));
                OpportunityReview(world,engine,s,directory);
                lastMap=s.Years;Console.WriteLine(JsonSerializer.Serialize(row));
            }
            if(s.Years%10000==0)Console.WriteLine($"Progress {preset}/{seed}: {s.Years}, cultures {s.Society.Cultures.Length}, towns {s.Society.Settlements.Length}, {watch.Elapsed.TotalSeconds:F1}s");
            if(tail>=0&&s.Years>=tail+1000)break;
            // Optional weapons can remain deliberately uninvented. Do not
            // accumulate150,000 postmodern years and gigabytes of diagnostic
            // exports waiting for a universal nuclear-weapons milestone.
            if(tail<0&&civilianTail>=0&&s.Years>=civilianTail+3000)
            {Console.WriteLine("BOUNDED:3,000-year civilian postmodern tail; not all optional technologies discovered. This is NOT the all-technology performance acceptance.");break;}
            if(File.Exists(Path.Combine(output,"stop-after-checkpoint")))
            {Console.WriteLine("Requested diagnostic stop at an intact checkpoint.");break;}
            if(first>=0&&s.Years>first+30000&&era<8){Console.WriteLine("STALLED:30,000 years after first state without industry; preserve evidence for correction.");break;}
        }
        var final=session.Snapshot();engine.Validate(final);world.Prehistory=final;
        File.WriteAllBytes(Path.Combine(output,"final.world"),WorldFile.Write(world));
        FinalPoliticalChronology(world,output);
        Console.WriteLine("FINAL "+final.Hash+" at "+final.Years+" after "+watch.Elapsed.TotalSeconds.ToString("F1")+"s");
    }
}
