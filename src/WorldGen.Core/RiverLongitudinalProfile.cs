using System;

namespace WorldGen.Core
{
    /// <summary>Prepared-reach channel variation in physical metres. This is a
    /// static geomorphic approximation, not an erosion or water simulation.
    /// Sample only while constructing immutable stations, never per terrain vertex.</summary>
    public readonly struct RiverLongitudinalProfile
    {
        public readonly double WidthScale,DepthScale;
        RiverLongitudinalProfile(double width,double depth){WidthScale=width;DepthScale=depth;}
        static double Noise(double distance,double wavelength,ulong key,uint stream)
        {
            double q=distance/wavelength;int index=(int)Math.Floor(q);double t=RiverscapeKernel.Smooth(q-index);
            double At(int i)=>(Rng.Mix((uint)key^Rng.Mix((uint)(key>>32))^Rng.Mix(unchecked((uint)i+stream)))>>8)/8388607.5-1;
            double a=At(index),b=At(index+1);return a+(b-a)*t;
        }
        public static RiverLongitudinalProfile At(double distanceM,double halfWidthM,double grade,double sediment,double hardness,double bend,ulong key)
        {
            double width=Math.Max(.6,halfWidthM);
            double freedom=(1-.8*OrogenMorphology.Smooth(.006,.06,grade))*(1-.65*D.Clamp(hardness));
            double broad=Noise(distanceM,Math.Max(140,width*28),key,0x51a7u);
            // Pool/riffle scales follow channel size, rather than restarting
            // exactly two/five sine waves on every arbitrary graph segment.
            double bed=Noise(distanceM,Math.Max(24,width*(7+5*D.Clamp(sediment))),key,0x7f43u);
            double local=Noise(distanceM,Math.Max(18,width*4),key,0x941bu);
            double bendPool=D.Clamp(Math.Abs(bend));
            double widthScale=D.Clamp(1+freedom*(.22*broad+.13*bed+.06*local-.04*bendPool),.68,1.42);
            double depthScale=D.Clamp(.94+freedom*(.16*broad-.30*bed+.12*local+.48*bendPool),.48,1.72);
            return new RiverLongitudinalProfile(widthScale,depthScale);
        }
    }
}
