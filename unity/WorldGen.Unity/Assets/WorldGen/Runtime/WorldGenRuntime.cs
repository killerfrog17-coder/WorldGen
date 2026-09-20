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

public sealed class WorldGenRuntime : MonoBehaviour
{
    Recipe recipe = new Recipe();
    World world; Task exportJob; Task<World> job; CancellationTokenSource cancel;
    string progress = "Ready", error = "", inspection = "Click a cell to inspect its terrain and climate.", filePath;
    int percent, seaLevel; Layer layer; Projection projection; float centerLongitude;
    GameObject globe, pendingRoot; Material surfaceMaterial, lineMaterial; Camera cameraView;
    BlockingCollection<UploadPiece> pieces; ViewOptions viewOptions = new ViewOptions();
    bool wireframe, showPlates, resizingPanel; int gridSpacing = 30;
    float panelWidth = 350, yaw = 150, pitch = -12, globeDistance = 3.2f;
    bool Busy => job != null;
    ViewOptions committedOptions; int uploadedChunks; long lastFrameTick; double maxBusyFrameMs; int busyFrames;
    bool uiCheck;
    [Serializable] sealed class UiEvidence { public int Detail, BusyFrames, MeshChunks; public double MaxBusyFrameMs, CancelMilliseconds, LongitudeMilliseconds; public bool Completed, CancelledPresentation, Resized, NorthUp; }
    sealed class UploadPiece
    {
        public Vector3[] Vertices, Lines; public Color32[] Colors, LineColors; public int[] Triangles, LineIndices;
    }
    Vector2 scroll, layerScroll; bool layerMenu, dirty, dragging, screenshotPending;
    string[] groups = Recipe.Settings.Select(f => f.GetCustomAttribute<SettingAttribute>().Group).Distinct().ToArray();
    bool[] panels; string preset = "Orogen defaults"; int presetIndex;
    readonly string[] presets = { "Orogen defaults", "Preview", "Ocean world", "Continental", "Glacial", "500 plates" };
    Vector2 previousMouse; float dragDistance, mapZoom = 1.1f; Quaternion globeRotation = Quaternion.Euler(-12, 150, 0);
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
        if (int.TryParse(Argument("--worldgen-detail", ""), out int detail)) recipe.Detail = detail;
        if (Enum.TryParse(Argument("--worldgen-layer", ""), out Layer requestedLayer)) layer = requestedLayer;
        if (Argument("--worldgen-view", "") == "Map") projection = Projection.Map;
        cameraView = Camera.main;
        if (cameraView == null) cameraView = new GameObject("WorldGen Camera", typeof(Camera)).GetComponent<Camera>();
        cameraView.transform.position = new Vector3(0, 0, -3.2f); cameraView.transform.LookAt(Vector3.zero);
        cameraView.clearFlags = CameraClearFlags.SolidColor; cameraView.nearClipPlane = .005f; cameraView.backgroundColor = new Color(.035f, .065f, .09f); cameraView.fieldOfView = 45;
        surfaceMaterial = new Material(Resources.Load<Shader>("WorldGenVertex"));
        lineMaterial = new Material(surfaceMaterial); lineMaterial.SetFloat("_Unlit", 1);
        wireframe = Environment.GetCommandLineArgs().Contains("--worldgen-wireframe"); showPlates = Environment.GetCommandLineArgs().Contains("--worldgen-plates");
        if (int.TryParse(Argument("--worldgen-grid", "30"), out int grid) && (grid == 0 || grid == 15 || grid == 30)) gridSpacing = grid;
        Generate();
        if (uiCheck) StartCoroutine(CheckInteraction());
    }
    void Generate() => BeginWork(true);
    void RollSeed() { recipe.Seed = UnityEngine.Random.Range(0, 1 << 24); inputText.Remove(nameof(Recipe.Seed)); dirty = true; }
    void Refresh() { if (world != null) BeginWork(false); }
    void BeginWork(bool generate)
    {
        if (Busy) return;
        try
        {
            if (generate) { ValidateInputs(); recipe.Validate(); }
            cancel?.Dispose(); cancel = new CancellationTokenSource(); var token = cancel.Token;
            var snapshot = recipe.Clone(); var existing = world;
            viewOptions = new ViewOptions { Projection = projection, Layer = layer, Wireframe = wireframe, ShowPlates = showPlates, GridSpacing = gridSpacing, SeaLevel = 0 };
            var options = viewOptions.Clone(); bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            uploadedChunks = 0;
            pendingRoot = new GameObject("Preparing world"); pendingRoot.SetActive(false);
            pieces = new BlockingCollection<UploadPiece>(2); var queue = pieces; error = ""; percent = 0; progress = generate ? "Starting generation" : "Preparing view";
            job = Task.Run(() =>
            {
                try
                {
                    var elapsed = Stopwatch.StartNew();
                    var result = generate ? new WorldPipeline().Generate(snapshot, p => { progress = p.Stage; percent = p.Percent * 80 / 100; if (uiCheck) Debug.Log("WORLDGEN UI STAGE " + p.Stage + " " + elapsed.ElapsedMilliseconds + "ms"); }, token) : existing;
                    int index = 0, total = (result.Surface.Edges.Length + SurfaceMesh.EdgesPerChunk - 1) / SurfaceMesh.EdgesPerChunk + (options.GridSpacing == 0 ? 0 : 1);
                    foreach (var data in SurfaceMesh.Chunks(result, options, token))
                    {
                        token.ThrowIfCancellationRequested(); progress = "Preparing / uploading cell surfaces"; percent = 80 + 19 * index++ / Math.Max(1, total);
                        queue.Add(Prepare(data, linear, token), token);
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
        byte Channel(float value) { double v = Math.Max(0, Math.Min(1, value)); if (linear) v = v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); return (byte)Math.Round(v * 255); }
        for (int i = 0; i < piece.Vertices.Length; i++)
        {
            if ((i & 2047) == 0) token.ThrowIfCancellationRequested(); int k = i * 3;
            piece.Vertices[i] = new Vector3(data.Positions[k], data.Positions[k + 1], data.Positions[k + 2]);
            piece.Colors[i] = new Color32(Channel(data.Colors[k]), Channel(data.Colors[k + 1]), Channel(data.Colors[k + 2]), 255);
        }
        for (int i = 0; i < piece.Lines.Length; i++)
        {
            int k = i * 3; piece.Lines[i] = new Vector3(data.Lines[k], data.Lines[k + 1], data.Lines[k + 2]); piece.LineIndices[i] = i;
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
            mesh.vertices = vertices; mesh.colors32 = colors; mesh.SetIndices(indices, lines ? MeshTopology.Lines : MeshTopology.Triangles, 0);
            mesh.UploadMeshData(true);
            int copies = viewOptions.Projection == Projection.Map ? 3 : 1;
            for (int copy = 0; copy < copies; copy++)
            {
                var part = new GameObject(lines ? "Cell edges / grid" : "Cell surfaces", typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(pendingRoot.transform, false); part.transform.localPosition = new Vector3(copies == 1 ? 0 : (copy - 1) * 4, 0, 0);
                part.GetComponent<MeshFilter>().sharedMesh = mesh; part.GetComponent<MeshRenderer>().sharedMaterial = lines ? lineMaterial : surfaceMaterial;
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
        if (cameraView == null) return;
        long tick = Stopwatch.GetTimestamp();
        if (Busy && lastFrameTick != 0) { maxBusyFrameMs = Math.Max(maxBusyFrameMs, (tick - lastFrameTick) * 1000.0 / Stopwatch.Frequency); busyFrames++; }
        lastFrameTick = tick;
        if (Keyboard.current != null && Keyboard.current.f11Key.wasPressedThisFrame)
            Screen.fullScreenMode = Screen.fullScreen ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
        cameraView.rect = new Rect(PanelWidth / Screen.width, 0, 1 - PanelWidth / Screen.width, 1);
        if (projection == Projection.Map)
        {
            cameraView.orthographic = true; cameraView.orthographicSize = Math.Max(1.05f, 2.1f / cameraView.aspect) * mapZoom;
            cameraView.transform.position = new Vector3(mapPan.x, mapPan.y, -3.2f);
        }
        else
        {
            cameraView.orthographic = false; cameraView.transform.position = new Vector3(0, 0, -globeDistance);
            if (autoRotate && !dragging) yaw += 6 * Time.deltaTime;
        }
        if (globe != null)
        {
            globe.transform.rotation = (committedOptions?.Projection ?? projection) == Projection.Map ? Quaternion.identity : Quaternion.Euler(pitch, 0, 0) * Quaternion.Euler(0, yaw, 0);
            globe.transform.position = (committedOptions?.Projection ?? projection) == Projection.Map ? new Vector3(-centerLongitude / 90, 0, 0) : Vector3.zero;
        }
        if (Busy)
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
                    world = job.Result; committedOptions = viewOptions.Clone(); dirty = StateHash.Compute(recipe) != StateHash.Compute(world.Recipe);
                    StartCoroutine(Release(globe)); globe = pendingRoot; globe.name = "World surface"; globe.SetActive(true);
                    bool map = viewOptions.Projection == Projection.Map;
                    surfaceMaterial.SetFloat("_MapMode", map ? 1 : 0); lineMaterial.SetFloat("_MapMode", map ? 1 : 0);
                    surfaceMaterial.SetFloat("_Unlit", map || layer == Layer.Climate || layer == Layer.Heightmap ? 1 : 0);
                    progress = "Ready"; percent = 100;
                    if (Environment.GetCommandLineArgs().Contains("--worldgen-screenshot") && !screenshotPending) { screenshotPending = true; StartCoroutine(Screenshot()); }
                }
                pendingRoot = null; job = null; pieces.Dispose(); pieces = null;
            }
        }
        var mouse = Mouse.current; if (mouse == null) return; var pos = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame && Math.Abs(pos.x - PanelWidth) < 7) resizingPanel = true;
        if (resizingPanel) { panelWidth = pos.x; if (!mouse.leftButton.isPressed) resizingPanel = false; }
        bool overSurface = !resizingPanel && pos.x > PanelWidth + 8 && pos.y > 175 && pos.y < Screen.height - 170;
        if (mouse.leftButton.wasPressedThisFrame && overSurface) { dragging = true; dragDistance = 0; previousMouse = pos; }
        if (dragging && mouse.leftButton.isPressed)
        {
            var delta = pos - previousMouse; dragDistance += delta.magnitude;
            if (projection == Projection.Globe) { yaw -= delta.x * .3f; pitch = Mathf.Clamp(pitch + delta.y * .3f, -89, 89); }
            else { float scale = cameraView.orthographicSize * 2 / Screen.height; mapPan += new Vector3(-delta.x * scale, -delta.y * scale, 0); mapPan.x = Mathf.Clamp(mapPan.x, -2, 2); mapPan.y = Mathf.Clamp(mapPan.y, -1, 1); }
            previousMouse = pos;
        }
        if (mouse.leftButton.wasReleasedThisFrame && dragging)
        {
            dragging = false; if (dragDistance < 5 && !Busy && world != null && globe != null) Pick(pos);
        }
        if (overSurface)
        {
            float wheel = mouse.scroll.ReadValue().y;
            if (Math.Abs(wheel) > 0)
            {
                // Input System scroll can be normalized to 1 or reported as 120 per notch.
                float steps = Math.Abs(wheel) >= 20 ? wheel / 120 : wheel;
                float factor = Mathf.Exp(-steps * .22f);
                if (projection == Projection.Map) mapZoom = Mathf.Clamp(mapZoom * factor, .015f, 2);
                else globeDistance = 1.025f + Mathf.Clamp((globeDistance - 1.025f) * factor, .035f, 5);
            }
        }
    }
    void Pick(Vector2 screen)
    {
        var ray = cameraView.ScreenPointToRay(screen); V point;
        if (projection == Projection.Map)
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
        inspection = GlobeView.Describe(world, Erosion.Nearest(world.Surface, point));
    }
    void SetProjection(Projection next)
    {
        if (projection == next || Busy) return; projection = next; mapPan = Vector3.zero; mapZoom = 1.1f; Refresh();
    }
    void RestoreView()
    {
        if (committedOptions == null) return;
        projection = committedOptions.Projection; layer = committedOptions.Layer; wireframe = committedOptions.Wireframe; showPlates = committedOptions.ShowPlates; gridSpacing = committedOptions.GridSpacing;
    }
    IEnumerator CheckInteraction()
    {
        var evidence = new UiEvidence { Detail = recipe.Detail };
        while (Busy && uploadedChunks < 3) yield return null;
        if (!Busy) { Debug.LogError("UI check did not reach cancellable presentation: " + error); Application.Quit(1); yield break; }
        var watch = Stopwatch.StartNew(); cancel.Cancel();
        while (Busy) yield return null;
        evidence.CancelMilliseconds = watch.Elapsed.TotalMilliseconds; evidence.CancelledPresentation = world == null;
        Generate(); while (Busy) yield return null;
        if (world == null || error.Length > 0) { Debug.LogError("UI generation failed: " + error); Application.Quit(1); yield break; }
        evidence.Completed = world.Surface.Cells.Length == evidence.Detail; evidence.MeshChunks = globe.transform.childCount;
        SetProjection(Projection.Map); while (Busy) yield return null;
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
        Application.Quit(evidence.Completed && evidence.CancelledPresentation && evidence.Resized && evidence.NorthUp && evidence.BusyFrames > 30 && evidence.MaxBusyFrameMs < 2000 && evidence.CancelMilliseconds < 2000 ? 0 : 1);
    }
    void OnGUI()
    {
        if (cameraView == null) return; GUI.skin.label.wordWrap = true; GUI.backgroundColor = new Color(.13f, .23f, .28f); float width = PanelWidth;
        GUI.Box(new Rect(0, 0, width, Screen.height), ""); GUILayout.BeginArea(new Rect(16, 15, width - 32, Screen.height - 30));
        GUILayout.Label("WORLDGEN", new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold }); GUILayout.Label("Shape your world");
        GUILayout.BeginHorizontal(); GUILayout.Label(preset, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Next preset", GUILayout.Width(95))) { presetIndex = (presetIndex + 1) % presets.Length; preset = presets[presetIndex]; int seed = recipe.Seed; recipe = Recipe.Preset(preset); recipe.Seed = seed; inputText.Clear(); dirty = true; }
        GUILayout.EndHorizontal();
        GUI.enabled = !Busy;
        if (GUILayout.Button(world == null ? "BUILD WORLD" : "REGENERATE / REAPPLY", GUILayout.Height(35))) Generate();
        GUI.enabled = true;
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Max(180, Screen.height - 300)));
        for (int g = 0; g < groups.Length; g++)
        {
            if (GUILayout.Button((panels[g] ? "−  " : "+  ") + groups[g])) panels[g] = !panels[g]; if (!panels[g]) continue;
            foreach (var field in Recipe.Settings.Where(f => f.GetCustomAttribute<SettingAttribute>().Group == groups[g]))
            {
                var meta = field.GetCustomAttribute<SettingAttribute>(); GUILayout.BeginHorizontal(); GUILayout.Label(meta.Label, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
                if (!inputText.TryGetValue(field.Name, out string old)) old = Convert.ToString(field.GetValue(recipe), CultureInfo.InvariantCulture);
                string value = GUILayout.TextField(old, GUILayout.Width(76));
                if (value != old)
                {
                    inputText[field.Name] = value;
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && !double.IsNaN(parsed) && !double.IsInfinity(parsed))
                    { field.SetValue(recipe, field.FieldType == typeof(int) ? (object)(int)parsed : parsed); if (field.GetCustomAttribute<DisplaySettingAttribute>() == null) dirty = true; }
                }
                if (field.Name == nameof(Recipe.Seed) && GUILayout.Button("Roll", GUILayout.Width(42))) RollSeed();
                GUILayout.EndHorizontal();
            }
            if (groups[g] == "World") GUILayout.Label("20,000 base plate cells; terrain runs at selected detail.");
        }
        GUILayout.EndScrollView();
        GUILayout.Label("Recipe v" + recipe.SchemaVersion + " · " + recipe.AlgorithmVersion);
        filePath = GUILayout.TextField(filePath);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save recipe")) Try(() => { ValidateInputs(); recipe.Validate(); File.WriteAllText(filePath, WorldJson.Write(recipe)); });
        if (GUILayout.Button("Load recipe")) Try(() => { recipe = WorldJson.ReadRecipe(File.ReadAllText(filePath)); inputText.Clear(); dirty = true; });
        GUILayout.EndHorizontal(); GUI.enabled = world != null && !Busy && exportJob == null;
        if (GUILayout.Button("Export result")) Export("result.json", (w, path) => File.WriteAllText(path, WorldJson.Write(w)));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Heightmap")) Export("pgm", (w, path) => File.WriteAllBytes(path, WorldJson.Heightmap(w)));
        if (GUILayout.Button("Cell CSV")) Export("csv", (w, path) => File.WriteAllText(path, WorldJson.Csv(w)));
        GUILayout.EndHorizontal(); GUI.enabled = true;
        if (world != null)
        {
            foreach (var warning in world.Warnings) GUILayout.Label(warning);
            GUILayout.Label("Land: " + (world.Metrics.First(m => m.Name == "land_fraction").Value * 100).ToString("0.0") + "%");
            GUILayout.Label("Inspect a cell for plate, terrain and climate data.");
        }
        if (exportJob != null) GUILayout.Label("Exporting...");
        if (dirty && world != null) GUILayout.Label("Settings changed. Regenerate to apply."); if (error.Length > 0) GUILayout.Label(error);
        if (job != null) { GUILayout.Label(progress + " · " + percent + "%"); if (GUILayout.Button("Cancel")) cancel.Cancel(); }
        GUILayout.EndArea();
        GUI.Box(new Rect(width - 3, 0, 6, Screen.height), "");
        GUI.enabled = !Busy;
        GUILayout.BeginArea(new Rect(width + 24, 18, Math.Max(220, Screen.width - width - 48), 195));
        GUILayout.BeginHorizontal();
        foreach (Layer candidate in new[] { Layer.Terrain, Layer.Satellite, Layer.Climate, Layer.Heightmap })
            if (GUILayout.Toggle(layer == candidate, candidate.ToString(), GUI.skin.button, GUILayout.Width(95)) && layer != candidate) { layer = candidate; Try(Refresh); }
        if (GUILayout.Toggle(projection == Projection.Globe, "Globe", GUI.skin.button, GUILayout.Width(90))) SetProjection(Projection.Globe);
        if (GUILayout.Toggle(projection == Projection.Map, "Map", GUI.skin.button, GUILayout.Width(90))) SetProjection(Projection.Map);
        if (projection == Projection.Globe) autoRotate = GUILayout.Toggle(autoRotate, "Auto rotate", GUILayout.Width(120));
        if (GUILayout.Button("Fullscreen (F11)", GUILayout.Width(135))) Screen.fullScreenMode = Screen.fullScreen ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
        GUILayout.EndHorizontal(); GUILayout.BeginHorizontal();
        if (GUILayout.Button("Inspect: " + GlobeView.LayerName(layer) + " ▾", GUILayout.Width(250))) layerMenu = !layerMenu;
        GUILayout.EndHorizontal();
        if (layerMenu)
        {
            layerScroll = GUILayout.BeginScrollView(layerScroll, GUILayout.Height(110), GUILayout.Width(270));
            foreach (Layer candidate in GlobeView.VisibleLayers) if (GUILayout.Button(GlobeView.LayerName(candidate))) { layer = candidate; layerMenu = false; Try(Refresh); }
            GUILayout.EndScrollView();
        }
        else
        {
            if (projection == Projection.Map)
            {
                GUILayout.BeginHorizontal(); GUILayout.Label("Center longitude", GUILayout.Width(125)); float lon = GUILayout.HorizontalSlider(centerLongitude, -180, 180, GUILayout.Width(200));
                if (Math.Abs(lon - centerLongitude) > .001f) centerLongitude = lon; GUILayout.Label(centerLongitude.ToString("0") + "°"); GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal(); bool wire = GUILayout.Toggle(wireframe, "Wireframe", GUILayout.Width(110)); bool plates = GUILayout.Toggle(showPlates, "Show plates", GUILayout.Width(110));
            GUILayout.Label("Grid", GUILayout.Width(35)); int grid = GUILayout.SelectionGrid(gridSpacing == 0 ? 0 : gridSpacing == 15 ? 1 : 2, new[] { "Off", "15 deg", "30 deg" }, 3, GUILayout.Width(145));
            int spacing = grid == 0 ? 0 : grid == 1 ? 15 : 30;
            if (wire != wireframe || plates != showPlates || spacing != gridSpacing) { wireframe = wire; showPlates = plates; gridSpacing = spacing; Refresh(); }
            GUILayout.EndHorizontal();
            GUILayout.Label(layer == Layer.Heightmap ? "Black −5,000 m · White +6,000 m · Absolute elevation" : layer == Layer.Climate ? "Köppen climate · click a cell for its classification" : "Drag to " + (projection == Projection.Map ? "pan" : "orbit") + " · scroll to zoom · click to inspect");
        }
        GUILayout.EndArea(); GUI.enabled = true;
        if (Busy)
        {
            float x = width + (Screen.width - width - 380) / 2;
            GUI.Box(new Rect(x, Screen.height / 2 - 55, 380, 115), "Preparing world");
            GUI.Label(new Rect(x + 20, Screen.height / 2 - 25, 340, 28), progress + " - " + percent + "%");
            if (GUI.Button(new Rect(x + 140, Screen.height / 2 + 15, 100, 28), cancel.IsCancellationRequested ? "Cancelling..." : "Cancel")) cancel.Cancel();
        }
        GUI.Box(new Rect(width + 20, Screen.height - 170, Math.Max(200, Screen.width - width - 40), 150), "");
        GUI.Label(new Rect(width + 34, Screen.height - 160, Math.Max(190, Screen.width - width - 65), 135), inspection);
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
        }
    }
    void Try(Action action) { try { action(); error = ""; } catch (Exception e) { error = e.Message; Debug.LogException(e); } }
    async void RunPlayerChecks()
    {
        try
        {
            string output = Argument("--worldgen-output", Path.Combine(Application.persistentDataPath, "cross-host")); Directory.CreateDirectory(output);
            foreach (var entry in GoldenCorpus.Recipes())
            { var w = await Task.Run(() => new WorldPipeline().Generate(entry.Recipe)); File.WriteAllText(Path.Combine(output, entry.Name + ".hashes.json"), WorldJson.Write(w.Stages)); Debug.Log("WORLDGEN PLAYER " + entry.Name + " " + w.Stages.Last().Hash); }
            Application.Quit(0);
        }
        catch (Exception e) { Debug.LogException(e); Application.Quit(1); }
    }
    IEnumerator Screenshot()
    {
        yield return new WaitForEndOfFrame();
        var target = new RenderTexture(1600, 1000, 24); cameraView.targetTexture = target; cameraView.rect = new Rect(0, 0, 1, 1);
        if (projection == Projection.Map) { cameraView.orthographic = true; cameraView.orthographicSize = 1.4f; globe.transform.rotation = Quaternion.identity; cameraView.transform.position = new Vector3(0, 0, -3.2f); }
        cameraView.Render(); var prior = RenderTexture.active; RenderTexture.active = target; var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); texture.Apply();
        File.WriteAllBytes(Argument("--worldgen-screenshot", Path.Combine(Application.persistentDataPath, "worldgen.png")), texture.EncodeToPNG());
        RenderTexture.active = prior; cameraView.targetTexture = null; Destroy(texture); target.Release(); Destroy(target); yield return null; Application.Quit(error.Length == 0 ? 0 : 1);
    }
    static string Argument(string flag, string fallback) { var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, flag); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
    void OnDestroy() { cancel?.Cancel(); if (surfaceMaterial != null) Destroy(surfaceMaterial); if (lineMaterial != null) Destroy(lineMaterial); }
}
