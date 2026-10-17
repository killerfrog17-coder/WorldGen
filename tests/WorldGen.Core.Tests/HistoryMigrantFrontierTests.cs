using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

// A controlled actual production-method migration on accepted world geography.
// Initialization is diagnostic only; no fixture result changes saved inputs.
public static class HistoryMigrantFrontierTests
{
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));string input=world.Prehistory.Hash;var engine=new Prehistory(world);
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,args)!;
        var queueField=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.NonPublic|BindingFlags.Instance)!;
        Call("PrepareDiffusionPassages");
        var passages=(Prehistory.Route[][])typeof(Prehistory).GetField("diffusionPassages",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(engine)!;
        PrehistoryState fixture=null;Prehistory.Route selected=null;int routeId=-1;
        foreach(int index in engine.Routes.Select((r,i)=>(r,i)).Where(p=>!p.r.Sea&&engine.Patches[p.r.A].Capacity>=2000&&engine.Patches[p.r.B].Capacity>=1000).OrderByDescending(p=>engine.Patches[p.r.A].Capacity).Take(40).Select(p=>p.i))
        {
            var route=engine.Routes[index];var s=engine.Start(engine.Prepare(route.A));var a=s.History.Aggregates;
            s.Years=150000;s.Timeline.Seconds=150000L*SimulationTime.Year;
            Array.Clear(s.Population);Array.Fill(s.ArrivalYear,-1);s.Population[route.A]=10000;s.Population[route.B]=600;s.ArrivalYear[route.A]=s.ArrivalYear[route.B]=0;a.TotalPopulation=10600;
            queueField.SetValue(engine,new SimulationEventQueue(Array.Empty<SimulationEvent>()));
            try{Call("FoundAggregateCulture",s,route.A,route.LandCellA,-1);}
            finally{queueField.SetValue(engine,null);}
            var c=s.Society.Cultures[0];var hc=s.History.Cultures[0];var crops=s.History.Agriculture[0];c.Farming=600;
            for(int k=0;k<6;k++)
            {
                var cultivar=(HistoricalCultivar)Call("AggregateCultivar",s,0,k,route.LandCellA);if(cultivar==null)continue;
                if((double)Call("CommunityTransferFit",s,route.LandCellB,cultivar,false)<.2)continue;
                crops.AdoptedYear[k]=149001;crops.Cultivars[k]=cultivar;crops.PracticeCell[k]=route.LandCellA;crops.HusbandryYears[k]=1000;
            }
            if((double)Call("LivingCropLandscape",s,0,route.LandCellB,false)<.12)continue;
            hc.Technology[(int)HistoricalTechnology.Cultivation]=149001;hc.Technical.Levels[(int)HistoricalTechnology.Cultivation]=2;hc.Technical.LevelYear[(int)HistoricalTechnology.Cultivation]=149000;
            a.Regions[route.A].Routes=new[]{index};passages[route.A]=Array.Empty<Prehistory.Route>();
            fixture=s;selected=route;routeId=index;break;
        }
        if(fixture==null)throw new Exception("No suitable neighboring agricultural districts for the controlled fixture.");
        int from=selected.A,to=selected.B;double transmission=(double)Call("EnvironmentalTransmission",fixture,selected.LandCellA,selected.LandCellB,0,false);
        for(int year=150000;year<151000;year++)
            if((double)Call("HistoryRoll","provincial-dispersal-0",from,year)<.01*transmission){fixture.Years=year;fixture.Timeline.Seconds=(long)year*SimulationTime.Year;break;}
        var a0=fixture.History.Aggregates;long sourcePeople=fixture.Population[from],targetPeople=fixture.Population[to],total=fixture.Population.Sum();
        var preceding=fixture.Copy();preceding.History.Aggregates.Empires.Connections.Realms.Version=7;
        queueField.SetValue(engine,new SimulationEventQueue(Array.Empty<SimulationEvent>()));
        try{Call("AdvanceProvincialDispersal",fixture,from,100,engine.Patches[from].Capacity);}
        finally{queueField.SetValue(engine,null);}
        long moved=sourcePeople-fixture.Population[from];
        check(moved>=50&&moved<=500,"The actual land-distribution event transfers a bounded real household cohort.");
        check(fixture.Population.Sum()==total&&a0.TotalPopulation==total&&fixture.Population[to]-targetPeople==moved,"Migration conserves every regional and world person; destination residents are not newly spawned.");
        check(fixture.Society.Cultures.Length==2&&a0.Regions[to].Culture==1,"Actual land arrival can create a local descendant community, not only a remote teacher memory.");
        check(fixture.Society.Cultures[1].Farming>0&&fixture.History.Agriculture[1].AdoptedYear.Any(y=>y>0)&&fixture.History.Cultures[1].ParentCultureId==0,"Descendant households really retain their viable crop and named ancestry.");
        check(fixture.History.Polities.Length==0&&fixture.Society.Settlements.Length==0,"A farming community is not an automatically granted town or state.");
        check(fixture.Society.Cultures[0].Population==fixture.Population[from]&&fixture.Society.Cultures[1].Population==fixture.Population[to],"Culture population counters debit the departing households and credit their actual local descendants exactly once.");
        queueField.SetValue(engine,new SimulationEventQueue(Array.Empty<SimulationEvent>()));
        try{Call("AdvanceProvincialDispersal",preceding,from,100,engine.Patches[from].Capacity);}
        finally{queueField.SetValue(engine,null);}
        check(preceding.Population.SequenceEqual(fixture.Population)&&preceding.Society.Cultures.Length==fixture.Society.Cultures.Length,"Revision8's count correction does not invent extra migration or change the physical route.");
        check(preceding.Society.Cultures[0].Population==sourcePeople,"Saved revision7 retains its preceding culture-counter arithmetic until explicit upgrade.");
        check(world.Prehistory.ComputeHash()==input,"The controlled migration leaves its original saved world intact.");
        Console.WriteLine($"Migration route{routeId}: {moved} people in farming households; {from} -> {to}; year{fixture.Years}.");
    }
}
