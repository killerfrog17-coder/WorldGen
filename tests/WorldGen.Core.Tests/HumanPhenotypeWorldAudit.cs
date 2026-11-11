using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

static class HumanPhenotypeWorldAudit
{
    public sealed class Row
    {
        public string Label="",Culture="",People="",Species="";public int Cohort,SourceCell;public ForagerPersonSample Person;
    }
    public sealed class Receipt
    {public string HistoryHash="";public int Seed,Year;public Row[] Rows=Array.Empty<Row>();public int DistinctPeople,DistinctSpecies;}
    public static void Run(string input,string output)
    {
        var world=WorldFile.ReadFile(input);var state=world.Prehistory;var f=state.Foragers??throw new Exception("Foraging history required");var history=new Prehistory(world);string before=state.ComputeHash();
        var live=f.Cohorts.Where(c=>c.Population>=100).ToArray();
        var candidates=live.OrderBy(c=>c.Appearance[0]).Take(2).Concat(live.OrderByDescending(c=>c.Appearance[0]).Take(2)).Concat(live.GroupBy(c=>c.Lineage).Select(g=>g.OrderByDescending(c=>c.Population).First())).Concat(live.OrderBy(c=>Rng.Value(world.Recipe.Seed,"phenotype-review-cohort",c.Id))).DistinctBy(c=>c.Identity).Take(8);
        var rows=new List<Row>();
        foreach(var c in candidates)
        {
            var band=history.ForagerBand(state,c.Id);if(band==null)continue;
            var selected=band.Members.Where(p=>p.AgeYears>=18).Take(2).Concat(band.Members.Where(p=>p.AgeYears>=3&&p.AgeYears<16).Take(1));
            foreach(var person in selected)
            {
                person.Phenotype.Validate();var repeat=history.ForagerBand(state,c.Id).Members.Single(p=>p.Id==person.Id);
                if(!person.Phenotype.Traits.SequenceEqual(repeat.Phenotype.Traits))throw new Exception("Actual NPC query changed inherited appearance");
                rows.Add(new Row{Label="Actual generated resident",Cohort=c.Id,SourceCell=f.Districts[c.District].Cell,Culture=f.Cultures[c.Culture].Name,People=person.Phenotype.PeopleName,Species=person.Phenotype.Species,Person=PlayerIdentity.CopyPerson(person)});
            }
        }
        if(rows.Count<8)throw new Exception("Insufficient actual regional residents");
        if(before!=state.ComputeHash())throw new Exception("Appearance queries mutated regional history");
        var receipt=new Receipt{HistoryHash=before,Seed=world.Recipe.Seed,Year=state.Years,Rows=rows.ToArray(),DistinctPeople=rows.Select(r=>r.People).Distinct().Count(),DistinctSpecies=rows.Select(r=>r.Species).Distinct().Count()};
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(receipt,new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
        Console.WriteLine($"PASS {rows.Count} actual generated resident profiles /{receipt.DistinctPeople} peoples /{receipt.DistinctSpecies} human species; repeated query exact; history unchanged");
    }
}
