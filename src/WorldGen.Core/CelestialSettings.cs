using System;
using System.Linq;

namespace WorldGen.Core
{
    public enum StellarEnvironment { SolarNeighborhood, DenseGalacticField, StarCluster }
    public enum MoonSurface { Cratered, RustRock, Ice, DarkCaptured }
    [Serializable] public sealed class MoonSettings
    {
        public string Name="Moon";
        public MoonSurface Surface;
        public double RadiusKm=1737.4,OrbitRadiusKm=384400,DensityKgM3=3344,Albedo=.12;
        public double InclinationDegrees=5.145,NodeDegrees=125,LongitudeAtEpochDegrees=90;
        public V Tint=new V(.94,.95,1);
        public MoonSettings Copy()=>(MoonSettings)MemberwiseClone();
        public double MassKg {get{double r=RadiusKm*1000;return 4*D.Pi/3*r*r*r*DensityKgM3;}}
        public double OrbitalPeriodDays {get{double a=OrbitRadiusKm*1000;return D.Q(2*D.Pi*D.Sqrt(a*a*a/(6.67430e-11*(5.9722e24+MassKg)))/86400);}}
        public double SynodicPeriodDays=>D.Q(1/Math.Abs(1/OrbitalPeriodDays-1/365d));
        public void Validate()
        {
            void Range(double v,double min,double max){if(double.IsNaN(v)||double.IsInfinity(v)||v<min||v>max)throw new ArgumentException("Invalid moon configuration.");}
            if(string.IsNullOrWhiteSpace(Name)||Name.Length>48||!Enum.IsDefined(typeof(MoonSurface),Surface))throw new ArgumentException("Invalid moon identity.");
            Range(RadiusKm,2,3500);Range(OrbitRadiusKm,30000,700000);Range(DensityKgM3,1500,6000);Range(Albedo,.01,.95);
            Range(InclinationDegrees,0,60);Range(NodeDegrees,0,360);Range(LongitudeAtEpochDegrees,0,360);
            Range(Tint.X,.02,1);Range(Tint.Y,.02,1);Range(Tint.Z,.02,1);
            double roche=2.44*6371*Math.Pow(5514/DensityKgM3,1d/3);
            if(OrbitRadiusKm<=Math.Max(roche,6371+RadiusKm+1000))throw new ArgumentException("Moon orbit intersects the planet or lies within the fluid Roche limit.");
        }
    }
    [Serializable] public sealed class CelestialSettings
    {
        public int Version=1,Seed=481071;
        public StellarEnvironment Stars;
        public double Haze=1;
        public MoonSettings[] Moons={new MoonSettings()};
        public CelestialSettings Copy()=>new CelestialSettings{Version=Version,Seed=Seed,Stars=Stars,Haze=Haze,Moons=Moons.Select(m=>m.Copy()).ToArray()};
        public static readonly string[] Presets={"Earth-like","No moons","Rust moon","Twin moons","Ice moon","Captured pair"};
        public static CelestialSettings Preset(string name)
        {
            var s=new CelestialSettings();
            switch(name)
            {
                case "No moons":s.Moons=Array.Empty<MoonSettings>();break;
                case "Rust moon":s.Moons=new[]{new MoonSettings{Name="Ochre",Surface=MoonSurface.RustRock,RadiusKm=2100,OrbitRadiusKm=180000,Albedo=.17,Tint=new V(1,.42,.23),NodeDegrees=35}};break;
                case "Twin moons":s.Moons=new[]{new MoonSettings{Name="Pearl",RadiusKm=1800,OrbitRadiusKm=70000,Albedo=.32,InclinationDegrees=3,NodeDegrees=15,LongitudeAtEpochDegrees=80},new MoonSettings{Name="Ember",Surface=MoonSurface.RustRock,RadiusKm=1300,OrbitRadiusKm=150000,Albedo=.16,Tint=new V(1,.35,.18),InclinationDegrees=9,LongitudeAtEpochDegrees=115}};break;
                case "Ice moon":s.Moons=new[]{new MoonSettings{Name="Rime",Surface=MoonSurface.Ice,RadiusKm=1500,OrbitRadiusKm=220000,DensityKgM3=1900,Albedo=.65,Tint=new V(.73,.88,1)}};break;
                case "Captured pair":s.Moons=new[]{new MoonSettings{Name="Shard",Surface=MoonSurface.DarkCaptured,RadiusKm=24,OrbitRadiusKm=38000,DensityKgM3=1900,Albedo=.07,InclinationDegrees=18},new MoonSettings{Name="Cinder",Surface=MoonSurface.DarkCaptured,RadiusKm=45,OrbitRadiusKm=60000,DensityKgM3=2100,Albedo=.05,Tint=new V(.65,.48,.34),InclinationDegrees=35,LongitudeAtEpochDegrees=240}};break;
            }
            return s;
        }
        public void Validate()
        {
            if(Version!=1||!Enum.IsDefined(typeof(StellarEnvironment),Stars)||Seed<0||double.IsNaN(Haze)||double.IsInfinity(Haze)||Haze<0||Haze>5||Moons==null||Moons.Length>4)throw new ArgumentException("Invalid celestial settings.");
            foreach(var m in Moons){if(m==null)throw new ArgumentException("Missing moon.");m.Validate();}
            for(int a=0;a<Moons.Length;a++)for(int b=a+1;b<Moons.Length;b++)if(Math.Abs(Moons[a].OrbitRadiusKm-Moons[b].OrbitRadiusKm)<Moons[a].RadiusKm+Moons[b].RadiusKm)throw new ArgumentException("Moon orbits overlap. Separate their orbital distances.");
        }
    }
    public static class CelestialMechanics
    {
        public const double ObliquityDegrees=23.44,YearDays=365;
        public static double Fraction(double value)=>value-Math.Floor(value);
        public static V SunEquatorial(double days)
        {double l=2*Math.PI*(days/YearDays-.218),e=ObliquityDegrees*Math.PI/180;return new V(Math.Cos(l),Math.Sin(l)*Math.Cos(e),Math.Sin(l)*Math.Sin(e));}
        public static V MoonEcliptic(MoonSettings moon,double days)
        {
            double a=2*Math.PI*Fraction(days/moon.OrbitalPeriodDays)+moon.LongitudeAtEpochDegrees*Math.PI/180,n=moon.NodeDegrees*Math.PI/180,i=moon.InclinationDegrees*Math.PI/180;
            return new V(Math.Cos(n)*Math.Cos(a)-Math.Sin(n)*Math.Sin(a)*Math.Cos(i),Math.Sin(n)*Math.Cos(a)+Math.Cos(n)*Math.Sin(a)*Math.Cos(i),Math.Sin(a)*Math.Sin(i))*moon.OrbitRadiusKm;
        }
        public static V ToEquatorial(V p){double e=ObliquityDegrees*Math.PI/180;return new V(p.X,p.Y*Math.Cos(e)-p.Z*Math.Sin(e),p.Y*Math.Sin(e)+p.Z*Math.Cos(e));}
        public static double PhaseCycle(MoonSettings moon,double days)
        {var p=MoonEcliptic(moon,days);return Fraction((Math.Atan2(p.Y,p.X)-2*Math.PI*(days/YearDays-.218))/(2*Math.PI));}
        public static double NextPhase(MoonSettings moon,double days,double target)
        {
            // Circular-orbit longitude is monotonic for the supported inclinations.
            double phase=PhaseCycle(moon,days),gap=Fraction(target-phase);if(gap<1e-9)return days;
            double lo=days,hi=days+moon.SynodicPeriodDays*1.1;
            for(int k=0;k<32;k++){double mid=(lo+hi)/2,advance=Fraction(PhaseCycle(moon,mid)-phase);advance+=Math.Round((mid-days)/moon.SynodicPeriodDays-advance);if(advance<gap)lo=mid;else hi=mid;}
            return (lo+hi)/2;
        }
    }
}
