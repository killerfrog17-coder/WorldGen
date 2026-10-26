using System;
using System.Collections.Generic;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Ready forest coverage, including empty pages and retained LOD
    /// fallbacks. Requested pages and unbaked art are deliberately excluded.</summary>
    public sealed class ForestCoverageFootprint
    {
        const int MaximumLevel=24;
        readonly HashSet<ForestPageKey> full,partial=new HashSet<ForestPageKey>();
        readonly int latticeCount;
        public ForestCoverageFootprint(IEnumerable<ForestPageKey> ready,int latticeCount)
        {
            if(latticeCount<=0)throw new ArgumentOutOfRangeException(nameof(latticeCount));
            this.latticeCount=latticeCount;
            full=new HashSet<ForestPageKey>(ready);
            foreach(var key in new List<ForestPageKey>(full))
            {
                for(var parent=key.Parent;parent.Level<=MaximumLevel;parent=parent.Parent)partial.Add(parent);
                var at=key;
                while(at.Level<MaximumLevel)
                {
                    var parent=at.Parent;bool complete=true;
                    for(int y=0;y<2&&complete;y++)for(int x=0;x<2&&complete;x++)
                    {var child=Child(parent,x,y);complete=Outside(child)||full.Contains(child);}
                    if(!complete)break;
                    full.Add(parent);at=parent;
                }
            }
        }
        static ForestPageKey Child(ForestPageKey key,int x,int y)=>new ForestPageKey(key.Face,key.X*2+x,key.Y*2+y,key.Level-1);
        bool Outside(ForestPageKey key)
        {long size=(long)LocalScatter.ForestPageSide*(1L<<key.Level);return key.X*size>=latticeCount||key.Y*size>=latticeCount;}
        public bool Covers(ForestPageKey key)
        {
            for(var at=key;at.Level<=MaximumLevel;at=at.Parent)if(full.Contains(at))return true;
            return Outside(key);
        }
        // The caller supplies a conservative distance to a page footprint in
        // its current camera chart. Only partially covered nodes need descent.
        public double NearestGap(IEnumerable<ForestPageKey> requested,Func<ForestPageKey,double> distance,double maximumDistance)
        {
            double nearest=maximumDistance;
            void Visit(ForestPageKey key)
            {
                if(Outside(key)||full.Contains(key))return;
                double d=distance(key);if(d>=nearest)return;
                if(key.Level==0||!partial.Contains(key)){nearest=d;return;}
                for(int y=0;y<2;y++)for(int x=0;x<2;x++)Visit(Child(key,x,y));
            }
            foreach(var key in requested)if(!Covers(key))Visit(key);
            return nearest;
        }
    }
}
