using System;
using System.Linq;
using System.Collections.Generic;
namespace WorldGen.Core
{
    // Independently authored habitat/behavior priors. No external species dataset.
    public static class NativeLifePack
    {
        public const string Version="earth-native-2";
        public static Taxon[] Create()
        {
            var list=EarthLifePack.Create().ToList();
            Taxon Animal(string name,string latin,string realm,Habitat habitat,double mass,Food diet,int cold,int warm,int aridity,double lat,double lon,Stratum occupies=Stratum.Ground,Stratum hunts=Stratum.Ground)
            {
                var t=new Taxon{Id=list.Count,Name=name,ScientificName=latin,ParentSpecies=latin,OriginGroup=realm,Rank=TaxonRank.Species,MassKg=mass,Eats=diet,Provides=Food.Vertebrate,Occupies=occupies,Hunts=hunts,MinColdTenthsC=cold,MinWarmTenthsC=warm,MaxWarmTenthsC=450,MinAridityMilli=aridity,MaxAridityMilli=20000,PassageColdTenthsC=cold-50,MaxGapKm=350,SpreadBudgetKm=18000,EarthLatitude=lat,EarthLongitude=lon,MinPreyKg=.000001,MaxPreyKg=Math.Max(.005,mass*.35),MaxFloodDays=240};
                t.Habitats=habitat;list.Add(t);return t;
            }
            var forest=Habitat.Forest;var open=Habitat.Open;var wet=Habitat.Wetland;var ground=Stratum.Ground;var canopy=Stratum.Canopy|Stratum.Understory;
            var plants=Food.Seed|Food.Fruit|Food.Browse;var omni=plants|Food.Invertebrate;
            Animal("African forest mouse","Hylomyscus stella","afrotropical",forest,.025,omni,160,220,900,0,25);
            Animal("Blue duiker","Philantomba monticola","afrotropical",forest,5,plants,120,200,750,0,25);
            Animal("Red river hog","Potamochoerus porcus","afrotropical",forest|wet,70,omni,140,220,800,0,20);
            Animal("African forest elephant","Loxodonta cyclotis","afrotropical",forest,2800,plants,170,230,1000,0,22);
            Animal("Guereza","Colobus guereza","afrotropical",forest,10,Food.Browse|Food.Fruit,100,180,700,1,30,canopy,canopy);
            Animal("African palm civet","Nandinia binotata","afrotropical",forest,2.5,omni|Food.Vertebrate,140,220,850,0,25,canopy,canopy|ground);
            var leopard=Animal("Leopard","Panthera pardus","afrotropical",forest|open,55,Food.Vertebrate,20,160,200,0,28);leopard.MinPreyKg=.5;leopard.MaxPreyKg=130;leopard.Hunts|=canopy;
            var eagle=Animal("Crowned eagle","Stephanoaetus coronatus","afrotropical",forest,3.5,Food.Vertebrate,100,180,700,0,25,Stratum.Air,canopy|ground);eagle.MinPreyKg=.1;eagle.MaxPreyKg=12;eagle.OceanCrossingKm=80;
            Animal("African rock python","Python sebae","afrotropical",forest|open|wet,35,Food.Vertebrate,110,220,350,2,30).MaxPreyKg=45;
            Animal("Impala","Aepyceros melampus","afrotropical",open,55,Food.Grass|Food.Browse,40,180,200,-8,30);
            Animal("African buffalo","Syncerus caffer","afrotropical",forest|open|wet,550,Food.Grass,50,180,350,-5,28);
            Animal("Spotted hyena","Crocuta crocuta","afrotropical",open,60,Food.Vertebrate,30,180,170,-4,35).MaxPreyKg=450;
            Animal("South American agouti","Dasyprocta leporina","neotropical",forest,4,Food.Seed|Food.Fruit,140,220,800,0,-55);
            Animal("Lowland paca","Cuniculus paca","neotropical",forest|wet,9,plants,130,210,800,-4,-65);
            Animal("Lowland tapir","Tapirus terrestris","neotropical",forest|wet,200,plants,110,210,700,-8,-60);
            Animal("Red howler monkey","Alouatta seniculus","neotropical",forest,7,Food.Browse|Food.Fruit,150,220,800,3,-65,canopy,canopy);
            Animal("Ocelot","Leopardus pardalis","neotropical",forest|open,12,Food.Vertebrate,100,200,450,0,-65).MaxPreyKg=8;
            Animal("Boa constrictor","Boa constrictor","neotropical",forest|open,12,Food.Vertebrate,120,220,400,-5,-60,ground|canopy,ground|canopy).MaxPreyKg=12;
            var harpy=Animal("Harpy eagle","Harpia harpyja","neotropical",forest,6,Food.Vertebrate,160,220,900,0,-65,Stratum.Air,canopy);harpy.MaxPreyKg=10;
            Animal("Guanaco","Lama guanicoe","neotropical",open|Habitat.Desert|Habitat.Alpine,110,Food.Grass|Food.Browse,-150,60,100,-40,-70);
            Animal("Hispid cotton rat","Sigmodon hispidus","nearctic",open|wet,.15,Food.Grass|Food.Seed,-30,180,300,28,-95);
            Animal("Snowshoe hare","Lepus americanus","nearctic",forest|Habitat.Tundra,1.5,Food.Browse|Food.Grass,-450,50,350,58,-110);
            Animal("American beaver","Castor canadensis","nearctic",wet|forest,22,Food.Browse,-350,80,300,50,-100);
            Animal("American black bear","Ursus americanus","nearctic",forest,130,omni|Food.Vertebrate,-350,100,400,45,-110).MaxPreyKg=80;
            Animal("Canada lynx","Lynx canadensis","nearctic",forest,10,Food.Vertebrate,-450,60,300,58,-110).MaxPreyKg=5;
            Animal("Wild boar","Sus scrofa","palearctic",forest|open|wet,90,omni,-150,120,300,45,30);
            Animal("Wild goat","Capra aegagrus","palearctic",open|Habitat.Alpine|Habitat.Desert,55,Food.Browse|Food.Grass,-200,100,120,36,45);
            Animal("Mouflon","Ovis gmelini","palearctic",open|Habitat.Alpine,45,Food.Grass|Food.Browse,-200,100,150,38,45);
            Animal("Przewalski's horse","Equus ferus przewalskii","palearctic",open,300,Food.Grass,-350,120,150,46,95).MaxColdTenthsC=150;
            Animal("European rabbit","Oryctolagus cuniculus","palearctic",open|forest,1.5,Food.Grass|Food.Browse,-70,120,200,40,-5);
            Animal("Mountain hare","Lepus timidus","palearctic",forest|Habitat.Tundra|Habitat.Alpine,3,Food.Grass|Food.Browse,-500,20,150,65,35).MaxColdTenthsC=80;
            Animal("Brown bear","Ursus arctos","holarctic",forest|open|Habitat.Alpine,220,omni|Food.Vertebrate,-450,60,250,55,100).MaxPreyKg=250;
            Animal("Eurasian lynx","Lynx lynx","palearctic",forest,22,Food.Vertebrate,-350,70,350,55,35).MaxPreyKg=40;
            Animal("Rajah spiny rat","Maxomys rajah","indomalayan",forest,.15,omni,160,220,900,3,105);
            Animal("Indian muntjac","Muntiacus vaginalis","indomalayan",forest,23,plants,80,180,650,22,85);
            Animal("Sambar","Rusa unicolor","indomalayan",forest|open,220,Food.Browse|Food.Grass,80,200,500,22,85);
            Animal("Wild water buffalo","Bubalus arnee","indomalayan",wet|open,700,Food.Grass,120,220,600,26,91);
            Animal("Red junglefowl","Gallus gallus","indomalayan",forest|open,1,Food.Seed|Food.Invertebrate,100,200,500,18,100);
            Animal("Reticulated python","Malayopython reticulatus","indomalayan",forest|wet,45,Food.Vertebrate,160,240,1000,3,105).MaxPreyKg=65;
            Animal("Banded palm civet","Hemigalus derbyanus","indomalayan",forest,2,omni|Food.Vertebrate,180,240,1000,2,110).MaxPreyKg=.5;
            Animal("Long-nosed bandicoot","Perameles nasuta","australasian",forest|open,1,omni,-10,150,350,-30,150);
            Animal("Common brushtail possum","Trichosurus vulpecula","australasian",forest,3,plants,-30,100,250,-30,145,canopy,canopy);
            Animal("Spotted-tailed quoll","Dasyurus maculatus","australasian",forest,4,Food.Vertebrate|Food.Invertebrate,-10,120,600,-30,152).MaxPreyKg=5;
            // Local functional prey pools fill unresolved small fauna, not absent
            // large mammals or a fictional globally native apex species.
            var frogs=Animal("Native frogs (guild)","Anura habitat guild","widespread",forest|wet,.025,Food.Invertebrate,20,150,650,0,0);frogs.Rank=TaxonRank.FunctionalGuild;frogs.MaxPreyKg=.004;
            var lizards=Animal("Native small lizards (guild)","Squamata habitat guild","widespread",forest|open|Habitat.Desert,.015,Food.Invertebrate,30,180,120,0,0);lizards.Rank=TaxonRank.FunctionalGuild;lizards.MaxPreyKg=.002;
            var birds=Animal("Native insectivorous understory birds (guild)","Understory bird guild","widespread",forest,.025,Food.Invertebrate,-50,120,450,0,0,canopy|Stratum.Air,canopy|ground|Stratum.Air);birds.Rank=TaxonRank.FunctionalGuild;birds.MaxPreyKg=.003;
            foreach(var t in list)
            {
                if(t.Name=="Common buzzard"||t.Name=="Wood mouse"||t.Name=="Bank vole"||t.Name=="European hare"||t.Name=="Pedunculate oak")t.MaxColdTenthsC=150;
                if(t.Name=="Red-tailed hawk"||t.Name=="Red deer"||t.Name=="Deer mouse"||t.Name=="European rabbit"||t.Name=="Brown bear"||t.Name=="Eurasian lynx")t.MaxColdTenthsC=160;
                if(t.Name=="Snowshoe hare"||t.Name=="Canada lynx"||t.Name=="Snowy owl")t.MaxColdTenthsC=80;
                if(t.Name=="Snowshoe hare"||t.Name=="Canada lynx")t.MaxWarmTenthsC=270;
                if(t.Name=="Lion")t.Habitats=open; // No routine closed-canopy lion assemblage.
                if(t.Name=="African savanna elephant")t.Habitats=open|wet;
                if(t.Producer||t.BasalResource||t.Rank==TaxonRank.FunctionalGuild)continue;
                t.Traits=new AnimalTraits{DietFlexibility=600,CaptiveBreeding=250,Docility=250,PanicResistance=300,SocialTolerance=350,MaturityMonths=t.MassKg>300?72:t.MassKg>10?30:12};
                if(t.Name=="Wild goat"||t.Name=="Mouflon"||t.Name=="Guanaco")t.Traits=new AnimalTraits{DietFlexibility=800,CaptiveBreeding=800,Docility=650,PanicResistance=650,SocialTolerance=850,MaturityMonths=24,Milk=t.Name!="Guanaco",Fiber=true};
                if(t.Name=="Wild boar")t.Traits=new AnimalTraits{DietFlexibility=950,CaptiveBreeding=850,Docility=400,PanicResistance=650,SocialTolerance=750,MaturityMonths=15};
                if(t.Name=="Przewalski's horse")t.Traits=new AnimalTraits{DietFlexibility=650,CaptiveBreeding=700,Docility=500,PanicResistance=550,SocialTolerance=850,MaturityMonths=36,Milk=true,Traction=true};
                if(t.Name=="Plains zebra")t.Traits=new AnimalTraits{DietFlexibility=600,CaptiveBreeding=450,Docility=100,PanicResistance=150,SocialTolerance=450,MaturityMonths=36,Traction=true};
                if(t.Name=="Red junglefowl"||t.Name=="European rabbit")t.Traits=new AnimalTraits{DietFlexibility=850,CaptiveBreeding=850,Docility=550,PanicResistance=500,SocialTolerance=650,MaturityMonths=6};
                if(t.Name=="Wild water buffalo")t.Traits=new AnimalTraits{DietFlexibility=650,CaptiveBreeding=650,Docility=500,PanicResistance=500,SocialTolerance=800,MaturityMonths=36,Milk=true,Traction=true};
                if(t.Name=="Mountain hare"||t.Name=="Snowshoe hare"||t.Name=="Arctic hare")t.Traits.SeasonalWhite=true;
                if(t.Name=="Mountain hare"||t.Name=="Snowshoe hare"||t.Name=="Reindeer"||t.Name=="Arctic wolf"||t.Name=="Moose"||t.Name=="Canada lynx")t.Traits.InsulationPermille=850;
                if(t.OriginGroup=="holarctic")t.HistoricalBridgeAllowed=true;
            }
            return list.ToArray();
        }
        public static double Domesticability(AnimalTraits t)
        {
            if(t==null||t.MaturityMonths<=0)return 0;
            double limiting=Math.Min(t.CaptiveBreeding,Math.Min(t.Docility,t.PanicResistance))/1000.0;
            return D.Q(limiting*(.4+.6*t.DietFlexibility/1000.0)*(.3+.7*t.SocialTolerance/1000.0)*Math.Min(1,36.0/t.MaturityMonths),1000000);
        }
        public static RegionalAnimalVariant Variant(World w,Taxon t,Ecoregion r)
        {
            if(t.Traits==null)return null;
            var h=w.Ecology.Habitat[r.RepresentativeCell];int band=h.ColdTenthsC<0?0:h.ColdTenthsC<150?1:2;
            int key=unchecked(r.ProvinceId*8191+t.Id*31+band);double drift=Rng.Value(w.Recipe.Seed,"animal-regional-form-2",key);
            return new RegionalAnimalVariant{Name=(band==0?"Cold-country":band==2?"Warm-country":"Temperate")+" form of "+t.Name,MassKg=D.Q(t.MassKg*(.85+.30*drift),1000000),InsulationPermille=Math.Max(100,Math.Min(950,t.Traits.InsulationPermille+(band==0?150:band==2?-150:0))),CoatLightnessPermille=Math.Max(100,Math.Min(900,t.Traits.CoatLightnessPermille+(int)((drift-.5)*200))),SeasonalWhiteningPermille=t.Traits.SeasonalWhite?(int)(1000*D.Clamp(-h.ColdTenthsC/150.0)):0,Docility=Math.Max(0,Math.Min(1000,t.Traits.Docility+(int)((drift-.5)*80))),PanicResistance=Math.Max(0,Math.Min(1000,t.Traits.PanicResistance+(int)((drift-.5)*80)))};
        }
    }
}
