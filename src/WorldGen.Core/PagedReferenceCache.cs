using System;
using System.Threading;

namespace WorldGen.Core
{
    /// <summary>
    /// Fixed logical address space with reference storage only for touched pages.
    /// Unvisited planetary topology should not add a million empty reference
    /// slots to every collector scan. Values retain ordinary array semantics;
    /// this class does not change their strong/weak lifetime or eviction policy.
    /// </summary>
    public sealed class PagedReferenceCache<T> where T : class
    {
        const int Shift=6,Size=1<<Shift,Mask=Size-1;
        readonly T[][] pages;
        int allocatedSlots;
        public int Length { get; }
        public int AllocatedReferenceSlots=>pages.Length+Volatile.Read(ref allocatedSlots);
        public PagedReferenceCache(int length)
        {
            if(length<0)throw new ArgumentOutOfRangeException(nameof(length));
            Length=length;pages=new T[(int)(((long)length+Mask)>>Shift)][];
        }
        public T this[int index]
        {
            get
            {
                if((uint)index>=(uint)Length)throw new IndexOutOfRangeException();
                var page=Volatile.Read(ref pages[index>>Shift]);
                return page==null?null:Volatile.Read(ref page[index&Mask]);
            }
            set
            {
                if((uint)index>=(uint)Length)throw new IndexOutOfRangeException();
                int block=index>>Shift;var page=Volatile.Read(ref pages[block]);
                if(page==null)
                {
                    if(value==null)return;
                    var created=new T[Math.Min(Size,Length-(block<<Shift))];
                    page=Interlocked.CompareExchange(ref pages[block],created,null);
                    if(page==null){page=created;Interlocked.Add(ref allocatedSlots,created.Length);}
                }
                Volatile.Write(ref page[index&Mask],value);
            }
        }
    }
}
