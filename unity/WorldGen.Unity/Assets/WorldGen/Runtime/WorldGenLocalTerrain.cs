using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using WorldGen.Core;
using WorldGen.Presentation;
using Debug = UnityEngine.Debug;

public sealed partial class WorldGenRuntime
{
    LocalTerrainExplorer terrainExplorer;
    Task<LocalTerrain> localTerrainTask;
    bool enteringTerrain;
    World localTerrainWorld;
    CancellationTokenSource localTerrainCancellation;
    V terrainPickedPosition; int terrainPickedCell = -1, terrainReturnFrame = -1; World terrainPickedWorld;
    bool CanExploreTerrain => worldReady && !Busy && world != null && humanStep == null;
    bool CanRequestTerrain => worldReady && !Busy && world != null && !enteringTerrain;
    void RequestTerrain()
    { if(CanRequestTerrain)StartCoroutine(WaitForTerrainEntry()); }
    IEnumerator WaitForTerrainEntry()
    {
        enteringTerrain=true;bool resume=humanPlaying;humanPlaying=false;
        while(humanStep!=null)yield return null;
        enteringTerrain=false;ExploreTerrain(resumePlayback:resume);
    }
    void ExploreTerrain(V? exactPosition = null,bool? resumePlayback=null)
    {
        if (!CanExploreTerrain || terrainExplorer != null) return;
        if (!ReferenceEquals(localTerrainWorld, world) || localTerrainTask == null || localTerrainTask.IsCanceled || localTerrainTask.IsFaulted)
        {
            localTerrainCancellation?.Cancel(); localTerrainCancellation?.Dispose(); localTerrainCancellation = new CancellationTokenSource();
            var source = world; var token = localTerrainCancellation.Token; localTerrainWorld = source;
            localTerrainTask = Task.Run(() => new LocalTerrain(source, token), token);
        }
        int cell = selectedCell;
        if (cell < 0) cell = Array.FindIndex(world.ElevationM, h => h > 100 && h < 900);
        if (cell < 0) cell = 0;
        bool playing = resumePlayback ?? humanPlaying; humanPlaying = false; cameraView.enabled = false; dragging = false; GUIUtility.keyboardControl = 0;
        if (globe != null) globe.SetActive(false);
        var scene = SceneManager.CreateScene("Local terrain preview");
        var root = new GameObject("Terrain explorer"); SceneManager.MoveGameObjectToScene(root, scene);
        terrainExplorer = root.AddComponent<LocalTerrainExplorer>();
        terrainExplorer.Initialize(localTerrainTask, exactPosition ?? (terrainPickedCell == cell && ReferenceEquals(terrainPickedWorld,world) ? terrainPickedPosition : world.Surface.Cells[cell].Position), () =>
        {
            terrainExplorer = null; terrainReturnFrame = Time.frameCount; cameraView.enabled = true; humanClockSample=Time.realtimeSinceStartupAsDouble;humanPlayback.Reset();humanPlaying = playing;
            if (globe != null) globe.SetActive(true); SceneManager.UnloadSceneAsync(scene);
        });
    }
    IEnumerator CheckLocalTerrain()
    {
        string output = Argument("--worldgen-output", ""); Directory.CreateDirectory(output); AudioListener.volume = 0;
        var renderErrors=new List<string>();Application.LogCallback record=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception)renderErrors.Add(message);};Application.logMessageReceived+=record;
        double deadline = Time.realtimeSinceStartupAsDouble + 240;
        while ((Busy || !worldReady || humanStep != null || humans == null || humanPaletteJob != null || humanPaletteReady != null) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        if (!CanExploreTerrain) { Debug.LogError("TERRAIN CHECK world unavailable: " + error); Application.Quit(1); yield break; }
        humanPlaying = false; string hash = StateHash.Compute(world);
        var cells = Enumerable.Range(0, world.Surface.Cells.Length).Where(i => Math.Abs(world.Surface.Cells[i].Position.Z) < .75).ToArray();
        int[] sites = {
            cells.FirstOrDefault(i => world.ElevationM[i] > 150 && world.ElevationM[i] < 500),
            cells.Where(i => world.ElevationM[i] > 2000).OrderByDescending(i => world.Surface.Cells[i].Neighbors.Max(j => Math.Abs(world.ElevationM[i] - world.ElevationM[j]))).DefaultIfEmpty(-1).First(),
            cells.Where(i => world.ElevationM[i] < -100 && world.ElevationM[i] > -1000).DefaultIfEmpty(-1).First() };
        var report = new List<string>();
        var probes = new List<string>();
        for (int s = 0; s < sites.Length; s++)
        {
            if (sites[s] < 0) continue;
            selectedCell = sites[s]; ExploreTerrain();
            deadline = Time.realtimeSinceStartupAsDouble + 180;
            while (terrainExplorer != null && (!terrainExplorer.Ready || terrainExplorer.TileCount < 169) && terrainExplorer.Error == "" && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (terrainExplorer == null || !terrainExplorer.Ready || terrainExplorer.TileCount < 169 || terrainExplorer.Error != "")
            { Debug.LogError("TERRAIN CHECK failed: " + terrainExplorer?.Error); Application.Quit(1); yield break; }
            yield return new WaitForEndOfFrame(); terrainExplorer.Capture(Path.Combine(output, "terrain-" + s + ".png"));
            report.Add("site " + sites[s] + ": " + terrainExplorer.Diagnostics);
            probes.AddRange(terrainExplorer.Probes());
            terrainExplorer.TestMove(4300, 2800); yield return null;
            deadline = Time.realtimeSinceStartupAsDouble + 60;
            while ((!terrainExplorer.CenterReady || terrainExplorer.TileCount < 169) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (!terrainExplorer.CenterReady) { Debug.LogError("TERRAIN CHECK streaming movement failed"); Application.Quit(1); yield break; }
            terrainExplorer.TestWalking(); yield return new WaitForEndOfFrame();
            if (s == 0) terrainExplorer.Capture(Path.Combine(output, "walking.png"));
            if(s==0)
            {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.F));yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;
                if(terrainExplorer.IsWalking){Debug.LogError("TERRAIN CHECK F control failed");Application.Quit(1);yield break;}
                double before=terrainExplorer.TravelM;
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.W));yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;
                if(terrainExplorer.TravelM==before){Debug.LogError("TERRAIN CHECK movement control failed");Application.Quit(1);yield break;}
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.Escape));yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());InputSystem.RemoveDevice(keyboard);
            }
            else terrainExplorer.Close(); yield return null; yield return null;
            if (!cameraView.enabled || terrainExplorer != null || StateHash.Compute(world) != hash)
            { Debug.LogError("TERRAIN CHECK changed world or failed return"); Application.Quit(1); yield break; }
        }
        Application.logMessageReceived-=record;
        if(renderErrors.Count>0){Debug.LogError("TERRAIN CHECK rendering errors: "+string.Join(";",renderErrors));Application.Quit(1);yield break;}
        report.Add("PASS independent scene; three sites; movement/streaming; walking; F/W/Esc input; return; no render errors; unchanged world " + hash);
        File.WriteAllLines(Path.Combine(output, "result.txt"), report); File.WriteAllLines(Path.Combine(output, "probes.csv"), probes); Application.Quit(0);
    }
}

