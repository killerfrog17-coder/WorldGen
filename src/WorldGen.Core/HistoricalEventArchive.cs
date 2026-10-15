using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
namespace WorldGen.Core
{
    // The wire contract is the historical List<HistoricalEvent> contract.
    // Frozen pages are shared between checkpoints. Mutable public records are
    // copied on first access, then retained locally so edits are still real
    // edits that integrity validation can detect. No lifetime-log clone is
    // needed merely to publish the next map frame.
    [CollectionDataContract(Name="ArrayOfHistoricalEvent",Namespace="http://schemas.datacontract.org/2004/07/WorldGen.Core",ItemName="HistoricalEvent")]
    public sealed class HistoricalEventArchive:IList<HistoricalEvent>,IReadOnlyList<HistoricalEvent>
    {
        const int PageSize=256;
        List<HistoricalEvent[]> pages=new List<HistoricalEvent[]>();
        readonly object gate=new object();
        Dictionary<int,HistoricalEvent> materialized;
        int count,version;bool detached=true,writableLast;
        public HistoricalEventArchive(){}
        public HistoricalEventArchive(IEnumerable<HistoricalEvent> events){foreach(var item in events)Add(item);}
        public int Count=>count;
        public bool IsReadOnly=>false;
        public HistoricalEvent this[int index]
        {
            get
            {
                Check(index);
                if(!detached)return materialized!=null&&materialized.TryGetValue(index,out var owned)?owned:pages[index/PageSize][index%PageSize];
                lock(gate){if(materialized!=null&&materialized.TryGetValue(index,out var value))return value;var copy=pages[index/PageSize][index%PageSize]?.Copy();Local()[index]=copy;return copy;}
            }
            set{Check(index);lock(gate){Local()[index]=value;version++;}}
        }
        void Check(int index){if(index<0||index>=count)throw new ArgumentOutOfRangeException(nameof(index));}
        Dictionary<int,HistoricalEvent> Local()=>materialized??(materialized=new Dictionary<int,HistoricalEvent>());
        internal HistoricalEventArchive Fork(bool publicRecords)
        {
            lock(gate){
            var next=new HistoricalEventArchive{pages=new List<HistoricalEvent[]>(pages),count=count,detached=publicRecords};
            // Usually empty for private playback. Explicitly edited/read public
            // records must be frozen before another branch can share them.
            if(materialized!=null&&materialized.Count>0)
            {
                var copied=new HashSet<int>();
                foreach(var entry in materialized)
                {
                    int page=entry.Key/PageSize;if(copied.Add(page))next.pages[page]=(HistoricalEvent[])next.pages[page].Clone();
                    next.pages[page][entry.Key%PageSize]=entry.Value?.Copy();
                }
            }
            writableLast=false;return next;}
        }
        public void Add(HistoricalEvent item)
        {
            if(count%PageSize==0){pages.Add(new HistoricalEvent[PageSize]);writableLast=true;}
            else if(!writableLast){pages[pages.Count-1]=(HistoricalEvent[])pages[pages.Count-1].Clone();writableLast=true;}
            pages[count/PageSize][count%PageSize]=detached?item?.Copy():item;
            if(detached)Local()[count]=item;count++;version++;
        }
        public void Clear(){pages=new List<HistoricalEvent[]>();materialized=null;count=0;writableLast=false;version++;}
        public IEnumerator<HistoricalEvent> GetEnumerator()=>Enumerate(version).GetEnumerator();
        IEnumerable<HistoricalEvent> Enumerate(int expected){for(int i=0;i<count;i++){if(expected!=version)throw new InvalidOperationException("Archive modified during enumeration.");yield return this[i];}if(expected!=version)throw new InvalidOperationException("Archive modified during enumeration.");}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public int IndexOf(HistoricalEvent item){for(int i=0;i<count;i++)if(ReferenceEquals(this[i],item))return i;return -1;}
        public bool Contains(HistoricalEvent item)=>IndexOf(item)>=0;
        public void CopyTo(HistoricalEvent[] array,int index){if(array==null)throw new ArgumentNullException(nameof(array));if(index<0||index>array.Length-count)throw new ArgumentOutOfRangeException(nameof(index));for(int i=0;i<count;i++)array[index+i]=this[i];}
        // Explicit editing is uncommon; preserve normal collection semantics
        // without introducing these linear operations into simulation turns.
        public void Insert(int index,HistoricalEvent item){var values=this.ToList();values.Insert(index,item);Replace(values);}
        public void RemoveAt(int index){var values=this.ToList();values.RemoveAt(index);Replace(values);}
        public bool Remove(HistoricalEvent item){int index=IndexOf(item);if(index<0)return false;RemoveAt(index);return true;}
        void Replace(IEnumerable<HistoricalEvent> values){Clear();foreach(var value in values)Add(value);}
        public static implicit operator HistoricalEventArchive(List<HistoricalEvent> values)=>values==null?null:new HistoricalEventArchive(values);
    }
}
