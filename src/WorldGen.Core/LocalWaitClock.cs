using System;
namespace WorldGen.Core
{
    public sealed class LocalWaitClock
    {
        public const double StepSeconds=.05;
        public int Steps{get;private set;}=1;
        public void Reset()=>Steps=1;
        public void Feedback(double milliseconds)
        {if(!double.IsFinite(milliseconds)||milliseconds>8)Steps=Math.Max(1,Steps/2);else if(milliseconds<5)Steps=Math.Min(32,Steps+1);}
        public double Advance(bool waiting,bool blocked,double realSeconds)
            =>blocked?0:!waiting?Math.Max(0,realSeconds):Steps*StepSeconds;
        public static string Elapsed(double solarDays)
        {long minutes=(long)Math.Floor(Math.Max(0,solarDays)*1440);return minutes>=1440?(minutes/1440)+" d "+(minutes/60%24)+" h":minutes>=60?(minutes/60)+" h "+(minutes%60)+" m":minutes+" m";}
    }
}
