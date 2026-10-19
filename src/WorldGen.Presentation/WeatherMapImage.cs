using System;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Optional bounded-resolution meteorological overlay, independent of the base map cache.
    /// Samples exactly the explorer's weather provider. This shows coverage, not individual metre-scale billows.</summary>
    public static class WeatherMapImage
    {
        public static TerrainMapImage Build(World world,LocalTerrain terrain,TerrainMapViewport view,double days,string mode="auto",int width=128,int height=64,CancellationToken token=default,WorldWeather? weather=null)
        {
            if(width<1||height<1||width>256||height>128)throw new ArgumentOutOfRangeException(nameof(width));
            var control=new WeatherControl();if(!control.Set(mode))throw new ArgumentException("Invalid weather mode.");
            weather=weather??new WorldWeather(world,terrain);
            var image=new TerrainMapImage{Width=width,Height=height,View=view.Clone(),Rgba=new byte[width*height*4]};
            // A local terrain patch gives the same ground-height lapse correction when zoomed in.
            var center=view.Position(.5,.5);double extent=D.Angle(center,view.Position(0,0))*terrain.RadiusM*1.12+20;
            var patch=view.LongitudeSpan<1&&extent<100000?terrain.Patch(center,Math.Max(100,extent)):null;
            for(int y=0;y<height;y++)
            {
                token.ThrowIfCancellationRequested();
                for(int x=0;x<width;x++)
                {
                    var p=view.Position((x+.5)/width,1-(y+.5)/height);int id=terrain.NearestCell(p);
                    double ground=patch==null?Math.Max(0,world.ElevationM[id]):patch.Sample(p).ElevationM;
                    var f=control.Apply(weather.AtPosition(p).At(days,includeFog:false),ground);
                    int i=(y*width+x)*4;double dark=f.CloudDarkness;
                    image.Rgba[i]=(byte)(245-185*dark);image.Rgba[i+1]=(byte)(248-180*dark);image.Rgba[i+2]=(byte)(255-165*dark);
                    image.Rgba[i+3]=(byte)(255*Math.Min(.9,Math.Pow(f.CloudCover,1.8)*.83+f.CirrusCover*.12));
                }
            }
            return image;
        }
    }
}
