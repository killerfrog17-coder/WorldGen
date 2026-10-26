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
        Console.WriteLine("PASS "+checks+" pure streaming-fog checks; no world or player launched.");
    }
}
