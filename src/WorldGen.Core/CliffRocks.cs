using System;

namespace WorldGen.Core
{
    /// <summary>Opaque jointed bedrock fitted to exposed steep terrain. These
    /// are solid three-dimensional outcrops, not a second terrain elevation.</summary>
    public static class CliffRocks
    {
        const int Marker=1<<20;
        public static bool Is(int variant)=>(variant&Marker)!=0;
        public static RockFamily Family(int variant)=>(RockFamily)((variant>>3)&7);
        public static int Encode(RockFamily family,int shape)=>Marker|((int)family<<3)|(shape&7);
        public static int Rows(int variant)=>Family(variant)==RockFamily.Granite||Family(variant)==RockFamily.Basalt?3:5;
        public const int Columns=3;
        public static bool Potential(LocalTerrainSample sample)
            =>!sample.Underwater&&sample.ElevationM>2&&sample.Biome!=Biome.Ice
                &&(sample.Province.ExposedRock>.15||sample.Ground.DepthM<.65&&sample.MountainHeightM>30);
        public static bool TryCreate(ulong id,V point,LocalTerrainSample sample,LocalTerrain.LocalTerrainPatch patch,double radius,double spacing,int seed,out ScatterInstance result)
        {
            result=default;if(!Potential(sample))return false;
            V east=V.Cross(Math.Abs(point.Z)<.99?new V(0,0,1):new V(0,1,0),point).Unit,north=V.Cross(point,east).Unit;
            double probe=D.Clamp(spacing*.3,4,12),e=probe/radius;
            double sx=(patch.Elevation((point+east*e).Unit)-patch.Elevation((point-east*e).Unit))/(2*probe);
            double sz=(patch.Elevation((point+north*e).Unit)-patch.Elevation((point-north*e).Unit))/(2*probe);
            double slope=Math.Sqrt(sx*sx+sz*sz);if(slope<1.3||GroundEnvironment.SandMantle(sample.Ground,1/Math.Sqrt(1+slope*slope))>.25)return false;
            V outward=(east*(-sx)+north*(-sz)).Unit;
            double half=spacing*.7/radius,top=patch.Elevation((point-outward*half).Unit),bottom=patch.Elevation((point+outward*half).Unit);
            if(top-bottom<8)return false;
            uint hash=Rng.Mix((uint)id^(uint)(id>>32)^unchecked((uint)seed));double variation=(hash&65535)/65535d;
            result=new ScatterInstance{Id=id,Kind=ScatterKind.Outcrop,Variant=Encode(sample.Ground.Bedrock,(int)(hash>>16)&7),Position=point,
                // For a cliff instance Normal is the outward wall direction;
                // its vertical axis remains the planet's radial up direction.
                Normal=outward,GroundM=sample.ElevationM,HeightM=D.Q(D.Clamp((top-bottom)*1.18+4,12,280),1000),
                WidthM=D.Q(spacing*(1.65+variation*.35),1000),YawDegrees=0,Color=sample.Ground.RockColor};
            return true;
        }
        static double Noise(int variant,int row,int column,int word)
            =>(Rng.Mix(unchecked((uint)(variant*173+row*7919+column*313+word*1013)))&65535)/65535d;
        public static V Surface(int variant,int row,int column,int face,double u,double v)
        {
            V c=face==0?new V(1,u,v):face==1?new V(-1,u,-v):face==2?new V(u,1,-v):face==3?new V(u,-1,v):face==4?new V(u,v,1):new V(-u,v,-1);
            bool massive=Rows(variant)==3;double bevel=massive?.23:.13;
            V inner=new V(D.Clamp(c.X,-1+bevel,1-bevel),D.Clamp(c.Y,-1+bevel,1-bevel),D.Clamp(c.Z,-1+bevel,1-bevel));
            V q=inner+(c-inner).Unit*bevel;
            q*=1+.022*Math.Sin(q.X*4.3+q.Y*3.1+q.Z*5.7+Noise(variant,row,column,1)*31);
            int rows=Rows(variant);double band=1d/rows;
            double width=.183+.035*Noise(variant,row,column,2),height=band*(.54+.06*Noise(variant,row,column,3));
            double cx=(column-1)*.31+(row%2==0?-.035:.035)+.035*(Noise(variant,row,column,4)-.5);
            double cy=(row+.5)*band+.025*(Noise(variant,row,column,5)-.5);
            // Unequal ledge projections leave actual downward-facing roofs and
            // recesses. The back of every block penetrates the same rock mass.
            double cz=-.08+.075*Math.Sin(row*2.1+(variant&7))+.035*(Noise(variant,row,column,6)-.5);
            double depth=.20+.045*Noise(variant,row,column,7)+(row==rows-1?.045:0);
            return new V(cx+q.X*width,cy+q.Y*height+q.X*band*.025,cz+q.Z*depth+q.Y*.018);
        }
    }
}
