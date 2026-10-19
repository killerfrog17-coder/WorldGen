using WorldGen.Core;
using WorldGen.Presentation;

static class LocalScatterTests
{
    public static void Run(Action<bool,string> check)
    {
        var recipe=Recipe.Preset("Preview");recipe.Detail=5000;var surface=SphericalMesh.Build(recipe,default);int n=surface.Cells.Length;
        World Fixture(Biome biome,int warmth=240)=>new World{Recipe=recipe.Clone(),Surface=surface,ElevationM=Enumerable.Repeat(250,n).ToArray(),Hydrology=new HydrologyResult{OceanConnected=new bool[n]},
            Ecology=new EcologyResult{Habitat=Enumerable.Range(0,n).Select(_=>new HabitatCell{Biome=biome,Soil=SoilKind.Loam,SoilFertilityPermille=650,AridityMilli=1000,WarmTenthsC=warmth,GrowingDays=200}).ToArray()}};
        var forestWorld=Fixture(Biome.TemperateForest);string before=StateHash.Compute(forestWorld);var forest=new LocalScatter(new LocalTerrain(forestWorld));V point=TerrainMapViewport.FromDegrees(40,-80);
        var buried=new GroundProfile{DepthM=2,Gravel=.02,Sand=.9,Bedrock=RockFamily.Sandstone,RockMixA=new V(0,0,1)};
        var exposed=new GroundProfile{DepthM=.08,Gravel=.5,Sand=.4,Bedrock=RockFamily.Granite,RockMixA=new V(1,0,0)};
        var rockDensities=new List<double>();int exceptional=0;
        for(int i=0;i<800;i++)
        {
            var p=TerrainMapViewport.FromDegrees(-70+i%140,-180+i*.45);
            var bare=LocalGeology.Describe(exposed,p,recipe.Seed,recipe.RadiusKm*1000);
            var cover=LocalGeology.Describe(buried,p,recipe.Seed,recipe.RadiusKm*1000);
            check(cover.LooseRock==0&&cover.Outcrop==0,"Deep stone-free deposits can remain genuinely free of boulders/outcrops");
            check(bare.LooseRock>=0&&bare.LooseRock<=1&&bare.Exposure>=0&&bare.Exposure<=1,"Geology values are bounded physical cover fractions");
            rockDensities.Add(bare.LooseRock);if(bare.Outcrop>.01)exceptional++;
            var s=LocalGeology.Surface(exposed,p,recipe.Seed,250,.8,0,0,recipe.RadiusKm*1000);
            check(Math.Abs(Enumerable.Range(0,8).Sum(s.Weight)-1)<1e-10,"Exposed lithology preserves normalized soil/material weights");
        }
        check(rockDensities.Max()-rockDensities.Min()>.1,"Local noise creates rock-poor and rock-rich areas in the same parent geology");
        check(exceptional>0&&exceptional<200,"Exceptional outcrops are possible but regional, not ubiquitous");
        var key=forest.Address(point);var a=forest.Build(key);var b=forest.Build(key);var cold=new LocalScatter(new LocalTerrain(forestWorld)).Build(key);
        string Describe(ScatterInstance p)=>string.Join("|",p.Id,p.Kind,p.Variant,p.Position.X,p.Position.Y,p.Position.Z,p.GroundM,p.HeightM,p.WidthM,p.YawDegrees);
        check(a.Instances.Length>30,"A forest fixture contains enough physical objects to provide scale");
        check(a.Instances.Select(Describe).SequenceEqual(b.Instances.Select(Describe))&&a.Instances.Select(Describe).SequenceEqual(cold.Instances.Select(Describe)),"Revisit/cold reconstruction retain exact identities, sizes and positions");
        check(a.Instances.Select(p=>p.Id).Distinct().Count()==a.Instances.Length,"No duplicate identities within a scatter patch");
        var seaWorld=Fixture(Biome.Ocean);Array.Fill(seaWorld.ElevationM,-200);Array.Fill(seaWorld.Hydrology.OceanConnected,true);var sea=new LocalScatter(new LocalTerrain(seaWorld));
        check(sea.Build(sea.Address(point)).Instances.Length==0,"Ocean beds do not receive terrestrial trees or grass");
        var ice=new LocalScatter(new LocalTerrain(Fixture(Biome.Ice)));
        check(ice.Build(ice.Address(point)).Instances.All(p=>p.Kind==ScatterKind.Boulder||p.Kind==ScatterKind.Stone),"Ice has no trees, shrubs or grass");
        var dry=new LocalScatter(new LocalTerrain(Fixture(Biome.Desert)));var grass=new LocalScatter(new LocalTerrain(Fixture(Biome.Grassland)));
        int Trees(ScatterPatch p)=>p.Instances.Count(i=>i.Kind==ScatterKind.Broadleaf||i.Kind==ScatterKind.Conifer||i.Kind==ScatterKind.SavannaTree);
        check(Trees(a)>Trees(grass.Build(grass.Address(point)))*5&&Trees(dry.Build(dry.Address(point)))==0,"Forest versus open/desert structure is ecological, not uniform random scatter");
        var frozen=new LocalScatter(new LocalTerrain(Fixture(Biome.TemperateForest,30)));
        check(Trees(frozen.Build(frozen.Address(point)))==0,"Cold summer conditions exclude trees even if a coarse biome label says forest");
        var saltyWorld=Fixture(Biome.TemperateForest);foreach(var h in saltyWorld.Ecology.Habitat)h.SalinityMilliPpt=10000;
        var salty=new LocalScatter(new LocalTerrain(saltyWorld));check(Trees(salty.Build(salty.Address(point)))==0,"Saline soils exclude ordinary forest trees");
        foreach(var p in a.Instances)
        {
            check(p.Variant>=0&&p.Variant<(p.Kind==ScatterKind.Boulder||p.Kind==ScatterKind.Stone||p.Kind==ScatterKind.Outcrop?18:3),"Rock variants carry lithology without colliding with plant variant keys");
            check(double.IsFinite(p.GroundM)&&p.HeightM>0&&p.WidthM>0,"Scatter dimensions and roots are finite physical metres");
            check(p.Normal.Length>.999&&p.Normal.Length<1.001,"Ground orientation is normalized");
        }
        var detail=forest.Build(forest.Address(point,true));check(detail.Instances.Any(i=>i.Kind==ScatterKind.Grass),"Nearby detail includes individual grass clumps");
        check(detail.Instances.All(i=>i.Kind==ScatterKind.Grass||i.Kind==ScatterKind.Stone),"Detail-tier LOD does not invent or reseed trees");
        foreach(var at in new[]{point,new V(1,1,0).Unit,new V(1,1,1).Unit,new V(0,0,1),new V(0,0,-1)})
        {
            var keys=forest.Around(at,100,true);check(keys.Distinct().Count()==keys.Length,"Planetary neighbourhoods have no duplicate tiles at poles/cube seams");
            check(keys.Contains(forest.Address(at,true)),"Neighbourhood enumeration includes the exact point's tile");
            for(int i=0;i<16;i++)
            {
                V east=V.Tangent(at,Math.Abs(at.Z)<.9?new V(0,0,1):new V(1,0,0)),north=V.Cross(at,east);double angle=i*Math.PI/8;
                V p=(at+(east*Math.Cos(angle)+north*Math.Sin(angle))*(95/(recipe.RadiusKm*1000))).Unit;
                check(keys.Contains(forest.Address(p,true)),"Zoom/patch planning covers each sampled neighbourhood direction without face seams");
            }
        }
        var neighbours=forest.Around(point,300);check(neighbours.Length<100,"Local scatter planning is bounded, not a world-cell census");
        var combined=new HashSet<ulong>();foreach(var k in neighbours.Take(3))foreach(var p in forest.Build(k).Instances)check(combined.Add(p.Id),"Adjacent global tiles do not overlap object identities");
        var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;try{forest.Build(key,cancel.Token);}catch(OperationCanceledException){cancelled=true;}check(cancelled,"Scatter work cancels before the first candidate row");
        var tasks=new ScatterPatch[4];Parallel.For(0,4,i=>tasks[i]=forest.Build(key));check(tasks.All(t=>t.Instances.Select(Describe).SequenceEqual(a.Instances.Select(Describe))),"Background scheduling does not change placements");
        check(before==StateHash.Compute(forestWorld),"Derived vegetation never changes the complete source world hash");
        Console.WriteLine($"forest {a.Instances.Length} props ({Trees(a)} trees); detail {detail.Instances.Length} clumps/stones; {neighbours.Length} planned macro patches");
    }
}
