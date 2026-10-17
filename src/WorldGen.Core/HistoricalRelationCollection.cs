using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
namespace WorldGen.Core
{
    // Same collection contract and canonical bytes as HistoricalRelation[].
    // Mutable records are private to a branch on FIRST access, for simulation
    // transactions as well as publications. Only accessed records are frozen
    // at the next fork; appending never copies the accumulated history.
    [CollectionDataContract(Name="ArrayOfHistoricalRelation",Namespace="http://schemas.datacontract.org/2004/07/WorldGen.Core",ItemName="HistoricalRelation")]
    public sealed class HistoricalRelationCollection:ICollection<HistoricalRelation>,IReadOnlyList<HistoricalRelation>
    {
        const int PageSize=256;
        List<HistoricalRelation[]> pages=new List<HistoricalRelation[]>();
        readonly object gate=new object();
        Dictionary<int,HistoricalRelation> materialized;
        int count;bool writableLast;
        public HistoricalRelationCollection(){}
        public HistoricalRelationCollection(IEnumerable<HistoricalRelation> records){foreach(var record in records)Add(record);}
        public int Count=>count;
        public int Length=>count;
        public bool IsReadOnly=>false;
        void Check(int index){if((uint)index>=(uint)count)throw new ArgumentOutOfRangeException(nameof(index));}
        Dictionary<int,HistoricalRelation> Local()=>materialized??(materialized=new Dictionary<int,HistoricalRelation>());
        internal HistoricalRelation Peek(int index)
        {Check(index);lock(gate)return materialized!=null&&materialized.TryGetValue(index,out var record)?record:pages[index/PageSize][index%PageSize];}
        internal IEnumerable<HistoricalRelation> ReadOnlyValues(){int length=count;for(int i=0;i<length;i++)yield return Peek(i);}
        public HistoricalRelation this[int index]
        {
            get{Check(index);lock(gate){if(materialized!=null&&materialized.TryGetValue(index,out var record)){if(record!=null)record.OwnedCanonicalRecord=null;return record;}var copy=pages[index/PageSize][index%PageSize]?.Copy();if(copy!=null)copy.OwnedCanonicalRecord=null;Local()[index]=copy;return copy;}}
            set{Check(index);lock(gate){if(value!=null)value.OwnedCanonicalRecord=null;Local()[index]=value;}}
        }
        internal HistoricalRelationCollection Fork()
        {
            lock(gate)
            {
                var next=new HistoricalRelationCollection{pages=new List<HistoricalRelation[]>(pages),count=count};
                if(materialized!=null&&materialized.Count>0)
                {
                    var copied=new HashSet<int>();
                    foreach(var entry in materialized)
                    {
                        int page=entry.Key/PageSize;if(copied.Add(page))next.pages[page]=(HistoricalRelation[])next.pages[page].Clone();
                        var copy=entry.Value?.Copy();if(copy!=null)copy.OwnedCanonicalRecord=null;
                        next.pages[page][entry.Key%PageSize]=copy;
                    }
                }
                writableLast=false;return next;
            }
        }
        public HistoricalRelationCollection Copy()=>Fork();
        public void Add(HistoricalRelation record)
        {
            if(count%PageSize==0){pages.Add(new HistoricalRelation[PageSize]);writableLast=true;}
            else if(!writableLast){pages[pages.Count-1]=(HistoricalRelation[])pages[pages.Count-1].Clone();writableLast=true;}
            pages[count/PageSize][count%PageSize]=record?.Copy();Local()[count]=record;count++;
        }
        public void Clear(){pages=new List<HistoricalRelation[]>();materialized=null;count=0;writableLast=false;}
        public int IndexOf(HistoricalRelation record){for(int i=0;i<count;i++)if(ReferenceEquals(this[i],record))return i;return -1;}
        public int FindIndex(Predicate<HistoricalRelation> predicate){if(predicate==null)throw new ArgumentNullException(nameof(predicate));for(int i=0;i<count;i++)if(predicate(this[i]))return i;return -1;}
        public bool Contains(HistoricalRelation record)=>IndexOf(record)>=0;
        public bool Remove(HistoricalRelation record)=>throw new NotSupportedException("Relation identities cannot be shifted.");
        public void CopyTo(HistoricalRelation[] array,int offset){if(array==null)throw new ArgumentNullException(nameof(array));if(offset<0||offset>array.Length-count)throw new ArgumentOutOfRangeException(nameof(offset));for(int i=0;i<count;i++)array[offset+i]=this[i];}
        // An append used to replace the array. Existing enumerators therefore
        // retain their original prefix rather than visiting newly added pairs.
        public IEnumerator<HistoricalRelation> GetEnumerator()=>Enumerate(count).GetEnumerator();
        IEnumerable<HistoricalRelation> Enumerate(int length){for(int i=0;i<length;i++)yield return this[i];}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public static implicit operator HistoricalRelationCollection(HistoricalRelation[] records)=>records==null?null:new HistoricalRelationCollection(records);
    }
    // Aligned campaign IDs have the historical int[] data contract. Values are
    // immutable scalars; a branch clones a 256-value page only on first write.
    [CollectionDataContract(Name="ArrayOfint",Namespace="http://schemas.microsoft.com/2003/10/Serialization/Arrays",ItemName="int")]
    public sealed class HistoricalRelationCampaigns:ICollection<int>,IReadOnlyList<int>
    {
        const int PageSize=256;
        List<int[]> pages=new List<int[]>();
        readonly object gate=new object();
        HashSet<int> writable=new HashSet<int>();
        int count;
        public HistoricalRelationCampaigns(){}
        public HistoricalRelationCampaigns(IEnumerable<int> values){foreach(int value in values)Add(value);}
        public int Count=>count;
        public int Length=>count;
        public bool IsReadOnly=>false;
        void Check(int index){if((uint)index>=(uint)count)throw new ArgumentOutOfRangeException(nameof(index));}
        void WritePage(int page){if(writable.Add(page))pages[page]=(int[])pages[page].Clone();}
        public int this[int index]{get{Check(index);return pages[index/PageSize][index%PageSize];}set{Check(index);int page=index/PageSize;if(pages[page][index%PageSize]==value)return;WritePage(page);pages[page][index%PageSize]=value;}}
        internal HistoricalRelationCampaigns Fork(){lock(gate){var next=new HistoricalRelationCampaigns{pages=new List<int[]>(pages),count=count};writable.Clear();return next;}}
        public void Add(int value){int page=count/PageSize;if(count%PageSize==0){pages.Add(new int[PageSize]);writable.Add(page);}else WritePage(page);pages[page][count%PageSize]=value;count++;}
        public void Grow(int length,int value){if(length<count)throw new ArgumentOutOfRangeException(nameof(length));while(count<length)Add(value);}
        public void Clear(){pages=new List<int[]>();writable.Clear();count=0;}
        public bool Contains(int value){for(int i=0;i<count;i++)if(this[i]==value)return true;return false;}
        public bool Remove(int value)=>throw new NotSupportedException("Relation identities cannot be shifted.");
        public void CopyTo(int[] array,int offset){if(array==null)throw new ArgumentNullException(nameof(array));if(offset<0||offset>array.Length-count)throw new ArgumentOutOfRangeException(nameof(offset));for(int i=0;i<count;i++)array[offset+i]=this[i];}
        public IEnumerator<int> GetEnumerator()=>Enumerate(count).GetEnumerator();
        IEnumerable<int> Enumerate(int length){for(int i=0;i<length;i++)yield return this[i];}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public static implicit operator HistoricalRelationCampaigns(int[] values)=>values==null?null:new HistoricalRelationCampaigns(values);
    }
}
