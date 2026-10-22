using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class TreeShadowBatch
    {
        public Mesh Mesh;public readonly Matrix4x4[] Matrices=new Matrix4x4[256];
        public Bounds Bounds;public int Count;
    }
    readonly Dictionary<int,Mesh> treeShadowMeshes=new Dictionary<int,Mesh>();
    readonly List<TreeShadowBatch> treeShadowBatches=new List<TreeShadowBatch>(),treeShadowPool=new List<TreeShadowBatch>();
    readonly Dictionary<int,TreeShadowBatch> treeShadowActive=new Dictionary<int,TreeShadowBatch>();
    Material treeShadowMaterial;UniversalRenderPipelineAsset foliageShadowPipeline;
    float originalShadowDistance,publishedShadowDistance;int foliageShadowRevision=-1;
    readonly Vector3[] shadowFrustumCorners=new Vector3[4];
    Vector3 shadowAxisX,shadowAxisY,shadowAxisZ,shadowReceiverMin,shadowReceiverMax;
    public int TreeShadowObjects {get;private set;}
    public int TreeShadowDraws {get;private set;}
    bool UseTreeShadowProxies=>TerrainVideoOptions.SimplifiedForestShadows&&treeShadowMaterial!=null&&TerrainVideoOptions.GeneratedTrees&&TerrainVideoOptions.TreeRebuild&&TerrainVideoOptions.TreeShadows&&TerrainVideoOptions.VegetationShadows&&!TerrainDiagnostics.HideTrees&&!TerrainDiagnostics.HidePlants;
    bool ReplaceTreeShadow(ScatterInstance plant)=>plant.Kind==ScatterKind.GeneratedTree&&UseTreeShadowProxies&&treeShadowMeshes.ContainsKey(TreeFlora.RenderCode(plant.Variant));

    void ApplyFoliageShadowOptions()
    {
        if(localSun==null||view==null)return;
        if(TerrainVideoOptions.SimplifiedForestShadows&&treeShadowMaterial==null)
        {
            var template=Resources.Load<Material>("TreeShadowProxyMaterial");
            if(template!=null&&template.shader.isSupported)treeShadowMaterial=new Material(template){enableInstancing=true,name="Shared opaque tree shadow proxies"};
        }
        var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if(pipeline!=foliageShadowPipeline)
        {
            RestoreFoliageShadowDistance();foliageShadowPipeline=pipeline;
            if(pipeline!=null)originalShadowDistance=publishedShadowDistance=pipeline.shadowDistance;
            foliageShadowRevision=-1;
        }
        float range=UseTreeShadowProxies?TerrainVideoOptions.ForestShadowDistance:originalShadowDistance;
        if(pipeline!=null&&pipeline.shadowDistance!=range){pipeline.shadowDistance=range;publishedShadowDistance=range;scatterDirty=true;}
        if(foliageShadowRevision!=TerrainVideoOptions.VegetationRevision)
        {foliageShadowRevision=TerrainVideoOptions.VegetationRevision;scatterDirty=true;}
    }
    void RestoreFoliageShadowDistance()
    {
        if(foliageShadowPipeline!=null&&foliageShadowPipeline.shadowDistance==publishedShadowDistance)
            foliageShadowPipeline.shadowDistance=originalShadowDistance;
    }
    void BuildTreeShadowBatches()
    {
        foreach(var b in treeShadowBatches){b.Count=0;treeShadowPool.Add(b);}treeShadowBatches.Clear();treeShadowActive.Clear();TreeShadowObjects=0;
        if(!UseTreeShadowProxies||vegetationShadowRadius<=0)return;
        PrepareTreeShadowFrustum();
        bool built=false;
        foreach(var chunk in scatterChunks.Values)
        {
            if(chunk.Bounds==null)PrepareScatterBounds(chunk);
            if(!MayCastVisibleShadow(chunk.AllBounds,scatterEye)||!TreeShadowReachesView(chunk.AllBounds))continue;
            foreach(var group in chunk.Groups)
            {
                if(!group.Trees||!MayCastVisibleShadow(group.Bounds,scatterEye)||!TreeShadowReachesView(group.Bounds))continue;
                foreach(int index in group.Indices)
                {
                    var plant=chunk.Source.Instances[index];
                    if(!TerrainVideoOptions.KeepVegetation(plant.Kind,plant.Id)||!MayCastVisibleShadow(chunk.Bounds[index],scatterEye)||!TreeShadowReachesView(chunk.Bounds[index]))continue;
                    int code=TreeFlora.RenderCode(plant.Variant);
                    if(!treeShadowMeshes.TryGetValue(code,out var mesh))
                    {
                        // At most one new shared anatomy per batch update, never
                        // per instance. Preserve old casting until it is ready.
                        if(built){scatterDirty=true;continue;}
                        if(treeShadowMeshes.Count>=96)continue;
                        mesh=LocalScatterMeshes.BuildTreeShadowProxy(code);treeShadowMeshes.Add(code,mesh);built=true;
                    }
                    if(!treeShadowActive.TryGetValue(code,out var batch)||batch.Count==batch.Matrices.Length)
                    {
                        if(treeShadowPool.Count>0){int last=treeShadowPool.Count-1;batch=treeShadowPool[last];treeShadowPool.RemoveAt(last);}else batch=new TreeShadowBatch();
                        batch.Mesh=mesh;batch.Count=0;treeShadowBatches.Add(batch);treeShadowActive[code]=batch;
                    }
                    batch.Matrices[batch.Count]=chunk.Matrices[index];
                    if(batch.Count++==0)batch.Bounds=chunk.Bounds[index];else batch.Bounds.Encapsulate(chunk.Bounds[index]);
                    TreeShadowObjects++;
                }
            }
        }
    }
    void PrepareTreeShadowFrustum()
    {
        // Project the receiving camera frustum into sunlight coordinates. A
        // caster can be outside the camera yet shade it, but cannot cast across
        // the light's XY axes. No terrain-height guess or per-tree ray query.
        shadowAxisX=localSun.transform.right;shadowAxisY=localSun.transform.up;shadowAxisZ=localSun.transform.forward;
        shadowReceiverMin=shadowReceiverMax=Vector3.zero;
        for(int plane=0;plane<2;plane++)
        {
            float distance=plane==0?view.nearClipPlane:Mathf.Min(view.farClipPlane,foliageShadowPipeline==null?50:foliageShadowPipeline.shadowDistance);
            view.CalculateFrustumCorners(new Rect(0,0,1,1),distance,Camera.MonoOrStereoscopicEye.Mono,shadowFrustumCorners);
            foreach(var corner in shadowFrustumCorners)
            {
                var p=view.transform.TransformVector(corner);
                var light=new Vector3(Vector3.Dot(p,shadowAxisX),Vector3.Dot(p,shadowAxisY),Vector3.Dot(p,shadowAxisZ));
                shadowReceiverMin=Vector3.Min(shadowReceiverMin,light);shadowReceiverMax=Vector3.Max(shadowReceiverMax,light);
            }
        }
        shadowReceiverMin-=Vector3.one*8;shadowReceiverMax+=Vector3.one*8;
    }
    bool TreeShadowReachesView(Bounds bounds)
    {
        var p=bounds.center-scatterEye;var e=bounds.extents;
        float Extent(Vector3 axis)=>Mathf.Abs(axis.x)*e.x+Mathf.Abs(axis.y)*e.y+Mathf.Abs(axis.z)*e.z;
        float x=Vector3.Dot(p,shadowAxisX),y=Vector3.Dot(p,shadowAxisY),z=Vector3.Dot(p,shadowAxisZ);
        float ex=Extent(shadowAxisX),ey=Extent(shadowAxisY);
        return x+ex>=shadowReceiverMin.x&&x-ex<=shadowReceiverMax.x&&y+ey>=shadowReceiverMin.y&&y-ey<=shadowReceiverMax.y&&z-Extent(shadowAxisZ)<=shadowReceiverMax.z;
    }
    void DrawTreeShadowProxies()
    {
        TreeShadowDraws=0;
        if(!UseTreeShadowProxies||localSun==null||localSun.shadows==LightShadows.None||localSun.intensity<=0)return;
        foreach(var batch in treeShadowBatches)
        {
            if(batch.Count==0)continue;
            var parameters=new RenderParams(treeShadowMaterial){camera=view,layer=30,worldBounds=batch.Bounds,shadowCastingMode=ShadowCastingMode.ShadowsOnly,receiveShadows=false};
            Graphics.RenderMeshInstanced(in parameters,batch.Mesh,0,batch.Matrices,batch.Count);TreeShadowDraws++;
        }
    }
    void DisposeFoliageShadows()
    {
        RestoreFoliageShadowDistance();foliageShadowPipeline=null;
        foreach(var mesh in treeShadowMeshes.Values)Destroy(mesh);treeShadowMeshes.Clear();
        if(treeShadowMaterial!=null)Destroy(treeShadowMaterial);treeShadowMaterial=null;
        treeShadowBatches.Clear();treeShadowPool.Clear();treeShadowActive.Clear();
        Shader.SetGlobalVector("_WorldGenFoliageShadowSettings",Vector4.zero);
    }
}

