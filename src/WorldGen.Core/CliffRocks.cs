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
        public static int Model(int variant)=>((variant&7)+(Family(variant)==RockFamily.Granite||Family(variant)==RockFamily.Metamorphic?2:0))%3;
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
            double width=spacing*(1.65+variation*.35);int family=0;
            for(int i=1;i<6;i++)if(sample.Ground.RockWeight(i)>sample.Ground.RockWeight(family))family=i;
            result=new ScatterInstance{Id=id,Kind=ScatterKind.Outcrop,Variant=Encode((RockFamily)family,(int)(hash>>16)&7),Position=point,
                Normal=(outward+point*(1/slope)).Unit,GroundM=sample.ElevationM,
                HeightM=D.Q(D.Clamp(((top-bottom)*1.18+4)*Math.Sqrt(1+1/(slope*slope)),12,Math.Min(280,width*2.2)),1000),
                WidthM=D.Q(width,1000),YawDegrees=0,Color=sample.Ground.RockColor};
            return true;
        }
    }
}
