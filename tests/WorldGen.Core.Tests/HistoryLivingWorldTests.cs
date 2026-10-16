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
        double productive=HistoricalProtocentreEffects.AnnualDispersalHazard(.619,.141,0,false);
        check(Math.Exp(-productive*500)>.5&&Math.Exp(-weak*500)<.001,"A viable stored-food village can maintain a centuries-long farming catchment while weak camps remain transient");
        check(HistoricalProtocentreEffects.AnnualDispersalHazard(.619,.141,.6,false)>productive*5,"Real harvest failure still disrupts an otherwise durable productive village");
        check(HistoricalProtocentreEffects.AnnualDispersalHazard(.00005,.04,0,false)>.03,"Removing duplicated provisioning penalties does not make unsupported ever-wet camps durable centers");
        check(HistoricalProtocentreEffects.AnnualDispersalHazard(.1,.05,1,true)==0,"An established city does not randomly vanish merely because it lacks a current ruler");
        double origin=HistoricalLivingWorldOrigins.IndependentStateHazard(.65,1,.8,1);
        check(origin>HistoricalEnvironmentalEffects.IndependentStateHazard(.65,1,.8,1)*5&&origin<.0001,"A good finite-lived protocenter gets an exceptional but not hundred-millennia institutional opportunity");
        check(HistoricalLivingWorldOrigins.IndependentStateHazard(.65,.00005,.8,1)<origin/10000,"Correcting compounded urban requirements does not waive the wet-tropical environmental penalty");
        check(HistoricalLivingWorldOrigins.IndependentStateHazard(.65,1,.8,.00001)<origin/50000,"New urban calibration retains the explicit strong anti-homeland bias");
        long earlyImports=HistoricalUrbanNetworks.ImportThroughput(10000,1,1,1,1,1,1,0,0,0),modernImports=HistoricalUrbanNetworks.ImportThroughput(10000,1,1,1,1,1,1,1,1,1);
        check(earlyImports<2000000&&modernImports>earlyImports*5&&modernImports<20000000,"Real transport throughput permits major ancient/modern cities without allocating a whole country's surplus to one200Mcenter");
        check(HistoricalUrbanNetworks.ImportThroughput(10000,0,.2,1,1,1,0,0,0,0)<earlyImports/5,"Unbuilt inland handling infrastructure does not receive a free ocean-port food throughput");
        long pool=HistoricalSettlementMigration.Available(100000,10000,0,0,1);var transfer=HistoricalSettlementMigration.Allocate(2000,100000,10000,0,0,1);
        check(pool>=1600&&transfer.Total==2000&&transfer.Rural==0,"An urbanized metropolis can supply actual founders without requiring1600obsolete local farm workers");
        check(HistoricalSettlementMigration.Allocate(20000,1000,0,100,0,1).Total==100,"Founder allocation never creates households or empties a source center below its retained core");
        var remote=HistoricalSettlementMigration.Allocate(1000,1000,0,0,8000,2);check(remote.Remote==1000&&remote.Total==1000,"State remote recruitment is bounded and explicitly accounted, not repeated imaginary settlers");
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
        // This is an explicit diagnostic search, never an ordinary simulation
        // event. Exercise the complete founding transaction and its ledgers.
        probe=s.Copy();var queue=new SimulationEventQueue(probe.Timeline.Events);var queueField=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.NonPublic|BindingFlags.Instance)!;queueField.SetValue(engine,queue);
        var can=typeof(Prehistory).GetMethod("CanFoundLivingTown",BindingFlags.NonPublic|BindingFlags.Instance)!;
        var found=typeof(Prehistory).GetMethod("FoundLivingTown",BindingFlags.NonPublic|BindingFlags.Instance)!;
        var candidates=probe.Society.Settlements.Where(x=>probe.History.Aggregates.Living.Cities[x.Id].AbandonedYear<0&&x.Population>=1000).OrderByDescending(x=>x.Population).Take(16).ToArray();
        bool founded=false;
        foreach(var parent in candidates)
        {
            foreach(int cell in Enumerable.Range(0,world.Surface.Cells.Length).Where(cell=>engine.CellPatch[cell]>=0).OrderBy(cell=>D.Angle(world.Surface.Cells[parent.Cell].Position,world.Surface.Cells[cell].Position)))
            {
                if(!(bool)can.Invoke(engine,new object[]{probe,parent.CultureId,cell,parent.Id})!)continue;
                long population=probe.History.Aggregates.TotalPopulation;int count=probe.Society.Settlements.Length;
                found.Invoke(engine,new object[]{probe,parent.CultureId,cell,parent.Id});
                check(probe.Society.Settlements.Length==count+1&&probe.History.Aggregates.TotalPopulation==population,"Actual secondary founding conserves all urban, suburban, local-rural and remote households");
                probe.Timeline.Events=queue.Ordered();queueField.SetValue(engine,null);probe.Seal();engine.Validate(probe);founded=true;break;
            }
            if(founded)break;
        }
        queueField.SetValue(engine,null);check(founded,"The populated fixture contains a viable secondary settlement site, not a permanently blocked urban network");
    }
}
