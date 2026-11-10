using System;

namespace WorldGen.Core
{
    /// <summary>Bounded hydraulic-geometry approximation. Source discharge is
    /// m3/s on the source globe, not a river rank or a compressed catchment.
    /// World scale changes the spacing/admission of channels, never the physical
    /// size or discharge of an admitted channel.</summary>
    public static class RiverHydraulics
    {
        public const double MinimumLocalDischarge=.1;
        public static double Discharge(double sourceDischarge,double worldScale)
            =>Math.Max(0,sourceDischarge);
        public static double HalfWidth(double sourceDischarge,double worldScale)
            =>Math.Max(.6,Math.Min(2000,3*Math.Sqrt(Math.Max(0,sourceDischarge))));
        public static double Depth(double sourceDischarge,double worldScale,double halfWidth)
            =>Math.Min(Math.Min(55,halfWidth*.75),.25+.7*Math.Pow(Discharge(sourceDischarge,worldScale),.35));
    }
}
