using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    bool debugOpen,mapOpen,paused,mapDirty,mapDragging,mapLayerMenu; double viewDistance=200000,mapDue;
    Vector2 mapDown,mapPrevious,mapLayerScroll; Rect mapRect;
    TerrainMapViewport mapViewport=new TerrainMapViewport(),shownMapViewport;
    sealed class MapResult{public TerrainMapImage Image;public Layer Layer;public float[] Palette;}
    readonly Dictionary<Layer,float[]> mapPalettes=new Dictionary<Layer,float[]>();Layer mapLayer=Layer.Satellite;
    Texture2D mapTexture;Task<MapResult> mapJob;CancellationTokenSource mapCancellation;int mapRevision,mapJobRevision;
    public bool DebugVisible=>debugOpen;
    public bool MapVisible=>mapOpen;
    public bool Paused=>paused;
    public bool MapReady=>mapTexture!=null&&!mapDirty&&mapJob==null;
    double MaximumViewDistance=>terrain==null?250000:Math.Min(250000,terrain.RadiusM*HorizontalScale*.3);
    void CaptureMouse(){if(passiveSkyCheck)return;Cursor.lockState=mapOpen||paused||chatOpen?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=mapOpen||paused||chatOpen;}
    void OnApplicationFocus(bool focus){if(focus&&!closed)CaptureMouse();}
    void HandleDebugKeys()
    {
        var k=ActiveKeyboard;if(k==null)return;
        if(k.f3Key.wasPressedThisFrame)debugOpen=!debugOpen;
        if(k.mKey.wasPressedThisFrame&&!paused){mapOpen=!mapOpen;if(mapOpen)InvalidateMap();CaptureMouse();}
        if(k.escapeKey.wasPressedThisFrame){if(mapOpen){mapOpen=false;mapLayerMenu=false;}else paused=!paused;CaptureMouse();}
    }
    void InvalidateMap(){mapRevision++;mapDirty=true;mapDue=Time.realtimeSinceStartupAsDouble+.12;mapCancellation?.Cancel();}
    void UpdateMapWork()
    {
        if(mapJob!=null&&mapJob.IsCompleted)
        {
            if(mapJob.IsFaulted){error=mapJob.Exception.GetBaseException().Message;debugOpen=true;}
            else if(!mapJob.IsCanceled&&mapJobRevision==mapRevision)
            {
                var result=mapJob.Result;var image=result.Image;if(result.Palette!=null)mapPalettes[result.Layer]=result.Palette;
                var texture=new Texture2D(image.Width,image.Height,TextureFormat.RGBA32,false);texture.LoadRawTextureData(image.Rgba);texture.Apply();texture.filterMode=FilterMode.Bilinear;
                if(mapTexture!=null)Destroy(mapTexture);mapTexture=texture;shownMapViewport=image.View;
            }
            mapJob=null;
        }
        if(!mapOpen||!mapDirty||mapJob!=null||terrain==null||Time.realtimeSinceStartupAsDouble<mapDue)return;
        mapDirty=false;mapCancellation?.Dispose();mapCancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);var token=mapCancellation.Token;var viewport=mapViewport.Clone();mapJobRevision=mapRevision;
        var selected=mapLayer;mapPalettes.TryGetValue(selected,out var cached);var source=sourceWorld;
        mapJob=Task.Run(()=>{var palette=cached??(source==null?null:GlobeView.CellValues(source,selected,token:token).Colors);return new MapResult{Layer=selected,Palette=palette,Image=TerrainMapImage.Build(terrain,viewport,768,384,token,selected,palette)};},token);
    }
    public void DrawViewSettings()
    {
        GUILayout.Label("View distance  "+(viewDistance/1000).ToString("F0")+" km");
        double distance=GUILayout.HorizontalSlider((float)viewDistance,(float)Math.Min(30000,MaximumViewDistance*.1),(float)MaximumViewDistance);
        if(Math.Abs(distance-viewDistance)>100){viewDistance=distance;SetFog();RefreshWanted();}
        GUILayout.Label("Flight speed  "+motion.FlySpeed.ToString("F0")+" m/s");motion.FlySpeed=Math.Exp(GUILayout.HorizontalSlider((float)Math.Log(motion.FlySpeed),(float)Math.Log(5),(float)Math.Log(20000)));
    }
    void OnGUI()
    {
        if(MenuSkin!=null)GUI.skin=MenuSkin;
        GUI.color=GUI.contentColor=GUI.backgroundColor=Color.white;
        DrawChat();
        if(debugOpen)
        {
            string text=terrain==null?"Preparing terrain…":DebugText();var style=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=false,richText=false};style.normal.textColor=Color.black;
            GUI.Label(new Rect(14,14,Screen.width-28,170),text,style);style.normal.textColor=Color.white;GUI.Label(new Rect(12,12,Screen.width-28,170),text,style);
        }
        if(paused){if(drawPause!=null)drawPause();else{GUILayout.BeginArea(new Rect(Screen.width/2f-130,Screen.height/2f-70,260,140),GUI.skin.box);if(GUILayout.Button("Resume"))Resume();if(GUILayout.Button("World simulation"))Close();GUILayout.EndArea();}return;}
        if(!mapOpen||terrain==null)return;
        float width=Math.Min(Screen.width-64,Math.Min(1150,(Screen.height-150)*2));mapRect=new Rect((Screen.width-width)/2,72,width,width*.5f);
        GUI.Box(new Rect(mapRect.x-12,mapRect.y-48,mapRect.width+24,mapRect.height+108),"");GUI.Label(new Rect(mapRect.x,mapRect.y-38,mapRect.width-250,30),"WORLD MAP");
        if(GUI.Button(new Rect(mapRect.xMax-235,mapRect.y-40,190,30),GlobeView.LayerName(mapLayer)+"  ▾"))mapLayerMenu=!mapLayerMenu;
        if(GUI.Button(new Rect(mapRect.xMax-37,mapRect.y-40,37,30),"×")){mapOpen=false;CaptureMouse();}
        if(mapTexture!=null)
        {
            GUI.BeginGroup(mapRect);double w=mapRect.width*shownMapViewport.LongitudeSpan/mapViewport.LongitudeSpan,h=mapRect.height*shownMapViewport.LatitudeSpan/mapViewport.LatitudeSpan;
            double x=mapRect.width*(.5+TerrainMapViewport.Wrap(shownMapViewport.Longitude-mapViewport.Longitude)/mapViewport.LongitudeSpan)-w*.5;
            double y=mapRect.height*(.5-(shownMapViewport.Latitude-mapViewport.Latitude)/mapViewport.LatitudeSpan)-h*.5;
            for(int wrap=-1;wrap<=1;wrap++)GUI.DrawTexture(new Rect((float)(x+wrap*360/mapViewport.LongitudeSpan*mapRect.width),(float)y,(float)w,(float)h),mapTexture,ScaleMode.StretchToFill,false);GUI.EndGroup();
        }
        else GUI.Box(mapRect,"Preparing map…");
        TerrainMapViewport.Degrees(Position,out double playerLat,out double playerLon);double px=.5+TerrainMapViewport.Wrap(playerLon-mapViewport.Longitude)/mapViewport.LongitudeSpan,py=.5-(playerLat-mapViewport.Latitude)/mapViewport.LatitudeSpan;
        if(px>=0&&px<=1&&py>=0&&py<=1){float x=mapRect.x+(float)px*mapRect.width,y=mapRect.y+(float)py*mapRect.height;GUI.DrawTexture(new Rect(x-5,y-1,10,2),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(x-1,y-5,2,10),Texture2D.whiteTexture);}
        GUI.Label(new Rect(mapRect.x,mapRect.yMax+8,mapRect.width-250,44),"Click to teleport · drag to pan · scroll to zoom"+(mapDirty||mapJob!=null?"  · Updating…":""));
        if(GUI.Button(new Rect(mapRect.xMax-240,mapRect.yMax+8,115,30),"Player")){mapViewport.Longitude=playerLon;mapViewport.Latitude=playerLat;InvalidateMap();}
        if(GUI.Button(new Rect(mapRect.xMax-115,mapRect.yMax+8,115,30),"Whole world")){mapViewport=new TerrainMapViewport();InvalidateMap();}
        if(mapLayerMenu)
        {
            var menu=new Rect(mapRect.xMax-235,mapRect.y-6,235,Math.Min(320,mapRect.height));GUI.Box(menu,"");GUILayout.BeginArea(menu);mapLayerScroll=GUILayout.BeginScrollView(mapLayerScroll);
            foreach(var candidate in GlobeView.VisibleLayers.Where(l=>l!=Layer.Simulation)){GUI.enabled=sourceWorld==null||GenerationPreview.Available(sourceWorld,candidate);if(GUILayout.Button(GlobeView.LayerName(candidate),GUILayout.Height(30))){mapLayer=candidate;mapLayerMenu=false;InvalidateMap();}}
            GUI.enabled=true;GUILayout.EndScrollView();GUILayout.EndArea();
        }
        if(!mapLayerMenu)MapInput(Event.current);
    }
    public void Pause(){paused=true;mapOpen=false;CaptureMouse();}
    string DebugText()
    {
        TerrainMapViewport.Degrees(Position,out double lat,out double lon);
        return "WORLDGEN  ·  "+(1/Math.Max(.001,Time.smoothDeltaTime)).ToString("F0")+" FPS\n"+lat.ToString("F6")+"°, "+lon.ToString("F6")+"°  ·  scale "+HorizontalScale.ToString("G3")+"\n"+
            "Elevation "+(walkingPatch.Sample(Position).ElevationM+HeightAboveGround).ToString("F1")+" m  ·  ground clearance "+HeightAboveGround.ToString("F2")+" m\n"+
            (motion.Flying?"Flying":"Walking")+"  ·  "+Diagnostics+"\nScatter "+ScatterObjects.ToString("N0")+" instances  ·  "+ScatterDrawCalls+" draws"+
            (playState==null?"":"\nSolar time "+playState.SolarHour.ToString("F2")+" h  ·  calendar ×"+playState.Settings.CalendarDaysPerDay.ToString("G4"))+(MusicDebugText.Length>0?"\n"+MusicDebugText:"")+(error.Length>0?"\n"+error:"");
    }
    void MapInput(Event e)
    {
        if(e.type==EventType.ScrollWheel&&mapRect.Contains(e.mousePosition)){mapViewport.Zoom(Math.Exp(e.delta.y*.15),(e.mousePosition.x-mapRect.x)/mapRect.width,(e.mousePosition.y-mapRect.y)/mapRect.height);InvalidateMap();e.Use();}
        else if(e.type==EventType.MouseDown&&e.button==0&&mapRect.Contains(e.mousePosition)){mapDragging=true;mapDown=mapPrevious=e.mousePosition;e.Use();}
        else if(e.type==EventType.MouseDrag&&mapDragging){var delta=e.mousePosition-mapPrevious;mapPrevious=e.mousePosition;if((e.mousePosition-mapDown).sqrMagnitude>16){mapViewport.Pan(delta.x/mapRect.width,delta.y/mapRect.height);InvalidateMap();}e.Use();}
        else if(e.type==EventType.MouseUp&&e.button==0&&mapDragging){mapDragging=false;if((e.mousePosition-mapDown).sqrMagnitude<=16&&mapRect.Contains(e.mousePosition))Teleport(mapViewport.Position((e.mousePosition.x-mapRect.x)/mapRect.width,(e.mousePosition.y-mapRect.y)/mapRect.height));e.Use();}
    }
    public void TestMapLayer(Layer layer){mapLayer=layer;InvalidateMap();}
    public void TestMapClick(double u,double v){Teleport(mapViewport.Position(u,v));}
    public void TestMapZoom(double factor){mapViewport.Zoom(factor);InvalidateMap();}
    public void CaptureMap(string path){if(!MapReady)throw new InvalidOperationException("World map is not ready.");System.IO.File.WriteAllBytes(path,mapTexture.EncodeToPNG());}
}
