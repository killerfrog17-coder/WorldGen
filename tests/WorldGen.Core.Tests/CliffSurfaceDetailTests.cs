using WorldGen.Core;

static class CliffSurfaceDetailTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        double min=0,max=-100,total=0;int n=0;
        foreach(int seed in new[]{73421,-12,0})foreach(double hardness in new[]{0d,.5,1d})
        for(int z=-10;z<=10;z++)for(int x=-10;x<=10;x++)
        {
            V p=new V(x*1.871-300000,z*2.193+100000,x*.37-z*.81+50000);
            double cut=CliffSurfaceDetail.Cut(p,seed,1,hardness);
            Check(double.IsFinite(cut)&&cut<=0&&cut>=-5.35,"Fracture relief is bounded cut-only terrain");
            Check(cut==CliffSurfaceDetail.Cut(p,seed,1,hardness),"Planet address and seed reconstruct exactly");
            Check(CliffSurfaceDetail.Cut(p,seed,0,hardness)==0,"Unexposed ground remains exactly unchanged");
            Check(Math.Abs(cut-CliffSurfaceDetail.Cut(p+new V(.00001,0,0),seed,1,hardness))<.002,"Fracture field remains continuous at metre-scale addresses");
            min=Math.Min(min,cut);max=Math.Max(max,cut);total+=cut;n++;
        }
        // Explicit lattice boundaries, including negative and large positions.
        foreach(int axis in new[]{0,1,2})foreach(int cell in new[]{-16384,-1,0,1,16384})
        {
            double d=cell*16;V p=axis==0?new V(d,23.371,-49.18):axis==1?new V(23.371,d,-49.18):new V(23.371,-49.18,d);
            V e=axis==0?new V(.00001,0,0):axis==1?new V(0,.00001,0):new V(0,0,.00001);
            Check(Math.Abs(CliffSurfaceDetail.Cut(p-e,73421,1,.5)-CliffSurfaceDetail.Cut(p+e,73421,1,.5))<.002,"Cell boundaries cannot create height seams");
        }
        Check(min< -4&&max>-.8,"Fractured walls retain both incised joints and broad rock faces");
        for(int i=0;i<1000;i++)
        {
            double u=Rng.Value(73421,"cliff-continuity-y",i),v=Rng.Value(73421,"cliff-continuity-z",i);
            var p=new V((i-500)*16,(u-.5)*10000,(v-.5)*10000);var e=new V(.000001,0,0);
            Check(Math.Abs(CliffSurfaceDetail.Cut(p-e,i,1,.5)-CliffSurfaceDetail.Cut(p+e,i,1,.5))<.001,"Random seeded cell boundaries preserve continuous rock relief");
        }
        Console.WriteLine($"PASS {checks} fracture relief controls; range{min:F3}..{max:F3}m mean{total/n:F3}m");
    }
}
