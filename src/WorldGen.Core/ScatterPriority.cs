using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    /// <summary>Reusable exact distance ordering for a local scatter request set.</summary>
    public sealed class ScatterPriority
    {
        struct Entry { public ScatterKey Key; public double Distance; }
        readonly List<Entry> entries=new List<Entry>();
        static readonly Comparison<Entry> Compare=(a,b)=>
        {int d=a.Distance.CompareTo(b.Distance);return d!=0?d:a.Key.CompareTo(b.Key);};
        public void Sort(LocalScatter source,V position,List<ScatterKey> keys)
        {
            entries.Clear();
            foreach(var key in keys)
            {
                V p=source.Position(key)-position;
                entries.Add(new Entry{Key=key,Distance=p.X*p.X+p.Y*p.Y+p.Z*p.Z});
            }
            entries.Sort(Compare);
            for(int i=0;i<entries.Count;i++)keys[i]=entries[i].Key;
        }
    }
}
