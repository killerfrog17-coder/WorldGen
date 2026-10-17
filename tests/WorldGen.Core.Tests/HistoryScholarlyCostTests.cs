using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using WorldGen.Core;

public static class HistoryScholarlyCostTests
{
    const int Scholar=(int)HistoricalTechnology.Scholasticism;
    const long RecordedProgress=1634419061;
    static PrehistoryState CostFrame(int revision,string history="history-32")=>new PrehistoryState{History=new HistoryState{Version=history,Aggregates=new HistoricalAggregateState{Empires=new HistoricalImperialSystem{Connections=new HistoricalColonialConnections{Realms=new HistoricalRealmLifecycles{Version=revision}}}}}};
    static long Preceding(int revision,int technology,int level,string history)
    {
        if(revision<5||technology!=(int)HistoricalTechnology.Iron)return HistoricalTechnicalGraph.Cost(technology,level,history);
        long value=540000000;for(int i=1;i<level;i++)value=checked((value*33+10)/20);
        return checked((value*(level==1?270:level==2?125:100)+50)/100);
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(string history in new string[]{null,"history-24","history-28","history-29","history-30","history-31","history-32"})
        foreach(int revision in Enumerable.Range(0,27))
        {
            var state=CostFrame(revision,history);
            for(int k=0;k<HistoricalTechnicalGraph.Count;k++)for(int rank=1;rank<=8;rank++)
                if(revision<=25||k!=Scholar)check(HistoricalTechnicalGraph.Cost(state,k,rank)==Preceding(revision,k,rank,history),"Every preceding-rule cost and every other technology retain the literal prior arithmetic, including revision5 iron calibration.");
        }
        var next=CostFrame(26);long previous=0;
        long[] expected={720000000,841500000,1143450000,1617165000,2668322250,4402731713,7264507326,11986437088};
        for(int rank=1;rank<=8;rank++)
        {
            long value=HistoricalTechnicalGraph.Cost(next,Scholar,rank);
            check(value==expected[rank-1]&&value>previous,"All eight calibrated scholarly ranks remain strictly increasing, with exact integer costs and no high-rank cap.");previous=value;
            check(HistoricalTechnicalGraph.Cost(CostFrame(100),Scholar,rank)==value,"The new scholarly rule remains stable beyond its introducing revision.");
        }
        check(HistoricalTechnicalGraph.Cost(next,Scholar,1)==HistoricalTechnicalGraph.Cost(CostFrame(25),Scholar,1),"Foundational scholarly work remains exactly720 units; the proposal is not an earlier first-rank unlock.");
        check(HistoricalTechnicalGraph.Cost(next,Scholar,0)==HistoricalTechnicalGraph.Cost(CostFrame(25),Scholar,0),"The existing public zero-rank arithmetic is not silently altered.");
        check(HistoricalTechnicalGraph.Cost((PrehistoryState)null,Scholar,3)==HistoricalTechnicalGraph.Cost(Scholar,3,(string)null),"Absent historical extensions keep the preceding public overload behavior.");
        check(HistoricalTechnicalGraph.Cost(next,Scholar,4)/8==202145625&&HistoricalTechnicalGraph.Cost(CostFrame(25),Scholar,4)/8==404291250,"Actual delivered teaching retains the same one-eighth cost basis, not another independent discount or grant.");
        Caller(check);
    }
    static void Caller(Action<bool,string> check)
    {
        // Tiny controlled actor frame, not a validated full-world history.
        // Its stored own progress is copied exactly from the saved X23 c3 row.
        PrehistoryState Frame(int revision,bool lesson=false)
        {
            var t=new HistoricalTechnicalPractice();foreach(var pair in new[]{(6,3),(10,2),(11,2),(14,3)}){t.Levels[pair.Item1]=pair.Item2;t.Deployment[pair.Item1]=700;t.LevelYear[pair.Item1]=155769;}
            t.Progress[Scholar]=lesson?0:RecordedProgress;if(lesson){t.Offers[Scholar]=4;t.OfferYear[Scholar]=156899;t.Sources[Scholar]=1;t.Learning[Scholar]=202145625;}
            var hc=new HistoricalCulture{Id=0,Institution=600,Surplus=700,Technical=t,Technology=new int[37],Research=new int[37]};foreach(int k in new[]{6,10,11,14})hc.Technology[k]=155770;
            return new PrehistoryState{Years=156900,Population=new long[]{10000},Society=new SocietyState{Cultures=new[]{new CultureIdentity{Id=0,OriginCell=0,Population=10000},new CultureIdentity{Id=1,OriginCell=0}}},History=new HistoryState{Version="history-29",Modern=new HistoricalModernSettings{TechnologyCeiling=HistoricalTechnologyCeiling.Medieval},Cultures=new[]{hc,new HistoricalCulture{Id=1,Technical=new HistoricalTechnicalPractice()}},Agriculture=new[]{new HistoricalAgriculture{CultureId=0}},Aggregates=new HistoricalAggregateState{TotalPopulation=10000,Cultures=new[]{new HistoricalCultureAggregate{Id=0,CivilizationSince=150000,SupportedUrban=10000}},Provinces=new HistoricalProvincialSupport{Exchanges=new[]{new HistoricalExchangeMemory()}},Empires=new HistoricalImperialSystem{Connections=new HistoricalColonialConnections{Realms=new HistoricalRealmLifecycles{Version=revision}}}}}};
        }
        var engine=(Prehistory)RuntimeHelpers.GetUninitializedObject(typeof(Prehistory));
        typeof(Prehistory).GetField("World")!.SetValue(engine,new World{Recipe=new Recipe{Seed=123},Ecology=new EcologyResult{Habitat=new[]{new HabitatCell()}}});
        typeof(Prehistory).GetField("Patches")!.SetValue(engine,new[]{new Prehistory.Patch{Island=true}});typeof(Prehistory).GetField("CellPatch")!.SetValue(engine,new[]{0});
        typeof(Prehistory).GetField("riverSites",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(engine,new double[]{0});
        var actor=typeof(Prehistory).GetMethod("AdvanceTechnicalResearch",BindingFlags.Instance|BindingFlags.NonPublic)!;
        foreach(bool lesson in new[]{false,true})
        {
            var old=Frame(25,lesson);var next=Frame(26,lesson);actor.Invoke(engine,new object[]{old,0,1,-1,0});actor.Invoke(engine,new object[]{next,0,1,-1,0});
            var a=old.History.Cultures[0].Technical;var b=next.History.Cultures[0].Technical;
            check(a.Levels[Scholar]==3&&b.Levels[Scholar]==4,"The actual private caller applies the calibrated cost to exactly one "+(lesson?"delivered lesson":"supported own-research")+" completion, while the preceding rule remains below its threshold.");
            check(a.LastResearchBudget==b.LastResearchBudget&&a.LastResearchSpent==b.LastResearchSpent&&a.LastLearningBudget==b.LastLearningBudget&&a.LastLearningSpent==b.LastLearningSpent&&a.Allocation.SequenceEqual(b.Allocation),"Calibrated settlement creates no effort, changes no project allocations and preserves every actual review budget.");
            check(b.LastResearchSpent>=0&&b.LastResearchSpent<=b.LastResearchBudget&&b.LastLearningSpent>=0&&b.LastLearningSpent<=b.LastLearningBudget,"Actual allocations remain within both conserved nonnegative budgets.");
            check(lesson?b.Learning[Scholar]==a.Learning[Scholar]-202145625:b.Progress[Scholar]==a.Progress[Scholar]-1617165000,"Completion debits the exact new cost from its actual bank, preserving only legitimate unspent effort.");
            check(next.History.Aggregates.TotalPopulation==10000&&next.Population[0]==10000,"The private technical review introduces no people or settlement resources.");
        }
        var missing=Frame(26);missing.History.Cultures[0].Technical.Levels[6]=2;actor.Invoke(engine,new object[]{missing,0,1,-1,0});
        check(missing.History.Cultures[0].Technical.Levels[Scholar]==3&&missing.History.Cultures[0].Technical.Progress[Scholar]==RecordedProgress,"A cheaper rank cannot bypass a missing writing prerequisite for own research.");
        var unsupported=Frame(26);unsupported.History.Cultures[0].Institution=0;actor.Invoke(engine,new object[]{unsupported,0,1,-1,0});
        check(unsupported.History.Cultures[0].Technical.Levels[Scholar]==3&&unsupported.History.Cultures[0].Technical.Progress[Scholar]==RecordedProgress,"A lower threshold does not substitute for an actually supported scholarly research community.");
        var idle=Frame(26);actor.Invoke(engine,new object[]{idle,0,0,-1,0});
        check(idle.History.Cultures[0].Technical.Levels[Scholar]==3&&idle.History.Cultures[0].Technical.Progress[Scholar]==RecordedProgress,"A zero-work review does not retroactively grant a rank merely because the new cost is below a stored bank.");
        var advanced=Frame(26,true);advanced.History.Cultures[0].Technical.Offers[Scholar]=8;actor.Invoke(engine,new object[]{advanced,0,1,-1,0});
        check(advanced.History.Cultures[0].Technical.Levels[Scholar]==4,"A higher delivered offer still cannot leap several ranks in one paid review.");
        var foundationOld=Frame(25);var foundationNew=Frame(26);
        foreach(var state in new[]{foundationOld,foundationNew}){state.History.Cultures[0].Technical.Levels[Scholar]=0;state.History.Cultures[0].Technology[Scholar]=0;actor.Invoke(engine,new object[]{state,0,1,-1,0});}
        check(foundationNew.History.Cultures[0].Technical.Levels[Scholar]==1&&StateHash.Compute(foundationOld.History.Cultures[0].Technical)==StateHash.Compute(foundationNew.History.Cultures[0].Technical),"The actual first scholarly acquisition has literal preceding technical-state arithmetic, including its budgets, cost debit and deployment.");
    }
    public static void Recorded(string path,Action<bool,string> check)
    {
        using var json=JsonDocument.Parse(File.ReadAllText(path));var root=json.RootElement;
        var culture=root.GetProperty("Technical").EnumerateArray().Single(c=>c.GetProperty("Id").GetInt32()==3);var p=culture.GetProperty("Practice");
        check(root.GetProperty("Year").GetInt32()==156900&&p.GetProperty("Levels")[Scholar].GetInt32()==3&&p.GetProperty("Progress")[Scholar].GetInt64()==RecordedProgress&&p.GetProperty("Allocation")[Scholar].GetInt32()==2700&&p.GetProperty("Offers")[Scholar].GetInt32()==3,"The exact X23 c3 row records independent fourth-rank work with no higher scholarly offer, not rounded or invented effort.");
        check(RecordedProgress<HistoricalTechnicalGraph.Cost(CostFrame(25),Scholar,4)&&RecordedProgress>HistoricalTechnicalGraph.Cost(CostFrame(26),Scholar,4),"The proposed cost crosses an actually paid research threshold; this arithmetic does not predict a counterfactual acquisition date.");
    }
}
