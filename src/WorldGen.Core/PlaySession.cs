using System;

namespace WorldGen.Core
{
    /// <summary>Save/game rules, deliberately separate from the immutable generation recipe.</summary>
    [Serializable]
    public sealed class PlaySettings
    {
        public double HorizontalScale=.1,RealMinutesPerDay=60,CalendarDaysPerDay=1,StructureDensity=1;
        public PlaySettings Copy()=>(PlaySettings)MemberwiseClone();
        public void Validate()
        {
            void Range(double v,double low,double high,string name){if(double.IsNaN(v)||double.IsInfinity(v)||v<low||v>high)throw new ArgumentException("Invalid "+name+".");}
            Range(HorizontalScale,.01,2,"horizontal scale");Range(RealMinutesPerDay,1,10080,"day length");
            Range(CalendarDaysPerDay,1,365,"calendar compression");Range(StructureDensity,0,100,"structure density");
        }
    }
    [Serializable]
    public sealed class PlaySession
    {
        public string Version="play-session-1";
        public PlaySettings Settings=new PlaySettings();
        public double SolarDays,PendingHistorySeconds;
        public bool HasPlayer,Flying;
        public V PlayerPosition;
        public double Heading,Pitch,HeightAboveGround=1.65;
        public PlaySession Copy(){var s=(PlaySession)MemberwiseClone();s.Settings=Settings.Copy();return s;}
        public static PlaySession Legacy()=>new PlaySession{Settings=new PlaySettings{HorizontalScale=1}};
        public void Validate()
        {
            if(Version!="play-session-1"||Settings==null)throw new ArgumentException("Unsupported play session.");Settings.Validate();
            bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
            if(!Finite(SolarDays)||SolarDays<0||!Finite(PendingHistorySeconds)||PendingHistorySeconds<0||PendingHistorySeconds>1e12||
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
        public double SolarHour=>((SolarDays+.375)%1)*24; // A new game starts at09:00 local solar time.
        public double StructureCountFactor=>Settings.HorizontalScale*Settings.HorizontalScale*Settings.StructureDensity;
    }
}
