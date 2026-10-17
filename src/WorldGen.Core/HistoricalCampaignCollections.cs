using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
namespace WorldGen.Core
{
    // Internal storage only; each public wrapper retains its preceding array
    // data contract. A fork copies page references and freezes only records
    // exposed since its preceding fork. Both branches detach on first access.
    internal sealed class HistoricalPagedRecords<T> where T:class
    {
        const int PageSize=256;
        readonly Func<T,T> copy;readonly Action<T> expose;
        readonly object gate=new object();
        List<T[]> pages=new List<T[]>();Dictionary<int,T> materialized;
        int count;bool writableLast;
        public HistoricalPagedRecords(Func<T,T> copy,Action<T> expose=null){this.copy=copy;this.expose=expose;}
        public int Count=>count;
        void Check(int index){if((uint)index>=(uint)count)throw new ArgumentOutOfRangeException(nameof(index));}
        Dictionary<int,T> Local()=>materialized??(materialized=new Dictionary<int,T>());
        T Clone(T value)=>value==null?null:copy(value);
        public T Peek(int index){Check(index);lock(gate)return materialized!=null&&materialized.TryGetValue(index,out var row)?row:pages[index/PageSize][index%PageSize];}
        public T Get(int index)
        {
            Check(index);lock(gate)
            {
                if(materialized==null||!materialized.TryGetValue(index,out var row)){row=Clone(pages[index/PageSize][index%PageSize]);Local()[index]=row;}
                expose?.Invoke(row);return row;
            }
        }
        public void Set(int index,T value){Check(index);lock(gate){expose?.Invoke(value);Local()[index]=value;}}
        public void Add(T value)
        {
            if(count%PageSize==0){pages.Add(new T[PageSize]);writableLast=true;}
            else if(!writableLast){pages[pages.Count-1]=(T[])pages[pages.Count-1].Clone();writableLast=true;}
            var frozen=Clone(value);expose?.Invoke(frozen);pages[count/PageSize][count%PageSize]=frozen;
            expose?.Invoke(value);Local()[count]=value;count++;
        }
        public HistoricalPagedRecords<T> Fork()
        {
            lock(gate)
            {
                var next=new HistoricalPagedRecords<T>(copy,expose){pages=new List<T[]>(pages),count=count};
                if(materialized!=null&&materialized.Count>0)
                {
                    var copied=new HashSet<int>();
                    foreach(var entry in materialized)
                    {
                        int page=entry.Key/PageSize;if(copied.Add(page))next.pages[page]=(T[])next.pages[page].Clone();
                        var frozen=Clone(entry.Value);expose?.Invoke(frozen);next.pages[page][entry.Key%PageSize]=frozen;
                    }
                }
                writableLast=false;return next;
            }
        }
        public void Clear(){pages=new List<T[]>();materialized=null;count=0;writableLast=false;}
        public bool Contains(T value){for(int i=0;i<count;i++)if(ReferenceEquals(Get(i),value))return true;return false;}
        public void CopyTo(T[] array,int offset){if(array==null)throw new ArgumentNullException(nameof(array));if(offset<0||offset>array.Length-count)throw new ArgumentOutOfRangeException(nameof(offset));for(int i=0;i<count;i++)array[offset+i]=Get(i);}
        // Capture the prefix before returning the iterator, as array replacement
        // formerly left an existing enumerator on its original array prefix.
        public IEnumerator<T> GetEnumerator()=>Enumerate(count).GetEnumerator();
        IEnumerable<T> Enumerate(int length){for(int i=0;i<length;i++)yield return Get(i);}
        public IEnumerable<T> ReadOnlyValues(){int length=count;for(int i=0;i<length;i++)yield return Peek(i);}
    }
    [CollectionDataContract(Name="ArrayOfHistoricalCampaign",Namespace="http://schemas.datacontract.org/2004/07/WorldGen.Core",ItemName="HistoricalCampaign")]
    public sealed class HistoricalCampaignCollection:ICollection<HistoricalCampaign>,IReadOnlyList<HistoricalCampaign>
    {
        readonly HistoricalPagedRecords<HistoricalCampaign> rows;
        static void Expose(HistoricalCampaign row){if(row!=null)row.OwnedCanonicalRecord=null;}
        public HistoricalCampaignCollection(){rows=new HistoricalPagedRecords<HistoricalCampaign>(row=>row.Copy(),Expose);}
        public HistoricalCampaignCollection(IEnumerable<HistoricalCampaign> records):this(){foreach(var record in records)Add(record);}
        HistoricalCampaignCollection(HistoricalPagedRecords<HistoricalCampaign> rows){this.rows=rows;}
        public int Count=>rows.Count;public int Length=>rows.Count;public bool IsReadOnly=>false;
        public HistoricalCampaign this[int index]{get=>rows.Get(index);set=>rows.Set(index,value);}
        internal HistoricalCampaign Peek(int index)=>rows.Peek(index);
        internal IEnumerable<HistoricalCampaign> ReadOnlyValues()=>rows.ReadOnlyValues();
        public HistoricalCampaignCollection Copy()=>new HistoricalCampaignCollection(rows.Fork());
        public void Add(HistoricalCampaign row)=>rows.Add(row);public void Clear()=>rows.Clear();
        public bool Contains(HistoricalCampaign row)=>rows.Contains(row);public void CopyTo(HistoricalCampaign[] array,int offset)=>rows.CopyTo(array,offset);
        public bool Remove(HistoricalCampaign row)=>throw new NotSupportedException("Campaign identities cannot be shifted.");
        public IEnumerator<HistoricalCampaign> GetEnumerator()=>rows.GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public static implicit operator HistoricalCampaignCollection(HistoricalCampaign[] records)=>records==null?null:new HistoricalCampaignCollection(records);
    }
    [CollectionDataContract(Name="ArrayOfHistoricalCampaignTheater",Namespace="http://schemas.datacontract.org/2004/07/WorldGen.Core",ItemName="HistoricalCampaignTheater")]
    public sealed class HistoricalCampaignTheaterCollection:ICollection<HistoricalCampaignTheater>,IReadOnlyList<HistoricalCampaignTheater>
    {
        readonly HistoricalPagedRecords<HistoricalCampaignTheater> rows;
        static void Expose(HistoricalCampaignTheater row){if(row!=null)row.OwnedCanonicalRecord=null;}
        public HistoricalCampaignTheaterCollection(){rows=new HistoricalPagedRecords<HistoricalCampaignTheater>(row=>row.Copy(),Expose);}
        public HistoricalCampaignTheaterCollection(IEnumerable<HistoricalCampaignTheater> records):this(){foreach(var record in records)Add(record);}
        HistoricalCampaignTheaterCollection(HistoricalPagedRecords<HistoricalCampaignTheater> rows){this.rows=rows;}
        public int Count=>rows.Count;public int Length=>rows.Count;public bool IsReadOnly=>false;
        public HistoricalCampaignTheater this[int index]{get=>rows.Get(index);set=>rows.Set(index,value);}
        internal HistoricalCampaignTheater Peek(int index)=>rows.Peek(index);
        internal IEnumerable<HistoricalCampaignTheater> ReadOnlyValues()=>rows.ReadOnlyValues();
        public HistoricalCampaignTheaterCollection Copy()=>new HistoricalCampaignTheaterCollection(rows.Fork());
        public void Add(HistoricalCampaignTheater row)=>rows.Add(row);public void Clear()=>rows.Clear();
        public bool Contains(HistoricalCampaignTheater row)=>rows.Contains(row);public void CopyTo(HistoricalCampaignTheater[] array,int offset)=>rows.CopyTo(array,offset);
        public bool Remove(HistoricalCampaignTheater row)=>throw new NotSupportedException("Campaign identities cannot be shifted.");
        public IEnumerator<HistoricalCampaignTheater> GetEnumerator()=>rows.GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public static implicit operator HistoricalCampaignTheaterCollection(HistoricalCampaignTheater[] records)=>records==null?null:new HistoricalCampaignTheaterCollection(records);
    }
    [CollectionDataContract(Name="ArrayOfHistoricalCampaignAdvance",Namespace="http://schemas.datacontract.org/2004/07/WorldGen.Core",ItemName="HistoricalCampaignAdvance")]
    public sealed class HistoricalCampaignAdvanceCollection:ICollection<HistoricalCampaignAdvance>,IReadOnlyList<HistoricalCampaignAdvance>
    {
        readonly HistoricalPagedRecords<HistoricalCampaignAdvance> rows;
        static void Expose(HistoricalCampaignAdvance row){if(row!=null)row.OwnedCanonicalRecord=null;}
        public HistoricalCampaignAdvanceCollection(){rows=new HistoricalPagedRecords<HistoricalCampaignAdvance>(row=>row.Copy(),Expose);}
        public HistoricalCampaignAdvanceCollection(IEnumerable<HistoricalCampaignAdvance> records):this(){foreach(var record in records)Add(record);}
        HistoricalCampaignAdvanceCollection(HistoricalPagedRecords<HistoricalCampaignAdvance> rows){this.rows=rows;}
        public int Count=>rows.Count;public int Length=>rows.Count;public bool IsReadOnly=>false;
        public HistoricalCampaignAdvance this[int index]{get=>rows.Get(index);set=>rows.Set(index,value);}
        internal HistoricalCampaignAdvance Peek(int index)=>rows.Peek(index);
        internal IEnumerable<HistoricalCampaignAdvance> ReadOnlyValues()=>rows.ReadOnlyValues();
        public HistoricalCampaignAdvanceCollection Copy()=>new HistoricalCampaignAdvanceCollection(rows.Fork());
        public void Add(HistoricalCampaignAdvance row)=>rows.Add(row);public void Clear()=>rows.Clear();
        public bool Contains(HistoricalCampaignAdvance row)=>rows.Contains(row);public void CopyTo(HistoricalCampaignAdvance[] array,int offset)=>rows.CopyTo(array,offset);
        public bool Remove(HistoricalCampaignAdvance row)=>throw new NotSupportedException("Campaign identities cannot be shifted.");
        public IEnumerator<HistoricalCampaignAdvance> GetEnumerator()=>rows.GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public static implicit operator HistoricalCampaignAdvanceCollection(HistoricalCampaignAdvance[] records)=>records==null?null:new HistoricalCampaignAdvanceCollection(records);
    }
    [CollectionDataContract(Name="ArrayOfHistoricalMilitaryOperation",Namespace="http://schemas.datacontract.org/2004/07/WorldGen.Core",ItemName="HistoricalMilitaryOperation")]
    public sealed class HistoricalMilitaryOperationCollection:ICollection<HistoricalMilitaryOperation>,IReadOnlyList<HistoricalMilitaryOperation>
    {
        readonly HistoricalPagedRecords<HistoricalMilitaryOperation> rows;
        static void Expose(HistoricalMilitaryOperation row){if(row!=null)row.OwnedCanonicalRecord=null;}
        public HistoricalMilitaryOperationCollection(){rows=new HistoricalPagedRecords<HistoricalMilitaryOperation>(row=>row.Copy(),Expose);}
        public HistoricalMilitaryOperationCollection(IEnumerable<HistoricalMilitaryOperation> records):this(){foreach(var record in records)Add(record);}
        HistoricalMilitaryOperationCollection(HistoricalPagedRecords<HistoricalMilitaryOperation> rows){this.rows=rows;}
        public int Count=>rows.Count;public int Length=>rows.Count;public bool IsReadOnly=>false;
        public HistoricalMilitaryOperation this[int index]{get=>rows.Get(index);set=>rows.Set(index,value);}
        internal HistoricalMilitaryOperation Peek(int index)=>rows.Peek(index);
        internal IEnumerable<HistoricalMilitaryOperation> ReadOnlyValues()=>rows.ReadOnlyValues();
        public HistoricalMilitaryOperationCollection Copy()=>new HistoricalMilitaryOperationCollection(rows.Fork());
        public void Add(HistoricalMilitaryOperation row)=>rows.Add(row);public void Clear()=>rows.Clear();
        public bool Contains(HistoricalMilitaryOperation row)=>rows.Contains(row);public void CopyTo(HistoricalMilitaryOperation[] array,int offset)=>rows.CopyTo(array,offset);
        public bool Remove(HistoricalMilitaryOperation row)=>throw new NotSupportedException("Campaign identities cannot be shifted.");
        public IEnumerator<HistoricalMilitaryOperation> GetEnumerator()=>rows.GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public static implicit operator HistoricalMilitaryOperationCollection(HistoricalMilitaryOperation[] records)=>records==null?null:new HistoricalMilitaryOperationCollection(records);
    }
}
