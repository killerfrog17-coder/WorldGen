using System;

namespace WorldGen.Presentation
{
    /// <summary>Bounded render influence. Low-angle light never admits an
    /// unlimited upstream forest; broad cached canopy shelter covers the rest.</summary>
    public static class VegetationShadowBudget
    {
        public static double CasterLimit(double receiverDistance)=>Math.Max(0,receiverDistance)+76;
        public static double Slope(double lightUp)
        {double y=Math.Max(.001,Math.Min(1,lightUp));return Math.Sqrt(Math.Max(0,1-y*y))/y;}
        public static double Reach(double receiverDistance,double topAboveEye,double lightUp)
            =>ReachWithSlope(receiverDistance,topAboveEye,Slope(lightUp));
        public static double ReachWithSlope(double receiverDistance,double topAboveEye,double slope)
        {
            double radius=Math.Max(0,receiverDistance)+8;
            return Math.Min(CasterLimit(receiverDistance),radius+Math.Max(0,topAboveEye+radius)*slope);
        }
        static double Smooth(double t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        public static double CasterFade(double distance,double receiverDistance)=>1-Smooth((distance-(Math.Max(0,receiverDistance)+44))/32);
        public static double CardWeight(double lightUp)=>Smooth((lightUp-.20)/.10);
        public static double ShadowRange(double lightUp)=>60+120*Smooth((lightUp-.05)/.30);
        public static int ResolutionCap(double lightUp)=>lightUp<.10?1024:lightUp<.28?2048:4096;
        public static double DirectSunlight(double lightUp)=>Smooth((lightUp-Math.Sin(2.5*Math.PI/180))/(Math.Sin(8*Math.PI/180)-Math.Sin(2.5*Math.PI/180)));
    }
}
