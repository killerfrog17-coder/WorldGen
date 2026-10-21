using System;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    readonly Plane[] scatterFrustum=new Plane[6];
    readonly System.Collections.Generic.HashSet<Tile> vegetationOccluders=new System.Collections.Generic.HashSet<Tile>();int occlusionFrame=-1;
    float occlusionCeiling,vegetationShadowRadius,vegetationShadowSlope;bool allShadowCasters;
    Vector3 scatterEye;Quaternion scatterRotation;Matrix4x4 scatterProjection;int scatterOptions;
    public int ScatterFrustumRejected {get;private set;}
    public int ScatterTerrainRejected {get;private set;}
    public int ScatterShadowOnly {get;private set;}
    public int GrassPagesOccluded {get;private set;}
    int ScatterOptionsStamp=>HashCode.Combine(HashCode.Combine(TerrainVideoOptions.SpecialistPlants,TerrainVideoOptions.MidPlants,
        TerrainVideoOptions.GeneratedTrees,TerrainVideoOptions.MidPlantDetail,TerrainVideoOptions.TreeDetail,
        TerrainVideoOptions.MidPlantRange,TerrainVideoOptions.ScatterFrustum,TerrainVideoOptions.VegetationOcclusion),TerrainVideoOptions.VegetationShadows);
    bool ScatterViewChanged=>view!=null&&((view.transform.localPosition-scatterEye).sqrMagnitude>.25f||
        Quaternion.Angle(view.transform.localRotation,scatterRotation)>1||view.projectionMatrix!=scatterProjection||scatterOptions!=ScatterOptionsStamp);
    void PrepareScatterBounds(ScatterChunk chunk)
    {
        int count=chunk.Source.Instances.Length;
        if(chunk.Bounds==null||chunk.Bounds.Length!=count)chunk.Bounds=new Bounds[count];
        bool first=true;
        for(int i=0;i<count;i++)
        {
            var p=chunk.Source.Instances[i];bool tree=p.Kind==ScatterKind.GeneratedTree,mid=p.Kind==ScatterKind.MidPlant,herb=IsSpecialist(p.Kind);
            // Full crown and tilted local up, NOT a root-only visibility test.
            var local=new Bounds(new Vector3(0,.5f,0),tree||mid?new Vector3(1.4f,1.3f,1.4f):new Vector3(1.5f,1.5f,1.5f));
            Matrix4x4 matrix=chunk.Matrices[i];Vector3 ext=local.extents;
            Vector3 a=matrix.MultiplyVector(new Vector3(ext.x,0,0)),b=matrix.MultiplyVector(new Vector3(0,ext.y,0)),c=matrix.MultiplyVector(new Vector3(0,0,ext.z));
            Vector3 Abs(Vector3 v)=>new Vector3(Math.Abs(v.x),Math.Abs(v.y),Math.Abs(v.z));
            var bounds=new Bounds(matrix.MultiplyPoint3x4(local.center),2*(Abs(a)+Abs(b)+Abs(c)));
            float padding=(float)p.HeightM*(tree?.25f:mid||herb?.9f:.05f)+.6f;bounds.Expand(2*padding);
            chunk.Bounds[i]=bounds;if(first){chunk.AllBounds=bounds;first=false;}else chunk.AllBounds.Encapsulate(bounds);
        }
    }
    // Conservative terrain-only occlusion, no baked-world assumptions/readback.
    // At t ALL eye-to-box rays lie inside an interpolated rectangle. Its entire
    // terrain lower bound must exceed the highest ray to reject the object.
    // Missing/transitioning/coarse terrain is UNKNOWN, hence visible. This is
    // not general scene Hi-Z and does not treat foliage as a solid occluder.
    bool TerrainOccluded(Bounds box,Vector3 eye)
    {
        if(!TerrainVideoOptions.VegetationOcclusion||box.SqrDistance(eye)<45*45)return false;
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
                foreach(float lower in pair.Value.OcclusionMin)occlusionCeiling=Math.Max(occlusionCeiling,lower);
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
        }
    }
    void PrepareVegetationShadowVisibility()
    {
        vegetationShadowRadius=0;allShadowCasters=false;
        if(!TerrainVideoOptions.VegetationShadows||localSun==null||localSun.intensity<=0||localSun.shadows==LightShadows.None)return;
        var pipeline=GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        float distance=pipeline==null?50:pipeline.shadowDistance;
        if(distance<=0)return;
        // Enclose the entire shadow-receiving frustum, including its vertical
        // drop below the eye. Tree height alone misses hill-to-valley shadows.
        float tangent=Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f);
        vegetationShadowRadius=distance*Mathf.Sqrt(1+tangent*tangent*(1+view.aspect*view.aspect))+8;
        Vector3 sun=-localSun.transform.forward;
        allShadowCasters=sun.y<=.01f;
        vegetationShadowSlope=Mathf.Sqrt(sun.x*sun.x+sun.z*sun.z)/Math.Max(.0001f,sun.y);
    }
    bool MayCastVisibleShadow(Bounds box,Vector3 eye)
    {
        if(vegetationShadowRadius<=0)return false;if(allShadowCasters)return true;
        float drop=Math.Max(0,box.max.y-eye.y+vegetationShadowRadius);
        float reach=vegetationShadowRadius+drop*vegetationShadowSlope;
        return box.SqrDistance(eye)<reach*reach;
    }
}
