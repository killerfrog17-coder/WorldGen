using System;
using System.Collections.Generic;

namespace WorldGen.Presentation
{
    /// <summary>Bounded exact-length decoded buffers. Transport attributes use
    /// one pool and release after native upload. Cover grids use a separate pool and explicit TerrainCoverBuffers lease.
    /// Unity releases that wire lease after copying to owned native residency.</summary>
    public sealed class TerrainUploadBufferPool : IDisposable
    {
        readonly object gate=new object();
        sealed class Bucket<T>{public readonly Stack<T[]> Available=new Stack<T[]>();public long Used;}
        readonly Dictionary<int,Bucket<float>> floats=new Dictionary<int,Bucket<float>>();
        readonly Dictionary<int,Bucket<int>> ints=new Dictionary<int,Bucket<int>>();
        readonly long budget;
        long retained,created,reused,clock;bool closed;
        public TerrainUploadBufferPool(long maximumBytes=16*1024*1024)
        {if(maximumBytes<0)throw new ArgumentOutOfRangeException(nameof(maximumBytes));budget=maximumBytes;}
        public long RetainedBytes{get{lock(gate)return retained;}}
        public long CreatedBytes{get{lock(gate)return created;}}
        public long ReusedBytes{get{lock(gate)return reused;}}
        internal float[] Floats(int length)=>Rent(floats,length);
        internal int[] Ints(int length)=>Rent(ints,length);
        T[] Rent<T>(Dictionary<int,Bucket<T>> buckets,int length)
        {
            if(length==0)return Array.Empty<T>();
            lock(gate)
            {
                if(!closed&&buckets.TryGetValue(length,out var bucket)&&bucket.Available.Count>0)
                {bucket.Used=++clock;retained-=(long)length*4;reused+=(long)length*4;return bucket.Available.Pop();}
                created+=(long)length*4;
            }
            return new T[length];
        }
        internal void Return(float[] data)=>Return(floats,data);
        internal void Return(int[] data)=>Return(ints,data);
        void Return<T>(Dictionary<int,Bucket<T>> buckets,T[] data)
        {
            if(data.Length==0)return;
            lock(gate)
            {
                long bytes=(long)data.Length*4;
                if(closed||bytes>budget)return;
                buckets.TryGetValue(data.Length,out var bucket);
                if(bucket!=null&&bucket.Available.Count>=8)return;
                // Keep the current working set, not the first 128 lengths seen
                // during loading. Previously full/empty stale buckets could
                // permanently exclude every newly encountered mesh size.
                while(bytes>budget-retained||(bucket==null&&buckets.Count>=128))
                    if(!EvictOldest(bucket))return;
                if(bucket==null){bucket=new Bucket<T>();buckets.Add(data.Length,bucket);}
                bucket.Used=++clock;bucket.Available.Push(data);retained+=bytes;
            }
        }
        bool EvictOldest(object? except)
        {
            long oldest=long.MaxValue;int key=0;bool found=false,isFloat=false;
            foreach(var entry in floats)if(!ReferenceEquals(entry.Value,except)&&entry.Value.Used<oldest)
            {oldest=entry.Value.Used;key=entry.Key;isFloat=true;found=true;}
            foreach(var entry in ints)if(!ReferenceEquals(entry.Value,except)&&entry.Value.Used<oldest)
            {oldest=entry.Value.Used;key=entry.Key;isFloat=false;found=true;}
            if(!found)return false;
            if(isFloat){retained-=(long)key*4*floats[key].Available.Count;floats.Remove(key);}
            else{retained-=(long)key*4*ints[key].Available.Count;ints.Remove(key);}
            return true;
        }
        public void Dispose(){lock(gate){closed=true;floats.Clear();ints.Clear();retained=0;}}
    }
}
