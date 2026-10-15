using WorldGen.Core;
using WorldGen.Serialization;
public static class OceanReview
{
    public static void Run(string[] args)
    {
        int at=Array.IndexOf(args,"--ocean-review");var w=WorldFile.Read(File.ReadAllBytes(args[at+1]));var e=new Prehistory(w);var s=w.Prehistory;
        int Landmass(int cell)=>w.Hydrology.OceanConnected[cell]?w.Surface.Cells.Length+cell:w.Landmass[cell];
        var coast=Enumerable.Range(0,w.Surface.Cells.Length).Where(i=>e.CellPatch[i]>=0&&e.Patches[e.CellPatch[i]].Capacity>=25&&(w.Hydrology.OceanConnected[i]||w.Surface.Cells[i].Neighbors.Any(j=>w.Hydrology.OceanConnected[j]))).ToArray();
        var rows=new List<string>{"culture,cell,landmass,patchResidents,fishing,storage,navigationYear,remoteRoutes,nearestKm,nearestForeignCoastKm,foreignLandmass"};
        foreach(int cell in coast.Where(i=>s.Society.CellCulture[i]>=0))
        {
            int id=s.Society.CellCulture[cell];var routes=e.AssessRemoteOceanRoutes(cell);int other=coast.Where(i=>Landmass(i)!=Landmass(cell)).OrderByDescending(i=>V.Dot(w.Surface.Cells[i].Position,w.Surface.Cells[cell].Position)).ThenBy(i=>i).DefaultIfEmpty(-1).First();
            double nearest=other<0?-1:D.Angle(w.Surface.Cells[other].Position,w.Surface.Cells[cell].Position)*w.Recipe.RadiusKm;
            if(args.Contains("--cell")&&cell==int.Parse(args[Array.IndexOf(args,"--cell")+1])&&other>=0)
            {
                Console.WriteLine($"Closest other eligible coast: {cell} [landmass {Landmass(cell)}] -> {other} [landmass {Landmass(other)}], {nearest:F1} km; returned routes {routes.Length}");
                var a=w.Surface.Cells[cell].Position;var b=w.Surface.Cells[other].Position;var locator=new CellLocator(w.Surface.Cells);int steps=Math.Max(12,(int)Math.Ceiling(nearest/80));
                for(int k=1;k<steps;k++){int sample=locator.Nearest((a*(1-k/(double)steps)+b*(k/(double)steps)).Unit);Console.WriteLine($"  {k}/{steps}: cell {sample}, ocean={w.Hydrology.OceanConnected[sample]}, landmass={Landmass(sample)}");}
            }
            rows.Add(FormattableString.Invariant($"{id},{cell},{Landmass(cell)},{s.Population[e.CellPatch[cell]]},{s.Society.Cultures[id].Fishing},{e.HistoricalStorageOpportunity(cell):F3},{s.History.Cultures[id].Technology[13]-1},{routes.Length},{routes.Select(r=>r.DistanceM/1000).DefaultIfEmpty(-1).Min()},{nearest:F3},{(other<0?-1:Landmass(other))}"));
        }
        File.WriteAllLines(args[at+2],rows);Console.WriteLine($"Ocean staging areas reviewed {rows.Count-1}; radius {w.Recipe.RadiusKm}; {coast.Length} eligible coast nodes, {coast.Select(Landmass).Distinct().Count()} landmasses");
    }
}
