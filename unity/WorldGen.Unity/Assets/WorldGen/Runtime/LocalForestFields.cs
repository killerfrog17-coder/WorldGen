using System;
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
        public ArtModel Model;public V Pigment;public readonly List<ForestRoot> Data=new List<ForestRoot>();public readonly Stack<int> Free=new Stack<int>();
        public GraphicsBuffer Roots,Visible,Args;public int Capacity;public bool Dirty=true,CullValid;public Matrix4x4 CullMatrix;public Vector3 CullEye;public Vector4 CullChart,CullRange;public int CullRevision;
        public MaterialPropertyBlock Properties=new MaterialPropertyBlock();
        public void Dispose(){Roots?.Dispose();Visible?.Dispose();Args?.Dispose();CullValid=false;}
    }
    sealed class ForestField
    {
        public ForestPage Source;public int Id;public Bounds Bounds;public bool WasReady;
        public readonly Dictionary<ForestBatch,List<int>> Roots=new Dictionary<ForestBatch,List<int>>();
        public bool Ready=>WasReady;
        public bool EvaluateReady(){foreach(var batch in Roots.Keys)if(!batch.Model.TreeBaked)return false;return true;}
    }
    sealed class ForestWork {public ForestPageKey Key;public Task<ForestPage> Task;public CancellationTokenSource Cancellation;}
    readonly Dictionary<ForestPageKey,ForestField> forestFields=new Dictionary<ForestPageKey,ForestField>();
    readonly Dictionary<int,ForestBatch> forestBatches=new Dictionary<int,ForestBatch>();
    readonly List<ForestWork> forestJobs=new List<ForestWork>();readonly HashSet<ForestPageKey> forestWanted=new HashSet<ForestPageKey>();
    readonly Dictionary<ForestPageKey,bool> forestOwnership=new Dictionary<ForestPageKey,bool>();int forestOwnershipRevision=-1,forestOwnershipEpoch;
    ForestPageKey[] forestPriority=Array.Empty<ForestPageKey>();Task<ForestPageKey[]> forestPlan;
    readonly Stack<int> forestFreeIds=new Stack<int>();int forestNextId,forestRootCount,forestHashMask,forestRevision,forestOptions=-1,forestNextRequest;bool forestMaintenanceDirty;
    readonly int[] forestFlags=new int[8192];GraphicsBuffer forestFlagBuffer,forestHashBuffer;
    ComputeShader forestCull;bool forestHashDirty;LocalTerrainFrame forestFrame;V forestPlanAt;double forestPlanRange=-1,nextForestVisibility;int forestGroundRevision=-1,forestCoverageRevision=-1;
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
            if(scatterChunks.ContainsKey(pair.Key))continue;
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
    double ForestHandoffReach=>Math.Min(TerrainVideoOptions.TreeIdentityRange,Math.Max(350,TerrainVideoOptions.TreeFarLod*2.7));
    public bool ForestPending=>forestPlan!=null||forestJobs.Count>0||forestPriority.Any(k=>!forestFields.ContainsKey(k))||forestFields.Values.Any(f=>!f.Ready);
    static uint ForestMix(uint x){x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16);}
    static uint ForestKeyHash(int x,int z,int level,int face=0)=>ForestMix(unchecked((uint)x*73856093u^(uint)z*19349663u^(uint)level*83492791u^(uint)face*15485863u));
    ArtModel ForestArt(int code)
    {
        code=TreeFlora.RenderCode(code);long key=ScatterArtKey(ScatterKind.GeneratedTree,code,2)|(1L<<49);
        if(scatterArt.TryGetValue(key,out var model))return model;
        model=CreateRebuiltTree(key,code,2);model.TreeStand=true;return model;
    }
    bool ForestOwnsTree(ScatterInstance tree)
    {
        int side=LocalScatter.TreeCandidateSide,gx=(int)((tree.Id>>40)&0xfffff)*side+(int)(tree.Id&65535)%side,gy=(int)((tree.Id>>20)&0xfffff)*side+(int)(tree.Id&65535)/side;
        var key=new ForestPageKey((int)(tree.Id>>61),gx/LocalScatter.ForestPageSide,gy/LocalScatter.ForestPageSide,0);
        if(forestOwnershipRevision!=forestOwnershipEpoch){forestOwnership.Clear();forestOwnershipRevision=forestOwnershipEpoch;}
        if(!WorldGenPerformance.ForceFreshForestOwnership&&forestOwnership.TryGetValue(key,out bool owned))return owned;
        var query=key;for(int level=0;level<=24;level++,query=query.Parent)if(forestFields.TryGetValue(query,out var field)&&field.Ready){forestOwnership[key]=true;return true;}
        forestOwnership[key]=false;return false;
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
        if(forestCull==null){forestCull=Resources.Load<ComputeShader>("WorldGenForestCull");if(forestCull==null){forestUnavailable=true;Debug.LogWarning("Forest compute shader missing; using local tree coverage.");return;}forestFlagBuffer=new GraphicsBuffer(GraphicsBuffer.Target.Structured,forestFlags.Length,4);}
        foreach(var batch in forestBatches.Values)batch.Model.LastUsed=Time.realtimeSinceStartupAsDouble;
        if(!ReferenceEquals(forestFrame,frame))
        {
            // Reproject immutable planet-addressed payloads; never reseed a forest
            // because its terrain chart was replaced.
            forestFrame=frame;foreach(var batch in forestBatches.Values){batch.Data.Clear();batch.Free.Clear();batch.Dirty=true;}
            forestRootCount=0;foreach(var field in forestFields.Values){field.Roots.Clear();ProjectForest(field);}forestRevision++;forestHashDirty=true;
        }
        if(forestPlan!=null&&forestPlan.IsCompleted)
        {
            if(forestPlan.IsCompletedSuccessfully)
            {
                forestPriority=forestPlan.Result;forestWanted.Clear();foreach(var key in forestPriority)forestWanted.Add(key);forestNextRequest=0;forestMaintenanceDirty=true;
                forestCoveragePlanned=true;forestCoverageAt=forestPlanAt;forestCoverageRange=forestPlanRange;forestRevealBounds.Clear();fogCheckAt=0;
                foreach(var work in forestJobs)if(!forestWanted.Contains(work.Key))work.Cancellation.Cancel();
            }
            else if(forestPlan.IsFaulted)Debug.LogWarning("Forest plan: "+forestPlan.Exception.GetBaseException().Message);
            forestPlan=null;
        }
        double moved=(Position-forestPlanAt).Length*frame.RadiusM;
        if(forestPlan==null&&(forestPlanRange!=viewDistance||moved>128||forestOptions!=TerrainVideoOptions.VegetationRevision))
        {
            if(forestOptions!=TerrainVideoOptions.VegetationRevision)nextForestVisibility=0;
            forestPlanAt=Position;forestPlanRange=viewDistance;forestOptions=TerrainVideoOptions.VegetationRevision;
            var at=Position;double range=viewDistance,exact=ForestHandoffReach+256,inner=Math.Max(0,VegetationQuality.ForestStart(ForestHandoffReach)-160);var source=scatter;
            forestPlan=Task.Run(()=>source.PlanForest(at,range,exact,4096,inner),cancellation.Token);
        }
        if(forestHashDirty)foreach(var field in forestFields.Values)
        {bool ready=field.EvaluateReady();if(ready!=field.WasReady){field.WasReady=ready;scatterDirty=true;forestRevision++;forestOwnershipEpoch++;forestMaintenanceDirty=true;}}
        for(int i=forestJobs.Count-1;i>=0;i--)
        {
            var job=forestJobs[i];if(!job.Task.IsCompleted)continue;forestJobs.RemoveAt(i);job.Cancellation.Dispose();
            if(job.Task.IsCanceled){forestNextRequest=0;continue;} // Retry interrupted coverage after near-ground pressure ends.
            if(job.Task.IsFaulted){Debug.LogWarning("Forest page retained fallback: "+job.Task.Exception.GetBaseException().Message);forestNextRequest=0;forestRetryAt=Time.realtimeSinceStartupAsDouble+2;continue;}
            if(!job.Task.IsCompletedSuccessfully||!forestWanted.Contains(job.Key))continue;
            if(forestFreeIds.Count==0&&forestNextId>=forestFlags.Length)break;
            var field=new ForestField{Source=job.Task.Result,Id=forestFreeIds.Count>0?forestFreeIds.Pop():forestNextId++};forestFields.Add(job.Key,field);ProjectForest(field);forestHashDirty=true;forestRevision++;forestOwnershipEpoch++;scatterDirty=true;forestMaintenanceDirty=true;
            break; // Bounded Unity object / buffer installation work.
        }
        if(forestPlan==null&&forestMaintenanceDirty)
        {
        forestMaintenanceDirty=false;
        foreach(var key in forestFields.Keys.Where(k=>!forestWanted.Contains(k)).ToArray())
        {
            var f=forestFields[key];scatter.ForestBounds(key,out V center,out double radius);
            if((center-Position).Length*frame.RadiusM-radius>viewDistance+512||(center-Position).Length*frame.RadiusM+radius<VegetationQuality.ForestStart(ForestHandoffReach)-128||ForestReplacementReady(key))RemoveForestField(key);
        }
        }
    }
    bool ScheduleForestWork()
    {
        // Near ground retains priority. When forest coverage is the reveal
        // blocker, it also gets a turn before distant terrain fills every lane.
        if(!TerrainVideoOptions.GeneratedTrees||!TerrainVideoOptions.TreeRebuild||!TerrainVideoOptions.TreeImpostors||!TerrainVideoOptions.FarForest||!Ready||!CenterReady||forestCull==null||NearTerrainPending()||mapOpen||Time.realtimeSinceStartupAsDouble<forestRetryAt||ActiveWorkerCount>=WorkerLimit||forestJobs.Count>=Math.Min(2,WorkerLimit)||forestRootCount>=524288)return false;
        while(forestNextRequest<forestPriority.Length)
        {
            var key=forestPriority[forestNextRequest++];
            if(forestFields.ContainsKey(key)||forestJobs.Any(j=>j.Key.Equals(key)))continue;
            var source=scatter;var producer=meshProcess;var stop=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);var token=stop.Token;
            forestJobs.Add(new ForestWork{Key=key,Cancellation=stop,Task=Task.Run(()=>producer==null?source.BuildForest(key,token):producer.Forest(key,token),token)});return true;
        }
        return false;
    }
    bool ForestReplacementReady(ForestPageKey key)
    {
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
    void ProjectForest(ForestField field)
    {
        bool first=true;var page=field.Source;
        foreach(var source in page.Roots)
        {
            var tree=source.Tree;int code=TreeFlora.RenderCode(tree.Variant);
            if(!forestBatches.TryGetValue(code,out var batch)){batch=new ForestBatch{Model=ForestArt(code),Pigment=new TreeMorphology(code).Leaf};forestBatches.Add(code,batch);}
            double dot=Math.Max(.1,V.Dot(tree.Position,frame.Origin)),px=V.Dot(tree.Position,frame.East)*frame.RadiusM/dot,pz=V.Dot(tree.Position,frame.North)*frame.RadiusM/dot;
            float height=(float)frame.SurfaceY(tree.Position,tree.GroundM);
            var pigment=TreePigment(tree.Variant);var shared=batch.Pigment;
            uint densityHash=Rng.Mix((uint)tree.Id^Rng.Mix((uint)(tree.Id>>32))^0x9a65c4u)&0xffffffu;
            int candidateSide=LocalScatter.TreeCandidateSide,slotId=(int)(tree.Id&65535),cellX=(int)((tree.Id>>40)&0xfffff)*candidateSide+slotId%candidateSide,cellY=(int)((tree.Id>>20)&0xfffff)*candidateSide+slotId/candidateSide;
            var root=new ForestRoot{Position=new Vector3((float)px,height-.045f,(float)pz),Width=(float)(tree.WidthM*source.CrownScale),Height=(float)tree.HeightM,Yaw=(float)(tree.YawDegrees*Math.PI/180),Field=(uint)field.Id|((uint)page.Key.Level<<16),CellX=cellX,CellY=cellY,Face=(uint)page.Key.Face|(densityHash<<8),
                Tint=new Vector3((float)(tree.Color.X*pigment.X/Math.Max(.01,shared.X)),(float)(tree.Color.Y*pigment.Y/Math.Max(.01,shared.Y)),(float)(tree.Color.Z*pigment.Z/Math.Max(.01,shared.Z))),Padding=source.CrownScale>1.001?1:0};
            ProjectForestRootGround(ref root);
            int slot;if(batch.Free.Count>0){slot=batch.Free.Pop();batch.Data[slot]=root;}else{slot=batch.Data.Count;batch.Data.Add(root);}batch.Dirty=true;forestRootCount++;
            if(!field.Roots.TryGetValue(batch,out var slots)){slots=new List<int>();field.Roots.Add(batch,slots);}slots.Add(slot);
            var b=ForestRootBounds(root);if(first){field.Bounds=b;first=false;}else field.Bounds.Encapsulate(b);
        }
        field.WasReady=field.EvaluateReady();
    }
    void ProjectForestGround()
    {
        foreach(var field in forestFields.Values)
        {
            bool affected=scatterGroundChanges.Count==0;
            foreach(var key in scatterGroundChanges)
            {var b=field.Bounds;double x=key.X*key.SizeM,z=key.Z*key.SizeM;if(b.max.x>=x&&b.min.x<=x+key.SizeM&&b.max.z>=z&&b.min.z<=z+key.SizeM){affected=true;break;}}
            if(!affected)continue;
            foreach(var pair in field.Roots)foreach(int slot in pair.Value)
            {
                var root=pair.Key.Data[slot];var before=root;ProjectForestRootGround(ref root);
                if(root.Position.y==before.Position.y&&root.SlopeX==before.SlopeX&&root.SlopeZ==before.SlopeZ)continue;pair.Key.Data[slot]=root;pair.Key.Dirty=true;
                field.Bounds.Encapsulate(ForestRootBounds(root));
            }
        }
        forestRevision++;nextForestVisibility=0;
    }
    void ProjectForestRootGround(ref ForestRoot root)
    {
        double x=root.Position.x,z=root.Position.z,y=VisibleGround(x,z,root.Position.y+.045f);
        root.Position.y=(float)y-.045f;
        // Only aggregated stands follow the broad terrain slope. Individual
        // tree silhouettes stay upright; no per-frame terrain queries are added.
        if(root.Padding==0){root.SlopeX=root.SlopeZ=0;return;}
        double step=Math.Max(2,root.Width*.5);
        root.SlopeX=(float)Math.Max(-2,Math.Min(2,(VisibleGround(x+step,z,y)-VisibleGround(x-step,z,y))/(2*step)));
        root.SlopeZ=(float)Math.Max(-2,Math.Min(2,(VisibleGround(x,z+step,y)-VisibleGround(x,z-step,y))/(2*step)));
    }
    static Bounds ForestRootBounds(ForestRoot root)=>new Bounds(root.Position+Vector3.up*root.Height*.5f,
        new Vector3(root.Width*3,root.Height*3+root.Width*3*(Math.Abs(root.SlopeX)+Math.Abs(root.SlopeZ)),root.Width*3));
    void RemoveForestField(ForestPageKey key)
    {
        var field=forestFields[key];foreach(var pair in field.Roots)foreach(int slot in pair.Value){pair.Key.Data[slot]=default;pair.Key.Free.Push(slot);pair.Key.Dirty=true;forestRootCount--;}
        forestFlags[field.Id]=0;forestFreeIds.Push(field.Id);forestFields.Remove(key);forestHashDirty=true;forestRevision++;forestOwnershipEpoch++;scatterDirty=true;
    }
    void CompactForest(ForestBatch batch)
    {
        if(batch.Free.Count<256||batch.Free.Count<batch.Data.Count/3)return;
        var map=new int[batch.Data.Count];int live=0;for(int i=0;i<batch.Data.Count;i++)if(batch.Data[i].Height>0){map[i]=live;batch.Data[live++]=batch.Data[i];}
        batch.Data.RemoveRange(live,batch.Data.Count-live);batch.Free.Clear();batch.Dirty=true;
        foreach(var field in forestFields.Values)if(field.Roots.TryGetValue(batch,out var slots))for(int i=0;i<slots.Count;i++)slots[i]=map[slots[i]];
    }
    void PrepareForestHash()
    {
        if(!forestHashDirty)return;forestHashDirty=false;int count=16;while(count<forestFields.Count*4)count*=2;
        var hash=new ForestHash[count];forestHashMask=count-1;
        foreach(var pair in forestFields)
        {
            if(!pair.Value.Ready)continue;var key=pair.Key;int slot=(int)(ForestKeyHash(key.X,key.Y,key.Level,key.Face)&(uint)forestHashMask);
            while(hash[slot].State!=0)slot=(slot+1)&forestHashMask;hash[slot]=new ForestHash{X=key.X,Z=key.Y,Level=key.Level,State=key.Face+1};
        }
        if(forestHashBuffer==null||forestHashBuffer.count!=count){forestHashBuffer?.Dispose();forestHashBuffer=new GraphicsBuffer(GraphicsBuffer.Target.Structured,count,16);}
        forestHashBuffer.SetData(hash);forestRevision++;nextForestVisibility=0;
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
        foreach(var batch in forestBatches.Values)
        {
            var model=batch.Model;if(!model.TreeBaked||batch.Data.Count==0)continue;CompactForest(batch);
            // Compaction can remove the final dead slots. Never dispatch zero
            // groups or reuse an earlier indirect instance count for that batch.
            if(batch.Data.Count==0){batch.CullValid=false;continue;}
            if(batch.Capacity<batch.Data.Count)
            {
                batch.Dispose();batch.Capacity=Mathf.NextPowerOfTwo(Math.Max(64,batch.Data.Count));batch.Roots=new GraphicsBuffer(GraphicsBuffer.Target.Structured,batch.Capacity,64);batch.Visible=new GraphicsBuffer(GraphicsBuffer.Target.Append,batch.Capacity,4);batch.Args=new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,1,GraphicsBuffer.IndirectDrawArgs.size);
                batch.Args.SetData(new[]{new GraphicsBuffer.IndirectDrawArgs{vertexCountPerInstance=6}});batch.Dirty=true;
            }
            if(batch.Dirty){batch.Roots.SetData(batch.Data,0,0,batch.Data.Count);batch.Dirty=false;batch.CullValid=false;}
            if(!batch.CullValid||!batch.CullMatrix.Equals(matrix)||!batch.CullEye.Equals(eye)||!batch.CullChart.Equals(chart)||!batch.CullRange.Equals(ranges)||batch.CullRevision!=forestRevision)
            {
                batch.Visible.SetCounterValue(0);forestCull.SetInt("_RootCount",batch.Data.Count);forestCull.SetBuffer(0,"_Roots",batch.Roots);forestCull.SetBuffer(0,"_Visible",batch.Visible);forestCull.Dispatch(0,(batch.Data.Count+63)/64,1,1);GraphicsBuffer.CopyCount(batch.Visible,batch.Args,4);
                batch.CullValid=true;batch.CullMatrix=matrix;batch.CullEye=eye;batch.CullChart=chart;batch.CullRange=ranges;batch.CullRevision=forestRevision;
            }
            var props=batch.Properties;props.SetBuffer("_Roots",batch.Roots);props.SetBuffer("_Visible",batch.Visible);props.SetVector("_ForestChart",chart);props.SetVector("_ForestRange",new Vector4(start,reach,0,0));
            var parameters=new RenderParams(model.Parts[0].Material){camera=view,layer=30,worldBounds=new Bounds(eye,Vector3.one*(float)viewDistance*2.2f),matProps=props,shadowCastingMode=ShadowCastingMode.Off,receiveShadows=true};
            Graphics.RenderPrimitivesIndirect(in parameters,MeshTopology.Triangles,batch.Args,1);ForestDrawCalls++;
        }
    }
    void ResetForestFields()
    {
        foreach(var work in forestJobs){work.Cancellation.Cancel();var stop=work.Cancellation;work.Task.ContinueWith(_=>stop.Dispose(),TaskScheduler.Default);}forestJobs.Clear();
        foreach(var batch in forestBatches.Values)batch.Dispose();forestBatches.Clear();forestFields.Clear();forestFreeIds.Clear();forestRootCount=forestNextId=0;
        forestFlagBuffer?.Dispose();forestHashBuffer?.Dispose();forestFlagBuffer=forestHashBuffer=null;forestCull=null;Array.Clear(forestFlags,0,forestFlags.Length);forestHashDirty=true;nextForestVisibility=0;
        forestOwnershipEpoch++;forestOwnership.Clear();
        forestFootprint=null;forestFootprintEpoch=-1;forestCoveragePlanned=false;forestRevealBounds.Clear();forestUnavailable=false;forestRetryAt=0;
    }
}
