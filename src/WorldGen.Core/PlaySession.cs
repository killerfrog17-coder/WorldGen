using System;

namespace WorldGen.Core
{
    /// <summary>Save/game rules, deliberately separate from the immutable generation recipe.</summary>
    [Serializable]
    public sealed class PlaySettings
    {
        public double HorizontalScale=.1,RealMinutesPerDay=60,CalendarDaysPerDay=1,StructureDensity=1;
        [System.Runtime.Serialization.OptionalField] public CelestialSettings Sky=new CelestialSettings();
        [System.Runtime.Serialization.OptionalField] public double MoonVisualExaggeration=1,SunVisualExaggeration=1;
        [System.Runtime.Serialization.OnDeserializing] void InitializeOptionalSky(System.Runtime.Serialization.StreamingContext context){Sky=null;MoonVisualExaggeration=SunVisualExaggeration=1;}
        public PlaySettings Copy(){var s=(PlaySettings)MemberwiseClone();s.Sky=Sky?.Copy();return s;}
        public void Validate()
        {
            void Range(double v,double low,double high,string name){if(double.IsNaN(v)||double.IsInfinity(v)||v<low||v>high)throw new ArgumentException("Invalid "+name+".");}
            Range(HorizontalScale,.01,2,"horizontal scale");Range(RealMinutesPerDay,1,10080,"day length");
            Range(CalendarDaysPerDay,1,365,"calendar compression");Range(StructureDensity,0,100,"structure density");
            Range(MoonVisualExaggeration,.01,20,"moon visual exaggeration");Range(SunVisualExaggeration,.01,20,"sun visual exaggeration");
            Sky?.Validate();
        }
    }
    [Serializable]
    public sealed class PlaySession
    {
        public string Version="play-session-1";
        public PlaySettings Settings=new PlaySettings();
        public double SolarDays,PendingHistorySeconds;
        [System.Runtime.Serialization.OptionalField] public double CalendarOffsetSeconds;
        [System.Runtime.Serialization.OptionalField] public int MeridianVersion;
        [System.Runtime.Serialization.OptionalField] public double PrimeMeridianLongitude;
        public bool HasPlayer,Flying;
        public V PlayerPosition;
        public double Heading,Pitch,HeightAboveGround=1.65;
        public PlaySession Copy(){var s=(PlaySession)MemberwiseClone();s.Settings=Settings.Copy();return s;}
        public static PlaySession Legacy()=>new PlaySession{Settings=new PlaySettings{HorizontalScale=1,Sky=CelestialSettings.Preset("No moons")}};
        public void Validate()
        {
            if(Version!="play-session-1"||Settings==null)throw new ArgumentException("Unsupported play session.");Settings.Validate();
            bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
            if(MeridianVersion<0||MeridianVersion>1||!Finite(PrimeMeridianLongitude)||PrimeMeridianLongitude< -180||PrimeMeridianLongitude>=180||!Finite(SolarDays)||SolarDays<0||!Finite(CalendarOffsetSeconds)||Math.Abs(CalendarOffsetSeconds)>1e15||!Finite(PendingHistorySeconds)||PendingHistorySeconds<0||PendingHistorySeconds>1e12||
                !Finite(Heading)||!Finite(Pitch)||Math.Abs(Pitch)>85||!Finite(HeightAboveGround)||HeightAboveGround<0||HeightAboveGround>1e7||
                HasPlayer&&(!Finite(PlayerPosition.X)||!Finite(PlayerPosition.Y)||!Finite(PlayerPosition.Z)||Math.Abs(PlayerPosition.Length-1)>1e-5))throw new ArgumentException("Invalid play session state.");
        }
        // Visual solar cycles and historical/seasonal time have intentionally independent rates.
        // Fractional seconds survive saves; committed historical time remains in the existing event calendar.
        public void Accumulate(double realSeconds)
        {
            if(double.IsNaN(realSeconds)||double.IsInfinity(realSeconds)||realSeconds<0)throw new ArgumentOutOfRangeException(nameof(realSeconds));
            double days=realSeconds/(Settings.RealMinutesPerDay*60);SolarDays+=days;PendingHistorySeconds+=days*86400*Settings.CalendarDaysPerDay;
        }
        public void Consume(double seconds)=>PendingHistorySeconds=Math.Max(0,PendingHistorySeconds-seconds);
        public double SolarHour=>((SolarDays+.375)%1)*24; // Reference time at the saved prime meridian; initially09:00.
        public double OfficialLongitude(double longitude)=>WrapDegrees(longitude-PrimeMeridianLongitude);
        public double LocalSolarHour(double longitude)=>WrapHours(SolarHour+OfficialLongitude(longitude)/15);
        public void SetLocalSolarHour(double hour,double longitude)
        {double reference=WrapHours(hour-OfficialLongitude(longitude)/15);SolarDays=Math.Floor(SolarDays)+((reference/24-.375+1)%1);}
        static double WrapDegrees(double angle)=>angle-360*Math.Floor((angle+180)/360);
        static double WrapHours(double hour){double value=hour-24*Math.Floor(hour/24);return value<1e-10||value>24-1e-10?0:value;}
        public double StructureCountFactor=>Settings.HorizontalScale*Settings.HorizontalScale*Settings.StructureDensity;
    }
}
