using System;

namespace WorldGen.Core
{
    /// <summary>Static alluvial banks over the existing paired channel geometry.
    /// Coefficients are prepared per reach; queries need no noise, objects or ticks.</summary>
    public static class RiverAlluvium
    {
        static double Smooth(double a,double b,double x)=>RiverscapeKernel.Smooth((x-a)/(b-a));
        public static double Strength(double halfWidth,double grade,double sediment,double hardness)
            =>Smooth(2,8,halfWidth)*(1-Smooth(.002,.012,grade))
                *(.45+.55*D.Clamp(sediment))*(1-.8*D.Clamp(hardness));
        public static double Curvature(V incoming,V outgoing,V side,double distance,double halfWidth)
        {
            V change=outgoing-incoming;
            return D.Clamp((change.X*side.X+change.Y*side.Y+change.Z*side.Z)*halfWidth*12/Math.Max(.01,distance),-.9,.9);
        }
        // Geometry follows shared stations, not the categorical flow animation.
        // Otherwise a calm/turbulent boundary abruptly loses its entire bar.
        public static void PrepareBanks(RiverscapeSpan[] spans,double radius,double reachStrength)
        {
            if(reachStrength<=0||spans.Length==0)return;
            var strength=new double[spans.Length+1];var bends=new double[strength.Length];
            double total=0;foreach(var s in spans)total+=(s.B-s.A).Length*radius;
            double travelled=0;
            for(int i=1;i<spans.Length;i++)
            {
                var before=spans[i-1];var after=spans[i];
                double previous=(before.B-before.A).Length*radius,next=(after.B-after.A).Length*radius;
                travelled+=previous;double t=travelled/Math.Max(.001,total);
                double grade=Math.Max(0,(before.LevelA-after.LevelB)/Math.Max(.001,previous+next));
                bool falls=before.Primitive==RiverFlowPrimitive.Waterfall||after.Primitive==RiverFlowPrimitive.Waterfall;
                strength[i]=falls?0:reachStrength*Smooth(0,.12,t)*(1-Smooth(.88,1,t))*(1-Smooth(.004,.025,grade));
                bends[i]=Curvature((before.B-before.A).Unit,(after.B-after.A).Unit,after.SideA,(previous+next)*.5,after.WidthA);
            }
            for(int i=0;i<spans.Length;i++)
            {
                var s=spans[i];s.AlluviumA=strength[i];s.AlluviumB=strength[i+1];s.Alluvium=(strength[i]+strength[i+1])*.5;
                s.BankBendA=bends[i];s.BankBendB=bends[i+1];
                // Alluvial asymmetry is supplied by the continuous bank field.
                // Retire the old independent per-span correction on these reaches.
                s.Bend=0;spans[i]=s;
            }
        }
        public static double Bed(double original,double level,double depth,double relative,double signed,double bend,double strength)
        {
            if(strength<=0)return original;
            double bowl=Math.Max(0,1-relative*relative);
            double correction=depth*bowl*bend*signed*5*strength;
            // A bar can emerge inside the old wet ribbon. The renderer clips
            // against this same terrain, so it exposes land rather than a decal.
            return Math.Min(level+Math.Min(1.2,.15+depth*.25),original+correction);
        }
        public static double Deposit(double level,double terrain,double depth,double relative,double strength)
        {
            if(strength<=0||relative>=1.85)return 0;
            double bank=Smooth(.35,.65,relative)*(1-Smooth(1.15,1.85,relative));
            if(relative<1)bank=Math.Max(bank,Smooth(-.02,.05,terrain-level));
            double height=1-Smooth(.6,Math.Max(2,depth+1),terrain-level);
            return D.Clamp(Smooth(.08,.32,strength)*bank*height);
        }
        public static double RootedAdmission(double sand,double gravel)
            =>sand<=0&&gravel<=0?1:1-Smooth(.12,.45,sand+gravel);
        public static void Ground(double sand,double gravel,ref GroundProfile ground,ref GroundCoverProfile cover,ref MidCoverProfile mid,ref ForestCoverProfile forest)
        {
            double amount=D.Clamp(sand+gravel);if(amount<=0)return;
            double coarse=gravel/Math.Max(.000001,sand+gravel),keep=1-amount;
            ground.Sand=ground.Sand*keep+(.92-.35*coarse)*amount;
            ground.Clay=ground.Clay*keep+.04*amount;ground.Silt=Math.Max(0,1-ground.Sand-ground.Clay);
            ground.Gravel=ground.Gravel*keep+(.06+.84*coarse)*amount;
            ground.Organic*=1-.98*amount;ground.DepthM=Math.Max(ground.DepthM,.8*amount);
            ground.Drainage=ground.Drainage*keep+(.7+.25*coarse)*amount;
            ground.SandColor=ground.SandColor*keep+CoastalTerrain.SandColour(ground)*amount;
            ground.Deposit=amount>.35?ParentDeposit.Alluvium:ground.Deposit;
            // Active deposits have a sparse pioneer margin, not mature forest
            // or a lawn crossing an exposed gravel/sand bar.
            double bare=1-RootedAdmission(sand,gravel);
            cover.ScaleWeights(1-.99*bare);mid.Coverage*=1-.99*bare;forest.Coverage*=1-bare;
        }
    }
}
