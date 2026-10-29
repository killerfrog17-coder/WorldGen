using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    public static class WildlifeVocalisation
    {
        static readonly Dictionary<string,string> banks=Create();
        static Dictionary<string,string> Create()
        {
            var result=new Dictionary<string,string>(StringComparer.Ordinal);
            void Add(string bank,params string[] models){foreach(string model in models)result[model]=bank;}
            Add("deer","deer","reindeer","duiker","impala","gazelle","pronghorn");Add("moose","moose");
            Add("wolf","wolf","african-hunting-dog");Add("fox","red-fox");Add("bear","bear","polar-bear");
            Add("boar","wild-boar","forest-hog","warthog","collared-peccary");
            Add("bovid","bison","wild-cattle","buffalo","musk-ox","yak","wildebeest");
            Add("goat","wild-goat","ibex");Add("sheep","mouflon");Add("horse","horse","wild-ass","zebra");
            Add("rabbit","rabbit","hare","pika");Add("owl","owl");Add("duck","duck");Add("goose","goose","swan");
            Add("pheasant","pheasant","junglefowl","turkey","peafowl");Add("grouse","grouse");Add("quail","quail");
            Add("gull","gull","tern");Add("raptor","hawk","falcon","fish-eagle","forest-eagle");
            Add("lion","lion");Add("big-cat","lynx","jaguar","leopard","ocelot","puma","tiger");
            Add("howler","howler-monkey");Add("baboon","baboon");Add("crane","crane");
            Add("bee-wings","bees-guild");Add("fly-wings","native-detritivorous-flies-guild");
            foreach(string bank in FaunaAmbience.Banks)if(bank!="bird"&&bank!="cricket"&&bank!="cicada"&&bank!="katydid"&&bank!="frog")result[bank]=bank;
            return result;
        }
        // Canonical exported model ID, never a translated/display species name.
        public static string Bank(string model)=>model!=null&&banks.TryGetValue(model,out string bank)?bank:null;
        public static bool FlightNoise(string bank)=>bank=="bee-wings"||bank=="fly-wings"||bank=="hummingbird-wings";
        public static double ReferenceMass(string bank)=>bank=="deer"?90:bank=="moose"?400:bank=="wolf"?40:bank=="fox"?6:bank=="bear"?200:bank=="boar"?80:bank=="bovid"?500:bank=="goat"||bank=="sheep"?45:bank=="horse"?350:bank=="big-cat"?60:bank=="lion"?170:1;
        public static double CalmGap(string bank)=>bank=="owl"?65:bank=="wolf"?95:bank=="deer"||bank=="moose"?90:bank=="rabbit"?300:bank=="howler"?90:bank=="bee-wings"||bank=="fly-wings"?1:24;
    }
}
