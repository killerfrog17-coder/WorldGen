using System;

namespace WorldGen.Core
{
    /// <summary>Audibility tuning in world metres. Applied once, independently
    /// of the graphics radius or calendar compression.</summary>
    public static class WildlifeAcoustics
    {
        public static double AmbientRange(FaunaSoundKind kind)=>kind==FaunaSoundKind.Cricket||kind==FaunaSoundKind.Katydid?45:kind==FaunaSoundKind.Cicada?90:kind==FaunaSoundKind.Frog?65:kind==FaunaSoundKind.Owl?180:120;
        public static double AmbientReference(FaunaSoundKind kind)=>kind==FaunaSoundKind.Cricket||kind==FaunaSoundKind.Katydid?1.5:kind==FaunaSoundKind.Cicada?3:3;
        public static double AmbientLevel(FaunaSoundKind kind)=>kind==FaunaSoundKind.Cicada?.42:kind==FaunaSoundKind.Cricket||kind==FaunaSoundKind.Katydid?.5:.65;
        public static double Range(string bank,string cue)=>bank=="bee-wings"?3.5:bank=="fly-wings"?1.8:bank=="hummingbird-wings"?6:cue=="howl"?350:cue=="ambient"?120:cue=="contact"?65:150;
        public static double Reference(string bank,string cue)=>bank=="bee-wings"?.14:bank=="fly-wings"?.09:bank=="hummingbird-wings"?.35:cue=="howl"?7:cue=="contact"?1.5:3;
        public static double Gain(double distance,double reference,double range)
        {
            if(distance>=range||range<=0)return 0;
            double fade=Math.Max(0,Math.Min(1,(range-distance)/(range*.2)));fade=fade*fade*(3-2*fade);
            return Math.Min(1,reference/Math.Max(reference,distance))*fade;
        }
        public static double CallingHour(double hour,uint identity)=>hour+((identity%1001)/1000d-.5)*1.5;
        public static double DayActivity(double solarAltitude,bool nocturnal)
        {double x=Math.Max(0,Math.Min(1,(solarAltitude+6)/12));x=x*x*(3-2*x);return nocturnal?1-x:x;}
        public static ulong TreeIdentity(ulong tree,FaunaSoundKind kind)=>tree^((ulong)((int)kind+1)*0x9e3779b97f4a7c15UL);
    }
}
