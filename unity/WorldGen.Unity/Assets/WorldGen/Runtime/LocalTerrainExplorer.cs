using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Core;
using WorldGen.Presentation;

/// <summary>Input and bounded rendering only; all height queries use the shared terrain sampler.</summary>
public sealed partial class LocalTerrainExplorer : MonoBehaviour
{
    sealed class Tile { public int Segments; public double[] Edges; public float[] Heights,Cover,MidCover,ForestCover;public sbyte MovingCover=-1; public float[] OcclusionMin;public float OcclusionCeiling;public int OcclusionStride; public GameObject Root; public Texture2D Coverage,CoverageTimes,FloraSurface,MidSurface,ForestSurface; public float[] CoverageNodes,CoverageTimingNodes; public float FadeIn,FadeOut; public long BufferBytes,DiagnosticVertices,DiagnosticTriangles; public int DiagnosticRevision=-1; public Material[] Materials;public MeshRenderer[] Renderers;public TerrainMaterialBinding MaterialBinding;public CoverGpu Grass,GrassSprites; public GraphicsBuffer CoverGrid; public readonly Dictionary<CoverPageKey,CoverGpu> CoverPages=new Dictionary<CoverPageKey,CoverGpu>(); }
    sealed class Work { public TerrainTileKey Key; public double[] Edges; public int Segments,Generation; public bool Prefetch,Backdrop; public Task<TerrainUpload> Task; public PreparedFrame Prepared; public CancellationTokenSource Cancellation; }
    Task<LocalTerrain> building;
    LocalTerrain terrain; LocalTerrainFrame frame; LocalTerrain.LocalTerrainPatch walkingPatch;
    TerrainMeshProcess meshProcess;
    readonly TerrainMotion motion=new TerrainMotion();
    Dictionary<TerrainTileKey,Tile> tiles=new Dictionary<TerrainTileKey,Tile>();
    GameObject terrainRoot;
    Dictionary<TerrainTileKey,int> wanted=new Dictionary<TerrainTileKey,int>();
    readonly Dictionary<TerrainTileKey,double[]> wantedEdges=new Dictionary<TerrainTileKey,double[]>();
    readonly List<TerrainTileKey> priority=new List<TerrainTileKey>(); readonly List<Work> jobs=new List<Work>();
    readonly CancellationTokenSource cancellation=new CancellationTokenSource();
    Action close; Camera view; Material landMaterial,waterMaterial;
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
    public bool CenterReady=>tiles.Keys.Any(k=>motion.X>=k.X*k.SizeM&&motion.X<(k.X+1)*k.SizeM&&motion.Z>=k.Z*k.SizeM&&motion.Z<(k.Z+1)*k.SizeM);
    public bool NearReady=>wanted.Count>0&&!NearTerrainPending();
    public bool AllReady=>wanted.All(p=>tiles.TryGetValue(p.Key,out var t)&&t.Segments==p.Value&&TerrainEdges.Equal(t.Edges,wantedEdges[p.Key]));
    public V Position=>frame.Position(motion.X,motion.Z);
    public double HeightAboveGround=>motion.Y-Ground(motion.X,motion.Z);
    public string Diagnostics=>tiles.Count+" / "+wanted.Count+" tiles · last "+lastBuildMs.ToString("F1")+" ms · max "+largestBuildMs.ToString("F1")+" ms/tile";
    public void Initialize(Task<LocalTerrain> source,V position,Action onClose,World sourceWorld=null,PlaySession session=null,Action pauseMenu=null,TerrainMeshProcess worker=null)
    {
        building=source;meshProcess=worker;startingPoint=session!=null&&session.HasPlayer?session.PlayerPosition:position;close=onClose;this.sourceWorld=sourceWorld;playState=session;drawPause=pauseMenu;explorationSettings=session?.Settings.Copy()??new PlaySettings{HorizontalScale=1};
        mapViewport.Longitude=session?.PrimeMeridianLongitude??0;
        var cameraRoot=new GameObject("Exploration camera",typeof(Camera),typeof(UnityEngine.Rendering.Universal.UniversalAdditionalCameraData));
        cameraRoot.transform.SetParent(transform,false);cameraRoot.layer=30;view=cameraRoot.GetComponent<Camera>();
        view.cullingMask=1<<30;view.nearClipPlane=.1f;view.farClipPlane=300000;view.fieldOfView=68;
        ConfigureExplorationRenderer(false);
        view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.57f,.69f,.76f);
        var shader=Resources.Load<Shader>("WorldGenLocalTerrain");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Local terrain shader is missing or unsupported.");
        landMaterial=new Material(shader);landMaterial.SetFloat("_GroundFlora",1);waterMaterial=new Material(landMaterial);waterMaterial.SetFloat("_Water",1);SetFog();CaptureMouse();
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
        float distance=(float)Math.Min(viewDistance*.98,streamedFog);
        // A settled reveal used to resend the identical uniform to every cached
        // species material every frame. New art is initialized to this datum too.
        if(!WorldGenPerformance.Original&&distance==publishedFog&&publishedFogMaterials==ownedScatterMaterials.Count&&publishedFogLand==landMaterial&&publishedFogWater==waterMaterial)return;
        publishedFog=distance;publishedFogMaterials=ownedScatterMaterials.Count;publishedFogLand=landMaterial;publishedFogWater=waterMaterial;
        landMaterial.SetFloat("_FogDistance",distance);waterMaterial.SetFloat("_FogDistance",distance);foreach(var material in legacyVegetationScene)if(material!=null&&material.HasProperty("_FogDistance"))material.SetFloat("_FogDistance",distance);
        PublishTerrainScene();
    }
    LocalTerrainFrame groundMemoFrame;double groundMemoX,groundMemoZ,groundMemoY,groundMemoElevation;
    double Ground(double x,double z)
    {
        if(!WorldGenPerformance.Original&&ReferenceEquals(frame,groundMemoFrame)&&x==groundMemoX&&z==groundMemoZ&&walkingPatch!=null)return groundMemoY;
        long started=System.Diagnostics.Stopwatch.GetTimestamp();
        var at=frame.Position(x,z);
        if(walkingPatch==null||(at-walkingPatch.Center).Length*terrain.RadiusM>500/HorizontalScale)walkingPatch=terrain.Patch(at,2048/HorizontalScale);
        double elevation=WorldGenPerformance.Original?walkingPatch.Sample(at).ElevationM:walkingPatch.Elevation(at);double height=frame.SurfaceY(at,elevation);
        groundMemoElevation=elevation;
        double elapsed=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        largestGroundMs=Math.Max(largestGroundMs,elapsed);
        if(WorldGenPerformance.TraceEnabled)WorldGenPerformance.TraceMilliseconds[4]+=elapsed;
        if(elapsed>20)Debug.Log("Local ground query "+elapsed.ToString("F2")+"ms");
        groundMemoFrame=frame;groundMemoX=x;groundMemoZ=z;groundMemoY=height;
        return height;
    }
    public void Teleport(V point)
    {
        if(terrain==null)return;
        CancelPreparedFrame();
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
        bool survivalInput=!WorldGenPerformance.Enabled&&HandleSurvivalKeys();
        bool chatInput=!survivalInput&&!WorldGenPerformance.Enabled&&HandleChatKeys();if(!chatInput&&!survivalInput&&!WorldGenPerformance.Enabled)HandleDebugKeys();if(closed)return;
        if(terrain==null)
        {
            if(!building.IsCompleted)return;
            if(building.IsFaulted||building.IsCanceled){error=building.Exception?.GetBaseException().Message??"Cancelled";debugOpen=true;CaptureMouse();return;}
            terrain=building.Result;viewDistance=Math.Min(viewDistance,MaximumViewDistance);InitializeScatter();Teleport(startingPoint);InitializeAnimalFauna();InitializeHumans();InitializeSurvival();
            if(playState!=null&&playState.HasPlayer){heading=(float)playState.Heading;pitch=(float)playState.Pitch;motion.Flying=playState.Flying;motion.Y=Ground(0,0)+Math.Max(TerrainMotion.EyeHeightM,playState.HeightAboveGround);UpdateCamera();}
        }
        ApplyTerrainDiagnostics();ApplyFoliageShadowOptions();installationsThisFrame=0;frameUploadMs=0;double movementStartX=motion.X,movementStartZ=motion.Z;
        long mapStarted=WorldGenPerformance.TraceBegin();UpdateMapWork();WorldGenPerformance.TraceEnd(1,mapStarted);
        var keyboard=ActiveKeyboard;var mouse=Mouse.current;
        if(!mapOpen&&!paused&&!chatInput&&!ecologyOpen&&!SurvivalBlocked&&!SurvivalBusy&&Application.isFocused)
        {
            if(mouse!=null&&!passiveSkyCheck&&!passiveReanchorCheck){var d=mouse.delta.ReadValue();heading+=d.x*.1f;pitch=Mathf.Clamp(pitch-d.y*.1f,-85,85);
                ReadHumanCameraInput(mouse,keyboard);}
            if(keyboard!=null&&!WorldGenPerformance.Enabled)
            {
                MovementSeconds+=Math.Max(0,Math.Min(.25,Time.unscaledDeltaTime));
                if(keyboard.fKey.wasPressedThisFrame&&survival?.Mode!=SurvivalMode.Survival){motion.Flying=!motion.Flying;motion.VerticalSpeed=0;}
                motion.Move(Time.unscaledDeltaTime,(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0),
                    (keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.spaceKey.isPressed?1:0)-(keyboard.ctrlKey.isPressed?1:0),
                    heading,pitch,keyboard.shiftKey.isPressed&&(survival==null||survival.Stamina>5),keyboard.spaceKey.wasPressedThisFrame&&(survival==null||survival.Stamina>5),Ground,walk:keyboard.cKey.isPressed||keyboard.ctrlKey.isPressed);
            }
        }
        TrackStreamingMotion(motion.X-movementStartX,motion.Z-movementStartZ,Time.unscaledDeltaTime);
        if(Math.Abs(motion.X-originX)>8192||Math.Abs(motion.Z-originZ)>8192)
        {
            originX=Math.Floor(motion.X/2048)*2048;originZ=Math.Floor(motion.Z/2048)*2048;
            landMaterial.SetVector("_LocalOrigin",new Vector4((float)originX,(float)originZ,0,0));
            PublishTerrainScene();
            foreach(var entry in tiles)entry.Value.Root.transform.localPosition=new Vector3((float)(entry.Key.X*entry.Key.SizeM-originX),0,(float)(entry.Key.Z*entry.Key.SizeM-originZ));
            foreach(var entry in previousTerrain)entry.Value.Root.transform.localPosition=new Vector3((float)(entry.Key.X*entry.Key.SizeM-originX),0,(float)(entry.Key.Z*entry.Key.SizeM-originZ));
            foreach(var chunk in scatterChunks.Values)ProjectScatter(chunk);scatterDirty=true;
        }
        if(!TerrainDiagnostics.HoldTerrain)UpdatePreparedFrame();
        UpdateSurvival();UpdateHumans();UpdateCamera();UpdateFootsteps();double altitude=HeightAboveGround;
        debugGroundClearance=altitude;debugElevation=groundMemoElevation+altitude;
        if(TerrainDiagnostics.HoldTerrain){UpdateScatter();UpdateAnimalFauna();UpdateWeatherPresentation();UpdateDebugReadout();return;}
        double replanDistance=TerrainLod.ReplanDistance(HorizontalScale);
        if(double.IsNaN(plannedX)||Math.Abs(motion.X-plannedX)>replanDistance||Math.Abs(motion.Z-plannedZ)>replanDistance||Math.Abs(altitude-plannedAltitude)>Math.Max(150,plannedAltitude*.3))RefreshWanted();
        if(!mapOpen){PollTerrainDetail();RefreshStreamingPrediction();}
        // Completed nearby products precede cache-only/hidden background work.
        jobs.Sort((a,b)=>{double Score(Work w)=>WorkMatchesCurrent(w)?StreamingPriority(w.Key):double.MaxValue;return Score(b).CompareTo(Score(a));});
        for(int i=jobs.Count-1;i>=0;i--)
        {
            if(!CanInstall)break;
            var job=jobs[i];if(!job.Task.IsCompleted)continue;jobs.RemoveAt(i);job.Cancellation?.Dispose();
            if(job.Task.IsFaulted){error=job.Task.Exception.GetBaseException().Message;debugOpen=true;CaptureMouse();continue;}
            if(job.Task.IsCanceled)continue;
            using var upload=job.Task.Result;
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
            if(!WorkMatchesCurrent(job))continue;
            Upload(job.Key,upload,job.Edges);
        }
        UpdateTerrainTransitions();
        // Retain fallback through the transition, not merely until upload.
        foreach(var key in tiles.Keys.Where(k=>!wanted.ContainsKey(k)).ToArray())
            if(CanRetireTransition(key))
            {CacheTerrain(key,tiles[key]);tiles.Remove(key);residentRevision++;scatterGroundRevision++;scatterGroundChanges.Add(key);}
        long scatterTrace=WorldGenPerformance.TraceBegin(),scatterStarted=WorldGenPerformance.Begin();UpdateScatter();WorldGenPerformance.End(0,scatterStarted);WorldGenPerformance.TraceEnd(2,scatterTrace);
        UpdateAnimalFauna();
        long weatherTrace=WorldGenPerformance.TraceBegin(),weatherStarted=WorldGenPerformance.Begin();UpdateWeatherPresentation();WorldGenPerformance.End(3,weatherStarted);WorldGenPerformance.TraceEnd(3,weatherTrace);
        UpdateDebugReadout();
        if(mapOpen){UpdateTerrainCoverage();UpdateStreamingFog();RetireTerrainGradually();return;}
        // Nearby ground always goes first, including when a hidden replacement
        // chart is overdue. Do not let scatter or far work monopolize both slots.
        bool nearPending=NearTerrainPending();
        // Free ONE occupied lane only if neither lane is serving near ground.
        // Continually cancelling all background work would itself cause churn.
        if(nearPending&&ActiveWorkerCount>=WorkerLimit&&!jobs.Any(j=>WorkMatchesCurrent(j)&&ImmediateTerrain(j.Key)))
        {
            var forest=forestJobs.LastOrDefault(j=>!j.Task.IsCompleted&&!j.Cancellation.IsCancellationRequested);
            if(forest!=null)forest.Cancellation.Cancel();
            else jobs.LastOrDefault(j=>j.Prepared!=null||!ImmediateTerrain(j.Key))?.Cancellation?.Cancel();
        }
        bool handoffDue=preparedFrame!=null&&ReanchorDistance>=ReanchorLimit;
        // Share the outward loading front instead of finishing a bare horizon
        // before the forest gets any worker time. Alternate with terrain on a
        // single-worker machine; never reserve this turn ahead of near ground
        // or an overdue chart handoff.
        long priorTerrainRequests=TerrainBuildRequests;
        if(!nearPending&&!handoffDue&&forestRevealRadius<terrainRevealRadius&&(!forestScheduledFirst||WorkerLimit>1))
            if(ScheduleForestWork())forestScheduledFirst=true;
        if(!handoffDue){ScheduleBackdrop();SchedulePredictiveTerrain();}
        foreach(var key in priority)
        {
            int detail=wanted[key];
            if(handoffDue&&!ImmediateTerrain(key))continue;
            if(tiles.TryGetValue(key,out var tile)&&tile.Segments==detail&&TerrainEdges.Equal(tile.Edges,wantedEdges[key]))continue;
            if(TerrainProducts.Contains(new TerrainProductKey(key,detail,wantedEdges[key]))){RestoreTerrain(key,detail,wantedEdges[key]);continue;}
            if(!CanRequest||ActiveWorkerCount>=WorkerLimit||HasMatchingTerrainJob(key))continue;
            RequestTerrain(key,detail,wantedEdges[key]);
        }
        SchedulePreparedWork(false);
        if(TerrainBuildRequests>priorTerrainRequests)forestScheduledFirst=false;
        ScheduleForestWork();
        UpdateTerrainCoverage();UpdateStreamingFog();
        RetireTerrainGradually();
        if(WorldGenPerformance.Enabled&&!WorldGenPerformance.Measuring)PerformanceVerifyPending();
    }
    bool HasMatchingTerrainJob(TerrainTileKey key)
    {foreach(var job in jobs)if(WorkMatchesCurrent(job)&&job.Key.Equals(key))return true;return false;}
    void UpdateCamera(){if(humanPlayer!=null){UpdateHumanCamera();return;}view.transform.localPosition=new Vector3((float)(motion.X-originX),(float)motion.Y,(float)(motion.Z-originZ));view.transform.localRotation=Quaternion.Euler(pitch,heading,0);}
    int residentRevision,nearMemoFrame=-1,nearMemoRevision,nearMemoCount;double nearMemoX,nearMemoZ;bool nearMemoPending;
    Dictionary<TerrainTileKey,int> nearMemoWanted;
    bool NearTerrainPendingFresh()=>wanted.Any(p=>ImmediateTerrain(p.Key)&&
        (!tiles.TryGetValue(p.Key,out var tile)||tile.Segments!=p.Value||!TerrainEdges.Equal(tile.Edges,wantedEdges[p.Key])));
    bool NearTerrainPending()
    {
        if(WorldGenPerformance.Original)return NearTerrainPendingFresh();
        // This predicate is asked by several schedulers per frame. Uploads,
        // restores, plan/chart replacement and movement invalidate immediately.
        if(nearMemoFrame==Time.frameCount&&nearMemoRevision==residentRevision&&nearMemoCount==tiles.Count&&ReferenceEquals(nearMemoWanted,wanted)&&nearMemoX==motion.X&&nearMemoZ==motion.Z)return nearMemoPending;
        nearMemoFrame=Time.frameCount;nearMemoRevision=residentRevision;nearMemoCount=tiles.Count;nearMemoWanted=wanted;nearMemoX=motion.X;nearMemoZ=motion.Z;
        return nearMemoPending=NearTerrainPendingFresh();
    }
    void RefreshWanted()
    {
        plannedX=motion.X;plannedZ=motion.Z;plannedAltitude=HeightAboveGround;
        detailCancellation?.Cancel();detailDirty=true;
        wanted=InitialDetailPlan(motion.X,motion.Z,plannedAltitude);priority.Clear();priority.AddRange(wanted.Keys);PlanBackdrop();
        wantedEdges.Clear();foreach(var pair in TerrainEdges.Build(wanted))wantedEdges.Add(pair.Key,pair.Value);
        foreach(var key in tiles.Keys.Where(k=>!wanted.ContainsKey(k)&&!wanted.Keys.Any(w=>Overlaps(k,w))).ToArray()){CacheTerrain(key,tiles[key]);tiles.Remove(key);}
        foreach(var job in jobs)
            if(job.Prepared==null&&!job.Prefetch&&!job.Backdrop&&(!wanted.TryGetValue(job.Key,out int detail)||detail!=job.Segments||!TerrainEdges.Equal(job.Edges,wantedEdges[job.Key])))job.Cancellation?.Cancel();
        double Distance(TerrainTileKey k)=>TerrainLod.DistanceSquared(k,motion.X,motion.Z);
        priority.Sort((a,b)=>{int d=Distance(a).CompareTo(Distance(b));return d!=0?d:a.Level!=b.Level?a.Level.CompareTo(b.Level):a.X!=b.X?a.X.CompareTo(b.X):a.Z.CompareTo(b.Z);});
    }
    static bool Overlaps(TerrainTileKey a,TerrainTileKey b)=>Math.Max(a.X*a.SizeM,b.X*b.SizeM)<Math.Min((a.X+1)*a.SizeM,(b.X+1)*b.SizeM)&&Math.Max(a.Z*a.SizeM,b.Z*b.SizeM)<Math.Min((a.Z+1)*a.SizeM,(b.Z+1)*b.SizeM);
    void Upload(TerrainTileKey key,TerrainUpload data,double[] edges,PreparedFrame prepared=null,bool cacheOnly=false)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();var root=new GameObject("Terrain "+key.X+","+key.Z+" L"+key.Level);root.transform.SetParent(prepared==null?terrainRoot.transform:prepared.Root.transform,false);
        if(cacheOnly)root.SetActive(false);installationsThisFrame++;
        root.transform.localPosition=new Vector3((float)(data.EastM-(prepared==null?originX:0)),0,(float)(data.NorthM-(prepared==null?originZ:0)));
        int collections=GC.CollectionCount(0);double beforeApply=timer.Elapsed.TotalMilliseconds;
        var meshes=data.Apply();double applyMs=timer.Elapsed.TotalMilliseconds-beforeApply;
        void Part(string name,Mesh mesh,Material material){var part=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));part.layer=30;part.transform.SetParent(root.transform,false);part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<MeshRenderer>().sharedMaterial=material;}
        Part("Land and seabed",meshes[0],landMaterial);
        if(data.HasWater)Part("Water surface",meshes[1],waterMaterial);
        var target=prepared==null?tiles:prepared.Tiles;
        if(prepared!=null)prepared.Revision++;
        var product=new Tile{Root=root,Segments=data.Segments,Edges=edges,Heights=data.Heights,Cover=data.Cover,MidCover=data.MidCover,ForestCover=data.ForestCover,BufferBytes=data.BufferBytes};
        foreach(var mesh in meshes){product.DiagnosticVertices+=mesh.vertexCount;product.DiagnosticTriangles+=(long)mesh.GetIndexCount(0)/3;}
        product.FloraSurface=CreateFloraSurface(product);if(product.FloraSurface!=null)product.BufferBytes+=(long)(product.Segments+1)*(product.Segments+1)*8;
        product.MidSurface=CreateMidSurface(product);if(product.MidSurface!=null)product.BufferBytes+=(long)(product.Segments+1)*(product.Segments+1)*8;
        product.ForestSurface=CreateForestSurface(product);if(product.ForestSurface!=null)product.BufferBytes+=(long)(product.Segments+1)*(product.Segments+1)*8;
        BindTerrainTile(product,product,key,Vector4.zero);
        if(cacheOnly){CacheTerrain(key,product);return;}
        if(target.TryGetValue(key,out var old)){if(prepared==null)BeginTerrainReplacement(key,product,old);else DestroyTile(old);}
        else if(prepared==null)BeginTerrainReplacement(key,product,null);
        target[key]=product;if(prepared==null)residentRevision++;
        if(prepared==null)coverageDirty=true;
        if(prepared==null){scatterGroundRevision++;scatterGroundChanges.Add(key);}lastBuildMs=data.BuildMilliseconds;largestBuildMs=Math.Max(largestBuildMs,lastBuildMs);
        LargestUploadMs=Math.Max(LargestUploadMs,timer.Elapsed.TotalMilliseconds);
        frameUploadMs+=timer.Elapsed.TotalMilliseconds;
        if(timer.ElapsedMilliseconds>20)Debug.Log("Local terrain upload "+timer.ElapsedMilliseconds+"ms; apply "+applyMs.ToString("F2")+"ms; objects/retire "+(timer.Elapsed.TotalMilliseconds-beforeApply-applyMs).ToString("F2")+"ms; collections "+(GC.CollectionCount(0)-collections)+"; worker buffers "+data.BufferMilliseconds.ToString("F2")+"ms");
    }
    void DestroyTile(Tile tile){ReleaseGroundCover(tile);if(tile.FloraSurface!=null)Destroy(tile.FloraSurface);if(tile.MidSurface!=null)Destroy(tile.MidSurface);if(tile.ForestSurface!=null)Destroy(tile.ForestSurface);if(tile.Coverage!=null)Destroy(tile.Coverage);if(tile.CoverageTimes!=null)Destroy(tile.CoverageTimes);if(tile.Materials!=null){foreach(var material in tile.Materials)if(material!=null)Destroy(material);tile.Materials=null;tile.Renderers=null;tile.MaterialBinding=null;}if(tile.Root==null)return;tile.Root.SetActive(false);foreach(var filter in tile.Root.GetComponentsInChildren<MeshFilter>())Destroy(filter.sharedMesh);Destroy(tile.Root);}
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
    public void StorePlayer(PlaySession session){session.Survival=survival?.Copy();session.HasPlayer=true;session.PlayerPosition=Position;session.Flying=motion.Flying;session.Heading=heading;session.Pitch=pitch;session.HeightAboveGround=Math.Max(0,HeightAboveGround);session.CameraDistance=humanCamera.Distance;session.CameraDistanceLocked=humanCamera.Locked;session.HumanRunStyle=humanRunStyle;if(humanPlayer!=null)session.PlayerHumanVariant=humanPlayer.Definition.Variant[0]-'A';}
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
    public void Close(){if(closed)return;RestoreTerrainDiagnostics();closed=true;if(view!=null)view.enabled=false;detailCancellation?.Cancel();cancellation.Cancel();if(!passiveSkyCheck&&!passiveReanchorCheck){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}close?.Invoke();}
    void OnDestroy(){RestoreTerrainDiagnostics();closed=true;cancellation.Cancel();ClearTerrainTransitions();terrainProducts?.Clear();foreach(var work in jobs)work.Task.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion)t.Result.Dispose();},TaskScheduler.Default);DisposeProgressiveMap();DisposeMapInspection();DisposeEcologyDebug();cancellation.Dispose();CancelPreparedFrame();DisposeRetiringTerrain();DisposeFootsteps();DisposeCloudMap();DisposeRiverMap();DisposeChat();DisposeWeather();DisposeSky();DisposeSurvival();DisposeHumans();DisposeAnimalFauna();DisposeScatter();DisposeTerrainScene();foreach(var tile in tiles.Values)DestroyTile(tile);if(mapTexture!=null)Destroy(mapTexture);if(landMaterial!=null)Destroy(landMaterial);if(waterMaterial!=null)Destroy(waterMaterial);}
}
