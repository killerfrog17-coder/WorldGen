using System;

namespace WorldGen.Presentation
{
    /// <summary>Continuous stereo rainfall: filtered wash plus independently decaying water impacts.
    /// No audio assets, repeating seams, engine calls or callback allocations.</summary>
    public sealed class RainSynth
    {
        public volatile float MillimetresPerHour;
        uint state=718921u;double gain,l,r,ll,rr,impactL,impactR,dcL,dcR;
        double Random(){state^=state<<13;state^=state>>17;state^=state<<5;return state/4294967295d;}
        public void Read(float[] data)
        {
            double rain=Math.Max(0,MillimetresPerHour),target=1-Math.Exp(-rain/7),chance=Math.Min(.018,rain*.00022);
            for(int i=0;i<data.Length;i+=2)
            {
                gain+=(target-gain)*.00018;double a=Random()*2-1,b=Random()*2-1;
                l+=(a-l)*.45;r+=(b-r)*.45;ll+=(l-ll)*.065;rr+=(r-rr)*.065;
                impactL*=.75;impactR*=.77;
                if(Random()<chance){double volume=.12+Random()*.32,pan=Random();impactL+=volume*pan;impactR+=volume*(1-pan);}
                double left=((l-ll)*.3+ll*.2+impactL)*gain,right=((r-rr)*.3+rr*.2+impactR)*gain;
                dcL+=(left-dcL)*.002;dcR+=(right-dcR)*.002;
                data[i]=(float)Math.Max(-.9,Math.Min(.9,left-dcL));if(i+1<data.Length)data[i+1]=(float)Math.Max(-.9,Math.Min(.9,right-dcR));
            }
        }
    }
}
