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
