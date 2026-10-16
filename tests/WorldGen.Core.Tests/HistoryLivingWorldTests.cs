using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryLivingWorldTests
{
    public static void Run(Action<bool,string> check)
    {
        var x=HistoricalClimateVariability.At(42,3,120000);var again=HistoricalClimateVariability.At(42,3,120000);
        check(x.TemperatureTenthsC==again.TemperatureTenthsC&&x.PrecipitationPermille==again.PrecipitationPermille,"Historical climate anomalies are seeded deterministic queries");
        check(Enumerable.Range(0,100).Select(k=>HistoricalClimateVariability.At(42,3,120000+k*100).PrecipitationPermille).Distinct().Count()>20,"Regional wet and dry histories are not one permanent multiplier");
        check(Enumerable.Range(0,100).All(k=>{var a=HistoricalClimateVariability.At(42,k,120000+k*100);return a.PrecipitationPermille>=350&&a.PrecipitationPermille<=1650&&Math.Abs(a.TemperatureTenthsC)<=23;}),"Climate priors remain bounded and do not rewrite planet geography");
        var dry=new HabitatCell{MeanTenthsC=220,AridityMilli=350};var cold=new HabitatCell{MeanTenthsC=50,AridityMilli=700};
        var drought=new HistoricalClimateAnomaly(5,500);var normal=new HistoricalClimateAnomaly(0,1000);
        double loss=HistoricalClimateVariability.FoodResponse(dry,drought,0,true,0);
        check(loss<.8&&HistoricalClimateVariability.FoodResponse(dry,normal,0,true,0)==1,"Multi-decadal drought reduces rainfed food support, not a cosmetic climate label");
        check(HistoricalClimateVariability.FoodResponse(dry,drought,.2,true,0)>loss,"River access buffers local rainfall failure without removing all risk");
        check(HistoricalClimateVariability.FoodResponse(dry,drought,0,true,1)>loss,"Installed environmental management can mitigate crop failures");
        check(HistoricalClimateVariability.FoodResponse(cold,new HistoricalClimateAnomaly(15,1000),0,true,0)>1,"Modest warming may benefit short cold growing seasons");
        check(HistoricalProtocentreEffects.MaximumResidents(.8,.05,200,0)<800,"Poor early stored surplus does not support thousands of urban specialists");
        check(HistoricalProtocentreEffects.MaximumResidents(.8,.35,700,4)>1200,"Exceptional provisioned pre-state centers can reach the state-emergence threshold");
        check(HistoricalProtocentreEffects.MaximumResidents(.001,.35,700,4)>1200,"Lowland tropical exceptions are not capped forever below the civic threshold");
        check(HistoricalProtocentreEffects.MaximumResidents(.001,.10,700,4)>1200,"Even a low-staple tropical site can exceptionally reach civic scale rather than suffer an absolute deadlock");
        double weak=HistoricalProtocentreEffects.AnnualDispersalHazard(.1,.05,1,false),robust=HistoricalProtocentreEffects.AnnualDispersalHazard(1,.35,0,false);
        check(weak>robust*10&&robust>0,"Unsupported protocenters disperse much more often while exceptional sites can persist");
        check(HistoricalProtocentreEffects.AnnualDispersalHazard(.1,.05,1,true)==0,"An established city does not randomly vanish merely because it lacks a current ruler");
        double origin=HistoricalLivingWorldOrigins.IndependentStateHazard(.65,1,.8,1);
        check(origin>HistoricalEnvironmentalEffects.IndependentStateHazard(.65,1,.8,1)*5&&origin<.0001,"A good finite-lived protocenter gets an exceptional but not hundred-millennia institutional opportunity");
        check(HistoricalLivingWorldOrigins.IndependentStateHazard(.65,.00005,.8,1)<origin/10000,"Correcting compounded urban requirements does not waive the wet-tropical environmental penalty");
        check(HistoricalLivingWorldOrigins.IndependentStateHazard(.65,1,.8,.00001)<origin/50000,"New urban calibration retains the explicit strong anti-homeland bias");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var original=world.Prehistory;string hash=original.ComputeHash();
        var s=engine.UpgradeToLivingWorldHistory(original);engine.Validate(s);
        check(s.History.Version==Prehistory.LivingWorldHistoryVersion&&original.ComputeHash()==hash,"Living-world upgrade preserves the original checkpoint");
        var next=engine.AdvanceYears(s,20);engine.Validate(next);var partition=engine.AdvanceYears(engine.AdvanceYears(s,7),13);
        check(next.Hash==partition.Hash,"Climate, village lifecycle and physical transmission are partition-independent");
        world.Prehistory=next;var loaded=WorldFile.Read(WorldFile.Write(world));check(engine.AdvanceYears(loaded.Prehistory,3).Hash==engine.AdvanceYears(next,3).Hash,"Living-world save roundtrip resumes exactly");
        int town=Array.FindIndex(s.Society.Settlements,t=>t.Population>0&&s.History.Aggregates.Resolve(t.PolityId,s.History)<0);
        if(town<0)throw new InvalidOperationException("Fixture needs an active unruled settlement.");
        var probe=s.Copy();var t=probe.Society.Settlements[town];int region=engine.CellPatch[t.Cell];long before=probe.History.Aggregates.TotalPopulation,regional=probe.Population[region],residents=t.Population+probe.History.Aggregates.Cities[town].Rural+probe.History.Aggregates.Cities[town].Suburban;
        typeof(Prehistory).GetMethod("AbandonLivingTown",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,new object[]{probe,town,"Diagnostic lifecycle check."});
        check(probe.History.Aggregates.TotalPopulation==before&&probe.Population[region]==regional+residents,"Village dispersal returns all surviving households without deleting or creating population");
        check(probe.History.Aggregates.Living.Cities[town].AbandonedYear==probe.Years&&probe.History.Aggregates.TownAt[t.Cell]<0,"Abandoned sites leave the active spatial index, not historical records");
        check(probe.Society.Settlements[town].FoundedYear==s.Society.Settlements[town].FoundedYear&&probe.History.Archive.Last().Kind=="abandonment","A former settlement retains its dated narrative history");
        probe.Seal();engine.Validate(probe);
    }
}
