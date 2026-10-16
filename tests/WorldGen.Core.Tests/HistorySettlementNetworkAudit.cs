using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Explicit diagnostic only. It is permitted to inspect actor collections and
// terrain; none of this is called by ordinary simulation events or a renderer.
public static class HistorySettlementNetworkAudit
{
    public static void Run(string path,string folder)
    {
        Directory.CreateDirectory(folder);var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);
        string hash=world.Prehistory.ComputeHash();
        // Some existing routing helpers increment diagnostic read counters.
        // Inspect an isolated branch; never mutate or reseal the loaded input.
        var s=engine.UpgradeToLivingWorldHistory(world.Prehistory).Copy();var h=s.History;var a=h.Aggregates;
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
        double[] Field(string name)=>(double[])typeof(Prehistory).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;
        var river=Field("riverAccess");var seasons=Field("landscapeSeason");
        double Km(int from,int to)=>D.Angle(world.Surface.Cells[from].Position,world.Surface.Cells[to].Position)*world.Recipe.RadiusKm;
        var towns=s.Society.Settlements.Where(t=>a.Living.Cities[t.Id].AbandonedYear<0).Select(t=>
        {
            var city=a.Cities[t.Id];var record=h.Towns[t.Id];var hc=h.Cultures[t.CultureId];var life=a.Living.Cities[t.Id];int owner=a.Resolve(t.PolityId,h);
            double fiscal=(double)Call("LivingCropLandscape",s,t.CultureId,t.Cell,true),environment=(double)Call("EnvironmentalEstablishment",s,t.Cell,t.CultureId),staples=(double)Call("EnvironmentalStaples",s,t.CultureId,t.Cell);
            double concentration=Math.Min(1,t.Population/3500.0),institutions=Math.Min(1,hc.Institution/600.0),water=Math.Min(1,river[t.Cell]*4),exchange=Math.Min(1,city.Neighbors.Count(n=>s.Society.Settlements[n].Population>=500)/4.0),seasonal=.25+.75*seasons[t.Cell];
            double core=Math.Min(1,fiscal/.30)*concentration*institutions*(.60+.25*water+.15*exchange)*(.85+.15*seasonal);
            int husbandry=h.Agriculture[t.CultureId].HusbandryYears?.Max()??0;
            double homeland=(double)Call("HomelandOriginFactor",s,t.Cell);
            bool provisioningGate=t.Population>=1200&&(fiscal>=.10||fiscal>=.04&&husbandry>=12000)&&t.FoodReserve>=t.Population*.3&&life.DeclineYears<=0;
            var region=a.Living.Regions[engine.CellPatch[t.Cell]];
            return new
            {
                t.Id,t.Name,t.Cell,t.CultureId,Owner=owner,Age=s.Years-t.FoundedYear,Latitude=Math.Asin(world.Surface.Cells[t.Cell].Position.Z)*180/Math.PI,Longitude=Math.Atan2(world.Surface.Cells[t.Cell].Position.Y,world.Surface.Cells[t.Cell].Position.X)*180/Math.PI,
                Climate=world.Climate.Classification[t.Cell].ToString(),Biome=world.Ecology.Habitat[t.Cell].Biome.ToString(),CellAreaKm2=world.Surface.Cells[t.Cell].AreaM2/1000000.0,
                Coastal=(bool)Call("DiffusionShore",t.Cell),
                t.Population,city.Suburban,city.Rural,city.RuralCapacity,city.AreaKm2,t.FoodCapacity,t.FoodReserve,city.Supported,life.Productivity,life.DeclineYears,
                CultureFarming=s.Society.Cultures[t.CultureId].Farming,hc.Institution,HusbandryYears=husbandry,Fiscal=fiscal,Environment=environment,Staples=staples,Core=core,Homeland=homeland,
                CivicProvisioningGate=provisioningGate,record.CivicPreparationYears,record.CivicExampleExposure,record.CivicTeacherPolityId,
                IndependentHazard=HistoricalLivingWorldOrigins.IndependentStateHazard(core,environment,staples,homeland),LearnedHazard=HistoricalEnvironmentalEffects.LearnedStateHazard(core,environment,staples,record.CivicExampleExposure),
                Continuous=(bool)Call("LivingInstitutionalContinuity",s,t.Id),ProtoCapacity=(long)Call("LivingProtocentreCapacity",s,t.Id,long.MaxValue),
                ComputedUrbanCapacity=(long)Call("CommunityUrbanCapacity",s,t.Id),ImportedCapacity=owner<0?0:(long)Call("CommunityUrbanImports",s,owner,t.Id),
                RegionTeacher=region.Teacher,RegionExposure=region.Exposure,RegionCivicTeacher=region.CivicTeacher,RegionCivicExposure=region.CivicExposure,RegionLastDeliveryAge=s.Years-region.LastExchangeYear,
                ParentsCanFound=t.Population>=1000&&t.FoodReserve>=t.Population*.5&&city.Rural>=1600,
                Neighbors=city.Neighbors.Select(id=>{var n=s.Society.Settlements[id];int court=a.Resolve(n.PolityId,h);return new{n.Id,n.Name,n.Cell,n.Population,Owner=court,DistanceKm=Km(t.Cell,n.Cell),a.Cities[id].Supported,Abandoned=a.Living.Cities[id].AbandonedYear>=0,RouteAllowed=(bool)Call("InstitutionalRouteAllowed",s,n.Cell,t.Cell,n.CultureId),DeliveredExample=court<0?0:(double)Call("InstitutionalLocalExample",s,court,n.Cell,t.Cell)};})
            };
        }).ToArray();
        var secondary=s.Society.Cultures.Where(c=>a.Cultures[c.Id].Cities.Count>0).Select(c=>
        {
            var ca=a.Cultures[c.Id];int parent=ca.Cities.At((int)((double)Call("HistoryRoll","living-town-source",c.Id,s.Years)*ca.Cities.Count));var town=s.Society.Settlements[parent];int from=town.Cell;
            var candidates=Enumerable.Range(0,3).Select(trial=>
            {
                int site=from;
                for(int step=0;step<4+trial;step++)
                {
                    var ns=world.Surface.Cells[site].Neighbors;int offset=(int)((double)Call("HistoryRoll","living-town-walk-"+trial+"-"+step,c.Id,s.Years)*ns.Length),next=-1;
                    for(int k=0;k<ns.Length;k++){int n=ns[(offset+k)%ns.Length];if(engine.CellPatch[n]>=0&&world.Landmass[n]==world.Landmass[from]&&world.Ecology.Habitat[n].ReliefM<2000){next=n;break;}}
                    if(next<0)break;site=next;
                }
                int owner=HistoricalCells.Culture(s,site),recipient=owner>=0?owner:c.Id;
                double localYield=(double)Call("LivingCropLandscape",s,recipient,site,false);
                double rails=HistoricalModernEffects.Adoption(h.Cultures[recipient],HistoricalTechnology.Railways,s.Years),spacing=(100-45*rails)*Math.Sqrt(Math.Min(4,Math.Max(1,.25/Math.Max(.02,localYield))));
                int[] nearby=(int[])Call("NearbyLivingTowns",s,site,spacing);int region=engine.CellPatch[site];
                return new{Trial=trial,Cell=site,Recipient=recipient,LocalYield=localYield,SpacingKm=spacing,NearbyTowns=nearby,ActualCanFound=(bool)Call("CanFoundLivingTown",s,recipient,site,parent),ExistingTown=a.TownAt[site],RegionCommittedFraction=a.Living.Regions[region].CommittedAreaKm2/Math.Max(1.0,a.Regions[region].AreaKm2)};
            }).ToArray();
            return new{c.Id,c.Name,c.Farming,ca.CityCount,ca.Urban,ca.Rural,Parent=parent,ParentUrban=town.Population,ParentRural=a.Cities[parent].Rural,ParentFood=town.FoodReserve,Candidates=candidates};
        }).ToArray();
        var maritime=h.Polities.Where(p=>p.EndedYear<0).Select(p=>
        {
            int harbor=(int)Call("ColonialProvincialHarbor",s,p.Id);
            var live=a.Polities[p.Id].Cities;
            var charts=a.Empires.Connections.Cultures[p.CultureId].Charts;
            var plans=charts.Take(6).Select(chart=>
            {
                var route=harbor<0?null:(Prehistory.Route)Call("ColonialTargetRoute",s,harbor,chart.Region,p.Id);
                if(route==null)return new{chart.Region,Target=-1,Town=-1,TargetPolity=-1,Supplied=false,SupplyPort=-1,Provisions=0L};
                object[] args={s,p.Id,route,-1,-1,0L};bool supplied=(bool)Call("MaritimeCharterLogistics",args);
                return new{chart.Region,Target=route.CellB,Town=a.TownAt[route.CellB],TargetPolity=HistoricalCells.Polity(s,route.CellB),Supplied=supplied,SupplyPort=(int)args[4],Provisions=(long)args[5]};
            }).ToArray();
            return new{p.Id,p.Name,p.Treasury,a.Polities[p.Id].Receipts,a.Polities[p.Id].ServiceCost,Harbor=harbor,Coasts=a.Empires.Connections.Coasts.Governments[p.Id].Count,
                Ports=Enumerable.Range(0,live.Count).Select(k=>s.Society.Settlements[live.At(k)]).Where(t=>(bool)Call("DiffusionShore",t.Cell)).Select(t=>new{t.Id,t.Name,t.Population,t.FoodReserve,a.Cities[t.Id].Supported}),
                Offices=a.Empires.Governments[p.Id].AdministrativePosts.Count,Charts=charts.Length,Plans=plans};
        }).ToArray();
        var result=new{Year=s.Years,h.Version,a.TotalPopulation,a.TotalUrban,LiveTowns=towns.Length,Towns=towns,SecondaryFounding=secondary,Maritime=maritime};
        File.WriteAllText(Path.Combine(folder,"settlement-network.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        if(world.Prehistory.ComputeHash()!=hash)throw new Exception("Diagnostic unexpectedly mutated the loaded authoritative history.");
        Console.WriteLine($"AUDIT {towns.Length} live centers; {a.TotalUrban:N0} urban / {a.TotalPopulation:N0} world residents; {towns.Count(t=>t.Owner<0&&t.CivicProvisioningGate)} unruled centers pass fiscal gates.");
    }
}
