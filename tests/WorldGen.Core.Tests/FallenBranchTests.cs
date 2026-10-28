using WorldGen.Core;

static class FallenBranchTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
        var recipe=Recipe.Preset("Preview");recipe.Detail=1500;var surface=SphericalMesh.Build(recipe,default);int count=surface.Cells.Length;
        World Fixture(Biome biome)=>new World{Recipe=recipe.Clone(),Surface=surface,ElevationM=Enumerable.Repeat(250,count).ToArray(),Hydrology=new HydrologyResult{OceanConnected=new bool[count]},
            Ecology=new EcologyResult{Region=new int[count],Regions=new[]{new Ecoregion{Id=0,RepresentativeCell=0,Biome=biome}},Habitat=Enumerable.Range(0,count).Select(_=>new HabitatCell{Biome=biome,Soil=SoilKind.Loam,SoilFertilityPermille=800,AridityMilli=1300,WarmTenthsC=270,ColdTenthsC=100,ProductivityGCM2Year=900,GrowingDays=280}).ToArray()}};
        var world=Fixture(Biome.TemperateForest);string before=StateHash.Compute(world);var terrain=new LocalTerrain(world,horizontalScale:.1);var scatter=new LocalScatter(terrain,.1);var centre=scatter.Address(new V(1,0,0),true);
        int total=0,treeDerived=0;var ids=new HashSet<ulong>();
        for(int y=-2;y<=2;y++)for(int x=-2;x<=2;x++)
        {
            var key=new ScatterKey(centre.Face,centre.X+x,centre.Y+y,true);var first=scatter.Build(key);var branches=first.Instances.Where(p=>p.Kind==ScatterKind.Branch).ToArray();total+=branches.Length;
            var patch=terrain.Patch(scatter.Position(key),110/.1);
            foreach(var branch in branches)
            {
                Check(ids.Add(branch.Id),"Branch identities unique across adjacent tiles");
                if(((branch.Id>>16)&15)!=4)continue;treeDerived++;
                Check(scatter.TryFallenBranchParent(key,(int)(branch.Id&65535),patch,out var parent)&&parent.Kind==ScatterKind.GeneratedTree,"Tree-derived branch has an admitted tree parent");
                Check((parent.Position-branch.Position).Length*terrain.RadiusM*.1<=6.251,"Branch lies under/near the actual parent tree");
            }
            if(x==0&&y==0)
            {
                var repeat=scatter.Build(key).Instances.Where(p=>p.Kind==ScatterKind.Branch).ToArray();
                Check(branches.Select(p=>(p.Id,p.Position,p.WidthM,p.HeightM,p.Variant)).SequenceEqual(repeat.Select(p=>(p.Id,p.Position,p.WidthM,p.HeightM,p.Variant))),"Streaming revisit retains branch identities, positions and traits");
            }
        }
        Check(total>25*8,"Wooded forest supply substantially denser than sparse habitat-only drops");
        double fraction=treeDerived/(double)total;Check(fraction>.9&&fraction<.99,"Approximately95% tree-derived, with small woodland background");
        Check(StateHash.Compute(world)==before,"Branch derivation never changes the saved world");
        var desert=new LocalScatter(new LocalTerrain(Fixture(Biome.Desert),horizontalScale:.1),.1);Check(!desert.Build(desert.Address(new V(1,0,0),true)).Instances.Any(p=>p.Kind==ScatterKind.Branch),"No woody litter in treeless desert");
        Console.WriteLine($"PASS {checks} fallen-branch checks; {total} branches, {treeDerived} tree-derived ({fraction:P1}) across25forest tiles");
    }
}
