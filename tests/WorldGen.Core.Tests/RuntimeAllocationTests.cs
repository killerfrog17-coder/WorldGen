using WorldGen.Core;
using WorldGen.Presentation;
static class RuntimeAllocationTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var w=new World();var s=new PrehistoryState{Foragers=new ForagerState()};w.Prehistory=s;
        var cached=ForagerHabitat.For(w);for(int i=0;i<1000;i++)ForagerHabitat.For(w);
        long start=GC.GetAllocatedBytesForCurrentThread();bool same=true;
        for(int i=0;i<10000;i++)same&=ReferenceEquals(cached,ForagerHabitat.For(w));
        long bytes=GC.GetAllocatedBytesForCurrentThread()-start;
        Check(same&&bytes==0,"Warm habitat lookups preserve identity without allocation");
        Check(ForagerHabitat.For(w,new PrehistoryState())==null,"Absent foragers retain null habitat");
        var another=new PrehistoryState{Foragers=new ForagerState()};var concurrent=new ForagerHabitat[64];
        Parallel.For(0,64,i=>concurrent[i]=ForagerHabitat.For(w,another));
        Check(concurrent.All(h=>ReferenceEquals(h,concurrent[0]))&&!ReferenceEquals(cached,concurrent[0]),"Concurrent cache misses keep one canonical habitat per state");
        long beforeBytes=0,afterBytes=0;
        foreach(double scale in new[]{.01,.1,.3,1d})foreach(double altitude in new[]{0d,3000d})foreach(double x in new[]{-1721d,97d})
        {
            var plan=TerrainLod.Plan(x,-55,altitude,200000,scale);
            start=GC.GetAllocatedBytesForCurrentThread();var expected=TerrainEdgesReference.Build(plan);beforeBytes+=GC.GetAllocatedBytesForCurrentThread()-start;
            start=GC.GetAllocatedBytesForCurrentThread();var actual=TerrainEdges.Build(plan);afterBytes+=GC.GetAllocatedBytesForCurrentThread()-start;
            foreach(var p in expected)Check(actual[p.Key].SequenceEqual(p.Value),"Every packed boundary preserves literal original output");
            var retained=actual.First();var copied=(double[])retained.Value.Clone();TerrainEdges.Build(plan);
            Check(retained.Value.SequenceEqual(copied),"Returned boundaries do not alias scratch storage");
        }
        Check(afterBytes<beforeBytes*.75,"Packing eliminates most temporary allocations without changing boundary grids");
        Console.WriteLine($"PASS {checks} exact cache/terrain-edge checks; habitat {bytes}B; edge allocation {beforeBytes} -> {afterBytes}B");
    }
}
