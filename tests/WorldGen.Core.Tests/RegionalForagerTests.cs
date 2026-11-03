using System.Diagnostics;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

static class RegionalForagerTests
{
    public static void Run(string folder,int detail,int years=10000,string preset="Continents")
    {
        Directory.CreateDirectory(folder);int checks=0;
        void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
        var watch=Stopwatch.StartNew();string geometry=Path.Combine(folder,"geometry.world");World w;
        if(File.Exists(geometry))w=WorldFile.ReadFile(geometry);
        else{var recipe=Recipe.Preset(preset);recipe.Detail=detail;recipe.Seed=73421;w=new WorldPipeline().Generate(recipe);File.WriteAllBytes(geometry,WorldFile.Write(w));}
        w.Prehistory=null;Console.WriteLine($"Geometry {w.Surface.Cells.Length} cells in {watch.Elapsed.TotalSeconds:F2}s");
        watch.Restart();var engine=new Prehistory(w);var initial=engine.StartForagers(engine.PrepareForagers());double prepare=watch.Elapsed.TotalSeconds;
        var session=new SimulationSession(engine,initial);var clock=initial;
        long fineStep=Math.Max(1,Math.Min(SimulationTime.Day/5,(initial.Foragers.NextReviewSeconds-1)/1000));
        watch.Restart();for(int i=0;i<1000;i++)clock=session.AdvanceSeconds(fineStep);
        double fineMs=watch.Elapsed.TotalMilliseconds;
        Check(fineMs<1000,"1000 fine-clock publications finish below one second");
        Check(ReferenceEquals(clock.Foragers.Cohorts,initial.Foragers.Cohorts)&&ReferenceEquals(clock.Society,initial.Society),"Empty clock intervals share frozen regional data");
        Check(clock.Hash==clock.ComputeHash(),"Fine-clock digest equals independently recomputed digest");
        Check(initial.Foragers.Seconds==0,"Publishing clocks does not mutate the input");
        var pending=RegionalQueueNode.Entries(initial.Foragers.PendingEvents).ToArray();
        Check(pending.Select(e=>e.Due).Distinct().Count()>pending.Length*.9,"Initial regional events are independently staggered");
        var single=engine.AdvanceSeconds(initial,initial.Foragers.NextReviewSeconds,1);
        Check(single.Foragers.ProcessedEvents==1,"One queued event processes exactly one entity/process");
        Check(initial.Foragers.Cohorts.Zip(single.Foragers.Cohorts,(a,b)=>ReferenceEquals(a,b)).Count(same=>!same)<=2,"A regional event leaves unrelated population records shared and untouched");
        var costs=new Dictionary<string,(long calls,double ms,long bytes)>();engine.WorkObserver=m=>{costs.TryGetValue(m.Stage,out var prior);costs[m.Stage]=(prior.calls+1,prior.ms+m.Milliseconds,prior.bytes+m.AllocatedBytes);};
        session=new SimulationSession(engine,initial);watch.Restart();var state=session.AdvanceSeconds(years*SimulationTime.Year,int.MaxValue);double historical=watch.Elapsed.TotalSeconds;
        engine.ValidateForagers(state);
        Check(state.Years==years,"Historical advance reaches requested date");
        Check(state.Foragers.Cohorts.Length<=state.Foragers.Districts.Length,"At most one population aggregate per ecoregion");
        Check(state.Foragers.Cohorts.Select(c=>c.District).Distinct().Count()==state.Foragers.Cohorts.Length,"Migration merges into regional totals");
        Check(state.Foragers.Cultures.Length<=state.Foragers.Districts.Length&&state.Foragers.Languages.Length<=state.Foragers.Districts.Length&&state.Foragers.Beliefs.Length<=state.Foragers.Districts.Length,"Full cultural profiles are bounded by regions");
        Check(state.Foragers.LanguageFamilies.Length<=Math.Max(16,state.Foragers.Districts.Length*2),"Family ancestry summaries remain bounded");
        Check(state.Foragers.Discoveries.Length<=state.Foragers.Districts.Length*ForagerTechnology.Count,"Discovery summaries do not accumulate repeated local reinventions");
        var partition=new SimulationSession(engine,initial);var split=state;
        if(years<=10000)
        {split=initial;for(int i=0;i<4;i++)split=partition.AdvanceSeconds(years*SimulationTime.Year/4,int.MaxValue);}
        if(split.Hash!=state.Hash)
        {
            foreach(var field in typeof(ForagerState).GetFields())
            {var aa=StateHash.Compute(field.GetValue(state.Foragers));var bb=StateHash.Compute(field.GetValue(split.Foragers));if(aa!=bb)Console.WriteLine("DIFF "+field.Name+" "+aa+" "+bb);}
            foreach(var field in typeof(PrehistoryState).GetFields())
            {if(field.Name=="Foragers")continue;var aa=StateHash.Compute(field.GetValue(state));var bb=StateHash.Compute(field.GetValue(split));if(aa!=bb)Console.WriteLine("STATE DIFF "+field.Name);}
        }
        if(years<=10000)Check(split.Hash==state.Hash,"Caller publication frequency does not change history");
        var daySession=new SimulationSession(engine,state);long lateStep=Math.Max(1,Math.Min(SimulationTime.Day/5,(state.Foragers.NextReviewSeconds-state.Foragers.Seconds-1)/1000));watch.Restart();for(int i=0;i<1000;i++)clock=daySession.AdvanceSeconds(lateStep);double lateMs=watch.Elapsed.TotalMilliseconds;
        Check(lateMs<1000,"Late-world fine-clock publications remain below one second");
        w.Prehistory=state;watch.Restart();var bytes=WorldFile.Write(w);var restored=WorldFile.Read(bytes);new Prehistory(restored).ValidateForagers(restored.Prehistory);double saveSeconds=watch.Elapsed.TotalSeconds;
        Check(restored.Prehistory.Hash==state.Hash,"Current-version real save/reload retains regional state");
        var resident=state.Foragers.Cohorts.First(c=>c.Population>=50);var a=engine.ForagerBand(state,resident.Id);var b=engine.ForagerBand(state,resident.Id);
        Check(a!=null&&a.Id==b.Id&&a.Position.X==b.Position.X&&a.Members.Select(p=>p.Id).SequenceEqual(b.Members.Select(p=>p.Id)),"Local camp and residents regenerate deterministically");
        Check(state.Foragers.Cohorts.Length==split.Foragers.Cohorts.Length,"Local queries create no global demographic actors");
        File.WriteAllBytes(Path.Combine(folder,"final.world"),bytes);
        var result=new{checks,cells=w.Surface.Cells.Length,regions=state.Foragers.Districts.Length,aggregates=state.Foragers.Cohorts.Length,active=state.Foragers.Cohorts.Count(c=>c.Population>=1),years,prepareSeconds=prepare,fineClock1000Ms=fineMs,lateFineClock1000Ms=lateMs,historicalSeconds=historical,yearsPerSecond=years/historical,saveBytes=bytes.Length,saveReadWriteSeconds=saveSeconds,state.Hash,population=state.Foragers.Cohorts.Sum(c=>c.Population),languages=state.Foragers.Languages.Length,families=state.Foragers.LanguageFamilies.Length,techniquePatterns=state.Foragers.Cohorts.Where(c=>c.Population>=1).Select(c=>string.Join(",",c.Use.Select(x=>(int)(x*4)))).Distinct().Count()};
        File.WriteAllText(Path.Combine(folder,"result.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(result));
        foreach(var cost in costs.OrderByDescending(p=>p.Value.ms))Console.WriteLine(JsonSerializer.Serialize(new{stage=cost.Key,cost.Value.calls,cost.Value.ms,cost.Value.bytes}));
    }
}
