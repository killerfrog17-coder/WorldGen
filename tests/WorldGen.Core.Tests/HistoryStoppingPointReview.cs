using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

// Deliberate offline comparison only: these inventories are not actor turns.
public static class HistoryStoppingPointReview
{
    public static void Snapshot(string path,string folder)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);engine.Validate(world.Prehistory);
        Directory.CreateDirectory(folder);
        HistoryAudit.Snapshot(world,engine,world.Prehistory,folder,new[]{HistoryOverlay.States},compressed:true,backgroundOpacity:1,overlayOpacity:.58,includeLabels:false);
    }
    public static void Run(string path,string folder,int years)
    {
        if(years<1||years>1000)throw new ArgumentOutOfRangeException(nameof(years));
        if(Directory.Exists(folder))throw new IOException("Refusing to overwrite a completed comparison.");
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;
        string hash=original.ComputeHash();var engine=new Prehistory(world);
        if(original.History.ReceiverFrontier!=null||original.History.Aggregates.Empires.Connections.CoastalFarming!=null)
            throw new ArgumentException("Use a frozen V control without the candidate rules.");
        object Summary(PrehistoryState s)
        {
            var a=s.History.Aggregates;var farms=a.Empires.Connections.CoastalFarming;
            var live=s.Society.Settlements.Where(t=>t.Population>0&&a.Living.Cities[t.Id].AbandonedYear<0).ToArray();
            return new{Year=s.Years,Era=WorldEraView.Name(WorldEraView.Of(s)),Population=a.TotalPopulation,
                RecordedTowns=a.Cities.Length,LiveTowns=live.Length,Abandoned=a.Living.Cities.Count(c=>c.AbandonedYear>=0),
                LiveStates=s.History.Polities.Count(p=>p.EndedYear<0),Urban=live.Sum(t=>t.Population),
                LocalFamilies=live.Count(t=>farms!=null&&farms.Families[t.Id]>=0),
                NewLocalFamilies=live.Where(t=>t.Id>=original.Society.Settlements.Length&&farms!=null&&farms.Families[t.Id]>=0)
                    .Select(t=>new{t.Id,t.Name,t.Cell,t.FoundedYear,t.Population,Rural=a.Cities[t.Id].Rural,Parent=farms.Families[t.Id]}).ToArray(),
                LivePorts=a.Empires.Connections.ProvisionedPorts.Ports.Count(p=>a.Living.Cities[p.TownId].AbandonedYear<0),
                SupplierExperimentEnabled=a.Empires.Connections.ProvisionedPorts.SupplierRouting!=null,Hash=s.Hash};
        }
        var baseline=engine.AdvanceYears(original,years);engine.Validate(baseline);
        var enabled=engine.UpgradeCoastalFarming(engine.UpgradeReceiverFrontierLearning(original));
        var candidate=engine.AdvanceYears(enabled,years);engine.Validate(candidate);
        if(original.ComputeHash()!=hash)throw new InvalidOperationException("Comparison mutated its input.");
        if(candidate.History.Aggregates.Empires.Connections.ProvisionedPorts.SupplierRouting!=null)
            throw new InvalidOperationException("Rejected supplier routing must remain disabled.");
        Directory.CreateDirectory(folder);
        world.Prehistory=baseline;File.WriteAllBytes(Path.Combine(folder,"baseline.world"),WorldFile.Write(world));
        world.Prehistory=candidate;File.WriteAllBytes(Path.Combine(folder,"candidate.world"),WorldFile.Write(world));
        File.WriteAllText(Path.Combine(folder,"comparison.json"),JsonSerializer.Serialize(new{Source=Path.GetFullPath(path),InputHash=hash,Years=years,
            Before=Summary(original),Baseline=Summary(baseline),Candidate=Summary(candidate)},new JsonSerializerOptions{WriteIndented=true}));
        foreach(var item in new[]{("baseline",baseline),("candidate",candidate)})
        {
            string images=Path.Combine(folder,item.Item1);Directory.CreateDirectory(images);
            HistoryAudit.Snapshot(world,engine,item.Item2,images,new[]{HistoryOverlay.States},compressed:true,backgroundOpacity:1,overlayOpacity:.58,includeLabels:false);
        }
        Console.WriteLine("Validated matched stopping-point comparison: "+folder);
    }
}
