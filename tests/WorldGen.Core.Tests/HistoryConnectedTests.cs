using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

public static class HistoryConnectedTests
{
    public static void Run(string path,Action<bool,string> check)
    {
        var warm=new HabitatCell{GrowingDays=330,WarmTenthsC=290,ColdTenthsC=140};
        var cold=new HabitatCell{GrowingDays=170,WarmTenthsC=230,ColdTenthsC=-160,PermafrostPermille=100};
        check(HistoricalDevelopmentEffects.GrowingSeason(warm)>HistoricalDevelopmentEffects.GrowingSeason(cold),"Warm long growing seasons retain their annual production advantage");
        check(HistoricalDevelopmentEffects.StorageSeason(cold,.15)<HistoricalDevelopmentEffects.StorageSeason(warm,.85),"A cold winter is not a larger harvest/storage advantage than a seasonal warm region");
        var technology=new HistoricalCulture{Technology=new int[HistoricalTechnologies.Names.Length]};
        int early=HistoricalDevelopmentEffects.FrontierBudget(technology,1000,3,true);
        double coast=HistoricalDevelopmentEffects.CoastRangeKm(technology,1000);
        Array.Fill(technology.Technology,1);
        check(HistoricalDevelopmentEffects.FrontierBudget(technology,1000,3,true)>early*3,"Transport capabilities change frontier throughput, not just nominal reach");
        check(HistoricalDevelopmentEffects.FrontierBudget(technology,1000,100000,true)<=64,"Even very large actors have bounded local frontier work");
        check(HistoricalDevelopmentEffects.CoastRangeKm(technology,1000)>coast*30,"Ocean-going capabilities unlock genuinely longer voyages");
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string originalHash=original.Hash;
        var engine=new Prehistory(world);var source=engine.UpgradeToCurrentHistory(original);engine.Validate(source);
        check(source.History.Version==Prehistory.CurrentHistoryVersion&&original.ComputeHash()==originalHash,"Connected rules are explicit and do not reinterpret the saved history");
        check(!Prehistory.CanUpgradeHistory(source),"The current rules do not offer a spurious update button");
        if(original.History.Version==Prehistory.LivingHistoryVersion)check(Prehistory.CanUpgradeHistory(original),"Existing history18 saves expose the explicit upgrade action in both hosts");
        check(source.Years==original.Years&&SocietyView.Population(source)==SocietyView.Population(original),"Upgrade neither advances time nor creates people");
        check(typeof(Prehistory).GetField("cropLatitudes",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!=null,"Full-mesh cultivar caches are initialized before any actor event");
        var metrics=new List<SimulationWorkMetric>();engine.WorkObserver=metrics.Add;
        var session=new SimulationSession(engine,source);session.AdvanceSeconds(20*SimulationTime.Year,int.MaxValue);var expected=session.Snapshot();engine.Validate(expected);
        var partition=new SimulationSession(new Prehistory(world),source);long target=partition.Seconds+20*SimulationTime.Year;
        while(partition.Seconds<target)partition.AdvanceSeconds(target-partition.Seconds,97,maximumGlobalWork:64);
        check(partition.Hash==expected.Hash,"Multi-contact learning, voyages and technological expansion are publication-partition deterministic");
        check(metrics.Where(m=>m.Stage.StartsWith("aggregate event ")).All(m=>m.Items<16000),"All recurring actors retain bounded terrain access, not territory censuses");
        if(source.Society.Cultures.Length>=3)
        {
            var probe=source.Copy();probe.Society.Cultures[0].Contacts=new[]{1,2};
            if(probe.History.Aggregates.Provinces!=null)probe.History.Aggregates.Provinces.Exchanges[0]=new HistoricalExchangeMemory();
            probe.History.Cultures[1].Technology[(int)HistoricalTechnology.Sailing]=0;
            probe.History.Cultures[2].Technology[(int)HistoricalTechnology.Sailing]=1;
            typeof(Prehistory).GetMethod("LivingCultureContact",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,new object[]{probe,0,2});
            var method=typeof(Prehistory).GetMethod("DevelopmentTeacher",BindingFlags.Instance|BindingFlags.NonPublic)!;
            check((int)method.Invoke(engine,new object[]{probe,0,(int)HistoricalTechnology.Sailing,1})! ==2,"A valid second contact is not hidden by an unhelpful sampled first contact");
            var own=probe.History.Aggregates.Cultures[0];own.CivilizationSince=0;own.SupportedUrban=5000;own.SupportedCities=1;
            var peer=probe.History.Aggregates.Cultures[1];peer.CivilizationSince=0;peer.SupportedUrban=60000;peer.SupportedCities=2;
            probe.History.Cultures[0].Institution=600;probe.History.Cultures[0].Surplus=500;
            typeof(Prehistory).GetMethod("LivingCultureContact",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,new object[]{probe,0,1});
            var network=typeof(Prehistory).GetMethod("DevelopmentResearchNetwork",BindingFlags.Instance|BindingFlags.NonPublic)!;
            check((bool)network.Invoke(engine,new object[]{probe,0,(int)HistoricalTechnology.Printing,false})!,"A supported research network need not all belong to one culture");
        }
        world.Prehistory=expected;var loaded=WorldFile.Read(WorldFile.Write(world));
        var resumed=new SimulationSession(new Prehistory(loaded),loaded.Prehistory);resumed.AdvanceSeconds(5*SimulationTime.Year,int.MaxValue);session.AdvanceSeconds(5*SimulationTime.Year,int.MaxValue);
        check(resumed.Hash==session.Hash,"Connected-rule save/load continuation is deterministic");
        check(original.ComputeHash()==originalHash,"Diagnostics leave the original legacy save untouched");
    }
}
