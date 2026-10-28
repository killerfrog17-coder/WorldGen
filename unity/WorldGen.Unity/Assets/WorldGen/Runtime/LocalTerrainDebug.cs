using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    int fpsFrames;double fpsElapsed,fpsClock;string fpsDisplay="…";
    string debugDisplay="Preparing terrain…",debugFastDisplay="Preparing terrain…";double debugRefreshAt,debugFastAt,debugGroundClearance,debugElevation;
    bool debugFull;
    public int DebugReadoutUpdates {get;private set;}
    void UpdateDebugReadout()
    {
        if(!debugOpen||ecologyOpen||terrain==null||walkingPatch==null)return;
        double now=Time.realtimeSinceStartupAsDouble;
        if(now>=debugFastAt){debugFastAt=now+1d/30;debugFastDisplay=FastDebugText();if(debugFull){UpdateTargetReadout(now);RefreshDebugPosition();}}
        if(!debugFull||now<debugRefreshAt)return;
        debugRefreshAt=now+.25;debugDisplay=DebugText();CacheDebugSections();DebugReadoutUpdates++;
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
    Layer mapLayer=Layer.Satellite;
    Texture2D mapTexture;int mapRevision;
    public bool DebugVisible=>debugOpen;
    public bool MapVisible=>mapOpen;
    public bool Paused=>paused||inventoryOpen&&!CraftWorking;
    public bool MapReady=>mapSurface!=null&&!mapDirty&&!mapRefining&&shownMapRevision==mapRevision;
    public int MapBuildCount {get;private set;}
    double MaximumViewDistance=>terrain==null?250000:Math.Min(250000,terrain.RadiusM*HorizontalScale*.3);
    bool WantsMouseLook=>!closed&&Application.isFocused&&!DeathVisible&&!inventoryOpen&&!mapOpen&&!paused&&!chatOpen&&!ecologyOpen;
    void CaptureMouse()
    {
        if(passiveSkyCheck||passiveReanchorCheck)return;
        bool capture=WantsMouseLook;var state=capture?CursorLockMode.Locked:CursorLockMode.None;
        if(Cursor.lockState!=state)Cursor.lockState=state;
        if(Cursor.visible==capture)Cursor.visible=!capture;
    }
    void OnApplicationFocus(bool focus)
    {
        if(closed||passiveSkyCheck||passiveReanchorCheck)return;
        // Re-establish the native window clip after Alt-Tab/focus changes even
        // when the managed lock property still reports its previous value.
        if(focus)Cursor.lockState=CursorLockMode.None;
        CaptureMouse();
    }
    void HandleDebugKeys()
    {
        var k=ActiveKeyboard;if(k==null)return;
        bool ecologyChord=k.f3Key.isPressed&&k.eKey.wasPressedThisFrame||k.eKey.isPressed&&k.f3Key.wasPressedThisFrame;
        if(ecologyChord&&!paused){ToggleEcologyDebug();return;}
        if(k.f3Key.wasPressedThisFrame){if(k.shiftKey.isPressed){debugFull=!debugFull;debugOpen=true;}else debugOpen=!debugOpen;if(debugOpen)debugRefreshAt=debugFastAt=0;}
        if(k.mKey.wasPressedThisFrame&&!paused&&!ecologyOpen){mapOpen=!mapOpen;if(mapOpen){InitializeMapSelection();InvalidateMap();}CaptureMouse();}
        if(mapOpen&&!paused)HandleMapSelectionKeys(k);
        if(k.escapeKey.wasPressedThisFrame){if(ecologyOpen)ecologyOpen=false;else if(mapOpen){mapOpen=false;mapLayerMenu=false;}else paused=!paused;CaptureMouse();}
    }
    void InvalidateMap(){mapRevision++;mapDirty=true;mapDue=Time.realtimeSinceStartupAsDouble+.025;mapStream?.Cancel();InvalidateCloudMap();InvalidateRiverMap();}
    void UpdateMapWork()
    {
        UpdateCloudMapWork();UpdateRiverMapWork();UpdateMapInspection();UpdateEcologyDebug();
        UpdateProgressiveMap();
    }
    public void DrawViewSettings()
    {
        GUILayout.Label("Flight speed  "+motion.FlySpeed.ToString("F0")+" m/s");motion.FlySpeed=Math.Exp(GUILayout.HorizontalSlider((float)Math.Log(motion.FlySpeed),(float)Math.Log(5),(float)Math.Log(20000)));
    }
    void OnGUI()
    {
        if(MenuSkin!=null)GUI.skin=MenuSkin;
        GUI.color=GUI.contentColor=GUI.backgroundColor=Color.white;
        if(DeathVisible){DrawPlayerDeath();return;}
        if(inventoryOpen){DrawInventory();return;}
        DrawChat();
        DrawTerrainDiagnosticOverlay();
        if(ecologyOpen){DrawEcologyDebug();return;}
        if(debugOpen)DrawDebugOverlay();
        if(paused){if(drawPause!=null)drawPause();else{GUILayout.BeginArea(new Rect(Screen.width/2f-130,Screen.height/2f-70,260,140),GUI.skin.box);if(GUILayout.Button("Resume"))Resume();if(GUILayout.Button("World simulation"))Close();GUILayout.EndArea();}return;}
        DrawWorldMap();DrawPickupPrompt();
    }
    public void Pause(){paused=true;mapOpen=false;CaptureMouse();}
    public string TestDebugText()=>FastDebugText()+"\n"+DebugText();
    string FastDebugText()
    {
        TerrainMapViewport.Degrees(Position,out double lat,out double lon);
        if(playState!=null)lon=playState.OfficialLongitude(lon);
        double bearing=ExplorerNavigation.Bearing(frame,Position,heading);
        string date=playState==null?"":PlayCalendar.Date(PlayCalendar.Seconds(playState,sourceWorld==null?0:SimulationTime.Seconds(sourceWorld.Prehistory)));
        return fpsDisplay+" FPS\n"+lat.ToString("F6")+"°, "+lon.ToString("F6")+"°\n"+
            "Facing "+ExplorerNavigation.Compass(bearing)+" · "+bearing.ToString("F1")+"°\n"+
            "Elevation "+debugElevation.ToString("F1")+" m · clearance "+debugGroundClearance.ToString("F2")+" m\n"+
            "Temperature "+CurrentWeather.TemperatureC.ToString("F1")+" °C\n"+
            PlayCommands.TimeText(LocalSolarHour)+" · "+date;
    }
    string DebugText()
    {
        return "GROUND & STREAMING\nScale "+HorizontalScale.ToString("G3")+" · "+GroundDebugText+"\n"+
            (motion.Flying?"Flying":ActiveKeyboard!=null&&ActiveKeyboard.cKey.isPressed?"Walking":ActiveKeyboard!=null&&ActiveKeyboard.shiftKey.isPressed?"Sprinting":"Jogging")+"  ·  "+Diagnostics+"\n\nVEGETATION\nScatter "+ScatterObjects.ToString("N0")+" instances  ·  "+ScatterDrawCalls+" draws"+
            "\nLow vegetation · "+GroundCoverDraws+" draws · "+(GroundCoverBytes/1048576d).ToString("F1")+" MiB GPU buffers · "+GroundCoverCandidates.ToString("N0")+" candidate slots · "+SpecialistObjects+" herbs/fungi · "+MidPlantObjects+" mid plants · "+GeneratedTreeObjects+" trees"+
            "\nVegetation culling · "+ScatterFrustumRejected+" outside view · "+ScatterTerrainRejected+" behind hills · "+ScatterShadowOnly+" shadow-only · "+GrassPagesOccluded+" grass pages hidden"+
            "\nGrass field · "+(TerrainVideoOptions.GroundCover?TerrainVideoOptions.GrassDensity.ToString("F2")+"× · close "+TerrainVideoOptions.GrassBladeDistance.ToString("F0")+"m · geometry "+(TerrainVideoOptions.GrassCanopy?TerrainVideoOptions.GrassDistance.ToString("F0")+"m":"OFF")+" · detail "+TerrainVideoOptions.GrassDetail:"OFF")+
            "\nSun shadows ? "+(SunShadowsHeld?"held daylight":"following")+" ? caster limit "+VegetationShadowBudget.CasterLimit(vegetationShadowDistance).ToString("F0")+"m"+
            "\nTerrain reuse "+CachedTerrainTiles+" cached · "+(CachedTerrainBytes/1048576d).ToString("F1")+" MiB buffers · "+TerrainCacheHits+" hits · "+TerrainBuildRequests+" requests ("+TerrainPrefetchRequests+" ahead)"+
            "\nTree renderer · "+(TerrainVideoOptions.TreeRebuild?"rebuilt":"previous")+" · "+BakedTreeModels+" trees / "+BakedForestModels+" stands baked / "+PendingTreeModels+" pending · "+(TreeBakeBytes/1048576d).ToString("F1")+" MiB views"+
            "\nTree quality · "+VegetationQuality.Names[TerrainVideoOptions.TreeQuality]+" · meshes "+TerrainVideoOptions.TreeNearLod.ToString("F0")+" / "+TerrainVideoOptions.TreeFarLod.ToString("F0")+"m · individual field handoff "+ForestHandoffReach.ToString("F0")+"m"+
            "\nForest shadows · "+(TerrainVideoOptions.SimplifiedForestShadows?"proxies / "+TerrainVideoOptions.ForestShadowSamples+"-tap · "+TerrainVideoOptions.ForestShadowResolution+"px atlas / 180m · "+TreeShadowObjects+" casters, "+TreeShadowCards+" cards / "+TreeShadowCoarse+" coarse / "+TreeShadowTriangles.ToString("N0")+" tris / "+TreeShadowDraws+" draws":"original")+" · canopy "+(!TerrainVideoOptions.CanopyShade?"off":CanopyShadeBuilding?"building":CanopyShadeReady?"ready":"pending")+" · near-foot grass "+TerrainVideoOptions.GrassNearDensity.ToString("F1")+"x"+
            "\nDistant forest · "+ForestFieldCount+" resident / "+forestPriority.Length+" planned pages · "+ForestPacketCount+" representatives · "+ForestDrawCalls+" draws"+
            "\n\nTERRAIN LOD\nAdaptive terrain · "+new[]{"Low","Medium","High","Ultra","Extreme"}[detailOptions.Quality]+" · "+new[]{"Low","Balanced","High","Maximum"}[detailOptions.Effort]+" loading ("+WorkerLimit+" lanes) · reveal "+(streamedFog/1000).ToString("F1")+" km"+(DetailPlanning?" · planning":"")+
            "\nReady coverage: ground "+(terrainRevealRadius/1000).ToString("F1")+" km / trees "+(TreesNeededForReveal?(forestRevealRadius/1000).ToString("F1")+" km":"off")+
            "\n"+AnimalFaunaDebug+"\n"+HumanDebug+
            (mapStream==null?"":"\nMap cache · "+mapStream.TileHits+" tile hits · "+mapStream.TilesBuilt+" built · "+mapStream.WorkerCount+" workers")+
            (weatherScreen==null?"":"\n\nWEATHER & SKY\nWeather "+CurrentWeather.TemperatureC.ToString("F1")+" °C · rain "+CurrentWeather.RainMmPerHour.ToString("F2")+" / snow water "+CurrentWeather.SnowWaterMmPerHour.ToString("F2")+" mm/h · cloud "+(CurrentWeather.CloudCover*100).ToString("F0")+"% · gust "+CurrentWeather.GustMps.ToString("F1")+" m/s"+(WeatherControl.Mode=="auto"?"":" · TEST: "+WeatherControl.Mode)+"\n"+WeatherSoundDebug)+
            (weatherScreen==null?"":"\n"+FaunaAudioDebug+"\n"+WindDebugText)+
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
    public void CaptureMap(string path){if(!MapReady)throw new InvalidOperationException("World map is not ready.");var image=ReadMapTexture();try{System.IO.File.WriteAllBytes(path,image.EncodeToPNG());}finally{Destroy(image);}}
}
