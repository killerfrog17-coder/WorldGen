using System.Globalization;
using System.Text;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public static class HistoryRegionalReview
{
    public static void Run(string source,string folder)
    {
        Directory.CreateDirectory(folder);var world=WorldFile.Read(File.ReadAllBytes(source));var state=world.Prehistory;var engine=new Prehistory(world);engine.Validate(state);string hash=state.Hash;
        string Region(int cell)
        {
            if(world.Recipe.TerrainSource!="Earth")return "Landmass "+world.Landmass[cell];
            var v=world.Surface.Cells[cell].Position;double lat=Math.Asin(v.Z)*180/Math.PI,lon=Math.Atan2(v.Y,v.X)*180/Math.PI;
            // Observational labels only. Never consumed by a historical rule.
            if(lon>=-170&&lon<-35&&lat>-60&&lat<75)return "Americas";
            // Avoid counting the Arabian Peninsula as Africa or India as East Asia.
            // These remain broad observational boxes, not measured country masks.
            if(lon>=-20&&lon<55&&lat>=-40&&lat<35&&(lon<35||lat<15))return "Africa";
            if(lon>=30&&lon<70&&lat>=15&&lat<45)return "West Asia";
            if(lon>=60&&lon<95&&lat>=0&&lat<35)return "South Asia";
            if(lon>=-15&&lon<50&&lat>=35&&lat<75)return "Europe";
            if(lon>=95&&lon<145&&lat>=10&&lat<55)return "East Asia";
            return "Other";
        }
        var patches=new StringBuilder("patch,region,arrival,residents,capacity,knowledge,density,storage,nativeCrops,exposure,source,originatingCultures\n");
        var groups=new SortedDictionary<string,List<int>>(StringComparer.Ordinal);
        for(int p=0;p<engine.Patches.Length;p++)
        {
            var patch=engine.Patches[p];var site=patch.Cells.Select(c=>engine.AssessHistoricalSite(state,c)).OrderByDescending(x=>x.StorageOpportunity).ThenBy(x=>x.Cell).First();
            string region=Region(site.Cell);if(!groups.TryGetValue(region,out var list))groups.Add(region,list=new List<int>());list.Add(p);
            patches.AppendLine(FormattableString.Invariant($"{p},{region},{state.ArrivalYear[p]},{state.Population[p]},{patch.Capacity},{state.Society.Knowledge[p]},{site.Density:F6},{site.StorageOpportunity:F6},{site.NativeCropMask},{state.History.RegionalLearning?.FarmingExposure[p]??0},{state.History.RegionalLearning?.FarmingSource[p]??-1},{state.Society.Cultures.Count(c=>engine.CellPatch[c.OriginCell]==p)}"));
        }
        File.WriteAllText(Path.Combine(folder,"regional-patches.csv"),patches.ToString());
        var towns=new StringBuilder("town,culture,region,year,residents,foodCapacity,reserveYears,storedSupport,fiscalPotential,readiness,practice,administrativeOpportunity,institutionalExamples,polity\n");
        foreach(var t in state.Society.Settlements)
        {
            var site=engine.AssessHistoricalFoundation(state,t.Id);
            towns.AppendLine(FormattableString.Invariant($"{t.Id},{t.CultureId},{Region(t.Cell)},{t.FoundedYear},{t.Population},{t.FoodCapacity},{t.FoodReserve/(double)Math.Max(1,t.Population):F4},{site.StoredSupport:F6},{site.FiscalPotential:F6},{site.Readiness:F6},{site.Preparation},{site.AdministrativeOpportunity:F6},{site.InstitutionalExamples},{t.PolityId}"));
        }
        File.WriteAllText(Path.Combine(folder,"formation-cohorts.csv"),towns.ToString());
        var cropRows=new StringBuilder("culture,region,origin,crop,originNative,observedNative,wildCell,practiceCell,practice,adoption,source\n");
        foreach(var c in state.Society.Cultures)
        {
            var a=state.History.Agriculture[c.Id];int original=engine.AssessHistoricalSite(state,c.OriginCell).NativeCropMask;
            for(int k=0;k<6;k++)cropRows.AppendLine($"{c.Id},{Region(c.OriginCell)},{c.OriginCell},{k},{(original&(1<<k))!=0},{(a.NativeMask&(1<<k))!=0},{a.WildOriginCell?[k]??-1},{a.PracticeCell?[k]??c.OriginCell},{a.Practice[k]},{a.AdoptedYear[k]},{a.SourceCulture[k]}");
        }
        File.WriteAllText(Path.Combine(folder,"culture-crops.csv"),cropRows.ToString());
        var report=new StringBuilder($"Historical cohort review — {state.History.Version}, year {state.Years}\n");
        report.AppendLine("Labels describe sampled locations, not production masks or real historical states. Town residents include villages. First-primary excludes inherited/seceding courts but can include later restoration of an unruled center.");
        foreach(var group in groups)
        {
            var occupied=group.Value.Where(p=>state.Population[p]>0).ToArray();
            var cultures=state.Society.Cultures.Where(c=>Region(c.OriginCell)==group.Key).ToArray();
            var sites=state.Society.Settlements.Where(t=>t.Population>=100&&Region(t.Cell)==group.Key).ToArray();
            var courts=state.History.Polities.Where(p=>Region(state.Society.Settlements[p.CapitalId].Cell)==group.Key).ToArray();
            report.AppendLine(FormattableString.Invariant($"{group.Key}: {occupied.Length}/{group.Value.Count} occupied patches; rural {occupied.Sum(p=>state.Population[p])}; knowledge {(occupied.Length==0?0:occupied.Average(p=>state.Society.Knowledge[p])):F1}; {cultures.Length} cultures/{cultures.Count(c=>c.Farming>=150)} established farming; {sites.Length} named sites/{sites.Sum(t=>t.Population)} residents; {courts.Count(p=>p.EndedYear<0)} active courts; first primary {courts.Where(p=>p.ParentId<0).Select(p=>p.FoundedYear).DefaultIfEmpty(-1).Min()}"));
        }
        if(state.ComputeHash()!=hash)throw new Exception("Cohort diagnostics mutated historical state");
        File.WriteAllText(Path.Combine(folder,"cohort-review.txt"),report.ToString());Console.WriteLine(report);
    }
}
