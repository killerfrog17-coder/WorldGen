using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    // A body state is separate from the atmosphere. Later orbital providers can
    // supply additional luminous or nonluminous bodies without replacing the clock.
    public readonly struct SkyBody
    {
        public readonly V Direction,Color;public readonly double AngularRadiusDegrees,Illuminance;
        public SkyBody(V direction,V color,double radius,double light){Direction=direction;Color=color;AngularRadiusDegrees=radius;Illuminance=light;}
    }
    public sealed class PlayableSkyFrame
    {
        public SkyBody[] Bodies=Array.Empty<SkyBody>();
        public V Zenith,Horizon,Ambient,StarRight,StarUp,StarForward;
        public double SunAltitudeDegrees,Daylight,StarVisibility,Twilight;
    }
    public static class PlayableSky
    {
        public static V SunDirection(double latitude,double hour,double year)
        {
            double lat=latitude*Math.PI/180,declination=23.44*Math.PI/180*Math.Sin(2*Math.PI*(year-.218)),angle=(hour-12)*Math.PI/12;
            return new V(-Math.Cos(declination)*Math.Sin(angle),Math.Sin(lat)*Math.Sin(declination)+Math.Cos(lat)*Math.Cos(declination)*Math.Cos(angle),Math.Cos(lat)*Math.Sin(declination)-Math.Sin(lat)*Math.Cos(declination)*Math.Cos(angle)).Unit;
        }
        static double Smooth(double a,double b,double value){double t=Math.Max(0,Math.Min(1,(value-a)/(b-a)));return t*t*(3-2*t);}
        static V Mix(V a,V b,double t)=>a*(1-t)+b*t;
        public static PlayableSkyFrame Sample(double latitude,double hour,double year)
        {
            V sun=SunDirection(latitude,hour,year);double altitude=Math.Asin(sun.Y)*180/Math.PI;
            double daylight=Smooth(-10,12,altitude),twilight=(1-Smooth(3,18,Math.Abs(altitude)))*Smooth(-18,-3,altitude);
            double lat=latitude*Math.PI/180,angle=(hour*15+year*360)*Math.PI/180;
            V pole=new V(0,Math.Sin(lat),Math.Cos(lat)),zero=new V(0,Math.Cos(lat),-Math.Sin(lat)),east=new V(1,0,0);
            V right=east*Math.Cos(angle)+zero*Math.Sin(angle),forward=zero*Math.Cos(angle)-east*Math.Sin(angle);
            V sunColor=Mix(new V(1,.35,.12),new V(1,.96,.87),Smooth(0,25,altitude));
            return new PlayableSkyFrame{
                Bodies=new[]{new SkyBody(sun,sunColor,.2666,1.45*Smooth(-1,12,altitude))},SunAltitudeDegrees=altitude,Daylight=daylight,Twilight=twilight,
                StarVisibility=1-Smooth(-16,-3,altitude),
                Zenith=Mix(new V(.003,.006,.019),new V(.10,.30,.63),daylight),
                Horizon=Mix(Mix(new V(.012,.018,.038),new V(.66,.78,.87),daylight),new V(.69,.37,.24),twilight*.48),
                Ambient=Mix(new V(.008,.011,.022),new V(.10,.13,.18),daylight),StarRight=right,StarUp=pole,StarForward=forward};
        }
    }
}
