using System;

namespace WorldGen.Presentation
{
    /// <summary>Presentation budgets, not plant populations or world-generation rules.</summary>
    public readonly struct VegetationQuality
    {
        public readonly double GrassClose,GrassRange,GrassDensity,HerbNear,HerbRange,FungusRange,MidNear,MidFar,MidRange,TreeNear,TreeFar;
        public readonly int GrassDetail;
        public readonly double TreeVariantRange;
        public const double TreeIdentityRange=1400;
        public static readonly string[] Names={"Very low","Low","Medium","High","Ultra"};
        VegetationQuality(double grassClose,double grassRange,double density,int detail,double herbNear,double herbRange,double fungusRange,double midNear,double midFar,double midRange,double treeNear,double treeFar)
        {GrassClose=grassClose;GrassRange=grassRange;GrassDensity=density;GrassDetail=detail;HerbNear=herbNear;HerbRange=herbRange;FungusRange=fungusRange;MidNear=midNear;MidFar=midFar;MidRange=midRange;TreeNear=treeNear;TreeFar=treeFar;TreeVariantRange=treeFar*6;}
        public static VegetationQuality At(int level)
        {
            switch(Math.Max(0,Math.Min(4,level)))
            {
                case 0:return new VegetationQuality(12,120,.60,0,3,45,12,10,22,240,8,24);
                case 1:return new VegetationQuality(18,210,.80,1,4,70,16,14,30,360,12,35);
                // Owner's saved 20/50m trees, 20/40/500m mid layer and 5/100/20m herbs.
                case 2:return new VegetationQuality(30,300,1,2,5,100,20,20,40,500,20,50);
                case 3:return new VegetationQuality(36,350,1.10,2,7,120,28,25,55,650,26,70);
                default:return new VegetationQuality(42,400,1.20,3,9,140,35,30,70,800,32,90);
            }
        }
        // Local identities persist after their meshes simplify. This interval
        // must not follow a close-detail slider or a sapling's crown width.
        public static double ForestStart(double reach)=>Math.Min(950,reach*.8);
        public static double VariantWeight(double distance,double range)
        {double t=Math.Max(0,Math.Min(1,(distance-range*.7)/Math.Max(1,range*.3)));return t*t*(3-2*t);}
        public static double ForestWeight(double distance,double reach=TreeIdentityRange)
        {double a=ForestStart(reach),t=Math.Max(0,Math.Min(1,(distance-a)/Math.Max(1,reach-a)));return t*t*(3-2*t);}
    }
}
