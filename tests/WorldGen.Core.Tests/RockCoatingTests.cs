using System.Reflection;
using WorldGen.Core;

static class RockCoatingTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var input=new GroundProfile{Sand=.4,Silt=.3,Clay=.3,DepthM=.8,Organic=.1,Iron=.7,Weathering=.8,Drainage=.6,PH=6.4,Fertility=.4,WaterCapacityMm=80,RockColor=new V(.55,.43,.32),SoilColor=new V(.3,.2,.1),RockMixA=new V(0,0,1)};
        var fields=typeof(GroundProfile).GetFields(BindingFlags.Public|BindingFlags.Instance);
        double minimum=1,maximum=0;
        foreach(float exposed in new[]{0f,.0001f,.00101f,.2f,1f})foreach(float moisture in new[]{0f,.5f,1f})foreach(float frozen in new[]{0f,1f})
        {
            var controls=new LandscapeControls{CanyonExposure=exposed,Moisture=moisture,Frozen=frozen};
            var baseline=LandscapeProvince.Ground(input,controls,new V(0,0,0),31);
            for(int i=0;i<180;i++)
            {
                var position=new V(-1100+i*17,-350,22000-i*7);
                var actual=LandscapeProvince.Ground(input,controls,position,31);
                foreach(var field in fields)if(field.Name!="RockColor")Check(Equals(field.GetValue(baseline),field.GetValue(actual)),"Surface coating changed physical substrate "+field.Name);
                Check(actual.RockColor.Equals(LandscapeProvince.Ground(input,controls,position,31).RockColor),"Canonical point must reconstruct exactly");
                Check(double.IsFinite(actual.RockColor.X)&&actual.RockColor.X>0&&actual.RockColor.X<1,"Coating remains bounded");
                if(exposed<=.001)Check(actual.RockColor.Equals(input.RockColor),"Unexposed ground is unchanged");
                if(exposed==1&&moisture==0&&frozen==0){minimum=Math.Min(minimum,actual.RockColor.X);maximum=Math.Max(maximum,actual.RockColor.X);}
                var adjacent=LandscapeProvince.Ground(input,controls,position+new V(.001,0,0),31);
                Check((adjacent.RockColor-actual.RockColor).Length<.0001,"Cross-chunk world-space coating is continuous");
                controls.BareRock=1;
                var fresh=LandscapeProvince.Ground(input,controls,position,31);
                Check(fresh.RockColor.Equals(new V(.18,.17,.16)),"Fresh volcanic exposure retains basalt override");
                controls.BareRock=0;
            }
        }
        Check(maximum-minimum>.04,"Broad exposed surfaces have visible bounded tonal variation");
        Console.WriteLine($"PASS {checks} rock coating continuity, bounds and physical substrate invariants; red range {minimum:F4}..{maximum:F4}");
    }
}
