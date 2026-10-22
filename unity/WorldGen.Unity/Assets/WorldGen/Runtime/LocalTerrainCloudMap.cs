using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    bool mapClouds;Texture2D cloudMapTexture;TerrainMapViewport cloudMapViewport;
    Task<TerrainMapImage> cloudMapJob;CancellationTokenSource cloudMapCancellation;
    int cloudMapRevision,cloudMapJobRevision,cloudMapWeatherRevision=-1;long cloudMapHour=long.MinValue;double cloudMapDue;
    public bool CloudMapReady=>cloudMapTexture!=null&&cloudMapRevision==cloudMapJobRevision&&cloudMapJob==null;
    void SetMapClouds(bool enabled)
    {
        if(mapClouds==enabled)return;mapClouds=enabled;InvalidateCloudMap();
        if(!enabled)cloudMapCancellation?.Cancel();
    }
    void InvalidateCloudMap(){cloudMapRevision++;cloudMapDue=Time.realtimeSinceStartupAsDouble+.15;cloudMapCancellation?.Cancel();}
    void UpdateCloudMapWork()
    {
        if(cloudMapJob!=null&&cloudMapJob.IsCompleted)
        {
            if(cloudMapJob.IsFaulted){error=cloudMapJob.Exception.GetBaseException().Message;debugOpen=true;}
            else if(!cloudMapJob.IsCanceled&&cloudMapJobRevision==cloudMapRevision&&mapClouds)
            {
                var image=cloudMapJob.Result;var texture=new Texture2D(image.Width,image.Height,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                texture.LoadRawTextureData(image.Rgba);texture.Apply();if(cloudMapTexture!=null)Destroy(cloudMapTexture);cloudMapTexture=texture;cloudMapViewport=image.View;
            }
            cloudMapJob=null;
        }
        if(!mapOpen||!mapClouds||sourceWorld==null||terrain==null)return;
        long hour=(long)Math.Floor(weatherDays*24);
        if(cloudMapWeatherRevision!=WeatherControl.Revision){cloudMapWeatherRevision=WeatherControl.Revision;InvalidateCloudMap();}
        if(hour!=cloudMapHour&&cloudMapJob==null&&Time.realtimeSinceStartupAsDouble>=cloudMapDue)
        {cloudMapHour=hour;cloudMapRevision++;}
        if(cloudMapJob!=null||cloudMapJobRevision==cloudMapRevision||Time.realtimeSinceStartupAsDouble<cloudMapDue)return;
        cloudMapCancellation?.Dispose();cloudMapCancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);var token=cloudMapCancellation.Token;
        var viewport=mapViewport.Clone();var source=sourceWorld;var local=terrain;var weather=worldWeather;double days=weatherDays;string mode=WeatherControl.Mode;cloudMapJobRevision=cloudMapRevision;
        cloudMapDue=Time.realtimeSinceStartupAsDouble+3;
        cloudMapJob=Task.Run(()=>WeatherMapImage.Build(source,local,viewport,days,mode,token:token,weather:weather),token);
    }
    void DrawCloudMap()
    {
        if(!mapClouds||cloudMapTexture==null)return;
        GUI.BeginGroup(mapRect);double w=mapRect.width*cloudMapViewport.LongitudeSpan/mapViewport.LongitudeSpan,h=mapRect.height*cloudMapViewport.LatitudeSpan/mapViewport.LatitudeSpan;
        double x=mapRect.width*(.5+TerrainMapViewport.Wrap(cloudMapViewport.Longitude-mapViewport.Longitude)/mapViewport.LongitudeSpan)-w*.5;
        double y=mapRect.height*(.5-(cloudMapViewport.Latitude-mapViewport.Latitude)/mapViewport.LatitudeSpan)-h*.5;
        for(int wrap=-1;wrap<=1;wrap++)GUI.DrawTexture(new Rect((float)(x+wrap*360/mapViewport.LongitudeSpan*mapRect.width),(float)y,(float)w,(float)h),cloudMapTexture,ScaleMode.StretchToFill,true);
        GUI.EndGroup();
    }
    public void TestCloudMap(bool enabled){if(!mapOpen){mapOpen=true;InvalidateMap();}SetMapClouds(enabled);}
    public void CaptureCloudMap(string path,bool previewOverBase=false)
    {
        if(!CloudMapReady)throw new InvalidOperationException("Cloud map not ready.");
        if(!previewOverBase){System.IO.File.WriteAllBytes(path,cloudMapTexture.EncodeToPNG());return;}
        // Diagnostic composite only, not part of the per-frame map path.
        var baseImage=ReadMapTexture();var pixels=baseImage.GetPixels32();int width=baseImage.width,height=baseImage.height;Destroy(baseImage);
        for(int y=0;y<height;y++)for(int x=0;x<width;x++){Color cloud=cloudMapTexture.GetPixelBilinear((x+.5f)/width,(y+.5f)/height);pixels[y*width+x]=Color.Lerp(pixels[y*width+x],cloud,cloud.a);}
        var image=new Texture2D(width,height,TextureFormat.RGBA32,false);image.SetPixels32(pixels);image.Apply();System.IO.File.WriteAllBytes(path,image.EncodeToPNG());Destroy(image);
    }
    void DisposeCloudMap(){cloudMapCancellation?.Cancel();cloudMapCancellation?.Dispose();if(cloudMapTexture!=null)Destroy(cloudMapTexture);}
}
