using System;
using System.Collections.Generic;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Readiness only. Does not change terrain samples, LOD planning or rendering.</summary>
    public static class TerrainHandoff
    {
        public static void Coordinates(LocalTerrainFrame frame,V point,out double x,out double z)
        {
            double dot=V.Dot(point,frame.Origin);
            if(dot<=0)throw new ArgumentOutOfRangeException(nameof(point),"Point is outside the local chart hemisphere.");
            x=V.Dot(point,frame.East)*frame.RadiusM/dot;
            z=V.Dot(point,frame.North)*frame.RadiusM/dot;
        }
        public static double Limit(LocalTerrainFrame frame)=>Math.Min(65536,frame.RadiusM*.01);
        static TerrainTileKey Parent(TerrainTileKey key)=>new TerrainTileKey(
            (int)Math.Floor(key.X/2.0),(int)Math.Floor(key.Z/2.0),key.Level+1,key.Scale);
        public static bool Ready(IReadOnlyDictionary<TerrainTileKey,int> wanted,
            IReadOnlyDictionary<TerrainTileKey,int> resident,double x,double z,double nearRadius)
        {
            if(wanted.Count==0)return false;
            var ancestors=new HashSet<TerrainTileKey>();
            foreach(var key in resident.Keys)
                for(var p=Parent(key);p.Level<=20;p=Parent(p))ancestors.Add(p);
            var memo=new Dictionary<TerrainTileKey,bool>();
            bool Overlaps(TerrainTileKey a,TerrainTileKey b)=>
                Math.Max(a.X*a.SizeM,b.X*b.SizeM)<Math.Min((a.X+1)*a.SizeM,(b.X+1)*b.SizeM)&&
                Math.Max(a.Z*a.SizeM,b.Z*b.SizeM)<Math.Min((a.Z+1)*a.SizeM,(b.Z+1)*b.SizeM);
            bool Covered(TerrainTileKey key)
            {
                if(resident.ContainsKey(key))return true;
                if(key.Level==0||!ancestors.Contains(key))return false;
                if(memo.TryGetValue(key,out bool found))return found;
                bool all=true;
                for(int j=0;j<2&&all;j++)for(int i=0;i<2&&all;i++)
                    all=Covered(new TerrainTileKey(key.X*2+i,key.Z*2+j,key.Level-1,key.Scale));
                memo[key]=all;return all;
            }
            foreach(var pair in wanted)
            {
                var key=pair.Key;
                double dx=Math.Max(Math.Max(key.X*key.SizeM-x,0),x-(key.X+1)*key.SizeM);
                double dz=Math.Max(Math.Max(key.Z*key.SizeM-z,0),z-(key.Z+1)*key.SizeM);
                // No coarse fallback beneath/around the player at handoff.
                if(dx*dx+dz*dz<=nearRadius*nearRadius&&
                    (!resident.TryGetValue(key,out int segments)||segments<pair.Value))return false;
                bool covered=Covered(key);
                for(var p=Parent(key);!covered&&p.Level<=20;p=Parent(p))covered=resident.ContainsKey(p);
                if(!covered)return false;
                if(dx*dx+dz*dz<=nearRadius*nearRadius)
                    foreach(var old in resident.Keys)
                    {
                        if(wanted.ContainsKey(old)||!Overlaps(old,key))continue;
                        // A stale coarse ancestor can draw above its new fine
                        // children. Require the exact replacement set that the
                        // ordinary renderer needs before retiring that ancestor.
                        foreach(var replacement in wanted)
                            if(Overlaps(old,replacement.Key)&&
                                (!resident.TryGetValue(replacement.Key,out int detail)||detail!=replacement.Value))return false;
                    }
            }
            return true;
        }
    }
}
