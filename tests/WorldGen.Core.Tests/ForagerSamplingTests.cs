using WorldGen.Core;
using WorldGen.Serialization;
static class ForagerSamplingTests
{
    public static void Run(string path)
    {
        var w=WorldFile.ReadFile(path);var engine=new Prehistory(w);var s=w.Prehistory;var f=s.Foragers;int checks=0;
        void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        var selected=f.Cohorts.Where(c=>c.Population>60).OrderByDescending(c=>c.Population).Take(30).ToArray();
        var locator=new CellLocator(w.Surface.Cells);var samples=new List<ForagerBandSample>();
        foreach(var cohort in selected)
        {
            var a=engine.ForagerBand(s,cohort.Id);var b=engine.ForagerBand(s,cohort.Id);
            Check(a.Id==b.Id&&a.Members.Select(p=>p.Id).SequenceEqual(b.Members.Select(p=>p.Id)),"Repeated local queries preserve bands, people and household IDs");
            Check(a.Members.Length>=4&&a.Members.Any(p=>p.AgeYears>=18),"Sampled resident bands include an adult population");
            Check(a.Members.Select(p=>p.Id).Distinct().Count()==a.Members.Length,"No duplicated people within a band");
            Check(a.Members.All(p=>p.AgeYears>=0&&p.AgeYears<96&&p.Skills.Length==ForagerTechnology.Count),"Age and taught-skill samples stay plausible");
            Check(a.SeasonalCamps.Length==(int)Math.Round(a.MoveFrequencyPerYear)&&a.SeasonalCamps.All(c=>c.Season>=0&&c.Season<4&&f.CellDistrict[c.Cell]==cohort.District&&c.RequiresLocalGroundValidation),"Residence-specific seasonal itineraries remain in real accessible land districts and require local placement checks");
            Check(f.CellDistrict[locator.Nearest(a.Home)]==cohort.District,"Band homes do not drift over a district boundary or into ocean");
            Check(a.SeasonalCamps.All(c=>f.CellDistrict[locator.Nearest(c.Position)]==cohort.District),"Seasonal offsets do not push camps outside their land district");
            for(int k=0;k<a.Techniques.Length;k++)if(a.Techniques[k])Check(ForagerTechnology.All[k].Prerequisites.All(p=>a.Techniques[p]),"Band skills retain necessary prerequisite techniques");
            samples.Add(a);
        }
        Check(samples.Select(b=>b.Id).Distinct().Count()==samples.Count,"Neighbouring bands do not share identity IDs");
        var first=samples[0];var near=engine.ForagerBandsNear(s,first.Position,2,64);Check(near.Any(b=>b.Id==first.Id),"Spatial lookup finds an actual resident at its current camp");
        var later=engine.AdvanceSeconds(s,SimulationTime.Day);var tomorrow=engine.ForagerBand(later,first.Cohort);
        var common=first.Members.Join(tomorrow.Members,p=>p.Id,p=>p.Id,(a,b)=>(a,b)).ToArray();
        Check(common.Length>=first.Members.Length-1&&common.All(p=>Math.Abs(p.b.AgeYears-p.a.AgeYears-1/365d)<.000001),"A gameplay day ages the same residents instead of rerolling them");
        Check(s.Hash==s.ComputeHash(),"Sampling and future snapshots cannot alter source history");
        Console.WriteLine($"PASS {checks} band/household/seasonal-camp checks across {samples.Count} communities");
    }
}
