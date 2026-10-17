using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

// Offline source-aware evidence. Never call a delivery, migration, adoption or
// preparation handler: all physiology and availability probes are read-only.
public static class HistorySeedOfferGapAudit
{
    public static void Run(string source,string output)
    {
        if(File.Exists(output))throw new IOException("Use a new seed-offer audit output.");
        var world=WorldFile.Read(File.ReadAllBytes(source));var original=world.Prehistory;string before=original.ComputeHash();
        var s=original.Copy();var h=s.History;var a=h.Aggregates;var engine=new Prehistory(world);
        long originalReads=a.CellReads,originalWrites=a.CellWrites,originalEvents=a.ProcessedLocalEvents;
        if(a.Empires.Connections.Realms.Version!=17)throw new NotSupportedException("This audit targets fixed source17, not a silently upgraded world.");
        string validationError=null;
        try{engine.Validate(original);}catch(Exception error){validationError=error.Message;}
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(engine,values);
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
        double Lat(int cell)=>Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI;
        double Lon(int cell)=>Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI;
        object Place(int cell)=>new{Cell=cell,Latitude=Lat(cell),Longitude=Lon(cell),Landmass=world.Landmass[cell],Climate=world.Climate.Classification[cell].ToString(),Biome=world.Ecology.Habitat[cell].Biome.ToString()};
        int Farming(int culture)=>culture>=0?s.Society.Cultures[culture].Farming:0;
        var first=h.Polities.OrderBy(p=>p.FoundedYear).ThenBy(p=>p.Id).First();
        int firstLandmass=world.Landmass[s.Society.Settlements[first.CapitalId].Cell];
        var counts=new SortedDictionary<string,int>();void Count(string key){counts.TryGetValue(key,out int n);counts[key]=n+1;}
        var rows=new List<object>();var receivers=new HashSet<int>();
        var seen=new HashSet<string>();
        bool Receiver(int region,int cell)
        {
            int culture=a.Regions[region].Culture;
            return culture>=0&&Farming(culture)<180&&a.Cultures[culture].Cities.Count==0&&HistoricalCells.Polity(s,cell)<0
                &&world.Landmass[cell]==firstLandmass;
        }
        void Probe(string pathKind,int route,int from,int to,int departure,int arrival,int donor,HistoricalTownSeeds holder,int explicitTown=-1)
        {
            int receiver=a.Regions[to].Culture;if(donor<0||donor==receiver||Farming(donor)<180)return;
            if(!seen.Add(pathKind+":"+from+":"+to+":"+departure+":"+arrival+":"+donor))return;
            Count("Context."+pathKind);receivers.Add(receiver);
            int sourceOwner=HistoricalCells.Polity(s,departure),targetCellCulture=HistoricalCells.Culture(s,arrival);
            long contactResidents=(long)Call("ProvincialContactResidents",s,to,false);
            bool routeAllowed=(bool)Call("InstitutionalRouteAllowed",s,departure,arrival,donor);
            double transmission=(double)Call("EnvironmentalTransmission",s,departure,arrival,donor,false);
            var memory=a.Provinces.Exchanges[receiver];int peer=Array.IndexOf(memory.Peers,donor);
            int receipt=peer>=0?memory.LastYear[peer]:-1,direct=peer>=0?memory.LastDirectYear[peer]:-1;
            bool recent=receipt>=0&&s.Years-receipt<=250,recentDirect=direct>=0&&s.Years-direct<=250;
            int sourceTown=holder?.TownId?? -1;var town=sourceTown>=0?s.Society.Settlements[sourceTown]:null;
            bool live=town!=null&&a.Living.Cities[sourceTown].AbandonedYear<0&&town.Population+a.Cities[sourceTown].Rural>=10;
            bool supported=live&&a.Cities[sourceTown].Supported;
            bool regionalFarmer=a.Regions[from].Culture==donor&&Farming(donor)>=180&&s.Population[from]>=125;
            long remote=sourceOwner>=0&&h.Polities[sourceOwner].CultureId==donor?a.Polities[sourceOwner].Remote:0;
            bool remoteFarmer=remote>=250&&Farming(donor)>=250;
            int staff=town!=null?HistoricalRegionalTownExchange.Staff(town.Population):0;
            double effort=town!=null?(double)Call("LivingTravelKm",s,town.Cell,arrival,donor):double.MaxValue;
            double transport=HistoricalModernEffects.LandTransport(h.Cultures[donor],s.Years);
            long food=staff>0&&effort<double.MaxValue?HistoricalRegionalTownExchange.Food(staff,effort,transport):0;
            long investment=staff>0&&effort<double.MaxValue?HistoricalRegionalTownExchange.Investment(staff,effort,transport):0;
            int townOwner=town!=null?a.Resolve(town.PolityId,h):-1;
            bool solvent=townOwner<0||h.Polities[townOwner].EndedYear<0&&h.Polities[townOwner].Treasury>=investment*2&&a.Polities[townOwner].Receipts>a.Polities[townOwner].ServiceCost;
            bool paidTownDelivery=pathKind=="NearbyTown"&&supported&&a.Living.Cities[sourceTown].DeclineYears==0&&staff>0&&contactResidents>=100&&routeAllowed
                &&world.Landmass[town.Cell]==world.Landmass[arrival]&&effort<=600*transport&&town.FoodReserve>=food+town.Population/2&&solvent;
            if(holder==null)Count("HolderMissing");
            if(routeAllowed)Count("PhysicalRouteAllowed");if(recent)Count("RecentDatedKnowledgeReceipt");if(paidTownDelivery)Count("CurrentTownDeliveryFundable");
            var groups=new List<object>();bool anyFitGap=false,anyDatedFitGap=false;
            for(int k=0;k<6;k++)
            {
                var global=h.Agriculture[donor].Cultivars?[k];var lot=holder?[k];var local=lot?.Cultivar;
                if(global==null&&local==null)continue;
                int practice=h.Agriculture[receiver].PracticeCell[k];var incumbent=h.Agriculture[receiver].Cultivars?[k];
                double Fit(HistoricalCultivar seed,int cell)=>seed==null?0:(double)Call("CommunityTransferFit",s,cell,seed,false);
                double globalArrival=Fit(global,arrival),localArrival=Fit(local,arrival),globalPractice=Fit(global,practice),localPractice=Fit(local,practice);
                bool adoptionPending=h.Agriculture[receiver].AdoptedYear[k]<=0;
                bool globalAtReceipt=receipt>=0&&h.Agriculture[donor].AdoptedYear[k]>0&&h.Agriculture[donor].AdoptedYear[k]-1<=receipt;
                bool lotAtReceipt=lot!=null&&receipt>=lot.AcquiredYear,profileAtReceipt=lotAtReceipt&&receipt>=lot.LastPracticeYear;
                bool gap=local!=null&&globalPractice<=.12&&localPractice>.12;
                bool borderGap=local!=null&&globalArrival<=.12&&localArrival>.12;
                anyFitGap|=gap||borderGap;anyDatedFitGap|=(gap||borderGap)&&recent&&globalAtReceipt&&profileAtReceipt;
                if(gap)Count("Group.PracticeGenotypeThresholdGap");if(borderGap)Count("Group.BorderGenotypeThresholdGap");
                int actualDonor=(int)Call("DevelopmentCropDonor",s,receiver,k,practice,donor);
                var selected=actualDonor>=0?h.Agriculture[actualDonor].Cultivars?[k]:null;
                var harvest=town!=null?h.Towns[sourceTown].Cropping:null;int cropShare=0;
                if(local!=null&&harvest?.CropIds!=null&&harvest.SharesPermille!=null)
                    for(int j=0;j<Math.Min(harvest.CropIds.Length,harvest.SharesPermille.Length);j++)if(harvest.CropIds[j]==local.CropId)cropShare+=harvest.SharesPermille[j];
                groups.Add(new{Group=k,PracticePlace=Place(practice),RecipientAdoptedYear=h.Agriculture[receiver].AdoptedYear[k],AdoptionBranchApplies=adoptionPending,
                    RecipientIncumbent=incumbent,RecipientIncumbentFit=Fit(incumbent,practice),DonorGlobal=global,DonorLocal=local,
                    GlobalDepartureFit=Fit(global,departure),LocalDepartureFit=Fit(local,departure),GlobalArrivalFit=globalArrival,LocalArrivalFit=localArrival,
                    GlobalPracticeFit=globalPractice,LocalPracticeFit=localPractice,PracticeThresholdGap=gap,BorderThresholdGap=borderGap,
                    ActualDevelopmentCropDonor=actualDonor,ActualChosenGlobalFit=Fit(selected,practice),
                    DonorAdoptedYear=h.Agriculture[donor].AdoptedYear[k],LotAcquiredYear=lot?.AcquiredYear,LotSourceTown=lot?.SourceTown,LotSourceCell=lot?.SourceCell,
                    LotPracticeYears=lot?.PracticeYears,LotLastPracticeYear=lot?.LastPracticeYear,GlobalCropExistedAtReceipt=globalAtReceipt,
                    LocalLotExistedAtReceipt=lotAtReceipt,CurrentLocalPhenotypePredatesReceipt=profileAtReceipt,
                    CurrentSourceCropShare=cropShare,SourceHarvestYear=harvest?.Year});
            }
            if(anyDatedFitGap)Count("Context.CurrentPhenotypeGapWithDatedReceipt");
            double packageGlobal=(double)Call("CommunitySeededCropLandscape",s,donor,arrival,false,null,true);
            double packageLocal=holder!=null?(double)Call("CommunitySeededCropLandscape",s,donor,arrival,false,holder,true):0;
            rows.Add(new{PathKind=pathKind,Route=route,From=from,To=to,Departure=Place(departure),Arrival=Place(arrival),
                Receiver=receiver,ReceiverName=s.Society.Cultures[receiver].Name,ReceiverFarming=Farming(receiver),ReceiverNativeMask=h.Agriculture[receiver].NativeMask,
                ReceiverTownCount=a.Cultures[receiver].Cities.Count,ReceiverRawPopulation=s.Population[to],ReceiverContactResidents=contactResidents,TargetCellCulture=targetCellCulture,
                Donor=donor,DonorName=s.Society.Cultures[donor].Name,DonorFarming=Farming(donor),SourceOwner=sourceOwner,SourceRaw=s.Population[from],SourceRemote=remote,
                RealRegionalFarmerPool=regionalFarmer,OwnerRemoteFarmerPool=remoteFarmer,SourceHolderTown=sourceTown,SourceHolderField=holder?.FieldCell,
                SourceHolderPlace=holder!=null?Place(holder.FieldCell):null,SourceTownPopulation=town?.Population,SourceTownRural=town!=null?a.Cities[sourceTown].Rural:0,
                SourceHolderLive=live,SourceHolderSupported=supported,RouteAllowed=routeAllowed,Transmission=transmission,
                LastKnowledgeReceipt=receipt,LastDirectReceipt=direct,RecentKnowledgeReceipt=recent,RecentDirectReceipt=recentDirect,
                CurrentTownDeliveryFundable=paidTownDelivery,QuotedStaff=staff,QuotedFood=food,QuotedInvestment=investment,QuotedEffortKm=effort<double.MaxValue?(double?)effort:null,
                PackageGlobalYield=packageGlobal,PackageHeldLocalYield=packageLocal,AnyCurrentFitGap=anyFitGap,AnyGapWithCurrentPhenotypeKnownBeforeReceipt=anyDatedFitGap,Groups=groups});
        }
        for(int id=0;id<engine.Routes.Length;id++)
        {
            var route=engine.Routes[id];if(route.Sea)continue;
            for(int direction=0;direction<2;direction++)
            {
                int from=direction==0?route.A:route.B,to=direction==0?route.B:route.A;
                int departure=(int)Call("RegionalContactCell",s,route,from),arrival=(int)Call("RegionalContactCell",s,route,to);
                if(!Receiver(to,arrival))continue;Count("DirectedRouteToNamedPreTownNonFarmer");
                int owner=HistoricalCells.Polity(s,departure),local=a.Regions[from].Culture,teacher=(int)Call("DevelopmentRegionalTeacher",s,from);
                foreach(int donor in new[]{owner>=0?h.Polities[owner].CultureId:-1,local,teacher}.Where(c=>c>=0&&Farming(c)>=180).Distinct())
                    Probe("LandRoute",id,from,to,departure,arrival,donor,(HistoricalTownSeeds)Call("LocalSeedHolder",s,donor,departure,-1));
            }
        }
        for(int region=0;region<a.Regions.Length;region++)
        {
            int target=a.Regions[region].BestCell;if(!Receiver(region,target))continue;
            foreach(int townId in (int[])Call("NearbyLivingTowns",s,target,280.0))
            {
                var town=s.Society.Settlements[townId];if(Farming(town.CultureId)<180||world.Landmass[town.Cell]!=firstLandmass)continue;
                Probe("NearbyTown",-1,engine.CellPatch[town.Cell],region,town.Cell,target,town.CultureId,engine.LocalSeedsAt(s,townId),townId);
            }
        }
        var adoptionArithmetic=HistorySeedAdoptionArithmeticAudit.Inspect(world,s,engine,receivers.Concat(new[]{269,517,617}).Where(id=>id>=0&&id<s.Society.Cultures.Length).ToArray());
        long diagnosticReadDelta=a.CellReads-originalReads;
        // DevelopmentRegionalTeacher calls ProvincialRegionalCourt, whose
        // production helper deliberately charges saved diagnostic CellReads.
        // Restore ONLY that instrumentation on the detached probe copy; all
        // other authoritative fields must still hash exactly as loaded.
        a.CellReads=originalReads;
        string originalAfter=original.ComputeHash(),probeAfter=s.ComputeHash();
        if(originalAfter!=before||probeAfter!=before||a.CellWrites!=originalWrites||a.ProcessedLocalEvents!=originalEvents)
            throw new Exception("Seed-offer integrity failure: original="+originalAfter+" probe="+probeAfter+" expected="+before+" reads="+diagnosticReadDelta);
        string assembly=typeof(Prehistory).Assembly.Location;
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=Path.GetFullPath(source),Year=s.Years,Hash=before,Realm=a.Empires.Connections.Realms.Version,
            CoreAssembly=assembly,CoreSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),ValidationPassed=validationError==null,ValidationError=validationError,
            DiagnosticReadCounterDelta=diagnosticReadDelta,OriginalPreserved=true,ProbePreservedApartFromReadCounter=true,FirstCourtLandmass=firstLandmass,
            Counts=counts,InspectedReceivers=receivers.OrderBy(i=>i).ToArray(),Contexts=rows,AdoptionArithmetic=adoptionArithmetic,Notes=new[]{
                "Offline fixed17 snapshot audit only: no delivery, migration, adoption, prepared-field commit, rules upgrade, replay or save write.",
                "Receiving named cultures are Farming<180 with no current culture towns, on the actual first-court landmass. Physical border and nearby actual-town paths are separate.",
                "A remembered teacher is not an actual farmer pool. Source holder/presence, dated knowledge receipts, current food/treasury delivery quotes and physical gates are reported separately.",
                "Fit uses the exact adoption function with managed=false, matching DevelopmentCropDonor/AdvanceAggregateCulture. Package landscape yield is a separate donor-context measure and does not itself certify recipient town capacity or paid field clearance.",
                "Current local phenotype may have evolved after the last contact. AcquiredYear and LastPracticeYear are compared to receipt dates; current fit is not falsely backdated.",
                "Generic knowledge receipts do not record the crop group/genotype carried. A positive gap plus a valid dated receipt identifies a causal opportunity, not proof that this exact crop parcel was historically delivered.",
                "CurrentTownDeliveryFundable reconstructs the bounded nearby-town physical/staff/food/treasury gates without performing a delivery roll or debit. It is not a claim that a mission actually occurred.",
                "No claim that a named-forager wall is binding unless actual source/physical/genotype/receipt evidence supports it; zero eligible cases is a valid result."}},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine($"Seed offers {s.Years}: {rows.Count} source-aware contexts, {receivers.Count} receivers; preserved {before}");
    }
}
