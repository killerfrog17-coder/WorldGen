using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;
public static class HistoryModernTests
{
    public static void CeilingScenarios(string source,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.Read(File.ReadAllBytes(source));var original=world.Prehistory;string oldHash=original.Hash;
        var engine=new Prehistory(world);var basis=engine.UpgradeToModernHistory(original);
        if(basis.History.Cultures.Any(c=>c.Technology.Skip(15).Any(y=>y>0)))throw new ArgumentException("Use a pre-modern checkpoint to test all four discovery ceilings.");
        var rows=new List<string>{"ceiling,startYear,endYear,capstoneYear,postCapstoneYears,adoptedAtOrAboveEarlyModern,forbiddenAdoptions,hash"};
        foreach(var ceiling in Enum.GetValues<HistoricalTechnologyCeiling>())
        {
            var currentEngine=new Prehistory(world);var state=currentEngine.SetTechnologyCeiling(basis,ceiling);int reached=-1;
            var targets=ceiling==HistoricalTechnologyCeiling.Medieval?new[]{HistoricalTechnology.Steel,HistoricalTechnology.Scholasticism}:ceiling==HistoricalTechnologyCeiling.EarlyModern?new[]{HistoricalTechnology.Printing,HistoricalTechnology.ScientificMethod,HistoricalTechnology.CropRotation}:ceiling==HistoricalTechnologyCeiling.Industrial?new[]{HistoricalTechnology.SteamPower,HistoricalTechnology.Railways,HistoricalTechnology.Electricity,HistoricalTechnology.Sanitation}:new[]{HistoricalTechnology.Computing,HistoricalTechnology.Telecommunications,HistoricalTechnology.Antibiotics,HistoricalTechnology.MechanizedAgriculture};
            while(state.Years<basis.Years+7000)
            {
                reached=state.History.Cultures.Where(c=>targets.All(t=>HistoricalTechnologies.Has(c,t))).Select(c=>targets.Max(t=>c.Technology[(int)t])-1).DefaultIfEmpty(-1).Min();
                int end=reached>=0?Math.Max(basis.Years+750,reached+750):basis.Years+7000;
                if(state.Years>=end)break;state=currentEngine.AdvanceYears(state,Math.Min(100,end-state.Years));currentEngine.Validate(state);
            }
            int forbidden=state.History.Cultures.Sum(c=>c.Technology.Select((year,k)=>(year,k)).Count(x=>x.year>0&&HistoricalModernEffects.Era((HistoricalTechnology)x.k)>ceiling));
            if(forbidden>0||reached<0)throw new Exception("Technology ceiling failed to constrain or support development: "+ceiling);
            world.Prehistory=state;File.WriteAllBytes(Path.Combine(output,ceiling+".world"),WorldFile.Write(world));
            rows.Add($"{ceiling},{basis.Years},{state.Years},{reached},{state.Years-reached},{state.History.Cultures.Sum(c=>c.Technology.Skip(15).Count(y=>y>0))},{forbidden},{state.Hash}");File.WriteAllLines(Path.Combine(output,"ceilings.csv"),rows);
            Console.WriteLine("PASS ceiling "+ceiling+" at "+state.Years+", post-capstone years "+(state.Years-reached)+", forbidden "+forbidden);
        }
        if(original.ComputeHash()!=oldHash)throw new Exception("Ceiling scenarios mutated the original checkpoint.");
    }
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalTechnologies.Names.Length==Enum.GetValues<HistoricalTechnology>().Length&&HistoricalTechnologies.Prerequisites.Length==HistoricalTechnologies.Names.Length,"Every capability has a name and prerequisites");
        for(int k=0;k<HistoricalTechnologies.Names.Length;k++)check(HistoricalTechnologies.Prerequisites[k].All(p=>p>=0&&p<k),"Technology DAG remains ordered at "+k);
        var warm=new HabitatCell{WarmTenthsC=290,ColdTenthsC=240,GrowingDays=365,AridityMilli=1700,SoilFertilityPermille=350,Soil=SoilKind.Leached};
        var shortSeason=new HabitatCell{WarmTenthsC=150,ColdTenthsC=-200,GrowingDays=75,AridityMilli=800};
        var maize=new HistoricalCultivar{CropId=4,MaturityDays=150,WaterRequirementPermille=450,DaylengthLatitudeTenths=100};
        check(HistoricalCropCatalog.Fit(warm,maize,10)>HistoricalCropCatalog.Fit(warm,maize,50),"An unadapted photoperiod-sensitive lineage does not transfer freely across latitude");
        check(HistoricalCropCatalog.Fit(shortSeason,maize,50)==0,"Warm-season maize cannot support an unadapted short growing season");
        check(HistoricalCropCatalog.LeachingPenalty(warm,true)>HistoricalCropCatalog.LeachingPenalty(warm,false),"Soil management mitigates, rather than ignores, leached soils");
        var boreal=new HabitatCell{WarmTenthsC=150,ColdTenthsC=-150,GrowingDays=80,AridityMilli=800,SoilFertilityPermille=700,Soil=SoilKind.Loam,Habitat=Habitat.Forest};
        var wildBarley=new HistoricalCultivar{CropId=1,MaturityDays=110,WaterRequirementPermille=260,MinimumWarmTenthsC=120,DaylengthLatitudeTenths=600};
        var selectedBarley=wildBarley.Copy();selectedBarley.MaturityDays=77;selectedBarley.MinimumWarmTenthsC=80;
        check(HistoricalCrops.Suitability(boreal,0)<.3&&HistoricalCropCatalog.TransferFit(boreal,selectedBarley,60)>=.3&&HistoricalCropCatalog.TransferFit(boreal,wildBarley,60)<.3,"Selected short-season crops can cross a real growing-season barrier that their ancestral strain cannot");
        var leachedJungle=new HabitatCell{WarmTenthsC=290,ColdTenthsC=240,GrowingDays=365,AridityMilli=1700,SoilFertilityPermille=250,Soil=SoilKind.Leached,Biome=Biome.TropicalRainforest,Habitat=Habitat.Forest};
        check(HistoricalCropCatalog.TransferFit(leachedJungle,maize,10)<.3&&HistoricalCropCatalog.TransferFit(leachedJungle,maize,10,managed:true)>HistoricalCropCatalog.TransferFit(leachedJungle,maize,10),"A carried grain crop does not bypass rainforest soil/clearing costs; management can improve its prospects");
        var desert=new HabitatCell{GrowingDays=365,AridityMilli=80,Biome=Biome.Desert};
        check(HistoricalCropDiffusionEffects.EnvironmentalCost(warm,desert,false)>HistoricalCropDiffusionEffects.EnvironmentalCost(warm,desert,true),"Dry-country experience reduces, without eliminating, a desert exchange barrier");
        check(HistoricalCropDiffusionEffects.Exposure(500,0,false)>HistoricalCropDiffusionEffects.Exposure(500,200000,false)&&HistoricalCropDiffusionEffects.Exposure(500,200000,true)>HistoricalCropDiffusionEffects.Exposure(500,200000,false),"Local crop exposure decays along travel effort and roads improve actual reach");
        check(HistoricalCropDiffusionEffects.AdoptionChance(.2,.1,900)==0&&HistoricalCropDiffusionEffects.AdoptionChance(.7,.1,400)==0&&HistoricalCropDiffusionEffects.AdoptionChance(.7,.1,900)>0,"Crop fit and local exposure are separate adoption requirements");
        var seaAir=new HabitatCell{WarmTenthsC=270,ColdTenthsC=220,MeanTenthsC=250,GrowingDays=365,AridityMilli=1000,SalinityMilliPpt=35000,ReliefM=3000,Soil=SoilKind.Saline};
        var islandLand=HistoricalIslandCropEnvironment.FromOcean(seaAir,25000000,120);
        check(HistoricalCrops.Suitability(seaAir,3)==0&&HistoricalCrops.Suitability(islandLand,3)>.2&&seaAir.SalinityMilliPpt==35000,"Fractional island fields use their real land area and air climate, not ocean salinity or seabed relief");
        check(HistoricalCrops.Suitability(HistoricalIslandCropEnvironment.FromOcean(seaAir,100000,3),3)<HistoricalCrops.Suitability(islandLand,3),"Tiny spray-exposed cays are less favorable farmland than substantial islands");
        var industrial=new HistoricalCulture{Technology=new int[HistoricalTechnologies.Names.Length]};
        check(HistoricalModernEffects.AdministrativeCapacity(industrial,1000)==1,"Modern administrative capacity cannot appear before its actual capabilities");
        foreach(var tech in new[]{HistoricalTechnology.Printing,HistoricalTechnology.Railways,HistoricalTechnology.Telegraph,HistoricalTechnology.Computing})industrial.Technology[(int)tech]=901;
        check(HistoricalModernEffects.AdministrativeCapacity(industrial,1000)==5,"Mature records, transport and communications increase funded administrative throughput within explicit bounds");
        var processedRoots=new HistoricalCultivar{CropId=8,SelectedYears=8000,YieldPermille=1300};
        double initialPlot=.48*leachedJungle.SoilFertilityPermille/700.0;
        double beginner=initialPlot*1.3*HistoricalHusbandryEffects.LeachingPenalty(leachedJungle,0,false)*HistoricalHusbandryEffects.StoragePermille(3,processedRoots,false)/1000.0;
        double practiced=initialPlot*HistoricalHusbandryEffects.ClearingMultiplier(leachedJungle,4000)*1.3*HistoricalHusbandryEffects.LeachingPenalty(leachedJungle,4000,false)*HistoricalHusbandryEffects.StoragePermille(3,processedRoots,true)/1000.0;
        check(beginner<.23&&practiced>.23,"A poor tropical root-garden plot is initially unsuitable for concentrated state surplus, but sustained management and food processing prevent an implicit permanent biome ban");
        check(HistoricalHusbandryEffects.ClearingMultiplier(leachedJungle,100000)==HistoricalHusbandryEffects.ClearingMultiplier(leachedJungle,4000)&&HistoricalHusbandryEffects.LeachingPenalty(leachedJungle,100000,false)<=.85,"Long practice has bounded effects and does not remove leached-soil constraints");
        var vessels=new HistoricalCulture{Technology=new int[HistoricalTechnologies.Names.Length]};
        check(HistoricalModernVoyageEffects.RangeM(vessels,1000)==3500000&&HistoricalModernVoyageEffects.FounderScale(vessels,1000)==1,"Long-distance colonization cannot acquire modern ships before the actual capabilities");
        vessels.Technology[(int)HistoricalTechnology.OceanNavigation]=901;vessels.Technology[(int)HistoricalTechnology.SteamPower]=901;
        check(HistoricalModernVoyageEffects.RangeM(vessels,1000)==9000000&&HistoricalModernVoyageEffects.FounderScale(vessels,1000)==20,"Mature ocean navigation and steam support longer provisioned voyages and larger bounded founder groups");
        check(HistoricalVoyageEffects.SettlementChance(6000000,2000,450,.7,.7)==0&&HistoricalVoyageEffects.SettlementChance(6000000,2000,450,.7,.7,20000,9000000,3)>0,"Modern vessels can cross a gap that oral-navigation expeditions cannot");
        var crew=HistoricalVoyageEffects.AllocateFounders(1500,10000,200,1000,20000);
        check(crew.Total==1500&&crew.Urban>0&&crew.Rural<=100&&crew.Urban<=2000,"Founders are conserved actual port and rural residents, not a newly created expedition population");
        check(HistoricalVoyageEffects.AllocateFounders(1500,40,200,1000,20000).Total==0,"A destination without room for a viable founder group does not receive free settlers");
        check(HistoricalModernEffects.CarriedAtFounding(vessels,(int)HistoricalTechnology.SteamPower,950)&&!HistoricalModernEffects.CarriedAtFounding(vessels,(int)HistoricalTechnology.SteamPower,900),"Migrants remember methods known before departure, never later discoveries by their ancestors");
        var civicMemory=new HistoricalCivicRivalry{IntensityPermille=800,LastConflictYear=100};
        check(HistoricalCivicMemoryEffects.Intensity(civicMemory,100)==800&&HistoricalCivicMemoryEffects.Intensity(civicMemory,700)<400&&HistoricalCivicMemoryEffects.Intensity(civicMemory,700)>0,"Dated civic conflict memory fades without a tick-size-dependent accumulation");
        check(HistoricalCivicMemoryEffects.PoliticalSalience(civicMemory,100,true)>0&&HistoricalCivicMemoryEffects.PoliticalSalience(civicMemory,100,true)<HistoricalCivicMemoryEffects.PoliticalSalience(civicMemory,100,false),"A shared government moderates political rivalry without deleting city history");
        check(!HistoricalCivicMemoryEffects.CanCooperate(700,.3)&&HistoricalCivicMemoryEffects.CanCooperate(700,.8),"Former rivals are reluctant coalition partners, but strong common grievances can unite them");
        V Point(double longitude,double latitude){double lon=longitude*Math.PI/180,lat=latitude*Math.PI/180;return new V(Math.Cos(lat)*Math.Cos(lon),Math.Cos(lat)*Math.Sin(lon),Math.Sin(lat));}
        var channel=new Surface{Cells=new[]{new Cell{Id=0,Position=Point(-2,0),Neighbors=new[]{2},DistanceM=new[]{300000}},new Cell{Id=1,Position=Point(2,0),Neighbors=new[]{4},DistanceM=new[]{300000}},new Cell{Id=2,Position=Point(-2,-1),Neighbors=new[]{0,3},DistanceM=new[]{300000,300000}},new Cell{Id=3,Position=Point(0,-1),Neighbors=new[]{2,4},DistanceM=new[]{300000,300000}},new Cell{Id=4,Position=Point(2,-1),Neighbors=new[]{3,1},DistanceM=new[]{300000,300000}}}};
        var water=new[]{false,false,true,true,true};var detour=HistoricalOceanPaths.Find(channel,water,0,1,6371,1300000);
        check(detour.DistanceM==1200000&&detour.WaterCells.SequenceEqual(new[]{2,3,4}),"Bounded navigation can follow real ocean edges around intervening land instead of demanding a straight line");
        check(HistoricalOceanPaths.Find(channel,water,0,1,6371,1000000).DistanceM==0,"An over-range ocean detour is not a free connection");
        var bounded=HistoricalOceanPaths.Find(channel,water,0,1,6371,1300000,1);check(bounded.DistanceM==0&&bounded.Expanded<=1,"Navigation respects its explicit work budget and fails closed");
        check(HistoricalOceanPaths.Find(channel,new[]{false,false,true,false,true},0,1,6371,1300000).DistanceM==0,"Sea paths cannot cross a land bridge merely because the ports are near");
        using(var stopSea=new CancellationTokenSource()){stopSea.Cancel();bool seaCanceled=false;try{HistoricalOceanPaths.Find(channel,water,0,1,6371,1300000,token:stopSea.Token);}catch(OperationCanceledException){seaCanceled=true;}check(seaCanceled,"Ocean path queries honor cancellation before doing work");}
        string fixture=Environment.GetEnvironmentVariable("MODERN_FIXTURE")??"D:/WorldGen-history12-20261014/release-earth20k/final.world";
        var w=WorldFile.Read(File.ReadAllBytes(fixture));var engine=new Prehistory(w);var old=w.Prehistory;string oldHash=old.Hash,geography=StateHash.Compute(w);
        var start=engine.UpgradeToModernHistory(old);engine.Validate(start);
        if(w.Recipe.TerrainSource=="Earth")
        {
            int Cape(double lon,double lat)=>Enumerable.Range(0,w.Surface.Cells.Length).Where(i=>!w.Hydrology.OceanConnected[i]&&w.Surface.Cells[i].Neighbors.Any(j=>w.Hydrology.OceanConnected[j])).OrderBy(i=>D.Angle(w.Surface.Cells[i].Position,Point(lon,lat))).ThenBy(i=>i).First();
            int west=Cape(17,-33),east=Cape(32,-30);var cape=engine.AssessOceanPath(west,east);
            check(cape.DistanceM>0&&cape.Expanded<=5000&&cape.WaterCells.All(i=>w.Hydrology.OceanConnected[i])&&cape.WaterCells.Zip(cape.WaterCells.Skip(1),(a,b)=>w.Surface.Cells[a].Neighbors.Contains(b)).All(x=>x),"The actual Earth ocean graph supports a bounded all-water passage around southern Africa");
        }
        check(start.History.Version=="history-13"&&old.Hash==oldHash&&old.ComputeHash()==oldHash,"Modern rules are explicit and leave the old checkpoint untouched");
        check(start.History.Cultures.All(c=>c.Technology.Length==HistoricalTechnologies.Names.Length)&&start.History.Modern.TechnologyCeiling==HistoricalTechnologyCeiling.Modern,"Upgrade appends capabilities with a modern default ceiling");
        check(start.History.Archive.Count==old.History.Archive.Count&&start.Society.Settlements.Select(t=>t.FoundedYear).SequenceEqual(old.Society.Settlements.Select(t=>t.FoundedYear)),"No past dates or events are fabricated by the upgrade");
        var low=engine.SetTechnologyCeiling(start,HistoricalTechnologyCeiling.Medieval);var after=engine.AdvanceYears(low,120);engine.Validate(after);
        check(after.History.Cultures.All(c=>c.Technology.Skip(15).All(y=>y==0)),"A medieval ceiling actually prevents later development");
        var high=engine.SetTechnologyCeiling(start,HistoricalTechnologyCeiling.Modern);
        var branch=high.Copy();foreach(var r in branch.History.Relations)r.WarSince=-1;
        var branchEngine=new Prehistory(w);var baseGraph=branchEngine.HistoricalTownContacts(branch);
        var farPorts=(from a in branch.Society.Settlements where a.Population>=100 from b in branch.Society.Settlements where b.Id>a.Id&&b.Population>=100&&!baseGraph[a.Id].Contains(b.Id) let metres=(int)Math.Round(D.Angle(w.Surface.Cells[a.Cell].Position,w.Surface.Cells[b.Cell].Position)*w.Recipe.RadiusKm*1000) where metres>2000000&&metres<9000000 select new HistoricalShippingLink{PortA=a.Id,PortB=b.Id,CultureA=a.CultureId,CultureB=b.CultureId,EstablishedYear=branch.Years,DistanceM=metres}).Take(2).ToArray();
        check(farPorts.Length==2,"The exchange regression fixture contains two distinct remote port pairs");
        branch.History.Modern.Shipping=new[]{farPorts[0]};branch.Seal();branchEngine.HistoricalTownContacts(branch);
        branch.History.Modern.Shipping=new[]{farPorts[1]};branch.Seal();var changedGraph=branchEngine.HistoricalTownContacts(branch);
        check(StateHash.Compute(changedGraph)==StateHash.Compute(new Prehistory(w).HistoricalTownContacts(branch)),"Replacing a shipping route with the same route count invalidates the warm town-contact graph");
        var signatureField=typeof(Prehistory).GetField("townNetworkSignature",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;var oldSignature=(int[])signatureField.GetValue(branchEngine)!;
        branch.History.Cultures[branch.Society.Settlements[farPorts[1].PortA].CultureId].Technology[(int)HistoricalTechnology.OceanNavigation]=branch.Years;branch.Seal();branchEngine.HistoricalTownContacts(branch);
        check(!oldSignature.SequenceEqual((int[])signatureField.GetValue(branchEngine)!),"Ocean navigation is an explicit town-contact dependency, not a stale pre-navigation verdict");
        var shippingLookup=typeof(Prehistory).GetMethod("ModernShippingContact",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        int navigator=branch.Society.Settlements[farPorts[1].PortA].CultureId;
        bool SlowShipping(int from,int to)=>branch.History.Modern.Shipping.Any(link=>
        {var a=branch.Society.Settlements[link.PortA];var b=branch.Society.Settlements[link.PortB];bool Near(int x,int y)=>D.Angle(w.Surface.Cells[x].Position,w.Surface.Cells[y].Position)*w.Recipe.RadiusKm<=150;return a.Population>=100&&b.Population>=100&&(Near(from,a.Cell)&&Near(to,b.Cell)||Near(from,b.Cell)&&Near(to,a.Cell));});
        bool IndexedShipping(int from,int to)=>(bool)shippingLookup.Invoke(branchEngine,new object[]{branch,from,to,navigator})!;
        var queryCells=branch.Society.Settlements.Take(40).Select(t=>t.Cell).Concat(new[]{branch.Society.Settlements[farPorts[1].PortA].Cell,branch.Society.Settlements[farPorts[1].PortB].Cell}).Distinct().ToArray();
        check(queryCells.All(from=>queryCells.All(to=>IndexedShipping(from,to)==SlowShipping(from,to))),"Indexed sea contact exactly matches the original spherical scan over actual remote ports and nearby town queries");
        var sleepingPort=branch.Society.Settlements[farPorts[1].PortB];long wakingPopulation=sleepingPort.Population;sleepingPort.Population=0;
        check(!IndexedShipping(branch.Society.Settlements[farPorts[1].PortA].Cell,sleepingPort.Cell),"Port indexing retains live population eligibility rather than caching a stale open-port verdict");sleepingPort.Population=wakingPopulation;
        var relationProbe=high.Copy();var relation=relationProbe.History.Relations.First();
        var relationLookup=typeof(Prehistory).GetMethod("FindHistoricalRelation",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        HistoricalRelation Lookup()=>(HistoricalRelation)relationLookup.Invoke(engine,new object[]{relationProbe,relation.B,relation.A})!;
        check(ReferenceEquals(Lookup(),relation),"Relation indexing preserves unordered pair lookup and exact first-record identity");
        relation.WarSince=relationProbe.Years;relation.LastBattle=relationProbe.Years;
        check(Lookup().WarSince==relationProbe.Years&&Lookup().LastBattle==relationProbe.Years,"Live war and battle changes are not cached as stale diplomatic verdicts");
        var replaced=relation.Copy();replaced.WarSince=-1;relationProbe.History.Relations=(HistoricalRelation[])relationProbe.History.Relations.Clone();relationProbe.History.Relations[Array.IndexOf(relationProbe.History.Relations,relation)]=replaced;
        check(ReferenceEquals(Lookup(),replaced),"Replacing a relationship array invalidates its derived lookup immediately");
        var pairRoll=typeof(Prehistory).GetMethod("HistoryPairRoll",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        double Pair(int a,int b)=>(double)pairRoll.Invoke(engine,new object[]{high,"decision-probe",a,b})!;
        check(Pair(1,40)!=Pair(2,9)&&Pair(1,40)==Pair(1,40),"Modern actor-pair decisions use distinct named streams instead of aliasing a*31+b");
        // Controlled subsystem exercise, not a calibrated history: mature ships
        // and ample paid stores isolate the actual migration/resource ledger.
        var voyageState=high.Copy();var voyageEngine=new Prehistory(w);
        foreach(var c in voyageState.Society.Cultures){c.Fishing=450;c.Farming=Math.Max(600,c.Farming);}
        foreach(var c in voyageState.History.Cultures)for(int k=0;k<c.Technology.Length;k++)c.Technology[k]=voyageState.Years-1000;
        foreach(var t in voyageState.Society.Settlements)t.FoodReserve=Math.Max(t.FoodReserve,t.Population*10);
        long beforePeople=voyageState.Population.Sum()+voyageState.Society.Settlements.Sum(t=>t.Population),beforeFood=voyageState.Society.Settlements.Sum(t=>t.FoodReserve);
        int previousVoyages=voyageState.History.OceanSettlements.Length;
        typeof(Prehistory).GetMethod("AdvanceModernOceanSettlements",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(voyageEngine,new object[]{voyageState,CancellationToken.None,50000000.0});
        var paidVoyages=voyageState.History.OceanSettlements.Skip(previousVoyages).ToArray();
        check(paidVoyages.Length>0&&paidVoyages.Any(v=>v.UrbanMigrants>0),"Actual modern voyage processing can recruit existing urban households from funded ports");
        check(beforePeople==voyageState.Population.Sum()+voyageState.Society.Settlements.Sum(t=>t.Population),"Successful overseas landings conserve total rural and urban people exactly");
        check(beforeFood-voyageState.Society.Settlements.Sum(t=>t.FoodReserve)==paidVoyages.Sum(v=>v.Provisions),"Voyage consumption and landing reserves are paid rather than created");
        check(paidVoyages.Any(v=>v.ArrivalFood>0)&&paidVoyages.Where(v=>v.ArrivalFood>0).All(v=>voyageState.Society.Settlements.Any(t=>t.Cell==v.ToCell&&t.FoundedYear==v.Year&&t.Population>=100)),"Large funded landings retain a physically concentrated settlement instead of disappearing across a rural patch");
        var paletteHash=StateHash.Compute(w);var green=Enumerable.Range(0,w.Surface.Cells.Length).Where(i=>w.Ecology.Region[i]>=0&&!w.Hydrology.OceanConnected[i]&&w.Ecology.Habitat[i].Biome==Biome.TropicalRainforest).ToArray();
        check(green.Length>0&&green.All(i=>{var color=EcologyView.Color(w,i,Layer.Ecoregions);return color.Y>color.X&&color.Y>color.Z;}),"Ecoregion identity tint cannot overwhelm the recognizable rainforest biome color");
        check(StateHash.Compute(w)==paletteHash,"Ecological color and narrative presentation cannot mutate geography");
        var next=engine.AdvanceSeconds(high,137*SimulationTime.Year+17*SimulationTime.Day);engine.Validate(next);
        var split=engine.AdvanceSeconds(engine.AdvanceSeconds(high,51*SimulationTime.Year+3*SimulationTime.Hour),86*SimulationTime.Year+17*SimulationTime.Day-3*SimulationTime.Hour);
        check(next.Hash==split.Hash,"Crops, adaptation, research and transport are independent of playback request sizes");
        check(new Prehistory(w).AdvanceSeconds(high,137*SimulationTime.Year+17*SimulationTime.Day).Hash==next.Hash,"Actor search caches cannot change modern outcomes");
        var session=new SimulationSession(engine,high);long sessionTarget=SimulationTime.Seconds(high)+3*SimulationTime.Year+17*SimulationTime.Day;
        var fast=SimulationClockView.Publication(10*SimulationTime.Year+123,1000*SimulationTime.Year,1000*SimulationTime.Year);
        check(fast.Seconds==1000*SimulationTime.Year&&fast.MaximumEvents==SimulationClockView.FastMaximumEvents&&fast.MaximumGlobalWork==SimulationClockView.FastMaximumGlobalWork,"Fast publication crosses empty years and batches populated years while bounding estimated global work");
        var fine=SimulationClockView.Publication(10*SimulationTime.Year+123,17,1);check(fine.Seconds==1&&fine.MaximumEvents==SimulationClockView.FastMaximumEvents,"Fine-speed playback batches by an exact short date interval, not one unrelated actor event");
        var monthly=SimulationClockView.Publication(0,SimulationTime.Year,SimulationTime.Year/2.0);check(monthly.Seconds==SimulationTime.Year/10&&monthly.MaximumEvents>1,"Half-year-per-second playback processes every event within a fifth-second display interval");
        var batched=new SimulationSession(new Prehistory(w),high);
        while(batched.Seconds<sessionTarget){var slice=SimulationClockView.Publication(batched.Seconds,sessionTarget-batched.Seconds,1000*SimulationTime.Year);batched.AdvanceSeconds(slice.Seconds,slice.MaximumEvents,maximumGlobalWork:slice.MaximumGlobalWork);}
        check(batched.Snapshot().Hash==engine.AdvanceSeconds(high,sessionTarget-SimulationTime.Seconds(high)).Hash,"Multi-year playback batching preserves the exact calendar, domain effects and final integrity hash");
        while(session.Seconds<sessionTarget)session.AdvanceSeconds(sessionTarget-session.Seconds,1);
        var sessionSnapshot=session.Snapshot();check(sessionSnapshot.Hash==engine.AdvanceSeconds(high,3*SimulationTime.Year+17*SimulationTime.Day).Hash,"Private playback sessions preserve the exact public calendar result");
        string sessionHash=session.Hash;sessionSnapshot.Population[0]++;session.LastSnapshot.Population[0]+=7;
        session.LastSnapshot.History.Archive[0].Text+=" external mutation";
        if(session.LastSnapshot.Society.Events.Length>0)session.LastSnapshot.Society.Events[0].Text+=" external mutation";
        check(session.Hash==sessionHash&&session.Snapshot().ComputeHash()==sessionHash,"Mutating either publication cannot reach the private session checkpoint");
        var sessionNext=session.AdvanceSeconds(SimulationTime.Day);engine.Validate(sessionNext);
        check(sessionNext.Hash==engine.AdvanceSeconds(high,3*SimulationTime.Year+18*SimulationTime.Day).Hash,"Detached publication mutations cannot alter a later continuation");
        var clockOnly=session.AdvanceSeconds(1);check(PrehistoryState.SameDomain(sessionNext,clockOnly)&&clockOnly.Hash!=sessionNext.Hash&&!ReferenceEquals(clockOnly.History,sessionNext.History),"Detached fine-clock publications reuse only a presentation identity, not mutable private objects or an old integrity hash");
        sessionNext=clockOnly;
        using(var sessionCancel=new CancellationTokenSource()){sessionCancel.Cancel();bool rolledBack=false;try{session.AdvanceSeconds(SimulationTime.Year,1,sessionCancel.Token);}catch(OperationCanceledException){rolledBack=true;}check(rolledBack&&session.Hash==sessionNext.Hash,"Cancelled session work does not commit a partial private state");}
        using(var midCancel=new CancellationTokenSource())
        {
            var interrupted=new SimulationSession(engine,high);bool stoppedMidWork=false,observed=false;
            engine.WorkObserver=m=>{if(m.Stage=="annual complete"){observed=true;midCancel.Cancel();}};
            try{interrupted.AdvanceSeconds(3*SimulationTime.Year,int.MaxValue,midCancel.Token);}catch(OperationCanceledException){stoppedMidWork=true;}finally{engine.WorkObserver=null;}
            check(observed&&stoppedMidWork&&interrupted.Hash==high.Hash,"Cancellation after real annual work rolls back the entire private transaction");
            check(interrupted.AdvanceSeconds(3*SimulationTime.Year,int.MaxValue).Hash==new Prehistory(w).AdvanceSeconds(high,3*SimulationTime.Year).Hash,"Canceled derived route/food caches cannot poison the resumed checkpoint");
        }
        int vacant=Enumerable.Range(0,w.Surface.Cells.Length).First(i=>engine.CellPatch[i]<0);
        string beforeTransmission=high.Hash;var emptyTransmission=engine.AssessCropTransmission(high,vacant);
        check(emptyTransmission.SourceCulture<0&&emptyTransmission.Exposure==0&&high.ComputeHash()==beforeTransmission,"A diagnostic crop-route query cannot teach uninhabited ocean or mutate the source");
        var restored=WorldJson.Read<PrehistoryState>(WorldJson.Write(next));engine.Validate(restored);check(restored.Hash==next.Hash,"JSON preserves cultivar lineage, local crop surveys and ceiling");
        check(next.History.Towns.Any(t=>t.Cropping?.CropIds.Length>0),"Real city crop surveys record suitable named food plants");
        check(next.History.Modern.CivicRivalries?.Length>0,"Actual scheduled nearby wars create persistent city rivalry records");
        var rivalry=next.History.Modern.CivicRivalries![0];var cityArticle=HistoryAtlas.Read(next,"city",rivalry.TownA,w);
        check(cityArticle.Sections.Any(section=>section.Title=="Civic rivalries and memory"&&section.Links.Any(l=>l.Id==rivalry.TownB)),"City articles link their actual former rivals rather than fabricating generic relationships");
        var namesake=next.Copy();namesake.Society.Settlements[rivalry.TownB].Name=namesake.Society.Settlements[rivalry.TownA].Name;
        check(HistoryAtlas.Read(namesake,"city",rivalry.TownA,w).Sections.Any(section=>section.Title=="Civic rivalries and memory"&&section.Links.Any(l=>l.Id==rivalry.TownB&&l.Name.EndsWith(" (another settlement)"))),"Namesake rival cities are explicitly distinguished instead of looking like a self-rivalry");
        var memoryCopy=next.Copy();memoryCopy.History.Modern.CivicRivalries![0].IntensityPermille=1;
        check(next.History.Modern.CivicRivalries[0].IntensityPermille!=1&&memoryCopy.ComputeHash()!=next.Hash,"Civic memory is detached in checkpoints and participates in authoritative integrity");
        memoryCopy.History.Modern.CivicRivalries[0].TownB=memoryCopy.History.Modern.CivicRivalries[0].TownA;memoryCopy.Seal();bool invalidCivicMemory=false;
        try{engine.Validate(memoryCopy);}catch(ArgumentException){invalidCivicMemory=true;}check(invalidCivicMemory,"Self-rivalries are rejected even when an arbitrary caller reseals its checkpoint");
        check(next.History.Agriculture.SelectMany(a=>a.Cultivars).Where(c=>c!=null).All(c=>c.YieldPermille>=650&&c.YieldPermille<=1300),"Selection is bounded rather than creating unlimited agricultural yield");
        check(next.History.Agriculture.Where(a=>a.HusbandryYears!=null).All(a=>Enumerable.Range(0,6).All(k=>a.AdoptedYear[k]>0||a.HusbandryYears[k]==0)),"Only adopted crops accumulate actual local cultivation practice");
        var town=next.Society.Settlements.First(t=>next.History.Towns[t.Id].Cropping?.CropIds.Length>0);
        check(HistoryAtlas.Read(next,"city",town.Id,w).Sections.Any(section=>section.Title=="Crops and cultivated food"),"Local cropping is inspectable in the city atlas");
        w.Prehistory=next;var binary=WorldFile.Read(WorldFile.Write(w));check(new Prehistory(binary).AdvanceYears(binary.Prehistory,13).Hash==engine.AdvanceYears(next,13).Hash,"Binary continuation preserves modern history");
        check(StateHash.Compute(w)==geography&&old.ComputeHash()==oldHash,"Modern history cannot reshape terrain or mutate its source");
        var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;try{engine.AdvanceYears(next,1,cancelled.Token);}catch(OperationCanceledException){stopped=true;}check(stopped&&next.ComputeHash()==next.Hash,"Cancelled processing leaves the published checkpoint intact");
        string output=Environment.GetEnvironmentVariable("MODERN_OUTPUT")??"D:/WorldGen-modern-20261014/modern-tests";Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,"Earth.world"),WorldFile.Write(w));
        var freshWorld=new World();foreach(var field in typeof(World).GetFields())field.SetValue(freshWorld,field.GetValue(w));
        freshWorld.Prehistory=engine.SetTechnologyCeiling(next,HistoricalTechnologyCeiling.Industrial);string priorHash=freshWorld.Prehistory.Hash;
        var freshEngine=new Prehistory(freshWorld);var foragers=freshEngine.Prepare();freshEngine.Validate(foragers);
        check(foragers.History.Modern.TechnologyCeiling==HistoricalTechnologyCeiling.Industrial&&foragers.History.Modern.Shipping.Length==0&&foragers.History.Modern.CivicRivalries==null,"A fresh start retains only the user's technology limit, never old ports or campaign/city memory");
        string foragerFolder=Path.Combine(output,"forager-review");Directory.CreateDirectory(foragerFolder);HistoryAuditReview.Write(freshWorld,freshEngine,foragers,foragerFolder);
        check(File.ReadAllText(Path.Combine(foragerFolder,"review.txt")).Contains("0 inhabited settlement sites")&&freshWorld.Prehistory.ComputeHash()==priorHash,"A zero-culture review is valid and cannot mutate the older history it is reviewing alongside");
        Console.WriteLine("Modern mechanism checkpoint "+next.Hash);
    }
}
