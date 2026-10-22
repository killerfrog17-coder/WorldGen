using System;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    ProgressiveTerrainMap mapStream;RenderTexture mapSurface;bool mapRefining;int shownMapRevision=-1,mapTargetWidth;
    string mapProgress="";
    void UpdateProgressiveMap()
    {
        if(!mapOpen){if(mapRefining){mapStream?.Cancel();mapRefining=false;}return;}
        if(terrain==null)return;
        if(mapStream==null)mapStream=new ProgressiveTerrainMap(terrain,sourceWorld,cancellation.Token);
        if(mapDirty&&Time.realtimeSinceStartupAsDouble>=mapDue)
        {
            mapDirty=false;mapRefining=true;MapBuildCount++;
            int pixels=(int)Math.Max(32,mapRect.width>0?mapRect.width:Math.Min(1150,Screen.width-64));
            mapTargetWidth=Math.Min(SystemInfo.maxTextureSize,Mathf.NextPowerOfTwo(pixels));
            mapProgress="Preview…";mapStream.Begin(mapRevision,mapViewport,mapLayer,mapTargetWidth);
        }
        // Upload bounded small tiles; copying GPU regions avoids reuploading a
        // multi-megapixel texture every time a single tile becomes available.
        for(int n=0;n<12&&mapStream.TryTake(out var update);n++)
        {
            if(update.Revision!=mapRevision)continue;
            if(update.Error.Length>0){error="Map: "+update.Error;mapProgress=error;mapRefining=false;continue;}
            if(update.Complete){mapRefining=false;mapProgress="Sharp · "+mapTargetWidth+" px";continue;}
            var image=update.Image;if(image==null)continue;
            if(mapTexture==null||mapTexture.width!=image.Width||mapTexture.height!=image.Height)
            {if(mapTexture!=null)Destroy(mapTexture);mapTexture=new Texture2D(image.Width,image.Height,TextureFormat.RGBA32,false);}
            mapTexture.LoadRawTextureData(image.Rgba);mapTexture.Apply(false,false);
            bool newView=shownMapRevision!=mapRevision;
            if(newView||mapSurface==null||mapSurface.width!=update.CanvasWidth||mapSurface.height!=update.CanvasHeight)
            {
                var replacement=new RenderTexture(new RenderTextureDescriptor(update.CanvasWidth,update.CanvasHeight){graphicsFormat=mapTexture.graphicsFormat,depthBufferBits=0,msaaSamples=1,mipCount=1});replacement.Create();
                replacement.filterMode=TerrainMapSampling.Continuous(update.Layer)?FilterMode.Bilinear:FilterMode.Point;
                if(newView||mapSurface==null)Graphics.Blit(mapTexture,replacement);else Graphics.Blit(mapSurface,replacement);
                if(mapSurface!=null){mapSurface.Release();Destroy(mapSurface);}mapSurface=replacement;
            }
            Graphics.CopyTexture(mapTexture,0,0,0,0,image.Width,image.Height,mapSurface,0,0,update.X,update.Y);
            shownMapViewport=image.View;shownMapRevision=update.Revision;
            mapProgress=update.Preview?"Preview · refining terrain…":"Refining · "+update.CanvasWidth+" / "+mapTargetWidth+" px";
        }
    }
    Texture2D ReadMapTexture()
    {
        var image=new Texture2D(mapSurface.width,mapSurface.height,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
        try{RenderTexture.active=mapSurface;image.ReadPixels(new Rect(0,0,mapSurface.width,mapSurface.height),0,0);image.Apply();return image;}
        finally{RenderTexture.active=previous;}
    }
    void DisposeProgressiveMap(){mapStream?.Dispose();if(mapSurface!=null){mapSurface.Release();Destroy(mapSurface);}}
}
