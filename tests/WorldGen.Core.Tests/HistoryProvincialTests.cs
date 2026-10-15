using WorldGen.Core;
using WorldGen.Serialization;
using System.Reflection;
public static class HistoryProvincialTests
{
    public static void Run(Action<bool,string> check,string path=null)
    {
        check(HistoricalProvincialEffects.FarmingDensity(.5,600)>HistoricalProvincialEffects.FarmingDensity(.1,600)*2,"Actual cultivated productivity varies with suitable local production");
        check(HistoricalProvincialEffects.RuralProductivity(0,.8,new HistoricalCulture(),1000)==1,"Wild communities do not gain agricultural capacity without agriculture");
        double core=HistoricalProvincialEffects.ProvincialPressure(.2,.8,false,0,.9,0,0,0,.8);
        double remote=HistoricalProvincialEffects.ProvincialPressure(2,.8,true,1,.5,.6,.6,.3,.2);
        check(core<remote,"Remote exclusion and fiscal/war strain differ from a supported home province");
        check(HistoricalProvincialEffects.IndependenceHazard(core,.8)==0,"A well-supported home province has no arbitrary state expiry hazard");
        check(HistoricalProvincialEffects.IndependenceHazard(remote,.4)>0,"Organized strained provinces can pursue independent institutions");
        check(Prehistory.IsAggregateHistoryVersion(Prehistory.RegionalHistoryVersion),"The regional model retains the actor-calendar boundary");
        check(HistoricalTechnologyDevelopment.Cost((int)HistoricalTechnology.Iron)>HistoricalTechnologyDevelopment.Cost((int)HistoricalTechnology.Bronze)*2,"Independent iron working is not a duplicate bronze-cost lottery");
        check(HistoricalTechnologyDevelopment.Maturation((int)HistoricalTechnology.Computing,true)<HistoricalTechnologyDevelopment.Maturation((int)HistoricalTechnology.Iron,false),"Adopting modern equipment differs from independent ancient experimental development");
        check(HistoricalTechnologies.Requirements(Prehistory.RegionalHistoryVersion,(int)HistoricalTechnology.Printing).Contains((int)HistoricalTechnology.Scholasticism)&&!HistoricalTechnologies.Requirements(Prehistory.ConnectedHistoryVersion,(int)HistoricalTechnology.Printing).Contains((int)HistoricalTechnology.Scholasticism),"New institutional dependencies do not reinterpret the history19 rules");
        if(path==null)return;
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string before=original.Hash;
        var engine=new Prehistory(world);var source=engine.UpgradeToCurrentHistory(original);engine.Validate(source);
        check(source.History.Aggregates.Provinces!=null&&original.Hash==before,"Provincial support is an explicit isolated historical upgrade");
        var probe=source.Copy();var memory=probe.History.Aggregates.Provinces;
        memory.Exchanges[0]=new HistoricalExchangeMemory();probe.Society.Cultures[0].Contacts=Array.Empty<int>();
        int electric=(int)HistoricalTechnology.Electricity;probe.History.Cultures[1].Technology[electric]=0;
        var contact=typeof(Prehistory).GetMethod("LivingCultureContact",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var teacher=typeof(Prehistory).GetMethod("DevelopmentTeacher",BindingFlags.Instance|BindingFlags.NonPublic)!;
        contact.Invoke(engine,new object[]{probe,0,1});probe.History.Cultures[1].Technology[electric]=1;
        check((int)teacher.Invoke(engine,new object[]{probe,0,electric,1})! == -1,"An old acquaintance cannot transmit future discoveries without another exchange");
        contact.Invoke(engine,new object[]{probe,0,1});
        check((int)teacher.Invoke(engine,new object[]{probe,0,electric,1})! == 1,"A renewed physical exchange carries discoveries that now exist");
        var claim=typeof(Prehistory).GetMethod("ClaimAggregatePolity",BindingFlags.Instance|BindingFlags.NonPublic)!;
        int cell=Enumerable.Range(0,engine.CellPatch.Length).First(i=>HistoricalCells.Polity(source,i)>=0&&source.History.Aggregates.TownAt[i]<0);
        var changed=source.Copy();int owner=HistoricalCells.Polity(changed,cell);long total=changed.History.Aggregates.TotalPopulation;
        claim.Invoke(engine,new object[]{changed,cell,-1});changed.Seal();engine.Validate(changed);
        check(changed.History.Aggregates.TotalPopulation==total,"Releasing countryside transfers its residents without deleting or duplicating them");
        claim.Invoke(engine,new object[]{changed,cell,owner});changed.Seal();engine.Validate(changed);
        check(changed.History.Aggregates.TotalPopulation==total,"Reclaiming countryside conserves all population accounts");
        check(source.ComputeHash()==source.Hash&&original.ComputeHash()==before,"Spatial/knowledge probes cannot mutate a published source or old save");
    }
}
