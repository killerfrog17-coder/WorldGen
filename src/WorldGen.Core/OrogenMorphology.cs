using System;

namespace WorldGen.Core
{
    /// <summary>3.33 continental landform priors, in metres. Not a tectonic history simulation.</summary>
    public static class OrogenMorphology
    {
        public static double Smooth(double a,double b,double value)
        { double t=D.Clamp((value-a)/(b-a));return t*t*(3-2*t); }
        public static double Retention(double lifetime,double segment)
            => (.28+2.05*lifetime*lifetime)*(.72+.38*D.Clamp(segment));
        // Saturate the baseline physical mechanism, not the owner's percentage.
        // Extreme custom settings remain genuinely extreme; 100% has Earth-like
        // crustal-elevation limits without clipping all ranges to one peak height.
        public static double Uplift(double raw,double limit,double strength)
            => strength<=0?0:limit*(1-Math.Exp(-Math.Max(0,raw)/(limit*strength)))*strength;
        // A narrow rugged front and a much broader hinterland surface. The latter
        // is not another mountain range, and tapers gently toward the interior.
        public static double CollisionWidth(double widthVariation)=>230000*widthVariation;
        public static double PlateauEnvelope(double distance,double width)
            => Smooth(45000,230000,distance)*(1-Smooth(width*.50,width,distance));
        public static double ContinentalHeight(double height)
            => height>8500?8500+(height-8500)*.15:height;
    }
}
