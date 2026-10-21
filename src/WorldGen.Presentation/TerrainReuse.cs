using System;
using System.Collections.Generic;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Single-owner byte/count-bounded LRU. Eviction owns disposal;</n+    /// Take transfers ownership without disposing the returned value.</summary>
    public sealed class TerrainLru<TKey,TValue> where TKey:notnull
    {
        sealed class Entry{public TKey Key=default!;public TValue Value=default!;public long Bytes;}
        readonly Dictionary<TKey,LinkedListNode<Entry>> entries=new Dictionary<TKey,LinkedListNode<Entry>>();
        readonly LinkedList<Entry> order=new LinkedList<Entry>();readonly Action<TValue> discard;
        readonly long limit;readonly int countLimit;
        public long Bytes {get;private set;}public int Count=>entries.Count;
        public TerrainLru(long bytes,int count,Action<TValue> discard){limit=bytes;countLimit=count;this.discard=discard;}
        public bool Take(TKey key,out TValue value)
        {
            if(!entries.TryGetValue(key,out var node)){value=default!;return false;}
            value=node.Value.Value;entries.Remove(key);order.Remove(node);Bytes-=node.Value.Bytes;return true;
        }
        public bool Contains(TKey key)=>entries.ContainsKey(key);
        public void Put(TKey key,TValue value,long bytes)
        {
            if(Take(key,out var previous))discard(previous);
            if(bytes>limit||bytes<0||countLimit<=0){discard(value);return;}
            var node=order.AddLast(new Entry{Key=key,Value=value,Bytes=bytes});entries.Add(key,node);Bytes+=bytes;
            while(Bytes>limit||Count>countLimit){var first=order.First!;entries.Remove(first.Value.Key);order.RemoveFirst();Bytes-=first.Value.Bytes;discard(first.Value.Value);}
        }
        public void Clear(){foreach(var item in order)discard(item.Value);order.Clear();entries.Clear();Bytes=0;}
    }
    /// <summary>Exact point samples, not approximate LOD-dependent heights.
    /// Owned by one world/scale worker and shared between its two build lanes.</summary>
    public sealed class TerrainSampleCache
    {
        readonly object gate=new object();readonly int capacity;
        readonly Dictionary<(double,double,double),LocalTerrainSample> samples=new Dictionary<(double,double,double),LocalTerrainSample>();
        readonly Queue<(double,double,double)> fifo=new Queue<(double,double,double)>();
        public long Hits {get;private set;}public long Misses {get;private set;}
        public int Count{get{lock(gate)return samples.Count;}}
        public TerrainSampleCache(int capacity=32768){this.capacity=Math.Max(1,capacity);}
        public bool TryGet(V p,out LocalTerrainSample sample)
        {lock(gate){bool found=samples.TryGetValue((p.X,p.Y,p.Z),out sample);if(found)Hits++;else Misses++;return found;}}
        public void Store(V p,LocalTerrainSample sample)
        {
            lock(gate)
            {
                var key=(p.X,p.Y,p.Z);if(samples.ContainsKey(key))return;
                while(samples.Count>=capacity)samples.Remove(fifo.Dequeue());
                samples.Add(key,sample);fifo.Enqueue(key);
            }
        }
    }
    public readonly struct TerrainProductKey:IEquatable<TerrainProductKey>
    {
        public readonly TerrainTileKey Tile;public readonly int Segments;readonly double a,b,c,d;
        public TerrainProductKey(TerrainTileKey tile,int segments,double[] edges){Tile=tile;Segments=segments;a=edges[0];b=edges[1];c=edges[2];d=edges[3];}
        public bool Equals(TerrainProductKey k)=>Tile.Equals(k.Tile)&&Segments==k.Segments&&a==k.a&&b==k.b&&c==k.c&&d==k.d;
        public override bool Equals(object o)=>o is TerrainProductKey k&&Equals(k);
        public override int GetHashCode()=>unchecked(Tile.GetHashCode()*31+Segments^a.GetHashCode()^b.GetHashCode()^c.GetHashCode()^d.GetHashCode());
    }
}
