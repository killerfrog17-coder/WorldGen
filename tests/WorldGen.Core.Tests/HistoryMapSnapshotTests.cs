using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public static class HistoryMapSnapshotTests
{
    public static void Run(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var s=world.Prehistory;var engine=new Prehistory(world);string hash=s.Hash;
        var display=HistoricalMapSnapshot.For(s);int count=world.Surface.Cells.Length;
        check(ReferenceEquals(display,HistoricalMapSnapshot.For(s)),"A published checkpoint shares one projection across views and borders");
        check(Enumerable.Range(0,count).All(i=>display.Boundary(i,HistoryOverlay.Cultures)==display.Culture(i)&&display.Boundary(i,HistoryOverlay.Religions)==display.Religion(i)),"Local cultural and religious borders are not replaced by state or recognized-title borders");
        check(Enumerable.Range(0,count).All(i=>display.Culture(i)==HistoricalCells.Culture(s,i)&&display.Polity(i)==HistoricalCells.Polity(s,i)&&display.Stage(i)==HistoricalCells.Stage(s,i)),"Bulk display projection matches exact local culture, ownership succession and stage lookups");
        foreach(var mode in Enum.GetValues<HistoryOverlay>())
        {
            check(Enumerable.Range(0,count).All(i=>display.OverlayId(i,mode)==SocietyView.OverlayId(s,i,mode)&&display.ColorId(i,mode)==SocietyView.OverlayColorId(s,i,mode)),mode+" retains literal selection and ruling/local color identities");
            foreach(int cell in new[]{-1,engine.Patches[s.Origin].Cell})
                check(PrehistoryView.Codes(world,engine,s,cell,mode).SequenceEqual(PrehistoryView.Codes(world,engine,s,cell,mode,optimized:false)),mode+" occupancy/selection codes match the literal renderer");
        }
        var export=new int[s.History.Aggregates.Cells.Length+6];Array.Fill(export,8123);s.History.Aggregates.Cells.CopyTo(export,3);
        var technicalLegend=SocietyView.Legend(s,HistoryOverlay.Technology);
        check(technicalLegend.All(e=>display.OverlayId(e.Cell,HistoryOverlay.Technology)==e.ColorId),"Technology labels select cells that actually display that knowledge group, not a displaced ancestral homeland");
        check(technicalLegend.All(e=>SocietyView.LegendColor(e).Equals(SocietyView.OverlayColor(s,e.ColorId,HistoryOverlay.Technology))),"Technology legend and overlay share their distinct observer-era colors");
        if(s.History.Aggregates.Empires!=null)check(Enumerable.Range(2,9).Select(i=>SocietyView.LegendColorHex(new HistoryLegendEntry{EraColor=true,ColorId=i})).Distinct().Count()==9,"All nine observer eras have different categorical colors");
        check(export[0]==8123&&export[^1]==8123&&Enumerable.Range(0,s.History.Aggregates.Cells.Length).All(i=>export[i+3]==s.History.Aggregates.Cells[i]),"Bulk tree export preserves absent values, bounds and offsets");
        check(s.ComputeHash()==hash,"Display caches do not edit, validate away or rehash the authoritative history");
    }
}
