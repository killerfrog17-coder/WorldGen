using System;
using System.Globalization;
using UnityEngine;

public sealed partial class WorldGenRuntime
{
    bool displayVSync;int displayMaxFps=60;string displayFpsInput="60";Vector2 displayMenuScroll;
    void InitializeDisplayOptions()
    {
        SetDisplayOptions(PlayerPrefs.GetInt("display-vsync",QualitySettings.vSyncCount>0?1:0)!=0,Math.Max(0,Math.Min(1000,PlayerPrefs.GetInt("display-max-fps",60))),false);
        WeatherVideoOptions.Current.Load();
        TerrainVideoOptions.Current.Load();
    }
    void SetDisplayOptions(bool sync,int maximumFps,bool persist=true)
    {
        if(maximumFps<0||maximumFps>1000)throw new ArgumentOutOfRangeException(nameof(maximumFps));
        displayVSync=sync;displayMaxFps=maximumFps;displayFpsInput=maximumFps.ToString(CultureInfo.InvariantCulture);
        QualitySettings.vSyncCount=sync?1:0;Application.targetFrameRate=maximumFps==0?-1:maximumFps;
        if(persist){PlayerPrefs.SetInt("display-vsync",sync?1:0);PlayerPrefs.SetInt("display-max-fps",maximumFps);PlayerPrefs.Save();}
    }
    void DrawDisplayOptions()
    {
        GUILayout.Space(12);GUILayout.Label("DISPLAY",SectionStyle());
        bool full=GUILayout.Toggle(Screen.fullScreen,"Fullscreen");if(full!=Screen.fullScreen)Screen.fullScreenMode=full?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;
        bool sync=GUILayout.Toggle(displayVSync,"VSync");if(sync!=displayVSync)SetDisplayOptions(sync,displayMaxFps);
        GUILayout.BeginHorizontal();GUILayout.Label("Maximum FPS");GUI.SetNextControlName("display-max-fps");displayFpsInput=GUILayout.TextField(displayFpsInput,5,GUILayout.Width(75));
        bool valid=int.TryParse(displayFpsInput,NumberStyles.Integer,CultureInfo.InvariantCulture,out int fps)&&fps>=0&&fps<=1000;
        bool previous=GUI.enabled;GUI.enabled=previous&&valid;bool apply=GUILayout.Button("Apply",GUILayout.Width(70));GUI.enabled=previous;GUILayout.EndHorizontal();
        var e=Event.current;if(previous&&valid&&GUI.GetNameOfFocusedControl()=="display-max-fps"&&e.type==EventType.KeyDown&&(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter)){apply=true;e.Use();}
        if(apply)SetDisplayOptions(displayVSync,fps);
        GUILayout.Label(displayVSync?"VSync follows the display refresh rate. Turn it off to use the FPS limit.":"0 = Unlimited · 1–1,000 FPS · Apply or press Enter",MutedStyle());
        DrawTerrainVideoOptions();DrawWeatherVideoOptions();
    }
}
