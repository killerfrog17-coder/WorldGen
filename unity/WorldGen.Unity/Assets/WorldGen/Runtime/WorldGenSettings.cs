using System;
using UnityEngine;
using WorldGen.Presentation;

public static class TerrainVideoOptions
{
    public static readonly TerrainDetailOptions Current=new TerrainDetailOptions();
    public static int Revision;
    public static bool GroundCover=true;
    // Temporary, session-only prototype controls. Do not invalidate terrain jobs.
    public static float GrassDensity=1,GrassDistance=350;
    public static float GrassBladeDistance=45;
    public static int GrassDetail=2;
    public static bool GrassWind=true;
    public static bool WeatherDrivenVegetationWind=true;
    public static float VegetationWindResponse=6;
    public static bool GrassCanopy=true;
    public static bool SpecialistPlants=true;
    public static void Load()
    {
        Current.Quality=Mathf.Clamp(PlayerPrefs.GetInt("terrain-quality",2),0,4);
        Current.Effort=Mathf.Clamp(PlayerPrefs.GetInt("terrain-effort",1),0,3);
        Current.ViewDistanceM=Mathf.Clamp(PlayerPrefs.GetFloat("terrain-distance",200000),1000,250000);
        Current.FullDetailTiles=Mathf.Clamp(PlayerPrefs.GetFloat("terrain-full-radius",1.5f),1.5f,8);
        GroundCover=PlayerPrefs.GetInt("terrain-ground-cover",1)!=0;
        Revision++;
    }
    public static void Save()
    {
        PlayerPrefs.SetInt("terrain-quality",Current.Quality);PlayerPrefs.SetInt("terrain-effort",Current.Effort);
        PlayerPrefs.SetFloat("terrain-distance",(float)Current.ViewDistanceM);PlayerPrefs.SetFloat("terrain-full-radius",(float)Current.FullDetailTiles);
        PlayerPrefs.SetInt("terrain-ground-cover",GroundCover?1:0);
        PlayerPrefs.Save();Revision++;
    }
}

public sealed partial class WorldGenRuntime
{
    int settingsCategory;Vector2 settingsScroll;
    static readonly string[] SettingsTabs={"Overview","Display","Terrain","Audio","Atmosphere","Grass debug"};
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
            case 5:DrawGrassDebugOptions();break;
        }
        GUILayout.EndScrollView();
    }
    void DrawGrassDebugOptions()
    {
        GUILayout.Label("GRASS PROTOTYPE · LIVE CONTROLS",SectionStyle());
        TerrainVideoOptions.GroundCover=GUILayout.Toggle(TerrainVideoOptions.GroundCover,"Enable ground vegetation (off = no grass draws/buffers)");
        GUILayout.Label("Field density / blade spacing  "+TerrainVideoOptions.GrassDensity.ToString("F2")+"×");
        TerrainVideoOptions.GrassDensity=Mathf.Round(GUILayout.HorizontalSlider(TerrainVideoOptions.GrassDensity,.25f,2)*20)/20;
        GUILayout.Label("Close-detail range  "+TerrainVideoOptions.GrassBladeDistance.ToString("F0")+" m");
        TerrainVideoOptions.GrassBladeDistance=Mathf.Round(GUILayout.HorizontalSlider(TerrainVideoOptions.GrassBladeDistance,15,60));
        TerrainVideoOptions.GrassCanopy=GUILayout.Toggle(TerrainVideoOptions.GrassCanopy,"Distant grass geometry (progressive leaf LOD)");
        GUILayout.Label("Grass geometry distance  "+TerrainVideoOptions.GrassDistance.ToString("F0")+" m");
        TerrainVideoOptions.GrassDistance=Mathf.Round(GUILayout.HorizontalSlider(TerrainVideoOptions.GrassDistance,50,500)/10)*10;
        GUILayout.Label("Blade detail / local blade count");
        TerrainVideoOptions.GrassDetail=GUILayout.SelectionGrid(TerrainVideoOptions.GrassDetail,new[]{"Low","Medium","High","Ultra"},2);
        TerrainVideoOptions.GrassWind=GUILayout.Toggle(TerrainVideoOptions.GrassWind,"Weather wind animation (grass and low plants)");
        TerrainVideoOptions.WeatherDrivenVegetationWind=GUILayout.Toggle(TerrainVideoOptions.WeatherDrivenVegetationWind,"Weather-driven bend and travelling gusts (off = direction-only rollback)");
        GUILayout.Label("Wind response: "+TerrainVideoOptions.VegetationWindResponse.ToString("F0")+" seconds");
        TerrainVideoOptions.VegetationWindResponse=GUILayout.HorizontalSlider(TerrainVideoOptions.VegetationWindResponse,2,12);
        TerrainVideoOptions.SpecialistPlants=GUILayout.Toggle(TerrainVideoOptions.SpecialistPlants,"Generated flowers, low herbs, ferns and fungi");
        if(GUILayout.Button("Reset grass prototype defaults"))
        {TerrainVideoOptions.GroundCover=true;TerrainVideoOptions.GrassDensity=1;TerrainVideoOptions.GrassDistance=350;TerrainVideoOptions.GrassBladeDistance=45;TerrainVideoOptions.GrassCanopy=true;TerrainVideoOptions.GrassDetail=2;TerrainVideoOptions.GrassWind=true;TerrainVideoOptions.WeatherDrivenVegetationWind=true;TerrainVideoOptions.VegetationWindResponse=6;}
        GUILayout.Space(10);
        GUILayout.Label("Session-only controls. Extra blades fill the first 4-14m. Middle-distance grass keeps the same leaves, progressively reducing their count/segments and widening survivors. A filtered grass mat continues on the terrain. Longer ranges cost more.",MutedStyle());
        GUILayout.Label("Off removes local leaf geometry. Grass, stationary moss and lichen ground materials stay. Loading is instant once data is ready; vegetation fades only with distance. F3 shows FPS and grass buffer/draw counts.",MutedStyle());
    }
    void DrawTerrainOptions(bool compact=false)
    {
        var o=TerrainVideoOptions.Current;bool changed=GUI.changed;GUI.changed=false;
        GUILayout.Space(12);GUILayout.Label("TERRAIN",SectionStyle());
        GUILayout.Label("Terrain quality");o.Quality=GUILayout.SelectionGrid(o.Quality,new[]{"Low","Medium","High","Ultra","Extreme"},compact?3:3);
        GUILayout.Label("Loading effort");o.Effort=GUILayout.SelectionGrid(o.Effort,new[]{"Low","Balanced","High","Maximum"},2);
        if(!compact)
        {
            TerrainVideoOptions.GroundCover=GUILayout.Toggle(TerrainVideoOptions.GroundCover,"Ground vegetation · nearby plants (distant cover colour stays)");
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
