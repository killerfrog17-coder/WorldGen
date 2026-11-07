using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class MangroveTests
{
    public static void Controls()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var roots=new HashSet<TreeRootForm>();var soil=new GroundProfile{DepthM=1,Fertility=.8,Salinity=.15,WaterCapacityMm=120,Drainage=.15};
        for(int seed=0;seed<30;seed++)
        {
            var world=TreeLineageSnapshotTests.Fixture(Biome.Mangrove,seed);var species=TreeFlora.Create(world,0,3);var habitat=world.Ecology.Habitat[0];
            foreach(var s in species.Where(s=>s.Form==TreeGrowthForm.BroadCrown))
            {
                Check(TreeFlora.TidalCode(s.MorphologyCode)&&s.Morphology.Strategy==TreeCrownStrategy.Mangrove,"Mapped coastal broadleaf lineages acquire actual tidal anatomy");
                Check(s.HeightM>=6&&s.HeightM<20&&s.WidthM>=s.HeightM*.7,"Mangroves have bounded low spreading crowns rather than generic30m inland trunks");
                Check(s.Morphology.CrownBase>=.19&&s.Morphology.CrownBase<=.31&&s.LeafHabit==TreeLeafHabit.Evergreen,"Low forking evergreen coastal architecture");
                Check(s.Morphology.LeafShape==TreeLeafShape.Oval||s.Morphology.LeafShape==TreeLeafShape.Lanceolate,"Simple mangrove leaves rather than arbitrary lobed or compound foliage");
                var root=TreeFlora.RootForm(s.MorphologyCode);roots.Add(root);Check(root!=TreeRootForm.Ordinary,"Every tidal lineage has an inherited aerial root form");
                Check(TreeFlora.RootForm(TreeFlora.RenderCode(s.MorphologyCode))==root,"Near and baked distant anatomy share the same root type");
                var p=new ForestCoverProfile{MorphologyCode=s.MorphologyCode,Coverage=.7,WarmC=30,HeightM=s.HeightM,WidthM=s.WidthM};
                Check(TreeFlora.Rooting(p,.2,false,0,Biome.Mangrove),"Bare low tidal mud can support mapped mangroves");
                Check(TreeFlora.Rooting(p,-.8,true,.8,Biome.Mangrove),"Mapped mangroves root in genuinely shallow salt water");
                Check(!TreeFlora.Rooting(p,-2,true,2,Biome.Mangrove),"Deep water remains unrootable");
                Check(!TreeFlora.Rooting(p,-.8,true,.8,Biome.Ocean),"A blended tidal code cannot plant an unrelated open-ocean region");
                var cold=habitat;cold.ColdTenthsC=-20;
                Check(TreeFlora.Suitability(s,cold,soil,Biome.Mangrove)==0,"Freezing winters exclude mangroves");
                Check(TreeFlora.Suitability(s,habitat,soil,Biome.Mangrove)>0,"Warm saturated mildly saline soil supports the lineage");
                Check(TreeFlora.Surface(p,new V(1,0,0),seed,637100,-.8,1,true,.8,Biome.Mangrove).Coverage>0,"Terrain/map forest surface includes the same shallow tidal roots");
                Check(TreeFlora.Surface(p,new V(1,0,0),seed,637100,-2,1,true,2,Biome.Mangrove).Coverage==0,"Distant terrain forest does not paint deep water");
            }
            foreach(var s in species.Where(s=>s.Form!=TreeGrowthForm.BroadCrown))Check(TreeFlora.Suitability(s,habitat,soil,Biome.Peatland)==0,"Peat label at a coastal mud flat cannot turn palms or conifers into salt-adapted mangroves");
        }
        Check(roots.SetEquals(new[]{TreeRootForm.Prop,TreeRootForm.Pneumatophore,TreeRootForm.Knee}),"Seeded lineage diversity includes prop roots, breathing pegs and knees");
        var inland=new ForestCoverProfile{MorphologyCode=123<<8,Coverage=.8,WarmC=25,HeightM=20,WidthM=12};
        Check(!TreeFlora.Rooting(inland,-.5,true,.5,Biome.Mangrove)&&!TreeFlora.Rooting(inland,1,false,0,Biome.Mangrove),"Ordinary inland trees retain their previous submerged/low-shore exclusion");
        Console.WriteLine($"PASS {checks} tidal lineage, rooting, climate and shared forest-surface controls");
    }
    public static void World(string input,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(input);string history=world.Prehistory.Hash;int checks=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var model=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);var sites=TidalForestSites.Nearshore(world,model).Take(3).ToArray();
        int mapped=Enumerable.Range(0,world.Surface.Cells.Length).Count(i=>model.Profile(i).Biome==Biome.Mangrove);
        Console.WriteLine($"Mapped mangrove cells {mapped}; actual nearshore sites {sites.Length}");
        Check(sites.Length>0,"Real mapped warm coastal forests supply a shallow tidal shore");var rows=new List<object>();
        foreach(var site in sites)
        {
            var scatter=new LocalScatter(model,.1);var key=scatter.Address(site.Position);var chunk=scatter.Build(key);
            var trees=chunk.Instances.Where(t=>t.Kind==ScatterKind.GeneratedTree).ToDictionary(t=>t.Id);int tidal=0,low=0,submerged=0;var forms=new HashSet<TreeRootForm>();
            var patch=model.Patch(scatter.Position(key),25000);
            foreach(var tree in trees.Values)
            {
                var sample=patch.Sample(tree.Position);bool isTidal=TreeFlora.TidalCode(tree.Variant);
                Check(TreeFlora.Rooting(sample.Forest,sample.ElevationM,sample.Underwater,sample.Underwater?sample.WaterSurfaceM-sample.ElevationM:0,model.Profile(sample.Cell).Biome),"Every admitted tree obeys the shared physical rooting rule");
                if(!isTidal){Check(!sample.Underwater&&sample.ElevationM>=2,"Inland lineages retain their old water/height exclusion");continue;}
                tidal++;forms.Add(TreeFlora.RootForm(tree.Variant));if(sample.ElevationM<2)low++;
                if(sample.Underwater){submerged++;Check(sample.WaterSurfaceM-sample.ElevationM<=1.25,"Submerged mangrove roots remain in shallow water");}
            }
            Check(tidal>0,"Actual coastal scatter contains tidal-anatomy trees");
            var independent=new LocalScatter(model,.1);var far=new Dictionary<ulong,ScatterInstance>();int pages=LocalScatter.TreeCandidateSide/LocalScatter.ForestPageSide;
            for(int y=0;y<pages;y++)for(int x=0;x<pages;x++)
            {
                var page=independent.BuildForest(new ForestPageKey(key.Face,key.X*pages+x,key.Y*pages+y,0));
                foreach(var root in page.Roots){Check(root.Population==1&&root.CrownScale==1,"Detailed tidal forest uses exact individual roots");Check(far.TryAdd(root.Tree.Id,root.Tree),"No duplicate tidal forest root across pages");}
            }
            Check(trees.Count==far.Count,"Near scatter and distant exact forest census agree");
            foreach(var tree in trees)Check(far.TryGetValue(tree.Key,out var other)&&tree.Value.Equals(other),"Near/far tidal root identity, anatomy, dimensions, position and ground agree exactly");
            Console.WriteLine($"Tidal coast {site.Cell}: trees {trees.Count}, tidal {tidal}, low {low}, submerged {submerged}, forms {string.Join(",",forms)}");
            rows.Add(new{site.Cell,Position=new[]{site.Position.X,site.Position.Y,site.Position.Z},site.Height,site.ForestCoverage,Trees=trees.Count,Tidal=tidal,LowShore=low,Submerged=submerged,Forms=forms.Select(f=>f.ToString()).ToArray()});
        }
        Check(rows.Count>0,"Actual world mangrove evidence exists");Check(world.Prehistory.Hash==history,"Tidal forest derivation does not mutate broad history");
        File.WriteAllText(Path.Combine(output,"mangroves.json"),System.Text.Json.JsonSerializer.Serialize(new{MappedCells=mapped,Checks=checks,Sites=rows},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {checks} actual tidal rooting and exact near/far forest checks");
    }
}
