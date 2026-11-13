using WorldGen.Core;
using WorldGen.Presentation;

static class ForestPlacementTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var recipe=Recipe.Preset("Preview");recipe.Detail=5000;var surface=SphericalMesh.Build(recipe,default);int count=surface.Cells.Length;
        World Fixture(Biome biome)=>new World{Recipe=recipe.Clone(),Surface=surface,ElevationM=Enumerable.Repeat(250,count).ToArray(),Hydrology=new HydrologyResult{OceanConnected=new bool[count]},
            Ecology=new EcologyResult{Region=new int[count],Regions=new[]{new Ecoregion{Id=0,RepresentativeCell=0,Biome=biome}},Habitat=Enumerable.Range(0,count).Select(_=>new HabitatCell{Biome=biome,Soil=SoilKind.Loam,SoilFertilityPermille=800,AridityMilli=biome==Biome.Savanna?650:1300,WarmTenthsC=300,ColdTenthsC=180,ProductivityGCM2Year=900,GrowingDays=330}).ToArray()}};
        var densities=new Dictionary<Biome,int>();
        foreach(var biome in new[]{Biome.TropicalRainforest,Biome.TemperateForest,Biome.BorealForest,Biome.Savanna,Biome.Grassland,Biome.Desert,Biome.Tundra})
        {
            var world=Fixture(biome);string before=StateHash.Compute(world);var terrain=new LocalTerrain(world,horizontalScale:.1);var scatter=new LocalScatter(terrain,.1);var at=new V(1,0,0);var key=scatter.Address(at);
            var near=scatter.Build(key).Instances.Where(p=>p.Kind==ScatterKind.GeneratedTree).ToDictionary(p=>p.Id);
            densities[biome]=near.Count;double nominal=2*terrain.RadiusM*.1/scatter.Divisions(false);
            Console.WriteLine($"{biome}: {near.Count} roots / {nominal*nominal/10000:F2}ha ({near.Count*10000/(nominal*nominal):F1}/ha)");
            Check(before==StateHash.Compute(world),"Derived forest modified saved world "+biome);
            // The map must use the same open/closed stand noise as planting,
            // not the old default (open woodland) for every forest profile.
            var mapPatch=terrain.Patch(at,2048/.1);
            for(int i=0;i<16;i++)
            {
                var point=mapPatch.Position(i*3,i*7);var sample=mapPatch.Sample(point);
                double potential=sample.Forest.Coverage,up=.68+i*.012;
                double expected=10000*TreeFlora.PlacementPerM2*D.Clamp(potential*TreeFlora.Stocking(sample.Forest)*TreeFlora.Stand(sample.Forest,point,recipe.Seed,terrain.RadiusM*.1))*TreeFlora.SlopeAdmission(up);
                double actual=TerrainMapSampling.TreeDensity(terrain,null,sample,point,up);
                Check(Math.Abs(actual-expected)<1e-10,"Tree-density map shares planting stand field "+biome+": "+actual+" vs "+expected);
            }
            if(biome==Biome.Desert||biome==Biome.Tundra)Check(near.Count==0,"No temperate forest in barren habitat");
            if(biome==Biome.Savanna)Check(near.Values.All(p=>new TreeMorphology(p.Variant).Form!=TreeGrowthForm.NeedleCrown),"No generic pine trees in warm savannah");
            if(biome!=Biome.TropicalRainforest)continue;
            Check(near.Count>1000,"Rainforest must be a connected stand, not decorative trees");
            Check(near.Values.All(p=>new TreeMorphology(p.Variant).Form!=TreeGrowthForm.NeedleCrown),"No needle trees in warm lowland jungle");
            int pagesPerChunk=LocalScatter.TreeCandidateSide/LocalScatter.ForestPageSide;
            bool cancelled=false;try{scatter.BuildForest(new ForestPageKey(key.Face,key.X*pagesPerChunk,key.Y*pagesPerChunk,0),new CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}
            Check(cancelled,"Near-ground pressure can cancel forest preparation");
            var broad=near.Values.Where(p=>new TreeMorphology(p.Variant).Form==TreeGrowthForm.BroadCrown).ToArray();
            Check(broad.Any(p=>p.HeightM>40)&&broad.Any(p=>p.HeightM<15)&&broad.Any(p=>p.HeightM>25&&p.HeightM<40),"Jungle emergent/canopy/understory cohorts");
            var independent=new LocalScatter(terrain,.1);var distant=new Dictionary<ulong,ScatterInstance>();
            var candidatePatch=terrain.Patch(scatter.Position(key),840/.1);
            for(int py=0;py<pagesPerChunk;py++)for(int px=0;px<pagesPerChunk;px++)
            {
                var pageKey=new ForestPageKey(key.Face,key.X*pagesPerChunk+px,key.Y*pagesPerChunk+py,0);
                var timer=System.Diagnostics.Stopwatch.StartNew();var page=independent.BuildForest(pageKey);timer.Stop();
                Console.WriteLine($"Fixed planting page: {page.Roots.Length} blocks / {page.Probes} probes in {timer.Elapsed.TotalMilliseconds:F2}ms");
                Check(page.Probes<=64&&page.Roots.Length<=64*terrain.FloraRegion(0).Trees.Length,"Bounded species batches per planting block");
                var groups=page.Roots.GroupBy(r=>(r.CellX,r.CellY)).ToArray();int mixed=0;
                foreach(var group in groups)
                {
                    ForestPlantingMask mask=default;foreach(var species in group){Check(!mask.Intersects(species.AllowedMask),"Each planting cell belongs to exactly one species batch");mask.Union(species.AllowedMask);}
                    if(group.Select(r=>r.Tree.Variant).Distinct().Count()>1)mixed++;
                }
                Check(mixed>groups.Length*.8,"Suitable rainforest species mix within planting blocks rather than rectangular monocultures");
                foreach(var block in page.Roots)
                {
                    Check(block.Population==-LocalScatter.ForestStandTreeCount&&!block.AllowedMask.Empty,"Fixed candidate block, never a statistical population");
                    for(int cy=0;cy<LocalScatter.ForestPlantingSide;cy++)for(int cx=0;cx<LocalScatter.ForestPlantingSide;cx++)
                    {
                        int gx=block.CellX+cx,gy=block.CellY+cy;uint h=LocalScatter.PlantingHash(key.Face,gx,gy,recipe.Seed);
                        LocalScatter.PlantingOffset(h,out float ox,out float oy);float u=(cx+ox)/LocalScatter.ForestPlantingSide,v=(cy+oy)/LocalScatter.ForestPlantingSide;
                        float probability=(block.P00+(block.P10-block.P00)*u)*(1-v)+(block.P01+(block.P11-block.P01)*u)*v;
                        bool admitted=block.AllowedMask.Contains(cy*LocalScatter.ForestPlantingSide+cx)&&LocalScatter.PlantingUnit(h^0x27d4eb2du)<probability;
                        int x=gx-key.X*LocalScatter.TreeCandidateSide,y=gy-key.Y*LocalScatter.TreeCandidateSide;
                        bool actual=independent.TryTreeCandidate(key,x,y,candidatePatch,out var tree);
                        Check((actual&&tree.Variant==block.Tree.Variant)==admitted,"GPU species masks and interactive CPU predicate select the same trees");
                        if(!admitted)continue;
                        LocalScatter.PlantingDimensions(h,block.Tree.Variant,out float height,out float width,out float yaw,out float tint);
                        Check(tree.Position.Equals(scatter.ForestPosition(key.Face,gx+ox,gy+oy))&&tree.Variant==block.Tree.Variant&&
                            tree.HeightM==block.Tree.HeightM*height&&tree.WidthM==block.Tree.WidthM*width&&tree.Color.Equals(new V(tint,tint,tint))&&tree.YawDegrees==yaw*180/Math.PI,
                            "GPU expansion preserves each CPU tree position, species, dimensions, tint and orientation");
                        Check(distant.TryAdd(tree.Id,tree),"No duplicate tree across block/page borders");
                    }
                }
                using var stream=new MemoryStream();using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))TerrainMeshWire.WriteForest(writer,page);stream.Position=0;
                using var reader=new BinaryReader(stream);Check(page.Roots.SequenceEqual(TerrainMeshWire.ReadForest(reader,page.Key).Roots),"Planting masks and probabilities survive worker transport");
                var fresh=new LocalScatter(terrain,.1);
                Check(page.Roots.SequenceEqual(fresh.BuildForest(page.Key).Roots),"Independent planting output is deterministic");
            }
            bool SameCandidate(ScatterInstance a,ScatterInstance b)=>a.Id==b.Id&&a.Kind==b.Kind&&a.Variant==b.Variant&&a.Position.Equals(b.Position)&&
                a.HeightM==b.HeightM&&a.WidthM==b.WidthM&&a.YawDegrees==b.YawDegrees&&a.Color.Equals(b.Color);
            Check(near.Count==distant.Count,"Interactive and rendered forest populations are identical");
            foreach(var tree in near)Check(distant.TryGetValue(tree.Key,out var far)&&SameCandidate(tree.Value,far),"Physical grounding cannot replace a tree");
            var rootPatch=terrain.Patch(scatter.Position(key),840/.1);
            foreach(var tree in near.Values.Take(64))
            {
                var geometry=rootPatch.Geometry(tree.Position);
                Check(tree.GroundM==geometry.ElevationM,"Shared habitat retains exact root elevation");
                Check(!geometry.Underwater||TreeFlora.TidalCode(tree.Variant),"Ordinary tree cannot gain an underwater collidable root");
            }
            var cachePatch=terrain.Patch(scatter.Position(key),840/.1);
            for(int y=0;y<LocalScatter.TreeCandidateSide;y++)for(int x=0;x<LocalScatter.TreeCandidateSide;x++)
            {bool cached=scatter.TryTree(key,x,y,cachePatch,out var a),fresh=independent.TryTree(key,x,y,cachePatch,out var b);Check(cached==fresh&&(!cached||a.Equals(b)),"Exact positive AND negative tree cache equivalence");}
            foreach(var point in new[]{at,new V(1,1,0).Unit,new V(0,0,1),new V(-1,0,0)})
            {
                var plan=scatter.PlanForest(point,6000,1600);Check(plan.Length<=4096,"Bounded forest page plan "+plan.Length);var keys=new HashSet<ForestPageKey>(plan);Check(keys.Count==plan.Length,"No duplicate planned pages");
                foreach(var page in plan)
                {
                    Check(page.Level==0,"Coarse forest pages retain a bounded physical footprint");
                    scatter.ForestBounds(page,out var center,out var radius);
                    double gap=Math.Max(0,(center-point).Length*terrain.RadiusM*.1-radius);
                    Check(page.Level==0||gap>=LocalScatter.ForestIdentityDistanceM,"Recognisable forest keeps canonical exact placement out to two kilometres");
                }
                foreach(var page in plan){var parent=page.Parent;for(int l=page.Level+1;l<=24;l++,parent=parent.Parent)Check(!keys.Contains(parent),"Plan partitions coverage without overlapping ancestors");}
                Check(plan.SequenceEqual(scatter.PlanForest(point,6000,1600)),"Stable plan at unchanged point");
                var frame=new LocalTerrainFrame(point,terrain.RadiusM,250,.1);var alternate=new LocalTerrainFrame((point+frame.East*(120/frame.RadiusM)).Unit,terrain.RadiusM,250,.1);double worst=0;
                for(int i=0;i<200;i++)
                {var p=frame.Position(Math.Sin(i)*60000,Math.Cos(i*1.7)*60000);double dot=V.Dot(p,alternate.Origin),xx=V.Dot(p,alternate.East)*alternate.RadiusM/dot,zz=V.Dot(p,alternate.North)*alternate.RadiusM/dot;double error=(alternate.Position(xx,zz)-p).Length*alternate.RadiusM;worst=Math.Max(worst,error);Check(error<.02,"Quantized chart roundtrip must be below 2cm");}
                Console.WriteLine($"Reanchor {point.X},{point.Y},{point.Z}: max {worst:F6}m over 60km projections");
            }
        }
        Check(densities[Biome.TropicalRainforest]>densities[Biome.Savanna]*15,"Closed jungle distinct from sparse savannah");Check(densities[Biome.Savanna]>densities[Biome.Grassland],"Grassland sparser than savannah");
        Console.WriteLine($"PASS {checks} canonical forest placement / transport / hierarchy assertions");
    }
}
