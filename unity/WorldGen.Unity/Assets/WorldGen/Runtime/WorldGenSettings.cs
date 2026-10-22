using System;
using UnityEngine;
using WorldGen.Presentation;

public static partial class TerrainVideoOptions
{
    public static readonly TerrainDetailOptions Current=new TerrainDetailOptions();
    public static int Revision;
    public static int SurfaceQuality=2;
    public static bool SurfaceTextures=true;
    public static readonly string[] SurfaceQualityNames={"Very low","Low","Medium","High","Ultra"};
    // Physical metres, independent of mesh/world scale. Full detail, texture
    // retirement, normal/mask retirement, secondary-layer/axis retirement.
    public static Vector4 SurfaceRanges=>SurfaceQuality==0?new Vector4(3,18,0,6):
        SurfaceQuality==1?new Vector4(6,40,12,12):
        SurfaceQuality==2?new Vector4(12,90,25,24):
        SurfaceQuality==3?new Vector4(18,120,35,36):new Vector4(24,160,50,48);
    public static Vector4 SurfaceControls=>new Vector4(SurfaceTextures?1:0,SurfaceQuality,
        SurfaceQuality<2?1:SurfaceQuality<4?2:3,SurfaceQuality<2?1:SurfaceQuality<4?2:3);
    public static void SaveSurface()
    {
        PlayerPrefs.SetInt("terrain-surface-quality",SurfaceQuality);
        PlayerPrefs.SetInt("terrain-surface-textures",SurfaceTextures?1:0);
        PlayerPrefs.Save(); // Does NOT invalidate mesh plans or vegetation.
    }
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
    public static bool MidPlants=true;
    public static int MidPlantDetail=2;
    public static bool GeneratedTrees=true,FarForest=true;
    public static bool TreeRebuild=true,TreeImpostors=true;
    public static int TreeDetail=2;
    public static bool ScatterFrustum=true,VegetationOcclusion=true,VegetationShadows=true;
    public static void Load()
    {
        Current.Quality=Mathf.Clamp(PlayerPrefs.GetInt("terrain-quality",2),0,4);
        Current.Effort=Mathf.Clamp(PlayerPrefs.GetInt("terrain-effort",1),0,3);
        Current.ViewDistanceM=Mathf.Clamp(PlayerPrefs.GetFloat("terrain-distance",200000),1000,250000);
        Current.FullDetailTiles=Mathf.Clamp(PlayerPrefs.GetFloat("terrain-full-radius",1.5f),1.5f,8);
        GroundCover=PlayerPrefs.GetInt("terrain-ground-cover",1)!=0;
        SurfaceQuality=Mathf.Clamp(PlayerPrefs.GetInt("terrain-surface-quality",2),0,4);
        SurfaceTextures=PlayerPrefs.GetInt("terrain-surface-textures",1)!=0;
        LoadVegetation();
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
    static readonly string[] SettingsTabs={"Overview","Display","Terrain","Audio","Atmosphere","Vegetation","Terrain lab"};
    void DrawSettingsPage()
    {
        int selected=GUILayout.SelectionGrid(settingsCategory,SettingsTabs,3);
        if(selected!=settingsCategory){settingsCategory=selected;settingsScroll=Vector2.zero;GUI.FocusControl(null);}
        GUILayout.Space(12);settingsScroll=GUILayout.BeginScrollView(settingsScroll);
        switch(settingsCategory)
        {
            case 0:
                DrawMasterVolume();DrawMusicVolume();DrawDisplayOptions(true);DrawTerrainOptions(true);
                GUILayout.Space(12);GUILayout.Label("Category tabs contain additional settings. Changes apply immediately.",MutedStyle());break;
            case 1:
                DrawDisplayOptions();
                bool smooth=GUILayout.Toggle(naturalTerrain,"Smooth world-map surface");if(smooth!=naturalTerrain){naturalTerrain=smooth;ApplyCachedDisplay();}break;
            case 2:DrawTerrainOptions();terrainExplorer?.DrawViewSettings();break;
            case 3:DrawAudioOptions();break;
            case 4:DrawWeatherVideoOptions();break;
            case 5:DrawVegetationOptions();break;
            case 6:DrawTerrainLab();break;
        }
        GUILayout.EndScrollView();
    }
    void DrawTerrainOptions(bool compact=false)
    {
        var o=TerrainVideoOptions.Current;bool changed=GUI.changed;GUI.changed=false;
        GUILayout.Space(12);GUILayout.Label("TERRAIN",SectionStyle());
        GUILayout.Label("Terrain geometry quality");o.Quality=GUILayout.SelectionGrid(o.Quality,new[]{"Low","Medium","High","Ultra","Extreme"},compact?3:3);
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
        bool beforeSurfaceChanged=GUI.changed;GUI.changed=false;
        GUILayout.Space(12);GUILayout.Label("GROUND SURFACE",SectionStyle());
        bool textures=GUILayout.Toggle(TerrainVideoOptions.SurfaceTextures,"Ground textures (lighting and shadows stay on when disabled)");
        GUILayout.Label("Surface quality");
        int surface=GUILayout.SelectionGrid(TerrainVideoOptions.SurfaceQuality,TerrainVideoOptions.SurfaceQualityNames,3);
        if(surface!=TerrainVideoOptions.SurfaceQuality||textures!=TerrainVideoOptions.SurfaceTextures)
        {TerrainVideoOptions.SurfaceQuality=surface;TerrainVideoOptions.SurfaceTextures=textures;TerrainVideoOptions.SaveSurface();}
        if(!compact)
        {
            var ranges=TerrainVideoOptions.SurfaceRanges;
            GUILayout.Label("Full texture detail nearby; smoothly simplifies through "+ranges.y.ToString("F0")+" m. Distant ground keeps habitat colour, lighting and shadows.",MutedStyle());
            GUILayout.Label("Very low: minimal sampling. Medium: balanced. Ultra: restrained close-range refinement for 1080p, not unlimited draw distance. Changes need no terrain reload.",MutedStyle());
        }
        GUI.changed=beforeSurfaceChanged;
    }
}
