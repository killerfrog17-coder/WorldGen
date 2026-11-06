using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    // Persistent native mirrors of immutable scatter and resident height data.
    // Only admission runs in Burst; art, engine objects and draw order remain
    // on the main thread. All jobs join before any terrain can be published.
    struct BurstGroup {public Bounds Bounds,Roots;public int Start,Count;public ScatterKind Kind;public double Width;}
    struct BurstGroupResult {public ScatterCounts Counts;public int Count;public bool Managed;}
    struct BurstOwner {public int Epoch;public bool Owned;}
    struct BurstSlab {public int Start,Segments,Stride,Side;}
    sealed class BurstChunk : IDisposable
    {
        public NativeArray<ScatterInstance> Source;
        public NativeArray<Vector3> Roots;
        public NativeArray<Bounds> Bounds;
        public NativeArray<int> Indices;
        public NativeArray<BurstGroup> Groups;
        public NativeArray<BurstOwner> Owners;
        public NativeArray<BurstGroupResult> Results;
        public NativeArray<ScatterAdmission> Admissions;
        public ScatterGroup[] GroupSource;public int Revision=-1;
        public void Dispose(){if(Source.IsCreated)Source.Dispose();if(Roots.IsCreated)Roots.Dispose();if(Bounds.IsCreated)Bounds.Dispose();if(Indices.IsCreated)Indices.Dispose();if(Groups.IsCreated)Groups.Dispose();if(Owners.IsCreated)Owners.Dispose();if(Results.IsCreated)Results.Dispose();if(Admissions.IsCreated)Admissions.Dispose();}
    }
    struct BurstScatterOptions
    {
        public Vector3 Eye;public float Slack,ShadowRadius,ShadowDistance;public double ShadowSlope,OriginX,OriginZ,TileSize,Ceiling;
        public bool AllHeadings,Frustum,Occlusion,GpuOcclusion,Specialists,Mid,Tree,Rebuilt,Impostors,FarForest,NoTrees,IndividualTrees,MidShadows,TreeShadows;
        public int MidDetail,TreeDetail,SpecialistDetail,ForestEpoch;
        public float TreeDensity,MidDensity,SpecialistDensity,TreeNear,TreeFar,MidNear,MidFar,SpecialistNear;
        public double TreeIdentity,TreeMaximum,MidMaximum,HerbMaximum,FungusMaximum,Handoff;
        public bool Camp;public double CampX,CampZ,Radius;public V East,North,Origin;
    }
    // The managed player's IL evaluation retains wider intermediates for the
    // magnitude expression. Make that precision explicit in the native job;
    // roots/subtraction still round to Vector3 floats before this sum.
    static float ScatterDistance(Vector3 a,Vector3 b)
    {
        float x=a.x-b.x,y=a.y-b.y,z=a.z-b.z;
        double square=(double)x*x+(double)y*y+(double)z*z;return (float)Math.Sqrt(square);
    }
    [BurstCompile(FloatMode=FloatMode.Strict,FloatPrecision=FloatPrecision.High,CompileSynchronously=true)]
    struct ScatterAdmissionJob : IJobParallelFor, IJob
    {
        [ReadOnly] public NativeArray<ScatterInstance> Source;
        [ReadOnly] public NativeArray<Vector3> Roots;
        [ReadOnly] public NativeArray<Bounds> Bounds;
        [ReadOnly] public NativeArray<int> Indices;
        [ReadOnly] public NativeArray<BurstGroup> Groups;
        [ReadOnly] public NativeArray<Plane> Planes;
        [ReadOnly] public NativeArray<byte> Casts,CampMask;
        [ReadOnly] public NativeParallelHashSet<ulong> Collected;
        [ReadOnly] public NativeParallelHashSet<ForestPageKey> Forest;
        [ReadOnly] public NativeParallelHashMap<long,int> Slabs;
        [ReadOnly] public NativeArray<BurstSlab> SlabInfo;
        [ReadOnly] public NativeArray<float> SlabMinimum;
        [NativeDisableParallelForRestriction] public NativeArray<BurstOwner> Owners;
        [NativeDisableParallelForRestriction] public NativeArray<BurstGroupResult> Results;
        [NativeDisableParallelForRestriction] public NativeArray<ScatterAdmission> Admissions;
        public BurstScatterOptions Options;public bool Grouped;
        [BurstDiscard] static void ManagedExecution(ref bool managed){managed=true;}
        public void Execute()
        {
            // Resident patches already provide ample independent jobs. Avoid
            // scheduling four tiny work items for every patch on a camera move.
            bool managed=false;ManagedExecution(ref managed);
            for(int index=0;index<Groups.Length;index++)
            {var result=Classify(index);result.Managed=managed;Results[index]=result;}
        }
        public void Execute(int partition)
        {
            bool managed=false;ManagedExecution(ref managed);
            for(int index=partition;index<Groups.Length;index+=4)
            {var result=Classify(index);result.Managed=managed;Results[index]=result;}
        }
        static bool Specialist(ScatterKind kind)=>kind==ScatterKind.Herb||kind==ScatterKind.Fungus;
        static float DistanceSquared(Bounds bounds,Vector3 eye)
        {
            var d=bounds.center-eye;var e=bounds.extents;
            float x=Math.Max(0,Math.Abs(d.x)-e.x),y=Math.Max(0,Math.Abs(d.y)-e.y),z=Math.Max(0,Math.Abs(d.z)-e.z);
            return x*x+y*y+z*z;
        }
        bool Shadow(Bounds bounds)
        {
            if(Options.ShadowRadius<=0)return false;
            float reach=(float)VegetationShadowBudget.ReachWithSlope(Options.ShadowDistance,bounds.max.y-Options.Eye.y,Options.ShadowSlope);
            return DistanceSquared(bounds,Options.Eye)<reach*reach;
        }
        bool InFrustum(Bounds bounds)
        {
            if(Options.AllHeadings)return true;
            if(Options.Slack>0)
            {
                var d=bounds.center-Options.Eye;var e=bounds.extents;
                float reach=Math.Abs(d.x)+Math.Abs(d.y)+Math.Abs(d.z)+e.x+e.y+e.z;bounds.Expand(2*Options.Slack*reach);
            }
            var center=bounds.center;var extent=bounds.extents;
            for(int i=0;i<6;i++)
            {var n=Planes[i].normal;float radius=Math.Abs(n.x)*extent.x+Math.Abs(n.y)*extent.y+Math.Abs(n.z)*extent.z;if(n.x*center.x+n.y*center.y+n.z*center.z+Planes[i].distance+radius<0)return false;}
            return true;
        }
        bool FullyInside(Bounds bounds)
        {
            var center=bounds.center;var extent=bounds.extents;
            for(int i=0;i<6;i++)
            {var n=Planes[i].normal;float radius=Math.Abs(n.x)*extent.x+Math.Abs(n.y)*extent.y+Math.Abs(n.z)*extent.z;if(Vector3.Dot(n,center)+Planes[i].distance-radius<=.001f)return false;}
            return true;
        }
        bool Hidden(Bounds bounds)
        {
            if(!Options.Occlusion||DistanceSquared(bounds,Options.Eye)<45*45||Options.Ceiling<=Math.Min(Options.Eye.y,bounds.max.y))return false;
            bounds.Expand(4);var ext=bounds.extents;
            for(int step=1;step<=7;step++)
            {
                float t=step*.125f;var center=Vector3.Lerp(Options.Eye,bounds.center,t);var r=ext*t+Vector3.one*.75f;
                double x0=center.x-r.x+Options.OriginX,x1=center.x+r.x+Options.OriginX,z0=center.z-r.z+Options.OriginZ,z1=center.z+r.z+Options.OriginZ,size=Options.TileSize;
                int x=(int)Math.Floor(x0/size),z=(int)Math.Floor(z0/size);
                if((int)Math.Floor(x1/size)!=x||(int)Math.Floor(z1/size)!=z||!Slabs.TryGetValue(((long)x<<32)|(uint)z,out int tile))continue;
                var slab=SlabInfo[tile];int n=slab.Segments,stride=slab.Stride,side=slab.Side;
                int Clamp(int v)=>Math.Max(0,Math.Min(side-1,v));
                int ix0=Clamp((int)Math.Floor((x0/size-x)*n)/stride),ix1=Clamp((int)Math.Floor((x1/size-x)*n)/stride),iz0=Clamp((int)Math.Floor((z0/size-z)*n)/stride),iz1=Clamp((int)Math.Floor((z1/size-z)*n)/stride);
                float highest=center.y+r.y+2;bool blocked=true;
                for(int iz=iz0;iz<=iz1&&blocked;iz++)for(int ix=ix0;ix<=ix1;ix++)if(SlabMinimum[slab.Start+iz*side+ix]<=highest){blocked=false;break;}
                if(blocked)return true;
            }
            return false;
        }
        bool ForestOwns(ulong id)
        {
            int side=LocalScatter.TreeCandidateSide,gx=(int)((id>>40)&0xfffff)*side+(int)(id&65535)%side,gy=(int)((id>>20)&0xfffff)*side+(int)(id&65535)/side;
            var key=new ForestPageKey((int)(id>>61),gx/LocalScatter.ForestPageSide,gy/LocalScatter.ForestPageSide,0);
            for(int level=0;level<=24;level++,key=key.Parent)if(Forest.Contains(key))return true;
            return false;
        }
        bool ForestOwnsGroup(int index)
        {
            var cached=Owners[index];if(cached.Epoch==Options.ForestEpoch)return cached.Owned;
            var group=Groups[index];cached.Epoch=Options.ForestEpoch;cached.Owned=true;
            for(int j=0;j<group.Count;j++)if(!ForestOwns(Source[Indices[group.Start+j]].Id)){cached.Owned=false;break;}
            Owners[index]=cached;return cached.Owned;
        }
        double MaximumDistance(ScatterKind kind,double width)
        {
            if(kind==ScatterKind.GeneratedTree){double reach=Options.Rebuilt&&Options.Impostors?Options.TreeIdentity:Options.TreeMaximum;return ScatterLodBlend.MaximumDistance(kind,Options.Rebuilt?Math.Max(width,reach/150):width,reach);}
            return ScatterLodBlend.MaximumDistance(kind,width,kind==ScatterKind.MidPlant?Options.MidMaximum:kind==ScatterKind.Herb?Options.HerbMaximum:kind==ScatterKind.Fungus?Options.FungusMaximum:double.PositiveInfinity);
        }
        ScatterBlend Blend(ScatterKind kind,double width,double distance)
        {
            if(kind==ScatterKind.GeneratedTree)
            {double reach=Options.Rebuilt&&Options.Impostors?Options.TreeIdentity:Options.TreeMaximum,size=Options.Rebuilt?Math.Max(.72,Math.Min(1.35,width/10)):1;return ScatterLodBlend.At(kind,Options.Rebuilt?Math.Max(width,reach/150):width,distance,Options.TreeNear*size,Options.TreeFar*size,reach);}
            if(kind==ScatterKind.MidPlant)return ScatterLodBlend.At(kind,width,distance,Options.MidNear,Options.MidFar,Options.MidMaximum);
            if(Specialist(kind))return ScatterLodBlend.At(kind,width,distance,Options.SpecialistNear,0,kind==ScatterKind.Fungus?Options.FungusMaximum:Options.HerbMaximum);
            return ScatterLodBlend.At(kind,width,distance);
        }
        bool Cleared(ScatterInstance item)
        {
            bool plant=item.Kind==ScatterKind.MidPlant||item.Kind==ScatterKind.Shrub||Specialist(item.Kind)||item.Kind==ScatterKind.GeneratedTree&&item.HeightM<1.8;
            if(!plant||!Options.Camp)return false;
            double dot=V.Dot(item.Position,Options.Origin),x=V.Dot(item.Position,Options.East)*Options.Radius/dot,z=V.Dot(item.Position,Options.North)*Options.Radius/dot;
            int px=(int)Math.Floor(x-Options.CampX),pz=(int)Math.Floor(z-Options.CampZ);
            return px>=0&&pz>=0&&px<CampMaskSide&&pz<CampMaskSide&&CampMask[pz*CampMaskSide+px]>160;
        }
        void Admit(ref BurstGroupResult result,int start,int index,int lod,float low,float high,bool shadow)
        {if(high-low>=.0001f)Admissions[start+result.Count++]=new ScatterAdmission{Index=index,Lod=lod,Low=low,High=high,ShadowOnly=shadow};}
        BurstGroupResult Classify(int index)
        {
            var result=new BurstGroupResult();var group=Groups[index];bool inside=!Options.Frustum||Options.AllHeadings,outside=false,hidden=false;
            if(Grouped)
            {
                double range=MaximumDistance(group.Kind,group.Width)+.1;
                if(DistanceSquared(group.Roots,Options.Eye)>=range*range){result.Counts.Frustum=group.Count;return result;}
                if(Specialist(group.Kind)&&!Options.Specialists||group.Kind==ScatterKind.MidPlant&&!Options.Mid)return result;
                if(group.Kind==ScatterKind.GeneratedTree&&!Options.IndividualTrees)
                {
                    double local=Options.TreeIdentity+.1,field=Options.Handoff+.1;
                    if(!Options.Tree||Options.NoTrees||DistanceSquared(group.Roots,Options.Eye)>=local*local)return result;
                    if(Options.Rebuilt&&Options.Impostors&&Options.FarForest&&DistanceSquared(group.Roots,Options.Eye)>=field*field&&ForestOwnsGroup(index))return result;
                }
                if(Options.Frustum||Options.Occlusion)
                {
                    var bounds=group.Bounds;bounds.Expand(10+((bounds.center-Options.Eye).magnitude+bounds.extents.magnitude)*.04f);bool casts=Casts[(int)group.Kind]!=0;
                    outside=Options.Frustum&&!InFrustum(bounds);
                    if(outside&&(!casts||!Shadow(bounds))){result.Counts.Frustum=group.Count;return result;}
                    inside|=!outside&&FullyInside(bounds);hidden=!outside&&!Options.GpuOcclusion&&Hidden(bounds);
                    if(hidden&&(!casts||!Shadow(bounds))){result.Counts.Terrain=group.Count;return result;}
                }
            }
            for(int member=0;member<group.Count;member++)
            {
                int i=Indices[group.Start+member];var p=Source[i];double distance=ScatterDistance(Roots[i],Options.Eye);
                if((p.Kind==ScatterKind.Stone||p.Kind==ScatterKind.Boulder||p.Kind==ScatterKind.Branch||p.Kind==ScatterKind.Fungus)&&Collected.Contains(p.Id)||Cleared(p))continue;
                if(Specialist(p.Kind)&&!Options.Specialists||p.Kind==ScatterKind.MidPlant&&!Options.Mid||p.Kind==ScatterKind.GeneratedTree&&(!Options.Tree||Options.NoTrees||distance>=Options.TreeIdentity))continue;
                float density=p.Kind==ScatterKind.GeneratedTree?Options.TreeDensity:p.Kind==ScatterKind.MidPlant?Options.MidDensity:Specialist(p.Kind)?Options.SpecialistDensity:1;
                if(density<1&&(density<=0||(Rng.Mix((uint)p.Id^Rng.Mix((uint)(p.Id>>32))^0x9a65c4u)&0xffffffu)/16777216f>=density))continue;
                var blend=Blend(p.Kind,p.WidthM,distance);
                if(p.Kind==ScatterKind.GeneratedTree&&Options.Rebuilt&&Options.Impostors&&Options.FarForest)
                {double start=Math.Min(950,Options.Handoff*.8),t=Math.Max(0,Math.Min(1,(distance-start)/Math.Max(1,Options.Handoff-start))),weight=t*t*(3-2*t);blend=new ScatterBlend(blend.Near,blend.Far,blend.Blend,distance<start?1:ForestOwns(p.Id)?1-weight:1);}
                if(blend.Visibility<=0)continue;
                var bounds=Bounds[i];bounds.Expand(6+(float)distance*.04f);
                bool frustum=outside||!inside&&Options.Frustum&&!InFrustum(bounds),occluded=!frustum&&!Options.GpuOcclusion&&(hidden||Hidden(bounds)),shadow=frustum||occluded;
                if(frustum)result.Counts.Frustum++;if(occluded)result.Counts.Terrain++;
                if(shadow&&(p.Kind==ScatterKind.Grass||Specialist(p.Kind)||!Shadow(bounds)||p.Kind==ScatterKind.MidPlant&&!Options.MidShadows||p.Kind==ScatterKind.GeneratedTree&&!Options.TreeShadows))continue;
                int minimum=p.Kind==ScatterKind.MidPlant?2-Options.MidDetail:p.Kind==ScatterKind.GeneratedTree?2-Options.TreeDetail:Specialist(p.Kind)?1-Options.SpecialistDetail:0;
                int near=Math.Max(minimum,blend.Near),far=Math.Max(minimum,blend.Far);
                if(p.Kind==ScatterKind.GeneratedTree&&Options.Rebuilt&&!Options.Impostors){near=Math.Min(1,near);far=Math.Min(1,far);}
                if(shadow&&p.Kind==ScatterKind.GeneratedTree){near=Math.Max(1,near);far=Math.Max(1,far);}
                if(near==far)Admit(ref result,group.Start*2,i,near,0,(float)blend.Visibility,shadow);
                else{float split=(float)(blend.Visibility*(1-blend.Blend));Admit(ref result,group.Start*2,i,near,0,split,shadow);Admit(ref result,group.Start*2,i,far,split,(float)blend.Visibility,shadow);}
                if(shadow){result.Counts.Shadow++;continue;}result.Counts.Objects++;if(Specialist(p.Kind))result.Counts.Specialists++;if(p.Kind==ScatterKind.MidPlant)result.Counts.Mid++;if(p.Kind==ScatterKind.GeneratedTree)result.Counts.Trees++;
            }
            return result;
        }
    }
    [BurstCompile(FloatMode=FloatMode.Strict,FloatPrecision=FloatPrecision.High,CompileSynchronously=true)]
    struct ScatterDistanceProbe : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Vector3> Points;
        [WriteOnly] public NativeArray<float> Distances;
        [WriteOnly] public NativeArray<byte> Compiled;
        [BurstDiscard] static void ManagedExecution(ref bool managed){managed=true;}
        public void Execute(int index)
        {
            bool managed=false;ManagedExecution(ref managed);Compiled[index]=managed?(byte)0:(byte)1;
            Distances[index]=ScatterDistance(Points[index],new Vector3(.123f,18.375f,-.456f));
        }
    }
    public static string PerformanceVerifyBurstDistances()
    {
        const int count=65536;
        var samples=new Vector3[count];
        uint state=72791;float Next(){state=state*1664525+1013904223;return ((state>>8)/16777216f-.5f)*4000;}
        for(int i=0;i<count;i++)samples[i]=new Vector3(Next(),Next(),Next());
        using var points=new NativeArray<Vector3>(samples,Allocator.TempJob);
        using var distances=new NativeArray<float>(count,Allocator.TempJob);
        using var compiled=new NativeArray<byte>(count,Allocator.TempJob);
        new ScatterDistanceProbe{Points=points,Distances=distances,Compiled=compiled}.Schedule(count,64).Complete();
        var eye=new Vector3(.123f,18.375f,-.456f);int changed=0;float maximum=0;string first="";
        for(int i=0;i<count;i++)
        {
            if(compiled[i]==0)throw new InvalidOperationException("Burst distance probe executed managed fallback");
            float expected=(points[i]-eye).magnitude,actual=distances[i];
            if(BitConverter.SingleToInt32Bits(expected)!=BitConverter.SingleToInt32Bits(actual))
            {changed++;maximum=Math.Max(maximum,Math.Abs(expected-actual));if(first=="")first="; first point "+points[i].ToString("R")+", reference "+expected.ToString("R")+", native "+actual.ToString("R")+", managed explicit "+ScatterDistance(points[i],eye).ToString("R");}
        }
        if(changed>0)throw new InvalidOperationException("Burst distance differs from managed Vector3.magnitude: "+changed+"/"+count+", max "+maximum.ToString("R")+first);
        return "PASS "+count+" bit-exact Burst-compiled/managed distances across the resident scatter range";
    }
    NativeArray<Plane> burstPlanes;
    NativeArray<byte> burstCasts,burstCamp;
    NativeParallelHashSet<ulong> burstCollected;
    NativeParallelHashSet<ForestPageKey> burstForest;
    NativeParallelHashMap<long,int> burstSlabs;
    NativeArray<BurstSlab> burstSlabInfo;
    NativeArray<float> burstSlabMinimum;
    readonly List<JobHandle> burstJobs=new List<JobHandle>();
    readonly List<ScatterChunk> burstScheduled=new List<ScatterChunk>();
    int burstCollectedCount=-1,burstCampRevision=-1;bool burstUnavailable;
    public int BurstScatterBuilds {get;private set;}
    static void BurstArray<T>(ref NativeArray<T> data,int count) where T:unmanaged
    {count=Math.Max(1,count);if(data.IsCreated&&data.Length>=count)return;if(data.IsCreated)data.Dispose();data=new NativeArray<T>(Mathf.NextPowerOfTwo(count),Allocator.Persistent,NativeArrayOptions.UninitializedMemory);}
    void PrepareBurstChunk(ScatterChunk chunk)
    {
        int count=chunk.Source.Instances.Length;var data=chunk.Burst;
        if(data==null){data=chunk.Burst=new BurstChunk();data.Source=new NativeArray<ScatterInstance>(chunk.Source.Instances,Allocator.Persistent);data.Roots=new NativeArray<Vector3>(count,Allocator.Persistent);data.Bounds=new NativeArray<Bounds>(count,Allocator.Persistent);data.Admissions=new NativeArray<ScatterAdmission>(count*2,Allocator.Persistent);}
        if(!ReferenceEquals(data.GroupSource,chunk.Groups))
        {
            if(data.Indices.IsCreated){data.Indices.Dispose();data.Groups.Dispose();data.Owners.Dispose();data.Results.Dispose();}
            data.Indices=new NativeArray<int>(count,Allocator.Persistent);data.Groups=new NativeArray<BurstGroup>(chunk.Groups.Length,Allocator.Persistent);data.Owners=new NativeArray<BurstOwner>(chunk.Groups.Length,Allocator.Persistent);data.Results=new NativeArray<BurstGroupResult>(chunk.Groups.Length,Allocator.Persistent);
            int at=0;for(int i=0;i<chunk.Groups.Length;i++){foreach(int index in chunk.Groups[i].Indices)data.Indices[at++]=index;data.Owners[i]=new BurstOwner{Epoch=-1};}
            data.GroupSource=chunk.Groups;data.Revision=-1;
        }
        if(data.Revision==chunk.RootRevision)return;
        data.Roots.CopyFrom(chunk.Roots);data.Bounds.CopyFrom(chunk.Bounds);int start=0;
        for(int i=0;i<chunk.Groups.Length;i++){var g=chunk.Groups[i];data.Groups[i]=new BurstGroup{Bounds=g.Bounds,Roots=g.RootBounds,Kind=g.Kind,Width=g.MaximumWidth,Start=start,Count=g.Indices.Length};start+=g.Indices.Length;}
        data.Revision=chunk.RootRevision;
    }
    bool CollectBurstScatter(Vector3 eye)
    {
        if(frame==null||terrain==null||!WorldGenPerformance.BurstScatter||burstUnavailable||!BurstCompiler.IsEnabled||WorldGenPerformance.Original||WorldGenPerformance.FineCullTrace||WorldGenPerformance.LegacyGroupVisibility||prefabScatterKinds.Count>0)return false;
        PrepareTerrainOccluders();BurstArray(ref burstPlanes,6);BurstArray(ref burstCasts,13);NativeArray<Plane>.Copy(scatterFrustum,0,burstPlanes,0,6);for(int i=0;i<13;i++)burstCasts[i]=ScatterGroupCanCast((ScatterKind)i)?(byte)1:(byte)0;
        if(!burstCollected.IsCreated)burstCollected=new NativeParallelHashSet<ulong>(Math.Max(16,inventoryCollected.Count),Allocator.Persistent);
        if(burstCollectedCount!=inventoryCollected.Count){burstCollected.Clear();if(burstCollected.Capacity<inventoryCollected.Count)burstCollected.Capacity=inventoryCollected.Count;foreach(ulong id in inventoryCollected)burstCollected.Add(id);burstCollectedCount=inventoryCollected.Count;}
        if(!burstForest.IsCreated)burstForest=new NativeParallelHashSet<ForestPageKey>(Math.Max(16,forestFields.Count),Allocator.Persistent);
        burstForest.Clear();if(burstForest.Capacity<forestFields.Count)burstForest.Capacity=forestFields.Count;foreach(var pair in forestFields)if(pair.Value.Ready)burstForest.Add(pair.Key);
        bool camp=campMaskCount>0&&campMaskFrame==frame;
        BurstArray(ref burstCamp,camp?campClearingPixels.Length:1);if(camp&&burstCampRevision!=coverVisibilityRevision){NativeArray<byte>.Copy(campClearingPixels,0,burstCamp,0,campClearingPixels.Length);burstCampRevision=coverVisibilityRevision;}
        if(!burstSlabs.IsCreated)burstSlabs=new NativeParallelHashMap<long,int>(Math.Max(16,tiles.Count),Allocator.Persistent);
        burstSlabs.Clear();if(burstSlabs.Capacity<tiles.Count)burstSlabs.Capacity=tiles.Count;
        int values=0;foreach(var tile in vegetationOccluders)values+=tile.OcclusionMin.Length;BurstArray(ref burstSlabInfo,vegetationOccluders.Count);BurstArray(ref burstSlabMinimum,values);
        int slabAt=0,slabOffset=0;foreach(var pair in tiles)if(vegetationOccluders.Contains(pair.Value))
        {var tile=pair.Value;burstSlabs.Add(((long)pair.Key.X<<32)|(uint)pair.Key.Z,slabAt);burstSlabInfo[slabAt++]=new BurstSlab{Start=slabOffset,Segments=tile.Segments,Stride=tile.OcclusionStride,Side=(tile.Segments+tile.OcclusionStride-1)/tile.OcclusionStride};NativeArray<float>.Copy(tile.OcclusionMin,0,burstSlabMinimum,slabOffset,tile.OcclusionMin.Length);slabOffset+=tile.OcclusionMin.Length;}
        var o=new BurstScatterOptions{Eye=eye,Slack=scatterAngularSlack,ShadowRadius=vegetationShadowRadius,ShadowDistance=vegetationShadowDistance,ShadowSlope=vegetationShadowSlope,OriginX=originX,OriginZ=originZ,TileSize=TerrainLod.BaseSizeM*HorizontalScale,Ceiling=occlusionCeiling,
            AllHeadings=scatterCachedAllHeadings,Frustum=TerrainVideoOptions.ScatterFrustum,Occlusion=TerrainVideoOptions.VegetationOcclusion,GpuOcclusion=WorldGenPerformance.GpuNearOcclusion,Specialists=TerrainVideoOptions.SpecialistPlants,Mid=TerrainVideoOptions.MidPlants,Tree=TerrainVideoOptions.GeneratedTrees,Rebuilt=TerrainVideoOptions.TreeRebuild,Impostors=TerrainVideoOptions.TreeImpostors,FarForest=TerrainVideoOptions.FarForest,NoTrees=WorldGenPerformance.NoLocalTrees,IndividualTrees=WorldGenPerformance.ForceIndividualTreeAdmission,MidShadows=TerrainVideoOptions.MidPlantShadows,TreeShadows=TerrainVideoOptions.TreeShadows,
            MidDetail=TerrainVideoOptions.MidPlantDetail,TreeDetail=TerrainVideoOptions.TreeDetail,SpecialistDetail=TerrainVideoOptions.SpecialistDetail,ForestEpoch=forestOwnershipEpoch,TreeDensity=TerrainVideoOptions.TreeDensity,MidDensity=TerrainVideoOptions.MidPlantDensity,SpecialistDensity=TerrainVideoOptions.SpecialistDensity,TreeNear=TerrainVideoOptions.TreeNearLod,TreeFar=TerrainVideoOptions.TreeFarLod,MidNear=TerrainVideoOptions.MidNearLod,MidFar=TerrainVideoOptions.MidFarLod,SpecialistNear=TerrainVideoOptions.SpecialistNearLod,TreeIdentity=TerrainVideoOptions.TreeIdentityRange,TreeMaximum=TerrainVideoOptions.TreeMaxDistance,MidMaximum=TerrainVideoOptions.MidMaxDistance,HerbMaximum=TerrainVideoOptions.SpecialistDistance,FungusMaximum=TerrainVideoOptions.FungusDistance,Handoff=ForestHandoffReach,Camp=camp,CampX=campMaskX,CampZ=campMaskZ,Radius=frame.RadiusM,East=frame.East,North=frame.North,Origin=frame.Origin};
        burstJobs.Clear();burstScheduled.Clear();
        try
        {
            foreach(var chunk in scatterCollectWork)
            {
                chunk.BurstAdmissions=false;chunk.Admissions.Clear();chunk.AdmissionCounts=default;if(chunk.Source.Instances.Length==0)continue;
                var bounds=chunk.AllBounds;bounds.Expand(10+(bounds.center-eye).magnitude*.04f);
                if(TerrainVideoOptions.ScatterFrustum&&!ScatterInFrustum(bounds,eye)&&!MayCastVisibleShadow(bounds,eye)){chunk.AdmissionCounts=new ScatterCounts{Frustum=chunk.Source.Instances.Length};continue;}
                PrepareBurstChunk(chunk);var b=chunk.Burst;
                var job=new ScatterAdmissionJob{Source=b.Source,Roots=b.Roots,Bounds=b.Bounds,Indices=b.Indices,Groups=b.Groups,Owners=b.Owners,Results=b.Results,Admissions=b.Admissions,Planes=burstPlanes,Casts=burstCasts,CampMask=burstCamp,Collected=burstCollected,Forest=burstForest,Slabs=burstSlabs,SlabInfo=burstSlabInfo,SlabMinimum=burstSlabMinimum,Options=o,Grouped=chunk.Groups.Length>1};
                burstJobs.Add(WorldGenPerformance.CompactScatterJobs?job.Schedule():job.Schedule(4,1));burstScheduled.Add(chunk);
            }
            JobHandle.ScheduleBatchedJobs();
        }
        finally{Exception failure=null;foreach(var job in burstJobs)try{job.Complete();}catch(Exception e){failure??=e;}if(failure!=null)throw failure;}
        foreach(var chunk in burstScheduled)
        {
            var b=chunk.Burst;var counts=new ScatterCounts();
            for(int i=0;i<b.Groups.Length;i++)
            {
                var result=b.Results[i];if(result.Managed){burstUnavailable=true;return false;}
                var c=result.Counts;counts.Objects+=c.Objects;counts.Specialists+=c.Specialists;counts.Mid+=c.Mid;counts.Trees+=c.Trees;counts.Frustum+=c.Frustum;counts.Terrain+=c.Terrain;counts.Shadow+=c.Shadow;
                if(!WorldGenPerformance.CompactScatterJobs)
                {int start=b.Groups[i].Start*2;for(int j=0;j<result.Count;j++)chunk.Admissions.Add(b.Admissions[start+j]);}
            }
            chunk.AdmissionCounts=counts;chunk.BurstAdmissions=WorldGenPerformance.CompactScatterJobs;
        }
        BurstScatterBuilds++;return true;
    }
    void DisposeBurstScatter()
    {
        foreach(var chunk in scatterChunks.Values)chunk.Burst?.Dispose();installingScatter?.Burst?.Dispose();
        if(burstPlanes.IsCreated)burstPlanes.Dispose();if(burstCasts.IsCreated)burstCasts.Dispose();if(burstCamp.IsCreated)burstCamp.Dispose();if(burstSlabInfo.IsCreated)burstSlabInfo.Dispose();if(burstSlabMinimum.IsCreated)burstSlabMinimum.Dispose();
        if(burstCollected.IsCreated)burstCollected.Dispose();if(burstForest.IsCreated)burstForest.Dispose();if(burstSlabs.IsCreated)burstSlabs.Dispose();
    }
}
