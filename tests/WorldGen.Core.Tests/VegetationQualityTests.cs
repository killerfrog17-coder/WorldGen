using WorldGen.Core;
using WorldGen.Presentation;

static class VegetationQualityTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var medium=VegetationQuality.At(2);
        Check(medium.TreeNear==20&&medium.TreeFar==50,"Owner tree detail is Medium, not an inflated default");
        Check(medium.TreeVariantRange==300,"Owner's 300m cap now simplifies branch variation rather than moving roots");
        Check(medium.MidNear==20&&medium.MidFar==40&&medium.MidRange==500,"Saved mid layer baseline");
        Check(medium.HerbNear==5&&medium.HerbRange==100&&medium.FungusRange==20,"Saved specialist baseline");
        for(int level=0;level<5;level++)
        {
            var p=VegetationQuality.At(level);
            Check(p.TreeFar<100&&p.TreeFar>p.TreeNear*2,"Bounded tree precision at every preset");
            Check(p.GrassClose<=42&&p.GrassRange<=400&&p.GrassDensity<=1.2,"Ultra remains bounded");
            if(level>0){var prior=VegetationQuality.At(level-1);Check(p.TreeNear>prior.TreeNear&&p.TreeFar>prior.TreeFar&&p.MidRange>prior.MidRange&&p.GrassRange>prior.GrassRange,"Monotone quality budgets");}
            for(int distance=0;distance<=1500;distance++)
            {
                var blend=ScatterLodBlend.At(ScatterKind.GeneratedTree,VegetationQuality.TreeIdentityRange/150,distance,p.TreeNear,p.TreeFar,VegetationQuality.TreeIdentityRange);
                double stand=VegetationQuality.ForestWeight(distance);
                double variant=VegetationQuality.VariantWeight(distance,p.TreeVariantRange);
                Check(variant>=0&&variant<=1&&(!double.IsNaN(variant)),"Bounded complementary variant transition");
                Check(Math.Abs(blend.Visibility+stand-1)<1e-10,"No empty radial handoff or density reset");
                if(distance>=150&&distance<950)Check(blend.Far==2&&blend.Blend==1&&blend.Visibility==1,"Cheap same-position trees continue beyond close-detail range");
            }
        }
        Console.WriteLine("PASS "+checks+" vegetation preset/identity-continuity assertions");
    }
}
