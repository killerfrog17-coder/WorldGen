using System;
using UnityEngine;
using WorldGen.Presentation;

// Session-only experiments. No PlayerPrefs, world edits, or benchmark overrides.
[Flags]
public enum TerrainDiagnosticFlags
{
    FlatColour=1, NoTextures=2, NoAlbedo=4, NoNormals=8, NoMasks=16,
    OneAxis=32, OneLayer=64, SimpleLight=128, Unlit=256,
    NoGrassMat=512, NoMoss=1024, NoLichen=2048, NoMidTint=4096,
    NoForestTint=8192, NoAir=16384, NoGrain=32768, NearTexturesOnly=65536, NoCoverage=131072
}

public static class TerrainDiagnostics
{
    public static TerrainDiagnosticFlags Flags;
    public static bool NoReceiveShadows, NoShadowMaps, HideLand, HideWater, HidePlants, HideTrees, HideGrass, NoWeather, CullBackFaces;
    public static bool HoldTerrain, HoldPlants, DistanceOnly, NoProximity;
    public static int Grid=64, Revision;
    public static bool Active=>Flags!=0||NoReceiveShadows||NoShadowMaps||HideLand||HideWater||HidePlants||HideTrees||HideGrass||NoWeather||CullBackFaces||HoldTerrain||HoldPlants||DistanceOnly||NoProximity||Grid!=64;
    public static void Changed(bool geometry=false){Revision++;if(geometry)TerrainVideoOptions.Revision++;}
    public static void Reset()
    {
        bool geometry=DistanceOnly||NoProximity||Grid!=64;
        Flags=0;NoReceiveShadows=NoShadowMaps=HideLand=HideWater=HidePlants=HideTrees=HideGrass=NoWeather=CullBackFaces=false;
        HoldTerrain=HoldPlants=DistanceOnly=NoProximity=false;Grid=64;Changed(geometry);
    }
    public static void Detail(TerrainDetailOptions options)
    {options.EstimateRelief=!DistanceOnly;options.ProximityRefinement=!NoProximity;options.FinestSegments=Grid;}
}

public sealed partial class LocalTerrainExplorer
{
    // Kept with the tile so menu counts never read back vertex/index buffers.
    int terrainDiagnosticRevision=-1;bool diagnosticShadowOverride;LightShadows diagnosticOriginalShadows;
    string diagnosticReadout="",diagnosticReference="";double diagnosticReadoutAt;
    void ApplyTerrainDiagnostics()
    {
        if(terrainDiagnosticRevision==TerrainDiagnostics.Revision)return;
        terrainDiagnosticRevision=TerrainDiagnostics.Revision;diagnosticReadoutAt=0;scatterDirty=true;
        if(localSun!=null)
        {
            if(TerrainDiagnostics.NoShadowMaps&&!diagnosticShadowOverride)
            {diagnosticOriginalShadows=localSun.shadows;diagnosticShadowOverride=true;localSun.shadows=LightShadows.None;}
            else if(!TerrainDiagnostics.NoShadowMaps&&diagnosticShadowOverride)
            {localSun.shadows=diagnosticOriginalShadows;diagnosticShadowOverride=false;}
        }
        foreach(var tile in tiles.Values)ApplyTerrainDiagnosticTile(tile);
        foreach(var tile in previousTerrain.Values)ApplyTerrainDiagnosticTile(tile);
    }
    void ApplyTerrainDiagnosticTile(Tile tile)
    {
        if(tile.Renderers==null||tile.DiagnosticRevision==TerrainDiagnostics.Revision)return;
        tile.DiagnosticRevision=TerrainDiagnostics.Revision;
        for(int i=0;i<tile.Renderers.Length;i++)
        {
            bool water=i>0;var renderer=tile.Renderers[i];
            renderer.enabled=!(water?TerrainDiagnostics.HideWater:TerrainDiagnostics.HideLand);
            var material=tile.Materials[i];
            if(TerrainDiagnostics.Flags!=0)material.EnableKeyword("WORLDGEN_TERRAIN_LAB");else material.DisableKeyword("WORLDGEN_TERRAIN_LAB");
            if(material.HasProperty("_TerrainCull"))material.SetFloat("_TerrainCull",!water&&TerrainDiagnostics.CullBackFaces?2:0);
            if(TerrainDiagnostics.NoReceiveShadows)material.EnableKeyword("_RECEIVE_SHADOWS_OFF");else material.DisableKeyword("_RECEIVE_SHADOWS_OFF");
        }
    }
    void RestoreTerrainDiagnostics()
    {
        if(diagnosticShadowOverride&&localSun!=null)localSun.shadows=diagnosticOriginalShadows;
        diagnosticShadowOverride=false;Shader.SetGlobalInteger("_TerrainDiagnosticFlags",0);TerrainDiagnostics.Reset();
    }
    string TerrainDiagnosticReadout()
    {
        if(Time.realtimeSinceStartupAsDouble<diagnosticReadoutAt)return diagnosticReadout;
        diagnosticReadoutAt=Time.realtimeSinceStartupAsDouble+1;
        long vertices=0,triangles=0;int finest=0,outside=0;
        double full=TerrainVideoOptions.Current.FullRadius(HorizontalScale);
        foreach(var pair in tiles)
        {
            vertices+=pair.Value.DiagnosticVertices;triangles+=pair.Value.DiagnosticTriangles;
            if(pair.Key.Level==0){finest++;if(TerrainLod.DistanceSquared(pair.Key,motion.X,motion.Z)>full*full)outside++;}
        }
        foreach(var tile in previousTerrain.Values){vertices+=tile.DiagnosticVertices;triangles+=tile.DiagnosticTriangles;}
        diagnosticReadout=fpsDisplay+" FPS · "+tiles.Count+" / "+wanted.Count+" terrain tiles\n"+
            vertices.ToString("N0")+" vertices · "+triangles.ToString("N0")+" triangles resident (includes water/fades; not a visible GPU count)\n"+
            finest+" finest tiles · "+outside+" entirely beyond protected radius\n"+
            (TerrainDiagnostics.HoldTerrain?"Terrain work held":AllReady&&!DetailPlanning&&jobs.Count==0?"Terrain loaded":"Terrain still loading")+
            " · "+ActiveWorkerCount+" active/queued worker slots · "+PendingTreeModels+" pending tree bakes";
        return diagnosticReadout;
    }
    public void DrawTerrainDiagnostics()
    {
        GUILayout.Label(TerrainDiagnosticReadout());
        if(diagnosticReference.Length>0)GUILayout.Label("Reference: "+diagnosticReference);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Pin reference FPS"))diagnosticReference=fpsDisplay+" FPS · "+tiles.Count+" tiles";
        if(GUILayout.Button("Restore normal rendering")){TerrainDiagnostics.Reset();ApplyTerrainDiagnostics();}
        GUILayout.EndHorizontal();
    }
    void DrawTerrainDiagnosticOverlay()
    {
        if(!TerrainDiagnostics.Active||paused||mapOpen||ecologyOpen)return;
        GUI.Label(new Rect(12,Screen.height-48,Math.Max(1,Screen.width-24),42),
            "TERRAIN LAB ACTIVE · "+fpsDisplay+" FPS · restore in Game settings > Terrain lab\n"+
            (TerrainDiagnostics.HoldTerrain?"Terrain work held; compare while stationary":DetailPlanning||jobs.Count>0?"Terrain loading; wait before comparing":""));
    }
}

