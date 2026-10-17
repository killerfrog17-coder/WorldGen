using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryUrbanSurvivalTests
{
    static object Call(Prehistory engine,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(engine,args);
    public static void Run(Action<bool,string> check)
    {
        var warehouse=HistoricalUrbanSurvivalEffects.Food(9013572,25600,2999500,25600,1);
        check(warehouse.Supply==1&&warehouse.Consumed==2999500&&warehouse.Reserve==6039672,"Gicfupismu's existing warehouse feeds its three million residents after capacity collapses; a lower annual yield does not erase stored food.");
        check(warehouse.Produced==25600&&warehouse.Overflow==0&&9013572+warehouse.Produced==warehouse.Reserve+warehouse.Consumed+warehouse.Overflow,"The real warehouse control conserves produced, stored and consumed rations exactly.");
        var empty=HistoricalUrbanSurvivalEffects.Food(0,250,1000,250,1);
        check(empty.Reserve==0&&empty.Consumed==250&&empty.Supply==.25,"An actually underprovisioned town receives no imaginary rations.");
        var delayed=HistoricalUrbanSurvivalEffects.Food(900,0,1000,0,1);
        check(delayed.Consumed==900&&delayed.Supply==.9&&delayed.Reserve==0,"Supply measures actual consumed food, not an already-consumed reserve discounted a second time.");
        var overflow=HistoricalUrbanSurvivalEffects.Food(100,1000,1,10,1);
        check(overflow.Reserve==100&&overflow.Overflow==999&&100+overflow.Produced==overflow.Reserve+overflow.Consumed+overflow.Overflow,"Finite handling overflow can discard new surplus without destroying the pre-existing warehouse or manufacturing food.");
        var none=HistoricalUrbanSurvivalEffects.Food(40,100,100,10,0);
        check(none.Reserve==40&&none.Produced==0&&none.Consumed==0&&none.Overflow==0,"A zero elapsed interval creates neither harvest nor consumption.");
        var noPeople=HistoricalUrbanSurvivalEffects.Food(0,0,0,0,1);
        check(noPeople.Consumed==0&&noPeople.Supply==1,"An empty actor does not invent food demand or inhabitants.");
        bool conservative=true;
        foreach(long stored in new[]{0L,50L,10000L})foreach(long harvest in new[]{0L,37L,2000L})foreach(int elapsed in new[]{1,3,20})
        {
            var b=HistoricalUrbanSurvivalEffects.Food(stored,harvest,500,700,elapsed);
            conservative&=stored+b.Produced==b.Reserve+b.Consumed+b.Overflow&&b.Reserve>=0&&b.Consumed>=0&&b.Consumed<=500L*elapsed&&b.Supply>=0&&b.Supply<=1;
        }
        check(conservative,"Eighty-one finite actor-local food balances conserve all rations and bound actual consumption.");
        check(HistoricalUrbanSurvivalEffects.NaturalChange(2999500,25600,.01063,1,warehouse.Supply)==0,"A fed over-capacity city cannot suffer the old negative quadratic demographic extinction.");
        long famine=HistoricalUrbanSurvivalEffects.NaturalChange(1000000,0,.01,1,0);
        check(famine<0&&famine>-60000,"Real starvation still causes finite mortality rather than instant total extinction in one update.");
        check(HistoricalUrbanSurvivalEffects.NaturalChange(1000000,0,.01,10000,0)>=-1000000,"Even a long stale actor review cannot remove more urban inhabitants than actually exist.");
        check(HistoricalUrbanSurvivalEffects.NaturalChange(1000,4000,.01,1,1)>0&&HistoricalUrbanSurvivalEffects.NaturalChange(0,4000,.01,1,1)==0,"A supported existing community can grow, but growth does not seed an empty town with free people.");
        long rural=HistoricalUrbanSurvivalEffects.RuralChange(1000000,1000,.006,1);
        check(rural<0&&rural>-35000&&HistoricalUrbanSurvivalEffects.RuralChange(1000000,0,.006,10000)>=-1000000,"A lost farm catchment yields bounded rural decline, never negative households from a quadratic population step.");
        int fed=HistoricalUrbanSurvivalEffects.Distress(79,1,1,1069265,1045112);
        check(fed==77&&!HistoricalUrbanSurvivalEffects.Retire(1069265,1045112,3135336,80,1),"Nalekcin's supplied million-person agglomeration recovers distress and is not deleted at eighty years merely because local productivity is low.");
        check(HistoricalUrbanSurvivalEffects.Distress(29,1,1,282367,307938)==27&&!HistoricalUrbanSurvivalEffects.Retire(282367,307938,313220,30,1),"Yufihocin's reduced but provisioned town can stabilize below its old metropolitan peak.");
        check(HistoricalUrbanSurvivalEffects.Distress(0,3,.5,10000,5000)==3,"Sustained actual food deficit still accumulates local distress.");
        check(HistoricalUrbanSurvivalEffects.Retire(24,0,0,0,0)&&HistoricalUrbanSurvivalEffects.Retire(100,50,0,30,.2),"A genuinely empty or persistently near-empty unsupported center can still retire.");
        check(HistoricalUrbanSurvivalEffects.Retire(100000,1000,0,80,.1)&&!HistoricalUrbanSurvivalEffects.Retire(100000,1000,300000,80,1),"A severe sustained unprovisioned collapse is distinct from a warehouse-supported temporary shock.");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;var engine=new Prehistory(world);engine.Validate(source);string original=source.Hash;
        int oldRules=source.History.Aggregates.Empires.Connections.Realms.Version;
        var revised=engine.UpgradeRealmLifecycles(source);engine.Validate(revised);
        check(revised.History.Aggregates.Empires.Connections.Realms.Version>=5&&source.History.Aggregates.Empires.Connections.Realms.Version==oldRules&&source.ComputeHash()==original,"Only explicit upgrade enables urban survival revision5 and never rewrites the old checkpoint.");
        check(ReferenceEquals(engine.UpgradeRealmLifecycles(revised),revised),"The complete revised realm and urban-survival upgrade is idempotent.");
        world.Prehistory=revised;var restored=WorldFile.Read(WorldFile.Write(world)).Prehistory;
        check(restored.Hash==revised.Hash,"Urban survival rules retain exact full-world serialization continuity.");
        var full=engine.AdvanceYears(revised,10);var split=engine.AdvanceYears(engine.AdvanceYears(restored,4),6);engine.Validate(full);
        check(full.Hash==split.Hash,"Actual urban food, mortality and distress replay across a saved10-year versus4+6-year continuation.");
        var a=revised.History.Aggregates;int id=Array.FindIndex(revised.Society.Settlements,t=>t.Population>=600&&a.Living.Cities[t.Id].AbandonedYear<0&&a.Resolve(t.PolityId,revised.History)>=0);
        check(id>=0,"The actual-world control has an existing state-affiliated town for the guarded lifecycle experiment.");
        if(id<0)return;
        // Deliberate controlled actor fixture. Stored provisions are assigned
        // here as an experimental initial stock, not produced by simulation.
        var viable=revised.Copy();var town=viable.Society.Settlements[id];var life=viable.History.Aggregates.Living.Cities[id];
        town.FoodReserve=town.Population*10;life.DeclineYears=79;life.Productivity=.2;life.DisruptedUntil=viable.Years+200;viable.Years+=2;
        var legacy=viable.Copy();legacy.History.Aggregates.Empires.Connections.Realms.Version=4;
        var queue=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.NonPublic|BindingFlags.Instance);
        queue.SetValue(engine,new SimulationEventQueue(viable.Timeline.Events));
        Call(engine,"PrepareAggregateRelations",viable);Call(engine,"AdvanceAggregateCity",viable,id);
        check(viable.History.Aggregates.Living.Cities[id].AbandonedYear<0&&viable.Society.Settlements[id].Population>0&&viable.History.Aggregates.Living.Cities[id].DeclineYears<79,"The actual revised city event preserves supplied households under low local productivity instead of the old eighty-year deletion.");
        queue.SetValue(engine,new SimulationEventQueue(legacy.Timeline.Events));
        Call(engine,"PrepareAggregateRelations",legacy);Call(engine,"AdvanceAggregateCity",legacy,id);
        check(legacy.History.Aggregates.Living.Cities[id].AbandonedYear>=0&&legacy.Society.Settlements[id].Population==0,"The guarded realm4 city event retains its preceding eighty-year retirement behavior.");
        check(source.ComputeHash()==original,"Lifecycle controls and publication replay never mutate the loaded historical source.");
        queue.SetValue(engine,null);
    }
}
