using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    bool mapRivers;Texture2D riverMapTexture;TerrainMapViewport riverMapViewport;
    Task<TerrainMapImage> riverMapJob;CancellationTokenSource riverMapCancellation;
    int riverMapRevision,riverMapJobRevision=-1;double riverMapDue;
    public bool RiverMapReady=>riverMapTexture!=null&&riverMapRevision==riverMapJobRevision&&riverMapJob==null;
    void SetMapRivers(bool enabled){if(mapRivers==enabled)return;mapRivers=enabled;InvalidateRiverMap();}
    void InvalidateRiverMap(){riverMapRevision++;riverMapDue=Time.realtimeSinceStartupAsDouble+.15;riverMapCancellation?.Cancel();}
    void UpdateRiverMapWork()
    {
        if(riverMapJob!=null&&riverMapJob.IsCompleted)
        {
            if(riverMapJob.IsFaulted){error=riverMapJob.Exception.GetBaseException().Message;debugOpen=true;}
            else if(!riverMapJob.IsCanceled&&riverMapJobRevision==riverMapRevision&&mapRivers)
            {
                var image=riverMapJob.Result;var texture=new Texture2D(image.Width,image.Height,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                texture.LoadRawTextureData(image.Rgba);texture.Apply();if(riverMapTexture!=null)Destroy(riverMapTexture);riverMapTexture=texture;riverMapViewport=image.View;
            }
            riverMapJob=null;
        }
        if(!mapOpen||!mapRivers||sourceWorld==null||terrain==null||riverMapJob!=null||riverMapJobRevision==riverMapRevision||Time.realtimeSinceStartupAsDouble<riverMapDue)return;
        riverMapCancellation?.Dispose();riverMapCancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);var token=riverMapCancellation.Token;
        var viewport=mapViewport.Clone();var source=sourceWorld;var local=terrain;riverMapJobRevision=riverMapRevision;
        riverMapJob=Task.Run(()=>WaterwaysMapImage.Build(source,local,viewport,token:token),token);
    }
    void DrawRiverMap()
    {
        if(!mapRivers||riverMapTexture==null)return;GUI.BeginGroup(mapRect);
        double w=mapRect.width*riverMapViewport.LongitudeSpan/mapViewport.LongitudeSpan,h=mapRect.height*riverMapViewport.LatitudeSpan/mapViewport.LatitudeSpan;
        double x=mapRect.width*(.5+TerrainMapViewport.Wrap(riverMapViewport.Longitude-mapViewport.Longitude)/mapViewport.LongitudeSpan)-w*.5;
        double y=mapRect.height*(.5-(riverMapViewport.Latitude-mapViewport.Latitude)/mapViewport.LatitudeSpan)-h*.5;
        for(int wrap=-1;wrap<=1;wrap++)GUI.DrawTexture(new Rect((float)(x+wrap*360/mapViewport.LongitudeSpan*mapRect.width),(float)y,(float)w,(float)h),riverMapTexture,ScaleMode.StretchToFill,true);
        GUI.EndGroup();
    }
    public void TestRiverMap(bool enabled){if(!mapOpen){mapOpen=true;InvalidateMap();}SetMapRivers(enabled);}
    public void CaptureRiverMap(string path){if(!RiverMapReady)throw new InvalidOperationException("River map not ready");System.IO.File.WriteAllBytes(path,riverMapTexture.EncodeToPNG());}
    void DisposeRiverMap(){riverMapCancellation?.Cancel();riverMapCancellation?.Dispose();if(riverMapTexture!=null)Destroy(riverMapTexture);}
}
