using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Offline explanation only; results never feed authoritative decisions.
public static class HistoryPrestateOpportunityAudit
{
    public static void Run(string input,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new diagnostic output.");
        var world=WorldFile.Read(File.ReadAllBytes(input));var s=world.Prehistory;var engine=new Prehistory(world);engine.Validate(s);string original=s.Hash;
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
        var a=s.History.Aggregates;long readCounter=a.CellReads;
        var regions=a.Regions.Where(r=>s.Population[r.Id]>0).Select(r=>new{r.Id,r.BestCell,r.Capacity,People=s.Population[r.Id],r.Culture,Arrival=s.ArrivalYear[r.Id],Landmass=world.Landmass[r.BestCell],Habitat=world.Ecology.Habitat[r.BestCell],LandLinks=r.Routes.Count(id=>!engine.Routes[id].Sea),SeaLinks=r.Routes.Count(id=>engine.Routes[id].Sea),NeighborCapacity=r.Routes.Select(id=>engine.Routes[id]).Select(route=>a.Regions[route.A==r.Id?route.B:route.A].Capacity).DefaultIfEmpty().Max()});
        var cultures=s.Society.Cultures.Select(c=>{
            var hc=s.History.Cultures[c.Id];var crop=s.History.Agriculture[c.Id];int region=engine.CellPatch[c.OriginCell];
            bool maritime=(bool)Call("CommunityMaritimePractice",s,c.Id);
            return new{c.Id,c.Name,c.OriginCell,c.FoundedYear,c.Population,c.Farming,c.Fishing,c.Hunting,c.Gathering,Region=region,Retired=s.History.RegionalTraditions?.Retired[c.Id]??-1,
                CurrentCulture=HistoricalCells.Culture(s,c.OriginCell),Habitat=world.Ecology.Habitat[c.OriginCell],Maritime=maritime,MarineFood=Call("MarineForagerSupport",s,c.OriginCell),MarineCatchment=Call("ForagerMaritimeResidents",s,c.Id),MarineSupport=Call("ForagerMaritimeSupport",s,c.Id,maritime),
                crop.NativeMask,crop.AdoptedYear,crop.Practice,crop.PracticeCell,crop.WildOriginCell,
                WildFits=Enumerable.Range(0,6).Select(k=>Call("CommunityWildCropFit",s,c.OriginCell,k)),
                hc.Institution,hc.Surplus,hc.Sedentism,Levels=hc.Technical?.Levels,Deployment=hc.Technical?.Deployment,Offers=hc.Technical?.Offers,
                Knowledge=hc.Technology.Select((year,k)=>new{Technology=HistoricalTechnologies.Names[k],Year=year-1}).Where(x=>x.Year>=0),
                Environment=Call("EnvironmentalEstablishment",s,c.OriginCell,c.Id),CropSupport=Call("AggregateCropSupport",s,c.Id,c.OriginCell,false)};
        }).ToArray();
        // Private accounting queries instrument CellReads. Observation must not
        // become an authoritative work-counter change in the input checkpoint.
        a.CellReads=readCounter;
        if(s.ComputeHash()!=original)throw new Exception("Read-only diagnostic mutated history.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Year=s.Years,Hash=original,Population=a.TotalPopulation,Regions=regions,Cultures=cultures},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine("Read-only pre-state audit: "+output);
    }
}
