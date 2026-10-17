using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Threading;

namespace WorldGen.Core
{
    // Immutable, persistent spatial storage. Five fixed radix levels cover
    // 33,554,432 entries. A claim copies 5 x 32 slots, never a country's cells.
    // No writable page is exposed to publications or callers. Serialization
    // stores actual values; integrity validation rebuilds the Merkle hashes.
    [DataContract] public sealed class HistoricalCellTree
    {
        [DataContract] sealed class Node
        {
            [DataMember] internal Node[] Children;
            [DataMember] internal int[] Values;
            [IgnoreDataMember] internal byte[] Digest;
        }
        [ThreadStatic] static ReadInterningScope readScope;
        // Transport-only scope. Old files expanded persistent DAG nodes by
        // value; recover equal immutable nodes before publishing the world.
        // No authoritative fields, fingerprints or simulation work change.
        public static IDisposable BeginReadInterning(CancellationToken cancellation=default)=>new ReadInterningScope(cancellation);
        sealed class NodeContentComparer:IEqualityComparer<Node>
        {
            public int GetHashCode(Node node)
            {
                unchecked
                {
                    int hash=node.Values!=null?1231:1237;
                    for(int i=0;i<32;i++)hash=hash*31+(node.Values!=null?node.Values[i]:node.Children[i]==null?0:RuntimeHelpers.GetHashCode(node.Children[i]));
                    return hash;
                }
            }
            public bool Equals(Node a,Node b)
            {
                if(ReferenceEquals(a,b))return true;if(a==null||b==null||(a.Values==null)!=(b.Values==null))return false;
                if(a.Values!=null){for(int i=0;i<32;i++)if(a.Values[i]!=b.Values[i])return false;}
                else for(int i=0;i<32;i++)if(!ReferenceEquals(a.Children[i],b.Children[i]))return false;
                return true;
            }
        }
        sealed class ReadInterningScope:IDisposable
        {
            readonly ReadInterningScope previous;readonly CancellationToken token;
            readonly HashSet<Node> pool=new HashSet<Node>(new NodeContentComparer());bool disposed;
            internal ReadInterningScope(CancellationToken token){token.ThrowIfCancellationRequested();this.token=token;previous=readScope;readScope=this;}
            internal Node Intern(Node node)
            {
                token.ThrowIfCancellationRequested();if(node==null)return null;
                // Children have already been canonicalized by the wire DTO.
                // Hashes only choose buckets; every collision compares all32
                // actual integers or canonical-child identities.
                if(pool.TryGetValue(node,out var canonical))return canonical;
                pool.Add(node);return node;
            }
            public void Dispose()
            {
                if(disposed)return;
                if(!ReferenceEquals(readScope,this))throw new InvalidOperationException("Spatial read scopes must close in reverse order.");
                readScope=previous;disposed=true;
                // Only unique nodes are retained. Duplicate input nodes can be
                // collected while reading; neither worlds nor dictionaries live
                // in a process-wide cache after the synchronous read completes.
                pool.Clear();
            }
        }
        // XML binary transport alone uses reference-enabled nodes. Keeping the
        // logical Node contract by-value preserves the existing JSON adapter.
        [DataContract(Name="HistoricalCellTree.Node",Namespace="http://schemas.datacontract.org/2004/07/WorldGen.Core",IsReference=true)]
        sealed class BinaryNode
        {
            [DataMember] internal BinaryNode[] Children;
            [DataMember] internal int[] Values;
            [IgnoreDataMember] internal Node Logical;
            [IgnoreDataMember] internal bool Visiting;
            [IgnoreDataMember] internal byte Height;
        }
        sealed class BinaryNodeContentComparer:IEqualityComparer<BinaryNode>
        {
            public int GetHashCode(BinaryNode node)
            {
                unchecked
                {
                    int hash=node.Values!=null?1231:1237;
                    for(int i=0;i<32;i++)hash=hash*31+(node.Values!=null?node.Values[i]:node.Children[i]==null?0:RuntimeHelpers.GetHashCode(node.Children[i]));
                    return hash;
                }
            }
            public bool Equals(BinaryNode a,BinaryNode b)
            {
                if(ReferenceEquals(a,b))return true;if(a==null||b==null||(a.Values==null)!=(b.Values==null))return false;
                if(a.Values!=null){for(int i=0;i<32;i++)if(a.Values[i]!=b.Values[i])return false;}
                else for(int i=0;i<32;i++)if(!ReferenceEquals(a.Children[i],b.Children[i]))return false;
                return true;
            }
        }
        public static BinaryTransportScope CreateBinaryTransport(CancellationToken cancellation=default)=>new BinaryTransportScope(cancellation);
        public sealed class BinaryTransportScope:ISerializationSurrogateProvider,IDisposable
        {
            readonly CancellationToken token;readonly Dictionary<Node,BinaryNode> written=new Dictionary<Node,BinaryNode>();
            readonly HashSet<BinaryNode> writePool=new HashSet<BinaryNode>(new BinaryNodeContentComparer());bool disposed;
            internal BinaryTransportScope(CancellationToken cancellation){token=cancellation;cancellation.ThrowIfCancellationRequested();}
            public Type GetSurrogateType(Type type)=>type==typeof(Node)?typeof(BinaryNode):type;
            public object GetObjectToSerialize(object value,Type targetType)
            {Check();return value is Node node?WriteNode(node):value;}
            public object GetDeserializedObject(object value,Type targetType)
            {Check();return value is BinaryNode node&&targetType==typeof(Node)?ReadNode(node):value;}
            void Check(){if(disposed)throw new ObjectDisposedException(nameof(BinaryTransportScope));token.ThrowIfCancellationRequested();}
            BinaryNode WriteNode(Node node,int depth=0)
            {
                Check();if(node==null)return null;
                if(written.TryGetValue(node,out var prior))
                {if(prior.Visiting||depth+prior.Height>5)throw new SerializationException("Invalid spatial radix depth or cycle.");return prior;}
                if(depth>=5)throw new SerializationException("Invalid spatial radix depth.");
                var result=new BinaryNode{Values=node.Values,Visiting=true};written.Add(node,result);
                try
                {
                    int height=1;
                    if(node.Values!=null)
                    {if(node.Values.Length!=32||node.Children!=null)throw new SerializationException("Invalid spatial leaf.");}
                    else
                    {
                        if(node.Children?.Length!=32)throw new SerializationException("Invalid spatial branch.");
                        result.Children=new BinaryNode[32];
                        for(int i=0;i<32;i++){result.Children[i]=WriteNode(node.Children[i],depth+1);height=Math.Max(height,1+(result.Children[i]?.Height??0));}
                    }
                    if(depth+height>5)throw new SerializationException("Invalid spatial radix depth.");
                    result.Height=(byte)height;result.Visiting=false;
                    // Fresh simulation pages need not share object identities.
                    // Canonicalize children first, then compare every actual
                    // value/pointer: neither cached digests nor hash collisions
                    // may merge unequal content. This pool lives for one save.
                    if(writePool.TryGetValue(result,out var canonical)){written[node]=canonical;return canonical;}
                    writePool.Add(result);return result;
                }
                catch{written.Remove(node);throw;}
                finally{result.Visiting=false;}
            }
            Node ReadNode(BinaryNode node,int depth=0)
            {
                Check();if(node==null)return null;
                if(node.Logical!=null){if(depth+node.Height>5)throw new SerializationException("Invalid spatial radix depth.");return node.Logical;}
                if(depth>=5||node.Visiting)throw new SerializationException("Invalid spatial radix depth or cycle.");
                node.Visiting=true;
                try
                {
                    var result=new Node{Values=node.Values};int height=1;
                    if(node.Values!=null){if(node.Values.Length!=32||node.Children!=null)throw new SerializationException("Invalid spatial leaf.");}
                    else
                    {
                        if(node.Children?.Length!=32)throw new SerializationException("Invalid spatial branch.");
                        result.Children=new Node[32];
                        for(int i=0;i<32;i++){result.Children[i]=ReadNode(node.Children[i],depth+1);height=Math.Max(height,1+(node.Children[i]?.Height??0));}
                    }
                    if(depth+height>5)throw new SerializationException("Invalid spatial radix depth.");node.Height=(byte)height;
                    return node.Logical=readScope?.Intern(result)??result;
                }
                finally{node.Visiting=false;}
            }
            public void Dispose(){if(disposed)return;disposed=true;written.Clear();writePool.Clear();}
        }
        [DataMember] Node root;
        [DataMember] int length;
        [IgnoreDataMember] string fingerprint;
        [IgnoreDataMember] byte[] canonicalFingerprint;
        [IgnoreDataMember] string canonicalFingerprintHash;
        internal void WriteCanonicalFingerprint(BinaryWriter target)
        {
            string hash=Fingerprint();
            if(canonicalFingerprint==null||!ReferenceEquals(hash,canonicalFingerprintHash))
            {
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {writer.Write("WorldGen.Core.HistoricalCellTree");writer.Write(hash);canonicalFingerprint=stream.ToArray();canonicalFingerprintHash=hash;}
            }
            target.Write(canonicalFingerprint);
        }
        public int Length=>length;
        // Explicit saved-state validation only. Visit allocated sparse entries,
        // not every possible cell/region and never an actor's recurring turn.
        internal bool AllPresentValues(Func<int,int,bool> predicate)
        {if(predicate==null)throw new ArgumentNullException(nameof(predicate));return length>=0&&length<=33554432&&PresentValues(root,0,20,predicate);}
        bool PresentValues(Node node,int first,int shift,Func<int,int,bool> predicate)
        {
            if(node==null)return true;
            if(shift==0)
            {
                if(node.Values?.Length!=32||node.Children!=null)return false;
                for(int i=0;i<32;i++)if(node.Values[i]!=-1&&(first+i>=length||!predicate(first+i,node.Values[i])))return false;
                return true;
            }
            if(node.Children?.Length!=32||node.Values!=null)return false;
            for(int i=0;i<32;i++)if(!PresentValues(node.Children[i],first+(i<<shift),shift-5,predicate))return false;
            return true;
        }
        // Read-only bulk export for rendering/diagnostics, not actor turns.
        // Leaf copies avoid a five-level lookup for each displayed value.
        public void CopyTo(int[] destination,int offset=0)
        {
            if(destination==null||offset<0||offset>destination.Length-length)throw new ArgumentOutOfRangeException(nameof(destination));
            for(int i=0;i<length;i++)destination[offset+i]=-1;
            CopyValues(root,0,20,destination,offset);
        }
        void CopyValues(Node node,int first,int shift,int[] destination,int offset)
        {
            if(node==null||first>=length)return;
            if(shift==0){Array.Copy(node.Values,0,destination,offset+first,Math.Min(32,length-first));return;}
            for(int i=0;i<32;i++){int start=first+(i<<shift);if(start>=length)break;CopyValues(node.Children[i],start,shift-5,destination,offset);}
        }
        public int this[int index] { get { if((uint)index>=(uint)length)throw new ArgumentOutOfRangeException(nameof(index));var node=root;for(int shift=20;shift>0;shift-=5){node=node?.Children[(index>>shift)&31];if(node==null)return -1;}return node?.Values[index&31]??-1; } }
        public HistoricalCellTree(int length){if(length<0||length>33554432)throw new ArgumentOutOfRangeException(nameof(length));this.length=length;}
        HistoricalCellTree(int length,Node root){this.length=length;this.root=root;}
        internal HistoricalCellTree Grow(int capacity){if(capacity<length||capacity>33554432)throw new ArgumentOutOfRangeException(nameof(capacity));return new HistoricalCellTree(capacity,root);}
        public HistoricalCellTree With(int index,int value)
        {if((uint)index>=(uint)length)throw new ArgumentOutOfRangeException(nameof(index));if(this[index]==value)return this;return new HistoricalCellTree(length,Set(root,index,20,value));}
        // One bounded actor record, sharing untouched nodes. In particular a
        // religious body must not copy the same radix path fifteen times.
        // Existing With/canonical/transport behavior is unchanged.
        public HistoricalCellTree WithBlock(int start,ReadOnlySpan<int> values)
        {
            if(values.Length>32||start<0||start>length-values.Length)throw new ArgumentOutOfRangeException(nameof(start));
            bool changed=false;for(int i=0;i<values.Length;i++)if(this[start+i]!=values[i]){changed=true;break;}
            return changed?new HistoricalCellTree(length,SetBlock(root,0,20,start,values)):this;
        }
        static Node SetBlock(Node old,int first,int shift,int start,ReadOnlySpan<int> values)
        {
            if(shift==0)
            {
                var leaf=old?.Values==null?EmptyValues():(int[])old.Values.Clone();values.CopyTo(leaf.AsSpan(start-first,values.Length));
                for(int i=0;i<32;i++)if(leaf[i]!=-1)return new Node{Values=leaf};return null;
            }
            var children=old==null?new Node[32]:(Node[])old.Children.Clone();int end=start+values.Length;
            for(int child=(start-first)>>shift;child<=((end-1-first)>>shift);child++)
            {
                int childFirst=first+(child<<shift),lo=Math.Max(start,childFirst),hi=Math.Min(end,childFirst+(1<<shift));
                children[child]=SetBlock(children[child],childFirst,shift-5,lo,values.Slice(lo-start,hi-lo));
            }
            for(int i=0;i<32;i++)if(children[i]!=null)return new Node{Children=children};return null;
        }
        static Node Set(Node old,int index,int shift,int value)
        {
            if(shift==0){var values=old?.Values==null?EmptyValues():(int[])old.Values.Clone();values[index&31]=value;if(value!=-1)return new Node{Values=values};for(int i=0;i<32;i++)if(values[i]!=-1)return new Node{Values=values};return null;}
            var children=old==null?new Node[32]:(Node[])old.Children.Clone();int at=(index>>shift)&31;children[at]=Set(children[at],index,shift-5,value);if(children[at]!=null)return new Node{Children=children};for(int i=0;i<32;i++)if(children[i]!=null)return new Node{Children=children};return null;
        }
        static int[] EmptyValues(){var values=new int[32];for(int i=0;i<32;i++)values[i]=-1;return values;}
        public static HistoricalCellTree From(int length,Func<int,int> read)
        {if(read==null)throw new ArgumentNullException(nameof(read));var result=new HistoricalCellTree(length);result.root=Build(0,20,length,read);return result;}
        static Node Build(int first,int shift,int length,Func<int,int> read)
        {
            if(first>=length)return null;
            if(shift==0){var values=EmptyValues();bool any=false;for(int i=0;i<32&&first+i<length;i++){values[i]=read(first+i);any|=values[i]!=-1;}return any?new Node{Values=values}:null;}
            var children=new Node[32];bool present=false;for(int i=0;i<32;i++){children[i]=Build(first+(i<<shift),shift-5,length,read);present|=children[i]!=null;}return present?new Node{Children=children}:null;
        }
        public string Fingerprint(bool verify=false)
        {
            if(!verify&&fingerprint!=null)return fingerprint;
            CanonicalHashCosts.TreeFingerprint();
            // The tree and its length are immutable. Trusted publication can
            // reuse their digest; explicit integrity verification still walks
            // every actual node and recomputes the result, including old saves.
            var context=CanonicalTreeScratch.Acquire(out bool owned);
            try
            {
                // One scratch buffer per radix depth, allocated only when that
                // depth has an uncached node. Child recursion never overwrites
                // its parent's partially assembled canonical bytes.
                var scratch=context.Buffers;var bytes=context.Root;PutTreeInteger(bytes,0,length);
                Buffer.BlockCopy(Digest(root,context.Sha,verify,scratch,0),0,bytes,4,32);
                return fingerprint=Hex(context.Sha.ComputeHash(bytes));
            }
            finally{if(owned)context.Dispose();}
        }
        static readonly byte[] Absent=new byte[32];
        static void PutTreeInteger(byte[] bytes,int offset,int value)
        {bytes[offset]=(byte)value;bytes[offset+1]=(byte)(value>>8);bytes[offset+2]=(byte)(value>>16);bytes[offset+3]=(byte)(value>>24);}
        static byte[] Digest(Node node,SHA256 sha,bool verify,byte[][] scratch,int depth)
        {
            if(node==null)return Absent;if(!verify&&node.Digest!=null)return node.Digest;
            if(depth>=scratch.Length)throw new SerializationException("Invalid spatial radix depth.");
            bool leaf=node.Values!=null;int count=leaf?129:1025;var bytes=scratch[depth]??(scratch[depth]=new byte[count]);
            CanonicalHashCosts.TreeNode(count);
            // Valid trees have branches at depths0–3 and leaves at4. Retain a
            // deterministic rejection rather than aliasing scratch on bad data.
            if(bytes.Length<count)throw new SerializationException("Invalid spatial radix shape.");
            bytes[0]=(byte)(leaf?1:0);
            if(leaf)
            {
                if(node.Values.Length!=32||node.Children!=null)throw new SerializationException("Invalid spatial leaf.");
                for(int k=0;k<32;k++)PutTreeInteger(bytes,1+k*4,node.Values[k]);
            }
            else
            {
                if(node.Children?.Length!=32)throw new SerializationException("Invalid spatial branch.");
                // Absent child digests are exactly32 zero bytes. Clear the
                // reused branch payload once instead of crossing BlockCopy
                // (and recursing) once for every absent slot.
                Array.Clear(bytes,1,1024);
                bool trace=CanonicalHashCosts.TraceTreeBranches;int present=0;
                for(int k=0;k<32;k++){var child=node.Children[k];if(child==null)continue;if(trace)present++;Buffer.BlockCopy(Digest(child,sha,verify,scratch,depth+1),0,bytes,1+k*32,32);}
                if(trace)CanonicalHashCosts.TreeBranch(present);
            }
            return node.Digest=sha.ComputeHash(bytes,0,count);
        }
        static string Hex(byte[] bytes)=>BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
    }
    public static class HistoricalCells
    {
        public static int Count(PrehistoryState s)=>s?.History?.Aggregates?.Cells.Length/5??s?.History?.CellPolity.Length??s?.Society?.CellCulture.Length??0;
        public static int Culture(PrehistoryState s,int cell)=>Read(s,cell,0);
        public static int Polity(PrehistoryState s,int cell){int owner=Read(s,cell,1);return s?.History?.Aggregates is HistoricalAggregateState a?a.Resolve(owner,s.History):owner;}
        public static int Religion(PrehistoryState s,int cell)=>Read(s,cell,2);
        public static int Market(PrehistoryState s,int cell)=>Read(s,cell,3);
        public static int Stage(PrehistoryState s,int cell)=>Read(s,cell,4);
        static int Read(PrehistoryState s,int cell,int layer)
        {
            var a=s?.History?.Aggregates;if(a!=null)
            {
                if(cell<0||cell>=a.Cells.Length/5)return -1;
                if(s.History.RegionalTraditions!=null)
                {
                    if(layer==0)return HistoricalRegionalTraditions.Culture(s,cell);
                    if(layer==4)return HistoricalRegionalTraditions.Stage(s,cell);
                    if(layer==2&&a.Cells[cell*5+2]<0){int culture=HistoricalRegionalTraditions.Culture(s,cell);return culture>=0?s.History.Cultures[culture].ReligionId:-1;}
                }
                return a.Cells[cell*5+layer];
            }
            var values=layer==0?s?.Society?.CellCulture:layer==1?s?.History?.CellPolity:layer==2?s?.History?.CellReligion:layer==3?s?.History?.CellMarket:s?.History?.CellStage;
            return values!=null&&cell>=0&&cell<values.Length?values[cell]:-1;
        }
    }
}
