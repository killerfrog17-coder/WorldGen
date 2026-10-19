using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    bool debugOpen,mapOpen,paused,mapDirty,mapDragging; double viewDistance=200000,mapDue;
    Vector2 mapDown,mapPrevious; Rect mapRect;
    TerrainMapViewport mapViewport=new TerrainMapViewport(),shownMapViewport;
    Texture2D mapTexture;Task<TerrainMapImage> mapJob;CancellationTokenSource mapCancellation;int mapRevision,mapJobRevision;
    public bool DebugVisible=>debugOpen;
    public bool MapVisible=>debugOpen&&mapOpen;
    public bool Paused=>paused;
    public bool MapReady=>mapTexture!=null&&!mapDirty&&mapJob==null;
    void CaptureMouse(){Cursor.lockState=debugOpen||paused?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=debugOpen||paused;}
    void OnApplicationFocus(bool focus){if(focus&&!closed)CaptureMouse();}
    void HandleDebugKeys()
    {
        var k=Keyboard.current;if(k==null)return;
        bool chord=k.f3Key.isPressed&&k.mKey.wasPressedThisFrame||k.mKey.isPressed&&k.f3Key.wasPressedThisFrame;
        if(chord){debugOpen=true;mapOpen=!mapOpen;paused=false;if(mapOpen)InvalidateMap();CaptureMouse();}
        else if(k.f3Key.wasPressedThisFrame){debugOpen=!debugOpen;paused=false;CaptureMouse();}
        if(k.escapeKey.wasPressedThisFrame){if(debugOpen)debugOpen=false;else paused=!paused;CaptureMouse();}
    }
    void InvalidateMap(){mapRevision++;mapDirty=true;mapDue=Time.realtimeSinceStartupAsDouble+.12;mapCancellation?.Cancel();}
    void UpdateMapWork()
    {
        if(mapJob!=null&&mapJob.IsCompleted)
        {
            if(mapJob.IsFaulted){error=mapJob.Exception.GetBaseException().Message;debugOpen=true;CaptureMouse();}
            else if(!mapJob.IsCanceled&&mapJobRevision==mapRevision)
            {
                var image=mapJob.Result;var texture=new Texture2D(image.Width,image.Height,TextureFormat.RGBA32,false);texture.LoadRawTextureData(image.Rgba);texture.Apply();texture.filterMode=FilterMode.Bilinear;
                if(mapTexture!=null)Destroy(mapTexture);mapTexture=texture;shownMapViewport=image.View;
            }
            mapJob=null;
        }
        if(!MapVisible||!mapDirty||mapJob!=null||Time.realtimeSinceStartupAsDouble<mapDue)return;
        mapDirty=false;mapCancellation?.Dispose();mapCancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);var token=mapCancellation.Token;var viewport=mapViewport.Clone();mapJobRevision=mapRevision;
        mapJob=Task.Run(()=>TerrainMapImage.Build(terrain,viewport,512,256,token),token);
    }
    void OnGUI()
    {
        // Deliberately no labels, bars or hints during normal exploration.
        if(!debugOpen&&!paused)return;
        if(paused)
        {
            GUILayout.BeginArea(new Rect(Screen.width/2f-130,Screen.height/2f-95,260,190),GUI.skin.box);
            GUILayout.Space(15);if(GUILayout.Button("Resume",GUILayout.Height(38))){paused=false;CaptureMouse();}
            if(GUILayout.Button("Return to world",GUILayout.Height(38)))Close();GUILayout.EndArea();return;
        }
        float infoWidth=Math.Min(330,Screen.width*.3f);
        GUILayout.BeginArea(new Rect(12,12,infoWidth,Math.Min(460,Screen.height-24)),GUI.skin.box);
        GUILayout.Label("TERRAIN DEBUG · F3");
        if(terrain==null)GUILayout.Label(error.Length>0?error:"Preparing terrain…");
        else
        {
            TerrainMapViewport.Degrees(Position,out double lat,out double lon);
            GUILayout.Label(lat.ToString("F6")+"°, "+lon.ToString("F6")+"°");
            double aboveGround=HeightAboveGround,altitude=walkingPatch.Sample(Position).ElevationM+aboveGround;
            GUILayout.Label("Elevation "+altitude.ToString("F1")+" m · above ground "+HeightAboveGround.ToString("F1")+" m");
            GUILayout.Label((1/Math.Max(.001,Time.smoothDeltaTime)).ToString("F0")+" FPS · "+Diagnostics);
            motion.Flying=GUILayout.Toggle(motion.Flying,"Fly [F]");
            GUILayout.Label("Fly speed "+motion.FlySpeed.ToString("F0")+" m/s");
            motion.FlySpeed=Math.Exp(GUILayout.HorizontalSlider((float)Math.Log(motion.FlySpeed),(float)Math.Log(5),(float)Math.Log(20000)));
            GUILayout.Label("View distance "+(viewDistance/1000).ToString("F0")+" km");
            double distance=GUILayout.HorizontalSlider((float)viewDistance,30000,250000);
            if(Math.Abs(distance-viewDistance)>100){viewDistance=distance;SetFog();RefreshWanted();}
            if(GUILayout.Button(mapOpen?"Hide map [F3 + M]":"Map [F3 + M]")){mapOpen=!mapOpen;if(mapOpen)InvalidateMap();}
            if(GUILayout.Button("Center map on player")){mapViewport.Longitude=lon;mapViewport.Latitude=lat;mapViewport.Zoom(1);InvalidateMap();}
            if(GUILayout.Button("Return to world"))Close();
            if(error.Length>0)GUILayout.Label(error);
        }
        GUILayout.EndArea();
        if(!mapOpen||terrain==null)return;
        float mapWidth=Math.Min(Screen.width-infoWidth-48,Math.Min(1000,(Screen.height-105)*2));
        mapRect=new Rect(infoWidth+28,38,mapWidth,mapWidth*.5f);
        GUI.Box(new Rect(mapRect.x-8,12,mapRect.width+16,mapRect.height+72),"");
        GUI.Label(new Rect(mapRect.x,15,mapRect.width,24),"MAP · Click to teleport · Drag to pan · Wheel to zoom");
        if(mapTexture!=null)
        {
            GUI.BeginGroup(mapRect);double width=mapRect.width*shownMapViewport.LongitudeSpan/mapViewport.LongitudeSpan,height=mapRect.height*shownMapViewport.LatitudeSpan/mapViewport.LatitudeSpan;
            double x=mapRect.width*(.5+TerrainMapViewport.Wrap(shownMapViewport.Longitude-mapViewport.Longitude)/mapViewport.LongitudeSpan)-width*.5;
            double y=mapRect.height*(.5-(shownMapViewport.Latitude-mapViewport.Latitude)/mapViewport.LatitudeSpan)-height*.5;
            for(int wrap=-1;wrap<=1;wrap++)GUI.DrawTexture(new Rect((float)(x+wrap*360/mapViewport.LongitudeSpan*mapRect.width),(float)y,(float)width,(float)height),mapTexture,ScaleMode.StretchToFill,false);
            GUI.EndGroup();
        }
        else GUI.Box(mapRect,"Preparing map…");
        TerrainMapViewport.Degrees(Position,out double playerLat,out double playerLon);
        double px=.5+TerrainMapViewport.Wrap(playerLon-mapViewport.Longitude)/mapViewport.LongitudeSpan,py=.5-(playerLat-mapViewport.Latitude)/mapViewport.LatitudeSpan;
        if(px>=0&&px<=1&&py>=0&&py<=1){float x=mapRect.x+(float)px*mapRect.width,y=mapRect.y+(float)py*mapRect.height;GUI.DrawTexture(new Rect(x-5,y-1,10,2),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(x-1,y-5,2,10),Texture2D.whiteTexture);}
        GUI.Label(new Rect(mapRect.x,mapRect.yMax+4,mapRect.width-95,24),mapViewport.LongitudeSpan.ToString("G4")+"° span"+(mapDirty||mapJob!=null?" · Updating…":""));
        if(GUI.Button(new Rect(mapRect.xMax-90,mapRect.yMax+4,90,24),"Whole world")){mapViewport=new TerrainMapViewport();InvalidateMap();}
        MapInput(Event.current);
    }
    void MapInput(Event e)
    {
        if(e.type==EventType.ScrollWheel&&mapRect.Contains(e.mousePosition))
        {mapViewport.Zoom(Math.Exp(e.delta.y*.15),(e.mousePosition.x-mapRect.x)/mapRect.width,(e.mousePosition.y-mapRect.y)/mapRect.height);InvalidateMap();e.Use();}
        else if(e.type==EventType.MouseDown&&e.button==0&&mapRect.Contains(e.mousePosition)){mapDragging=true;mapDown=mapPrevious=e.mousePosition;e.Use();}
        else if(e.type==EventType.MouseDrag&&mapDragging){var delta=e.mousePosition-mapPrevious;mapPrevious=e.mousePosition;if((e.mousePosition-mapDown).sqrMagnitude>16){mapViewport.Pan(delta.x/mapRect.width,delta.y/mapRect.height);InvalidateMap();}e.Use();}
        else if(e.type==EventType.MouseUp&&e.button==0&&mapDragging)
        {mapDragging=false;if((e.mousePosition-mapDown).sqrMagnitude<=16&&mapRect.Contains(e.mousePosition))Teleport(mapViewport.Position((e.mousePosition.x-mapRect.x)/mapRect.width,(e.mousePosition.y-mapRect.y)/mapRect.height));e.Use();}
    }
    public void TestMapClick(double u,double v){Teleport(mapViewport.Position(u,v));}
    public void TestMapZoom(double factor){mapViewport.Zoom(factor);InvalidateMap();}
    public void CaptureMap(string path){if(!MapReady)throw new InvalidOperationException("Debug map is not ready.");System.IO.File.WriteAllBytes(path,mapTexture.EncodeToPNG());}
}
