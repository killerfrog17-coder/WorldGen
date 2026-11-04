using System;

namespace WorldGen.Core
{
    /// <summary>Bounded regional erosion forms attached to continuous lithology
    /// and climate controls. Shared CPU heights, not decorative feature meshes.</summary>
    public static class ErodedTerrain
    {
        static double Smooth(double a,double b,double value)=>RiverscapeKernel.Smooth((value-a)/(b-a));
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
                double wet=Smooth(.45,.80,controls.Moisture)*(1-controls.Glacial);
                // Fine dissolution grooves perturb the planform of exposed
                // humid walls. Their support retires before broad valley floors
                // and tower crowns; this is shared physical relief, not decals.
                double wall=Smooth(.47,.55,field)*(1-Smooth(.73,.81,field));
                if(wet*wall>.001)field+=.016*wet*wall*SphereNoise.Sample(metres,seed^0x4f731,1/32d);
                double cones=Math.Pow(Smooth(.25,.85,field),1.4);
                // Humid tower karst has isolated steep residuals above broad
                // low corridors. The old threshold lifted most of the field
                // into an undifferentiated plateau of rounded hilltops.
                double towers=Smooth(.53,.72,field)*(.85+.15*Smooth(.72,.91,field));
                double variation=.5+.5*SphereNoise.Sample(metres,seed^0x531d7,1/2700d);
                double scale=65+105*variation+wet*(55+45*variation);
                double doline=Smooth(.60,.86,.5+.5*SphereNoise.Sample(metres,seed^0x152d3,1/110d))*(1-cones);
                offset+=controls.Karst*(1-.6*controls.Glacial)*(scale*(cones*(1-wet)+towers*wet)-24*doline);
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
            return offset;
        }
    }
}
