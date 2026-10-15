using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Explicit, read-only review tool. Mesh/frontier enumeration belongs here, not
// in an authoritative simulation event. Reflection calls existing pure service
// queries so the audit does not maintain a second expansion algorithm.
public static class HistoryTerritorialAudit
{
    public static void Run(string[] args)
    {
        int at=Array.IndexOf(args,"--territorial-audit");
        var world=WorldFile.Read(File.ReadAllBytes(args[at+1]));var s=world.Prehistory;
        var engine=new Prehistory(world);engine.Validate(s);var h=s.History;var a=h.Aggregates;
        string hash=s.ComputeHash();
        var service=typeof(Prehistory).GetMethod("TerritorialServiceSource",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var worthwhile=typeof(Prehistory).GetMethod("TerritorialIncorporationWorthwhile",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var codes=PrehistoryView.Codes(world,engine,s,-1,HistoryOverlay.States);
        var land=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>!world.Hydrology.OceanConnected[i]).GroupBy(i=>world.Landmass[i]).Select(g=>new{
            Landmass=g.Key,AreaKm2=g.Sum(i=>world.Surface.Cells[i].AreaM2/1e6),
            InhabitedKm2=g.Where(i=>codes[i]>=170).Sum(i=>world.Surface.Cells[i].AreaM2/1e6),
            ClaimedKm2=g.Where(i=>HistoricalCells.Polity(s,i)>=0).Sum(i=>world.Surface.Cells[i].AreaM2/1e6),
            NamedCultureKm2=g.Where(i=>HistoricalCells.Culture(s,i)>=0).Sum(i=>world.Surface.Cells[i].AreaM2/1e6)
        }).OrderByDescending(g=>g.AreaKm2).ToArray();
        var fronts=new List<object>();
        foreach(var p in h.Polities.Where(p=>p.EndedYear<0))
        {
            var account=a.Polities[p.Id];var hc=h.Cultures[p.CultureId];var candidates=new SortedSet<int>();
            // A documented bounded sample for each court, not a census of all
            // its holdings; only the separate map totals above visit all cells.
            int sampled=Math.Min(128,account.Frontier.Count);
            for(int k=0;k<sampled;k++)
            {
                int cell=account.Frontier.At((int)((long)k*account.Frontier.Count/sampled));
                foreach(int n in world.Surface.Cells[cell].Neighbors)
                    if(!world.Hydrology.OceanConnected[n]&&world.Hydrology.LakeCoverageFraction[n]<.9&&HistoricalCells.Polity(s,n)<0)candidates.Add(n);
            }
            int reachable=0,profitable=0;long area=0;
            foreach(int target in candidates)
            {
                int source=(int)service.Invoke(engine,new object[]{s,p.Id,target})!;
                if(source== -1)continue;reachable++;
                if((bool)worthwhile.Invoke(engine,new object[]{s,p.Id,target,source})!)profitable++;
                area+=(long)Math.Round(world.Surface.Cells[target].AreaM2/1e6);
            }
            var capital=world.Surface.Cells[s.Society.Settlements[p.CapitalId].Cell].Position;
            fronts.Add(new{p.Id,p.Name,p.CultureId,p.Population,p.AreaKm2,p.Cohesion,p.Legitimacy,p.Treasury,
                CapitalLongitude=Math.Atan2(capital.Y,capital.X)*180/Math.PI,CapitalLatitude=Math.Asin(capital.Z)*180/Math.PI,
                account.Receipts,account.ServiceCost,account.CityCount,account.Urban,account.Remote,account.ReachKm,
                PublicWorks=h.Dynamics.States[p.Id].PublicWorks,FrontierCount=account.Frontier.Count,SampledFronts=sampled,
                UnclaimedCandidates=candidates.Count,ReachableCandidates=reachable,EconomicallySupportedCandidates=profitable,ReachableAreaKm2=area,
                Posts=a.Empires.Governments[p.Id].AdministrativePosts.Count,FrontierBudget=HistoricalDevelopmentEffects.FrontierBudget(hc,s.Years,account.CityCount,true),
                ModernTerritory=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Telegraph,s.Years)+HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years),
                KnowledgeEra=WorldEraView.KnowledgeEra(hc),PracticalEra=WorldEraView.CultureEra(hc)});
        }
        if(s.ComputeHash()!=hash)throw new Exception("Territorial audit changed authoritative history.");
        File.WriteAllText(args[at+2],JsonSerializer.Serialize(new{Rule=h.Version,Year=s.Years,Hash=hash,Landmasses=land,Frontiers=fronts},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Read-only territorial audit: "+land.Length+" landmasses and "+fronts.Count+" local governments; unchanged "+hash);
    }
}