public sealed partial class WorldGenRuntime
{
    static readonly string[] DiagnosticGridNames={"Normal","2× spacing","4× spacing","8× spacing"};
    void DrawTerrainLab()
    {
        GUILayout.Label("TERRAIN PERFORMANCE LAB",SectionStyle());
        GUILayout.Label("Temporary switches. Compare one at a time from the same spot, then restore normal rendering. Nothing here is saved.",MutedStyle());
        if(terrainExplorer==null||!terrainExplorer.Ready){GUILayout.Label("Enter a world to compare rendering.");return;}
        terrainExplorer.DrawTerrainDiagnostics();
        void Flag(TerrainDiagnosticFlags flag,string label)
        {bool before=(TerrainDiagnostics.Flags&flag)!=0,after=GUILayout.Toggle(before,label);if(after!=before){if(after)TerrainDiagnostics.Flags|=flag;else TerrainDiagnostics.Flags&=~flag;TerrainDiagnostics.Changed();}}
        bool Switch(bool before,string label,bool geometry=false)
        {bool after=GUILayout.Toggle(before,label);if(after!=before)TerrainDiagnostics.Changed(geometry);return after;}
        GUILayout.Space(12);GUILayout.Label("MATERIALS",SectionStyle());
        Flag(TerrainDiagnosticFlags.FlatColour,"Flat vertex colour — bypass all terrain materials and lighting");
        GUILayout.Label("Flat colour overrides the material/lighting tests below. Water and other objects keep their own rendering.",MutedStyle());
        Flag(TerrainDiagnosticFlags.NoTextures,"Skip all surface texture reads (keep coverage needed to prevent holes)");
        Flag(TerrainDiagnosticFlags.NoAlbedo,"Skip ground colour textures");
        Flag(TerrainDiagnosticFlags.NoNormals,"Skip ground normal-map textures");
        Flag(TerrainDiagnosticFlags.NoMasks,"Skip ground material masks (fixed roughness / AO / metal)");
        Flag(TerrainDiagnosticFlags.OneAxis,"One texture projection instead of three");
        Flag(TerrainDiagnosticFlags.OneLayer,"One dominant soil/rock layer instead of blending layers");
        Flag(TerrainDiagnosticFlags.NoGrain,"Skip procedural ground grain / colour noise");
        Flag(TerrainDiagnosticFlags.NearTexturesOnly,"Detailed textures only inside the protected radius (normally 850 m)");
        GUILayout.Space(12);GUILayout.Label("LIGHTING & SURFACE OVERLAYS",SectionStyle());
        Flag(TerrainDiagnosticFlags.SimpleLight,"Simple terrain lighting instead of PBR");
        Flag(TerrainDiagnosticFlags.Unlit,"Unlit terrain (overrides simple lighting; keeps material colours)");
        TerrainDiagnostics.NoReceiveShadows=Switch(TerrainDiagnostics.NoReceiveShadows,"Terrain stops receiving shadows (other objects unchanged)");
        TerrainDiagnostics.NoShadowMaps=Switch(TerrainDiagnostics.NoShadowMaps,"Disable sunlight shadow maps for the whole scene");
        Flag(TerrainDiagnosticFlags.NoGrassMat,"Skip grass material overlay on terrain");
        Flag(TerrainDiagnosticFlags.NoMoss,"Skip moss material overlay on terrain");
        Flag(TerrainDiagnosticFlags.NoLichen,"Skip lichen material overlay on terrain");
        Flag(TerrainDiagnosticFlags.NoMidTint,"Skip distant shrub tint on terrain");
        Flag(TerrainDiagnosticFlags.NoForestTint,"Skip distant forest tint on terrain");
        Flag(TerrainDiagnosticFlags.NoAir,"Skip atmosphere / distance fog in terrain shader");
        TerrainDiagnostics.NoWeather=Switch(TerrainDiagnostics.NoWeather,"Skip weather post-effect (clouds, weather fog, glare, adaptation)");
        GUILayout.Space(12);GUILayout.Label("DRAWING",SectionStyle());
        TerrainDiagnostics.HideLand=Switch(TerrainDiagnostics.HideLand,"Hide land and seabed drawing");
        TerrainDiagnostics.HideWater=Switch(TerrainDiagnostics.HideWater,"Hide water drawing");
        TerrainDiagnostics.HidePlants=Switch(TerrainDiagnostics.HidePlants,"Hide all vegetation / scatter drawing (including rocks)");
        TerrainDiagnostics.HideTrees=Switch(TerrainDiagnostics.HideTrees,"Hide local AND distant tree drawing");
        TerrainDiagnostics.HideGrass=Switch(TerrainDiagnostics.HideGrass,"Hide grass blade drawing");
        TerrainDiagnostics.CullBackFaces=Switch(TerrainDiagnostics.CullBackFaces,"Cull land back faces (normally terrain draws both sides)");
        Flag(TerrainDiagnosticFlags.NoCoverage,"Skip terrain coverage / fade clipping (overlapping fallback may appear)");
        GUILayout.Label("Drawing switches retain prepared data. Hidden land no longer fills depth or casts terrain shadows, so other drawing costs can change.",MutedStyle());
        GUILayout.Space(12);GUILayout.Label("MESH DETAIL & BACKGROUND WORK",SectionStyle());
        TerrainDiagnostics.DistanceOnly=Switch(TerrainDiagnostics.DistanceOnly,"Distance-only terrain LOD (ignore regional ruggedness estimate)",true);
        TerrainDiagnostics.NoProximity=Switch(TerrainDiagnostics.NoProximity,"Disable extra proximity refinement (protected radius stays)",true);
        int index=TerrainDiagnostics.Grid==64?0:TerrainDiagnostics.Grid==32?1:TerrainDiagnostics.Grid==16?2:3;
        GUILayout.Label("Finest terrain grid spacing: "+(TerrainLod.BaseSizeM*terrainExplorer.HorizontalScale/TerrainDiagnostics.Grid).ToString("F2")+" m");
        int next=GUILayout.SelectionGrid(index,DiagnosticGridNames,2);
        if(next!=index){TerrainDiagnostics.Grid=64>>next;TerrainDiagnostics.Changed(true);}
        GUILayout.Label("Mesh tests reload terrain and can visibly change ground accuracy. Wait for loading to finish; use while stationary. They do not change world heights or saves.",MutedStyle());
        TerrainDiagnostics.HoldTerrain=Switch(TerrainDiagnostics.HoldTerrain,"Hold loaded terrain (skip planning, uploads and maintenance)");
        TerrainDiagnostics.HoldPlants=Switch(TerrainDiagnostics.HoldPlants,"Hold vegetation preparation (keep existing plants drawing)");
        GUILayout.Label("Hold only after loading. In-flight jobs may still finish in the background. Release holds before moving or testing new mesh settings.",MutedStyle());
    }
}
