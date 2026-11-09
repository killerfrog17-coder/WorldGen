using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

public sealed partial class WorldGenRuntime
{
    bool displayVSync;int displayMaxFps=60;string displayFpsInput="60";Vector2 displayMenuScroll;
    int windowWidth=1280,windowHeight=800;
    bool simulationRenderBudget;
    void PollSimulationRenderBudget()
    {
        var ai=SettingsAiClient;
        int aiFps=ai?.IsBusy==true?ai.Config.InferenceFps:0;
        bool fast=screen=="Simulation"&&terrainExplorer==null&&humanPlaying&&!simulationPaused&&humanPlayback.Rate>=100*WorldGen.Core.SimulationTime.Year;
        if(aiFps>0)
        {
            QualitySettings.vSyncCount=0;Application.targetFrameRate=displayMaxFps==0?aiFps:Math.Min(aiFps,displayMaxFps);simulationRenderBudget=true;
        }
        else if(fast)
        {
            // Map presentation does not need hundreds of redundant redraws
            // while the history executor needs CPU/cache bandwidth. Keep the
            // map fluid at60Hz and retain the owner's gameplay preferences.
            QualitySettings.vSyncCount=0;Application.targetFrameRate=displayMaxFps==0?60:Math.Min(60,displayMaxFps);simulationRenderBudget=true;
        }
        else if(simulationRenderBudget){QualitySettings.vSyncCount=displayVSync?1:0;Application.targetFrameRate=displayMaxFps==0?-1:displayMaxFps;simulationRenderBudget=false;}
    }
    void InitializeDisplayOptions()
    {
        SetDisplayOptions(PlayerPrefs.GetInt("display-vsync",QualitySettings.vSyncCount>0?1:0)!=0,Math.Max(0,Math.Min(1000,PlayerPrefs.GetInt("display-max-fps",60))),false);
        WeatherVideoOptions.Current.Load();
        TerrainVideoOptions.Load();
        masterVolume=Mathf.Clamp01(PlayerPrefs.GetFloat("master-volume",1));masterMuted=PlayerPrefs.GetInt("master-muted",0)!=0;
        AudioListener.volume=masterMuted?0:masterVolume;
        // Automated windowed captures must not change the owner's display preferences.
        if(!Environment.GetCommandLineArgs().Contains("-screen-fullscreen"))
            StartCoroutine(InitializeFullscreen());
    }
    System.Collections.IEnumerator InitializeFullscreen()
    {yield return null;if(PlayerPrefs.GetInt("display-fullscreen",Screen.fullScreen?1:0)!=0)SetFullscreen(true,false);}
    void SetFullscreen(bool enabled,bool persist=true)
    {
        if(enabled)
        {
            if(!Screen.fullScreen){windowWidth=Screen.width;windowHeight=Screen.height;}
            var monitor=Screen.mainWindowDisplayInfo;
            int width=monitor.width>0?monitor.width:Screen.currentResolution.width;
            int height=monitor.height>0?monitor.height:Screen.currentResolution.height;
            Screen.SetResolution(width,height,FullScreenMode.FullScreenWindow);
        }
        else Screen.SetResolution(Math.Max(640,windowWidth),Math.Max(480,windowHeight),FullScreenMode.Windowed);
        if(persist){PlayerPrefs.SetInt("display-fullscreen",enabled?1:0);PlayerPrefs.Save();}
    }
    void SetDisplayOptions(bool sync,int maximumFps,bool persist=true)
    {
        if(maximumFps<0||maximumFps>1000)throw new ArgumentOutOfRangeException(nameof(maximumFps));
        displayVSync=sync;displayMaxFps=maximumFps;displayFpsInput=maximumFps.ToString(CultureInfo.InvariantCulture);
        QualitySettings.vSyncCount=sync?1:0;Application.targetFrameRate=maximumFps==0?-1:maximumFps;
        if(persist){PlayerPrefs.SetInt("display-vsync",sync?1:0);PlayerPrefs.SetInt("display-max-fps",maximumFps);PlayerPrefs.Save();}
    }
    void DrawDisplayOptions(bool compact=false)
    {
        GUILayout.Space(12);GUILayout.Label("DISPLAY",SectionStyle());
        bool full=GUILayout.Toggle(Screen.fullScreen,"Fullscreen · monitor resolution");if(full!=Screen.fullScreen)SetFullscreen(full);
        bool sync=GUILayout.Toggle(displayVSync,"VSync");if(sync!=displayVSync)SetDisplayOptions(sync,displayMaxFps);
        GUILayout.BeginHorizontal();GUILayout.Label("Maximum FPS");GUI.SetNextControlName("display-max-fps");displayFpsInput=GUILayout.TextField(displayFpsInput,5,GUILayout.Width(75));
        bool valid=int.TryParse(displayFpsInput,NumberStyles.Integer,CultureInfo.InvariantCulture,out int fps)&&fps>=0&&fps<=1000;
        bool previous=GUI.enabled;GUI.enabled=previous&&valid;bool apply=GUILayout.Button("Apply",GUILayout.Width(70));GUI.enabled=previous;GUILayout.EndHorizontal();
        var e=Event.current;if(previous&&valid&&GUI.GetNameOfFocusedControl()=="display-max-fps"&&e.type==EventType.KeyDown&&(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter)){apply=true;e.Use();}
        if(apply)SetDisplayOptions(displayVSync,fps);
        GUILayout.Label(displayVSync?"VSync follows the display refresh rate. Turn it off to use the FPS limit.":"0 = Unlimited · 1–1,000 FPS · Apply or press Enter",MutedStyle());
    }
}
