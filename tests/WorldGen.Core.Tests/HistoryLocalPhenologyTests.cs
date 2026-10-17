using WorldGen.Core;
using WorldGen.Serialization;
using System.Reflection;

public static class HistoryLocalPhenologyTests
{
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string sourceHash=original.Hash;
        var engine=new Prehistory(world);var s=original.Copy();s.History.Aggregates.Empires.Connections.Realms.Version=17;
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,args)!;
        Call("PrepareAggregateRelations",s);
        int selected=-1,group=-1;double selectedFit=0,bestDistance=0;
        s.Years+=20;
        foreach(var t in s.Society.Settlements)
        {
            if(s.History.Aggregates.Living.Cities[t.Id].AbandonedYear>=0)continue;
            var lots=engine.LocalSeedsAt(s,t.Id);if(lots==null)continue;
            for(int k=0;k<6;k++)
            {
                var seed=lots[k];if(seed==null)continue;
                bool managed=HistoricalTechnologies.Has(s.History.Cultures[t.CultureId],HistoricalTechnology.CropRotation);
                double fit=(double)Call("CommunityTransferFit",s,t.Cell,seed.Cultivar,managed);
                double generations=(double)Call("LocalSeedGenerations",s,t.Id,k,seed,fit,20);
                double latitude=Math.Abs(Math.Asin(world.Surface.Cells[t.Cell].Position.Z)*180/Math.PI),distance=Math.Abs(seed.Cultivar.DaylengthLatitudeTenths-latitude*10);
                if(generations>0&&distance>Math.Max(20,bestDistance)){selected=t.Id;group=k;selectedFit=fit;bestDistance=distance;}
            }
        }
        if(selected<0)throw new Exception("No actual planted, mismatched seed lot in the source fixture.");
        var town=s.Society.Settlements[selected];var before=engine.LocalSeedsAt(s,selected)[group];
        var old=s.Copy();old.History.Aggregates.Empires.Connections.Realms.Version=16;
        long people=s.History.Aggregates.TotalPopulation,food=s.Society.Settlements.Sum(t=>t.FoodReserve),money=s.History.Polities.Sum(p=>p.Treasury);
        Call("AdvanceLocalTownSeeds",old,selected);Call("AdvanceLocalTownSeeds",s,selected);
        var current=engine.LocalSeedsAt(s,selected)[group].Cultivar;var preceding=engine.LocalSeedsAt(old,selected)[group].Cultivar;
        int target=(int)Math.Round(Math.Abs(Math.Asin(world.Surface.Cells[town.Cell].Position.Z)*180/Math.PI)*10);
        check(Math.Abs(current.DaylengthLatitudeTenths-target)<Math.Abs(preceding.DaylengthLatitudeTenths-target),"An actual planted marginal variety advances phenology by successful seasons rather than yield-discounted calendar years.");
        check(current.SelectedYears==preceding.SelectedYears&&current.MinimumWarmTenthsC==preceding.MinimumWarmTenthsC&&current.MaturityDays==preceding.MaturityDays&&current.WaterRequirementPermille==preceding.WaterRequirementPermille&&current.YieldPermille==preceding.YieldPermille,"The new phenology clock does not accelerate every physiological/yield trait.");
        check(s.History.Aggregates.TotalPopulation==people&&s.Society.Settlements.Sum(t=>t.FoodReserve)==food&&s.History.Polities.Sum(p=>p.Treasury)==money,"A local phenotype update creates no people, food, treasury or new field.");
        var absent=original.Copy();absent.History.Aggregates.Empires.Connections.Realms.Version=17;absent.Years+=20;
        var cropRecord=absent.History.Towns[selected].Cropping;cropRecord.CropIds=Array.Empty<int>();cropRecord.SharesPermille=Array.Empty<int>();
        Call("AdvanceLocalTownSeeds",absent,selected);
        check(StateHash.Compute(engine.LocalSeedsAt(absent,selected)[group].Cultivar)==StateHash.Compute(before.Cultivar),"An unused stored seed bag changes neither phenology nor its other selection traits.");
        foreach(int blocked in new[]{0,1,2})
        {
            var failed=original.Copy();failed.History.Aggregates.Empires.Connections.Realms.Version=17;failed.Years+=20;
            if(blocked==0)failed.History.Aggregates.Cities[selected].Rural=0;
            if(blocked==1)failed.History.Aggregates.Cities[selected].Supported=false;
            if(blocked==2)failed.History.Aggregates.Living.Cities[selected].AbandonedYear=failed.Years;
            Call("AdvanceLocalTownSeeds",failed,selected);
            check(StateHash.Compute(engine.LocalSeedsAt(failed,selected)[group].Cultivar)==StateHash.Compute(before.Cultivar),"Unfarmed/import-only, unsupported and abandoned plots cannot obtain free local selection.");
        }
        check(original.ComputeHash()==sourceHash,"Controlled phenotype probes leave the saved source intact.");
        Console.WriteLine($"Actual phenology: town{selected}/cell{town.Cell}, group{group}, fit{selectedFit:F6}, target{target}, before{before.Cultivar.DaylengthLatitudeTenths}, old{preceding.DaylengthLatitudeTenths}, new{current.DaylengthLatitudeTenths}; controlled20yr mechanism, not a world replay.");
    }
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalLocalSeedPhenology.Generations(20,0,100,.15,true)==20,"Successful annual crop generations do not shrink to fifteen percent of the calendar because yield is low.");
        check(HistoricalLocalSeedPhenology.Generations(20,4,100,.15,true)==1,"A perennial seed generation has a distinct slower turnover prior.");
        check(HistoricalLocalSeedPhenology.Generations(20,0,9,.5,true)==0,"Fewer than ten actual planted farmer equivalents do not receive full selection.");
        check(HistoricalLocalSeedPhenology.Generations(20,0,100,.079,true)==0,"A failed reproductive niche does not adapt from no successful crop.");
        check(HistoricalLocalSeedPhenology.Generations(20,0,100,.5,false)==0,"Stored but unplanted inventory has no generations.");
        check(HistoricalLocalSeedPhenology.Generations(-1,0,100,.5,true)==0,"Future acquisition cannot generate negative or free elapsed time.");
        check(HistoricalLocalSeedPhenology.Movement(200,350,20,.5)==6,"The authored phenology prior moves a viable annual seed lot without changing physiological yield traits.");
        check(HistoricalLocalSeedPhenology.Movement(200,202,200,.5)==2,"Selection cannot overshoot the actual local phenology target.");
        check(HistoricalLocalSeedPhenology.Movement(200,200,200,.5)==0,"Already matched phenology does not drift away.");
        check(HistoricalLocalSeedPhenology.Movement(200,350,-20,.5)==0,"No reproductive generations yield no movement.");
        check(HistoricalLocalSeedPhenology.Movement(200,350,1,.1)==1&&HistoricalLocalSeedPhenology.Movement(200,350,1,.9)==0,"Subinteger progress is deterministically probability-rounded, not rounded away forever.");
    }
}
