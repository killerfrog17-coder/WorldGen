using WorldGen.Core;
public static class HistoryDependencyTests
{
    public static void Run(Action<bool,string> check)
    {
        var records=new[]{new HistoricalDependency{PatronId=0,ClientId=1},new HistoricalDependency{PatronId=1,ClientId=2}};
        check(HistoricalClientEffects.AreLinked(records,0,1)&&HistoricalClientEffects.AreLinked(records,1,0),"Direct tributary links are symmetric for diplomacy, not ownership");
        check(!HistoricalClientEffects.AreLinked(records,0,2),"Indirect subjects are not silently merged into direct dependency records");
        check(HistoricalClientEffects.WouldCycle(records,2,0),"A subject cannot become overlord of its own ancestor");
        check(HistoricalClientEffects.WouldCycle(records,0,0),"A court cannot owe tribute to itself");
        check(!HistoricalClientEffects.WouldCycle(records,0,3),"A new subject can enter a valid layered hierarchy");
        records[1].EndedYear=100;
        check(!HistoricalClientEffects.AreLinked(records,1,2)&&!HistoricalClientEffects.WouldCycle(records,2,0),"Ended dependencies retain lore without constraining current sovereignty");
        check(HistoricalClientEffects.Quota(150000)==100&&HistoricalClientEffects.Quota(0)==25&&HistoricalClientEffects.Quota(long.MaxValue)==5000,"Tributary obligations scale with people and remain bounded");
        double secure=HistoricalClientEffects.BreakawayChance(800,0,300,0,.1,false);
        check(HistoricalClientEffects.BreakawayChance(200,0,300,0,.1,false)>secure,"Weak legitimacy increases opportunities to contest dependency");
        check(HistoricalClientEffects.BreakawayChance(800,700,300,50,.1,true)>secure,"War exhaustion, unpaid demands and succession increase defiance");
        check(HistoricalClientEffects.BreakawayChance(800,0,900,0,.8,false)>secure,"Local ambition and changed military balance affect breakaway chances");
        var record=new HistoricalDependency{Id=1,PatronId=2,ClientId=3,StartedYear=10,LastReachableYear=10,AnnualQuota=100};
        string hash=StateHash.Compute(record);
        foreach(var field in typeof(HistoricalDependency).GetFields())
        {
            object original=field.GetValue(record)!;
            object changed=field.FieldType==typeof(string)?(object)((string)original+" altered"):field.FieldType==typeof(bool)?!(bool)original:field.FieldType==typeof(long)?(object)((long)original+1):(int)original+1;
            field.SetValue(record,changed);check(StateHash.Compute(record)!=hash,"Dependency integrity includes "+field.Name);field.SetValue(record,original);
        }
        var dynamics=new HistoricalDynamics{Dependencies=new[]{record}};var copy=dynamics.Copy();copy.Dependencies[0].TotalPaid=123;
        check(dynamics.Dependencies[0].TotalPaid==0,"Dependency copies preserve immutable published checkpoints");
    }
}
