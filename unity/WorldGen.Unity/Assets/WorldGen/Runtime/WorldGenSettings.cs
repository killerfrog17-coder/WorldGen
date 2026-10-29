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
    public static float GrassNearDensity=2;
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
    static readonly string[] SettingsTabs={"Overview","Display","Terrain","Audio","Atmosphere","Vegetation","Terrain lab","Character & help"};
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
            case 7:
                GUILayout.Label("INVENTORY & EQUIPMENT",SectionStyle());
                GUILayout.Label("F5: quicksave for this playthrough. Save game: keep the name to overwrite that slot, or change it for a separate save. Gameplay continues while the window is unfocused; pause explicitly when needed.");
                GUILayout.Label("I / Tab: inventory. Drag items between hands, container grids and Nearby; R rotates an item while dragging. Right-click an item for available transfers.");
                GUILayout.Label("E: pick up the aimed-at item into the right hand, then left. Q: swap held items. R: stow the right-hand item, or left if the right is empty. G: drop right then left; in inventory, drop the hovered item. B: set down the worn bag, or wear a nearby bag.");
                GUILayout.Label("Small rocks use one hand; large rocks use both. Your body has no storage. Worn carriers provide their own shaped grids; dropping one keeps its contents together. Transfers are immediate. Load includes everything actually carried.");
                GUILayout.Label("Right-click a suitable stone: strike off a cutting flake using another suitable stone. Progress appears on both items; right-click to pause/resume. Moving an involved item or walking pauses work. Item crafting time is under World settings → Time & calendar.");
                GUILayout.Label("Work automatically borrows tools and restores your previous hands. Displaced equipment uses attachments, then worn storage, then the ground. Held bags do not count as worn storage. Bows can be slung separately.");
                GUILayout.Label("COMBAT & TOOLS",SectionStyle());
                GUILayout.Label("Left mouse uses the right hand; right mouse uses the left when both hold separate items. With one item, right mouse is its secondary: hold/release to throw a small stone or spear, or hold to block with most other solid tools. Empty hands can strike and guard.");
                GUILayout.Label("Hold/release left mouse for bow, sling or spear-thrower. Carry arrows, small stones (15-300 g), or darts respectively in your hands or worn carriers. The other mouse button cancels. Projectiles can be retrieved, including embedded points. Attacks and blocking use stamina in Survival.");
                GUILayout.Label("Strike trees with a cutting edge for bark, or a chopping tool for reachable branches. Digging-stick secondary digs suitable unfrozen soil for fibres. Hand-line/net secondary works nearby fish; processing is deferred. Inventory tool options can also start digging/fishing. Right-click worn tools for maintenance; replacement materials are consumed.");
                GUILayout.Space(12);GUILayout.Label("MOVEMENT & CAMERA",SectionStyle());
                GUILayout.Label("WASD: move. C: walk. Shift: sprint. Ctrl: crouch. Space: jump. Mouse wheel: first/third person. Middle mouse: lock camera distance. Creative F: flight; Alt + wheel: flight speed. /gamemode survival | creative | spectator selects mode.");
                GUILayout.Label("Aim at reachable water and press E to drink. Freshwater is untreated; saltwater dehydrates you. Deep water: WASD/look to swim, Space to rise, Ctrl to dive. Survival vision blurs underwater; Creative/Spectator use a clear mask view. Hold your breath too long and you drown.");
                GUILayout.Label("F3: debug information. Shift + F3: full debug information. F3 + A: wildlife simulation radar. F3 + E: ecology inspection.");
                break;
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
