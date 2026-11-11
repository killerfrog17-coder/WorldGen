using System;
using System.Collections.Generic;

namespace WorldGen.Presentation
{
    /// <summary>Keep the last terrain-aware layout while its asynchronous
    /// successor is calculated. Only the protected nearby floor is refined
    /// synchronously; movement cannot replace known relief with a flat guess.</summary>
    public static class TerrainStreamingPlan
    {
        public static Dictionary<TerrainTileKey,int> Coverage(double x,double z,double scale,double range,double horizonLead=0)
        {
            var result=new Dictionary<TerrainTileKey,int>();
            void Add(int level,double radius,int segments)
            {
                double size=TerrainLod.BaseSizeM*scale*(1<<level);
                for(int j=(int)Math.Floor((z-radius)/size);j<=(int)Math.Floor((z+radius)/size);j++)
                    for(int i=(int)Math.Floor((x-radius)/size);i<=(int)Math.Floor((x+radius)/size);i++)
                    {var key=new TerrainTileKey(i,j,level,scale);if(TerrainLod.DistanceSquared(key,x,z)<=radius*radius)result[key]=segments;}
            }
            int backdrop=TerrainAdaptiveLod.BackdropLevel(scale,range);
            int oldHorizon=Math.Min(backdrop,Math.Max(0,(int)Math.Floor(Math.Log(16384/(TerrainLod.BaseSizeM*scale),2))));
            // Group up to 4x4 old horizon products without changing their
            // globally aligned height lattice or sampling footprint. This
            // bounds renderer/material/coverage objects at enormous ranges,
            // rather than trading the mountain silhouette for a coarser grid.
            int horizon=Math.Min(oldHorizon+2,Math.Min(backdrop,Math.Max(0,(int)Math.Floor(Math.Log(65536/(TerrainLod.BaseSizeM*scale),2)))));
            int horizonSegments=Math.Min(128,32<<(horizon-oldHorizon));
            Add(horizon,range+Math.Max(0,horizonLead),horizonSegments);
            // Bound the fine fallback disk independently of an enormous view
            // distance. At most a few hundred work units, including on small
            // planets, rather than a uniform 2km grid over a 250km horizon.
            int near=Math.Max(0,(int)Math.Floor(Math.Log(2048/(TerrainLod.BaseSizeM*scale),2)));
            Add(near,Math.Min(range,8192),32);return result;
        }
        public static Dictionary<TerrainTileKey,int> CompleteCoverage(IReadOnlyDictionary<TerrainTileKey,int> held,IReadOnlyDictionary<TerrainTileKey,int> fallback)
        {
            var result=new Dictionary<TerrainTileKey,int>(held.Count+32);
            var ancestors=new HashSet<TerrainTileKey>();
            int fallbackLevel=-1;
            foreach(var pair in fallback)fallbackLevel=Math.Max(fallbackLevel,pair.Key.Level);
            fallbackLevel=Math.Min(23,fallbackLevel);
            int maximumLevel=fallbackLevel;
            foreach(var pair in held)
            {
                result.Add(pair.Key,pair.Value);
                maximumLevel=Math.Max(maximumLevel,Math.Min(23,pair.Key.Level));
                // Visit only descends from fallback keys. Higher ancestors
                // can never be queried, even when held leaves are much finer.
                for(int depth=1;depth<=fallbackLevel-pair.Key.Level;depth++)
                    ancestors.Add(new TerrainTileKey(pair.Key.X>>depth,pair.Key.Z>>depth,pair.Key.Level+depth,pair.Key.Scale));
            }
            void Visit(TerrainTileKey key,int segments)
            {
                // Held and inserted fallback leaves cannot have an ancestor
                // above their actual maximum level in this result.
                for(int depth=0;depth<=maximumLevel-key.Level;depth++)
                    if(result.ContainsKey(new TerrainTileKey(key.X>>depth,key.Z>>depth,key.Level+depth,key.Scale)))return;
                if(key.Level>0&&ancestors.Contains(key))
                {
                    for(int j=0;j<2;j++)for(int i=0;i<2;i++)
                        Visit(new TerrainTileKey(key.X*2+i,key.Z*2+j,key.Level-1,key.Scale),segments);
                }
                else result.Add(key,segments);
            }
            foreach(var pair in fallback)Visit(pair.Key,pair.Value);
            return result;
        }
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
