using System;

namespace WorldGen.Core
{
    /// <summary>Compact riverbed/water primitives inspired by Procedural Riverscapes
    /// (Peytavie et al.,2019). Our own bounded implementation, not a fluid solver.
    /// Discharge and gradient are independent: a small creek can also be calm.</summary>
    public readonly struct RiverSection
    {
        public readonly double Level,HalfWidth,Depth,Support,Speed,Turbulence;
        public RiverSection(double level,double halfWidth,double depth,double discharge,double slope,double sediment,double supportLimit=double.PositiveInfinity)
        {
            Level=level;HalfWidth=Math.Max(.01,halfWidth);Depth=Math.Max(0,depth);
            Support=Math.Min(supportLimit,Math.Max(HalfWidth*5,Math.Min(3000,80+HalfWidth*3+sediment*400)));
            // Geometry/animation estimate, not an assertion of hydraulic balance.
            Speed=Math.Max(.08,Math.Min(7,Math.Sqrt(Math.Max(0,slope)*9.81*Math.Max(.1,depth))*2));
            Turbulence=OrogenMorphology.Smooth(.004,.09,slope)*(1-.35*D.Clamp(sediment));
        }
        public double Cut(double terrain,double distance,double alongM,double phase)
        {
            if(distance>=Support||Depth<=.001)return terrain;
            double q=distance/HalfWidth;
            double profile;
            if(q<=1)
            {
                double bed=Level-Depth*(1-q*q);
                // Submerged/occasionally exposed rough bedforms. Their phase
                // is continuous in physical space, never reset at tile borders.
                double rock=Math.Sin(alongM*.19+phase)*Math.Sin(alongM*.071-phase);
                bed+=Depth*Turbulence*.8*Math.Max(0,rock)*Math.Pow(Math.Max(0,1-q*q),2);
                profile=bed;
            }
            else profile=Level+(.3+Depth*.2)*(q-1);
            double blend=OrogenMorphology.Smooth(Math.Max(HalfWidth*1.5,Support*.25),Support,distance);
            return Math.Min(terrain,profile+(terrain-profile)*blend);
        }
        public static double Longitudinal(double t,double slope,double hardness,double variation)
        {
            t=D.Clamp(t);
            if(slope<.006)return t;
            // A monotone shelf/drop template: endpoint values stay identical.
            // Later rendering can replace a sufficiently sharp drop with a
            // detached fall sheet without changing the catchment or bed.
            double centre=.3+.4*variation;
            double width=.42-.34*hardness*OrogenMorphology.Smooth(.02,.2,slope);
            double drop=OrogenMorphology.Smooth(centre-width*.5,centre+width*.5,t);
            double strength=.75*OrogenMorphology.Smooth(.006,.08,slope);
            return t*(1-strength)+drop*strength;
        }
    }
}
