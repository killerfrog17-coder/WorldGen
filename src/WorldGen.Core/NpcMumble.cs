using System;
namespace WorldGen.Core
{
    // A deliberately non-verbal placeholder voice. Shared PCM banks are made
    // off the main thread; no TTS model, recordings or per-person audio assets.
    public static class NpcMumble
    {
        public const int SampleRate=22050;
        public static float[][] Build()
        {
            var bank=new float[8][];var random=new Random(173931);
            for(int v=0;v<bank.Length;v++)
            {
                int length=(int)(SampleRate*(.13+v%3*.018));var samples=new float[length];double fundamental=125+v*3;
                double f1=320+v%4*90,f2=850+v%3*190;var weights=new double[25];
                for(int harmonic=1;harmonic<weights.Length;harmonic++){double f=fundamental*harmonic;weights[harmonic]=(.15+2*Math.Exp(-Math.Pow((f-f1)/130,2))+Math.Exp(-Math.Pow((f-f2)/210,2)))/Math.Pow(harmonic,1.1);}
                double peak=.001;
                for(int i=0;i<length;i++)
                {
                    double t=i/(double)SampleRate,envelope=Math.Min(1,t/.018)*Math.Min(1,(length-i)/(SampleRate*.04)),value=0;
                    for(int h=1;h<weights.Length;h++)value+=Math.Sin(2*Math.PI*fundamental*h*(t+.0002*Math.Sin(t*30)))*weights[h];
                    value=(value+(random.NextDouble()-.5)*.015)*envelope;samples[i]=(float)value;peak=Math.Max(peak,Math.Abs(value));
                }
                for(int i=0;i<length;i++)samples[i]=(float)(samples[i]/peak*.38);bank[v]=samples;
            }
            return bank;
        }
    }
}
