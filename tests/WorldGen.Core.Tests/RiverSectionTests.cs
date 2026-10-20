using WorldGen.Core;
using WorldGen.Serialization;

static class RiverSectionTests
{
    public static void Routes(string path)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var terrain=new LocalTerrain(world,horizontalScale:.1);
        int samples=0,wet=0;double worstRise=0;
        foreach(var reach in terrain.Hydrology.Channels.Where(c=>c.StartM>20&&c.StartM<3000&&c.WidthM>10).Take(24))
        {
            double previous=double.PositiveInfinity;
            for(int k=1;k<16;k++)
            {
                var at=terrain.Amplification.RiverPoint(reach.From,k/16d);var s=terrain.Patch(at,2000).Sample(at);
                if(!double.IsFinite(s.ElevationM)||s.ElevationM>s.PreWaterElevationM+.002)throw new Exception("Invalid or raised river bed");
                if(s.Underwater)wet++;samples++;
                if(double.IsFinite(s.WaterSurfaceM)){worstRise=Math.Max(worstRise,s.WaterSurfaceM-previous);previous=s.WaterSurfaceM;}
            }
        }
        Console.WriteLine($"Actual routes: {wet}/{samples} wet, largest downstream rise {worstRise:F4}m");
        if(worstRise>.1)throw new Exception("Actual amplified river route reverses downhill profile");
    }
    public static void Run()
    {
        int count=0;
        void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
        foreach(double slope in new[]{0d,.001,.01,.1,1})foreach(double hard in new[]{0d,.5,1})foreach(double variation in new[]{0d,.4,1})
        {
            double last=-1;
            for(int i=0;i<=100;i++)
            {
                double t=RiverSection.Longitudinal(i/100d,slope,hard,variation);
                Check(t>=last&&t>=0&&t<=1,"Reach profile never reverses downstream");last=t;
            }
            Check(RiverSection.Longitudinal(0,slope,hard,variation)==0&&RiverSection.Longitudinal(1,slope,hard,variation)==1,"Adjacent reaches preserve common endpoint datums");
        }
        foreach(double flow in new[]{.02,1,100,10000})foreach(double slope in new[]{.0001,.01,.2})
        {
            var section=new RiverSection(100,Math.Sqrt(flow)*2,2,flow,slope,.3);
            foreach(double raw in new[]{-20d,95,101,2000})for(int k=0;k<=100;k++)
            {
                double d=section.Support*k/100d,h=section.Cut(raw,d,19,1);
                Check(double.IsFinite(h)&&h<=raw,"River primitive only cuts existing terrain");
            }
            Check(section.Cut(150,section.Support,0,0)==150,"Compact primitive retires to original terrain");
            Check(section.Cut(150,0,0,0)<section.Level,"A channel contains a submerged bed");
        }
        var creek=new RiverSection(100,1,.4,.1,.0001,.8);
        Check(creek.Turbulence==0&&creek.Speed<.2,"Small low-gradient creeks can be calm");
        Console.WriteLine($"PASS {count} river primitive assertions");
    }
}
