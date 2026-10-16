using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;
public static class HistoryOriginSuitabilityAudit
{
    static object Call(Prehistory engine,string method,params object[] values)=>typeof(Prehistory).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,values);
    sealed class Site
    {
        public int Region,Cell,Population,NativeMask,GrowingDays,Permafrost,Soil;
        public double Latitude,MeanC,ColdC,WarmC,Aridity,Freshwater,Environment,WildResources,Homeland,Score,ChancePer100Years,Maturity,MarineFoodSupport;
        public string Climate="",Biome="";
        public bool Coastal,SelectedRegionalSite;
    }
    public static void Run(string path,string folder)
    {
        Directory.CreateDirectory(folder);var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var state=world.Prehistory.Copy();engine.Validate(state);string before=state.ComputeHash();
        var regions=state.History.Aggregates.Regions;
        var river=(double[])typeof(Prehistory).GetField("riverAccess",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(engine)!;
        Site Read(int cell,bool candidate)
        {
            int region=engine.CellPatch[cell];var h=world.Ecology.Habitat[cell];
            double environment=(double)Call(engine,"EnvironmentalEstablishment",state,cell,-1),wild=(double)Call(engine,"EnvironmentalWildResources",state,cell),homeland=(double)Call(engine,"HomelandOriginFactor",state,cell);
            int actualSite=regions[region].BestCell;double chance=0;
            if(regions[region].Culture<0&&state.Population[region]>=400&&state.ArrivalYear[region]>=0)
            {regions[region].BestCell=cell;try{chance=(double)Call(engine,"LivingCommunityChance",state,region,100);}finally{regions[region].BestCell=actualSite;}}
            return new Site{Region=region,Cell=cell,Population=(int)Math.Min(int.MaxValue,state.Population[region]),NativeMask=(int)Call(engine,"CommunityNativeCropMask",state,cell),GrowingDays=h.GrowingDays,Permafrost=h.PermafrostPermille,Soil=h.SoilFertilityPermille,
                Latitude=Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI,MeanC=h.MeanTenthsC/10.0,ColdC=h.ColdTenthsC/10.0,WarmC=h.WarmTenthsC/10.0,Aridity=h.AridityMilli/1000.0,Freshwater=river[cell],Environment=environment,WildResources=wild,Homeland=homeland,Score=Math.Pow(environment,.75)*wild*homeland,
                ChancePer100Years=chance,MarineFoodSupport=(double)Call(engine,"MarineForagerSupport",state,cell),Maturity=(double)Call(engine,"CulturalRecognitionMaturity",state,region),Climate=world.Climate.Classification[cell].ToString(),Biome=h.Biome.ToString(),Coastal=world.Surface.Cells[cell].Neighbors.Any(n=>world.Hydrology.OceanConnected[n]),SelectedRegionalSite=actualSite==cell};
        }
        // These are explicit offline diagnostics, not a simulation event route.
        var selected=Enumerable.Range(0,regions.Length).Where(r=>regions[r].Culture<0&&state.Population[r]>=400&&state.ArrivalYear[r]>=0).Select(r=>Read(regions[r].BestCell,true)).ToArray();
        var cells=Enumerable.Range(0,world.Surface.Cells.Length).Where(c=>engine.CellPatch[c]>=0&&!world.Hydrology.OceanConnected[c]).Select(c=>Read(c,false)).ToArray();
        double annual=selected.Sum(x=>x.ChancePer100Years<=0?0:-Math.Log(1-x.ChancePer100Years)/100);
        double potentialAnnual=cells.GroupBy(x=>x.Region).Select(g=>g.Max(x=>x.ChancePer100Years)).Sum(chance=>chance<=0?0:-Math.Log(1-chance)/100);
        var options=new JsonSerializerOptions{WriteIndented=true,IncludeFields=true};
        var summary=new{Year=state.Years,Pack=world.Ecology.PackVersion,MarineTaxa=world.Ecology.Taxa.Where(t=>t.ScientificName.StartsWith("Coastal",StringComparison.Ordinal)).Select(t=>new{t.Id,t.Name,t.ScientificName,Cells=cells.Count(c=>Biogeography.Present(world.Ecology,c.Cell,t.Id))}),MarineSites=cells.Count(x=>x.MarineFoodSupport>0),BestMarineSites=cells.OrderByDescending(x=>x.MarineFoodSupport).Take(12),Population=state.History.Aggregates.TotalPopulation,OccupiedRegions=state.Population.Count(p=>p>0),EligibleRegions=selected.Length,
            PositiveCultureHazards=selected.Count(x=>x.ChancePer100Years>0),AnnualHazard=annual,InstantaneousExpectedWaitingYears=annual>0?(double?)(1/annual):null,
            PotentialAnnualHazardUsingBestSitePerRegion=potentialAnnual,PotentialExpectedWaitingYears=potentialAnnual>0?(double?)(1/potentialAnnual):null,
            Origin=Read(engine.Patches[state.Origin].Cell,false),BestSelected=selected.OrderByDescending(x=>x.ChancePer100Years).Take(20),BestOccupiedAlternatives=cells.Where(x=>x.Population>=400).OrderByDescending(x=>x.ChancePer100Years).Take(20),BestWorld=cells.OrderByDescending(x=>x.Score).Take(20),
            Organizations=state.History.International?.Organizations.Select(o=>new{o.Id,o.Name,Kind=o.Kind.ToString(),Members=o.Members.Count,o.FoundedYear,o.EndedYear,o.Reserve,o.Contributions,o.ServiceSpending,o.MediationSpending,AnnualAssessments=state.History.International.Finances?.Organizations[o.Id].AnnualAssessments,UnfundedWork=state.History.International.Finances?.Organizations[o.Id].UnfundedWork}),
            Cultures=state.Society.Cultures.Select(c=>new{c.Id,c.Name,c.FoundedYear,c.Fishing,c.Hunting,c.Gathering,c.Farming,c.Resources,c.Subsistence,Origin=Read(c.OriginCell,false)}),
            Habitat= cells.GroupBy(x=>x.Biome).Select(g=>new{Biome=g.Key,Cells=g.Count(),InhabitedCells=g.Count(x=>x.Population>0),Warmest=g.Max(x=>x.MeanC),LongestSeason=g.Max(x=>x.GrowingDays)}),
            Appearance=selected.Take(8).Select(x=>{var appearance=engine.RegionalAppearance(state,x.Region);return new{appearance.Region,appearance.Available,appearance.SkinPigmentation,appearance.HairPigmentation,appearance.IrisPigmentation,appearance.HairCurl,appearance.BodyCompactness,appearance.Basis};}),InputPreserved=before==state.ComputeHash()};
        File.WriteAllText(Path.Combine(folder,"origin-audit.json"),JsonSerializer.Serialize(summary,options));
        File.WriteAllText(Path.Combine(folder,"region-hazards.csv"),"region,cell,population,latitude,climate,biome,growingDays,meanC,environment,wild,homeland,maturity,chancePer100Years\n"+string.Join("\n",selected.Select(x=>FormattableString.Invariant($"{x.Region},{x.Cell},{x.Population},{x.Latitude},{x.Climate},{x.Biome},{x.GrowingDays},{x.MeanC},{x.Environment},{x.WildResources},{x.Homeland},{x.Maturity},{x.ChancePer100Years}"))));
        Console.WriteLine("Origin audit: "+selected.Length+" eligible regions, "+selected.Count(x=>x.ChancePer100Years>0)+" positive hazards; current expected wait "+(annual>0?(1/annual).ToString("N0"):"infinite at numerical precision")+" years. Input preserved="+(before==state.ComputeHash()));
    }
}
