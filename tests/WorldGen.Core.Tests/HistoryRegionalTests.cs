using WorldGen.Core;
public static class HistoryRegionalTests
{
    public static void Run(Action<bool,string> check)
    {
        int shortHop=HistoricalRegionalEffects.RelayedExposure(900,50000,10,10,0);
        check(shortHop>450&&shortHop<900,"A nearby forager relay retains usable farming knowledge without automatic adoption");
        check(HistoricalRegionalEffects.RelayedExposure(900,500000,10,10,0)<shortHop,"Longer exchange loses more exposure");
        check(HistoricalRegionalEffects.RelayedExposure(900,50000,500,500,0)<shortHop,"Large patch interiors charge travel rather than teleporting knowledge");
        check(HistoricalRegionalEffects.RelayedExposure(900,50000,10,10,3000)<shortHop,"Rugged terrain constrains regional transmission");
        check(HistoricalRegionalEffects.RelayedExposure(30,50000,10,10,0)==0,"Weak unsupported information does not amplify itself");
        check(HistoricalRegionalEffects.AdoptionChance(800,1000,.7,.04)>0,"Existing suitable communities can adopt introduced crops");
        check(HistoricalRegionalEffects.AdoptionChance(400,1000,.7,.04)==0,"A passing rumor is insufficient for a new farming community");
        check(HistoricalRegionalEffects.AdoptionChance(800,100,.7,.04)==0,"Knowledge cannot manufacture residents");
        check(HistoricalRegionalEffects.AdoptionChance(800,1000,.1,.04)==0,"Contact cannot make unsuitable crops viable");
        check(HistoricalRegionalEffects.AdoptionChance(800,1000,.7,0)==0,"Food knowledge does not found an empty local site");
        check(HistoricalRegionalEffects.PackageMerit(650,.8)>HistoricalRegionalEffects.PackageMerit(900,.1),"A more locally useful crop tradition can displace a louder but unsuitable donor");
        check(HistoricalRegionalEffects.PackageMerit(900,.8)>HistoricalRegionalEffects.PackageMerit(650,.8),"Equally suitable traditions retain stronger real exposure");
        check(HistoricalRegionalEffects.CanStudyRegionalCrop(600,2000,1000,.7),"Existing local cultures can study practices carried through intermediary communities");
        check(!HistoricalRegionalEffects.CanStudyRegionalCrop(400,2000,1000,.7),"A weak regional rumor does not supply a food system");
        check(!HistoricalRegionalEffects.CanStudyRegionalCrop(600,2000,2001,.7),"Newly adopted crops cannot relay again in the same pass");
        check(!HistoricalRegionalEffects.CanStudyRegionalCrop(600,2000,0,.7)&&!HistoricalRegionalEffects.CanStudyRegionalCrop(600,2000,1000,.2),"Regional exchange requires an established actual crop and suitable receiving fields");
        check(HistoricalFoodEffects.GrowthPressure(5000,0,0)==-1,"No phantom local food floor supports an isolated settlement");
        check(HistoricalFoodEffects.GrowthPressure(5000,0,5000)==0,"Real delivered food can maintain a town without local production");
        check(HistoricalFoodEffects.GrowthPressure(5000,0,7000)>0,"Actual additional provisioning permits bounded growth");
        check(HistoricalFoodEffects.GrowthPressure(0,0,0)==1,"Empty zero-production sites avoid division by zero");
        check(HistoricalFoodEffects.GrowthPressure(5000,5000,0)==0,"Bumper local harvests do not permanently inflate sustainable carrying capacity");
        check(HistoricalFoodEffects.RemainingReserve(5000,0,0,1000)==4000,"Lost production consumes real stored food rather than deleting the whole store");
        check(HistoricalFoodEffects.RemainingReserve(0,0,500,1000)==0,"Reserve accounting cannot manufacture food during a deficit");
        check(HistoricalFoodEffects.RemainingReserve(5000,1000,2000,1000)==6000,"Annual food balance retains actual excess within existing or current storage limits");
        double garden=HistoricalRegionalEffects.AdministrativeOpportunity(.3,1,.1,.1,0),grain=HistoricalRegionalEffects.AdministrativeOpportunity(.9,1,.1,.1,0);
        check(garden>0&&garden<grain,"Garden communities retain a slow independent administrative path; collectable crops make it easier");
        check(HistoricalRegionalEffects.AdministrativeOpportunity(.9,1,.8,.8,0)>grain,"Shared infrastructure and concentrated core conditions favor administration over a uniform age trigger");
        check(HistoricalRegionalEffects.AdministrativeOpportunity(.9,1,.1,.1,700)>grain,"Contemporary institutional examples accelerate local learning without granting a state");
        var old=new HistoryState{Version="history-8"};string hash=StateHash.Compute(old);
        var extended=old.Copy();extended.Version="history-9";extended.RegionalLearning=new HistoricalRegionalLearning{FarmingSource=new[]{0,-1},FarmingExposure=new[]{600,0}};
        var copied=extended.Copy();copied.RegionalLearning.FarmingExposure[0]=700;
        check(StateHash.Compute(copied)!=StateHash.Compute(extended)&&extended.RegionalLearning.FarmingExposure[0]==600,"Regional learning is integrity hashed and deep copied");
        copied.RegionalLearning.FarmingSource[0]=1;check(extended.RegionalLearning.FarmingSource[0]==0,"Regional source identity has no shared mutable backing array");
        check(old.RegionalLearning==null&&StateHash.Compute(old)==hash,"Old checkpoint remains untouched by new regional records");
    }
}
