using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryRemoteFarmerTests
{
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));string input=world.Prehistory.Hash;
        var engine=new Prehistory(world);var s=world.Prehistory.Copy();var a=s.History.Aggregates;
        s.History.Aggregates.Empires.Connections.Realms.Version=9;
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,args)!;
        Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
        var queueField=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.NonPublic|BindingFlags.Instance)!;
        Prehistory.Route selected=null;int from=-1,trialYear=-1;PrehistoryState fixture=null;
        foreach(var route in engine.Routes.Where(r=>!r.Sea))
        {
            for(int direction=0;direction<2;direction++)
            {
                int region=direction==0?route.A:route.B,cell=direction==0?route.LandCellA:route.LandCellB;
                if(cell<0||s.Population[region]>=125||HistoricalCells.Polity(s,cell)<0)continue;
                int targetRegion=region==route.A?route.B:route.A,targetCell=direction==0?route.LandCellB:route.LandCellA,sourceOwner=HistoricalCells.Polity(s,cell),sourceCulture=s.History.Polities[sourceOwner].CultureId;
                if(a.Regions[targetRegion].Culture>=0||HistoricalCells.Culture(s,targetCell)>=0||HistoricalCells.Polity(s,targetCell)>=0
                    ||s.Society.Cultures[sourceCulture].Farming<250||(double)Call("LivingCropLandscape",s,sourceCulture,targetCell,false)<.12)continue;
                double transmission=(double)Call("EnvironmentalTransmission",s,cell,targetCell,sourceCulture,false);
                // Search actual production rolls; no force-success mode or
                // altered production probability is introduced for fixtures.
                for(int j=0;j<200;j++)
                {
                    double chance=1-Math.Exp(-.05*transmission);
                    if((double)Call("HistoryRoll","remote-farmer-departure-0",region,s.Years+j)>=chance)continue;
                    var attempt=s.Copy();attempt.Years=s.Years+j;attempt.Timeline.Seconds=(long)attempt.Years*SimulationTime.Year;
                    queueField.SetValue(engine,new SimulationEventQueue(Array.Empty<SimulationEvent>()));
                    bool dispatched;try{dispatched=(bool)Call("TryRemoteFarmerFrontier",attempt,region,route,100,0);}
                    finally{queueField.SetValue(engine,null);}
                    if(!dispatched)continue;selected=route;from=region;trialYear=attempt.Years;fixture=s.Copy();break;
                }
                if(fixture!=null)break;
            }
            if(fixture!=null)break;
        }
        if(fixture==null)throw new Exception("No actually provisioned remote-farmer frontier in this saved world.");
        fixture.Years=trialYear;fixture.Timeline.Seconds=(long)trialYear*SimulationTime.Year;
        int origin=from==selected.A?selected.LandCellA:selected.LandCellB,target=from==selected.A?selected.LandCellB:selected.LandCellA,to=from==selected.A?selected.B:selected.A;
        var h=fixture.History;var fa=h.Aggregates;int owner=HistoricalCells.Polity(fixture,origin),culture=h.Polities[owner].CultureId;
        long remote=fa.Polities[owner].Remote,polity=h.Polities[owner].Population,cultural=fixture.Society.Cultures[culture].Population,
            residents=fixture.Population[to],total=fa.TotalPopulation,food=fixture.Society.Settlements.Sum(t=>t.FoodReserve),treasury=h.Polities[owner].Treasury;
        int townCount=fixture.Society.Settlements.Length,stateCount=h.Polities.Length;
        var old=fixture.Copy();old.History.Aggregates.Empires.Connections.Realms.Version=8;
        var poor=fixture.Copy();poor.History.Polities[owner].Treasury=0;
        var empty=fixture.Copy();foreach(var town in empty.Society.Settlements)town.FoodReserve=0;
        var overfull=fixture.Copy();overfull.History.Aggregates.Empires.Connections.Realms.Version=12;
        long extra=Math.Max(0,overfull.History.Aggregates.Regions[to].Capacity+2000-overfull.Population[to]);
        overfull.Population[to]+=extra;overfull.History.Aggregates.TotalPopulation+=extra;
        var oldOverfull=overfull.Copy();oldOverfull.History.Aggregates.Empires.Connections.Realms.Version=11;
        var sea=new Prehistory.Route{A=selected.A,B=selected.B,Sea=true,LandCellA=selected.LandCellA,LandCellB=selected.LandCellB};
        foreach(var blocked in new[]{old,poor,empty})
            check(!(bool)Call("TryRemoteFarmerFrontier",blocked,from,selected,100,0),"Old rules, no actual treasury, and empty granaries cannot dispatch the new rural migration.");
        check(!(bool)Call("TryRemoteFarmerFrontier",fixture,from,sea,100,0),"The adjacent-land household route cannot become an overseas colony.");
        queueField.SetValue(engine,new SimulationEventQueue(Array.Empty<SimulationEvent>()));
        bool success;try{success=(bool)Call("TryRemoteFarmerFrontier",fixture,from,selected,100,0);}
        finally{queueField.SetValue(engine,null);}
        long moved=fixture.Population[to]-residents;
        check(success&&moved>=50&&moved<=500,"A real administered countryside with an empty regional pool can send a bounded household group.");
        check(fa.Polities[owner].Remote==remote-moved&&h.Polities[owner].Population==polity-moved&&fa.TotalPopulation==total,"People debit the actual shared Remote ledger and country, without changing world population.");
        check(fixture.Society.Cultures[culture].Population==cultural-moved,"The source cultural population loses the actual emigrants exactly once.");
        check(fixture.Society.Settlements.Sum(t=>t.FoodReserve)<food&&h.Polities[owner].Treasury<treasury,"Actual provisions and investment are paid, rather than conjured from an inspection estimate.");
        check(fixture.Society.Settlements.Length==townCount&&h.Polities.Length==stateCount&&HistoricalCells.Polity(fixture,target)<0,"Migration grants neither a town, a state, nor sovereignty over the arrival.");
        int child=fa.Regions[to].Culture;
        check(child>=0&&h.Cultures[child].ParentCultureId==culture&&h.Agriculture[child].AdoptedYear.Any(y=>y>0),"An actual local descendant inherits the carried, viable farm package.");
        check(world.Prehistory.ComputeHash()==input,"Controlled frontier tests leave their saved source untouched.");
        check(!(bool)Call("TryRemoteFarmerFrontier",oldOverfull,from,selected,100,0),"The preceding additive-capacity allowance really blocks an overfull native forager district.");
        long overRemote=overfull.History.Aggregates.Polities[owner].Remote,overTotal=overfull.History.Aggregates.TotalPopulation;
        sourceQueueStart();bool fieldSuccess;
        try{fieldSuccess=(bool)Call("TryRemoteFarmerFrontier",overfull,from,selected,100,0);}
        finally{queueField.SetValue(engine,null);}
        check(fieldSuccess&&overfull.History.Aggregates.Polities[owner].Remote<overRemote&&overfull.History.Aggregates.TotalPopulation==overTotal,"A physically viable free farm plot admits only actual debited households despite unrelated regional forager overpopulation.");
        Console.WriteLine($"Remote migration: {moved} real people, {from}->{to}, {origin}->{target}, year{trialYear}.");
        void sourceQueueStart()=>queueField.SetValue(engine,new SimulationEventQueue(Array.Empty<SimulationEvent>()));
    }
}
