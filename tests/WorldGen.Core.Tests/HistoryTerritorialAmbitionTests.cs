using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;
public static class HistoryTerritorialAmbitionTests
{
    public static void Run(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;string before=source.ComputeHash();var engine=new Prehistory(world);
        var s=engine.UpgradeToTerritorialHistory(source);engine.Validate(s);var h=s.History;var c=h.Aggregates.Empires.Connections;
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,values)!;
        check(h.Version==Prehistory.TerritorialHistoryVersion&&source.History.Aggregates.Empires.Connections.Ambitions==null&&source.ComputeHash()==before,"Regional ambitions require an explicit upgrade and leave the source untouched");
        check(c.Ambitions.Governments.Length==h.Polities.Length&&h.Aggregates.TotalPopulation==source.History.Aggregates.TotalPopulation&&h.Archive.Count==source.History.Archive.Count,"Intent records do not invent population, recognition, territory or history");
        check(engine.UpgradeToCurrentHistory(s)==s&&engine.UpgradeToCoastalHistory(s)==s,"Current and older upgrade entry points cannot downgrade regional rules");
        int polity=Array.FindIndex(h.Polities,p=>p.EndedYear<0&&engine.CellPatch[s.Society.Settlements[p.CapitalId].Cell]>=0);
        int capital=s.Society.Settlements[h.Polities[polity].CapitalId].Cell,region=engine.CellPatch[capital];
        var probe=s.Copy();var aim=probe.History.Aggregates.Empires.Connections.Ambitions.Governments[polity];
        aim.Scope=HistoricalAmbitionScope.Ecoregion;aim.AnchorCell=capital;aim.Region=region;aim.Landmass=world.Landmass[capital];
        var cells=engine.Patches[region].Cells;
        check(cells.Length>1&&cells.All(cell=>(bool)Call("TerritorialAmbitionContains",probe,polity,cell)),"An ecoregion ambition covers its whole multi-cell district, not a single chosen cell");
        int outside=Enumerable.Range(0,engine.CellPatch.Length).First(cell=>engine.CellPatch[cell]>=0&&engine.CellPatch[cell]!=region);
        check(!(bool)Call("TerritorialAmbitionContains",probe,polity,outside),"A regional ambition does not silently claim unrelated regions");
        aim.Scope=HistoricalAmbitionScope.Continental;
        int far=Enumerable.Range(0,engine.CellPatch.Length).First(cell=>engine.CellPatch[cell]>=0&&world.Landmass[cell]==aim.Landmass&&D.Angle(world.Surface.Cells[capital].Position,world.Surface.Cells[cell].Position)*world.Recipe.RadiusKm>1800);
        check((bool)Call("TerritorialAmbitionContains",probe,polity,far),"A continental ambition can extend far beyond current administrative reach");
        aim.Scope=HistoricalAmbitionScope.Subcontinental;
        check((bool)Call("TerritorialAmbitionContains",probe,polity,capital)&&!(bool)Call("TerritorialAmbitionContains",probe,polity,far),"The provisional subcontinental sphere is local to the homeland, not the whole continent");
        var p=probe.History.Polities[polity];p.Exhaustion=0;p.Cohesion=700;p.Army=100;p.Treasury=10000;
        check((double)Call("TerritorialWarMotivation",probe,polity,capital)>(double)Call("TerritorialWarMotivation",probe,polity,far),"An ambition actually changes target preference");
        p.Exhaustion=700;check((double)Call("TerritorialWarMotivation",probe,polity,capital)==0,"A continental dream does not bypass war exhaustion");
        p.Exhaustion=0;p.Treasury=0;check((double)Call("TerritorialWarMotivation",probe,polity,capital)==0,"An unfunded court cannot launch discretionary wars for its ambitions");
        check(c.Ambitions.Governments[polity].Scope!=aim.Scope||!ReferenceEquals(c.Ambitions.Governments[polity],aim),"Published branches do not share editable ambition records");
        var recognized=s.Copy();var rc=recognized.History.Aggregates.Empires.Connections;
        int partner=Array.FindIndex(h.Polities,p=>p.EndedYear<0&&p.Id!=polity);
        // Isolate a funded inland capital from its optional provincial offices.
        // This is a mechanism probe, not a checkpoint passed to full validation.
        recognized.History.Aggregates.Empires.Governments[polity].AdministrativePosts=new HistoricalLocalBag();
        rc.TitleAt=new HistoricalCellTree(engine.Patches.Length);rc.Titles=Array.Empty<HistoricalRecognizedTitle>();foreach(var g in rc.Governments)g.Titles=new HistoricalLocalBag();
        rc.Cultures[h.Polities[partner].CultureId].Charts=new[]{new HistoricalCoastChart{Region=region,SourceCulture=h.Polities[partner].CultureId,Year=s.Years}};
        recognized.History.Polities[polity].Treasury=10000;recognized.History.Polities[polity].FoundedYear=Math.Max(0,s.Years-30);
        long population=recognized.History.Aggregates.TotalPopulation;
        var owners=cells.Select(cell=>HistoricalCells.Polity(recognized,cell)).ToArray();
        Call("TryRecognizeRegionalTitle",recognized,polity,partner);
        check(rc.Titles.Length==1&&rc.Titles[0].AreaKm2==rc.RegionAreas[region]&&cells.All(cell=>HistoricalRecognizedClaims.Holder(recognized,cell)==polity),"Bilateral recognition covers a whole inland ecoregional district, without requiring a maritime charter");
        check(population==recognized.History.Aggregates.TotalPopulation&&owners.SequenceEqual(cells.Select(cell=>HistoricalCells.Polity(recognized,cell))),"Regional recognition does not occupy cells or replace their inhabitants");
        check(probe.History.Aggregates.Empires.Connections.Titles.Length==c.Titles.Length&&HistoricalRecognizedClaims.Holder(probe,far)==HistoricalRecognizedClaims.Holder(s,far)&&HistoricalCells.Polity(probe,far)==HistoricalCells.Polity(s,far),"An ambition alone never grants a recognized title or changes distant control");
        var doc=HistoryAtlas.Read(s,"state",polity);
        check(doc.Sections.Any(section=>section.Title=="Territorial ambitions"),"State profiles distinguish ambitions from recognized territorial titles");
        var next=engine.AdvanceYears(s,3);engine.Validate(next);var split=engine.AdvanceYears(engine.AdvanceYears(s,1),2);
        check(next.Hash==split.Hash,"Regional intentions and actions are publication-partition deterministic");
        world.Prehistory=next;var loaded=WorldFile.Read(WorldFile.Write(world));
        check(loaded.Prehistory.Hash==next.Hash&&new Prehistory(loaded).AdvanceYears(loaded.Prehistory,2).Hash==engine.AdvanceYears(next,2).Hash,"Ambitions and region titles persist and resume deterministically");
        var broken=s.Copy();broken.History.Aggregates.Empires.Connections.Ambitions.Governments[polity].AnchorCell=-1;bool rejected=false;
        try{engine.Validate(broken);}catch(ArgumentException){rejected=true;}
        check(rejected,"Integrity validation rejects malformed geographic ambitions");
        check(source.ComputeHash()==before,"All probes preserve the original legacy checkpoint");
    }
}
