using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Serialization;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckChunkSettingsRuntime()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);
        bool hadToggle=PlayerPrefs.HasKey("terrain-loading-fog"),hadSeconds=PlayerPrefs.HasKey("terrain-loading-fog-seconds");
        int oldToggle=PlayerPrefs.GetInt("terrain-loading-fog",1);float oldSeconds=PlayerPrefs.GetFloat("terrain-loading-fog-seconds",3);
        bool hadHard=PlayerPrefs.HasKey("terrain-hard-chunk-updates");int oldHard=PlayerPrefs.GetInt("terrain-hard-chunk-updates",0);
        var errors=new List<string>();Application.LogCallback record=(m,t,k)=>{if(k==LogType.Error||k==LogType.Exception)errors.Add(m);};Application.logMessageReceived+=record;
        var checks=new List<string>();
        try
        {
            int revision=TerrainVideoOptions.Revision;
            TerrainVideoOptions.LoadingFog=false;TerrainVideoOptions.LoadingFogSeconds=5;TerrainVideoOptions.SaveLoadingFog();
            if(TerrainVideoOptions.Revision!=revision)throw new Exception("Fog setting invalidated chunk plans");
            TerrainVideoOptions.LoadingFog=true;TerrainVideoOptions.LoadingFogSeconds=1;TerrainVideoOptions.Load();
            if(TerrainVideoOptions.LoadingFog||TerrainVideoOptions.LoadingFogSeconds!=5)throw new Exception("Fog settings failed persistence");
            checks.Add("PASS fog toggle and transition survive preference reload without rebuilding chunks");
            revision=TerrainVideoOptions.Revision;TerrainVideoOptions.HardChunkUpdates=true;TerrainVideoOptions.SaveChunkTransitions();
            if(TerrainVideoOptions.Revision!=revision)throw new Exception("Hard chunk updates invalidated chunk plans");
            TerrainVideoOptions.HardChunkUpdates=false;TerrainVideoOptions.Load();
            if(!TerrainVideoOptions.HardChunkUpdates)throw new Exception("Hard chunk updates failed persistence");
            checks.Add("PASS hard chunk updates persist without invalidating the terrain plan");
            TerrainVideoOptions.LoadingFog=true;TerrainVideoOptions.LoadingFogSeconds=3;
            screen="Options";settingsCategory=2;settingsScroll=Vector2.zero;Application.targetFrameRate=60;
            yield return null;yield return null;yield return new WaitForEndOfFrame();
            var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"terrain-settings.png"),image.EncodeToPNG());Destroy(image);
            if(errors.Count!=0)throw new Exception(string.Join("\n",errors));
            checks.Add("PASS actual Terrain settings render without GUI exceptions; no world, humans, animals or model host started");
        }
        finally
        {
            if(hadToggle)PlayerPrefs.SetInt("terrain-loading-fog",oldToggle);else PlayerPrefs.DeleteKey("terrain-loading-fog");
            if(hadSeconds)PlayerPrefs.SetFloat("terrain-loading-fog-seconds",oldSeconds);else PlayerPrefs.DeleteKey("terrain-loading-fog-seconds");
            if(hadHard)PlayerPrefs.SetInt("terrain-hard-chunk-updates",oldHard);else PlayerPrefs.DeleteKey("terrain-hard-chunk-updates");
            PlayerPrefs.Save();Application.logMessageReceived-=record;
        }
        File.WriteAllLines(Path.Combine(output,"complete.txt"),checks);Application.Quit(0);
    }
    IEnumerator CheckTerrainDetail(string output,string hash)
    {
        var report=new List<string>();
        Application.logMessageReceived+=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        var options=TerrainVideoOptions.Current;options.Quality=2;options.Effort=1;options.ViewDistanceM=60000;options.FullDetailTiles=1.5;TerrainVideoOptions.Revision++;
        var session=WorldLibrary.ReadPlaySession(Argument("--worldgen-local-terrain-check",""));play.Settings.HorizontalScale=.1;play.Mode=GameMode.Spectator;
        double start=Time.realtimeSinceStartupAsDouble,deadline=start+120;ExploreTerrain(session.PlayerPosition);
        while((terrainExplorer==null||!terrainExplorer.Ready||!terrainExplorer.NearReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        var e=terrainExplorer;if(e==null||!e.Ready||!e.NearReady)throw new Exception("Adaptive explorer unavailable");e.TestView(120,20,12);e.BeginLoadingProfile();
        bool near=false,horizon=false;
        while(Time.realtimeSinceStartupAsDouble<deadline)
        {
            if(e.Error!="")throw new Exception(e.Error);
            if(!near&&e.NearReady){near=true;report.Add("Cold near ready "+(Time.realtimeSinceStartupAsDouble-start).ToString("F3")+"s; "+e.Diagnostics);}
            if(!horizon&&e.StreamingFogDistance>=58000){horizon=true;report.Add("Horizon reveal "+(Time.realtimeSinceStartupAsDouble-start).ToString("F3")+"s; "+e.Diagnostics);}
            if(near&&e.AllReady&&!e.DetailPlanning)break;
            yield return null;
        }
        report.Add((e.AllReady?"Full requested detail ":"PARTIAL requested detail ")+(Time.realtimeSinceStartupAsDouble-start).ToString("F3")+"s; "+e.Diagnostics);
        report.Add(e.EndLoadingProfile());e.Capture(Path.Combine(output,"terrain.png"));
        File.WriteAllLines(Path.Combine(output,"probes.csv"),new[]{"originX,originY,originZ,east,north,height"}.Concat(e.Probes()));
        if(!near)throw new Exception("Near terrain never ready");
        e.Pause();playPauseTab="Game settings";
        for(int tab=0;tab<SettingsTabs.Length;tab++)
        {
            settingsCategory=tab;settingsScroll=Vector2.zero;yield return null;yield return new WaitForEndOfFrame();
            var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"settings-"+SettingsTabs[tab]+".png"),image.EncodeToPNG());Destroy(image);
        }
        report.Add("All five shared pause-settings categories rendered without GUI exceptions. Screen captures require visual review.");
        int originalQuality=options.Quality;options.Quality=0;options.Effort=0;TerrainVideoOptions.Revision++;yield return null;yield return null;
        double until=Time.realtimeSinceStartupAsDouble+3;while(e.DetailPlanning&&Time.realtimeSinceStartupAsDouble<until)yield return null;
        report.Add("Live Low / Low: "+e.TestDebugText().Split('\n').First(t=>t.StartsWith("Adaptive terrain")));
        options.Quality=originalQuality;options.Effort=1;TerrainVideoOptions.Revision++;
        var monitor=Screen.mainWindowDisplayInfo;report.Add("Current monitor "+monitor.width+"x"+monitor.height+"; fullscreen uses these dimensions, not window dimensions.");
        if(StateHash.Compute(world)!=hash)throw new Exception("Terrain/settings altered world/history");
        report.Add("PASS source "+hash);File.WriteAllLines(Path.Combine(output,"terrain-detail.txt"),report);
        e.Close();Application.Quit(0);
    }
}
