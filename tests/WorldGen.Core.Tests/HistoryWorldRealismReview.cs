using System.Diagnostics;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

// Explicit diagnostics may census/export geometry. Ordinary simulation may not.
public static class HistoryWorldRealismReview
{
    public static void Inspect(string path)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var s=world.Prehistory;var engine=new Prehistory(world);engine.Validate(s);
        object Call(string method,params object[] values)=>typeof(Prehistory).GetMethod(method,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(engine,values)!;
        foreach(var t in s.Society.Settlements.Where(t=>t.Population>0).OrderByDescending(t=>t.Population).Take(20))
            Console.WriteLine(JsonSerializer.Serialize(new{t.Name,t.Id,t.Population,t.FoodCapacity,Age=s.Years-t.FoundedYear,Farming=s.Society.Cultures[t.CultureId].Farming,Institution=s.History.Cultures[t.CultureId].Institution,Climate=world.Climate.Classification[t.Cell].ToString(),Environment=Call("EnvironmentalEstablishment",s,t.Cell,t.CultureId),Storage=Call("LivingCropLandscape",s,t.CultureId,t.Cell,true),Links=s.History.Aggregates.Cities[t.Id].Neighbors.Length,Rural=s.History.Aggregates.Cities[t.Id].Rural,RuralCapacity=s.History.Aggregates.Cities[t.Id].RuralCapacity,Uncapped=Call("CommunityUrbanCapacity",s,t.Id),Staples=Call("EnvironmentalStaples",s,t.CultureId,t.Cell),Homeland=Call("HomelandOriginFactor",s,t.Cell),Civic=s.History.Towns[t.Id].CivicPreparationYears,Reserve=t.FoodReserve}));
        Console.WriteLine("Active:"+s.Society.Settlements.Count(t=>t.Population>0)+" abandoned:"+s.History.Aggregates.Living.Cities.Count(t=>t.AbandonedYear>=0));
    }
    public static void Run(string preset,int seed,int detail,string output,int safetyYear,string profile="earth-native-2")
    {
        if(preset=="ContinentsPlus")preset="Continents plus";
        if(!WorldPresets.Names.Contains(preset))throw new ArgumentException("Use an existing preset.");
        Directory.CreateDirectory(output);var setup=new WorldSetup();var recipe=setup.Resolve(setup.Preset(preset,new Recipe{Seed=seed,Detail=detail,EcologyProfile=profile}));recipe.EcologyProfile=profile;
        var world=new WorldPipeline().Generate(recipe);var engine=new Prehistory(world);var session=new SimulationSession(engine,engine.Start(engine.Prepare()));
        var watch=Stopwatch.StartNew();var rows=new List<object>();var seen=new HashSet<int>();int first=-1,tail=-1,lastMap=-1;double worst=0;
        engine.YearWorkObserver=(_,ms)=>worst=Math.Max(worst,ms);
        while(session.LastSnapshot.Years<safetyYear)
        {
            int step=first<0?1000:100;
            session.AdvanceSeconds(Math.Min(step,safetyYear-session.LastSnapshot.Years)*SimulationTime.Year,int.MaxValue);
            var s=session.LastSnapshot;first=s.History.Polities.Select(p=>p.FoundedYear).DefaultIfEmpty(-1).Min();
            int era=WorldEraView.Of(s);bool milestone=seen.Add(era);
            if(era>=10&&tail<0)tail=s.Years;
            bool observe=milestone||first>=0&&(lastMap<0||s.Years-lastMap>=(era>=7?300:1000))||first<0&&(lastMap<0||s.Years-lastMap>=20000)||s.Years==100000;
            if(observe)
            {
                string directory=Path.Combine(output,"year-"+s.Years);Directory.CreateDirectory(directory);
                HistoryAudit.Snapshot(world,engine,s,directory,new[]{HistoryOverlay.States,HistoryOverlay.Technology,HistoryOverlay.Cultures,HistoryOverlay.Religions},compressed:true,backgroundOpacity:1,overlayOpacity:.58,includeLabels:false);
                var towns=s.Society.Settlements.Where(t=>t.Population>0).ToArray();
                var row=new{Year=s.Years,Era=era,EraName=WorldEraView.Name(era),FirstState=first,Population=s.History.Aggregates.TotalPopulation,Cultures=s.Society.Cultures.Length,
                    ActiveTowns=towns.Length,AbandonedTowns=s.History.Aggregates.Living.Cities.Count(c=>c.AbandonedYear>=0),
                    VillagePopulation=towns.Where(t=>s.History.Aggregates.Resolve(t.PolityId,s.History)<0).Sum(t=>t.Population),
                    States=s.History.Polities.Count(p=>p.EndedYear<0),IndependentOrigins=s.History.Archive.Count(e=>e.Kind=="independent-institutions"),
                    ColonialCouncils=s.History.Archive.Count(e=>e.Kind=="colonial-council"),Charters=s.History.Aggregates.Empires.Territories.Posts.Count(p=>p.ClosedYear<0),
                    WorstYearMs=worst,WallSeconds=watch.Elapsed.TotalSeconds};
                rows.Add(row);File.WriteAllText(Path.Combine(output,"review.json"),JsonSerializer.Serialize(new{Preset=preset,Seed=seed,Recipe=recipe,Rules=s.History.Version,Rows=rows},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
                File.WriteAllText(Path.Combine(directory,"towns.json"),JsonSerializer.Serialize(towns.Select(t=>new{t.Id,t.Name,t.Cell,t.CultureId,t.PolityId,t.Population,Age=s.Years-t.FoundedYear,Latitude=Math.Asin(world.Surface.Cells[t.Cell].Position.Z)*180/Math.PI,Climate=world.Climate.Classification[t.Cell].ToString(),world.Ecology.Habitat[t.Cell].Biome}),new JsonSerializerOptions{WriteIndented=true}));
                lastMap=s.Years;Console.WriteLine(JsonSerializer.Serialize(row));
            }
            if(s.Years%10000==0)Console.WriteLine($"Progress {preset}/{seed}: {s.Years}, cultures {s.Society.Cultures.Length}, towns {s.Society.Settlements.Length}, {watch.Elapsed.TotalSeconds:F1}s");
            if(tail>=0&&s.Years>=tail+1000)break;
            if(first>=0&&s.Years>first+30000&&era<8){Console.WriteLine("STALLED:30,000 years after first state without industry; preserve evidence for correction.");break;}
        }
        var final=session.Snapshot();engine.Validate(final);world.Prehistory=final;
        File.WriteAllBytes(Path.Combine(output,"final.world"),WorldFile.Write(world));
        Console.WriteLine("FINAL "+final.Hash+" at "+final.Years+" after "+watch.Elapsed.TotalSeconds.ToString("F1")+"s");
    }
}
