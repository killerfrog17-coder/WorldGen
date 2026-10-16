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
        bool actorLocal=typeof(Prehistory).GetMethod("LivingLocalTownRate",BindingFlags.Instance|BindingFlags.NonPublic)!=null;
        bool householdTransfer=typeof(Prehistory).GetMethod("LivingFounderAvailability",BindingFlags.Instance|BindingFlags.NonPublic)!=null;
        object Navigation(int culture)
        {
            var hc=h.Cultures[culture];
            return new{Culture=culture,Name=s.Society.Cultures[culture].Name,MaritimePractice=(bool)Call("CommunityMaritimePractice",s,culture),
                Writing=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Writing,s.Years),Sailing=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Sailing,s.Years),
                Navigation=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Navigation,s.Years),OceanNavigation=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years),
                WritingLevel=hc.Technical.Levels[(int)HistoricalTechnology.Writing],SailingLevel=hc.Technical.Levels[(int)HistoricalTechnology.Sailing],
                NavigationLevel=hc.Technical.Levels[(int)HistoricalTechnology.Navigation],OceanNavigationLevel=hc.Technical.Levels[(int)HistoricalTechnology.OceanNavigation]};
        }
        double[] Field(string name)=>(double[])typeof(Prehistory).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;
        var river=Field("riverAccess");var seasons=Field("landscapeSeason");
        double Km(int from,int to)=>D.Angle(world.Surface.Cells[from].Position,world.Surface.Cells[to].Position)*world.Recipe.RadiusKm;
        bool LocalCommitAllowed(int parent,int target)
        {
            var town=s.Society.Settlements[parent];int culture=town.CultureId,owner=a.Resolve(town.PolityId,h);
            if(engine.CellPatch[target]<0||world.Landmass[target]!=world.Landmass[town.Cell]||!(bool)Call("InstitutionalRouteAllowed",s,town.Cell,target,culture))return false;
            int host=HistoricalCells.Polity(s,target);
            if(host>=0&&host!=owner)
            {
                var relations=(Dictionary<(int,int),int>)typeof(Prehistory).GetField("aggregateRelations",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;
                if(owner<0||!relations.TryGetValue((Math.Min(owner,host),Math.Max(owner,host)),out int relation)||!h.Relations[relation].Trade||h.Relations[relation].WarSince>=0)return false;
            }
            if(owner>=0&&((bool)Call("RespectRecognizedClaim",s,owner,target)||(double)Call("LivingTravelKm",s,town.Cell,target,culture)>(double)Call("TerritorialServiceRadius",s,owner,target)*1.5))return false;
            if(!(bool)Call("CanFoundLivingTown",s,culture,target,parent))return false;
            long available=(long)Call("LivingFounderAvailability",s,parent),founders=Math.Min(250,available/12),farms=Math.Min(available/3,Math.Max(800,founders*8)),people=founders+farms,investment=Math.Max(100,people/5);
            return town.FoodReserve>=people+town.Population/4&&(owner<0||h.Polities[owner].Treasury>=investment*2&&a.Polities[owner].Receipts>a.Polities[owner].ServiceCost);
        }
        object[] LocalTargets(int parent)
        {
            if(!actorLocal)return Array.Empty<object>();var source=s.Society.Settlements[parent];
            return Enumerable.Range(0,3).Select(trial=>
            {
                int site=source.Cell,previous=-1;
                bool sharedCandidate=typeof(Prehistory).GetMethod("LivingLocalTownCandidate",BindingFlags.Instance|BindingFlags.NonPublic)!=null;
                if(sharedCandidate)site=(int)Call("LivingLocalTownCandidate",s,parent,trial);
                // Frozen older candidate binaries retain their exact original
                // fixed-hop proposal for before/after diagnostic comparison.
                for(int step=0;!sharedCandidate&&step<4+trial;step++)
                {
                    var ns=world.Surface.Cells[site].Neighbors;int offset=(int)((double)Call("HistoryRoll","living-local-town-walk-"+trial+"-"+step,parent,s.Years)*ns.Length),next=-1;
                    for(int k=0;k<ns.Length;k++){int n=ns[(offset+k)%ns.Length];if(n!=previous&&engine.CellPatch[n]>=0&&world.Landmass[n]==world.Landmass[source.Cell]&&world.Ecology.Habitat[n].ReliefM<2000){next=n;break;}}
                    if(next<0)break;previous=site;site=next;
                }
                double localYield=(double)Call("LivingCropLandscape",s,source.CultureId,site,false),rails=HistoricalModernEffects.Adoption(h.Cultures[source.CultureId],HistoricalTechnology.Railways,s.Years);
                double spacing=(100-45*rails)*Math.Sqrt(Math.Min(4,Math.Max(1,.25/Math.Max(.02,localYield))));
                int[] nearby=(int[])Call("NearbyLivingTowns",s,site,spacing);
                return (object)new{Trial=trial,Cell=site,Km=Km(source.Cell,site),LocalYield=localYield,SpacingKm=spacing,Nearby=nearby,
                    RouteAllowed=(bool)Call("InstitutionalRouteAllowed",s,source.Cell,site,source.CultureId),Transmission=(double)Call("EnvironmentalTransmission",s,source.Cell,site,source.CultureId,false),
                    CanFound=(bool)Call("CanFoundLivingTown",s,source.CultureId,site,parent),CommitAllowed=LocalCommitAllowed(parent,site),ExistingTown=a.TownAt[site]};
            }).ToArray();
        }
        object[] GeodesicTargets(int parent)
        {
            var source=s.Society.Settlements[parent];var from=world.Surface.Cells[source.Cell].Position;
            var east=V.Tangent(from,new V(0,0,1));if(east.Length<.5)east=V.Tangent(from,new V(1,0,0));var north=V.Cross(from,east).Unit;
            var raster=(int[])typeof(Prehistory).GetField("aggregateTerrainRaster",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;
            double bearing=(double)Call("HistoryRoll","living-local-town-bearing",parent,s.Years)*Math.PI*2;
            return Enumerable.Range(0,3).Select(trial=>
            {
                double radius=120+60*trial,angle=bearing+trial*Math.PI*2/3;var tangent=east*Math.Cos(angle)+north*Math.Sin(angle);
                var point=(from*Math.Cos(radius/world.Recipe.RadiusKm)+tangent*Math.Sin(radius/world.Recipe.RadiusKm)).Unit;
                int site=raster[(int)Call("InstitutionalRasterNode",point)];
                for(int pass=0;pass<4;pass++)
                {
                    int best=site;double score=V.Dot(point,world.Surface.Cells[site].Position);
                    foreach(int candidate in world.Surface.Cells[site].Neighbors){double current=V.Dot(point,world.Surface.Cells[candidate].Position);if(current>score||current==score&&candidate<best){score=current;best=candidate;}}
                    if(best==site)break;site=best;
                }
                bool land=engine.CellPatch[site]>=0&&world.Landmass[site]==world.Landmass[source.Cell];
                double localYield=land?(double)Call("LivingCropLandscape",s,source.CultureId,site,false):0;
                double rails=HistoricalModernEffects.Adoption(h.Cultures[source.CultureId],HistoricalTechnology.Railways,s.Years),spacing=(100-45*rails)*Math.Sqrt(Math.Min(4,Math.Max(1,.25/Math.Max(.02,localYield))));
                int[] nearby=land?(int[])Call("NearbyLivingTowns",s,site,spacing):Array.Empty<int>();
                return (object)new{Trial=trial,Cell=site,Km=Km(source.Cell,site),Land=land,LocalYield=localYield,SpacingKm=spacing,Nearby=nearby,
                    RouteAllowed=land&&(bool)Call("InstitutionalRouteAllowed",s,source.Cell,site,source.CultureId),
                    Transmission=land?(double)Call("EnvironmentalTransmission",s,source.Cell,site,source.CultureId,false):0,
                    CanFound=land&&(bool)Call("CanFoundLivingTown",s,source.CultureId,site,parent),CommitAllowed=land&&LocalCommitAllowed(parent,site)};
            }).ToArray();
        }
        var towns=s.Society.Settlements.Where(t=>a.Living.Cities[t.Id].AbandonedYear<0).Select(t=>
        {
            var city=a.Cities[t.Id];var record=h.Towns[t.Id];var hc=h.Cultures[t.CultureId];var life=a.Living.Cities[t.Id];int owner=a.Resolve(t.PolityId,h);
            double fiscal=(double)Call("LivingCropLandscape",s,t.CultureId,t.Cell,true),environment=(double)Call("EnvironmentalEstablishment",s,t.Cell,t.CultureId),staples=(double)Call("EnvironmentalStaples",s,t.CultureId,t.Cell);
            double concentration=Math.Min(1,t.Population/3500.0),institutions=Math.Min(1,hc.Institution/600.0),water=Math.Min(1,river[t.Cell]*4),exchange=Math.Min(1,city.Neighbors.Count(n=>s.Society.Settlements[n].Population>=500)/4.0),seasonal=.25+.75*seasons[t.Cell];
            double core=Math.Min(1,fiscal/.30)*concentration*institutions*(.60+.25*water+.15*exchange)*(.85+.15*seasonal);
            int husbandry=h.Agriculture[t.CultureId].HusbandryYears?.Max()??0;
            double homeland=(double)Call("HomelandOriginFactor",s,t.Cell);
            bool provisioningGate=t.Population>=1200&&(fiscal>=.10||fiscal>=.04&&husbandry>=12000)&&t.FoodReserve>=t.Population*.3&&life.DeclineYears<=0;
            long transferable=householdTransfer?(long)Call("LivingFounderAvailability",s,t.Id):city.Rural;
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
                TransferableHouseholds=transferable,
                ActorLocalAnnualOpportunity=actorLocal?(double)Call("LivingLocalTownRate",s,t.Id):0,
                ActorLocalTargets=LocalTargets(t.Id),
                CounterfactualGeodesicTargets=GeodesicTargets(t.Id),
                ParentsCanFound=t.Population>=1000&&t.FoodReserve>=t.Population*.5&&transferable>=1600,
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
            int[] departures=(int[])Call("LivingColonialDepartures",s,p.Id);
            var voyages=(Prehistory.Route[][])typeof(Prehistory).GetField("communityVoyages",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;
            var plans=departures.SelectMany(source=>Enumerable.Range(0,6).Select(k=>
            {
                var nearby=voyages[engine.CellPatch[source]];
                int region=charts.Length>0&&k<3?charts[(int)((double)Call("HistoryRoll","living-colonial-chart-"+source+"-"+k,p.Id,s.Years)*charts.Length)].Region:
                    nearby.Length>0?nearby[(int)((double)Call("HistoryRoll","living-colonial-explore-"+source+"-"+k,p.Id,s.Years)*nearby.Length)].B:-1;
                var route=region<0?null:(Prehistory.Route)Call("ColonialTargetRoute",s,source,region,p.Id);
                if(route==null)return (object)new{Source=source,Trial=k,Region=region,Target=-1,Supplied=false};
                object[] args={s,p.Id,route,a.TownAt[source],-1,0L};bool supplied=(bool)Call("MaritimeCharterLogistics",args);
                int port=(int)args[4],target=route.CellB,post=a.Empires.Territories.PostAt[target],owner=HistoricalCells.Polity(s,target),parent=a.Empires.Territories.Sources[source];
                var founderOptions=Enumerable.Range(0,live.Count).Select(i=>
                {
                    int candidate=live.At(i);var home=s.Society.Settlements[candidate];object[] support={s,p.Id,route,candidate,-1,0L};
                    bool physical=(bool)Call("MaritimeCharterLogistics",support)&&(int)support[4]==candidate;
                    return new{Id=candidate,home.Name,home.CultureId,Coastal=(bool)Call("DiffusionShore",home.Cell),Physical=physical,
                        Yield=(double)Call("LivingCropLandscape",s,home.CultureId,target,false),CanFound=(bool)Call("CanFoundLivingTown",s,home.CultureId,target,candidate),
                        Available=(long)Call("LivingFounderAvailability",s,candidate),home.FoodReserve};
                }).ToArray();
                return (object)new{Source=source,Trial=k,Region=region,Target=target,DistanceKm=route.DistanceM/1000.0,Overseas=world.Landmass[source]!=world.Landmass[target],Town=a.TownAt[target],TargetPolity=owner,
                    Latitude=Math.Asin(world.Surface.Cells[target].Position.Z)*180/Math.PI,Longitude=Math.Atan2(world.Surface.Cells[target].Position.Y,world.Surface.Cells[target].Position.X)*180/Math.PI,
                    Climate=world.Climate.Classification[target].ToString(),Biome=world.Ecology.Habitat[target].Biome.ToString(),
                    Supplied=supplied,SupplyPort=port,Provisions=(long)args[5],Post=post,
                    Interest=(bool)Call("LivingTerritorialInterest",s,p.Id,target),InstitutionalRoute=(bool)Call("InstitutionalRouteAllowed",s,source,target,p.CultureId),
                    ClaimBlocked=(bool)Call("RespectRecognizedClaim",s,p.Id,target),Parent=parent,ParentLive=(bool)Call("TerritorialNodeLive",s,p.Id,parent),
                    TargetCropYield=port<0?0:(double)Call("LivingCropLandscape",s,s.Society.Settlements[port].CultureId,target,false),
                    CanFound=port>=0&&(bool)Call("CanFoundLivingTown",s,s.Society.Settlements[port].CultureId,target,port),
                    SourceFarming=port<0?0:s.Society.Cultures[s.Society.Settlements[port].CultureId].Farming,
                    SourceHouseholds=port<0?0:(long)Call("LivingFounderAvailability",s,port),SourceFood=port<0?0:s.Society.Settlements[port].FoodReserve,FounderOptions=founderOptions};
            })).ToArray();
            return new{p.Id,p.Name,p.CultureId,Sovereign=HistoricalImperialHierarchy.Sovereign(s,p.Id),Patron=HistoricalImperialHierarchy.Patron(s,p.Id),p.Population,p.AreaKm2,Age=s.Years-p.FoundedYear,
                Capital=p.CapitalId,CapitalCell=s.Society.Settlements[p.CapitalId].Cell,
                p.Treasury,a.Polities[p.Id].Receipts,a.Polities[p.Id].ServiceCost,RulingNavigation=Navigation(p.CultureId),Harbor=harbor,Coasts=a.Empires.Connections.Coasts.Governments[p.Id].Count,
                Ports=Enumerable.Range(0,live.Count).Select(k=>s.Society.Settlements[live.At(k)]).Where(t=>(bool)Call("DiffusionShore",t.Cell)).Select(t=>new{t.Id,t.Name,t.Population,t.FoodReserve,a.Cities[t.Id].Supported,Navigation=Navigation(t.CultureId)}),
                Offices=a.Empires.Governments[p.Id].AdministrativePosts.Count,Charts=charts.Length,
                KnownCoasts=charts.Select(chart=>
                {
                    int cell=((int[])typeof(Prehistory).GetField("developmentCoast",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!)[chart.Region];
                    return new{chart.Region,chart.Year,chart.SourceCulture,Cell=cell,
                        Latitude=cell<0?0:Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI,Longitude=cell<0?0:Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI,
                        RulerYield=cell<0?0:(double)Call("LivingCropLandscape",s,p.CultureId,cell,false),
                        NearbyVoyages=voyages[chart.Region].Select(route=>new{route.B,route.DistanceM}).ToArray()};
                }),DepartureDetails=departures.Select(cell=>
                {
                    int node=a.Empires.Territories.Sources[cell];return new{Cell=cell,Node=node,Depth=node>=0?0:a.Empires.Territories.Posts[-node-2].Depth,
                        Latitude=Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI,Longitude=Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI};
                }),CompatibleKnownTargets=departures.SelectMany(source=>charts.Select(chart=>
                {
                    var route=(Prehistory.Route)Call("ColonialTargetRoute",s,source,chart.Region,p.Id);if(route==null)return (object)null;
                    object[] logistics={s,p.Id,route,a.TownAt[source],-1,0L};if(!(bool)Call("MaritimeCharterLogistics",logistics))return null;
                    int port=(int)logistics[4];var depot=s.Society.Settlements[port];
                    var farmers=Enumerable.Range(0,live.Count).Select(k=>s.Society.Settlements[live.At(k)]).Where(t=>world.Landmass[t.Cell]==world.Landmass[depot.Cell]
                        &&(double)Call("LivingTravelKm",s,t.Cell,depot.Cell,t.CultureId)<=(double)Call("TerritorialServiceRadius",s,p.Id,depot.Cell)*1.5
                        &&(bool)Call("InstitutionalRouteAllowed",s,t.Cell,depot.Cell,t.CultureId)&&(bool)Call("CanFoundLivingTown",s,t.CultureId,route.CellB,t.Id)).Select(t=>new{t.Id,t.CultureId}).ToArray();
                    return (object)new{Source=source,chart.Region,Target=route.CellB,SupplyPort=port,PortCanFound=(bool)Call("CanFoundLivingTown",s,depot.CultureId,route.CellB,port),Farmers=farmers};
                })).Where(x=>x!=null),Departures=departures,Plans=plans};
        }).ToArray();
        var result=new{Year=s.Years,h.Version,a.TotalPopulation,a.TotalUrban,LiveTowns=towns.Length,Towns=towns,
            ActorLocalSecondaryOpportunities=actorLocal,LegacyCultureLotteryActive=!actorLocal,LegacyCultureLotteryDiagnostic=secondary,Maritime=maritime};
        File.WriteAllText(Path.Combine(folder,"settlement-network.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        if(world.Prehistory.ComputeHash()!=hash)throw new Exception("Diagnostic unexpectedly mutated the loaded authoritative history.");
        Console.WriteLine($"AUDIT {towns.Length} live centers; {a.TotalUrban:N0} urban / {a.TotalPopulation:N0} world residents; {towns.Count(t=>t.Owner<0&&t.CivicProvisioningGate)} unruled centers pass fiscal gates.");
    }
}
