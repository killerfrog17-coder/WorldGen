using WorldGen.Core;

static class RiverPlanformTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        const double radius=637100,length=6000;const int count=256;
        V Spine(double t)=>new V(1,(t-.5)*length/radius,0).Unit;
        var low=RiverPlanform.Build(Spine,count,length,14,.001,.8,.15,radius,7312);
        var repeat=RiverPlanform.Build(Spine,count,length,14,.001,.8,.15,radius,7312);
        var steep=RiverPlanform.Build(Spine,count,length,14,.08,.8,.15,radius,7312);
        double distance=0,maxSide=0,maxCurvature=0;
        for(int i=0;i<=count;i++)
        {
            Check((low[i]-repeat[i]).Length==0,"Recreated reach has identical bends");
            Check((steep[i]-Spine(i/(double)count)).Length<1e-14,"Steep confined river keeps its drainage path");
            Check(double.IsFinite(low[i].X)&&Math.Abs(low[i].Length-1)<1e-8,"Finite spherical channel centre within canonical coordinate precision");
            maxSide=Math.Max(maxSide,Math.Abs(low[i].Z)*radius);
            if(i>0){distance+=(low[i]-low[i-1]).Length*radius;Check(low[i].Y>low[i-1].Y,"Bends never loop over themselves or reverse the reach");}
            if(i>0&&i<count)
            {
                V a=(low[i]-low[i-1])*radius,b=(low[i+1]-low[i])*radius;
                double cross=V.Cross(a,b).Length;
                if(cross>1e-9)maxCurvature=Math.Max(maxCurvature,2*cross/(a.Length*b.Length*(a+b).Length));
            }
        }
        Check((low[0]-Spine(0)).Length==0&&(low[count]-Spine(1)).Length==0,"Confluence positions remain exact");
        Check(maxSide>20&&maxSide<45,"Lowland reach develops material-scale bends within its valley envelope");
        Check(distance>length*1.01&&distance<length*1.5,"Lowland river has bounded nontrivial sinuosity");
        Check(1/maxCurvature>14*2,"Bend radius exceeds channel width; opposite banks do not fold over");
        Check(Math.Abs(low[1].Z)*radius<.05&&Math.Abs(low[count-1].Z)*radius<.05,"Shared junction tangent collars are smooth");
        Console.WriteLine($"PASS {checks} planform checks; sinuosity {distance/length:F3}, offset {maxSide:F2}m, minimum radius {1/maxCurvature:F2}m");
    }
}
