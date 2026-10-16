using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;
using System.Text;

public static class FaunaPackTests
{
    public static void Review(string preset,int seed,int detail,string folder)
    {
        Directory.CreateDirectory(folder);var setup=new WorldSetup();var recipe=setup.Resolve(setup.Preset(preset,new Recipe{Seed=seed,Detail=detail}));recipe.Detail=detail;recipe.EcologyProfile=EarthFaunaPack.Version;
        var time=System.Diagnostics.Stopwatch.StartNew();var world=new WorldPipeline().Generate(recipe);time.Stop();
        int assertions=0;Validate(world,(ok,message)=>{assertions++;if(!ok)throw new Exception(message);});
        File.WriteAllBytes(Path.Combine(folder,"final.world"),WorldFile.Write(world));File.WriteAllText(Path.Combine(folder,"recipe.json"),WorldJson.Write(recipe));
        File.WriteAllText(Path.Combine(folder,"summary.json"),System.Text.Json.JsonSerializer.Serialize(new{Preset=preset,Seed=seed,Detail=detail,GenerationSeconds=time.Elapsed.TotalSeconds,Pack=world.Ecology!.PackVersion,Taxa=world.Ecology.Taxa.Length,Regions=world.Ecology.Regions.Length,
            Counts=world.Ecology.Taxa.Select(t=>new{Name=t.Name,Rank=t.Rank.ToString(),Sources=t.Lineage?.Sources,Cells=world.Surface.Cells.Count(c=>Biogeography.Present(world.Ecology,c.Id,t.Id)),Regions=world.Ecology.Regions.Count(r=>r.Species.Any(p=>p.TaxonId==t.Id))})},new System.Text.Json.JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        foreach(var layer in new[]{Layer.Climate,Layer.Ecoregions,Layer.Biomes})
        {
            var mesh=MapView.Build(world,layer);var b=new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='1600' height='850' viewBox='0 0 1600 850'><rect width='1600' height='850' fill='#10202a'/>");
            b.Append("<text x='20' y='30' fill='white' font-family='sans-serif' font-size='20'>").Append(System.Security.SecurityElement.Escape(preset+" / "+layer+" / native fauna 3 / seed "+seed)).Append("</text>");
            for(int k=0;k<mesh.Triangles.Length;k+=3)
            {
                int at=mesh.Triangles[k]*3;string color=$"#{(int)(255*mesh.Colors[at]):x2}{(int)(255*mesh.Colors[at+1]):x2}{(int)(255*mesh.Colors[at+2]):x2}";
                b.Append("<polygon points='");for(int q=0;q<3;q++){int i=mesh.Triangles[k+q]*3;b.Append(FormattableString.Invariant($"{800+400*mesh.Positions[i]:F2},{450-400*mesh.Positions[i+1]:F2} "));}b.Append("' fill='").Append(color).Append("'/>");
            }
            for(int lat=-60;lat<=60;lat+=30)b.Append(FormattableString.Invariant($"<path d='M0 {450-lat*400/90.0:F1}H1600' stroke='white' stroke-opacity='.2'/><text x='5' y='{445-lat*400/90.0:F1}' fill='white' font-size='13'>{lat}°</text>"));
            b.Append("</svg>");File.WriteAllText(Path.Combine(folder,layer+".svg"),b.ToString());
        }
        foreach(string animal in new[]{"Penguin","Gull","Elephant","Lion","Forest eagle"})
        {
            int id=world.Ecology.Taxa.Single(t=>t.Name==animal).Id;var region=world.Ecology.Regions.FirstOrDefault(r=>r.Species.Any(p=>p.TaxonId==id));
            if(region==null)continue;string safe=animal.Replace(' ','-').ToLowerInvariant();
            int cell=region.Cells.First(i=>Biogeography.Present(world.Ecology,i,id));
            File.WriteAllText(Path.Combine(folder,safe+"-web.svg"),FoodWebDiagram.Svg(world,cell).Replace("<svg ","<svg style='background:#10202a' "));
            File.WriteAllText(Path.Combine(folder,safe+"-habitat.txt"),GlobeView.Describe(world,cell));
        }
        Console.WriteLine("PASS fauna review "+preset+" "+detail+" cells, "+assertions+" assertions, "+time.Elapsed.TotalSeconds.ToString("F1")+" generation seconds.");
    }
    public static void Run(Action<bool,string> check)
    {
        FaunaOriginTests.Run(check);
        var taxa=EarthFaunaPack.Create();Taxon T(string name)=>taxa.Single(t=>t.Name==name);
        check(taxa.Length>=175,"Expanded pack covers broad terrestrial/coastal lineages and functional prey pools");
        check(taxa.Select(t=>t.Id).SequenceEqual(Enumerable.Range(0,taxa.Length)),"Expanded pack IDs are compact and deterministic");
        check(taxa.Select(t=>t.Name).Distinct().Count()==taxa.Length,"No duplicate conceptual animals");
        check(taxa.Count(t=>t.Name=="Elephant")==1&&taxa.Count(t=>t.Name=="Hare")==1&&taxa.Count(t=>t.Name=="Wolf")==1,"Regional elephant/hare/wolf forms are not separate preauthored species slots");
        check(T("Elephant").Rank==TaxonRank.Lineage&&T("Elephant").Lineage!.Sources.Select(s=>s.Realm).Distinct().Count()==2,"Elephant ancestry retains multiple native realms within one lineage");
        foreach(string name in new[]{"Penguin","Gull","Seal","Polar bear","Giraffe","Rhinoceros","Hippopotamus","Camel","Wild cattle","Yak","Gorilla","Orangutan","Sloth","Pangolin","Crocodile","Sea turtle","Kiwi","Platypus","Hummingbird","Whale","Dolphin","Orca","Shark","Dugong","Walrus"})
            check(T(name).Lineage!=null,"Major Earth animal lineage retained: "+name);
        check(AnimalEcotypes.Domesticability(T("Horse"))>5*AnimalEcotypes.Domesticability(T("Zebra")),"Similar equids retain strongly different husbandry opportunities");
        check(AnimalEcotypes.Domesticability(T("Elephant"))<AnimalEcotypes.Domesticability(T("Wild cattle"))/10,"Taming elephants is not easy captive-breeding domestication");
        check(AnimalEcotypes.RidingSuitability(T("Horse"))>0&&AnimalEcotypes.RidingSuitability(T("Camel"))>0&&AnimalEcotypes.RidingSuitability(T("Wild ass"))>0,"Actual riding-capable domesticates have explicit mounted-travel traits");
        check(AnimalEcotypes.RidingSuitability(T("Buffalo"))==0&&AnimalEcotypes.RidingSuitability(T("Wild cattle"))==0&&AnimalEcotypes.RidingSuitability(T("Zebra"))==0,"Traction and a similar body do not turn bovines/zebras into routine cavalry");
        var temperate=new HabitatCell{Habitat=Habitat.Forest|Habitat.Open,Biome=Biome.TemperateForest,ColdTenthsC=0,WarmTenthsC=220,MeanTenthsC=110,AridityMilli=1200};
        var jungle=new HabitatCell{Habitat=Habitat.Forest,Biome=Biome.TropicalRainforest,ColdTenthsC=250,WarmTenthsC=280,MeanTenthsC=265,AridityMilli=1800};
        var dryShore=new HabitatCell{Habitat=Habitat.Open|Habitat.Coastal,Biome=Biome.Grassland,ColdTenthsC=10,WarmTenthsC=180,AridityMilli=700};
        check(!Biogeography.Suitable(T("Hawk"),jungle,100),"Open-hunting temperate hawk is not a hot dense-rainforest apex bird");
        check(Biogeography.Suitable(T("Forest eagle"),jungle,100),"Dense equatorial forest supports a distinct canopy-hunting raptor");
        check(!Biogeography.Suitable(T("Penguin"),temperate,100)&&Biogeography.Suitable(T("Penguin"),dryShore,10),"Penguins require actual coast, not an arbitrary cold inland cell");
        check(!Biogeography.Suitable(T("Hippopotamus"),new HabitatCell{Habitat=Habitat.Open,Biome=Biome.Savanna,ColdTenthsC=160,WarmTenthsC=280,AridityMilli=600},10),"A grazing hippopotamus still requires a fresh-water refuge");
        check(!Biogeography.Suitable(T("Beaver"),temperate,100),"Forest alone does not supply a beaver's freshwater refuge");
        check(Biogeography.Suitable(T("Beaver"),new HabitatCell{Habitat=Habitat.Forest|Habitat.Freshwater,Biome=Biome.TemperateForest,ColdTenthsC=0,WarmTenthsC=220,AridityMilli=1200},100),"Actual riparian freshwater can support the beaver lineage");
        check(Biogeography.Suitable(T("Beaver"),new HabitatCell{Habitat=Habitat.Forest,Biome=Biome.TropicalRainforest,ColdTenthsC=250,WarmTenthsC=280,AridityMilli=1800,FloodFractionPermille=20,SalinityMilliPpt=0},100),"Freshwater dependence is not an invented latitude/tropical exclusion for generic beavers");
        check(Biogeography.CanEat(T("Penguin"),T("Coastal forage fish (guild)")),"Penguin food web includes physically accessible marine fish");
        foreach(string name in new[]{"Penguin","Puffin","Albatross","Cormorant","Seal","Sea lion","Sea turtle","Walrus"})
        {
            check((T(name).Occupies&Stratum.Ground)!=0&&T(name).Hunts==Stratum.Water,"Coastal nesting/haul-out does not grant land foraging: "+name);
            check(!Biogeography.CanEat(T(name),T("Native litter beetles (guild)"))&&!Biogeography.CanEat(T(name),T("Ants and termites (guild)")),"Marine feeder does not eat terrestrial insect pools: "+name);
            check(Biogeography.CanEat(T(name),T("Coastal crustaceans and molluscs (guild)"))||Biogeography.CanEat(T(name),T("Coastal forage fish (guild)")),"Marine feeder retains a compatible actual marine resource: "+name);
        }
        check(Biogeography.CanEat(T("Sea turtle"),T("Coastal algae and seagrass (feeding pool)"))&&!Biogeography.CanEat(T("Sea turtle"),T("Flowering herbs (guild)"))&&!Biogeography.CanEat(T("Sea turtle"),T("Woody understory (guild)"))&&!Biogeography.CanEat(T("Sea turtle"),T("Red mangrove")),"Marine-grazing turtle uses algae/seagrass, not terrestrial leaves merely sharing a coastal cell");
        check(Biogeography.CanEat(T("Gull"),T("Native litter beetles (guild)"))&&Biogeography.CanEat(T("Shorebird"),T("Ants and termites (guild)")),"Actual shore-ground foragers retain terrestrial opportunities");
        check(Biogeography.CanEat(T("Coastal forage fish (guild)"),T("Coastal crustaceans and molluscs (guild)"))&&Biogeography.CanEat(T("Coastal crustaceans and molluscs (guild)"),T("Coastal phytoplankton (feeding pool)")),"Coastal consumers have a genuine basal-rooted marine resource chain");
        check(!Biogeography.CanEat(T("Wolf"),T("Hawk")),"Ground predators do not routinely prey on aerial adult raptors");
        check(Biogeography.CanEat(T("Forest eagle"),T("Capuchin")),"Occasional vertebrate-eating capuchins are not incorrectly treated as apex predators immune to forest eagles");
        check(Biogeography.CanEat(T("Secretary bird"),T("Venomous snake")),"Explicit snake-handling technique permits a size-compatible venomous-snake feeding opportunity");
        check(!Biogeography.CanEat(T("Secretary bird"),T("Python"))&&!Biogeography.CanEat(T("Secretary bird"),T("Boa constrictor")),"Snake handling does not bypass routine prey-size limits");
        check(!Biogeography.CanEat(T("Wolf"),T("Lion"))&&!Biogeography.CanEat(T("Wolf"),T("Bear"))&&!Biogeography.CanEat(T("Lion"),T("Tiger")),"Dangerous adult predators and large omnivores retain the conservative counterattack gate");
        check(!Biogeography.CanEat(T("Wolf"),T("Capuchin")),"A valid prey body size does not grant ground hunters access to canopy occupants");
        var monkeyDefense=T("Capuchin").Lineage!.Predation;T("Capuchin").Lineage!.Predation=null;
        check(!Biogeography.CanEat(T("Forest eagle"),T("Capuchin")),"A saved old taxon with null defense traits retains exactly the legacy predatory-prey rule");
        T("Capuchin").Lineage!.Predation=monkeyDefense;
        var legacyLineage=new AnimalLineage{Key="legacy-fixture",Sources=new[]{new AnimalNativeSource{Realm="test",Latitude=10,Longitude=20,OriginCell=4}}};
        check(StateHash.Compute(legacyLineage)=="11a1629226f9ce39095ba86218052b8b046794f7aa314cd3bd9fb826b7bc4c9e","Null new predation traits preserve the independently measured old lineage canonical hash");
        check(Biogeography.CanEat(T("Giraffe"),T("Woody understory (guild)")),"Giraffe browsing can reach canopy and understory producers");
        check(Biogeography.CanEat(T("Aardvark"),T("Ants and termites (guild)")),"Specialized termite hunters have soil-accessible insect prey");
        var cold=new HabitatCell{Habitat=Habitat.Open|Habitat.Tundra,Biome=Biome.Tundra,ColdTenthsC=-280,WarmTenthsC=140,MeanTenthsC=-60,AridityMilli=800};
        var hot=new HabitatCell{Habitat=Habitat.Open|Habitat.Desert,Biome=Biome.Desert,ColdTenthsC=180,WarmTenthsC=330,MeanTenthsC=250,AridityMilli=100};
        var world=new World{Recipe=new Recipe{Seed=42},Ecology=new EcologyResult{Habitat=new[]{cold,hot,jungle}}};
        var region=new Ecoregion{Id=0,ProvinceId=7,RepresentativeCell=0};var snow=AnimalEcotypes.Create(world,T("Hare"),region);
        region.RepresentativeCell=1;var desert=AnimalEcotypes.Create(world,T("Hare"),region);
        check(snow.InsulationPermille>desert.InsulationPermille&&snow.Ecotype!.EarSizePermille<desert.Ecotype!.EarSizePermille,"Regional cold/warm hares develop coherent insulation/ear differences");
        check(snow.SeasonalWhiteningPermille>500&&desert.SeasonalWhiteningPermille==0,"Seasonal white forms emerge only in eligible cold seasonal lineages");
        region.RepresentativeCell=2;var forestElephant=AnimalEcotypes.Create(world,T("Elephant"),region);region.RepresentativeCell=1;var openElephant=AnimalEcotypes.Create(world,T("Elephant"),region);
        check(forestElephant.MassKg<openElephant.MassKg,"Closed-forest elephant forms tend smaller than exposed warm-country forms");
        check(StateHash.Compute(openElephant)==StateHash.Compute(AnimalEcotypes.Create(world,T("Elephant"),region)),"Ecotypes are query-order-independent deterministic realizations");
        check(StateHash.Compute(taxa)==StateHash.Compute(EarthFaunaPack.Create()),"New native lineage catalog is reproducible and not shared mutable state");
        check(NativeLifePack.Create().Length==96&&EarthLifePack.Create().Length==50,"Historical native packs retain their original contents");
        check(StateHash.Compute(EarthLifePack.Create())=="4703ebda57f51ea085de98c2800eb0f60cdd9938fbb87bde357ef004f5eb6c9b"&&StateHash.Compute(NativeLifePack.Create())=="16b2fbfbfe76e58bc09e14df04dc1012e1defb296f1a4e32bc5d7003d7a42d6e","Coastal feeding correction does not change original/native-2 catalog hashes");
        Console.WriteLine("Native fauna-3: "+taxa.Length+" entries, "+taxa.Count(t=>t.Rank==TaxonRank.Lineage)+" animal lineages.");
    }
    public static void Validate(World world,Action<bool,string> check)
    {
        var e=world.Ecology!;if(e.PackVersion!=EarthFaunaPack.Version)return;
        check(e.Taxa.Length==203,"Fresh fauna-3 world persists the complete broad catalog");
        check(e.Taxa.All(t=>t.Lineage==null||t.Rank==TaxonRank.FunctionalGuild||t.Lineage.Sources.All(s=>s.OriginCell<0||s.OriginCell<e.Habitat.Length&&!world.Hydrology.OceanConnected[s.OriginCell]&&Biogeography.Suitable(t,e.Habitat[s.OriginCell],world.ElevationM[s.OriginCell]))),"Every extant lineage source is a suitable actual terrestrial/coastal site");
        check(world.Surface.Cells.All(c=>e.Taxa.All(t=>!Biogeography.Present(e,c.Id,t.Id)||t.Lineage==null||!t.Lineage.RequiresCoast||c.Neighbors.Any(i=>world.Hydrology.OceanConnected[i]))),"Coastal-feeding fauna never occur in inland cells");
        check(e.Regions.All(r=>r.Species.All(p=>e.Taxa[p.TaxonId].Traits==null||p.Variant?.Ecotype?.LineageKey==e.Taxa[p.TaxonId].Lineage?.Key)),"Every retained animal has a generated matching regional ecotype");
        check(e.Regions.All(r=>r.Species.All(p=>p.Variant?.Ecotype==null||Biogeography.Present(e,p.Variant.Ecotype.SampleCell,p.TaxonId))),"Ecotype habitat is sampled where the animal actually occurs, not an unoccupied inland regional representative");
        check(e.Regions.All(r=>r.Species.All(p=>p.Variant==null||p.Variant.MassKg>0&&p.Variant.Ecotype!.BodySizePermille>=450&&p.Variant.Ecotype.BodySizePermille<=1700)),"Regional mass/shape variation remains bounded and positive");
        var marineOnly=new HashSet<string>(new[]{"Penguin","Puffin","Albatross","Cormorant","Seal","Sea lion","Sea turtle","Walrus"});
        check(e.Regions.All(r=>r.FoodWeb.All(link=>!marineOnly.Contains(e.Taxa[link.ConsumerId].Name)||e.Taxa[link.ResourceId].BasalResource||(e.Taxa[link.ResourceId].Occupies&Stratum.Water)!=0)),"Actual realized coastal webs have no terrestrial food links for marine-only feeders");
        var restored=WorldFile.Read(WorldFile.Write(world));
        check(StateHash.Compute(restored.Ecology)==StateHash.Compute(e),"Complete lineage, provenance, phenotype and food-web state round trips");
        int secretary=e.Taxa.Single(t=>t.Name=="Secretary bird").Id;
        check(restored.Ecology.Taxa[secretary].Lineage.Predation.SnakeHandling&&!ReferenceEquals(restored.Ecology.Taxa[secretary].Lineage.Predation,e.Taxa[secretary].Lineage.Predation),"Saved predation techniques survive as independent copied records, not shared mutable catalog state");
        foreach(string name in new[]{"Penguin","Gull","Elephant","Horse","Lion","Forest eagle"})
        {
            int id=e.Taxa.Single(t=>t.Name==name).Id;
            Console.WriteLine("Fauna-3 "+name+": "+e.Regions.Count(r=>r.Species.Any(p=>p.TaxonId==id))+" regions, "+world.Surface.Cells.Count(c=>Biogeography.Present(e,c.Id,id))+" cells.");
        }
    }
}
