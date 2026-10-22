using System;
using System.Collections.Generic;

namespace WorldGen.Presentation
{
    /// <summary>Union of visible dyadic terrain footprints, independent of the
    /// requested mesh quality. Prepared jobs and disabled cache products do not
    /// belong here. Parent and child products may coexist during a crossfade.</summary>
    public sealed class TerrainCoverageFootprint
    {
        const int MaximumLevel=23;
        readonly HashSet<TerrainTileKey> source,full,partial=new HashSet<TerrainTileKey>();
        int highest;
        public TerrainCoverageFootprint(IEnumerable<TerrainTileKey> visible)
        {
            source=new HashSet<TerrainTileKey>(visible);full=new HashSet<TerrainTileKey>(source);
            foreach(var key in source)
            {
                highest=Math.Max(highest,key.Level);
                for(var parent=Parent(key);parent.Level<=MaximumLevel;parent=Parent(parent))partial.Add(parent);
                // Four complete children are indistinguishable from a loaded
                // parent for fog, regardless of their segment counts/edge jobs.
                var at=key;
                while(at.Level<MaximumLevel)
                {
                    var parent=Parent(at);bool complete=true;
                    for(int z=0;z<2&&complete;z++)for(int x=0;x<2&&complete;x++)
                        complete=full.Contains(Child(parent,x,z));
                    if(!complete)break;
                    full.Add(parent);highest=Math.Max(highest,parent.Level);at=parent;
                }
            }
        }
        static TerrainTileKey Parent(TerrainTileKey key)=>new TerrainTileKey(key.X>>1,key.Z>>1,key.Level+1,key.Scale);
        static TerrainTileKey Child(TerrainTileKey key,int x,int z)=>new TerrainTileKey(key.X*2+x,key.Z*2+z,key.Level-1,key.Scale);
        public bool Matches(IEnumerable<TerrainTileKey> visible)=>source.SetEquals(visible);
        public double NearestGap(IEnumerable<TerrainTileKey> requested,double x,double z,double maximumDistance)
        {
            double nearest=maximumDistance*maximumDistance;
            void Visit(TerrainTileKey key)
            {
                double distance=TerrainLod.DistanceSquared(key,x,z);
                if(distance>=nearest||full.Contains(key))return;
                if(key.Level<=0||!partial.Contains(key)){nearest=distance;return;}
                // Split only partial nodes. A missing quadrant is measured at
                // ITS bounds, not at the bounds of its already-covered parent.
                for(int j=0;j<2;j++)for(int i=0;i<2;i++)Visit(Child(key,i,j));
            }
            foreach(var key in requested)
            {
                bool covered=false;
                for(var ancestor=key;ancestor.Level<=highest;ancestor=Parent(ancestor))
                    if(full.Contains(ancestor)){covered=true;break;}
                if(!covered)Visit(key);
            }
            return Math.Sqrt(nearest);
        }
    }
}