/// <summary>Rendering, bounded streaming and camera controls only. All terrain comes from shared C# queries.</summary>
public sealed class LocalTerrainExplorer : MonoBehaviour
{
    const double TileM = 2048;
    const int Radius = 6;
    struct Key : IEquatable<Key>
    {
        public int X, Z; public Key(int x, int z) { X = x; Z = z; }
        public bool Equals(Key b) => X == b.X && Z == b.Z;
        public override bool Equals(object b) => b is Key k && Equals(k);
        public override int GetHashCode() => unchecked(X * 73856093 ^ Z * 19349663);
    }
    sealed class Tile { public int Segments; public GameObject Root; }
    sealed class Work { public Key Key; public int Segments; public Task<LocalTerrainMesh> Task; }
    Task<LocalTerrain> building;
    LocalTerrain terrain; LocalTerrainFrame frame; LocalTerrain.LocalTerrainPatch walkingPatch;
    readonly Dictionary<Key, Tile> tiles = new Dictionary<Key, Tile>();
    readonly Dictionary<Key, int> wanted = new Dictionary<Key, int>();
    readonly List<Key> priority = new List<Key>(); readonly List<Work> jobs = new List<Work>();
    readonly CancellationTokenSource cancellation = new CancellationTokenSource();
    Action close; Camera view; Material landMaterial, waterMaterial;
    V startingPoint; double x, z, originX, originZ; float altitude, heading = 20, pitch = 13, speed = 200;
    bool walking, closed; int tileX = int.MinValue, tileZ;
    double lastBuildMs, largestBuildMs; string error = "";
    public bool Ready => terrain != null;
    public bool IsWalking => walking;
    public double TravelM => x+z;
    public string Error => error;
    public int TileCount => tiles.Count;
    public bool CenterReady => tiles.ContainsKey(new Key((int)Math.Floor(x / TileM), (int)Math.Floor(z / TileM)));
    public string Diagnostics => tiles.Count + " tiles · last " + lastBuildMs.ToString("F1") + " ms · max " + largestBuildMs.ToString("F1") + " ms/tile";
    public void Initialize(Task<LocalTerrain> source, V position, Action onClose)
    {
        building = source; startingPoint = position; close = onClose;
        var cameraRoot = new GameObject("Exploration camera", typeof(Camera), typeof(UnityEngine.Rendering.Universal.UniversalAdditionalCameraData));
        cameraRoot.transform.SetParent(transform, false); cameraRoot.layer = 30; view = cameraRoot.GetComponent<Camera>();
        view.cullingMask = 1 << 30; view.nearClipPlane = .15f; view.farClipPlane = 19000; view.fieldOfView = 68;
        view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.57f, .69f, .76f);
        var shader=Resources.Load<Shader>("WorldGenLocalTerrain");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Local terrain shader is missing or unsupported.");
        landMaterial = new Material(shader);
        waterMaterial = new Material(landMaterial); waterMaterial.SetFloat("_Water", 1);
    }
    void Update()
    {
        if (closed) return;
        view.rect=new Rect(0,35f/Screen.height,1,1-85f/Screen.height);
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) { Close(); return; }
        if (terrain == null)
        {
            if (!building.IsCompleted) return;
            if (building.IsFaulted || building.IsCanceled) { error = building.Exception?.GetBaseException().Message ?? "Cancelled"; return; }
            terrain = building.Result;
            var sample = terrain.Patch(startingPoint).Sample(0, 0);
            frame = new LocalTerrainFrame(startingPoint, terrain.RadiusM, sample.ElevationM);
            altitude = (float)Math.Max(100 + sample.MountainHeightM * .4, -frame.AltitudeM + 40); RefreshWanted();
        }
        var keyboard = Keyboard.current; var mouse = Mouse.current; float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
        if (mouse != null)
        {
            if (mouse.rightButton.isPressed)
            { var d = mouse.delta.ReadValue(); heading += d.x * .12f; pitch = Mathf.Clamp(pitch - d.y * .12f, -85, 85); }
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0) speed = Mathf.Clamp(speed * Mathf.Exp(Mathf.Sign(scroll) * .25f), 5, 5000);
        }
        if (keyboard != null)
        {
            if (keyboard.fKey.wasPressedThisFrame) walking = !walking;
            var forward = Quaternion.Euler(0, heading, 0) * Vector3.forward;
            var right = Quaternion.Euler(0, heading, 0) * Vector3.right;
            Vector3 movement = Vector3.zero;
            if (keyboard.wKey.isPressed) movement += forward; if (keyboard.sKey.isPressed) movement -= forward;
            if (keyboard.dKey.isPressed) movement += right; if (keyboard.aKey.isPressed) movement -= right;
            float rate = (walking ? 4.5f : speed) * (keyboard.shiftKey.isPressed ? 3 : 1) * dt;
            if (movement.sqrMagnitude > 0) { movement.Normalize(); x += movement.x * rate; z += movement.z * rate; }
            if (!walking) { if (keyboard.spaceKey.isPressed) altitude += rate; if (keyboard.ctrlKey.isPressed) altitude -= rate; }
        }
        // Floating origin keeps Unity transforms local while planetary/tile coordinates remain double precision.
        if (Math.Abs(x - originX) > 8192 || Math.Abs(z - originZ) > 8192)
        {
            originX = Math.Floor(x / TileM) * TileM; originZ = Math.Floor(z / TileM) * TileM;
            landMaterial.SetVector("_LocalOrigin", new Vector4((float)originX, (float)originZ, 0, 0));
            foreach (var entry in tiles) entry.Value.Root.transform.localPosition = new Vector3((float)(entry.Key.X * TileM - originX), 0, (float)(entry.Key.Z * TileM - originZ));
        }
        var at = frame.Position(x, z);
        if (walkingPatch == null || (at - walkingPatch.Center).Length * terrain.RadiusM > 500) walkingPatch = terrain.Patch(at, 2048);
        double ground = frame.SurfaceY(at, walkingPatch.Sample(at).ElevationM);
        altitude = walking ? (float)ground + 1.75f : Math.Max(altitude, (float)ground + 1.75f);
        view.transform.localPosition = new Vector3((float)(x - originX), altitude, (float)(z - originZ));
        view.transform.localRotation = Quaternion.Euler(pitch, heading, 0);
        if ((int)Math.Floor(x / TileM) != tileX || (int)Math.Floor(z / TileM) != tileZ) RefreshWanted();
        for (int i = jobs.Count - 1; i >= 0; i--)
        {
            var job = jobs[i]; if (!job.Task.IsCompleted) continue; jobs.RemoveAt(i);
            if (job.Task.IsFaulted) { error = job.Task.Exception.GetBaseException().Message; continue; }
            if (job.Task.IsCanceled || !wanted.TryGetValue(job.Key, out int detail) || detail != job.Segments) continue;
            Upload(job.Key, job.Task.Result); break; // bounded one mesh pair upload per frame
        }
        foreach (var key in priority)
        {
            if (jobs.Count >= 2) break;
            int detail = wanted[key];
            if (tiles.TryGetValue(key, out var tile) && tile.Segments == detail || jobs.Any(j => j.Key.Equals(key))) continue;
            var buildFrame = frame; var buildTerrain = terrain; var token = cancellation.Token;
            jobs.Add(new Work { Key = key, Segments = detail, Task = Task.Run(() =>
            {
                var result = LocalTerrainMesh.Build(buildTerrain, buildFrame, key.X * TileM, key.Z * TileM, TileM, detail, token);
                return result;
            }, token) });
        }
    }
    void RefreshWanted()
    {
        tileX = (int)Math.Floor(x / TileM); tileZ = (int)Math.Floor(z / TileM); wanted.Clear(); priority.Clear();
        for (int dz = -Radius; dz <= Radius; dz++) for (int dx = -Radius; dx <= Radius; dx++)
        {
            int distance = Math.Max(Math.Abs(dx), Math.Abs(dz)); var key = new Key(tileX + dx, tileZ + dz);
            wanted[key] = distance <= 1 ? 128 : distance <= 3 ? 32 : 16; priority.Add(key);
        }
        priority.Sort((a, b) => { long da = (long)(a.X - tileX) * (a.X - tileX) + (long)(a.Z - tileZ) * (a.Z - tileZ), db = (long)(b.X - tileX) * (b.X - tileX) + (long)(b.Z - tileZ) * (b.Z - tileZ); int d = da.CompareTo(db); return d != 0 ? d : a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z); });
        foreach (var key in tiles.Keys.Where(k => !wanted.ContainsKey(k)).ToArray()) { DestroyTile(tiles[key]); tiles.Remove(key); }
    }
    void Upload(Key key, LocalTerrainMesh data)
    {
        var timer = Stopwatch.StartNew(); var root = new GameObject("Terrain " + key.X + "," + key.Z); root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3((float)(data.EastM - originX), 0, (float)(data.NorthM - originZ));
        Mesh Make(float[] positions, float[] colors, bool water)
        {
            var vertices = new Vector3[positions.Length / 3]; var normals = new Vector3[vertices.Length]; var rgb = new Color[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            { vertices[i] = new Vector3(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]); normals[i] = water ? Vector3.up : new Vector3(data.Normals[i * 3], data.Normals[i * 3 + 1], data.Normals[i * 3 + 2]); rgb[i] = new Color(colors[i * 4], colors[i * 4 + 1], colors[i * 4 + 2], colors[i * 4 + 3]); }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = vertices, normals = normals, colors = rgb };
            mesh.triangles = water ? data.Triangles.Take(data.Segments * data.Segments * 6).ToArray() : data.Triangles;
            mesh.RecalculateBounds(); mesh.UploadMeshData(true); return mesh;
        }
        void Part(string name, Mesh mesh, Material material)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); part.layer = 30; part.transform.SetParent(root.transform, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh; part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        Part("Land and seabed", Make(data.Positions, data.Colors, false), landMaterial);
        if (data.WaterColors.Where((_, i) => i % 4 == 3).Any(a => a > .01)) Part("Ocean surface", Make(data.WaterPositions, data.WaterColors, true), waterMaterial);
        if (tiles.TryGetValue(key, out var old)) DestroyTile(old); tiles[key] = new Tile { Root = root, Segments = data.Segments };
        lastBuildMs = data.BuildMilliseconds; largestBuildMs = Math.Max(largestBuildMs, lastBuildMs);
        if (timer.ElapsedMilliseconds > 20) Debug.Log("Local terrain upload " + timer.ElapsedMilliseconds + "ms");
    }
    void DestroyTile(Tile tile)
    { if(tile.Root==null)return;tile.Root.SetActive(false); foreach (var filter in tile.Root.GetComponentsInChildren<MeshFilter>()) Destroy(filter.sharedMesh); Destroy(tile.Root); }
    public string[] Probes()
    {
        var patch = terrain.Patch(frame.Origin, 6000); var result = new List<string>(); var culture = System.Globalization.CultureInfo.InvariantCulture;
        foreach(double offset in new[]{-2000.0,0,1234.0,4000.0})result.Add(string.Join(",", new[]{frame.Origin.X,frame.Origin.Y,frame.Origin.Z,offset,333.0,patch.Sample(offset,333).ElevationM}.Select(v=>v.ToString("R",culture)).ToArray()));
        return result.ToArray();
    }
    public void Capture(string path)
    {
        var target = new RenderTexture(1280, 800, 24); var previous = RenderTexture.active;
        var texture = new Texture2D(1280, 800, TextureFormat.RGB24, false); var previousRect=view.rect;view.rect=new Rect(0,0,1,1);view.targetTexture = target;
        try
        {
            view.Render(); RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); texture.Apply();
            var pixels = texture.GetPixels32(); var first = pixels[0];
            if (!pixels.Any(p => p.r != first.r || p.g != first.g || p.b != first.b)) throw new InvalidOperationException("Terrain capture is blank; do not accept it as visual evidence.");
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { view.targetTexture = null;view.rect=previousRect;RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(texture); }
    }
    public void TestMove(double east, double north) { x = east; z = north; }
    public void TestWalking() { walking = true; pitch = 0; }
    public void Close() { if (closed) return; closed = true; if(view!=null)view.enabled=false;cancellation.Cancel(); close?.Invoke(); }
    void OnDestroy()
    {
        cancellation.Cancel(); cancellation.Dispose(); foreach (var tile in tiles.Values) DestroyTile(tile);
        if (landMaterial != null) Destroy(landMaterial); if (waterMaterial != null) Destroy(waterMaterial);
    }
    void OnGUI()
    {
        GUI.Box(new Rect(0, 0, Screen.width, 50), "");
        if (GUI.Button(new Rect(10, 8, 125, 32), "← World [Esc]")) Close();
        GUI.Label(new Rect(150, 12, Screen.width - 170, 28), terrain == null ? "Preparing terrain profiles…" : (walking ? "Walking" : "Flying") + " · " + (walking ? "4.5" : speed.ToString("F0")) + " m/s · " + Diagnostics);
        GUI.Box(new Rect(0, Screen.height - 35, Screen.width, 35), "");
        GUI.Label(new Rect(14, Screen.height - 29, Screen.width - 25, 28), error.Length > 0 ? error : "WASD move · Right-drag look · F walk/fly · Shift faster · Space/Ctrl fly up/down · Wheel fly speed · Preview: inland water/vegetation pending");
    }
}
