using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;
public static class HistoryColonialReview
{
    public static void Run(string[] args)
    {
        int at=Array.IndexOf(args,"--colonial-review");var w=WorldFile.Read(File.ReadAllBytes(args[at+1]));var s=w.Prehistory;var h=s.History;var a=h.Aggregates;var c=a.Empires.Connections;
        var engine=new Prehistory(w);long farmerPeople=0,foragerPeople=0,remote=0,rural=0,capacity=0;
        for(int r=0;r<a.Regions.Length;r++){if(a.Regions[r].Culture>=0&&s.Society.Cultures[a.Regions[r].Culture].Farming>=180)farmerPeople+=s.Population[r];else foragerPeople+=s.Population[r];}
        foreach(var p in a.Polities){remote+=p.Remote;capacity+=p.RemoteCapacity;}
        foreach(var town in a.Cities)rural+=town.Rural;
        var first=h.Polities.Min(p=>p.FoundedYear);var eras=WorldEraView.FirstYears(s);
        var report=new{h.Version,s.Years,a.TotalPopulation,FarmerPeople=farmerPeople,ForagerPeople=foragerPeople,Remote=remote,CatchmentRural=rural,RemoteBaseCapacity=capacity,
            FirstCultures=s.Society.Cultures.OrderBy(x=>x.FoundedYear).Take(12).Select(x=>new{x.Id,x.Name,x.FoundedYear,x.Farming}),
            IndependentCivilizationRoots=c?.Cultures.Where(x=>x.CivilizationRoot>=0).Select(x=>x.CivilizationRoot).Distinct().Count(),SponsoredCultures=c?.Cultures.Count(x=>x.ColonialSponsor>=0),Charts=c?.Cultures.Sum(x=>x.Charts.Length),RecognizedTitles=c?.Titles.Length,
            Roots=c?.Cultures.Select((x,i)=>new{Culture=i,x.CivilizationRoot,x.ColonialSponsor,x.DormantSince,Episodes=x.Episodes.Length}).Where(x=>x.CivilizationRoot>=0),
            OldestActiveGovernments=h.Polities.Where(p=>p.EndedYear<0).OrderBy(p=>p.FoundedYear).Take(12).Select(p=>new{p.Id,p.Name,Age=s.Years-p.FoundedYear,Area=HistoricalImperialHierarchy.AreaKm2(s,p.Id),Patron=HistoricalImperialHierarchy.Patron(s,p.Id)}),
            LongestAllegiances=a.Empires.Governments.Select((g,i)=>new{State=i,g.Patron,Age=s.Years-g.SinceYear,g.Imposed}).Where(x=>x.Patron>=0).OrderByDescending(x=>x.Age).Take(12),
            ColonialEvents=h.Archive.Where(e=>e.Kind=="overseas-charter"||e.Kind=="colonial-community"||e.Kind=="territorial-recognition"||e.Kind=="territorial-purchase").Select(e=>new{e.Year,SinceFirst=e.Year-first,SinceEarlyModern=e.Year-eras[7],e.Kind,e.PolityId,e.CultureId,e.Text,Latitude=Math.Asin(w.Surface.Cells[e.Cell].Position.Z)*180/Math.PI,Longitude=Math.Atan2(w.Surface.Cells[e.Cell].Position.Y,w.Surface.Cells[e.Cell].Position.X)*180/Math.PI}),
            FirstCenturyCharters=h.Archive.Where(e=>e.Kind=="overseas-charter").GroupBy(e=>e.PolityId).Select(g=>new{State=g.Key,First=g.Min(e=>e.Year),FirstCentury=g.Count(e=>e.Year<=g.Min(x=>x.Year)+100)}),
            Regions=a.Regions.Select((r,i)=>new{Id=i,r.Culture,Population=s.Population[i],r.AreaKm2,Farmer=r.Culture>=0?s.Society.Cultures[r.Culture].Farming:0,Exposure=a.Living.Regions[i].Exposure}).OrderByDescending(r=>r.Population).Take(20)};
        File.WriteAllText(args[at+2],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));Console.WriteLine($"Colonial audit {h.Version}: {a.TotalPopulation:N0} people, {c?.Titles.Length??0} recognized titles, {c?.Cultures.Sum(x=>x.Charts.Length)??0} charts.");
    }
}
