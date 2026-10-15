using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public static class HistoryRealismTests
{
    public static void Run(string folder,Action<bool,string> check)
    {
        Directory.CreateDirectory(folder);
        var taxa=NativeLifePack.Create();Taxon T(string name)=>taxa.Single(t=>t.Name==name);
        check(taxa.Length>=90&&taxa.Select(t=>t.Id).SequenceEqual(Enumerable.Range(0,taxa.Length)),"Expanded native pack has stable contiguous identities");
        var jungle=new HabitatCell{Biome=Biome.TropicalRainforest,Habitat=Habitat.Forest,ColdTenthsC=240,WarmTenthsC=280,GrowingDays=365,AridityMilli=1700,SoilFertilityPermille=280,Soil=SoilKind.Leached};
        var seasonal=new HabitatCell{Biome=Biome.Savanna,Habitat=Habitat.Open,ColdTenthsC=190,WarmTenthsC=300,GrowingDays=300,AridityMilli=700,SoilFertilityPermille=700};
        check(!Biogeography.Suitable(T("Common buzzard"),jungle,100),"Common buzzard cannot inhabit hot year-round rainforest");
        check(new[]{"Snowshoe hare","Canada lynx","Snowy owl","Red deer","Deer mouse","Red-tailed hawk"}.All(name=>!Biogeography.Suitable(T(name),jungle,100)),"Cold and temperate forest taxa are not interchangeable with tropical forest fauna");
        check(Biogeography.Suitable(T("Leopard"),jungle,100)&&Biogeography.Suitable(T("Blue duiker"),jungle,100),"Rainforest has a plausible regional predator and mammal prey");
        check(Biogeography.CanEat(T("Leopard"),T("Blue duiker")),"Leopard can eat forest ungulate prey");
        check(Biogeography.CanEat(T("Crowned eagle"),T("Guereza")),"Canopy hunting reaches arboreal prey");
        check(!Biogeography.CanEat(T("Lion"),T("Crowned eagle")),"Ground hunting does not reach airborne raptors");
        check(NativeLifePack.Domesticability(T("Przewalski's horse").Traits)>5*NativeLifePack.Domesticability(T("Plains zebra").Traits),"Equid breeding/behavior traits distinguish husbandry potential");
        double dry=HistoricalLandscapeEffects.CropAreaYield(seasonal,.04,.65),wet=HistoricalLandscapeEffects.CropAreaYield(jungle,.04,.15);
        check(dry>wet*4,"Small river fraction does not erase rainforest clearing/soil constraints");
        check(HistoricalLandscapeEffects.CropAreaYield(jungle,.04,.15)>0,"Forest cultivation retains a real, nonzero path");
        var mask=WorldJson.Read<HabitatCell>(WorldJson.Write(jungle));mask.Biome=Biome.Floodplain;mask.Habitat=Habitat.Wetland;
        check(HistoricalLandscapeEffects.CropAreaYield(mask,.04,.15)<dry/4,"Floodplain label cannot bypass ever-wet constraints");
        check(HistoricalLandscapeEffects.LivelihoodContinuity(seasonal,seasonal,jungle)<.05,"Strong environmental boundaries resist a single unchanged cultural footprint");
        check(HistoricalPoliticalCycleEffects.CrisisChance(800,800,600,500,300,800)>HistoricalPoliticalCycleEffects.CrisisChance(0,0,0,0,900,0)*20,"Personal conquest and elite capture destabilize succession more than integrated administration");
        var practice=new HistoricalCulture{Institution=800,Surplus=800,Technology=Enumerable.Repeat(1,HistoricalTechnologies.Names.Length).ToArray()};var people=new CultureIdentity{Farming=800};
        var isolated=new[]{new Settlement{Population=200000,FoodCapacity=250000,Stability=800,PolityId=0}};
        check(!HistoricalPracticeRequirements.Supports(200000,people,practice,isolated,HistoricalTechnology.Computing,false,false,"history-16"),"An isolated large town is not a modern specialist urban network");
        var network=Enumerable.Range(0,4).Select(_=>new Settlement{Population=30000,FoodCapacity=50000,Stability=800,PolityId=0}).ToArray();
        check(HistoricalPracticeRequirements.Supports(200000,people,practice,network,HistoricalTechnology.Computing,false,false,"history-16"),"A provisioned specialist urban network can sustain modern practices");
        // Five feeding levels in a valid acyclic example; no display-only three-tier clamp.
        var webWorld=new World{Ecology=new EcologyResult{Region=new[]{0},Taxa=Enumerable.Range(0,5).Select(id=>new Taxon{Id=id,Name="Level "+id,Producer=id==0}).ToArray(),Regions=new[]{new Ecoregion{Id=0,Species=Enumerable.Range(0,5).Select(id=>new SpeciesPresence{TaxonId=id}).ToArray(),FoodWeb=Enumerable.Range(1,4).Select(id=>new FoodLink{ResourceId=id-1,ConsumerId=id}).ToArray()}}}};
        check(FoodWebDiagram.Build(webWorld,0).Nodes.Max(n=>n.Column)==4,"Food web renders all feeding levels rather than merging apex columns");
        var r=Recipe.Preset("Earth");r.Detail=5000;var pipeline=new WorldPipeline();var w=pipeline.Generate(r);
        check(w.Ecology.PackVersion==NativeLifePack.Version,"New worlds select explicit versioned native content");
        var old=r.Clone();old.EcologyProfile=null;var before=pipeline.Generate(old);
        check(StateHash.Compute(new object[]{w.ElevationM,w.Climate,w.Hydrology})==StateHash.Compute(new object[]{before.ElevationM,before.Climate,before.Hydrology})&&w.CoarseHash==before.CoarseHash,"Ecology update leaves terrain/climate/water/coarse partition unchanged");
        check(before.Ecology.Taxa.Length==50,"Old ecology profile retains its original species pack");
        foreach(var again in new[]{pipeline.Reapply(r),pipeline.Generate(WorldJson.ReadRecipe(WorldJson.Write(r))),pipeline.Reconstruct(w)})check(w.Stages.Select(s=>s.Hash).SequenceEqual(again.Stages.Select(s=>s.Hash)),"New native content reconstructs identically through every route");
        var restored=WorldFile.Read(WorldFile.Write(w));check(StateHash.Compute(restored.Ecology)==StateHash.Compute(w.Ecology),"Expanded traits and regional forms persist in compact worlds");
        check(w.Ecology.Regions.SelectMany(x=>x.Species).Any(p=>p.Variant!=null),"Regional animal forms are generated and persisted");
        var engine=new Prehistory(w);var state=engine.Start(engine.Prepare());check(state.History.Version=="history-16","New simulations use versioned realistic environment and succession rules");w.Prehistory=state;
        File.WriteAllBytes(Path.Combine(folder,"earth5000.world"),WorldFile.Write(w));
        File.WriteAllText(Path.Combine(folder,"ecology-summary.json"),System.Text.Json.JsonSerializer.Serialize(new{pack=w.Ecology.PackVersion,taxa=taxa.Length,present=w.Ecology.Regions.SelectMany(x=>x.Species).Select(x=>x.TaxonId).Distinct().Count(),links=w.Ecology.Regions.Sum(x=>x.FoodWeb.Length)}));
    }
    public static void Fixture(string preset,int detail,int seed,string output)
    {
        var r=Recipe.Preset(preset);r.Detail=detail;r.Seed=seed;var w=new WorldPipeline().Generate(r);var engine=new Prehistory(w);w.Prehistory=engine.Start(engine.Prepare());File.WriteAllBytes(output,WorldFile.Write(w));Console.WriteLine("Native ecology/history16 fixture "+output);
    }
    public static void Political(string file,Action<bool,string> check)
    {
        var w=WorldFile.Read(File.ReadAllBytes(file));var engine=new Prehistory(w);var original=w.Prehistory;var state=engine.UpgradeToCurrentHistory(original);
        string prior=original.Hash;check(original.ComputeHash()==prior,"Upgrade preserves the legacy checkpoint hash");
        var roll=typeof(Prehistory).GetMethod("HistoryRoll",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        var chosen=state.History.Polities.Where(p=>p.EndedYear<0&&state.Society.Settlements.Count(t=>t.PolityId==p.Id&&t.Population>=500)==1).First(p=>(double)roll.Invoke(engine,new object[]{"political-transition-16",state.History.Dynamics.States[p.Id].LeaderId,state.Years})!<.8);
        var ds=state.History.Dynamics.States[chosen.Id];ds.Cycle=new HistoricalPoliticalCycle{EliteCapture=1000,ConquestDebt=1000};ds.Strain=700;chosen.Exhaustion=1000;state.History.Cultures[chosen.CultureId].Institution=0;
        var leader=state.History.Dynamics.Leaders[ds.LeaderId];leader.Renown=1000;int oldCount=state.History.Polities.Length;long people=SocietyView.Population(state),money=chosen.Treasury;int army=chosen.Army;int[] cells=Enumerable.Range(0,state.History.CellPolity.Length).Where(i=>state.History.CellPolity[i]==chosen.Id).ToArray();
        var crisis=typeof(Prehistory).GetMethod("PoliticalSuccessionCrisis",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        check((bool)crisis.Invoke(engine,new object[]{state,chosen,leader,leader,CancellationToken.None})!,"A stressed one-city court can experience a real succession crisis");
        state.Seal();engine.Validate(state);var successor=state.History.Polities[oldCount];
        check(chosen.EndedYear==state.Years&&successor.ParentId==chosen.Id,"Contested one-city regime records a successor state, not eternal identity");
        check(SocietyView.Population(state)==people&&successor.Army==army&&chosen.Army==0&&successor.Treasury<=money&&chosen.Treasury==0,"Political transition conserves people and armies and cannot mint treasury");
        check(cells.All(cell=>state.History.CellPolity[cell]==successor.Id),"Existing territory transfers without a new circular cultural claim");
        var labels=SocietyView.MapLabels(w,state,HistoryOverlay.Civilizations,engine);check(labels.Any(e=>e.Name==successor.Name)&&!labels.Any(e=>e.Name==chosen.Name),"Civilization overlay labels current individual states");
        var occupied=state.Copy();int sample=Enumerable.Range(0,occupied.Society.CellCulture.Length).First(i=>occupied.Society.Settlements.All(t=>t.Cell!=i));int native=(successor.CultureId+1)%occupied.Society.Cultures.Length;
        occupied.History.CellPolity[sample]=successor.Id;occupied.Society.CellCulture[sample]=native;
        check(HistoryAtlas.Selection(occupied,sample,HistoryOverlay.Civilizations)?.Id==successor.CultureId,"Civilization inspector follows ruling culture after conquest");
        check(HistoryAtlas.Selection(occupied,sample,HistoryOverlay.Cultures)?.Id==native,"Culture inspector retains the distinct local identity");
        int capitalCell=state.Society.Settlements[successor.CapitalId].Cell;
        check(HistoryAtlas.Selection(state,capitalCell,HistoryOverlay.States)?.Kind=="state","Capital cell does not mask state-mode inspection with a city profile");
        check(HistoryAtlas.Selection(state,capitalCell,HistoryOverlay.Civilizations)?.Kind=="people","Capital cell retains broader civilization inspection in civilization mode");
        w.Prehistory=state;var restored=WorldFile.Read(WorldFile.Write(w));check(restored.Prehistory.Hash==state.Hash,"Political cycle and succession persist in compact saves");
        check(new Prehistory(restored).AdvanceYears(restored.Prehistory,3).Hash==engine.AdvanceYears(state,3).Hash,"Political continuation agrees after save/restore");
    }
    public static void Project(string file,int years,string output)
    {
        var w=WorldFile.Read(File.ReadAllBytes(file));var engine=new Prehistory(w);var before=w.Prehistory;var watch=System.Diagnostics.Stopwatch.StartNew();var after=engine.AdvanceYears(before,years);engine.Validate(after);
        object Summary(PrehistoryState s)=>new{year=s.Years,population=SocietyView.Population(s),states=s.History.Polities.Count(p=>p.EndedYear<0),claimedCells=s.History.CellPolity.Count(i=>i>=0),claimedKm2=s.History.Polities.Where(p=>p.EndedYear<0).Sum(p=>p.AreaKm2),records=s.History.Polities.Length};
        File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(new{before=Summary(before),after=Summary(after),elapsedSeconds=watch.Elapsed.TotalSeconds,hash=after.Hash}));Console.WriteLine(File.ReadAllText(output));
    }
}
