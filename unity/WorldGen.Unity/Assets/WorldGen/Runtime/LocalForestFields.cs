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
    struct ForestGroundGrid {public Vector4 A,B,C,D,E,F,G,H;public bool Equals(ForestGroundGrid other)=>A.Equals(other.A)&&B.Equals(other.B)&&C.Equals(other.C)&&D.Equals(other.D)&&E.Equals(other.E)&&F.Equals(other.F)&&G.Equals(other.G)&&H.Equals(other.H);}
    [StructLayout(LayoutKind.Sequential)]
    struct ForestHash {public int X,Z,Level,State;}
    sealed class ForestBatch
    {
        public ArtModel Model;public bool Macro;public V Pigment;public readonly List<ForestRoot> Data=new List<ForestRoot>();public readonly Stack<int> Free=new Stack<int>();
        public readonly List<int> GroundLevels=new List<int>();
        public readonly List<ForestGroundGrid> GroundData=new List<ForestGroundGrid>();
        public GraphicsBuffer Roots,Ground,Visible,Args;public int Capacity,VisibleCapacity,VisibleBudget;public bool Dirty=true,AllDirty=true,CullValid;public bool[] DirtyBlocks=Array.Empty<bool>();public Matrix4x4 CullMatrix;public Vector3 CullEye;public Vector4 CullChart,CullRange;public int CullRevision;
        public MaterialPropertyBlock Properties=new MaterialPropertyBlock();public int SubmittedFrame=-1;
        public void Dispose(){Roots?.Dispose();Ground?.Dispose();Visible?.Dispose();Args?.Dispose();CullValid=false;}
    }
    sealed class ForestField
    {
        public ForestPage Source;public int Id,UnknownGroundRoots;public Bounds Bounds;public bool WasReady,Projecting,Published;public float RetireAfter;
        public readonly Dictionary<ForestBatch,List<int>> Roots=new Dictionary<ForestBatch,List<int>>();
        public bool Ready=>!Projecting&&WasReady;
        public bool GroundReady=>UnknownGroundRoots==0;
        public bool RenderReady=>Ready&&GroundReady&&(Source.Roots.Length==0||Published);
        public bool EvaluateReady(){if(Projecting)return false;foreach(var batch in Roots.Keys)if(!batch.Model.TreeBaked)return false;return true;}
    }
    sealed class ForestWork {public ForestPageKey Key;public Task<ForestPage> Task;public CancellationTokenSource Cancellation;public object Group;}
    readonly HashSet<object> activeForestGroups=new HashSet<object>();
    int ActiveForestWorkers
    {
        get{activeForestGroups.Clear();int count=0;foreach(var work in forestJobs)if(!work.Task.IsCompleted&&(work.Group==null||activeForestGroups.Add(work.Group)))count++;return count;}
    }
    int ForestWorkerLimit=>Math.Min(InitialViewReady?2:Math.Max(2,WorkerLimit-1),WorkerLimit);
    bool CanPrepareForest=>!reserveScatterFrame&&!GroundPublicationPending&&frameUploadMs<StreamingUploadBudgetMs;
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
        {batch.Roots.SetData(batch.Data,0,0,batch.Data.Count);if(batch.Model?.TreeCluster==true)batch.Ground.SetData(batch.GroundData,0,0,batch.Data.Count);ForestRootUploadCalls++;ForestRootFullUploads++;ForestRootUploadBytes+=(long)batch.Data.Count*(batch.Model?.TreeCluster==true?192:64);}
        else
        {
            for(int block=0;block<batch.DirtyBlocks.Length;)
            {
                if(!batch.DirtyBlocks[block]){block++;continue;}
                int first=block++;while(block<batch.DirtyBlocks.Length&&batch.DirtyBlocks[block])block++;
                int start=first*ForestUploadBlock,count=Math.Min(batch.Data.Count,block*ForestUploadBlock)-start;
                if(count<=0)continue;
                batch.Roots.SetData(batch.Data,start,start,count);if(batch.Model?.TreeCluster==true)batch.Ground.SetData(batch.GroundData,start,start,count);ForestRootUploadCalls++;ForestRootUploadBytes+=(long)count*(batch.Model?.TreeCluster==true?192:64);
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
    readonly int[] forestFlags=new int[8192];GraphicsBuffer forestFlagBuffer,forestHashBuffer,forestGroundDummy;
    readonly float[] forestBirth=new float[8192];GraphicsBuffer forestBirthBuffer;
    void MarkForestBirth(ForestField field)
    {
        if(field.Source.Key.Level!=0||forestBirth[field.Id]!=0)return;
        // GPU fallback trees still replace a coarse parent. They need the same
        // complementary fade even before local interactive scatter is ready.
        var parent=field.Source.Key.Parent;
        for(int level=1;level<=15;level++,parent=parent.Parent)
            if(forestFields.TryGetValue(parent,out var coarse)&&coarse.RenderReady){forestBirth[field.Id]=Time.realtimeSinceStartup;coarse.RetireAfter=Time.realtimeSinceStartup+.7f;}
    }
    ComputeShader forestCull;bool forestHashDirty;LocalTerrainFrame forestFrame;V forestPlanAt;double forestPlanRange=-1,forestPlanAltitude=-1,nextForestVisibility;int forestGroundRevision=-1,forestCoverageRevision=-1,forestLocalRevision=-1;
    readonly Vector4[] forestPlanes=new Vector4[6];readonly Plane[] forestPlaneWork=new Plane[6];Vector3 forestVisibilityEye=new Vector3(float.NaN,0,0);
    ForestCoverageFootprint forestFootprint;int forestFootprintEpoch=-1;
    bool forestCoveragePlanned,forestUnavailable;V forestCoverageAt;double forestCoverageRange,forestRetryAt;
    readonly Dictionary<ForestPageKey,ForestRevealBounds> forestRevealBounds=new Dictionary<ForestPageKey,ForestRevealBounds>();
    // Terrain and vegetation use different world grids. Keep the dependency
    // with the terrain product, rather than pretending their keys coincide.
    readonly Dictionary<(TerrainTileKey Key,bool Fallback),ForestPageKey[]> terrainForestPages=new Dictionary<(TerrainTileKey,bool),ForestPageKey[]>();
    readonly HashSet<ForestPageKey> terrainForestRequired=new HashSet<ForestPageKey>();
    struct ForestRevealBounds {public V Center;public double Radius;}
    bool TreesNeededForReveal=>ScatterEnabled&&TerrainVideoOptions.GeneratedTrees&&TerrainVideoOptions.TreeDensity>0;
    bool ForestNeededForReveal=>TreesNeededForReveal&&TerrainVideoOptions.TreeRebuild&&TerrainVideoOptions.TreeImpostors&&TerrainVideoOptions.FarForest&&SystemInfo.supportsComputeShaders&&!forestUnavailable;
    // Distant terrain already carries continuous crown colour/coverage. Beyond
    // this band it represents the forest without per-tree GPU roots.
    double ForestBillboardEnd=>Math.Min(viewDistance,6000);
    double ForestBillboardStart=>Math.Min(5000,ForestBillboardEnd*5/6);
    double ForestPlanningRange=>Math.Min(viewDistance,ForestBillboardEnd+512);
    double ForestGapDistance(ForestPageKey key)
    {
        if(!forestRevealBounds.TryGetValue(key,out var bounds))
        {scatter.ForestBounds(key,out bounds.Center,out bounds.Radius);forestRevealBounds.Add(key,bounds);}
        return Math.Max(0,(bounds.Center-Position).Length*frame.RadiusM-bounds.Radius);
    }
    double ForestRequiredGap(ForestPageKey key)
    {
        double distance=ForestGapDistance(key);
        return distance<ForestBillboardEnd?distance:double.PositiveInfinity;
    }
    bool TerrainForestReady(TerrainTileKey key,bool fallback=false)
    {
        double altitude=Math.Max(0,HeightAboveGround);
        if(!ForestNeededForReveal||TerrainLod.DistanceSquared(key,motion.X,motion.Z)+altitude*altitude>=ForestBillboardEnd*ForestBillboardEnd)return true;
        if(!forestCoveragePlanned)return false;
        var dependency=(key,fallback);
        if(!terrainForestPages.TryGetValue(dependency,out var pages))
        {
            var overlaps=new List<ForestPageKey>();
            double left=key.X*key.SizeM,right=left+key.SizeM,bottom=key.Z*key.SizeM,top=bottom+key.SizeM;
            // First publication uses the final vegetation plan at this view.
            // A temporary parent grove is not a completed forested chunk.
            foreach(var page in forestPriority)
            {
                scatter.ForestBounds(page,out var center,out double radius);
                double dot=V.Dot(center,frame.Origin);if(dot<=.01)continue;
                double x=V.Dot(center,frame.East)*frame.RadiusM/dot,z=V.Dot(center,frame.North)*frame.RadiusM/dot;
                // Conservative gnomonic projection of the spherical page bound.
                double extent=radius/(dot*dot)+1;
                if(x+extent<left||x-extent>right||z+extent<bottom||z-extent>top)continue;
                overlaps.Add(page);
            }
            pages=overlaps.ToArray();terrainForestPages.Add(dependency,pages);
        }
        if(pages.Length==0)return false; // A stale plan is not a treeless census.
        bool ready=true;
        foreach(var page in pages)
        {
            // Root records and their shared prototypes may be prepared while
            // terrain is hidden. Ungrounded records have negative Padding and
            // cannot draw. Terrain publication grounds them before this frame's
            // vegetation submission, so no barren intermediate frame is needed.
            // A published finer census already replaces this coarse page.
            // Retirement and terrain admission must agree, otherwise the
            // required parent is rebuilt and retired forever before admission.
            if(forestFields.TryGetValue(page,out var field)&&field.Ready||ForestReplacementReady(page))continue;
            terrainForestRequired.Add(page);ready=false;
        }
        return ready;
    }
    double TreeRevealDistance(double limit)
    {
        if(!TreesNeededForReveal)return limit;
        double gap=limit/.94,readyLimit=limit,localRange=Math.Min(TerrainVideoOptions.TreeIdentityRange,ForestNeededForReveal?ForestHandoffReach:1800);
        foreach(var pair in scatterRevealBounds)
        {
            if(scatterChunks.TryGetValue(pair.Key,out var ready)&&(!ScatterNeedsTrees(pair.Key)||ready.TreesIncluded))continue;
            // A nearby mesh must be ready at first reveal, not arrive after
            // billboards have already appeared at the same viewing distance.
            double distance=Math.Max(0,(pair.Value.Center-Position).Length*frame.RadiusM-pair.Value.Radius);
            if(distance<localRange)gap=Math.Min(gap,distance);
        }
        if(ForestNeededForReveal)
        {
            if(!forestCoveragePlanned)return 100;
            if(forestFootprint==null||forestFootprintEpoch!=forestOwnershipEpoch)
            {
                forestFootprint=new ForestCoverageFootprint(forestFields.Where(p=>p.Value.RenderReady).Select(p=>p.Key),scatter.TreeLatticeCount);
                forestFootprintEpoch=forestOwnershipEpoch;
            }
            gap=forestFootprint.NearestGap(forestPriority,ForestRequiredGap,gap);
            // The old plan remains useful while a moved-camera plan is in flight,
            // but only the billboard annulus needs tree packets. Its completion
            // must not shorten a much farther terrain/fog view to six kilometres.
            double covered=Math.Max(0,forestCoverageRange-(Position-forestCoverageAt).Length*frame.RadiusM);
            if(covered<Math.Min(limit,ForestBillboardEnd))readyLimit=Math.Min(readyLimit,covered);
        }
        return Math.Min(readyLimit,gap*.94);
    }
    bool ForestCoversScatter(ScatterKey key)
    {
        int pages=LocalScatter.TreeCandidateSide/LocalScatter.ForestPageSide;
        for(int y=0;y<pages;y++)for(int x=0;x<pages;x++)
            if(!forestFields.TryGetValue(new ForestPageKey(key.Face,key.X*pages+x,key.Y*pages+y,0),out var field)||!field.RenderReady)return false;
        return true;
    }
    public int ForestFieldCount=>forestFields.Count;
    public int ForestPacketCount=>forestRootCount;
    public int ForestDrawCalls {get;private set;}
    // CPU instancing -> identical GPU individual impostors, NOT tree retirement.
    // The hierarchy continues exact roots past this handoff until projected
    // node error justifies aggregation. Do not spend a 1.4km CPU buffer twice.
    double LegacyForestReach=>Math.Min(TerrainVideoOptions.TreeIdentityRange,Math.Max(350,TerrainVideoOptions.TreeFarLod*2.7));
    bool residentForestReady;int residentForestEpoch=-1;double nextResidentForestCheck;V residentForestCheckAt;
    double ForestHandoffReach=>TerrainVideoOptions.EfficientTreeRendering&&!WorldGenPerformance.LegacyTreeResident?
        Math.Min(LegacyForestReach,Math.Max(120,TerrainVideoOptions.TreeFarLod*1.8)):LegacyForestReach;
    public bool ForestPending=>forestPlan!=null||forestJobs.Count>0||forestPriority.Any(k=>!forestFields.ContainsKey(k))||forestFields.Values.Any(f=>!f.Ready);
    static uint ForestMix(uint x){x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16);}
    static uint ForestKeyHash(int x,int z,int level,int face=0)=>ForestMix(unchecked((uint)x*73856093u^(uint)z*19349663u^(uint)level*83492791u^(uint)face*15485863u));
    ArtModel ForestArt(int code,bool macro=false,bool cluster=false)
    {
        code=LocalTreePrototype.PrototypeCode(code);long key=ScatterArtKey(ScatterKind.GeneratedTree,code,2)|(1L<<49)|(cluster?1L<<50:0);
        if(scatterArt.TryGetValue(key,out var model))return model;
        model=CreateRebuiltTree(key,code,2,true,cluster);return model;
    }
    bool ForestOwnsTree(in ScatterInstance tree)
    {
        int side=LocalScatter.TreeCandidateSide,gx=(int)((tree.Id>>40)&0xfffff)*side+(int)(tree.Id&65535)%side,gy=(int)((tree.Id>>20)&0xfffff)*side+(int)(tree.Id&65535)/side;
        var key=new ForestPageKey((int)(tree.Id>>61),gx/LocalScatter.ForestPageSide,gy/LocalScatter.ForestPageSide,0);
        if(forestOwnershipRevision!=forestOwnershipEpoch){forestOwnership.Clear();forestOwnershipRevision=forestOwnershipEpoch;}
        if(!WorldGenPerformance.ForceFreshForestOwnership&&forestOwnership.TryGetValue(key,out bool owned))return owned;
        var query=key;for(int level=0;level<=24;level++,query=query.Parent)if(forestFields.TryGetValue(query,out var field)&&field.RenderReady){if(!scatterParallelCollecting)forestOwnership[key]=true;return true;}
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
        if(!TerrainVideoOptions.GeneratedTrees||!TerrainVideoOptions.TreeRebuild||!TerrainVideoOptions.TreeImpostors||!TerrainVideoOptions.FarForest||!Ready)return;
        if(!SystemInfo.supportsComputeShaders)return;
        if(forestUnavailable)return;
        if(forestCull==null){forestCull=Resources.Load<ComputeShader>("WorldGenForestCull");if(forestCull==null){forestUnavailable=true;Debug.LogWarning("Forest compute shader missing; using local tree coverage.");return;}forestFlagBuffer=new GraphicsBuffer(GraphicsBuffer.Target.Structured,forestFlags.Length,4);forestBirthBuffer=new GraphicsBuffer(GraphicsBuffer.Target.Structured,forestBirth.Length,4);}
        foreach(var batch in forestBatches.Values)batch.Model.LastUsed=Time.realtimeSinceStartupAsDouble;
        if(!ReferenceEquals(forestFrame,frame))
        {
            terrainForestPages.Clear();terrainForestRequired.Clear();
            long phase=WorldGenPerformance.TraceBegin();
            // Reproject immutable planet-addressed payloads; never reseed a forest
            // because its terrain chart was replaced.
            if(forestInstalling!=null)RemoveForestField(forestInstalling.Source.Key);
            forestFrame=frame;foreach(var batch in forestBatches.Values){batch.Data.Clear();batch.GroundLevels.Clear();batch.GroundData.Clear();batch.Free.Clear();DirtyAllForestRoots(batch);}
            forestRootCount=0;foreach(var field in forestFields.Values){field.Published=false;field.Roots.Clear();ProjectForest(field);}forestRevision++;forestHashDirty=true;forestOwnershipEpoch++;scatterDirty=true;
            WorldGenPerformance.TraceEnd(58,phase);
        }
        if(forestPlan!=null&&forestPlan.IsCompleted)
        {
            long phase=WorldGenPerformance.TraceBegin();
            if(forestPlan.IsCompletedSuccessfully)
            {
                forestPriority=forestPlan.Result;forestWanted.Clear();foreach(var key in forestPriority)forestWanted.Add(key);forestNextRequest=0;forestMaintenanceDirty=true;
                terrainForestPages.Clear();terrainForestRequired.Clear();
                flightForestCoverage=motion.Flying&&HeightAboveGround>80;
                flightForestPriority=CoarseForestPriority(forestPriority);flightForestNext=0;
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
        if(forestPlan==null&&(forestPlanRange!=ForestPlanningRange||moved>128||Math.Abs(HeightAboveGround-forestPlanAltitude)>128||forestOptions!=TerrainVideoOptions.VegetationRevision||flightForestCoverage!=(motion.Flying&&HeightAboveGround>80)))
        {
            if(forestOptions!=TerrainVideoOptions.VegetationRevision)nextForestVisibility=0;
            forestPlanAt=Position;forestPlanRange=ForestPlanningRange;forestPlanAltitude=HeightAboveGround;forestOptions=TerrainVideoOptions.VegetationRevision;
            // Preserve the old exact-census radius. Only submission changes;
            // moving the GPU handoff must not coarsen the forest population.
            var at=Position;double range=ForestPlanningRange,exact=Math.Max(384,TerrainVideoOptions.TreeFarLod*2+128),inner=0,altitude=Math.Max(0,HeightAboveGround);var source=scatter;
            forestPlan=Task.Run(()=>source.PlanForest(at,range,exact,4096,inner,altitude),cancellation.Token);
        }
        if(forestHashDirty)foreach(var field in forestFields.Values)
        {bool ready=field.EvaluateReady();if(ready!=field.WasReady){field.WasReady=ready;scatterDirty=true;forestRevision++;forestOwnershipEpoch++;forestMaintenanceDirty=true;}}
        // The reverse removal loop used to install the newest completed page
        // first. A stream of quick far pages could indefinitely bypass the
        // oldest nearby page and keep the reveal disk at zero.
        if(!legacyLoader&&forestJobs.Count>1)
        {
            forestCompletionComparison??=(a,b)=>ForestGapDistance(b.Key).CompareTo(ForestGapDistance(a.Key));
            forestJobs.Sort(forestCompletionComparison);
        }
        DrainForestProjection();
        int emptyInstalls=0,preparedPages=0;
        for(int i=forestJobs.Count-1;i>=0;i--)
        {
            var job=forestJobs[i];if(!job.Task.IsCompleted)continue;
            bool metadataOnly=!legacyLoader&&job.Task.IsCompletedSuccessfully&&job.Task.Result.Roots.Length==0;
            // A completed zero-root page certifies an empty census. It does
            // not upload objects or correct ground, so terrain publication
            // must not block it and occupy both forest request slots.
            if(job.Task.IsCompletedSuccessfully&&(forestWanted.Contains(job.Key)||flightForestWanted.Contains(job.Key))&&!metadataOnly&&(!CanPrepareForest||forestProjection!=null))continue;
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
            if(!metadataOnly)DrainForestProjection();
            // Empty pages only certify a tree-free part of the census. They
            // create no tree models, transforms or GPU roots; do not charge
            // an entire object-upload frame for each of thousands of them.
            if(forestProjection!=null||++preparedPages>=16||metadataOnly&&++emptyInstalls>=8||frameUploadMs>=StreamingUploadBudgetMs)break;
        }
        if(forestMaintenanceDirty&&!GroundPublicationPending)
        {
        long phase=WorldGenPerformance.TraceBegin();
        forestMaintenanceDirty=false;
        foreach(var key in forestFields.Keys.Where(k=>!forestWanted.Contains(k)).ToArray())
        {
            var f=forestFields[key];scatter.ForestBounds(key,out V center,out double radius);
            if((center-Position).Length*frame.RadiusM-radius>ForestPlanningRange+512||(center-Position).Length*frame.RadiusM+radius<VegetationQuality.ForestStart(ForestHandoffReach)-128||ForestReplacementReady(key))RemoveForestField(key);
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
            if(!forestFields.TryGetValue(key,out var field)||!field.RenderReady){ready=false;break;}
        }
        if(residentForestReady!=ready){residentForestReady=ready;scatterDirty=true;}
    }
    bool ScheduleForestWork(bool coveredFlight=false)
    {
        // Near ground retains priority. When forest coverage is the reveal
        // blocker, it also gets a turn before distant terrain fills every lane.
        if(!TerrainVideoOptions.GeneratedTrees||!TerrainVideoOptions.TreeRebuild||!TerrainVideoOptions.TreeImpostors||!TerrainVideoOptions.FarForest||!Ready||forestCull==null||mapOpen||Time.realtimeSinceStartupAsDouble<forestRetryAt||RunningWorkerCount>=WorkerLimit||ActiveForestWorkers>=ForestWorkerLimit||forestJobs.Count>=32||forestRootCount>=LocalScatter.MaximumForestRoots)return false;
        foreach(var required in flightForestPriority)
        {
            if(!terrainForestRequired.Contains(required)||forestFields.ContainsKey(required)||forestJobs.Any(j=>j.Key.Equals(required))||ForestReplacementReady(required))continue;
            RequestForest(required);return true;
        }
        foreach(var required in forestPriority)
        {
            if(!terrainForestRequired.Contains(required)||forestFields.ContainsKey(required)||forestJobs.Any(j=>j.Key.Equals(required))||ForestReplacementReady(required))continue;
            RequestForest(required);return true;
        }
        while(flightForestNext<flightForestPriority.Length||forestNextRequest<forestPriority.Length)
        {
            // Keep the immediate exact forest first, then complete canopy
            // coverage before refining the rest of the two-kilometre census.
            // Otherwise upgrades consume the lanes while flight outruns its
            // next coarse forest/terrain packet.
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
                coverage=near>Math.Max(600,LegacyForestReach+64);
            }
            var key=coverage?flightForestPriority[flightForestNext++]:forestPriority[forestNextRequest++];
            RequestForest(key);return true;
        }
        return false;
    }
    void RequestForest(ForestPageKey key)
    {
        // Small placement pages share one worker request batch. The old
        // one-page-per-frame dispatch/installation ceiling could not keep up
        // with flight even when generation and model assets were already fast.
        var keys=new List<ForestPageKey>(8){key};
        foreach(var next in key.Level>0?flightForestPriority:forestPriority)
        {
            if(keys.Count>=Math.Min(8,32-forestJobs.Count))break;
            if(next.Equals(key)||forestFields.ContainsKey(next)||forestJobs.Any(j=>j.Key.Equals(next)))continue;
            if(!forestWanted.Contains(next)&&ForestReplacementReady(next))continue;
            if(terrainForestRequired.Count>0&&!terrainForestRequired.Contains(next))continue;
            keys.Add(next);
        }
        var source=scatter;var producer=meshProcess;var group=new object();
        var batch=new List<(ForestPageKey Key,CancellationToken Token,TaskCompletionSource<ForestPage> Result)>(keys.Count);
        foreach(var next in keys)
        {
            var stop=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);var result=new TaskCompletionSource<ForestPage>();
            forestJobs.Add(new ForestWork{Key=next,Cancellation=stop,Task=result.Task,Group=group});batch.Add((next,stop.Token,result));
        }
        _=Task.Run(()=>
        {
            foreach(var request in batch)
            {
                try{request.Token.ThrowIfCancellationRequested();request.Result.TrySetResult(producer==null?source.BuildForest(request.Key,request.Token):producer.Forest(request.Key,request.Token));}
                catch(OperationCanceledException){request.Result.TrySetCanceled();}
                catch(Exception e){request.Result.TrySetException(e);}
            }
        });
    }
    void CancelForestWorker(ForestWork work)
    {
        if(work.Group==null){work.Cancellation.Cancel();return;}
        foreach(var member in forestJobs)if(ReferenceEquals(work.Group,member.Group))member.Cancellation.Cancel();
    }
    ForestPageKey[] CoarseForestPriority(IEnumerable<ForestPageKey> leaves)
    {
        // Prioritize actual distant LODs, not provisional groves that would
        // later fill in while the camera remains still.
        return leaves.Where(key=>key.Level>0).OrderBy(ForestGapDistance).ToArray();
    }
    bool ForestReplacementReady(ForestPageKey key)
    {
        // Keep the statistical parent until its close exact descendants have
        // completed their complementary crossfade.
        if(forestFields.TryGetValue(key,out var fading)&&Time.realtimeSinceStartup<fading.RetireAfter){forestMaintenanceDirty=true;return false;}
        var parent=key.Parent;for(int level=key.Level+1;level<=24;level++,parent=parent.Parent)
            if(forestWanted.Contains(parent)&&forestFields.TryGetValue(parent,out var field)&&field.RenderReady)return true;
        if(key.Level==0)return false;
        for(int y=0;y<2;y++)for(int x=0;x<2;x++)
        {
            var child=new ForestPageKey(key.Face,key.X*2+x,key.Y*2+y,key.Level-1);
            if(forestFields.TryGetValue(child,out var field)&&field.RenderReady)continue;
            if(!ForestReplacementReadyDescendants(child))return false;
        }
        return true;
    }
    bool ForestReplacementReadyDescendants(ForestPageKey key)
    {
        long size=(long)LocalScatter.ForestPageSide*(1L<<key.Level);if(key.X*size>=scatter.TreeLatticeCount||key.Y*size>=scatter.TreeLatticeCount)return true;
        if(forestFields.TryGetValue(key,out var field)&&field.RenderReady)return true;if(key.Level==0)return false;
        for(int y=0;y<2;y++)for(int x=0;x<2;x++)if(!ForestReplacementReadyDescendants(new ForestPageKey(key.Face,key.X*2+x,key.Y*2+y,key.Level-1)))return false;return true;
    }
    void ForestProjectionChanged()
    {forestHashDirty=true;forestRevision++;forestOwnershipEpoch++;scatterDirty=true;forestMaintenanceDirty=true;}
    void ClearForestProjection()
    {
        (forestProjection as IDisposable)?.Dispose();forestProjection=null;forestInstalling=null;
    }
    void DrainForestProjection()
    {
        // A slice yields to the remaining upload budget, not necessarily the
        // next frame. Actual asset waits must return without polling in a loop.
        while(forestProjection!=null&&CanPrepareForest)
        {
            if(forestProjection.Current is ResourceRequest request&&!request.isDone)break;
            AdvanceForestProjection();
        }
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
        // Reanchor already-loaded art and empty census certificates immediately;
        // this synchronous path must not spin waiting for an async frame.
        var steps=ProjectForestSteps(field,false);
        try{while(steps.MoveNext()){} }finally{(steps as IDisposable)?.Dispose();}
    }
    ForestBatch ForestBatchFor(int code,bool macro,bool cluster=false)
    {
        // Prepared bank codes have an empty low byte. Keep the renderer's
        // cluster key separate from the canonical species/prototype code.
        int batchCode=code|(macro?int.MinValue:0)|(cluster?64:0);
        if(!forestBatches.TryGetValue(batchCode,out var batch))
        {
            batch=new ForestBatch{Model=ForestArt(code,macro,cluster),Macro=macro,Pigment=new TreeMorphology(code).Leaf};
            forestBatches.Add(batchCode,batch);
        }
        return batch;
    }
    void SetForestGroundLevel(ForestField field,ForestBatch batch,int slot,int level)
    {
        bool before=field.GroundReady;
        if(batch.GroundLevels[slot]>=17)field.UnknownGroundRoots--;
        batch.GroundLevels[slot]=level;if(level>=17)field.UnknownGroundRoots++;
        if(before!=field.GroundReady){forestHashDirty=true;forestOwnershipEpoch++;nextForestVisibility=0;scatterDirty=true;forestMaintenanceDirty=true;}
    }
    static int ForestMaskBits(uint bits)
    {bits-=((bits>>1)&0x55555555u);bits=(bits&0x33333333u)+((bits>>2)&0x33333333u);bits=(bits+(bits>>4))&0x0f0f0f0fu;return (int)((bits*0x01010101u)>>24);}
    static Vector4 ForestMaskVector(ulong bits)=>new Vector4(bits&65535,(bits>>16)&65535,(bits>>32)&65535,(bits>>48)&65535);
    static int ForestVectorBits(Vector4 v)=>ForestMaskBits((uint)v.x)+ForestMaskBits((uint)v.y)+ForestMaskBits((uint)v.z)+ForestMaskBits((uint)v.w);
    static int ForestVisibleBudget(in ForestRoot root,in ForestGroundGrid grid)
        =>(root.Face&32u)==0?1:ForestVectorBits(grid.E)+ForestVectorBits(grid.F)+ForestVectorBits(grid.G)+ForestVectorBits(grid.H);
    void AddForestRoot(ForestField field,ForestBatch batch,ForestRoot root,int groundLevel,ForestGroundGrid grid,ref bool first)
    {
        batch.VisibleBudget+=ForestVisibleBudget(root,grid);
        int slot;
        if(batch.Free.Count>0){slot=batch.Free.Pop();batch.Data[slot]=root;batch.GroundLevels[slot]=groundLevel;}
        else
        {
            slot=batch.Data.Count;
            if(WorldGenPerformance.TraceEnabled&&(batch.Data.Count==batch.Data.Capacity||batch.GroundLevels.Count==batch.GroundLevels.Capacity))
            {
                int oldRoots=batch.Data.Capacity,oldLevels=batch.GroundLevels.Capacity,gc=GC.CollectionCount(0);
                long growthStarted=System.Diagnostics.Stopwatch.GetTimestamp();
                batch.Data.Add(root);batch.GroundLevels.Add(groundLevel);
                double elapsed=(System.Diagnostics.Stopwatch.GetTimestamp()-growthStarted)*1000d/System.Diagnostics.Stopwatch.Frequency;
                if(elapsed>=8)Debug.Log("FOREST_ROOT_GROWTH page="+field.Source.Key+" batch="+batch.Model.TreeCode+" cluster="+batch.Model.TreeCluster+" count="+slot+"->"+batch.Data.Count+" rootCapacity="+oldRoots+"->"+batch.Data.Capacity+" levelCapacity="+oldLevels+"->"+batch.GroundLevels.Capacity+" gc0Delta="+(GC.CollectionCount(0)-gc)+" realtime="+Time.realtimeSinceStartupAsDouble.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+" elapsed_ms="+elapsed.ToString("F6",System.Globalization.CultureInfo.InvariantCulture));
            }
            else{batch.Data.Add(root);batch.GroundLevels.Add(groundLevel);}
        }
        if(batch.Model?.TreeCluster==true){if(slot<batch.GroundData.Count)batch.GroundData[slot]=grid;else batch.GroundData.Add(grid);}
        if(groundLevel>=17)field.UnknownGroundRoots++;
        DirtyForestRoot(batch,slot);forestRootCount++;
        if(!field.Roots.TryGetValue(batch,out var slots)){slots=new List<int>();field.Roots.Add(batch,slots);}slots.Add(slot);
        var bounds=ForestRootBounds(root);if(first){field.Bounds=bounds;first=false;}else field.Bounds.Encapsulate(bounds);
    }
    IEnumerator ProjectForestSteps(ForestField field,bool prepareAsynchronously=true)
    {
        field.Projecting=true;field.WasReady=false;field.UnknownGroundRoots=0;
        bool first=true;var page=field.Source;int added=0,lastX=-1,lastY=-1,lastRevision=-1,lastLevel=17;ForestRoot lastGround=default;ForestGroundGrid lastGrid=default;long started=System.Diagnostics.Stopwatch.GetTimestamp();
        foreach(var source in page.Roots)
        {
            var tree=source.Tree;int code=LocalTreePrototype.PrototypeCode(tree.Variant);
            if(prepareAsynchronously&&!LocalTreePrototype.IsPrepared(code))
            {
                var preparation=LocalTreePrototype.Prepare(code);
                while(preparation.MoveNext())
                {
                    if(added>0){forestRootTopologyRevision++;added=0;}
                    yield return preparation.Current;started=System.Diagnostics.Stopwatch.GetTimestamp();
                }
            }
            {
                // Exactly one fixed planting block, not a statistical grove.
                var target=ForestBatchFor(code,true,true);
                double dot=Math.Max(.1,V.Dot(tree.Position,frame.Origin));
                double x=V.Dot(tree.Position,frame.East)*frame.RadiusM/dot,z=V.Dot(tree.Position,frame.North)*frame.RadiusM/dot,extent=0;
                for(int j=0;j<2;j++)for(int i=0;i<2;i++)
                {
                    var corner=scatter.ForestPosition(page.Key.Face,source.CellX+i*LocalScatter.ForestPlantingSide,source.CellY+j*LocalScatter.ForestPlantingSide);
                    double cd=Math.Max(.1,V.Dot(corner,frame.Origin));
                    extent=Math.Max(extent,Math.Max(Math.Abs(V.Dot(corner,frame.East)*frame.RadiusM/cd-x),Math.Abs(V.Dot(corner,frame.North)*frame.RadiusM/cd-z)));
                }
                var pigment=TreePigment(tree.Variant);var shared=target.Pigment;
                bool tropical=new TreeMorphology(tree.Variant).Form==TreeGrowthForm.BroadCrown&&new TreeMorphology(tree.Variant).Strategy==TreeCrownStrategy.TropicalCanopy;
                var root=new ForestRoot{Position=new Vector3((float)x,(float)frame.SurfaceY(tree.Position,tree.GroundM)-.045f,(float)z),
                    Width=(float)tree.WidthM*LocalTreePrototype.StandSpan,Height=(float)tree.HeightM,
                    Field=(uint)field.Id,CellX=source.CellX,CellY=source.CellY,Face=(uint)page.Key.Face|8u|32u|(tropical?64u:0u),
                    Tint=new Vector3((float)(pigment.X/Math.Max(.01,shared.X)),(float)(pigment.Y/Math.Max(.01,shared.Y)),(float)(pigment.Z/Math.Max(.01,shared.Z))),Padding=(float)Math.Max(4,(extent+.1)/.75)};
                int level;ForestGroundGrid grid;
                // Species batches share the same support footprint. Sample its
                // ground once, unless terrain changed while art was loading.
                if(lastX==source.CellX&&lastY==source.CellY&&lastRevision==residentRevision)
                {root.Position.y=lastGround.Position.y;root.SlopeX=lastGround.SlopeX;root.SlopeZ=lastGround.SlopeZ;level=lastLevel;grid=lastGrid;}
                else
                {ProjectForestRootGround(ref root,out level,out grid);lastX=source.CellX;lastY=source.CellY;lastRevision=residentRevision;lastLevel=level;lastGround=root;lastGrid=grid;}
                grid.D=new Vector4(source.P00,source.P10,source.P01,source.P11);
                grid.E=ForestMaskVector(source.AllowedMask.A);grid.F=ForestMaskVector(source.AllowedMask.B);grid.G=ForestMaskVector(source.AllowedMask.C);grid.H=ForestMaskVector(source.AllowedMask.D);
                AddForestRoot(field,target,root,level,grid,ref first);added++;
                if(System.Diagnostics.Stopwatch.GetTimestamp()-started>=System.Diagnostics.Stopwatch.Frequency*.0007)
                {forestRootTopologyRevision++;added=0;yield return null;started=System.Diagnostics.Stopwatch.GetTimestamp();}
            }
        }
        if(added>0)forestRootTopologyRevision++;
        field.Projecting=false;field.WasReady=field.EvaluateReady();
    }
    void ProjectForestGround()
    {
        long trace=WorldGenPerformance.TraceBegin();
        int tracedFields=0,tracedRoots=0,tracedSkipped=0,tracedQueries=0,tracedMoved=0;
        foreach(var field in forestFields.Values)
        {
            int minimumChangedLevel=scatterGroundChanges.Count==0?0:int.MaxValue;
            foreach(var key in scatterGroundChanges)
            {
                var b=field.Bounds;double x=key.X*key.SizeM,z=key.Z*key.SizeM;
                if(b.max.x<x||b.min.x>x+key.SizeM||b.max.z<z||b.min.z>z+key.SizeM)continue;
                // This square encloses every root and its query margin. A
                // partial edge mask can reject the whole field before walking
                // thousands of unchanged interior roots; full-key masks keep
                // the existing conservative overlap behavior.
                if(scatterGroundChanges.Affects(key,b.center.x,b.center.z,Math.Max(b.extents.x,b.extents.z)+.01))minimumChangedLevel=Math.Min(minimumChangedLevel,key.Level);
            }
            if(minimumChangedLevel==int.MaxValue)continue;
            if(trace!=0)tracedFields++;
            foreach(var pair in field.Roots)foreach(int slot in pair.Value)
            {
                if(trace!=0)tracedRoots++;
                int previousGroundLevel=pair.Key.GroundLevels[slot];
                // A changed coarse ancestor cannot affect a root still owned by
                // finer resident ground. Apply that existing per-key rule once
                // before walking every changed key or copying the root record.
                if(previousGroundLevel<minimumChangedLevel){if(trace!=0)tracedSkipped++;continue;}
                var root=pair.Key.Data[slot];
                // Each render root depends only on the ground beneath it.
                double margin=ForestGroundMargin(root);
                if(!ChangedGroundAt(root.Position.x,root.Position.z,margin,previousGroundLevel))continue;
                if(trace!=0)tracedQueries++;
                var before=root;ProjectForestRootGround(ref root,out int groundLevel,out var grid);SetForestGroundLevel(field,pair.Key,slot,groundLevel);
                if((root.Face&32u)!=0){grid.D=pair.Key.GroundData[slot].D;grid.E=pair.Key.GroundData[slot].E;grid.F=pair.Key.GroundData[slot].F;grid.G=pair.Key.GroundData[slot].G;grid.H=pair.Key.GroundData[slot].H;}
                bool gridChanged=pair.Key.Model?.TreeCluster==true&&!pair.Key.GroundData[slot].Equals(grid);
                if(root.Position.y==before.Position.y&&root.SlopeX==before.SlopeX&&root.SlopeZ==before.SlopeZ&&root.Padding==before.Padding&&!gridChanged)continue;
                pair.Key.Data[slot]=root;if(pair.Key.Model?.TreeCluster==true)pair.Key.GroundData[slot]=grid;DirtyForestRoot(pair.Key,slot);
                if(trace!=0)tracedMoved++;
                field.Bounds.Encapsulate(ForestRootBounds(root));
            }
        }
        forestRevision++;nextForestVisibility=0;
        WorldGenPerformance.TraceEnd(31,trace);
        if(trace!=0)
        {
            double elapsed=(System.Diagnostics.Stopwatch.GetTimestamp()-trace)*1000d/System.Diagnostics.Stopwatch.Frequency;
            if(elapsed>=3)Debug.Log("FOREST_GROUND_CORRECTION ms="+elapsed.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" fields="+tracedFields+" roots="+tracedRoots+" finer_owner_skips="+tracedSkipped+" queries="+tracedQueries+" moved="+tracedMoved+" changed_keys="+scatterGroundChanges.Count+" realtime="+Time.realtimeSinceStartupAsDouble.ToString("F6",System.Globalization.CultureInfo.InvariantCulture));
        }
    }
    void ProjectForestRootGround(ref ForestRoot root,out int groundLevel)=>ProjectForestRootGround(ref root,out groundLevel,out _);
    void ProjectForestRootGround(ref ForestRoot root,out int groundLevel,out ForestGroundGrid grid)
    {
        long groundStarted=WorldGenPerformance.TraceEnabled?System.Diagnostics.Stopwatch.GetTimestamp():0;
        double x=root.Position.x,z=root.Position.z,y=VisibleGround(x,z,root.Position.y+.045f,out groundLevel);
        Tile tile=null;TerrainTileKey key=default;
        if(groundLevel<17)
        {
            double size=TerrainLod.BaseSizeM*HorizontalScale*(1<<groundLevel);
            key=new TerrainTileKey((int)Math.Floor(x/size),(int)Math.Floor(z/size),groundLevel,HorizontalScale);
            tiles.TryGetValue(key,out tile);
        }
        groundLevel=ApplyForestRootGround(ref root,y,groundLevel,tile,key,out grid,this);
        if(groundStarted!=0)
        {
            double elapsed=(System.Diagnostics.Stopwatch.GetTimestamp()-groundStarted)*1000d/System.Diagnostics.Stopwatch.Frequency;
            if(elapsed>=8)Debug.Log("FOREST ROOT GROUND ms="+elapsed.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" level="+groundLevel+" x="+x+" z="+z+" realtime="+Time.realtimeSinceStartupAsDouble.ToString("F6",System.Globalization.CultureInfo.InvariantCulture));
        }
    }
    static int ApplyForestRootGround(ref ForestRoot root,double y,int groundLevel,Tile tile,TerrainTileKey key,out ForestGroundGrid grid,LocalTerrainExplorer terrain=null,GroundPublication publication=null)
    {
        grid=default;
        root.Position.y=(float)y-.045f;
        if(Math.Abs(root.Padding)>2.5f&&(root.Face&8u)!=0)
        {
            // Padding>=4 carries the packet support span. Padding3 remains
            // the legacy width-sized group used by the focused shader fixture.
            double supportSpan=root.Padding>=4?root.Padding:root.Width;
            groundLevel=SampleForestGroundGrid(tile,key,root.Position.x,root.Position.z,y,supportSpan,out grid,terrain,publication);
            root.SlopeX=root.SlopeZ=grid.C.w/(float)Math.Max(1,supportSpan);
            return groundLevel;
        }
        bool macro=Math.Abs(root.Padding)>1.5f;
        float footprint=macro?Math.Max(2,Math.Abs(root.Padding)):0;
        root.Padding=groundLevel>=17?(macro?-footprint:-1):(macro?footprint:0);
        if(groundLevel<17&&tile!=null)
        {
            // Terrain already carries ecological admission, including water,
            // exposed cliffs and treeline. Read that grid instead of running
            // more exact tree/soil/height queries for a distant preview.
            if(tile.ForestCover.IsCreated)
            {
                double x=root.Position.x,z=root.Position.z;
                int side=tile.Segments+1,gx=Mathf.Clamp((int)Math.Round((x/key.SizeM-key.X)*tile.Segments),0,tile.Segments),gz=Mathf.Clamp((int)Math.Round((z/key.SizeM-key.Z)*tile.Segments),0,tile.Segments);
                if(tile.ForestCover[(gz*side+gx)*8+3]<=.001f)root.Padding=macro?-footprint:-1;
            }
            if(!macro&&tile.Heights!=null)
            {
                // Visual candidates use the same stable slope admission hash
                // as physical trees, against the already prepared local mesh.
                // Never run world geometry/noise once per distant tree here.
                int n=tile.Segments,side=n+1;
                double gx=(root.Position.x/key.SizeM-key.X)*n,gz=(root.Position.z/key.SizeM-key.Z)*n;
                int a=Math.Max(0,Math.Min(n-1,(int)gx)),b=Math.Max(0,Math.Min(n-1,(int)gz));double u=gx-a,v=gz-b,step=key.SizeM/n;
                float h00=tile.Heights[b*side+a],h10=tile.Heights[b*side+a+1],h01=tile.Heights[(b+1)*side+a],h11=tile.Heights[(b+1)*side+a+1];
                double dx=(u+v<=1?h10-h00:h11-h01)/step,dz=(u+v<=1?h01-h00:h11-h10)/step;
                if((root.Face>>8)/16777216.0>TreeFlora.SlopeAdmission(1/Math.Sqrt(1+dx*dx+dz*dz)))root.Padding=-1;
            }
        }
        root.SlopeX=root.SlopeZ=0;
        return groundLevel;
    }
    static double ForestGroundMargin(in ForestRoot root)
    {
        if((root.Face&8u)==0||Math.Abs(root.Padding)<1.5f)return .01;
        if(root.Padding>=4)return root.Padding*.75+.01;
        double width=Math.Abs(root.Padding)>2.5f?root.Width:root.Width*LocalTreePrototype.StandSpan;
        return width*.75+.01;
    }
    static bool TryForestGroundSample(Tile tile,TerrainTileKey key,double x,double z,out double y,out bool forest)
    {
        y=0;forest=false;if(tile==null||tile.Heights==null)return false;
        double size=key.SizeM,localX=x-key.X*size,localZ=z-key.Z*size;
        if(localX<0||localZ<0||localX>size||localZ>size)return false;
        int n=tile.Segments,side=n+1;
        double gx=localX*n/size,gz=localZ*n/size;
        int a=Math.Min(n-1,Math.Max(0,(int)gx)),b=Math.Min(n-1,Math.Max(0,(int)gz));double u=gx-a,v=gz-b;
        if(tile.Contact==null||!tile.Contact.TryHeight(localX,localZ,out y))
        {
            float h00=tile.Heights[b*side+a],h10=tile.Heights[b*side+a+1],h01=tile.Heights[(b+1)*side+a],h11=tile.Heights[(b+1)*side+a+1];
            y=u+v<=1?h00+(h10-h00)*u+(h01-h00)*v:h11+(h01-h11)*(1-u)+(h10-h11)*(1-v);
        }
        int cx=Math.Min(n,Math.Max(0,(int)Math.Round(gx))),cz=Math.Min(n,Math.Max(0,(int)Math.Round(gz)));
        // TerrainSurfaceVertex writes TreeFlora.Surface/Rooting admission here,
        // including actual underwater depth and mapped tidal forest. A whole
        // water mesh's bounds also enclose dry land and cannot reject roots.
        forest=tile.ForestCover.IsCreated&&tile.ForestCover[(cz*side+cx)*8+3]>.001f;
        return true;
    }
    static int SampleForestGroundGrid(Tile tile,TerrainTileKey key,double x,double z,double centerY,double width,out ForestGroundGrid grid,LocalTerrainExplorer terrain=null,GroundPublication publication=null)
    {
        grid=default;double radius=width*.75,maxDelta=0;int groundMask=0,forestMask=0,supportLevel=0;
        Span<float> heights=stackalloc float[9];
        for(int j=-1;j<=1;j++)for(int i=-1;i<=1;i++)
        {
            int index=(j+1)*3+i+1;double px=x+i*radius,pz=z+j*radius;Tile owner=tile;TerrainTileKey ownerKey=key;
            if(terrain!=null&&(i!=0||j!=0))
            {
                if(publication!=null)terrain.PublicationGround(publication,px,pz,centerY,out _,out owner,out ownerKey);
                else owner=terrain.VisibleGroundOwnerCached(px,pz,out ownerKey);
            }
            double h=centerY;
            if(TryForestGroundSample(owner,ownerKey,px,pz,out double sampleY,out bool forest))
            {
                h=sampleY;groundMask|=1<<index;if(forest)forestMask|=1<<index;
                supportLevel=Math.Max(supportLevel,ownerKey.Level);maxDelta=Math.Max(maxDelta,Math.Abs(h-centerY));
            }
            else supportLevel=17;
            heights[index]=(float)h;
        }
        grid.A=new Vector4(heights[0],heights[1],heights[2],heights[3]);
        grid.B=new Vector4(heights[4],heights[5],heights[6],heights[7]);
        grid.C=new Vector4(heights[8],forestMask,groundMask,(float)maxDelta);
        return supportLevel;
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
        foreach(var pair in field.Roots)foreach(int slot in pair.Value){pair.Key.VisibleBudget-=ForestVisibleBudget(pair.Key.Data[slot],pair.Key.Model?.TreeCluster==true?pair.Key.GroundData[slot]:default);pair.Key.Data[slot]=default;if(pair.Key.Model?.TreeCluster==true)pair.Key.GroundData[slot]=default;pair.Key.Free.Push(slot);DirtyForestRoot(pair.Key,slot);forestRootCount--;}
        forestFlags[field.Id]=0;forestBirth[field.Id]=0;forestFreeIds.Push(field.Id);forestFields.Remove(key);forestHashDirty=true;forestRevision++;forestOwnershipEpoch++;scatterDirty=true;
    }
    void CompactForest(ForestBatch batch)
    {
        if(batch.Free.Count<256||batch.Free.Count<batch.Data.Count/3)return;
        forestRootTopologyRevision++;
        var map=new int[batch.Data.Count];int live=0;for(int i=0;i<batch.Data.Count;i++)if(batch.Data[i].Height>0){map[i]=live;batch.Data[live]=batch.Data[i];if(batch.Model?.TreeCluster==true)batch.GroundData[live]=batch.GroundData[i];batch.GroundLevels[live++]=batch.GroundLevels[i];}
        if(batch.Model?.TreeCluster==true)batch.GroundData.RemoveRange(live,batch.Data.Count-live);
        batch.GroundLevels.RemoveRange(live,batch.Data.Count-live);batch.Data.RemoveRange(live,batch.Data.Count-live);batch.Free.Clear();DirtyAllForestRoots(batch);
        foreach(var field in forestFields.Values)if(field.Roots.TryGetValue(batch,out var slots))for(int i=0;i<slots.Count;i++)slots[i]=map[slots[i]];
    }
    void PrepareForestHash()
    {
        if(!forestHashDirty)return;forestHashDirty=false;int count=16;while(count<forestFields.Count*4)count*=2;
        var hash=new ForestHash[count];forestHashMask=count-1;
        foreach(var pair in forestFields)
        {
            if(!pair.Value.RenderReady)continue;var key=pair.Key;int slot=(int)(ForestKeyHash(key.X,key.Y,key.Level,key.Face)&(uint)forestHashMask);
            while(hash[slot].State!=0)slot=(slot+1)&forestHashMask;hash[slot]=new ForestHash{X=key.X,Z=key.Y,Level=key.Level,State=((pair.Value.Id+1)<<3)|(key.Face+1)};
        }
        if(forestHashBuffer==null||forestHashBuffer.count!=count){forestHashBuffer?.Dispose();forestHashBuffer=new GraphicsBuffer(GraphicsBuffer.Target.Structured,count,16);}
        forestHashBuffer.SetData(hash);forestBirthBuffer.SetData(forestBirth);forestRevision++;nextForestVisibility=0;
    }
    void DrawForestFields()
    {
        Shader.SetGlobalVector("_WorldGenForestDistanceRange",new Vector4((float)ForestBillboardStart,(float)ForestBillboardEnd,0,0));
        ForestDrawCalls=0;if(TerrainDiagnostics.HidePlants||TerrainDiagnostics.HideTrees||WorldGenPerformance.SuppressFarForest||!TerrainVideoOptions.GeneratedTrees||!TerrainVideoOptions.TreeRebuild||!TerrainVideoOptions.TreeImpostors||!TerrainVideoOptions.FarForest||forestCull==null||forestFlagBuffer==null||view==null||forestFields.Count==0)return;
        PrepareForestHash();var eye=view.transform.localPosition;
        if(canopySourceRevision!=forestLocalRevision||Time.realtimeSinceStartupAsDouble>=nextForestVisibility&&(!eye.Equals(forestVisibilityEye)||float.IsNaN(forestVisibilityEye.x)||nextForestVisibility==0||forestGroundRevision!=residentRevision||forestCoverageRevision!=coverVisibilityRevision))
        {
            nextForestVisibility=Time.realtimeSinceStartupAsDouble+.20;forestVisibilityEye=eye;forestGroundRevision=residentRevision;forestCoverageRevision=coverVisibilityRevision;forestLocalRevision=canopySourceRevision;
            foreach(var field in forestFields.Values)
            {
                // Empty ready pages still own/suppress coarser coverage in the
                // hash. They have no draw records and need no hill queries.
                if(field.Source.Roots.Length==0){forestFlags[field.Id]=0;continue;}
                var bounds=field.Bounds;bounds.center-=new Vector3((float)originX,0,(float)originZ);bounds.Expand(12+(bounds.center-eye).magnitude*.04f);
                int flags=field.RenderReady&&!TerrainOccluded(bounds,eye)?1:0;
                if(flags!=0&&field.Source.Key.Level==0)
                {
                    var page=field.Source.Key;int pagesPerChunk=LocalScatter.TreeCandidateSide/LocalScatter.ForestPageSide;
                    var local=new ScatterKey(page.Face,page.X/pagesPerChunk,page.Y/pagesPerChunk);
                    if(!scatterChunks.TryGetValue(local,out var chunk)||!chunk.TreesIncluded||chunk.Groups==null||!ReferenceEquals(chunk.GroupsFrame,frame))flags|=2;
                }
                else if(flags!=0)flags|=2;
                forestFlags[field.Id]=flags;
            }
            forestFlagBuffer.SetData(forestFlags);forestRevision++;
        }
        GeometryUtility.CalculateFrustumPlanes(view,forestPlaneWork);for(int i=0;i<6;i++)forestPlanes[i]=new Vector4(forestPlaneWork[i].normal.x,forestPlaneWork[i].normal.y,forestPlaneWork[i].normal.z,forestPlaneWork[i].distance);
        float reach=(float)ForestHandoffReach,start=(float)VegetationQuality.ForestStart(reach);var chart=new Vector4((float)originX,0,(float)originZ,TerrainVideoOptions.TreeDensity);var ranges=new Vector4(start,reach,(float)ForestBillboardEnd,forestHashMask);var matrix=view.projectionMatrix*view.worldToCameraMatrix;
        forestCull.SetVectorArray("_Planes",forestPlanes);forestCull.SetVector("_Eye",eye);forestCull.SetVector("_Chart",chart);forestCull.SetVector("_Ranges",ranges);forestCull.SetBuffer(0,"_Fields",forestFlagBuffer);forestCull.SetBuffer(0,"_Tiles",forestHashBuffer);
        forestCull.SetBuffer(0,"_ForestBirth",forestBirthBuffer);forestCull.SetFloat("_ForestClock",Time.realtimeSinceStartup);
        if(forestGroundDummy==null){forestGroundDummy=new GraphicsBuffer(GraphicsBuffer.Target.Structured,1,128);forestGroundDummy.SetData(new[]{default(ForestGroundGrid)});}
        var planet0=new Vector4((float)frame.Origin.X,(float)frame.Origin.Y,(float)frame.Origin.Z,(float)frame.RadiusM);
        var planet1=new Vector4((float)frame.East.X,(float)frame.East.Y,(float)frame.East.Z,0);
        var planet2=new Vector4((float)frame.North.X,(float)frame.North.Y,(float)frame.North.Z,0);
        Shader.SetGlobalVector("_ForestPlanet0",planet0);Shader.SetGlobalVector("_ForestPlanet1",planet1);Shader.SetGlobalVector("_ForestPlanet2",planet2);Shader.SetGlobalInt("_ForestLatticeCount",scatter.TreeLatticeCount);
        foreach(var batch in forestBatches.Values)
        {
            var model=batch.Model;if(!model.TreeBaked||batch.Data.Count==0)continue;CompactForest(batch);
            // Compaction can remove the final dead slots. Never dispatch zero
            // groups or reuse an earlier indirect instance count for that batch.
            if(batch.Data.Count==0){batch.CullValid=false;continue;}
            if(batch.Capacity<batch.Data.Count)
            {
                batch.Dispose();batch.Capacity=Mathf.NextPowerOfTwo(Math.Max(64,batch.Data.Count));batch.Roots=new GraphicsBuffer(GraphicsBuffer.Target.Structured,batch.Capacity,64);batch.VisibleCapacity=Mathf.NextPowerOfTwo(Math.Max(64,batch.VisibleBudget));batch.Visible=new GraphicsBuffer(GraphicsBuffer.Target.Append,batch.VisibleCapacity,4);batch.Args=new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,1,GraphicsBuffer.IndirectDrawArgs.size);
                if(model.TreeCluster)batch.Ground=new GraphicsBuffer(GraphicsBuffer.Target.Structured,batch.Capacity,128);
                batch.Args.SetData(new[]{new GraphicsBuffer.IndirectDrawArgs{vertexCountPerInstance=6u}});DirtyAllForestRoots(batch);
            }
            if(batch.VisibleCapacity<batch.VisibleBudget)
            {batch.Visible.Dispose();batch.VisibleCapacity=Mathf.NextPowerOfTwo(batch.VisibleBudget);batch.Visible=new GraphicsBuffer(GraphicsBuffer.Target.Append,batch.VisibleCapacity,4);batch.CullValid=false;}
            UploadForestRoots(batch);
            if(!batch.CullValid||!batch.CullMatrix.Equals(matrix)||!batch.CullEye.Equals(eye)||!batch.CullChart.Equals(chart)||!batch.CullRange.Equals(ranges)||batch.CullRevision!=forestRevision)
            {
                batch.Visible.SetCounterValue(0);forestCull.SetInt("_RootCount",batch.Data.Count);forestCull.SetInt("_ForestSeed",terrain.SourceSeed);forestCull.SetInt("_ForestLatticeCount",scatter.TreeLatticeCount);forestCull.SetBuffer(0,"_Roots",batch.Roots);forestCull.SetBuffer(0,"_Ground",model.TreeCluster?batch.Ground:forestGroundDummy);forestCull.SetBuffer(0,"_Visible",batch.Visible);forestCull.Dispatch(0,(batch.Data.Count+63)/64,1,1);GraphicsBuffer.CopyCount(batch.Visible,batch.Args,4);
                batch.CullValid=true;batch.CullMatrix=matrix;batch.CullEye=eye;batch.CullChart=chart;batch.CullRange=ranges;batch.CullRevision=forestRevision;
            }
            var props=batch.Properties;props.SetFloat("_ForestMacro",model.TreeCluster?2:batch.Macro?1:0);props.SetFloat("_ForestCanonical",model.TreeCluster?1:0);props.SetBuffer("_Roots",batch.Roots);props.SetBuffer("_Visible",batch.Visible);props.SetVector("_ForestChart",chart);props.SetVector("_ForestRange",new Vector4(start,reach,0,0));
            props.SetBuffer("_ForestBirth",forestBirthBuffer);props.SetBuffer("_Tiles",forestHashBuffer);props.SetFloat("_ForestClock",Time.realtimeSinceStartup);props.SetInt("_ForestHashMask",forestHashMask);
            props.SetBuffer("_Fields",forestFlagBuffer);
            props.SetBuffer("_Ground",model.TreeCluster?batch.Ground:forestGroundDummy);props.SetVector("_ForestPlanet0",planet0);props.SetVector("_ForestPlanet1",planet1);props.SetVector("_ForestPlanet2",planet2);props.SetInt("_ForestLatticeCount",scatter.TreeLatticeCount);props.SetInt("_ForestSeed",terrain.SourceSeed);props.SetFloat("_ForestPacketFeather",LocalTreePrototype.PacketFeather);
            var parameters=new RenderParams(model.Parts[0].Material){camera=view,layer=30,worldBounds=new Bounds(eye,Vector3.one*(float)ForestPlanningRange*2.2f),matProps=props,shadowCastingMode=ShadowCastingMode.Off,receiveShadows=true};
            Graphics.RenderPrimitivesIndirect(in parameters,MeshTopology.Triangles,batch.Args,1);batch.SubmittedFrame=Time.frameCount;ForestDrawCalls++;
        }
        bool published=false;
        foreach(var field in forestFields.Values)
        {
            if(field.Published||!field.Ready||field.Source.Roots.Length==0)continue;
            bool submitted=true;
            foreach(var batch in field.Roots.Keys)if(batch.SubmittedFrame!=Time.frameCount){submitted=false;break;}
            if(submitted){field.Published=true;MarkForestBirth(field);published=true;}
        }
        // CPU-ready art must not retire local trees before every replacement
        // batch has uploaded its roots and submitted an indirect draw.
        if(published){forestHashDirty=true;forestOwnershipEpoch++;scatterDirty=true;forestMaintenanceDirty=true;}
    }
    void ResetForestFields()
    {
        ClearForestProjection();
        flightForestPriority=Array.Empty<ForestPageKey>();flightForestWanted.Clear();flightForestNext=0;flightForestCoverage=false;
        residentForestReady=false;residentForestEpoch=-1;nextResidentForestCheck=0;
        foreach(var work in forestJobs){work.Cancellation.Cancel();var stop=work.Cancellation;work.Task.ContinueWith(_=>stop.Dispose(),TaskScheduler.Default);}forestJobs.Clear();
        foreach(var batch in forestBatches.Values)batch.Dispose();forestBatches.Clear();forestFields.Clear();forestFreeIds.Clear();forestRootCount=forestNextId=0;
        forestFlagBuffer?.Dispose();forestHashBuffer?.Dispose();forestGroundDummy?.Dispose();forestGroundDummy=null;forestFlagBuffer=forestHashBuffer=null;forestCull=null;Array.Clear(forestFlags,0,forestFlags.Length);forestHashDirty=true;nextForestVisibility=0;
        forestBirthBuffer?.Dispose();forestBirthBuffer=null;Array.Clear(forestBirth,0,forestBirth.Length);
        forestOwnershipEpoch++;forestOwnership.Clear();
        forestFootprint=null;forestFootprintEpoch=-1;forestCoveragePlanned=false;forestRevealBounds.Clear();forestUnavailable=false;forestRetryAt=0;
        terrainForestPages.Clear();terrainForestRequired.Clear();
    }
}
