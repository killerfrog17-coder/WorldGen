using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistoryLivingAtlasTests
{
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var s=engine.UpgradeToLivingWorldHistory(world.Prehistory);string hash=s.ComputeHash();
        int state=Array.FindIndex(s.History.Polities,p=>p.EndedYear<0);
        if(state<0)throw new InvalidOperationException("Atlas fixture needs a state.");
        var document=HistoryAtlas.Read(s,"state",state,world,engine);
        check(document.Sections.Single(x=>x.Title=="Local resource potential").Links.Length==13,"Atlas displays all thirteen resources as potentials rather than unrecorded reserves");
        var interests=document.Sections.Single(x=>x.Title=="Political interests");
        check(interests.Links.Length==5&&interests.Links.Sum(x=>x.Value)==1000&&interests.Text.Contains("not measured"),"Government infographic distinguishes five derived interests from actual party membership");
        check(document.Sections.Single(x=>x.Title=="Public priorities").Links.Sum(x=>x.Value)==1000,"Budget infographic preserves the finite four-way allocation");
        int culture=s.History.Polities[state].CultureId;var people=HistoryAtlas.Read(s,"people",culture,world,engine);
        var appearance=people.Sections.Single(x=>x.Title=="People and appearance");
        check(appearance.Facts.Count(x=>x.Swatch.Length==7&&x.Swatch.StartsWith("#",StringComparison.Ordinal))==3,"People page uses actual skin, hair and iris swatches, not an invented ethnic portrait");
        check(appearance.Text.Contains("no effect on intelligence"),"Appearance remains separate from historical capability");
        check(document.Sections.Any(x=>x.Title=="Climate and livelihoods")&&HistoryAtlas.PlainText(document).Contains("Local resource potential"),"Climate/resource explanations are available in the article and copied text");
        check(s.ComputeHash()==hash,"Opening all living-world infographics does not mutate history");
    }
}
