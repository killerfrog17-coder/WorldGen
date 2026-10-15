using System;
using System.Globalization;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static class SimulationClockView
    {
        public readonly struct PlaybackSlice
        {public readonly long Seconds;public readonly int MaximumEvents,MaximumGlobalWork;public PlaybackSlice(long seconds,int events,int globals=int.MaxValue){Seconds=seconds;MaximumEvents=events;MaximumGlobalWork=globals;}}
        public const int FastMaximumEvents=4096,FastMaximumGlobalWork=512;
        public static PlaybackSlice Publication(long now,long requested,double rate)
        {
            if(now<0||requested<1)throw new ArgumentOutOfRangeException(nameof(requested));
            // Display granularity must not cap simulation throughput. A fast
            // request can cross many empty years; the event budget bounds work
            // and the calendar still processes every due/coincident event.
            // Batch several populated years too: copying/hashing an entire
            // checkpoint after every year can cost more than its actual actors.
            // Slow clocks retain the original fine event publication cadence.
            bool yearOnly=rate>=20*(SimulationTime.Year/12.0);
            return new PlaybackSlice(requested,yearOnly?FastMaximumEvents:1,yearOnly?FastMaximumGlobalWork:int.MaxValue);
        }
        public static readonly string[] Units={"seconds","minutes","hours","days","weeks","months","years"};
        public static readonly long[] UnitSeconds={1,SimulationTime.Minute,SimulationTime.Hour,SimulationTime.Day,7*SimulationTime.Day,30*SimulationTime.Day,SimulationTime.Year};
        public static readonly double[] Rates={1,60,3600,43200,86400,604800,1209600,2592000,15768000,31536000,315360000,3153600000,15768000000,31536000000};
        static readonly string[] Months={"Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"};
        static readonly int[] MonthDays={31,28,31,30,31,30,31,31,30,31,30,31};
        public static double DefaultRate(int stepYears)=>5*stepYears*(double)SimulationTime.Year;
        // First recorded state institutions are the model's civilization trigger.
        // Retired states remain recorded, so collapse cannot reset the epoch.
        public static int EpochYear(PrehistoryState? state)
        {
            if(state?.History!=null){int first=int.MaxValue;foreach(var p in state.History.Polities)first=Math.Min(first,p.FoundedYear);return first==int.MaxValue?-1:first;}
            int legacy=int.MaxValue;if(state?.Society!=null)foreach(var c in state.Society.Cultures)if(c.Stage==SocietyStage.Civilization)legacy=Math.Min(legacy,c.StageYear);
            return legacy==int.MaxValue?-1:legacy;
        }
        public static string YearNumber(int year,int epochYear=-1)
        {long relative=(long)year-(epochYear>=0?epochYear:0);return relative<0?(-relative).ToString("N0",CultureInfo.InvariantCulture)+" before civilization":relative.ToString("N0",CultureInfo.InvariantCulture);}
        public static string Year(PrehistoryState? state,int year)=>"Year "+YearNumber(year,EpochYear(state));
        public static string Date(PrehistoryState state,double rate)=>Date(SimulationTime.Seconds(state),rate,EpochYear(state));
        public static string Rate(double seconds)
        {int unit=seconds>=SimulationTime.Year?6:seconds>=30*SimulationTime.Day?5:seconds>=7*SimulationTime.Day?4:seconds>=SimulationTime.Day?3:seconds>=SimulationTime.Hour?2:seconds>=60?1:0;return (seconds/UnitSeconds[unit]).ToString("0.###",CultureInfo.InvariantCulture)+" "+Units[unit]+"/s";}
        public static string Date(long seconds,double rate,int epochYear=-1)
        {
            if(seconds<0||double.IsNaN(rate)||double.IsInfinity(rate)||rate<=0)throw new ArgumentOutOfRangeException();
            string date="Year "+YearNumber(checked((int)(seconds/SimulationTime.Year)),epochYear);
            if(rate>=20*(SimulationTime.Year/12.0))return date;
            long within=seconds%SimulationTime.Year;int day=(int)(within/SimulationTime.Day),month=0;
            while(day>=MonthDays[month]){day-=MonthDays[month];month++;}date+=" · "+Months[month];
            if(rate>=20*SimulationTime.Day)return date;date+=" "+(day+1);
            if(rate>=20*SimulationTime.Hour)return date;date+=" · "+(within%SimulationTime.Day/SimulationTime.Hour).ToString("00",CultureInfo.InvariantCulture);
            if(rate<20*SimulationTime.Minute)date+=":"+(within%SimulationTime.Hour/SimulationTime.Minute).ToString("00",CultureInfo.InvariantCulture);
            else date+="h";
            return date;
        }
    }
    // Presentation-only wall-clock credit. Simulation decisions never read this.
    // A slow machine has bounded pending work and genuinely slower progress;
    // no simulation events are dropped to fabricate the chosen speed.
    public sealed class SimulationPlayback
    {
        double credit;public double Rate {get;private set;}=SimulationClockView.DefaultRate(100);
        public bool Manual {get;private set;}
        public void SetRate(double rate,bool manual=true)
        {if(double.IsNaN(rate)||double.IsInfinity(rate)||rate<.0001||rate>1000*SimulationTime.Year)throw new ArgumentOutOfRangeException(nameof(rate));Rate=rate;Manual=manual;credit=0;}
        public void Auto(int stepYears){SetRate(SimulationClockView.DefaultRate(stepYears),false);}
        public void Phase(int stepYears){if(!Manual&&Rate!=SimulationClockView.DefaultRate(stepYears))Auto(stepYears);}
        public void Change(int direction)
        {double next=direction<0?SimulationClockView.Rates[0]:SimulationClockView.Rates[SimulationClockView.Rates.Length-1];if(direction<0){foreach(double r in SimulationClockView.Rates)if(r<Rate)next=r;}else{foreach(double r in SimulationClockView.Rates)if(r>Rate){next=r;break;}}SetRate(next);}
        public void Accumulate(double realSeconds,bool running)
        {if(!running){credit=0;return;}if(realSeconds>0&&double.IsFinite(realSeconds))credit=Math.Min(Math.Max(1,Rate*.5),credit+Rate*Math.Min(.25,realSeconds));}
        public long Pending=>(long)Math.Floor(credit);
        public void Consume(long actualSeconds){credit=Math.Max(0,credit-Math.Max(0,actualSeconds));}
        public void Reset(){credit=0;}
    }
}
