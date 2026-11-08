using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class WetlandVegetationTests
{
    public static void PerformanceSites(string input,string output)
    {
        var world=WorldFile.ReadFile(input);var model=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var marsh=TidalMarshSites.Nearshore(world,model).First();
        var mangrove=TidalForestSites.Nearshore(world,model).First();
        var sites=new List<VegetationBenchmarkSite>();var receipts=new List<object>();
        foreach(var (name,center) in new[]{("warm-marsh-water",new V(-.4774612,.654411578,-.586324388)),("mangrove-water",mangrove.Position),("saltmarsh-water",marsh.Position)})
        {
            var patch=model.Patch(center,5000);var frame=new LocalTerrainFrame(center,model.RadiusM,0,.1);V? chosen=null;
            for(int ring=1;ring<=64&&!chosen.HasValue;ring++)for(int i=0;i<24&&!chosen.HasValue;i++)
            {
                var at=frame.Position(ring*8*Math.Cos(i*Math.PI/12),ring*8*Math.Sin(i*Math.PI/12));var s=patch.Sample(at);
                if(s.Underwater&&s.WaterSurfaceM-s.ElevationM>=.1&&s.WaterSurfaceM-s.ElevationM<=1.1&&
                    (name=="warm-marsh-water"?s.WaterKind==LocalWaterKind.Lake:s.WaterKind==LocalWaterKind.Ocean&&MidFlora.Tidal(s.Biome)))chosen=at;
            }
            if(!chosen.HasValue)throw new Exception("No actual shallow submerged benchmark point: "+name);
            var sample=patch.Sample(chosen.Value);
            sites.Add(new VegetationBenchmarkSite{Name=name,Position=chosen.Value,Biome=sample.Biome.ToString()});
            receipts.Add(new{Name=name,Position=new{chosen.Value.X,chosen.Value.Y,chosen.Value.Z},Ground=sample.ElevationM,Water=sample.WaterSurfaceM,Kind=sample.WaterKind.ToString(),Biome=sample.Biome.ToString()});
        }
        File.WriteAllText(output,WorldJson.Write(new VegetationBenchmarkSiteSet{HistoryHash=world.Prehistory.Hash,Sites=sites.ToArray()}));
        File.WriteAllText(output+".receipts.json",JsonSerializer.Serialize(receipts,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("PASS three explicit actual shallow-water rendering fixtures; use travel-whip-water-surface, no dry-site relocation");
    }
    public static void Controls()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var soil=new GroundProfile{DepthM=1,PH=6.8,WaterCapacityMm=100,Fertility=.7,Gravel=.1};
        var habitat=new HabitatCell{Biome=Biome.Marsh,AridityMilli=1200,FloodFractionPermille=900,WaterTableCm=0,WarmTenthsC=250};
        MidPlantSpecies Reed()=>new MidPlantSpecies{Form=MidGrowthForm.ReedStand,HeightM=2,WidthM=1.3,Moisture=1.3,Shade=.1,MinWarmC=0,MaxWarmC=45,MinPH=5,MaxPH=8.5,MaxSalinity=.1,FloodTolerance=.95};
        var reed=Reed();var shallow=new LocalTerrainSample{Biome=Biome.Marsh,ElevationM=.1,WaterSurfaceM=.3,WaterMaskM=.2,WaterKind=LocalWaterKind.Lake};
        Check(MidFlora.Rooting(shallow)&&MidFlora.WaterAdmission(reed,shallow)>.9,"Low-lying shallow freshwater supports rooted emergents");
        var beach=shallow;beach.Biome=Biome.Grassland;beach.WaterSurfaceM=double.NaN;
        Check(!MidFlora.Rooting(beach),"Low exposed beach does not gain ordinary upland scrub");
        var marine=shallow;marine.Biome=Biome.SaltMarsh;marine.WaterKind=LocalWaterKind.Ocean;
        Check(MidFlora.WaterAdmission(reed,marine)==0,"Freshwater ecotypes do not colonize seawater");
        var halophyte=Reed();halophyte.WaterSalinityLimitPpt=55;halophyte.MaxSalinity=.9;
        Check(MidFlora.WaterAdmission(halophyte,marine)>.9,"Coastal emergents can occupy shallow mapped tidal wetland");
        marine.Biome=Biome.Ocean;Check(MidFlora.WaterAdmission(halophyte,marine)==0,"Salt tolerance never authorizes offshore ocean vegetation");
        marine.Biome=Biome.SaltMarsh;marine.WaterSurfaceM=2;Check(!MidFlora.Rooting(marine)&&MidFlora.WaterAdmission(halophyte,marine)==0,"Deep tidal water excludes rooted marsh plants");
        marine.WaterSurfaceM=.3;marine.WaterSpeedMPerS=1;Check(MidFlora.WaterAdmission(halophyte,marine)==0,"Fast channel flow excludes still-water emergents");
        Check(MidFlora.WaterAdmission(new MidPlantSpecies{Form=MidGrowthForm.LeafyBush,HeightM=2},shallow)==0,"Ordinary shrubs remain terrestrial");
        var saline=habitat;saline.Biome=Biome.SaltMarsh;saline.SalinityMilliPpt=35000;soil.Salinity=1;
        Check(MidFlora.Suitability(reed,saline,soil,Biome.SaltMarsh,25,.1,0,1)==0,"Saturated soil index does not erase freshwater salt limits");
        Check(MidFlora.Suitability(halophyte,saline,soil,Biome.SaltMarsh,25,.1,0,1)>.1,"Marine-adapted taxa use actual coastal ppt rather than the saturated five-ppt soil index");
        saline.SalinityMilliPpt=90000;Check(MidFlora.Suitability(halophyte,saline,soil,Biome.SaltMarsh,25,.1,0,1)==0,"Hypersaline pools still exceed coastal plant tolerance");
        saline.SalinityMilliPpt=35000;
        Check(MidFlora.Suitability(halophyte,saline,soil,Biome.Mangrove,25,.99,0,1)<.0001,"Closed mangrove shade retains an open mud floor");
        soil.Salinity=0;
        Check(MidFlora.EstablishmentWeight(reed,habitat,soil,Biome.Marsh,25,.1)>MidFlora.Suitability(reed,habitat,soil,Biome.Marsh,25,.1),"Suitable marsh promotes emergent stands");
        Check(MidFlora.EstablishmentWeight(reed,habitat,soil,Biome.Grassland,25,.1)==MidFlora.Suitability(reed,habitat,soil,Biome.Grassland,25,.1),"The upland weight remains literally unchanged");
        foreach(double ph in new[]{3d,6.8,9.5})foreach(double warm in new[]{-20d,25,60})foreach(double depth in new[]{0d,.2,1})
        {
            var p=soil;p.PH=ph;p.DepthM=depth;
            double fit=MidFlora.EstablishmentWeight(reed,habitat,p,Biome.Marsh,warm,.1);
            Check(double.IsFinite(fit)&&fit>=0,"Bounded finite ecological establishment");
            if(ph!=6.8||warm!=25||depth==0)Check(fit==0,"Dense stands do not bypass temperature, pH or rooting depth");
        }
        foreach(var form in Enum.GetValues<MidGrowthForm>())Check(MidFlora.ColonyMembers(form)<=4,"Stable scatter IDs retain the four-member allocation");
        var cover=new GroundCoverProfile{Stature=1};var mid=new MidCoverProfile();var forest=new ForestCoverProfile();var biome=Biome.SaltMarsh;
        LandscapeProvince.Cover(new LandscapeControls{Wet=1,Peat=1},new V(10,20,30),73421,ref cover,ref mid,ref forest,ref biome);
        Check(biome==Biome.SaltMarsh,"Organic tidal marsh remains tidal rather than being renamed freshwater peatland");
        var fake=new World{Recipe=Recipe.Preset("Preview"),Ecology=new EcologyResult{Regions=new[]{new Ecoregion{RepresentativeCell=0,ProvinceId=3}},Habitat=new[]{saline}}};
        var a=MidFlora.Create(fake,0,3);var b=MidFlora.Create(fake,0,3);
        for(int i=0;i<a.Length;i++)
        {Check(a[i].Name==b[i].Name&&a[i].MorphologyCode==b[i].MorphologyCode&&a[i].HeightM==b[i].HeightM,"Generated coastal taxa remain deterministic");if(a[i].Form==MidGrowthForm.ReedStand){Check(a[i].WaterSalinityLimitPpt>=35&&a[i].WaterSalinityLimitPpt<=60,"Coastal emergents receive bounded physical salt tolerance");Check(a[i].Morphology.TidalEmergent,"Marine-adapted taxa use coastal grass/rush morphology rather than freshwater cattails");}}
        Console.WriteLine($"PASS {checks} wetland vegetation habitat/constraint controls");
    }

    public static void World(string input,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(input);string before=StateHash.Compute(world);
        var timer=System.Diagnostics.Stopwatch.StartNew();var model=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);var scatter=new LocalScatter(model,.1);
        var tidal=TidalMarshSites.Nearshore(world,model).FirstOrDefault();
        if(tidal==null)throw new Exception("No actual mapped intertidal saltmarsh discovered");
        var sites=new[]{("warm-marsh",new V(-.4774612,.654411578,-.586324388)),("tidal-mangrove",new V(-.876238281,.404683964,-.261605362)),("tidal-saltmarsh",tidal.Position)};
        int checks=0,totalSubmerged=0;var records=new List<object>();void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        foreach(var (name,at) in sites)
        {
            var patch=model.Patch(at,24000);var sample=patch.Sample(at);var frame=new LocalTerrainFrame(at,model.RadiusM,sample.ElevationM,.1);
            var keys=new HashSet<ScatterKey>();for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)keys.Add(scatter.Address(frame.Position(x*512,z*512),false));
            var ids=new HashSet<ulong>();int reeds=0,submerged=0,lowTidal=0,all=0;double maxDepth=0;
            foreach(var key in keys)
            {
                var page=scatter.Build(key);
                foreach(var item in page.Instances)
                {
                    Check(ids.Add(item.Id),"No duplicate scatter identities across neighbouring pages");all++;
                    if(item.Kind!=ScatterKind.MidPlant)continue;
                    var root=patch.Sample(item.Position);
                    Check(Math.Abs(root.ElevationM-item.GroundM)<.002,"Individual plant roots follow the actual terrain bed");
                    Check(MidFlora.Rooting(root),"Mid vegetation uses a supported actual root habitat");
                    if((MidGrowthForm)((item.Variant>>27)&7)!=MidGrowthForm.ReedStand)continue;
                    reeds++;if(MidFlora.Tidal(root.Biome)&&root.ElevationM<2)lowTidal++;
                    if(root.Underwater)
                    {
                        submerged++;double depth=root.WaterSurfaceM-root.ElevationM;maxDepth=Math.Max(maxDepth,depth);
                        Check(depth<1.2&&depth<item.HeightM*.65,"Emergent plants retain a visible canopy above shallow water");
                        Check(root.WaterSpeedMPerS<=.5,"Emergents do not occupy fast river flow");
                        Check(root.WaterKind!=LocalWaterKind.Ocean||MidFlora.Tidal(root.Biome),"Submerged marine reeds remain inside mapped tidal wetlands");
                    }
                }
            }
            Check(reeds>0,"Actual "+name+" contains emergent vegetation");
            if(name.StartsWith("tidal"))Check(lowTidal>0,"Actual "+name+" supports low intertidal plants");
            totalSubmerged+=submerged;
            records.Add(new{Name=name,Position=new{at.X,at.Y,at.Z},Cell=sample.Cell,Biome=sample.Biome.ToString(),Ground=sample.ElevationM,Water=double.IsFinite(sample.WaterSurfaceM)?(double?)sample.WaterSurfaceM:null,Pages=keys.Count,Instances=all,Reeds=reeds,Submerged=submerged,LowTidal=lowTidal,MaximumDepth=maxDepth});
            Console.WriteLine($"{name}: {reeds} reeds, {submerged} submerged, {lowTidal} low tidal; {keys.Count} pages");
        }
        Check(totalSubmerged>0,"Actual wetland fixtures include rooted submerged emergents");
        Check(StateHash.Compute(world)==before,"Flora derivation does not mutate the broad simulation");
        File.WriteAllText(Path.Combine(output,"world.json"),JsonSerializer.Serialize(new{checks,ElapsedSeconds=timer.Elapsed.TotalSeconds,WorldHash=before,Sites=records},new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(output,"sites.json"),WorldJson.Write(new VegetationBenchmarkSiteSet{HistoryHash=world.Prehistory.Hash,Sites=sites.Select(s=>new VegetationBenchmarkSite{Name=s.Item1,Position=s.Item2,Biome="Wetland"}).ToArray()}));
        Console.WriteLine($"PASS {checks} actual wetland vegetation checks; {totalSubmerged} submerged emergents in {timer.Elapsed.TotalSeconds:F2}s offline audit");
    }
}
