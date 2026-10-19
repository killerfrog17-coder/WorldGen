using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class WorldMeridianTests
{
    public static void Run(Action<bool,string> check)
    {
        World Grid(Func<double,double,bool> land)
        {
            var cells=new List<Cell>();var ocean=new List<bool>();
            for(int lat=-85;lat<90;lat+=10)for(int lon=-175;lon<180;lon+=10)
            {cells.Add(new Cell{Id=cells.Count,Position=TerrainMapViewport.FromDegrees(lat,lon),AreaM2=(long)(6371000d*6371000*Math.Pow(10*Math.PI/180,2)*Math.Cos(lat*Math.PI/180))});ocean.Add(!land(lat,lon));}
            return new World{Surface=new Surface{Cells=cells.ToArray()},ElevationM=ocean.Select(o=>o?-100:100).ToArray(),Hydrology=new HydrologyResult{OceanConnected=ocean.ToArray()}};
        }
        var water=WorldMeridian.For(Grid((lat,lon)=>false));check(water.PrimeLongitude==0&&water.LandCrossingDegrees==0,"Ocean world has a stable zero prime meridian");
        var land=WorldMeridian.For(Grid((lat,lon)=>true));check(land.PrimeLongitude==0&&land.LandCrossingDegrees==180,"Oceanless world has a deterministic fallback");
        var single=WorldMeridian.For(Grid((lat,lon)=>Math.Abs(lat)<55&&Math.Abs(lon)<35));
        check(single.LandCrossingDegrees==0&&Math.Abs(single.PrimeLongitude)<2,"A continent is centered opposite the broadest empty ocean seam");
        var wrap=WorldMeridian.For(Grid((lat,lon)=>Math.Abs(lat)<55&&Math.Abs(lon)>145));
        check(wrap.LandCrossingDegrees==0&&Math.Abs(Math.Abs(wrap.PrimeLongitude)-180)<2,"Seam search wraps across the stored longitude boundary");
        var polarWorld=Grid((lat,lon)=>lat< -70||Math.Abs(lat)<55&&Math.Abs(lon)<35);var polar=WorldMeridian.For(polarWorld);
        check(polar.LandCrossingDegrees>0&&polar.LandCrossingDegrees<40&&Math.Abs(polar.PrimeLongitude)<2,"Unavoidable polar land does not force a cut through the main continent");
        check(ReferenceEquals(polar,WorldMeridian.For(polarWorld)),"Immutable geography seam analysis is cached, not repeated per frame");
        var play=new PlaySession();WorldMeridian.Apply(play,polarWorld);double prime=play.PrimeMeridianLongitude;
        foreach(double longitude in new[]{-179.9,-100d,0,33.3,179.9})foreach(double hour in new[]{0d,6,12,18,23.99})
        {
            play.SetLocalSolarHour(hour,longitude);
            check(Math.Abs(play.LocalSolarHour(longitude)-hour)<1e-8,"Time setters target the supplied location");
            check(Math.Abs(((play.LocalSolarHour(longitude+180)-hour+24)%24)-12)<1e-8,"Opposite longitudes differ by exactly twelve hours");
            check(Math.Abs(((play.LocalSolarHour(longitude+15)-hour+24)%24)-1)<1e-8,"Eastward fifteen degrees advances local solar time one hour");
            var cold=WorldJson.Read<PlaySession>(WorldJson.Write(play));cold.Validate();check(cold.PrimeMeridianLongitude==prime&&cold.LocalSolarHour(longitude)==play.LocalSolarHour(longitude),"Prime meridian and local clock survive save/load");
        }
        PlayCommands.Execute(play,0,"/time set noon",75);check(Math.Abs(play.LocalSolarHour(75)-12)<1e-8&&PlayCommands.Execute(play,0,"/time",75).Lines[0].Contains("12:00"),"Commands set/query local rather than reference time");
        PlayCommands.Execute(play,0,"/time add 3",75);check(Math.Abs(play.LocalSolarHour(75)-15)<1e-8,"Time addition also operates at the player's longitude");
        play.SetLocalSolarHour(12,75);double season=.218;
        check(PlayableSky.Sample(20,play.LocalSolarHour(75),season).SunAltitudeDegrees>60&&PlayableSky.Sample(20,play.LocalSolarHour(-105),season).SunAltitudeDegrees< -60,"One shared clock produces noon and midnight on opposite sides");
        var old=new PlaySession{HasPlayer=true,PlayerPosition=TerrainMapViewport.FromDegrees(35,70)};double oldHour=old.SolarHour;WorldMeridian.Apply(old,polarWorld);
        check(Math.Abs(old.LocalSolarHour(70)-oldHour)<1e-8,"An old explorer adopts the prime meridian without jumping its saved local time");
        check(play.OfficialLongitude(prime)==0&&Math.Abs(play.OfficialLongitude(prime+180))==180,"Official longitude is relative to the chosen prime meridian");
        var grid=SurfaceMesh.Grid(30,polarWorld);int point=5*360*6+6*180*6+3;
        check(Math.Abs(TerrainMapViewport.Wrap(Math.Atan2(-grid.Lines[point+2],grid.Lines[point])*180/Math.PI-prime))<1e-4,"The displayed zero-longitude grid line follows the official prime meridian");
        var legacy=WorldJson.Write(new PlaySession()).Replace("\"MeridianVersion\":0,","").Replace("\"PrimeMeridianLongitude\":0,","");
        var legacyPlay=WorldJson.Read<PlaySession>(legacy);legacyPlay.Validate();check(legacyPlay.MeridianVersion==0&&legacyPlay.PrimeMeridianLongitude==0,"Pre-meridian gameplay sidecars remain readable");
    }
}
