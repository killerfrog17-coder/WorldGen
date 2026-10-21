using System;
using UnityEngine;
using WorldGen.Presentation;

public static class TerrainVideoOptions
{
    public static readonly TerrainDetailOptions Current=new TerrainDetailOptions();
    public static int Revision;
    public static void Load()
    {
        Current.Quality=Mathf.Clamp(PlayerPrefs.GetInt("terrain-quality",2),0,4);
        Current.Effort=Mathf.Clamp(PlayerPrefs.GetInt("terrain-effort",1),0,3);
        Current.ViewDistanceM=Mathf.Clamp(PlayerPrefs.GetFloat("terrain-distance",200000),1000,250000);
        Current.FullDetailTiles=Mathf.Clamp(PlayerPrefs.GetFloat("terrain-full-radius",1.5f),1.5f,8);
        Revision++;
    }
    public static void Save()
    {
        PlayerPrefs.SetInt("terrain-quality",Current.Quality);PlayerPrefs.SetInt("terrain-effort",Current.Effort);
        PlayerPrefs.SetFloat("terrain-distance",(float)Current.ViewDistanceM);PlayerPrefs.SetFloat("terrain-full-radius",(float)Current.FullDetailTiles);
        PlayerPrefs.Save();Revision++;
    }
}

public sealed partial class WorldGenRuntime
{
    int settingsCategory;Vector2 settingsScroll;
    static readonly string[] SettingsTabs={"Overview","Display","Terrain","Audio","Atmosphere"};
    void DrawSettingsPage()
    {
        int selected=GUILayout.SelectionGrid(settingsCategory,SettingsTabs,3);
        if(selected!=settingsCategory){settingsCategory=selected;settingsScroll=Vector2.zero;GUI.FocusControl(null);}
        GUILayout.Space(12);settingsScroll=GUILayout.BeginScrollView(settingsScroll);
        switch(settingsCategory)
        {
            case 0:
                DrawMasterVolume();DrawDisplayOptions(true);DrawTerrainOptions(true);
                GUILayout.Space(12);GUILayout.Label("Category tabs contain additional settings. Changes apply immediately.",MutedStyle());break;
            case 1:
                DrawDisplayOptions();
                bool smooth=GUILayout.Toggle(naturalTerrain,"Smooth world-map surface");if(smooth!=naturalTerrain){naturalTerrain=smooth;ApplyCachedDisplay();}break;
            case 2:DrawTerrainOptions();terrainExplorer?.DrawViewSettings();break;
            case 3:DrawAudioOptions();break;
            case 4:DrawWeatherVideoOptions();break;
        }
        GUILayout.EndScrollView();
    }
    void DrawTerrainOptions(bool compact=false)
    {
        var o=TerrainVideoOptions.Current;bool changed=GUI.changed;GUI.changed=false;
        GUILayout.Space(12);GUILayout.Label("TERRAIN",SectionStyle());
        GUILayout.Label("Terrain quality");o.Quality=GUILayout.SelectionGrid(o.Quality,new[]{"Low","Medium","High","Ultra","Extreme"},compact?3:3);
        GUILayout.Label("Loading effort");o.Effort=GUILayout.SelectionGrid(o.Effort,new[]{"Low","Balanced","High","Maximum"},2);
        if(!compact)
        {
            GUILayout.Label("View distance  "+(o.ViewDistanceM/1000).ToString("F0")+" km");
            o.ViewDistanceM=Math.Round(GUILayout.HorizontalSlider((float)o.ViewDistanceM,1000,250000)/1000)*1000;
            double scale=terrainExplorer?.HorizontalScale??play.Settings.HorizontalScale;
            GUILayout.Label("Full-detail distance  "+o.FullRadius(scale).ToString("F0")+" m");
            o.FullDetailTiles=Math.Round(GUILayout.HorizontalSlider((float)o.FullDetailTiles,1.5f,8)*2)/2;
            GUILayout.Label("Radius follows world scale. Larger radii can cost substantially more CPU and memory.",MutedStyle());
            GUILayout.Label("Quality controls estimated visible terrain error, not new terrain generation. Extreme is not a guarantee of imperceptible differences.",MutedStyle());
            GUILayout.Label("Loading effort changes background CPU concurrency and per-frame upload allowance, not the final detail. Maximum can reduce FPS while loading.",MutedStyle());
        }
        if(GUI.changed)TerrainVideoOptions.Save();GUI.changed|=changed;
    }
}