static partial class LocalScatterMeshes
{
    // Shadows inherit the actual shared art's leaf bounds, including palms,
    // needles and regional crown strategies. No new placement or giant sphere.
    public static Mesh BuildTreeShadowProxy(int code)
    {
        var source=BuildTree(code,1);var points=source.vertices;var masks=new List<Vector2>();source.GetUVs(1,masks);
        var envelope=source.bounds;var cells=new Dictionary<Vector3Int,Bounds>();
        for(int i=0;i<points.Length;i++)
        {
            if(masks.Count!=points.Length||masks[i].y<.5f)continue;
            var p=points[i];var uv=p-envelope.min;
            var key=new Vector3Int(Mathf.Clamp((int)(uv.x/Mathf.Max(.001f,envelope.size.x)*4),0,3),Mathf.Clamp((int)(uv.y/Mathf.Max(.001f,envelope.size.y)*4),0,3),Mathf.Clamp((int)(uv.z/Mathf.Max(.001f,envelope.size.z)*4),0,3));
            if(cells.TryGetValue(key,out var bounds)){bounds.Encapsulate(p);cells[key]=bounds;}else cells.Add(key,new Bounds(p,Vector3.zero));
        }
        var builder=new Builder();
        foreach(var cell in cells.Values)
        {
            var e=cell.extents;float minimum=Mathf.Max(.012f,envelope.size.y*.012f);e=new Vector3(Mathf.Max(minimum,e.x),Mathf.Max(minimum,e.y),Mathf.Max(minimum,e.z));
            int first=builder.V.Count;var c=cell.center;
            builder.Add(c+Vector3.right*e.x,Color.white,Vector2.zero);builder.Add(c-Vector3.right*e.x,Color.white,Vector2.zero);
            builder.Add(c+Vector3.up*e.y,Color.white,Vector2.zero);builder.Add(c-Vector3.up*e.y,Color.white,Vector2.zero);
            builder.Add(c+Vector3.forward*e.z,Color.white,Vector2.zero);builder.Add(c-Vector3.forward*e.z,Color.white,Vector2.zero);
            for(int top=2;top<=3;top++)
            {builder.Triangle(first+top,first,first+4);builder.Triangle(first+top,first+4,first+1);builder.Triangle(first+top,first+1,first+5);builder.Triangle(first+top,first+5,first);}
        }
        var morphology=new TreeMorphology(code);float trunk=(float)morphology.TrunkRadius;
        builder.Tube(Vector3.zero,Vector3.up*Mathf.Max(.2f,(float)morphology.CrownBase),trunk,trunk*.45f,4,Color.white);
        if(cells.Count==0)
        {
            // Succulent/legacy families can lack leaf masks; simplify their
            // actual envelope instead of dropping their entire shadow.
            builder.Crown(envelope.center,envelope.extents*.85f,5,3,Color.white,code);
        }
        UnityEngine.Object.Destroy(source);var result=builder.Finish(false);result.name="Tree shadow proxy / "+code;return result;
    }
}
