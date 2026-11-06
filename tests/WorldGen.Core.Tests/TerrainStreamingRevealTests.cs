using WorldGen.Presentation;

static class TerrainStreamingRevealTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var fine=new TerrainTileKey(0,0,0,1);var coarse=new TerrainTileKey(0,0,3,1);
        var requested=new Dictionary<TerrainTileKey,int>{{fine,64}};
        var resident=new Dictionary<TerrainTileKey,int>{{coarse,8}};
        double x=fine.SizeM*.5,z=x,limit=58800;
        double readiness=new TerrainQualityFootprint(resident).NearestGap(requested,x,z,limit);
        Check(readiness==0,"Reproduce: newly requested close detail rejects the existing coarse surface");
        double first=TerrainStreamingReveal.Advance(TerrainStreamingReveal.InitialDistance,readiness,limit,1);
        Check(first==TerrainStreamingReveal.InitialDistance,"Coarse backdrop cannot reveal an unloaded initial horizon");
        double visible=16000;
        for(int frame=0;frame<120;frame++)visible=TerrainStreamingReveal.Advance(visible,readiness,limit,1d/60);
        Check(visible==16000,"A near-detail upgrade cannot retract the already visible horizon");
        Check(TerrainStreamingReveal.Advance(visible,100,limit,1)==visible,"A replacement forest plan cannot restart the reveal");
        Check(TerrainStreamingReveal.Advance(visible,8000,limit,1)==visible,"Coarse replacement chart preserves the previous reveal distance");
        resident[fine]=64;readiness=new TerrainQualityFootprint(resident).NearestGap(requested,x,z,limit);
        Check(readiness==limit,"Installed requested detail permits outward reveal again");
        visible=TerrainStreamingReveal.Advance(visible,readiness,limit,1d/60);
        Check(visible>16000&&visible<17000,"Returning readiness grows the border without an outward jump");
        Check(TerrainStreamingReveal.Advance(visible,limit,980,0)==980,"Explicit shorter view distance still takes effect immediately");
        Check(TerrainStreamingReveal.Advance(TerrainStreamingReveal.InitialDistance,0,limit,1)==8,"Teleport/new-area reset does not inherit an old horizon");
        foreach(int fps in new[]{15,30,60,144})
        {
            double distance=TerrainStreamingReveal.InitialDistance;
            for(int frame=0;frame<fps*12;frame++)
            {
                double ready=frame<fps?5000:frame<fps*2?0:frame<fps*4?18000:frame<fps*6?100:limit;
                double previous=distance;distance=TerrainStreamingReveal.Advance(distance,ready,limit,1d/fps);
                Check(distance>=previous&&distance<=limit,"Movement/replan readiness changes never reverse or exceed the border");
                Check(distance<=Math.Max(previous,ready),"Readiness still gates every newly revealed metre");
            }
            Check(distance==limit,"Steady completed loading reaches the configured limit");
        }
        Check(TerrainStreamingReveal.Advance(1000,limit,limit,0)==1000,"Paused time cannot expand the border");
        foreach(double scale in new[]{.1,1d})
        {
            var surface=TerrainLod.Plan(0,0,6000,scale:scale).Where((p,i)=>i%5!=0).ToDictionary(p=>p.Key,p=>p.Value);
            var cached=new TerrainQualityFootprint(surface);var mutable=new Dictionary<TerrainTileKey,int>();
            for(int step=0;step<80;step++)
            {
                mutable.Clear();foreach(var pair in TerrainLod.Plan(step*41-1600,Math.Sin(step)*700,6000,scale:scale))mutable.Add(pair.Key,step%3==0?32:pair.Value);
                double px=step*37-1000,pz=Math.Cos(step)*900,expected=limit;
                foreach(var group in mutable.GroupBy(p=>p.Key.SizeM/p.Value))
                {
                    double spacing=group.Key;
                    var tier=new TerrainCoverageFootprint(surface.Where(p=>p.Value>0&&p.Key.SizeM/p.Value<=spacing*(1+1e-9)).Select(p=>p.Key));
                    expected=Math.Min(expected,tier.NearestGap(group.Select(p=>p.Key),px,pz,expected));
                }
                Check(BitConverter.DoubleToInt64Bits(cached.NearestGap(mutable,px,pz,limit))==BitConverter.DoubleToInt64Bits(expected),"Reused fog groups preserve literal mixed-LOD distances when the requested dictionary changes");
            }
            cached.NearestGap(mutable,0,0,limit);long before=GC.GetAllocatedBytesForCurrentThread();
            for(int step=0;step<1000;step++)cached.NearestGap(mutable,step,0,limit);
            Check(GC.GetAllocatedBytesForCurrentThread()-before<256000,"Steady fog checks do not reconstruct temporary groups or recursive closures");
        }
        // Exercise invalidation as the resident terrain itself changes, including
        // removal of the highest ancestor, resolution changes and empty regions.
        var reused=new TerrainQualityFootprint(Array.Empty<KeyValuePair<TerrainTileKey,int>>());
        var cases=new List<Dictionary<TerrainTileKey,int>>();
        for(int step=0;step<12;step++)
        {
            var data=TerrainLod.Plan(step*130-700,step*71-400,6000,scale:step%2==0?.1:1)
                .Where((p,i)=>i%4!=step%4).ToDictionary(p=>p.Key,p=>step%3==0?32:p.Value);
            if(step==5)data.Clear();cases.Add(data);
        }
        for(int step=0;step<120;step++)
        {
            var data=cases[step%cases.Count];reused.Reset(data);
            Check(reused.Matches(data),"Reset retains exactly the current resident keys and resolutions");
            var fresh=new TerrainQualityFootprint(data);
            for(int probe=0;probe<5;probe++)
            {
                var plan=cases[(step+probe+3)%cases.Count];double px=step*31-900,pz=probe*117-400;
                Check(BitConverter.DoubleToInt64Bits(reused.NearestGap(plan,px,pz,limit))==BitConverter.DoubleToInt64Bits(fresh.NearestGap(plan,px,pz,limit)),"Reused coverage sets preserve exact gaps across changing residents, scale, ancestor levels and empty regions");
            }
        }
        var fixedResident=cases[0];var fixedRequest=cases[2];
        for(int i=0;i<10;i++){reused.Reset(fixedResident);reused.NearestGap(fixedRequest,0,0,limit);}
        long allocationStart=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<100;i++)new TerrainQualityFootprint(fixedResident).NearestGap(fixedRequest,i,0,limit);
        long freshBytes=GC.GetAllocatedBytesForCurrentThread()-allocationStart;
        allocationStart=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<100;i++){reused.Reset(fixedResident);reused.NearestGap(fixedRequest,i,0,limit);}
        long reusedBytes=GC.GetAllocatedBytesForCurrentThread()-allocationStart;
        Check(reusedBytes<freshBytes/10,"Resident updates reuse coverage storage instead of rebuilding dictionaries and sets");
        Console.WriteLine($"Resident coverage allocation: fresh {freshBytes} bytes, reused {reusedBytes} bytes / 100 rebuilds.");
        Console.WriteLine("PASS "+checks+" pure streaming-fog checks; no world or player launched.");
    }
}
