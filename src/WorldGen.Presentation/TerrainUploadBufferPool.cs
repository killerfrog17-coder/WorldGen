using System;
using System.Collections.Generic;

namespace WorldGen.Presentation
{
    /// <summary>Bounded exact-length decoded buffers. Transport attributes use
    /// one pool and release after native upload. Persistent cover grids use a
    /// separate pool and an explicit TerrainCoverBuffers lease until retirement.</summary>
    public sealed class TerrainUploadBufferPool : IDisposable
    {
        readonly object gate=new object();
        readonly Dictionary<int,Stack<float[]>> floats=new Dictionary<int,Stack<float[]>>();
        readonly Dictionary<int,Stack<int[]>> ints=new Dictionary<int,Stack<int[]>>();
        readonly long budget;
        long retained,created,reused;bool closed;
        public TerrainUploadBufferPool(long maximumBytes=16*1024*1024)
        {if(maximumBytes<0)throw new ArgumentOutOfRangeException(nameof(maximumBytes));budget=maximumBytes;}
        public long RetainedBytes{get{lock(gate)return retained;}}
        public long CreatedBytes{get{lock(gate)return created;}}
        public long ReusedBytes{get{lock(gate)return reused;}}
        internal float[] Floats(int length)=>Rent(floats,length);
        internal int[] Ints(int length)=>Rent(ints,length);
        T[] Rent<T>(Dictionary<int,Stack<T[]>> buckets,int length)
        {
            if(length==0)return Array.Empty<T>();
            lock(gate)
            {
                if(!closed&&buckets.TryGetValue(length,out var available)&&available.Count>0)
                {retained-=(long)length*4;reused+=(long)length*4;return available.Pop();}
                created+=(long)length*4;
            }
            return new T[length];
        }
        internal void Return(float[] data)=>Return(floats,data);
        internal void Return(int[] data)=>Return(ints,data);
        void Return<T>(Dictionary<int,Stack<T[]>> buckets,T[] data)
        {
            if(data.Length==0)return;
            lock(gate)
            {
                long bytes=(long)data.Length*4;
                if(closed||bytes>budget-retained)return;
                if(!buckets.TryGetValue(data.Length,out var available))
                {if(buckets.Count>=128)return;buckets.Add(data.Length,available=new Stack<T[]>());}
                if(available.Count>=8)return;
                available.Push(data);retained+=bytes;
            }
        }
        public void Dispose(){lock(gate){closed=true;floats.Clear();ints.Clear();retained=0;}}
    }
}
