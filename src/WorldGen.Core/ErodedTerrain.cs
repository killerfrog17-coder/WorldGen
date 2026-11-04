using System;

namespace WorldGen.Core
{
    /// <summary>Bounded regional erosion forms attached to continuous lithology
    /// and climate controls. Shared CPU heights, not decorative feature meshes.</summary>
    public static class ErodedTerrain
    {
        static double Smooth(double a,double b,double value)=>RiverscapeKernel.Smooth((value-a)/(b-a));
        public static double Remnant(V metres,int seed,LandscapeControls controls)
        {
            if(controls.Remnant<.05)return 0;
            double province=Smooth(.62,.79,.5+.5*SphereNoise.Sample(metres,seed^0x6c251,1/4800d));
            if(province<.001)return 0;
            double rock=.5+.5*SphereNoise.Sample(metres,seed^0x1a4b7,1/650d);
            return controls.Remnant*province*Math.Pow(Smooth(.51,.79,rock),.65);
        }
        public static double HeightOffset(V metres,int seed,LandscapeControls controls)
        {
            double offset=0;
            if(controls.Badlands>.01)
            {
                double valley=1-Math.Abs(SphereNoise.Sample(metres,seed^0x39171,1/260d)
                    +.12*SphereNoise.Sample(metres,seed^0x29317,1/85d));
                double maturity=.35+.65*(.5+.5*SphereNoise.Sample(metres,seed^0x6b731,1/900d));
                double rill=1-Math.Abs(SphereNoise.Sample(metres,seed^0x194b7,1/38d));
                offset-=controls.Badlands*(45*Math.Pow(Smooth(.15,.88,valley),2)*maturity+6*Math.Pow(rill,6));
            }
            if(controls.Karst>.01)
            {
                double field=.5+.5*SphereNoise.Sample(metres,seed^0x1d731,1/520d)
                    +.08*SphereNoise.Sample(metres,seed^0x7e351,1/155d);
                double wet=Smooth(.45,.80,controls.Moisture);
                double cones=Math.Pow(Smooth(.25,.85,field),1.4);
                double towers=Smooth(.30,.62,field)*(.70+.30*Smooth(.62,.91,field));
                double scale=65+105*(.5+.5*SphereNoise.Sample(metres,seed^0x531d7,1/2700d));
                double doline=Smooth(.60,.86,.5+.5*SphereNoise.Sample(metres,seed^0x152d3,1/110d))*(1-cones);
                offset+=controls.Karst*(scale*(cones*(1-wet)+towers*wet)-24*doline);
            }
            if(controls.Loess>.01)
            {
                // Most loess remains a plain; dissection is concentrated into
                // coherent provinces, rather than adding hills everywhere.
                double province=Smooth(.48,.75,.5+.5*SphereNoise.Sample(metres,seed^0x65b31,1/2400d));
                if(province>.001)
                {
                    double hills=SphereNoise.Sample(metres,seed^0x731b1,1/320d);
                    double valley=1-Math.Abs(SphereNoise.Sample(metres,seed^0x3c171,1/190d));
                    offset+=controls.Loess*province*(28*hills-15*valley*valley*valley);
                }
            }
            offset+=220*Remnant(metres,seed,controls);
            return offset;
        }
    }
}
