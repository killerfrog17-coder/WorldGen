using WorldGen.Core;
using WorldGen.Presentation;

static class TreeFieldTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        var recipe=Recipe.Preset("Preview");recipe.Detail=5000;var surface=SphericalMesh.Build(recipe,default);int count=surface.Cells.Length;
        World Fixture(Biome biome)=>new World{Recipe=recipe.Clone(),Surface=surface,ElevationM=Enumerable.Repeat(250,count).ToArray(),Hydrology=new HydrologyResult{OceanConnected=new bool[count]},
            Ecology=new EcologyResult{Habitat=Enumerable.Range(0,count).Select(_=>new HabitatCell{Biome=biome,Soil=SoilKind.Loam,SoilFertilityPermille=800,AridityMilli=1300,WarmTenthsC=250,ColdTenthsC=80,GrowingDays=260}).ToArray()}};
        var world=Fixture(Biome.TemperateRainforest);string before=StateHash.Compute(world);var terrain=new LocalTerrain(world);var scatter=new LocalScatter(terrain,.1);var point=new V(1,0,0);
        var key=scatter.Address(point);var first=scatter.Build(key);var repeat=scatter.Build(key);
        var trees=first.Instances.Where(p=>p.Kind==ScatterKind.GeneratedTree).ToArray();
        Check(trees.Length>100,"Productive forest fixture must have substantially more than sparse decorative trees");
        Check(first.Instances.Select(p=>p.Id).Distinct().Count()==first.Instances.Length,"Unique IDs across denser tree and other scatter channels");
        Check(first.Instances.Select(p=>(p.Id,p.Kind,p.Position,p.WidthM,p.HeightM)).SequenceEqual(repeat.Instances.Select(p=>(p.Id,p.Kind,p.Position,p.WidthM,p.HeightM))),"Revisit reproduces placements and size cohorts exactly");
        Check(trees.All(p=>double.IsFinite(p.HeightM)&&p.HeightM>0&&p.WidthM>0),"Tree physical dimensions remain finite and positive");
        foreach(var biome in new[]{Biome.Ocean,Biome.Ice,Biome.Tundra})
        {
            var barren=Fixture(biome);if(biome==Biome.Ocean){Array.Fill(barren.ElevationM,-100);Array.Fill(barren.Hydrology.OceanConnected,true);}
            var plants=new LocalScatter(new LocalTerrain(barren),.1);Check(!plants.Build(plants.Address(point)).Instances.Any(p=>p.Kind==ScatterKind.GeneratedTree),"No tree field in "+biome);
        }
        for(int i=0;i<64;i++)
        {
            var at=TerrainMapViewport.FromDegrees(-60+i*1.8,-140+i*4);double density=TreeFlora.Stand(at,recipe.Seed,terrain.RadiusM*.1,0);
            Check(density>=0&&density<=1,"Bounded stand density");
            var moved=(at+V.Tangent(at,new V(0,0,1))/(terrain.RadiusM*.1*100)).Unit;
            Check(Math.Abs(density-TreeFlora.Stand(moved,recipe.Seed,terrain.RadiusM*.1,0))<.01,"Stand field has no hard jump over a centimetre");
        }
        var frame=new LocalTerrainFrame(point,terrain.RadiusM,250,.1);var mesh=LocalTerrainMesh.Build(terrain,frame,0,0,102.4,8);
        // Stitch/skirt vertices can exceed the grid count. The rejected forest
        // crowns are specifically marked by colour alpha -3.
        Check(!Enumerable.Range(0,mesh.Colors.Length/4).Any(i=>Math.Abs(mesh.Colors[i*4+3]+3)<1e-6),"No unrelated oval canopy vertices embedded in terrain");
        Check(mesh.ForestCover.Length==mesh.GroundVertices*8,"Forest sidecar remains the same eight-word stride");
        using(var stream=new MemoryStream())
        {
            using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))TerrainMeshWire.WriteMesh(writer,mesh);stream.Position=0;
            using var reader=new BinaryReader(stream,System.Text.Encoding.UTF8,true);var restored=TerrainMeshWire.ReadMesh(reader);
            Check(mesh.ForestCover.Select(BitConverter.SingleToInt32Bits).SequenceEqual(restored.ForestCover.Select(BitConverter.SingleToInt32Bits)),"Discrete anatomy words survive worker transport exactly");
        }
        for(int i=7;i<mesh.ForestCover.Length;i+=8)Check(((BitConverter.SingleToInt32Bits(mesh.ForestCover[i])>>28)&7)<=5,"Valid representative structural family");
        Check(before==StateHash.Compute(world),"Derived forest changes do not mutate source ecology, heights, water or history");
        double nominal=2*terrain.RadiusM*.1/scatter.Divisions(false);
        Console.WriteLine($"PASS {checks} tree field assertions; face-centre fixture {trees.Length} trees / ~{nominal*nominal/10000:F2}ha; source unchanged");
    }
}
