using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    public readonly struct WildlifeGroundPace
    {
        public readonly double WalkMps,BurstMps;
        public WildlifeGroundPace(double walk,double burst){WalkMps=walk;BurstMps=burst;}
    }
    /// <summary>Conservative locomotion priors in physical metres per second.
    /// Body length includes tails and is not a valid universal speed predictor.
    /// Values describe capable adults on favorable ground, before exertion,
    /// thermal limits and obstacles. They are not claimed record measurements.
    /// Profile preparation performs this lookup once, not in the update loop.</summary>
    public static class WildlifeLocomotion
    {
        static readonly Dictionary<string,WildlifeGroundPace> ground=Create();
        public static bool TryGround(string slug,out WildlifeGroundPace pace)=>ground.TryGetValue(slug??"",out pace);
        public static IReadOnlyDictionary<string,WildlifeGroundPace> Ground=>ground;
        static Dictionary<string,WildlifeGroundPace> Create()
        {
            var p=new Dictionary<string,WildlifeGroundPace>(StringComparer.Ordinal);
            void R(string names,double walk,double burst){foreach(string name in names.Split(' '))p.Add(name,new WildlifeGroundPace(walk,burst));}
            R("deer reindeer",1.1,13);R("moose",1.2,12);
            R("hare",.7,16);R("rabbit",.5,10);R("red-fox",1.1,11);R("wolf",1.3,14);
            R("wild-boar forest-hog warthog",1,10);R("collared-peccary",.8,8);
            R("mouflon wild-goat ibex",.9,10);R("bear polar-bear",1.1,10);
            R("bison buffalo wild-cattle yak",1.1,11);R("musk-ox",.9,9);
            R("horse",1.5,16);R("wild-ass zebra",1.3,15);R("camel guanaco",1.2,12);
            R("gazelle impala",1.1,20);R("pronghorn",1.2,22);R("oryx wildebeest",1.2,17);R("duiker",.8,10);
            R("african-hunting-dog",1.2,17);R("spotted-hyena",1.1,14);
            R("lion leopard puma tiger jaguar",1.1,15);R("lynx",.9,12);R("ocelot",.7,9);
            R("red-kangaroo",1,16);R("wallaby",.7,10);
            R("mouse bank-vole lemming grass-rat cotton-rat",.25,2.5);R("shrew",.15,1.8);
            R("squirrel ground-squirrel",.45,6);R("pika",.3,4);R("jerboa",.4,7);
            R("raccoon",.6,5);R("badger",.6,6);R("beaver",.5,3);R("otter",.6,4);
            R("civet mongoose",.6,7);R("weasel",.4,5);R("wolverine",.7,9);
            R("marmot",.4,4);R("meerkat",.5,6);R("agouti paca",.6,7);
            R("capybara",.7,8);R("nutria",.5,4);R("tapir",.8,10);R("platypus",.25,1.2);
            R("skunk porcupine",.4,2);R("wombat",.6,8);R("spotted-tailed-quoll",.6,7);
            R("common-brushtail-possum",.4,4);R("long-nosed-bandicoot",.4,5);
            R("aardvark",.7,6);R("anteater",.6,5);R("armadillo",.4,5);
            R("pangolin",.25,1.5);R("echidna hedgehog",.2,1);
            R("elephant",1.4,7);R("giraffe",1.5,14);R("rhinoceros",1.1,12);R("hippopotamus",1,8);
            R("baboon",.8,9);R("chimpanzee gorilla",.7,7);R("macaque",.6,7);
            R("capuchin guereza lemur spider-monkey tamarin",.45,5);
            R("gibbon howler-monkey",.4,4);R("orangutan",.35,2.5);R("giant-panda",.5,5);R("koala",.25,2);
            R("sloth",.035,.12);
            R("pheasant grouse junglefowl",.45,4);R("peafowl",.6,4);R("hornbill",.3,2);
            R("kiwi",.4,3);R("penguin",.35,1.5);R("ostrich",1.3,18);R("emu",1.1,13);R("cassowary",.9,11);
            R("crocodile",.4,4);R("boa-constrictor python",.15,1);R("venomous-snake",.3,2);
            R("iguana",.35,3);R("monitor-lizard",.5,5);R("native-small-lizards-guild",.2,2);
            R("freshwater-turtle",.06,.25);R("tortoise",.04,.15);R("salamanders-guild",.03,.2);
            R("native-litter-beetles-guild",.025,.12);R("orb-weaving-spiders-guild",.04,.25);
            R("grasshoppers-guild",.03,.2);
            return p;
        }
        /// <summary>Look ahead through the next scheduled obstacle decision and
        /// enough braking distance; still only the existing three bounded rays.</summary>
        public static double ObstacleLookahead(double speed,double burst,double interval,double radius)
        {
            double braking=speed*speed/(2*Math.Max(.225,burst*1.5));
            return Math.Min(40,Math.Max(1.8,radius+speed*(interval+.15)+braking));
        }
    }
}
