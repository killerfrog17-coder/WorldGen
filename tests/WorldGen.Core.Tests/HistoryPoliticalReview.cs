using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Explicit offline review only. Never called by an ordinary state event.
public static class HistoryPoliticalReview
{
    public static void Run(string path,string output)
    {
        if(File.Exists(output))throw new IOException("Refusing to overwrite a political review.");
        var world=WorldFile.Read(File.ReadAllBytes(path));var s=world.Prehistory;var engine=new Prehistory(world);engine.Validate(s);
        string before=s.ComputeHash();var h=s.History;var a=h.Aggregates;
        var rows=h.Polities.Select(p=>
        {
            var policy=a.Communities.Governments[p.Id];var ds=h.Dynamics.States[p.Id];var leader=h.Dynamics.Leaders[ds.LeaderId];
            var profile=engine.PoliticalProfile(s,p.Id);
            return new{p.Id,p.Name,Active=p.EndedYear<0,p.FoundedYear,p.EndedYear,Age=(p.EndedYear<0?s.Years:p.EndedYear)-p.FoundedYear,
                Sovereign=HistoricalImperialHierarchy.Sovereign(s,p.Id),p.CultureId,p.Government,p.Population,p.AreaKm2,
                policy.RepresentationMilli,policy.CentralizationMilli,p.Legitimacy,p.Cohesion,p.Exhaustion,
                EliteCapture=ds.Cycle?.EliteCapture,leader.Diplomacy,leader.Administration,leader.Military,
                CurrentOldBargain=leader.Diplomacy+leader.Administration+1000-policy.CentralizationMilli>1500,
                Form=profile?.Form,Blocs=profile?.Blocs,Influence=profile?.InfluencePermille,
                Cities=a.Polities[p.Id].Cities.Count,Receipts=a.Polities[p.Id].Receipts,Service=a.Polities[p.Id].ServiceCost};
        }).ToArray();
        var report=new{Year=s.Years,Rule=h.Version,Source=Path.GetFullPath(path),
            ActiveForms=rows.Where(x=>x.Active).GroupBy(x=>x.Form).Select(g=>new{Form=g.Key,Count=g.Count(),People=g.Sum(x=>x.Population)}),
            ActiveRepresentation=rows.Where(x=>x.Active).Select(x=>new{x.Id,x.RepresentationMilli,x.CentralizationMilli,x.CurrentOldBargain}),
            PoliticalEvents=h.Archive.Where(e=>e.Kind=="political-reform"||e.Kind=="political-repression").GroupBy(e=>e.Kind).Select(g=>new{Kind=g.Key,Count=g.Count()}),
            States=rows,InputPreserved=before==s.ComputeHash()};
        if(!report.InputPreserved)throw new InvalidOperationException("Political review mutated its input.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Political review: "+rows.Count(x=>x.Active)+" active states; source hash preserved.");
    }
}
