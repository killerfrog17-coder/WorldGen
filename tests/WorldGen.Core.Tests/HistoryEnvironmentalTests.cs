using System.Reflection;
using System.Diagnostics;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

public static class HistoryEnvironmentalTests
{
    static HabitatCell Habitat(int cold,int growing,int moisture,int soil,Biome biome=Biome.Grassland)=>new HabitatCell{ColdTenthsC=cold,WarmTenthsC=300,MeanTenthsC=(cold+300)/2,GrowingDays=growing,AridityMilli=moisture,SoilFertilityPermille=soil,Biome=biome};
    public static void Run(Action<bool,string> check)
    {
        var plain=Habitat(80,260,700,650);plain.Flooding=FloodRegime.RiverSeasonal;plain.FloodFractionPermille=50;
        var jungle=Habitat(250,365,1800,200,Biome.TropicalRainforest);jungle.Habitat=WorldGen.Core.Habitat.Forest;jungle.Soil=SoilKind.Leached;
        var cold=Habitat(-200,75,500,350,Biome.Tundra);cold.PermafrostPermille=650;
        var desert=Habitat(100,300,40,150,Biome.Desert);var flood=Habitat(100,300,40,500,Biome.Floodplain);flood.Flooding=FloodRegime.RiverSeasonal;flood.FloodFractionPermille=50;
        var highland=Habitat(120,260,900,600);double good=HistoricalEnvironmentalEffects.Establishment(plain,.7,.15),wet=HistoricalEnvironmentalEffects.Establishment(jungle,.1,.15);
        check(wet>0&&wet<good/1000,"Year-round lowland rainforest is strongly disfavored even on a river, not forbidden");
        check(HistoricalEnvironmentalEffects.Establishment(cold,.6,0)<good/1000,"Short frozen growing seasons do not become ideal early civilization sites through storage seasonality");
        var cool=Habitat(-47,273,1741,650,Biome.BorealForest);cool.MeanTenthsC=81;cool.WarmTenthsC=189;
        check(HistoricalEnvironmentalEffects.Establishment(cool,.7,.15)<good/30,"A nominal273-day growing season in a cool continental habitat is not treated like273 warm productive days");
        check(HistoricalEnvironmentalEffects.Establishment(flood,.7,.15)>HistoricalEnvironmentalEffects.Establishment(desert,.7,0)*20,"Real alluvial acreage supports an arid-world exception; desert alone does not");
        check(HistoricalEnvironmentalEffects.Establishment(highland,.7,.1)>wet*100,"Cooler seasonal tropical highlands remain distinguishable from lowland rainforest");
        check(HistoricalEnvironmentalEffects.Establishment(jungle,.1,.15,20000,1)>wet*20,"Actual practice and later management can ease tropical constraints");
        check(HistoricalEnvironmentalEffects.TraversalCost(jungle,.1,0)>HistoricalEnvironmentalEffects.TraversalCost(plain,.7,0)*10,"Jungle is intrinsically a movement barrier, even jungle to jungle");
        check(HistoricalEnvironmentalEffects.TraversalCost(jungle,.1,1)<HistoricalEnvironmentalEffects.TraversalCost(jungle,.1,0),"Later environmental management reduces, rather than reverses, terrain barriers");
        check(HistoricalEnvironmentalEffects.LatitudeTransfer(30,30,0)==1&&HistoricalEnvironmentalEffects.LatitudeTransfer(30,0,0)<.04,"Initial crop packages do not cross thirty degrees of daylength for free");
        check(HistoricalEnvironmentalEffects.LatitudeTransfer(30,0,1)>HistoricalEnvironmentalEffects.LatitudeTransfer(30,0,0)*5,"Selected cultivars can broaden their latitudinal range over time");
        check(HistoricalEnvironmentalEffects.HomelandMultiplier(0,false)==1,"An isolated starting island is not excluded before humans can establish distant populations");
        check(HistoricalEnvironmentalEffects.HomelandMultiplier(1500,true)==.00001&&HistoricalEnvironmentalEffects.HomelandMultiplier(3500,true)==1,"Anti-homeland rule is strong, explicit and has a continuous distance scale");
        double far=HistoricalEnvironmentalEffects.IndependentStateHazard(.9,good,.8,1),near=HistoricalEnvironmentalEffects.IndependentStateHazard(.9,good,.8,.00001);
        check(near>0&&near/far<.00002,"An otherwise equal near-homeland pristine site is far below one percent of the distant site's hazard");
        check(HistoricalEnvironmentalEffects.IndependentStateHazard(.9,good,.05,1)<far/50,"Age and a river do not substitute for a locally usable storable staple");
        check(HistoricalEnvironmentalEffects.LearnedStateHazard(.9,wet,.8,900)<HistoricalEnvironmentalEffects.LearnedStateHazard(.9,good,.8,900)/1000,"Diffusion does not waive local rainforest production constraints");
        check(HistoricalEnvironmentalEffects.CulturalAdoption(.0000001,0)<HistoricalEnvironmentalEffects.CulturalAdoption(1,0)/100000,"A fixed diffusion floor does not make frozen barren habitats ordinary advanced-culture sites");
        Console.WriteLine($"Ecological foundation factors: seasonal alluvial {good:G6}; ever-wet river {wet:G6}; equal-site homeland/distant hazard ratio {near/far:G6}.");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;var engine=new Prehistory(world);string original=source.ComputeHash();
        var s=engine.UpgradeToEnvironmentalHistory(source);engine.Validate(s);
        check(s.History.Version==Prehistory.EnvironmentalHistoryVersion&&source.History.OriginReach==null&&source.ComputeHash()==original,"Environmental rules require explicit opt-in and preserve the old checkpoint");
        check(s.Society.Settlements.Select(t=>t.Name).SequenceEqual(source.Society.Settlements.Select(t=>t.Name))&&s.History.Aggregates.TotalPopulation==source.History.Aggregates.TotalPopulation,"Upgrade preserves existing people and proper names");
        var copy=s.Copy();copy.History.OriginReach.DistantResidenceYear=-1;copy.History.OriginReach.DistantRegion=-1;
        check(!ReferenceEquals(copy.History.OriginReach,s.History.OriginReach)&&s.ComputeHash()==s.Hash,"Origin provenance is detached in snapshots");
        var next=engine.AdvanceYears(s,10);engine.Validate(next);var split=engine.AdvanceYears(engine.AdvanceYears(s,4),6);
        check(next.Hash==split.Hash,"Environmental transport and formation are partition independent");
        world.Prehistory=next;var restored=WorldFile.Read(WorldFile.Write(world));check(restored.Prehistory.Hash==next.Hash&&engine.AdvanceYears(restored.Prehistory,2).Hash==engine.AdvanceYears(next,2).Hash,"Saved origin reach and language profiles survive exact continuation");
        for(int k=0;k<37;k++)check(HistoricalTechnicalGraph.Cost(k,2,Prehistory.EnvironmentalHistoryVersion)==HistoricalTechnicalGraph.Cost(k,2,Prehistory.ResearchPacingHistoryVersion),"Environmental changes preserve calibrated research costs");
        // A stale remembered contact must not be stamped as a new delivery.
        var memory=next.History.Aggregates.Provinces.Exchanges.FirstOrDefault(m=>m.Peers.Length>0);
        if(memory!=null)
        {
            var probe=next.Copy();int receiver=Array.IndexOf(next.History.Aggregates.Provinces.Exchanges,memory);var m=probe.History.Aggregates.Provinces.Exchanges[receiver];
            for(int k=0;k<m.LastYear.Length;k++)m.LastYear[k]=Math.Max(0,probe.Years-1000);
            var culture=probe.Society.Cultures[receiver];int region=probe.History.Aggregates.Cultures[receiver].Region;probe.History.Aggregates.Living.Regions[region].Teacher=-1;
            int contact=(int)typeof(Prehistory).GetMethod("LivingContact",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,new object[]{probe,receiver,-1})!;
            check(contact<0,"An old contact without a fresh physical delivery is not a perpetually current teacher");
        }
    }
    public static void Review(string preset,int seed,int detail,string output,int maximumYear)
    {
        Directory.CreateDirectory(output);if(preset=="ContinentsPlus")preset="Continents plus";
        if(!WorldPresets.Names.Contains(preset))throw new ArgumentException("Use a real setup preset, not an unrecognized alias.");
        var setup=new WorldSetup();var recipe=setup.Resolve(setup.Preset(preset,new Recipe{Seed=seed,Detail=detail}));
        var watch=Stopwatch.StartNew();var world=new WorldPipeline().Generate(recipe);var engine=new Prehistory(world);var initial=engine.Start(engine.Prepare());
        var session=new SimulationSession(engine,initial);var rows=new List<object>();int first=-1;
        while(session.Seconds<maximumYear*SimulationTime.Year)
        {
            session.AdvanceSeconds(Math.Min(1000*SimulationTime.Year,maximumYear*SimulationTime.Year-session.Seconds),int.MaxValue);
            var state=session.LastSnapshot;first=state.History.Polities.Select(p=>p.FoundedYear).DefaultIfEmpty(-1).Min();
            if(state.Years%10000==0||first>=0){Console.WriteLine($"{preset}/{seed}: {state.Years} years, {state.Society.Cultures.Length} cultures, {state.Society.Settlements.Length} towns, {state.History.Polities.Length} states, {watch.Elapsed.TotalSeconds:F1}s");}
            if(first>=0&&state.Years>=first+2000)break;
        }
        var s=session.Snapshot();engine.Validate(s);world.Prehistory=s;
        var origin=world.Surface.Cells[engine.Patches[s.Origin].Cell].Position;
        object Location(int cell)=>new{Cell=cell,Latitude=Math.Asin(world.Surface.Cells[cell].Position.Z)*180/Math.PI,Longitude=Math.Atan2(world.Surface.Cells[cell].Position.Y,world.Surface.Cells[cell].Position.X)*180/Math.PI,OriginKm=D.Angle(origin,world.Surface.Cells[cell].Position)*recipe.RadiusKm,Climate=world.Climate.Classification[cell].ToString(),Habitat=world.Ecology.Habitat[cell]};
        object Call(string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,values)!;
        var origins=s.History.Archive.Where(e=>e.Kind=="independent-institutions").Select(e=>new{e.Year,Location=Location(e.Cell),Staples=Call("EnvironmentalStaples",s,e.CultureId,e.Cell),Environment=Call("EnvironmentalEstablishment",s,e.Cell,e.CultureId),Homeland=Call("HomelandOriginFactor",s,e.Cell)}).ToArray();
        var report=new{Preset=preset,Seed=seed,Detail=detail,Recipe=recipe,Rule=s.History.Version,Year=s.Years,FirstState=first,ElapsedSeconds=watch.Elapsed.TotalSeconds,Distant=s.History.OriginReach,Origins=origins,
            FirstCultures=s.Society.Cultures.Take(12).Select(c=>new{c.Name,c.FoundedYear,ParentCulture=s.History.Cultures[c.Id].ParentCultureId,Location=Location(c.OriginCell)}),States=s.History.Polities.Length,Towns=s.Society.Settlements.Length,Population=s.History.Aggregates.TotalPopulation};
        File.WriteAllText(Path.Combine(output,"origins.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        File.WriteAllBytes(Path.Combine(output,"final.world"),WorldFile.Write(world));HistoryAudit.Snapshot(world,engine,s,output,new[]{HistoryOverlay.States});
        Console.WriteLine("REVIEW "+JsonSerializer.Serialize(report,new JsonSerializerOptions{IncludeFields=true}));
    }
    public static void Benchmark(string path,int years)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var state=engine.UpgradeToEnvironmentalHistory(world.Prehistory);var session=new SimulationSession(engine,state);
        session.AdvanceSeconds(SimulationTime.Year,int.MaxValue);double worst=0;
        engine.YearWorkObserver=(year,ms)=>worst=Math.Max(worst,ms);var watch=Stopwatch.StartNew();
        for(int k=0;k<years;k++)session.AdvanceSeconds(SimulationTime.Year,int.MaxValue);
        double elapsed=watch.Elapsed.TotalSeconds;engine.Validate(session.LastSnapshot);
        Console.WriteLine(JsonSerializer.Serialize(new{Cells=world.Surface.Cells.Length,Years=years,ElapsedSeconds=elapsed,YearsPerSecond=years/elapsed,WorstYearWorkMilliseconds=worst,Rule=session.LastSnapshot.History.Version,FinalYear=session.LastSnapshot.Years}));
    }
}
