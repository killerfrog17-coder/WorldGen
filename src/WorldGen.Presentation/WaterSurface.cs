using System;
namespace WorldGen.Presentation
{
    public static class WaterSurface
    {
        // Same two dispersive swells as WorldGenWaterMotion.hlsl. Short waves
        // are normal-map detail, not extra geometry or collision tessellation.
        public static double Swell(double x,double z,double seconds,double windX,double windZ,double depth,bool ocean)
        {
            double speed=Math.Sqrt(windX*windX+windZ*windZ),dx=speed>.01?windX/speed:.8,dz=speed>.01?windZ/speed:.6;
            double a=(.12+.98*Math.Min(1,speed/14))*(ocean?1:.2)*Math.Min(1,Math.Max(0,depth/2.5));
            double k=2*Math.PI/75,l=2*Math.PI/43;
            return a*(Math.Sin((x*dx+z*dz)*k-seconds*Math.Sqrt(9.81*k))+.45*Math.Sin((x*(-dz*.6+dx*.8)+z*(dx*.6+dz*.8))*l-seconds*Math.Sqrt(9.81*l)));
        }
    }
}
