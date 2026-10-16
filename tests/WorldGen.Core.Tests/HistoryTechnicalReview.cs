using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public static class HistoryTechnicalReview
{
    public static void Run(string[] args)
    {
        int at=Array.IndexOf(args,"--technical-review");var world=WorldFile.Read(File.ReadAllBytes(args[at+1]));var s=world.Prehistory;var h=s.History;var a=h.Aggregates;
        int first=h.Polities.Select(p=>p.FoundedYear).DefaultIfEmpty(-1).Min();var eras=WorldEraView.FirstYears(s);
        int Known(HistoricalCulture c)=>c.Technology.Count(y=>y>0);
        int Installed(HistoricalCulture c)=>HistoricalImperialHierarchy.InstalledPractices(c);
        var live=h.Polities.Where(p=>p.EndedYear<0).ToArray();
        var report=new{Rule=h.Version,Year=s.Years,FirstState=first,Population=a.TotalPopulation,
            Eras=Enumerable.Range(2,9).Select(era=>new{Name=WorldEraView.Name(era),Year=eras[era],AfterFirstState=eras[era]<0?-1:eras[era]-first}),
            ActiveGovernments=live.Length,SovereignStates=live.Count(p=>HistoricalImperialHierarchy.Patron(s,p.Id)<0),Constituents=live.Count(p=>HistoricalImperialHierarchy.Patron(s,p.Id)>=0),
            Provinces=a.Empires?.Territories?.Posts.Length??0,ActiveProvinces=a.Empires?.Territories?.Posts.Count(p=>p.ClosedYear<0)??0,OverseasCharters=h.Archive.Count(e=>e.Kind=="overseas-charter"),NoHabitatLandCells=new Prehistory(world).CellPatch.Where((region,cell)=>region<0&&!world.Hydrology.OceanConnected[cell]).Count(),
            Largest=live.Where(p=>HistoricalImperialHierarchy.Patron(s,p.Id)<0).OrderByDescending(p=>HistoricalImperialHierarchy.AreaKm2(s,p.Id)).Take(12).Select(p=>new{p.Id,p.Name,DirectPopulation=p.Population,ImperialPopulation=HistoricalImperialHierarchy.Population(s,p.Id),DirectArea=p.AreaKm2,ImperialArea=HistoricalImperialHierarchy.AreaKm2(s,p.Id),Subjects=a.Empires?.Governments[p.Id].SubjectCount??0,Known=Known(h.Cultures[p.CultureId]),Installed=Installed(h.Cultures[p.CultureId]),GovernmentAge=s.Years-p.FoundedYear}),
            Technology=Enumerable.Range(0,HistoricalTechnologies.Count(h.Version)).Select(k=>new{Technology=k,Name=HistoricalTechnologies.Names[k],FirstKnown=h.Cultures.Where(c=>c.Technology[k]>0).Select(c=>c.Technology[k]-1).DefaultIfEmpty(-1).Min(),FirstInstalled=h.Cultures.Where(c=>c.Technical?.InstalledYear[k]>0).Select(c=>c.Technical.InstalledYear[k]-1).DefaultIfEmpty(-1).Min(),Known=h.Cultures.Count(c=>c.Technology[k]>0),Installed=h.Cultures.Count(c=>HistoricalTechnologies.Has(c,(HistoricalTechnology)k)),MeanDeployment=h.Cultures.Where(c=>c.Technology[k]>0).Select(c=>c.Technical?.Deployment[k]??1000).DefaultIfEmpty().Average()}),
            ContactGaps=s.Society.Cultures.SelectMany(c=>c.Contacts.Where(peer=>peer>c.Id).Select(peer=>new{A=c.Id,B=peer,KnownGap=Math.Abs(Known(h.Cultures[c.Id])-Known(h.Cultures[peer])),InstalledGap=Math.Abs(Installed(h.Cultures[c.Id])-Installed(h.Cultures[peer]))})).OrderByDescending(p=>p.KnownGap).Take(25),
            StateContactGaps=h.Relations.Where(r=>h.Polities[r.A].EndedYear<0&&h.Polities[r.B].EndedYear<0).Select(r=>new{r.A,r.B,StateA=h.Polities[r.A].Name,StateB=h.Polities[r.B].Name,PracticalA=WorldEraView.CultureEra(h.Cultures[h.Polities[r.A].CultureId]),PracticalB=WorldEraView.CultureEra(h.Cultures[h.Polities[r.B].CultureId]),KnownGap=Math.Abs(Known(h.Cultures[h.Polities[r.A].CultureId])-Known(h.Cultures[h.Polities[r.B].CultureId])),r.Trade,r.WarSince}).OrderByDescending(r=>Math.Abs(r.PracticalA-r.PracticalB)).Take(30),
            ImperialEvents=h.Archive.Where(e=>e.Kind.StartsWith("imperial-",StringComparison.Ordinal)).Select(e=>new{e.Year,e.Kind,e.PolityId,e.OtherPolityId,e.Text}),
            Technical=h.Cultures.Select(c=>new{c.Id,Name=s.Society.Cultures[c.Id].Name,SupportedUrban=a.Cultures[c.Id].SupportedUrban,Projects=TechnicalResearchView.Read(s,c.Id)})};
        string output=Path.GetFullPath(args[at+2]);Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine($"{h.Version}, first state {first}, {live.Length} governments, {live.Count(p=>HistoricalImperialHierarchy.Patron(s,p.Id)>=0)} constituents. {output}");
    }
}
