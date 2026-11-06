using System.Runtime.CompilerServices;
using WorldGen.Core;

static class PagedReferenceCacheTests
{
    sealed class Value { public readonly int Id;public Value(int id){Id=id;} }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference<Value> Expiring(PagedReferenceCache<WeakReference<Value>> cache)
    {
        var value=new Value(7);var weak=new WeakReference<Value>(value);cache[12345]=weak;return weak;
    }
    public static void Run()
    {
        const int length=1000003;
        var cache=new PagedReferenceCache<Value>(length);
        int emptySlots=cache.AllocatedReferenceSlots;
        if(emptySlots>length/60)throw new Exception("Unvisited cache allocated value pages");
        for(int i=0;i<length;i++)if(cache[i]!=null)throw new Exception("Unvisited value is not null");
        cache[55]=null!;
        if(cache.AllocatedReferenceSlots!=emptySlots)throw new Exception("Clearing an untouched page allocated it");
        var reference=new Dictionary<int,Value>();
        foreach(int id in new[]{0,1,63,64,65,127,128,700007,length-1})
        {var value=new Value(id);reference.Add(id,value);cache[id]=value;}
        for(int i=0;i<length;i++)if(!ReferenceEquals(cache[i],reference.GetValueOrDefault(i)))throw new Exception("Sparse index differs from dense logical array");
        cache[64]=null!;cache[65]=new Value(65);
        if(cache[64]!=null||cache[63].Id!=63||cache[65].Id!=65)throw new Exception("Page boundary replacement damaged a neighbour");
        foreach(int index in new[]{-1,length,int.MaxValue})
        {
            try{_ =cache[index];throw new Exception("Out of range read accepted");}catch(IndexOutOfRangeException){}
            try{cache[index]=new Value(0);throw new Exception("Out of range write accepted");}catch(IndexOutOfRangeException){}
        }
        var parallel=new PagedReferenceCache<Value>(65537);
        Parallel.For(0,parallel.Length,new ParallelOptions{MaxDegreeOfParallelism=4},i=>parallel[i]=new Value(i));
        Parallel.For(0,parallel.Length,new ParallelOptions{MaxDegreeOfParallelism=4},i=>
        {if(parallel[i].Id!=i)throw new Exception("Concurrent page publication lost a value");});
        if(parallel.AllocatedReferenceSlots!=parallel.Length+(parallel.Length+63)/64)throw new Exception("Page publication counted losing races");
        var weakCache=new PagedReferenceCache<WeakReference<Value>>(length);var weak=Expiring(weakCache);
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        if(weak.TryGetTarget(out _)||!ReferenceEquals(weak,weakCache[12345]))throw new Exception("Sparse cache changed weak target lifetime");
        long before=GC.GetAllocatedBytesForCurrentThread();int total=0;
        for(int n=0;n<100000;n++)total+=cache[n%129]?.Id??0;
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        if(allocated>1024||total==0)throw new Exception("Warm reference lookups allocate");
        Console.WriteLine($"PASS sparse reference cache: million-address equivalence, boundaries, 65537 concurrent publications, weak expiry, {allocated} warm lookup bytes; sparse slots {cache.AllocatedReferenceSlots}/{length}.");
    }
}
