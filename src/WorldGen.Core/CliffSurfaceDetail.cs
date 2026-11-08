using System;

namespace WorldGen.Core
{
    /// <summary>Metre-scale fracture relief in exposed cliff substrate.
    /// One continuous planet-addressed field, shared by ground, roots and
    /// movement queries; no tile seed, extra object, shader pass or world tick.</summary>
    public static class CliffSurfaceDetail
    {
        static uint Hash(int x,int y,int z,int seed)
            =>Rng.Mix(unchecked((uint)x*0x8da6b343u^(uint)y*0xd8163841u^(uint)z*0xcb1ab31fu^(uint)seed));
        static double Unit(uint hash)=>(hash&0xffffff)/16777215d;
        /// <summary>Continuous connected, tilted rock faces, in physical metres.
        /// Used by the submitted cliff mesh rather than cutting the height field.</summary>
        public static double Outward(V metres,int seed,double hardness)
            =>Outward(metres,seed,hardness,out _);
        public static double Outward(V metres,int seed,double hardness,out V gradient)
        {
            double spacing=18+12*D.Clamp(hardness);V p=metres/spacing;
            int ix=(int)Math.Floor(p.X),iy=(int)Math.Floor(p.Y),iz=(int)Math.Floor(p.Z);
            double first=double.MaxValue,second=double.MaxValue;uint winner=0;V local=default,secondLocal=default;
            for(int z=-1;z<=1;z++)for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
            {
                uint h=Hash(ix+x,iy+y,iz+z,seed);
                V q=new V(p.X-(ix+x+.35+.3*Unit(h)),p.Y-(iy+y+.35+.3*Unit(Rng.Mix(h^0x5831ab7u))),p.Z-(iz+z+.35+.3*Unit(Rng.Mix(h^0x39af83bu))));
                double d=q.X*q.X+q.Y*q.Y+q.Z*q.Z;
                if(d<first){second=first;secondLocal=local;first=d;winner=h;local=q;}else if(d<second){second=d;secondLocal=q;}
            }
            double r1=Math.Max(1e-9,Math.Sqrt(first)),r2=Math.Max(1e-9,Math.Sqrt(second));
            double t=D.Clamp((r2-r1-.015)/.115),joint=t*t*(3-2*t),jointPrime=6*t*(1-t)/.115;
            var slope=new V(Unit(Rng.Mix(winner^0x1593bu))-.5,Unit(Rng.Mix(winner^0x375abu))-.5,Unit(Rng.Mix(winner^0x712b5u))-.5);
            double rawFace=.58+.55*(local.X*slope.X+local.Y*slope.Y+local.Z*slope.Z),face=D.Clamp(rawFace);
            gradient=((secondLocal/r2-local/r1)*(jointPrime*face)+slope*(rawFace>0&&rawFace<1?.55*joint:0))*(8/spacing);
            return 8*joint*face;
        }
        public static double Cut(V metres,int seed,double exposure,double hardness)
        {
            double mask=OrogenMorphology.Smooth(.12,.65,exposure);if(mask<=0)return 0;
            hardness=D.Clamp(hardness);double spacing=12+8*hardness;
            V p=metres/spacing;int ix=(int)Math.Floor(p.X),iy=(int)Math.Floor(p.Y),iz=(int)Math.Floor(p.Z);
            double first=double.MaxValue,second=double.MaxValue;
            for(int z=-1;z<=1;z++)for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
            {
                uint h=Hash(ix+x,iy+y,iz+z,seed);
                // Bounded jitter keeps nearest competitors within the finite
                // neighbourhood when a query crosses a lattice-cell boundary.
                double dx=ix+x+.25+.5*Unit(h)-p.X,dy=iy+y+.25+.5*Unit(Rng.Mix(h^0x5831ab7u))-p.Y,dz=iz+z+.25+.5*Unit(Rng.Mix(h^0x39af83bu))-p.Z;
                double distance=dx*dx+dy*dy+dz*dz;
                if(distance<first){second=first;first=distance;}else if(distance<second)second=distance;
            }
            // Unequal cellular blocks with broad incised joints. Sampling in
            //3D prevents a repeating row/column pattern along the wall. Joint
            // width remains resolvable by the existing close terrain grid.
            double joint=1-OrogenMorphology.Smooth(.055,.28,Math.Sqrt(second)-Math.Sqrt(first));
            double grain=.5+.5*SphereNoise.Sample(metres,seed^0x4319b,1/9d);
            return -mask*((2+3*hardness)*(.2+.8*joint)+.35*grain);
        }
    }
}
