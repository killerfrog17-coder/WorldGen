using System;

namespace WorldGen.Core
{
    /// <summary>Small saturated-shore hollows around an EXISTING standing-water
    /// datum. Retains the higher ground as hummocks. No new water plane, basin
    /// identity, persistent simulation, or independent decorative wetland mesh.</summary>
    public static class WetlandMicrorelief
    {
        public static double Bed(double ground,double water,LandscapeControls province,V metres,int seed,double strength=1)
        {
            if(!double.IsFinite(water)||province.Wet<=.45||province.Salt>=.32)return ground;
            double above=ground-water;if(above<=-1.3||above>=1.4)return ground;
            double admission=OrogenMorphology.Smooth(.45,.8,province.Wet)*(1-OrogenMorphology.Smooth(.12,.32,province.Salt));
            double shore=OrogenMorphology.Smooth(-1.3,-.5,above)*(1-OrogenMorphology.Smooth(.6,1.4,above));
            if(admission*shore<=0)return ground;
            double pattern=LandscapeNoise.Value(metres,seed^0x51293,1/38d)+.2*LandscapeNoise.Value(metres,seed^0x36917,1/17d);
            double hollow=1-OrogenMorphology.Smooth(.10,.48,Math.Abs(pattern));
            return ground-.85*admission*shore*hollow*D.Clamp(strength);
        }
    }
}
