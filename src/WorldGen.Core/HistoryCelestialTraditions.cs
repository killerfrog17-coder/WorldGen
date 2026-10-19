using System;
using System.Linq;

namespace WorldGen.Core
{
    public enum CelestialCalendarBasis { SeasonalSolar, Lunar, Lunisolar }
    public enum CelestialMoonMeaning { None, Renewal, Ancestors, Navigation, CosmicOrder, PairedBalance }
    public enum CelestialStarMeaning { None, SeasonalMarkers, AncestorFires, DivineAssembly }
    public enum CelestialFestival { LocalCommemoration, SolarTurning, LunarObservance }
    [Serializable] public sealed class CelestialTradition
    {
        public int Id,Parent=-1,CalendarMoon=-1,SacredMoon=-1,MonthsPerCycle,Quarter;
        public CelestialCalendarBasis Basis;
        public CelestialMoonMeaning MoonMeaning;
        public CelestialStarMeaning StarMeaning;
        public CelestialFestival Festival;
        public double LunationDays,CalendarCycleDays=365,FestivalPhase;
        public bool Intercalation;
        public CelestialTradition Copy()=>(CelestialTradition)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalSkyContext
    {
        public int Version=1,SinceYear;
        public CelestialSettings Settings=new CelestialSettings();
        // Append-only profiles are shared on ordinary copies, never edited in place.
        public CelestialTradition[] Faiths=Array.Empty<CelestialTradition>(),Cultures=Array.Empty<CelestialTradition>();
        public HistoricalSkyContext Copy()=>new HistoricalSkyContext{Version=Version,SinceYear=SinceYear,Settings=Settings.Copy(),Faiths=Faiths,Cultures=Cultures};
        public static CelestialTradition Root(CelestialSettings sky,int id,Func<int,double> roll,bool agricultural)
        {
            var p=new CelestialTradition{Id=id,Quarter=(int)(roll(0)*4),FestivalPhase=roll(1)<.5?0:.5};
            if(sky.Moons.Length>0)
            {
                int moon=(int)(roll(2)*sky.Moons.Length);double period=sky.Moons[moon].SynodicPeriodDays;
                if(period>=2&&period<150&&roll(3)<.72)
                {
                    p.CalendarMoon=moon;p.LunationDays=period;p.MonthsPerCycle=Math.Max(1,(int)Math.Floor(365/period));
                    p.Basis=agricultural?CelestialCalendarBasis.Lunisolar:CelestialCalendarBasis.Lunar;
                    p.Intercalation=p.Basis==CelestialCalendarBasis.Lunisolar;p.CalendarCycleDays=p.Intercalation?365:p.MonthsPerCycle*period;
                }
                double angular=sky.Moons[moon].RadiusKm/sky.Moons[moon].OrbitRadiusKm/.00452,prominence=D.Q(Math.Min(1,angular*angular*sky.Moons[moon].Albedo/.12),1e6);
                if(roll(5)<.25+.35*prominence){p.SacredMoon=moon;p.MoonMeaning=(CelestialMoonMeaning)(1+(int)(roll(6)*(sky.Moons.Length>1?5:4)));p.Festival=CelestialFestival.LunarObservance;}
            }
            if(p.Festival!=CelestialFestival.LunarObservance&&roll(7)<.45)p.Festival=CelestialFestival.SolarTurning;
            if(roll(8)<(sky.Stars==StellarEnvironment.SolarNeighborhood?.22:.5))p.StarMeaning=(CelestialStarMeaning)(1+(int)(roll(9)*3));
            return p;
        }
        public void Validate(HistoryState h,int year)
        {
            Settings.Validate();if(Version!=1||SinceYear<0||SinceYear>year||Faiths.Length!=h.Religions.Length||Cultures.Length!=h.Cultures.Length)throw new ArgumentException("Invalid celestial tradition alignment.");
            void Profile(CelestialTradition p,int id)
            {
                if(p==null||p.Id!=id||p.Parent>=id||p.Parent< -1||p.CalendarMoon< -1||p.CalendarMoon>=Settings.Moons.Length||p.SacredMoon< -1||p.SacredMoon>=Settings.Moons.Length||!Enum.IsDefined(typeof(CelestialCalendarBasis),p.Basis)||!Enum.IsDefined(typeof(CelestialFestival),p.Festival)||!Enum.IsDefined(typeof(CelestialMoonMeaning),p.MoonMeaning)||!Enum.IsDefined(typeof(CelestialStarMeaning),p.StarMeaning)||p.Quarter<0||p.Quarter>3||p.FestivalPhase!=0&&p.FestivalPhase!=.5||p.CalendarCycleDays<=0||double.IsNaN(p.CalendarCycleDays)||p.Basis!=CelestialCalendarBasis.SeasonalSolar&&(p.CalendarMoon<0||p.LunationDays<=0)||p.MoonMeaning!=CelestialMoonMeaning.None&&p.SacredMoon<0)throw new ArgumentException("Invalid celestial observance profile.");
                if(p.CalendarMoon>=0&&Math.Abs(p.LunationDays-Settings.Moons[p.CalendarMoon].SynodicPeriodDays)>1e-7)throw new ArgumentException("Calendar does not match its recorded moon.");
                if(double.IsInfinity(p.CalendarCycleDays)||p.MonthsPerCycle<0||p.Intercalation!=(p.Basis==CelestialCalendarBasis.Lunisolar)||p.Festival==CelestialFestival.LunarObservance&&(p.SacredMoon<0||p.MoonMeaning==CelestialMoonMeaning.None)||p.Basis!=CelestialCalendarBasis.SeasonalSolar&&p.MonthsPerCycle<1)throw new ArgumentException("Invalid celestial calendar or festival.");
            }
            for(int i=0;i<Faiths.Length;i++)Profile(Faiths[i],i);for(int i=0;i<Cultures.Length;i++)Profile(Cultures[i],i);
        }
    }
    public sealed partial class Prehistory
    {
        public PrehistoryState ConfigureCelestialHistory(PrehistoryState source,CelestialSettings settings)
        {
            Validate(source);settings.Validate();if(source.History==null||source.History.Sky!=null)return source;
            var s=source.Copy();s.History.Sky=new HistoricalSkyContext{SinceYear=s.Years,Settings=settings.Copy()};
            for(int i=0;i<s.History.Religions.Length;i++)AppendCelestialFaith(s,i);
            for(int i=0;i<s.History.Cultures.Length;i++)AppendCelestialCulture(s,i);
            s.Seal();return s;
        }
        void AppendCelestialFaith(PrehistoryState s,int faith)
        {
            var sky=s.History.Sky;if(sky==null||faith<sky.Faiths.Length)return;if(faith!=sky.Faiths.Length)throw new ArgumentException("Celestial faith order.");
            var r=s.History.Religions[faith];int parent=r.ParentId;
            var p=parent>=0?sky.Faiths[parent].Copy():HistoricalSkyContext.Root(sky.Settings,faith,k=>new Rng(World.Recipe.Seed,"celestial-faith-1-"+k,faith).Unit(),s.Society.Cultures[r.CultureId].Farming>150);
            p.Id=faith;p.Parent=parent;sky.Faiths=sky.Faiths.Concat(new[]{p}).ToArray();
        }
        void AppendCelestialCulture(PrehistoryState s,int culture)
        {
            var sky=s.History.Sky;if(sky==null||culture<sky.Cultures.Length)return;if(culture!=sky.Cultures.Length)throw new ArgumentException("Celestial culture order.");
            var c=s.History.Cultures[culture];int parent=c.ParentCultureId;
            var p=parent>=0?sky.Cultures[parent].Copy():HistoricalSkyContext.Root(sky.Settings,culture,k=>new Rng(World.Recipe.Seed,"celestial-civil-calendar-1-"+k,culture).Unit(),s.Society.Cultures[culture].Farming>150);
            p.Id=culture;p.Parent=parent;sky.Cultures=sky.Cultures.Concat(new[]{p}).ToArray();
        }
    }
}
