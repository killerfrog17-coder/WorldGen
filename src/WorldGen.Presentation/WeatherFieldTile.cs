using System;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>A disposable view window into fixed global nodes, not a player-centred weather system.</summary>
    public sealed class WeatherFieldTile
    {
        public int Longitude,Latitude,Width,Height;public double Days,MinBase=double.MaxValue,MaxTop;public V Center;
        public float[] Conditions=Array.Empty<float>(),Layers=Array.Empty<float>();
        public bool Contains(double latitude,double longitude,double margin=4)
        {double x=(longitude-Longitude+720)%360,bottom=Latitude<=-90?-90:Latitude+margin,top=Latitude+Height-1>=90?90:Latitude+Height-1-margin;return (Width==361||x>=margin&&x<=Width-1-margin)&&latitude>=bottom&&latitude<=top;}
        public static WeatherFieldTile Build(WorldWeather weather,V center,double days,double radiusM,CancellationToken token=default)
        {
            TerrainMapViewport.Degrees(center,out double lat,out double lon);double angle=Math.Min(70,120000/Math.Max(100000,radiusM)*180/Math.PI+6);
            int halfLat=(int)Math.Ceiling(angle),halfLon=(int)Math.Ceiling(angle/Math.Max(.08,Math.Cos((Math.Abs(lat)+angle)*Math.PI/180)));
            if(Math.Abs(lat)+angle>=88)halfLon=180;
            var tile=new WeatherFieldTile{Center=center,Days=days,Longitude=(int)Math.Floor(lon)-Math.Min(180,halfLon),Latitude=Math.Max(-90,(int)Math.Floor(lat)-halfLat)};
            tile.Width=Math.Min(361,halfLon*2+1);tile.Height=Math.Min(181,tile.Latitude+halfLat*2+1>90?91-tile.Latitude:halfLat*2+1);
            tile.Conditions=new float[tile.Width*tile.Height*4];tile.Layers=new float[tile.Conditions.Length];
            for(int y=0;y<tile.Height;y++)
            {
                token.ThrowIfCancellationRequested();
                for(int x=0;x<tile.Width;x++)
                {
                    var f=weather.Node(tile.Longitude+x,tile.Latitude+y,days);int i=(y*tile.Width+x)*4;
                    tile.Conditions[i]=(float)f.CloudCover;tile.Conditions[i+1]=(float)f.CirrusCover;tile.Conditions[i+2]=(float)f.PrecipitationMmPerHour;tile.Conditions[i+3]=(float)f.Thunderstorm;
                    tile.Layers[i]=(float)f.CloudBaseM;tile.Layers[i+1]=(float)f.CloudThicknessM;tile.Layers[i+2]=(float)f.CirrusBaseM;tile.Layers[i+3]=(float)f.SnowWaterMmPerHour;
                    tile.MinBase=Math.Min(tile.MinBase,f.CloudBaseM);tile.MaxTop=Math.Max(tile.MaxTop,f.CloudBaseM+f.CloudThicknessM);
                }
            }
            return tile;
        }
    }
}
