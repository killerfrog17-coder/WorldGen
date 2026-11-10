using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Serialization;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public sealed class NpcPerformanceRow
    {
        public double MeanLateNpcMs;public bool AutomaticPoses,GcIncremental;public ulong GcSliceNanoseconds;public string GcMode;public long HeapStart,HeapEnd;
        public int ExplorationRenders,UnexpectedCameraRenders;
        public int EnabledSkins;public long EnabledSkinVertices;public bool AllocationCounterAvailable;
        public long ClockBefore,ClockNative,ClockAfter,ClockFrequency,ManagedFrequency;
        public string Mode;public int Residents,MinResidents,MaxResidents,Frames,Tiles,ScatterChunks;public bool TerrainReady;
        public double MeanNpcMs,P95NpcMs,P99NpcMs,MaxNpcMs,MeanFrameMs,P95FrameMs,P99FrameMs,MaxFrameMs,BackgroundQueryMs;
        public int UnfocusedFrames,Over10Ms,Over1667Ms,Width,Height,TreeQuality;public double Seconds,ViewDistanceM;public string Route;
        public double[] MeanStagesMs;
        public double[] MeanMainThreadTraceMs;public NpcSlowFrame[] SlowFrames;public int Gen0Collections,Gen1Collections,Gen2Collections;
        public long ScatterAppendAllocatedBytes,ScatterBuilds;public int ArtModelsStart,ArtModelsEnd;
        public bool ResidentForestReadyStart,ResidentForestReadyEnd;public int ForestPagesStart,ForestPagesEnd,ForestRootsStart,ForestRootsEnd;
        public double MaximumCameraErrorM,MaximumCameraAngleError,MaximumScatterEyeDeltaM;
    }
    public sealed class NpcSlowFrame
    {public double AtSeconds,FrameMs,NpcMs;public double[] MainThreadTraceMs;public int Gen0,Gen1,Gen2;public long MainAllocatedBytes,ManagedFrameEnd;public int ScatterObjects,TreeInstances,Batches,ForestPages;public bool ResidentForestReady;}
    public sealed class NpcCrowdProof {public int Residents,Groups,SkinnedRenderers,EnabledSkinnedRenderers,NativePropertyBlockedSkins;public bool SrpBatching;public long Vertices,EnabledVertices;public string Scope;public NpcResidentCheckRow[] People;}
    sealed class NpcRenderProof:IDisposable
    {
        readonly Camera expected;public int ExpectedRenders,UnexpectedRenders;public bool Measuring;public string UnexpectedName="";
        public NpcRenderProof(Camera camera){expected=camera;RenderPipelineManager.beginCameraRendering+=BeforeCamera;}
        public void Start(){ExpectedRenders=UnexpectedRenders=0;UnexpectedName="";Measuring=true;}
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {if(!Measuring)return;if(camera==expected)ExpectedRenders++;else{UnexpectedRenders++;if(UnexpectedName.Length==0)UnexpectedName=camera==null?"null":camera.name;}}
        public void Dispose(){Measuring=false;RenderPipelineManager.beginCameraRendering-=BeforeCamera;}
    }
    Vector3? npcCrowdFocus;
    static void VerifyCampPartSelection(LocalTerrainExplorer owner,string output)
    {
        int checks=0,construction=0,packingChoices=0;
        void Compare(HumanActor actor,bool packing)
        {
            var group=owner.livingGroups[actor.GroupId];CampObject expected=null;CampComponent expectedPart=null;
            // Original stable LINQ ordering, retained only in this native
            // verifier. The production selector must choose these exact objects.
            foreach(var house in owner.Camp.Objects.Where(c=>c.NpcGroup==actor.GroupId&&c.NpcSite==group.Situation.Camp.Id&&c.Blueprint!=null).OrderBy(c=>c.NpcHousehold==actor.Resident.Household?0:1).ThenBy(c=>owner.HumanDistance(c.Position,actor.Planet)))
            {
                var plan=house.Blueprint;var definitions=owner.NpcCampParts(house);int stage=definitions.Where(c=>!CampBlueprints.Done(plan,c)).Select(c=>c.Stage).DefaultIfEmpty(4).Min();
                var choices=packing?definitions.Where(c=>NpcCampWork.Portable(plan,c,owner.NpcCarryCampPoles(group),owner.NpcTakeCampCover(group))&&CampBlueprints.State(plan,c)?.Materials.Count>0).OrderByDescending(c=>c.Stage):definitions.Where(c=>c.Stage==stage&&!CampBlueprints.Done(plan,c)).OrderBy(c=>c.Key);
                expectedPart=choices.FirstOrDefault(c=>!owner.NpcCampPartClaimed(actor,house,c.Key));if(expectedPart==null)continue;expected=house;break;
            }
            owner.ChooseNpcCampPart(actor,group,packing,out var actual,out var part);
            if(actual!=expected||part!=expectedPart)throw new InvalidOperationException("Allocation-free camp selector changed household/distance/stage/key/claim ordering for "+actor.Resident.Name);
            checks++;if(actual!=null){if(packing)packingChoices++;else construction++;}
        }
        var host=owner.livingGroups.Values.First(g=>g.Residents!=null&&owner.Camp.Objects.Any(c=>c.NpcGroup==g.Residents.Id&&c.Blueprint?.Parts.Count>0));
        var shelter=owner.Camp.Objects.First(c=>c.NpcGroup==host.Residents.Id&&c.Blueprint?.Parts.Count>0);var state=shelter.Blueprint.Parts[0];double work=state.WorkSeconds;bool carry=host.Situation.Camp.CarryPoles;
        try
        {
            foreach(var actor in owner.livingPeople.Values){Compare(actor,false);Compare(actor,true);}
            state.WorkSeconds=0;foreach(var actor in owner.livingPeople.Values){Compare(actor,false);Compare(actor,true);}
            host.Situation.Camp.CarryPoles=true;foreach(var actor in owner.livingPeople.Values)if(actor.GroupId==host.Residents.Id)Compare(actor,true);
        }
        finally{state.WorkSeconds=work;host.Situation.Camp.CarryPoles=carry;}
        if(checks<400||construction==0||packingChoices==0)throw new InvalidOperationException("Camp selector verification lacks actual construction/portable packing choices.");
        File.WriteAllText(Path.Combine(output,"camp-selection-controls.txt"),"PASS "+checks+" exact native selections versus previous stable ordering; construction="+construction+", packing="+packingChoices+". Includes restored temporary incomplete-work and carry-poles fixtures; no material or schedule mutation retained.");
    }
    static IEnumerator PrepareLivingCrowd(LocalTerrainExplorer owner,string output)
    {
        // Controlled gathering, not a change to world population or camp density.
        // Every actor comes from a distinct real sampled person and retains the
        // production motor, belongings, perception, daily tasks and pose graph.
        owner.livingQueryAt=double.PositiveInfinity;
        while(owner.livingWork!=null)yield return null;
        var host=owner.livingGroups.Values.Where(g=>g.CampLoaded&&!g.TraceOnly).OrderBy(g=>owner.HumanDistance(g.Anchor,owner.Position)).First();
        foreach(var g in owner.livingGroups.Values)g.Activities=g.Activities?.Where(a=>owner.livingPeople.ContainsKey(a.Person)).ToArray();
        TerrainHandoff.Coordinates(owner.frame,host.Anchor,out double hx,out double hz);
        var world=owner.sourceWorld;var history=new Prehistory(world);var state=world.Prehistory;
        var groups=new List<LivingGroup>();groups.Add(host);
        foreach(var cohort in state.Foragers.Cohorts.OrderBy(c=>(state.Foragers.Districts[c.District].Center-host.Anchor).Length))
        {
            int count=history.ForagerBandCount(state,cohort);
            for(int ordinal=0;ordinal<Math.Min(count,12)&&owner.livingPeople.Count<100;ordinal++)
            {
                var band=history.ForagerBand(state,cohort.Id,ordinal);if(owner.livingGroups.ContainsKey(band.Id))continue;
                var site=new HumanCampSite{Id=band.Id,Position=host.Anchor,Cell=host.Situation.Camp.Cell,ShelterAvailable=false};
                var situation=new HumanGroupSituation{Band=band,Camp=site,Position=host.Anchor,Phase=HumanCampPhase.Occupied,LocalHour=12,DepartureDay=owner.LivingClock.CalendarDay+1000,ArrivalDay=owner.LivingClock.CalendarDay,PermanentBase=true};
                var group=new LivingGroup{Situation=situation,Residents=band,Anchor=host.Anchor,Grounded=true,CampLoaded=true,Resources=host.Resources??new NpcResourceSites(),PlannedSolarDay=owner.PlayerRules.SolarDays};
                owner.livingGroups.Add(band.Id,group);groups.Add(group);var activities=new List<HumanActivitySample>();
                foreach(var person in band.Members.Where(p=>p.AgeYears>=16))
                {
                    if(owner.livingPeople.Count>=100)break;
                    int index=owner.livingPeople.Count;double x=hx+(index%10-4.5)*2,z=hz+(index/10-4.5)*2;
                    var point=owner.frame.Position(x,z);var activity=new HumanActivitySample{Person=person.Id,Group=band.Id,Activity=index%4==0?HumanActivity.Watch:index%4==1?HumanActivity.Craft:HumanActivity.Socialise,Position=point,Destination=point,Role="CrowdGathering"};
                    if(owner.CreateLivingHuman(group,person,activity,point,null))activities.Add(activity);
                    yield return null;
                }
                group.Activities=activities.ToArray();
            }
            if(owner.livingPeople.Count>=100)break;
        }
        owner.livingOrdered=owner.livingGroups.Values.OrderBy(g=>g.Situation.DistanceToPlayerM).ToArray();
        if(owner.livingPeople.Count!=100||owner.livingPeople.Keys.Distinct().Count()!=100)throw new InvalidOperationException("Crowd benchmark requires exactly 100 distinct real residents.");
        owner.npcCrowdFocus=new Vector3((float)(hx-owner.originX),(float)owner.VisibleGround(hx,hz,0)+1,(float)(hz-owner.originZ));
        var skins=owner.livingPeople.Values.SelectMany(a=>a.Root.GetComponentsInChildren<SkinnedMeshRenderer>()).ToArray();
        File.WriteAllText(Path.Combine(output,"crowd-fixture.json"),WorldJson.Write(new NpcCrowdProof{SrpBatching=UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching,NativePropertyBlockedSkins=skins.Count(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="WorldGen/Human"&&r.HasPropertyBlock()),Residents=owner.livingPeople.Count,Groups=groups.Count,SkinnedRenderers=skins.Length,EnabledSkinnedRenderers=skins.Count(r=>r.enabled),Vertices=skins.Sum(r=>(long)(r.sharedMesh?.vertexCount??0)),EnabledVertices=skins.Where(r=>r.enabled).Sum(r=>(long)(r.sharedMesh?.vertexCount??0)),Scope="Controlled local gathering of distinct real sampled people; natural population/encounter density unchanged. Normal physical NPC systems active, broad itinerary projection held fixed for this benchmark only.",People=owner.livingPeople.Values.Select(a=>new NpcResidentCheckRow{Id=a.ResidentId,Group=a.GroupId,Name=a.Resident.Name,Age=a.Resident.AgeYears,Activity=a.CurrentActivity.ToString()}).ToArray()}));
        if(!UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching)throw new InvalidOperationException("Crowd pipeline has SRP batching disabled.");
        if(skins.Any(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="WorldGen/Human"&&r.HasPropertyBlock()))throw new InvalidOperationException("Unwounded NPC native skins still carry per-renderer overrides.");
        var subject=owner.livingPeople.Values.First(a=>a.Resident.AgeYears>=18);owner.EnsureHumanDetail();
        var skin=subject.Root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>owner.humanDetailMeshes.ContainsKey(r.sharedMesh));var details=owner.humanDetailMeshes[skin.sharedMesh];
        var eye=owner.view.transform.position;var posed=new Mesh();
        foreach(double distance in new[]{2d,20,80,2})
        {
            owner.view.transform.position=subject.Root.transform.position+Vector3.right*(float)distance;subject.DetailAt=0;owner.UpdateLivingDetail(subject);
            Mesh expected=distance<9?details.Original:distance>55?details.Far:details.Middle;
            if(skin.sharedMesh!=expected||owner.HumanNativeMesh(skin.sharedMesh)!=details.Original)throw new InvalidOperationException("Human distance mesh did not switch and restore its exact original.");
            skin.BakeMesh(posed);if(posed.vertices.Any(p=>!float.IsFinite(p.x)||!float.IsFinite(p.y)||!float.IsFinite(p.z))||posed.bounds.size.y<.8f||posed.bounds.size.y>2.5f)throw new InvalidOperationException("Reduced native skin has invalid posed geometry.");
        }
        Destroy(posed);owner.view.transform.position=eye;subject.DetailAt=0;owner.UpdateLivingDetail(subject);
        VerifyCampPartSelection(owner,output);
        File.WriteAllText(Path.Combine(output,"human-detail-controls.txt"),"PASS original/middle/far/original restoration, unchanged source identity, and finite metre-scale baked native poses. Full native bones/weights/UVs checked during offline generation.");
        // Let the unchanged surrounding forest and actor assets finish before
        // measuring a crowd, instead of attributing initial page publication to AI.
        double deadline=Time.realtimeSinceStartupAsDouble+180;
        while((!owner.AllReady||!owner.residentForestReady||owner.installingScatter!=null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!owner.AllReady||!owner.residentForestReady)throw new InvalidOperationException("Crowd benchmark surroundings not resident; no performance claim accepted.");
        double settle=Time.realtimeSinceStartupAsDouble+5;while(Time.realtimeSinceStartupAsDouble<settle)yield return null;
    }
    static IEnumerator MeasureLivingHumans(LocalTerrainExplorer owner,string output)
    {
        double deadline=Time.realtimeSinceStartupAsDouble+300;
        if(Math.Abs(owner.viewDistance-6000)>.01)throw new InvalidOperationException("NPC benchmark view distance is not the requested 6000 metres: "+owner.viewDistance);
        while((!owner.AllReady||owner.installingScatter!=null||owner.scatterChunks.Count<Math.Min(4,owner.scatterWanted.Count))&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        File.WriteAllText(Path.Combine(output,"performance-readiness.txt"),owner.CheckStreamingState+"; ready="+owner.AllReady+"; view="+owner.viewDistance);
        if(!owner.AllReady)throw new InvalidOperationException("NPC benchmark did not finish initial terrain loading; no FPS data accepted.");
        int cap=Application.targetFrameRate;Application.targetFrameRate=-1;var report=new List<NpcPerformanceRow>();
        var arguments=Environment.GetCommandLineArgs();int option=Array.IndexOf(arguments,"--worldgen-npc-perf-seconds");double duration=30;
        if(option>=0&&option+1<arguments.Length)duration=Math.Max(5,Math.Min(180,double.Parse(arguments[option+1],System.Globalization.CultureInfo.InvariantCulture)));
        bool allocationProfile=arguments.Contains("--worldgen-npc-allocation-profile");
        if(allocationProfile)
        {
            if(!Debug.isDebugBuild||arguments.Contains("--worldgen-npc-automatic-comparison"))throw new InvalidOperationException("Allocation capture requires a separate diagnostic DevelopmentPlayer without performance comparisons.");
            duration=3; // Bounded diagnostic; never capture the lengthy loading phase.
            File.WriteAllText(Path.Combine(output,"allocation-profile.txt"),"Three-second DevelopmentPlayer allocation diagnostic after terrain/forest/crowd readiness. Timings include profiler overhead and MUST NOT be used as release throughput.\n");
        }
        var group=owner.livingGroups.Values.Where(g=>g.CampLoaded&&!g.TraceOnly).OrderBy(g=>owner.HumanDistance(g.Anchor,owner.Position)).First();
        TerrainHandoff.Coordinates(owner.frame,group.Anchor,out double campX,out double campZ);float floor=(float)owner.VisibleGround(campX,campZ,0);
        var focus=owner.npcCrowdFocus??new Vector3((float)(campX-owner.originX),floor+1,(float)(campZ-owner.originZ));
        // Repeat the same slow scenic orbit in every mode. No head whipping,
        // no invisible off-screen-only render target and no model inference.
        owner.npcModel??=new NpcModelClient();owner.npcModel.Config.Enabled=false;
        var priorReviewCamera=owner.landscapeReviewCamera;var priorReviewRotation=owner.landscapeReviewRotation;
        bool closeCrowd=owner.npcCrowdFocus.HasValue&&arguments.Contains("--worldgen-npc-crowd-close");
        void CameraAt(double seconds)
        {
            var offset=Quaternion.Euler(0,(float)(seconds/duration*360),0)*(closeCrowd?new Vector3(0,.65f,10):new Vector3(0,6,24));
            var eye=focus+offset;
            if(closeCrowd)eye.y=(float)owner.VisibleGround(eye.x+owner.originX,eye.z+owner.originZ,floor)+1.65f;
            // Use the production review override so UpdateCamera, culling and
            // rendering all see the same requested pose across the frame.
            owner.landscapeReviewCamera=eye;owner.view.transform.localPosition=owner.landscapeReviewCamera.Value;
            owner.view.transform.LookAt(owner.transform.TransformPoint(focus));owner.landscapeReviewRotation=owner.view.transform.localRotation;
        }
        owner.view.targetTexture=null;
        bool trace=WorldGenPerformance.TraceEnabled,priorQueryTrace=TerrainQueryTiming.Enabled;
        if(trace)
        {
            TerrainQueryTiming.Enabled=true;
            File.WriteAllText(Path.Combine(output,"trace-legend.json"),WorldJson.Write(new[]{"Update","Map","Scatter","Weather","Ground","Fog","Coverage","Sky","CameraRender","Batches","Bakes","Projection","Forest","Shadows","CoverDraw","Canopy","ForestDraw","Survival","Prepared","Humans","Player","Fauna","AnimalMove","AnimalPose","BatchCollect","BatchUpload","TerrainOcclusion","FrustumTest","InstanceAppend","ScatterClassify","ScatterAppend","ForestGround","GroundPublication","Inventory","Planning","Install","Retirement","Scheduling","Readout","Solids","PlayerCamera","PlayerWater","PlayerCombat","PlayerBlood","PlayerFootsteps","PlayerFeedback","PlayerHud","PlayerAltitude","Late","Gui","ScatterPlan","ScatterArt"}));
            File.WriteAllText(Path.Combine(output,"trace-scope.txt"),"Optional release-player main-thread scopes; nested/inclusive, not additive and not GPU timings. Wall intervals bracket coroutine resumes; render/GC work may fall in adjacent frames. Only24 slowest samples per mode retained; no profiler binary capture or per-frame file IO.");
        }
        int probeOption=Array.IndexOf(arguments,"--worldgen-gc-probe");if(probeOption>=0&&probeOption+1<arguments.Length)WorldGenNativeGcProbe.Start(arguments[probeOption+1]);
        long allocationBefore=GC.GetAllocatedBytesForCurrentThread();var allocationProbe=new byte[65536];bool allocationCounterAvailable=GC.GetAllocatedBytesForCurrentThread()>allocationBefore;GC.KeepAlive(allocationProbe);
        bool compareAutomatic=arguments.Contains("--worldgen-npc-automatic-comparison"),requestedAutomatic=owner.npcAutomaticPoses;
        ulong priorGcSlice=UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds;
        using var cameraProof=new NpcRenderProof(owner.view);
        string[] modes=allocationProfile?new[]{"normal"}:compareAutomatic?new[]{"normal","automatic-poses","automatic-half-ms-gc","normal-half-ms-gc","normal-repeat"}:new[]{"normal","render-only","no-residents","normal-repeat"};
        foreach(string mode in modes)
        {
            UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds=mode.Contains("half-ms-gc")?500000UL:priorGcSlice;
            owner.SetNpcAutomaticPoses(compareAutomatic?mode.StartsWith("automatic",StringComparison.Ordinal):requestedAutomatic&&mode!="render-only"&&mode!="no-residents");
            owner.livingBenchmarkFreeze=mode=="render-only"||mode=="no-residents";
            foreach(var actor in owner.livingPeople.Values){actor.Root.SetActive(mode!="no-residents");if(actor.InfantCarrier!=null)actor.InfantCarrier.SetActive(mode!="no-residents");}
            Debug.Log("NPC performance begins "+mode+" / "+owner.livingPeople.Count+" residents / "+owner.tiles.Count+" tiles; ready "+owner.AllReady);
            double warmUntil=Time.realtimeSinceStartupAsDouble+2;while(Time.realtimeSinceStartupAsDouble<warmUntil){CameraAt(0);yield return null;}
            if(owner.npcCrowdFocus.HasValue&&mode=="normal")
            {
                if(owner.livingPeople.Count!=100||owner.livingPeople.Values.Any(a=>a.Health<=0))throw new InvalidOperationException("Crowd benchmark lost a living resident before measurement.");
                yield return new WaitForEndOfFrame();var capture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"crowd-100.png"),capture.EncodeToPNG());Destroy(capture);
            }
            if(owner.npcAutomaticPoses){yield return new WaitForEndOfFrame();string controls;try{controls=owner.VerifyAutomaticNpcPoses();}catch{WorldGenNativeGcProbe.Write(output);throw;}File.WriteAllText(Path.Combine(output,"automatic-pose-controls.txt"),controls);}
            long heapStart=UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            var enabledSkins=owner.livingPeople.Values.SelectMany(a=>a.Root.GetComponentsInChildren<SkinnedMeshRenderer>()).Where(r=>r.enabled).ToArray();long enabledVertices=enabledSkins.Sum(r=>(long)(r.sharedMesh?.vertexCount??0));
            var ticks=new List<double>(65536);var frames=new List<double>(65536);var stages=new double[5];double previous=Time.realtimeSinceStartupAsDouble,start=previous,lateNpcMs=0;long previousFrameTick=System.Diagnostics.Stopwatch.GetTimestamp();int unfocused=0,minPeople=owner.livingPeople.Count,maxPeople=minPeople;
            var traceTotals=trace?new double[WorldGenPerformance.TraceMilliseconds.Length]:null;var slow=trace?new List<NpcSlowFrame>(24):null;
            long appendBytes=owner.scatterAppendAllocations,appendBuilds=owner.scatterAppendBuilds;int artModels=owner.scatterArt.Count;
            bool forestReady=owner.residentForestReady;int forestPages=owner.forestFields.Count,forestRoots=owner.forestRootCount;
            int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);long windowTick=System.Diagnostics.Stopwatch.GetTimestamp();var clockBridge=WorldGenPerformance.CaptureNativeClock();
            double maxCameraError=0,maxAngleError=0,maxCullDelta=0;
            if(trace){WorldGenPerformance.ClearFaunaTrace();WorldGenPerformance.Measuring=true;}
            cameraProof.Start();
            if(allocationProfile)
            {
                UnityEngine.Profiling.Profiler.logFile=Path.Combine(output,"crowd-allocations.raw");
                UnityEngine.Profiling.Profiler.enableBinaryLog=true;
                UnityEngine.Profiling.Profiler.enableAllocationCallstacks=true;
                UnityEngine.Profiling.Profiler.enabled=true;
            }
            try
            {
            while(Time.realtimeSinceStartupAsDouble-start<duration)
            {
                CameraAt(Time.realtimeSinceStartupAsDouble-start);if(mode=="no-residents")foreach(var resident in owner.livingPeople.Values)if(resident.Root.activeSelf)resident.Root.SetActive(false);yield return null;
                maxCameraError=Math.Max(maxCameraError,Vector3.Distance(owner.view.transform.localPosition,owner.landscapeReviewCamera.Value));maxAngleError=Math.Max(maxAngleError,Quaternion.Angle(owner.view.transform.localRotation,owner.landscapeReviewRotation));maxCullDelta=Math.Max(maxCullDelta,Vector3.Distance(owner.scatterEye,owner.view.transform.localPosition));
                if(maxCameraError>.001||maxAngleError>.05)throw new InvalidOperationException("NPC scenic benchmark camera was overwritten during a measured frame; reject its moving-view results.");
                minPeople=Math.Min(minPeople,owner.livingPeople.Count);maxPeople=Math.Max(maxPeople,owner.livingPeople.Count);long frameEndTick=System.Diagnostics.Stopwatch.GetTimestamp();double now=Time.realtimeSinceStartupAsDouble,frameMs=(frameEndTick-previousFrameTick)*1000d/System.Diagnostics.Stopwatch.Frequency;previousFrameTick=frameEndTick;lateNpcMs+=owner.livingLatePoseMs;ticks.Add(owner.livingTickMs);frames.Add(frameMs);previous=now;if(!Application.isFocused)unfocused++;for(int k=0;k<stages.Length;k++)stages[k]+=owner.livingCosts[k];
                if(trace)
                {
                    var current=WorldGenPerformance.TraceMilliseconds;for(int k=0;k<current.Length;k++)traceTotals[k]+=current[k];
                    if(slow.Count<24||frameMs>slow[slow.Count-1].FrameMs)
                    {
                        var sample=new NpcSlowFrame{ManagedFrameEnd=frameEndTick,AtSeconds=now-start,FrameMs=frameMs,NpcMs=owner.livingTickMs,MainThreadTraceMs=(double[])current.Clone(),Gen0=GC.CollectionCount(0)-gc0,Gen1=GC.CollectionCount(1)-gc1,Gen2=GC.CollectionCount(2)-gc2,MainAllocatedBytes=GC.GetAllocatedBytesForCurrentThread(),ScatterObjects=owner.ScatterObjects,TreeInstances=owner.GeneratedTreeObjects,Batches=owner.drawBatches.Count,ForestPages=owner.forestFields.Count,ResidentForestReady=owner.residentForestReady};
                        int at=0;while(at<slow.Count&&slow[at].FrameMs>=frameMs)at++;if(slow.Count==24)slow.RemoveAt(23);slow.Insert(at,sample);
                    }
                }
            }
            }
            finally
            {
                cameraProof.Measuring=false;
                if(allocationProfile)
                {
                    UnityEngine.Profiling.Profiler.enabled=false;
                    UnityEngine.Profiling.Profiler.enableAllocationCallstacks=false;
                    UnityEngine.Profiling.Profiler.enableBinaryLog=false;
                    UnityEngine.Profiling.Profiler.logFile="";
                }
            }
            if(cameraProof.UnexpectedRenders>0||cameraProof.ExpectedRenders<frames.Count-1||cameraProof.ExpectedRenders>frames.Count+1)throw new InvalidOperationException("NPC performance requires one actual exploration-camera render per frame: expected="+cameraProof.ExpectedRenders+", frames="+frames.Count+", unexpected="+cameraProof.UnexpectedRenders+" / "+cameraProof.UnexpectedName);
            if(trace){WorldGenPerformance.Measuring=false;WorldGenPerformance.WriteFaunaTrace(Path.Combine(output,mode+"-fauna-events.csv"),windowTick);for(int k=0;k<traceTotals.Length;k++)traceTotals[k]/=ticks.Count;}
            for(int k=0;k<stages.Length;k++)stages[k]/=ticks.Count;
            ticks.Sort();frames.Sort();report.Add(new NpcPerformanceRow{Mode=mode,Residents=owner.livingPeople.Count,MinResidents=minPeople,MaxResidents=maxPeople,Frames=ticks.Count,Tiles=owner.tiles.Count,ScatterChunks=owner.scatterChunks.Count,TerrainReady=owner.AllReady,
                MeanNpcMs=ticks.Average(),P95NpcMs=ticks[(int)(ticks.Count*.95)],P99NpcMs=ticks[(int)(ticks.Count*.99)],MaxNpcMs=ticks.Last(),MeanFrameMs=frames.Average(),P95FrameMs=frames[(int)(frames.Count*.95)],P99FrameMs=frames[(int)(frames.Count*.99)],MaxFrameMs=frames.Last(),BackgroundQueryMs=owner.livingLastMs,MeanStagesMs=stages,MeanMainThreadTraceMs=traceTotals,SlowFrames=slow?.ToArray(),Gen0Collections=GC.CollectionCount(0)-gc0,Gen1Collections=GC.CollectionCount(1)-gc1,Gen2Collections=GC.CollectionCount(2)-gc2,UnfocusedFrames=unfocused,Over10Ms=frames.Count(v=>v>10),Over1667Ms=frames.Count(v=>v>1000d/60),Width=Screen.width,Height=Screen.height,TreeQuality=TerrainVideoOptions.TreeQuality,ViewDistanceM=owner.viewDistance,Seconds=previous-start,Route=closeCrowd?"Radius10m eye-height crowd orbit; same path and duration per mode":"Radius24m elevated scenic camp orbit; same path and duration per mode"});
            var recorded=report[report.Count-1];recorded.HeapStart=heapStart;recorded.HeapEnd=UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();recorded.AutomaticPoses=owner.npcAutomaticPoses;recorded.MeanLateNpcMs=lateNpcMs/ticks.Count;recorded.GcIncremental=UnityEngine.Scripting.GarbageCollector.isIncremental;recorded.GcMode=UnityEngine.Scripting.GarbageCollector.GCMode.ToString();recorded.GcSliceNanoseconds=UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds;recorded.AllocationCounterAvailable=allocationCounterAvailable;recorded.ClockBefore=clockBridge.Before;recorded.ClockNative=clockBridge.Native;recorded.ClockAfter=clockBridge.After;recorded.ClockFrequency=clockBridge.Frequency;recorded.ManagedFrequency=System.Diagnostics.Stopwatch.Frequency;recorded.EnabledSkins=enabledSkins.Length;recorded.EnabledSkinVertices=enabledVertices;recorded.ScatterAppendAllocatedBytes=owner.scatterAppendAllocations-appendBytes;recorded.ScatterBuilds=owner.scatterAppendBuilds-appendBuilds;recorded.ArtModelsStart=artModels;recorded.ArtModelsEnd=owner.scatterArt.Count;
            recorded.ExplorationRenders=cameraProof.ExpectedRenders;recorded.UnexpectedCameraRenders=cameraProof.UnexpectedRenders;
            recorded.MaximumCameraErrorM=maxCameraError;recorded.MaximumCameraAngleError=maxAngleError;recorded.MaximumScatterEyeDeltaM=maxCullDelta;
            recorded.ResidentForestReadyStart=forestReady;recorded.ResidentForestReadyEnd=owner.residentForestReady;recorded.ForestPagesStart=forestPages;recorded.ForestPagesEnd=owner.forestFields.Count;recorded.ForestRootsStart=forestRoots;recorded.ForestRootsEnd=owner.forestRootCount;
            File.WriteAllText(Path.Combine(output,"performance.json"),WorldJson.Write(report.ToArray()));
        }
        UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds=priorGcSlice;owner.SetNpcAutomaticPoses(requestedAutomatic);WorldGenNativeGcProbe.Write(output);
        owner.livingBenchmarkFreeze=false;foreach(var actor in owner.livingPeople.Values)actor.Root.SetActive(true);Application.targetFrameRate=cap;
        owner.landscapeReviewCamera=priorReviewCamera;owner.landscapeReviewRotation=priorReviewRotation;owner.UpdateCamera();
        if(trace)TerrainQueryTiming.Enabled=priorQueryTrace;
        File.WriteAllText(Path.Combine(output,"performance.json"),WorldJson.Write(report.ToArray()));
        Debug.Log("NPC measured main-thread normal "+report[0].MeanNpcMs.ToString("F3")+"ms / repeat "+report[report.Count-1].MeanNpcMs.ToString("F3")+"ms; "+owner.livingPeople.Count+" residents");
    }
}
