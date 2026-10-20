using System;

namespace WorldGen.Core
{
    /// <summary>Compact, oriented sparse phasor pattern. Fixed27-kernel cost,
    /// no neighbourhood terrain simulation or chunk-relative phase.</summary>
    public static class TerrainRavineNoise
    {
        public static double Sample(V metres,V across,int seed,double wavelength)
        {
            double x=metres.X/wavelength,y=metres.Y/wavelength,z=metres.Z/wavelength;
            int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y),iz=(int)Math.Floor(z);
            double real=0,imaginary=0,sum=0;
            for(int k=-1;k<=1;k++)for(int j=-1;j<=1;j++)for(int i=-1;i<=1;i++)
            {
                uint hash=Rng.Mix(unchecked((uint)seed^(uint)(ix+i)*0x8da6b343u^(uint)(iy+j)*0xd8163841u^(uint)(iz+k)*0xcb1ab31fu));
                double dx=x-(ix+i+.5),dy=y-(iy+j+.5),dz=z-(iz+k+.5),r2=(dx*dx+dy*dy+dz*dz)/2.25;
                if(r2>=1)continue;double w=1-r2;w*=w*w;
                double phase=(dx*across.X+dy*across.Y+dz*across.Z)*2*Math.PI+hash*(2*Math.PI/uint.MaxValue);
                real+=w*Math.Cos(phase);imaginary+=w*Math.Sin(phase);sum+=w;
            }
            if(sum<1e-12)return 0;
            // Magnitude attenuation removes hard singularities at pattern forks.
            double magnitude=Math.Sqrt(real*real+imaginary*imaginary+.04*sum*sum);
            double wave=real/magnitude;
            return (wave*.5+.5)*Math.Min(1,magnitude/sum);
        }
    }
}
