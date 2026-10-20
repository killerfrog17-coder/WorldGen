using System;

namespace WorldGen.Core
{
    /// <summary>Bounded, planet-addressed multifractal. Analytic gradients steer
    /// finer relief towards coarse structure without periodic ridge bands or
    /// neighbouring height queries. Independent implementation; see research notes.</summary>
    public static class TerrainFractal
    {
        public const int Octaves=6;

        // Derivative of the simplex corner kernel t^4 dot(g,p).
        public static double Noise(V p,int seed,out V gradient)
        {
            double skew=(p.X+p.Y+p.Z)/3;
            int i=(int)Math.Floor(p.X+skew),j=(int)Math.Floor(p.Y+skew),k=(int)Math.Floor(p.Z+skew);
            double unskew=(i+j+k)/6.0,x=p.X-i+unskew,y=p.Y-j+unskew,z=p.Z-k+unskew;
            int rx=(x>=y?1:0)+(x>=z?1:0),ry=(y>x?1:0)+(y>=z?1:0),rz=(z>x?1:0)+(z>y?1:0);
            int x1=rx==2?1:0,y1=ry==2?1:0,z1=rz==2?1:0,x2=rx>=1?1:0,y2=ry>=1?1:0,z2=rz>=1?1:0;
            double sum=0,dx=0,dy=0,dz=0;
            void Corner(int a,int b,int c,double xx,double yy,double zz)
            {
                double t=.6-xx*xx-yy*yy-zz*zz;if(t<=0)return;
                uint hash=Rng.Mix(unchecked((uint)seed^(uint)a*0x8da6b343u^(uint)b*0xd8163841u^(uint)c*0xcb1ab31fu));
                int g=(int)(hash%12),plane=g/4;
                double first=(g&1)==0?1:-1,second=(g&2)==0?1:-1;
                double gx=plane==2?0:first,gy=plane==0?second:plane==2?first:0,gz=plane==0?0:second;
                double dot=gx*xx+gy*yy+gz*zz,t2=t*t,t3=t2*t,t4=t2*t2;
                sum+=t4*dot;dx+=t4*gx-8*t3*xx*dot;dy+=t4*gy-8*t3*yy*dot;dz+=t4*gz-8*t3*zz*dot;
            }
            Corner(i,j,k,x,y,z);
            Corner(i+x1,j+y1,k+z1,x-x1+1/6.0,y-y1+1/6.0,z-z1+1/6.0);
            Corner(i+x2,j+y2,k+z2,x-x2+1/3.0,y-y2+1/3.0,z-z2+1/3.0);
            Corner(i+1,j+1,k+1,x-.5,y-.5,z-.5);
            gradient=new V(dx*32,dy*32,dz*32);return sum*32;
        }

        public static double Mountains(V physicalMetres,int seed,double sharpness,double broad)
        {
            // The large-scale signed mass prevents a constant-height ridge
            // network. Finer, gradient-warped spurs inherit that mass, rather
            // than becoming independent sine waves or equal-height walls.
            V p=physicalMetres*.00020;
            double bendA=Noise(p*.43,seed^0x19af,out _),bendB=Noise(p*.43,seed^0x3e71,out _);
            p+=new V(broad,bendA,bendB)*.35;
            V warp=new V();double height=0,amplitude=1,frequency=1;
            double rugged=D.Clamp((sharpness-1)*.65+.2);
            for(int octave=0;octave<Octaves;octave++)
            {
                double n=Noise(p*frequency+warp,unchecked(seed+octave*7919),out V gradient);
                if(octave==0)height=.85*n;
                else
                {
                    double crest=1-Math.Sqrt(n*n+.008);
                    double signal=(crest*crest-.42)*rugged+n*.65*(1-rugged);
                    double upperSlope=.18+.82*OrogenMorphology.Smooth(-.5,.45,height);
                    height+=amplitude*signal*upperSlope;
                }
                // Gradient feedback is in fixed global coordinates. No rotating
                // cell-local frame, mutable state, erosion iterations or cache.
                warp+=gradient*(-n*amplitude*.32);
                amplitude*=.52;frequency*=2.07;
            }
            return height;
        }
    }
}
