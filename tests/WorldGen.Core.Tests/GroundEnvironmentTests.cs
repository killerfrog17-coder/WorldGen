using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Presentation;

static class GroundEnvironmentTests
{
    public static void Run(Action<bool,string> check)
    {
        var recipe=Recipe.Preset("Preview");recipe.Detail=5000;var mesh=SphericalMesh.Build(recipe,default);int n=mesh.Cells.Length;
        var habitats=Enumerable.Range(0,n).Select(i=>new HabitatCell{Biome=Biome.Desert,Soil=SoilKind.Sand,MeanTenthsC=220,WarmTenthsC=350,ColdTenthsC=90,AridityMilli=150,ReliefM=80,WaterTableCm=1000,SoilFertilityPermille=400}).ToArray();
        var world=new World{Recipe=recipe,Surface=mesh,ElevationM=Enumerable.Repeat(100,n).ToArray(),Hydrology=new HydrologyResult{OceanConnected=new bool[n]},Ecology=new EcologyResult{Habitat=habitats},BasinPermille=Enumerable.Repeat(700,n).ToArray()};
        var env=GroundEnvironment.For(world);var rocks=new HashSet<RockFamily>();var types=new HashSet<GroundMaterial>();
        for(int i=0;i<n;i+=7)
        {
            var g=env.Cell(i);rocks.Add(g.Bedrock);var s=GroundEnvironment.Surface(g,mesh.Cells[i].Position,recipe.Seed,100,1,0,0,637100);
            types.Add(s.Material);
            check(Math.Abs(g.Sand+g.Silt+g.Clay-1)<.0002&&g.Sand>=0&&g.Silt>=0&&g.Clay>=0,"Mineral particle fractions stay nonnegative and sum to one");
            check(g.PH>=4&&g.PH<=9&&g.WaterCapacityMm>0&&g.DepthM>0&&g.Fertility>=0&&g.Fertility<=1,"Ground physical/chemical proxies have bounded explicit units");
            check(Math.Abs(Enumerable.Range(0,8).Sum(s.Weight)-1)<1e-10&&Enumerable.Range(0,8).All(k=>s.Weight(k)>=0),"Eight material weights form a nonnegative normalized mixture");
            check(env.Cell(i).WaterCapacityMm==g.WaterCapacityMm,"Ground queries reconstruct without random-state mutation");
        }
        check(rocks.Count>=4&&types.Count>=2,"One desert climate supports different geological families and surface facies");
        var rich=new GroundProfile{Sand=.35,Silt=.45,Clay=.20,DepthM=1.1,PH=6.5,Fertility=.8,Drainage=.65,WaterCapacityMm=170,SoilColor=new V(.4,.3,.2),SandColor=new V(.7,.6,.4),RockColor=new V(.4,.4,.4)};
        var thin=rich;thin.DepthM=.06;thin.Gravel=.6;var infertile=rich;infertile.Fertility=.1;var acid=rich;acid.PH=4;var dry=rich;dry.WaterCapacityMm=20;
        check(rich.FarmFit(0)>thin.FarmFit(0)*4&&rich.FarmFit(0)>acid.FarmFit(0)&&rich.FarmFit(0)>dry.FarmFit(0),"Thin stony, acidic and low-storage soils constrain cultivation independently of the climate");
        check(rich.FarmFit(0)>infertile.FarmFit(0),"Intrinsic nutrient supply affects farming rather than only rendering");
        var wet=rich;wet.Drainage=.1;check(wet.FarmFit(2)>wet.FarmFit(0),"Poor aeration affects wetland grain differently from dryland crops");
        var sand=rich;sand.Sand=.94;sand.Silt=.04;sand.Clay=.02;sand.Gravel=.01;
        var gravel=sand;gravel.Gravel=.65;
        var point=mesh.Cells[17].Position;
        var beachSand=GroundEnvironment.Surface(sand,point,7,1,1,.5,0,637100);var beachGravel=GroundEnvironment.Surface(gravel,point,7,1,1,.5,0,637100);
        check(beachSand.Sand>.7&&beachGravel.Gravel>beachGravel.Sand,"Shore sediment admits both sandy and gravel beaches");
        var cliff=GroundEnvironment.Surface(rich,point,7,150,.25,0,0,637100);check(cliff.Rock>.8,"Steep exposed faces use parent rock, not a biome-colored dirt slab");
        var terrain=new LocalTerrain(world,horizontalScale:.1);var a=terrain.Patch(point,6000);var frame=new LocalTerrainFrame(point,terrain.RadiusM,100,.1);var b=terrain.Patch(frame.Position(100,0),6000);
        foreach(int x in new[]{0,50,100,150})
        {
            var p=frame.Position(x,100);var ga=a.Sample(p).Ground;var gb=b.Sample(p).Ground;
            check(ga.SoilColor.X==gb.SoilColor.X&&ga.Sand==gb.Sand&&ga.PH==gb.PH,"Independent chunks agree on blended ground without nearest-cell switching");
        }
        var centre=a.Sample(point);var near=a.Sample(frame.Position(.01,0));check(Math.Abs(centre.Ground.Sand-near.Ground.Sand)<.0001,"Crossing tiny distances cannot flip the soil province");
        var watch=Stopwatch.StartNew();double checksum=0;for(int i=0;i<10000;i++)checksum+=a.Sample(frame.Position(i%100,i%37)).Ground.PH;
        Console.WriteLine("Ground/height query mean "+(watch.Elapsed.TotalMilliseconds/10000).ToString("F4")+"ms, checksum "+checksum.ToString("F2"));
        foreach(var gait in new[]{TravelGait.Walk,TravelGait.Jog,TravelGait.Sprint})
        {
            int Count(int fps){var cadence=new FootstepCadence();int count=0;double speed=gait==TravelGait.Walk?TerrainMotion.WalkSpeedMps:gait==TravelGait.Jog?TerrainMotion.JogSpeedMps:TerrainMotion.RunSpeedMps;for(int i=0;i<fps*10;i++)if(cadence.Advance(speed/fps,true,gait))count++;return count;}
            check(Math.Abs(Count(30)-Count(120))<=1,"Footfalls depend on actual distance, not frame rate or a repeating timer");
        }
        var steps=new FootstepCadence();check(!steps.Advance(0,true,TravelGait.Jog)&&!steps.Advance(2,false,TravelGait.Sprint)&&!steps.Advance(100,true,TravelGait.Jog),"Stationary, airborne and teleport motion never create footsteps");
        var interrupted=new FootstepCadence();int interruptedContacts=0;
        for(int i=0;i<100;i++){if(interrupted.Advance(.02,true,TravelGait.Jog))interruptedContacts++;check(!interrupted.Advance(.02,false,TravelGait.Jog),"Airborne gaps remain silent without erasing the preceding grounded stride");}
        check(interruptedContacts>=2,"Intermittent ground contact cannot suppress an entire walk's footsteps");
        check(FootstepCadence.Gain(TravelGait.Walk)<FootstepCadence.Gain(TravelGait.Jog)&&FootstepCadence.Gain(TravelGait.Jog)<FootstepCadence.Gain(TravelGait.Sprint),"Walk, jog and sprint have ordered restrained impact levels");
        foreach(double slope in new[]{-.27,.27})foreach(var gait in new[]{TravelGait.Walk,TravelGait.Jog,TravelGait.Sprint})
        {
            double Ground(double x,double z)=>Math.Round(slope*z*500)/500;var counts=new List<int>();
            foreach(int fps in new[]{60,10000})
            {
                var motion=new TerrainMotion();motion.Place(0,0,0);var cadence=new FootstepCadence();int contacts=0,air=0;
                for(int i=0;i<fps*3;i++){double z=motion.Z;motion.Move(1d/fps,1,0,0,0,0,gait==TravelGait.Sprint,false,Ground,gait==TravelGait.Walk);if(!motion.Grounded)air++;if(cadence.Advance((motion.Z-z)*Math.Sqrt(1+slope*slope),motion.Grounded,gait))contacts++;}
                check(air==0&&contacts>0,"Millimetre ground quantization cannot create false flight or silence footsteps, even at10000 FPS");counts.Add(contacts);
                motion.Place(0,0,0);motion.Move(1d/fps,1,0,0,0,0,false,false,(x,z)=>z*3);check(motion.Z==0,"Fixed-span grade checks prevent high-FPS creeping up steep slopes");
            }
            check(Math.Abs(counts[0]-counts[1])<=1,"Actual grounded movement/footstep counts agree between ordinary and extreme frame rates");
        }
        Agronomy(check);
    }
    public static void World(string input,string output,Action<bool,string> check)
    {
        Directory.CreateDirectory(output);var world=WorldGen.Serialization.WorldFile.Read(File.ReadAllBytes(input));var engine=new Prehistory(world);
        var current=engine.UpgradeRealmLifecycles(world.Prehistory);var preceding=current.Copy();preceding.History.Aggregates.Empires.Connections.Realms.Version=37;preceding.Seal();
        foreach(var pair in new[]{("soil38",current),("preceding37",preceding)})
        {
            world.Prehistory=pair.Item2;File.WriteAllBytes(Path.Combine(output,pair.Item1+".world"),WorldGen.Serialization.WorldFile.Write(world));
            var expected=engine.AdvanceYears(pair.Item2,12);engine.Validate(expected);
            var cold=WorldGen.Serialization.WorldFile.Read(File.ReadAllBytes(Path.Combine(output,pair.Item1+".world")));var resumed=new Prehistory(cold).AdvanceYears(cold.Prehistory,12);
            check(expected.Hash==resumed.Hash,"Actual actor world keeps exact cold continuation under "+pair.Item1);
            File.WriteAllText(Path.Combine(output,pair.Item1+".expected.txt"),expected.Hash);
            HistoryAgronomicFitCacheTests.World(Path.Combine(output,pair.Item1+".world"),check);
        }
    }
    static void Agronomy(Action<bool,string> check)
    {
        var recipe=Recipe.Preset("Preview");recipe.Detail=5000;var world=new WorldPipeline().Generate(recipe);string hash=StateHash.Compute(world);
        var sim=new Prehistory(world);var fresh=sim.Prepare();var old=fresh.Copy();old.History.Aggregates.Empires.Connections.Realms.Version=37;old.Seal();
        check(fresh.History.Aggregates.Empires.Connections.Realms.Version==38,"Fresh history explicitly opts into ground agronomy revision38");
        var cells=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>!world.Hydrology.OceanConnected[i]&&HistoricalCrops.Suitability(world.Ecology.Habitat[i],0)>.05).Take(40).ToArray();
        var method=typeof(Prehistory).GetMethod("CommunityTransferFit",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        bool different=false;
        foreach(int cell in cells)
        {
            check(sim.GroundCropConstraint(old,cell,0)==1,"Previous37 saves keep literal pre-ground rules");
            var definition=HistoricalCropCatalog.Crops[0];var crop=new HistoricalCultivar{CropId=0,MaturityDays=definition.MaturityDays,DaylengthLatitudeTenths=240,WaterRequirementPermille=definition.MinimumWaterPermille,MinimumWarmTenthsC=definition.MinimumWarmC*10,SelectedYears=1000};
            double Fit(PrehistoryState state)=>(double)method.Invoke(sim,new object[]{state,cell,crop,false})!;
            double before=Fit(old),after=Fit(fresh);different|=before!=after;
            check(Fit(old)==before&&Fit(fresh)==after,"Old and new soil rules cannot contaminate shared habitat/cultivar memo keys");
            sim.UseAgronomicFitCache=false;check(Fit(fresh)==after&&Fit(old)==before,"Ground-aware cached and literal actual cultivar fits agree");sim.UseAgronomicFitCache=true;
        }
        check(different,"Ground changes actual cultivar/food fits, not just a decorative fertility label");
        world.Prehistory=sim.Start(fresh);var loaded=WorldGen.Serialization.WorldFile.Read(WorldGen.Serialization.WorldFile.Write(world));
        var next=new Prehistory(loaded).AdvanceYears(loaded.Prehistory,10);var expected=sim.AdvanceYears(world.Prehistory,10);
        check(next.Hash==expected.Hash,"Revision38 cold save/load continues exactly without serializing a fine soil map");
        world.Prehistory=null;check(StateHash.Compute(world)==hash,"Ground/history queries preserve all accepted geography/ecology stage data");
    }
}
