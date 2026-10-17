#nullable enable
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using WorldGen.Core;

public static class HistorySmallCanonicalTests
{
    static byte[] ReferenceBytes(object? value)
    {
        using var memory=new MemoryStream();using var writer=new BinaryWriter(memory,Encoding.UTF8,true);
        void Write(object? item)
        {
            if(item==null){writer.Write((byte)0);return;}writer.Write((byte)1);var type=item.GetType();writer.Write(type.FullName!);
            if(item is string text){writer.Write(text);return;}if(item is int number){writer.Write(number);return;}if(type.IsEnum){writer.Write(Convert.ToInt32(item));return;}
            if(item is IEnumerable list){foreach(var entry in list){writer.Write((byte)1);Write(entry);}writer.Write((byte)0);return;}
            foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal))
            {
                if(item is HistoricalLeader leader&&(field.Name==nameof(HistoricalLeader.PredecessorId)&&leader.PredecessorId==-1||field.Name==nameof(HistoricalLeader.Selection)&&string.IsNullOrEmpty(leader.Selection)))continue;
                writer.Write(field.Name);Write(field.GetValue(item));
            }
        }
        Write(value);writer.Flush();return memory.ToArray();
    }
    static byte[] ActualBytes(object value)
    {
        using var memory=new MemoryStream();using var writer=new BinaryWriter(memory,Encoding.UTF8,true);
        typeof(StateHash).GetMethod("Write",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{writer,value});writer.Flush();return memory.ToArray();
    }
    static void Matches(object value,Action<bool,string> check)
    {
        byte[] reference=ReferenceBytes(value);check(ActualBytes(value).SequenceEqual(reference),"Typed single/array records preserve independently written literal canonical bytes.");
        string expected=Convert.ToHexString(SHA256.HashData(reference)).ToLowerInvariant();
        check(StateHash.Compute(value)==expected&&StateHash.ComputeHistoricalReference(value)==expected,"Small-buffer hashing and the original streaming/reference path produce exactly the same SHA256.");
    }
    public static void Run(Action<bool,string> check)
    {
        var leader=new HistoricalLeader();Matches(leader,check);int index=0;
        foreach(var field in typeof(HistoricalLeader).GetFields().OrderBy(f=>f.Name,StringComparer.Ordinal))
        {field.SetValue(leader,field.FieldType==typeof(int)?(object)(index%2==0?int.MinValue+index:int.MaxValue-index):"Live \" testimony / é水\n"+index);index++;Matches(leader,check);}
        foreach(string? selection in new[]{null,"","Elected é水"})foreach(int predecessor in new[]{-1,0,int.MinValue,int.MaxValue}){leader.Selection=selection!;leader.PredecessorId=predecessor;Matches(leader,check);}
        Matches(new HistoricalLeader[]{leader,null!,new HistoricalLeader()},check);Matches(Array.Empty<HistoricalLeader>(),check);
        string before=StateHash.Compute(leader);leader.Renown++;check(StateHash.Compute(leader)!=before,"A previously hashed single leader is read live after public mutation.");
        foreach(ChronicleCategory category in Enum.GetValues<ChronicleCategory>())
        {
            var e=new HistoricalEvent{Category=category,Cell=int.MinValue,Id=int.MaxValue,Text="A dated event: é水\n",Kind="event"};Matches(e,check);Matches(new object[]{"0123456789abcdef",e},check);
        }
        foreach(int length in new[]{4096,4097,65536}){leader.Legacy=new string('水',length);Matches(leader,check);Matches(new object[]{"hash",new HistoricalEvent{Text=new string('水',length)}},check);}
        var concurrent=new bool[32];Parallel.For(0,concurrent.Length,k=>{var live=new HistoricalLeader{Id=k,Name="Parallel"+k,Renown=k*7919,Selection=k%2==0?"":null!};concurrent[k]=ActualBytes(live).SequenceEqual(ReferenceBytes(live))&&StateHash.Compute(live)==StateHash.ComputeHistoricalReference(live);});
        foreach(bool correct in concurrent)check(correct,"Concurrent single-record writers own their mutable scratch and cryptographic state.");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();bool cancelled=false;try{StateHash.Compute(new HistoricalLeader(),cancellation.Token);}catch(OperationCanceledException){cancelled=true;}
        check(cancelled,"The known-small hashing path honors pre-cancelled work.");
        var example=new HistoricalLeader{Id=42,Name="Allocation fixture",Selection="A recorded succession"};StateHash.Compute(example);StateHash.ComputeHistoricalReference(example);
        const int count=1000;var timer=Stopwatch.StartNew();long start=GC.GetAllocatedBytesForCurrentThread();for(int k=0;k<count;k++)StateHash.ComputeHistoricalReference(example);long referenceBytes=GC.GetAllocatedBytesForCurrentThread()-start;double referenceMs=timer.Elapsed.TotalMilliseconds;
        timer.Restart();start=GC.GetAllocatedBytesForCurrentThread();for(int k=0;k<count;k++)StateHash.Compute(example);long typedBytes=GC.GetAllocatedBytesForCurrentThread()-start;
        check(typedBytes<referenceBytes/4,"Small live leader hashes allocate less than one quarter of the original streaming/reflection path.");
        Console.WriteLine($"Leader hash1000: reference {referenceMs:F2}ms/{referenceBytes}B, typed {timer.Elapsed.TotalMilliseconds:F2}ms/{typedBytes}B.");
    }
}
