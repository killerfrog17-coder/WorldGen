using System.Diagnostics;
using System.Text.Json;
using WorldGen.Core;

static class PaleolithicDispersalAudit
{
    static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    public static void Run(string output,int seed,int detail=200000,string preset="Continents",int end=190000)
    {
        Directory.CreateDirectory(output);var recipe=new Recipe{Seed=seed};WorldPresets.Apply(preset,recipe);recipe.Detail=detail;
        var watch=Stopwatch.StartNew();var world=new WorldPipeline().Generate(recipe);double generation=watch.Elapsed.TotalSeconds;
        var engine=new Prehistory(world);var initial=engine.StartForagers(engine.PrepareForagers());var session=new SimulationSession(engine,initial);
        File.WriteAllText(Path.Combine(output,"input.json"),JsonSerializer.Serialize(new{seed,detail,preset,end,generation,Cells=world.Surface.Cells.Length,Regions=initial.Foragers.Districts.Length,InitialSpecies=initial.Foragers.Lineages.Select(l=>new{l.Id,l.ScientificName,l.OriginDistrict,l.PlayerSpecies})},Json));
        // One reusable geographic raster; snapshots contain regional summaries,
        // never realised tribes, individual NPCs or full world archives.
        const int width=1024,height=512;var locator=new CellLocator(world.Surface.Cells);
        using(var file=File.Create(Path.Combine(output,"regions.i32")))using(var writer=new BinaryWriter(file))
        using(var map=File.Create(Path.Combine(output,"land.rgb")))
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            double lat=(.5-(y+.5)/height)*Math.PI,lon=((x+.5)/width*2-1)*Math.PI;
            int cell=locator.Nearest(new V(Math.Cos(lat)*Math.Cos(lon),Math.Cos(lat)*Math.Sin(lon),Math.Sin(lat)));
            writer.Write(initial.Foragers.CellDistrict[cell]);bool water=world.Hydrology.OceanConnected[cell];
            map.WriteByte(water?(byte)29:(byte)105);map.WriteByte(water?(byte)58:(byte)116);map.WriteByte(water?(byte)81:(byte)92);
        }
        var summaries=new List<object>();
        foreach(int year in new[]{0,10000,25000,50000,100000,150000,190000}.Where(y=>y<=end).Append(end).Distinct().OrderBy(y=>y))
        {
            var state=year==0?initial:session.AdvanceSeconds(year*SimulationTime.Year-session.Seconds,int.MaxValue);engine.ValidateForagers(state);
            if(state.Years!=year)throw new Exception("Audit did not reach requested year");var f=state.Foragers;
            if(f.Cohorts.Length>f.Districts.Length)throw new Exception("Population simulation exceeded regional aggregation");
            var rows=f.Cohorts.Select(c=>
            {
                var d=f.Districts[c.District];var p=ForagerAdoption.Project(f,c);double population=ForagerDemography.Present(f,c);
                var environment=ForagerBiota.Apply(ForagerEcology.At(seed,d,year,world.ElevationM[d.Cell]),f.Biota[d.Id]);
                var life=ForagerEcology.Livelihood(d,environment,f.Lineages[c.Lineage],p,f.Cultures[c.Culture],f.Resources[d.Id],true);
                return new{c.District,c.FoundedYear,d.Realm,d.AreaKm2,d.RadiusKm,Longitude=Math.Atan2(d.Center.Y,d.Center.X)*180/Math.PI,Latitude=Math.Asin(d.Center.Z)*180/Math.PI,
                    Population=population,Sapiens=population*f.SpeciesShare(c,0),Share=f.SpeciesShare(c,0),c.Occupation,c.FrontierKm,environment.WinterC,environment.SummerC,environment.Aridity,environment.Water,environment.IceCover,
                    life.Capacity,life.WinterSecurity,Wraps=ForagerAdoption.Access(p,8),Fitted=ForagerAdoption.Access(p,13),Insulated=ForagerAdoption.Access(p,14),Rafts=ForagerAdoption.Access(p,20),Boats=ForagerAdoption.Access(p,30),
                    Knowledge=p.Knowledge,Access=Enumerable.Range(0,ForagerTechnology.Count).Select(k=>ForagerAdoption.Access(p,k)).ToArray(),Use=p.Use};
            }).ToArray();
            var occupied=rows.Where(r=>r.Population>=1).ToArray();var sapiens=rows.Where(r=>r.Sapiens>=10).ToArray();
            var summary=new{Year=year,Population=rows.Sum(r=>r.Population),Sapiens=rows.Sum(r=>r.Sapiens),Occupied=occupied.Length,SapiensRegions=sapiens.Length,
                SapiensRealms=sapiens.Select(r=>r.Realm).Distinct().Count(),ColdSapiens=sapiens.Where(r=>r.WinterC< -15).Sum(r=>r.Sapiens),IceSapiens=sapiens.Where(r=>r.IceCover>.86).Sum(r=>r.Sapiens),
                MaxLatitude=sapiens.Select(r=>Math.Abs(r.Latitude)).DefaultIfEmpty(0).Max(),Elapsed=watch.Elapsed.TotalSeconds};
            summaries.Add(summary);File.WriteAllText(Path.Combine(output,$"year-{year}.json"),JsonSerializer.Serialize(rows,Json));
            File.WriteAllText(Path.Combine(output,"summary.json"),JsonSerializer.Serialize(summaries,Json));Console.WriteLine(JsonSerializer.Serialize(summary));
        }
    }
}
