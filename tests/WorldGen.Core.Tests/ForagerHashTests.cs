using WorldGen.Core;
using WorldGen.Serialization;
static class ForagerHashTests
{
    public static void Run(string file)
    {
        var w=WorldFile.ReadFile(file);var f=w.Prehistory.Foragers;int checks=0;
        void Compare(object value){checks++;if(StateHash.Compute(value)!=StateHash.ComputeHistoricalReference(value))throw new Exception("Typed forager hash differs from original canonical reflection bytes");}
        foreach(var c in f.Cohorts.Take(50).Concat(f.Cohorts.Where(c=>c.Population==0).Take(30))){Compare(c);Compare(c.Speech);Compare(c.Traditions);}
        foreach(var c in f.Cultures.Take(80))Compare(c);
        Compare(f.Cohorts.Take(100).ToArray());Compare(f.Cultures.Take(100).ToArray());Compare(new ForagerCohort[]{null,f.Cohorts[0]});Compare(Array.Empty<ForagerCulture>());Compare(new ForagerAffiliation[]{null,new ForagerAffiliation{Id=17,Share=.123456789}});
        var changed=f.Cohorts[0].Copy();string before=StateHash.Compute(changed);changed.Skill[0]+=.001;
        checks++;if(before==StateHash.Compute(changed))throw new Exception("Typed writer cached stale mutable values");Compare(changed);
        checks++;if(w.Prehistory.ComputeHash()!=w.Prehistory.Hash)throw new Exception("Full historical checkpoint changed under the fast writer");
        Console.WriteLine($"PASS {checks} exact canonical forager/reference and mutation checks");
    }
}
