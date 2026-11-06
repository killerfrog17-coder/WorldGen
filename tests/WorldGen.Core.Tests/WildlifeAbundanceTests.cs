using System;
using System.IO;
using System.Linq;
using System.Threading;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class WildlifeAbundanceTests
{
    static Taxon Animal(int id,double mass,Food eats,Habitat habitat=Habitat.Forest|Habitat.Open,Stratum occupies=Stratum.Ground,Stratum hunts=Stratum.Ground,Food provides=Food.Vertebrate)
        =>new Taxon{Id=id,Name="Fixture "+id,MassKg=mass,Eats=eats,Habitats=habitat,Occupies=occupies,Hunts=hunts,Provides=provides,MinPreyKg=.001,MaxPreyKg=1000};
    public static void Run()
    {
        int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        var forest=new WildlifeHabitat{Productivity=1,Canopy=.7,Shrubs=.3,Grass=.25,Browse=.65,Fruit=.6,Seed=.4,Nectar=.2,Invertebrates=.8,Detritus=.6};
        var workspace=new WildlifeAbundance.Workspace();var random=new Random(681103);
        var output=new WildlifeCommunity();
        WildlifeCommunity retained=null;double[] retainedValues=null;
        for(int step=0;step<240;step++)
        {
            var population=new Taxon[step%43];
            for(int i=0;i<population.Length;i++)
            {
                if(i%13==0)continue;
                population[i]=Animal(random.Next(-3,18),i%17==0?double.NaN:i%11==0?0:.01+random.NextDouble()*250,(Food)random.Next(1,256));
                population[i].Producer=i%19==0;population[i].BasalResource=i%23==0;
            }
            var habitat=forest;habitat.Productivity=step%9==0?double.NaN:random.NextDouble()*3;habitat.Canopy=random.NextDouble();habitat.Grass=random.NextDouble();habitat.Browse=random.NextDouble();
            var original=WildlifeAbundance.Allocate(population,habitat);var reused=WildlifeAbundance.Allocate(population,habitat,workspace);
            WildlifeAbundance.AllocateInto(population,habitat,workspace,output);
            Check(original.DensityPerKm2.Keys.SequenceEqual(output.DensityPerKm2.Keys),"Reusable output removes all previous taxon identities");
            foreach(int id in original.DensityPerKm2.Keys)Check(BitConverter.DoubleToInt64Bits(original.Density(id))==BitConverter.DoubleToInt64Bits(output.Density(id)),"Reusable output preserves each density bit");
            Check(original.PrimaryCapacity==output.PrimaryCapacity&&original.PrimaryAllocated==output.PrimaryAllocated&&original.PreyBiomassKg==output.PreyBiomassKg&&original.PredatorSupportedBiomassKg==output.PredatorSupportedBiomassKg,"Reusable output resets all resource totals");
            Check(original.DensityPerKm2.Keys.SequenceEqual(reused.DensityPerKm2.Keys),"Reusable population storage preserves filtered, unique, sorted taxon identity");
            foreach(int id in original.DensityPerKm2.Keys)Check(BitConverter.DoubleToInt64Bits(original.Density(id))==BitConverter.DoubleToInt64Bits(reused.Density(id)),"Reusable population storage preserves every density bit");
            Check(original.PrimaryCapacity==reused.PrimaryCapacity&&original.PrimaryAllocated==reused.PrimaryAllocated&&original.PreyBiomassKg==reused.PreyBiomassKg&&original.PredatorSupportedBiomassKg==reused.PredatorSupportedBiomassKg,"Reusable population storage preserves resource and predator totals");
            if(retained!=null)Check(retained.DensityPerKm2.Values.SequenceEqual(retainedValues),"A later population calculation overwrote an earlier community result");
            retained=reused;retainedValues=reused.DensityPerKm2.Values.ToArray();
        }
        var deer=Animal(1,100,Food.Browse);var elk=Animal(2,100,Food.Browse);var wolf=Animal(3,40,Food.Vertebrate);var cat=Animal(4,40,Food.Vertebrate);
        var allocationPopulation=new[]{deer,elk,wolf,cat};
        for(int i=0;i<10;i++){WildlifeAbundance.Allocate(allocationPopulation,forest,workspace);WildlifeAbundance.AllocateInto(allocationPopulation,forest,workspace,output);}
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1000;i++)WildlifeAbundance.Allocate(allocationPopulation,forest,workspace);
        long freshBytes=GC.GetAllocatedBytesForCurrentThread()-before;before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1000;i++)WildlifeAbundance.AllocateInto(allocationPopulation,forest,workspace,output);
        long reusedBytes=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(freshBytes>0&&reusedBytes<freshBytes/4,"Owned output storage removes repeated community dictionary growth");
        Console.WriteLine($"Community output allocations: fresh {freshBytes}, reused {reusedBytes} bytes /1000 calculations.");
        var alone=WildlifeAbundance.Allocate(new[]{deer},forest);var together=WildlifeAbundance.Allocate(new[]{deer,elk},forest);
        Check(Math.Abs(alone.Density(1)-together.Density(1)-together.Density(2))<1e-10,"Adding an equivalent browser shares capacity instead of doubling it");
        Check(together.Density(1)<alone.Density(1),"An unrendered ecological competitor still reduces the represented deer's allowance");
        Check(together.PrimaryAllocated<=together.PrimaryCapacity+1e-9,"Primary resource allocation is conserved");
        var noBrowse=forest;noBrowse.Browse=0;var hungry=WildlifeAbundance.Allocate(new[]{deer,wolf},noBrowse);
        Check(hungry.Density(1)==0&&hungry.Density(3)==0,"Without browse neither the browser nor its dependent predator receives a population");
        var sparse=forest;sparse.Browse=.1;
        Check(WildlifeAbundance.Allocate(new[]{deer,wolf},sparse).Density(3)<WildlifeAbundance.Allocate(new[]{deer,wolf},forest).Density(3),"Reduced prey food reduces supported predator abundance");
        var hunters=WildlifeAbundance.Allocate(new[]{deer,wolf,cat},forest);
        double carnivoreMass=hunters.Density(3)*40+hunters.Density(4)*40;
        Check(Math.Abs(carnivoreMass-hunters.PreyBiomassKg*.009)<1e-9,"Predators share each eligible prey biomass pool once");
        Check(hunters.Density(3)<hunters.Density(1),"Large carnivores remain scarcer than their herbivore prey");
        wolf.MaxPreyKg=10;Check(WildlifeAbundance.Allocate(new[]{deer,wolf},forest).Density(3)==0,"Inedible large prey does not support an inappropriate predator");wolf.MaxPreyKg=1000;
        var fish=Animal(10,.05,Food.Invertebrate,Habitat.Coastal,Stratum.Water,Stratum.Water,Food.Fish);
        var bird=Animal(11,1,Food.Fish,Habitat.Coastal,Stratum.Air,Stratum.Water);
        var coast=new WildlifeHabitat{Productivity=1,Coast=1,Water=1,Invertebrates=1};var seabirds=WildlifeAbundance.Allocate(new[]{fish,bird},coast);
        Check(seabirds.Density(10)>0&&seabirds.Density(11)>0,"Fish-only diets use a prey-supported population, not the old general bird allowance");
        Check(seabirds.Density(11)*bird.MassKg<=seabirds.Density(10)*fish.MassKg*.009+1e-9,"Fish-eating birds cannot bypass their shared prey budget");
        var dry=forest;dry.Freshwater=dry.Coast=dry.Wetland=dry.Water=0;
        var beaver=Animal(20,20,Food.Browse);beaver.Lineage=new AnimalLineage{RequiresFreshwater=true};
        Check(dry.Fit(beaver)==0,"Freshwater-dependent animals are rejected away from local freshwater");
        var woodlandBird=Animal(21,.1,Food.Invertebrate,Habitat.Forest,Stratum.Air);woodlandBird.Lineage=new AnimalLineage{MinCanopyPermille=500};
        var open=forest;open.Canopy=open.Shrubs=0;Check(open.Fit(woodlandBird)==0,"A forest bird does not occupy a treeless hillside just because the wider region contains forest");
        Check(forest.Fit(woodlandBird)>0,"The same forest bird has a usable woodland niche");
        Check(WildlifeBehaviour.IsNocturnal("civet")&&WildlifeBehaviour.IsNocturnal("badger")&&!WildlifeBehaviour.IsNocturnal("deer"),"Broad nocturnal species do not all roam in full daylight");
        var vegetation=new LocalTerrainSample{Biome=Biome.TemperateForest,WaterSurfaceM=double.NaN,Forest=new ForestCoverProfile{Coverage=.7},
            MidCover=new MidCoverProfile{Coverage=.3},Cover=new GroundCoverProfile{Grasses=new V(.6,0,0),Herbs=new V(.2,0,0)}};
        var climate=new HabitatCell{ProductivityGCM2Year=600,WarmTenthsC=250};
        var summer=WildlifeHabitat.From(vegetation,climate,.7,0,20);var winter=WildlifeHabitat.From(vegetation,climate,.7,0,-5);
        Check(winter.Grass<summer.Grass&&winter.Invertebrates<summer.Invertebrates&&winter.Browse>0,"Cold reduces seasonal food without removing all evergreen/twig browsing");
        Check(winter.Nectar==0&&summer.Nectar>0,"Freezing conditions do not retain summer nectar supply");
        var bare=forest;bare.Productivity=0;Check(WildlifeAbundance.Allocate(new[]{deer,wolf,woodlandBird},bare).DensityPerKm2.Values.All(v=>v==0),"Bare zero-productivity habitat does not receive an artificial minimum animal population");
        var invalid=forest;invalid.Productivity=double.NaN;Check(WildlifeAbundance.Allocate(new[]{deer,wolf},invalid).DensityPerKm2.Values.All(v=>v==0),"Invalid productivity cannot create nonfinite populations");
        foreach(double minutes in new[]{1d,5,15,60,240,1440,2880})
        {
            double effective=WildlifeAbundance.EffectiveDayMinutes(minutes),pace=WildlifeAbundance.EncounterPace(minutes);
            Check(effective>=15&&effective<=1440,"Effective wildlife day respects bounds: "+minutes);
            Check(pace>=1&&pace<=Math.Sqrt(96)+1e-10,"Encounter travel has bounded sublinear compression: "+minutes);
            Check(WildlifeAbundance.TravelPace(minutes,100,250)==1&&WildlifeAbundance.TravelPace(minutes,375,250)==1,"Nearby and potentially visible groups never accelerate: "+minutes);
            Check(Math.Abs(WildlifeAbundance.TravelPace(minutes,10000,250)-pace)<1e-10,"Only distant travel receives encounter pacing: "+minutes);
        }
        Check(WildlifeAbundance.EncounterPace(1)==WildlifeAbundance.EncounterPace(15),"A one-minute day uses fifteen-minute encounter pacing");
        Check(WildlifeAbundance.EncounterPace(1440)==WildlifeAbundance.EncounterPace(2880),"Days longer than24hours retain24hour encounter pacing");
        var longDay=new PlaySettings{RealMinutesPerDay=2880};longDay.Validate();Check(longDay.RealMinutesPerDay==2880,"The encounter clamp does not restrict the actual day-length setting");
        Check(WildlifeAbundance.ExposedFraction(.1,true,false)<1&&WildlifeAbundance.ExposedFraction(.1,true,false)>0&&WildlifeAbundance.ExposedFraction(.1,false,false)<1,"Visible proposals represent exposed activity instead of placing every resting resident in view");
        double last=1;for(int distance=0;distance<=1000;distance+=20)
        {double pace=WildlifeAbundance.TravelPace(60,distance,250);Check(pace>=last&&pace-last<1,"Offscreen speed transition is continuous and monotonic");last=pace;}
        var reversed=WildlifeAbundance.Allocate(new[]{cat,wolf,elk,deer},forest);var ordered=WildlifeAbundance.Allocate(new[]{deer,elk,wolf,cat},forest);
        Check(ordered.DensityPerKm2.All(p=>Math.Abs(reversed.Density(p.Key)-p.Value)<1e-12),"Community allocation is independent of species iteration order");
        var state=new WildlifeState{PopulationVersion=WildlifeAbundance.Version};var copy=state.Copy();var restored=WorldJson.Read<WildlifeState>(WorldJson.Write(state));restored.Validate();
        Check(copy.PopulationVersion==state.PopulationVersion&&restored.PopulationVersion==state.PopulationVersion,"Population projection version is deep-copy/save compatible");
        Console.WriteLine("PASS "+checks+" wildlife resource, competition, habitat and encounter-pacing checks");
    }
    public static void VerifyPlannerReuse(string path,string baseline=null,bool record=false)
    {
        var world=WorldFile.ReadFile(path);var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var profiles=WorldJson.Read<FaunaPrototypeCatalogue>(File.ReadAllText("unity/WorldGen.Unity/Assets/WorldGen/Resources/WorldGenFaunaCatalogue.json")).animals;
        var reused=new FaunaPrototypePlanner(world,terrain,profiles,.1);
        System.Collections.Generic.List<FaunaPrototypeSpawn> retained=null;string retainedText=null;int count=0;
        var records=new List<string>();long bytes=0;
        foreach(var site in new[]{(27.068410281540114,-167.35387593971296,12d,15d,.25),
            (27.068410281540114,-167.35387593971296,21d,7d,.75),
            (0d,25d,6d,27d,.4),(50d,10d,15d,-5d,.9),
            (27.068410281540114,-167.35387593971296,12d,15d,.25)})
        {
            var point=TerrainMapViewport.FromDegrees(site.Item1,site.Item2);
            var fresh=new FaunaPrototypePlanner(world,terrain,profiles,.1);
            var expected=fresh.Build(point,.1,site.Item3,CancellationToken.None,site.Item5,site.Item4);
            long before=GC.GetAllocatedBytesForCurrentThread();
            var actual=reused.Build(point,.1,site.Item3,CancellationToken.None,site.Item5,site.Item4);
            bytes+=GC.GetAllocatedBytesForCurrentThread()-before;records.Add(WorldJson.Write(actual));
            if(WorldJson.Write(expected)!=WorldJson.Write(actual))throw new Exception("Reused planner changed complete spawn data");
            if(retained!=null&&WorldJson.Write(retained)!=retainedText)throw new Exception("Later build mutated retained spawns");
            retained=actual;retainedText=WorldJson.Write(actual);count+=actual.Count;
            try{reused.Build(point,.1,site.Item3,new CancellationToken(true),site.Item5,site.Item4);throw new Exception("Cancellation ignored");}
            catch(OperationCanceledException){}
        }
        if(count==0)throw new Exception("No actual-world spawn coverage");
        if(baseline!=null)
        {
            string encoded=WorldJson.Write(records);
            if(record)File.WriteAllText(baseline,encoded);
            else if(File.ReadAllText(baseline)!=encoded)throw new Exception("Complete spawn records changed from the independent pre-change planner baseline");
        }
        Console.WriteLine($"PASS {count} complete actual-world spawn records across five changing site/time/weather scenarios, retained results and cancellation; reused Build allocated {bytes} bytes.");
    }
    public static void WorldReport(string path,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.Read(File.ReadAllBytes(path));
        var profiles=WorldJson.Read<FaunaPrototypeCatalogue>(File.ReadAllText("unity/WorldGen.Unity/Assets/WorldGen/Resources/WorldGenFaunaCatalogue.json")).animals;
        var rows=new System.Collections.Generic.List<object>();
        foreach(double scale in new[]{.1,1d})
        {
            var terrain=new LocalTerrain(world,horizontalScale:scale);var planner=new FaunaPrototypePlanner(world,terrain,profiles,scale);
            foreach(var at in new[]{(50d,10d),(0d,25d),(.18,117.31),(25d,10d)})
            {
                var point=TerrainMapViewport.FromDegrees(at.Item1,at.Item2);var sample=terrain.Patch(point,100/scale).Sample(point);var climate=world.Ecology.Habitat[sample.Cell];
                var timer=System.Diagnostics.Stopwatch.StartNew();var population=planner.Build(point,scale,12,CancellationToken.None,.25,climate.MeanTenthsC/10d);timer.Stop();
                var visible=population.Where(p=>(p.Position-point).Length*terrain.RadiusM*scale<=p.Profile.VisibleRadiusM).ToArray();
                var site=WildlifeHabitat.From(sample,climate,TreeFlora.CanopyShade(sample.Forest,point,terrain.SourceSeed,terrain.RadiusM*scale),0,climate.MeanTenthsC/10d);
                int region=world.Ecology.Region[sample.Cell];var community=WildlifeAbundance.Allocate(world.Ecology.Regions[region].Species.Select(p=>world.Ecology.Taxa[p.TaxonId]),site);
                var bindings=FaunaPrototypePlanner.BindProfiles(world.Ecology.Taxa,profiles);
                var legacyType=typeof(WildlifeAbundanceTests).Assembly.GetType("WorldGen.Presentation.LegacyFaunaPrototypePlanner");
                int oldVisible=-1;
                if(legacyType!=null)
                {
                    var legacy=Activator.CreateInstance(legacyType,world,terrain,profiles,scale);
                    var old=(System.Collections.Generic.List<FaunaPrototypeSpawn>)legacyType.GetMethod("Build").Invoke(legacy,new object[]{point,scale,12d,CancellationToken.None,.25,climate.MeanTenthsC/10d});
                    oldVisible=old.Count(p=>(p.Position-point).Length*terrain.RadiusM*scale<=p.Profile.VisibleRadiusM);
                }
                var row=new{scale,latitude=at.Item1,longitude=at.Item2,biome=sample.Biome.ToString(),npp=climate.ProductivityGCM2Year,canopy=sample.Forest.Coverage,tracked=population.Count,visible=visible.Length,
                    birds=visible.Count(p=>p.Profile.medium=="air"&&!p.Profile.tiny),largeGround=visible.Count(p=>p.Profile.medium=="ground"&&p.Profile.lengthM>=.5),milliseconds=timer.ElapsedMilliseconds,
                    oldVisible,localCanopy=site.Canopy,grass=site.Grass,browse=site.Browse,
                    densities=community.DensityPerKm2.Where(p=>p.Value>0&&bindings.ContainsKey(p.Key)).ToDictionary(p=>world.Ecology.Taxa[p.Key].Name,p=>p.Value),
                    species=visible.GroupBy(p=>p.Profile.slug).ToDictionary(g=>g.Key,g=>g.Count())};rows.Add(row);Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(row));
            }
        }
        File.WriteAllText(Path.Combine(output,"population-world.json"),System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    }
}
