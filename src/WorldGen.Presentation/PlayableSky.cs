using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public readonly struct SkyBody
    {
        public readonly V Direction,Color,LightDirection;public readonly double AngularRadiusDegrees,Illuminance,IlluminatedFraction,PhaseCycle,DistanceKm;
        public readonly int Surface,MoonIndex;public readonly string Name;
        public SkyBody(V direction,V color,double radius,double light,V lightDirection=default,int surface=0,string name="Sun",double fraction=1,double cycle=0,double distance=149597870.7,int moonIndex=-1)
        {Direction=direction;Color=color;AngularRadiusDegrees=radius;Illuminance=light;LightDirection=lightDirection;Surface=surface;Name=name;IlluminatedFraction=fraction;PhaseCycle=cycle;DistanceKm=distance;MoonIndex=moonIndex;}
    }
    public sealed class PlayableSkyFrame
    {
        public SkyBody[] Bodies=Array.Empty<SkyBody>();
        public V Zenith,Horizon,Ambient,StarRight,StarUp,StarForward,NightAirGlow;
        public double SunAltitudeDegrees,Daylight,StarVisibility,Twilight,NightIlluminanceLux,StellarGlow,GalacticGlow,ObserverAltitudeM;
    }
    public static class PlayableSky
    {
        public static V SunDirection(double latitude,double hour,double year)
        {
            V sun=CelestialMechanics.SunEquatorial(year*365);return Local(sun,latitude,SiderealAngle(sun,hour)).Unit;
        }
        static double SiderealAngle(V sun,double hour)=>Math.Atan2(sun.Y,sun.X)+(hour-12)*Math.PI/12;
        static V Local(V p,double latitude,double angle)
        {double lat=latitude*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle);return new V(-s*p.X+c*p.Y,Math.Cos(lat)*(c*p.X+s*p.Y)+Math.Sin(lat)*p.Z,-Math.Sin(lat)*(c*p.X+s*p.Y)+Math.Cos(lat)*p.Z);}
        static double Smooth(double a,double b,double value){double t=Math.Max(0,Math.Min(1,(value-a)/(b-a)));return t*t*(3-2*t);}
        static V Mix(V a,V b,double t)=>a*(1-t)+b*t;
        public static PlayableSkyFrame Sample(double latitude,double hour,double year,CelestialSettings? settings=null,double? calendarDays=null,double observerAltitudeM=0,double moonDiskScale=1,double sunDiskScale=1,double horizonDipDegrees=0)
        {
            V sunEq=CelestialMechanics.SunEquatorial(year*365);double angle=SiderealAngle(sunEq,hour),lat=latitude*Math.PI/180;V sun=Local(sunEq,latitude,angle).Unit;
            double altitude=Math.Asin(sun.Y)*180/Math.PI;
            double effectiveAltitude=altitude+Math.Max(0,horizonDipDegrees);
            double daylight=Smooth(-10,12,effectiveAltitude),twilight=(1-Smooth(3,18,Math.Abs(effectiveAltitude)))*Smooth(-18,-3,effectiveAltitude);
            V right=Local(new V(1,0,0),latitude,angle),pole=Local(new V(0,0,1),latitude,angle),forward=Local(new V(0,1,0),latitude,angle);
            V sunColor=Mix(new V(1,.35,.12),new V(1,.96,.87),Smooth(0,25,altitude));
            int moons=settings?.Moons.Length??0;var bodies=new SkyBody[moons+1];bodies[0]=new SkyBody(sun,sunColor,Math.Min(10,.2666*sunDiskScale),1.45*Smooth(-1,12,effectiveAltitude));
            V observer=new V(Math.Cos(lat)*Math.Cos(angle),Math.Cos(lat)*Math.Sin(angle),Math.Sin(lat))*(6371+Math.Max(0,observerAltitudeM)/1000),moonGlow=default;
            double lux=0;
            for(int i=0;i<moons;i++)
            {
                var m=settings!.Moons[i];double days=calendarDays??year*365;V position=CelestialMechanics.ToEquatorial(CelestialMechanics.MoonEcliptic(m,days)),delta=position-observer;
                double distance=delta.Length;V direction=Local(delta.Unit,latitude,angle).Unit,light=Local((sunEq*149597870.7-position).Unit,latitude,angle).Unit;
                double cosine=Math.Max(-1,Math.Min(1,V.Dot(direction*(-1),light))),alpha=Math.Acos(cosine),phase=(1+cosine)*.5;
                double lambert=(Math.Sin(alpha)+(Math.PI-alpha)*cosine)/Math.PI;
                // Display magnification never changes the orbit, phase or light.
                double irradiance=.25*Math.Pow(m.RadiusKm/1737.4,2)*Math.Pow(384400/distance,2)*(m.Albedo/.12)*lambert*Smooth(0,.12,direction.Y+Math.Sin(horizonDipDegrees*Math.PI/180));
                lux+=irradiance*(.2126*m.Tint.X+.7152*m.Tint.Y+.0722*m.Tint.Z);
                double renderLight=Math.Min(1.45,irradiance*.08)*(1-.98*daylight);moonGlow+=m.Tint*renderLight;
                bodies[i+1]=new SkyBody(direction,m.Tint,Math.Min(60,Math.Asin(m.RadiusKm/distance)*180/Math.PI*moonDiskScale),renderLight,light,1+(int)m.Surface,m.Name,phase,CelestialMechanics.PhaseCycle(m,days),distance,i);
            }
            Array.Sort(bodies,1,moons,System.Collections.Generic.Comparer<SkyBody>.Create((a,b)=>b.DistanceKm.CompareTo(a.DistanceKm)));
            double stellar=settings?.Stars==StellarEnvironment.DenseGalacticField?.012:settings?.Stars==StellarEnvironment.StarCluster?.022:.001;
            double night=1-daylight,starVisibility=(1-Smooth(-16,-3,effectiveAltitude))/(1+lux*.9);
            return new PlayableSkyFrame{
                Bodies=bodies,SunAltitudeDegrees=altitude,Daylight=daylight,Twilight=twilight,NightIlluminanceLux=lux+stellar,StellarGlow=stellar,GalacticGlow=settings?.Stars==StellarEnvironment.DenseGalacticField?7:settings?.Stars==StellarEnvironment.StarCluster?2:1,ObserverAltitudeM=observerAltitudeM,NightAirGlow=(new V(moonGlow.X*.35,moonGlow.Y*.55,moonGlow.Z)*.025+new V(.3,.38,.55)*stellar*.02)*night,
                StarVisibility=starVisibility,
                Zenith=Mix(new V(.003,.006,.019),new V(.10,.30,.63),daylight),
                Horizon=Mix(Mix(new V(.012,.018,.038),new V(.66,.78,.87),daylight),new V(.69,.37,.24),twilight*.48),
                Ambient=Mix(new V(.008,.011,.022)+new V(.7,.8,1)*stellar*night,new V(.10,.13,.18),daylight)+moonGlow*.35,StarRight=right,StarUp=pole,StarForward=forward};
        }
        public static string Season(double year)
        {
            double phase=CelestialMechanics.Fraction(year-.218)*4;int quarter=(int)phase;double progress=(phase-quarter)*100;
            string[] seasons={"Northern spring","Northern summer","Northern autumn","Northern winter"},from={"vernal equinox","summer solstice","autumnal equinox","winter solstice"};
            return seasons[quarter]+" · "+progress.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+"% from "+from[quarter]+" to "+from[(quarter+1)%4];
        }
        public static string PhaseName(double cycle)=>cycle<.025||cycle>.975?"new":cycle<.225?"waxing crescent":cycle<.275?"first quarter":cycle<.475?"waxing gibbous":cycle<.525?"full":cycle<.725?"waning gibbous":cycle<.775?"last quarter":"waning crescent";
    }
}
