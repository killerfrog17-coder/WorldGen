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
    TerrainDetailOptions detailOptions=new TerrainDetailOptions();
    TerrainDetailEstimator detailEstimator;
    Task<Dictionary<TerrainTileKey,int>> detailPlan;
    CancellationTokenSource detailCancellation;
    int detailRevision=-1,detailGeneration,plannedPixels;float plannedFov;
    double detailX,detailZ,nextRequestAt,frameUploadMs,fogSafeRadius,streamedFog=300,fogCheckAt;
    bool detailDirty;
    TerrainCoverageFootprint fogFootprint;
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
    {get{int count=jobs.Count+scatterJobs.Count;foreach(var work in forestJobs)if(!work.Task.IsCompleted)count++;return count;}}
    bool CanInstall=>installationsThisFrame<detailOptions.Installations&&(installationsThisFrame==0||frameUploadMs<detailOptions.UploadBudgetMs);
    bool CanRequest=>Time.realtimeSinceStartupAsDouble>=nextRequestAt;
    void ResetDetailFrame()
    {
        detailCancellation?.Cancel();detailEstimator=new TerrainDetailEstimator(terrain,frame);
        detailDirty=true;backdrops.Clear();backdropRevision++;streamedFog=300;fogSafeRadius=0;fogCheckAt=0;fogFootprint=null;
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
            if(detailPlan.IsFaulted){error=detailPlan.Exception.GetBaseException().Message;debugOpen=true;}
            else if(!detailPlan.IsCanceled&&detailGeneration==generation&&Math.Abs(detailX-motion.X)<TerrainLod.ReplanDistance(HorizontalScale)&&Math.Abs(detailZ-motion.Z)<TerrainLod.ReplanDistance(HorizontalScale))
                AdoptDetailPlan(detailPlan.Result);
            detailPlan=null;detailCancellation?.Dispose();detailCancellation=null;
        }
        if(detailPlan!=null||!detailDirty)return;
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
        wanted=plan;wantedEdges.Clear();foreach(var p in TerrainEdges.Build(wanted))wantedEdges.Add(p.Key,p.Value);
        priority.Clear();priority.AddRange(wanted.Keys);
        foreach(var job in jobs)if(job.Prepared==null&&!job.Prefetch&&!job.Backdrop&&!WorkMatchesCurrent(job))job.Cancellation?.Cancel();
        priority.Sort((a,b)=>StreamingPriority(a).CompareTo(StreamingPriority(b)));fogCheckAt=0;
    }
    void PlanBackdrop()
    {
        backdrops.Clear();backdropRevision++;int root=Math.Max(0,(int)Math.Floor(Math.Log(128/HorizontalScale,2)));
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
        if(detailBaseline){streamedFog=viewDistance*.98;SetFog();return;}
        double now=Time.realtimeSinceStartupAsDouble;
        if(now>=fogCheckAt)
        {
            fogCheckAt=now+.15;double limit=viewDistance*.98;
            if(fogFootprint==null)fogFootprint=new TerrainCoverageFootprint(tiles.Keys);
            double gap=fogFootprint.NearestGap(wanted.Keys,motion.X,motion.Z,limit/.94);
            double safe=Math.Min(limit,gap*.94);
            fogSafeRadius=Math.Max(100,safe);
        }
        // Only a real gap in the visible union can pull haze inward. A pending
        // replacement/edge rebuild/coarsening does not erase existing coverage.
        streamedFog=Math.Min(fogSafeRadius,streamedFog+Math.Max(3000,streamedFog*2)*Time.unscaledDeltaTime);
        SetFog();
    }
}
