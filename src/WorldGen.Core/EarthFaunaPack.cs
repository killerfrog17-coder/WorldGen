using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    /// <summary>Broad, independently authored Earth animal lineages and local
    /// functional guilds. Not an exhaustive species inventory or range atlas.</summary>
    public static class EarthFaunaPack
    {
        public const string Version="earth-native-3";
        public static Taxon[] Create()
        {
            var old=NativeLifePack.Create();var list=new List<Taxon>();
            var lineages=new Dictionary<string,Taxon>(StringComparer.Ordinal);
            foreach(var t in old)
            {
                if(t.Producer||t.BasalResource||t.Rank==TaxonRank.FunctionalGuild){t.Id=list.Count;list.Add(t);continue;}
                string key=Family(t.Name),name=key;
                if(lineages.TryGetValue(key,out var first))
                {
                    first.Habitats|=t.Habitats;first.MinColdTenthsC=Math.Min(first.MinColdTenthsC,t.MinColdTenthsC);
                    first.MaxColdTenthsC=Math.Max(first.MaxColdTenthsC,t.MaxColdTenthsC);first.MinWarmTenthsC=Math.Min(first.MinWarmTenthsC,t.MinWarmTenthsC);
                    first.MaxWarmTenthsC=Math.Max(first.MaxWarmTenthsC,t.MaxWarmTenthsC);first.MinAridityMilli=Math.Min(first.MinAridityMilli,t.MinAridityMilli);
                    first.Lineage.Sources=first.Lineage.Sources.Concat(new[]{Source(t.OriginGroup,t.EarthLatitude,t.EarthLongitude)}).GroupBy(s=>s.Realm).Select(g=>g.First()).ToArray();
                    continue;
                }
                t.Id=list.Count;t.Name=name;t.Rank=TaxonRank.Lineage;t.ParentSpecies="";
                t.Lineage=new AnimalLineage{Key=key.ToLowerInvariant().Replace(' ','-'),Sources=new[]{Source(t.OriginGroup,t.EarthLatitude,t.EarthLongitude)}};
                if(name=="Elephant"){t.ScientificName="Elephantidae lineage";t.MassKg=3900;t.Traits.CaptiveBreeding=120;t.Traits.MaturityMonths=144;t.Lineage.EarVariationPermille=400;t.Lineage.DisplayTrait="tusks";t.Lineage.BodyVariationPermille=350;}
                if(name=="Hare"){t.ScientificName="Lepus lineage";t.MassKg=3;t.MaxColdTenthsC=150;t.Traits.SeasonalWhite=true;t.Lineage.EarVariationPermille=350;}
                if(name=="Wolf"){t.ScientificName="Canis lupus lineage";t.MassKg=40;t.Traits.SocialTolerance=900;t.Traits.CaptiveBreeding=700;t.Traits.Docility=450;t.Traits.PanicResistance=650;t.Traits.DietFlexibility=650;}
                if(name=="Deer"){t.ScientificName="Cervidae browsing lineage";t.MassKg=100;t.Lineage.DisplayTrait="antlers";t.Lineage.BodyVariationPermille=350;}
                if(name=="Mouse"){t.ScientificName="Small murid/peromyscine lineage";t.MassKg=.025;}
                if(name=="Bear"){t.ScientificName="Ursidae omnivorous lineage";t.MassKg=180;t.Lineage.BodyVariationPermille=350;}
                if(name=="Forest eagle"){t.ScientificName="Forest-eagle lineage";t.Lineage.MinCanopyPermille=500;t.MaxColdTenthsC=600;}
                if(name=="Hawk"){t.ScientificName="Buteonine hawk lineage";t.MaxColdTenthsC=160;t.Lineage.MaxCanopyPermille=750;}
                if(name=="Horse")t.ScientificName="Equus horse lineage";
                if(name=="Rabbit"){t.ScientificName="Oryctolagus lineage";t.Lineage.EarVariationPermille=300;}
                if(name=="Tiger"||name=="Jaguar"||name=="Leopard")t.Lineage.Pattern=name=="Tiger"?"striped":"rosetted";
                list.Add(t);lineages.Add(key,t);
            }
            Taxon Add(string name,string latin,string realm,Habitat habitat,double mass,Food diet,int cold,int warm,int aridity,double lat,double lon,
                Stratum occupies=Stratum.Ground,Stratum hunts=Stratum.Ground,int maxCold=600)
            {
                var t=new Taxon{Id=list.Count,Name=name,ScientificName=latin+" lineage",Rank=TaxonRank.Lineage,OriginGroup=realm,Habitats=habitat,MassKg=mass,Eats=diet,Provides=Food.Vertebrate,
                    Occupies=occupies,Hunts=hunts,MinColdTenthsC=cold,MaxColdTenthsC=maxCold,MinWarmTenthsC=warm,MaxWarmTenthsC=450,MinAridityMilli=aridity,PassageColdTenthsC=cold-50,
                    MaxFloodDays=240,MinPreyKg=.000001,MaxPreyKg=Math.Max(.004,mass*.35),MaxGapKm=350,SpreadBudgetKm=18000,EarthLatitude=lat,EarthLongitude=lon,
                    Traits=new AnimalTraits{DietFlexibility=500,CaptiveBreeding=250,Docility=250,PanicResistance=300,SocialTolerance=350,MaturityMonths=mass>300?72:mass>10?30:12},
                    Lineage=new AnimalLineage{Key=name.ToLowerInvariant().Replace(' ','-'),Sources=new[]{Source(realm,lat,lon)}}};
                list.Add(t);return t;
            }
            Taxon Guild(string name,string latin,Habitat habitat,double mass,Food diet,Food provides,Stratum occupied,Stratum hunts,int cold=-500,int warm=30,int ai=0)
            {var t=Add(name,latin,"widespread",habitat,mass,diet,cold,warm,ai,0,0,occupied,hunts);t.Rank=TaxonRank.FunctionalGuild;t.Provides=provides;t.Traits=null;t.MaxFloodDays=365;return t;}
            var f=Habitat.Forest;var o=Habitat.Open;var d=Habitat.Desert;var a=Habitat.Alpine;var u=Habitat.Tundra;var w=Habitat.Wetland|Habitat.Freshwater;
            var g=Stratum.Ground;var c=Stratum.Canopy|Stratum.Understory;var air=Stratum.Air;var water=Stratum.Water;
            var browse=Food.Browse|Food.Grass;var fruit=Food.Fruit|Food.Seed;var omni=fruit|Food.Invertebrate;var meat=Food.Vertebrate;var bugs=Food.Invertebrate;
            // Ground, canopy, nocturnal and open-country consumers are distinct;
            // each uses actual strata/size/diet links instead of a trophic tier.
            Add("Giraffe","Giraffidae","afrotropical",o,900,Food.Browse,50,200,170,1,35,g,c).Lineage.DisplayTrait="ossicones";
            Add("Rhinoceros","Rhinocerotidae","afrotropical",o|f|w,1500,browse,60,200,220,-4,30).Lineage.DisplayTrait="horns";
            Add("Hippopotamus","Hippopotamidae","afrotropical",w|o,1400,Food.Grass,100,220,200,-2,30,g|water,g).Lineage.RequiresFreshwater=true;
            Add("Gazelle","Gazellinae","afrotropical",o|d,28,browse,-20,180,70,16,25).Lineage.DisplayTrait="horns";
            Add("Oryx","Oryx","afrotropical",o|d,150,browse,-20,180,30,20,20).Lineage.DisplayTrait="horns";
            Add("Warthog","Phacochoerus","afrotropical",o,80,browse|bugs,50,190,170,-5,30).Lineage.DisplayTrait="tusks";
            Add("Baboon","Papio","afrotropical",o|f,22,omni|Food.Browse,20,150,150,-5,30,g|c,g|c);
            Add("Chimpanzee","Pan","afrotropical",f,45,omni|Food.Browse|meat,170,230,900,0,25,g|c,g|c).Lineage.MinCanopyPermille=500;
            Add("Gorilla","Gorilla","afrotropical",f,130,Food.Browse|Food.Fruit,130,190,1000,0,25,g|c,g|c).Lineage.MinCanopyPermille=500;
            Add("Lemur","Lemuriformes","malagasy",f,2.5,omni|Food.Browse,100,180,600,-20,47,c,c);
            Add("Meerkat","Suricata","afrotropical",o|d,.7,bugs|meat,0,180,70,-25,23).MaxPreyKg=.08;
            Add("Mongoose","Herpestidae","afrotropical",o|f,1.4,bugs|meat,50,170,180,0,30).MaxPreyKg=.5;
            Add("Aardvark","Orycteropus","afrotropical",o|f|d,55,bugs,0,170,100,-12,30,g,Stratum.Soil|g).MaxPreyKg=.001;
            Add("Pangolin","Pholidota","afrotropical",f|o,6,bugs,100,200,500,0,20,g|c,Stratum.Soil|g|c).MaxPreyKg=.0005;
            Add("African hunting dog","Lycaon","afrotropical",o,25,meat,40,180,170,-12,25).MaxPreyKg=180;
            Add("Ostrich","Struthio","afrotropical",o|d,100,Food.Seed|Food.Browse|bugs,0,180,50,-20,25);
            Add("Secretary bird","Sagittarius","afrotropical",o,4,meat|bugs,30,180,180,-5,30,g|air,g).MaxPreyKg=2;
            Add("Hornbill","Bucerotidae","afrotropical",f,2,fruit|bugs|meat,120,210,700,0,25,c|air,c).MaxPreyKg=.15;
            Add("Turaco","Musophagidae","afrotropical",f,.35,Food.Fruit|Food.Browse,140,210,850,0,25,c|air,c);
            Add("Vulture","Accipitridae scavenger","afrotropical",o|d|a,7,meat,0,160,80,12,30,g|air,g).MaxPreyKg=2500;
            Add("Camel","Camelini","palearctic",o|d|a,500,browse,-300,100,20,36,70).Traits=Husbandry(700,650,500,600,800,48,true,true,true);
            Add("Wild ass","Equus asinine","afrotropical",o|d,200,browse,-50,150,30,10,42).Traits=Husbandry(750,750,500,600,800,30,true,false,true);
            Add("Wild cattle","Bos","indomalayan",o|f|w,750,browse,-100,150,300,25,85).Traits=Husbandry(700,700,500,650,850,30,true,false,true);
            Add("Yak","Bos grunniens","palearctic",a|o,450,browse,-400,20,100,32,90,maxCold:80).Traits=Husbandry(700,650,650,650,850,36,true,true,true);
            Add("Ibex","Capra alpine","palearctic",a|o,70,browse,-250,50,100,40,50).Lineage.DisplayTrait="horns";
            Add("Bison","Bison","holarctic",o|f,650,Food.Grass,-450,70,200,48,-105,maxCold:100).Traits=Husbandry(650,500,200,300,800,36,false,false,true);
            Add("Musk ox","Ovibos","nearctic",u|o,300,browse,-650,0,80,70,-105,maxCold:30).Lineage.DisplayTrait="horns";
            Add("Pronghorn","Antilocapra","nearctic",o|d,50,browse,-250,150,70,42,-110,maxCold:160);
            Add("Raccoon","Procyon","nearctic",f|w,7,omni|meat|Food.Fish,-180,100,400,40,-90,g|c|water,g|c|water).MaxPreyKg=.5;
            Add("Skunk","Mephitidae","nearctic",f|o,3,omni|meat,-180,100,200,40,-100).MaxPreyKg=.2;
            Add("Badger","Mustelidae badger","palearctic",f|o,10,bugs|fruit|meat,-250,100,200,48,25,g,g|Stratum.Soil).MaxPreyKg=1;
            Add("Weasel","Mustela","holarctic",f|o|u, .18,meat,-500,30,180,55,70,maxCold:150).MaxPreyKg=.4;
            Add("Wolverine","Gulo","holarctic",f|u|a,15,meat,-500,20,150,65,70,maxCold:50).MaxPreyKg=40;
            Add("Hedgehog","Erinaceinae","palearctic",f|o,.7,bugs,-150,100,220,45,30,g,g|Stratum.Soil).MaxPreyKg=.01;
            Add("Shrew","Soricidae","palearctic",f|o|w,.012,bugs,-300,70,350,50,30,g,g|Stratum.Soil).MaxPreyKg=.002;
            Add("Mole","Talpidae","palearctic",f|o,.08,bugs,-200,80,500,48,15,Stratum.Soil,Stratum.Soil).MaxPreyKg=.005;
            Add("Squirrel","Sciuridae arboreal","holarctic",f,.35,fruit|bugs,-350,70,350,50,70,g|c,g|c).MaxPreyKg=.001;
            Add("Ground squirrel","Sciuridae terrestrial","holarctic",o|u|a,.3,Food.Seed|Food.Grass|bugs,-500,30,100,50,80).MaxPreyKg=.001;
            Add("Marmot","Marmota","palearctic",a|o,4,browse,-350,40,180,45,80,maxCold:80);
            Add("Lemming","Lemmini","holarctic",u|w,.045,browse,-650,-10,100,70,100,maxCold:20);
            Add("Jerboa","Dipodidae","palearctic",d|o,.08,Food.Seed|bugs,-200,150,20,38,65).Lineage.AppendageVariationPermille=450;
            Add("Pika","Ochotona","palearctic",a|o,.2,browse,-450,20,100,38,95,maxCold:80);
            Add("Fruit bat","Pteropodidae","indomalayan",f,.5,Food.Fruit|Food.Nectar,130,210,650,12,100,air|c,c).OceanCrossingKm=500;
            Add("Insectivorous bat","Chiroptera insectivore","palearctic",f|o,.015,bugs,-150,130,250,45,35,air,air|c).OceanCrossingKm=200;
            Add("Porcupine","Hystricidae","afrotropical",f|o,12,browse|Food.Fruit,20,170,200,0,30);
            Add("Nutria","Myocastor","neotropical",w,6,browse,-100,130,350,-30,-60,g|water,g|water).Lineage.RequiresFreshwater=true;
            Add("Sloth","Folivora","neotropical",f,5,Food.Browse,170,230,1000,0,-65,c,c).Lineage.MinCanopyPermille=650;
            Add("Anteater","Vermilingua","neotropical",f|o,30,bugs,100,210,400,-10,-60,g,Stratum.Soil|g).MaxPreyKg=.001;
            Add("Armadillo","Cingulata","neotropical",o|f|d,5,bugs|Food.Detritus,40,180,180,-15,-55,g,Stratum.Soil|g).MaxPreyKg=.02;
            Add("Spider monkey","Ateles","neotropical",f,8,fruit|Food.Browse,160,230,900,0,-65,c,c).Lineage.MinCanopyPermille=650;
            Add("Capuchin","Cebinae","neotropical",f,3,omni|meat,140,210,650,-5,-60,c,c).MaxPreyKg=.1;
            Add("Tamarin","Callitrichidae","neotropical",f,.4,omni,170,230,1000,0,-60,c,c).MaxPreyKg=.005;
            Add("Orangutan","Pongo","indomalayan",f,65,Food.Fruit|Food.Browse,210,250,1300,0,112,c,c).Lineage.MinCanopyPermille=700;
            Add("Gibbon","Hylobatidae","indomalayan",f,6,fruit|Food.Browse,140,210,900,15,105,c,c).Lineage.MinCanopyPermille=650;
            Add("Macaque","Macaca","indomalayan",f|o,8,omni|Food.Browse,0,140,400,22,100,g|c,g|c);
            Add("Giant panda","Ailuropoda","indomalayan",f,100,Food.Browse,-20,100,1000,30,103).RequiredResource="Bamboo resource guild";
            Add("Peafowl","Pavo","indomalayan",f|o,4,omni,50,200,350,22,78,g|air,g).Lineage.DisplayTrait="train feathers";
            Add("Crocodile","Crocodylia","afrotropical",w,250,meat|Food.Fish,120,220,100,0,30,g|water,g|water).MaxPreyKg=200;
            Add("Monitor lizard","Varanus","indomalayan",f|o|d|w,12,meat|bugs,100,220,100,5,110,g|c|water,g|c|water).MaxPreyKg=12;
            Add("Iguana","Iguanidae","neotropical",f|o,3,Food.Browse|Food.Fruit,170,230,300,5,-70,g|c,g|c);
            Add("Tortoise","Testudinidae","afrotropical",o|d|f,20,browse|Food.Fruit,30,180,80,0,30);
            Add("Freshwater turtle","Testudines freshwater","nearctic",w,1.5,bugs|Food.Fish|Food.Browse,-100,170,150,35,-90,g|water,g|water).Lineage.RequiresFreshwater=true;
            Add("Venomous snake","Viperidae","afrotropical",f|o|d,1.5,meat,0,180,80,0,30,g,g).MaxPreyKg=.8;
            Add("Emu","Dromaius","australasian",o|d|f,35,Food.Seed|Food.Browse|bugs,0,160,80,-25,135);
            Add("Cassowary","Casuarius","australasian",f,45,Food.Fruit|bugs,170,230,1000,-8,145).Lineage.MinCanopyPermille=650;
            Add("Wombat","Vombatidae","australasian",f|o,25,browse,-50,100,300,-35,147);
            Add("Wallaby","Macropodidae wallaby","australasian",f|o,14,browse,-10,140,300,-25,145);
            Add("Platypus","Ornithorhynchus","australasian",w,1.5,bugs,-40,100,650,-35,146,g|water,water).Lineage.RequiresFreshwater=true;
            Add("Echidna","Tachyglossidae","australasian",f|o|d,4,bugs,-30,100,100,-30,140,g,Stratum.Soil|g).MaxPreyKg=.001;
            Add("Cockatoo","Cacatuidae","australasian",f|o,.6,fruit|Food.Browse|bugs,0,130,200,-25,140,c|air,c|g).OceanCrossingKm=150;
            Add("Kiwi","Apterygidae","zealandian",f,2.5,bugs|Food.Fruit,-30,80,700,-41,174,g,g|Stratum.Soil).MaxPreyKg=.02;
            // Birds of shore, inland waters and canopy have different foraging
            // requirements. Flight is not permission to colonize every ocean.
            Add("Parrot","Psittaciformes","neotropical",f|o,.4,fruit|Food.Nectar,100,190,450,0,-65,c|air,c).OceanCrossingKm=200;
            Add("Toucan","Ramphastidae","neotropical",f,.6,fruit|bugs|meat,170,230,1000,0,-65,c|air,c).MaxPreyKg=.03;
            Add("Hummingbird","Trochilidae","neotropical",f|o|a,.005,Food.Nectar|bugs,30,130,350,8,-75,air,c|air).MaxPreyKg=.00005;
            Add("Woodpecker","Picidae","palearctic",f,.08,bugs|Food.Seed,-250,80,450,50,30,c|air,c).MaxPreyKg=.002;
            Add("Kingfisher","Alcedinidae","palearctic",w,.08,Food.Fish|bugs,-100,130,150,45,30,air,water).MaxPreyKg=.05;
            Add("Crane","Gruidae","palearctic",w|o,5,Food.Seed|bugs|meat,-250,100,200,50,70,g|air,g|water).MaxPreyKg=.2;
            Add("Stork","Ciconiidae","afrotropical",w|o,4,meat|Food.Fish|bugs,40,170,100,0,30,g|air,g|water).MaxPreyKg=.4;
            Add("Flamingo","Phoenicopteridae","afrotropical",w|Habitat.Coastal,3,bugs|Food.Grass,50,170,0,-5,35,g|water,water).MaxSalinityMilliPpt=180000;
            Add("Pelican","Pelecanidae","palearctic",w|Habitat.Coastal,8,Food.Fish,-50,150,0,35,35,air|water,water).MaxPreyKg=2;
            Add("Duck","Anatidae duck","holarctic",w,1,Food.Seed|Food.Grass|bugs,-350,50,0,50,80,g|water|air,water|g).Traits=Husbandry(850,850,650,650,800,8,false,false,false);
            Add("Goose","Anserinae","holarctic",w|o,4,browse|Food.Seed,-400,50,100,55,80,g|water|air,g|water).Traits=Husbandry(800,800,500,650,850,12,false,false,false);
            Add("Swan","Cygnus","palearctic",w,10,Food.Grass|bugs,-250,60,0,52,35,g|water|air,water).MaxPreyKg=.01;
            Add("Turkey","Meleagris","nearctic",f|o,6,Food.Seed|bugs,-200,120,250,35,-100,g|air,g).Traits=Husbandry(850,800,550,500,750,10,false,false,false);
            Add("Pheasant","Phasianidae","palearctic",f|o,1.2,Food.Seed|bugs,-150,120,250,38,95,g|air,g);
            Add("Grouse","Tetraonini","holarctic",f|u,1.1,Food.Browse|Food.Seed|bugs,-450,30,200,60,80,g|air,g,maxCold:120).Traits.SeasonalWhite=true;
            Add("Quail","Coturnix","palearctic",o,.12,Food.Seed|bugs,-80,130,180,38,50,g|air,g).Traits=Husbandry(850,900,650,550,650,3,false,false,false);
            Add("Pigeon","Columbidae","palearctic",f|o,.3,Food.Seed|Food.Fruit,-100,120,180,40,35,g|air|c,g|c).Traits=Husbandry(800,900,700,550,850,6,false,false,false);
            Add("Raven","Corvus","holarctic",f|o|a|u,1.1,omni|meat,-500,30,100,55,65,g|air|c,g|c).MaxPreyKg=.25;
            Add("Owl","Strigidae woodland","palearctic",f,.6,meat,-300,80,350,50,35,air,g|c).MaxPreyKg=.7;
            Add("Falcon","Falconidae","palearctic",o|d|a,1,meat,-250,100,80,40,50,air,g|air).MaxPreyKg=1.2;
            Add("Fish eagle","Haliaeetinae","holarctic",w|Habitat.Coastal,4,Food.Fish|meat,-350,70,0,55,80,air,water|g).MaxPreyKg=4;
            var otter=Add("Otter","Lutrinae","palearctic",w,8,Food.Fish|bugs,-250,80,150,50,30,g|water,water);otter.Lineage.RequiresFreshwater=true;otter.MaxPreyKg=2;
            var polar=Add("Polar bear","Ursus maritimus","arctic-coastal",Habitat.Coastal|u,450,meat|Food.Fish,-650,-80,0,75,20,g|water,g|water,maxCold:20);polar.Lineage.RequiresCoast=true;polar.MaxPreyKg=450;polar.OceanCrossingKm=250;polar.MaxSalinityMilliPpt=45000;
            Taxon Coastal(string name,string latin,string realm,double mass,Food diet,int cold,int warm,int maxCold,double lat,double lon,Stratum occupied)
            {
                var t=Add(name,latin,realm,Habitat.Coastal,mass,diet,cold,warm,0,lat,lon,occupied,g|water,maxCold);
                t.Lineage.RequiresCoast=true;t.Lineage.MarineFeeder=true;t.Lineage.Aquatic=true;t.MaxFloodDays=365;t.MaxSalinityMilliPpt=60000;t.MaxElevationM=600;t.MaxGapKm=500;t.SpreadBudgetKm=30000;return t;
            }
            var penguin=Coastal("Penguin","Spheniscidae","southern-coastal",12,Food.Fish|bugs,-600,-80,160,-60,-55,g|water);penguin.OceanCrossingKm=1600;penguin.MaxPreyKg=.8;penguin.Lineage.BodyVariationPermille=550;
            var gull=Coastal("Gull","Larinae","holarctic-coastal",1,Food.Fish|bugs|meat|Food.Seed,-500,0,300,55,10,g|air|water);gull.OceanCrossingKm=1800;gull.MaxPreyKg=.2;gull.Lineage.Aquatic=false;
            var tern=Coastal("Tern","Sterninae","holarctic-coastal",.15,Food.Fish|bugs,-400,50,300,45,-65,g|air|water);tern.OceanCrossingKm=2200;tern.MaxPreyKg=.07;
            var puffin=Coastal("Puffin","Fratercula","arctic-coastal",.5,Food.Fish|bugs,-350,10,100,65,-20,g|air|water);puffin.OceanCrossingKm=1000;puffin.MaxPreyKg=.15;
            var albatross=Coastal("Albatross","Diomedeidae","southern-coastal",8,Food.Fish|bugs,-200,0,180,-50,70,g|air|water);albatross.OceanCrossingKm=6000;albatross.MaxPreyKg=2;
            var cormorant=Coastal("Cormorant","Phalacrocoracidae","holarctic-coastal",2.5,Food.Fish,-250,80,300,45,15,g|air|water);cormorant.OceanCrossingKm=800;cormorant.MaxPreyKg=.6;
            var sandpiper=Coastal("Shorebird","Charadriiformes wader","holarctic-coastal",.08,bugs,-400,20,300,50,20,g|air|water);sandpiper.OceanCrossingKm=1600;sandpiper.MaxPreyKg=.005;
            var seal=Coastal("Seal","Phocidae","arctic-coastal",120,Food.Fish|bugs,-550,-30,180,65,0,g|water);seal.OceanCrossingKm=2500;seal.MaxPreyKg=8;
            var seaLion=Coastal("Sea lion","Otariidae","southern-coastal",180,Food.Fish|bugs,-150,40,220,-45,-65,g|water);seaLion.OceanCrossingKm=1800;seaLion.MaxPreyKg=8;
            var seaTurtle=Coastal("Sea turtle","Chelonioidea","tropical-coastal",80,Food.Browse|bugs,160,230,600,10,-60,g|water);seaTurtle.OceanCrossingKm=4000;seaTurtle.MaxPreyKg=.5;
            // These records denote visitors/feeding habitat in the adjacent sea,
            // never land-breeding animals or a fabricated pelagic range raster.
            Taxon Marine(string name,string latin,string realm,double mass,Food diet,int cold,int warm,int maxCold,double lat,double lon)
            {var t=Coastal(name,latin,realm,mass,diet,cold,warm,maxCold,lat,lon,water);t.Lineage.AdjacentSeaOnly=true;t.Hunts=water;t.OceanCrossingKm=6000;t.SpreadBudgetKm=60000;t.MaxElevationM=100000;return t;}
            Marine("Whale","Mysticeti","southern-coastal",30000,bugs|Food.Fish,-450,-30,280,-45,-60).MaxPreyKg=.5;
            Marine("Dolphin","Delphinidae small cetacean","tropical-coastal",180,Food.Fish|bugs,50,140,600,20,-65).MaxPreyKg=12;
            Marine("Orca","Orcinus","holarctic-coastal",4500,Food.Fish|meat,-450,-10,300,55,-140).MaxPreyKg=5000;
            var shark=Marine("Shark","Elasmobranchii predator","tropical-coastal",250,Food.Fish|meat,-80,100,600,20,150);shark.MaxPreyKg=120;shark.Lineage.Ectotherm=true;
            Marine("Dugong","Dugongidae","tropical-coastal",300,Food.Browse,170,230,600,-15,135).MaxPreyKg=0;
            Marine("Sea otter","Enhydra","nearctic-coastal",30,bugs|Food.Fish,-200,40,160,55,-150).MaxPreyKg=3;
            var walrus=Coastal("Walrus","Odobenus","arctic-coastal",1100,bugs,-550,-50,30,70,-160,g|water);walrus.MaxPreyKg=1;walrus.OceanCrossingKm=1800;walrus.Lineage.DisplayTrait="tusks";
            // Coastal feeding pools explicitly represent access to the adjacent
            // sea, not marine fish mysteriously living on dry land. Open-ocean
            // population/range models remain outside terrestrial ecoregions.
            var plankton=Guild("Coastal phytoplankton (feeding pool)","Coastal primary producer guild",Habitat.Coastal,.000001,Food.None,Food.Grass,water,water,-650,-150);plankton.Producer=true;plankton.Lineage.RequiresCoast=true;plankton.MaxSalinityMilliPpt=60000;
            var kelp=Guild("Coastal algae and seagrass (feeding pool)","Coastal vegetation guild",Habitat.Coastal,.01,Food.None,Food.Browse,water,water,-500,-80);kelp.Producer=true;kelp.Lineage.RequiresCoast=true;kelp.MaxSalinityMilliPpt=60000;
            var crust=Guild("Coastal crustaceans and molluscs (guild)","Coastal invertebrate guild",Habitat.Coastal,.008,Food.Grass|Food.Detritus,bugs,water,water,-650,-150);crust.Lineage.RequiresCoast=true;crust.MaxSalinityMilliPpt=60000;crust.MaxPreyKg=.01;
            var marineFish=Guild("Coastal forage fish (guild)","Coastal fish guild",Habitat.Coastal,.08,bugs,Food.Fish,water,water,-600,-100);marineFish.Lineage.RequiresCoast=true;marineFish.MaxSalinityMilliPpt=60000;marineFish.MaxPreyKg=.05;
            var squid=Guild("Coastal squid (guild)","Coastal cephalopod guild",Habitat.Coastal,.2,Food.Fish|bugs,bugs,water,water,-400,0);squid.Lineage.RequiresCoast=true;squid.MaxSalinityMilliPpt=60000;squid.MaxPreyKg=.1;
            var bamboo=Guild("Bamboo (resource guild)","Bamboo resource guild",f,10,Food.None,Food.Browse,g|c,Stratum.None,-50,130,900);bamboo.Producer=true;
            var salineAlgae=Guild("Saline-lake algae (resource guild)","Saline-lake primary producer guild",Habitat.Wetland,.000001,Food.None,Food.Grass,water,water,-300,50);salineAlgae.Producer=true;salineAlgae.MaxSalinityMilliPpt=250000;salineAlgae.Lineage.MinSalinityMilliPpt=3000;
            var brine=Guild("Brine shrimp (guild)","Saline-lake crustacean guild",Habitat.Wetland,.0001,Food.Grass,bugs,water,water,-250,80);brine.MaxSalinityMilliPpt=250000;brine.Lineage.MinSalinityMilliPpt=3000;brine.MaxPreyKg=.001;
            Guild("Earthworms (guild)","Earthworm guild",f|o|w,.002,Food.Detritus,bugs,Stratum.Soil,Stratum.Soil,-300,80,400);
            Guild("Ants and termites (guild)","Social insect guild",f|o|d,.000005,Food.Detritus|Food.Seed,bugs,g|Stratum.Soil,g|Stratum.Soil,-100,150,80);
            Guild("Butterflies and moths (guild)","Lepidoptera guild",f|o|w,.0002,Food.Nectar,bugs,air, c|g,-250,100,250);
            Guild("Bees (guild)","Apiformes guild",f|o|a,.0001,Food.Nectar,bugs,air,c|g,-300,100,150);
            Guild("Grasshoppers (guild)","Acrididae guild",o|d,.001,Food.Grass,bugs,g,g,-250,100,80);
            Guild("Dragonflies (guild)","Odonata guild",w,.001,bugs,bugs,air,air,-250,100,0);
            Guild("Freshwater predatory fish (guild)","Predatory freshwater fish guild",w,1.5,Food.Fish,Food.Fish,water,water,-300,50,0).MaxPreyKg=.5;
            Guild("Salamanders (guild)","Caudata guild",f|w,.02,bugs,meat,g|water,g|water,-250,80,700).MaxPreyKg=.003;
            foreach(var t in list)
            {
                if(t.Lineage==null)continue;
                if(t.Lineage.RequiresCoast)t.Habitats|=Habitat.Coastal;
                if(t.Lineage.MarineFeeder)t.MaxSalinityMilliPpt=Math.Max(t.MaxSalinityMilliPpt,45000);
                if(t.Name=="Crocodile"||t.Name=="Monitor lizard"||t.Name=="Iguana"||t.Name.IndexOf("python",StringComparison.OrdinalIgnoreCase)>=0||t.Name=="Boa constrictor"||t.Name.Contains("turtle")||t.Name=="Tortoise")t.Lineage.Ectotherm=true;
                if(t.OriginGroup=="holarctic")t.HistoricalBridgeAllowed=true;
                if(t.Name=="Wolf")t.MaxColdTenthsC=180;
                if(t.Name=="Hare")t.MaxColdTenthsC=150;
                if(t.Name=="Wolf"||t.Name=="Hare"||t.Name=="Deer"||t.Name=="Bear")t.Lineage.ThermalSizeSensitivityPermille=550;
                if(t.Name=="Horse")t.Lineage.RidingSuitabilityPermille=900;
                if(t.Name=="Camel")t.Lineage.RidingSuitabilityPermille=800;
                if(t.Name=="Wild ass")t.Lineage.RidingSuitabilityPermille=550;
                if(t.Name=="Reindeer")t.Lineage.RidingSuitabilityPermille=250;
                if(t.Name=="Elephant")t.Lineage.RidingSuitabilityPermille=200;
                if(t.Name=="Reindeer"){t.Traits.Fiber=true;t.Traits.Milk=true;t.Traits.Traction=true;t.Traits.CaptiveBreeding=650;t.Traits.Docility=650;t.Traits.PanicResistance=650;t.Traits.SocialTolerance=850;}
                if(t.Name=="Giraffe"||t.Name=="Rhinoceros"||t.Name=="Hippopotamus"||t.Name=="Bison"||t.Name=="African buffalo"){t.Traits.Docility=Math.Min(t.Traits.Docility,200);t.Traits.PanicResistance=Math.Min(t.Traits.PanicResistance,300);}
            }
            return list.ToArray();
        }
        static AnimalTraits Husbandry(int diet,int captive,int docile,int panic,int social,int maturity,bool milk,bool fiber,bool traction)
            =>new AnimalTraits{DietFlexibility=diet,CaptiveBreeding=captive,Docility=docile,PanicResistance=panic,SocialTolerance=social,MaturityMonths=maturity,Milk=milk,Fiber=fiber,Traction=traction};
        static AnimalNativeSource Source(string realm,double lat,double lon)=>new AnimalNativeSource{Realm=realm,Latitude=lat,Longitude=lon};
        static string Family(string n)
        {
            switch(n)
            {
                case "Wood mouse":case "Deer mouse":case "African forest mouse":case "Rajah spiny rat":return "Mouse";
                case "Gray wolf":case "Arctic wolf":return "Wolf";
                case "Arctic hare":case "European hare":case "Mountain hare":case "Snowshoe hare":return "Hare";
                case "African savanna elephant":case "Asian elephant":case "African forest elephant":return "Elephant";
                case "Red deer":case "White-tailed deer":case "Chital":case "Sambar":case "Indian muntjac":return "Deer";
                case "American black bear":case "Brown bear":return "Bear";
                case "Canada lynx":case "Eurasian lynx":return "Lynx";
                case "Crowned eagle":case "Harpy eagle":return "Forest eagle";
                case "Common buzzard":case "Red-tailed hawk":return "Hawk";
                case "European rabbit":return "Rabbit";
                case "Przewalski's horse":return "Horse";
                case "Plains zebra":return "Zebra";
                case "Blue wildebeest":return "Wildebeest";
                case "African rock python":case "Reticulated python":return "Python";
                case "American beaver":return "Beaver";
                case "African palm civet":case "Banded palm civet":return "Civet";
                case "African buffalo":case "Wild water buffalo":return "Buffalo";
                case "Blue duiker":return "Duiker";
                case "South American agouti":return "Agouti";
                case "Lowland paca":return "Paca";
                case "Lowland tapir":return "Tapir";
                case "Red howler monkey":return "Howler monkey";
                case "Hispid cotton rat":return "Cotton rat";
                case "Red river hog":return "Forest hog";
                case "Red junglefowl":return "Junglefowl";
                default:return n;
            }
        }
    }
}
