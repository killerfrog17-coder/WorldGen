using System.Security.Cryptography;
using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Offline saved inspection or explicitly requested fresh replay; no model changes.
public static class HistoryMigrantCommunityAudit
{
    public static void RunFrontiers(string source,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new frontier audit output.");
        var world=WorldFile.Read(File.ReadAllBytes(source));WriteFrontiers(world,world.Prehistory,source,output,false);
    }
    public static void RunReplayFrontiers(string source,string output,int year)
    {
        if(File.Exists(output))throw new IOException("Use a new frontier audit output.");
        if(year<1||year>350000)throw new ArgumentOutOfRangeException(nameof(year));
        var world=WorldFile.Read(File.ReadAllBytes(source));world.Prehistory=null;
        var engine=new Prehistory(world);var session=new SimulationSession(engine,engine.Start(engine.Prepare()));
        while(session.LastSnapshot.Years<year)
        {
            int step=session.LastSnapshot.History.Polities.Length==0?1000:100;
            session.AdvanceSeconds(Math.Min(step,year-session.LastSnapshot.Years)*SimulationTime.Year,int.MaxValue);
            if(session.LastSnapshot.Years%20000==0)Console.WriteLine("Frontier replay "+session.LastSnapshot.Years);
        }
        var s=session.Snapshot();engine.Validate(s);world.Prehistory=s;WriteFrontiers(world,s,source,output,true);
    }
    static void WriteFrontiers(World world,PrehistoryState original,string source,string output,bool replay)
    {
        string before=original.ComputeHash();
        var engine=new Prehistory(world);var s=original.Copy();var h=s.History;var a=h.Aggregates;
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,values)!;
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
        double Lat(int cell)=>Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI;
        double Lon(int cell)=>Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI;
        var contact=new long[a.Regions.Length];var cohort=new int[contact.Length];
        for(int r=0;r<contact.Length;r++){contact[r]=(long)Call("ProvincialContactResidents",s,r,false);cohort[r]=(int)Call("ProvincialRegionalCohort",s,r);}
        var townRural=new long[contact.Length];
        foreach(var town in s.Society.Settlements)if(town.Population>0&&a.Living.Cities[town.Id].AbandonedYear<0)townRural[engine.CellPatch[town.Cell]]+=a.Cities[town.Id].Rural+a.Cities[town.Id].Suburban;
        var regions=a.Regions.Select((r,id)=>
        {
            int ledger=cohort[id],owner=ledger>=0?a.Resolve(ledger,h):-1,culture=owner>=0?h.Polities[owner].CultureId:r.Culture;
            long remote=ledger>=0?a.Polities[ledger].Remote:0;
            return new{Id=id,Latitude=Lat(r.BestCell),Longitude=Lon(r.BestCell),RawDepartureResidents=s.Population[id],ContactResidents=contact[id],
                a.Living.Regions[id].SettledPopulation,TownRuralAndSuburban=townRural[id],ClaimedAreaKm2=a.Provinces.ClaimedAreaKm2[id],
                PopulationCohort=ledger,Owner=owner,Culture=culture,Farming=culture>=0?s.Society.Cultures[culture].Farming:0,
                CohortRemote=remote,RemoteAreaKm2=ledger>=0?a.Polities[ledger].RemoteAreaKm2:0,
                DepartureBlockedByRawPool=s.Population[id]<125,HasRemoteContactButNoDeparture=contact[id]>=1000&&s.Population[id]<125&&a.Provinces.ClaimedAreaKm2[id]>0&&remote>0&&culture>=0&&s.Society.Cultures[culture].Farming>=250};
        }).ToArray();
        var edges=new List<object>();int ownedFarmerEdges=0,missingRawPoolEdges=0,anonymousUnownedEdges=0,viableEdges=0;
        var viableSourceRegions=new HashSet<int>();var viableTargetRegions=new HashSet<int>();
        for(int routeId=0;routeId<engine.Routes.Length;routeId++)
        {
            var route=engine.Routes[routeId];if(route.Sea)continue;
            for(int direction=0;direction<2;direction++)
            {
                int from=direction==0?route.A:route.B,to=direction==0?route.B:route.A;
                int departure=(int)Call("RegionalContactCell",s,route,from),arrival=(int)Call("RegionalContactCell",s,route,to);
                int rawOwner=HistoricalCells.Polity(s,departure),owner=rawOwner>=0?a.Resolve(rawOwner,h):-1;
                if(owner<0)continue;int sourceCulture=h.Polities[owner].CultureId;
                if(s.Society.Cultures[sourceCulture].Farming<250)continue;ownedFarmerEdges++;
                int sourceCohort=a.PopulationCohort(owner);long remote=a.Polities[sourceCohort].Remote;
                if(contact[from]<1000||s.Population[from]>=125||remote<=0)continue;missingRawPoolEdges++;
                int targetRawOwner=HistoricalCells.Polity(s,arrival),targetOwner=targetRawOwner>=0?a.Resolve(targetRawOwner,h):-1;
                bool anonymous=a.Regions[to].Culture<0&&HistoricalCells.Culture(s,arrival)<0&&targetOwner<0;
                if(!anonymous)continue;anonymousUnownedEdges++;
                double cropYield=(double)Call("LivingCropLandscape",s,sourceCulture,arrival,false),cropFit=(double)Call("AggregateCropSupport",s,sourceCulture,arrival,false);
                bool allowed=(bool)Call("InstitutionalRouteAllowed",s,departure,arrival,sourceCulture);
                bool viable=cropYield>=.12&&allowed&&world.Landmass[departure]>=0&&world.Landmass[departure]==world.Landmass[arrival];
                if(viable){viableEdges++;viableSourceRegions.Add(from);viableTargetRegions.Add(to);}
                var habitat=world.Ecology.Habitat[arrival];
                edges.Add(new{Route=routeId,From=from,To=to,DepartureCell=departure,ArrivalCell=arrival,Latitude=Lat(arrival),Longitude=Lon(arrival),
                    SourceOwner=owner,SourcePopulationCohort=sourceCohort,SourceCulture=sourceCulture,SourceFarming=s.Society.Cultures[sourceCulture].Farming,
                    SourceCohortRemote=remote,SourceRawDepartureResidents=s.Population[from],SourceContactResidents=contact[from],SourceClaimedAreaKm2=a.Provinces.ClaimedAreaKm2[from],
                    TargetRawResidents=s.Population[to],TargetForagerCapacity=a.Regions[to].Capacity,TargetLocalCulture=a.Regions[to].Culture,TargetCellCulture=HistoricalCells.Culture(s,arrival),TargetOwner=targetOwner,
                    TargetRecognizedClaim=HistoricalRecognizedClaims.Holder(s,arrival),CropYield=cropYield,CarriedCropFit=cropFit,RouteAllowed=allowed,Viable=viable,
                    PotentialBorderFarmers=(long)Call("ProvincialFarmPotential",s,arrival,sourceCulture),Climate=world.Climate.Classification[arrival].ToString(),Biome=habitat.Biome.ToString(),
                    habitat.GrowingDays,habitat.WarmTenthsC,habitat.PermafrostPermille,habitat.ReliefM});
            }
        }
        if(original.ComputeHash()!=before)throw new Exception("Frontier audit changed its published source snapshot.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=Path.GetFullPath(source),Replay=replay,Year=s.Years,Hash=before,Realm=a.Empires.Connections.Realms.Version,
            Counts=new{Regions=regions.Length,RawDepartureBlocked=regions.Count(r=>r.DepartureBlockedByRawPool),RemoteContactMissingDeparture=regions.Count(r=>r.HasRemoteContactButNoDeparture),
                OwnedFarmerDirectedLandEdges=ownedFarmerEdges,MissingRawDepartureDirectedEdges=missingRawPoolEdges,AnonymousUnownedDirectedEdges=anonymousUnownedEdges,
                ViableDirectedEdges=viableEdges,ViableSourceRegions=viableSourceRegions.Count,ViableTargetRegions=viableTargetRegions.Count},Regions=regions,AnonymousFrontiers=edges,
            Notes=new[]{"Offline census of a detached snapshot. Replay=true means a requested fresh run preceded inspection; no input-world mutation or rule upgrade.",
                "ContactResidents includes regional apportionment of a state Remote ledger and is not a population account that can be debited.",
                "CohortRemote is the actual shared ledger, not a fresh population copy per region; do not sum its repeated appearances.",
                "Viable frontier means current carried-crop yield>=.12, physical adjacent land gate passes, and recipient is unowned/anonymous. This does not authorize a free culture, town or state or prove food/household conservation.",
                "This final-snapshot opportunity census is not a reconstruction of preindustrial frontier opportunities."}
        },new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine($"Frontier audit {s.Years}: {regions.Count(r=>r.HasRemoteContactButNoDeparture)} remote-contact regions blocked by raw pool; {viableEdges} viable edges from {viableSourceRegions.Count} regions to {viableTargetRegions.Count}; preserved {before}");
    }
    public static void Run(string source,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new migrant audit output.");
        var world=WorldFile.Read(File.ReadAllBytes(source));var s=world.Prehistory;
        string before=s.ComputeHash();var h=s.History;var a=h.Aggregates;
        double Lat(int cell)=>Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI;
        double Lon(int cell)=>Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI;
        object Place(int cell)
        {
            var habitat=world.Ecology.Habitat[cell];
            return new{Cell=cell,Latitude=Lat(cell),Longitude=Lon(cell),Landmass=world.Landmass[cell],Ocean=world.Hydrology.OceanConnected[cell],
                Climate=world.Climate.Classification[cell].ToString(),Biome=habitat.Biome.ToString(),habitat.GrowingDays,habitat.ColdTenthsC,habitat.WarmTenthsC,
                habitat.AridityMilli,habitat.ReliefM,habitat.SoilFertilityPermille,habitat.PermafrostPermille};
        }
        var towns=s.Society.Settlements.GroupBy(t=>t.CultureId).ToDictionary(g=>g.Key,g=>g.OrderBy(t=>t.FoundedYear).ThenBy(t=>t.Id).ToArray());
        var births=h.Archive.Where(e=>e.Kind=="migrant-community").OrderBy(e=>e.Year).ThenBy(e=>e.Id).ToArray();
        var birthIds=new HashSet<int>(births.Select(e=>e.CultureId));
        var cultures=s.Society.Cultures.Select(c=>
        {
            var hc=h.Cultures[c.Id];var crops=h.Agriculture[c.Id];
            var own=towns.TryGetValue(c.Id,out var list)?list:Array.Empty<Settlement>();
            var first=own.FirstOrDefault();int parent=hc.ParentCultureId;
            return new{c.Id,c.Name,c.FoundedYear,ParentCulture=parent,c.Population,c.Farming,c.Fishing,Stage=c.Stage.ToString(),Origin=Place(c.OriginCell),
                Region=a.Cultures[c.Id].Region,a.Cultures[c.Id].CivilizationSince,KnowledgeEra=WorldEraView.KnowledgeEra(hc),hc.Institution,hc.Surplus,
                MigrantBirthArchived=birthIds.Contains(c.Id),crops.NativeMask,crops.Practice,crops.AdoptedYear,crops.SourceCulture,crops.PracticeCell,crops.WildOriginCell,crops.Cultivars,
                Levels=hc.Technical?.Levels,Deployment=hc.Technical?.Deployment,
                TotalTownRecords=own.Length,ActiveTownRecords=own.Count(t=>t.Population>0&&a.Living.Cities[t.Id].AbandonedYear<0),
                FirstTown=first==null?null:new{first.Id,first.Name,first.FoundedYear,YearsAfterCulture=first.FoundedYear-c.FoundedYear,Place=Place(first.Cell)},
                CurrentTownPopulation=own.Where(t=>t.Population>0&&a.Living.Cities[t.Id].AbandonedYear<0).Sum(t=>t.Population)};
        }).ToArray();
        var migrations=births.Select(e=>
        {
            int child=e.CultureId,parent=child>=0?h.Cultures[child].ParentCultureId:-1;
            var crop=child>=0?h.Agriculture[child]:null;
            return new{e.Id,e.Year,e.CultureId,e.Text,Location=Place(e.Cell),ParentCulture=parent,
                ParentName=parent>=0?s.Society.Cultures[parent].Name:null,
                ParentOrigin=parent>=0?Place(s.Society.Cultures[parent].OriginCell):null,
                ChildOrigin=child>=0?Place(s.Society.Cultures[child].OriginCell):null,
                CurrentChildFarming=child>=0?s.Society.Cultures[child].Farming:0,CurrentChildKnowledgeEra=child>=0?WorldEraView.KnowledgeEra(h.Cultures[child]):-1,
                CurrentCropSources=crop?.SourceCulture,CurrentAdoptedYear=crop?.AdoptedYear};
        }).ToArray();
        var regions=a.Regions.Select((r,id)=>new{Id=id,LocalCulture=r.Culture,Location=Place(r.BestCell),ForagerResidents=s.Population[id],
            a.Living.Regions[id].SettledPopulation,a.Living.Regions[id].Teacher,a.Living.Regions[id].Exposure,a.Living.Regions[id].LastExchangeYear,
            a.Living.Regions[id].CivicTeacher,a.Living.Regions[id].CivicExposure}).ToArray();
        if(s.ComputeHash()!=before)throw new Exception("Read-only migrant audit changed its saved input checkpoint.");
        string assembly=typeof(Prehistory).Assembly.Location;
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=Path.GetFullPath(source),Year=s.Years,Hash=before,Realm=a.Empires.Connections.Realms.Version,
            CoreAssembly=assembly,CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),MigrantCommunityEvents=migrations,Cultures=cultures,Regions=regions,
            Notes=new[]{"Pure saved-state inspection: no advance, upgrade, generator or simulation mutation.",
                "Archived migrant-community events identify actual created descendants and birth locations; they do not retain exact moved people, source route or food debit. Controlled numerical fixtures must verify conservation separately.",
                "Current crop arrays, knowledge, population and town survival are measured at this saved year, not reconstructed at birth.",
                "Parent origin can be far from its actual departing frontier: parent-origin to child-origin distance is not a migration leg or proof of teleportation.",
                "A descendant culture is not automatically a town or state. A missing local culture is not absence of inhabitants; no exact historical homeland is required."}
        },new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine($"Migrant saved audit {s.Years}: {migrations.Length} archived births, {cultures.Length} cultures; preserved {before}");
    }
}
