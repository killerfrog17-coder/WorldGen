using System;

namespace WorldGen.Presentation
{
    /// <summary>Render budgets only; never changes tree populations or placement.</summary>
    public static class TreeRenderingLod
    {
        public const double ShadowStart=64,ShadowEnd=96;
        public static double ViewStart(int quality)
        {switch(Math.Max(0,Math.Min(4,quality))){case 0:return 80;case 1:return 100;case 2:return 120;case 3:return 160;default:return 220;}}
        public static double PixelStart(int quality)=>12-Math.Max(0,Math.Min(4,quality))*1.75;
        public static double ShadowWeight(double distance)
        {double t=Math.Max(0,Math.Min(1,(distance-ShadowStart)/(ShadowEnd-ShadowStart)));return t*t*(3-2*t);}
    }
}
