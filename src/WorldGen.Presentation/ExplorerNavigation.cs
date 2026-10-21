using System;
using System.Globalization;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static class ExplorerNavigation
    {
        public const string TeleportHelp="/tp <latitude> <longitude> [height above surface] | /tp random [land] — coordinates use the F3/map meridian";
        public static double Bearing(LocalTerrainFrame frame,V position,double heading)
        {
            double a=heading*Math.PI/180;V direction=frame.East*Math.Sin(a)+frame.North*Math.Cos(a);
            double lon=Math.Atan2(position.Y,position.X);V east=new V(-Math.Sin(lon),Math.Cos(lon),0),north=V.Cross(position,east).Unit;
            return (Math.Atan2(V.Dot(direction,east),V.Dot(direction,north))*180/Math.PI+360)%360;
        }
        public static string Compass(double bearing)=>new[]{"N","NE","E","SE","S","SW","W","NW"}[(int)Math.Floor(((bearing%360+360)%360+22.5)/45)%8];
        public static bool Coordinates(string[] words,double prime,out V target,out double? height)
        {
            target=default;height=null;
            bool Number(string s,out double v)=>double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out v)&&!double.IsNaN(v)&&!double.IsInfinity(v);
            if(words.Length<3||words.Length>4||!Number(words[1],out double lat)||!Number(words[2],out double lon)||lat< -90||lat>90||lon< -180||lon>180)return false;
            if(words.Length==4){if(!Number(words[3],out double h)||h<1.65||h>1000000)return false;height=h;}
            target=TerrainMapViewport.FromDegrees(lat,TerrainMapViewport.Wrap(lon+prime));return true;
        }
        public static V RandomPoint(ref Rng rng)
        {
            double z=rng.Unit()*2-1,lon=rng.Unit()*Math.PI*2,r=Math.Sqrt(Math.Max(0,1-z*z));
            return new V(r*Math.Cos(lon),r*Math.Sin(lon),z);
        }
    }
}
