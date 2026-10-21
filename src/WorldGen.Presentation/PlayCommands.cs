using System;
using System.Globalization;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static class PlayCalendar
    {
        static readonly int[] Months={31,28,31,30,31,30,31,31,30,31,30,31};
        public static double Seconds(PlaySession play,long historySeconds)=>Math.Max(0,historySeconds+play.PendingHistorySeconds+play.CalendarOffsetSeconds);
        public static string Date(double seconds)
        {
            long days=(long)Math.Floor(Math.Max(0,seconds)/SimulationTime.Day),year=days/365+1;int day=(int)(days%365),month=0;
            while(day>=Months[month]){day-=Months[month];month++;}
            return year.ToString("D4",CultureInfo.InvariantCulture)+"-"+(month+1).ToString("D2")+"-"+(day+1).ToString("D2");
        }
        public static bool TryDate(string text,out double seconds)
        {
            seconds=0;var parts=text.Split('-');if(parts.Length!=3||!int.TryParse(parts[0],out int y)||!int.TryParse(parts[1],out int m)||!int.TryParse(parts[2],out int d)||y<1||y>9999999||m<1||m>12||d<1||d>Months[m-1])return false;
            seconds=((y-1L)*365+Months.Take(m-1).Sum()+d-1L)*SimulationTime.Day;return true;
        }
    }
    public sealed class PlayCommandResult
    {
        public bool Success;public string[] Lines;
        public PlayCommandResult(bool success,params string[] lines){Success=success;Lines=lines;}
    }
    public static class PlayCommands
    {
        public static readonly string[] Help={"/date [get | set YYYY-MM-DD | add days] — playable seasonal date (365-day years)","/help — list commands","/time [get | set day/night/noon/midnight/sunrise/sunset/HH:mm | add hours]",ExplorerNavigation.TeleportHelp,"/weather [get | auto | clear | cloudy | overcast | rain | downpour | storm | snow | blizzard | fog] — temporary weather test","/flora [species number] — inspect generated regional flowers, herbs, ferns and fungi"};
        public static string[] Complete(string input)
        {
            string[] options=input.StartsWith("/tp ",StringComparison.OrdinalIgnoreCase)?new[]{"/tp random","/tp random land"}:input.StartsWith("/weather ",StringComparison.OrdinalIgnoreCase)?WeatherControl.Modes.Select(m=>"/weather "+m).ToArray():input.StartsWith("/time set ",StringComparison.OrdinalIgnoreCase)?new[]{"/time set day","/time set midnight","/time set night","/time set noon","/time set sunrise","/time set sunset"}:new[]{"/date","/flora","/help","/time","/tp","/weather"};
            return options.Where(s=>s.StartsWith(input,StringComparison.OrdinalIgnoreCase)).OrderBy(s=>s,StringComparer.Ordinal).ToArray();
        }
        public static PlayCommandResult Execute(PlaySession play,long historySeconds,string input,double? longitude=null,double latitude=0,WeatherControl? weather=null)
        {
            double localLongitude=longitude??play.PrimeMeridianLongitude,localHour=play.LocalSolarHour(localLongitude);
            var words=(input??"").Trim().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);if(words.Length==0)return new PlayCommandResult(false,"Empty command. Try /help.");
            string command=words[0].ToLowerInvariant();
            if(command=="/help")return words.Length==1?new PlayCommandResult(true,Help):new PlayCommandResult(false,"Usage: /help");
            if(command=="/weather")
            {
                if(weather==null)return new PlayCommandResult(false,"Weather commands are available while exploring the world.");
                if(words.Length==1||words.Length==2&&words[1].Equals("get",StringComparison.OrdinalIgnoreCase))return new PlayCommandResult(true,"Weather: "+weather.Mode+(weather.Mode=="auto"?" (natural weather).":" (temporary test override)."));
                string mode=words.Length==2?words[1]:words.Length==3&&words[1].Equals("set",StringComparison.OrdinalIgnoreCase)?words[2]:"";
                if(!weather.Set(mode))return new PlayCommandResult(false,Help[4]);
                return new PlayCommandResult(true,weather.Mode=="auto"?"Natural weather restored.":"Testing "+weather.Mode+" weather. /weather auto restores natural weather; climate and water history are unchanged.");
            }
            if(command=="/time")
            {
                if(words.Length==1||words.Length==2&&words[1].Equals("get",StringComparison.OrdinalIgnoreCase))return new PlayCommandResult(true,"Local solar time: "+TimeText(localHour));
                double hour;
                if(words.Length==3&&words[1].Equals("set",StringComparison.OrdinalIgnoreCase))
                {
                    string value=words[2].ToLowerInvariant();hour=value=="day"?7:value=="night"?19:value=="noon"?12:value=="midnight"?0:value=="sunrise"?6:value=="sunset"?18:-1;
                    if(value=="sunrise"||value=="sunset")
                    {
                        double days=PlayCalendar.Seconds(play,historySeconds)/SimulationTime.Day,declination=Math.Asin(CelestialMechanics.SunEquatorial(days).Z),cosine=-Math.Tan(latitude*Math.PI/180)*Math.Tan(declination);
                        if(cosine< -1||cosine>1)return new PlayCommandResult(false,"No "+value+" occurs at this latitude on the current seasonal date.");
                        double halfDay=Math.Acos(cosine)*12/Math.PI;hour=12+(value=="sunrise"?-halfDay:halfDay);
                    }
                    if(hour<0){var hm=value.Split(':');if(hm.Length!=2||!int.TryParse(hm[0],out int h)||!int.TryParse(hm[1],out int m)||h<0||h>23||m<0||m>59)return new PlayCommandResult(false,"Use day, night, noon, midnight, sunrise, sunset or HH:mm.");hour=h+m/60d;}
                }
                else if(words.Length==3&&words[1].Equals("add",StringComparison.OrdinalIgnoreCase)&&double.TryParse(words[2],NumberStyles.Float,CultureInfo.InvariantCulture,out double hours)&&!double.IsNaN(hours)&&!double.IsInfinity(hours)&&Math.Abs(hours)<=1000000)hour=((localHour+hours)%24+24)%24;
                else return new PlayCommandResult(false,Help[2]);
                play.SetLocalSolarHour(hour,localLongitude);
                return new PlayCommandResult(true,"Local solar time set to "+TimeText(hour)+".");
            }
            if(command=="/date")
            {
                double now=PlayCalendar.Seconds(play,historySeconds),target;
                if(words.Length==1||words.Length==2&&words[1].Equals("get",StringComparison.OrdinalIgnoreCase))return new PlayCommandResult(true,"Date: "+PlayCalendar.Date(now)+" (playable calendar).");
                if(words.Length==3&&words[1].Equals("set",StringComparison.OrdinalIgnoreCase)&&PlayCalendar.TryDate(words[2],out target))target+=now%SimulationTime.Day;
                else if(words.Length==3&&words[1].Equals("add",StringComparison.OrdinalIgnoreCase)&&int.TryParse(words[2],NumberStyles.Integer,CultureInfo.InvariantCulture,out int days)&&Math.Abs((long)days)<=36500000)target=now+days*(double)SimulationTime.Day;
                else return new PlayCommandResult(false,Help[0],"Dates use a fixed 365-day calendar, without leap days.");
                if(target<0||target>=9999999d*SimulationTime.Year)return new PlayCommandResult(false,"Date outside the supported calendar.");
                play.CalendarOffsetSeconds=target-historySeconds-play.PendingHistorySeconds;
                return new PlayCommandResult(true,"Date set to "+PlayCalendar.Date(target)+". Recorded history is unchanged.");
            }
            return new PlayCommandResult(false,"Unknown command. Try /help.");
        }
        public static string TimeText(double hour){int minutes=(int)Math.Floor((hour*60+1e-7)%1440);return (minutes/60).ToString("D2")+":"+(minutes%60).ToString("D2");}
    }
}
