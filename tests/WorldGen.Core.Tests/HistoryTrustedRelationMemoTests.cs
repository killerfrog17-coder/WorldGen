using System.Reflection;
using System.Runtime.Serialization;
using WorldGen.Core;

public static class HistoryTrustedRelationMemoTests
{
    static readonly MethodInfo OwnedHash=typeof(StateHash).GetMethod("ComputeOwnedHistorical",BindingFlags.NonPublic|BindingFlags.Static)!;
    static readonly MethodInfo OwnedFork=typeof(HistoricalRelationCollection).GetMethod("ForkOwned",BindingFlags.NonPublic|BindingFlags.Instance)!;
    static readonly MethodInfo PeekMethod=typeof(HistoricalRelationCollection).GetMethod("Peek",BindingFlags.NonPublic|BindingFlags.Instance)!;
    static readonly FieldInfo MemoField=typeof(HistoricalRelation).GetField("OwnedCanonicalRecord",BindingFlags.NonPublic|BindingFlags.Instance)!;
    static string Owned(HistoricalRelationCollection rows,CancellationToken token=default)=>(string)OwnedHash.Invoke(null,new object[]{rows,new HistoryState(),token})!;
    static HistoricalRelationCollection Trusted(HistoricalRelationCollection rows)=>(HistoricalRelationCollection)OwnedFork.Invoke(rows,null)!;
    static HistoricalRelation Peek(HistoricalRelationCollection rows,int i)=>(HistoricalRelation)PeekMethod.Invoke(rows,new object[]{i})!;
    static byte[] Memo(HistoricalRelation row)=>(byte[])MemoField.GetValue(row)!;
    public static void Run(Action<bool,string> check)
    {
        var rows=new HistoricalRelationCollection(new[]{new HistoricalRelation{A=1,B=2,Trust=17},new HistoricalRelation{A=3,B=4,Trade=true}}).Copy();
        var held=rows[0];var heldOther=rows[1];string initial=Owned(rows);var trusted=Trusted(rows);
        check(!ReferenceEquals(Peek(rows,0),Peek(trusted,0))&&ReferenceEquals(Memo(Peek(rows,0)),Memo(Peek(trusted,0))),"Trusted fork clones exposed mutable values but retains their immutable exact byte array.");
        check(Owned(trusted)==initial&&Owned(trusted)==StateHash.ComputeHistoricalReference(trusted),"Trusted private reuse equals independent literal fields.");
        var memo=Memo(Peek(trusted,0));Peek(trusted,0);check(ReferenceEquals(memo,Memo(Peek(trusted,0))),"A read-only Peek leaves immutable memo bytes valid.");
        held.Trust=23;heldOther.Trade=false;
        check(Owned(trusted)==initial&&StateHash.Compute(rows)!=initial,"Source references retained before trusted fork cannot modify the copied private fields or memo bytes.");
        // A public source may contain stale private memo bytes after a retained
        // edit. Its ordinary Copy must discard them, not claim private trust.
        var publicCopy=rows.Copy();check(Memo(Peek(publicCopy,0))==null&&Owned(publicCopy)==StateHash.Compute(rows),"An edit made before public fork cannot smuggle stale bytes through public Copy.");
        var historyCopy=new HistoryState{Relations=rows}.Copy();check(Owned(historyCopy.Relations)==StateHash.Compute(rows),"HistoryState.Copy never selects the trusted-private relation path.");
        string publicFixed=Owned(publicCopy);held.Trust=29;check(Owned(publicCopy)==publicFixed,"A later retained-source edit cannot change a public fork's detached fields.");
        var changed=trusted[0];var readMemo=Memo(changed);check(readMemo!=null&&Owned(trusted)==initial&&ReferenceEquals(readMemo,Memo(changed)),"A mutable read without a changed scalar retains exactly verified private bytes.");
        changed.Trust=31;check(Owned(trusted)==StateHash.ComputeHistoricalReference(trusted)&&Owned(trusted)!=initial,"A genuine private mutation is encoded again with exactly current fields.");
        var after=Trusted(trusted);string afterHash=Owned(after);changed.Trust=37;
        check(Owned(after)==afterHash&&StateHash.Compute(trusted)!=afterHash,"References retained after a trusted branch hash cannot affect its next cloned branch.");
        var exposed=after[1];Owned(after);exposed.Trust=41;var untrusted=after.Copy();
        check(Owned(untrusted)==StateHash.ComputeHistoricalReference(after),"Public forking after memo refill still detects edits through an older retained reference.");
        using(var cancellation=new CancellationTokenSource())
        {
            var cancelled=Trusted(untrusted);cancelled[0].Trust=43;cancellation.Cancel();bool caught=false;
            try{Owned(cancelled,cancellation.Token);}catch(TargetInvocationException ex)when(ex.InnerException is OperationCanceledException){caught=true;}
            check(caught&&StateHash.Compute(untrusted)==StateHash.ComputeHistoricalReference(untrusted),"Cancelled owned hashing leaves the preceding fork's fields and integrity untouched.");
        }
        using(var memory=new MemoryStream())
        {
            var serializer=new DataContractSerializer(typeof(HistoricalRelationCollection));serializer.WriteObject(memory,untrusted);memory.Position=0;
            var cold=(HistoricalRelationCollection)serializer.ReadObject(memory)!;check(Memo(Peek(cold,0))==null&&Owned(cold)==StateHash.Compute(untrusted),"Nonserialized memo bytes are reconstructed exactly after a cold binary roundtrip.");
        }
        var stable=Trusted(untrusted);string fixedHash=Owned(stable);var publication=stable.Copy();var parallel=new bool[32];
        Parallel.For(0,32,i=>{var fork=publication.Copy();fork[0].Trust=i;parallel[i]=StateHash.Compute(fork)==StateHash.ComputeHistoricalReference(fork)&&Owned(stable)==fixedHash;});
        foreach(bool ok in parallel)check(ok,"Concurrent public edits/integrity checks cannot alter trusted private records or their immutable memo bytes.");
        var large=new HistoricalRelationCollection(Enumerable.Range(0,5000).Select(i=>new HistoricalRelation{A=i,B=i+1,Trust=i%99}));Owned(large);
        var retainedBytes=Trusted(large);long before=GC.GetAllocatedBytesForCurrentThread();Owned(retainedBytes);long retainedAllocated=GC.GetAllocatedBytesForCurrentThread()-before;
        var clearedBytes=large.Copy();before=GC.GetAllocatedBytesForCurrentThread();Owned(clearedBytes);long clearedAllocated=GC.GetAllocatedBytesForCurrentThread()-before;
        check(retainedAllocated<clearedAllocated/3,"A private fork avoids rebuilding thousands of unchanged record byte arrays while public forks remain conservative.");
        check(Owned(retainedBytes)==Owned(clearedBytes),"Allocation savings retain identical canonical output.");
        foreach(string field in new[]{"A","B","Trust","WarSince","TruceUntil","LastBattle","Trade","Alliance"})
        {
            var one=new HistoricalRelationCollection(new[]{new HistoricalRelation{A=2,B=4,Trust=19,WarSince=7,TruceUntil=11,LastBattle=13,Trade=true}}).Copy();
            var retained=one[0];string first=Owned(one);var firstMemo=Memo(retained);var info=typeof(HistoricalRelation).GetField(field)!;object value=info.GetValue(retained)!;
            info.SetValue(retained,info.FieldType==typeof(bool)?!(bool)value:(object)((int)value+3));
            string second=Owned(one);check(second!=first&&second==StateHash.ComputeHistoricalReference(one)&&!ReferenceEquals(firstMemo,Memo(retained)),"Retained-reference edits of "+field+" rebuild exact private bytes without needing another mutable getter.");
            var changedMemo=Memo(retained);check(Owned(one)==second&&ReferenceEquals(changedMemo,Memo(retained)),"Unchanged "+field+" snapshots reuse bytes after the real edit.");
            info.SetValue(retained,value);check(Owned(one)==first&&StateHash.Compute(one)==first,"Restoring "+field+" restores the original literal hash.");
        }
        Console.WriteLine("5000 exposed relations: trusted hash "+retainedAllocated+"B; public fork cold hash "+clearedAllocated+"B.");
    }
}
