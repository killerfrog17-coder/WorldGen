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
        long beforeBytes=0,afterBytes=0,reusedBytes=0;var workspace=new TerrainEdges.Workspace();
        foreach(double scale in new[]{.01,.1,.3,1d})foreach(double altitude in new[]{0d,3000d})foreach(double x in new[]{-1721d,97d})
        {
            var plan=TerrainLod.Plan(x,-55,altitude,200000,scale);
            start=GC.GetAllocatedBytesForCurrentThread();var expected=TerrainEdgesReference.Build(plan);beforeBytes+=GC.GetAllocatedBytesForCurrentThread()-start;
            start=GC.GetAllocatedBytesForCurrentThread();var actual=TerrainEdges.Build(plan);afterBytes+=GC.GetAllocatedBytesForCurrentThread()-start;
            workspace.Build(plan);start=GC.GetAllocatedBytesForCurrentThread();var reused=workspace.Build(plan);reusedBytes+=GC.GetAllocatedBytesForCurrentThread()-start;
            foreach(var p in expected){Check(actual[p.Key].SequenceEqual(p.Value),"Every packed boundary preserves literal original output");Check(reused[p.Key].SequenceEqual(p.Value),"Reused neighbour lists preserve every packed boundary");}
            var saved=reused.ToDictionary(p=>p.Key,p=>(double[])p.Value.Clone());
            workspace.Build(new Dictionary<TerrainTileKey,int>());workspace.Build(TerrainLod.Plan(x+650,777,2000,6000,.3));
            foreach(var p in saved)Check(reused[p.Key].SequenceEqual(p.Value),"Changing and clearing the workspace never mutates a prior published plan");
            var retained=actual.First();var copied=(double[])retained.Value.Clone();TerrainEdges.Build(plan);
            Check(retained.Value.SequenceEqual(copied),"Returned boundaries do not alias scratch storage");
        }
        Check(afterBytes<beforeBytes*.75,"Packing eliminates most temporary allocations without changing boundary grids");
        var giant=new Dictionary<TerrainTileKey,int>();for(int x=-48;x<48;x++)for(int z=-32;z<32;z++)giant.Add(new TerrainTileKey(x,z,0,.3),(x+z)%2==0?16:32);
        var giantExpected=TerrainEdgesReference.Build(giant);var giantActual=workspace.Build(giant);
        foreach(var p in giantExpected)Check(giantActual[p.Key].SequenceEqual(p.Value),"Oversized plans use disposable scratch with unchanged boundaries");
        Check(workspace.Build(new Dictionary<TerrainTileKey,int>()).Count==0,"Oversized fallback does not contaminate a later empty plan");
        Check(reusedBytes<afterBytes*.35,"Reused neighbour workspace removes most remaining scratch allocations");
        var tasks=Enumerable.Range(0,8).Select(worker=>Task.Run(()=>
        {
            var own=new TerrainEdges.Workspace();var plan=TerrainLod.Plan(worker*130-400,-370,worker*300,12000,.1);
            var reference=TerrainEdgesReference.Build(plan);for(int iteration=0;iteration<10;iteration++){var result=own.Build(plan);foreach(var pair in reference)if(!result[pair.Key].SequenceEqual(pair.Value))return false;}return true;
        })).ToArray();Task.WaitAll(tasks);Check(tasks.All(t=>t.Result),"Independent concurrent workspaces never share mutable scratch");
        Console.WriteLine($"PASS {checks} exact cache/terrain-edge checks; habitat {bytes}B; edge allocation {beforeBytes} -> {afterBytes} -> reused {reusedBytes}B");
    }
}
