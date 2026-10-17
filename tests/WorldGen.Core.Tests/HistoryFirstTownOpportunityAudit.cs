using System.Reflection;
using System.Text.Json;
using WorldGen.Core;

// Offline CURRENT actor/field evidence. Never calls a founding, exchange,
// migration, delivery, selection, random roll or simulation-advance handler.
public static class HistoryFirstTownOpportunityAudit
{
    public static void RunLoaded(World world,string source,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new first-town audit output.");
        var original=world.Prehistory;string before=original.ComputeHash();var s=original.Copy();var h=s.History;var a=h.Aggregates;
        long reads=a.CellReads,writes=a.CellWrites,events=a.ProcessedLocalEvents;var engine=new Prehistory(world);
        object Call(string name,params object[] args)
        {
            var method=typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!;
            if(args.Length<method.GetParameters().Length)args=args.Concat(Enumerable.Repeat<object>(Type.Missing,method.GetParameters().Length-args.Length)).ToArray();
            return method.Invoke(engine,args);
        }
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
        var river=(double[])typeof(Prehistory).GetField("riverSites",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;
        var access=(double[])typeof(Prehistory).GetField("riverAccess",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;
        double Lat(int cell)=>Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI;
        double Lon(int cell)=>Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI;
        object Place(int cell)=>new{Cell=cell,Latitude=Lat(cell),Longitude=Lon(cell),Region=engine.CellPatch[cell],Biome=world.Ecology.Habitat[cell].Biome.ToString(),Climate=world.Climate.Classification[cell].ToString()};
        var anchors=new List<(string Name,int Cell,int Region,int Local,int Regional)>();
        foreach(var target in new[]{("Levant",32.0,35.0),("Iraq",33.0,44.0),("Iran",33.0,52.0),("Indus",29.0,70.0),("Gujarat",23.0,72.0),("Ganges",26.0,82.0),("Deccan",18.0,77.0),("Bengal",23.0,90.0)})
        {
            double lat=target.Item2*Math.PI/180,lon=target.Item3*Math.PI/180;var point=new V(Math.Cos(lat)*Math.Cos(lon),Math.Cos(lat)*Math.Sin(lon),Math.Sin(lat));
            int cell=Enumerable.Range(0,world.Surface.Cells.Length).Where(c=>engine.CellPatch[c]>=0&&!world.Hydrology.OceanConnected[c]).OrderByDescending(c=>V.Dot(point,world.Surface.Cells[c].Position)).ThenBy(c=>c).First();
            int region=engine.CellPatch[cell];anchors.Add((target.Item1,cell,region,HistoricalCells.Culture(s,cell),a.Regions[region].Culture));
        }
        object Field(int culture,int cell)
        {
            int region=engine.CellPatch[cell];var habitat=(HabitatCell)Call("CropHabitat",s,cell);var crops=h.Agriculture[culture];var hc=h.Cultures[culture];
            var holder=(HistoricalTownSeeds)Call("LocalSeedHolder",s,culture,cell,-1);bool managed=HistoricalTechnologies.Has(hc,HistoricalTechnology.CropRotation);
            double yield=(double)Call("FoundingFieldLandscape",s,culture,cell,-1),stored=(double)Call("CarriedCropLandscape",s,culture,cell,true,-1),environment=(double)Call("EnvironmentalEstablishment",s,cell,culture);
            int practice=(int)Call("CarriedCropPractice",s,culture,cell,-1),owner=HistoricalCells.Polity(s,cell);
            double fraction=Math.Min(1,Math.Max(.08,D.Clamp((habitat.AridityMilli-100)/550.0)+access[cell]));
            double low=new[]{world.Climate.NorthernSpring.PrecipitationMm[cell],world.Climate.NorthernSummer.PrecipitationMm[cell],world.Climate.NorthernAutumn.PrecipitationMm[cell],world.Climate.NorthernWinter.PrecipitationMm[cell]}.Min();
            double high=new[]{world.Climate.NorthernSpring.PrecipitationMm[cell],world.Climate.NorthernSummer.PrecipitationMm[cell],world.Climate.NorthernAutumn.PrecipitationMm[cell],world.Climate.NorthernWinter.PrecipitationMm[cell]}.Max(),season=(high-low)/Math.Max(1,high);
            double best=0;var groups=new List<object>();
            for(int k=0;k<6;k++)
            {
                var seed=(HistoricalCultivar)Call("LocalSeedProfile",s,culture,k,holder);bool active=crops.AdoptedYear[k]>0||holder?[k]!=null;
                int group=seed?.CropId==10?6:seed?.CropId==12?7:k;var areaHabitat=(HabitatCell)Call("CommunityAgronomicHabitat",s,cell,group);
                double fit=seed==null?(double)Call("CommunityWildCropFit",s,cell,k):(double)Call("CommunityTransferFit",s,cell,seed,managed);
                double area=(double)Call("CommunityAreaYield",s,cell,group,areaHabitat,season),quality=D.Clamp(area/fraction);
                int years=(int)Call("LocalSeedPracticeAt",s,culture,k,holder,cell,true);
                if(HistoricalLandscapeEffects.EverWet(habitat))quality=Math.Max(quality,(managed?.35:.20*D.Clamp(years/15000.0))*HistoricalCropCatalog.LeachingPenalty(habitat,managed));
                double contribution=active&&(holder==null||seed!=null)?quality*fit:0;best=Math.Max(best,contribution);
                object physiological=null;
                if(seed!=null)
                {
                    var def=HistoricalCropCatalog.Crops[seed.CropId];int duration=a.Empires.Connections.Realms.Version>=22&&def.Group!=4?Math.Min(365,seed.MaturityDays):0;
                    var cropHabitat=(HabitatCell)Call("CommunityCalendarHabitat",s,cell,group,duration);
                    double species=HistoricalCropSpeciesNiches.TransferFit(cropHabitat,seed,Math.Abs(Lat(cell)),river[cell],managed),reproductive=HistoricalCropEcology.ReproductiveFit(cropHabitat,seed),transfer=(double)Call("EnvironmentalCropTransfer",s,cell,seed);
                    if(D.Q(species*reproductive*transfer,1000000)!=fit)throw new Exception("Actual first-town crop fit decomposition failed.");
                    physiological=new{CalendarDays=duration,WindowWarmth=cropHabitat.WarmTenthsC,cropHabitat.GrowingDays,cropHabitat.PermafrostPermille,cropHabitat.AridityMilli,
                        RiverSites=river[cell],SpeciesFit=species,ReproductiveFactor=reproductive,EnvironmentalTransfer=transfer,ExactFit=fit};
                }
                groups.Add(new{Group=k,Active=active,AdoptedYear=crops.AdoptedYear[k],SourceCulture=crops.SourceCulture[k],WildOriginCell=crops.WildOriginCell[k],CurrentPractice=crops.Practice[k],HusbandryAtField=years,
                    ActualProfile=seed,HolderTown=holder?.TownId,LotAcquiredYear=holder?[k]?.AcquiredYear,LotSourceCell=holder?[k]?.SourceCell,AreaYield=area,NormalizedAreaQuality=quality,Fit=fit,UnstoredContribution=contribution,Physiology=physiological});
            }
            if(D.Q(D.Clamp(best),1000000)!=yield)throw new Exception("Actual first-town field landscape reconstruction failed.");
            double rail=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Railways,s.Years),spacing=(100-45*rail)*Math.Sqrt(Math.Min(4,Math.Max(1,.25/Math.Max(.02,yield))));
            int bucket=(int)typeof(Prehistory).GetMethod("LivingTownBucket",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{world.Surface.Cells[cell].Position})!;
            bool modernSupply=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years)>.3;
            long available=s.Population[region]<800&&owner>=0?a.Polities[owner].Remote:s.Population[region];
            return new{Place=Place(cell),CanFoundUnparented=(bool)Call("CanFoundLivingTown",s,culture,cell,-1),FieldYield=yield,StoredYield=stored,CarriedPractice=practice,
                Pre24ComfortableFieldAllowed=HistoricalFarmsteadEstablishment.FieldAllowed(23,-1,yield,practice),ActualFieldAllowed=HistoricalFarmsteadEstablishment.FieldAllowed(a.Empires.Connections.Realms.Version,-1,yield,practice),
                MarginalOpportunityMultiplier=HistoricalFarmsteadEstablishment.Opportunity(yield,practice),HabitatColdGate=(habitat.GrowingDays>=90&&habitat.WarmTenthsC>=80&&habitat.PermafrostPermille<=700)||modernSupply,
                ProtocentreMaximum=HistoricalProtocentreEffects.MaximumResidents(environment,stored,hc.Institution,0),Environment=environment,WorldLakeCoverage=world.Hydrology.LakeCoverageFraction[cell],
                RegionalRawPeople=s.Population[region],ActualTerritorialOwner=owner,ActualOwnerRemote=owner>=0?a.Polities[owner].Remote:0,AvailableUnparentedPool=available,
                InitialUrban=Math.Min(250,available/12),InitialRural=Math.Min(available/3,Math.Max(800,Math.Min(250,available/12)*8)),
                TownAt=a.TownAt[cell],BucketCount=a.Living.BucketCounts[bucket],SpacingKm=spacing,NearbyTowns=(int[])Call("NearbyLivingTowns",s,cell,spacing),
                CommittedArea=a.Living.Regions[region].CommittedAreaKm2,RegionalArea=a.Regions[region].AreaKm2,NormalizedCultivableFraction=fraction,Crops=groups};
        }
        var cultures=new List<object>();
        foreach(int id in anchors.Select(t=>t.Local>=0?t.Local:t.Regional).Where(id=>id>=0).Distinct().Take(8))
        {
            var c=s.Society.Cultures[id];var crops=h.Agriculture[id];int chosen=(int)Call("DevelopmentEconomicSite",s,id,c.OriginCell);
            double investment=HistoricalTechnicalMigration.SettlementInvestment(h.Cultures[id],s.Years);int maturity=Math.Max(5,(int)((h.Cultures[id].ParentCultureId>=0?100:1000)/investment));
            var cells=new[]{c.OriginCell,chosen}.Concat(crops.PracticeCell).Concat(anchors.Where(t=>t.Local==id||t.Regional==id).Select(t=>t.Cell)).Distinct().Where(cell=>cell>=0&&engine.CellPatch[cell]>=0).ToArray();
            cultures.Add(new{Id=id,c.Name,c.Farming,c.FoundedYear,c.OriginCell,Age=s.Years-c.FoundedYear,ActualCities=a.Cultures[id].Cities.Count,SupportedCities=a.Cultures[id].SupportedCities,
                a.Cultures[id].Rural,a.Cultures[id].SupportedUrban,ChosenSite=chosen,Investment=investment,MaturationYears=maturity,FirstOpportunityEligible=a.Cultures[id].Cities.Count==0&&c.Farming>=180&&s.Years-c.FoundedYear>=maturity,
                crops.NativeMask,PracticeCells=crops.PracticeCell,Fields=cells.Select(cell=>Field(id,cell)).ToArray()});
        }
        var places=new List<object>();
        foreach(var target in anchors)
        {
            var r=a.Regions[target.Region];var life=a.Living.Regions[target.Region];var contacts=new List<object>();int receiver=target.Local>=0?target.Local:target.Regional;
            foreach(int routeId in r.Routes.Where(id=>!engine.Routes[id].Sea).Take(8))
            {
                var route=engine.Routes[routeId];int from=route.A==target.Region?route.B:route.A;
                int departure=(int)Call("RegionalContactCell",s,route,from),arrival=(int)Call("RegionalContactCell",s,route,target.Region);
                foreach(int donor in new[]{a.Regions[from].Culture,(int)Call("DevelopmentRegionalTeacher",s,from),a.Living.Regions[from].Teacher}.Where(id=>id>=0).Distinct())
                {
                    int receipt=-1,direct=-1;if(receiver>=0){var memory=a.Provinces.Exchanges[receiver];int at=Array.IndexOf(memory.Peers,donor);if(at>=0){receipt=memory.LastYear[at];direct=memory.LastDirectYear[at];}}
                    var holder=(HistoricalTownSeeds)Call("LocalSeedHolder",s,donor,departure,-1);
                    contacts.Add(new{Route=routeId,FromRegion=from,PhysicalRegionalCulture=a.Regions[from].Culture,Donor=donor,DonorFarming=s.Society.Cultures[donor].Farming,
                        SourceResidents=(long)Call("ProvincialContactResidents",s,from,false),RecipientResidents=(long)Call("ProvincialContactResidents",s,target.Region,false),
                        Departure=Place(departure),Arrival=Place(arrival),BoundaryAllowed=(bool)Call("InstitutionalRouteAllowed",s,departure,arrival,donor),
                        ActualRegionalRelayDistance=(double)Call("RegionalLandRelayDistance",s,route,donor,from,target.Region,departure,arrival),
                        BoundaryTransmission=(double)Call("EnvironmentalTransmission",s,departure,arrival,donor,false),ArrivalToRequestedFieldTransmission=(double)Call("EnvironmentalTransmission",s,arrival,target.Cell,donor,false),
                        LandTransport=HistoricalModernEffects.LandTransport(h.Cultures[donor],s.Years),LastKnowledgeReceipt=receipt,LastDirectReceipt=direct,CurrentHolderTown=holder?.TownId,
                        Crops=Enumerable.Range(0,6).Select(k=>{var seed=(HistoricalCultivar)Call("LocalSeedProfile",s,donor,k,holder);return new{Group=k,AdoptedYear=h.Agriculture[donor].AdoptedYear[k],ActualProfile=seed,LotAcquiredYear=holder?[k]?.AcquiredYear,
                            ArrivalFit=seed==null?0:(double)Call("CommunityTransferFit",s,arrival,seed,false),RequestedFieldFit=seed==null?0:(double)Call("CommunityTransferFit",s,target.Cell,seed,false)};}).ToArray()});
                }
            }
            places.Add(new{target.Name,ActualFineField=Place(target.Cell),LocalPhysicalCulture=target.Local,RegionalCulture=target.Regional,RegionalBestCell=Place(r.BestCell),
                RawPopulation=s.Population[target.Region],RegionalContactResidents=(long)Call("ProvincialContactResidents",s,target.Region,false),life.Teacher,life.Exposure,life.LastExchangeYear,
                ActualRegionalTeacher=(int)Call("DevelopmentRegionalTeacher",s,target.Region),Contacts=contacts});
        }
        long delta=a.CellReads-reads;a.CellReads=reads;
        if(original.ComputeHash()!=before||s.ComputeHash()!=before||a.CellWrites!=writes||a.ProcessedLocalEvents!=events)throw new Exception("First-town opportunity audit changed source/probe.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=Path.GetFullPath(source),Year=s.Years,Hash=before,Realm=a.Empires.Connections.Realms.Version,DiagnosticReadDelta=delta,Cultures=cultures,Places=places,
            Notes=new[]{"CURRENT saved-state evidence only. No WorldFile read, upgrade, advancement, founding/delivery/migration handler, random roll or world write is performed by this helper.",
                "At most eight requested fine-field anchors choose up to eight CURRENT physical/regional cultures, never hardcoded earlier culture461. RegionalBestCell is separately labeled and is not assumed to be the requested river valley.",
                "Every unparented gate is queried with actual source-specific carried seed holders; field quality and physiological/source factors reconstruct the actual current package yield. No paid clearing is granted to a first village.",
                "The old10000-practice comfortable-field exception is shown separately from the active24 marginal first-village rule; the latter is probabilistic and still not a proto-city or state guarantee.",
                "Current profiles, source holders, route allowance, contact dates and available farmer pools must NOT be backdated to an earlier era. A general knowledge receipt/teacher memory is not a dated genotype shipment or actual migrating household.",
                "Contact loops inspect only the first eight stored adjacent land routes per anchor; they are bounded current samples, not an exhaustive route/world census. Original and detached hashes remain unchanged after restoring only diagnostic reads."}},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine("First-town opportunities:8 current fine fields, "+cultures.Count+" actual cultures, unchanged "+before);
    }
}
