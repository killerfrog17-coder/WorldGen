using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Serialization;
static class ForagerWorldTests
{
    public static void History(string output,int years=40000,int seed=0,string preset="Continents",int detail=5000)
    {
        Directory.CreateDirectory(output);var recipe=Recipe.Preset(preset);recipe.Detail=detail;if(seed!=0)recipe.Seed=seed;
        var watch=Stopwatch.StartNew();var w=new WorldPipeline().Generate(recipe);var engine=new Prehistory(w);var s=engine.StartForagers(engine.PrepareForagers());
        var records=new List<object>();
        void Record()
        {
            var f=s.Foragers;var live=f.Cohorts.Where(c=>c.Population>=1).ToArray();
            var record=new{year=s.Years,population=live.Sum(c=>c.Population),sapiens=live.Where(c=>f.Lineages[c.Lineage].PlayerSpecies).Sum(c=>c.Population),districts=live.Select(c=>c.District).Distinct().Count(),cohorts=live.Length,families=live.Select(c=>f.Languages[c.Language].Family).Distinct().Count(),languages=live.Select(c=>c.Language).Distinct().Count(),beliefs=live.Select(c=>c.Belief).Distinct().Count(),extinctions=f.Cohorts.Count(c=>c.Population==0),meanSkill=live.Length==0?0:live.Average(c=>c.Skill.Sum()),minSkill=live.Length==0?0:live.Min(c=>c.Skill.Sum()),maxSkill=live.Length==0?0:live.Max(c=>c.Skill.Sum()),meanWinter=live.Length==0?0:live.Average(c=>c.WinterSecurity),births=f.Cohorts.Sum(c=>c.Births),deaths=f.Cohorts.Sum(c=>c.Deaths),elapsed=watch.Elapsed.TotalSeconds};
            records.Add(record);Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(record));
            File.WriteAllText(Path.Combine(output,"metrics.json"),System.Text.Json.JsonSerializer.Serialize(records,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            var techniques=ForagerTechnology.All.Select(t=>new{id=t.Id,name=t.Name,known=live.Count(c=>c.Knowledge[t.Id]>=.5),skilled=live.Count(c=>c.Skill[t.Id]>=.5),used=live.Count(c=>c.Use[t.Id]>=.5),meanUse=live.Length==0?0:live.Average(c=>c.Use[t.Id]),maximum=live.Length==0?0:live.Max(c=>c.Skill[t.Id])}).ToArray();
            File.WriteAllText(Path.Combine(output,"techniques.json"),System.Text.Json.JsonSerializer.Serialize(techniques,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        }
        Record();w.Prehistory=s;File.WriteAllBytes(Path.Combine(output,"year-0.world"),WorldFile.Write(w));
        while(s.Years<years)
        {
            s=engine.AdvanceSeconds(s,Math.Min(1000,years-s.Years)*SimulationTime.Year);engine.ValidateForagers(s);Record();
            if(s.Years%10000==0||s.Years==years){w.Prehistory=s;File.WriteAllBytes(Path.Combine(output,$"year-{s.Years}.world"),WorldFile.Write(w));}
        }
        Console.WriteLine("PASS complete forager history validation");
    }
    public static void Initial()
    {
        var watch=Stopwatch.StartNew();var recipe=Recipe.Preset("Continents");recipe.Detail=5000;var w=new WorldPipeline().Generate(recipe);var terrain=StateHash.Compute(w);var engine=new Prehistory(w);
        var s=engine.PrepareForagers();engine.ValidateForagers(s);var f=s.Foragers;
        void Check(bool ok,string why){if(!ok)throw new Exception(why);}
        Check(f.Districts.Length>engine.Patches.Length,"Demographic districts actually subdivide ecological regions");
        Check(f.Cohorts.Count(c=>f.Lineages[c.Lineage].PlayerSpecies)>1,"Established sapiens begin as a structured metapopulation");
        Check(f.Lineages.Select(l=>l.ScientificName).Distinct().Count()==f.Lineages.Length,"Human lineages have distinct identities, not duplicated thermal labels");
        foreach(var l in f.Lineages)foreach(var pair in l.AncestralRoute.Zip(l.AncestralRoute.Skip(1)))Check(f.Districts[pair.First].Passages.Any(p=>p.To==pair.Second),"Every lineage origin has a real connected ancestral dispersal path");
        Check(f.Lineages.All(l=>l.AncestralRoute[0]==l.AncestorOrigin),"Ancestral routes start from the actual ancestral geography");
        Check(f.Cohorts.GroupBy(c=>c.District).All(g=>g.Select(c=>f.Lineages[c.Lineage].PlayerSpecies).Distinct().Count()==1),"Initial sapiens range does not simply overwrite an already occupied archaic range");
        Check(f.LanguageFamilies.Length>1&&f.Beliefs.Length>1,"Initial world is not one newly invented universal speech/religion");
        Check(f.Cohorts.All(c=>c.Knowledge.Length==ForagerTechnology.Count&&c.Skill.Length==ForagerTechnology.Count&&c.Use.Length==ForagerTechnology.Count),"Every resident cohort has separate knowledge/skill/use records");
        Check(f.Cohorts.All(c=>f.Districts[c.District].AreaKm2>0&&c.Capacity>0),"No population is seeded into a zero-area water district");
        Check(StateHash.Compute(w)==terrain,"Human-world preparation never rewrites geography or source ecology");
        var copy=s.Copy();copy.Foragers.Cohorts[0].Skill[0]=0;Check(s.Hash==s.ComputeHash(),"Published copies cannot alter source cohort skills");
        w.Prehistory=s;var bytes=WorldFile.Write(w);var restored=WorldFile.Read(bytes);new Prehistory(restored).ValidateForagers(restored.Prehistory);Check(restored.Prehistory.Hash==s.Hash,"Real compact world save retains the new data ledger");
        Console.WriteLine($"PASS initial forager world; districts={f.Districts.Length}; old patches={engine.Patches.Length}; cohorts={f.Cohorts.Length}; people={f.Cohorts.Sum(c=>c.Population):F0}; sapiens districts={f.Cohorts.Count(c=>f.Lineages[c.Lineage].PlayerSpecies)}; language families={f.LanguageFamilies.Length}; beliefs={f.Beliefs.Length}; elapsed={watch.Elapsed.TotalSeconds:F2}s");
        foreach(var l in f.Lineages)Console.WriteLine($"{l.ScientificName}: population={f.Cohorts.Where(c=>c.Lineage==l.Id).Sum(c=>c.Population):F0}; origin={l.OriginDistrict}; body={l.AdultMassKg:F1}kg/{l.StatureM:F2}m; ancestral path={l.AncestralRoute.Length} districts");
    }
}
