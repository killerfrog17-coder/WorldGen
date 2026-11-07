using System;

namespace WorldGen.Core
{
    /// <summary>Exposed granitic uplands: exfoliation rounds summits while
    /// occasional regional joints leave a steep face. Terrain primitives only.</summary>
    public static class GraniteDomes
    {
        static double Smooth(double a,double b,double x)=>RiverscapeKernel.Smooth((x-a)/(b-a));
        public static double Admission(LocalTerrainProfile profile)
        {
            if(profile.Biome==Biome.Ocean||profile.Biome==Biome.Ice||profile.ElevationM<50)return 0;
            return Smooth(.45,.75,profile.Ground.RockWeight(0))*Smooth(120,600,profile.CoarseReliefM)
                *(1-Smooth(.15,.45,profile.Sediment))*(1-Smooth(.25,.6,profile.Landscape.Wet))
                *(1-Smooth(.1,.35,profile.Ground.Salinity));
        }
        public static double Profile(double radius)
        {
            if(radius>=1)return 0;
            return Math.Sqrt(Math.Max(0,1-radius*radius))*(1-Smooth(.80,1,radius));
        }
        public static double JointFace(double axialCoordinate,bool jointed)
            =>jointed?Smooth(-.48,-.37,axialCoordinate):1;
    }
}
