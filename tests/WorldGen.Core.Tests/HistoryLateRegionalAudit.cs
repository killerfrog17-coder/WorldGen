using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Deliberate OFFLINE observer census. Never referenced by production events,
// presentation or authoritative simulation. Geographic boxes are Earth diagnostics,
// not a rule declaring where a fictional civilization should arise.
public static class HistoryLateRegionalAudit
{
    sealed class Cell
    {
        public int Id,Region,Culture,Polity,Teacher,Exposure,CivicExposure,NativeMask;
        public long Rural;
        public double Latitude,Longitude,Area,Environment,Yield,Storage;
    }
    public static void Run(string path,string folder)
    {
        Directory.CreateDirectory(folder);var world=WorldFile.Read(File.ReadAllBytes(path));
        var engine=new Prehistory(world);string before=world.Prehistory.ComputeHash();
        var s=world.Prehistory.Copy();engine.Validate(s);var a=s.History.Aggregates;var h=s.History;
        var methods=typeof(Prehistory).GetMethods(BindingFlags.NonPublic|BindingFlags.Instance).GroupBy(m=>m.Name).ToDictionary(g=>g.Key,g=>g.First());
        object Call(string name,params object[] args)=>methods[name].Invoke(engine,args)!;
        double Latitude(int cell)=>Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI;
        double Longitude(int cell)=>Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI;
        var boxes=new[]{("India",8d,33d,68d,92d),("EastAsia",20d,44d,102d,125d),("Europe",36d,56d,-10d,32d),("NortheastAmerica",29d,47d,-96d,-66d)};
        var reports=new List<object>();
        foreach(var box in boxes)
        {
            var cells=new List<Cell>();
            for(int cell=0;cell<engine.CellPatch.Length;cell++)
            {
                int region=engine.CellPatch[cell];if(region<0||world.Hydrology.OceanConnected[cell])continue;
                double lat=Latitude(cell),lon=Longitude(cell);if(lat<box.Item2||lat>box.Item3||lon<box.Item4||lon>box.Item5)continue;
                int culture=HistoricalCells.Culture(s,cell);var local=a.Living.Regions[region];
                cells.Add(new Cell{Id=cell,Region=region,Culture=culture,Polity=HistoricalCells.Polity(s,cell),Teacher=local.Teacher,Exposure=local.Exposure,CivicExposure=local.CivicExposure,
                    Rural=(long)Call("AggregateRuralCellPopulation",s,cell),Latitude=lat,Longitude=lon,Area=world.Surface.Cells[cell].AreaM2/1e6,
                    Environment=(double)Call("EnvironmentalEstablishment",s,cell,culture),NativeMask=(int)Call("CommunityNativeCropMask",s,cell),
                    Yield=culture<0?0:(double)Call("LivingCropLandscape",s,culture,cell,false),Storage=culture<0?0:(double)Call("LivingCropLandscape",s,culture,cell,true)});
            }
            var towns=s.Society.Settlements.Where(t=>a.Living.Cities[t.Id].AbandonedYear<0&&Latitude(t.Cell)>=box.Item2&&Latitude(t.Cell)<=box.Item3&&Longitude(t.Cell)>=box.Item4&&Longitude(t.Cell)<=box.Item5).ToArray();
            object Culture(int id)
            {
                var c=s.Society.Cultures[id];var hc=h.Cultures[id];var ag=h.Agriculture[id];var actual=cells.Where(x=>x.Culture==id).ToArray();
                return new{Id=id,c.Name,c.FoundedYear,c.Farming,c.Fishing,c.Population,OriginCell=c.OriginCell,OriginLatitude=Latitude(c.OriginCell),OriginLongitude=Longitude(c.OriginCell),ag.NativeMask,RegionalBestCell=a.Regions[a.Cultures[id].Region].BestCell,RegionCells=actual.Length,RegionRuralEstimate=actual.Sum(x=>x.Rural),RegionUrban=towns.Where(t=>t.CultureId==id).Sum(t=>t.Population),
                    Contacts=c.Contacts,Exposure=actual.Select(x=>x.Exposure).DefaultIfEmpty(0).Max(),CivicExposure=actual.Select(x=>x.CivicExposure).DefaultIfEmpty(0).Max(),
                    CropGroups=Enumerable.Range(0,6).Where(k=>ag.AdoptedYear[k]>0).Select(k=>new{Group=HistoricalCrops.Names[k],Year=ag.AdoptedYear[k],Source=ag.SourceCulture[k],Husbandry=ag.HusbandryYears?[k]??0,Crop=ag.Cultivars?[k]?.CropId??-1}),
                    CropPractice=Enumerable.Range(0,6).Select(k=>new{Group=HistoricalCrops.Names[k],Practice=ag.Practice[k],Cell=ag.PracticeCell[k],WildOrigin=ag.WildOriginCell[k],WildFit=(double)Call("CommunityWildCropFit",s,ag.PracticeCell[k],k)}),
                    YieldMean=actual.Length==0?0:actual.Average(x=>x.Yield),YieldMax=actual.Select(x=>x.Yield).DefaultIfEmpty(0).Max(),StorageMax=actual.Select(x=>x.Storage).DefaultIfEmpty(0).Max(),
                    LocalTownCandidates=actual.Count(x=>(bool)Call("CanFoundLivingTown",s,id,x.Id,-1)),WorldwideActiveTowns=a.Cultures[id].Cities.Count,
                    Tech=new[]{HistoricalTechnology.Storage,HistoricalTechnology.Sailing,HistoricalTechnology.Navigation,HistoricalTechnology.OceanNavigation,HistoricalTechnology.Writing,HistoricalTechnology.Roads,HistoricalTechnology.Railways,HistoricalTechnology.MechanizedAgriculture}.Select(k=>new{Id=k.ToString(),Level=hc.Technical.Levels[(int)k],Adoption=HistoricalModernEffects.Adoption(hc,k,s.Years)})};
            }
            object Site(Cell x)
            {
                var habitat=world.Ecology.Habitat[x.Id];var region=a.Regions[x.Region];
                var nearest=s.Society.Settlements.Where(t=>a.Living.Cities[t.Id].AbandonedYear<0).Select(t=>(t,D.Angle(world.Surface.Cells[x.Id].Position,world.Surface.Cells[t.Cell].Position)*world.Recipe.RadiusKm)).OrderBy(v=>v.Item2).Take(3).ToArray();
                return new{x.Id,x.Region,x.Culture,x.Polity,x.Rural,x.Latitude,x.Longitude,x.Environment,x.NativeMask,x.Yield,x.Storage,x.Teacher,x.Exposure,x.CivicExposure,
                    Climate=world.Climate.Classification[x.Id].ToString(),Biome=habitat.Biome.ToString(),habitat.GrowingDays,habitat.ReliefM,habitat.SoilFertilityPermille,
                    RegionUnallocated=s.Population[x.Region],RegionSettled=a.Living.Regions[x.Region].SettledPopulation,region.BestCell,SelectedCulture=region.Culture,
                    FirstTownAllowed=x.Culture>=0&&(bool)Call("CanFoundLivingTown",s,x.Culture,x.Id,-1),
                    Nearest=nearest.Select(v=>new{v.t.Id,v.t.Name,v.t.CultureId,v.t.Population,Km=v.Item2,LocalYield=(double)Call("LivingCropLandscape",s,v.t.CultureId,x.Id,false),
                        RouteAllowed=(bool)Call("InstitutionalRouteAllowed",s,v.t.Cell,x.Id,v.t.CultureId),Transmission=(double)Call("EnvironmentalTransmission",s,v.t.Cell,x.Id,v.t.CultureId,false)})};
            }
            reports.Add(new{Name=box.Item1,Cells=cells.Count,InhabitedCells=cells.Count(x=>x.Rural>0),NamedCultureCells=cells.Count(x=>x.Culture>=0),StateCells=cells.Count(x=>x.Polity>=0),
                RuralDownscaledEstimate=cells.Sum(x=>x.Rural),Urban=towns.Sum(t=>t.Population),ActiveTowns=towns.Length,
                Habitats=cells.GroupBy(x=>world.Ecology.Habitat[x.Id].Biome.ToString()).Select(g=>new{Biome=g.Key,Cells=g.Count(),Rural=g.Sum(x=>x.Rural),EnvironmentMean=g.Average(x=>x.Environment),CropMaskCells=g.Count(x=>x.NativeMask!=0)}),
                Regions=cells.Select(x=>x.Region).Distinct().Select(id=>new{Id=id,a.Regions[id].Culture,a.Regions[id].BestCell,Unallocated=s.Population[id],Settled=a.Living.Regions[id].SettledPopulation,a.Living.Regions[id].Teacher,a.Living.Regions[id].Exposure,a.Living.Regions[id].CivicExposure}),
                Cultures=cells.Where(x=>x.Culture>=0).GroupBy(x=>x.Culture).OrderByDescending(g=>g.Sum(x=>x.Rural)).Take(12).Select(g=>Culture(g.Key)).ToArray(),
                BestUnurbanizedSites=cells.Where(x=>a.TownAt[x.Id]<0).OrderByDescending(x=>x.Environment).Take(6).Concat(cells.Where(x=>a.TownAt[x.Id]<0).OrderByDescending(x=>x.Rural).Take(6)).Distinct().Select(Site).ToArray()});
        }
        bool preserved=before==world.Prehistory.ComputeHash();
        File.WriteAllText(Path.Combine(folder,"regional-audit.json"),JsonSerializer.Serialize(new{Year=s.Years,Version=h.Version,InputPreserved=preserved,Note="Offline Earth boxes; rural figures are existing map downscaling proxies, not new authoritative population accounts.",Regions=reports},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine("Late regional audit complete; input preserved="+preserved);
    }
}
