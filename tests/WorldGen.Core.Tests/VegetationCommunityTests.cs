using WorldGen.Core;

static class VegetationCommunityTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        const double radius=637100;
        var soil=new GroundProfile{DepthM=.8,Fertility=.8,Gravel=.05,PH=6.1,WaterCapacityMm=110,Drainage=.6,Organic=.12};
        var recipe=Recipe.Preset("Preview");
        var profiles=new Dictionary<Biome,ForestCoverProfile>();
        foreach(var biome in new[]{Biome.TemperateForest,Biome.TropicalRainforest,Biome.BorealForest,Biome.Savanna,Biome.Mangrove})
        {
            var h=new HabitatCell{Biome=biome,WarmTenthsC=biome==Biome.BorealForest?150:biome==Biome.TemperateForest?240:300,ColdTenthsC=biome==Biome.BorealForest?-180:biome==Biome.TemperateForest?-20:180,AridityMilli=biome==Biome.Savanna?650:1300,ProductivityGCM2Year=1000};
            var world=new World{Recipe=recipe,Ecology=new EcologyResult{Habitat=new[]{h},Regions=new[]{new Ecoregion{RepresentativeCell=0}}}};
            var species=TreeFlora.Create(world,0,0);var profile=TreeFlora.Describe(h,soil,species,biome);profiles[biome]=profile;
            var stands=new List<double>();var shades=new List<double>();
            for(int z=0;z<48;z++)for(int x=0;x<48;x++)
            {
                var p=new V(1,(x-24)*25/radius,(z-24)*25/radius).Unit;
                double stand=TreeFlora.Stand(profile,p,recipe.Seed,radius),shade=TreeFlora.CanopyShade(profile,p,recipe.Seed,radius);
                Check(stand>=0&&stand<=1&&shade>=0&&shade<=1,"Finite bounded stand/light field");stands.Add(stand);shades.Add(shade);
                var moved=(p+new V(0,.01/radius,0)).Unit;
                Check(Math.Abs(stand-TreeFlora.Stand(profile,moved,recipe.Seed,radius))<.01,"No discontinuous patch boundary");
            }
            Check(stands.Max()-stands.Min()>.1,"Community has coherent spatial variation "+biome);
            if(biome==Biome.Mangrove)Check(species.Where(s=>TreeFlora.Suitability(s,h,soil,biome)>0).All(s=>s.Form==TreeGrowthForm.BroadCrown),"Salinity must not admit mangrove tree ferns/palms/conifers");
            Console.WriteLine($"{biome}: stem admission {profile.Coverage*TreeFlora.Stocking(profile):F3}, stand {stands.Min():F3}..{stands.Max():F3}, mean shade {shades.Average():F3}, shade<.35 {shades.Count(s=>s<.35)*100d/shades.Count:F1}%");
        }
        var small=profiles[Biome.TemperateForest];small.WidthM=20;var large=small;large.WidthM=40;
        Check(Math.Abs(TreeFlora.Stocking(large)/TreeFlora.Stocking(small)-.25)<1e-9,"Twice crown diameter needs one quarter stem density, not four times canopy overlap");
        var baseCover=new GroundCoverProfile{Grasses=new V(.25,.15,.1),Herbs=new V(.05,.05,.12),Mats=new V(.10,.08,0),Color=new V(.3,.4,.2),Stature=1,LeafWidth=1};
        GroundCoverProfile Cover(double up,double shade)=>GroundFlora.Surface(baseCover,new V(1,0,0),recipe.Seed,radius,soil,100,100,up,false,0,Biome.TemperateForest,shade);
        var cliff=Cover(.5,.2);Check(cliff.Grasses.Length==0&&cliff.Herbs.Length==0,"No rooted grass/herbs on cliff; crust material may remain");
        var lit=Cover(1,.1);var dark=Cover(1,.9);
        Check(lit.Grasses.Length>dark.Grasses.Length*2,"Forest openings admit more light-demanding grasses");
        Check(dark.Grasses.Length<lit.Grasses.Length*.04,"Closed-canopy understory is not an open-ground lawn");
        Check(dark.Herbs.Z>lit.Herbs.Z,"Shade favours moist fern cover rather than identical lawn");
        Check(Cover(.7,.2).Grasses.Length<lit.Grasses.Length,"Rooted cover decreases continuously on steep ground");
        Check(profiles[Biome.TropicalRainforest].GapThreshold<profiles[Biome.TemperateForest].GapThreshold,"Wet closed communities recover openings more readily; no universal clearing quota");
        Console.WriteLine($"PASS {checks} vegetation community assertions");
    }
}
