using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using WorldGen.Core;

public static class HistoryTreeScratchTests
{
    // Deliberately independent digest assembly: fresh zeroed buffers, grouped
    // actual values and platform SHA, never the production Node/Digest method.
    static string IndependentDigest(int length,Dictionary<int,int> values)
    {
        byte[] Node(Dictionary<int,int> entries,int shift)
        {
            if(entries.Count==0)return new byte[32];
            if(shift==0)
            {using var bytes=new MemoryStream();using var writer=new BinaryWriter(bytes);writer.Write((byte)1);for(int i=0;i<32;i++)writer.Write(entries.FirstOrDefault(p=>(p.Key&31)==i,new KeyValuePair<int,int>(0,-1)).Value);return SHA256.HashData(bytes.ToArray());}
            var payload=new byte[1025];foreach(var group in entries.GroupBy(p=>(p.Key>>shift)&31)){byte[] child=Node(group.ToDictionary(p=>p.Key,p=>p.Value),shift-5);Array.Copy(child,0,payload,1+group.Key*32,32);}return SHA256.HashData(payload);
        }
        using var root=new MemoryStream();using var output=new BinaryWriter(root);output.Write(length);output.Write(Node(values,20));return Convert.ToHexString(SHA256.HashData(root.ToArray())).ToLowerInvariant();
    }
    static HistoricalCellTree[] Trees(int count)=>Enumerable.Range(0,count).Select(i=>new HistoricalCellTree(73).With(i%73,i*7919).With((i+17)%73,~i)).ToArray();
    static string Literal(string type,IEnumerable<HistoricalCellTree> trees)
    {
        using var memory=new MemoryStream();using var writer=new BinaryWriter(memory);writer.Write((byte)1);writer.Write(type);
        foreach(var tree in trees){writer.Write((byte)1);writer.Write((byte)(tree==null?0:1));if(tree!=null){writer.Write("WorldGen.Core.HistoricalCellTree");writer.Write(tree.Fingerprint());}}
        writer.Write((byte)0);writer.Flush();return Convert.ToHexString(SHA256.HashData(memory.ToArray())).ToLowerInvariant();
    }
    sealed class Sequence:IEnumerable<HistoricalCellTree>
    {
        public Action Between;public HistoricalCellTree[] Values;
        public IEnumerator<HistoricalCellTree> GetEnumerator(){yield return Values[0];Between?.Invoke();for(int i=1;i<Values.Length;i++)yield return Values[i];}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    public static void Run(Action<bool,string> check)
    {
        const int maximum=33554432;var values=new Dictionary<int,int>();var sparse=new HistoricalCellTree(maximum);
        foreach(var entry in new[]{(0,0),(31,-7),(32,123),(1023,int.MaxValue),(1024,int.MinValue),(32767,91),(32768,-2),(1048576,17),(33554431,35),(32,-1),(0,-1),(1048576,-1)})
        {
            sparse=sparse.With(entry.Item1,entry.Item2);if(entry.Item2==-1)values.Remove(entry.Item1);else values[entry.Item1]=entry.Item2;
            check(sparse.Fingerprint()==IndependentDigest(maximum,values)&&sparse.Fingerprint(true)==IndependentDigest(maximum,values),"Sparse/dense radix boundaries, signed values and removed nodes retain independently assembled literal Merkle bytes.");
        }
        var dense=HistoricalCellTree.From(65536,i=>i%19==0?-1:i);string denseExpected=IndependentDigest(65536,Enumerable.Range(0,65536).Where(i=>i%19!=0).ToDictionary(i=>i,i=>i));
        var empty=new HistoricalCellTree(maximum);var single=empty.With(33554431,int.MinValue);check(ReferenceEquals(empty,empty.With(17,-1))&&ReferenceEquals(single,single.With(33554431,int.MinValue)),"Absent/default and same signed-value writes retain the original immutable tree reference.");
        check(single.With(33554431,-1).Fingerprint()==empty.Fingerprint()&&empty[33554431]==-1&&single[33554431]==int.MinValue,"Removing the final value still prunes all empty ancestors without changing either retained source.");
        var mixed=new object[]{dense,sparse,new HistoricalCellTree(73).With(17,-9),new HistoricalCellTree(0)};
        StateHash.Compute(mixed);check(dense.Fingerprint(true)==denseExpected&&sparse.Fingerprint(true)==IndependentDigest(maximum,values),"Sequential dense/sparse/empty trees cannot leak stale nonzero scratch slots into absent children.");
        var malformed=new HistoricalCellTree(73).With(17,29);var rootField=typeof(HistoricalCellTree).GetField("root",BindingFlags.NonPublic|BindingFlags.Instance)!;object badRoot=rootField.GetValue(malformed)!;badRoot.GetType().GetField("Children",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(badRoot,Array.CreateInstance(badRoot.GetType(),31));
        bool malformedRejected=false;try{StateHash.Compute(malformed);}catch(System.Runtime.Serialization.SerializationException){malformedRejected=true;}check(malformedRejected,"Malformed branch shape is still rejected rather than treated as missing sparse children.");
        var scopeType=typeof(StateHash).Assembly.GetType("WorldGen.Core.CanonicalTreeScratch")!;var current=scopeType.GetField("current",BindingFlags.NonPublic|BindingFlags.Static)!;
        check(current.GetValue(null)==null,"Ordinary execution starts without a retained tree-hash context.");
        foreach(int count in new[]{0,1,31,32,73,257})
        {
            string expected=Literal(typeof(HistoricalCellTree[]).FullName!,Trees(count));var fresh=Trees(count);
            check(StateHash.Compute(fresh)==expected,"Scoped sequential fingerprints retain independently emitted standalone canonical bytes.");
            check(StateHash.ComputeHistoricalReference(fresh)==expected,"Reference/public hashing retain identical array framing and tree digests.");
            check(current.GetValue(null)==null,"Completed root hashes dispose their secondary SHA context.");
        }
        object outerContext=null;bool nestedRestored=false,nestedFailureRestored=false;
        var sequence=new Sequence{Values=Trees(3)};sequence.Between=()=>
        {
            outerContext=current.GetValue(null);check(outerContext!=null,"A tree creates its scoped SHA/scratch lazily, independent of the root SHA.");
            check(StateHash.Compute(Trees(17))==Literal(typeof(HistoricalCellTree[]).FullName!,Trees(17)),"A reentrant root hash uses an independent correct tree context.");
            nestedRestored=ReferenceEquals(outerContext,current.GetValue(null));
            try{StateHash.Compute(new Sequence{Values=Trees(2),Between=()=>throw new InvalidOperationException("Synthetic nested failure")});}catch(InvalidOperationException){nestedFailureRestored=ReferenceEquals(outerContext,current.GetValue(null));}
        };
        check(StateHash.Compute(sequence)==Literal(typeof(Sequence).FullName!,Trees(3))&&nestedRestored&&nestedFailureRestored,"Nested success/failure restores the caller's SHA and partially reusable scratch before continuing.");
        check(current.GetValue(null)==null,"Nested scope restoration never leaves a world/context retained after the outer hash.");
        var disposedSha=(SHA256)outerContext.GetType().GetField("Sha",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(outerContext)!;bool disposed=false;try{disposedSha.Initialize();}catch(ObjectDisposedException){disposed=true;}check(disposed,"Root completion releases the independent native tree-hash handle.");
        using(var token=new CancellationTokenSource())
        {
            token.Cancel();bool cancelled=false;try{StateHash.Compute(Trees(73),token.Token);}catch(OperationCanceledException){cancelled=true;}
            check(cancelled&&current.GetValue(null)==null,"Cancellation disposes tree scratch and restores the ambient context.");
        }
        var parallel=new bool[32];Parallel.For(0,32,i=>{var trees=Trees(i+1);string expected=Literal(typeof(HistoricalCellTree[]).FullName!,Trees(i+1));parallel[i]=StateHash.Compute(trees)==expected&&current.GetValue(null)==null;});
        foreach(bool ok in parallel)check(ok,"Concurrent root hashes own separate lazy SHA contexts and buffers.");
        var standalone=Trees(1000);long before=GC.GetAllocatedBytesForCurrentThread();foreach(var tree in standalone)tree.Fingerprint();long own=GC.GetAllocatedBytesForCurrentThread()-before;
        var scoped=Trees(1000);before=GC.GetAllocatedBytesForCurrentThread();string hash=StateHash.Compute(scoped);long shared=GC.GetAllocatedBytesForCurrentThread()-before;
        check(shared<own*.6,"Scoped1000-tree root hashing allocates substantially less than standalone per-tree contexts, without skipping any node digest.");
        check(hash==Literal(typeof(HistoricalCellTree[]).FullName!,standalone),"Allocation savings preserve all1000 tree values and final canonical bytes.");
        check(new HistoricalCellTree(73).With(17,29).Fingerprint()==new HistoricalCellTree(73).With(17,29).Fingerprint()&&current.GetValue(null)==null,"Standalone fingerprints still own/dispose their own context and preserve their result.");
        Console.WriteLine("1000 trees: standalone fingerprints "+own+"B; scoped full canonical hash "+shared+"B.");
    }
    public static void SetBenchmark()
    {
        const int capacity=33554432,operations=65536;var edits=new (int Index,int Value)[operations];
        for(int i=0;i<operations;i++){int index=i%3==0?33554431-(i%1024):i%3==1?(i*7919)&32767:(i*104729)&(capacity-1);edits[i]=(index,i%5==0?-1:i%7==0?int.MinValue+i:i%11==0?0:i);}
        var values=new Dictionary<int,int>();var source=new HistoricalCellTree(capacity).With(17,-9);values[17]=-9;
        long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=System.Diagnostics.Stopwatch.StartNew();var tree=source;int noops=0,removals=0,signed=0;
        for(int i=0;i<edits.Length;i++)
        {
            var edit=edits[i];if(i%13==0)edit.Value=tree[edit.Index];if(edit.Value==-1)removals++;else if(edit.Value<0)signed++;
            var next=tree.With(edit.Index,edit.Value);if(ReferenceEquals(next,tree))noops++;tree=next;if(edit.Value==-1)values.Remove(edit.Index);else values[edit.Index]=edit.Value;
        }
        watch.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
        if(tree.Fingerprint()!=IndependentDigest(capacity,values)||source[17]!=-9||source[33554431]!=-1)throw new Exception("Mixed Set benchmark changed values or its immutable source.");
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{Operations=operations,Noops=noops,Removals=removals,SignedValues=signed,Milliseconds=watch.Elapsed.TotalMilliseconds,AllocatedBytes=allocated,Hash=tree.Fingerprint()}));
    }
}
