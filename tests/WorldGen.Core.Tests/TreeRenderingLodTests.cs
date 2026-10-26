using WorldGen.Presentation;

static class TreeRenderingLodTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        Check(TreeRenderingLod.ShadowWeight(0)==0&&TreeRenderingLod.ShadowWeight(64)==0,"Close shadows retain proxies");
        Check(TreeRenderingLod.ShadowWeight(96)==1&&TreeRenderingLod.ShadowWeight(1000)==1,"Distant shadows finish card handoff");
        double prior=0;
        for(int step=0;step<=640;step++)
        {
            double weight=TreeRenderingLod.ShadowWeight(48+step*.1);
            Check(weight>=prior&&weight>=0&&weight<=1,"Shadow transition is bounded and monotonic");prior=weight;
        }
        for(int quality=0;quality<5;quality++)
        {
            double start=TreeRenderingLod.ViewStart(quality),pixels=TreeRenderingLod.PixelStart(quality);
            Check(start>=80&&start*1.5<=330,"Detailed views have a bounded retained distance");
            Check(pixels>=5&&pixels<=12,"One-view rendering is limited to small projected crowns");
            if(quality>0)Check(start>TreeRenderingLod.ViewStart(quality-1)&&pixels<TreeRenderingLod.PixelStart(quality-1),"Higher quality keeps detailed views farther and for smaller crowns");
        }
        Check(TreeRenderingLod.ViewStart(-2)==TreeRenderingLod.ViewStart(0)&&TreeRenderingLod.ViewStart(10)==TreeRenderingLod.ViewStart(4),"Quality input is clamped");
        Console.WriteLine("PASS "+checks+" pure tree rendering LOD checks; no world or player launched.");
    }
}
