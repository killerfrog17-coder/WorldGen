using System.Reflection;
using System.Text.Json;
using WorldGen.Core;

// Follow-up CURRENT endpoint/account inspection. No world loading, mutating
// migration/founding/recognition handler, random draw or future replay.
public static class HistoryFarmerFrontierOpportunityAudit
{
    public static void RunLoaded(World world,string source,string priorFirstTownJson,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new farmer-frontier audit output.");
        var original=world.Prehistory;string before=original.ComputeHash();
        using var prior=JsonDocument.Parse(File.ReadAllText(priorFirstTownJson));
        if(prior.RootElement.GetProperty("Hash").GetString()!=before)throw new ArgumentException("Fine anchors must come from this exact saved checkpoint.");
        var s=original.Copy();var h=s.History;var a=h.Aggregates;long reads=a.CellReads,writes=a.CellWrites,events=a.ProcessedLocalEvents;var engine=new Prehistory(world);
        object Call(string name,params object[] args)
        {
            var method=typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static)!;
            if(args.Length<method.GetParameters().Length)args=args.Concat(Enumerable.Repeat<object>(Type.Missing,method.GetParameters().Length-args.Length)).ToArray();
            return method.Invoke(engine,args);
        }
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
        var area=(double[])typeof(Prehistory).GetField("inhabitableArea",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(engine)!;
        var rivers=(double[])typeof(Prehistory).GetField("riverAccess",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(engine)!;
        object Place(int cell)=>new{Cell=cell,Region=engine.CellPatch[cell],Latitude=Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI,Longitude=Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI};
        object Finite(double value)=>double.IsNaN(value)||double.IsInfinity(value)?(object)value.ToString(System.Globalization.CultureInfo.InvariantCulture):value;
        double Q(object quote,string field)=>quote==null?0:Convert.ToDouble(quote.GetType().GetField(field)!.GetValue(quote));
        object Quote(object quote)=>quote==null?null:quote.GetType().GetFields().ToDictionary(f=>f.Name,f=>f.GetValue(quote));
        var anchors=prior.RootElement.GetProperty("Places").EnumerateArray().Take(12).Select(p=>(Name:p.GetProperty("Name").GetString(),Cell:p.GetProperty("ActualFineField").GetProperty("Cell").GetInt32())).ToArray();
        var regions=anchors.Select(p=>engine.CellPatch[p.Cell]).Distinct().ToArray();
        object RecognitionAt(int region,int cell,double actualBestProbability)
        {
            double[] Field(string name)=>(double[])typeof(Prehistory).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(engine)!;
            var local=a.Living.Regions[region];var habitat=world.Ecology.Habitat[cell];
            bool connected=(bool)Call("ConnectedHistory",s),colonial=(bool)Call("ColonialHistory",s),environmental=(bool)Call("EnvironmentalHistory",s),living=(bool)Call("LivingWorldHistory",s);
            bool coast=world.Surface.Cells[cell].Neighbors.Any(n=>world.Hydrology.OceanConnected[n]);
            double season=Field("landscapeSeason")[cell],storage=Field(connected?"developmentStored":"landscapeStored")[cell];
            double cropStorage=Field("wildCropStorage")[cell]*(double)Call("LandscapeCropFactor",cell)*(connected?HistoricalDevelopmentEffects.GrowingSeason(habitat):1);
            double concentration=Math.Max(connected?.35+.35*season:season,Math.Max(rivers[cell]*3,coast?.65:0));
            double stored=Math.Max(storage,cropStorage),aquatic=Math.Max(rivers[cell],coast?.55:0);
            double ecological=HistoricalEmergenceEffects.EcologicalConcentration(habitat,concentration,stored,aquatic);
            double occupied=HistoricalLocalEffects.RegionalTraditionOpportunity(s.Years-s.ArrivalYear[region]);
            double independent=.0025*Math.Pow(D.Clamp(Math.Max(stored,aquatic*.65)/.45),2)*ecological*occupied;
            double diffusion=local.Teacher>=0?.03*D.Clamp(local.Exposure/600.0)*(.02+.98*ecological)*D.Clamp(concentration):0;
            int revision=a.Empires?.Connections?.Realms?.Version??0;
            int civicTeacher=(int)Call("ProvincialRegionalCourt",s,region);if(civicTeacher<0)civicTeacher=local.CivicTeacher;
            double civic=civicTeacher>=0&&civicTeacher<h.Polities.Length?(double)Call("InstitutionalRegionalExample",s,region,civicTeacher):0;
            bool storageDelivered=(a.Provinces.RegionalKnowledge[region]&(1L<<(int)HistoricalTechnology.Storage))!=0,qualifiedContact=false;
            if(revision>=28)
            {
                // Keep this offline reconstruction compilable with the original
                // frozen26 assembly as well as the new optional rule assembly.
                qualifiedContact=local.Teacher>=0&&local.Teacher<h.Cultures.Length&&local.Exposure>=600&&local.LastExchangeYear>0&&local.LastExchangeYear<=s.Years
                    &&(long)s.Years-local.LastExchangeYear<=250&&s.Population[region]>=400&&storageDelivered&&civic>=600;
                if(qualifiedContact)diffusion=.03*D.Clamp(local.Exposure/600.0)*D.Clamp(concentration);
            }
            double dissemination=1,environment=1,management=0,homeland=1,wild=1,marine=0;
            if(colonial&&local.Teacher>=0)
            {
                var teacher=h.Cultures[local.Teacher];
                dissemination=(environmental?1:8)*(1+HistoricalModernEffects.Adoption(teacher,HistoricalTechnology.Printing,s.Years)+2*HistoricalModernEffects.Adoption(teacher,HistoricalTechnology.Telecommunications,s.Years));
                diffusion*=dissemination;
            }
            if(environmental)
            {
                environment=(double)Call("EnvironmentalEstablishment",s,cell,-1);management=local.Teacher>=0?(double)Call("EnvironmentalManagement",s,local.Teacher):0;
                homeland=(double)Call("HomelandOriginFactor",s,cell);if(living)homeland=HistoricalForagerMaritime.RecognitionHomeland(homeland);
                wild=(double)Call("EnvironmentalWildResources",s,cell);independent*=4*Math.Pow(environment,.75)*wild*homeland;
                if(living){marine=HistoricalMarineForagerEffects.IndependentCultureOpportunity((double)Call("MarineForagerSupport",s,cell),occupied,homeland);independent=Math.Max(independent,marine);}
                diffusion*=HistoricalEnvironmentalEffects.CulturalAdoption(environment,management);
            }
            double maturity=colonial?(double)Call("CulturalRecognitionMaturity",s,region):1,probability=1-Math.Exp(-(independent+diffusion)*maturity);
            if(cell==a.Regions[region].BestCell&&Math.Abs(probability-actualBestProbability)>1e-14)throw new Exception("Recognition diagnostic no longer reconstructs the actual best-cell probability.");
            return new{Place=Place(cell),Coast=coast,habitat.Biome,habitat.GrowingDays,habitat.SoilFertilityPermille,habitat.AridityMilli,habitat.FloodFractionPermille,
                Season=season,StoredDevelopmentOrLandscape=storage,WildCropStorage=cropStorage,Stored=stored,Aquatic=aquatic,Concentration=concentration,EcologicalConcentration=ecological,
                RegionalOccupation=occupied,EnvironmentalEstablishment=environment,TeacherManagement=management,Homeland=homeland,WildResourceFactor=wild,Dissemination=dissemination,
                IndependentHazard=independent,MarineHazardFloor=marine,DiffusionHazard=diffusion,RegionalMaturity=maturity,RecognitionChancePer100ActorYears=probability,
                RecognitionRevision=revision,QualifiedContactCommunity=qualifiedContact,DeliveredStorage=storageDelivered,ActualCivicTeacher=civicTeacher,ActualCivicExample=civic,
                SameRegionalParameters=true,UsesActualBestCell=cell==a.Regions[region].BestCell};
        }
        object Remote(int to,int routeId)
        {
            var route=engine.Routes[routeId];int from=route.A==to?route.B:route.A,departure=route.A==from?route.LandCellA:route.LandCellB,target=route.A==to?route.LandCellA:route.LandCellB;
            var row=new Dictionary<string,object>{{"Route",routeId},{"FromRegion",from},{"ToRegion",to}};var gates=new List<object>();string failed=null;
            bool Gate(string name,bool pass){gates.Add(new{Name=name,Pass=pass});if(!pass&&failed==null)failed=name;return pass;}
            object Done(){row["Gates"]=gates;row["FirstFailedInspectedGate"]=failed;row["AllInspectedNonRandomGatesPass"]=failed==null;return row;}
            if(!Gate("Real land boundary endpoints",!route.Sea&&departure>=0&&target>=0))return Done();
            row["Departure"]=Place(departure);row["Arrival"]=Place(target);row["TargetPhysicalCulture"]=HistoricalCells.Culture(s,target);row["TargetOwner"]=HistoricalCells.Polity(s,target);
            if(!Gate("Same actual landmass and anonymous unowned arrival",world.Landmass[departure]>=0&&world.Landmass[departure]==world.Landmass[target]&&a.Regions[to].Culture<0&&HistoricalCells.Culture(s,target)<0&&HistoricalCells.Polity(s,target)<0))return Done();
            int owner=HistoricalCells.Polity(s,departure),raw=a.Cells[departure*5+1];row["ActualSourceOwner"]=owner;row["RawSourceCohort"]=raw;row["SourcePhysicalCulture"]=HistoricalCells.Culture(s,departure);
            if(!Gate("Owned actual departure cell and population cohort",owner>=0&&raw>=0))return Done();
            int cohort=a.PopulationCohort(raw);row["ResolvedPopulationCohort"]=cohort;
            if(!Gate("The live owner is the actual paying cohort",cohort==owner))return Done();
            var country=a.Polities[cohort];int culture=h.Polities[cohort].CultureId;var c=s.Society.Cultures[culture];
            row["CountryCulture"]=culture;row["CountryFarming"]=c.Farming;row["ActualRemotePool"]=country.Remote;row["RemoteAreaKm2"]=country.RemoteAreaKm2;row["DepartureInhabitableArea"]=area[departure];
            bool routeAllowed=(bool)Call("InstitutionalRouteAllowed",s,departure,target,culture);row["PhysicalBoundaryAllowed"]=routeAllowed;
            if(!Gate("Real remote farmer pool and permitted border",country.Remote>=250&&c.Farming>=250&&country.RemoteAreaKm2>0&&routeAllowed))return Done();
            long local=(long)(country.Remote*Math.Min(1,area[departure]/country.RemoteAreaKm2));row["DownscaledLocalRemote"]=local;
            if(!Gate("At least250 locally staged remote residents",local>=250))return Done();
            long proposed=Math.Min(500,Math.Min(local/20,country.Remote/100));row["InitialMigrantLimit"]=proposed;
            int sourceNode=a.Empires.Territories.Sources[departure],sourceTown=sourceNode;bool nodeLive=(bool)Call("TerritorialNodeLive",s,owner,sourceNode);
            for(int depth=0;sourceTown<=-2&&depth<8;depth++)sourceTown=a.Empires.Territories.Posts[-sourceTown-2].ParentSource;
            bool granaryLive=sourceTown>=0&&sourceTown<a.Cities.Length&&a.Cities[sourceTown].Supported&&a.Living.Cities[sourceTown].AbandonedYear<0;
            row["SourceNode"]=sourceNode;row["SourceNodeLive"]=nodeLive;row["GranaryTown"]=sourceTown;row["GranaryLiveSupported"]=granaryLive;
            object prepared=Call("QuoteRemoteFields",s,culture,departure,target,owner,proposed);
            object unpriced=nodeLive&&granaryLive?Call("QuotePreparedFields",s,culture,target,sourceTown,proposed,departure,-1):null;
            double virgin=(double)Call("CarriedCropLandscape",s,culture,target,false,-1,departure),landscape=prepared==null?virgin:Q(prepared,"Landscape");
            row["VirginCarriedLandscape"]=virgin;row["FundedPreparation"]=Quote(prepared);row["RawPreparationBeforeAffordability"]=Quote(unpriced);row["ActualArrivalLandscape"]=landscape;
            var holder=(HistoricalTownSeeds)Call("LocalSeedHolder",s,culture,departure,-1);row["ActualSeedHolderTown"]=holder?.TownId;
            row["ActualCarriedCrops"]=Enumerable.Range(0,6).Select(k=>{var seed=(HistoricalCultivar)Call("LocalSeedProfile",s,culture,k,holder);return new{Group=k,Profile=seed,AdoptedYear=h.Agriculture[culture].AdoptedYear[k],Acquired=holder?[k]?.AcquiredYear,ArrivalFit=seed==null?0:(double)Call("CommunityTransferFit",s,target,seed,false)};}).ToArray();
            if(!Gate("Actual carried seed or affordable prepared field reaches.12",landscape>=.12))return Done();
            long fields=(long)((long)Call("CarriedFarmPotential",s,culture,target,-1,departure)*D.Clamp(c.Farming/600.0));
            if(prepared!=null)fields=(long)(fields*HistoricalPreparedFieldEffects.ClearingGain((HabitatCell)Call("CropHabitat",s,target),rivers[target]));
            long ordinary=Math.Max(0,a.Regions[to].Capacity+Math.Min(500,fields)-s.Population[to]);
            long room=prepared!=null?HistoricalFarmFrontier.Room(ordinary,fields,(long)Call("AggregateRuralCellPopulation",s,target),a.Regions[to].Culture>=0?s.Society.Cultures[a.Regions[to].Culture].Farming:0):(long)Call("CarriedFarmFrontierRoom",s,to,target,culture,ordinary,departure);
            long migrants=Math.Min(500,Math.Min(room,Math.Min(local/20,country.Remote/100)));row["PotentialFieldFarmers"]=fields;row["OrdinaryRegionalRoom"]=ordinary;row["ActualFrontierRoom"]=room;row["ProposedMigrants"]=migrants;
            if(!Gate("At least50 real migrants and physical field room",migrants>=50))return Done();
            if(prepared!=null&&Q(prepared,"Farmers")!=migrants)prepared=Call("QuoteRemoteFields",s,culture,departure,target,owner,migrants);
            if(!Gate("A live actual service chain and supported paying granary",nodeLive&&granaryLive))return Done();
            var granary=s.Society.Settlements[sourceTown];double effort=(double)Call("LivingTravelKm",s,granary.Cell,departure,culture)+(double)Call("LivingTravelKm",s,departure,target,culture),transport=HistoricalModernEffects.LandTransport(h.Cultures[culture],s.Years);
            row["EffortKm"]=Finite(effort);row["LandTransport"]=transport;row["GranaryFood"]=granary.FoodReserve;row["GranaryPeople"]=granary.Population;row["CourtTreasury"]=h.Polities[owner].Treasury;
            if(!Gate("Finite actual granary-to-departure plus arrival effort",!double.IsInfinity(effort)&&effort<double.MaxValue))return Done();
            long food=HistoricalInlandSettlement.Provisions(migrants,effort,transport)+migrants/2,investment=HistoricalInlandSettlement.Investment(migrants,effort,transport);
            row["RequiredFoodWithReserve"]=food+(long)Q(prepared,"Food")+granary.Population/2;row["RequiredTreasuryWithBuffer"]=(investment+(long)Q(prepared,"Investment"))*2;
            if(!Gate("Actual existing food and treasury pay transport and preparation",granary.FoodReserve>=food+(long)Q(prepared,"Food")+granary.Population/2&&h.Polities[owner].Treasury>=(investment+(long)Q(prepared,"Investment"))*2))return Done();
            row["CurrentBoundaryTransmission"]=(double)Call("EnvironmentalTransmission",s,departure,target,culture,false);row["CultureWouldBeInheritedFrom"]=culture;row["RawRecipientsAfterArrival"]=s.Population[to]+migrants;
            return Done();
        }
        var regionRows=new List<object>();var routeRows=new List<object>();
        foreach(int region in regions)
        {
            var r=a.Regions[region];var life=a.Living.Regions[region];int cohort=(int)Call("ProvincialRegionalCohort",s,region),teacher=life.Teacher;
            long contact=(long)Call("ProvincialContactResidents",s,region,false);int previous=h.RegionalTraditions.Predecessors[region];
            var land=r.Routes.Where(id=>!engine.Routes[id].Sea).ToArray();
            double recognition=(double)Call("LivingCommunityChance",s,region,100);
            regionRows.Add(new{Region=region,RequestedFineFields=anchors.Where(p=>engine.CellPatch[p.Cell]==region).Select(p=>new{p.Name,Place=Place(p.Cell)}),Best=Place(r.BestCell),r.Culture,r.LastCultureYear,LastCultureAgo=s.Years-r.LastCultureYear,
                Arrival=s.ArrivalYear[region],RawPeople=s.Population[region],life.SettledPopulation,ContactStagingEstimate=contact,ApportionedRemoteEstimate=contact-s.Population[region]-life.SettledPopulation,ActualRegionalCohort=cohort,
                ClaimedArea=a.Provinces.ClaimedAreaKm2[region],RawCapacity=r.Capacity,PreviousRegionalCulture=previous,PreviousRetired=previous>=0?h.RegionalTraditions.Retired[previous]:-1,
                Teacher=teacher,life.Exposure,life.LastExchangeYear,life.CivicTeacher,life.CivicExposure,
                RawCultureEligibility=r.Culture<0&&s.Population[region]>=400&&s.ArrivalYear[region]>=0&&s.Years-r.LastCultureYear>=800,
                CurrentRecognitionChancePer100ActorYears=recognition,RecognitionAtStoredBest=RecognitionAt(region,r.BestCell,recognition),
                BoundedFineFieldRecognitionCounterfactuals=anchors.Where(p=>engine.CellPatch[p.Cell]==region).Select(p=>new{p.Name,Decomposition=RecognitionAt(region,p.Cell,recognition)}).ToArray(),
                BestCellUnclaimedCulture=HistoricalCells.Culture(s,r.BestCell)<0,BestCellTownAt=a.TownAt[r.BestCell],
                TotalLandRoutes=land.Length,InspectedLandRoutes=land.Take(12).ToArray()});
            foreach(int route in land.Take(12))routeRows.Add(Remote(region,route));
        }
        var towns=regions.SelectMany(region=>s.Society.Settlements.Where(t=>t.Population>0&&a.Living.Cities[t.Id].AbandonedYear<0&&engine.CellPatch[t.Cell]==region).OrderBy(t=>t.Id).Take(2)).Select(t=>
        {
            int owner=a.Resolve(t.PolityId,h);long available=(long)Call("LivingFounderAvailability",s,t.Id);bool civic=owner>=0||h.Towns[t.Id].CivicExampleExposure>=400;
            return new{t.Id,t.Name,t.CultureId,Place=Place(t.Cell),Age=s.Years-t.FoundedYear,t.Population,t.FoodReserve,a.Cities[t.Id].Rural,a.Cities[t.Id].RuralCapacity,Owner=owner,
                AvailableActualFounders=available,Civic=civic,MinimumLocalFounderAge=civic?30:80,ActualLocalTownOpportunityRate=(double)Call("LivingLocalTownRate",s,t.Id),
                RequestedFieldQueries=anchors.Where(p=>engine.CellPatch[p.Cell]==engine.CellPatch[t.Cell]).Select(p=>new{p.Name,Target=Place(p.Cell),ActualSeedHolderCulture=t.CultureId,
                    BoundaryAllowed=(bool)Call("InstitutionalRouteAllowed",s,t.Cell,p.Cell,t.CultureId),EffortKm=Finite((double)Call("LivingTravelKm",s,t.Cell,p.Cell,t.CultureId)),
                    CanFoundWithThisActualParent=(bool)Call("CanFoundLivingTown",s,t.CultureId,p.Cell,t.Id),CarriedLandscape=(double)Call("FoundingFieldLandscape",s,t.CultureId,p.Cell,t.Id)}).ToArray()};
        }).ToArray();
        long delta=a.CellReads-reads;a.CellReads=reads;
        if(original.ComputeHash()!=before||s.ComputeHash()!=before||a.CellWrites!=writes||a.ProcessedLocalEvents!=events)throw new Exception("Physical farmer-frontier audit changed its source/probe.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=Path.GetFullPath(source),Year=s.Years,Hash=before,Realm=a.Empires.Connections.Realms.Version,DiagnosticReadDelta=delta,Regions=regionRows,RemoteCandidates=routeRows,ActualLocalTowns=towns,
            Notes=new[]{"Current final-state endpoint/account evidence only. No WorldFile read, upgrade, advance, migration/founding/delivery/commit handler or random event draw.",
                "Recognition probability queries use the existing deterministic seed-authored maturity coefficient; they do not roll recognition or select a random community site.",
                "Fine-field recognition counterfactuals change only the current ecological cell inputs at at most eight already-exported anchors, retaining this region's actual teacher, exposure, occupation and maturity. They are not a proposed legal origin, actual random site selection, past recognition log or additional people. The stored-best decomposition must reconstruct the actual private caller within1e-14.",
                "ContactStagingEstimate apportions existing Remote people and is not an extra account or proof of their presence at a particular boundary. Actual paying boundary ownership, cohort, density, crop/field quote, granary and funds are checked separately.",
                "At most twelve stored land routes and two actual local towns per bounded region are inspected. FirstFailedInspectedGate follows dependencies needed for a safe quote; it is not a historical rejection log. A successful current quote does not prove earlier availability or a random departure.",
                "Remote source culture is the existing paying country's rule, not a new assignment made by this audit. An unnamed region does not itself prevent that actual inherited migrant path. Food/cash/preparation are quotes only and are never paid by the diagnostic.",
                "Current local-town targets are fixed previously exported fine anchors, not invented random proposals. Missing CanFound alone does not distinguish all separate diplomacy/service-range gates in TryFoundLivingLocalTown.",
                "No current stock, genotype, town or route is backdated to Early Modern; original and detached hashes are preserved after restoring diagnostic reads."}},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine("Physical farmer frontier:"+regions.Length+" current regions,"+routeRows.Count+" bounded endpoints, unchanged "+before);
    }
}
