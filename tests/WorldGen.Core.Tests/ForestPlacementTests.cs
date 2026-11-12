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
            bool cancelled=false;try{scatter.BuildForest(new ForestPageKey(key.Face,key.X*4,key.Y*4,0),new CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}
            Check(cancelled,"Near-ground pressure can cancel forest preparation");
            var broad=near.Values.Where(p=>new TreeMorphology(p.Variant).Form==TreeGrowthForm.BroadCrown).ToArray();
            Check(broad.Any(p=>p.HeightM>40)&&broad.Any(p=>p.HeightM<15)&&broad.Any(p=>p.HeightM>25&&p.HeightM<40),"Jungle emergent/canopy/understory cohorts");
            var distant=new Dictionary<ulong,ScatterInstance>();var independent=new LocalScatter(terrain,.1);
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            {
                var page=independent.BuildForest(new ForestPageKey(key.Face,key.X*4+x,key.Y*4+y,0));
                foreach(var root in page.Roots){Check(root.Population==1&&root.CrownScale==1,"Exact page must not merge trees");Check(distant.TryAdd(root.Tree.Id,root.Tree),"No duplicate far root");}
                using var stream=new MemoryStream();using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))TerrainMeshWire.WriteForest(writer,page);stream.Position=0;
                using var reader=new BinaryReader(stream);var roundtrip=TerrainMeshWire.ReadForest(reader,page.Key);Check(page.Roots.SequenceEqual(roundtrip.Roots),"Forest transport exact payload");
            }
            // Forest pages now carry cheap potential trees. Local interaction
            // performs exact physical admission, so it may remove candidates;
            // it MUST NOT choose another model, size, root, colour or yaw.
            bool SameCandidate(ScatterInstance a,ScatterInstance b)=>a.Id==b.Id&&a.Kind==b.Kind&&a.Variant==b.Variant&&a.Position.Equals(b.Position)&&
                a.HeightM==b.HeightM&&a.WidthM==b.WidthM&&a.YawDegrees==b.YawDegrees&&a.Color.Equals(b.Color);
            Check(near.Count<=distant.Count,"Physically admitted roots are a subset of forest candidates");
            foreach(var tree in near)Check(distant.TryGetValue(tree.Key,out var far)&&SameCandidate(tree.Value,far),"Same candidate identity, anatomy, dimensions, root, tint and yaw after physical admission");
            var candidatePatch=terrain.Patch(scatter.Position(key),840/.1);
            foreach(var candidate in distant.Values)
            {
                int slot=(int)(candidate.Id&65535);
                Check(scatter.TryTreeCandidate(key,slot%LocalScatter.TreeCandidateSide,slot/LocalScatter.TreeCandidateSide,candidatePatch,out var cachedCandidate)&&candidate.Equals(cachedCandidate),"Physical positive/negative cache cannot rewrite candidate identity or approximate grounding");
            }
            Console.WriteLine($"Canonical forest: {distant.Count} candidates, {near.Count} exact physical roots");
            // Reversing page completion also reverses which caller first fills
            // the shared habitat nodes. It must not alter any physical tree.
            var reverse=new LocalScatter(terrain,.1);
            for(int y=3;y>=0;y--)for(int x=3;x>=0;x--)
            {
                var page=reverse.BuildForest(new ForestPageKey(key.Face,key.X*4+x,key.Y*4+y,0));
                foreach(var root in page.Roots)Check(distant.TryGetValue(root.Tree.Id,out var prior)&&prior.Equals(root.Tree),"Habitat-node fill order preserves canonical trees");
            }
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
            for(int level=1;level<=4;level++)
            {
                int span=LocalScatter.ForestPageSide*(1<<level);var page=scatter.BuildForest(new ForestPageKey(key.Face,key.X*LocalScatter.TreeCandidateSide/span,key.Y*LocalScatter.TreeCandidateSide/span,level));
                Check(page.Probes>0&&page.Probes<=4096&&page.Roots.Length<=64,"Distant canopy uses bounded canonical density samples without per-root physical geometry");
                foreach(var root in page.Roots)
                {
                    Check(root.Population==0,"Canopy descriptor is not a fabricated population member");Check(root.Tree.WidthM>0&&root.Tree.HeightM>0&&double.IsFinite(root.Tree.WidthM),"Finite canopy dimensions");
                    Check(root.CanopyTreeCount>0&&double.IsFinite(root.CanopyTreeCount),"Coarse density carries a finite expected local tree count");
                    double represented=LocalScatter.ForestCanopyAnchorCount(root)*(LocalScatter.ForestCanopyUsesStand(root)?LocalScatter.ForestStandTreeCount:1);
                    Check(represented>=root.CanopyTreeCount&&represented-root.CanopyTreeCount<LocalScatter.ForestStandTreeCount,"Coarse representation preserves density to within one prepared stand");
                }
                using(var stream=new MemoryStream())
                {
                    using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))TerrainMeshWire.WriteForest(writer,page);stream.Position=0;
                    using var reader=new BinaryReader(stream);Check(page.Roots.SequenceEqual(TerrainMeshWire.ReadForest(reader,page.Key).Roots),"Coarse tree density survives worker transport");
                }
                Check(page.Roots.SequenceEqual(scatter.BuildForest(page.Key).Roots),"Deterministic regional canopy");
            }
            foreach(var point in new[]{at,new V(1,1,0).Unit,new V(0,0,1),new V(-1,0,0)})
            {
                var plan=scatter.PlanForest(point,10000,1600);Check(plan.Length<=4096,"Bounded forest page plan "+plan.Length);var keys=new HashSet<ForestPageKey>(plan);Check(keys.Count==plan.Length,"No duplicate planned pages");
                foreach(var page in plan)
                {
                    Check(page.Level<=scatter.ForestCanopyMaximumLevel,"Coarse forest pages retain a bounded physical footprint");
                    scatter.ForestBounds(page,out var center,out var radius);
                    double gap=Math.Max(0,(center-point).Length*terrain.RadiusM*.1-radius);
                    Check(page.Level==0||gap>=LocalScatter.ForestIdentityDistanceM,"Recognisable forest keeps canonical exact placement out to two kilometres");
                }
                foreach(var page in plan){var parent=page.Parent;for(int l=page.Level+1;l<=24;l++,parent=parent.Parent)Check(!keys.Contains(parent),"Plan partitions coverage without overlapping ancestors");}
                Check(plan.SequenceEqual(scatter.PlanForest(point,10000,1600)),"Stable plan at unchanged point");
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
