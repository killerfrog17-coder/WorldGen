using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    [StructLayout(LayoutKind.Sequential)]
    struct ForestRoot
    {public Vector3 Position;public float Width,Height,Yaw,SlopeX,SlopeZ;public Vector3 Tint;public uint Field;public int CellX,CellY;public uint Face;public float Padding;}
    [StructLayout(LayoutKind.Sequential)]
    struct ForestHash {public int X,Z,Level,State;}
    sealed class ForestBatch
    {
        public ArtModel Model;public bool Macro;public V Pigment;public readonly List<ForestRoot> Data=new List<ForestRoot>();public readonly Stack<int> Free=new Stack<int>();
        public readonly List<int> GroundLevels=new List<int>();
        public GraphicsBuffer Roots,Visible,Args;public int Capacity;public bool Dirty=true,AllDirty=true,CullValid;public bool[] DirtyBlocks=Array.Empty<bool>();public Matrix4x4 CullMatrix;public Vector3 CullEye;public Vector4 CullChart,CullRange;public int CullRevision;
        public MaterialPropertyBlock Properties=new MaterialPropertyBlock();
        public void Dispose(){Roots?.Dispose();Visible?.Dispose();Args?.Dispose();CullValid=false;}
    }
    sealed class ForestField
    {
        public ForestPage Source;public int Id;public Bounds Bounds;public bool WasReady,Projecting;public float RetireAfter;
        public readonly Dictionary<ForestBatch,List<int>> Roots=new Dictionary<ForestBatch,List<int>>();
        public bool Ready=>!Projecting&&WasReady;
        public bool EvaluateReady(){if(Projecting)return false;foreach(var batch in Roots.Keys)if(!batch.Model.TreeBaked)return false;return true;}
    }
    sealed class ForestWork {public ForestPageKey Key;public Task<ForestPage> Task;public CancellationTokenSource Cancellation;}
    readonly Dictionary<ForestPageKey,ForestField> forestFields=new Dictionary<ForestPageKey,ForestField>();
    readonly Dictionary<int,ForestBatch> forestBatches=new Dictionary<int,ForestBatch>();
    public long ForestRootUploadBytes {get;private set;}
    public long ForestRootUploadCalls {get;private set;}
    public long ForestRootFullUploads {get;private set;}
    const int ForestUploadBlock=256;
    static void DirtyForestRoot(ForestBatch batch,int slot)
    {
        batch.Dirty=true;if(batch.AllDirty)return;
        int blocks=(Math.Max(batch.Data.Count,batch.Capacity)+ForestUploadBlock-1)/ForestUploadBlock;
        if(batch.DirtyBlocks.Length<blocks)Array.Resize(ref batch.DirtyBlocks,blocks);
        batch.DirtyBlocks[slot/ForestUploadBlock]=true;
    }
    static void DirtyAllForestRoots(ForestBatch batch){batch.Dirty=batch.AllDirty=true;}
    void UploadForestRoots(ForestBatch batch,bool forceFull=false)
    {
        if(!batch.Dirty||batch.Data.Count==0)return;
        bool marked=false;foreach(bool dirty in batch.DirtyBlocks)if(dirty){marked=true;break;}
        if(forceFull||!WorldGenPerformance.PartialForestUploads||batch.AllDirty||!marked)
        {batch.Roots.SetData(batch.Data,0,0,batch.Data.Count);ForestRootUploadCalls++;ForestRootFullUploads++;ForestRootUploadBytes+=(long)batch.Data.Count*64;}
        else
        {
            for(int block=0;block<batch.DirtyBlocks.Length;)
            {
                if(!batch.DirtyBlocks[block]){block++;continue;}
                int first=block++;while(block<batch.DirtyBlocks.Length&&batch.DirtyBlocks[block])block++;
                int start=first*ForestUploadBlock,count=Math.Min(batch.Data.Count,block*ForestUploadBlock)-start;
                if(count<=0)continue;
                batch.Roots.SetData(batch.Data,start,start,count);ForestRootUploadCalls++;ForestRootUploadBytes+=(long)count*64;
            }
        }
        Array.Clear(batch.DirtyBlocks,0,batch.DirtyBlocks.Length);batch.Dirty=batch.AllDirty=false;batch.CullValid=false;
    }
    ForestField forestInstalling;IEnumerator forestProjection;
    long forestProjectionSlices;double forestProjectionMaxMs;
    readonly List<ForestWork> forestJobs=new List<ForestWork>();readonly HashSet<ForestPageKey> forestWanted=new HashSet<ForestPageKey>();
    Comparison<ForestWork> forestCompletionComparison;
    readonly Dictionary<ForestPageKey,bool> forestOwnership=new Dictionary<ForestPageKey,bool>();int forestOwnershipRevision=-1,forestOwnershipEpoch;
    ForestPageKey[] forestPriority=Array.Empty<ForestPageKey>();Task<ForestPageKey[]> forestPlan;
    ForestPageKey[] flightForestPriority=Array.Empty<ForestPageKey>();
    readonly HashSet<ForestPageKey> flightForestWanted=new HashSet<ForestPageKey>();
    int flightForestNext;bool flightForestCoverage;
    readonly Stack<int> forestFreeIds=new Stack<int>();int forestNextId,forestRootCount,forestHashMask,forestRevision,forestRootTopologyRevision,forestOptions=-1,forestNextRequest;bool forestMaintenanceDirty;
    readonly int[] forestFlags=new int[8192];GraphicsBuffer forestFlagBuffer,forestHashBuffer;
    readonly float[] forestBirth=new float[8192];GraphicsBuffer forestBirthBuffer;
    void MarkForestBirth(ForestField field)
    {
        if(field.Source.Key.Level!=0||forestBirth[field.Id]!=0)return;
        var parent=field.Source.Key.Parent;
        for(int level=1;level<=15;level++,parent=parent.Parent)
            if(forestFields.TryGetValue(parent,out var coarse)&&coarse.Ready){forestBirth[field.Id]=Time.realtimeSinceStartup;coarse.RetireAfter=Time.realtimeSinceStartup+.7f;}
    }
    ComputeShader forestCull;bool forestHashDirty;LocalTerrainFrame forestFrame;V forestPlanAt;double forestPlanRange=-1,forestPlanAltitude=-1,nextForestVisibility;int forestGroundRevision=-1,forestCoverageRevision=-1;
    readonly Vector4[] forestPlanes=new Vector4[6];readonly Plane[] forestPlaneWork=new Plane[6];Vector3 forestVisibilityEye=new Vector3(float.NaN,0,0);
    ForestCoverageFootprint forestFootprint;int forestFootprintEpoch=-1;
    bool forestCoveragePlanned,forestUnavailable;V forestCoverageAt;double forestCoverageRange,forestRetryAt;
    readonly Dictionary<ForestPageKey,ForestRevealBounds> forestRevealBounds=new Dictionary<ForestPageKey,ForestRevealBounds>();
    struct ForestRevealBounds {public V Center;public double Radius;}
    bool TreesNeededForReveal=>ScatterEnabled&&TerrainVideoOptions.GeneratedTrees&&TerrainVideoOptions.TreeDensity>0;
    bool ForestNeededForReveal=>TreesNeededForReveal&&TerrainVideoOptions.TreeRebuild&&TerrainVideoOptions.TreeImpostors&&TerrainVideoOptions.FarForest&&SystemInfo.supportsComputeShaders&&!forestUnavailable;
    double ForestGapDistance(ForestPageKey key)
    {
        if(!forestRevealBounds.TryGetValue(key,out var bounds))
        {scatter.ForestBounds(key,out bounds.Center,out bounds.Radius);forestRevealBounds.Add(key,bounds);}
        return Math.Max(0,(bounds.Center-Position).Length*frame.RadiusM-bounds.Radius);
    }
    double TreeRevealDistance(double limit)
    {
        if(!TreesNeededForReveal)return limit;
        double gap=limit/.94,readyLimit=limit,localRange=Math.Min(TerrainVideoOptions.TreeIdentityRange,ForestNeededForReveal?ForestHandoffReach:1800);
        foreach(var pair in scatterRevealBounds)
        {
            if(scatterChunks.TryGetValue(pair.Key,out var ready)&&(!ScatterNeedsTrees(pair.Key)||ready.TreesIncluded))continue;
            double distance=Math.Max(0,(pair.Value.Center-Position).Length*frame.RadiusM-pair.Value.Radius);
            if(distance<localRange)gap=Math.Min(gap,distance);
        }
        if(ForestNeededForReveal)
        {
            if(!forestCoveragePlanned)return 100;
            if(forestFootprint==null||forestFootprintEpoch!=forestOwnershipEpoch)
            {
                forestFootprint=new ForestCoverageFootprint(forestFields.Where(p=>p.Value.Ready).Select(p=>p.Key),scatter.TreeLatticeCount);
                forestFootprintEpoch=forestOwnershipEpoch;
            }
            gap=forestFootprint.NearestGap(forestPriority,ForestGapDistance,gap);
            // The old plan remains useful while a moved-camera plan is in flight,
            // but cannot certify newly exposed space beyond its original range.
            readyLimit=Math.Min(readyLimit,Math.Max(0,forestCoverageRange-(Position-forestCoverageAt).Length*frame.RadiusM));
        }
        return Math.Min(readyLimit,gap*.94);
    }
    public int ForestFieldCount=>forestFields.Count;
    public int ForestPacketCount=>forestRootCount;
    public int ForestDrawCalls {get;private set;}
    // CPU instancing -> identical GPU individual impostors, NOT tree retirement.
    // The hierarchy continues exact roots past this handoff until projected
    // node error justifies aggregation. Do not spend a 1.4km CPU buffer twice.
    double LegacyForestReach=>Math.Min(TerrainVideoOptions.TreeIdentityRange,Math.Max(350,TerrainVideoOptions.TreeFarLod*2.7));
    bool residentForestReady;int residentForestEpoch=-1;double nextResidentForestCheck;V residentForestCheckAt;
    double ForestHandoffReach=>residentForestReady&&TerrainVideoOptions.EfficientTreeRendering&&!WorldGenPerformance.LegacyTreeResident?
        Math.Min(LegacyForestReach,Math.Max(120,TerrainVideoOptions.TreeFarLod*1.8)):LegacyForestReach;
    public bool ForestPending=>forestPlan!=null||forestJobs.Count>0||forestPriority.Any(k=>!forestFields.ContainsKey(k))||forestFields.Values.Any(f=>!f.Ready);
    static uint ForestMix(uint x){x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16);}
    static uint ForestKeyHash(int x,int z,int level,int face=0)=>ForestMix(unchecked((uint)x*73856093u^(uint)z*19349663u^(uint)level*83492791u^(uint)face*15485863u));
    ArtModel ForestArt(int code,bool macro=false)
    {
        code=TreeFlora.RenderCode(code);long key=ScatterArtKey(ScatterKind.GeneratedTree,code,2)|(1L<<49);
        if(scatterArt.TryGetValue(key,out var model))return model;
        model=CreateRebuiltTree(key,code,2);model.TreeStand=true;return model;
    }
    bool ForestOwnsTree(in ScatterInstance tree)
    {
        int side=LocalScatter.TreeCandidateSide,gx=(int)((tree.Id>>40)&0xfffff)*side+(int)(tree.Id&65535)%side,gy=(int)((tree.Id>>20)&0xfffff)*side+(int)(tree.Id&65535)/side;
        var key=new ForestPageKey((int)(tree.Id>>61),gx/LocalScatter.ForestPageSide,gy/LocalScatter.ForestPageSide,0);
        if(forestOwnershipRevision!=forestOwnershipEpoch){forestOwnership.Clear();forestOwnershipRevision=forestOwnershipEpoch;}
        if(!WorldGenPerformance.ForceFreshForestOwnership&&forestOwnership.TryGetValue(key,out bool owned))return owned;
        var query=key;for(int level=0;level<=24;level++,query=query.Parent)if(forestFields.TryGetValue(query,out var field)&&field.Ready){if(!scatterParallelCollecting)forestOwnership[key]=true;return true;}
        if(!scatterParallelCollecting)forestOwnership[key]=false;return false;
    }
    bool ForestOwnsGroup(ScatterChunk chunk,ScatterGroup group)
    {
        if(group.ForestEpoch==forestOwnershipEpoch)return group.ForestOwned;
        group.ForestEpoch=forestOwnershipEpoch;group.ForestOwned=false;
        foreach(int index in group.Indices)if(!ForestOwnsTree(chunk.Source.Instances[index]))return false;
        group.ForestOwned=true;return true;
    }
    void UpdateForestFields()
    {
        if(!TerrainVideoOptions.GeneratedTrees||!TerrainVideoOptions.TreeRebuild||!TerrainVideoOptions.TreeImpostors||!TerrainVideoOptions.FarForest||!Ready||!CenterReady)return;
        if(!SystemInfo.supportsComputeShaders)return;
        if(forestUnavailable)return;
        if(forestCull==null){forestCull=Resources.Load<ComputeShader>("WorldGenForestCull");if(forestCull==null){forestUnavailable=true;Debug.LogWarning("Forest compute shader missing; using local tree coverage.");return;}forestFlagBuffer=new GraphicsBuffer(GraphicsBuffer.Target.Structured,forestFlags.Length,4);forestBirthBuffer=new GraphicsBuffer(GraphicsBuffer.Target.Structured,forestBirth.Length,4);}
        foreach(var batch in forestBatches.Values)batch.Model.LastUsed=Time.realtimeSinceStartupAsDouble;
        if(!ReferenceEquals(forestFrame,frame))
        {
            long phase=WorldGenPerformance.TraceBegin();
            // Reproject immutable planet-addressed payloads; never reseed a forest
            // because its terrain chart was replaced.
            if(forestInstalling!=null)RemoveForestField(forestInstalling.Source.Key);
            forestFrame=frame;foreach(var batch in forestBatches.Values){batch.Data.Clear();batch.GroundLevels.Clear();batch.Free.Clear();DirtyAllForestRoots(batch);}
            forestRootCount=0;foreach(var field in forestFields.Values){field.Roots.Clear();ProjectForest(field);}forestRevision++;forestHashDirty=true;
            WorldGenPerformance.TraceEnd(58,phase);
        }
        if(forestPlan!=null&&forestPlan.IsCompleted)
        {
            long phase=WorldGenPerformance.TraceBegin();
            if(forestPlan.IsCompletedSuccessfully)
            {
                forestPriority=forestPlan.Result;forestWanted.Clear();foreach(var key in forestPriority)forestWanted.Add(key);forestNextRequest=0;forestMaintenanceDirty=true;
                flightForestCoverage=motion.Flying&&HeightAboveGround>80;
                flightForestPriority=flightForestCoverage?FlightForestParents(forestPriority):Array.Empty<ForestPageKey>();flightForestNext=0;
                flightForestWanted.Clear();foreach(var key in flightForestPriority)flightForestWanted.Add(key);
                forestCoveragePlanned=true;forestCoverageAt=forestPlanAt;forestCoverageRange=forestPlanRange;forestRevealBounds.Clear();fogCheckAt=0;
                foreach(var work in forestJobs)if(!forestWanted.Contains(work.Key)&&!flightForestWanted.Contains(work.Key))work.Cancellation.Cancel();
                if(forestInstalling!=null&&!forestWanted.Contains(forestInstalling.Source.Key)&&!flightForestWanted.Contains(forestInstalling.Source.Key))
                {RemoveForestField(forestInstalling.Source.Key);forestNextRequest=flightForestNext=0;}
            }
            else if(forestPlan.IsFaulted)Debug.LogWarning("Forest plan: "+forestPlan.Exception.GetBaseException().Message);
            forestPlan=null;
            WorldGenPerformance.TraceEnd(55,phase);
        }
        double moved=(Position-forestPlanAt).Length*frame.RadiusM;
        if(forestPlan==null&&(forestPlanRange!=viewDistance||moved>128||Math.Abs(HeightAboveGround-forestPlanAltitude)>128||forestOptions!=TerrainVideoOptions.VegetationRevision||flightForestCoverage!=(motion.Flying&&HeightAboveGround>80)))
        {
            if(forestOptions!=TerrainVideoOptions.VegetationRevision)nextForestVisibility=0;
            forestPlanAt=Position;forestPlanRange=viewDistance;forestPlanAltitude=HeightAboveGround;forestOptions=TerrainVideoOptions.VegetationRevision;
            // Preserve the old exact-census radius. Only submission changes;
            // moving the GPU handoff must not coarsen the forest population.
            var at=Position;double range=viewDistance,exact=Math.Max(384,TerrainVideoOptions.TreeFarLod*2+128),inner=0,altitude=Math.Max(0,HeightAboveGround);var source=scatter;
            forestPlan=Task.Run(()=>source.PlanForest(at,range,exact,4096,inner,altitude),cancellation.Token);
        }
        if(forestHashDirty)foreach(var field in forestFields.Values)
        {bool ready=field.EvaluateReady();if(ready!=field.WasReady){field.WasReady=ready;if(ready)MarkForestBirth(field);scatterDirty=true;forestRevision++;forestOwnershipEpoch++;forestMaintenanceDirty=true;}}
        // The reverse removal loop used to install the newest completed page
        // first. A stream of quick far pages could indefinitely bypass the
        // oldest nearby page and keep the reveal disk at zero.
        if(!legacyLoader&&forestJobs.Count>1)
        {
            forestCompletionComparison??=(a,b)=>ForestGapDistance(b.Key).CompareTo(ForestGapDistance(a.Key));
            forestJobs.Sort(forestCompletionComparison);
        }
        bool projected=false;
        if(forestProjection!=null&&CanInstall){AdvanceForestProjection();projected=true;}
        int emptyInstalls=0;
        for(int i=forestJobs.Count-1;i>=0;i--)
        {
            var job=forestJobs[i];if(!job.Task.IsCompleted)continue;
            bool metadataOnly=!legacyLoader&&job.Task.IsCompletedSuccessfully&&job.Task.Result.Roots.Length==0;
            // A completed zero-root page certifies an empty census. It does
            // not upload objects or correct ground, so terrain publication
            // must not block it and occupy both forest request slots.
            if(job.Task.IsCompletedSuccessfully&&(forestWanted.Contains(job.Key)||flightForestWanted.Contains(job.Key))&&!metadataOnly&&(!CanInstall||forestProjection!=null||projected))continue;
            forestJobs.RemoveAt(i);job.Cancellation.Dispose();
            if(job.Task.IsCanceled){forestNextRequest=flightForestNext=0;continue;} // Retry interrupted coverage after near-ground pressure ends.
            if(job.Task.IsFaulted){Debug.LogWarning("Forest page retained fallback: "+job.Task.Exception.GetBaseException().Message);forestNextRequest=flightForestNext=0;forestRetryAt=Time.realtimeSinceStartupAsDouble+2;continue;}
            if(!job.Task.IsCompletedSuccessfully||!forestWanted.Contains(job.Key)&&!flightForestWanted.Contains(job.Key))continue;
            if(forestFreeIds.Count==0&&forestNextId>=forestFlags.Length)break;
            long installTrace=WorldGenPerformance.TraceBegin();long installed=installTrace!=0?installTrace:System.Diagnostics.Stopwatch.GetTimestamp();
            var field=new ForestField{Source=job.Task.Result,Id=forestFreeIds.Count>0?forestFreeIds.Pop():forestNextId++,Projecting=!metadataOnly};
            forestFlags[field.Id]=0;forestBirth[field.Id]=0;forestFields.Add(job.Key,field);forestHashDirty=true;
            if(metadataOnly){ProjectForest(field);ForestProjectionChanged();}
            else{forestInstalling=field;forestProjection=ProjectForestSteps(field);}
            WorldGenPerformance.TraceEnd(56,installTrace);
            frameUploadMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-installed)*1000d/System.Diagnostics.Stopwatch.Frequency;
            if(!metadataOnly){AdvanceForestProjection();projected=true;}
            // Empty pages only certify a tree-free part of the census. They
            // create no tree models, transforms or GPU roots; do not charge
            // an entire object-upload frame for each of thousands of them.
            if(!metadataOnly||++emptyInstalls>=8||frameUploadMs>=detailOptions.UploadBudgetMs)break;
        }
        if(forestPlan==null&&forestMaintenanceDirty&&!GroundPublicationPending)
        {
        long phase=WorldGenPerformance.TraceBegin();
        forestMaintenanceDirty=false;
        foreach(var key in forestFields.Keys.Where(k=>!forestWanted.Contains(k)).ToArray())
        {
            var f=forestFields[key];scatter.ForestBounds(key,out V center,out double radius);
            if((center-Position).Length*frame.RadiusM-radius>viewDistance+512||(center-Position).Length*frame.RadiusM+radius<VegetationQuality.ForestStart(ForestHandoffReach)-128||ForestReplacementReady(key))RemoveForestField(key);
        }
        WorldGenPerformance.TraceEnd(57,phase);
        }
        UpdateResidentForestReadiness();
    }
    void UpdateResidentForestReadiness()
    {
        double now=Time.realtimeSinceStartupAsDouble;
        if(residentForestEpoch==forestOwnershipEpoch&&now<nextResidentForestCheck&&(Position-residentForestCheckAt).Length*frame.RadiusM<32)return;
        residentForestEpoch=forestOwnershipEpoch;nextResidentForestCheck=now+.2;residentForestCheckAt=Position;
        // A previously loaded coarse parent must never retire a close exact
        // CPU tree. Move submission closer only after ALL nearby level-zero
        // pages (including empty ones) and their actual-tree bakes are ready.
        bool ready=ForestNeededForReveal&&forestCoveragePlanned&&(Position-forestCoverageAt).Length*frame.RadiusM<=128;
        if(ready)foreach(var key in forestPriority)
        {
            if(key.Level!=0||ForestGapDistance(key)>LegacyForestReach+64)continue;
            if(!forestFields.TryGetValue(key,out var field)||!field.Ready){ready=false;break;}
        }
        if(residentForestReady!=ready){residentForestReady=ready;scatterDirty=true;}
    }
    bool ScheduleForestWork(bool coveredFlight=false)
    {
        // Near ground retains priority. When forest coverage is the reveal
        // blocker, it also gets a turn before distant terrain fills every lane.
        if(!TerrainVideoOptions.GeneratedTrees||!TerrainVideoOptions.TreeRebuild||!TerrainVideoOptions.TreeImpostors||!TerrainVideoOptions.FarForest||!Ready||!CenterReady||forestCull==null||!coveredFlight&&NearTerrainPending()||mapOpen||Time.realtimeSinceStartupAsDouble<forestRetryAt||RunningWorkerCount>=WorkerLimit||forestJobs.Count>=Math.Min(2,WorkerLimit)||forestRootCount>=524288)return false;
        while(flightForestNext<flightForestPriority.Length||forestNextRequest<forestPriority.Length)
        {
            // Merge the two existing queues instead of draining every flight
            // parent first. Nearby exact pages must get their turn even when
            // a large coarse parent's bounds also contain the camera.
            while(forestNextRequest<forestPriority.Length&&
                (forestFields.ContainsKey(forestPriority[forestNextRequest])||forestJobs.Any(j=>j.Key.Equals(forestPriority[forestNextRequest]))))forestNextRequest++;
            while(flightForestNext<flightForestPriority.Length&&
                (forestFields.ContainsKey(flightForestPriority[flightForestNext])||forestJobs.Any(j=>j.Key.Equals(flightForestPriority[flightForestNext]))||ForestReplacementReady(flightForestPriority[flightForestNext])))flightForestNext++;
            bool leaf=forestNextRequest<forestPriority.Length,parent=flightForestNext<flightForestPriority.Length;
            if(!leaf&&!parent)break;
            bool coverage=parent;
            if(leaf&&parent)
            {
                double near=ForestGapDistance(forestPriority[forestNextRequest]);
                coverage=near>LegacyForestReach+64&&ForestGapDistance(flightForestPriority[flightForestNext])<near;
            }
            var key=coverage?flightForestPriority[flightForestNext++]:forestPriority[forestNextRequest++];
            var source=scatter;var producer=meshProcess;var stop=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);var token=stop.Token;
            forestJobs.Add(new ForestWork{Key=key,Cancellation=stop,Task=Task.Run(()=>producer==null?source.BuildForest(key,token):producer.Forest(key,token),token)});return true;
        }
        return false;
    }
    ForestPageKey[] FlightForestParents(IEnumerable<ForestPageKey> leaves)
    {
        // Reuse the existing real-tree stand representation, not a second
        // planting rule or extra tree models. A roughly 2km page supplies a
        // cheap continuous canopy while its exact descendants stream in.
        int level=Math.Max(0,(int)Math.Floor(Math.Log(2048/(LocalScatter.ForestPageSide*scatter.TreeLatticeStep),2)));
        var parents=new HashSet<ForestPageKey>();
        foreach(var leaf in leaves)
        {
            if(leaf.Level>=level)continue;var key=leaf;
            while(key.Level<level)key=key.Parent;parents.Add(key);
        }
        return parents.OrderBy(ForestGapDistance).ToArray();
    }
    bool ForestReplacementReady(ForestPageKey key)
    {
        // Keep the statistical parent until its close exact descendants have
        // completed their complementary crossfade.
        if(forestFields.TryGetValue(key,out var fading)&&Time.realtimeSinceStartup<fading.RetireAfter){forestMaintenanceDirty=true;return false;}
        var parent=key.Parent;for(int level=key.Level+1;level<=24;level++,parent=parent.Parent)
            if(forestWanted.Contains(parent)&&forestFields.TryGetValue(parent,out var field)&&field.Ready)return true;
        if(key.Level==0)return false;
        for(int y=0;y<2;y++)for(int x=0;x<2;x++)
        {
            var child=new ForestPageKey(key.Face,key.X*2+x,key.Y*2+y,key.Level-1);
            if(forestFields.TryGetValue(child,out var field)&&field.Ready)continue;
            if(!ForestReplacementReadyDescendants(child))return false;
        }
        return true;
    }
    bool ForestReplacementReadyDescendants(ForestPageKey key)
    {
        long size=(long)LocalScatter.ForestPageSide*(1L<<key.Level);if(key.X*size>=scatter.TreeLatticeCount||key.Y*size>=scatter.TreeLatticeCount)return true;
        if(forestFields.TryGetValue(key,out var field)&&field.Ready)return true;if(key.Level==0)return false;
        for(int y=0;y<2;y++)for(int x=0;x<2;x++)if(!ForestReplacementReadyDescendants(new ForestPageKey(key.Face,key.X*2+x,key.Y*2+y,key.Level-1)))return false;return true;
    }
    void ForestProjectionChanged()
    {forestHashDirty=true;forestRevision++;forestOwnershipEpoch++;scatterDirty=true;forestMaintenanceDirty=true;}
    void ClearForestProjection()
    {
        (forestProjection as IDisposable)?.Dispose();forestProjection=null;forestInstalling=null;
    }
    void AdvanceForestProjection()
    {
        long trace=WorldGenPerformance.TraceBegin(),started=System.Diagnostics.Stopwatch.GetTimestamp();
        installationsThisFrame++;
        try
        {
            if(!forestProjection.MoveNext()){ClearForestProjection();ForestProjectionChanged();}
        }
        catch{var key=forestInstalling.Source.Key;ClearForestProjection();RemoveForestField(key);throw;}
        finally
        {
            double elapsed=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency;
            frameUploadMs+=elapsed;forestProjectionSlices++;forestProjectionMaxMs=Math.Max(forestProjectionMaxMs,elapsed);
            WorldGenPerformance.TraceEnd(56,trace);
        }
    }
    void ProjectForest(ForestField field)
    {
        var steps=ProjectForestSteps(field);
        try{while(steps.MoveNext()){} }finally{(steps as IDisposable)?.Dispose();}
    }
    IEnumerator ProjectForestSteps(ForestField field)
    {
        field.Projecting=true;field.WasReady=false;
        bool first=true;var page=field.Source;int added=0;
        long started=System.Diagnostics.Stopwatch.GetTimestamp();
        foreach(var source in page.Roots)
        {
            var tree=source.Tree;bool macro=source.Population==0;int code=TreeFlora.RenderCode(tree.Variant),batchCode=code|(macro?int.MinValue:0);
            if(!forestBatches.TryGetValue(batchCode,out var batch)){batch=new ForestBatch{Model=ForestArt(code,macro),Macro=macro,Pigment=new TreeMorphology(code).Leaf};forestBatches.Add(batchCode,batch);}
            double dot=Math.Max(.1,V.Dot(tree.Position,frame.Origin)),px=V.Dot(tree.Position,frame.East)*frame.RadiusM/dot,pz=V.Dot(tree.Position,frame.North)*frame.RadiusM/dot;
            float height=(float)frame.SurfaceY(tree.Position,tree.GroundM);
            var pigment=TreePigment(tree.Variant);var shared=batch.Pigment;
            int candidateSide=LocalScatter.TreeCandidateSide,slotId=(int)(tree.Id&65535),cellX=(int)((tree.Id>>40)&0xfffff)*candidateSide+slotId%candidateSide,cellY=(int)((tree.Id>>20)&0xfffff)*candidateSide+slotId/candidateSide;
            uint identity=(uint)tree.Id^Rng.Mix((uint)(tree.Id>>32));
            var random=new Rng(unchecked((int)identity),"regional-render-roots-1");
            int crowns=macro?LocalScatter.ForestCanopyTrees:1;
            for(int crown=0;crown<crowns;crown++)
            {
                double x=px,z=pz;float cohort=1;int face=page.Key.Face,gx=cellX,gy=cellY;
                if(macro)
                {
                    // Stratified, independently jittered physical trees. Every
                    // root uses the resident ground beneath it; a single plane
                    // across a packet leaves trees suspended beside cliffs.
                    double span=source.CrownScale;
                    x+=((crown%4+random.Unit())/4-.5)*span;
                    z+=((crown/4+random.Unit())/4-.5)*span;
                    cohort=(float)(.62+.46*random.Unit());
                    scatter.ForestCoordinates(frame.Position(x,z),out face,out gx,out gy);
                }
                uint densityHash=Rng.Mix(identity^unchecked((uint)crown*0x9e3779b9u)^0x9a65c4u)&0xffffffu;
                var root=new ForestRoot{Position=new Vector3((float)x,height-.045f,(float)z),Width=(float)(tree.WidthM*(macro?cohort:source.CrownScale)),Height=(float)tree.HeightM*cohort,Yaw=macro?(float)(random.Unit()*Math.PI*2):(float)(tree.YawDegrees*Math.PI/180),Field=(uint)field.Id|((uint)page.Key.Level<<16),CellX=gx,CellY=gy,Face=(uint)face|(densityHash<<8),
                    Tint=macro?new Vector3((float)(tree.Color.X/Math.Max(.01,shared.X)),(float)(tree.Color.Y/Math.Max(.01,shared.Y)),(float)(tree.Color.Z/Math.Max(.01,shared.Z))):new Vector3((float)(tree.Color.X*pigment.X/Math.Max(.01,shared.X)),(float)(tree.Color.Y*pigment.Y/Math.Max(.01,shared.Y)),(float)(tree.Color.Z*pigment.Z/Math.Max(.01,shared.Z))),Padding=macro?2:source.CrownScale>1.001?1:0};
                ProjectForestRootGround(ref root,out int groundLevel);
                int slot;if(batch.Free.Count>0){slot=batch.Free.Pop();batch.Data[slot]=root;batch.GroundLevels[slot]=groundLevel;}else{slot=batch.Data.Count;batch.Data.Add(root);batch.GroundLevels.Add(groundLevel);}DirtyForestRoot(batch,slot);forestRootCount++;
                if(!field.Roots.TryGetValue(batch,out var slots)){slots=new List<int>();field.Roots.Add(batch,slots);}slots.Add(slot);
                var bounds=ForestRootBounds(root);if(first){field.Bounds=bounds;first=false;}else field.Bounds.Encapsulate(bounds);
                // A partial page remains physically represented for exact ground
                // correction, but owns/draws nothing until its final root exists.
                // Bump topology before every yield so staged ground plans cannot
                // resume stale slot-list enumerators after this slice adds roots.
                added++;
                if(added%16==0&&(added>=4096||System.Diagnostics.Stopwatch.GetTimestamp()-started>=System.Diagnostics.Stopwatch.Frequency*.0007))
                {forestRootTopologyRevision++;added=0;yield return null;started=System.Diagnostics.Stopwatch.GetTimestamp();}
            }
        }
        if(added>0)forestRootTopologyRevision++;
        field.Projecting=false;field.WasReady=field.EvaluateReady();
        if(field.WasReady)MarkForestBirth(field);
    }
    void ProjectForestGround()
    {
        long trace=WorldGenPerformance.TraceBegin();
        foreach(var field in forestFields.Values)
        {
            bool affected=scatterGroundChanges.Count==0;
            foreach(var key in scatterGroundChanges)
            {
                var b=field.Bounds;double x=key.X*key.SizeM,z=key.Z*key.SizeM;
                if(b.max.x<x||b.min.x>x+key.SizeM||b.max.z<z||b.min.z>z+key.SizeM)continue;
                // This square encloses every root and its query margin. A
                // partial edge mask can reject the whole field before walking
                // thousands of unchanged interior roots; full-key masks keep
                // the existing conservative overlap behavior.
                if(scatterGroundChanges.Affects(key,b.center.x,b.center.z,Math.Max(b.extents.x,b.extents.z)+.01)){affected=true;break;}
            }
            if(!affected)continue;
            foreach(var pair in field.Roots)foreach(int slot in pair.Value)
            {
                var root=pair.Key.Data[slot];
                // Each render root depends only on the ground beneath it.
                double margin=.01;
                if(!ChangedGroundAt(root.Position.x,root.Position.z,margin,pair.Key.GroundLevels[slot]))continue;
                var before=root;ProjectForestRootGround(ref root,out int groundLevel);pair.Key.GroundLevels[slot]=groundLevel;
                if(root.Position.y==before.Position.y&&root.SlopeX==before.SlopeX&&root.SlopeZ==before.SlopeZ&&root.Padding==before.Padding)continue;pair.Key.Data[slot]=root;DirtyForestRoot(pair.Key,slot);
                field.Bounds.Encapsulate(ForestRootBounds(root));
            }
        }
        forestRevision++;nextForestVisibility=0;
        WorldGenPerformance.TraceEnd(31,trace);
    }
    void ProjectForestRootGround(ref ForestRoot root,out int groundLevel)
    {
        double x=root.Position.x,z=root.Position.z,y=VisibleGround(x,z,root.Position.y+.045f,out groundLevel);
        Tile tile=null;TerrainTileKey key=default;
        if(Math.Abs(root.Padding)>1.5f&&groundLevel<17)
        {
            double size=TerrainLod.BaseSizeM*HorizontalScale*(1<<groundLevel);
            key=new TerrainTileKey((int)Math.Floor(x/size),(int)Math.Floor(z/size),groundLevel,HorizontalScale);
            tiles.TryGetValue(key,out tile);
        }
        ApplyForestRootGround(ref root,y,groundLevel,tile,key);
    }
    static void ApplyForestRootGround(ref ForestRoot root,double y,int groundLevel,Tile tile,TerrainTileKey key)
    {
        root.Position.y=(float)y-.045f;
        bool macro=Math.Abs(root.Padding)>1.5f;
        float footprint=macro?Math.Max(2,Math.Abs(root.Padding)):0;
        root.Padding=groundLevel>=17?(macro?-footprint:-1):(macro?footprint:0);
        if(macro&&groundLevel<17)
        {
            // Terrain already carries ecological admission, including water,
            // exposed cliffs and treeline. Read that grid instead of running
            // more exact tree/soil/height queries for a distant preview.
            if(tile!=null&&tile.ForestCover.IsCreated)
            {
                double x=root.Position.x,z=root.Position.z;
                int side=tile.Segments+1,gx=Mathf.Clamp((int)Math.Round((x/key.SizeM-key.X)*tile.Segments),0,tile.Segments),gz=Mathf.Clamp((int)Math.Round((z/key.SizeM-key.Z)*tile.Segments),0,tile.Segments);
                if(tile.ForestCover[(gz*side+gx)*8+3]<=.001f)root.Padding=-footprint;
            }
        }
        // Trees remain upright. Terrain affects their root position, never
        // stretches or shears the silhouette.
        root.SlopeX=root.SlopeZ=0;
    }
    static Bounds ForestRootBounds(ForestRoot root)
    {
        float span=Math.Max(root.Width,Math.Abs(root.Padding));
        return new Bounds(root.Position+Vector3.up*root.Height*.5f,
            new Vector3(span*3,root.Height*3+span*3*(Math.Abs(root.SlopeX)+Math.Abs(root.SlopeZ)),span*3));
    }
    void RemoveForestField(ForestPageKey key)
    {
        var field=forestFields[key];if(ReferenceEquals(field,forestInstalling))ClearForestProjection();if(field.Roots.Count>0)forestRootTopologyRevision++;
        foreach(var pair in field.Roots)foreach(int slot in pair.Value){pair.Key.Data[slot]=default;pair.Key.Free.Push(slot);DirtyForestRoot(pair.Key,slot);forestRootCount--;}
        forestFlags[field.Id]=0;forestBirth[field.Id]=0;forestFreeIds.Push(field.Id);forestFields.Remove(key);forestHashDirty=true;forestRevision++;forestOwnershipEpoch++;scatterDirty=true;
    }
    void CompactForest(ForestBatch batch)
    {
        if(batch.Free.Count<256||batch.Free.Count<batch.Data.Count/3)return;
        forestRootTopologyRevision++;
        var map=new int[batch.Data.Count];int live=0;for(int i=0;i<batch.Data.Count;i++)if(batch.Data[i].Height>0){map[i]=live;batch.Data[live]=batch.Data[i];batch.GroundLevels[live++]=batch.GroundLevels[i];}
        batch.GroundLevels.RemoveRange(live,batch.Data.Count-live);batch.Data.RemoveRange(live,batch.Data.Count-live);batch.Free.Clear();DirtyAllForestRoots(batch);
        foreach(var field in forestFields.Values)if(field.Roots.TryGetValue(batch,out var slots))for(int i=0;i<slots.Count;i++)slots[i]=map[slots[i]];
    }
    void PrepareForestHash()
    {
        if(!forestHashDirty)return;forestHashDirty=false;int count=16;while(count<forestFields.Count*4)count*=2;
        var hash=new ForestHash[count];forestHashMask=count-1;
        foreach(var pair in forestFields)
        {
            if(!pair.Value.Ready)continue;var key=pair.Key;int slot=(int)(ForestKeyHash(key.X,key.Y,key.Level,key.Face)&(uint)forestHashMask);
            while(hash[slot].State!=0)slot=(slot+1)&forestHashMask;hash[slot]=new ForestHash{X=key.X,Z=key.Y,Level=key.Level,State=((pair.Value.Id+1)<<3)|(key.Face+1)};
        }
        if(forestHashBuffer==null||forestHashBuffer.count!=count){forestHashBuffer?.Dispose();forestHashBuffer=new GraphicsBuffer(GraphicsBuffer.Target.Structured,count,16);}
        forestHashBuffer.SetData(hash);forestBirthBuffer.SetData(forestBirth);forestRevision++;nextForestVisibility=0;
    }
    void DrawForestFields()
    {
        ForestDrawCalls=0;if(TerrainDiagnostics.HidePlants||TerrainDiagnostics.HideTrees||WorldGenPerformance.SuppressFarForest||!TerrainVideoOptions.GeneratedTrees||!TerrainVideoOptions.TreeRebuild||!TerrainVideoOptions.TreeImpostors||!TerrainVideoOptions.FarForest||forestCull==null||forestFlagBuffer==null||view==null||forestFields.Count==0)return;
        PrepareForestHash();var eye=view.transform.localPosition;
        if(Time.realtimeSinceStartupAsDouble>=nextForestVisibility&&(!eye.Equals(forestVisibilityEye)||float.IsNaN(forestVisibilityEye.x)||nextForestVisibility==0||forestGroundRevision!=residentRevision||forestCoverageRevision!=coverVisibilityRevision))
        {
            nextForestVisibility=Time.realtimeSinceStartupAsDouble+.20;forestVisibilityEye=eye;forestGroundRevision=residentRevision;forestCoverageRevision=coverVisibilityRevision;
            foreach(var field in forestFields.Values)
            {
                // Empty ready pages still own/suppress coarser coverage in the
                // hash. They have no draw records and need no hill queries.
                if(field.Source.Roots.Length==0){forestFlags[field.Id]=0;continue;}
                var bounds=field.Bounds;bounds.center-=new Vector3((float)originX,0,(float)originZ);bounds.Expand(12+(bounds.center-eye).magnitude*.04f);forestFlags[field.Id]=field.Ready&&!TerrainOccluded(bounds,eye)?1:0;
            }
            forestFlagBuffer.SetData(forestFlags);forestRevision++;
        }
        GeometryUtility.CalculateFrustumPlanes(view,forestPlaneWork);for(int i=0;i<6;i++)forestPlanes[i]=new Vector4(forestPlaneWork[i].normal.x,forestPlaneWork[i].normal.y,forestPlaneWork[i].normal.z,forestPlaneWork[i].distance);
        float reach=(float)ForestHandoffReach,start=(float)VegetationQuality.ForestStart(reach);var chart=new Vector4((float)originX,0,(float)originZ,TerrainVideoOptions.TreeDensity);var ranges=new Vector4(start,reach,(float)viewDistance,forestHashMask);var matrix=view.projectionMatrix*view.worldToCameraMatrix;
        forestCull.SetVectorArray("_Planes",forestPlanes);forestCull.SetVector("_Eye",eye);forestCull.SetVector("_Chart",chart);forestCull.SetVector("_Ranges",ranges);forestCull.SetBuffer(0,"_Fields",forestFlagBuffer);forestCull.SetBuffer(0,"_Tiles",forestHashBuffer);
        forestCull.SetBuffer(0,"_ForestBirth",forestBirthBuffer);forestCull.SetFloat("_ForestClock",Time.realtimeSinceStartup);
        foreach(var batch in forestBatches.Values)
        {
            var model=batch.Model;if(!model.TreeBaked||batch.Data.Count==0)continue;CompactForest(batch);
            // Compaction can remove the final dead slots. Never dispatch zero
            // groups or reuse an earlier indirect instance count for that batch.
            if(batch.Data.Count==0){batch.CullValid=false;continue;}
            if(batch.Capacity<batch.Data.Count)
            {
                batch.Dispose();batch.Capacity=Mathf.NextPowerOfTwo(Math.Max(64,batch.Data.Count));batch.Roots=new GraphicsBuffer(GraphicsBuffer.Target.Structured,batch.Capacity,64);batch.Visible=new GraphicsBuffer(GraphicsBuffer.Target.Append,batch.Capacity,4);batch.Args=new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,1,GraphicsBuffer.IndirectDrawArgs.size);
                batch.Args.SetData(new[]{new GraphicsBuffer.IndirectDrawArgs{vertexCountPerInstance=6u}});DirtyAllForestRoots(batch);
            }
            UploadForestRoots(batch);
            if(!batch.CullValid||!batch.CullMatrix.Equals(matrix)||!batch.CullEye.Equals(eye)||!batch.CullChart.Equals(chart)||!batch.CullRange.Equals(ranges)||batch.CullRevision!=forestRevision)
            {
                batch.Visible.SetCounterValue(0);forestCull.SetInt("_RootCount",batch.Data.Count);forestCull.SetBuffer(0,"_Roots",batch.Roots);forestCull.SetBuffer(0,"_Visible",batch.Visible);forestCull.Dispatch(0,(batch.Data.Count+63)/64,1,1);GraphicsBuffer.CopyCount(batch.Visible,batch.Args,4);
                batch.CullValid=true;batch.CullMatrix=matrix;batch.CullEye=eye;batch.CullChart=chart;batch.CullRange=ranges;batch.CullRevision=forestRevision;
            }
            var props=batch.Properties;props.SetFloat("_ForestMacro",batch.Macro?1:0);props.SetBuffer("_Roots",batch.Roots);props.SetBuffer("_Visible",batch.Visible);props.SetVector("_ForestChart",chart);props.SetVector("_ForestRange",new Vector4(start,reach,0,0));
            props.SetBuffer("_ForestBirth",forestBirthBuffer);props.SetBuffer("_Tiles",forestHashBuffer);props.SetFloat("_ForestClock",Time.realtimeSinceStartup);props.SetInt("_ForestHashMask",forestHashMask);
            var parameters=new RenderParams(model.Parts[0].Material){camera=view,layer=30,worldBounds=new Bounds(eye,Vector3.one*(float)viewDistance*2.2f),matProps=props,shadowCastingMode=ShadowCastingMode.Off,receiveShadows=true};
            Graphics.RenderPrimitivesIndirect(in parameters,MeshTopology.Triangles,batch.Args,1);ForestDrawCalls++;
        }
    }
    void ResetForestFields()
    {
        ClearForestProjection();
        flightForestPriority=Array.Empty<ForestPageKey>();flightForestWanted.Clear();flightForestNext=0;flightForestCoverage=false;
        residentForestReady=false;residentForestEpoch=-1;nextResidentForestCheck=0;
        foreach(var work in forestJobs){work.Cancellation.Cancel();var stop=work.Cancellation;work.Task.ContinueWith(_=>stop.Dispose(),TaskScheduler.Default);}forestJobs.Clear();
        foreach(var batch in forestBatches.Values)batch.Dispose();forestBatches.Clear();forestFields.Clear();forestFreeIds.Clear();forestRootCount=forestNextId=0;
        forestFlagBuffer?.Dispose();forestHashBuffer?.Dispose();forestFlagBuffer=forestHashBuffer=null;forestCull=null;Array.Clear(forestFlags,0,forestFlags.Length);forestHashDirty=true;nextForestVisibility=0;
        forestBirthBuffer?.Dispose();forestBirthBuffer=null;Array.Clear(forestBirth,0,forestBirth.Length);
        forestOwnershipEpoch++;forestOwnership.Clear();
        forestFootprint=null;forestFootprintEpoch=-1;forestCoveragePlanned=false;forestRevealBounds.Clear();forestUnavailable=false;forestRetryAt=0;
    }
}
