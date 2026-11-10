using WorldGen.Core;

static class RiverHydraulicsTests
{
    public static void Run()
    {
        int count=0;void Check(bool ok,string reason){count++;if(!ok)throw new Exception(reason);}
        foreach(double scale in new[]{.01,.1,1,2})
        {
            double prior=0;
            foreach(double q in new[]{0,.01,.1,1,10,100,500,1000,5000,10000,100000,209000,1e9})
            {
                double w=RiverHydraulics.HalfWidth(q,scale),d=RiverHydraulics.Depth(q,scale,w),flow=RiverHydraulics.Discharge(q,scale);
                Check(double.IsFinite(w)&&w>=.6&&w<=2000&&w>=prior,"Width stays finite, bounded and monotonic");prior=w;
                Check(double.IsFinite(d)&&d>=0&&d<=55&&d<=w*.75,"Depth stays finite and fits its cross-section");
                Check(w==RiverHydraulics.HalfWidth(q,1)&&d==RiverHydraulics.Depth(q,1,w)&&flow==Math.Max(0,q),"Source-globe dimensions and discharge do not shrink with map scale");
                var section=new RiverSection(0,w,d,flow,.0001,.5);Check(section.Speed>=.15&&section.Speed<=7,"Current remains a plausible bounded movement rate");
            }
        }
        double miss=RiverHydraulics.HalfWidth(16792,.1),amazon=RiverHydraulics.HalfWidth(209000,.1);
        Check(miss*2>500&&miss*2<1500,"Mississippi-scale discharge permits a broad main channel");
        Check(amazon*2>2000&&amazon*2<4000,"Amazon-scale discharge permits kilometre-scale width");
        Check(RiverHydraulics.Depth(209000,.1,amazon)>20,"A giant channel is genuinely deep, not an ankle-deep sheet");
        Check(RiverHydraulics.HalfWidth(.1,.1)*2<3,"A small source-globe stream remains small");
        Check(RiverHydraulics.HalfWidth(10,.1)*2>12&&RiverHydraulics.HalfWidth(10,.1)*2<25,"A ten-cubic-metre river retains metre-scale banks instead of becoming a trickle");
        Check(RiverHydraulics.MinimumLocalDischarge>.02,"Channel admission is tightened separately from width");
        Console.WriteLine("PASS "+count+" river hydraulic geometry checks");
    }
}
