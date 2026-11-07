using WorldGen.Core;
using System.Text.Json;

static class TreeLineageSnapshotTests
{
    public static World Fixture(Biome biome,int seed)
    {
        var recipe=Recipe.Preset("Preview");recipe.Seed=seed;
        return new World{Recipe=recipe,Ecology=new EcologyResult{
            Regions=new[]{new Ecoregion{Id=0,RepresentativeCell=0,ProvinceId=3,Biome=biome},new Ecoregion{Id=1,RepresentativeCell=0,ProvinceId=3,Biome=biome},new Ecoregion{Id=2,RepresentativeCell=0,ProvinceId=8,Biome=biome}},
            Habitat=new[]{new HabitatCell{Biome=biome,AridityMilli=biome==Biome.Desert?120:biome==Biome.Savanna?650:1400,WarmTenthsC=biome==Biome.BorealForest?180:300,ColdTenthsC=biome==Biome.BorealForest?-160:180}}}};
    }
    public static void Run(string output,string baseline=null)
    {
        var records=new SortedDictionary<string,string>();
        foreach(int seed in new[]{719,2311,61843})
        foreach(var biome in new[]{Biome.TropicalRainforest,Biome.TemperateForest,Biome.BorealForest,Biome.Savanna,Biome.Desert,Biome.Marsh,Biome.Peatland,Biome.MediterraneanWoodland})
        {
            var world=Fixture(biome,seed);
            for(int region=0;region<3;region++)records.Add(seed+"/"+biome+"/"+region,StateHash.Compute(TreeFlora.Create(world,region,region==2?8:3)));
        }
        if(baseline!=null)
        {
            var expected=JsonSerializer.Deserialize<SortedDictionary<string,string>>(File.ReadAllText(baseline));
            if(!records.SequenceEqual(expected))throw new Exception("An inland lineage changed outside the intended coastal mangrove update");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        File.WriteAllText(output,JsonSerializer.Serialize(records,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {records.Count} complete inland species/anatomy lineage snapshots"+(baseline==null?" captured":" EXACT baseline"));
    }
}
