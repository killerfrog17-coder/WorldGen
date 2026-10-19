using System;

namespace WorldGen.Presentation
{
    /// <summary>Continuous stereo, band-limited turbulence; no recorded loop or audio-thread engine calls.
    /// Main thread publishes controls, audio thread alone owns the filters and gust scheduler.</summary>
    public sealed class WindSynth
    {
        public volatile float Strength,Storm;
        readonly int rate;uint noise=381921u;long sample,nextGust;double start,duration=1,gain,slow,fast,deep,rightSlow,rightFast,rightDeep,band,bandLow;
        readonly double aSlow,aFast,aDeep,resonance;
        public WindSynth(int sampleRate=22050)
        {rate=sampleRate;aSlow=1-Math.Exp(-2*Math.PI*130/rate);aFast=1-Math.Exp(-2*Math.PI*850/rate);aDeep=1-Math.Exp(-2*Math.PI*35/rate);resonance=2*Math.Sin(Math.PI*370/rate);nextGust=rate*3L;}
        double Random(){noise^=noise<<13;noise^=noise>>17;noise^=noise<<5;return noise/4294967295d;}
        public void Read(float[] data)
        {
            double strength=Math.Max(0,Math.Min(1,Strength)),storm=Math.Max(0,Math.Min(1,Storm));
            for(int i=0;i<data.Length;i+=2,sample++)
            {
                double seconds=sample/(double)rate;
                if(sample>=nextGust)
                {
                    start=seconds;duration=4+Random()*9;
                    double gap=(1-storm)*(10+Random()*25)+storm*(1+Random()*3);
                    nextGust=sample+(long)((duration+gap)*rate);
                }
                double q=(seconds-start)/duration;
                double pulse=sample<rate*3L||q<0||q>1?0:Math.Sin(q*Math.PI);pulse*=pulse;
                // Calm weather is silent between gusts; only a true gale has a continuous low bed.
                double target=strength*(pulse*(.28+.25*storm)+storm*.2);gain+=(target-gain)*.0008;
                double white=Random()*2-1,other=Random()*2-1;
                slow+=(white-slow)*aSlow;fast+=(white-fast)*aFast;deep+=(white-deep)*aDeep;
                rightSlow+=(other-rightSlow)*aSlow;rightFast+=(other-rightFast)*aFast;rightDeep+=(other-rightDeep)*aDeep;
                // Resonant noise, not a fixed whistle tone. Strong weather brings out the hollow howl.
                bandLow+=resonance*band;double high=white-bandLow-.12*band;band+=resonance*high;
                double howl=band*.08*storm*(.4+.6*pulse);
                double left=((slow-deep)*2.1+(fast-slow)*.25+deep*.4+howl)*gain;
                double right=((rightSlow-rightDeep)*1.7+(rightFast-rightSlow)*.2+rightDeep*.4+(slow-deep)*.4+howl)*gain;
                data[i]=(float)Math.Max(-.9,Math.Min(.9,left));if(i+1<data.Length)data[i+1]=(float)Math.Max(-.9,Math.Min(.9,right));
            }
        }
    }
}
