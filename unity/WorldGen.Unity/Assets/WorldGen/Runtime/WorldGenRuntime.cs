using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class WorldGenRuntime : MonoBehaviour
{
    Recipe recipe = new Recipe();
    World world; Task exportJob; Task<World> job; CancellationTokenSource cancel;
    WorldLibrary library; WorldLibraryEntry[] saves = Array.Empty<WorldLibraryEntry>();
    bool libraryOpen; Vector2 libraryScroll; string saveName = "My world", libraryNotice = "";
    string progress = "Ready", error = "", inspection = "Click a cell to inspect its terrain and climate.", filePath;
    Vector2 inspectionScroll;
    bool expandedInspection;
    int percent, seaLevel; Layer layer; Projection projection; float centerLongitude;
    GameObject globe, pendingRoot; Material surfaceMaterial, lineMaterial, naturalMaterial, smallFeatureMaterial, riverMaterial; Camera cameraView;
    UnityEngine.Rendering.Universal.UniversalAdditionalCameraData presentationCamera;bool originalPostProcessing;
    bool naturalTerrain=true;
    BlockingCollection<UploadPiece> pieces; ViewOptions viewOptions = new ViewOptions();
    bool wireframe, showPlates, showRivers, resizingPanel, loadingSnapshot; int gridSpacing = 30;
    float panelWidth = 350, yaw = 30, pitch = -12, globeDistance = 3.2f;
    bool Busy => job != null || Generating;
    ViewOptions committedOptions; int uploadedChunks; long lastFrameTick; double maxBusyFrameMs; int busyFrames;
    bool uiCheck;
    bool reusePendingGeometry;
    [Serializable] sealed class UiEvidence { public int Detail, BusyFrames, MeshChunks; public double MaxBusyFrameMs, CancelMilliseconds, LongitudeMilliseconds, CachedSwitchMilliseconds, ProjectionSwitchMilliseconds; public bool Completed, CancelledPresentation, Resized, NorthUp, InstantDisplay, StableProjection; }
    sealed class UploadPiece
    {
        public string Kind = "surface"; public Vector2[] UV, NaturalHeight, BlendCells; public Vector4[] Stroke; public Vector3[] MapVertices, MapLines, Vertices, Lines; public Color32[] Colors, LineColors; public int[] Triangles, LineIndices;
    }
    Vector2 scroll, layerScroll; bool layerMenu, dirty, dragging, screenshotPending;
    string[] groups = Recipe.Settings.Select(f => f.GetCustomAttribute<SettingAttribute>().Group).Distinct().ToArray();
    bool[] panels; string preset = "Continents plus"; int presetIndex;
    readonly string[] presets = WorldPresets.Names;
    Vector2 previousMouse; float dragDistance, mapZoom = 1f; Quaternion globeRotation = Quaternion.Euler(-12, 150, 0);
    Vector3 mapPan; bool autoRotate;
    readonly Dictionary<string, string> inputText = new Dictionary<string, string>();
    float PanelWidth => Mathf.Clamp(panelWidth, 240, Math.Max(240, Screen.width - 360));
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { if (FindAnyObjectByType<WorldGenRuntime>() == null) new GameObject("WorldGen Runtime").AddComponent<WorldGenRuntime>(); }
    void Start()
    {
        Application.runInBackground = true; Application.targetFrameRate = 60;
        uiCheck = Environment.GetCommandLineArgs().Contains("--worldgen-ui-check");
        panels = groups.Select(g => g == "World").ToArray();
        filePath = Path.Combine(Application.persistentDataPath, "worldgen.recipe.json");
        if (Environment.GetCommandLineArgs().Contains("--worldgen-check")) { RunPlayerChecks(); return; }
        if(Environment.GetCommandLineArgs().Contains("--worldgen-history-resume")){CheckHistoricalResume();return;}
        string requestedPreset = Argument("--worldgen-preset", "");
        if (requestedPreset.Length > 0) { recipe = Recipe.Preset(requestedPreset); preset = requestedPreset; presetIndex = Array.IndexOf(presets, requestedPreset); inputText.Clear(); }
        if (int.TryParse(Argument("--worldgen-detail", ""), out int detail)) recipe.Detail = detail;
        if (Enum.TryParse(Argument("--worldgen-layer", ""), out Layer requestedLayer)) layer = requestedLayer;
        if (Argument("--worldgen-view", "") == "Map") projection = Projection.Map;
        cameraView = Camera.main;
        if (cameraView == null) cameraView = new GameObject("WorldGen Camera", typeof(Camera)).GetComponent<Camera>();
        presentationCamera=cameraView.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();if(presentationCamera!=null)originalPostProcessing=presentationCamera.renderPostProcessing;
        cameraView.transform.position = new Vector3(0, 0, -3.2f); cameraView.transform.LookAt(Vector3.zero);
        cameraView.clearFlags = CameraClearFlags.SolidColor; cameraView.nearClipPlane = .005f; cameraView.backgroundColor = new Color(.035f, .065f, .09f); cameraView.fieldOfView = 45;
        surfaceMaterial = new Material(Resources.Load<Shader>("WorldGenVertex"));
        lineMaterial = new Material(surfaceMaterial); lineMaterial.SetFloat("_Unlit", 1);
        riverMaterial=new Material(Resources.Load<Shader>("WorldGenRiver"));
        smallFeatureMaterial=new Material(lineMaterial);smallFeatureMaterial.SetFloat("_SmallFeatures",1);
        naturalMaterial=new Material(surfaceMaterial);naturalMaterial.SetFloat("_Natural",1);naturalMaterial.SetFloat("_Linear",QualitySettings.activeColorSpace==ColorSpace.Linear?1:0);
        wireframe = Environment.GetCommandLineArgs().Contains("--worldgen-wireframe"); showPlates = Environment.GetCommandLineArgs().Contains("--worldgen-plates"); showRivers = Environment.GetCommandLineArgs().Contains("--worldgen-rivers");
        if (int.TryParse(Argument("--worldgen-grid", "30"), out int grid) && (grid == 0 || grid == 5 || grid == 10 || grid == 15 || grid == 30)) gridSpacing = grid;
        InitializeLibrary();
        InitializeMusic();
        if (WorldPresets.Names.Contains(preset)) recipe=setup.Preset(preset,recipe); else setup.From(recipe);
        if (!Environment.GetCommandLineArgs().Contains("--worldgen-library-earth") &&
            (requestedPreset.Length > 0 || uiCheck || Environment.GetCommandLineArgs().Contains("--worldgen-screenshot"))) Generate();
        else screen = "Menu";
        if (uiCheck) StartCoroutine(CheckInteraction());
        if(Environment.GetCommandLineArgs().Contains("--worldgen-screen-check"))StartCoroutine(CheckScreens());
        if(Environment.GetCommandLineArgs().Contains("--worldgen-prehistory-check"))StartCoroutine(CheckPrehistory());
        if(Environment.GetCommandLineArgs().Contains("--worldgen-history-view")){screen="Setup";BeginWork(false,Argument("--worldgen-history-view",""));StartCoroutine(CheckHistoricalView());}
        if(Environment.GetCommandLineArgs().Contains("--worldgen-map-marks-check")){screen="Setup";BeginWork(false,Argument("--worldgen-map-marks-check",""));StartCoroutine(CheckMapMarks());}
        if(Environment.GetCommandLineArgs().Contains("--worldgen-religion-check")){screen="Setup";BeginWork(false,Argument("--worldgen-religion-check",""));StartCoroutine(CheckReligiousView());}
        if(Environment.GetCommandLineArgs().Contains("--worldgen-era-review")){screen="Setup";BeginWork(false,Argument("--worldgen-era-review",""));StartCoroutine(CheckHistoricalEras());}
        if(Environment.GetCommandLineArgs().Contains("--worldgen-overnight-review")){screen="Setup";BeginWork(false,Argument("--worldgen-overnight-review",""));StartCoroutine(CheckOvernightHistory());}
        if(Environment.GetCommandLineArgs().Contains("--worldgen-history-playback")){screen="Setup";BeginWork(false,Argument("--worldgen-history-playback",""));StartCoroutine(CheckHistoricalPlayback());}
        if(Environment.GetCommandLineArgs().Contains("--worldgen-dock-check")){screen="Setup";BeginWork(false,Argument("--worldgen-dock-check",""));StartCoroutine(CheckHistoryDock());}
        if(Environment.GetCommandLineArgs().Contains("--worldgen-atlas-check")){screen="Setup";BeginWork(false,Argument("--worldgen-atlas-check",""));StartCoroutine(CheckAtlasNavigation());}
    }
    void Generate() => StartGeneration();
    async void InitializeLibrary()
    {
        try
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            for (var directory = new DirectoryInfo(root); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "WorldGen.sln"))) { root = directory.FullName; break; }
            try { library = new WorldLibrary(Argument("--worldgen-library-root", Path.Combine(root, "world"))); }
            catch (UnauthorizedAccessException) { library = new WorldLibrary(Path.Combine(Application.persistentDataPath, "world")); }
            string bundled = Path.Combine(Application.streamingAssetsPath, "world", "Earth.world");
            if (Environment.GetCommandLineArgs().Contains("--worldgen-library-earth") && File.Exists(bundled))
            {
                libraryNotice = "Installing bundled Earth...";
                exportJob = Task.Run(() => library.InstallEarth(bundled)); await exportJob;
                libraryNotice = "";
            }
            else libraryNotice = "";
            saves = library.Entries();
            if (Environment.GetCommandLineArgs().Contains("--worldgen-library-earth"))
            {
                // Integration check and convenient direct launch use the same saved-world path.
                var earth = saves.First(e => e.Id == "earth");
                var loaded = await Task.Run(() => library.Load(earth.Id));
                var copy = await Task.Run(() => library.SaveNew("Earth round-trip check", loaded));
                var restored = await Task.Run(() => library.Load(copy.Id));
                if (StateHash.Compute(loaded) != StateHash.Compute(restored)) throw new InvalidOperationException("World library round-trip mismatch.");
                Debug.Log("WORLD LIBRARY PASS full Earth snapshot load/save/reload " + StateHash.Compute(restored));
                saves = library.Entries(); libraryOpen = false; BeginWork(false, earth.SnapshotPath);
            }
        }
        catch (Exception e)
        {
            libraryNotice = e.Message; Debug.LogException(e);
            if (Environment.GetCommandLineArgs().Contains("--worldgen-library-earth")) Application.Quit(1);
        }
        finally { exportJob = null; }
    }
    async void SaveToLibrary()
    {
        if (library == null || world == null || Busy || exportJob != null) return;
        // Geography is immutable; retain this exact immutable demographic checkpoint while saving.
        var snapshot=new World();foreach(var field in typeof(World).GetFields())field.SetValue(snapshot,field.GetValue(world));string name=saveName;
        try
        {
            libraryNotice = "Saving complete world...";
            var saving = Task.Run(() => activeWorldId == null ? library.SaveNew(name, snapshot) : library.SaveAdditional(activeWorldId, snapshot)); exportJob = saving; var saved = await saving; activeWorldId = saved.GroupId; saveDialog = false;
            saves = library.Entries(); libraryNotice = "Saved. Existing worlds were not overwritten.";
        }
        catch (Exception e) { libraryNotice = "Save failed: " + e.Message; }
        finally { exportJob = null; }
    }
    void RollSeed() { recipe.Seed = UnityEngine.Random.Range(0, 1 << 24); inputText.Remove(nameof(Recipe.Seed)); dirty = true; }
    void Refresh() { if (world != null && !ApplyCachedDisplay()) BeginWork(false); }
    void LoadWorldSnapshot()
    {
        if (Busy) return; string compact = Path.ChangeExtension(filePath, "world"), json = Path.ChangeExtension(filePath, "result.json");
        string path = Path.GetExtension(filePath).Equals(".world", StringComparison.OrdinalIgnoreCase) && File.Exists(filePath) ? filePath
            : File.Exists(compact) ? compact : filePath.EndsWith(".result.json", StringComparison.OrdinalIgnoreCase) && File.Exists(filePath) ? filePath : json;
        BeginWork(false, path);
    }
    void BeginWork(bool generate, string snapshotPath = null)
    {
        if (job != null) return;
        try
        {
            if (generate) { ValidateInputs(); ClimateCalibration.Upgrade(recipe); recipe.Validate(); }
            cancel?.Dispose(); cancel = new CancellationTokenSource(); var token = cancel.Token;
            var snapshot = recipe.Clone(); var existing = world;
            loadingSnapshot = snapshotPath != null;
            reusePendingGeometry=!generate && snapshotPath==null && displayedWorld!=null && globe!=null &&
                ReferenceEquals(displayedWorld.Surface,world.Surface) && ReferenceEquals(displayedWorld.ElevationM,world.ElevationM) &&
                ReferenceEquals(displayedWorld.Hydrology.Waters,world.Hydrology.Waters) && ReferenceEquals(displayedWorld.Hydrology.Rivers,world.Hydrology.Rivers) &&
                ReferenceEquals(displayedWorld.Ecology,world.Ecology) && ReferenceEquals(displayedWorld.Subcells,world.Subcells);
            viewOptions = new ViewOptions { Projection = projection, Layer = layer, Wireframe = wireframe, ShowPlates = showPlates, ShowRivers = showRivers, GridSpacing = gridSpacing, SeaLevel = 0 };
            var options = viewOptions.Clone(); bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            uploadedChunks = 0;
            pendingRoot = reusePendingGeometry?null:new GameObject("Preparing world"); if(pendingRoot!=null)pendingRoot.SetActive(false);
            pieces = new BlockingCollection<UploadPiece>(2); var queue = pieces; error = ""; percent = 0; progress = snapshotPath != null ? "Loading world snapshot" : generate ? "Starting generation" : "Preparing view";
            job = Task.Run(() =>
            {
                try
                {
                    var elapsed = Stopwatch.StartNew();
                    var result = snapshotPath != null ? Path.GetExtension(snapshotPath).Equals(".world", StringComparison.OrdinalIgnoreCase)
                        ? WorldFile.Read(File.ReadAllBytes(snapshotPath), token,stage=>progress=stage) : WorldJson.ReadWorld(File.ReadAllText(snapshotPath))
                        : generate ? new WorldPipeline().Generate(snapshot, p => { progress = p.Stage; percent = p.Percent * 80 / 100; if (uiCheck) Debug.Log("WORLDGEN UI STAGE " + p.Stage + " " + elapsed.ElapsedMilliseconds + "ms"); }, token) : existing;
                    token.ThrowIfCancellationRequested();
                    int index = 0, total = DisplayCache.ExpectedChunkCount(result);
                    pendingPalettes = BuildPalettes(result, options.Layer, linear, token);
                    if(reusePendingGeometry)return result;
                    foreach (var cached in DisplayCache.Geometry(result, Projection.Globe, token))
                    {
                        token.ThrowIfCancellationRequested(); progress = "Preparing / uploading cell surfaces"; percent = Math.Min(99,80 + 19 * index++ / Math.Max(1, total));
                        var data=MapView.Project(cached.Data,0,cached.Kind=="smallfeatures"?-.004:cached.Kind=="rivers"?-.003:0,true);
                        var piece=Prepare(data, linear, token); piece.Kind=cached.Kind;
                        if(cached.Kind=="surface" || cached.Kind=="natural"){piece.UV=new Vector2[data.CellIds.Length]; int height=(result.Surface.Cells.Length+2047)/2048; for(int v=0;v<piece.UV.Length;v++){int id=data.CellIds[v];piece.UV[v]=new Vector2((id%2048+.5f)/2048,(id/2048+.5f)/height);}}
                        queue.Add(piece, token);
                        if (uiCheck && index % 100 == 0) Debug.Log("WORLDGEN UI MESH " + index + "/" + total);
                    }
                    token.ThrowIfCancellationRequested(); return result;
                }
                finally { queue.CompleteAdding(); }
            }, token);
        }
        catch (Exception e) { error = e.Message; }
    }
    static UploadPiece Prepare(ViewData data, bool linear, CancellationToken token)
    {
        var piece = new UploadPiece { Vertices = new Vector3[data.Positions.Length / 3], Colors = new Color32[data.Colors.Length / 3], Triangles = data.Triangles,
            Lines = new Vector3[data.Lines.Length / 3], LineColors = new Color32[data.Lines.Length / 3], LineIndices = new int[data.Lines.Length / 3] };
        piece.MapVertices=new Vector3[piece.Vertices.Length];piece.MapLines=new Vector3[piece.Lines.Length];
        if(data.BlendCellIds.Length>0){piece.BlendCells=new Vector2[data.BlendCellIds.Length/2];for(int i=0;i<piece.BlendCells.Length;i++)piece.BlendCells[i]=new Vector2(data.BlendCellIds[i*2],data.BlendCellIds[i*2+1]);}
        if(data.NaturalHeight.Length>0){piece.NaturalHeight=new Vector2[data.NaturalHeight.Length];for(int i=0;i<piece.NaturalHeight.Length;i++)piece.NaturalHeight[i]=new Vector2(data.NaturalHeight[i],0);}
        if(data.Stroke.Length>0){piece.Stroke=new Vector4[data.Stroke.Length/4];for(int i=0;i<piece.Stroke.Length;i++)piece.Stroke[i]=new Vector4(data.Stroke[i*4],data.Stroke[i*4+1],data.Stroke[i*4+2],data.Stroke[i*4+3]);}
        byte Channel(float value) { double v = Math.Max(0, Math.Min(1, value)); if (linear) v = v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); return (byte)Math.Round(v * 255); }
        // Shared geometry uses the browser's right-handed camera convention.
        // Unity looks from negative Z: reflect globe depth, including overlays,
        // and reverse winding to preserve outward lighting. Maps are unchanged.
        float depthSign = data.IsMap ? 1 : -1;
        if (!data.IsMap)
        {
            piece.Triangles = (int[])data.Triangles.Clone();
            for (int t = 0; t < piece.Triangles.Length; t += 3)
            { int swap = piece.Triangles[t + 1]; piece.Triangles[t + 1] = piece.Triangles[t + 2]; piece.Triangles[t + 2] = swap; }
        }
        for (int i = 0; i < piece.Vertices.Length; i++)
        {
            if ((i & 2047) == 0) token.ThrowIfCancellationRequested(); int k = i * 3;
            piece.MapVertices[i] = new Vector3(data.Positions[k], data.Positions[k + 1], data.Positions[k + 2]);
            piece.Vertices[i] = new Vector3(data.SpherePositions[k],data.SpherePositions[k+1],-data.SpherePositions[k+2]);
            piece.Colors[i] = new Color32(Channel(data.Colors[k]), Channel(data.Colors[k + 1]), Channel(data.Colors[k + 2]), 255);
        }
        for (int i = 0; i < piece.Lines.Length; i++)
        {
            int k = i * 3; piece.MapLines[i]=new Vector3(data.Lines[k],data.Lines[k+1],data.Lines[k+2]);
            piece.Lines[i] = new Vector3(data.SphereLines[k], data.SphereLines[k + 1], -data.SphereLines[k + 2]); piece.LineIndices[i] = i;
            piece.LineColors[i] = new Color32(Channel(data.LineColors[k]), Channel(data.LineColors[k + 1]), Channel(data.LineColors[k + 2]), 255);
        }
        return piece;
    }
    void Upload(UploadPiece piece)
    {
        uploadedChunks++;
        void Part(Vector3[] vertices, Color32[] colors, int[] indices, bool lines)
        {
            if (vertices.Length == 0) return;
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices; mesh.colors32 = colors; if(!lines && piece.UV!=null)mesh.uv=piece.UV; mesh.SetIndices(indices, lines ? MeshTopology.Lines : MeshTopology.Triangles, 0);
            mesh.SetUVs(1,lines?piece.MapLines:piece.MapVertices);
            if(!lines && piece.NaturalHeight!=null)mesh.SetUVs(2,piece.NaturalHeight);
            if(!lines && piece.BlendCells!=null)mesh.SetUVs(3,piece.BlendCells);
            if(!lines && piece.Stroke!=null)mesh.SetUVs(4,piece.Stroke);
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(4.2f,3,3));
            mesh.UploadMeshData(true);
            int copies = 3;
            for (int copy = 0; copy < copies; copy++)
            {
                var part = new GameObject(lines ? "Cell edges / grid" : "Cell surfaces", typeof(MeshFilter), typeof(MeshRenderer));
                var parent=pendingRoot.transform.Find(piece.Kind); if(parent==null){var group=new GameObject(piece.Kind);group.transform.SetParent(pendingRoot.transform,false);parent=group.transform;}
                string copyName="copy"+(copy-1);var copyRoot=parent.Find(copyName);
                if(copyRoot==null){var container=new GameObject(copyName);container.transform.SetParent(parent,false);container.transform.localPosition=new Vector3((copy-1)*4,0,0);copyRoot=container.transform;}
                part.transform.SetParent(copyRoot, false);
                part.GetComponent<MeshFilter>().sharedMesh = mesh; part.GetComponent<MeshRenderer>().sharedMaterial = piece.Kind=="rivers"?riverMaterial:piece.Kind=="natural"?naturalMaterial:piece.Kind=="smallfeatures"?smallFeatureMaterial:piece.Kind == "surface" ? surfaceMaterial : lineMaterial;
            }
        }
        Part(piece.Vertices, piece.Colors, piece.Triangles, false); Part(piece.Lines, piece.LineColors, piece.LineIndices, true);
    }
    IEnumerator Release(GameObject root)
    {
        if (root == null) yield break; root.SetActive(false); var freed = new HashSet<Mesh>(); var timer = Stopwatch.StartNew();
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (freed.Add(filter.sharedMesh)) Destroy(filter.sharedMesh); Destroy(filter.gameObject);
            if (timer.ElapsedMilliseconds >= 6) { yield return null; timer.Restart(); }
        }
        Destroy(root);
    }
    void Update()
    {
        UpdateMusic();
        if((screen=="Setup"||screen=="Simulation")&&!saveDialog&&!atlasOpen&&!simulationPaused&&GUIUtility.keyboardControl==0&&Keyboard.current!=null&&!Keyboard.current.ctrlKey.isPressed&&!Keyboard.current.altKey.isPressed)
        {
            if(Keyboard.current.rKey.wasPressedThisFrame){showRivers=!showRivers;ApplyCachedDisplay();}
            if(screen=="Simulation"&&Keyboard.current.cKey.wasPressedThisFrame)settlementMarkerMode=SettlementMarkers.Next(settlementMarkerMode);
        }
        if(screen=="Simulation"&&!saveDialog&&Keyboard.current!=null)
        {
            if(Keyboard.current.escapeKey.wasPressedThisFrame){if(atlasOpen)CloseWorldAtlas();else ToggleSimulationPause();}
            else if(!simulationPaused&&!Keyboard.current.ctrlKey.isPressed&&!Keyboard.current.altKey.isPressed&&Keyboard.current.wKey.wasPressedThisFrame&&!atlasSearchFocused&&(atlasOpen||GUIUtility.keyboardControl==0))WorldAtlasKey();
        }
        PollHumans();
        if(screen=="Simulation"&&!atlasOpen&&!simulationPaused&&!saveDialog&&Keyboard.current!=null&&!atlasSearchFocused)
        {
            if(Keyboard.current.spaceKey.wasPressedThisFrame)humanPlaying=!humanPlaying;
            bool add=Keyboard.current.equalsKey.wasPressedThisFrame||Keyboard.current.numpadPlusKey.wasPressedThisFrame,subtract=Keyboard.current.minusKey.wasPressedThisFrame||Keyboard.current.numpadMinusKey.wasPressedThisFrame;
            bool background=Keyboard.current.leftShiftKey.isPressed||Keyboard.current.rightShiftKey.isPressed;
            if(add||subtract){float delta=add?.25f:-.25f;if(background)satelliteOpacity=Mathf.Clamp01(satelliteOpacity+delta);else humanOverlayOpacity=Mathf.Clamp01(humanOverlayOpacity+delta);ApplyCachedDisplay();}
            if(!Keyboard.current.ctrlKey.isPressed&&!Keyboard.current.altKey.isPressed)
                for(int n=1;n<=(background?MapShortcuts.Backgrounds.Length:MapShortcuts.Overlays.Length);n++)if(Keyboard.current[(Key)((int)Key.Digit1+n-1)].wasPressedThisFrame)
                {if(background){var mode=MapShortcuts.Backgrounds[n-1];if(GenerationPreview.Available(displayedWorld,mode)){layer=preferredLayer=mode;Refresh();}}else{historyOverlay=MapShortcuts.Overlays[n-1];UpdateHumanPalette();}}
            if(Keyboard.current.commaKey.wasPressedThisFrame)ChangeHumanSpeed(-1);
            if(Keyboard.current.periodKey.wasPressedThisFrame)ChangeHumanSpeed(1);
        }
        if (cameraView == null) return;
        long tick = Stopwatch.GetTimestamp();
        if (Busy && lastFrameTick != 0) { maxBusyFrameMs = Math.Max(maxBusyFrameMs, (tick - lastFrameTick) * 1000.0 / Stopwatch.Frequency); busyFrames++; }
        lastFrameTick = tick;
        if (Keyboard.current != null && Keyboard.current.f11Key.wasPressedThisFrame)
            Screen.fullScreenMode = Screen.fullScreen ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
        float sidebar=screen=="Setup"?PanelWidth:screen=="Simulation"?SimulationSidebar:0;
        cameraView.rect = new Rect(sidebar / Screen.width, screen=="Simulation"?DockHeight/Screen.height:0, 1 - sidebar / Screen.width, screen=="Simulation"?1-(52f+DockHeight)/Screen.height:1);
        PollGeneration();
        if (globe != null)
        {
            globe.SetActive(!libraryOpen && screen!="Menu" && screen!="Options");
            globe.transform.rotation = (committedOptions?.Projection ?? projection) == Projection.Map ? Quaternion.identity : Quaternion.Euler(pitch, 0, 0) * Quaternion.Euler(0, yaw, 0);
            globe.transform.position = (committedOptions?.Projection ?? projection) == Projection.Map ? new Vector3(-centerLongitude / 90, 0, 0) : Vector3.zero;
        }
        if (job != null)
        {
            if (!cancel.IsCancellationRequested)
            {
                var timer = Stopwatch.StartNew();
                while (pieces.TryTake(out var piece)) { Upload(piece); if (timer.ElapsedMilliseconds >= 6) break; }
            }
            else while (pieces.TryTake(out _)) { }
            if (job.IsCompleted && (cancel.IsCancellationRequested || pieces.Count == 0))
            {
                if (job.IsCanceled || cancel.IsCancellationRequested) { progress = "Cancelled"; StartCoroutine(Release(pendingRoot)); RestoreView(); }
                else if (job.IsFaulted) { error = job.Exception.GetBaseException().Message; StartCoroutine(Release(pendingRoot)); RestoreView(); }
                else
                {
                    world = job.Result;
                    if (loadingSnapshot) { recipe = world.Recipe.Clone(); preset = recipe.TerrainSource == "Earth" ? "Earth" : "Saved world"; presetIndex = Array.IndexOf(presets, preset); inputText.Clear(); }
                    viewOptions.Layer=GenerationPreview.Available(world,layer)?layer:viewOptions.Layer;layer=viewOptions.Layer;
                    viewOptions.Projection=projection;committedOptions = viewOptions.Clone();
                    InstallPalettes(); displayedWorld=world;
                    if(loadingSnapshot){activeWorldId=pendingWorldId;saveName=pendingWorldName??"My world";worldReady=true;screen="Simulation";simulationTab="";simulationPaused=false;projection=viewOptions.Projection=committedOptions.Projection=Projection.Map;mapPan=Vector3.zero;mapZoom=1;layer=preferredLayer=Layer.Climate;satelliteOpacity=0;humanOverlayOpacity=1;historyOverlay=HistoryOverlay.States;setup.From(recipe);dirty=false;}
                    if(!reusePendingGeometry){StartCoroutine(Release(globe)); globe = pendingRoot;}
                    globe.name = "World surface"; globe.SetActive(!libraryOpen && screen!="Menu" && screen!="Options"); ApplyCachedDisplay();
                    globe.transform.rotation=VisibleProjection==Projection.Map?Quaternion.identity:Quaternion.Euler(pitch,0,0)*Quaternion.Euler(0,yaw,0);
                    globe.transform.position=VisibleProjection==Projection.Map?new Vector3(-centerLongitude/90,0,0):Vector3.zero;
                    bool map = viewOptions.Projection == Projection.Map;
                    surfaceMaterial.SetFloat("_MapMode", map ? 1 : 0); lineMaterial.SetFloat("_MapMode", map ? 1 : 0);
                    surfaceMaterial.SetFloat("_Unlit", map || layer == Layer.Climate || layer == Layer.Heightmap || EcologyView.IsLayer(layer) ? 1 : 0);
                    if(!Generating){progress = "Ready"; percent = 100;}
                    if (worldReady && Environment.GetCommandLineArgs().Contains("--worldgen-screenshot") && !screenshotPending) { screenshotPending = true; StartCoroutine(Screenshot()); }
                }
                pendingRoot = null; job = null; pieces.Dispose(); pieces = null; loadingSnapshot = false;pendingWorldId=pendingWorldName=null;ApplyCachedDisplay();
            }
        }
        if (VisibleProjection == Projection.Map)
        {
            cameraView.orthographic = true; cameraView.orthographicSize = Math.Max(1f, 2f / cameraView.aspect) * Mathf.Min(1f,mapZoom);
            float panLimit=Mathf.Max(0,1-cameraView.orthographicSize);mapPan.y=Mathf.Clamp(mapPan.y,-panLimit,panLimit);
            cameraView.transform.position = new Vector3(mapPan.x, mapPan.y, -3.2f);
        }
        else
        {
            cameraView.orthographic = false; cameraView.transform.position = new Vector3(0, 0, -globeDistance);
            if (autoRotate && !dragging) yaw += 6 * Time.deltaTime;
        }
        var mouse = Mouse.current; if (mouse == null || saveDialog || libraryOpen || simulationPaused || screen=="Menu" || screen=="Options") return; var pos = mouse.position.ReadValue();
        if (screen=="Setup" && mouse.leftButton.wasPressedThisFrame && Math.Abs(pos.x - PanelWidth) < 7) resizingPanel = true;
        if (resizingPanel) { panelWidth = pos.x; if (!mouse.leftButton.isPressed) resizingPanel = false; }
        float inspectorTop=expandedInspection ? Screen.height : layer==Layer.Simulation?120:65;
        bool overSurface = screen=="Simulation"?!OverSocietyPanel(pos)&&pos.y<Screen.height-52:!gridMenu && !resizingPanel && pos.x > sidebar + 8 && pos.y > inspectorTop && pos.y < Screen.height - (layerMenu?350:layer==Layer.Simulation?140:100);
        if (mouse.leftButton.wasPressedThisFrame && overSurface) { dragging = true; dragDistance = 0; previousMouse = pos; }
        if (dragging && mouse.leftButton.isPressed)
        {
            var delta = pos - previousMouse; dragDistance += delta.magnitude;
            if (VisibleProjection == Projection.Globe) { yaw -= delta.x * .3f; pitch = Mathf.Clamp(pitch + delta.y * .3f, -89, 89); }
            else { float scale = cameraView.orthographicSize * 2 / Screen.height; centerLongitude=Mathf.Repeat(centerLongitude-delta.x*scale*90+180,360)-180; mapPan.y = Mathf.Clamp(mapPan.y-delta.y*scale,-1,1); }
            previousMouse = pos;
        }
        if (mouse.leftButton.wasReleasedThisFrame && dragging)
        {
            dragging = false; if (dragDistance < 5 && displayedWorld == world && world != null && globe != null) Pick(pos);
        }
        if (overSurface)
        {
            float wheel = mouse.scroll.ReadValue().y;
            if (Math.Abs(wheel) > 0)
            {
                // Input System scroll can be normalized to 1 or reported as 120 per notch.
                float steps = Math.Abs(wheel) >= 20 ? wheel / 120 : wheel;
                float factor = Mathf.Exp(-steps * .22f);
                if (VisibleProjection == Projection.Map) mapZoom = Mathf.Clamp(mapZoom * factor, .015f, 1);
                else globeDistance = 1.025f + Mathf.Clamp((globeDistance - 1.025f) * factor, .035f, 5);
            }
        }
    }
    void Pick(Vector2 screen)
    {
        var ray = cameraView.ScreenPointToRay(screen); V point;
        if (VisibleProjection == Projection.Map)
        {
            float t = -ray.origin.z / ray.direction.z; var p = ray.GetPoint(t);
            if (p.x < -2 || p.x > 2 || p.y < -1 || p.y > 1) return;
            double lon = p.x * Math.PI / 2 + centerLongitude * Math.PI / 180, lat = p.y * Math.PI / 2;
            point = new V(Math.Cos(lat) * Math.Cos(lon), Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat));
        }
        else
        {
            float b = Vector3.Dot(ray.origin, ray.direction), c = ray.origin.sqrMagnitude - 1, d = b * b - c; if (d < 0) return;
            var p = globe.transform.InverseTransformPoint(ray.GetPoint(-b - Mathf.Sqrt(d))).normalized; point = new V(p.x, -p.z, p.y);
        }
        selectedCell = Erosion.Nearest(world.Surface, point);
        inspectionScroll=Vector2.zero;
    }
    void SetProjection(Projection next)
    {
        if (projection == next) return; projection = next;
        if(committedOptions!=null)committedOptions.Projection=next;
        ApplyCachedDisplay();
    }
    void RestoreView()
    {
        if (committedOptions == null) return;
        projection = committedOptions.Projection; layer = committedOptions.Layer; wireframe = committedOptions.Wireframe; showPlates = committedOptions.ShowPlates; showRivers = committedOptions.ShowRivers; gridSpacing = committedOptions.GridSpacing;
    }
    IEnumerator CheckInteraction()
    {
        var evidence = new UiEvidence { Detail = recipe.Detail };
        while (Busy && uploadedChunks < 3) yield return null;
        if (!Busy) { Debug.LogError("UI check did not reach cancellable presentation: " + error); Application.Quit(1); yield break; }
        var watch = Stopwatch.StartNew(); CancelGeneration();
        while (Busy) yield return null;
        evidence.CancelMilliseconds = watch.Elapsed.TotalMilliseconds; evidence.CancelledPresentation = world == null;
        Generate(); while (Busy) yield return null;
        if (world == null || error.Length > 0) { Debug.LogError("UI generation failed: " + error); Application.Quit(1); yield break; }
        evidence.Completed = world.Surface.Cells.Length == evidence.Detail; evidence.MeshChunks = globe.GetComponentsInChildren<MeshFilter>(true).Length;
        var originalRoot=globe;var displayTimer=Stopwatch.StartNew();
        foreach(var main in DisplayCache.MainLayers){layer=main;Refresh();}
        foreach(int spacing in new[]{0,5,10,15,30}){gridSpacing=spacing;showRivers=!showRivers;wireframe=!wireframe;Refresh();}
        evidence.CachedSwitchMilliseconds=displayTimer.Elapsed.TotalMilliseconds;
        evidence.InstantDisplay=job==null && globe==originalRoot && palettes.Count>=6;
        displayTimer.Restart();for(int i=0;i<10;i++){SetProjection(Projection.Globe);SetProjection(Projection.Map);}
        evidence.ProjectionSwitchMilliseconds=displayTimer.Elapsed.TotalMilliseconds/20;
        evidence.StableProjection=globe==originalRoot && VisibleProjection==Projection.Map && job==null;
        var preparing=job;layer=Layer.Terrain;Refresh();
        evidence.InstantDisplay &= job==preparing && committedOptions.Layer==Layer.Terrain;
        while (Busy) yield return null;
        watch.Restart();
        for (int i = 0; i < 120; i++) { centerLongitude = -180 + i * 3; yield return null; }
        evidence.LongitudeMilliseconds = watch.Elapsed.TotalMilliseconds / 120;
        evidence.Completed &= !Busy && projection == Projection.Map && error.Length == 0;
        Screen.SetResolution(1280, 850, FullScreenMode.Windowed); panelWidth = 420;
        for (int i = 0; i < 30; i++) yield return null;
        evidence.Resized = Screen.width == 1280 && Screen.height == 850 && Math.Abs(PanelWidth - 420) < 1;
        pitch = 89; yaw = 230; var orientation = Quaternion.Euler(pitch, 0, 0) * Quaternion.Euler(0, yaw, 0); var north = orientation * Vector3.up;
        evidence.NorthUp = Math.Abs(north.x) < .0001f && north.y > 0;
        evidence.BusyFrames = busyFrames; evidence.MaxBusyFrameMs = maxBusyFrameMs;
        string path = Argument("--worldgen-ui-check", Path.Combine(Application.persistentDataPath, "ui-check.json"));
        File.WriteAllText(path, JsonUtility.ToJson(evidence, true));
        Debug.Log("WORLDGEN UI CHECK " + JsonUtility.ToJson(evidence));
        Application.Quit(evidence.Completed && evidence.InstantDisplay && evidence.StableProjection && evidence.CancelledPresentation && evidence.Resized && evidence.NorthUp && evidence.BusyFrames > 30 && evidence.MaxBusyFrameMs < 2000 && evidence.CancelMilliseconds < 2000 ? 0 : 1);
    }
    async void Export(string extension, Action<World, string> action)
    {
        if (world == null || exportJob != null) return;
        var snapshot = world; string path = Path.ChangeExtension(filePath, extension);
        try { exportJob = Task.Run(() => action(snapshot, path)); await exportJob; }
        catch (Exception e) { error = e.Message; }
        finally { exportJob = null; }
    }
    void ValidateInputs()
    {
        foreach (var field in Recipe.Settings) if (inputText.TryGetValue(field.Name, out string text))
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value)
                || field.FieldType == typeof(int) && (value != Math.Truncate(value) || value < int.MinValue || value > int.MaxValue))
                throw new ArgumentException("Enter a valid " + (field.FieldType == typeof(int) ? "whole number" : "number") + " for " + field.GetCustomAttribute<SettingAttribute>().Label + ".");
            var limits=field.GetCustomAttribute<SettingAttribute>();
            if(value<limits.Min || value>limits.Max)throw new ArgumentException(limits.Label+" must be between "+limits.Min+" and "+limits.Max+".");
        }
        foreach(var key in new[]{"PlatesMin","PlatesMax","ContinentsMin","ContinentsMax","InterconnectednessMin","InterconnectednessMax"})
            if(inputText.TryGetValue(key,out string text) && !int.TryParse(text,out _))throw new ArgumentException("Enter a whole number for "+key+".");
    }
    void Try(Action action) { try { action(); error = ""; } catch (Exception e) { error = e.Message; Debug.LogException(e); } }
    async void RunPlayerChecks()
    {
        try
        {
            string output = Argument("--worldgen-output", Path.Combine(Application.persistentDataPath, "cross-host")); Directory.CreateDirectory(output);
            foreach (var entry in GoldenCorpus.Recipes())
            {
                var w = await Task.Run(() => new WorldPipeline().Generate(entry.Recipe));
                var packed = await Task.Run(() => WorldFile.Write(w)); var restored = await Task.Run(() => WorldFile.Read(packed));
                if (StateHash.Compute(w) != StateHash.Compute(restored)) throw new InvalidOperationException("Compact world snapshot mismatch: " + entry.Name);
                File.WriteAllText(Path.Combine(output, entry.Name + ".hashes.json"), WorldJson.Write(w.Stages)); Debug.Log("WORLDGEN PLAYER " + entry.Name + " " + w.Stages.Last().Hash);
            }
            Application.Quit(0);
        }
        catch (Exception e) { Debug.LogException(e); Application.Quit(1); }
    }
    IEnumerator Screenshot()
    {
        yield return new WaitForEndOfFrame();
        var target = new RenderTexture(1600, 1000, 24); cameraView.targetTexture = target; cameraView.rect = new Rect(0, 0, 1, 1);
        if (projection == Projection.Map)
        {
            float CaptureNumber(string flag,float fallback)=>float.TryParse(Argument(flag,""),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float value)?value:fallback;
            cameraView.orthographic = true; cameraView.orthographicSize = Mathf.Clamp(CaptureNumber("--worldgen-capture-scale",1.4f),.015f,1.4f); globe.transform.rotation = Quaternion.identity;
            cameraView.transform.position = new Vector3(CaptureNumber("--worldgen-capture-lon",0)/90,CaptureNumber("--worldgen-capture-lat",0)/90,-3.2f);
        }
        cameraView.Render(); var prior = RenderTexture.active; RenderTexture.active = target; var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); texture.Apply();
        File.WriteAllBytes(Argument("--worldgen-screenshot", Path.Combine(Application.persistentDataPath, "worldgen.png")), texture.EncodeToPNG());
        RenderTexture.active = prior; cameraView.targetTexture = null; Destroy(texture); target.Release(); Destroy(target); yield return null; Application.Quit(error.Length == 0 ? 0 : 1);
    }
    static string Argument(string flag, string fallback) { var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, flag); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        void OnDestroy() { if(riverMaterial!=null)Destroy(riverMaterial);if(settlementDot!=null)Destroy(settlementDot);DisposeMusic(); DisposeHabitatPortraits(); generationCancel?.Cancel(); cancel?.Cancel(); humanCancellation?.Cancel(); if (surfaceMaterial != null) Destroy(surfaceMaterial); if (lineMaterial != null) Destroy(lineMaterial);if(naturalMaterial!=null)Destroy(naturalMaterial);if(smallFeatureMaterial!=null)Destroy(smallFeatureMaterial);if(borderMaterial!=null)Destroy(borderMaterial);if(borderMesh!=null)Destroy(borderMesh); }
}
