using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class EcologyTests
{
    public static void WriteReview()
    {
        string folder="artifacts/ecology-review"; Directory.CreateDirectory(folder);
        foreach(string name in new[]{"earth","one50","six30","crosshost"})
        {
            var recipe=Recipe.Preset(name=="earth"||name=="crosshost"?"Earth":"Preview"); recipe.Detail=name=="crosshost"?5000:20000;
            if(name=="one50"||name=="six30") { recipe.Seed=name=="one50"?73421:42; recipe.ContinentCount=name=="one50"?1:6; recipe.LandFraction=name=="one50"?.5:.3; }
            var w=new WorldPipeline().Generate(recipe); string prefix=Path.Combine(folder,name);
            File.WriteAllBytes(prefix+".world",WorldFile.Write(w)); File.WriteAllText(prefix+".recipe.json",WorldJson.Write(w.Recipe)); File.WriteAllText(prefix+".hashes.json",WorldJson.Write(w.Stages));
            Console.WriteLine(name+": "+w.Ecology!.Regions.Length+" ecoregions. Wrote review artifacts only; no golden files updated.");
        }
    }
    public static void Run(Action<bool,string> check)
    {
        FaunaPackTests.Run(check);
        var taxa=EarthLifePack.Create(); Taxon T(string name)=>taxa.Single(t=>t.Name==name);
        check(!Biogeography.CanEat(T("Gray wolf"),T("Red-tailed hawk")),"Ground predator cannot routinely catch aerial hawks");
        check(!Biogeography.CanEat(T("Gray wolf"),T("Lion")),"Pack ungulate-hunting capacity does not allow routine adult apex-predator prey");
        check(!Biogeography.CanEat(T("Gray wolf"),T("Native detritivorous flies (guild)")),"Wolf cannot live on flies");
        check(Biogeography.CanEat(T("Orb-weaving spiders (guild)"),T("Native detritivorous flies (guild)")),"Web spiders can catch flies");
        check(Biogeography.CanEat(T("Red-tailed hawk"),T("Deer mouse")),"Hawk can catch mouse");
        check(Biogeography.CanEat(T("Lion"),T("Plains zebra")),"Lion can catch zebra");
        check(!Biogeography.CanEat(T("Lion"),T("African savanna elephant")),"Adult elephant is outside routine lion prey envelope");
        check(!Biogeography.CanEat(T("Koala"),T("Scots pine")) && Biogeography.CanEat(T("Koala"),T("River red gum")),"Specialized koala food requirement");
        check(T("Arctic wolf").Rank==TaxonRank.Subspecies && T("Arctic wolf").ParentSpecies=="Canis lupus","Wolf taxonomy is explicit");
        check(T("Gray wolf").HistoricalBridgeAllowed && !T("Scots pine").HistoricalBridgeAllowed,"Historical interchange is species-specific, not every cold-tolerant species");
        check(!Biogeography.Suitable(T("Gray wolf"),new HabitatCell { Habitat=Habitat.Forest,ColdTenthsC=240,WarmTenthsC=280,AridityMilli=1800 },100),"Generic gray wolves do not inhabit hot year-round rainforest");
        var corridor=new World { Surface=new Surface { Cells=Enumerable.Range(0,5).Select(i=>new Cell { Id=i,AreaM2=1000000,Neighbors=Enumerable.Range(0,5).Where(j=>Math.Abs(i-j)==1).ToArray(),DistanceM=Enumerable.Repeat(50000,i==0||i==4?1:2).ToArray() }).ToArray() },ElevationM=new[]{100,0,-50,0,100}, Hydrology=new HydrologyResult { OceanConnected=new[]{false,false,true,false,false} },Ecology=new EcologyResult { Habitat=Enumerable.Range(0,5).Select(i=>new HabitatCell { Habitat=Habitat.Open,ColdTenthsC=i==2?-200:100,WarmTenthsC=220,AridityMilli=700 }).ToArray() } };
        check(!Biogeography.Reach(corridor,T("Gray wolf"),0,false,default)[4],"Present strait blocks wolf");
        check(Biogeography.Reach(corridor,T("Gray wolf"),0,true,default)[4],"Cold-tolerant wolf can cross exposed historical shelf");
        check(!Biogeography.Reach(corridor,T("Lion"),0,true,default)[4],"Lion blocked by same cold land bridge");
        corridor.ElevationM[2]=-1000;
        check(!Biogeography.Reach(corridor,T("Gray wolf"),0,true,default)[4],"Deep ocean cannot become land bridge");
        check(Ecology.PotentialEvaporationMm(0,30,0)>Ecology.PotentialEvaporationMm(0,10,0),"Heat increases evaporative demand");
        using(var canceled=new CancellationTokenSource()) { canceled.Cancel(); try { Ecology.BuildHabitats(corridor,canceled.Token); check(false,"Habitat cancellation"); } catch(OperationCanceledException) { check(true,"Habitat cancellation"); } }
        var r=Recipe.Preset("Preview"); r.Detail=5000;r.EcologyProfile=EarthFaunaPack.Version; var pipeline=new WorldPipeline();string abioticBefore="";
        var w=pipeline.Generate(r,preview:(stage,before)=>{if(stage=="hydrology")abioticBefore=StateHash.Compute(new object[]{before.ElevationM,before.Climate,before.Hydrology});});
        Validate(w,check);
        var oldContent=r.Clone();oldContent.EcologyProfile=NativeLifePack.Version;var previousContent=pipeline.Generate(oldContent);
        check(w.Ecology!.Region.SequenceEqual(previousContent.Ecology!.Region)&&w.Ecology.Province.SequenceEqual(previousContent.Ecology.Province)&&w.Ecology.Habitat.Select(h=>h.Biome).SequenceEqual(previousContent.Ecology.Habitat.Select(h=>h.Biome)),"Fauna-3 coastal/resource access changes neither biome classification nor province/ecoregion membership");
        check(StateHash.Compute(new object[]{w.ElevationM,w.Climate,w.Hydrology})==StateHash.Compute(new object[]{previousContent.ElevationM,previousContent.Climate,previousContent.Hydrology}),"Content profile changes do not modify accepted abiotic generation");
        check(previousContent.Ecology.Taxa.All(t=>t.Lineage==null)&&previousContent.Ecology.Regions.All(region=>region.Species.All(p=>p.Variant?.Ecotype==null)),"Explicit native-2 retains absent lineage/ecotype extensions");
        var legacy=r.Clone(); legacy.AlgorithmVersion=Recipe.TerrainControlsAlgorithm; var prior=pipeline.Generate(legacy);
        check(abioticBefore==StateHash.Compute(new object[]{w.ElevationM,w.Climate,w.Hydrology}),"Ecology never changes terrain, trained climate or water within the same algorithm");
        check(WorldFile.Read(WorldFile.Write(prior)).Ecology==null,"Pre-ecology snapshots retain their old hash and null ecology");
        check(StateHash.Compute(WorldFile.Read(WorldFile.Write(w)).Ecology)==StateHash.Compute(w.Ecology),"Complete ecology binary persistence");
        check(StateHash.Compute(WorldJson.ReadWorld(WorldJson.Write(w)).Ecology)==StateHash.Compute(w.Ecology),"Complete ecology JSON persistence");
        foreach(var again in new[]{pipeline.Reapply(r),pipeline.Generate(WorldJson.ReadRecipe(WorldJson.Write(r))),pipeline.Reconstruct(w)}) check(w.Stages.Select(s=>s.Hash).SequenceEqual(again.Stages.Select(s=>s.Hash)),"All ecology stages reconstruct identically");
        check(GlobeView.Describe(w,w.Ecology.Regions[0].RepresentativeCell).Contains("FOOD WEB"),"Shared inspector exposes food web");
        foreach(var layer in new[]{Layer.Ecoregions,Layer.Biomes,Layer.Floodplains,Layer.Soils,Layer.WaterTable,Layer.Aridity,Layer.Salinity,Layer.Permafrost,Layer.FirePotential})
            check(GlobeView.Build(w,layer).Colors.All(float.IsFinite),"Ecology layer finite: "+layer);
        foreach(string test in new[]{"Earth","one50","six30"})
        {
            var recipe=Recipe.Preset(test=="Earth"?"Earth":"Preview"); recipe.Detail=5000;recipe.EcologyProfile=EarthFaunaPack.Version;
            if(test!="Earth") { recipe.Seed=test=="one50"?73421:42; recipe.ContinentCount=test=="one50"?1:6; recipe.LandFraction=test=="one50"?.5:.3; }
            var world=pipeline.Generate(recipe); Validate(world,check);
            Console.WriteLine(test+": "+world.Ecology.Regions.Length+" ecoregions; "+world.Ecology.Regions.SelectMany(x=>x.Species).Select(x=>x.TaxonId).Distinct().Count()+" taxa present; "+world.Ecology.Regions.Sum(x=>x.FoodWeb.Length)+" food links.");
            if(test=="Earth")
            {
                int lion=world.Ecology.Taxa.Single(t=>t.Name=="Lion").Id;
                check(!world.Surface.Cells.Any(c=>c.Position.Z<.2 && c.Position.Z>-.9 && Math.Atan2(c.Position.Y,c.Position.X)<-.6 && Math.Atan2(c.Position.Y,c.Position.X)>-1.45 && Biogeography.Present(world.Ecology,c.Id,lion)),"Modern lions never appear in South America");
            }
        }
        var frozen="artifacts/player-3-27/WorldGen_Data/StreamingAssets/world/Earth.world";
        if(File.Exists(frozen)) check(WorldFile.Read(File.ReadAllBytes(frozen)).Ecology==null,"Actual prior-release Earth snapshot remains loadable");
    }
    static void Validate(World w,Action<bool,string> check)
    {
        var e=w.Ecology!;
        FaunaPackTests.Validate(w,check);
        check(e.Region.Length==w.Surface.Cells.Length && w.Surface.Cells.All(c=>w.Hydrology.OceanConnected[c.Id]?e.Region[c.Id]==-1:e.Region[c.Id]>=0),"Every non-ocean cell belongs to one ecoregion");
        check(e.Regions.Sum(r=>r.Cells.Length)==w.Surface.Cells.Count(c=>!w.Hydrology.OceanConnected[c.Id]),"No duplicate or missing regional membership");
        check(e.Regions.All(r=>r.Cells.All(i=>e.Region[i]==r.Id && e.Habitat[i].Biome==r.Biome)),"Ecoregions agree with habitat and membership");
        check(e.Regions.All(r=>r.Neighbors.All(j=>e.Regions[j].Neighbors.Contains(r.Id))),"Symmetric regional neighbors");
        check(e.Habitat.All(a=>a.FloodFractionPermille>=0 && a.FloodFractionPermille<=1000 && a.FloodDaysPerYear>=0 && a.FloodDaysPerYear<=365 && a.PermafrostPermille>=0 && a.PermafrostPermille<=1000),"Habitat proxies bounded");
        check(e.Regions.Length>w.Climate.Classification.Distinct().Count(),"Multiple ecoregions beyond climate classes");
        check(e.Regions.All(r=>r.FoodWeb.All(l=>Biogeography.CanEat(e.Taxa[l.ConsumerId],e.Taxa[l.ResourceId]) && r.Cells.Any(i=>Biogeography.Present(e,i,l.ConsumerId)&&Biogeography.Present(e,i,l.ResourceId)))),"Every trophic link has a compatible actual co-occurrence");
        check(w.Surface.Cells.All(c=>e.Taxa.All(t=>!Biogeography.Present(e,c.Id,t.Id) || Biogeography.Suitable(t,e.Habitat[c.Id],w.ElevationM[c.Id]))),"All retained taxa satisfy cell habitat constraints");
        check(w.Surface.Cells.All(c=>e.Taxa.All(t=>!Biogeography.Present(e,c.Id,t.Id) || t.Producer || t.BasalResource || e.Taxa.Any(p=>Biogeography.Present(e,c.Id,p.Id)&&Biogeography.CanEat(t,p)))),"No consumer without a local compatible food resource");
    }
}
