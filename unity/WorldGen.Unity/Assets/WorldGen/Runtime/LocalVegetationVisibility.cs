using System;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    readonly Plane[] scatterFrustum=new Plane[6];
    readonly System.Collections.Generic.HashSet<Tile> vegetationOccluders=new System.Collections.Generic.HashSet<Tile>();int occlusionFrame=-1;
    float occlusionCeiling,vegetationShadowRadius,vegetationShadowDistance,vegetationShadowUp;double vegetationShadowSlope;
    Vector3 scatterEye;Quaternion scatterRotation;Matrix4x4 scatterProjection;int scatterOptions;
    public int ScatterFrustumRejected {get;private set;}
    public int ScatterTerrainRejected {get;private set;}
    public int ScatterShadowOnly {get;private set;}
    public int GrassPagesOccluded {get;private set;}
    int ScatterOptionsStamp=>HashCode.Combine(HashCode.Combine(TerrainVideoOptions.SpecialistPlants,TerrainVideoOptions.MidPlants,
        TerrainVideoOptions.GeneratedTrees,TerrainVideoOptions.MidPlantDetail,TerrainVideoOptions.TreeDetail,
        TerrainVideoOptions.VegetationRevision,TerrainVideoOptions.ScatterFrustum,TerrainVideoOptions.VegetationOcclusion),HashCode.Combine(TerrainVideoOptions.VegetationShadows,TerrainVideoOptions.TreeRebuild,TerrainVideoOptions.TreeImpostors));
    bool ScatterViewChanged=>view!=null&&((view.transform.localPosition-scatterEye).sqrMagnitude>.25f||
        Quaternion.Angle(view.transform.localRotation,scatterRotation)>1||view.projectionMatrix!=scatterProjection||scatterOptions!=ScatterOptionsStamp);
    void PrepareScatterBounds(ScatterChunk chunk,LocalTerrainFrame preparedFrame=null)
    {
        var boundsFrame=preparedFrame??frame;
        int count=chunk.Source.Instances.Length;
        if(chunk.Bounds==null||chunk.Bounds.Length!=count)chunk.Bounds=new Bounds[count];
        bool first=true;
        for(int i=0;i<count;i++)
        {
            PrepareScatterRootBounds(chunk,i);
            var bounds=chunk.Bounds[i];if(first){chunk.AllBounds=bounds;first=false;}else chunk.AllBounds.Encapsulate(bounds);
        }
        PrepareScatterGroups(chunk,boundsFrame);
    }
    void PrepareScatterRootBounds(ScatterChunk chunk,int i)
    {
            var p=chunk.Source.Instances[i];bool tree=p.Kind==ScatterKind.GeneratedTree,mid=p.Kind==ScatterKind.MidPlant,herb=IsSpecialist(p.Kind);
            // Full crown and tilted local up, NOT a root-only visibility test.
            var local=new Bounds(new Vector3(0,.5f,0),tree||mid?new Vector3(1.4f,1.3f,1.4f):new Vector3(1.5f,1.5f,1.5f));
            Matrix4x4 matrix=chunk.Matrices[i];Vector3 ext=local.extents;
            Vector3 a=matrix.MultiplyVector(new Vector3(ext.x,0,0)),b=matrix.MultiplyVector(new Vector3(0,ext.y,0)),c=matrix.MultiplyVector(new Vector3(0,0,ext.z));
            Vector3 Abs(Vector3 v)=>new Vector3(Math.Abs(v.x),Math.Abs(v.y),Math.Abs(v.z));
            var bounds=new Bounds(matrix.MultiplyPoint3x4(local.center),2*(Abs(a)+Abs(b)+Abs(c)));
            float padding=(float)p.HeightM*(tree?.25f:mid||herb?.9f:.05f)+.6f;bounds.Expand(2*padding);
            chunk.Bounds[i]=bounds;
    }
    void PrepareScatterGroups(ScatterChunk chunk,LocalTerrainFrame boundsFrame)
    {
        int count=chunk.Source.Instances.Length;
        // Static spatial bins avoid revisiting every plant in a half-kilometre
        // source patch whenever the camera turns. This is an additional broad
        // phase; the original per-plant LOD/frustum/shadow tests remain intact.
        if(chunk.Groups!=null&&ReferenceEquals(chunk.GroupsFrame,boundsFrame))
        {
            // Terrain replacements only reproject Y; floating-origin shifts are
            // uniform multiples of 2048m (also a multiple of the 128m bin size).
            // Membership therefore stays exact until the tangent chart changes.
            foreach(var group in chunk.Groups)
            {
                var b=chunk.Bounds[group.Indices[0]];
                var roots=new Bounds(chunk.Roots[group.Indices[0]],Vector3.zero);
                for(int j=1;j<group.Indices.Length;j++){b.Encapsulate(chunk.Bounds[group.Indices[j]]);roots.Encapsulate(chunk.Roots[group.Indices[j]]);}
                group.Bounds=b;group.RootBounds=roots;
            }
            return;
        }
        var bins=new System.Collections.Generic.Dictionary<Vector3Int,System.Collections.Generic.List<int>>();
        for(int i=0;i<count;i++)
        {
            var p=chunk.Roots[i];var key=new Vector3Int(Mathf.FloorToInt(p.x/128),Mathf.FloorToInt(p.z/128),chunk.Source.Instances[i].Kind==ScatterKind.GeneratedTree?1:0);
            if(!bins.TryGetValue(key,out var list)){list=new System.Collections.Generic.List<int>();bins.Add(key,list);}list.Add(i);
        }
        var groups=new ScatterGroup[bins.Count];int at=0;
        foreach(var bin in bins)
        {
            var indices=bin.Value;var b=chunk.Bounds[indices[0]];var roots=new Bounds(chunk.Roots[indices[0]],Vector3.zero);
            for(int j=1;j<indices.Count;j++){b.Encapsulate(chunk.Bounds[indices[j]]);roots.Encapsulate(chunk.Roots[indices[j]]);}
            groups[at++]=new ScatterGroup{Bounds=b,RootBounds=roots,Indices=indices.ToArray(),Trees=bin.Key.z!=0};
        }
        chunk.Groups=groups;chunk.GroupsFrame=boundsFrame;
    }
    // Conservative terrain-only occlusion, no baked-world assumptions/readback.
    // At t ALL eye-to-box rays lie inside an interpolated rectangle. Its entire
    // terrain lower bound must exceed the highest ray to reject the object.
    // Missing/transitioning/coarse terrain is UNKNOWN, hence visible. This is
    // not general scene Hi-Z and does not treat foliage as a solid occluder.
    bool TerrainOccluded(Bounds box,Vector3 eye,bool acoustic=false)
    {
        if((!TerrainVideoOptions.VegetationOcclusion&&!acoustic)||box.SqrDistance(eye)<45*45)return false;
        if(occlusionFrame!=Time.frameCount)
        {
            occlusionFrame=Time.frameCount;vegetationOccluders.Clear();occlusionCeiling=float.NegativeInfinity;
            foreach(var pair in tiles)
            {
                if(pair.Key.Level!=0||pair.Value.Root==null||!pair.Value.Root.activeInHierarchy)continue;
                var timing=TerrainTiming(pair.Key);
                if(timing.Out>0||timing.In>0&&Time.time-timing.In<TerrainCoverage.FadeSeconds)continue;
                if(pair.Value.Heights==null)continue;
                if(pair.Value.OcclusionMin==null)BuildTerrainOcclusion(pair.Value);
                if(WorldGenPerformance.Original)foreach(float lower in pair.Value.OcclusionMin)occlusionCeiling=Math.Max(occlusionCeiling,lower);
                else occlusionCeiling=Math.Max(occlusionCeiling,pair.Value.OcclusionCeiling);
                vegetationOccluders.Add(pair.Value);
            }
        }
        // No eligible slab lower bound can rise above ANY interpolated top ray:
        // avoid seven dictionary/grid queries per plant on open flat terrain.
        if(occlusionCeiling<=Math.Min(eye.y,box.max.y))return false;
        box.Expand(4);Vector3 ext=box.extents;
        for(int step=1;step<=7;step++)
        {
            // Eye slack covers the <=0.5m movement before scatter lists rebuild.
            float t=step*.125f;Vector3 center=Vector3.Lerp(eye,box.center,t),r=ext*t+Vector3.one*.75f;
            double x0=center.x-r.x+originX,x1=center.x+r.x+originX,z0=center.z-r.z+originZ,z1=center.z+r.z+originZ;
            double size=TerrainLod.BaseSizeM*HorizontalScale;
            int x=(int)Math.Floor(x0/size),z=(int)Math.Floor(z0/size);
            if((int)Math.Floor(x1/size)!=x||(int)Math.Floor(z1/size)!=z)continue;
            var key=new TerrainTileKey(x,z,0,HorizontalScale);
            if(!tiles.TryGetValue(key,out var tile)||tile.Heights==null||!vegetationOccluders.Contains(tile))continue;
            int stride=tile.OcclusionStride,n=tile.Segments,side=(n+stride-1)/stride;
            int ix0=Mathf.Clamp((int)Math.Floor((x0/size-x)*n)/stride,0,side-1),ix1=Mathf.Clamp((int)Math.Floor((x1/size-x)*n)/stride,0,side-1);
            int iz0=Mathf.Clamp((int)Math.Floor((z0/size-z)*n)/stride,0,side-1),iz1=Mathf.Clamp((int)Math.Floor((z1/size-z)*n)/stride,0,side-1);
            float highestRay=center.y+r.y+2;bool blocked=true;
            for(int iz=iz0;iz<=iz1&&blocked;iz++)for(int ix=ix0;ix<=ix1;ix++)
                if(tile.OcclusionMin[iz*side+ix]<=highestRay){blocked=false;break;}
            if(blocked)return true;
        }
        return false;
    }
    static void BuildTerrainOcclusion(Tile tile)
    {
        int n=tile.Segments,stride=Math.Max(1,(n+15)/16),side=(n+stride-1)/stride;
        tile.OcclusionStride=stride;tile.OcclusionMin=new float[side*side];
        tile.OcclusionCeiling=float.NegativeInfinity;
        tile.BufferBytes+=(long)tile.OcclusionMin.Length*4;
        for(int z=0;z<side;z++)for(int x=0;x<side;x++)
        {
            float minimum=float.PositiveInfinity;
            // Every vertex of ALL overlapping linear triangles, not sparse rays.
            for(int iz=z*stride;iz<=Math.Min(n,(z+1)*stride);iz++)for(int ix=x*stride;ix<=Math.Min(n,(x+1)*stride);ix++)
            {
                float h=tile.Heights[iz*(n+1)+ix];minimum=Math.Min(minimum,float.IsNaN(h)||float.IsInfinity(h)?float.NegativeInfinity:h);
            }
            tile.OcclusionMin[z*side+x]=minimum;
            tile.OcclusionCeiling=Math.Max(tile.OcclusionCeiling,minimum);
        }
    }
    void PrepareVegetationShadowVisibility()
    {
        vegetationShadowRadius=0;
        if(!TerrainVideoOptions.VegetationShadows||localSun==null||localSun.intensity<=0||localSun.shadows==LightShadows.None)return;
        var pipeline=GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        float distance=pipeline==null?50:pipeline.shadowDistance;
        if(distance<=0)return;
        // URP fades receiving shadows by radial world distance, not by the
        // diagonal of a far-clip rectangle. Never expand casters without bound
        // as sunlight approaches the horizon.
        vegetationShadowDistance=distance;vegetationShadowRadius=distance+8;
        vegetationShadowUp=(-localSun.transform.forward).y;
        vegetationShadowSlope=VegetationShadowBudget.Slope(vegetationShadowUp);
    }
    bool MayCastVisibleShadow(Bounds box,Vector3 eye)
    {
        if(vegetationShadowRadius<=0)return false;
        float reach=(float)VegetationShadowBudget.ReachWithSlope(vegetationShadowDistance,box.max.y-eye.y,vegetationShadowSlope);
        return box.SqrDistance(eye)<reach*reach;
    }
}
