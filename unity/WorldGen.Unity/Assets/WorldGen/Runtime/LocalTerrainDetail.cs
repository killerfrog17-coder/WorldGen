using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    readonly bool detailBaseline=Environment.GetCommandLineArgs().Contains("--worldgen-detail-baseline");
    readonly bool legacyLoader=Environment.GetCommandLineArgs().Contains("--worldgen-loader-legacy");
    TerrainDetailOptions detailOptions=new TerrainDetailOptions();
    TerrainDetailEstimator detailEstimator;
    Task<Dictionary<TerrainTileKey,int>> detailPlan;
    CancellationTokenSource detailCancellation;
    int detailRevision=-1,detailGeneration,plannedPixels;float plannedFov;
    double detailX,detailZ,nextRequestAt,frameUploadMs,fogSafeRadius,terrainRevealRadius,forestRevealRadius,streamedFog=TerrainStreamingReveal.InitialDistance,fogCheckAt;
    bool detailDirty;double renderedStreamingFog=TerrainStreamingReveal.InitialDistance;
    TerrainQualityFootprint fogFootprint;
    readonly Dictionary<TerrainTileKey,double[]> backdrops=new Dictionary<TerrainTileKey,double[]>();
    struct BackdropEntry{public TerrainTileKey Key;public double[] Value;public int Order;}
    readonly List<BackdropEntry> backdropOrder=new List<BackdropEntry>();Comparison<BackdropEntry> backdropComparison;
    int backdropRevision,sortedBackdropRevision=-1;double sortedBackdropX=double.NaN,sortedBackdropZ=double.NaN;
    public double StreamingFogDistance=>streamedFog;
    public bool DetailPlanning=>detailPlan!=null||detailDirty;
    bool ImmediateTerrain(TerrainTileKey key)=>detailBaseline?TerrainLod.Immediate(key,motion.X,motion.Z):TerrainLod.DistanceSquared(key,motion.X,motion.Z)<=Math.Pow(detailOptions.FullRadius(HorizontalScale),2);
    int WorkerLimit=>Math.Max(1,Math.Min(detailOptions.Workers,Math.Max(1,SystemInfo.processorCount-2)));
    // Forest requests use the SAME worker permits. Completed forest payloads
    // awaiting their bounded install are no longer consuming a CPU permit
    // (including while the user has disabled their drawing).
    int ActiveWorkerCount
    {get{
        int count=0;
        foreach(var work in jobs)if(legacyLoader||!work.Task.IsCompleted)count++;
        foreach(var work in scatterJobs)if(legacyLoader||!work.Task.IsCompleted)count++;
        foreach(var work in forestJobs)if(!work.Task.IsCompleted)count++;
        if(animalPlan!=null&&!animalPlan.IsCompleted)count++;
        // A finished payload releases its CPU lane, but never allows an
        // unbounded producer queue while main-thread publication is occupied.
        if(!legacyLoader&&jobs.Count+scatterJobs.Count>=WorkerLimit*2)return Math.Max(count,WorkerLimit);
        return count;
    }}
    bool reserveScatterFrame;double lastStreamingOpportunity;
    // A necessary visible-set rebuild cannot be delayed when the camera turns.
    // Prefer a different frame for optional uploads, but guarantee an upload
    // opportunity every 100 ms even during uninterrupted fast movement.
    void ReserveScatterFrame()
    {
        reserveScatterFrame=false;
        if(!WorldGenPerformance.SeparateStreamingAndScatter||scatter==null)return;
        double now=Time.realtimeSinceStartupAsDouble;
        bool rebuilding=scatterDirty||ScatterViewChanged||!WorldGenPerformance.SuppressIdleScatter&&now-lastBatchTime>1;
        if(rebuilding&&now-lastStreamingOpportunity<.1)reserveScatterFrame=true;
        else lastStreamingOpportunity=now;
    }
    bool CanInstall=>!reserveScatterFrame&&!GroundPublicationPending&&installationsThisFrame<detailOptions.Installations&&(installationsThisFrame==0||frameUploadMs<detailOptions.UploadBudgetMs);
    bool CanRequest=>Time.realtimeSinceStartupAsDouble>=nextRequestAt;
    public string CheckStreamingState=>"error="+error+"; generation="+generation+"; jobs="+jobs.Count+"; done="+jobs.Count(j=>j.Task.IsCompleted)+"; matched="+jobs.Count(WorkMatchesCurrent)+"; detail="+(detailPlan?.Status.ToString()??"none")+"; installs="+detailOptions.Installations+"; y="+motion.Y+"; "+string.Join(" | ",jobs.Take(3).Select(j=>j.Key.X+","+j.Key.Z+","+j.Key.SizeM+"/"+j.Segments+" gen="+j.Generation+" "+j.Task.Status));
    void ResetDetailFrame()
    {
        detailCancellation?.Cancel();detailEstimator=new TerrainDetailEstimator(terrain,frame);
        detailDirty=true;backdrops.Clear();backdropRevision++;renderedStreamingFog=streamedFog=TerrainStreamingReveal.InitialDistance;fogSafeRadius=terrainRevealRadius=forestRevealRadius=0;fogCheckAt=0;fogFootprint=null;
    }
    Dictionary<TerrainTileKey,int> InitialDetailPlan(double x,double z,double altitude)
    {
        if(detailBaseline)return TerrainLod.Plan(x,z,altitude,viewDistance,HorizontalScale);
        var options=detailOptions.Copy();options.ViewDistanceM=viewDistance;TerrainDiagnostics.Detail(options);
        return TerrainAdaptiveLod.Plan(x,z,altitude,HorizontalScale,options,(_,__)=>0,Screen.height,view.fieldOfView);
    }
    void PollTerrainDetail()
    {
        if(detailRevision!=TerrainVideoOptions.Revision)
        {
            detailRevision=TerrainVideoOptions.Revision;detailOptions=TerrainVideoOptions.Current.Copy();
            viewDistance=Math.Min(detailOptions.ViewDistanceM,MaximumViewDistance);SetFog();
            CancelPreparedFrame();RefreshWanted();
        }
        if(detailBaseline){detailDirty=false;return;}
        if(plannedPixels!=Screen.height||Math.Abs(plannedFov-view.fieldOfView)>.1)
        {plannedPixels=Screen.height;plannedFov=view.fieldOfView;detailDirty=true;}
        if(detailPlan!=null&&detailPlan.IsCompleted)
        {
            if(detailPlan.IsFaulted)
            {
                var fault=detailPlan.Exception.GetBaseException();
                if(!(detailCancellation?.IsCancellationRequested==true&&fault is OperationCanceledException))
                {Debug.LogWarning("Terrain detail plan failed: "+detailPlan.Exception);error=fault.Message;debugOpen=true;}
            }
            else if(!detailPlan.IsCanceled&&detailGeneration==generation&&Math.Abs(detailX-motion.X)<TerrainLod.ReplanDistance(HorizontalScale)&&Math.Abs(detailZ-motion.Z)<TerrainLod.ReplanDistance(HorizontalScale))
                AdoptDetailPlan(detailPlan.Result);
            detailPlan=null;detailCancellation?.Dispose();detailCancellation=null;
        }
        if(detailPlan!=null||!detailDirty)return;
        ScheduleTerrainDetailPlan();
    }
    // Captured job inputs belong to scheduling, not every idle polling frame.
    void ScheduleTerrainDetailPlan()
    {
        detailDirty=false;detailGeneration=generation;detailX=motion.X;detailZ=motion.Z;
        double x=motion.X,z=motion.Z,altitude=HeightAboveGround,scale=HorizontalScale;
        var options=detailOptions.Copy();options.ViewDistanceM=viewDistance;TerrainDiagnostics.Detail(options);
        var previous=new Dictionary<TerrainTileKey,int>(wanted);var estimator=detailEstimator;
        int pixels=Screen.height;double fov=view.fieldOfView;
        detailCancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);var token=detailCancellation.Token;
        // Small profile/error metadata only, not mesh buffers in Unity's managed heap.
        detailPlan=Task.Run(()=>TerrainAdaptiveLod.Plan(x,z,altitude,scale,options,estimator.Error,pixels,fov,previous,token),token);
    }
    void AdoptDetailPlan(Dictionary<TerrainTileKey,int> plan)
    {
        wanted=plan;wantedEdges.Clear();foreach(var p in terrainEdgeWorkspace.Build(wanted))wantedEdges.Add(p.Key,p.Value);
        priority.Clear();priority.AddRange(wanted.Keys);
        foreach(var job in jobs)if(job.Prepared==null&&!job.Prefetch&&!job.Backdrop&&!WorkMatchesCurrent(job))job.Cancellation?.Cancel();
        priority.Sort((a,b)=>StreamingPriority(a).CompareTo(StreamingPriority(b)));fogCheckAt=0;
    }
    void PlanBackdrop()
    {
        backdrops.Clear();backdropRevision++;int root=TerrainAdaptiveLod.BackdropLevel(HorizontalScale,viewDistance);
        double size=TerrainLod.BaseSizeM*HorizontalScale*(1<<root);
        for(int z=(int)Math.Floor((motion.Z-viewDistance)/size);z<=(int)Math.Floor((motion.Z+viewDistance)/size);z++)
            for(int x=(int)Math.Floor((motion.X-viewDistance)/size);x<=(int)Math.Floor((motion.X+viewDistance)/size);x++)
            {
                var key=new TerrainTileKey(x,z,root,HorizontalScale);
                if(TerrainLod.DistanceSquared(key,motion.X,motion.Z)>viewDistance*viewDistance)continue;
                double step=key.SizeM/8;backdrops[key]=new[]{step,step,step,step};
            }
    }
    bool BackdropNeeded(TerrainTileKey key){foreach(var p in wanted)if(Overlaps(key,p.Key)&&!HasResidentCoverage(p.Key))return true;return false;}
    bool HasResidentCoverage(TerrainTileKey key)
    {
        for(int depth=0;depth<24-key.Level;depth++)
            if(tiles.ContainsKey(new TerrainTileKey(key.X>>depth,key.Z>>depth,key.Level+depth,key.Scale)))return true;
        return false;
    }
    void ScheduleBackdrop()
    {
        if(detailBaseline)return;
        if(NearTerrainPending()||!CanRequest)return;
        // Coarse background charts do not satisfy the requested-quality reveal
        // gate. Fill the first visible neighbourhood before spending the same
        // limited lanes on distant fallback charts.
        if(!legacyLoader&&terrainRevealRadius<Math.Min(600,viewDistance*.75))return;
        if(sortedBackdropRevision!=backdropRevision)
        {
            backdropOrder.Clear();int order=0;foreach(var p in backdrops)backdropOrder.Add(new BackdropEntry{Key=p.Key,Value=p.Value,Order=order++});
        }
        if(sortedBackdropRevision!=backdropRevision||sortedBackdropX!=motion.X||sortedBackdropZ!=motion.Z)
        {
            if(backdropComparison==null)backdropComparison=CompareBackdrop;
            backdropOrder.Sort(backdropComparison);sortedBackdropRevision=backdropRevision;sortedBackdropX=motion.X;sortedBackdropZ=motion.Z;
        }
        foreach(var p in backdropOrder)
        {
            if(ActiveWorkerCount>=WorkerLimit)return;
            if(tiles.ContainsKey(p.Key)||HasChartTerrainJob(p.Key)||!BackdropNeeded(p.Key))continue;
            RequestTerrain(p.Key,8,p.Value,backdrop:true);if(!CanRequest)return;
        }
    }
    int CompareBackdrop(BackdropEntry a,BackdropEntry b)
    {int byDistance=TerrainLod.DistanceSquared(a.Key,motion.X,motion.Z).CompareTo(TerrainLod.DistanceSquared(b.Key,motion.X,motion.Z));return byDistance!=0?byDistance:a.Order.CompareTo(b.Order);}
    bool HasChartTerrainJob(TerrainTileKey key)
    {foreach(var job in jobs)if(job.Prepared==null&&job.Key.Equals(key))return true;return false;}
    void UpdateStreamingFog()
    {
        long traceStarted=WorldGenPerformance.TraceBegin();try{UpdateStreamingFogBody();}finally{WorldGenPerformance.TraceEnd(5,traceStarted);}
    }
    void UpdateStreamingFogBody()
    {
        if(detailBaseline){renderedStreamingFog=streamedFog=viewDistance*.98;SetFog();return;}
        double now=Time.realtimeSinceStartupAsDouble;
        if(now>=fogCheckAt)
        {
            fogCheckAt=now+.15;double limit=viewDistance*.98;
            if(fogFootprint==null)fogFootprint=new TerrainQualityFootprint(tiles.Select(p=>new KeyValuePair<TerrainTileKey,int>(p.Key,p.Value.Segments)));
            double gap=fogFootprint.NearestGap(wanted,motion.X,motion.Z,limit/.94);
            terrainRevealRadius=Math.Min(limit,gap*.94);
            forestRevealRadius=TreeRevealDistance(limit);
            double safe=Math.Min(terrainRevealRadius,forestRevealRadius);
            fogSafeRadius=Math.Max(8,safe);
        }
        // Readiness gates NEW reveal only. Walking into a finer LOD request or
        // replacing a forest page must not pull the whole visible world inward.
        // A late product uses its local transition instead of restarting fog.
        streamedFog=TerrainStreamingReveal.Advance(streamedFog,fogSafeRadius,viewDistance*.98,Time.unscaledDeltaTime);
        renderedStreamingFog=TerrainStreamingReveal.Smooth(renderedStreamingFog,TerrainVideoOptions.LoadingFog?streamedFog:viewDistance*.98,Time.unscaledDeltaTime,TerrainVideoOptions.LoadingFogSeconds);
        SetFog();
    }
}
