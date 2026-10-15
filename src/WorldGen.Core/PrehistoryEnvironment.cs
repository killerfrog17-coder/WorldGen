using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // Intrinsic expansion into available habitat is not the long-run net growth
        // of a population constrained by resources. Keep the old crowded-population
        // rate; add a bounded low-density response, with no geography/ancestry bonus.
        // Century coefficients are game priors, not inferred archaeological rates.
        public static double FrontierGrowthPerCentury(long population,long capacity)
        {
            double room=D.Clamp(1-population/(double)Math.Max(1,capacity));
            return D.Q(.048+.16*room*room,1000000);
        }
        public static double RegionalEmergenceExposure(long population)=>D.Q(Math.Max(1,Math.Min(4,D.Sqrt(Math.Max(0,population)/2000.0))),1000000);
        // Slow accumulation, relatively rapid deglaciation. Elapsed years, not a reconstruction of Earth dates.
        public static double GlacialIntensity(int years)
        {
            double t=(years%100000)/100000.0;
            double x=t<.85?t/.85:(1-t)/.15;
            return D.Q(x*x*(3-2*x),1000000);
        }
        public static int SeaLevelDropM(int years)=>(int)Math.Round(130*GlacialIntensity(years));
        public string PatchName(int patch)=>Patches[patch].Island?"Island refuge "+Patches[patch].Cell:World.Ecology.Regions[Patches[patch].Region].Name;
        public long CapacityAt(int patch,int years,bool glaciation=true)
        {
            var p=Patches[patch];var h=World.Ecology.Habitat[p.Cell];
            int epoch=years/2000;double blend=(years%2000)/2000.0;blend=blend*blend*(3-2*blend);
            double a=Rng.Value(World.Recipe.Seed,"human-resources",unchecked(patch*104729+epoch));
            double b=Rng.Value(World.Recipe.Seed,"human-resources",unchecked(patch*104729+epoch+1));
            double weather=1+.32*(a+(b-a)*blend-.5);
            double cold=D.Clamp((120-h.MeanTenthsC)/220.0);
            double ice=1-(glaciation?GlacialIntensity(years):0)*(.08+.65*cold);
            return Math.Max(0,(long)Math.Round(p.Capacity*weather*ice));
        }
        public double HabitatSimilarity(int a,int b)
        {
            var pa=Patches[a];var pb=Patches[b];var x=World.Ecology.Habitat[pa.Cell];var y=World.Ecology.Habitat[pb.Cell];
            double difference=Math.Abs(x.MeanTenthsC-y.MeanTenthsC)/180.0+Math.Abs(x.AridityMilli-y.AridityMilli)/1200.0;
            if(x.Biome!=y.Biome)difference+=pa.Lifestyle==pb.Lifestyle?.35:1.1;
            return D.Q(Math.Exp(-difference),1000000);
        }
        public static double RouteChance(Route route,int years)
        {
            if(!route.Sea)return 1;
            if(route.Coastal)return D.Q(.3*Math.Exp(-route.DistanceM/350000.0),1000000000);
            if(route.Shallow)return SeaLevelDropM(years)>=route.SeaDropM?.8:0;
            // No glacial bonus for deep ocean. A distant voyage remains rare even after many millennia.
            return D.Q(.35*Math.Exp(-route.DistanceM/70000.0),1000000000);
        }
        public static double MaritimeChance(Route route,int navigation)
        {
            if(!route.Sea||navigation<=0)return 0;
            return D.Q((navigation>=2?.35:.12)*Math.Exp(-route.DistanceM/(navigation>=2?450000.0:180000.0)),1000000000);
        }
        IEnumerable<Route> ActiveRoutes(int years,int[] navigation=null)
        {
            // Retain distinct shelf-depth alternatives; choose the best currently accessible route once per pair.
            var best=new SortedDictionary<(int,int),Route>();
            double Chance(Route r)=>Math.Max(RouteChance(r,years),navigation==null?0:MaritimeChance(r,Math.Max(navigation[r.A],navigation[r.B])));
            foreach(var r in Routes){var key=(r.A,r.B);if(!best.TryGetValue(key,out var old)||Chance(r)>Chance(old))best[key]=r;}
            return best.Values;
        }
        public double DispersalChance(Route route,int a,int b,long population,long capacity,int years,int residenceYears)
        {
            double similarity=HabitatSimilarity(a,b),crowding=D.Clamp(population/(double)Math.Max(1,capacity));
            double exploration=D.Clamp(population/(150+850*(1-similarity)*(1-similarity)));
            double willingness=.05+.75*similarity+.55*crowding*crowding;
            double experience=route.Sea&&!route.Shallow?.35+.65*(1-Math.Exp(-Math.Max(0,residenceYears)/10000.0)):1;
            return D.Q(D.Clamp(RouteChance(route,years)*exploration*willingness*experience),1000000000);
        }
    }
}
