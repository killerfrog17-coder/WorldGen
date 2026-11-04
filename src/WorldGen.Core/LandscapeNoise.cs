using System;

namespace WorldGen.Core
{
    /// <summary>Continuous bounded value noise for local erosional detail.
    /// Eight integer hashes, no allocation or chunk-relative coordinates.</summary>
    public static class LandscapeNoise
    {
        public static double Value(V metres,int seed,double frequency)
        {
            double x=metres.X*frequency,y=metres.Y*frequency,z=metres.Z*frequency;
            int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y),iz=(int)Math.Floor(z);
            double Ease(double t)=>t*t*t*(t*(t*6-15)+10);
            double Mix(double a,double b,double t)=>a+(b-a)*t;
            double Corner(int a,int b,int c)=>Rng.Mix(unchecked((uint)seed^(uint)a*0x8da6b343u^(uint)b*0xd8163841u^(uint)c*0xcb1ab31fu))*(2d/uint.MaxValue)-1;
            double u=Ease(x-ix),v=Ease(y-iy),w=Ease(z-iz);
            return Mix(Mix(Mix(Corner(ix,iy,iz),Corner(ix+1,iy,iz),u),Mix(Corner(ix,iy+1,iz),Corner(ix+1,iy+1,iz),u),v),
                Mix(Mix(Corner(ix,iy,iz+1),Corner(ix+1,iy,iz+1),u),Mix(Corner(ix,iy+1,iz+1),Corner(ix+1,iy+1,iz+1),u),v),w);
        }
    }
}
