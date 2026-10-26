using WorldGen.Core;
using WorldGen.Presentation;

static class SunShadowBudgetTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        V Direction(double degrees){double a=degrees*Math.PI/180;return new V(Math.Sin(a),Math.Cos(a),0);}
        double Angle(V a,V b)=>Math.Acos(Math.Max(-1,Math.Min(1,V.Dot(a,b))))*180/Math.PI;
        var motion=new SunShadowMotion();var initial=motion.Step(Direction(0),0,false,true);
        for(int degree=1;degree<5;degree++)Check((motion.Step(Direction(degree),degree,false,true)-initial).Length==0&&motion.Holding,"Stationary daylight remains exactly fixed below the catch-up threshold");
        Check((motion.Step(Direction(6),6,false,true)-initial).Length==0&&!motion.Holding,"Catch-up begins without a pose jump");
        var middle=motion.Step(Direction(6.1),8.5,false,true);
        Check(Angle(initial,middle)>2&&Angle(initial,middle)<4,"Catch-up has an intermediate eased pose");
        Check((motion.Step(Direction(6.2),11,false,true)-Direction(6.2)).Length<4e-9&&motion.Holding,"Five-second catch-up settles and holds again");
        var held=motion.Direction;motion.Step(Direction(7),13,true,true);
        Check((motion.Direction-held).Length<4e-9&&!motion.Holding,"Observer movement starts a smooth catch-up");
        Check((motion.Step(Direction(8),18,true,true)-Direction(8)).Length<4e-9,"Continued movement reaches current sunlight");
        Check((motion.Step(Direction(8.1),18.1,false,true)-Direction(8.1)).Length<4e-9&&motion.Holding,"Stopping movement establishes a new stable light pose");
        motion.Step(Direction(9),20,false,false);
        Check(!motion.Holding,"Near-horizon daylight follows the current sun");
        Check((motion.Step(Direction(10),25,false,false)-Direction(10)).Length<4e-9,"Dawn/dusk catch-up completes while still");
        Check((motion.Step(Direction(11),26,false,false)-Direction(11)).Length<4e-9,"Continuous low-sun following does not restart five-second easing");
        Check((motion.Step(Direction(80),27,false,true)-Direction(80)).Length<4e-9,"Large clock changes reset to the correct pose");
        Check((motion.Step(Direction(90),28,false,true,true)-Direction(90)).Length<4e-9,"Explicit moon/teleport/setting reset is immediate");
        Check((motion.Step(Direction(91),1,false,true)-Direction(91)).Length<4e-9,"Clock rollback cannot strand the transition");
        foreach(int fps in new[]{15,30,60,144})
        {
            var sample=new SunShadowMotion();sample.Step(Direction(0),0,false,true);sample.Step(Direction(6),1,true,true);
            for(int frame=0;frame<=fps*5;frame++)
            {
                var d=sample.Step(Direction(6),1+frame/(double)fps,true,true);
                Check(Math.Abs(d.Length-1)<4e-9&&double.IsFinite(d.X)&&double.IsFinite(d.Y),"Interpolated directions remain finite/unit length at every frame rate");
            }
            Check((sample.Direction-Direction(6)).Length<4e-9,"Catch-up duration is frame-rate independent");
        }
        foreach(double range in new[]{50d,100,180})foreach(double top in new[]{-1000d,0,30,2000})foreach(double sun in new[]{-1d,0,.000001,.001,.01,.1,.5,1})
        {
            double reach=VegetationShadowBudget.Reach(range,top,sun);
            Check(double.IsFinite(reach)&&reach>=0&&reach<=VegetationShadowBudget.CasterLimit(range),"Grazing/below-horizon light and high hills never admit an unlimited forest");
        }
        double y=Math.Sin(Math.PI/180),oldRadius=180*Math.Sqrt(1+Math.Pow(Math.Tan(34*Math.PI/180),2)*(1+Math.Pow(16d/9,2)))+8;
        double oldReach=oldRadius+(30+oldRadius)*Math.Sqrt(1-y*y)/y;
        Check(oldReach>19000&&VegetationShadowBudget.Reach(180,30,y)==256,"Reproduce and bound the old one-degree sun caster explosion");
        Check(VegetationShadowBudget.CasterFade(224,180)==1&&VegetationShadowBudget.CasterFade(256,180)==0,"Caster budget retires smoothly across 224–256m");
        double previous=1;
        for(int metre=0;metre<=1000;metre++)
        {
            double fade=VegetationShadowBudget.CasterFade(metre,180);
            Check(fade>=0&&fade<=previous,"Caster distance fade cannot grow outward or exceed full coverage");previous=fade;
        }
        Check(VegetationShadowBudget.CardWeight(.1)==0&&VegetationShadowBudget.CardWeight(.2)==0,"Grazing light uses opaque crowns, not alpha cards");
        Check(Math.Abs(VegetationShadowBudget.CardWeight(.3)-1)<4e-9&&VegetationShadowBudget.CardWeight(1)==1,"Higher sun permits the cheaper textured silhouette");
        Check(VegetationShadowBudget.CardWeight(.25)>.49&&VegetationShadowBudget.CardWeight(.25)<.51,"Low-sun shape transition has a smooth intermediate blend");
        Check(VegetationShadowBudget.ShadowRange(0)==60&&VegetationShadowBudget.ShadowRange(1)==180,"Horizon/full-daylight shadow ranges have fixed budgets");
        Check(VegetationShadowBudget.ResolutionCap(0)==1024&&VegetationShadowBudget.ResolutionCap(.2)==2048&&VegetationShadowBudget.ResolutionCap(1)==4096,"Low sun limits atlas cost without increasing any preset's resolution");
        Check(VegetationShadowBudget.DirectSunlight(Math.Sin(2*Math.PI/180))==0&&VegetationShadowBudget.DirectSunlight(Math.Sin(9*Math.PI/180))==1,"Horizon illumination fades to ambient while full daytime sunlight is preserved");
        double priorRange=0,priorLight=0;
        for(int step=0;step<=100;step++)
        {
            double up=step/100d,range=VegetationShadowBudget.ShadowRange(up),light=VegetationShadowBudget.DirectSunlight(up);
            Check(range>=priorRange&&range>=60&&range<=180&&light>=priorLight&&light>=0&&light<=1,"Dawn/dusk range and direct light taper continuously in the same direction");
            priorRange=range;priorLight=light;
        }
        Console.WriteLine("PASS "+checks+" pure sun-motion/caster-budget checks; no world or player launched.");
    }
}
