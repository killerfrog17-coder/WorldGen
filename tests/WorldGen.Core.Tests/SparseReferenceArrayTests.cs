using WorldGen.Core;
using WorldGen.Serialization;

static class SparseReferenceArrayTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        foreach(int length in new[]{0,1,255,256,257,513,1000003})
        {
            var flat=new SparseReferenceArray<object>(length,false);var paged=new SparseReferenceArray<object>(length);
            Check(paged.Length==length,"Exact public length");
            foreach(int invalid in new[]{-1,length})
            {
                try{var value=paged[invalid];throw new Exception("Out-of-range read accepted");}catch(IndexOutOfRangeException){checks++;}
                try{paged[invalid]=null;throw new Exception("Out-of-range write accepted");}catch(IndexOutOfRangeException){checks++;}
            }
            if(length==0)continue;
            int initial=paged.AllocatedSlots;var random=new Random(193);
            for(int i=0;i<5000;i++)
            {
                int index=random.Next(length);object value=i%3==0?null:new object();
                paged[index]=flat[index]=value;Check(ReferenceEquals(flat[index],paged[index]),"Assignment/clear identity");
                int other=random.Next(length);Check(ReferenceEquals(flat[other],paged[other]),"Unrelated slot preserved");
            }
            for(int i=0;i<length;i++)Check(ReferenceEquals(flat[i],paged[i]),"Full array identity including partial final page");
            Check(paged.AllocatedSlots<=length+initial,"Reference page storage remains bounded");
        }
        var sparse=new SparseReferenceArray<object>(1000003);var token=new object();
        for(int i=12300;i<12400;i++)sparse[i]=token;
        Check(sparse.AllocatedSlots<5000,"Small visited area does not allocate a planetary reference array");
        Parallel.For(0,16,_=>{for(int i=12300;i<12400;i++)if(!ReferenceEquals(sparse[i],token))throw new Exception("Concurrent reader changed reference");});
        long start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<100000;i++){if(sparse[12345]!=token)throw new Exception();}
        Check(GC.GetAllocatedBytesForCurrentThread()==start,"Warm reads allocate no managed memory");
        Console.WriteLine($"PASS {checks} sparse reference slot checks; sparse million-slot index uses {sparse.AllocatedSlots} allocated references.");
    }
    public static void World(string input)
    {
        var world=WorldFile.ReadFile(input);bool old=HyperAmplification.PagedRootCaches;
        try
        {
            HyperAmplification.PagedRootCaches=false;var flat=new LocalTerrain(world,horizontalScale:.1);
            HyperAmplification.PagedRootCaches=true;var paged=new LocalTerrain(world,horizontalScale:.1);
            Console.WriteLine($"After preparation: flat handles/live {flat.Amplification.RootCacheHandles()}, paged {paged.Amplification.RootCacheHandles()}.");
            var selected=Enumerable.Range(0,world.Surface.Cells.Length).GroupBy(i=>flat.Profile(i).Biome).Select(g=>g.First()).Concat(new[]{108692,145551,130822}).Distinct();
            int checks=0,wet=0;
            foreach(int id in selected)
            {
                var point=world.Surface.Cells[id].Position;var a=flat.Patch(point,20480);var b=paged.Patch(point,20480);
                var expectedSpans=a.RiverSpans();var actualSpans=b.RiverSpans();
                if(!expectedSpans.SequenceEqual(actualSpans))throw new Exception("Paged root cache changed a river span");
                foreach(double x in new[]{-17000d,-8000,0,8000,17000})foreach(double z in new[]{-17000d,-8000,0,8000,17000})
                {
                    var at=a.Position(x,z);var expected=a.Sample(at);var actual=b.Sample(at);
                    if(!expected.Equals(actual))throw new Exception("Paged root cache changed complete terrain sample");
                    checks++;if(actual.Underwater)wet++;
                }
            }
            if(wet==0)throw new Exception("No water coverage");
            Console.WriteLine($"PASS {checks} complete actual-world samples and all local river spans, {wet} wet probes; flat slots {flat.Amplification.RootCacheReferenceSlots}, paged slots {paged.Amplification.RootCacheReferenceSlots}. Slot count is not an FPS result.");
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            Console.WriteLine($"After explicit test-only collection: flat handles/live {flat.Amplification.RootCacheHandles()}, paged {paged.Amplification.RootCacheHandles()}.");
        }
        finally{HyperAmplification.PagedRootCaches=old;}
    }
}
