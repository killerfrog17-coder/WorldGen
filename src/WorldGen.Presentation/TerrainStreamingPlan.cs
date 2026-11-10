using System;
using System.Collections.Generic;

namespace WorldGen.Presentation
{
    /// <summary>Keep the last terrain-aware layout while its asynchronous
    /// successor is calculated. Only the protected nearby floor is refined
    /// synchronously; movement cannot replace known relief with a flat guess.</summary>
    public static class TerrainStreamingPlan
    {
        public static Dictionary<TerrainTileKey,int> ProtectNear(IReadOnlyDictionary<TerrainTileKey,int> previous,
            double x,double z,double scale,double radius,int finestSegments=64,double range=double.PositiveInfinity)
        {
            var result=new Dictionary<TerrainTileKey,int>(previous.Count+32);
            double squared=radius*radius;int finest=Math.Max(8,Math.Min(64,finestSegments));
            void Visit(TerrainTileKey key,int segments)
            {
                bool near=radius>0&&TerrainLod.DistanceSquared(key,x,z)<=squared;
                if(near&&key.Level>0)
                {
                    for(int j=0;j<2;j++)for(int i=0;i<2;i++)
                        Visit(new TerrainTileKey(key.X*2+i,key.Z*2+j,key.Level-1,scale),segments);
                }
                else result[key]=near?Math.Max(finest,segments):segments;
            }
            foreach(var pair in previous)if(TerrainLod.DistanceSquared(pair.Key,x,z)<=range*range)Visit(pair.Key,pair.Value);
            double size=TerrainLod.BaseSizeM*scale;
            for(int j=(int)Math.Floor((z-radius)/size);radius>0&&j<=(int)Math.Floor((z+radius)/size);j++)
                for(int i=(int)Math.Floor((x-radius)/size);i<=(int)Math.Floor((x+radius)/size);i++)
                {
                    var key=new TerrainTileKey(i,j,0,scale);
                    if(TerrainLod.DistanceSquared(key,x,z)<=squared)result[key]=Math.Max(finest,result.TryGetValue(key,out int old)?old:0);
                }
            return result;
        }
    }
}
