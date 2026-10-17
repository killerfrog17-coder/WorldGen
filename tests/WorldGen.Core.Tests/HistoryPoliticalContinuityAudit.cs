using System.Text.Json;
using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

// Read-only explicit diagnostics. No upgrade, continuation, or cell census.
public static class HistoryPoliticalContinuityAudit
{
    public static void Run(string path,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new political-audit output path.");
        var w=WorldFile.Read(File.ReadAllBytes(path));var s=w.Prehistory;var e=new Prehistory(w);e.Validate(s);var h=s.History;var a=h.Aggregates;var realms=a.Empires.Connections.Realms;
        var events=h.Archive.ToArray();
        var nodeLive=typeof(Prehistory).GetMethod("TerritorialNodeLive",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var nodes=new Dictionary<(int,int),bool>();var serviceArea=new double[h.Polities.Length];var staleArea=new double[h.Polities.Length];var closedArea=new double[h.Polities.Length];var unsupportedCityArea=new double[h.Polities.Length];
        // Explicit review-only physical census: a named country's colour does
        // not demonstrate that the saved local service source remains alive.
        for(int cell=0;cell<w.Surface.Cells.Length;cell++)
        {
            int owner=HistoricalCells.Polity(s,cell);if(owner<0||w.Hydrology.OceanConnected[cell]||w.Hydrology.LakeCoverageFraction[cell]>=.9)continue;
            int source=a.Empires.Territories.Sources[cell];double area=w.Surface.Cells[cell].AreaM2/1e6*(1-w.Hydrology.LakeCoverageFraction[cell]);
            if(!nodes.TryGetValue((owner,source),out bool live)){live=(bool)nodeLive.Invoke(e,new object[]{s,owner,source})!;nodes[(owner,source)]=live;}
            if(live)serviceArea[owner]+=area;else staleArea[owner]+=area;
            if(source<=-2&&a.Empires.Territories.Posts[-source-2].ClosedYear>=0)closedArea[owner]+=area;
            if(source>=0&&!a.Cities[source].Supported)unsupportedCityArea[owner]+=area;
        }
        var byPolity=events.Where(x=>x.PolityId>=0).GroupBy(x=>x.PolityId).ToDictionary(g=>g.Key,g=>g.GroupBy(x=>x.Kind).ToDictionary(k=>k.Key,k=>k.Count()));
        var related=events.Where(x=>x.OtherPolityId>=0).GroupBy(x=>x.OtherPolityId).ToDictionary(g=>g.Key,g=>g.GroupBy(x=>x.Kind).ToDictionary(k=>k.Key,k=>k.Count()));
        object Row(HistoricalPolity p)
        {
            var g=a.Polities[p.Id];var empire=a.Empires.Governments[p.Id];var country=realms?.Governments[p.Id];var dynamics=h.Dynamics.States[p.Id];
            var cities=Enumerable.Range(0,g.Cities.Count).Select(k=>g.Cities.At(k)).ToArray();
            return new{p.Id,p.Name,p.CultureId,p.ParentId,p.FoundedYear,p.EndedYear,Age=(p.EndedYear<0?s.Years:p.EndedYear)-p.FoundedYear,
                Sovereign=p.EndedYear<0?HistoricalImperialHierarchy.Sovereign(s,p.Id):-1,DirectArea=p.AreaKm2,EmpireArea=p.EndedYear<0?HistoricalImperialHierarchy.AreaKm2(s,p.Id):0,
                LiveServiceArea=serviceArea[p.Id],StaleServiceArea=staleArea[p.Id],ClosedOfficeArea=closedArea[p.Id],UnsupportedCitySourceArea=unsupportedCityArea[p.Id],
                dynamics.PeakAreaKm2,p.Population,empire.SubjectCount,Cities=cities.Length,SupportedCities=cities.Count(k=>a.Cities[k].Supported),
                p.CapitalId,CapitalFounded=s.Society.Settlements[p.CapitalId].FoundedYear,p.Cohesion,p.Legitimacy,p.Exhaustion,p.Treasury,g.Receipts,g.ServiceCost,g.ReachKm,
                Representation=a.Communities.Governments[p.Id].RepresentationMilli,Centralization=a.Communities.Governments[p.Id].CentralizationMilli,
                PublicWorks=dynamics.PublicWorks,Debt=dynamics.Cycle?.ConquestDebt,Capture=dynamics.Cycle?.EliteCapture,
                country?.RegimeChanges,country?.RegionalSeparations,country?.CrisisSeverity,country?.CrisisUntil,
                DirectChildren=h.Polities.Count(x=>x.ParentId==p.Id),LivingChildren=h.Polities.Count(x=>x.ParentId==p.Id&&x.EndedYear<0),
                Events=byPolity.TryGetValue(p.Id,out var own)?own:null,Related=related.TryGetValue(p.Id,out var other)?other:null};
        }
        var alive=h.Polities.Where(p=>p.EndedYear<0).ToArray();var oldest=alive.OrderBy(p=>p.FoundedYear).Take(20).ToArray();var first=h.Polities.OrderBy(p=>p.FoundedYear).ThenBy(p=>p.Id).First();
        var important=new HashSet<string>{"state","secession","collapse","realm-succession-crisis","imperial-allegiance","imperial-independence","regional-realignment","provincial-independence","conquest"};
        var firstTimeline=events.Where(x=>(x.PolityId==first.Id||x.OtherPolityId==first.Id)&&important.Contains(x.Kind)).Select(x=>new{x.Year,x.Kind,x.PolityId,x.OtherPolityId,x.SettlementId,x.Text}).ToArray();
        var summary=new{Year=s.Years,s.Hash,Cells=w.Surface.Cells.Length,RealmVersion=realms?.Version,RecordedCountries=h.Polities.Length,SurvivingCountries=alive.Length,
            Sovereigns=alive.Count(p=>HistoricalImperialHierarchy.Patron(s,p.Id)<0),OlderThan500=alive.Count(p=>s.Years-p.FoundedYear>500),OlderThan1000=alive.Count(p=>s.Years-p.FoundedYear>1000),OlderThan3000=alive.Count(p=>s.Years-p.FoundedYear>3000),
            Wars=h.Dynamics.Campaigns.Length,ActiveWars=h.Dynamics.Campaigns.Count(c=>c.EndedYear<0),Crises=events.Count(x=>x.Kind=="realm-succession-crisis"),Secessions=events.Count(x=>x.Kind=="secession"),
            LiveServiceArea=serviceArea.Sum(),StaleServiceArea=staleArea.Sum(),ClosedOfficeArea=closedArea.Sum(),UnsupportedCitySourceArea=unsupportedCityArea.Sum(),
            Allegiances=events.Count(x=>x.Kind=="imperial-allegiance"),Independences=events.Count(x=>x.Kind=="imperial-independence"),Conquests=events.Count(x=>x.Kind=="conquest"),Abandonments=events.Count(x=>x.Kind=="abandonment")};
        var data=new{Input=path,Summary=summary,FirstCountry=Row(first),Oldest=oldest.Select(Row).ToArray(),Largest=alive.OrderByDescending(p=>HistoricalImperialHierarchy.AreaKm2(s,p.Id)).Take(15).Select(Row).ToArray(),FirstCountryTimeline=firstTimeline,
            EventCounts=events.GroupBy(x=>x.Kind).OrderByDescending(g=>g.Count()).Select(g=>new{Kind=g.Key,Count=g.Count()}).ToArray()};
        File.WriteAllText(output,JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(summary));Console.WriteLine(JsonSerializer.Serialize(Row(first)));
    }
}
