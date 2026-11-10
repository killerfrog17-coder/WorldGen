using System;

namespace WorldGen.Presentation
{
    /// <summary>Initial loading reveals outward. Later LOD/forest replans can
    /// hold expansion, but cannot erase a horizon the player has already seen.
    /// Teleports start a new reveal; an explicit view-distance reduction caps it.</summary>
    public static class TerrainStreamingReveal
    {
        public const double InitialDistance=8;
        public static double Advance(double current,double ready,double limit,double seconds)
        {
            limit=Math.Max(0,limit);current=Math.Min(limit,Math.Max(0,current));
            double target=Math.Min(limit,Math.Max(current,ready));
            if(target==current)return current;
            return Smooth(current,target,seconds,3);
        }
        // Blend apparent distance rather than racing across the first few
        // hundred metres at the same speed as a distant horizon.
        public static double Smooth(double current,double target,double seconds,double transitionSeconds)
        {
            current=Math.Max(0,current);target=Math.Max(0,target);if(seconds<=0)return current;
            if(current==target)return current;
            double blend=1-Math.Exp(-Math.Min(.1,seconds)*5/Math.Max(.5,transitionSeconds));
            double next=Math.Exp(Math.Log(current+100)+(Math.Log(target+100)-Math.Log(current+100))*blend)-100;
            return Math.Abs(next-target)<=Math.Max(.05,target*.0001)?target:next;
        }
    }
}
