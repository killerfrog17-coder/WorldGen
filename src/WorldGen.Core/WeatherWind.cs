using System;

namespace WorldGen.Core
{
    /// <summary>Stateless, world-addressed wind detail. Mirrors WorldGenWind.hlsl.
    /// Weather owns the mean/peak, event occupancy/duration and turbulence.
    /// Two independent event lanes avoid a metronomic gust; no per-frame RNG.</summary>
    public static class WeatherWind
    {
        public const string Version="weather-wind-1";
        static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16);}}
        static double Unit(uint x)=>(Mix(x)&0xffffffu)/16777216d;
        public static V Axis(int seed,int lane)
        {
            uint h=unchecked((uint)seed+(uint)lane*0x9e3779b9u);
            return new V(Unit(h)-.5,Unit(h+1)-.5,Unit(h+2)-.5).Unit;
        }
        public static double Wavelength(int lane)=>lane==0?190:lane==1?110:lane==2?19:11;
        public static double Period(int lane)=>lane==0?64:lane==1?109:lane==2?3.7:2.1;
        public static double Phase(int seed,int lane,V position,double radiusM,double seconds)
            =>V.Dot(position,Axis(seed,lane))*radiusM/Wavelength(lane)-seconds/Period(lane);
        static double Smooth(double a,double b,double x){x=D.Clamp((x-a)/(b-a));return x*x*(3-2*x);}
        static double Pulse(double phase,int seed,int lane,double chance,double duration)
        {
            long slot=(long)Math.Floor(phase);double f=phase-slot;
            uint h=Mix(unchecked((uint)slot^(uint)seed^((uint)lane+1)*0x651eddu));
            double active=Smooth(Unit(h)-.04,Unit(h)+.04,chance);
            double start=.10+.24*Unit(h+1),width=Math.Min(.45,duration/Period(lane))*(.8+.4*Unit(h+2));
            double u=D.Clamp((f-start)/Math.Max(.03,width));
            return 16*u*u*(1-u)*(1-u)*active*(.7+.3*Unit(h+3));
        }
        // East/north velocity in X/Y, in reference-height m/s; cosmetic GPU motion
        // is allowed small floating-point differences across platforms.
        public static V Sample(WeatherFrame weather,int seed,V position,double radiusM,double seconds)
        {
            double a=Phase(seed,0,position,radiusM,seconds),b=Phase(seed,1,position,radiusM,seconds);
            double gust=Math.Max(Pulse(a,seed,0,weather.WindGustChance,weather.WindGustDurationS),.7*Pulse(b,seed,1,weather.WindGustChance*.75,weather.WindGustDurationS*1.3));
            double fine=Math.Sin(Phase(seed,2,position,radiusM,seconds)*2*Math.PI),flutter=Math.Sin(Phase(seed,3,position,radiusM,seconds)*2*Math.PI);
            double mean=Math.Sqrt(weather.WindEastMps*weather.WindEastMps+weather.WindNorthMps*weather.WindNorthMps);
            double east=mean>.0001?weather.WindEastMps/mean:1,north=mean>.0001?weather.WindNorthMps/mean:0;
            double speed=Math.Max(0,mean*(1+weather.WindTurbulence*.12*fine)+Math.Max(0,weather.GustMps-mean)*gust);
            double across=speed*weather.WindTurbulence*(.09*fine+.035*flutter);
            return new V(east*speed-north*across,north*speed+east*across,0);
        }
    }
}
