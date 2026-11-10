using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Serialization;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public sealed class NpcPerformanceRow
    {
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
    {public double AtSeconds,FrameMs,NpcMs;public double[] MainThreadTraceMs;public int Gen0,Gen1,Gen2;public long MainAllocatedBytes;public int ScatterObjects,TreeInstances,Batches,ForestPages;public bool ResidentForestReady;}
    Vector3? npcCrowdFocus;
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
        File.WriteAllText(Path.Combine(output,"crowd-fixture.json"),WorldJson.Write(new{Residents=owner.livingPeople.Count,Groups=groups.Count,Scope="Controlled local gathering of distinct real sampled people; natural population/encounter density unchanged. Normal physical NPC systems active, broad itinerary projection held fixed for this benchmark only.",People=owner.livingPeople.Values.Select(a=>new{a.ResidentId,a.GroupId,a.Resident.Name,a.Resident.AgeYears}).ToArray()}));
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
        var group=owner.livingGroups.Values.Where(g=>g.CampLoaded&&!g.TraceOnly).OrderBy(g=>owner.HumanDistance(g.Anchor,owner.Position)).First();
        TerrainHandoff.Coordinates(owner.frame,group.Anchor,out double campX,out double campZ);float floor=(float)owner.VisibleGround(campX,campZ,0);
        var focus=owner.npcCrowdFocus??new Vector3((float)(campX-owner.originX),floor+1,(float)(campZ-owner.originZ));
        // Repeat the same slow scenic orbit in every mode. No head whipping,
        // no invisible off-screen-only render target and no model inference.
        owner.npcModel??=new NpcModelClient();owner.npcModel.Config.Enabled=false;
        var priorReviewCamera=owner.landscapeReviewCamera;var priorReviewRotation=owner.landscapeReviewRotation;
        void CameraAt(double seconds)
        {
            var offset=Quaternion.Euler(0,(float)(seconds/duration*360),0)*new Vector3(0,6,24);
            // Use the production review override so UpdateCamera, culling and
            // rendering all see the same requested pose across the frame.
            owner.landscapeReviewCamera=focus+offset;owner.view.transform.localPosition=owner.landscapeReviewCamera.Value;
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
        foreach(string mode in new[]{"normal","render-only","no-residents","normal-repeat"})
        {
            owner.livingBenchmarkFreeze=mode=="render-only"||mode=="no-residents";
            foreach(var actor in owner.livingPeople.Values){actor.Root.SetActive(mode!="no-residents");if(actor.InfantCarrier!=null)actor.InfantCarrier.SetActive(mode!="no-residents");}
            Debug.Log("NPC performance begins "+mode+" / "+owner.livingPeople.Count+" residents / "+owner.tiles.Count+" tiles; ready "+owner.AllReady);
            double warmUntil=Time.realtimeSinceStartupAsDouble+2;while(Time.realtimeSinceStartupAsDouble<warmUntil){CameraAt(0);yield return null;}
            var ticks=new List<double>(65536);var frames=new List<double>(65536);var stages=new double[5];double previous=Time.realtimeSinceStartupAsDouble,start=previous;int unfocused=0,minPeople=owner.livingPeople.Count,maxPeople=minPeople;
            var traceTotals=trace?new double[WorldGenPerformance.TraceMilliseconds.Length]:null;var slow=trace?new List<NpcSlowFrame>(24):null;
            long appendBytes=owner.scatterAppendAllocations,appendBuilds=owner.scatterAppendBuilds;int artModels=owner.scatterArt.Count;
            bool forestReady=owner.residentForestReady;int forestPages=owner.forestFields.Count,forestRoots=owner.forestRootCount;
            int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);long windowTick=System.Diagnostics.Stopwatch.GetTimestamp();
            double maxCameraError=0,maxAngleError=0,maxCullDelta=0;
            if(trace){WorldGenPerformance.ClearFaunaTrace();WorldGenPerformance.Measuring=true;}
            while(Time.realtimeSinceStartupAsDouble-start<duration)
            {
                CameraAt(Time.realtimeSinceStartupAsDouble-start);if(mode=="no-residents")foreach(var resident in owner.livingPeople.Values)if(resident.Root.activeSelf)resident.Root.SetActive(false);yield return null;
                maxCameraError=Math.Max(maxCameraError,Vector3.Distance(owner.view.transform.localPosition,owner.landscapeReviewCamera.Value));maxAngleError=Math.Max(maxAngleError,Quaternion.Angle(owner.view.transform.localRotation,owner.landscapeReviewRotation));maxCullDelta=Math.Max(maxCullDelta,Vector3.Distance(owner.scatterEye,owner.view.transform.localPosition));
                if(maxCameraError>.001||maxAngleError>.05)throw new InvalidOperationException("NPC scenic benchmark camera was overwritten during a measured frame; reject its moving-view results.");
                minPeople=Math.Min(minPeople,owner.livingPeople.Count);maxPeople=Math.Max(maxPeople,owner.livingPeople.Count);double now=Time.realtimeSinceStartupAsDouble,frameMs=(now-previous)*1000;ticks.Add(owner.livingTickMs);frames.Add(frameMs);previous=now;if(!Application.isFocused)unfocused++;for(int k=0;k<stages.Length;k++)stages[k]+=owner.livingCosts[k];
                if(trace)
                {
                    var current=WorldGenPerformance.TraceMilliseconds;for(int k=0;k<current.Length;k++)traceTotals[k]+=current[k];
                    if(slow.Count<24||frameMs>slow[slow.Count-1].FrameMs)
                    {
                        var sample=new NpcSlowFrame{AtSeconds=now-start,FrameMs=frameMs,NpcMs=owner.livingTickMs,MainThreadTraceMs=(double[])current.Clone(),Gen0=GC.CollectionCount(0)-gc0,Gen1=GC.CollectionCount(1)-gc1,Gen2=GC.CollectionCount(2)-gc2,MainAllocatedBytes=GC.GetAllocatedBytesForCurrentThread(),ScatterObjects=owner.ScatterObjects,TreeInstances=owner.GeneratedTreeObjects,Batches=owner.drawBatches.Count,ForestPages=owner.forestFields.Count,ResidentForestReady=owner.residentForestReady};
                        int at=0;while(at<slow.Count&&slow[at].FrameMs>=frameMs)at++;if(slow.Count==24)slow.RemoveAt(23);slow.Insert(at,sample);
                    }
                }
            }
            if(trace){WorldGenPerformance.Measuring=false;WorldGenPerformance.WriteFaunaTrace(Path.Combine(output,mode+"-fauna-events.csv"),windowTick);for(int k=0;k<traceTotals.Length;k++)traceTotals[k]/=ticks.Count;}
            for(int k=0;k<stages.Length;k++)stages[k]/=ticks.Count;
            ticks.Sort();frames.Sort();report.Add(new NpcPerformanceRow{Mode=mode,Residents=owner.livingPeople.Count,MinResidents=minPeople,MaxResidents=maxPeople,Frames=ticks.Count,Tiles=owner.tiles.Count,ScatterChunks=owner.scatterChunks.Count,TerrainReady=owner.AllReady,
                MeanNpcMs=ticks.Average(),P95NpcMs=ticks[(int)(ticks.Count*.95)],P99NpcMs=ticks[(int)(ticks.Count*.99)],MaxNpcMs=ticks.Last(),MeanFrameMs=frames.Average(),P95FrameMs=frames[(int)(frames.Count*.95)],P99FrameMs=frames[(int)(frames.Count*.99)],MaxFrameMs=frames.Last(),BackgroundQueryMs=owner.livingLastMs,MeanStagesMs=stages,MeanMainThreadTraceMs=traceTotals,SlowFrames=slow?.ToArray(),Gen0Collections=GC.CollectionCount(0)-gc0,Gen1Collections=GC.CollectionCount(1)-gc1,Gen2Collections=GC.CollectionCount(2)-gc2,UnfocusedFrames=unfocused,Over10Ms=frames.Count(v=>v>10),Over1667Ms=frames.Count(v=>v>1000d/60),Width=Screen.width,Height=Screen.height,TreeQuality=TerrainVideoOptions.TreeQuality,ViewDistanceM=owner.viewDistance,Seconds=previous-start,Route="Radius24m scenic camp orbit; same path and duration per mode"});
            var recorded=report[report.Count-1];recorded.ScatterAppendAllocatedBytes=owner.scatterAppendAllocations-appendBytes;recorded.ScatterBuilds=owner.scatterAppendBuilds-appendBuilds;recorded.ArtModelsStart=artModels;recorded.ArtModelsEnd=owner.scatterArt.Count;
            recorded.MaximumCameraErrorM=maxCameraError;recorded.MaximumCameraAngleError=maxAngleError;recorded.MaximumScatterEyeDeltaM=maxCullDelta;
            recorded.ResidentForestReadyStart=forestReady;recorded.ResidentForestReadyEnd=owner.residentForestReady;recorded.ForestPagesStart=forestPages;recorded.ForestPagesEnd=owner.forestFields.Count;recorded.ForestRootsStart=forestRoots;recorded.ForestRootsEnd=owner.forestRootCount;
            File.WriteAllText(Path.Combine(output,"performance.json"),WorldJson.Write(report.ToArray()));
        }
        owner.livingBenchmarkFreeze=false;foreach(var actor in owner.livingPeople.Values)actor.Root.SetActive(true);Application.targetFrameRate=cap;
        owner.landscapeReviewCamera=priorReviewCamera;owner.landscapeReviewRotation=priorReviewRotation;owner.UpdateCamera();
        if(trace)TerrainQueryTiming.Enabled=priorQueryTrace;
        File.WriteAllText(Path.Combine(output,"performance.json"),WorldJson.Write(report.ToArray()));
        Debug.Log("NPC measured main-thread normal "+report[0].MeanNpcMs.ToString("F3")+"ms / repeat "+report[3].MeanNpcMs.ToString("F3")+"ms; "+owner.livingPeople.Count+" residents");
    }
}
