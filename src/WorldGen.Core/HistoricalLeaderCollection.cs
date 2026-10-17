using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
namespace WorldGen.Core
{
    // Array-compatible saved contract, with stable leader identities. Finished
    // reigns do not have to be recopied when appointing or publishing one ruler.
    // Public records still detach on first access, including retained references
    // across forks; this changes storage costs, never historical decisions.
    [CollectionDataContract(Name="ArrayOfHistoricalLeader",Namespace="http://schemas.datacontract.org/2004/07/WorldGen.Core",ItemName="HistoricalLeader")]
    public sealed class HistoricalLeaderCollection:ICollection<HistoricalLeader>,IReadOnlyList<HistoricalLeader>
    {
        const int PageSize=256;
        List<HistoricalLeader[]> pages=new List<HistoricalLeader[]>();
        readonly object gate=new object();
        Dictionary<int,HistoricalLeader> materialized;
        int count;bool writableLast;
        public HistoricalLeaderCollection(){}
        public HistoricalLeaderCollection(IEnumerable<HistoricalLeader> records){foreach(var record in records)Add(record);}
        public int Count=>count;
        public int Length=>count;
        public bool IsReadOnly=>false;
        void Check(int index){if((uint)index>=(uint)count)throw new ArgumentOutOfRangeException(nameof(index));}
        Dictionary<int,HistoricalLeader> Local()=>materialized??(materialized=new Dictionary<int,HistoricalLeader>());
        internal HistoricalLeader Peek(int index)
        {Check(index);lock(gate)return materialized!=null&&materialized.TryGetValue(index,out var record)?record:pages[index/PageSize][index%PageSize];}
        internal IEnumerable<HistoricalLeader> ReadOnlyValues(){int length=count;for(int i=0;i<length;i++)yield return Peek(i);}
        public HistoricalLeader this[int index]
        {
            get{Check(index);lock(gate){if(materialized!=null&&materialized.TryGetValue(index,out var record))return record;var copy=pages[index/PageSize][index%PageSize]?.Copy();Local()[index]=copy;return copy;}}
            set{Check(index);lock(gate)Local()[index]=value;}
        }
        internal HistoricalLeaderCollection Fork()
        {
            lock(gate)
            {
                var next=new HistoricalLeaderCollection{pages=new List<HistoricalLeader[]>(pages),count=count};
                if(materialized!=null&&materialized.Count>0)
                {
                    var copied=new HashSet<int>();
                    foreach(var entry in materialized)
                    {
                        int page=entry.Key/PageSize;if(copied.Add(page))next.pages[page]=(HistoricalLeader[])next.pages[page].Clone();
                        next.pages[page][entry.Key%PageSize]=entry.Value?.Copy();
                    }
                }
                writableLast=false;return next;
            }
        }
        public HistoricalLeaderCollection Copy()=>Fork();
        public void Add(HistoricalLeader record)
        {
            if(count%PageSize==0){pages.Add(new HistoricalLeader[PageSize]);writableLast=true;}
            else if(!writableLast){pages[pages.Count-1]=(HistoricalLeader[])pages[pages.Count-1].Clone();writableLast=true;}
            pages[count/PageSize][count%PageSize]=record?.Copy();Local()[count]=record;count++;
        }
        public void Clear(){pages=new List<HistoricalLeader[]>();materialized=null;count=0;writableLast=false;}
        public bool Contains(HistoricalLeader record){for(int i=0;i<count;i++)if(ReferenceEquals(this[i],record))return true;return false;}
        public bool Remove(HistoricalLeader record)=>throw new NotSupportedException("Recorded leader identities cannot be shifted.");
        public void CopyTo(HistoricalLeader[] array,int offset){if(array==null)throw new ArgumentNullException(nameof(array));if(offset<0||offset>array.Length-count)throw new ArgumentOutOfRangeException(nameof(offset));for(int i=0;i<count;i++)array[offset+i]=this[i];}
        public IEnumerator<HistoricalLeader> GetEnumerator()=>Enumerate(count).GetEnumerator();
        IEnumerable<HistoricalLeader> Enumerate(int length){for(int i=0;i<length;i++)yield return this[i];}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public static implicit operator HistoricalLeaderCollection(HistoricalLeader[] records)=>records==null?null:new HistoricalLeaderCollection(records);
    }
}
