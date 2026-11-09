using System;
using System.Linq;
using WorldGen.Core;

static class NpcCampSiteIndexTests
{
    public static void Run()
    {
        var random=new Random(7141);int n=18000,checks=0;var x=new double[n];var z=new double[n];var radius=new double[n];
        for(int i=0;i<n;i++){x[i]=random.NextDouble()*2400-1200;z[i]=random.NextDouble()*2400-1200;radius[i]=random.NextDouble()*3+.08;}
        // Exact grid boundaries and both signs are important for broad phases.
        for(int i=0;i<20;i++){x[i]=(i-10)*32;z[i]=(10-i)*32;}
        radius[0]=60;var index=new NpcCampSiteIndex(x,z);double largest=radius.Max();var found=new bool[n];long visited=0;
        for(int sample=0;sample<1000;sample++)
        {
            double cx=sample<20?x[sample]:random.NextDouble()*2600-1300,cz=sample<20?z[sample]:random.NextDouble()*2600-1300;
            double footprint=sample%3==0?0:sample%3==1?16:44;Array.Clear(found,0,n);var query=index.Around(cx,cz,footprint+largest);
            while(query.MoveNext()){int i=query.Current;if(found[i])throw new Exception("A camp obstacle was visited twice");found[i]=true;visited++;}
            for(int i=0;i<n;i++)if((x[i]-cx)*(x[i]-cx)+(z[i]-cz)*(z[i]-cz)<=(footprint+radius[i])*(footprint+radius[i])&&!found[i])throw new Exception("Spatial camp broad phase omitted a possible obstacle");
            checks++;
        }
        if(visited>=n*1000L/20)throw new Exception("Local camp queries still enumerate a substantial fraction of the whole survey");checks++;
        for(int j=0;j<100;j++){var q=index.Around(-32,32,47);while(q.MoveNext())_ = q.Current;}
        long start=GC.GetAllocatedBytesForCurrentThread();int total=0;
        for(int j=0;j<20000;j++){var q=index.Around(-32,32,47);while(q.MoveNext())total+=q.Current;}
        long allocated=GC.GetAllocatedBytesForCurrentThread()-start;
        if(allocated!=0)throw new Exception("Warm camp index lookups allocated "+allocated+" bytes");checks++;
        Console.WriteLine("NPC camp spatial index PASS: "+checks+"; "+visited+" visits versus "+(n*1000L)+" full-scan visits; 20000 warm queries "+allocated+" bytes; checksum "+total);
    }
}
