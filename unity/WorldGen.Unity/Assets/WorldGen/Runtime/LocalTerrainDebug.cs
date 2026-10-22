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
    int fpsFrames;double fpsElapsed,fpsClock;string fpsDisplay="…";
    string debugDisplay="Preparing terrain…";double debugRefreshAt;
    public int DebugReadoutUpdates {get;private set;}
    void UpdateDebugReadout()
    {
        if(!debugOpen||terrain==null||walkingPatch==null)return;
        double now=Time.realtimeSinceStartupAsDouble;if(now<debugRefreshAt)return;
        debugRefreshAt=now+.25;debugDisplay=DebugText();DebugReadoutUpdates++;
    }
    void UpdateDebugFps(double elapsed)
    {
        if(elapsed<=0)return;
        fpsFrames++;fpsElapsed+=elapsed;
        if(fpsElapsed<.5)return;
        fpsDisplay=(fpsFrames/fpsElapsed).ToString("F0");
        fpsFrames=0;fpsElapsed=0;
    }
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
    public int MapBuildCount {get;private set;}
    double MaximumViewDistance=>terrain==null?250000:Math.Min(250000,terrain.RadiusM*HorizontalScale*.3);
    void CaptureMouse(){if(passiveSkyCheck||passiveReanchorCheck)return;Cursor.lockState=mapOpen||paused||chatOpen||ecologyOpen?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=mapOpen||paused||chatOpen||ecologyOpen;}
    void OnApplicationFocus(bool focus){if(focus&&!closed)CaptureMouse();}
    void HandleDebugKeys()
    {
        var k=ActiveKeyboard;if(k==null)return;
        bool ecologyChord=k.f3Key.isPressed&&k.eKey.wasPressedThisFrame||k.eKey.isPressed&&k.f3Key.wasPressedThisFrame;
        if(ecologyChord&&!paused){ToggleEcologyDebug();return;}
        if(k.f3Key.wasPressedThisFrame){debugOpen=!debugOpen;if(debugOpen)debugRefreshAt=0;}
        if(k.mKey.wasPressedThisFrame&&!paused&&!ecologyOpen){mapOpen=!mapOpen;if(mapOpen){InitializeMapSelection();InvalidateMap();}CaptureMouse();}
        if(mapOpen&&!paused)HandleMapSelectionKeys(k);
        if(k.escapeKey.wasPressedThisFrame){if(ecologyOpen)ecologyOpen=false;else if(mapOpen){mapOpen=false;mapLayerMenu=false;}else paused=!paused;CaptureMouse();}
    }
    void InvalidateMap(){mapRevision++;mapDirty=true;mapDue=Time.realtimeSinceStartupAsDouble+.12;mapCancellation?.Cancel();InvalidateCloudMap();InvalidateRiverMap();}
    void UpdateMapWork()
    {
        UpdateCloudMapWork();UpdateRiverMapWork();UpdateMapInspection();UpdateEcologyDebug();
        if(mapJob!=null&&mapJob.IsCompleted)
        {
            if(mapJob.IsFaulted){error=mapJob.Exception.GetBaseException().Message;debugOpen=true;}
            else if(!mapJob.IsCanceled&&mapJobRevision==mapRevision)
            {
                var result=mapJob.Result;var image=result.Image;if(result.Palette!=null)mapPalettes[result.Layer]=result.Palette;
                var texture=new Texture2D(image.Width,image.Height,TextureFormat.RGBA32,false);texture.LoadRawTextureData(image.Rgba);texture.Apply();texture.filterMode=FilterMode.Bilinear;
                texture.filterMode=TerrainMapSampling.Continuous(result.Layer)?FilterMode.Bilinear:FilterMode.Point;
                if(mapTexture!=null)Destroy(mapTexture);mapTexture=texture;shownMapViewport=image.View;
            }
            mapJob=null;
        }
        if(!mapOpen||!mapDirty||mapJob!=null||terrain==null||Time.realtimeSinceStartupAsDouble<mapDue)return;
        mapDirty=false;MapBuildCount++;mapCancellation?.Dispose();mapCancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);var token=mapCancellation.Token;var viewport=mapViewport.Clone();mapJobRevision=mapRevision;
        var selected=mapLayer;mapPalettes.TryGetValue(selected,out var cached);var source=sourceWorld;
        mapJob=Task.Run(()=>{bool continuous=TerrainMapSampling.Continuous(selected);var palette=continuous?null:cached??(source==null?null:GlobeView.CellValues(source,selected,token:token).Colors);int width=continuous?TerrainMapSampling.RasterWidth(viewport):768;return new MapResult{Layer=selected,Palette=palette,Image=TerrainMapImage.Build(terrain,viewport,width,width/2,token,selected,palette,source)};},token);
    }
    public void DrawViewSettings()
    {
        GUILayout.Label("Flight speed  "+motion.FlySpeed.ToString("F0")+" m/s");motion.FlySpeed=Math.Exp(GUILayout.HorizontalSlider((float)Math.Log(motion.FlySpeed),(float)Math.Log(5),(float)Math.Log(20000)));
    }
    void OnGUI()
    {
        if(MenuSkin!=null)GUI.skin=MenuSkin;
        GUI.color=GUI.contentColor=GUI.backgroundColor=Color.white;
        DrawChat();
        if(ecologyOpen){DrawEcologyDebug();return;}
        if(debugOpen)
        {
            string text=debugDisplay;var style=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=false,richText=false};style.normal.textColor=Color.black;
            GUI.Label(new Rect(14,14,Screen.width-28,Screen.height-28),text,style);style.normal.textColor=Color.white;GUI.Label(new Rect(12,12,Screen.width-28,Screen.height-28),text,style);
        }
        if(paused){if(drawPause!=null)drawPause();else{GUILayout.BeginArea(new Rect(Screen.width/2f-130,Screen.height/2f-70,260,140),GUI.skin.box);if(GUILayout.Button("Resume"))Resume();if(GUILayout.Button("World simulation"))Close();GUILayout.EndArea();}return;}
        DrawWorldMap();
    }
    public void Pause(){paused=true;mapOpen=false;CaptureMouse();}
    public string TestDebugText()=>DebugText();
    string DebugText()
    {
        TerrainMapViewport.Degrees(Position,out double lat,out double lon);
        if(playState!=null)lon=playState.OfficialLongitude(lon);
        double bearing=ExplorerNavigation.Bearing(frame,Position,heading);
        double clearance=HeightAboveGround;
        return "WORLDGEN  ·  "+fpsDisplay+" FPS\n"+lat.ToString("F6")+"°, "+lon.ToString("F6")+"°  ·  scale "+HorizontalScale.ToString("G3")+"\n"+
            "Facing "+ExplorerNavigation.Compass(bearing)+" · "+bearing.ToString("F1")+"° (N 0°, E 90°, S 180°, W 270°)\n"+
            "Elevation "+(walkingPatch.Sample(Position).ElevationM+clearance).ToString("F1")+" m  ·  ground clearance "+clearance.ToString("F2")+" m\n"+GroundDebugText+"\n"+
            (motion.Flying?"Flying":ActiveKeyboard!=null&&ActiveKeyboard.cKey.isPressed?"Walking":ActiveKeyboard!=null&&ActiveKeyboard.shiftKey.isPressed?"Sprinting":"Jogging")+"  ·  "+Diagnostics+"\nScatter "+ScatterObjects.ToString("N0")+" instances  ·  "+ScatterDrawCalls+" draws"+
            "\nLow vegetation · "+GroundCoverDraws+" draws · "+(GroundCoverBytes/1048576d).ToString("F1")+" MiB GPU buffers · "+GroundCoverCandidates.ToString("N0")+" candidate slots · "+SpecialistObjects+" herbs/fungi · "+MidPlantObjects+" mid plants · "+GeneratedTreeObjects+" trees"+
            "\nVegetation culling · "+ScatterFrustumRejected+" outside view · "+ScatterTerrainRejected+" behind hills · "+ScatterShadowOnly+" shadow-only · "+GrassPagesOccluded+" grass pages hidden"+
            "\nGrass field · "+(TerrainVideoOptions.GroundCover?TerrainVideoOptions.GrassDensity.ToString("F2")+"× · close "+TerrainVideoOptions.GrassBladeDistance.ToString("F0")+"m · geometry "+(TerrainVideoOptions.GrassCanopy?TerrainVideoOptions.GrassDistance.ToString("F0")+"m":"OFF")+" · detail "+TerrainVideoOptions.GrassDetail:"OFF")+
            "\nTerrain reuse "+CachedTerrainTiles+" cached · "+(CachedTerrainBytes/1048576d).ToString("F1")+" MiB buffers · "+TerrainCacheHits+" hits · "+TerrainBuildRequests+" requests ("+TerrainPrefetchRequests+" ahead)"+
            "\nTree renderer · "+(TerrainVideoOptions.TreeRebuild?"rebuilt":"previous")+" · "+BakedTreeModels+" trees / "+BakedForestModels+" stands baked / "+PendingTreeModels+" pending · "+(TreeBakeBytes/1048576d).ToString("F1")+" MiB views"+
            "\nTree quality · "+VegetationQuality.Names[TerrainVideoOptions.TreeQuality]+" · meshes "+TerrainVideoOptions.TreeNearLod.ToString("F0")+" / "+TerrainVideoOptions.TreeFarLod.ToString("F0")+"m · same-root views to "+TerrainVideoOptions.TreeIdentityRange.ToString("F0")+"m"+
            "\nDistant forest · "+ForestFieldCount+" fields / "+ForestPacketCount+" packets / "+ForestDrawCalls+" draws"+
            "\nAdaptive terrain · "+new[]{"Low","Medium","High","Ultra","Extreme"}[detailOptions.Quality]+" · "+new[]{"Low","Balanced","High","Maximum"}[detailOptions.Effort]+" loading ("+WorkerLimit+" lanes) · reveal "+(streamedFog/1000).ToString("F1")+" km"+(DetailPlanning?" · planning":"")+
            (weatherScreen==null?"":"\nWeather "+CurrentWeather.TemperatureC.ToString("F1")+" °C · rain "+CurrentWeather.RainMmPerHour.ToString("F2")+" / snow water "+CurrentWeather.SnowWaterMmPerHour.ToString("F2")+" mm/h · cloud "+(CurrentWeather.CloudCover*100).ToString("F0")+"% · gust "+CurrentWeather.GustMps.ToString("F1")+" m/s"+(WeatherControl.Mode=="auto"?"":" · TEST: "+WeatherControl.Mode)+"\n"+WeatherSoundDebug)+
            (weatherScreen==null?"":"\n"+WindDebugText)+
            (weatherScreen==null?"":"\nWeather visibility "+(CurrentWeather.VisibilityM/1000).ToString("F2")+" km · ground fog "+(CurrentWeather.Fog*100).ToString("F0")+"% · "+(weatherScreen.UseField?"spatial cloud field":"uniform test / preparing field"))+
            (playState==null?"":"\nLocal solar time "+PlayCommands.TimeText(LocalSolarHour)+"  ·  prime "+PlayCommands.TimeText(playState.SolarHour)+"  ·  calendar ×"+playState.Settings.CalendarDaysPerDay.ToString("G4"))+"\n"+PlayableSky.Season(skyYear)+
            (SkyFrame==null?"":"\nCelestial night light "+SkyFrame.NightIlluminanceLux.ToString("G3")+" lux · "+(skyConfig?.Stars.ToString()??"SolarNeighborhood")+string.Concat(SkyFrame.Bodies.Where(b=>b.Surface>0).Select(b=>"\n"+b.Name+" · "+PlayableSky.PhaseName(b.PhaseCycle)+" · "+(100*b.IlluminatedFraction).ToString("F0")+"% lit · "+(b.Direction.Y>0?"above":"below")+" horizon")))+(MusicDebugText.Length>0?"\n"+MusicDebugText:"")+(error.Length>0?"\n"+error:"");
    }
    void MapInput(Event e)
    {
        if(e.type==EventType.ScrollWheel&&mapRect.Contains(e.mousePosition)){mapViewport.Zoom(Math.Exp(e.delta.y*.15),(e.mousePosition.x-mapRect.x)/mapRect.width,(e.mousePosition.y-mapRect.y)/mapRect.height);InvalidateMap();e.Use();}
        else if(e.type==EventType.MouseDown&&e.button==0&&mapRect.Contains(e.mousePosition)){mapDragging=true;mapDown=mapPrevious=e.mousePosition;e.Use();}
        else if(e.type==EventType.MouseDrag&&mapDragging){var delta=e.mousePosition-mapPrevious;mapPrevious=e.mousePosition;if((e.mousePosition-mapDown).sqrMagnitude>16){mapViewport.Pan(delta.x/mapRect.width,delta.y/mapRect.height);InvalidateMap();}e.Use();}
        else if(e.type==EventType.MouseUp&&e.button==0&&mapDragging){mapDragging=false;if((e.mousePosition-mapDown).sqrMagnitude<=16&&mapRect.Contains(e.mousePosition))SelectMapPoint(mapViewport.Position((e.mousePosition.x-mapRect.x)/mapRect.width,(e.mousePosition.y-mapRect.y)/mapRect.height));e.Use();}
    }
    public void TestMapLayer(Layer layer){mapLayer=layer;InvalidateMap();}
    public void TestMapClick(double u,double v){SelectMapPoint(mapViewport.Position(u,v));}
    public void TestMapTeleport()=>TeleportMapSelection();
    public void TestMapZoom(double factor){mapViewport.Zoom(factor);InvalidateMap();}
    public void CaptureMap(string path){if(!MapReady)throw new InvalidOperationException("World map is not ready.");System.IO.File.WriteAllBytes(path,mapTexture.EncodeToPNG());}
}
