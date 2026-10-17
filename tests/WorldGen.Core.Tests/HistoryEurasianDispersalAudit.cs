using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Deliberate offline regional/edge census, not an authoritative simulation path.
public static class HistoryEurasianDispersalAudit
{
    public static void Run(string source,string output,int replayYear=-1)
    {
        if(File.Exists(output))throw new IOException("Use a new dispersal audit output.");
        var world=WorldFile.Read(File.ReadAllBytes(source));
        if(replayYear>=0)
        {
            world.Prehistory=null;var replay=new Prehistory(world);var session=new SimulationSession(replay,replay.Start(replay.Prepare()));
            while(session.LastSnapshot.Years<replayYear)
            {
                int step=session.LastSnapshot.History.Polities.Length==0?1000:100;
                session.AdvanceSeconds(Math.Min(step,replayYear-session.LastSnapshot.Years)*SimulationTime.Year,int.MaxValue);
                if(session.LastSnapshot.Years%20000==0)Console.WriteLine("Dispersal replay "+session.LastSnapshot.Years);
            }
            world.Prehistory=session.Snapshot();replay.Validate(world.Prehistory);
        }
        var original=world.Prehistory;string before=original.ComputeHash();var s=original.Copy();var h=s.History;var a=h.Aggregates;var engine=new Prehistory(world);
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,values)!;
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
        var first=h.Polities.OrderBy(p=>p.FoundedYear).ThenBy(p=>p.Id).First();int landmass=world.Landmass[s.Society.Settlements[first.CapitalId].Cell];
        double Lat(int cell)=>Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI;
        double Lon(int cell)=>Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI;
        int Farmer(int culture)=>culture>=0?s.Society.Cultures[culture].Farming:0;
        var teacher=new int[a.Regions.Length];var contacts=new long[teacher.Length];
        for(int id=0;id<teacher.Length;id++)
        {
            teacher[id]=(int)Call("DevelopmentRegionalTeacher",s,id);contacts[id]=(long)Call("ProvincialContactResidents",s,id,false);
            if(teacher[id]<0&&s.Population[id]+a.Living.Regions[id].SettledPopulation>=100&&a.Living.Regions[id].Exposure>=100)teacher[id]=a.Living.Regions[id].Teacher;
        }
        var edges=new List<object>();var counts=new SortedDictionary<string,int>();
        void Count(string key){counts.TryGetValue(key,out int count);counts[key]=count+1;}
        for(int routeId=0;routeId<engine.Routes.Length;routeId++)
        {
            var route=engine.Routes[routeId];if(route.Sea)continue;
            for(int direction=0;direction<2;direction++)
            {
                int from=direction==0?route.A:route.B,to=direction==0?route.B:route.A;
                int departure=(int)Call("RegionalContactCell",s,route,from),arrival=(int)Call("RegionalContactCell",s,route,to);
                if(world.Landmass[departure]!=landmass||world.Landmass[arrival]!=landmass)continue;
                var r=a.Regions[from];var destination=a.Regions[to];
                int raw=HistoricalCells.Polity(s,departure),owner=raw>=0?a.Resolve(raw,h):-1,ownerCulture=owner>=0?h.Polities[owner].CultureId:-1;
                int cohort=owner>=0?a.PopulationCohort(owner):-1;long remote=cohort>=0?a.Polities[cohort].Remote:0;
                bool regionalFarmer=Farmer(r.Culture)>=180,ownerFarmer=Farmer(ownerCulture)>=250&&remote>0,teacherFarmer=Farmer(teacher[from])>=180;
                string sourceKind=regionalFarmer?"RegionalFarmer":ownerFarmer?"OwnerRemoteFarmer":teacherFarmer?"TeacherOnly":"NoFarmer";
                Count("Source."+sourceKind);
                if(sourceKind=="NoFarmer")continue;
                int probe=regionalFarmer?r.Culture:ownerFarmer?ownerCulture:teacher[from];
                int targetRawOwner=HistoricalCells.Polity(s,arrival),targetOwner=targetRawOwner>=0?a.Resolve(targetRawOwner,h):-1;
                int targetCellCulture=HistoricalCells.Culture(s,arrival),targetCulture=destination.Culture;
                bool anonymous=targetCulture<0&&targetCellCulture<0;
                string targetKind=targetOwner>=0?"Owned":anonymous?"Anonymous":Farmer(targetCulture)>=180?"ExistingFarmerCulture":"ExistingNonFarmerCulture";
                double yield=(double)Call("LivingCropLandscape",s,probe,arrival,false),fit=(double)Call("AggregateCropSupport",s,probe,arrival,false);
                bool gate=(bool)Call("InstitutionalRouteAllowed",s,departure,arrival,probe);
                long fields=(long)Call("ProvincialFarmPotential",s,arrival,probe);
                long capacity=targetCulture>=0?(long)Call("ProvincialRegionalCapacity",s,to,targetCulture):destination.Capacity;
                long ordinaryRoom=Math.Max(0,capacity-s.Population[to]);
                long anonymousAllowance=regionalFarmer&&Farmer(r.Culture)>=250&&targetCulture<0&&yield>=.12?Math.Min(500,(long)(fields*D.Clamp(Farmer(r.Culture)/600.0))):0;
                long legacyRoom=Math.Max(0,Math.Max(capacity,destination.Capacity+anonymousAllowance)-s.Population[to]);
                long farmFieldRoom=a.Empires.Connections.Realms.Version>=12&&(regionalFarmer||ownerFarmer)
                    ?(long)Call("FarmFrontierRoom",s,to,arrival,probe,0L):0;
                long currentRoom=Math.Max(legacyRoom,farmFieldRoom);
                bool rawEligible=s.Population[from]>=125,quality=yield>=.12&&gate;
                Count("Recipient."+sourceKind+"."+targetKind);
                if(targetOwner<0&&quality)
                {
                    Count("CropGate."+sourceKind+"."+targetKind);
                    if(fields>=400)Count("FreeFields."+sourceKind+"."+targetKind);
                    if(rawEligible&&currentRoom>=50&&regionalFarmer)Count("CurrentRawAndRoom."+targetKind);
                    if(rawEligible&&currentRoom>=25&&regionalFarmer)Count("CurrentRawAndRoom25."+targetKind);
                    if(!rawEligible&&ownerFarmer)Count("RemoteButRawExcluded."+targetKind);
                    if(targetKind=="ExistingNonFarmerCulture"&&currentRoom<50&&fields>=400)Count("ExistingNonFarmerFullDespiteFarmFields");
                }
                var habitat=world.Ecology.Habitat[arrival];
                double bestTransmission=(double)Call("EnvironmentalTransmission",s,r.BestCell,destination.BestCell,probe,false);
                edges.Add(new{Route=routeId,From=from,To=to,DepartureCell=departure,ArrivalCell=arrival,Latitude=Lat(arrival),Longitude=Lon(arrival),
                    SourceKind=sourceKind,ProbeCulture=probe,ProbeFarming=Farmer(probe),SourceLocalCulture=r.Culture,SourceLocalFarming=Farmer(r.Culture),SourceTeacher=teacher[from],SourceTeacherFarming=Farmer(teacher[from]),
                    SourceOwner=owner,SourceOwnerCulture=ownerCulture,SourceCohort=cohort,SourceRemote=remote,SourceRaw=s.Population[from],SourceContact=contacts[from],SourceRawEligible=rawEligible,
                    SourceClaimedAreaKm2=a.Provinces.ClaimedAreaKm2[from],TargetKind=targetKind,TargetOwner=targetOwner,TargetCulture=targetCulture,TargetCellCulture=targetCellCulture,TargetFarming=Farmer(targetCulture),
                    TargetRaw=s.Population[to],TargetNativeCapacity=destination.Capacity,TargetCurrentCapacity=capacity,OrdinaryRoom=ordinaryRoom,AnonymousFarmAllowance=anonymousAllowance,
                    LegacyCurrentRoom=legacyRoom,FarmFieldRoom=farmFieldRoom,CurrentRoom=currentRoom,
                    TargetRecognizedClaim=HistoricalRecognizedClaims.Holder(s,arrival),CropYield=yield,CarriedFit=fit,RouteAllowed=gate,BestCellTransmission=bestTransmission,PotentialFreeFields=fields,
                    Climate=world.Climate.Classification[arrival].ToString(),Biome=habitat.Biome.ToString(),habitat.GrowingDays,habitat.WarmTenthsC,habitat.PermafrostPermille,habitat.ReliefM});
            }
        }
        var births=h.Archive.Where(e=>e.Kind=="migrant-community"&&world.Landmass[e.Cell]==landmass).Select(e=>new{e.Id,e.Year,e.CultureId,Parent=h.Cultures[e.CultureId].ParentCultureId,e.Cell,Latitude=Lat(e.Cell),Longitude=Lon(e.Cell)}).ToArray();
        var frontiers=h.Archive.Where(e=>e.Kind=="migrant-frontier"&&world.Landmass[e.Cell]==landmass).Select(e=>new{e.Id,e.Year,e.CultureId,e.Cell,Latitude=Lat(e.Cell),Longitude=Lon(e.Cell),e.Text}).ToArray();
        if(original.ComputeHash()!=before)throw new Exception("Dispersal audit changed its published source.");
        string assembly=typeof(Prehistory).Assembly.Location;
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=Path.GetFullPath(source),Replay=replayYear>=0,Year=s.Years,Hash=before,Realm=a.Empires.Connections.Realms.Version,
            CoreAssembly=assembly,CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),InspectedFirstStateLandmass=landmass,Counts=counts,Edges=edges,MigrantBirths=births,RemoteFrontierEvents=frontiers,
            Notes=new[]{"Explicit offline directed-land-edge census; no physics or input-world changes. Source landmass is selected from the actual first court, not a required homeland.",
                "TeacherOnly is a memory of farming knowledge, not actual farmer households or a debitable population account. OwnerRemoteFarmer is separate from the current regional raw departure pool.",
                "CurrentRawAndRoom uses a50-person receiving-room screen; CurrentRawAndRoom25 uses the ordinary25-person minimum. Neither is a simulated migration: stochastic choice, residence time, crop practice and food conservation still matter.",
                "Free-field and crop-quality tests are separate. LegacyCurrentRoom mirrors the older anonymous-only farm allowance; realm12+ CurrentRoom additionally calls the frozen engine's actual FarmFrontierRoom helper. OwnerRemoteFarmer remains a source proxy, not a full service/payment eligibility result.",
                "RemoteFrontierEvents are actual paid frontier-departure archive records, not all new community births. MigrantBirths is the separate migrant-community event kind; do not conflate event counts or infer an unrecorded exact departure route.",
                "Region contact counts apportion shared Remote ledgers. Do not sum repeated SourceRemote values or debit contact estimates."}
        },new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine($"Dispersal audit {s.Years}: {edges.Count} farmer/teacher source edges, {births.Length} birth events, preserved {before}");
    }
}
