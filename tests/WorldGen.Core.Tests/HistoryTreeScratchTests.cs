using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using WorldGen.Core;

public static class HistoryTreeScratchTests
{
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
}
