using System;
using System.Threading;

namespace WorldGen.Core
{
    // A planet-sized index is mostly empty during local play. Keep reference
    // slots in small, lazily allocated pages so the GC need not scan a single
    // million-slot mutable array. Writers must hold their existing owner lock;
    // readers can use the same lock-free lookup as the previous flat array.
    public sealed class SparseReferenceArray<T> where T:class
    {
        const int Shift=8,Size=1<<Shift,Mask=Size-1;
        readonly T[][] pages;
        readonly T[] flat;
        public int Length {get;}
        public SparseReferenceArray(int length,bool paged=true)
        {
            if(length<0)throw new ArgumentOutOfRangeException(nameof(length));
            Length=length;
            if(paged)pages=new T[(int)(((long)length+Mask)>>Shift)][];
            else flat=new T[length];
        }
        public T this[int index]
        {
            get
            {
                if((uint)index>=(uint)Length)throw new IndexOutOfRangeException();
                if(flat!=null)return Volatile.Read(ref flat[index]);
                var page=Volatile.Read(ref pages[index>>Shift]);
                return page==null?null:Volatile.Read(ref page[index&Mask]);
            }
            set
            {
                if((uint)index>=(uint)Length)throw new IndexOutOfRangeException();
                if(flat!=null){Volatile.Write(ref flat[index],value);return;}
                int slot=index>>Shift;var page=pages[slot];
                if(page==null)
                {
                    if(value==null)return;
                    page=new T[Math.Min(Size,Length-(slot<<Shift))];
                    page[index&Mask]=value;Volatile.Write(ref pages[slot],page);
                }
                else Volatile.Write(ref page[index&Mask],value);
            }
        }
        public int AllocatedSlots
        {
            get{if(flat!=null)return flat.Length;int count=pages.Length;foreach(var page in pages)if(page!=null)count+=page.Length;return count;}
        }
    }
}
