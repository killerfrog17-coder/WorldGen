using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryInlandSettlementTests
{
    static object Call(Prehistory engine,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalInlandSettlement.ReachFactor(4.5,1000,false)==1&&HistoricalInlandSettlement.ReachFactor(1,1000,true)==1,"Neither hypothetical transport without a funded state nor funded ancient works grant modern migration reach");
        check(HistoricalInlandSettlement.ReachFactor(4.5,1000,true)==4.5&&HistoricalInlandSettlement.ReachFactor(4.5,0,true)<2,"Actual public works moderate the deployed-transport proposal window");
        check(HistoricalInlandSettlement.ProbeScale(4.5,0)==1&&HistoricalInlandSettlement.ProbeScale(4.5,2)==4.5,"One probe stays genuinely local while the outer probe uses the supported longer reach");
        check(HistoricalInlandSettlement.ReachFactor(10000,10000,true)==4.5,"Proposal reach retains a finite1080km target radius even under extreme inputs");
        foreach(var point in new[]{new V(1,0,0),new V(0,0,1),new V(0,0,-1),new V(.2,.7,.4).Unit})
        for(int trial=0;trial<3;trial++)
        {
            var early=HistoricalSettlementNetworks.LocalProbe(point,6371,.173,trial);var modern=HistoricalSettlementNetworks.TransportProbe(point,6371,.173,trial,4.5);
            double expected=(120+60*trial)*HistoricalInlandSettlement.ProbeScale(4.5,trial);
            check(Math.Abs(D.Angle(point,early)*6371-(120+60*trial))<.1&&Math.Abs(D.Angle(point,modern)*6371-expected)<.1,"Both ancient and modern probes preserve physical spherical distances at equator, poles and midlatitudes");
            var repeat=HistoricalSettlementNetworks.TransportProbe(point,6371,.173,trial,4.5);check(modern.X==repeat.X&&modern.Y==repeat.Y&&modern.Z==repeat.Z,"Modern proposal geometry is deterministic and consumes no mutable randomness");
        }
        long people=2250,near=HistoricalInlandSettlement.Provisions(people,120,1),far=HistoricalInlandSettlement.Provisions(people,1000,1),rail=HistoricalInlandSettlement.Provisions(people,1000,4.5);
        check(far>near&&far>rail&&rail>0,"Long inland journeys pay more real food, while deployed transport reduces travel time rather than making it free");
        check(HistoricalInlandSettlement.Investment(people,1000,1)>HistoricalInlandSettlement.Investment(people,1000,4.5)&&HistoricalInlandSettlement.Investment(people,120,1)>people/5,"Actual distance and transport affect carriage beyond the existing construction cost");
        check(HistoricalInlandSettlement.TravelDays(1000,1)>HistoricalInlandSettlement.TravelDays(1000,4.5)&&HistoricalInlandSettlement.TravelDays(0,4.5)==7,"Travel includes a finite staging allowance and a transport-sensitive effective family journey");
        check(HistoricalInlandSettlement.Provisions(0,1000,1)==0&&HistoricalInlandSettlement.Investment(0,1000,1)==0,"An empty migrant cohort cannot create or consume a fake supply account");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string oldHash=original.ComputeHash();var engine=new Prehistory(world);engine.Validate(original);var s=original.Copy();var a=s.History.Aggregates;var h=s.History;
        Call(engine,"PrepareAggregateRelations",s);int parent=-1,target=-1,owner=-1;long people=0,provisions=0,investment=0;
        foreach(var town in s.Society.Settlements.Where(t=>a.Living.Cities[t.Id].AbandonedYear<0))
        {
            int polity=a.Resolve(town.PolityId,h);if(polity<0||(double)Call(engine,"LivingLocalTownRate",s,town.Id)<=0)continue;
            for(int trial=1;trial<3;trial++)
            {
                int site=(int)Call(engine,"LivingLocalTownCandidate",s,town.Id,trial),host=HistoricalCells.Polity(s,site);double km=D.Angle(world.Surface.Cells[town.Cell].Position,world.Surface.Cells[site].Position)*world.Recipe.RadiusKm;
                if(km<300||host>=0&&host!=polity||(bool)Call(engine,"RespectRecognizedClaim",s,polity,site)||!(bool)Call(engine,"CanFoundLivingTown",s,town.CultureId,site,town.Id)||!(bool)Call(engine,"InstitutionalRouteAllowed",s,town.Cell,site,town.CultureId)||
                    (double)Call(engine,"LivingTravelKm",s,town.Cell,site,town.CultureId)>(double)Call(engine,"TerritorialServiceRadius",s,polity,site)*1.5)continue;
                long available=(long)Call(engine,"LivingFounderAvailability",s,town.Id),founders=Math.Min(250,available/12),farms=Math.Min(available/3,Math.Max(800,founders*8)),p=founders+farms;
                object[] args={s,town.Id,site,p,0L,0L};Call(engine,"LivingInlandSettlementCosts",args);long food=(long)args[4],cost=(long)args[5];
                if(town.FoodReserve<p+food+town.Population/4||h.Polities[polity].Treasury<cost*2)continue;
                parent=town.Id;target=site;owner=polity;people=p;provisions=food;investment=cost;break;
            }
            if(parent>=0)break;
        }
        check(parent>=0,"Actual saved modern network proposes a funded compatible destination beyond the old240km window, without inventing resources");
        var depot=s.Society.Settlements[parent];var ruler=h.Polities[owner];var city=a.Cities[parent];long money=ruler.Treasury,foodReserve=depot.FoodReserve,total=a.TotalPopulation;int count=a.Cities.Length;
        double rate=(double)Call(engine,"LivingLocalTownRate",s,parent);int publicWorks=h.Dynamics.States[owner].PublicWorks;
        ruler.Treasury=0;check((double)Call(engine,"LivingSettlementProbeReach",s,parent)==1&&!(bool)Call(engine,"TryFoundLivingLocalTown",s,parent,target)&&a.Cities.Length==count,"An unfunded state loses expanded reach and cannot send a free long-range colony");ruler.Treasury=money;
        int works=h.Dynamics.States[owner].PublicWorks;h.Dynamics.States[owner].PublicWorks=0;double noWorks=(double)Call(engine,"LivingSettlementProbeReach",s,parent);h.Dynamics.States[owner].PublicWorks=works;
        check(noWorks<(double)Call(engine,"LivingSettlementProbeReach",s,parent),"The same deployed transport has less useful reach without funded works");
        var practice=h.Cultures[depot.CultureId].Technical;int rail=practice.Deployment[(int)HistoricalTechnology.Railways],engineDeployment=practice.Deployment[(int)HistoricalTechnology.CombustionEngines];
        practice.Deployment[(int)HistoricalTechnology.Railways]=practice.Deployment[(int)HistoricalTechnology.CombustionEngines]=0;
        check((double)Call(engine,"LivingSettlementProbeReach",s,parent)==1,"A wealthy public-works program without deployed rail or engine transport retains ancient proposal distances");
        practice.Deployment[(int)HistoricalTechnology.Railways]=rail;practice.Deployment[(int)HistoricalTechnology.CombustionEngines]=engineDeployment;
        depot.FoodReserve=people+depot.Population/4;
        check(!(bool)Call(engine,"TryFoundLivingLocalTown",s,parent,target)&&a.Cities.Length==count&&ruler.Treasury==money,"Startup food alone cannot waive the additional real journey rations");depot.FoodReserve=foodReserve;
        var crops=h.Agriculture[depot.CultureId];var adopted=crops.AdoptedYear;crops.AdoptedYear=new int[6];
        check(!(bool)Call(engine,"TryFoundLivingLocalTown",s,parent,target)&&a.TotalPopulation==total&&depot.FoodReserve==foodReserve,"Transport reach cannot manufacture locally usable crops or transfer households into barren farmland");crops.AdoptedYear=adopted;
        check((double)Call(engine,"LivingLocalTownRate",s,parent)==rate,"Geometry and travel charges do not boost the actor's annual settlement opportunity hazard");
        int blocked=world.Surface.Cells.Where(cell=>engine.CellPatch[cell.Id]>=0&&world.Landmass[cell.Id]==world.Landmass[depot.Cell]&&
            D.Angle(world.Surface.Cells[depot.Cell].Position,cell.Position)*world.Recipe.RadiusKm>5000).Select(cell=>cell.Id).First(cell=>!(bool)Call(engine,"InstitutionalRouteAllowed",s,depot.Cell,cell,depot.CultureId));
        check(!(bool)Call(engine,"TryFoundLivingLocalTown",s,parent,blocked)&&a.TotalPopulation==total&&ruler.Treasury==money&&depot.FoodReserve==foodReserve,"A physically unsupported long inland route cannot waive barriers or consume resources through an uncommitted foundation");
        string version=h.Version;h.Version=Prehistory.EnvironmentalHistoryVersion;object[] legacy={s,parent,target,people,0L,0L};Call(engine,"LivingInlandSettlementCosts",legacy);
        check((long)legacy[4]==0&&(long)legacy[5]==Math.Max(100,people/5)&&(double)Call(engine,"LivingSettlementProbeReach",s,parent)==1,"History31 retains its original unscaled proposal and startup-only construction costs");h.Version=version;
        var queue=new SimulationEventQueue(s.Timeline.Events);var field=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;field.SetValue(engine,queue);
        check((bool)Call(engine,"TryFoundLivingLocalTown",s,parent,target)&&a.Cities.Length==count+1,"An actually eligible modern inland route can transfer its real founders into the proposed town");
        check(a.TotalPopulation==total&&s.Society.Settlements[count].Population+a.Cities[count].Rural==people,"Longer reach conserves every global household rather than inventing distant natives or residents");
        check(depot.FoodReserve==foodReserve-people-provisions&&ruler.Treasury==money-investment,"Startup, travel food and fiscal carriage/construction are each charged exactly once");
        long foodAfter=depot.FoodReserve,moneyAfter=ruler.Treasury;
        check(!(bool)Call(engine,"TryFoundLivingLocalTown",s,parent,target)&&depot.FoodReserve==foodAfter&&ruler.Treasury==moneyAfter,"A failed/repeated foundation cannot debit supplies a second time");
        s.Timeline.Events=queue.Ordered();field.SetValue(engine,null);s.Seal();engine.Validate(s);
        var next=engine.AdvanceYears(s,3);var split=engine.AdvanceYears(engine.AdvanceYears(s,1),2);
        check(next.Hash==split.Hash,"Technology-scaled proposals remain exact across publication partitioning");
        world.Prehistory=next;var loaded=WorldFile.Read(WorldFile.Write(world));
        check(loaded.Prehistory.Hash==next.Hash&&new Prehistory(loaded).AdvanceYears(loaded.Prehistory,2).Hash==engine.AdvanceYears(next,2).Hash,"Actual founders, stores, route-derived costs and later events survive exact save/load continuation");
        check(original.ComputeHash()==oldHash,"The controlled inland migration fixture preserves its source checkpoint");
    }
}
