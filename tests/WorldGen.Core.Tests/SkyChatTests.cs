using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class SkyChatTests
{
    public static void Run(Action<bool,string> check)
    {
        foreach(double latitude in new[]{-89d,-60,-30,0,30,60,89})foreach(double year in new[]{0d,.218,.468,.718})foreach(double hour in new[]{0d,6,9,12,18,23.99})
        {
            var sky=PlayableSky.Sample(latitude,hour,year);check(sky.Bodies.Length==1&&sky.Bodies[0].AngularRadiusDegrees>0,"Exactly one sun, no generated moons");
            check(Math.Abs(sky.Bodies[0].Direction.Length-1)<3e-9,"Sun direction stays normalized within V quantization");
            check(Math.Abs(sky.StarRight.Length-1)<3e-9&&Math.Abs(V.Dot(sky.StarRight,sky.StarUp))<3e-9&&Math.Abs(V.Dot(sky.StarRight,sky.StarForward))<3e-9,"Stellar frame is orthonormal within V quantization at every latitude/time");
            check(sky.Daylight>=0&&sky.Daylight<=1&&sky.StarVisibility>=0&&sky.StarVisibility<=1&&sky.Bodies[0].Illuminance>=0,"Light/visibility are bounded");
            check(Math.Abs(sky.SunAltitudeDegrees-ExplorationMusicContext.SolarAltitude(latitude,hour,year))<1e-8,"Music and sky use the same solar altitude");
        }
        var noon=PlayableSky.Sample(0,12,.218);var midnight=PlayableSky.Sample(0,0,.218);
        check(noon.StarVisibility==0&&midnight.StarVisibility==1&&midnight.Bodies[0].Illuminance==0,"Stars only appear as sunlight fades, without a nighttime sun light");
        var play=new PlaySession{PendingHistorySeconds=.25};const long history=123456789;
        check(PlayCommands.Help.SequenceEqual(PlayCommands.Help.OrderBy(s=>s,StringComparer.Ordinal)),"Help is alphabetically sorted");
        var help=PlayCommands.Execute(play,history,"/help");check(help.Success&&help.Lines.Length==3,"Help exposes only the implemented command families");
        foreach(var pair in new[]{("day",7d),("night",19d),("noon",12d),("midnight",0d),("sunrise",6d),("sunset",18d),("23:59",23+59/60d)})
        {check(PlayCommands.Execute(play,history,"/time set "+pair.Item1).Success&&Math.Abs(play.SolarHour-pair.Item2)<1e-8,"Set time: "+pair.Item1);}
        PlayCommands.Execute(play,history,"/time set midnight");check(PlayCommands.Execute(play,history,"/time add -1").Success&&Math.Abs(play.SolarHour-23)<1e-8,"Time adjustment wraps backward safely");
        string before=WorldJson.Write(play);foreach(string bad in new[]{"/time set 24:00","/time set 2:60","/time add NaN","/time add Infinity","/time explode noon","/time set","/help extra","/date set 2024-02-29","/date set 2024-13-01","/date set -3-1-1","/date add -999999999","/unknown"})
        {check(!PlayCommands.Execute(play,history,bad).Success&&WorldJson.Write(play)==before,"Invalid command leaves clock/state untouched: "+bad);}
        double solar=play.SolarDays,pending=play.PendingHistorySeconds;
        check(PlayCommands.Execute(play,history,"/date set 0042-06-21").Success&&PlayCalendar.Date(PlayCalendar.Seconds(play,history))=="0042-06-21","Date can be set without replaying historical events");
        check(play.SolarDays==solar&&play.PendingHistorySeconds==pending,"Date changes neither the solar phase nor pending historical credit");
        PlayCommands.Execute(play,history,"/date add 365");check(PlayCalendar.Date(PlayCalendar.Seconds(play,history))=="0043-06-21","Calendar uses the existing 365-day year consistently");
        var cold=WorldJson.Read<PlaySession>(WorldJson.Write(play));cold.Validate();check(cold.CalendarOffsetSeconds==play.CalendarOffsetSeconds,"Saved calendar adjustment survives a cold roundtrip");
        string legacy=WorldJson.Write(new PlaySession()).Replace("\"CalendarOffsetSeconds\":0,","");check(!legacy.Contains("CalendarOffsetSeconds"),"Legacy envelope fixture omits only the new optional member");
        var old=WorldJson.Read<PlaySession>(legacy);old.Validate();check(old.CalendarOffsetSeconds==0,"Old gameplay envelopes retain an unshifted calendar");
        check(PlayCommands.Complete("/d").SequenceEqual(new[]{"/date"})&&PlayCommands.Complete("/time set n").SequenceEqual(new[]{"/time set night","/time set noon"}),"Command completion is scoped and sorted");
        foreach(int year in new[]{1,42,9999,200001,9999999})foreach(string date in new[]{"01-01","02-28","06-21","12-31"})
        {string text=year.ToString("D4")+"-"+date;check(PlayCalendar.TryDate(text,out double seconds)&&PlayCalendar.Date(seconds)==text,"Large simulation dates roundtrip: "+text);}
        PlayCommands.Execute(play,history,"/date set 9999999-12-31");before=WorldJson.Write(play);
        check(!PlayCommands.Execute(play,history,"/date add 1").Success&&WorldJson.Write(play)==before,"Calendar addition cannot exceed the maximum accepted year");
        cold.CalendarOffsetSeconds=double.NaN;bool invalid=false;try{cold.Validate();}catch(ArgumentException){invalid=true;}check(invalid,"Nonfinite saved calendar offsets are rejected");
    }
}
