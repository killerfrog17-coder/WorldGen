using System;

namespace WorldGen.Core
{
    public readonly struct PleistoceneConditions
    {
        public readonly double Glacial,TemperatureDeltaC,PrecipitationFactor,SeaLevelM,IceSuitability;
        public PleistoceneConditions(double glacial,double temperature,double rain,double sea,double ice)
        {Glacial=glacial;TemperatureDeltaC=temperature;PrecipitationFactor=rain;SeaLevelM=sea;IceSuitability=ice;}
    }
    /// <summary>Regional seasonal glacial envelopes, not a palaeoclimate reconstruction/GCM.
    /// Does not mutate saved terrain, climate or generated ecological region identities.</summary>
    public static class PleistoceneClimate
    {
        public const int DefaultOriginYearsBeforePresent=2800000;
        static double Clamp(double n,double a=0,double b=1)=>Math.Max(a,Math.Min(b,n));
        static double Smooth(double n){n=Clamp(n);return n*n*(3-2*n);}
        public static double GlacialFraction(int seed,int yearsBeforePresent)
        {
            double bp=Math.Max(0,yearsBeforePresent),transition=Smooth((1200000-bp)/500000);
            // Integrate cycle frequency continuously through the mid-Pleistocene
            // transition; no phase jump when moving between regime envelopes.
            double ancient=Math.Max(0,bp-1200000)/41000,mid=Math.Min(500000,Math.Max(0,bp-700000));
            double phase=ancient+mid/65000+Math.Min(700000,bp)/100000+Rng.Value(seed,"pleistocene-phase",0)*.02;
            phase-=Math.Floor(phase);
            // This coordinate runs BACKWARD from the reference interglacial:
            // fast warming forward in time is the short rising limb in BP time.
            double saw=phase<.2?Smooth(phase/.2):1-Smooth((phase-.2)/.8);
            double precession=Math.Sin(bp/23000*2*Math.PI+Rng.Value(seed,"pleistocene-precession",0)*6.28);
            return Clamp(saw*(.58+.42*transition)+precession*.06);
        }
        public static PleistoceneConditions At(int seed,int yearsBeforePresent,double latitude,double longitude,double elevationM,int season=0)
        {
            if(!SurvivalMath.Finite(latitude)||!SurvivalMath.Finite(longitude)||!SurvivalMath.Finite(elevationM))throw new ArgumentException("Invalid climate location.");
            latitude=Clamp(latitude,-90,90);double glacial=GlacialFraction(seed,yearsBeforePresent),abs=Math.Abs(latitude)/90;
            double regional=Math.Sin(longitude*Math.PI/90+latitude*Math.PI/180)*.5+.5;
            double summer=(season==1&&latitude>=0||season==3&&latitude<0)?1:0;
            double cooling=glacial*(2.5+9*Math.Pow(abs,1.3)+Math.Max(0,elevationM)*.0004)*(1-.18*summer);
            double rain=Clamp(1-glacial*(.12+.30*(1-abs))+.12*glacial*Math.Sin(longitude*Math.PI/90+latitude*.04),.42,1.15);
            // Ice retains a lagged climate signal rather than following every
            // short fluctuation. Geography/sea routes consume the same sea level.
            double iceCycle=GlacialFraction(seed,yearsBeforePresent+3000);
            double ice=Smooth((Math.Abs(latitude)-(72-iceCycle*(latitude>0?20:12))+Math.Max(0,elevationM-900)/220)/12)*iceCycle;
            return new PleistoceneConditions(glacial,-cooling,rain,-120*iceCycle,ice);
        }
        public static double CapacityFactor(HabitatCell h,PleistoceneConditions climate,double coldAdaptation,bool fire,bool clothing)
        {
            if(climate.IceSuitability>.75)return 0;
            double mean=h.MeanTenthsC/10d+climate.TemperatureDeltaC;
            double protection=5*coldAdaptation+(fire?4:0)+(clothing?7:0);
            double cold=Clamp((mean+8+protection)/18,.015,1);
            double aridity=h.AridityMilli/1000d*climate.PrecipitationFactor;
            double water=Clamp(aridity/.35,.03,1.2);
            return Clamp(cold*water*(1-climate.IceSuitability),0,1.3);
        }
    }
}
