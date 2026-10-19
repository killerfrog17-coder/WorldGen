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
    AsyncOperation terrainUnloading;
    World localTerrainWorld;
    double localTerrainScale;
    CancellationTokenSource localTerrainCancellation;
    V terrainPickedPosition; int terrainPickedCell = -1, terrainReturnFrame = -1; World terrainPickedWorld;
    bool CanExploreTerrain => worldReady && !Busy && world != null && humanStep == null;
    bool CanRequestTerrain => worldReady && !Busy && world != null && !enteringTerrain;
    void RequestTerrain()
    { if(CanRequestTerrain)StartCoroutine(WaitForTerrainEntry()); }
    IEnumerator WaitForTerrainEntry()
    {
        enteringTerrain=true;humanPlaying=false;
        while(humanStep!=null||terrainUnloading!=null&&!terrainUnloading.isDone)yield return null;
        enteringTerrain=false;ExploreTerrain();
    }
    void ExploreTerrain(V? exactPosition = null)
    {
        if (!CanExploreTerrain || terrainExplorer != null) return;
        WorldMeridian.Apply(play,world);
        if (!ReferenceEquals(localTerrainWorld, world) || localTerrainScale!=play.Settings.HorizontalScale || localTerrainTask == null || localTerrainTask.IsCanceled || localTerrainTask.IsFaulted)
        {
            localTerrainCancellation?.Cancel(); localTerrainCancellation?.Dispose(); localTerrainCancellation = new CancellationTokenSource();
            var source = world; var token = localTerrainCancellation.Token; localTerrainWorld = source;var scale=localTerrainScale=play.Settings.HorizontalScale;
            localTerrainTask = Task.Run(() => new LocalTerrain(source, token,scale), token);
        }
        int cell = selectedCell;
        if (cell < 0) cell = Array.FindIndex(world.ElevationM, h => h > 100 && h < 900);
        if (cell < 0) cell = 0;
        humanPlaying = false; cameraView.enabled = false; dragging = false; GUIUtility.keyboardControl = 0;
        if (globe != null) globe.SetActive(false);
        var scene = SceneManager.CreateScene("Local terrain preview");
        var root = new GameObject("Terrain explorer"); SceneManager.MoveGameObjectToScene(root, scene);
        terrainExplorer = root.AddComponent<LocalTerrainExplorer>();
        terrainExplorer.Initialize(localTerrainTask, exactPosition ?? (terrainPickedCell == cell && ReferenceEquals(terrainPickedWorld,world) ? terrainPickedPosition : world.Surface.Cells[cell].Position), () =>
        {
            terrainExplorer = null; terrainReturnFrame = Time.frameCount; cameraView.enabled = true; humanClockSample=Time.realtimeSinceStartupAsDouble;humanPlayback.Reset();humanPlaying = false;
            if (globe != null) globe.SetActive(true); terrainUnloading=SceneManager.UnloadSceneAsync(scene);
        },world,play,DrawPlayPause);
        terrainExplorer.MenuSkin=gameSkin;
        terrainExplorer.SetCommandHandler(text=>PlayCommands.Execute(play,SimulationTime.Seconds(world.Prehistory),text,terrainExplorer.Ready?Math.Atan2(terrainExplorer.Position.Y,terrainExplorer.Position.X)*180/Math.PI:play.PrimeMeridianLongitude,terrainExplorer.Ready?Math.Asin(Math.Max(-1,Math.Min(1,terrainExplorer.Position.Z)))*180/Math.PI:0,terrainExplorer.WeatherControl));
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
        bool earthSites=Environment.GetCommandLineArgs().Contains("--worldgen-local-earth-sites");
        bool groundReview=Environment.GetCommandLineArgs().Contains("--worldgen-ground-review");
        bool scatterReview=Environment.GetCommandLineArgs().Contains("--worldgen-local-scatter-review");
        if(scatterReview)
        {
            int Pick(Func<HabitatCell,bool> predicate)=>Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>world.ElevationM[i]>10&&world.ElevationM[i]<1800&&world.Ecology?.Habitat!=null&&predicate(world.Ecology.Habitat[i])).OrderBy(i=>world.Surface.Cells[i].Neighbors.Max(j=>Math.Abs(world.ElevationM[i]-world.ElevationM[j]))).DefaultIfEmpty(-1).First();
            sites=new[]{Pick(h=>h.Biome==Biome.Grassland||h.Biome==Biome.Savanna),Pick(h=>h.Biome==Biome.TemperateForest&&h.WarmTenthsC>140),Pick(h=>h.Biome==Biome.BorealForest&&h.WarmTenthsC>120)};
        }
        var locations=new[]{TerrainMapViewport.FromDegrees(27.98,86.92),TerrainMapViewport.FromDegrees(-32.65,-70.01),TerrainMapViewport.FromDegrees(38.07,-122.96),TerrainMapViewport.FromDegrees(32,88)};
        var names=new[]{"himalayas","andes","point-reyes","tibetan-plateau"};
        if(groundReview)
        {
            earthSites=true;
            if(world.Recipe.TerrainSource=="Earth")
            {locations=new[]{TerrainMapViewport.FromDegrees(38.6,-109.5),TerrainMapViewport.FromDegrees(-25,133),TerrainMapViewport.FromDegrees(38.07,-122.96),TerrainMapViewport.FromDegrees(50,10),TerrainMapViewport.FromDegrees(1,15)};names=new[]{"utah-ground","outback-ground","coastal-ground","temperate-ground","tropical-ground"};}
            else
            {var selected=new List<V>();foreach(var rock in new[]{RockFamily.Sandstone,RockFamily.Granite,RockFamily.Limestone}){int cell=cells.Where(i=>world.ElevationM[i]>10&&world.ElevationM[i]<1200&&GroundEnvironment.For(world).Cell(i).Bedrock==rock).OrderBy(i=>world.Ecology.Habitat[i].ReliefM).DefaultIfEmpty(-1).First();if(cell>=0)selected.Add(world.Surface.Cells[cell].Position);}locations=selected.ToArray();names=Enumerable.Range(0,locations.Length).Select(i=>"generated-ground-"+i).ToArray();}
        }
        var report = new List<string>();
        var probes = new List<string>();
        for (int s = 0; s < (earthSites?locations.Length:sites.Length); s++)
        {
            if (!earthSites&&sites[s] < 0) continue;
            deadline = Time.realtimeSinceStartupAsDouble + 180;
            selectedCell = sites[Math.Min(s,sites.Length-1)]; ExploreTerrain(earthSites?locations[s]:(V?)null);
            while(terrainExplorer!=null&&!terrainExplorer.Ready&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(terrainExplorer!=null&&terrainExplorer.Ready)
            {
                if(earthSites&&world.Recipe.TerrainSource=="Earth"&&s==2)terrainExplorer.TestNearbyShore();
                terrainExplorer.TestView(earthSites?(s==2?25:s==1?2000:900):600,earthSites?(s==0?0:s==1?90:s==2?340:45):20,earthSites&&s==2?6:12);
                if(scatterReview){terrainExplorer.TestWalking();terrainExplorer.TestView(1.65,20,-3);}
                if(groundReview){terrainExplorer.TestView(1.65,20,18);PlayCommands.Execute(play,0,"/weather clear",weather:terrainExplorer.WeatherControl);TerrainMapViewport.Degrees(terrainExplorer.Position,out double lat,out double lon);play.SetLocalSolarHour(12,lon);}
            }
            deadline = Time.realtimeSinceStartupAsDouble + 180;
            while (terrainExplorer != null && (!terrainExplorer.Ready || !terrainExplorer.AllReady || (scatterReview&&!terrainExplorer.ScatterReady)) && terrainExplorer.Error == "" && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (terrainExplorer == null || !terrainExplorer.Ready || !terrainExplorer.AllReady || (scatterReview&&!terrainExplorer.ScatterReady) || terrainExplorer.Error != "")
            { Debug.LogError("TERRAIN CHECK failed: " + terrainExplorer?.Error); Application.Quit(1); yield break; }
            yield return new WaitForEndOfFrame(); terrainExplorer.Capture(Path.Combine(output,earthSites?names[s]+".png":"terrain-" + s + ".png"));
            TerrainMapViewport.Degrees(terrainExplorer.Position,out double siteLat,out double siteLon);
            report.Add((earthSites?names[s]:"site "+sites[s])+" at "+siteLat.ToString("F6")+","+siteLon.ToString("F6")+": " + terrainExplorer.Diagnostics);
            if(groundReview)
            {
                report.Add(terrainExplorer.GroundDebugText);terrainExplorer.TestView(14,20,65);yield return new WaitForSecondsRealtime(.2f);terrainExplorer.Capture(Path.Combine(output,names[s]+"-overhead.png"));
                if(!terrainExplorer.GroundDebugText.Contains("pH")){Debug.LogError("GROUND CHECK missing physical ground inspection");Application.Quit(1);yield break;}
            }
            if(scatterReview)
            {
                var covered=terrainExplorer.LastCapturePixels;terrainExplorer.ScatterEnabled=false;
                yield return null;yield return null;yield return new WaitForEndOfFrame();terrainExplorer.Capture(null);
                var bare=terrainExplorer.LastCapturePixels;int changed=0;for(int i=0;i<bare.Length;i++)if(Math.Abs(bare[i].r-covered[i].r)+Math.Abs(bare[i].g-covered[i].g)+Math.Abs(bare[i].b-covered[i].b)>15)changed++;
                if(changed<1000){Debug.LogError("SCATTER CHECK vegetation is not visibly rendered: "+changed+" changed pixels");Application.Quit(1);yield break;}
                report.Add("Actual cover visibility: "+changed+" changed pixels versus the same camera with scatter disabled.");
                terrainExplorer.ScatterEnabled=true;yield return null;yield return null;
                double started=Time.realtimeSinceStartupAsDouble,worst=0;for(int f=0;f<90;f++){double at=Time.realtimeSinceStartupAsDouble;yield return null;worst=Math.Max(worst,Time.realtimeSinceStartupAsDouble-at);}
                report.Add("SCATTER "+world.Ecology.Habitat[sites[s]].Biome+": "+terrainExplorer.ScatterObjects+" submitted objects, "+terrainExplorer.ScatterChunks+" patches, "+terrainExplorer.ScatterDrawCalls+" draw calls; "+(90/(Time.realtimeSinceStartupAsDouble-started)).ToString("F1")+" FPS, worst frame "+worst.ToString("F4")+"s");
                if(terrainExplorer.ScatterObjects<10){Debug.LogError("SCATTER CHECK missing visible scale objects");Application.Quit(1);yield break;}
                terrainExplorer.TestView(65,20,55);yield return null;yield return new WaitForEndOfFrame();terrainExplorer.Capture(Path.Combine(output,"scatter-overhead-"+s+".png"));
            }
            probes.AddRange(terrainExplorer.Probes());
            terrainExplorer.TestMove(4300, 2800); yield return null;
            deadline = Time.realtimeSinceStartupAsDouble + 60;
            while ((!terrainExplorer.CenterReady || terrainExplorer.TileCount < 169) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (!terrainExplorer.CenterReady) { Debug.LogError("TERRAIN CHECK streaming movement failed"); Application.Quit(1); yield break; }
            terrainExplorer.TestWalking(); yield return null;yield return new WaitForEndOfFrame();
            if (s == 0) terrainExplorer.Capture(Path.Combine(output, "walking.png"));
            if(s==0)
            {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();terrainExplorer.TestKeyboard(keyboard);
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.F3));yield return null;yield return null;
                if(!terrainExplorer.DebugVisible){Debug.LogError("TERRAIN CHECK F3 failed");Application.Quit(1);yield break;}
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.M));yield return null;yield return null;
                if(!terrainExplorer.MapVisible){Debug.LogError("TERRAIN CHECK M failed");Application.Quit(1);yield break;}
                deadline=Time.realtimeSinceStartupAsDouble+60;
                while(!terrainExplorer.MapReady&&terrainExplorer.Error==""&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                if(!terrainExplorer.MapReady){Debug.LogError("TERRAIN CHECK debug map image failed: "+terrainExplorer.Error);Application.Quit(1);yield break;}
                terrainExplorer.CaptureMap(Path.Combine(output,"debug-map.png"));
                terrainExplorer.TestMapZoom(.05);var beforeTeleport=terrainExplorer.Position;terrainExplorer.TestMapClick(.73,.44);
                if(D.Angle(beforeTeleport,terrainExplorer.Position)<.01){Debug.LogError("TERRAIN CHECK map teleport failed");Application.Quit(1);yield break;}
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.M));yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.F3));yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;
                if(terrainExplorer.DebugVisible){Debug.LogError("TERRAIN CHECK debug close failed");Application.Quit(1);yield break;}
                terrainExplorer.TestWalking();float beforeLook=terrainExplorer.Heading;
                var mouse=InputSystem.AddDevice<Mouse>();mouse.MakeCurrent();
                InputSystem.QueueDeltaStateEvent(mouse.delta,new Vector2(40,0));yield return null;yield return null;
                if(terrainExplorer.Heading==beforeLook){Debug.LogError("TERRAIN CHECK mouse-look without drag failed");Application.Quit(1);yield break;}
                InputSystem.RemoveDevice(mouse);
                if(groundReview)
                {
                    terrainExplorer.TestNearbyFlatGround();yield return null;
                    double waitEnd=Time.realtimeSinceStartupAsDouble+30;while(!terrainExplorer.FootstepsReady&&Time.realtimeSinceStartupAsDouble<waitEnd)yield return null;
                    if(!terrainExplorer.FootstepsReady||terrainExplorer.FootstepClipCount!=28){Debug.LogError("GROUND CHECK generated footfall banks unavailable");Application.Quit(1);yield break;}
                    if(!terrainExplorer.FootstepsCentered){Debug.LogError("GROUND CHECK own-footstep stereo route is not centered mono");Application.Quit(1);yield break;}
                    report.Add("PASS own-footstep sources centered 2D, zero stereo pan, spatializer disabled, 28 mono clips");
                    var distances=new double[3];int mode=0;
                    foreach(var keys in new[]{new[]{Key.W,Key.C},new[]{Key.W},new[]{Key.W,Key.LeftShift}})
                    {
                        terrainExplorer.TestWalking();double beforeGait=terrainExplorer.TravelM,beforeSeconds=terrainExplorer.MovementSeconds;int beforeSteps=terrainExplorer.FootstepPlaybackCount;
                        Debug.Log("GROUND GAIT before "+mode+" "+terrainExplorer.FootstepDebug);
                        InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(keys));
                        double gaitDeadline=Time.realtimeSinceStartupAsDouble+45;
                        while(terrainExplorer.MovementSeconds-beforeSeconds<2&&Time.realtimeSinceStartupAsDouble<gaitDeadline)yield return null;
                        InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;
                        distances[mode++]=Math.Abs(terrainExplorer.TravelM-beforeGait)/Math.Max(.001,terrainExplorer.MovementSeconds-beforeSeconds);
                        if(terrainExplorer.FootstepPlaybackCount<=beforeSteps){Debug.LogError("GROUND CHECK gait "+mode+" movement produced no footfalls; travel="+(terrainExplorer.TravelM-beforeGait)+" time="+(terrainExplorer.MovementSeconds-beforeSeconds)+" "+terrainExplorer.FootstepDebug+" ground="+terrainExplorer.GroundDebugText+" map="+terrainExplorer.MapVisible+" paused="+terrainExplorer.Paused);Application.Quit(1);yield break;}
                        Debug.Log("GROUND GAIT "+mode+" speed "+distances[mode-1]+" "+terrainExplorer.FootstepDebug);
                    }
                    if(!(distances[0]<distances[1]&&distances[1]<distances[2])){Debug.LogError("GROUND CHECK C/default/Shift gait order failed");Application.Quit(1);yield break;}
                    int stationary=terrainExplorer.FootstepPlaybackCount;yield return new WaitForSecondsRealtime(.4f);
                    if(terrainExplorer.FootstepPlaybackCount!=stationary){Debug.LogError("GROUND CHECK stationary timer footsteps");Application.Quit(1);yield break;}
                    report.Add("PASS actual C walk / default jog / Shift sprint and distance-driven footsteps; stationary silence;28 loaded AI variants");
                }
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.Space));yield return null;yield return null;
                if(terrainExplorer.HeightAboveGround<=TerrainMotion.EyeHeightM){Debug.LogError("TERRAIN CHECK jump failed");Application.Quit(1);yield break;}
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.F));yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;
                if(terrainExplorer.IsWalking){Debug.LogError("TERRAIN CHECK F control failed");Application.Quit(1);yield break;}
                double before=terrainExplorer.TravelM;
                int beforeFlyingSteps=terrainExplorer.FootstepPlaybackCount;
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.W));yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;
                if(terrainExplorer.TravelM==before){Debug.LogError("TERRAIN CHECK movement control failed");Application.Quit(1);yield break;}
                if(groundReview&&terrainExplorer.FootstepPlaybackCount!=beforeFlyingSteps){Debug.LogError("GROUND CHECK flying created footsteps");Application.Quit(1);yield break;}
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.Escape));yield return null;yield return null;
                if(!terrainExplorer.Paused){Debug.LogError("TERRAIN CHECK Escape pause failed");Application.Quit(1);yield break;}
                InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());InputSystem.RemoveDevice(keyboard);
                terrainExplorer.Close();
            }
            else terrainExplorer.Close(); yield return null; yield return null;
            if (!cameraView.enabled || terrainExplorer != null || StateHash.Compute(world) != hash)
            { Debug.LogError("TERRAIN CHECK changed world or failed return"); Application.Quit(1); yield break; }
        }
        Application.logMessageReceived-=record;
        if(renderErrors.Count>0){Debug.LogError("TERRAIN CHECK rendering errors: "+string.Join(";",renderErrors));Application.Quit(1);yield break;}
        report.Add("PASS independent scene; Earth/seeded sites; movement/streaming; walking; F3/M map/teleport/F/W/Esc input; return; no render errors; unchanged world " + hash);
        File.WriteAllLines(Path.Combine(output, "result.txt"), report); File.WriteAllLines(Path.Combine(output, "probes.csv"), probes); Application.Quit(0);
    }
}
