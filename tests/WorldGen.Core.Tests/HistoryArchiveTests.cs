using System.Reflection;
using System.Runtime.Serialization;
using WorldGen.Core;
using WorldGen.Serialization;
public static class HistoryArchiveTests
{
    static HistoricalEventArchive Fork(HistoricalEventArchive archive,bool publicRecords)=>(HistoricalEventArchive)typeof(HistoricalEventArchive).GetMethod("Fork",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(archive,new object[]{publicRecords})!;
    public static void Run(Action<bool,string> check)
    {
        var source=new HistoricalEventArchive();var first=new HistoricalEvent{Id=0,Year=17,Text="Original"};source.Add(first);
        var branch=Fork(source,true);first.Text="Source changed";
        check(branch[0].Text=="Original","A retained external record cannot alter an already published archive");
        branch[0].Text="Branch changed";
        check(source[0].Text=="Source changed"&&branch[0].Text=="Branch changed","Public record edits are local, persistent edits, not discarded temporary copies");
        var next=Fork(branch,true);branch[0].Text="Later edit";
        check(next[0].Text=="Branch changed","Forking freezes materialized mutable records before sharing pages");
        for(int i=1;i<600;i++)source.Add(new HistoricalEvent{Id=i,Year=i+17,Text="Event "+i});
        var left=Fork(source,true);source.Add(new HistoricalEvent{Id=600,Text="Appended"});
        check(left.Count==600&&source.Count==601&&left[599].Text=="Event 599","Appending to a shared partial page preserves the old publication");
        var privateBranch=Fork(source,false);var publicBranch=Fork(privateBranch,true);privateBranch.Add(new HistoricalEvent{Id=601,Text="Private append"});
        check(publicBranch.Count==601&&privateBranch.Count==602,"Private appends never extend a public branch");
        publicBranch[1].Text="Public edit";
        check(privateBranch[1].Text=="Event 1","A public getter never leaks the mutable private event object");
        var assigned=new HistoricalEvent{Id=1,Text="Assigned"};publicBranch[1]=assigned;var assignedCopy=Fork(publicBranch,true);assigned.Text="Edited after fork";
        check(assignedCopy[1].Text=="Assigned","Explicit index assignments are also frozen across branches");
        check(StateHash.Compute(source)==StateHash.Compute(source.ToList()),"Paged and historical List archives have identical canonical bytes");
        check(WorldJson.Write(source)==WorldJson.Write(source.ToList()),"The archive JSON wire format remains an ordinary historical array");
        using var oldBytes=new MemoryStream();new DataContractSerializer(typeof(List<HistoricalEvent>)).WriteObject(oldBytes,source.ToList());oldBytes.Position=0;
        var restored=(HistoricalEventArchive)new DataContractSerializer(typeof(HistoricalEventArchive)).ReadObject(oldBytes)!;
        check(restored.Count==source.Count&&StateHash.Compute(restored)==StateHash.Compute(source),"The new archive reads the existing binary data contract without changing its hash");
        using var newBytes=new MemoryStream();new DataContractSerializer(typeof(HistoricalEventArchive)).WriteObject(newBytes,restored);newBytes.Position=0;
        var oldReader=(List<HistoricalEvent>)new DataContractSerializer(typeof(List<HistoricalEvent>)).ReadObject(newBytes)!;
        check(StateHash.Compute(oldReader)==StateHash.Compute(source),"An old List reader can read the new archive wire representation");
        restored.Insert(1,new HistoricalEvent{Id=999,Text="Inserted"});check(restored[1].Text=="Inserted"&&restored.Count==source.Count+1,"Explicit insertion retains collection semantics");
        restored.RemoveAt(1);check(StateHash.Compute(restored)==StateHash.Compute(source),"Explicit deletion restores the original event sequence");
        restored.Clear();check(restored.Count==0&&source.Count==601,"Clearing a branch cannot clear a shared prefix");
        var large=new HistoricalEventArchive();for(int i=0;i<100000;i++)large.Add(new HistoricalEvent{Id=i,Text="Lifetime record"});
        var owned=Fork(large,false);Fork(owned,true);long allocated=GC.GetAllocatedBytesForCurrentThread();var published=Fork(owned,true);allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
        check(allocated<32768&&published.Count==100000,"Publishing 100k lifetime records allocates page references, not 100k event copies ("+allocated+" bytes)");
        published[99999].Text="Late public edit";check(owned[99999].Text=="Lifetime record","The allocation saving does not sacrifice late-record isolation");
        var readers=new HistoricalEvent[64];Parallel.For(0,readers.Length,i=>readers[i]=published[50000]);check(readers.All(e=>ReferenceEquals(e,readers[0])),"Concurrent public readers safely materialize one local record");
        using var iterator=published.GetEnumerator();published.Add(new HistoricalEvent{Id=100000});bool modified=false;try{iterator.MoveNext();}catch(InvalidOperationException){modified=true;}check(modified,"Structural edits retain the List contract's enumeration guard");
    }
}
