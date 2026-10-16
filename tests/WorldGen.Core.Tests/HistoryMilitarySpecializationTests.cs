using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryMilitarySpecializationTests
{
    public static void Run(Action<bool,string> check)
    {
        var plain=new HabitatCell{Habitat=Habitat.Open,Biome=Biome.Grassland,AridityMilli=650,GrowingDays=240,ReliefM=100,Soil=SoilKind.Loam};
        var forest=new HabitatCell{Habitat=Habitat.Forest,Biome=Biome.TemperateForest,AridityMilli=1000,GrowingDays=240,ReliefM=100,Soil=SoilKind.Loam};
        var marsh=new HabitatCell{Habitat=Habitat.Wetland,Biome=Biome.Marsh,AridityMilli=1100,GrowingDays=240,ReliefM=100,Soil=SoilKind.Alluvial,FloodFractionPermille=600};
        var mountain=new HabitatCell{Habitat=Habitat.Alpine,Biome=Biome.Alpine,AridityMilli=650,GrowingDays=150,ReliefM=1500};
        var drought=new HistoricalClimateAnomaly(0,500);var normal=new HistoricalClimateAnomaly(0,1000);var wet=new HistoricalClimateAnomaly(0,1400);
        double open=HistoricalMilitarySpecializationEffects.MountedTerrain(plain,normal,0),woods=HistoricalMilitarySpecializationEffects.MountedTerrain(forest,normal,0),swamp=HistoricalMilitarySpecializationEffects.MountedTerrain(marsh,wet,0);
        check(open>woods*4&&open>swamp*3,"Mounted maneuver and free grazing favor open plains rather than forest and waterlogged ground");
        check(HistoricalMilitarySpecializationEffects.MountedTerrain(mountain,normal,0)<open*.3,"A mounted advantage does not transfer unchanged into rugged mountains");
        check(HistoricalMilitarySpecializationEffects.MountedTerrain(marsh,wet,0)<HistoricalMilitarySpecializationEffects.MountedTerrain(marsh,normal,0),"A wet historical interval can impair cavalry operations without repainting the terrain");
        var dry=new HabitatCell{Habitat=Habitat.Open,Biome=Biome.Grassland,AridityMilli=250,GrowingDays=180,ReliefM=100};
        check(HistoricalMilitarySpecializationEffects.MountedTerrain(dry,drought,0)<HistoricalMilitarySpecializationEffects.MountedTerrain(dry,normal,0),"Drought limits grazing support even in otherwise open terrain");
        check(HistoricalMilitarySpecializationEffects.MountedTerrain(forest,normal,1)>woods&&HistoricalMilitarySpecializationEffects.MountedTerrain(forest,normal,1)<open,"Engineering eases difficult terrain but does not erase its military geography");
        check(HistoricalMilitarySpecializationEffects.MountedFieldMultiplier(.8,open,0)>1&&HistoricalMilitarySpecializationEffects.MountedFieldMultiplier(.8,woods,0)<1,"The same mounted specialization has real strengths and weaknesses instead of a universal power buff");
        check(HistoricalMilitarySpecializationEffects.MountedFieldMultiplier(0,open,0)==1&&HistoricalMilitarySpecializationEffects.MountedFieldMultiplier(.8,woods,1)==1,"No actual mounted forces, or replacement by motorized forces, removes the old cavalry specialization");
        check(HistoricalMilitarySpecializationEffects.FortifiedMountedMultiplier(.8,1,0,0)<HistoricalMilitarySpecializationEffects.FortifiedMountedMultiplier(.8,1,1,0),"Siege engineering reduces a mounted force's disadvantage against organized fortified centers");
        check(!HistoricalMilitarySpecializationEffects.PortSupply(false,true,true,1000,2000,1,1,true)&&!HistoricalMilitarySpecializationEffects.PortSupply(true,false,true,1000,2000,1,1,true),"A coastline somewhere in the empire does not provision arbitrary inland armies");
        check(!HistoricalMilitarySpecializationEffects.PortSupply(true,true,true,100,2000,1,1,true)&&!HistoricalMilitarySpecializationEffects.PortSupply(true,true,false,1000,2000,1,1,true),"An unsupported or unfed port does not grant maritime campaign reach");
        check(!HistoricalMilitarySpecializationEffects.PortSupply(true,true,true,1000,2000,1,.19,true)&&!HistoricalMilitarySpecializationEffects.PortSupply(true,true,true,1000,2000,1,1,false),"Knowledge without deployed navigation or an actual route is not a naval supply chain");
        check(HistoricalMilitarySpecializationEffects.PortSupply(true,true,true,1000,2000,.2,.2,true),"A real supported port and deployed craft can sustain a coastal supply advantage");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var engine=new Prehistory(world);var source=engine.UpgradeToLivingWorldHistory(world.Prehistory);string oldHash=source.ComputeHash();
        var s=source.Copy();int culture=0;
        var horse=world.Ecology.Taxa.First(t=>AnimalEcotypes.RidingSuitability(t)>=800);
        int cell=Enumerable.Range(0,world.Surface.Cells.Length).First(i=>engine.CellPatch[i]>=0&&Biogeography.Suitable(horse,world.Ecology.Habitat[i],world.ElevationM[i]));
        var method=typeof(Prehistory).GetMethod("MilitaryRidingPractice",BindingFlags.Instance|BindingFlags.NonPublic)!;
        double Practice()=>(double)method.Invoke(engine,new object[]{s,culture,cell})!;
        s.History.Agriculture[culture].Animals=Array.Empty<HistoricalAnimalHusbandry>();check(Practice()==0,"A nearby wild riding animal is not an already trained cavalry tradition");
        var managed=new HistoricalAnimalHusbandry{TaxonId=horse.Id,OriginCell=cell,Practice=900,AdoptedYear=0,Name=horse.Name};s.History.Agriculture[culture].Animals=new[]{managed};
        check(Practice()==0,"Unfinished management trials do not provide mounted armies");
        managed.AdoptedYear=s.Years;double newPractice=Practice();managed.AdoptedYear=s.Years-200;double mature=Practice();
        check(mature>newPractice*3&&mature<=1,"Real locally viable domestication and riding practice mature rather than appearing at full strength on discovery day");
        var bovine=world.Ecology.Taxa.First(t=>t.Traits?.Traction==true&&AnimalEcotypes.RidingSuitability(t)==0);
        managed.TaxonId=bovine.Id;check(Practice()==0,"Traction cattle and buffalo are not automatically converted into cavalry");
        check(source.ComputeHash()==oldHash,"Military specialization queries do not mutate the input checkpoint");
    }
}
