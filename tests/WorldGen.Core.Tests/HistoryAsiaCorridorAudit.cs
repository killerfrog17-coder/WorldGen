using System.Reflection;
using System.Text.Json;
using WorldGen.Core;

// Bounded offline current-field evidence, never a migration/delivery handler.
public static class HistoryAsiaCorridorAudit
{
    public static void RunLoaded(World world,string source,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new Asia corridor output.");
        var original=world.Prehistory;string before=original.ComputeHash();var s=original.Copy();
        var h=s.History;var a=h.Aggregates;long reads=a.CellReads,writes=a.CellWrites,events=a.ProcessedLocalEvents;
        var engine=new Prehistory(world);
        object Call(string name,params object[] args)
        {
            var method=typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance)!;
            if(args.Length<method.GetParameters().Length)args=args.Concat(Enumerable.Repeat<object>(Type.Missing,method.GetParameters().Length-args.Length)).ToArray();
            return method.Invoke(engine,args);
        }
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
        var riverSites=(double[])typeof(Prehistory).GetField("riverSites",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(engine)!;
        double Lat(int cell)=>Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI;
        double Lon(int cell)=>Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI;
        double Km(int first,int second)=>D.Angle(world.Surface.Cells[first].Position,world.Surface.Cells[second].Position)*world.Recipe.RadiusKm;
        object Place(int cell)=>new{Cell=cell,Latitude=Lat(cell),Longitude=Lon(cell),Climate=world.Climate.Classification[cell].ToString(),Biome=world.Ecology.Habitat[cell].Biome.ToString(),Landmass=world.Landmass[cell],
            ActualHabitat=world.Ecology.Habitat[cell],SeasonalTemperatureTenths=new[]{world.Climate.NorthernSpring.TemperatureTenthsC[cell],world.Climate.NorthernSummer.TemperatureTenthsC[cell],world.Climate.NorthernAutumn.TemperatureTenthsC[cell],world.Climate.NorthernWinter.TemperatureTenthsC[cell]}};
        var active=s.Society.Settlements.Where(t=>t.Population>0&&a.Living.Cities[t.Id].AbandonedYear<0).ToArray();
        int Farming(int culture)=>culture>=0?s.Society.Cultures[culture].Farming:0;
        object Region(int id)
        {
            var r=a.Regions[id];var l=a.Living.Regions[id];int culture=r.Culture;
            return new{Id=id,Place=Place(r.BestCell),RawPopulation=s.Population[id],ContactResidents=(long)Call("ProvincialContactResidents",s,id,false),l.SettledPopulation,
                r.AreaKm2,r.Capacity,r.NativeMask,Culture=culture,Farming=Farming(culture),l.Teacher,l.Exposure,l.LastExchangeYear,l.CivicTeacher,l.CivicExposure,
                ActualRegionalTeacher=(int)Call("DevelopmentRegionalTeacher",s,id),
                Crops=culture>=0?Enumerable.Range(0,6).Select(k=>new{Group=k,AdoptedYear=h.Agriculture[culture].AdoptedYear[k],Practice=h.Agriculture[culture].Practice[k],PracticeCell=h.Agriculture[culture].PracticeCell[k],Profile=h.Agriculture[culture].Cultivars?[k]}).ToArray():null,
                Towns=active.Where(t=>engine.CellPatch[t.Cell]==id).Select(t=>new{t.Id,t.Name,t.Population,t.CultureId,t.FoundedYear,Place=Place(t.Cell)}).ToArray()};
        }
        object Context(string kind,int from,int to,int departure,int arrival,int targetField,int donor,int route,int explicitTown=-1)
        {
            int receiver=a.Regions[to].Culture;var holder=explicitTown>=0?engine.LocalSeedsAt(s,explicitTown):(HistoricalTownSeeds)Call("LocalSeedHolder",s,donor,departure,-1);
            int receipt=-1,direct=-1;
            if(receiver>=0){var exchanges=a.Provinces.Exchanges[receiver];int at=Array.IndexOf(exchanges.Peers,donor);if(at>=0){receipt=exchanges.LastYear[at];direct=exchanges.LastDirectYear[at];}}
            var groups=Enumerable.Range(0,6).Select(k=>
            {
                var global=h.Agriculture[donor].Cultivars?[k];var lot=holder?[k];var local=lot?.Cultivar;
                int practice=receiver>=0?h.Agriculture[receiver].PracticeCell[k]:arrival;
                double Fit(HistoricalCultivar seed,int cell)=>seed==null?0:(double)Call("CommunityTransferFit",s,cell,seed,false);
                object Parts(HistoricalCultivar seed,int cell)
                {
                    if(seed==null)return null;
                    var definition=HistoricalCropCatalog.Crops[seed.CropId];int group=seed.CropId==10?6:seed.CropId==12?7:definition.Group;
                    int duration=a.Empires.Connections.Realms.Version>=22&&definition.Group!=4?Math.Min(365,seed.MaturityDays):0;
                    var habitat=(HabitatCell)(typeof(Prehistory).GetMethod("CommunityCalendarHabitat",BindingFlags.NonPublic|BindingFlags.Instance)!=null
                        ?Call("CommunityCalendarHabitat",s,cell,group,duration):Call("CommunityAgronomicHabitat",s,cell,group));
                    double latitude=Math.Abs(Lat(cell)),river=riverSites[cell],environment=(double)Call("EnvironmentalCropTransfer",s,cell,seed);
                    double physiology=HistoricalCropCatalog.Fit(habitat,seed,latitude,river);
                    double transfer=HistoricalCropSpeciesNiches.TransferFit(habitat,seed,latitude,river,false),reproduction=HistoricalCropEcology.ReproductiveFit(habitat,seed);
                    double actual=Fit(seed,cell),reconstructed=D.Q(transfer*reproduction*environment,1000000);
                    if(reconstructed!=actual)throw new Exception("Actual source-specific corridor fit decomposition failed.");
                    double water=habitat.AridityMilli;
                    bool fresh=habitat.Flooding==FloodRegime.RiverSeasonal||habitat.Flooding==FloodRegime.RiverPersistent||river>.2;if(fresh)water=Math.Max(water,900);
                    double minimum=seed.MinimumWarmTenthsC==0?definition.MinimumWarmC:seed.MinimumWarmTenthsC/10.0;
                    return new{Place=Place(cell),CalendarDurationDays=duration,WindowWarmTenths=habitat.WarmTenthsC,
                        CatalogSeason=D.Clamp((habitat.GrowingDays-seed.MaturityDays+50)/70.0),CatalogWarmth=D.Clamp((habitat.WarmTenthsC/10.0-minimum+3)/8.0),
                        CatalogMoisture=D.Clamp(water/Math.Max(1,seed.WaterRequirementPermille)),FreshwaterOverride=fresh,RiverSites=river,
                        CatalogPhotoperiod=definition.DaylengthSensitive?Math.Exp(-Math.Abs(latitude*10-seed.DaylengthLatitudeTenths)/100.0):1,
                        CatalogFrozen=D.Clamp(1-habitat.PermafrostPermille/700.0),Physiology=physiology,SpeciesTransferFit=transfer,
                        ReproductiveFactor=reproduction,EnvironmentalFactor=environment,AbioticPhysical=HistoricalCrops.AbioticPotential(habitat,river),ActualFit=actual,ReconstructedFit=reconstructed};
                }
                int chosen=receiver>=0?(int)Call("DevelopmentCropDonor",s,receiver,k,practice,donor):-1;
                return new{Group=k,DonorAdoptedYear=h.Agriculture[donor].AdoptedYear[k],GlobalProfile=global,LocalHeldProfile=local,
                    GlobalDepartureFit=Fit(global,departure),LocalDepartureFit=Fit(local,departure),GlobalArrivalFit=Fit(global,arrival),LocalArrivalFit=Fit(local,arrival),
                    GlobalTargetFieldFit=Fit(global,targetField),LocalTargetFieldFit=Fit(local,targetField),
                    GlobalTargetDecomposition=Parts(global,targetField),LocalTargetDecomposition=Parts(local,targetField),
                    RecipientPracticePlace=Place(practice),GlobalPracticeFit=Fit(global,practice),LocalPracticeFit=Fit(local,practice),ActualDevelopmentDonor=chosen,
                    ActualChosenFit=chosen>=0?Fit(h.Agriculture[chosen].Cultivars?[k],practice):0,
                    LotAcquiredYear=lot?.AcquiredYear,LotSourceTown=lot?.SourceTown,LotSourceCell=lot?.SourceCell,LotLastPracticeYear=lot?.LastPracticeYear,LotPracticeYears=lot?.PracticeYears};
            }).ToArray();
            int town=holder?.TownId??-1;var t=town>=0?s.Society.Settlements[town]:null;
            return new{Kind=kind,Route=route,From=from,To=to,Departure=Place(departure),Arrival=Place(arrival),Donor=donor,DonorName=s.Society.Cultures[donor].Name,DonorFarming=Farming(donor),Receiver=receiver,
                BoundaryAllowed=(bool)Call("InstitutionalRouteAllowed",s,departure,arrival,donor),Transmission=(double)Call("EnvironmentalTransmission",s,departure,arrival,donor,false),
                SourceResidents=(long)Call("ProvincialContactResidents",s,from,false),RecipientResidents=(long)Call("ProvincialContactResidents",s,to,false),
                LastKnowledgeReceipt=receipt,LastDirectReceipt=direct,HolderTown=town,HolderField=holder?.FieldCell,
                HolderLive=t!=null&&t.Population>0&&a.Living.Cities[town].AbandonedYear<0,HolderSupported=t!=null&&a.Cities[town].Supported,
                HolderPopulation=t?.Population,HolderFood=t?.FoodReserve,CurrentTravelKm=(double)Call("LivingTravelKm",s,departure,arrival,donor),
                CarriedPackageYield=(double)Call("CommunitySeededCropLandscape",s,donor,arrival,false,holder,true),Groups=groups};
        }
        var fields=new List<object>();
        foreach(var target in new[]{("Levant",32.0,35.0),("Iraq",33.0,44.0),("Iran",33.0,52.0),("Indus",29.0,70.0),("Gujarat",23.0,72.0),("Ganges",26.0,82.0)})
        {
            double latitude=target.Item2*Math.PI/180,longitude=target.Item3*Math.PI/180;var point=new V(Math.Cos(latitude)*Math.Cos(longitude),Math.Cos(latitude)*Math.Sin(longitude),Math.Sin(latitude));
            // Select the actual fine field first: a regional BestCell may be
            // hundreds of kilometers from its lowland member cells.
            int anchor=Enumerable.Range(0,world.Surface.Cells.Length).Where(cell=>!world.Hydrology.OceanConnected[cell])
                .OrderByDescending(cell=>V.Dot(point,world.Surface.Cells[cell].Position)).ThenBy(cell=>cell).First();
            int region=engine.CellPatch[anchor];
            var contexts=new List<object>();var neighborIds=new HashSet<int>();
            foreach(int id in a.Regions[region].Routes)
            {
                var route=engine.Routes[id];if(route.Sea)continue;int from=route.A==region?route.B:route.A;neighborIds.Add(from);
                int departure=(int)Call("RegionalContactCell",s,route,from),arrival=(int)Call("RegionalContactCell",s,route,region),owner=HistoricalCells.Polity(s,departure);
                foreach(int donor in new[]{a.Regions[from].Culture,(int)Call("DevelopmentRegionalTeacher",s,from),a.Living.Regions[from].Teacher,owner>=0?h.Polities[owner].CultureId:-1}.Where(c=>c>=0).Distinct())
                    contexts.Add(Context("Actual adjacent land route",from,region,departure,arrival,anchor,donor,id));
            }
            foreach(var town in active.Where(t=>Farming(t.CultureId)>=180&&world.Landmass[t.Cell]==world.Landmass[anchor]).OrderBy(t=>Km(t.Cell,anchor)).ThenBy(t=>t.Id).Take(3))
                contexts.Add(Context("Nearest current farmer town; distance is not delivery proof",engine.CellPatch[town.Cell],region,town.Cell,anchor,anchor,town.CultureId,-1,town.Id));
            fields.Add(new{Name=target.Item1,RequestedLatitude=target.Item2,RequestedLongitude=target.Item3,ActualNearestFineField=Place(anchor),TargetRegion=Region(region),CurrentAdjacentRegions=neighborIds.OrderBy(id=>id).Select(Region).ToArray(),Contexts=contexts});
        }
        long delta=a.CellReads-reads;a.CellReads=reads;
        if(original.ComputeHash()!=before||s.ComputeHash()!=before||a.CellWrites!=writes||a.ProcessedLocalEvents!=events)throw new Exception("Asia corridor probe altered authoritative state.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=source,Year=s.Years,Hash=before,Realm=a.Empires.Connections.Realms.Version,DiagnosticReadDelta=delta,Fields=fields,
            Notes=new[]{"Current final-state bounded offline evidence only; no delivery, migration, adoption, upgrade, replay or world write. Original and detached probe hashes preserved after normalizing only the diagnostic read counter.",
                "ActualNearestFineField selects nearest accepted non-ocean fine cell before finding its containing region. TargetRegion.Place separately reports a possibly distant BestCell: do not substitute it for the requested field. Physical route contexts use actual oriented boundary endpoints, with arrival versus target-field fits separately exported.",
                "Current nearest farmer town and current carrier/profile/fit availability must NOT be backdated to Early Modern. Adopted, acquired, practice and knowledge dates are exported separately; generic knowledge contact does not certify a crop genotype shipment.",
                "Teacher memories are not proof of living farming households. Current nearest-town contexts may be too distant or unfunded; no mission is performed or inferred.",
                "Fit is actual current unmodified CommunityTransferFit with managed=false; held seed yield is a separate package metric, not proof of founder capacity or paid field clearance."}},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine($"Asia corridor {s.Years}:6 bounded fields, unchanged {before}");
    }
}
