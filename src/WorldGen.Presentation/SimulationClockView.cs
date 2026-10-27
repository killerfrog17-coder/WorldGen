using System;
using System.Globalization;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static class SimulationClockView
    {
        public readonly struct PlaybackSlice
        {public readonly long Seconds;public readonly int MaximumEvents,MaximumGlobalWork;public PlaybackSlice(long seconds,int events,int globals=int.MaxValue){Seconds=seconds;MaximumEvents=events;MaximumGlobalWork=globals;}}
        public const int FastMaximumEvents=16384,FastMaximumGlobalWork=2048;
        public static PlaybackSlice Publication(long now,long requested,double rate)
        {
            if(now<0||requested<1)throw new ArgumentOutOfRangeException(nameof(requested));
            // Display granularity must not cap simulation throughput. A fast
            // request can cross many empty years; the event budget bounds work
            // and the calendar still processes every due/coincident event.
            // Batch several populated years too: copying/hashing an entire
            // checkpoint after every year can cost more than its actual actors.
            // Slow clocks publish short DATE intervals, not one private actor
            // event. A modern world may have thousands of unrelated events in
            // a year: repainting/resealing after every one destroys throughput.
            bool yearOnly=rate>=20*(SimulationTime.Year/12.0);
            long seconds=yearOnly?requested:Math.Min(requested,Math.Max(1,(long)Math.Floor(rate/5)));
            return new PlaybackSlice(seconds,FastMaximumEvents,FastMaximumGlobalWork);
        }
        public static readonly string[] Units={"seconds","minutes","hours","days","weeks","months","years"};
        public static readonly long[] UnitSeconds={1,SimulationTime.Minute,SimulationTime.Hour,SimulationTime.Day,7*SimulationTime.Day,30*SimulationTime.Day,SimulationTime.Year};
        public static readonly double[] Rates={1,60,3600,43200,86400,604800,1209600,2592000,15768000,
            SimulationTime.Year,2d*SimulationTime.Year,5d*SimulationTime.Year,10d*SimulationTime.Year,20d*SimulationTime.Year,50d*SimulationTime.Year,
            100d*SimulationTime.Year,200d*SimulationTime.Year,500d*SimulationTime.Year,1000d*SimulationTime.Year,2000d*SimulationTime.Year,5000d*SimulationTime.Year,10000d*SimulationTime.Year};
        static readonly string[] Months={"Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"};
        static readonly int[] MonthDays={31,28,31,30,31,30,31,31,30,31,30,31};
        public static double DefaultRate(int stepYears)=>5*stepYears*(double)SimulationTime.Year;
        public static double EraRate(int era)=>(era<=2?2000:era==3?100:era==4?5:era<8?2:1)*(double)SimulationTime.Year;
        // Playback milestones are deliberately separate from observer era names/music.
        // Current histories retain culture/state records and the first proto date,
        // so a collapse does not send Auto back to prehistoric speeds.
        public static double AutoRate(PrehistoryState? state,int era)
        {
            if(era>=4)return EraRate(era);
            if(state?.History?.Polities.Length>0)return 10d*SimulationTime.Year;
            var regional=state?.History?.RegionalTraditions;
            if(regional!=null)
            {
                if(regional.FirstProtoYear>=0)return 100d*SimulationTime.Year;
            }
            else if(era==3||state?.Society?.Phase>=SocietyStage.EmbryonicCivilization)
                return 100d*SimulationTime.Year;
            return (state?.Society?.Cultures.Length>0||state?.History?.Cultures.Length>0?500d:2000d)*SimulationTime.Year;
        }
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
        {int unit=seconds>=SimulationTime.Year?6:seconds>=30*SimulationTime.Day?5:seconds>=7*SimulationTime.Day?4:seconds>=SimulationTime.Day?3:seconds>=SimulationTime.Hour?2:seconds>=60?1:0;double value=seconds/UnitSeconds[unit];return value.ToString("0.###",CultureInfo.InvariantCulture)+" "+(value==1?Units[unit].TrimEnd('s'):Units[unit])+"/s";}
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
        double credit;public double Rate {get;private set;}=SimulationClockView.EraRate(2);
        public bool Manual {get;private set;}
        public void SetRate(double rate,bool manual=true)
        {if(double.IsNaN(rate)||double.IsInfinity(rate)||rate<.0001||rate>10000d*SimulationTime.Year)throw new ArgumentOutOfRangeException(nameof(rate));Rate=rate;Manual=manual;credit=0;}
        public void AutoEra(int era)=>SetRate(SimulationClockView.EraRate(era),false);
        public void Era(int era){if(!Manual&&Rate!=SimulationClockView.EraRate(era))AutoEra(era);}
        public void AutoWorld(PrehistoryState? state,int era)=>SetRate(SimulationClockView.AutoRate(state,era),false);
        public void World(PrehistoryState? state,int era){if(!Manual&&Rate!=SimulationClockView.AutoRate(state,era))AutoWorld(state,era);}
        public void HoldRate()=>SetRate(Rate,true);
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
