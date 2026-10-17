using System.Reflection;
using System.Runtime.Serialization;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryPreparedFieldTests
{
    static object Call(Prehistory engine,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args);
    static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name)!.GetValue(value)!;
    public static void Run(Action<bool,string> check)
    {
        var forest=new HabitatCell{Habitat=Habitat.Forest,ColdTenthsC=20,AridityMilli=1200,SoilFertilityPermille=650};
        double gain=HistoricalPreparedFieldEffects.ClearingGain(forest,0);
        check(Math.Abs(gain-1/(.7*.85))<1e-12,"Prepared ordinary forest removes only the existing area and transfer clearance factors.");
        check(HistoricalPreparedFieldEffects.ClearingGain(forest,1)<gain,"Already alluvial plot area is not charged or rewarded with the forest rainfed-area factor twice.");
        var wet=new HabitatCell{Habitat=Habitat.Forest,ColdTenthsC=220,AridityMilli=1600,SoilFertilityPermille=400,WaterDeficitMm=0};
        check(HistoricalPreparedFieldEffects.ClearingGain(wet,0)==1&&!HistoricalPreparedFieldEffects.OrdinaryForest(wet),"Ever-wet tropical forest cannot use ordinary temperate clearance to waive leaching and establishment limits.");
        var jungle=new HabitatCell{Habitat=Habitat.Forest,Biome=Biome.TropicalRainforest,ColdTenthsC=170,AridityMilli=1600,SoilFertilityPermille=400};
        check(HistoricalPreparedFieldEffects.ClearingGain(jungle,0)==1,"Tropical rainforest retains its own clearing constraints even at an ever-wet classifier boundary.");
        check(HistoricalPreparedFieldEffects.Landscape(.09,gain,0,500)==.09,"An unpaid plot keeps virgin forest productivity.");
        check(HistoricalPreparedFieldEffects.Landscape(.09,gain,500,500)>.15,"A genuinely prepared cohort can use ordinary forest plots with the same viable crop physiology.");
        check(HistoricalPreparedFieldEffects.Landscape(.09,gain,500,30000)<.092,"Five hundred prepared farmers cannot clear an entire thirty-thousand-person region by label.");
        check(HistoricalPreparedFieldEffects.Landscape(0,gain,500,500)==0,"Clearing cannot create a viable crop from zero physical fit.");
        check(HistoricalPreparedFieldEffects.Capacity(100000,gain,500)<100500,"A small paid cohort cannot multiply a huge unworked catchment's capacity.");
        check(HistoricalPreparedFieldEffects.Food(501)==251&&HistoricalPreparedFieldEffects.Investment(501)==251,"Preparation quotes finite additional labor rations and material investment, rounded upward once.");
        check(HistoricalPreparedFieldEffects.Remaining(500,100,120)<500&&HistoricalPreparedFieldEffects.Remaining(500,100,260)==0,"Vacated clearing regrows rather than granting free fields to an ancient refoundation.");
        var p=new HistoricalPreparedPlots{SinceYear=100,Cells=new HistoricalCellTree(600).With(12,500),RegionalCapacity=new HistoricalCellTree(3).With(1,500),RegionalEpoch=new HistoricalCellTree(3)};var copy=p.Copy();copy.Cells=copy.Cells.With(12,1000);
        check(p.Cells[12]==500&&copy.Cells[12]==1000,"Prepared plots share immutable roots but detach every local edit.");
        var serializer=new DataContractSerializer(typeof(HistoricalPreparedPlots));using var stream=new MemoryStream();serializer.WriteObject(stream,p);stream.Position=0;var restored=(HistoricalPreparedPlots)serializer.ReadObject(stream)!;
        check(StateHash.Compute(restored)==StateHash.Compute(p),"The saved sparse prepared-field ledger round-trips exact capacity and chronology.");
        check(StateHash.Compute(new HistoricalColonialConnections())=="3e6f409c4cdc77e11d2119ec182e566258a65f5eaa5265d0ebc2bd8ea6d094ea","Null prepared-field extension preserves literal old colonial canonical bytes.");
    }
    public static void World(string path,Action<bool,string> check,bool cold=true,int revision=16)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;var engine=new Prehistory(world);engine.Validate(source);string original=source.ComputeHash();
        var beginning=engine.Prepare(source.Origin);var freshPlots=beginning.History.Aggregates.Empires.Connections.PreparedPlots;
        check(beginning.History.Aggregates.Empires.Connections.Realms.Version>=16&&freshPlots!=null&&beginning.Society.Settlements.Length==0&&freshPlots.Cells.Fingerprint()==new HistoricalCellTree(engine.CellPatch.Length*6).Fingerprint(),"An actually fresh history initializes the current prepared-field extension without copying old farms or granting unworked land.");
        var started=engine.Start(beginning);var early=engine.AdvanceYears(started,3);var earlySplit=engine.AdvanceYears(engine.AdvanceYears(started,1),2);
        check(early.Hash==earlySplit.Hash&&early.History.Aggregates.Empires.Connections.PreparedPlots.RegionalEpoch.Fingerprint()==freshPlots.RegionalEpoch.Fingerprint(),"Fresh band continuation is publication-partition exact and does not fill a world-sized empty-field epoch ledger.");
        string originalPlots=source.History.Aggregates.Empires.Connections.PreparedPlots==null?"null":StateHash.Compute(source.History.Aggregates.Empires.Connections.PreparedPlots);
        var s=engine.UpgradeRealmLifecycles(source).Copy();
        if(revision>=17)check(s.History.Aggregates.Empires.Connections.PreparedPlots.Version>=2,"The public explicit upgrade itself migrates a preceding ledger to Version2 before any diagnostic setup call.");
        check(s.History.Aggregates.TotalPopulation==source.History.Aggregates.TotalPopulation&&s.Society.Settlements.Select(t=>t.FoodReserve).SequenceEqual(source.Society.Settlements.Select(t=>t.FoodReserve))&&s.History.Polities.Select(p=>p.Treasury).SequenceEqual(source.History.Polities.Select(p=>p.Treasury)),"Explicit ledger migration changes no actual population, warehouse reserve or court treasury.");
        s.History.Aggregates.Empires.Connections.Realms.Version=revision;if(revision==16&&source.History.Aggregates.Empires.Connections.PreparedPlots==null)s.History.Aggregates.Empires.Connections.PreparedPlots=null;Call(engine,"InitializePreparedPlots",s);Call(engine,"PrepareAggregateRelations",s);Call(engine,"ValidatePreparedPlots",s);
        if(source.History.Aggregates.Empires.Connections.PreparedPlots is HistoricalPreparedPlots precedingPlots)check((bool)typeof(HistoricalCellTree).GetMethod("AllPresentValues",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(precedingPlots.Cells,new object[]{new Func<int,int,bool>((key,value)=>key%6==5||s.History.Aggregates.Empires.Connections.PreparedPlots.Cells[key]==value)})!,"Sparse ledger migration preserves every paid plot's raw capacity, physical dates and holder; only account epoch encoding changes.");
        check(StateHash.Compute(s.History.Agriculture)==StateHash.Compute(source.History.Agriculture),"Explicit prepared-field upgrade changes no global cultivar, crop adoption or practice history.");
        var a=s.History.Aggregates;var plots=a.Empires.Connections.PreparedPlots;
        long occupied=s.Society.Settlements.Where(t=>a.Living.Cities[t.Id].AbandonedYear<0).Sum(t=>(long)Math.Max(0,plots.Cells[t.Cell*6]));
        long actual=s.Society.Settlements.Where(t=>a.Living.Cities[t.Id].AbandonedYear<0).Sum(t=>a.Cities[t.Id].Rural);
        check(occupied>0&&occupied<=actual,"Prospective setup recognizes only actual occupied rural fields, never full catchment potential.");
        check((source.History.Aggregates.Empires.Connections.PreparedPlots==null?"null":StateHash.Compute(source.History.Aggregates.Empires.Connections.PreparedPlots))==originalPlots,"A preceding save's original field extension is not silently assigned or modified during explicit upgrade of its detached copy.");
        var town=s.Society.Settlements.First(t=>a.Living.Cities[t.Id].AbandonedYear<0&&a.Cities[t.Id].Rural>=100&&s.Society.Cultures[t.CultureId].Farming>=180&&HistoricalPreparedFieldEffects.OrdinaryForest(world.Ecology.Habitat[t.Cell])&&(double)Call(engine,"LivingCropLandscape",s,t.CultureId,t.Cell,false)>0);
        var probe=s.Copy();var pp=probe.History.Aggregates.Empires.Connections.PreparedPlots;int key=town.Cell*6;pp.Cells=pp.Cells.With(key,0).With(key+2,probe.Years-20);
        var payer=probe.Society.Settlements[town.Id];payer.FoodReserve=Math.Max(payer.FoodReserve,payer.Population*4+50000);int owner=probe.History.Aggregates.Resolve(payer.PolityId,probe.History);if(owner>=0)probe.History.Polities[owner].Treasury=Math.Max(50000,probe.History.Polities[owner].Treasury);
        long farmers=Math.Min(500,probe.History.Aggregates.Cities[town.Id].Rural);object quote=Call(engine,"QuotePreparedFields",probe,town.CultureId,town.Cell,town.Id,farmers,-1,-2);
        check(quote!=null&&Get<long>(quote,"Farmers")==farmers&&Get<double>(quote,"Landscape")>(double)Call(engine,"CarriedCropLandscape",probe,town.CultureId,town.Cell,false,town.Id,-1),"An actual occupied ordinary-forest field quotes its actual local carried seed and a bounded improved plot.");
        long food=payer.FoodReserve,cash=owner>=0?probe.History.Polities[owner].Treasury:0,people=probe.History.Aggregates.TotalPopulation;
        Call(engine,"CommitPreparedFields",probe,quote,town.Id);
        check(payer.FoodReserve==food-Get<long>(quote,"Food")&&(owner<0||probe.History.Polities[owner].Treasury==cash-Get<long>(quote,"Investment"))&&probe.History.Aggregates.TotalPopulation==people,"A committed plot consumes exactly its real quoted food and treasury and creates no farmers.");
        check((long)Call(engine,"PreparedFieldCapacity",probe,town.Cell)==farmers,"The paid preparation credits only the actual cohort, not the town's large catchment.");
        var occupiedRural=probe.Copy();occupiedRural.History.Aggregates.TownAt=occupiedRural.History.Aggregates.TownAt.With(town.Cell,-1);Call(engine,"RecordPreparedField",occupiedRural,town.Cell,farmers,-1);
        object incoming=Call(engine,"QuotePreparedFields",occupiedRural,town.CultureId,town.Cell,town.Id,farmers,-1,-2);
        check(Get<long>(incoming,"NewPreparation")==farmers,"A second incoming cohort cannot reuse the first cohort's still-occupied paid plots for free.");
        Call(engine,"CommitPreparedFields",occupiedRural,incoming,-1);
        check((long)Call(engine,"PreparedFieldCapacity",occupiedRural,town.Cell)==farmers*2,"Two paid rural cohorts accumulate exactly their two investments, rather than overwrite or double-book one allowance.");
        long sameYearFood=payer.FoodReserve,sameYearCash=owner>=0?probe.History.Polities[owner].Treasury:0;Call(engine,"AdvancePreparedTownFields",probe,town.Id,1);
        check(payer.FoodReserve==sameYearFood&&(owner<0||probe.History.Polities[owner].Treasury==sameYearCash),"A field committed this year is not charged again by its city's same-year review.");
        var collapse=probe.Copy();collapse.History.Aggregates.Empires.Connections.Realms.Version=17;collapse.Years+=20;collapse.Timeline.Seconds=(long)collapse.Years*SimulationTime.Year;Call(engine,"ChangeAggregateCityPopulation",collapse,town.Id,collapse.Society.Settlements[town.Id].Population,collapse.History.Aggregates.Cities[town.Id].Suburban,50L);
        var priorCollapse=collapse.Copy();priorCollapse.History.Aggregates.Empires.Connections.Realms.Version=16;
        var priorCells=priorCollapse.History.Aggregates.Empires.Connections.PreparedPlots.Cells;long collapseFood=collapse.Society.Settlements[town.Id].FoodReserve,collapseCash=owner>=0?collapse.History.Polities[owner].Treasury:0,collapsePeople=collapse.History.Aggregates.TotalPopulation;
        Call(engine,"AdvancePreparedTownFields",collapse,town.Id,20);Call(engine,"AdvancePreparedTownFields",priorCollapse,town.Id,20);
        check(engine.PreparedFarmersAt(collapse,town.Cell)==50&&collapse.Society.Settlements[town.Id].FoodReserve==collapseFood&&(owner<0||collapse.History.Polities[owner].Treasury==collapseCash)&&collapse.History.Aggregates.TotalPopulation==collapsePeople,"Revision17 partial rural collapse withdraws unused maintained-field credit without making food, people or treasury payments.");
        check(engine.PreparedFarmersAt(priorCollapse,town.Cell)==farmers&&priorCollapse.History.Aggregates.Empires.Connections.PreparedPlots.Cells.Fingerprint()==priorCells.With(key+2,priorCollapse.Years).Fingerprint(),"Revision16 partial-collapse review preserves its literal preceding field tree update, not the new pruning rule.");
        var poor=s.Copy();poor.Society.Settlements[town.Id].FoodReserve=0;var poorQuote=Call(engine,"QuotePreparedFields",poor,town.CultureId,town.Cell,town.Id,farmers,-1,-2);
        check(!(bool)Call(engine,"PreparedQuoteAffordable",poor,poorQuote,0L,0L),"An unfunded field quote cannot authorize cleared productivity or materialization.");
        var abandoned=probe.Copy();Call(engine,"VacatePreparedTownFields",abandoned,town.Id);abandoned.Years+=160;
        check((long)Call(engine,"PreparedFieldCapacity",abandoned,town.Cell)==0,"A real field record expires after long actual vacancy, without scanning surrounding cells.");
        var rural=s.Copy();var rp=rural.History.Aggregates.Empires.Connections.PreparedPlots;int ruralCell=Enumerable.Range(0,engine.CellPatch.Length).First(cell=>engine.CellPatch[cell]>=0&&rural.History.Aggregates.TownAt[cell]<0&&rp.Cells[cell*6]<0);int region=engine.CellPatch[ruralCell];
        rural.Population[region]=30000;Call(engine,"RecordPreparedField",rural,ruralCell,500L,-1);long baseCapacity=30000;
        check((long)Call(engine,"PreparedRegionalCapacity",rural,region,baseCapacity)==baseCapacity+500,"A saved rural arrival adds exactly its five-hundred-person paid plot allowance, not all thirty-thousand forager residents.");
        rural.Population[region]=0;Call(engine,"PreparedRegionalCapacity",rural,region,baseCapacity);rural.Years++;
        check((long)Call(engine,"PreparedFieldCapacity",rural,ruralCell)==0,"An emptied rural account resets its plot epoch so ancient rural fields cannot be reclaimed for free.");
        var old=probe.Copy();old.History.Aggregates.Empires.Connections.Realms.Version=15;string root=old.History.Aggregates.Empires.Connections.PreparedPlots.Cells.Fingerprint();old.Years+=100;Call(engine,"AdvancePreparedTownFields",old,town.Id,100);
        check(old.History.Aggregates.Empires.Connections.PreparedPlots.Cells.Fingerprint()==root&&(double)Call(engine,"PreparedTownLandscape",old,town.Id,false)==(double)Call(engine,"LivingCropLandscape",old,town.CultureId,town.Cell,false),"Revision15 remains exactly on unpaid preceding field behavior even if a diagnostic ledger is attached.");
        if(original=="eacf2838970bf41cf3ff49bb2d7edb1ec7911222c0df74a9e59d5f0afb1af4d6"){France(s,engine,check);FundedForest(s,engine,check);FundedTown(s,engine,check);}
        check(a.Empires.Connections.ProvisionedPorts.Ports.Where(port=>a.Living.Cities[port.TownId].AbandonedYear<0&&a.TownAt[s.Society.Settlements[port.TownId].Cell]==port.TownId).All(port=>a.Cities[port.TownId].Rural==0&&(engine.PreparedFarmersAt(s,s.Society.Settlements[port.TownId].Cell)==0||plots.Cells[s.Society.Settlements[port.TownId].Cell*6+4]!=port.TownId)),"Actual live import-dependent harbors receive no owned farming plots; a different rural holding at the same physical cell is not the port's harvest.");
        Call(engine,"ValidatePreparedPlots",s);s.Seal();
        if(cold)
        {
            engine.Validate(s);world.Prehistory=s;var loadedWorld=WorldFile.Read(WorldFile.Write(world));var restored=loadedWorld.Prehistory;var fresh=new Prehistory(loadedWorld);fresh.Validate(restored);
            check(s.Hash==restored.Hash&&StateHash.Compute(plots)==StateHash.Compute(restored.History.Aggregates.Empires.Connections.PreparedPlots),"Full-world save/load retains exact prepared plots, paid capacity and vacancy clocks.");
            var full=engine.AdvanceYears(s,12);var split=fresh.AdvanceYears(fresh.AdvanceYears(restored,5),7);engine.Validate(full);check(full.Hash==split.Hash,"Prepared-field continuation matches genuine cold save/load and5+7-year publication partitions.");
            Console.WriteLine("Prepared-field cold hashes: initial="+s.Hash+", twelve="+full.Hash+".");
        }
        check(source.ComputeHash()==original,"All actual-field probes leave the saved source exactly unchanged.");
        Console.WriteLine($"Prepared fields: revision{revision}/ledger{plots.Version}, actual town{town.Id}/cell{town.Cell}, farmers{farmers}, setup{occupied}/{actual}; cold={cold}.");
    }
    static void France(PrehistoryState s,Prehistory engine,Action<bool,string> check)
    {
        // Controlled causal fixture on the exact reviewed physical French edge.
        // Retuning a copied seed is NOT evidence that the historical replay has
        // already delivered that genotype or that clearing alone fixes Europe.
        const int origin=175424,target=176034,owner=3408,culture=51;var route=engine.Routes[12479];int from=engine.CellPatch[origin],to=engine.CellPatch[target];
        var p=s.Copy();var a=p.History.Aggregates;var holder=(HistoricalTownSeeds)Call(engine,"LocalSeedHolder",p,culture,origin,-1);
        var wheat=(holder?[0]?.Cultivar??p.History.Agriculture[culture].Cultivars[0]).Copy();wheat.DaylengthLatitudeTenths=495;
        if(holder==null)p.History.Agriculture[culture].Cultivars[0]=wheat;
        else
        {
            var slots=Enumerable.Range(0,6).Select(k=>holder[k]).ToArray();slots[0]=new HistoricalLocalSeedlot(wheat,p.Years,-1,origin,holder[0].PracticeYears,p.Years);
            a.Empires.Connections.LocalSeeds.Towns=a.Empires.Connections.LocalSeeds.Towns.With(holder.TownId,new HistoricalTownSeeds(holder.TownId,holder.FieldCell,slots));
        }
        double virgin=(double)Call(engine,"CarriedCropLandscape",p,culture,target,false,-1,origin);
        var areas=(double[])typeof(Prehistory).GetField("inhabitableArea",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;
        long local=(long)(a.Polities[owner].Remote*Math.Min(1,areas[origin]/a.Polities[owner].RemoteAreaKm2));
        long cohort=Math.Min(500,Math.Min(local/20,a.Polities[owner].Remote/100));
        object quote=Call(engine,"QuoteRemoteFields",p,culture,origin,target,owner,cohort);
        int supply=a.Empires.Territories.Sources[origin];for(int depth=0;supply<=-2&&depth<8;depth++)supply=a.Empires.Territories.Posts[-supply-2].ParentSource;
        Console.WriteLine($"French preflight: virgin={virgin:F6}, local={local}, cohort={cohort}, holder={holder?.TownId??-1}, source={supply}, supported={(supply>=0?a.Cities[supply].Supported:false)}, abandoned={(supply>=0?a.Living.Cities[supply].AbandonedYear:-2)}, food={(supply>=0?p.Society.Settlements[supply].FoodReserve:-1)}, treasury={p.History.Polities[owner].Treasury}, quote={(quote==null?"none":Get<double>(quote,"Landscape").ToString("F6"))}.");
        object physical=Call(engine,"QuotePreparedFields",p,culture,target,supply,cohort,origin,-1);
        check(physical!=null&&virgin<.12&&Get<double>(physical,"Landscape")>=.12,"The actual French substrate can cross the cultivation threshold with a locally matched copied seed and cohort-sized preparation; this does not assert actual funding or waive climate/peat constraints.");
        check(quote==null,"The actual G French source still lacks an affordable complete journey even after a copied seed is locally matched: clearing alone is not a ready historical crossing.");
        double effort=(double)Call(engine,"LivingTravelKm",p,p.Society.Settlements[supply].Cell,origin,culture)+(double)Call(engine,"LivingTravelKm",p,origin,target,culture);
        double transport=HistoricalModernEffects.LandTransport(p.History.Cultures[culture],p.Years);
        long journeyFood=HistoricalInlandSettlement.Provisions(cohort,effort,transport)+cohort/2,journeyCash=HistoricalInlandSettlement.Investment(cohort,effort,transport);
        Console.WriteLine($"French unfunded journey: source population{p.Society.Settlements[supply].Population}, effort{effort:F1}km-equivalent, food{journeyFood}+prep{Get<long>(physical,"Food")}+half-cityreserve, investment{journeyCash}+prep{Get<long>(physical,"Investment")}.");
        // Explicit conditional financed scenario, NOT observed G stock. Keep the
        // actual live source, route, physical soil and real production rolls.
        p.Society.Settlements[supply].FoodReserve=Math.Max(p.Society.Settlements[supply].FoodReserve,journeyFood+Get<long>(physical,"Food")+p.Society.Settlements[supply].Population/2+1);
        p.History.Polities[owner].Treasury=Math.Max(p.History.Polities[owner].Treasury,2*(journeyCash+Get<long>(physical,"Investment"))+1);
        quote=Call(engine,"QuoteRemoteFields",p,culture,origin,target,owner,cohort);
        check(quote!=null,"A separately labeled financed control can quote the same real French route and physical crop without weakening its funding requirements.");
        long overfill=Math.Max(0,a.Regions[to].Capacity+5000-p.Population[to]);p.Population[to]+=overfill;a.TotalPopulation+=overfill;
        var noFood=p.Copy();foreach(var t in noFood.Society.Settlements)t.FoodReserve=0;
        check(!(bool)Call(engine,"TryRemoteFarmerFrontier",noFood,from,route,100,0),"The same overfull-forager French crossing cannot use quoted clearance without real startup food.");
        var old=p.Copy();old.History.Aggregates.Empires.Connections.Realms.Version=15;
        check(!(bool)Call(engine,"TryRemoteFarmerFrontier",old,from,route,100,0),"The preceding virgin-land threshold genuinely blocks this matched-seed French fixture.");
        long remote=a.Polities[owner].Remote,total=a.TotalPopulation,food=p.Society.Settlements.Sum(t=>t.FoodReserve),cash=p.History.Polities[owner].Treasury,before=p.Population[to];int towns=p.Society.Settlements.Length,states=p.History.Polities.Length;
        var queue=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;bool success=false;
        for(int year=0;year<256&&!success;year++)
        {
            p.Years=s.Years+year;p.Timeline.Seconds=(long)p.Years*SimulationTime.Year;queue.SetValue(engine,new SimulationEventQueue(Array.Empty<SimulationEvent>()));
            try{success=(bool)Call(engine,"TryRemoteFarmerFrontier",p,from,route,100,0);}finally{queue.SetValue(engine,null);}
        }
        long moved=p.Population[to]-before;
        check(success&&moved>=50&&moved<=500&&a.Polities[owner].Remote==remote-moved&&a.TotalPopulation==total,"A real named-roll French land crossing can commit its paid small plot despite unrelated forager overpopulation, conserving the actual Remote ledger.");
        check(p.Society.Settlements.Sum(t=>t.FoodReserve)<food&&p.History.Polities[owner].Treasury<cash&&(long)Call(engine,"PreparedFieldCapacity",p,target)==moved,"The successful French arrival consumes real provisions/materials and saves exactly its actual prepared cohort.");
        check(p.Society.Settlements.Length==towns&&p.History.Polities.Length==states&&HistoricalCells.Polity(p,target)<0,"A paid French small plot grants no city, country or sovereignty.");
        Console.WriteLine($"Controlled French physical frontier: virgin{virgin:F6}, quote{Get<double>(quote,"Landscape"):F6}, actual paid migrants{moved}, year{p.Years}; copied locally matched seed AND separately financed stock, not an observed historical diffusion claim.");
    }
    static void FundedForest(PrehistoryState s,Prehistory engine,Action<bool,string> check)
    {
        var a=s.History.Aggregates;var areas=(double[])typeof(Prehistory).GetField("inhabitableArea",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;
        Prehistory.Route selected=null;int departure=-1,destination=-1,owner=-1,from=-1,to=-1;
        foreach(var route in engine.Routes.Where(route=>!route.Sea&&route.LandCellA>=0&&route.LandCellB>=0))
        {
            for(int direction=0;direction<2;direction++)
            {
                int cell=direction==0?route.LandCellA:route.LandCellB,target=direction==0?route.LandCellB:route.LandCellA,region=direction==0?route.A:route.B,other=direction==0?route.B:route.A,payer=HistoricalCells.Polity(s,cell);
                if(payer<0||a.Regions[other].Culture>=0||HistoricalCells.Culture(s,target)>=0||HistoricalCells.Polity(s,target)>=0||!HistoricalPreparedFieldEffects.OrdinaryForest(engine.World.Ecology.Habitat[target]))continue;
                var account=a.Polities[payer];int culture=s.History.Polities[payer].CultureId;
                if(account.Remote<250||account.RemoteAreaKm2<=0||s.Society.Cultures[culture].Farming<250||a.PopulationCohort(a.Cells[cell*5+1])!=payer||!(bool)Call(engine,"InstitutionalRouteAllowed",s,cell,target,culture))continue;
                long local=(long)(account.Remote*Math.Min(1,areas[cell]/account.RemoteAreaKm2)),cohort=Math.Min(500,Math.Min(local/20,account.Remote/100));
                if(cohort<50)continue;object quote=Call(engine,"QuoteRemoteFields",s,culture,cell,target,payer,cohort);
                if(quote==null||Get<double>(quote,"Landscape")<.12||(long)Call(engine,"CarriedFarmPotential",s,culture,target,-1,cell)<500)continue;
                selected=route;departure=cell;destination=target;owner=payer;from=region;to=other;break;
            }
            if(selected!=null)break;
        }
        check(selected!=null,"The actual saved world has at least one ordinary-forest frontier with real viable seed, a live supplier and affordable unaltered food/cash stocks.");
        var probe=s.Copy();long remote=probe.History.Aggregates.Polities[owner].Remote,total=probe.History.Aggregates.TotalPopulation,before=probe.Population[to],food=probe.Society.Settlements.Sum(t=>t.FoodReserve),cash=probe.History.Polities[owner].Treasury;int towns=probe.Society.Settlements.Length,states=probe.History.Polities.Length;
        var queue=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;bool success=false;
        for(int year=0;year<256&&!success;year++)
        {
            probe.Years=s.Years+year;probe.Timeline.Seconds=(long)probe.Years*SimulationTime.Year;queue.SetValue(engine,new SimulationEventQueue(Array.Empty<SimulationEvent>()));
            try{success=(bool)Call(engine,"TryRemoteFarmerFrontier",probe,from,selected,100,0);}finally{queue.SetValue(engine,null);}
        }
        long moved=probe.Population[to]-before;
        check(success&&moved>=50&&moved<=500&&probe.History.Aggregates.Polities[owner].Remote==remote-moved&&probe.History.Aggregates.TotalPopulation==total,"An actual funded forest frontier commits a real household cohort and conserves population without any fixture seed, food or treasury endowment.");
        check(probe.Society.Settlements.Sum(t=>t.FoodReserve)<food&&probe.History.Polities[owner].Treasury<cash&&engine.PreparedFarmersAt(probe,destination)==moved,"The actual ready frontier pays its existing warehouse and treasury and records precisely its prepared farmers.");
        check(probe.Society.Settlements.Length==towns&&probe.History.Polities.Length==states&&HistoricalCells.Polity(probe,destination)<0,"The real funded forest frontier creates no town, state or claimed sovereignty.");
        Console.WriteLine($"Actual unendowed funded forest frontier: route{Array.IndexOf(engine.Routes,selected)}, cells{departure}->{destination}, owner{owner}, paidmigrants{moved}, year{probe.Years}.");
    }
    static void FundedTown(PrehistoryState s,Prehistory engine,Action<bool,string> check)
    {
        var a=s.History.Aggregates;int parent=-1,target=-1;object quote=null;
        foreach(var source in s.Society.Settlements)
        {
            if(a.Living.Cities[source.Id].AbandonedYear>=0||source.Population<1000||!a.Cities[source.Id].Supported||a.Living.Cities[source.Id].DeclineYears>0||a.Resolve(source.PolityId,s.History)<0||(long)Call(engine,"LivingFounderAvailability",s,source.Id)<1600)continue;
            for(int trial=0;trial<3;trial++)
            {
                int cell=(int)Call(engine,"LivingLocalTownCandidate",s,source.Id,trial);
                if(cell<0||!HistoricalPreparedFieldEffects.OrdinaryForest(engine.World.Ecology.Habitat[cell])||!(bool)Call(engine,"CanFoundLivingTown",s,source.CultureId,cell,source.Id))continue;
                quote=Call(engine,"QuoteFoundingFields",s,source.CultureId,cell,source.Id);if(quote==null)continue;
                int host=HistoricalCells.Polity(s,cell),owner=a.Resolve(source.PolityId,s.History);
                if(host>=0&&host!=owner||engine.World.Landmass[cell]!=engine.World.Landmass[source.Cell]||!(bool)Call(engine,"InstitutionalRouteAllowed",s,source.Cell,cell,source.CultureId)||(bool)Call(engine,"RespectRecognizedClaim",s,owner,cell)||(double)Call(engine,"LivingTravelKm",s,source.Cell,cell,source.CultureId)>(double)Call(engine,"TerritorialServiceRadius",s,owner,cell)*1.5||a.Polities[owner].Receipts<=a.Polities[owner].ServiceCost)continue;
                parent=source.Id;target=cell;break;
            }
            if(parent>=0)break;
        }
        check(parent>=0,"The actual saved world has a funded ordinary-forest town-founding opportunity on its existing local physical proposal path.");
        var p=s.Copy();var town=p.Society.Settlements[parent];int polity=p.History.Aggregates.Resolve(town.PolityId,p.History);long food=town.FoodReserve,cash=p.History.Polities[polity].Treasury,total=p.History.Aggregates.TotalPopulation,available=(long)Call(engine,"LivingFounderAvailability",p,parent),urban=Math.Min(250,available/12),rural=Math.Min(available/3,Math.Max(800,urban*8));
        var costArgs=new object[]{p,parent,target,urban+rural,0L,0L};typeof(Prehistory).GetMethod("LivingInlandSettlementCosts",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,costArgs);long provisions=(long)costArgs[4],investment=(long)costArgs[5];
        var queue=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;queue.SetValue(engine,new SimulationEventQueue(Array.Empty<SimulationEvent>()));bool success;
        try{success=(bool)Call(engine,"TryFoundLivingLocalTown",p,parent,target);}finally{queue.SetValue(engine,null);}
        int founded=p.History.Aggregates.TownAt[target];
        check(success&&founded>=s.Society.Settlements.Length&&p.History.Aggregates.Cities[founded].Rural==rural&&engine.PreparedFarmersAt(p,target)==rural&&p.History.Aggregates.TotalPopulation==total,"The actual funded inland caller transfers the real farming cohort and commits exactly its prepared fields without creating people.");
        check(town.FoodReserve==food-urban-rural-provisions-Get<long>(quote,"Food")&&p.History.Polities[polity].Treasury==cash-investment-Get<long>(quote,"Investment"),"The actual inland founding caller pays household startup, carriage and preparation once each from the original source accounts.");
        Console.WriteLine($"Actual unendowed inland prepared farms: parent{parent}, target{target}, newtown{founded}, rural{rural}, preparationfood{Get<long>(quote,"Food")}, investment{Get<long>(quote,"Investment")}.");
    }
}
