using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
namespace WorldGen.Core
{
    // Immutable published rows; an actor replacement copies one fixed-size page.
    // Reads detach, so callers cannot edit the saved biography through a reference.
    [CollectionDataContract(ItemName="HistoricalNotablePerson")]
    public sealed class HistoricalPersonCollection:ICollection<HistoricalNotablePerson>,IReadOnlyList<HistoricalNotablePerson>
    {
        const int Size=128;
        List<HistoricalNotablePerson[]> pages=new List<HistoricalNotablePerson[]>();
        int count;bool writableLast;
        public int Count=>count;
        public bool IsReadOnly=>false;
        internal HistoricalNotablePerson Peek(int id){if((uint)id>=(uint)count)throw new ArgumentOutOfRangeException(nameof(id));return pages[id/Size][id%Size];}
        public HistoricalNotablePerson this[int id]=>Peek(id).Copy();
        public void Add(HistoricalNotablePerson p)
        {
            if(count%Size==0){pages.Add(new HistoricalNotablePerson[Size]);writableLast=true;}
            else if(!writableLast){pages[pages.Count-1]=(HistoricalNotablePerson[])pages[pages.Count-1].Clone();writableLast=true;}
            pages[count/Size][count%Size]=p.Copy();count++;
        }
        internal void Replace(int id,HistoricalNotablePerson p)
        {
            Peek(id);int page=id/Size;
            pages[page]=(HistoricalNotablePerson[])pages[page].Clone();pages[page][id%Size]=p.Copy();if(page==pages.Count-1)writableLast=true;
        }
        public HistoricalPersonCollection Copy(){writableLast=false;return new HistoricalPersonCollection{pages=new List<HistoricalNotablePerson[]>(pages),count=count};}
        public void Clear(){pages=new List<HistoricalNotablePerson[]>();count=0;writableLast=false;}
        public bool Remove(HistoricalNotablePerson p)=>throw new NotSupportedException("Historical identities cannot be shifted.");
        public bool Contains(HistoricalNotablePerson p)=>p!=null&&p.Id>=0&&p.Id<count;
        public void CopyTo(HistoricalNotablePerson[] a,int offset){for(int i=0;i<count;i++)a[offset+i]=this[i];}
        public IEnumerator<HistoricalNotablePerson> GetEnumerator(){for(int i=0;i<count;i++)yield return this[i];}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
}
