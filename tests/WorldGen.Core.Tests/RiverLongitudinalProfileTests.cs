using WorldGen.Core;

static class RiverLongitudinalProfileTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        double min=2,max=0,confinedMin=2,confinedMax=0,minDepth=2,maxDepth=0;
        bool different=false;
        foreach(double width in new[]{1d,4,16,64,300,1400})
        for(int i=0;i<400;i++)
        {
            double at=i*width*.7;
            var p=RiverLongitudinalProfile.At(at,width,.001,.8,.1,0,73421);
            var repeat=RiverLongitudinalProfile.At(at,width,.001,.8,.1,0,73421);
            var other=RiverLongitudinalProfile.At(at,width,.001,.8,.1,0,74123);
            Check(p.WidthScale==repeat.WidthScale&&p.DepthScale==repeat.DepthScale,"Recreated reach preserves every longitudinal station");
            Check(p.WidthScale>=.68&&p.WidthScale<=1.42&&p.DepthScale>=.48&&p.DepthScale<=1.72,"Physical variation stays bounded across stream and giant-river scales");
            different|=p.WidthScale!=other.WidthScale||p.DepthScale!=other.DepthScale;
            var nearby=RiverLongitudinalProfile.At(at+.001,width,.001,.8,.1,0,73421);
            Check(Math.Abs(p.WidthScale-nearby.WidthScale)<.001&&Math.Abs(p.DepthScale-nearby.DepthScale)<.001,"Bank and bed fields do not jump at sampling-cell boundaries");
            var bend=RiverLongitudinalProfile.At(at,width,.001,.8,.1,.8,73421);
            Check(bend.DepthScale>p.DepthScale,"Curved lowland sections develop deeper bend pools");
            if(width==16)
            {
                min=Math.Min(min,p.WidthScale);max=Math.Max(max,p.WidthScale);
                minDepth=Math.Min(minDepth,p.DepthScale);maxDepth=Math.Max(maxDepth,p.DepthScale);
                var confined=RiverLongitudinalProfile.At(at,width,.08,.1,.9,0,73421);
                confinedMin=Math.Min(confinedMin,confined.WidthScale);confinedMax=Math.Max(confinedMax,confined.WidthScale);
            }
        }
        Check(different,"Different river identities produce different irregular profiles");
        Check(max/min>1.25&&maxDepth/minDepth>1.35,"Alluvial reaches have visible width and depth variation");
        Check(confinedMax-confinedMin<(max-min)*.3,"Steep resistant channels retain more confined banks");
        for(int i=0;i<200;i++)_ = RiverLongitudinalProfile.At(i,16,.001,.8,.1,0,73421);
        long allocated=GC.GetAllocatedBytesForCurrentThread();
        double checksum=0;for(int i=0;i<10000;i++)checksum+=RiverLongitudinalProfile.At(i,16,.001,.8,.1,.4,73421).DepthScale;
        allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
        Check(allocated==0&&checksum>0,"Prepared-station sampling adds no managed allocation in warm CoreCLR control");
        Console.WriteLine($"PASS {checks} longitudinal profile controls; width {min:F3}..{max:F3}, depth {minDepth:F3}..{maxDepth:F3}. Not a game FPS measurement.");
    }
}
