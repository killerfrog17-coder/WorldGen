using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryGeographyReview
{
    public static void Run(string[] args)
    {
        int at=Array.IndexOf(args,"--history-geography-review");
        var world=WorldFile.Read(File.ReadAllBytes(args[at+1]));var state=world.Prehistory;
        var engine=new Prehistory(world);engine.Validate(state);
        var regions=engine.Patches.Select((p,id)=>new {Id=id,p.Cell,p.Capacity,p.Island,Population=state.Population[id],Arrival=state.ArrivalYear[id],
            Latitude=Math.Asin(world.Surface.Cells[p.Cell].Position.Z)*180/Math.PI,
            Longitude=Math.Atan2(world.Surface.Cells[p.Cell].Position.Y,world.Surface.Cells[p.Cell].Position.X)*180/Math.PI,
            p.Lifestyle,CellCount=p.Cells.Length,AreaKm2=p.Cells.Sum(c=>world.Surface.Cells[c].AreaM2/1e6),
            Culture=state.History.Aggregates.Regions[id].Culture,Settled=state.History.Aggregates.Living.Regions[id].SettledPopulation});
        var origins=state.History.Archive.Where(e=>e.Kind=="independent-institutions"&&e.Cell>=0&&e.Cell<world.Surface.Cells.Length).Select(e=>new{
            e.Year,e.CultureId,e.PolityId,e.Cell,e.Text,
            Latitude=Math.Asin(world.Surface.Cells[e.Cell].Position.Z)*180/Math.PI,
            Longitude=Math.Atan2(world.Surface.Cells[e.Cell].Position.Y,world.Surface.Cells[e.Cell].Position.X)*180/Math.PI,
            Climate=world.Climate.Classification[e.Cell].ToString(),Biome=world.Ecology.Habitat[e.Cell].Biome.ToString(),
            world.Ecology.Habitat[e.Cell].GrowingDays,world.Ecology.Habitat[e.Cell].SoilFertilityPermille,
            world.Ecology.Habitat[e.Cell].AridityMilli,world.Ecology.Habitat[e.Cell].FloodFractionPermille,
            world.Ecology.Habitat[e.Cell].ColdTenthsC,world.Ecology.Habitat[e.Cell].WarmTenthsC});
        var report=new {Year=state.Years,Cells=world.Surface.Cells.Length,Origin=state.Origin,Origins=origins,Regions=regions,Routes=engine.Routes.Select(r=>new{r.A,r.B,r.DistanceM,r.Sea,r.Shallow,r.SeaDropM,r.CellA,r.CellB})};
        File.WriteAllText(args[at+2],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Wrote explicit geography diagnostic; no simulation was advanced.");
    }
}
