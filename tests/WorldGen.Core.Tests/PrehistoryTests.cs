using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public static class PrehistoryTests
{
    public static void Run(Action<bool,string> check)
    {
        foreach(string preset in new[]{"Earth","Continents"})
        {
            var recipe=Recipe.Preset(preset);recipe.Detail=5000;var world=new WorldPipeline().Generate(recipe);string terrainHash=StateHash.Compute(world);
            var watch=System.Diagnostics.Stopwatch.StartNew();var engine=new Prehistory(world);var prepared=engine.Prepare();
            check(Prehistory.YearsPerStep(prepared)==100,"Prehistory defaults to 100-year steps");
            bool savanna=engine.Patches.Any(p=>!p.Island&&p.Capacity>=200&&world.Ecology.Regions[p.Region].Biome==Biome.Savanna);
            check(!savanna||Enumerable.Range(0,10).All(v=>world.Ecology.Regions[engine.Patches[engine.ChooseOrigin(v)].Region].Biome==Biome.Savanna),"Savanna preferred for initial and rerolled origins");
            check(Prehistory.CrossingChance(20000,false)>Prehistory.CrossingChance(130000,false)&&Prehistory.CrossingChance(130000,false)>.025,"More frequent distance-sensitive short crossings");
            check(prepared.Population.All(p=>p==0),"Setup has only an origin, no advancing population");
            var codes=PrehistoryView.Codes(world,engine,prepared);check(codes.Count(c=>c==255)==engine.Patches[prepared.Origin].Cells.Length,"Starting patch highlight");
            check(engine.Routes.Any(r=>r.Coastal),"Explicit shoreline detour routes");
            check(Prehistory.RouteChance(new Prehistory.Route{Sea=true,Coastal=true,DistanceM=200000},0)>Prehistory.RouteChance(new Prehistory.Route{Sea=true,DistanceM=200000},0),"Coastal travel is distinct from a deep-ocean voyage");
            check(engine.Routes.All(r=>!r.Sea||r.DistanceM<=(r.Shallow?1800000:900000)),"No unbounded sea crossings");
            check(engine.Patches.All(p=>p.Island?p.Capacity>=25&&SubcellGeography.Find(world,p.Cell)!=null:p.Cells.All(c=>world.Ecology.Region[c]==p.Region&&!world.Hydrology.OceanConnected[c])),"Land patches and persistent island refuges");
            check(Prehistory.SeaLevelDropM(0)==0&&Prehistory.SeaLevelDropM(85000)==130&&Prehistory.SeaLevelDropM(100000)==0,"Continuous glacial cycle");
            var shallowRoute=new Prehistory.Route{Sea=true,Shallow=true,SeaDropM=70,DistanceM=400000};
            check(Prehistory.RouteChance(shallowRoute,0)==0&&Prehistory.RouteChance(shallowRoute,85000)>.5,"Shelf route opens with sufficient sea-level fall");
            var deepRoute=new Prehistory.Route{Sea=true,DistanceM=400000};
            check(Prehistory.RouteChance(deepRoute,0)==Prehistory.RouteChance(deepRoute,85000)&&Prehistory.RouteChance(deepRoute,0)<.01,"Ice cannot expose deep ocean");
            check(engine.HabitatSimilarity(prepared.Origin,prepared.Origin)==1,"Identical habitat maximally similar");
            int unfamiliar=Enumerable.Range(0,engine.Patches.Length).OrderBy(i=>engine.HabitatSimilarity(prepared.Origin,i)).First();
            var landRoute=new Prehistory.Route();int origin=prepared.Origin;
            check(engine.DispersalChance(landRoute,origin,origin,200,10000,1000,1000)>engine.DispersalChance(landRoute,origin,unfamiliar,200,10000,1000,1000),"Small bands favor familiar habitats");
            check(engine.DispersalChance(landRoute,origin,unfamiliar,9000,10000,1000,1000)>engine.DispersalChance(landRoute,origin,unfamiliar,200,10000,1000,1000),"Crowding increases unfamiliar-habitat migration");
            check(Prehistory.RouteChance(new Prehistory.Route{Sea=true,DistanceM=100000},0)>Prehistory.RouteChance(deepRoute,0),"Deep crossing probability decays with distance");
            check(engine.CapacityAt(prepared.Origin,0)!=engine.CapacityAt(prepared.Origin,85000),"Resource and glacial carrying-capacity variation");
            var started=engine.Start(prepared);string before=started.Hash;
            var once=engine.Advance(started,1);check(once.Population.Count(p=>p>0)==1,"No immediate long-range spread");
            var block=engine.Advance(started,200);var repeated=started;
            for(int i=0;i<200;i++)repeated=engine.Advance(repeated,1);
            check(block.Hash==repeated.Hash,"Playback speed/batch independent "+preset);check(started.Hash==before&&started.Years==0,"Advance preserves prior checkpoint");
            var final=engine.Advance(block,1800);check(final.Years==200000,"200,000-year timeline");
            check(final.Society.Cultures.Length>0&&final.Society.Settlements.Length>0&&final.Society.Phase==SocietyStage.Civilization,"Opportunity-driven progression through cities "+preset);
            check(Prehistory.YearsPerStep(final)==1,"Civilization automatically uses one-year steps");
            var split=engine.AdvanceYears(engine.AdvanceYears(final,17),83);var whole=engine.AdvanceYears(final,100);
            check(split.Hash==whole.Hash,"Annual city simulation independent of irregular batching");
            var annual=final;for(int i=0;i<100;i++)annual=engine.AdvanceYears(annual,1);
            check(annual.Hash==whole.Hash,"One-year and century checkpoints identical");
            var manual=engine.SetStepYears(started,37);manual=engine.AdvanceYears(manual,200000);
            check(manual.Society.ManualStep&&Prehistory.YearsPerStep(manual)==37,"Manual step survives every phase transition");
            manual=engine.SetStepYears(manual,1,true);check(manual.Hash==final.Hash,"Clock preferences never change authoritative historical outcomes");
            check(final.Society.CellCulture.Select((c,i)=>c<0||!world.Hydrology.OceanConnected[i]).All(x=>x),"Cultural land claims exclude ocean");
            check(final.Society.Settlements.All(t=>final.Society.CellCulture[t.Cell]==t.CultureId&&t.PolityId==t.Id&&t.FoodCapacity<=30000&&t.FoodReserve<=t.FoodCapacity*3),"Independent city polities, bounded food budget, valid cell ownership");
            check(SocietyView.Population(final)==final.Population.Sum()+final.Society.Settlements.Sum(t=>t.Population),"Total includes rural and urban residents exactly once");
            check(Enumerable.Range(0,world.Surface.Cells.Length).Sum(i=>engine.RuralCellPopulation(final,i))==final.Population.Sum(),"Cell allocations conserve all rural people");
            check(SocietyView.Ranked(final).Select(c=>c.Stage).SequenceEqual(SocietyView.Ranked(final).Select(c=>c.Stage).OrderByDescending(x=>x)),"Civilization-first ranking");
            var frozen=final.Society.Cultures.Where(c=>c.Stage==SocietyStage.Civilization).Select(c=>(c.Id,c.Farming,c.Organization)).ToArray();
            var longRun=engine.AdvanceYears(final,100000);
            check(frozen.All(c=>longRun.Society.Cultures[c.Id].Farming==c.Farming&&longRun.Society.Cultures[c.Id].Organization==c.Organization),"Earliest-civilization technology remains fixed over 100,000 years");
            var legacy=prepared.Copy();legacy.Version="prehistory-1";legacy.Seal();legacy=engine.Start(legacy);
            check(engine.Advance(legacy,1).Years==250,"Legacy save retains original step rules");
            var highlighted=PrehistoryView.Codes(world,engine,final,engine.Patches[final.Origin].Cell);check(highlighted.Any(c=>c==255||c==245)&&highlighted.Any(c=>c==0),"Selected population and uniform-water codes");
            check(final.Population.Count(p=>p>0)>1,"Humans expand "+preset);check(final.Ancestry.Any(g=>g!=5000),"Neutral drift occurs");
            check(final.Population.Select((p,i)=>p>=0&&p<=engine.Patches[i].Capacity*6.5+1).All(x=>x),"Population bounded by foraging/cultivation habitat envelope");
            world.Prehistory=final;check(StateHash.Compute(world)==terrainHash,"Simulation cannot change generated world hash");
            var restored=WorldFile.Read(WorldFile.Write(world));check(restored.Prehistory.Hash==final.Hash,"Population and ancestry save roundtrip");
            Directory.CreateDirectory("artifacts/societies-1");File.WriteAllBytes("artifacts/societies-1/"+preset+".world",WorldFile.Write(world));
            check(new Prehistory(restored).Advance(restored.Prehistory,4).Hash==engine.Advance(final,4).Hash,"Save/load exact continuation");
            var corrupt=final.Copy();corrupt.Population[corrupt.Origin]++;try{engine.Validate(corrupt);check(false,"Corruption rejected");}catch(ArgumentException){check(true,"Corruption rejected");}
            var alternate=engine.Prepare(engine.ChooseOrigin(1));check(alternate.Years==0&&!alternate.Started,"Origin relocation resets only unstarted simulation");
            check(Prehistory.LowSeaLevel(25000)&&!Prehistory.LowSeaLevel(0),"Shelf accessibility cycle");
            var clusters=engine.Clusters(final);check(engine.Routes.Where(r=>!r.Sea&&final.Population[r.A]>0&&final.Population[r.B]>0&&engine.Patches[r.A].Lifestyle==engine.Patches[r.B].Lifestyle).All(r=>clusters[r.A]==clusters[r.B]),"Connected lifestyle groups");
            Console.WriteLine($"{preset}: {engine.Patches.Length} patches, {engine.Routes.Count(r=>r.Sea)} sea/shelf routes, {final.Population.Count(p=>p>0)} occupied at 200k years, {final.Population.Sum():N0} rural people, {final.Society?.Cultures.Length} cultures, {final.Society?.Settlements.Length} settlements, {final.Society?.Phase}, {watch.ElapsedMilliseconds} ms including save/replay checks");
        }
    }
}
