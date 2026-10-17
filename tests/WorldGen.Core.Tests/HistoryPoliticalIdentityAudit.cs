using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using WorldGen.Core;

// Explicit saved-world diagnostics only; never installed in the simulation loop.
public static class HistoryPoliticalIdentityAudit
{
    public static void RunLoaded(World world,string source,string folder)
    {
        if(Directory.Exists(folder))throw new IOException("Use a new political identity output directory.");
        var s=world.Prehistory;var h=s.History;string before=s.ComputeHash();
        int n=world.Surface.Cells.Length;var controllers=new int[n];var sovereigns=new int[n];
        var ownedCounts=new int[h.Polities.Length];var sovereignCounts=new int[h.Polities.Length];
        int land=0,owned=0;
        for(int cell=0;cell<n;cell++)
        {
            controllers[cell]=-1;sovereigns[cell]=-1;
            if(world.Hydrology.OceanConnected[cell])continue;
            land++;int owner=HistoricalCells.Polity(s,cell);if(owner<0)continue;
            if(h.Polities[owner].EndedYear>=0)throw new Exception("Resolved political controller is ended.");
            int sovereign=HistoricalImperialHierarchy.Sovereign(s,owner);
            if(sovereign<0||h.Polities[sovereign].EndedYear>=0)throw new Exception("Resolved sovereign is not living.");
            controllers[cell]=owner;sovereigns[cell]=sovereign;ownedCounts[owner]++;sovereignCounts[sovereign]++;owned++;
        }
        var towns=s.Society.Settlements.Where(t=>t.Population>0&&h.Aggregates.Living.Cities[t.Id].AbandonedYear<0).ToArray();
        var directTownCounts=new int[h.Polities.Length];var sovereignTownCounts=new int[h.Polities.Length];
        foreach(var town in towns)
        {
            int owner=h.Aggregates.Resolve(town.PolityId,h);if(owner<0)continue;
            directTownCounts[owner]++;sovereignTownCounts[HistoricalImperialHierarchy.Sovereign(s,owner)]++;
        }
        var rows=h.Polities.Where(p=>p.EndedYear<0).Select(p=>new
        {
            p.Id,p.Name,p.CultureId,p.FoundedYear,Age=s.Years-p.FoundedYear,
            Sovereign=HistoricalImperialHierarchy.Sovereign(s,p.Id),
            DirectControlledFineCells=ownedCounts[p.Id],SovereignControlledFineCells=sovereignCounts[p.Id],
            ShareOfControlledFineCells=owned==0?0:sovereignCounts[p.Id]/(double)owned,
            ExclusiveCourtPopulation=p.Population,ExclusiveCourtAreaKm2=p.AreaKm2,
            DirectActiveTowns=directTownCounts[p.Id],SovereignActiveTowns=sovereignTownCounts[p.Id]
        }).OrderByDescending(p=>p.SovereignControlledFineCells).ThenBy(p=>p.Id).ToArray();
        Directory.CreateDirectory(folder);
        string[] directLegend=rows.OrderByDescending(p=>p.DirectControlledFineCells).ThenBy(p=>p.Id).Take(12)
            .Select(p=>$"{p.Id}: {p.Name} / {p.DirectControlledFineCells} owned cells").ToArray();
        string[] sovereignLegend=rows.Where(p=>p.Sovereign==p.Id).Take(12)
            .Select(p=>$"{p.Id}: {p.Name} / {p.SovereignActiveTowns} towns").ToArray();
        HistoryReligionEvidenceAudit.Draw(world,s,controllers,Path.Combine(folder,"controllers.svg"),
            "Saved actual court controller / opaque; grey = unowned; palette may repeat",directLegend);
        HistoryReligionEvidenceAudit.Draw(world,s,sovereigns,Path.Combine(folder,"sovereigns.svg"),
            "Saved actual sovereign controller / opaque; grey = unowned; palette may repeat",sovereignLegend);
        if(s.ComputeHash()!=before)throw new Exception("Political identity audit changed source state.");
        File.WriteAllText(Path.Combine(folder,"identities.json"),JsonSerializer.Serialize(new
        {
            Source=Path.GetFullPath(source),Year=s.Years,Hash=before,
            RealmRules=h.Aggregates.Empires.Connections.Realms.Version,
            NonOceanFineCells=land,OwnedFineCells=owned,UnownedFineCells=land-owned,Courts=rows,
            Notes=new[]{
                "Explicit offline census of saved resolved controller IDs, not a runtime pass or a recognized-title/ambition map.",
                "Sovereign identity follows the existing bounded imperial hierarchy. Direct court ownership and sovereign allegiance are separate layers.",
                "Fine-cell counts are not exact physical area or population shares; exclusive saved court area/population accounts are separately labeled.",
                "Opaque colors use the existing presentation palette and can repeat; exact JSON IDs, not color similarity, determine identity. No town-marker overlay obscures the map.",
                "A saved ownership record alone does not prove historical continuity, currently supplied shipping or effective service at every cell. Those require actual dated campaigns/service evidence.",
                "Grey means no saved resolved controller, not uninhabited land, atheism, an unclaimed cultural region or missing human history."
            }
        },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Political identity {s.Years}: {rows.Length} living courts; {owned}/{land} non-ocean fine cells controlled; preserved {before}");
    }
}
