using System;

namespace WorldGen.Presentation
{
    /// <summary>Only playback weights for recorded effects; never generates PCM samples.</summary>
    public readonly struct WeatherAudioMix
    {
        public readonly double WindStrength,WindGapMinSeconds,WindGapMaxSeconds,RainLight,RainMedium,RainHeavy;
        public readonly int WindBand;
        public readonly bool RainAllowed,ThunderAllowed;
        public WeatherAudioMix(double windMps,double gustMps,double rainMmPerHour,bool aboveCloud,double cloudCover=1,double thunderstorm=0)
        {
            double wind=Finite(windMps)*.65+Finite(gustMps)*.35;
            WindStrength=Clamp((wind-2)/20);WindBand=wind<9?0:wind<19?1:2;
            WindGapMinSeconds=20*(1-WindStrength)+.8*WindStrength;
            WindGapMaxSeconds=45*(1-WindStrength)+3*WindStrength;
            RainAllowed=!aboveCloud&&Finite(cloudCover)>=.85&&Finite(rainMmPerHour)>=.1;
            ThunderAllowed=RainAllowed&&Finite(thunderstorm)>.2;
            double rain=RainAllowed?Finite(rainMmPerHour):0,volume=1-Math.Exp(-rain/5);
            double medium=Clamp((rain-.5)/4.5),heavy=Clamp((rain-7)/23);
            RainLight=volume*Math.Sqrt(1-medium);
            RainMedium=volume*Math.Sqrt(medium*(1-heavy));
            RainHeavy=volume*Math.Sqrt(medium*heavy);
        }
        static double Finite(double x)=>double.IsNaN(x)||double.IsInfinity(x)?0:Math.Max(0,x);
        static double Clamp(double x)=>Math.Max(0,Math.Min(1,x));
    }
}
