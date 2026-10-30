using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    public enum WildlifeDiel { Diurnal,Nocturnal,Crepuscular,Cathemeral }
    public enum WildlifeSocial { Solitary,Pair,Loose,Herd,Family,Pack,Flock,School,Colony,Troop,Pod }
    public enum WildlifeHuntStyle { None,Forage,Stalk,Chase,Ambush,Aquatic,Scavenge }
    public enum WildlifePeopleResponse { Avoid,Defensive,RarePredation,OpportunisticPredation,PolarPredation,WaterAmbush }
    public enum WildlifeSensoryKind { General,Ungulate,Macropod,Canid,SmallCarnivore,LargeCat,Bear,SmallMammal,Primate,Elephant,Rhino,Bird,Raptor,Owl,Bat,Reptile,Snake,Fish,Marine,Invertebrate }
    public struct WildlifeNaturalHistoryProfile
    {
        public WildlifeDiel Diel;
        public WildlifeSocial Social;
        public WildlifeHuntStyle Hunt;
        public WildlifePeopleResponse People;
        public WildlifeSensoryKind Sensory;
        public int GroupMin,GroupMax;
        public double DefendM;
        public double RutStart,RutEnd;
        public bool Ectotherm,SmallBudget,SeasonalRut,Hibernates,WaterIndependent;
    }

    /// <summary>Explicit natural-history priors for every visible model family.
    /// Native taxon overrides distinguish species that share a model. Numerical
    /// ranges are conservative game priors, not measured species constants.
    /// This table is read once during profile creation, never scanned per frame.</summary>
    public static class WildlifeNaturalHistory
    {
        static readonly Dictionary<string,WildlifeNaturalHistoryProfile> profiles=Create();
        public static IReadOnlyDictionary<string,WildlifeNaturalHistoryProfile> Profiles=>profiles;
        public static bool TryGet(string slug,out WildlifeNaturalHistoryProfile profile)=>profiles.TryGetValue(slug??"",out profile);
        static Dictionary<string,WildlifeNaturalHistoryProfile> Create()
        {
            var result=new Dictionary<string,WildlifeNaturalHistoryProfile>(StringComparer.Ordinal);
            void R(string names,WildlifeDiel diel,WildlifeSocial social,WildlifeHuntStyle hunt,WildlifeSensoryKind sensory,int min=1,int max=1,double defend=0,WildlifePeopleResponse people=WildlifePeopleResponse.Avoid,bool ecto=false,bool cheap=false,bool rut=false,bool hibernate=false,bool independent=false)
            {
                foreach(string name in names.Split(' '))result.Add(name,new WildlifeNaturalHistoryProfile{Diel=diel,Social=social,Hunt=hunt,Sensory=sensory,GroupMin=min,GroupMax=max,DefendM=defend,People=people,Ectotherm=ecto,SmallBudget=cheap,SeasonalRut=rut,RutStart=.65,RutEnd=.91,Hibernates=hibernate,WaterIndependent=independent});
            }
            const WildlifeDiel D=WildlifeDiel.Diurnal,N=WildlifeDiel.Nocturnal,C=WildlifeDiel.Crepuscular,A=WildlifeDiel.Cathemeral;
            const WildlifeSocial Solo=WildlifeSocial.Solitary,Pair=WildlifeSocial.Pair,Loose=WildlifeSocial.Loose,Herd=WildlifeSocial.Herd,Family=WildlifeSocial.Family,Pack=WildlifeSocial.Pack,Flock=WildlifeSocial.Flock,School=WildlifeSocial.School,Colony=WildlifeSocial.Colony,Troop=WildlifeSocial.Troop,Pod=WildlifeSocial.Pod;
            const WildlifeHuntStyle None=WildlifeHuntStyle.None,Forage=WildlifeHuntStyle.Forage,Stalk=WildlifeHuntStyle.Stalk,Chase=WildlifeHuntStyle.Chase,Ambush=WildlifeHuntStyle.Ambush,Water=WildlifeHuntStyle.Aquatic,Scavenge=WildlifeHuntStyle.Scavenge;
            const WildlifeSensoryKind Ungulate=WildlifeSensoryKind.Ungulate,Small=WildlifeSensoryKind.SmallMammal,Carnivore=WildlifeSensoryKind.SmallCarnivore,Primate=WildlifeSensoryKind.Primate,Bird=WildlifeSensoryKind.Bird,Raptor=WildlifeSensoryKind.Raptor,Reptile=WildlifeSensoryKind.Reptile,Snake=WildlifeSensoryKind.Snake,Fish=WildlifeSensoryKind.Fish,Marine=WildlifeSensoryKind.Marine,Bug=WildlifeSensoryKind.Invertebrate;
            const WildlifePeopleResponse Defensive=WildlifePeopleResponse.Defensive,Rare=WildlifePeopleResponse.RarePredation,Possible=WildlifePeopleResponse.OpportunisticPredation;
            R("deer",C,Herd,None,Ungulate,3,8,1.5,Defensive,rut:true);
            R("hare",C,Solo,None,Small);R("rabbit",C,Loose,None,Small,2,5);
            R("butterflies-and-moths-guild",D,Loose,None,Bug,1,4,ecto:true,cheap:true,independent:true);
            R("fireflies-derived-lineage",N,Loose,None,Bug,3,8,ecto:true,cheap:true,independent:true);
            R("red-fox",C,Solo,Stalk,WildlifeSensoryKind.Canid);
            R("wolf",A,Pack,Chase,WildlifeSensoryKind.Canid,3,8,2,Rare);
            R("wild-boar",C,Family,Forage,Ungulate,3,7,3,Defensive);
            R("mouflon wild-goat ibex",D,Herd,None,Ungulate,3,8,1.8,Defensive,rut:true);
            R("bear",A,Solo,Forage,WildlifeSensoryKind.Bear,1,1,6,Defensive,hibernate:true);
            R("bison",A,Herd,None,Ungulate,5,14,5,Defensive,rut:true);
            R("bees-guild",D,Colony,None,Bug,3,7,ecto:true,cheap:true,independent:true);
            R("dragonflies-guild",D,Loose,Forage,Bug,1,3,ecto:true,cheap:true,independent:true);
            R("native-detritivorous-flies-guild",D,Loose,Forage,Bug,2,5,ecto:true,cheap:true,independent:true);
            R("duck",D,Flock,Forage,Bird,3,8);
            R("pheasant",D,Loose,Forage,Bird,1,4);
            R("small-native-freshwater-fish-guild",A,School,Forage,Fish,5,12,ecto:true,cheap:true,independent:true);
            R("mouse",N,Solo,Forage,Small,1,2,independent:true);
            R("squirrel",D,Solo,Forage,Small,1,2);
            R("horse",A,Herd,None,Ungulate,3,9,2,Defensive);
            R("reindeer",A,Herd,None,Ungulate,5,18,2,Defensive,rut:true);
            R("moose",C,Solo,None,Ungulate,1,1,5,Defensive,rut:true);
            R("wild-cattle",A,Herd,None,Ungulate,4,10,4,Defensive,rut:true);
            R("grouse",D,Loose,Forage,Bird,1,5);
            R("quail",C,Flock,Forage,Bird,4,10);
            R("turkey",D,Flock,Forage,Bird,4,10);
            R("junglefowl",D,Family,Forage,Bird,3,6);
            R("raccoon",N,Solo,Forage,Carnivore,1,2);
            R("badger",N,Family,Forage,Carnivore,1,3,1.5,Defensive);
            R("beaver",N,Family,None,Small,2,5);
            R("otter",A,Family,Water,Carnivore,1,3,1,Defensive);
            R("lynx",C,Solo,Stalk,WildlifeSensoryKind.LargeCat);
            R("owl",N,Solo,Stalk,WildlifeSensoryKind.Owl);
            R("raven",D,Pair,Forage,Bird,1,2);
            R("african-hunting-dog",A,Pack,Chase,WildlifeSensoryKind.Canid,4,10,2,Rare);
            R("agouti",D,Pair,Forage,Small,1,2);
            R("bank-vole",A,Solo,Forage,Small,1,2,independent:true);
            R("buffalo",A,Herd,None,Ungulate,5,12,5,Defensive);
            R("camel",D,Herd,None,Ungulate,3,8,2,Defensive,independent:true);
            R("cassowary",D,Solo,Forage,Bird,1,1,2,Defensive);
            R("civet",N,Solo,Forage,Carnivore);
            R("cockatoo",D,Flock,Forage,Bird,3,10);
            R("collared-peccary",A,Herd,Forage,Ungulate,3,8,2,Defensive);
            R("cotton-rat",C,Loose,Forage,Small,1,3,independent:true);
            R("crane",D,Pair,Forage,Bird,2,2);
            R("duiker",C,Pair,None,Ungulate,1,2);
            R("emu",D,Loose,Forage,Bird,2,6,1.5,Defensive);
            R("falcon",D,Solo,Stalk,Raptor);
            R("fish-eagle forest-eagle",D,Pair,Stalk,Raptor,1,2);
            R("flamingo",A,Flock,Forage,Bird,5,15);
            R("forest-hog",C,Family,Forage,Ungulate,3,7,3,Defensive);
            R("fruit-bat",N,Colony,Forage,WildlifeSensoryKind.Bat,3,8);
            R("gazelle",C,Herd,None,Ungulate,4,12);
            R("goose",D,Flock,Forage,Bird,4,12,1.2,Defensive);
            R("grass-rat",D,Loose,Forage,Small,1,3,independent:true);
            R("grasshoppers-guild",D,Loose,Forage,Bug,1,3,ecto:true,cheap:true,independent:true);
            R("grey-heron",C,Solo,Ambush,Bird);
            R("ground-squirrel",D,Colony,Forage,Small,2,6);
            R("guanaco",D,Herd,None,Ungulate,3,8,1.5,Defensive);
            R("hawk",D,Solo,Stalk,Raptor);
            R("hornbill",D,Pair,Forage,Bird,2,2);
            R("hummingbird",D,Solo,Forage,Bird,independent:true);
            R("impala",C,Herd,None,Ungulate,4,12);
            R("insectivorous-bat",N,Colony,Stalk,WildlifeSensoryKind.Bat,3,8,independent:true);
            R("jaguar",C,Solo,Stalk,WildlifeSensoryKind.LargeCat,1,1,2,Possible);
            R("jerboa",N,Solo,Forage,Small,independent:true);
            R("kingfisher",D,Solo,Ambush,Bird);
            R("kiwi",N,Pair,Forage,Bird,1,2);
            R("lemming",A,Loose,Forage,Small,1,3,independent:true);
            R("leopard",N,Solo,Stalk,WildlifeSensoryKind.LargeCat,1,1,2,Possible);
            R("lion",C,Pack,Stalk,WildlifeSensoryKind.LargeCat,3,7,3,Possible);
            R("marmot",D,Colony,Forage,Small,2,6,hibernate:true);
            R("meerkat",D,Family,Forage,Carnivore,4,8);
            R("mongoose",D,Family,Forage,Carnivore,1,4);
            R("musk-ox",A,Herd,None,Ungulate,4,10,5,Defensive,rut:true);
            R("native-litter-beetles-guild",N,Loose,Forage,Bug,1,3,ecto:true,cheap:true,independent:true);
            R("native-small-lizards-guild",D,Solo,Forage,Reptile,ecto:true,cheap:true,independent:true);
            R("ocelot",N,Solo,Stalk,WildlifeSensoryKind.LargeCat);
            R("oryx",C,Herd,None,Ungulate,3,8,2.5,Defensive,independent:true);
            R("ostrich",D,Loose,Forage,Bird,2,6,2,Defensive);
            R("paca",N,Solo,Forage,Small);
            R("parrot",D,Flock,Forage,Bird,3,8);
            R("peafowl",D,Family,Forage,Bird,2,5);
            R("pelican",D,Flock,Water,Bird,3,8);
            R("pigeon",D,Flock,Forage,Bird,3,9);
            R("pika",D,Solo,Forage,Small,independent:true);
            R("pronghorn",D,Herd,None,Ungulate,4,12);
            R("puma",C,Solo,Stalk,WildlifeSensoryKind.LargeCat,1,1,2,Possible);
            R("red-kangaroo",C,Loose,None,WildlifeSensoryKind.Macropod,3,10,2,Defensive,independent:true);
            R("salamanders-guild",N,Solo,Forage,Reptile,ecto:true,cheap:true,independent:true);
            R("secretary-bird",D,Pair,Stalk,Raptor,1,2);
            R("skunk",N,Solo,Forage,Carnivore,1,1,1,Defensive);
            R("snowy-owl",A,Solo,Stalk,WildlifeSensoryKind.Owl);
            R("spotted-hyena",N,Pack,Chase,WildlifeSensoryKind.Canid,3,8,2,Rare);
            R("spotted-tailed-quoll",N,Solo,Stalk,Carnivore);
            R("stork",D,Pair,Forage,Bird,1,2);
            R("swan",D,Pair,Forage,Bird,2,2,1.5,Defensive);
            R("tapir",C,Solo,None,Ungulate,1,2,1.5,Defensive);
            R("tiger",C,Solo,Stalk,WildlifeSensoryKind.LargeCat,1,1,3,Possible);
            R("toucan turaco",D,Family,Forage,Bird,2,5);
            R("vulture",D,Loose,Scavenge,Raptor,2,6);
            R("wallaby",C,Loose,None,WildlifeSensoryKind.Macropod,1,4);
            R("warthog",D,Family,Forage,Ungulate,2,6,2,Defensive);
            R("weasel",A,Solo,Stalk,Carnivore);
            R("wild-ass",D,Loose,None,Ungulate,2,5,1.5,Defensive,independent:true);
            R("wildebeest",A,Herd,None,Ungulate,5,18,2,Defensive);
            R("wolverine",A,Solo,Stalk,Carnivore,1,1,2,Defensive);
            R("wombat",N,Solo,None,Small);
            R("woodpecker",D,Pair,Forage,Bird,1,2);
            R("yak",A,Herd,None,Ungulate,3,9,3,Defensive,rut:true);
            R("aardvark",N,Solo,Forage,Carnivore);
            R("albatross",A,Loose,Water,Bird,1,4);
            R("anteater",C,Solo,Forage,Carnivore,1,1,1.5,Defensive);
            R("armadillo",C,Solo,Forage,Small);
            R("baboon",D,Troop,Forage,Primate,6,18,2,Defensive);
            R("boa-constrictor",N,Solo,Ambush,Snake,ecto:true,independent:true);
            R("capuchin",D,Troop,Forage,Primate,4,10);
            R("capybara",C,Herd,None,Small,3,10);
            R("chimpanzee",D,Troop,Forage,Primate,4,12,3,Defensive);
            R("coastal-forage-fish-guild",A,School,Forage,Fish,8,20,ecto:true,cheap:true,independent:true);
            R("coastal-squid-guild",A,School,Water,Fish,3,10,ecto:true,cheap:true,independent:true);
            R("common-brushtail-possum",N,Solo,Forage,Small);
            R("cormorant",D,Loose,Water,Bird,2,6);
            R("crocodile",A,Loose,Ambush,Reptile,1,3,3,WildlifePeopleResponse.WaterAmbush,ecto:true);
            R("dolphin",A,Pod,Water,Marine,3,10);
            R("dugong",A,Loose,None,Marine,1,3);
            R("echidna",A,Solo,Forage,Carnivore);
            R("elephant",A,Family,None,WildlifeSensoryKind.Elephant,3,9,9,Defensive);
            R("freshwater-predatory-fish-guild",A,Solo,Ambush,Fish,ecto:true,independent:true);
            R("freshwater-turtle",D,Loose,Forage,Reptile,1,3,ecto:true);
            R("giant-panda",A,Solo,None,WildlifeSensoryKind.Bear,1,1,2,Defensive);
            R("gibbon",D,Family,Forage,Primate,2,4);
            R("giraffe",A,Loose,None,Ungulate,2,7,2,Defensive);
            R("gorilla",D,Family,Forage,Primate,4,9,4,Defensive);
            R("guereza",D,Troop,Forage,Primate,4,10);
            R("gull",D,Flock,Forage,Bird,3,9);
            R("hedgehog",N,Solo,Forage,Small,hibernate:true);
            R("hippopotamus",N,Herd,None,Ungulate,3,10,9,Defensive);
            R("howler-monkey",D,Troop,Forage,Primate,3,8);
            R("iguana",D,Loose,None,Reptile,1,3,ecto:true);
            R("koala",A,Solo,None,Small,independent:true);
            R("lemur",D,Troop,Forage,Primate,3,9);
            R("long-nosed-bandicoot",N,Solo,Forage,Small);
            R("macaque",D,Troop,Forage,Primate,4,12,1.5,Defensive);
            R("monitor-lizard",D,Solo,Stalk,Reptile,1,1,1,Defensive,ecto:true);
            R("nutria",C,Family,None,Small,2,6);
            R("orangutan",D,Solo,Forage,Primate,1,2,2,Defensive);
            R("orb-weaving-spiders-guild",N,Solo,Ambush,Bug,ecto:true,cheap:true,independent:true);
            R("orca",A,Pod,Water,Marine,3,8);
            R("pangolin",N,Solo,Forage,Carnivore);
            R("penguin",D,Colony,Water,Bird,4,12);
            R("platypus",N,Solo,Forage,Carnivore);
            R("polar-bear",A,Solo,Stalk,WildlifeSensoryKind.Bear,1,1,5,WildlifePeopleResponse.PolarPredation);
            R("porcupine",N,Family,Forage,Small,1,3,1.2,Defensive);
            R("puffin",D,Colony,Water,Bird,3,9);
            R("python",C,Solo,Ambush,Snake,ecto:true,independent:true);
            R("rhinoceros",A,Solo,None,WildlifeSensoryKind.Rhino,1,2,7,Defensive);
            R("sea-lion",A,Colony,Water,Marine,3,9,3,Defensive);
            R("sea-otter",D,Loose,Forage,Marine,2,6);
            R("sea-turtle",A,Solo,Forage,Reptile,1,2,ecto:true);
            R("seal",A,Colony,Water,Marine,3,8,2,Defensive);
            R("shark",A,Solo,Water,Fish,1,1,0,Rare,ecto:true,independent:true);
            R("shorebird",A,Flock,Forage,Bird,4,12);
            R("shrew",A,Solo,Forage,Small,independent:true);
            R("sloth",A,Solo,None,Small,independent:true);
            R("spider-monkey",D,Troop,Forage,Primate,4,10);
            R("tamarin",D,Family,Forage,Primate,3,8);
            R("tern",D,Flock,Water,Bird,3,10);
            R("tortoise",D,Solo,None,Reptile,ecto:true,independent:true);
            R("venomous-snake",C,Solo,Ambush,Snake,1,1,.75,Defensive,ecto:true,independent:true);
            R("walrus",A,Colony,Forage,Marine,3,8,3,Defensive);
            R("whale",A,Loose,Forage,Marine,1,4);
            R("zebra",A,Herd,None,Ungulate,4,12,2,Defensive);
            void Season(string slug,double start,double end){var p=result[slug];p.RutStart=start;p.RutEnd=end;result[slug]=p;}
            // Northern seasonal phase, shifted by half a year in the south.
            // Bison rut in summer; caprines generally breed later than deer.
            Season("bison",.5,.74);Season("moose",.7,.86);Season("mouflon",.79,.99);
            Season("wild-goat",.79,.99);Season("ibex",.79,.99);Season("musk-ox",.6,.83);Season("yak",.6,.83);
            return result;
        }

        public static WildlifeNaturalHistoryProfile Native(string slug,string name)
        {
            if(!profiles.TryGetValue(slug??"",out var p))p=new WildlifeNaturalHistoryProfile{GroupMin=1,GroupMax=1};name=(name??"").ToLowerInvariant();
            if(slug=="deer"&&name.Contains("muntjac")){p.Social=WildlifeSocial.Solitary;p.GroupMin=1;p.GroupMax=2;p.SeasonalRut=false;}
            if(slug=="deer"&&name.Contains("chital")){p.Diel=WildlifeDiel.Cathemeral;p.GroupMin=5;p.GroupMax=14;p.SeasonalRut=false;}
            if(slug=="deer"&&name.Contains("sambar")){p.Diel=WildlifeDiel.Crepuscular;p.Social=WildlifeSocial.Loose;p.GroupMin=1;p.GroupMax=4;p.SeasonalRut=false;}
            if(slug=="deer"&&(name.Contains("elk")||name.Contains("wapiti")||name.Contains("red deer")))p.DefendM=3.5;
            if(slug=="bear"&&name.Contains("black")){p.Diel=WildlifeDiel.Crepuscular;p.DefendM=4;}
            return p;
        }
        public static bool GroundForaging(Food diet)=>(diet&(Food.Grass|Food.Browse|Food.Seed|Food.Fruit|Food.Nectar|Food.Invertebrate|Food.Detritus))!=0;
        public static bool CompatiblePrey(Taxon hunter,Taxon prey)
            =>hunter==null||prey==null||(hunter.Hunts&prey.Occupies)!=0;
        public static bool HuntPerson(WildlifeNaturalHistoryProfile p,double hunger,double boldness,bool vulnerable,bool atWater)
        {
            switch(p.People)
            {
                case WildlifePeopleResponse.PolarPredation:return hunger>.35&&boldness>.2;
                case WildlifePeopleResponse.WaterAmbush:return atWater&&hunger>.45&&boldness>.35;
                case WildlifePeopleResponse.OpportunisticPredation:return hunger>.75&&boldness>.65&&(vulnerable||hunger>.94);
                case WildlifePeopleResponse.RarePredation:return hunger>.94&&boldness>.985&&vulnerable;
                default:return false;
            }
        }
        public static double ThermalMobility(WildlifeNaturalHistoryProfile p,double temperatureC,bool water)
        {
            if(!p.Ectotherm||water||p.Sensory==WildlifeSensoryKind.Fish)return 1;
            // Submerged animals use water temperature, not the air above it.
            // Until that field is available, do not chill a fish or turtle from
            // a cold breeze. Terrestrial ectotherm mobility is gradual, not dead.
            return Math.Max(.08,Math.Min(1,(temperatureC+2)/22));
        }
    }
}
