using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    // Immutable broad phase only: callers retain the exact trunk/rock and
    // oriented shelter tests, including their live collected-object ledger.
    public sealed class NpcCampSiteIndex
    {
        const double CellM=32;
        readonly Dictionary<long,int> heads=new Dictionary<long,int>();
        readonly int[] next;
        static int Cell(double value)=>(int)Math.Floor(value/CellM);
        static long Key(int x,int z)=>((long)x<<32)|(uint)z;
        public NpcCampSiteIndex(double[] x,double[] z)
        {
            if(x.Length!=z.Length)throw new ArgumentException("Camp obstacle coordinate counts differ.");
            next=new int[x.Length];
            for(int i=0;i<x.Length;i++)
            {
                long key=Key(Cell(x[i]),Cell(z[i]));next[i]=heads.TryGetValue(key,out int previous)?previous:-1;heads[key]=i;
            }
        }
        public Query Around(double x,double z,double radius)=>new Query(this,Cell(x-radius),Cell(z-radius),Cell(x+radius),Cell(z+radius));
        public struct Query
        {
            readonly NpcCampSiteIndex index;readonly int minX,maxX,maxZ;
            int x,z,node;bool started;
            internal Query(NpcCampSiteIndex index,int minX,int minZ,int maxX,int maxZ)
            {this.index=index;this.minX=minX;this.maxX=maxX;this.maxZ=maxZ;x=minX;z=minZ;node=-1;started=false;Current=-1;}
            public int Current{get;private set;}
            public bool MoveNext()
            {
                if(node>=0){Current=node;node=index.next[node];return true;}
                while(z<=maxZ)
                {
                    if(started){if(++x>maxX){x=minX;z++;}if(z>maxZ)break;}else started=true;
                    if(index.heads.TryGetValue(Key(x,z),out int head)){Current=head;node=index.next[head];return true;}
                }
                return false;
            }
        }
    }
}
