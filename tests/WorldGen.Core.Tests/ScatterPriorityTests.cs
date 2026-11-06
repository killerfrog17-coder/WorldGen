using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class ScatterPriorityTests
{
    public static void Run(string input)
    {
        var world=WorldFile.ReadFile(input);
        var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var scatter=new LocalScatter(terrain);var order=new ScatterPriority();
        var keys=new List<ScatterKey>();int checkedKeys=0;
        var positions=Enumerable.Range(0,64).Select(i=>world.Surface.Cells[i*world.Surface.Cells.Length/64].Position)
            .Concat(new[]{new V(1,1,1).Unit,new V(-1,1,1).Unit,new V(1,-1,1).Unit,new V(1,1,-1).Unit,new V(1,0,0),new V(0,0,-1)});
        foreach(var at in positions)
        {
            keys.Clear();keys.AddRange(scatter.Around(at,1800));keys.AddRange(scatter.Around(at,100,true));
            var literal=keys.ToArray();
            double Distance(ScatterKey key){V p=scatter.Position(key)-at;return p.X*p.X+p.Y*p.Y+p.Z*p.Z;}
            Array.Sort(literal,(a,b)=>{int d=Distance(a).CompareTo(Distance(b));return d!=0?d:a.CompareTo(b);});
            order.Sort(scatter,at,keys);
            if(!keys.SequenceEqual(literal))throw new Exception("Scatter request order changed");
            keys.Reverse();order.Sort(scatter,at,keys);
            if(!keys.SequenceEqual(literal))throw new Exception("Scatter ordering depends on prior enumeration");
            checkedKeys+=keys.Count;
        }
        order.Sort(scatter,new V(1,0,0),keys);long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<100;i++)order.Sort(scatter,new V(1,0,0),keys);
        long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
        if(bytes!=0)throw new Exception("Warm ordering allocates "+bytes+" bytes");
        Console.WriteLine($"PASS {checkedKeys} exact scatter priorities at 70 world/pole/cube-boundary positions, reversed input, zero warm allocation.");
    }
}
