using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class GroundFloraTests
{
    public static void Run(string path)
    {
        int checks=0;void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
        var recipe=Recipe.Preset("Preview");recipe.Detail=5000;
        var world=new World{Recipe=recipe,Ecology=new EcologyResult{Regions=new[]{new Ecoregion{ProvinceId=3},new Ecoregion{ProvinceId=3},new Ecoregion{ProvinceId=9}}}};
        var flora=GroundFlora.For(world);var a=flora.Region(0);var b=flora.Region(1);var c=flora.Region(2);
        var other=new World{Recipe=recipe.Clone(),Ecology=world.Ecology};other.Recipe.Seed++;
        for(int f=0;f<9;f++)
        {
            Check(a.Species[f].Name==GroundFlora.For(world).Region(0).Species[f].Name,"Stable generated species");
            Check(a.Species[f].Ancestor==b.Species[f].Ancestor,"Province shares actual ancestry");
            Check(a.Species[f].Stature!=b.Species[f].Stature,"Regional branching changes phenotype");
            Check(a.Species[f].Name!=GroundFlora.For(other).Region(0).Species[f].Name,"Different world seed changes flora");
            Check(Math.Abs(a.Species[f].Stature-b.Species[f].Stature)<.5,"Regional divergence stays bounded");
        }
        var soil=new GroundProfile{DepthM=1,Fertility=.7,Drainage=.6,WaterCapacityMm=100,Organic=.2};
        HabitatCell Env(Biome biome,double wet,double warm=22,int waterTable=400)=>new HabitatCell{Biome=biome,AridityMilli=(int)(wet*1000),WarmTenthsC=(int)(warm*10),WaterTableCm=waterTable};
        var meadow=GroundFlora.Describe(Env(Biome.Grassland,1),soil,a);
        var desert=GroundFlora.Describe(Env(Biome.Desert,.06,35),soil,a);
        var forest=GroundFlora.Describe(Env(Biome.TemperateRainforest,2),soil,a);
        var marsh=GroundFlora.Describe(Env(Biome.Marsh,1,20,0),soil,a);
        Check(meadow.Coverage>desert.Coverage*3,"Meadow vs sparse arid ground");
        Check(forest.Herbs.Z>meadow.Herbs.Z&&forest.Mats.X>meadow.Mats.X,"Shade/wetness favors ferns and moss");
        Check(marsh.Herbs.X>meadow.Herbs.X,"Sedges/rushes favor wet ground");
        Check(GroundFlora.Describe(Env(Biome.Ocean,1),soil,a).Coverage==0,"No terrestrial ocean cover");
        Check(GroundFlora.Describe(Env(Biome.Ice,1),soil,a).Coverage==0,"No permanent ice cover");
        var p=TerrainMapViewport.FromDegrees(30,45);
        Check(GroundFlora.Surface(meadow,p,7,637100,soil,100,100,1,true,0).Coverage==0,"Submerged plants excluded");
        Check(GroundFlora.Surface(meadow,p,7,637100,soil,100,100,.2,false,0).Coverage==0,"Cliff face exclusion");
        for(double wet=0;wet<3;wet+=.1)for(double warm=-10;warm<45;warm+=3)
        {
            var profile=GroundFlora.Describe(Env(Biome.Grassland,wet,warm),soil,a);
            Check(profile.Coverage>=0&&profile.Coverage<=.950001,"Bounded joint cover, not nine full overlapping lawns");
            for(int f=0;f<9;f++)Check(profile.Weight(f)>=0&&!double.IsNaN(profile.Weight(f)),"Finite nonnegative growth form");
        }
        var testMesh=new LocalTerrainMesh{Segments=4,SizeM=4,GroundVertices=25,GroundCover=Enumerable.Repeat(1f,25*16).ToArray(),
            WaterPositions=new[]{1.45f,1,0,1.55f,1,0,1.45f,1,4,1.55f,1,4},WaterTriangles=new[]{0,2,1,1,2,3}};
        GroundCoverGrid.ExcludeWater(testMesh);
        Check(testMesh.GroundCover[(2*5+1)*16+3]==0&&testMesh.GroundCover[(2*5+2)*16+3]==0,"Sub-grid stream excludes roots on both sides");
        Check(testMesh.GroundCover[(2*5+4)*16+3]==1,"Water bounding doesn't erase unrelated ground");
        if(path!=null)
        {
            world=WorldFile.Read(File.ReadAllBytes(path));string hash=StateHash.Compute(world);
            var watch=System.Diagnostics.Stopwatch.StartNew();long before=GC.GetTotalMemory(true);
            var terrain=new LocalTerrain(world,horizontalScale:.1);
            Console.WriteLine($"Actual {world.Surface.Cells.Length} cells prep incl terrain+flora {watch.Elapsed.TotalSeconds:F2}s; retained delta {(GC.GetTotalMemory(true)-before)/1048576d:F1}MiB");
            var ecologicalCopy=new World{Recipe=world.Recipe,Ecology=world.Ecology};var coldFlora=GroundFlora.For(ecologicalCopy);
            watch.Restart();double total=0;
            for(int i=0;i<world.Surface.Cells.Length;i++)total+=coldFlora.Cell(i,terrain.Profile(i).Ground).Coverage;
            Console.WriteLine($"Flora-only cold catalog+{world.Surface.Cells.Length} profiles {watch.Elapsed.TotalMilliseconds:F1}ms; mean potential cover {total/world.Surface.Cells.Length:F3}");
            int cell=Enumerable.Range(0,world.ElevationM.Length).Where(i=>world.ElevationM[i]>30&&world.ElevationM[i]<1200)
                .OrderByDescending(i=>terrain.Profile(i).Cover.Coverage).First();
            var at=world.Surface.Cells[cell].Position;var patch=terrain.Patch(at,10000);var overlap=terrain.Patch(patch.Position(100,50),10000);
            for(int i=0;i<100;i++)
            {
                var point=patch.Position(i*.7,i*.3);var s=patch.Sample(point);var t=overlap.Sample(point);
                Check(s.Cover.Coverage==t.Cover.Coverage&&s.Cover.Color.X==t.Cover.Color.X,"Exact independent patch flora");
            }
            watch.Restart();var frame=new LocalTerrainFrame(at,terrain.RadiusM,patch.Sample(at).ElevationM,.1);
            var mesh=LocalTerrainMesh.Build(terrain,frame,0,0,102.4,64);
            Check(mesh.GroundCover.Length==mesh.GroundVertices*16,"One bounded flora grid, no saved fine vegetation raster");
            using var memory=new MemoryStream();using(var writer=new BinaryWriter(memory,System.Text.Encoding.UTF8,true))TerrainMeshWire.WriteMesh(writer,mesh);
            memory.Position=0;using var reader=new BinaryReader(memory);var roundtrip=TerrainMeshWire.ReadMesh(reader);
            Check(mesh.GroundCover.SequenceEqual(roundtrip.GroundCover),"Exact flora worker transport");
            Check(mesh.Colors.Any(v=>v>0),"Terrain receives cover color");
            Check(hash==StateHash.Compute(world),"Authoritative snapshot unchanged");
            Console.WriteLine($"Actual tile {watch.Elapsed.TotalMilliseconds:F1}ms; cover grid {mesh.GroundCover.Length*4/1024}KiB; cell {cell}; coverage {terrain.Profile(cell).Cover.Coverage:F3}; source {hash}");
            watch.Restart();var warmed=LocalTerrainMesh.Build(terrain,frame,0,0,102.4,64);
            Console.WriteLine($"Repeated fine tile with warmed amplification {watch.Elapsed.TotalMilliseconds:F1}ms");
            Check(warmed.GroundCover.SequenceEqual(mesh.GroundCover),"Repeat same flora/ground grid");
        }
        Console.WriteLine($"PASS {checks} ground-flora checks");
    }
}
