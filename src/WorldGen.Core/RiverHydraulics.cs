using System;

namespace WorldGen.Core
{
    /// <summary>Bounded hydraulic-geometry approximation. Source discharge is
    /// m3/s, not a river rank. Small catchments follow world compression; major
    /// channels smoothly recover physical dimensions instead of all hitting the
    /// old 120m-wide limit on a tenth-scale planet.</summary>
    public static class RiverHydraulics
    {
        public const double MinimumLocalDischarge=.1;
        public static double Compression(double sourceDischarge,double worldScale)
        {
            double t=D.Clamp((Math.Log10(Math.Max(1,sourceDischarge))-2)/2);
            t=t*t*(3-2*t);return worldScale+(1-worldScale)*t;
        }
        public static double Discharge(double sourceDischarge,double worldScale)
        {double c=Compression(sourceDischarge,worldScale);return Math.Max(0,sourceDischarge)*c*c;}
        public static double HalfWidth(double sourceDischarge,double worldScale)
            =>Math.Max(.6,Math.Min(2000,3*Math.Sqrt(Math.Max(0,sourceDischarge))*Compression(sourceDischarge,worldScale)));
        public static double Depth(double sourceDischarge,double worldScale,double halfWidth)
            =>Math.Min(Math.Min(55,halfWidth*.75),.25+.7*Math.Pow(Discharge(sourceDischarge,worldScale),.35));
    }
}
