using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryReceiverFrontierTests
{
    public static void Run(Action<bool,string> check)
    {
        var levels=new int[HistoricalTechnicalGraph.Count];var offers=new int[levels.Length];var needed=new int[levels.Length];
        levels[(int)HistoricalTechnology.Storage]=3;levels[(int)HistoricalTechnology.Pottery]=1;
        offers[(int)HistoricalTechnology.Writing]=5;offers[(int)HistoricalTechnology.Pottery]=4;offers[(int)HistoricalTechnology.MechanizedAgriculture]=3;
        var originalLevels=(int[])levels.Clone();var originalOffers=(int[])offers.Clone();
        HistoricalReceiverFrontier.NeededLevels(levels,offers,needed);
        check(needed[(int)HistoricalTechnology.Pottery]==2,"A delivered writing offer prioritizes its actually delivered pottery prerequisite.");
        check(needed[(int)HistoricalTechnology.CombustionEngines]==0,"A missing technique is not invented merely because a future offer needs it.");
        check(levels.SequenceEqual(originalLevels)&&offers.SequenceEqual(originalOffers),"Receiver prioritization does not alter levels or delivered awareness.");
        long pottery=HistoricalReceiverFrontier.Priority((int)HistoricalTechnology.Pottery,levels,needed[(int)HistoricalTechnology.Pottery],1000000);
        long writing=HistoricalReceiverFrontier.Priority((int)HistoricalTechnology.Writing,levels,needed[(int)HistoricalTechnology.Writing],1000000);
        long machinery=HistoricalReceiverFrontier.Priority((int)HistoricalTechnology.MechanizedAgriculture,levels,needed[(int)HistoricalTechnology.MechanizedAgriculture],1000000);
        check(pottery>writing&&writing>machinery&&machinery>0,"Finite instruction favors an intelligible prerequisite frontier but retains future awareness effort.");
        levels[(int)HistoricalTechnology.Pottery]=2;
        check(HistoricalReceiverFrontier.Priority((int)HistoricalTechnology.Writing,levels,0,1000000)==1000000,"A newly intelligible delivered technique regains full priority without a calendar gate.");
        offers[(int)HistoricalTechnology.CombustionEngines]=4;
        HistoricalReceiverFrontier.NeededLevels(levels,offers,needed);
        check(needed[(int)HistoricalTechnology.CombustionEngines]==2,"Actual delivered engine knowledge can become the next learning frontier.");
        check(HistoricalReceiverFrontier.Priority(0,levels,0,0)==0,"Zero original effort does not become a free project.");
        int last=1001;
        for(int missing=0;missing<=64;missing++)
        {
            int value=HistoricalReceiverFrontier.ReadinessPermille(missing);
            check(value>0&&value<=last,"Missing prerequisite penalties are positive, bounded and monotone.");last=value;
        }
        for(int level=0;level<=HistoricalTechnicalGraph.MaximumLevel;level++)
        {
            Array.Fill(levels,level);Array.Fill(offers,Math.Min(HistoricalTechnicalGraph.MaximumLevel,level+1));
            HistoricalReceiverFrontier.NeededLevels(levels,offers,needed);
            for(int k=0;k<levels.Length;k++)
            {
                long weight=HistoricalReceiverFrontier.Priority(k,levels,needed[k],50000);
                check(weight>0&&weight<=100000,"Every catalog priority remains bounded at every possible level.");
            }
        }
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var original=world.Prehistory;
        engine.Validate(original);string inputHash=original.ComputeHash();
        check(original.History.ReceiverFrontier==null,"Frozen predecessor fixture has no silently enabled receiver-frontier marker.");
        var upgraded=engine.UpgradeReceiverFrontierLearning(original);engine.Validate(upgraded);
        check(upgraded.History.ReceiverFrontier?.Version==1&&original.History.ReceiverFrontier==null,"Explicit rules upgrade is copy-on-write and preserves the old world.");
        check(upgraded.History.Aggregates.TotalPopulation==original.History.Aggregates.TotalPopulation&&upgraded.Society.Cultures.Length==original.Society.Cultures.Length,"Enabling learning priorities creates no people or cultures.");
        check(upgraded.History.Cultures.Zip(original.History.Cultures).All(pair=>pair.First.Technical.Levels.SequenceEqual(pair.Second.Technical.Levels)&&pair.First.Technical.Offers.SequenceEqual(pair.Second.Technical.Offers)),"The upgrade changes neither attained technology nor dated offered levels.");
        check(upgraded.History.Agriculture.Zip(original.History.Agriculture).All(pair=>pair.First.AdoptedYear.SequenceEqual(pair.Second.AdoptedYear)),"Learning-frontier opt-in grants no crops.");
        check(engine.UpgradeReceiverFrontierLearning(upgraded).Hash==upgraded.Hash,"An explicit receiver-frontier upgrade is idempotent.");
        var copy=upgraded.Copy();copy.History.ReceiverFrontier.Version=2;
        check(upgraded.History.ReceiverFrontier.Version==1,"Rule marker copy does not alias its source.");
        bool rejected=false;try{copy.Seal();engine.Validate(copy);}catch(ArgumentException){rejected=true;}
        check(rejected,"Unknown receiver-frontier rules are rejected rather than silently interpreted.");
        world.Prehistory=upgraded;var restored=WorldFile.Read(WorldFile.Write(world)).Prehistory;
        check(restored.Hash==upgraded.Hash&&restored.History.ReceiverFrontier?.Version==1,"Saved receiver rules retain their canonical marker and hash.");
        var one=engine.AdvanceYears(upgraded,20);var partitioned=engine.AdvanceYears(engine.AdvanceYears(upgraded,7),13);
        check(one.Hash==partitioned.Hash,"Receiver learning is deterministic under calendar publication partitioning.");
        var reloaded=engine.AdvanceYears(restored,20);
        check(one.Hash==reloaded.Hash,"Reloading does not change receiver learning or future actor behavior.");
        check(one.History.Cultures.All(c=>c.Technical.LastLearningSpent<=c.Technical.LastLearningBudget&&c.Technical.LastResearchSpent<=c.Technical.LastResearchBudget),"New learning allocation conserves both real finite budgets.");
        var legacy=engine.AdvanceYears(original,20);
        check(legacy.History.ReceiverFrontier==null,"Continuing a predecessor world does not opt in, including its new cultures.");
        check(original.ComputeHash()==inputHash,"Actual-world receiver tests preserve the supplied source hash.");
    }
}
