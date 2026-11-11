using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Collections;
using UnityEngine.InputSystem;
using WorldGen.Core;
using WorldGen.Presentation;

/// <summary>Input and bounded rendering only; all height queries use the shared terrain sampler.</summary>
public sealed partial class LocalTerrainExplorer : MonoBehaviour
{
    sealed class Tile { public TerrainContactSurface Contact; public bool CoverageOnly,WaterBoundsKnown,HasWater;public Bounds WaterBounds; public int Segments; public double[] Edges; public float[] Heights;public NativeArray<float> Cover,MidCover,ForestCover;public ResidentCoverBuffers CoverBuffers;public sbyte MovingCover=-1; public float[] OcclusionMin;public float OcclusionCeiling;public int OcclusionStride; public GameObject Root; public Texture2D Coverage,CoverageTimes,FloraSurface,MidSurface,ForestSurface,ForestFloor; public float[] CoverageNodes,CoverageTimingNodes; public TerrainFadeCache CoverageInputs; public List<TerrainTileKey> CoverageChildren; public float FadeIn,FadeOut; public long BufferBytes,DiagnosticVertices,DiagnosticTriangles; public int DiagnosticRevision=-1; public Material[] Materials;public MeshRenderer[] Renderers;public TerrainMaterialBinding MaterialBinding;public CoverGpu Grass,GrassSprites; public GraphicsBuffer CoverGrid; public readonly Dictionary<CoverPageKey,CoverGpu> CoverPages=new Dictionary<CoverPageKey,CoverGpu>(); }
    sealed class Work { public TerrainTileKey Key; public double[] Edges; public int Segments,Generation; public bool Prefetch,Backdrop; public Task<TerrainUpload> Task; public PreparedFrame Prepared; public CancellationTokenSource Cancellation; }
    readonly TerrainEdges.Workspace terrainEdgeWorkspace=new TerrainEdges.Workspace();
    Task<LocalTerrain> building;
    LocalTerrain terrain; LocalTerrainFrame frame; LocalTerrain.LocalTerrainPatch walkingPatch;
    TerrainMeshProcess meshProcess;
    readonly TerrainMotion motion=new TerrainMotion();
    Dictionary<TerrainTileKey,Tile> tiles=new Dictionary<TerrainTileKey,Tile>();
    GameObject terrainRoot;
    Dictionary<TerrainTileKey,int> wanted=new Dictionary<TerrainTileKey,int>();
    readonly Dictionary<TerrainTileKey,double[]> wantedEdges=new Dictionary<TerrainTileKey,double[]>();
    readonly List<TerrainTileKey> priority=new List<TerrainTileKey>(); readonly List<Work> jobs=new List<Work>();
    readonly List<TerrainTileKey> terrainRetirementScratch=new List<TerrainTileKey>();
    Comparison<Work> terrainJobPriorityComparison;
    readonly CancellationTokenSource cancellation=new CancellationTokenSource();
    Action close; Camera view; Material landMaterial,waterMaterial;
    ExplorationAudioListener audioListener;
    Action drawPause;World sourceWorld;PlaySession playState;PlaySettings explorationSettings=new PlaySettings{HorizontalScale=1};
    public double HorizontalScale=>explorationSettings.HorizontalScale;
    public double LocalSolarHour=>playState==null?9:Ready?playState.LocalSolarHour(Math.Atan2(Position.Y,Position.X)*180/Math.PI):playState.SolarHour;
    public GUISkin MenuSkin { get; set; }
    readonly bool passiveSkyCheck=Environment.GetCommandLineArgs().Contains("--worldgen-sky-chat-check")||Environment.GetCommandLineArgs().Contains("--worldgen-atmosphere-check")||Environment.GetCommandLineArgs().Contains("--worldgen-weather-check");
    readonly bool passiveReanchorCheck=WorldGenPerformance.Enabled||Environment.GetCommandLineArgs().Contains("--worldgen-reanchor-review")||Environment.GetCommandLineArgs().Contains("--worldgen-landform-review");
    Keyboard testKeyboard;
    Keyboard ActiveKeyboard=>testKeyboard??Keyboard.current;
    public void TestKeyboard(Keyboard value){testKeyboard=value;}
    public void TestFieldOfView(float value){view.fieldOfView=value;}
    public string MusicDebugText { get; set; }="";
    public ExplorationMusicContext MusicContext(double hour,double year)
    {
        var position=Position;var biome=walkingPatch.Sample(position).Biome;
        double altitude=ExplorationMusicContext.SolarAltitude(Math.Asin(position.Z)*180/Math.PI,hour,year);
        return new ExplorationMusicContext(biome,ExplorationMusicContext.TimeAt(hour,altitude));
    }
    V startingPoint; double originX,originZ,plannedX=double.NaN,plannedZ,plannedAltitude;
    float heading=20,pitch=0; bool closed; int generation;
    double lastBuildMs,largestBuildMs; string error="";
    bool forestScheduledFirst;
    public bool Ready=>terrain!=null;
    public bool AcceptsQuickSave=>Ready&&!DeathVisible&&!inventoryOpen&&!mapOpen&&!paused&&!chatOpen&&!ecologyOpen;
    public bool IsWalking=>!motion.Flying;
    public double TravelM=>motion.X+motion.Z;
    public double MovementSeconds {get;private set;}
    public float Heading=>heading;
    public string Error=>error;
    public Color32[] LastCapturePixels { get; private set; }
    RenderTexture weatherCheckTarget;
    Texture2D weatherCheckPixel;
    public void BeginWeatherRenderCheck(){weatherCheckTarget=new RenderTexture(1280,800,24);weatherCheckTarget.Create();view.targetTexture=weatherCheckTarget;}
    public void WeatherCheckGpuBarrier(){if(weatherCheckPixel==null)weatherCheckPixel=new Texture2D(1,1,TextureFormat.RGB24,false);var prior=RenderTexture.active;RenderTexture.active=weatherCheckTarget;try{weatherCheckPixel.ReadPixels(new Rect(0,0,1,1),0,0,false);}finally{RenderTexture.active=prior;}}
    public int TileCount=>tiles.Count;
    public int WantedCount=>wanted.Count;
    public bool CenterReady
    {get{foreach(var k in tiles.Keys)if(motion.X>=k.X*k.SizeM&&motion.X<(k.X+1)*k.SizeM&&motion.Z>=k.Z*k.SizeM&&motion.Z<(k.Z+1)*k.SizeM)return true;return false;}}
    public bool NearReady=>wanted.Count>0&&!NearTerrainPending();
    public bool AllReady=>wanted.All(p=>tiles.TryGetValue(p.Key,out var t)&&!t.CoverageOnly&&t.Segments==p.Value&&TerrainEdges.Equal(t.Edges,wantedEdges[p.Key]));
    public V Position=>frame.Position(motion.X,motion.Z);
    public double HeightAboveGround=>motion.Y-Ground(motion.X,motion.Z);
    public string Diagnostics=>tiles.Count+" / "+wanted.Count+" tiles · last "+lastBuildMs.ToString("F1")+" ms · max "+largestBuildMs.ToString("F1")+" ms/tile";
    public void Initialize(Task<LocalTerrain> source,V position,Action onClose,World sourceWorld=null,PlaySession session=null,Action pauseMenu=null,TerrainMeshProcess worker=null,Action<string> onDeathExit=null)
    {
        building=source;meshProcess=worker;startingPoint=session!=null&&session.HasPlayer?session.PlayerPosition:position;close=onClose;deathExit=onDeathExit;this.sourceWorld=sourceWorld;playState=session;drawPause=pauseMenu;explorationSettings=session?.Settings.Copy()??new PlaySettings{HorizontalScale=1};
        mapViewport.Longitude=session?.PrimeMeridianLongitude??0;
        var cameraRoot=new GameObject("Exploration camera",typeof(Camera),typeof(UnityEngine.Rendering.Universal.UniversalAdditionalCameraData));
        cameraRoot.transform.SetParent(transform,false);cameraRoot.layer=30;view=cameraRoot.GetComponent<Camera>();
        view.cullingMask=1<<30;view.nearClipPlane=.1f;view.farClipPlane=300000;view.fieldOfView=68;
        ConfigureExplorationRenderer(false);
        view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.57f,.69f,.76f);
        var shader=Resources.Load<Shader>("WorldGenLocalTerrain");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Local terrain shader is missing or unsupported.");
        landMaterial=new Material(shader);landMaterial.SetFloat("_GroundFlora",1);InitializePlayerWater();SetFog();CaptureMouse();
        audioListener=new ExplorationAudioListener(view);
    }
    float publishedFog=float.NaN;int publishedFogMaterials=-1;Material publishedFogLand,publishedFogWater;
    public void ConfigureExplorationRenderer(bool original)
    {
        var data=view.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.SetRenderer(original?0:1);
        data.requiresColorOption=original?UnityEngine.Rendering.Universal.CameraOverrideOption.On:UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
    }
    void SetFog()
    {
        float distance=(float)Math.Min(viewDistance*.98,renderedStreamingFog);
        // Detached diagnostic cameras must not expose ground beyond the loaded
        // player-centred disk. Ordinary gameplay never sets this camera.
        if(landscapeReviewCamera.HasValue)
        {
            var camera=landscapeReviewCamera.Value;
            double dx=camera.x-(motion.X-originX),dz=camera.z-(motion.Z-originZ);
            distance=(float)Math.Min(distance,Math.Max(40,Math.Min(terrainRevealRadius,forestRevealRadius)-Math.Sqrt(dx*dx+dz*dz)-8));
        }
        // A settled reveal used to resend the identical uniform to every cached
        // species material every frame. New art is initialized to this datum too.
        if(!WorldGenPerformance.Original&&distance==publishedFog&&publishedFogMaterials==ownedScatterMaterials.Count&&publishedFogLand==landMaterial&&publishedFogWater==waterMaterial)return;
        publishedFog=distance;publishedFogMaterials=ownedScatterMaterials.Count;publishedFogLand=landMaterial;publishedFogWater=waterMaterial;
        landMaterial.SetFloat("_FogDistance",distance);waterMaterial.SetFloat("_FogDistance",distance);foreach(var material in legacyVegetationScene)if(material!=null&&material.HasProperty("_FogDistance"))material.SetFloat("_FogDistance",distance);
        PublishTerrainScene();
    }
    LocalTerrainFrame groundMemoFrame;double groundMemoX,groundMemoZ,groundMemoY,groundMemoElevation;
    Task<LocalTerrain.LocalTerrainPatch> walkingPreparation;int walkingPreparationGeneration;
    double walkingPreparationAt,nextWalkingPreparation;
    void PrepareWalkingSampler(double x,double z)
    {
        if(walkingPreparation!=null||Time.realtimeSinceStartupAsDouble<nextWalkingPreparation)return;
        double speed=Math.Sqrt(streamingVX*streamingVX+streamingVZ*streamingVZ);if(speed<20)return;
        nextWalkingPreparation=Time.realtimeSinceStartupAsDouble+.15;
        double lead=Math.Min(1000,speed*.9);var target=frame.Position(x+streamingVX/speed*lead,z+streamingVZ/speed*lead);
        if(walkingPatch!=null&&(target-walkingPatch.Center).Length*terrain.RadiusM*HorizontalScale<400)return;
        var model=terrain;double extent=2048/HorizontalScale;var token=cancellation.Token;
        walkingPreparationGeneration=generation;walkingPreparationAt=Time.realtimeSinceStartupAsDouble;
        walkingPreparation=Task.Run(()=>{token.ThrowIfCancellationRequested();var patch=model.Patch(target,extent);patch.RiverSpans();patch.Elevation(target);token.ThrowIfCancellationRequested();return patch;},token);
    }
    double Ground(double x,double z)
    {
        if(!WorldGenPerformance.Original&&ReferenceEquals(frame,groundMemoFrame)&&x==groundMemoX&&z==groundMemoZ&&walkingPatch!=null)return groundMemoY;
        long started=System.Diagnostics.Stopwatch.GetTimestamp();
        var at=frame.Position(x,z);
        if(walkingPreparation!=null&&walkingPreparation.IsCompleted)
        {
            if(walkingPreparation.IsCompletedSuccessfully&&walkingPreparationGeneration==generation)
            {
                var prepared=walkingPreparation.Result;
                if((at-prepared.Center).Length*terrain.RadiusM*HorizontalScale<=450){walkingPatch=prepared;walkingPreparation=null;}
                else if(Time.realtimeSinceStartupAsDouble-walkingPreparationAt>2)walkingPreparation=null;
            }
            else walkingPreparation=null;
        }
        if(walkingPatch==null||(at-walkingPatch.Center).Length*terrain.RadiusM>500/HorizontalScale)walkingPatch=terrain.Patch(at,2048/HorizontalScale);
        if(WorldGenPerformance.IndexedWalkingRivers)walkingPatch.RiverSpans();
        double elevation=WorldGenPerformance.Original?walkingPatch.Sample(at).ElevationM:walkingPatch.Elevation(at);double height=frame.SurfaceY(at,elevation);
        groundMemoElevation=elevation;
        double elapsed=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        largestGroundMs=Math.Max(largestGroundMs,elapsed);
        if(WorldGenPerformance.TraceEnabled)WorldGenPerformance.TraceMilliseconds[4]+=elapsed;
        if(elapsed>20)Debug.Log("Local ground query "+elapsed.ToString("F2")+"ms");
        groundMemoFrame=frame;groundMemoX=x;groundMemoZ=z;groundMemoY=height;
        PrepareWalkingSampler(motion.X,motion.Z);
        return height;
    }
    public void Teleport(V point)
    {
        if(terrain==null)return;
        DetachWatercraft();
        ClearInventoryPhysics();CancelPreparedFrame();
        ResetStreamingReuse(true);
        generation++;startingPoint=point;walkingPatch=null;originX=originZ=0;
        frame=new LocalTerrainFrame(point,terrain.RadiusM,terrain.Patch(point).Sample(0,0).ElevationM,HorizontalScale);
        motion.Place(0,0,Ground(0,0),motion.Flying);ResetFootsteps();landMaterial.SetVector("_LocalOrigin",Vector4.zero);
        foreach(var tile in tiles.Values)DestroyTile(tile);tiles.Clear();if(terrainRoot!=null)Destroy(terrainRoot);terrainRoot=NewTerrainRoot(true);
        ResetDetailFrame();plannedX=double.NaN;RefreshWanted();UpdateCamera();ResetScatterFrame();ResetWeatherFrame();
        if(SkyFrame!=null)SetDaylight(LocalSolarHour,skyYear); // No blank-sky frame during automatic tangent-chart reanchoring.
    }
    void Update()
    {
        long started=WorldGenPerformance.TraceBegin();try{UpdateBody();}finally{WorldGenPerformance.TraceEnd(0,started);}
    }
    void UpdateBody()
    {
        double fpsNow=Time.realtimeSinceStartupAsDouble;
        if(fpsClock>0)UpdateDebugFps(fpsNow-fpsClock);
        fpsClock=fpsNow;
        RecordLoadingFrame();
        if(closed)return;
        CaptureMouse();
        bool inventoryInput=!WorldGenPerformance.Enabled&&HandleInventoryKeys();
        bool chatInput=!DeathVisible&&!inventoryInput&&!WorldGenPerformance.Enabled&&HandleChatKeys();if(Application.isFocused&&!DeathVisible&&!inventoryInput&&!chatInput&&!WorldGenPerformance.Enabled)HandleDebugKeys();if(closed)return;
        if(terrain==null)
        {
            if(!building.IsCompleted)return;
            if(building.IsFaulted||building.IsCanceled){error=building.Exception?.GetBaseException().Message??"Cancelled";debugOpen=true;CaptureMouse();return;}
            terrain=building.Result;viewDistance=Math.Min(viewDistance,MaximumViewDistance);InitializeScatter();Teleport(startingPoint);InitializeAnimalFauna();InitializeHumans();InitializeInventory();
            if(playState!=null&&playState.HasPlayer){heading=(float)playState.Heading;pitch=(float)playState.Pitch;motion.Flying=playState.Flying;motion.Y=Ground(0,0)+(Spectating?playState.HeightAboveGround:Math.Max(TerrainMotion.EyeHeightM,playState.HeightAboveGround));UpdateCamera();}
            ApplyPlayerMode();
        }
        UpdateLandmarkSearch();ApplyTerrainDiagnostics();ApplyFoliageShadowOptions();installationsThisFrame=0;frameUploadMs=0;reserveScatterFrame=false;double movementStartX=motion.X,movementStartZ=motion.Z;
        long inventoryTrace=WorldGenPerformance.TraceBegin();UpdateInventory();WorldGenPerformance.TraceEnd(33,inventoryTrace);
        long mapStarted=WorldGenPerformance.TraceBegin();UpdateMapWork();WorldGenPerformance.TraceEnd(1,mapStarted);
        var keyboard=ActiveKeyboard;var mouse=Mouse.current;
        UpdatePlayerDeath();
        long solidsTrace=WorldGenPerformance.TraceBegin();PreparePlayerSolids();WorldGenPerformance.TraceEnd(39,solidsTrace);
        motion.Water=SampleMotionWater;motion.WaterEyeOffset=humanEyeOffset;UpdateWatercraft();
        if(activeWatercraft==null&&!DeathVisible&&!inventoryOpen&&(!inventoryInput||campPlaceMaterial!=null||blueprintPlacement!=null)&&!mapOpen&&!paused&&!chatInput&&!ecologyOpen&&(Application.isFocused||testKeyboard!=null))
        {
            if(mouse!=null&&(!SurvivalActive||!Vitals.Incapacitated)&&!passiveSkyCheck&&!passiveReanchorCheck&&!inventoryReviewMode){var d=mouse.delta.ReadValue();heading+=d.x*.1f;pitch=Mathf.Clamp(pitch-d.y*.1f,-85,85);
                ReadHumanCameraInput(mouse,keyboard);}
            if(keyboard!=null&&!WorldGenPerformance.Enabled)
            {
                MovementSeconds+=Math.Max(0,Math.Min(.25,Time.unscaledDeltaTime));
                if(Mode==GameMode.Creative&&keyboard.fKey.wasPressedThisFrame){motion.Flying=!motion.Flying;motion.VerticalSpeed=0;}
                bool alive=!SurvivalActive||!Vitals.Incapacitated;
                survivalSprinting=!PlayerCrouching&&!keyboard.cKey.isPressed&&keyboard.shiftKey.isPressed&&(!SurvivalActive||Vitals.CanSprint);
                bool jump=alive&&!motion.Flying&&!motion.Swimming&&keyboard.spaceKey.wasPressedThisFrame&&(!SurvivalActive||Vitals.Stamina>=SurvivalRules.JumpStaminaCost);
                if(SurvivalActive&&jump&&motion.Grounded)SurvivalRules.SpendStamina(Vitals,SurvivalRules.JumpStaminaCost);
                motion.Move(Time.unscaledDeltaTime,alive?(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0):0,
                    alive?(keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed&&!keyboard.f3Key.isPressed?1:0):0,(keyboard.spaceKey.isPressed?1:0)-(keyboard.ctrlKey.isPressed?1:0),
                    heading,pitch,survivalSprinting,jump,PlayerSolidGround,walk:keyboard.cKey.isPressed||PlayerCrouching,speedScale:PlayerEffortSpeedScale(survivalSprinting,keyboard.cKey.isPressed||PlayerCrouching)*Inventory.LoadSpeedScale*(PlayerCrouching?.55:1));
                double moveDt=Math.Max(.001,Math.Min(.25,Time.unscaledDeltaTime));
                playerDeathVelocity=new Vector3((float)((motion.X-movementStartX)/moveDt),(float)(-Math.Max(-motion.VerticalSpeed,motion.LandingSpeedMps)),(float)((motion.Z-movementStartZ)/moveDt));
                PlayerDamageRules.Apply(Vitals,Mode,PlayerDamageRules.FallDamage(motion.LandingSpeedMps),PlayerDeathCause.Fall);
            }
        }
        if(activeWatercraft==null&&(!Application.isFocused&&testKeyboard==null||inventoryOpen)&&!DeathVisible&&!mapOpen&&!paused&&!chatOpen&&!ecologyOpen)
        {
            survivalSprinting=false;motion.Move(Time.unscaledDeltaTime,0,0,0,heading,pitch,false,false,PlayerSolidGround,speedScale:1);
            playerDeathVelocity=new Vector3(0,(float)motion.VerticalSpeed,0);
            PlayerDamageRules.Apply(Vitals,Mode,PlayerDamageRules.FallDamage(motion.LandingSpeedMps),PlayerDeathCause.Fall);
        }
        TrackStreamingMotion(motion.X-movementStartX,motion.Z-movementStartZ,Time.unscaledDeltaTime);
        huntingSpeed=Math.Sqrt(Math.Pow(motion.X-movementStartX,2)+Math.Pow(motion.Z-movementStartZ,2))/Math.Max(.001,Time.unscaledDeltaTime);
        long componentTrace=WorldGenPerformance.TraceBegin();
        UpdateCamp();UpdatePlayerSurvival(motion.X-movementStartX,motion.Z-movementStartZ);UpdateCombatBleeding();
        WorldGenPerformance.TraceEnd(17,componentTrace);
        if(Math.Abs(motion.X-originX)>8192||Math.Abs(motion.Z-originZ)>8192)
        {
            double newOriginX=Math.Floor(motion.X/2048)*2048,newOriginZ=Math.Floor(motion.Z/2048)*2048;
            if(newOriginX!=originX||newOriginZ!=originZ)ClearInventoryPhysics();
            originX=newOriginX;originZ=newOriginZ;
            landMaterial.SetVector("_LocalOrigin",new Vector4((float)originX,(float)originZ,0,0));
            PublishTerrainScene();
            foreach(var entry in tiles)entry.Value.Root.transform.localPosition=new Vector3((float)(entry.Key.X*entry.Key.SizeM-originX),0,(float)(entry.Key.Z*entry.Key.SizeM-originZ));
            foreach(var entry in previousTerrain)entry.Value.Root.transform.localPosition=new Vector3((float)(entry.Key.X*entry.Key.SizeM-originX),0,(float)(entry.Key.Z*entry.Key.SizeM-originZ));
            foreach(var chunk in scatterChunks.Values)ProjectScatter(chunk);scatterDirty=true;
        }
        UpdatePlayerDeath();
        componentTrace=WorldGenPerformance.TraceBegin();
        if(!TerrainDiagnostics.HoldTerrain&&!DeathVisible)UpdatePreparedFrame();
        WorldGenPerformance.TraceEnd(18,componentTrace);
        componentTrace=WorldGenPerformance.TraceBegin();UpdateHumans();WorldGenPerformance.TraceEnd(19,componentTrace);
        componentTrace=WorldGenPerformance.TraceBegin();
        long playerTrace=WorldGenPerformance.TraceBegin();UpdateCamera();WorldGenPerformance.TraceEnd(40,playerTrace);
        playerTrace=WorldGenPerformance.TraceBegin();UpdatePlayerWater();WorldGenPerformance.TraceEnd(41,playerTrace);
        playerTrace=WorldGenPerformance.TraceBegin();UpdateItemCombat();WorldGenPerformance.TraceEnd(42,playerTrace);
        playerTrace=WorldGenPerformance.TraceBegin();UpdateBloodSigns();WorldGenPerformance.TraceEnd(43,playerTrace);
        playerTrace=WorldGenPerformance.TraceBegin();UpdateFootsteps();WorldGenPerformance.TraceEnd(44,playerTrace);
        playerTrace=WorldGenPerformance.TraceBegin();UpdateBodyFeedback();WorldGenPerformance.TraceEnd(45,playerTrace);
        playerTrace=WorldGenPerformance.TraceBegin();UpdateSurvivalHud();WorldGenPerformance.TraceEnd(46,playerTrace);
        playerTrace=WorldGenPerformance.TraceBegin();double altitude=HeightAboveGround;WorldGenPerformance.TraceEnd(47,playerTrace);
        WorldGenPerformance.TraceEnd(20,componentTrace);
        debugGroundClearance=altitude;debugElevation=groundMemoElevation+altitude;
        ReserveScatterFrame();
        if(TerrainDiagnostics.HoldTerrain){UpdateScatter();UpdateAnimalFauna();UpdateWeatherPresentation();UpdateDebugReadout();return;}
        long planningTrace=WorldGenPerformance.TraceBegin();
        double replanDistance=TerrainLod.ReplanDistance(HorizontalScale);
        if(double.IsNaN(plannedX)||Math.Abs(motion.X-plannedX)>replanDistance||Math.Abs(motion.Z-plannedZ)>replanDistance||Math.Abs(altitude-plannedAltitude)>Math.Max(150,plannedAltitude*.3))RefreshWanted();
        if(!mapOpen){PollTerrainDetail();RefreshStreamingPrediction();}
        WorldGenPerformance.TraceEnd(34,planningTrace);
        AdvanceGroundPublication();
        long installTrace=WorldGenPerformance.TraceBegin();
        // Completed nearby products precede cache-only/hidden background work.
        terrainJobPriorityComparison??=(a,b)=>{double Score(Work w)
        {
            // Once the next chart is due, its finished contact meshes must
            // actually reach the scene. An endless current-frame upload stream
            // otherwise starves them even after the worker did the work.
            var pending=w.Prepared;
            if(pending!=null&&ReferenceEquals(pending,preparedFrame)&&ReanchorDistance>=ReanchorLimit*.8&&
                TerrainLod.DistanceSquared(w.Key,pending.X,pending.Z)<=Math.Pow(2048*HorizontalScale,2))
                return -1e12+TerrainLod.DistanceSquared(w.Key,pending.X,pending.Z);
            return WorkMatchesCurrent(w)?StreamingPriority(w.Key):double.MaxValue;
        }return Score(b).CompareTo(Score(a));};
        jobs.Sort(terrainJobPriorityComparison);
        for(int i=jobs.Count-1;i>=0;i--)
        {
            if(!CanInstall)break;
            var job=jobs[i];if(!job.Task.IsCompleted)continue;jobs.RemoveAt(i);
            bool requestedCancel=job.Cancellation?.IsCancellationRequested??false;job.Cancellation?.Dispose();
            if(job.Task.IsFaulted)
            {
                var fault=job.Task.Exception.GetBaseException();
                if(requestedCancel&&fault is OperationCanceledException){streamingCanceled++;continue;}
                Debug.LogWarning("Terrain mesh request failed (generation "+job.Generation+", current "+generation+", cancel="+requestedCancel+"): "+job.Task.Exception);
                error=fault.Message;debugOpen=true;CaptureMouse();continue;
            }
            if(job.Task.IsCanceled){streamingCanceled++;continue;}
            using var upload=job.Task.Result;
            streamingCompleted++;
            if(job.Prepared!=null)
            {
                if(!job.Task.IsCanceled&&job.Generation==generation&&ReferenceEquals(job.Prepared,preparedFrame))
                    Upload(job.Key,upload,job.Edges,job.Prepared);
                continue;
            }
            if(job.Generation!=generation)continue;
            if(job.Prefetch&&!WorkMatchesCurrent(job))
            {
                if(prefetchWanted.TryGetValue(job.Key,out int predictedDetail)&&predictedDetail==job.Segments&&TerrainEdges.Equal(job.Edges,prefetchEdges[job.Key]))Upload(job.Key,upload,job.Edges,cacheOnly:true);
                continue;
            }
            if(!WorkMatchesCurrent(job))
            {
                if(!wanted.TryGetValue(job.Key,out int currentDetail))streamingDiscardKey++;
                else if(currentDetail!=job.Segments)streamingDiscardDetail++;
                else streamingDiscardEdges++;
                continue;
            }
            Upload(job.Key,upload,job.Edges,fallback:job.Backdrop);
        }
        WorldGenPerformance.TraceEnd(35,installTrace);
        long retirementTrace=WorldGenPerformance.TraceBegin();
        UpdateTerrainTransitions();RetireObsoleteTerrain();
        WorldGenPerformance.TraceEnd(36,retirementTrace);
        long scatterTrace=WorldGenPerformance.TraceBegin(),scatterStarted=WorldGenPerformance.Begin();UpdateScatter();WorldGenPerformance.End(0,scatterStarted);WorldGenPerformance.TraceEnd(2,scatterTrace);
        componentTrace=WorldGenPerformance.TraceBegin();UpdateAnimalFauna();WorldGenPerformance.TraceEnd(21,componentTrace);
        long weatherTrace=WorldGenPerformance.TraceBegin(),weatherStarted=WorldGenPerformance.Begin();UpdateWeatherPresentation();WorldGenPerformance.End(3,weatherStarted);WorldGenPerformance.TraceEnd(3,weatherTrace);
        long readoutTrace=WorldGenPerformance.TraceBegin();UpdateDebugReadout();WorldGenPerformance.TraceEnd(38,readoutTrace);
        if(mapOpen){UpdateTerrainCoverage();UpdateStreamingFog();RetireTerrainGradually();return;}
        // Nearby ground always goes first, including when a hidden replacement
        // chart is overdue. Do not let scatter or far work monopolize both slots.
        long schedulingTrace=WorldGenPerformance.TraceBegin();
        bool nearPending=NearTerrainPending();
        bool coveredFlight=motion.Flying&&HeightAboveGround>80&&WorkerLimit>1&&!double.IsNaN(VisibleGround(motion.X,motion.Z,double.NaN));
        // Free ONE occupied lane only if neither lane is serving near ground.
        // Continually cancelling all background work would itself cause churn.
        bool urgentContact=!coveredFlight||HeightAboveGround<=detailOptions.FullRadius(HorizontalScale);
        if(nearPending&&urgentContact&&ActiveWorkerCount>=WorkerLimit&&!jobs.Any(j=>WorkMatchesCurrent(j)&&ImmediateTerrain(j.Key)))
        {
            var background=jobs.LastOrDefault(j=>j.Prepared==null&&!ImmediateTerrain(j.Key)&&!j.Task.IsCompleted);
            if(background!=null){if(background.Cancellation?.IsCancellationRequested==false)streamingPreempted++;background.Cancellation?.Cancel();}
            else if(!coveredFlight||forestJobs.Count(j=>!j.Task.IsCompleted)+jobs.Count(j=>j.Prepared!=null&&!j.Task.IsCompleted)>=WorkerLimit)
            {
                var forest=forestJobs.LastOrDefault(j=>!j.Task.IsCompleted&&!j.Cancellation.IsCancellationRequested);
                if(forest!=null)forest.Cancellation.Cancel();else jobs.LastOrDefault(j=>j.Prepared!=null&&!j.Task.IsCompleted)?.Cancellation?.Cancel();
            }
        }
        bool handoffDue=preparedFrame!=null&&ReanchorDistance>=ReanchorLimit;
        // Current ground gets the lanes first. A covered flight is not a
        // licence to queue the distant horizon and trees ahead of nearby land.
        long priorTerrainRequests=TerrainBuildRequests;
        ScheduleCurrentTerrain(nearOnly:true);
        if(!NearTerrainPending())
        {
            // Fill missing outward coverage before upgrading already visible
            // land. Only after that do forest/prediction use spare workers.
            ScheduleBackdrop();
            ScheduleCurrentTerrain(missingOnly:true);
            ScheduleCurrentTerrain();
            SchedulePreparedWork(false);
            if(coveredFlight)ScheduleFlightBackground();
            ScheduleForestWork();
            SchedulePredictiveTerrain();
        }
        if(TerrainBuildRequests>priorTerrainRequests)forestScheduledFirst=false;
        WorldGenPerformance.TraceEnd(37,schedulingTrace);
        UpdateTerrainCoverage();UpdateStreamingFog();
        RetireTerrainGradually();
        if(WorldGenPerformance.Enabled&&!WorldGenPerformance.Measuring)PerformanceVerifyPending();
    }
    void ScheduleCurrentTerrain(bool nearOnly=false,bool missingOnly=false)
    {
        foreach(var key in priority)
        {
            if(nearOnly&&!ImmediateTerrain(key)||missingOnly&&HasResidentCoverage(key))continue;
            int detail=wanted[key];
            if(tiles.TryGetValue(key,out var tile)&&!tile.CoverageOnly&&tile.Segments==detail&&TerrainEdges.Equal(tile.Edges,wantedEdges[key]))continue;
            if(TerrainProducts.Contains(new TerrainProductKey(key,detail,wantedEdges[key],false)))
            {RestoreTerrain(key,detail,wantedEdges[key]);continue;}
            if(!CanRequest||ActiveWorkerCount>=WorkerLimit)break;
            if(HasMatchingTerrainJob(key))continue;
            RequestTerrain(key,detail,wantedEdges[key]);
        }
    }
    bool HasMatchingTerrainJob(TerrainTileKey key)
    {if(GroundPublicationPending&&groundPublication.Key.Equals(key))return true;foreach(var job in jobs)if(WorkMatchesCurrent(job)&&job.Key.Equals(key))return true;return false;}
    void UpdateCamera(){if(landscapeReviewCamera.HasValue){view.transform.localPosition=landscapeReviewCamera.Value;view.transform.localRotation=landscapeReviewRotation;return;}if(deathPresented){UpdateDeathCamera();return;}if(humanPlayer!=null){UpdateHumanCamera();return;}view.transform.localPosition=new Vector3((float)(motion.X-originX),(float)motion.Y,(float)(motion.Z-originZ));view.transform.localRotation=Quaternion.Euler(pitch,heading,0);}
    int residentRevision,nearMemoFrame=-1,nearMemoRevision,nearMemoCount;double nearMemoX,nearMemoZ;bool nearMemoPending;
    Dictionary<TerrainTileKey,int> nearMemoWanted;
    bool NearTerrainPendingFresh()
    {
        foreach(var pair in wanted)if(ImmediateTerrain(pair.Key)&&
            (!tiles.TryGetValue(pair.Key,out var tile)||tile.CoverageOnly||tile.Segments!=pair.Value||!TerrainEdges.Equal(tile.Edges,wantedEdges[pair.Key])))return true;
        return false;
    }
    bool NearTerrainPending()
    {
        if(WorldGenPerformance.Original)return NearTerrainPendingFresh();
        // This predicate is asked by several schedulers per frame. Uploads,
        // restores, plan/chart replacement and movement invalidate immediately.
        if(nearMemoFrame==Time.frameCount&&nearMemoRevision==residentRevision&&nearMemoCount==tiles.Count&&ReferenceEquals(nearMemoWanted,wanted)&&nearMemoX==motion.X&&nearMemoZ==motion.Z)return nearMemoPending;
        nearMemoFrame=Time.frameCount;nearMemoRevision=residentRevision;nearMemoCount=tiles.Count;nearMemoWanted=wanted;nearMemoX=motion.X;nearMemoZ=motion.Z;
        return nearMemoPending=NearTerrainPendingFresh();
    }
    void RefreshWanted(bool resetDetail=false)
    {
        bool bootstrap=resetDetail||detailBaseline||legacyPlanRefresh||double.IsNaN(plannedX);
        plannedX=motion.X;plannedZ=motion.Z;plannedAltitude=HeightAboveGround;
        if(bootstrap)detailCancellation?.Cancel();detailDirty=true;
        var fallback=InitialDetailPlan(motion.X,motion.Z,plannedAltitude);
        wanted=bootstrap?fallback:TerrainStreamingPlan.CompleteCoverage(TerrainStreamingPlan.ProtectNear(wanted,motion.X,motion.Z,HorizontalScale,detailOptions.ProtectedRadius(HorizontalScale,plannedAltitude),detailOptions.FinestSegments,Math.Max(1000,viewDistance)),fallback);
        priority.Clear();priority.AddRange(wanted.Keys);PlanBackdrop();
        wantedEdges.Clear();foreach(var pair in terrainEdgeWorkspace.Build(wanted))wantedEdges.Add(pair.Key,pair.Value);
        foreach(var key in tiles.Keys.Where(k=>!wanted.ContainsKey(k)&&!wanted.Keys.Any(w=>Overlaps(k,w))).ToArray()){CacheTerrain(key,tiles[key]);tiles.Remove(key);residentRevision++;}
        foreach(var job in jobs)
            if(job.Prepared==null&&!job.Prefetch&&!job.Backdrop&&(!wanted.TryGetValue(job.Key,out int detail)||detail!=job.Segments||!TerrainEdges.Equal(job.Edges,wantedEdges[job.Key])))job.Cancellation?.Cancel();
        double Distance(TerrainTileKey k)=>TerrainLod.DistanceSquared(k,motion.X,motion.Z);
        priority.Sort((a,b)=>{int d=Distance(a).CompareTo(Distance(b));return d!=0?d:a.Level!=b.Level?a.Level.CompareTo(b.Level):a.X!=b.X?a.X.CompareTo(b.X):a.Z.CompareTo(b.Z);});
    }
    static bool Overlaps(TerrainTileKey a,TerrainTileKey b)=>Math.Max(a.X*a.SizeM,b.X*b.SizeM)<Math.Min((a.X+1)*a.SizeM,(b.X+1)*b.SizeM)&&Math.Max(a.Z*a.SizeM,b.Z*b.SizeM)<Math.Min((a.Z+1)*a.SizeM,(b.Z+1)*b.SizeM);
    void Upload(TerrainTileKey key,TerrainUpload data,double[] edges,PreparedFrame prepared=null,bool cacheOnly=false,bool fallback=false)
    {
        streamingInstalled++;streamingWorkerMs+=data.BuildMilliseconds;
        var timer=System.Diagnostics.Stopwatch.StartNew();var root=new GameObject("Terrain "+key.X+","+key.Z+" L"+key.Level);root.transform.SetParent(prepared==null?terrainRoot.transform:prepared.Root.transform,false);
        if(cacheOnly)root.SetActive(false);installationsThisFrame++;
        root.transform.localPosition=new Vector3((float)(data.EastM-(prepared==null?originX:0)),0,(float)(data.NorthM-(prepared==null?originZ:0)));
        int collections=GC.CollectionCount(0);double beforeApply=timer.Elapsed.TotalMilliseconds;
        var meshes=data.Apply(WorldGenPerformance.ReuseTerrainMeshes?terrainMeshPool:null);double applyMs=timer.Elapsed.TotalMilliseconds-beforeApply;
        void Part(string name,Mesh mesh,Material material){var part=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));part.layer=30;part.transform.SetParent(root.transform,false);part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<MeshRenderer>().sharedMaterial=material;}
        Part("Land and seabed",meshes[0],landMaterial);
        if(data.HasWater)Part("Water surface",meshes[1],waterMaterial);
        var target=prepared==null?tiles:prepared.Tiles;
        if(prepared!=null)prepared.Revision++;
        var product=new Tile{Contact=data.Contact,CoverageOnly=fallback,Root=root,Segments=data.Segments,Edges=edges,Heights=data.Heights,Cover=data.Cover,MidCover=data.MidCover,ForestCover=data.ForestCover,BufferBytes=data.BufferBytes,WaterBoundsKnown=true,HasWater=data.HasWater,WaterBounds=data.WaterBounds};
        foreach(var mesh in meshes){product.DiagnosticVertices+=mesh.GetSubMesh(0).vertexCount;product.DiagnosticTriangles+=(long)mesh.GetIndexCount(0)/3;}
        product.FloraSurface=CreateFloraSurface(product);if(product.FloraSurface!=null)product.BufferBytes+=(long)(product.Segments+1)*(product.Segments+1)*8;
        product.MidSurface=CreateMidSurface(product);if(product.MidSurface!=null)product.BufferBytes+=(long)(product.Segments+1)*(product.Segments+1)*8;
        product.ForestSurface=CreateForestSurface(product);if(product.ForestSurface!=null)product.BufferBytes+=(long)(product.Segments+1)*(product.Segments+1)*8;
        product.ForestFloor=CreateForestFloorSurface(product);if(product.ForestFloor!=null)product.BufferBytes+=(long)(product.Segments+1)*(product.Segments+1)*4;
        BindTerrainTile(product,product,key,Vector4.zero);
        product.CoverBuffers=data.TakeCoverBuffers();
        if(cacheOnly){CacheTerrain(key,product);return;}
        if(prepared==null&&StageGroundPublication(key,product,fallback))
        {LargestUploadMs=Math.Max(LargestUploadMs,timer.Elapsed.TotalMilliseconds);frameUploadMs+=timer.Elapsed.TotalMilliseconds;return;}
        if(target.TryGetValue(key,out var old)){if(prepared==null)BeginTerrainReplacement(key,product,old);else DestroyTile(old);}
        else if(prepared==null)BeginTerrainReplacement(key,product,null);
        target[key]=product;if(prepared==null)residentRevision++;
        if(prepared==null)coverageDirty=true;
        if(prepared==null&&scatterGroundChanges.Add(key,old,product)){scatterGroundRevision++;NoteGroundChange(key,GroundOrigin.Upload);}lastBuildMs=data.BuildMilliseconds;largestBuildMs=Math.Max(largestBuildMs,lastBuildMs);
        LargestUploadMs=Math.Max(LargestUploadMs,timer.Elapsed.TotalMilliseconds);
        frameUploadMs+=timer.Elapsed.TotalMilliseconds;
        if(timer.ElapsedMilliseconds>20)Debug.Log("Local terrain upload "+timer.ElapsedMilliseconds+"ms; apply "+applyMs.ToString("F2")+"ms; objects/retire "+(timer.Elapsed.TotalMilliseconds-beforeApply-applyMs).ToString("F2")+"ms; collections "+(GC.CollectionCount(0)-collections)+"; worker buffers "+data.BufferMilliseconds.ToString("F2")+"ms");
    }
    readonly List<MeshFilter> terrainMeshReleaseScratch=new List<MeshFilter>(2);
    static int ReleaseTerrainMeshes(GameObject root,List<MeshFilter> scratch,TerrainMeshPool pool=null)
    {
        // Include individually disabled descendants too. Disabling only the
        // root does not by itself establish that Unity omits its active parts.
        root.GetComponentsInChildren(true,scratch);int count=0;
        foreach(var filter in scratch)if(filter.sharedMesh!=null){var mesh=filter.sharedMesh;filter.sharedMesh=null;if(pool==null||!pool.Release(mesh))UnityEngine.Object.Destroy(mesh);count++;}
        scratch.Clear();return count;
    }
    void DestroyTile(Tile tile){ReleaseGroundCover(tile);if(tile.CoverBuffers!=null){tile.CoverBuffers.Dispose();tile.CoverBuffers=null;tile.Cover=tile.MidCover=tile.ForestCover=default;}if(tile.FloraSurface!=null)Destroy(tile.FloraSurface);if(tile.MidSurface!=null)Destroy(tile.MidSurface);if(tile.ForestSurface!=null)Destroy(tile.ForestSurface);if(tile.ForestFloor!=null)Destroy(tile.ForestFloor);if(tile.Coverage!=null)Destroy(tile.Coverage);if(tile.CoverageTimes!=null)Destroy(tile.CoverageTimes);if(tile.Materials!=null){foreach(var material in tile.Materials)if(material!=null)Destroy(material);tile.Materials=null;tile.Renderers=null;tile.MaterialBinding=null;}if(tile.Root==null)return;tile.Root.SetActive(false);ReleaseTerrainMeshes(tile.Root,terrainMeshReleaseScratch,terrainMeshPool);Destroy(tile.Root);}
    public string[] Probes()
    {
        var patch=terrain.Patch(frame.Origin,6000);var result=new List<string>();var culture=System.Globalization.CultureInfo.InvariantCulture;
        foreach(double offset in new[]{-2000.0,0,1234.0,4000.0})result.Add(string.Join(",",new[]{frame.Origin.X,frame.Origin.Y,frame.Origin.Z,offset,333.0,patch.Sample(offset,333).ElevationM}.Select(v=>v.ToString("R",culture)).ToArray()));return result.ToArray();
    }
    public void Capture(string path)
    {
        var target=new RenderTexture(1280,800,24);var previous=RenderTexture.active;var priorTarget=view.targetTexture;var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);view.targetTexture=target;
        try{DrawScatter();DrawGroundCover();view.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();var pixels=texture.GetPixels32();LastCapturePixels=pixels;var first=pixels[0];if(!pixels.Any(p=>p.r!=first.r||p.g!=first.g||p.b!=first.b))throw new InvalidOperationException("Terrain capture is blank.");if(path!=null)File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{view.targetTexture=priorTarget;RenderTexture.active=previous;target.Release();Destroy(target);Destroy(texture);}
    }
    public void TestMove(double east,double north){motion.X=east;motion.Z=north;}
    public void StorePlayer(PlaySession session){CancelItemCharge();CaptureInventoryPhysics();CaptureWildlife();CapturePlayerBlood();CaptureLivingHumans();session.Humans=ReferenceEquals(session,PlayerRules)?LivingState:LivingState.Copy();session.Wildlife=ReferenceEquals(session,PlayerRules)?Wildlife:Wildlife.Copy();session.Camp=ReferenceEquals(session,PlayerRules)?Camp:Camp.Copy();session.WatercraftId=activeWatercraft?.Id??"";session.HasPlayer=true;session.PlayerPosition=Position;session.Flying=motion.Flying;session.Heading=heading;session.Pitch=pitch;session.HeightAboveGround=Spectating?HeightAboveGround:Math.Max(0,HeightAboveGround);session.Mode=Mode;session.Survival=Vitals.Copy();session.Inventory=ReferenceEquals(session,PlayerRules)?Inventory:Inventory.Copy();session.CameraDistance=humanCamera.Distance;session.CameraDistanceLocked=humanCamera.Locked;if(humanPlayer!=null)session.PlayerHumanVariant=humanPlayer.Definition.Variant[0]-'A';}
    public void Resume(){paused=false;CaptureMouse();}
    public void TestWalking(){motion.Place(motion.X,motion.Z,Ground(motion.X,motion.Z));pitch=0;}
    public void TestNearbyFlatGround()
    {
        var patch=terrain.Patch(Position,3000/HorizontalScale);
        for(int ring=0;ring<=8;ring++)for(int z=-ring;z<=ring;z++)for(int x=-ring;x<=ring;x++)
        {
            if(Math.Max(Math.Abs(x),Math.Abs(z))!=ring)continue;double dx=x*150/HorizontalScale,dz=z*150/HorizontalScale;
            var sample=patch.Sample(dx,dz);if(sample.ElevationM<10||sample.Underwater)continue;
            bool clear=true;foreach(var delta in new[]{new[]{30d,0d},new[]{-30d,0d},new[]{0d,30d},new[]{0d,-30d}})
            {var adjacent=patch.Sample(dx+delta[0]/HorizontalScale,dz+delta[1]/HorizontalScale);if(adjacent.Underwater||Math.Abs(adjacent.ElevationM-sample.ElevationM)>4)clear=false;}
            if(!clear||!double.IsNaN(sample.WaterSurfaceM)&&sample.ElevationM-sample.WaterSurfaceM<5)continue;
            double gx=(patch.Sample(dx+1,dz).ElevationM-patch.Sample(dx-1,dz).ElevationM)/HorizontalScale*.5,gz=(patch.Sample(dx,dz+1).ElevationM-patch.Sample(dx,dz-1).ElevationM)/HorizontalScale*.5;
            if(gx*gx+gz*gz>.04)continue;Teleport(patch.Position(dx,dz));TestWalking();return;
        }
        throw new InvalidOperationException("No gentle grounded gait test site near review location.");
    }
    public void TestNearbyShore()
    {
        // Review helper only: find a resolved shore near a nominated real-world coastal region.
        var patch=terrain.Patch(Position,120000);double previous=patch.Sample(-100000,0).ElevationM;
        for(double x=-99000;x<=100000;x+=1000)
        {
            double h=patch.Sample(x,0).ElevationM;
            if(previous<0&&h>=0)
            {
                double lo=x-1000,hi=x;for(int i=0;i<28;i++){double mid=(lo+hi)*.5;if(patch.Sample(mid,0).ElevationM<0)lo=mid;else hi=mid;}
                Teleport(patch.Position(hi+80,0));return;
            }
            previous=h;
        }
        throw new InvalidOperationException("No resolved western shore near the nominated coastal review site.");
    }
    public void TestView(double height,float yaw=20,float tilt=12){motion.Flying=true;motion.Y=Ground(motion.X,motion.Z)+height;heading=yaw;pitch=tilt;RefreshWanted();UpdateCamera();}
    public void Close(){if(closed)return;DisposeNpcMinds();audioListener?.Dispose();RestoreTerrainDiagnostics();closed=true;if(view!=null)view.enabled=false;detailCancellation?.Cancel();cancellation.Cancel();if(!passiveSkyCheck&&!passiveReanchorCheck){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}close?.Invoke();}
    void OnDestroy(){audioListener?.Dispose();DisposeInventory();RestoreTerrainDiagnostics();closed=true;cancellation.Cancel();CancelGroundPublication();ClearTerrainTransitions();terrainProducts?.Clear();foreach(var work in jobs)work.Task.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion)t.Result.Dispose();},TaskScheduler.Default);DisposeProgressiveMap();DisposeMapInspection();DisposeEcologyDebug();cancellation.Dispose();CancelPreparedFrame();DisposeRetiringTerrain();DisposeFootsteps();DisposeCloudMap();DisposeRiverMap();DisposeChat();DisposeWeather();DisposeSky();DisposeHumans();DisposeAnimalFauna();DisposeScatter();DisposeTerrainScene();foreach(var tile in tiles.Values)DestroyTile(tile);terrainMeshPool.Dispose();if(mapTexture!=null)Destroy(mapTexture);if(landMaterial!=null)Destroy(landMaterial);if(waterMaterial!=null)Destroy(waterMaterial);}
}

/// <summary>Transfer the ears with the active exploration view, including
/// third-person orbit and floating-origin changes. No per-frame listener scan.</summary>
sealed class ExplorationAudioListener : IDisposable
{
    readonly List<AudioListener> previous=new List<AudioListener>();
    public readonly AudioListener Listener;
    bool disposed;
    public ExplorationAudioListener(Camera camera)
    {
        if(camera==null)throw new ArgumentNullException(nameof(camera));
        foreach(var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if(listener.isActiveAndEnabled){previous.Add(listener);listener.enabled=false;}
        Listener=camera.GetComponent<AudioListener>()??camera.gameObject.AddComponent<AudioListener>();
        Listener.enabled=true;
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        if(Listener!=null)Listener.enabled=false;
        foreach(var listener in previous)if(listener!=null)listener.enabled=true;
        previous.Clear();
    }
}
