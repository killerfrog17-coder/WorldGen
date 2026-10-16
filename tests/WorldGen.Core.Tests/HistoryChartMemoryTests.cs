using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryChartMemoryTests
{
    static object Call(Prehistory engine,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
    static string Mechanics(PrehistoryState s)
    {
        var h=s.History;var a=h.Aggregates;
        // Epidemic wave IDs point into the archive, not a stochastic seed or
        // population account. Different harmless log lengths renumber those
        // references; compare every other town field and every actor ledger.
        var towns=h.Towns.Select(t=>{var copy=t.Copy();copy.DiseaseWaveId=0;return copy;}).ToArray();
        return StateHash.Compute(new object[]{s.Years,s.Population,s.ArrivalYear,s.Ancestry,s.Timeline,s.Society.Cultures,s.Society.Settlements,s.Society.Knowledge,
            h.Cultures,h.Polities,h.Relations,towns,h.Religions,h.Agriculture,h.Dynamics.States,h.Dynamics.Campaigns,h.Dynamics.Dependencies,h.Dynamics.Leaders,h.Languages,h.Nuclear,h.International,
            a.TotalPopulation,a.TotalUrban,a.Cells,a.TownAt,a.Successors,a.Cities,a.Cultures,a.Polities,a.Regions,a.Living,a.Provinces,a.Communities,a.Empires});
    }
    public static void Run(Action<bool,string> check)
    {
        // Independently measured against frozen H's pre-extension Core DLL.
        var legacy=new HistoricalCulturalContinuity{PrecursorRegion=7,LastEvolutionYear=10,WayOfLife=2};
        check(StateHash.Compute(legacy)=="0dc60456c61256ce14ae79e5dd70f57b90ae0196d3e42a25ff552d71ff0ac706","An absent chart-receipt extension preserves the exact old canonical record hash");
        var tree=new HistoricalCellTree(1000000).With(3,7).With(999999,12);
        legacy.SeenCharts=tree;var copy=legacy.Copy();copy.SeenCharts=copy.SeenCharts.With(4,20);
        check(ReferenceEquals(legacy.SeenCharts,tree)&&legacy.SeenCharts[4]<0&&copy.SeenCharts[4]==20,"Cultural copies safely share immutable receipt nodes while insertions isolate branches");
        check(StateHash.Compute(tree.With(4,20))==StateHash.Compute(new HistoricalCellTree(1000000).With(4,20).With(999999,12).With(3,7)),"Receipt insertion order does not change the resulting canonical tree");
        int visited=0;Func<int,int,bool> valid=(region,year)=>{visited++;return region>=0&&year>=0&&year<=12;};
        var validate=typeof(HistoricalCellTree).GetMethod("AllPresentValues",BindingFlags.Instance|BindingFlags.NonPublic)!;
        check((bool)validate.Invoke(tree,new object[]{valid})!&&visited==2,"Explicit validation visits only two stored receipts, not a million possible regions");
        check(!(bool)validate.Invoke(tree.With(4,13),new object[]{valid})!,"Sparse receipt validation rejects future historical dates");
        var corrupt=new HistoricalCellTree(100).With(99,1);typeof(HistoricalCellTree).GetField("length",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(corrupt,2);
        check(!(bool)validate.Invoke(corrupt,new object[]{valid})!,"Sparse receipt validation rejects saved values outside their logical key range");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;string oldHash=source.ComputeHash();var engine=new Prehistory(world);
        var s=engine.UpgradeToCurrentHistory(source).Copy();var records=s.History.Aggregates.Empires.Connections.Cultures;
        int culture=Array.FindIndex(s.Society.Cultures,c=>c.Population>0);if(culture<0)culture=0;
        var record=records[culture];record.Charts=Array.Empty<HistoricalCoastChart>();record.SeenCharts=null;
        var hc=s.History.Cultures[culture];int budget=HistoricalMaritimeIntegrationEffects.ChartBudget(HistoricalTechnologies.Has(hc,HistoricalTechnology.OceanNavigation),HistoricalTechnologies.Has(hc,HistoricalTechnology.SteamPower)||HistoricalTechnologies.Has(hc,HistoricalTechnology.Telecommunications));
        check(engine.Patches.Length>budget,"Fixture provides more coast regions than the bounded working chart budget");
        int before=s.History.Archive.Count;
        // Storage regression only: actual route/paid-delivery callers remain
        // unchanged. These direct calls deliberately exercise eviction.
        for(int region=0;region<=budget;region++)Call(engine,"RecordCoastChart",s,culture,region,culture);
        check(record.Charts.Length==budget&&!record.Charts.Any(c=>c.Region==0)&&record.SeenCharts![0]==s.Years,"A first receipt survives working-chart eviction without granting active navigation access");
        check(s.History.Archive.Count==before+budget+1,"Every genuinely new culture/region acquisition keeps its historical event");
        var remembered=s.Copy();var forgotten=s.Copy();forgotten.History.Aggregates.Empires.Connections.Cultures[culture].SeenCharts=null;
        Call(engine,"RecordCoastChart",remembered,culture,0,culture);Call(engine,"RecordCoastChart",forgotten,culture,0,culture);
        check(forgotten.History.Archive.Count==remembered.History.Archive.Count+1&&Mechanics(remembered)==Mechanics(forgotten),"Suppressing a duplicate changes only narrative records, not active charts, queues, population, state, research, territory or diplomacy");
        remembered.Seal();forgotten.Seal();
        var futureRemembered=engine.AdvanceYears(remembered,3);var futureForgotten=engine.AdvanceYears(forgotten,3);
        check(Mechanics(futureRemembered)==Mechanics(futureForgotten),"Archive suppression cannot change later stochastic actor outcomes or scheduling under the same history32 rules");
        int archived=s.History.Archive.Count;Call(engine,"RecordCoastChart",s,culture,0,culture);
        check(record.Charts.Length==budget&&record.Charts[^1].Region==0&&s.History.Archive.Count==archived,"A real rediscovery restores the active working chart but does not re-archive the same acquisition");
        var branch=s.Copy();Call(engine,"RecordCoastChart",branch,culture,budget+1,culture);
        check(record.SeenCharts![budget+1]<0&&branch.History.Aggregates.Empires.Connections.Cultures[culture].SeenCharts![budget+1]==s.Years,"A branch's newly recorded destination cannot mutate an older publication's receipt tree");
        var bootstrap=s.Copy();var b=bootstrap.History.Aggregates.Empires.Connections.Cultures[culture];b.SeenCharts=null;
        int bootstrapCount=bootstrap.History.Archive.Count;Call(engine,"RecordCoastChart",bootstrap,culture,budget+1,culture);Call(engine,"RecordCoastChart",bootstrap,culture,2,culture);
        check(b.SeenCharts![0]>=0&&bootstrap.History.Archive.Count==bootstrapCount+1,"Old candidate data bootstraps current charts without scanning the archive or re-announcing an evicted bootstrap chart");
        var broken=s.Copy();broken.History.Aggregates.Empires.Connections.Cultures[culture].SeenCharts=record.SeenCharts!.With(budget+2,s.Years+1);bool rejected=false;
        try{Call(engine,"ValidateColonialConnections",broken);}catch(TargetInvocationException e)when(e.InnerException is ArgumentException){rejected=true;}
        check(rejected,"Saved future chart dates are rejected by explicit integrity validation");
        s.Seal();engine.Validate(s);var three=engine.AdvanceYears(s,3);var split=engine.AdvanceYears(engine.AdvanceYears(s,1),2);
        check(three.Hash==split.Hash,"Receipt updates remain publication-partition deterministic");
        world.Prehistory=three;var loaded=WorldFile.Read(WorldFile.Write(world));
        check(loaded.Prehistory.Hash==three.Hash&&loaded.Prehistory.History.Aggregates.Empires.Connections.Cultures[culture].SeenCharts![0]==record.SeenCharts![0],"Durable receipts and canonical hashes survive full snapshot serialization");
        check(new Prehistory(loaded).AdvanceYears(loaded.Prehistory,2).Hash==engine.AdvanceYears(three,2).Hash,"Saved receipt-bearing actors continue identically");
        check(source.ComputeHash()==oldHash,"Chart memory upgrades and all branches leave the old history checkpoint unchanged");
    }
}
