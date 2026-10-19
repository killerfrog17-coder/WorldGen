using System;

namespace WorldGen.Presentation
{
    /// <summary>Only playback weights for recorded effects; never generates PCM samples.</summary>
    public readonly struct WeatherAudioMix
    {
        public readonly double WindLight,WindStrong,WindGale,RainLight,RainMedium,RainHeavy;
        public WeatherAudioMix(double windMps,double gustMps,double rainMmPerHour,bool aboveCloud)
        {
            double wind=Finite(Math.Max(windMps,gustMps*.8));
            double strength=Clamp((wind-2)/22),strong=Clamp((wind-6)/10),gale=Clamp((wind-17)/13);
            WindLight=strength*Math.Sqrt(1-strong);
            WindStrong=strength*Math.Sqrt(strong*(1-gale));
            WindGale=strength*Math.Sqrt(strong*gale);
            double rain=aboveCloud?0:Finite(rainMmPerHour),volume=1-Math.Exp(-rain/5);
            double medium=Clamp((rain-.5)/4.5),heavy=Clamp((rain-7)/23);
            RainLight=volume*Math.Sqrt(1-medium);
            RainMedium=volume*Math.Sqrt(medium*(1-heavy));
            RainHeavy=volume*Math.Sqrt(medium*heavy);
        }
        static double Finite(double x)=>double.IsNaN(x)||double.IsInfinity(x)?0:Math.Max(0,x);
        static double Clamp(double x)=>Math.Max(0,Math.Min(1,x));
    }
}
