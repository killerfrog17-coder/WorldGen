using WorldGen.Core;

static class RiverSectionTests
{
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
