using System.Collections.Generic;

namespace WorldGen.Core
{
    /// <summary>Representative modern native taxa, with explicit procedural trait priors.
    /// Not a global species inventory or an empirical species-distribution database.</summary>
    public static class EarthLifePack
    {
        public static Taxon[] Create()
        {
            var taxa=new List<Taxon>();
            Taxon Add(string name,string latin,string group,Habitat habitat,double mass,Food eats,Food provides,Stratum occupies,Stratum hunts,int cold,int warmMin,int warmMax,int aridity,double lat,double lon)
            {
                var t=new Taxon { Id=taxa.Count,Name=name,ScientificName=latin,ParentSpecies=latin,OriginGroup=group, Habitats=habitat,MassKg=mass,Eats=eats,Provides=provides,Occupies=occupies,Hunts=hunts,MinColdTenthsC=cold,MinWarmTenthsC=warmMin,MaxWarmTenthsC=warmMax,MinAridityMilli=aridity,PassageColdTenthsC=cold-40,EarthLatitude=lat,EarthLongitude=lon,MinPreyKg=mass*.015,MaxPreyKg=mass*2,MaxFloodDays=200 };
                taxa.Add(t); return t;
            }
            Taxon Plant(string name,string latin,string group,Habitat habitat,Food food,int cold,int aridity,double lat,double lon)
            { var t=Add(name,latin,group,habitat,10,Food.None,food,Stratum.Ground,Stratum.None,cold,60,450,aridity,lat,lon); t.Producer=true; t.MaxFloodDays=365; t.MaxGapKm=150; t.SpreadBudgetKm=12000; return t; }
            var land=Habitat.AnyLand; var forest=Habitat.Forest; var open=Habitat.Open;
            Plant("Grasses and sedges (guild)","Poaceae / Cyperaceae guild","widespread",land,Food.Grass|Food.Seed,-600,80,45,30).Rank=TaxonRank.FunctionalGuild;
            Plant("Woody understory (guild)","Woody plant guild","widespread",forest|open|Habitat.Wetland,Food.Browse|Food.Fruit|Food.Seed,-400,250,40,30).Rank=TaxonRank.FunctionalGuild;
            Plant("Flowering herbs (guild)","Forb guild","widespread",land,Food.Nectar|Food.Seed|Food.Browse,-400,120,40,30).Rank=TaxonRank.FunctionalGuild;
            Plant("Mosses and lichens (guild)","Bryophyte / lichen guild","widespread",Habitat.Tundra|Habitat.Alpine|forest|Habitat.Wetland,Food.Grass,-600,150,65,30).Rank=TaxonRank.FunctionalGuild;
            Plant("Aquatic primary producers (guild)","Freshwater producer guild","widespread",Habitat.Freshwater|Habitat.Wetland,Food.Grass|Food.Seed,-300,0,50,30).Rank=TaxonRank.FunctionalGuild;
            Plant("Scots pine","Pinus sylvestris","palearctic",forest,Food.Seed|Food.Browse,-400,400,55,30);
            Plant("Pedunculate oak","Quercus robur","palearctic",forest,Food.Seed|Food.Browse,-150,650,50,10);
            Plant("White spruce","Picea glauca","nearctic",forest,Food.Seed|Food.Browse,-450,450,58,-110);
            Plant("Umbrella thorn","Vachellia tortilis","afrotropical",open|forest|Habitat.Desert,Food.Browse|Food.Seed,50,120,-3,35);
            Plant("Cecropia","Cecropia peltata","neotropical",forest,Food.Browse|Food.Fruit,150,900,5,-70);
            Plant("Sal tree","Shorea robusta","indomalayan",forest,Food.Seed|Food.Browse,100,650,25,85);
            Plant("River red gum","Eucalyptus camaldulensis","australasian",forest|open|Habitat.Wetland,Food.Browse|Food.Nectar|Food.Seed,-10,150,-28,140);
            var mangrove=Plant("Red mangrove","Rhizophora mangle","neotropical",Habitat.Coastal|Habitat.Wetland,Food.Browse|Food.Detritus,180,650,15,-85); mangrove.MaxSalinityMilliPpt=45000; mangrove.OceanCrossingKm=1000;
            var reed=Plant("Common reed","Phragmites australis","palearctic",Habitat.Wetland|Habitat.Freshwater,Food.Grass|Food.Seed,-250,200,45,30); reed.MaxSalinityMilliPpt=15000;
            var detritus=Plant("Detritus and microbial decomposers (pool)","Detrital resource guild","widespread",land|Habitat.Freshwater,Food.Detritus,-600,0,40,30); detritus.Rank=TaxonRank.FunctionalGuild; detritus.Producer=false; detritus.BasalResource=true;
            Taxon Animal(string name,string latin,string group,Habitat habitat,double mass,Food eats,Stratum occupies,Stratum hunts,int cold,int minWarm,int maxWarm,int ai,double lat,double lon)
                => Add(name,latin,group,habitat,mass,eats,Food.Vertebrate,occupies,hunts,cold,minWarm,maxWarm,ai,lat,lon);
            var ground=Stratum.Ground; var browse=Food.Browse|Food.Grass;
            var fly=Animal("Native detritivorous flies (guild)","Diptera guild","widespread",land,.000012,Food.Detritus,Stratum.Air,ground,-350,80,450,80,30,30); fly.Provides=Food.Invertebrate; fly.Rank=TaxonRank.FunctionalGuild;
            var beetle=Animal("Native litter beetles (guild)","Coleoptera guild","widespread",land,.0002,Food.Detritus,ground,ground,-400,50,450,100,40,30); beetle.Provides=Food.Invertebrate; beetle.Rank=TaxonRank.FunctionalGuild;
            var spider=Animal("Orb-weaving spiders (guild)","Araneidae guild","widespread",forest|open|Habitat.Wetland,.0005,Food.Invertebrate,Stratum.Understory,Stratum.Air|Stratum.Understory,-250,100,400,200,40,30); spider.MinPreyKg=.000001; spider.MaxPreyKg=.001; spider.Provides=Food.Invertebrate; spider.Rank=TaxonRank.FunctionalGuild;
            Animal("Wood mouse","Apodemus sylvaticus","palearctic",forest|open,.025,Food.Seed|Food.Fruit|Food.Invertebrate,ground,ground,-200,100,350,250,48,10);
            Animal("Deer mouse","Peromyscus maniculatus","nearctic",forest|open|Habitat.Desert,.022,Food.Seed|Food.Invertebrate,ground,ground,-350,100,380,100,40,-110);
            Animal("Grass rat","Arvicanthis niloticus","afrotropical",open,.1,Food.Grass|Food.Seed,ground,ground,50,180,450,150,8,35);
            Animal("Bank vole","Myodes glareolus","palearctic",forest,.025,Food.Seed|Food.Browse,ground,ground,-350,100,320,400,55,30);
            Animal("Arctic hare","Lepus arcticus","nearctic",Habitat.Tundra|Habitat.Alpine,.4e1,browse,ground,ground,-600,0,180,100,72,-95);
            Animal("European hare","Lepus europaeus","palearctic",open,4,browse,ground,ground,-200,100,350,200,45,30);
            Animal("Red deer","Cervus elaphus","palearctic",forest|open,180,browse,ground,ground,-250,100,350,300,48,20);
            Animal("White-tailed deer","Odocoileus virginianus","nearctic",forest|open|Habitat.Wetland,70,browse,ground,ground,-300,100,380,300,40,-85);
            Animal("Reindeer","Rangifer tarandus","holarctic",Habitat.Tundra|forest|Habitat.Alpine,120,browse,ground,ground,-600,20,230,180,66,110);
            Animal("Moose","Alces alces","holarctic",forest|Habitat.Wetland,450,browse,ground,ground,-450,90,250,450,60,100);
            Animal("Plains zebra","Equus quagga","afrotropical",open,300,Food.Grass,ground,ground,30,180,450,200,-5,35);
            Animal("Blue wildebeest","Connochaetes taurinus","afrotropical",open,220,Food.Grass,ground,ground,50,180,430,250,-3,35);
            Animal("African savanna elephant","Loxodonta africana","afrotropical",forest|open,4500,browse|Food.Fruit,ground,ground,50,180,450,200,-5,30);
            Animal("Asian elephant","Elephas maximus","indomalayan",forest|open,3500,browse|Food.Fruit,ground,ground,120,200,430,600,20,90);
            Animal("Chital","Axis axis","indomalayan",forest|open,70,browse,ground,ground,100,200,430,400,22,80);
            Animal("Capybara","Hydrochoerus hydrochaeris","neotropical",Habitat.Wetland|open,50,Food.Grass,ground,ground,120,200,420,600,-8,-60).MaxFloodDays=365;
            Animal("Collared peccary","Pecari tajacu","neotropical",forest|open|Habitat.Desert,25,Food.Fruit|Food.Browse,ground,ground,50,180,420,200,0,-60);
            Animal("Red kangaroo","Osphranter rufus","australasian",open|Habitat.Desert,65,Food.Grass,ground,ground,0,180,450,100,-25,135);
            Animal("Koala","Phascolarctos cinereus","australasian",forest,9,Food.Browse,Stratum.Canopy,Stratum.Canopy,0,150,350,350,-28,150).RequiredResource="Eucalyptus camaldulensis";
            var lion=Animal("Lion","Panthera leo","afrotropical",open|forest,180,Food.Vertebrate,ground,ground,30,180,450,170,-4,35); lion.MinPreyKg=15; lion.MaxPreyKg=900; lion.MaxGapKm=150;
            var tiger=Animal("Tiger","Panthera tigris","indomalayan",forest|Habitat.Wetland,190,Food.Vertebrate,ground,ground,-200,150,400,600,25,90); tiger.MinPreyKg=5; tiger.MaxPreyKg=650;
            var wolf=Animal("Gray wolf","Canis lupus","holarctic",forest|open|Habitat.Tundra|Habitat.Alpine|Habitat.Desert,40,Food.Vertebrate,ground,ground,-600,30,380,100,55,90); wolf.MinPreyKg=.5; wolf.MaxPreyKg=600; wolf.MaxGapKm=800; wolf.SpreadBudgetKm=26000;
            var arctic=Animal("Arctic wolf","Canis lupus arctos","nearctic",Habitat.Tundra|Habitat.Alpine,40,Food.Vertebrate,ground,ground,-650,-10,180,80,75,-95); arctic.Rank=TaxonRank.Subspecies; arctic.ParentSpecies="Canis lupus"; arctic.MinPreyKg=.5; arctic.MaxPreyKg=600; arctic.MaxGapKm=600;
            var fox=Animal("Red fox","Vulpes vulpes","holarctic",land,6,Food.Vertebrate|Food.Invertebrate|Food.Fruit,ground,ground,-500,50,400,120,50,60); fox.MinPreyKg=.0001; fox.MaxPreyKg=4;
            var jaguar=Animal("Jaguar","Panthera onca","neotropical",forest|Habitat.Wetland,85,Food.Vertebrate,ground,ground,100,180,420,600,-5,-65); jaguar.MinPreyKg=1; jaguar.MaxPreyKg=250;
            var puma=Animal("Puma","Puma concolor","nearctic",forest|open|Habitat.Desert|Habitat.Alpine,60,Food.Vertebrate,ground,ground,-250,80,400,120,35,-110); puma.MinPreyKg=.5; puma.MaxPreyKg=300;
            var hawk=Animal("Red-tailed hawk","Buteo jamaicensis","nearctic",forest|open|Habitat.Desert,1.2,Food.Vertebrate,Stratum.Air,ground,-250,100,380,100,40,-100); hawk.MinPreyKg=.005; hawk.MaxPreyKg=2; hawk.OceanCrossingKm=150;
            var buzzard=Animal("Common buzzard","Buteo buteo","palearctic",forest|open,1,Food.Vertebrate,Stratum.Air,ground,-250,100,350,200,50,20); buzzard.MinPreyKg=.005; buzzard.MaxPreyKg=1.5; buzzard.OceanCrossingKm=150;
            var owl=Animal("Snowy owl","Bubo scandiacus","holarctic",Habitat.Tundra|open,2,Food.Vertebrate,Stratum.Air,ground,-600,0,230,100,70,100); owl.MinPreyKg=.015; owl.MaxPreyKg=4; owl.OceanCrossingKm=600;
            var aquatic=Animal("Aquatic invertebrates (guild)","Freshwater invertebrate guild","widespread",Habitat.Freshwater|Habitat.Wetland,.0002,Food.Detritus|Food.Grass,Stratum.Water,Stratum.Water|ground,-400,20,400,0,45,20); aquatic.Provides=Food.Invertebrate; aquatic.Rank=TaxonRank.FunctionalGuild; aquatic.MaxFloodDays=365;
            var fish=Animal("Small native freshwater fish (guild)","Freshwater fish guild","widespread",Habitat.Freshwater|Habitat.Wetland,.03,Food.Invertebrate,Stratum.Water,Stratum.Water,-300,50,350,0,45,20); fish.Provides=Food.Fish; fish.Rank=TaxonRank.FunctionalGuild; fish.MinPreyKg=.000001; fish.MaxPreyKg=.005; fish.MaxFloodDays=365;
            var heron=Animal("Grey heron","Ardea cinerea","palearctic",Habitat.Wetland|Habitat.Freshwater|Habitat.Coastal,1.5,Food.Fish|Food.Invertebrate,Stratum.Air,Stratum.Water,-150,100,380,100,50,20); heron.MinPreyKg=.001; heron.MaxPreyKg=.6; heron.OceanCrossingKm=300; heron.MaxFloodDays=365;
            // Basal guilds denote ecological functions, not one magically cosmopolitan
            // species. Named species still require origin and dispersal accessibility.
            foreach(var t in taxa) if(t.Producer && (t.Provides & Food.Browse)!=0) t.Occupies=Stratum.Ground|Stratum.Understory|Stratum.Canopy;
            foreach(var t in taxa) if(t.BasalResource || t.ScientificName=="Freshwater producer guild") t.Occupies|=Stratum.Water;
            foreach(var t in taxa)
            {
                // A possible physical corridor is not evidence that every modern
                // species used it. Recent historical interchange is a pack trait.
                t.HistoricalBridgeAllowed=t.OriginGroup=="holarctic" || t.Name=="Lion";
                if(t.Name=="Gray wolf" || t.Name=="Red fox") t.MaxColdTenthsC=180;
                if(t.Name=="Reindeer" || t.Name=="Moose" || t.Name=="White spruce" || t.Name=="Scots pine") t.MaxColdTenthsC=80;
                if(t.Name=="Red-tailed hawk") t.SpreadBudgetKm=8000;
            }
            return taxa.ToArray();
        }
    }
}
