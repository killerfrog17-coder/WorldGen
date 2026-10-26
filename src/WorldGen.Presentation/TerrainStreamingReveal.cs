using System;

namespace WorldGen.Presentation
{
    /// <summary>Initial loading reveals outward. Later LOD/forest replans can
    /// hold expansion, but cannot erase a horizon the player has already seen.
    /// Teleports start a new reveal; an explicit view-distance reduction caps it.</summary>
    public static class TerrainStreamingReveal
    {
        public const double InitialDistance=8;
        public static double Advance(double current,double ready,double limit,double seconds)
        {
            limit=Math.Max(0,limit);current=Math.Min(limit,Math.Max(0,current));
            double target=Math.Min(limit,Math.Max(current,ready));
            return Math.Min(target,current+Math.Max(3000,current*2)*Math.Max(0,seconds));
        }
    }
}
