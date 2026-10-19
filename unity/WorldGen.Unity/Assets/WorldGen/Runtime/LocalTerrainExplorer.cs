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
    sealed class Tile { public int Segments; public double[] Edges; public float[] Heights; public GameObject Root; }
    sealed class Work { public TerrainTileKey Key; public double[] Edges; public int Segments,Generation; public Task<LocalTerrainMesh> Task; public PreparedFrame Prepared; }
    Task<LocalTerrain> building;
    LocalTerrain terrain; LocalTerrainFrame frame; LocalTerrain.LocalTerrainPatch walkingPatch;
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
    readonly bool passiveReanchorCheck=Environment.GetCommandLineArgs().Contains("--worldgen-reanchor-review");
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
    public bool AllReady=>wanted.All(p=>tiles.TryGetValue(p.Key,out var t)&&t.Segments==p.Value&&t.Edges.SequenceEqual(wantedEdges[p.Key]));
    public V Position=>frame.Position(motion.X,motion.Z);
    public double HeightAboveGround=>motion.Y-Ground(motion.X,motion.Z);
    public string Diagnostics=>tiles.Count+" / "+wanted.Count+" tiles · last "+lastBuildMs.ToString("F1")+" ms · max "+largestBuildMs.ToString("F1")+" ms/tile";
    public void Initialize(Task<LocalTerrain> source,V position,Action onClose,World sourceWorld=null,PlaySession session=null,Action pauseMenu=null)
    {
        building=source;startingPoint=session!=null&&session.HasPlayer?session.PlayerPosition:position;close=onClose;this.sourceWorld=sourceWorld;playState=session;drawPause=pauseMenu;explorationSettings=session?.Settings.Copy()??new PlaySettings{HorizontalScale=1};
        mapViewport.Longitude=session?.PrimeMeridianLongitude??0;
        var cameraRoot=new GameObject("Exploration camera",typeof(Camera),typeof(UnityEngine.Rendering.Universal.UniversalAdditionalCameraData));
        cameraRoot.transform.SetParent(transform,false);cameraRoot.layer=30;view=cameraRoot.GetComponent<Camera>();
        view.cullingMask=1<<30;view.nearClipPlane=.1f;view.farClipPlane=300000;view.fieldOfView=68;
        view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.57f,.69f,.76f);
        var shader=Resources.Load<Shader>("WorldGenLocalTerrain");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Local terrain shader is missing or unsupported.");
        landMaterial=new Material(shader);waterMaterial=new Material(landMaterial);waterMaterial.SetFloat("_Water",1);SetFog();CaptureMouse();
    }
    void SetFog(){landMaterial.SetFloat("_FogDistance",(float)(viewDistance*.98));waterMaterial.SetFloat("_FogDistance",(float)(viewDistance*.98));foreach(var material in ownedScatterMaterials)if(material.HasProperty("_FogDistance"))material.SetFloat("_FogDistance",(float)(viewDistance*.98));}
    double Ground(double x,double z)
    {
        var at=frame.Position(x,z);
        if(walkingPatch==null||(at-walkingPatch.Center).Length*terrain.RadiusM>500/HorizontalScale)walkingPatch=terrain.Patch(at,2048/HorizontalScale);
        return frame.SurfaceY(at,walkingPatch.Sample(at).ElevationM);
    }
    public void Teleport(V point)
    {
        if(terrain==null)return;
        CancelPreparedFrame();
        generation++;startingPoint=point;walkingPatch=null;originX=originZ=0;
        frame=new LocalTerrainFrame(point,terrain.RadiusM,terrain.Patch(point).Sample(0,0).ElevationM,HorizontalScale);
        motion.Place(0,0,Ground(0,0),motion.Flying);ResetFootsteps();landMaterial.SetVector("_LocalOrigin",Vector4.zero);
        foreach(var tile in tiles.Values)DestroyTile(tile);tiles.Clear();if(terrainRoot!=null)Destroy(terrainRoot);terrainRoot=NewTerrainRoot(true);
        plannedX=double.NaN;RefreshWanted();UpdateCamera();ResetScatterFrame();ResetWeatherFrame();
        if(SkyFrame!=null)SetDaylight(LocalSolarHour,skyYear); // No blank-sky frame during automatic tangent-chart reanchoring.
    }
    void Update()
    {
        if(closed)return;
        bool chatInput=HandleChatKeys();if(!chatInput)HandleDebugKeys();if(closed)return;
        if(terrain==null)
        {
            if(!building.IsCompleted)return;
            if(building.IsFaulted||building.IsCanceled){error=building.Exception?.GetBaseException().Message??"Cancelled";debugOpen=true;CaptureMouse();return;}
            terrain=building.Result;viewDistance=Math.Min(viewDistance,MaximumViewDistance);InitializeScatter();Teleport(startingPoint);
            if(playState!=null&&playState.HasPlayer){heading=(float)playState.Heading;pitch=(float)playState.Pitch;motion.Flying=playState.Flying;motion.Y=Ground(0,0)+Math.Max(TerrainMotion.EyeHeightM,playState.HeightAboveGround);UpdateCamera();}
        }
        UpdateMapWork();
        var keyboard=ActiveKeyboard;var mouse=Mouse.current;
        if(!mapOpen&&!paused&&!chatInput)
        {
            if(mouse!=null&&!passiveSkyCheck&&!passiveReanchorCheck){var d=mouse.delta.ReadValue();heading+=d.x*.1f;pitch=Mathf.Clamp(pitch-d.y*.1f,-85,85);
                if(motion.Flying){float scroll=mouse.scroll.ReadValue().y;if(scroll!=0)motion.FlySpeed=Math.Max(5,Math.Min(20000,motion.FlySpeed*Math.Exp(Math.Sign(scroll)*.25)));}}
            if(keyboard!=null)
            {
                MovementSeconds+=Math.Max(0,Math.Min(.25,Time.unscaledDeltaTime));
                if(keyboard.fKey.wasPressedThisFrame){motion.Flying=!motion.Flying;motion.VerticalSpeed=0;}
                motion.Move(Time.unscaledDeltaTime,(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0),
                    (keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.spaceKey.isPressed?1:0)-(keyboard.ctrlKey.isPressed?1:0),
                    heading,pitch,keyboard.shiftKey.isPressed,keyboard.spaceKey.wasPressedThisFrame,Ground,walk:keyboard.cKey.isPressed);
            }
        }
        if(Math.Abs(motion.X-originX)>8192||Math.Abs(motion.Z-originZ)>8192)
        {
            originX=Math.Floor(motion.X/2048)*2048;originZ=Math.Floor(motion.Z/2048)*2048;
            landMaterial.SetVector("_LocalOrigin",new Vector4((float)originX,(float)originZ,0,0));
            foreach(var entry in tiles)entry.Value.Root.transform.localPosition=new Vector3((float)(entry.Key.X*entry.Key.SizeM-originX),0,(float)(entry.Key.Z*entry.Key.SizeM-originZ));
            foreach(var chunk in scatterChunks.Values)ProjectScatter(chunk);scatterDirty=true;
        }
        UpdatePreparedFrame();
        UpdateCamera();UpdateFootsteps();double altitude=HeightAboveGround;
        if(double.IsNaN(plannedX)||Math.Abs(motion.X-plannedX)>1024||Math.Abs(motion.Z-plannedZ)>1024||Math.Abs(altitude-plannedAltitude)>Math.Max(150,plannedAltitude*.3))RefreshWanted();
        for(int i=jobs.Count-1;i>=0;i--)
        {
            var job=jobs[i];if(!job.Task.IsCompleted)continue;jobs.RemoveAt(i);
            if(job.Task.IsFaulted){error=job.Task.Exception.GetBaseException().Message;debugOpen=true;CaptureMouse();continue;}
            if(job.Prepared!=null)
            {
                if(!job.Task.IsCanceled&&job.Generation==generation&&ReferenceEquals(job.Prepared,preparedFrame))
                    Upload(job.Key,job.Task.Result,job.Edges,job.Prepared);
                break;
            }
            if(job.Task.IsCanceled||job.Generation!=generation||!wanted.TryGetValue(job.Key,out int detail)||detail!=job.Segments||!job.Edges.SequenceEqual(wantedEdges[job.Key]))continue;
            Upload(job.Key,job.Task.Result,job.Edges);break;
        }
        // Old LOD coverage remains visible until every replacement leaf is resident; no clearing the horizon on each replan.
        foreach(var key in tiles.Keys.Where(k=>!wanted.ContainsKey(k)).ToArray())
            if(wanted.Where(w=>Overlaps(key,w.Key)).All(w=>tiles.TryGetValue(w.Key,out var replacement)&&replacement.Segments==w.Value))
            {DestroyTile(tiles[key]);tiles.Remove(key);}
        UpdateScatter();UpdateWeatherPresentation();
        SchedulePreparedWork(true);
        // Once a handoff is due, spend terrain work on its replacement rather
        // than repeatedly refining the chart that is about to be retired.
        if(preparedFrame!=null&&ReanchorDistance>=ReanchorLimit)
        {SchedulePreparedWork(false);RetireTerrainGradually();return;}
        foreach(var key in priority)
        {
            if(jobs.Count+scatterJobs.Count>=2)break;int detail=wanted[key];
            if(tiles.TryGetValue(key,out var tile)&&tile.Segments==detail&&tile.Edges.SequenceEqual(wantedEdges[key])||jobs.Any(j=>j.Prepared==null&&j.Generation==generation&&j.Key.Equals(key)))continue;
            var buildFrame=frame;var buildTerrain=terrain;var token=cancellation.Token;int stamp=generation;var edges=wantedEdges[key];
            jobs.Add(new Work{Key=key,Edges=edges,Segments=detail,Generation=stamp,Task=Task.Run(()=>LocalTerrainMesh.Build(buildTerrain,buildFrame,key.X*key.SizeM,key.Z*key.SizeM,key.SizeM,detail,token,edges),token)});
        }
        SchedulePreparedWork(false);
        RetireTerrainGradually();
    }
    void UpdateCamera(){view.transform.localPosition=new Vector3((float)(motion.X-originX),(float)motion.Y,(float)(motion.Z-originZ));view.transform.localRotation=Quaternion.Euler(pitch,heading,0);}
    void RefreshWanted()
    {
        plannedX=motion.X;plannedZ=motion.Z;plannedAltitude=HeightAboveGround;
        wanted=TerrainLod.Plan(motion.X,motion.Z,plannedAltitude,viewDistance,HorizontalScale);priority.Clear();priority.AddRange(wanted.Keys);
        wantedEdges.Clear();foreach(var key in priority)wantedEdges[key]=TerrainLod.EdgeSteps(key,wanted);
        foreach(var key in tiles.Keys.Where(k=>!wanted.ContainsKey(k)&&!wanted.Keys.Any(w=>Overlaps(k,w))).ToArray()){DestroyTile(tiles[key]);tiles.Remove(key);}
        double Distance(TerrainTileKey k){double dx=k.X*k.SizeM+k.SizeM*.5-motion.X,dz=k.Z*k.SizeM+k.SizeM*.5-motion.Z;return dx*dx+dz*dz;}
        priority.Sort((a,b)=>{int d=Distance(a).CompareTo(Distance(b));return d!=0?d:a.Level!=b.Level?a.Level.CompareTo(b.Level):a.X!=b.X?a.X.CompareTo(b.X):a.Z.CompareTo(b.Z);});
    }
    static bool Overlaps(TerrainTileKey a,TerrainTileKey b)=>Math.Max(a.X*a.SizeM,b.X*b.SizeM)<Math.Min((a.X+1)*a.SizeM,(b.X+1)*b.SizeM)&&Math.Max(a.Z*a.SizeM,b.Z*b.SizeM)<Math.Min((a.Z+1)*a.SizeM,(b.Z+1)*b.SizeM);
    void Upload(TerrainTileKey key,LocalTerrainMesh data,double[] edges,PreparedFrame prepared=null)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();var root=new GameObject("Terrain "+key.X+","+key.Z+" L"+key.Level);root.transform.SetParent(prepared==null?terrainRoot.transform:prepared.Root.transform,false);
        root.transform.localPosition=new Vector3((float)(data.EastM-(prepared==null?originX:0)),0,(float)(data.NorthM-(prepared==null?originZ:0)));
        Mesh Make(float[] positions,float[] colors,bool water)
        {
            var vertices=new Vector3[positions.Length/3];var normals=new Vector3[vertices.Length];var rgb=new Color[vertices.Length];var uv=new Vector2[vertices.Length];var weights=new List<Vector4>(vertices.Length);var groundB=new List<Vector4>(vertices.Length);
            for(int i=0;i<vertices.Length;i++){vertices[i]=new Vector3(positions[i*3],positions[i*3+1],positions[i*3+2]);normals[i]=water?Vector3.up:new Vector3(data.Normals[i*3],data.Normals[i*3+1],data.Normals[i*3+2]);rgb[i]=new Color(colors[i*4],colors[i*4+1],colors[i*4+2],colors[i*4+3]);uv[i]=new Vector2((float)(data.EastM+positions[i*3])*.5f,(float)(data.NorthM+positions[i*3+2])*.5f);weights.Add(water?Vector4.zero:new Vector4(data.SurfaceWeights[i*4],data.SurfaceWeights[i*4+1],data.SurfaceWeights[i*4+2],data.SurfaceWeights[i*4+3]));}
            for(int i=0;i<vertices.Length;i++)groundB.Add(water?Vector4.zero:new Vector4(data.GroundWeightsB[i*4],data.GroundWeightsB[i*4+1],data.GroundWeightsB[i*4+2],data.GroundWeightsB[i*4+3]));
            var rocks=new List<Vector4>(vertices.Length);var rocksB=new List<Vector2>(vertices.Length);
            for(int i=0;i<vertices.Length;i++)
            {
                rocks.Add(water?Vector4.zero:new Vector4(data.Lithology[i*6],data.Lithology[i*6+1],data.Lithology[i*6+2],data.Lithology[i*6+3]));
                rocksB.Add(water?Vector2.zero:new Vector2(data.Lithology[i*6+4],data.Lithology[i*6+5]));
            }
            var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=vertices,normals=normals,colors=rgb,uv=uv};mesh.SetUVs(1,weights);mesh.SetUVs(2,groundB);mesh.SetUVs(4,rocks);mesh.SetUVs(5,rocksB);mesh.triangles=water?data.Triangles.Take(data.Segments*data.Segments*6).ToArray():data.Triangles;mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        void Part(string name,Mesh mesh,Material material){var part=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));part.layer=30;part.transform.SetParent(root.transform,false);part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<MeshRenderer>().sharedMaterial=material;}
        Part("Land and seabed",Make(data.Positions,data.Colors,false),landMaterial);
        if(data.WaterColors.Where((_,i)=>i%4==3).Any(a=>a>.01))Part("Ocean surface",Make(data.WaterPositions,data.WaterColors,true),waterMaterial);
        var heights=new float[data.GroundVertices];for(int i=0;i<heights.Length;i++)heights[i]=data.Positions[i*3+1];
        var target=prepared==null?tiles:prepared.Tiles;
        if(target.TryGetValue(key,out var old))DestroyTile(old);target[key]=new Tile{Root=root,Segments=data.Segments,Edges=edges,Heights=heights};
        if(prepared==null&&key.Level==0)nearGroundRevision++;lastBuildMs=data.BuildMilliseconds;largestBuildMs=Math.Max(largestBuildMs,lastBuildMs);
        LargestUploadMs=Math.Max(LargestUploadMs,timer.Elapsed.TotalMilliseconds);
        if(timer.ElapsedMilliseconds>20)Debug.Log("Local terrain upload "+timer.ElapsedMilliseconds+"ms");
    }
    void DestroyTile(Tile tile){if(tile.Root==null)return;tile.Root.SetActive(false);foreach(var filter in tile.Root.GetComponentsInChildren<MeshFilter>())Destroy(filter.sharedMesh);Destroy(tile.Root);}
    public string[] Probes()
    {
        var patch=terrain.Patch(frame.Origin,6000);var result=new List<string>();var culture=System.Globalization.CultureInfo.InvariantCulture;
        foreach(double offset in new[]{-2000.0,0,1234.0,4000.0})result.Add(string.Join(",",new[]{frame.Origin.X,frame.Origin.Y,frame.Origin.Z,offset,333.0,patch.Sample(offset,333).ElevationM}.Select(v=>v.ToString("R",culture)).ToArray()));return result.ToArray();
    }
    public void Capture(string path)
    {
        var target=new RenderTexture(1280,800,24);var previous=RenderTexture.active;var priorTarget=view.targetTexture;var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);view.targetTexture=target;
        try{DrawScatter();view.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();var pixels=texture.GetPixels32();LastCapturePixels=pixels;var first=pixels[0];if(!pixels.Any(p=>p.r!=first.r||p.g!=first.g||p.b!=first.b))throw new InvalidOperationException("Terrain capture is blank.");if(path!=null)File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{view.targetTexture=priorTarget;RenderTexture.active=previous;target.Release();Destroy(target);Destroy(texture);}
    }
    public void TestMove(double east,double north){motion.X=east;motion.Z=north;}
    public void StorePlayer(PlaySession session){session.HasPlayer=true;session.PlayerPosition=Position;session.Flying=motion.Flying;session.Heading=heading;session.Pitch=pitch;session.HeightAboveGround=Math.Max(0,HeightAboveGround);}
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
    public void Close(){if(closed)return;closed=true;if(view!=null)view.enabled=false;cancellation.Cancel();if(!passiveSkyCheck&&!passiveReanchorCheck){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}close?.Invoke();}
    void OnDestroy(){cancellation.Cancel();mapCancellation?.Cancel();cancellation.Dispose();mapCancellation?.Dispose();CancelPreparedFrame();DisposeRetiringTerrain();DisposeFootsteps();DisposeCloudMap();DisposeChat();DisposeWeather();DisposeSky();DisposeScatter();foreach(var tile in tiles.Values)DestroyTile(tile);if(mapTexture!=null)Destroy(mapTexture);if(landMaterial!=null)Destroy(landMaterial);if(waterMaterial!=null)Destroy(waterMaterial);}
}
