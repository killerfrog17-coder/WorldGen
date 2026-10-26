using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class TreeShadowBatch
    {
        public Mesh Mesh;public Material Material;public readonly Matrix4x4[] Matrices=new Matrix4x4[256];
        public readonly Vector4[] Coverage=new Vector4[256];public readonly MaterialPropertyBlock Properties=new MaterialPropertyBlock();
        public Bounds Bounds;public int Count;
    }
    readonly Dictionary<int,Mesh> treeShadowMeshes=new Dictionary<int,Mesh>(),treeShadowFarMeshes=new Dictionary<int,Mesh>();
    readonly List<TreeShadowBatch> treeShadowBatches=new List<TreeShadowBatch>(),treeShadowPool=new List<TreeShadowBatch>();
    readonly Dictionary<(int,int,int,int),TreeShadowBatch> treeShadowActive=new Dictionary<(int,int,int,int),TreeShadowBatch>();
    readonly Dictionary<ArtModel,Material> treeShadowCardMaterials=new Dictionary<ArtModel,Material>();
    readonly HashSet<ArtModel> treeShadowCardModels=new HashSet<ArtModel>();
    Material treeShadowMaterial;UniversalRenderPipelineAsset foliageShadowPipeline;
    float originalShadowDistance,publishedShadowDistance,originalShadowBorder,publishedShadowBorder;
    int originalShadowResolution,publishedShadowResolution,originalShadowCascades,publishedShadowCascades;
    Vector3 originalShadowSplits,publishedShadowSplits;int foliageShadowRevision=-1;
    double nextSolarCasterRefresh;
    readonly Vector3[] shadowFrustumCorners=new Vector3[4];
    Vector3 shadowAxisX,shadowAxisY,shadowAxisZ,shadowReceiverMin,shadowReceiverMax;
    public int TreeShadowObjects {get;private set;}
    public int TreeShadowDraws {get;private set;}
    public int TreeShadowCards {get;private set;}
    public int TreeShadowCoarse {get;private set;}
    public long TreeShadowTriangles {get;private set;}
    bool UseTreeShadowProxies=>TerrainVideoOptions.SimplifiedForestShadows&&treeShadowMaterial!=null&&TerrainVideoOptions.GeneratedTrees&&TerrainVideoOptions.TreeRebuild&&TerrainVideoOptions.TreeShadows&&TerrainVideoOptions.VegetationShadows&&!TerrainDiagnostics.HideTrees&&!TerrainDiagnostics.HidePlants;
    bool ReplaceTreeShadow(ScatterInstance plant)=>plant.Kind==ScatterKind.GeneratedTree&&UseTreeShadowProxies&&treeShadowMeshes.ContainsKey(TreeFlora.RenderCode(plant.Variant));

    void ApplyFoliageShadowOptions()
    {
        if(localSun==null||view==null)return;
        TerrainVideoOptions.ForestLightUp=Mathf.Clamp01((-localSun.transform.forward).y);
        if(TerrainVideoOptions.SimplifiedForestShadows&&treeShadowMaterial==null)
        {
            var template=Resources.Load<Material>("TreeShadowProxyMaterial");
            if(template!=null&&template.shader.isSupported)treeShadowMaterial=new Material(template){enableInstancing=true,name="Shared opaque tree shadow proxies"};
        }
        var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if(pipeline!=foliageShadowPipeline)
        {
            RestoreFoliageShadowDistance();foliageShadowPipeline=pipeline;
            if(pipeline!=null)
            {
                originalShadowDistance=publishedShadowDistance=pipeline.shadowDistance;
                originalShadowResolution=publishedShadowResolution=pipeline.mainLightShadowmapResolution;
                originalShadowCascades=publishedShadowCascades=pipeline.shadowCascadeCount;
                originalShadowSplits=publishedShadowSplits=pipeline.cascade4Split;
                originalShadowBorder=publishedShadowBorder=pipeline.cascadeBorder;
            }
            foliageShadowRevision=-1;
        }
        float range=UseTreeShadowProxies?TerrainVideoOptions.ForestShadowDistance:originalShadowDistance;
        if(pipeline!=null&&pipeline.shadowDistance!=range)
        {
            pipeline.shadowDistance=range;publishedShadowDistance=range;
            // Smooth cascade range must not trigger a full vegetation rebuild
            // every frame while the sun is rising/setting. Moving-camera and
            // setting changes still refresh immediately through existing paths.
            if(Time.realtimeSinceStartupAsDouble>=nextSolarCasterRefresh)
            {scatterDirty=true;nextSolarCasterRefresh=Time.realtimeSinceStartupAsDouble+1;}
        }
        if(pipeline!=null)
        {
            int resolution=UseTreeShadowProxies?TerrainVideoOptions.ForestShadowResolution:originalShadowResolution;
            int cascades=UseTreeShadowProxies?4:originalShadowCascades;
            float daylight=Mathf.Clamp01((range-60)/120);
            var splits=UseTreeShadowProxies?new Vector3(16/range,Mathf.Lerp(32,48,daylight)/range,Mathf.Lerp(46,100,daylight)/range):originalShadowSplits;
            float border=UseTreeShadowProxies?.30f:originalShadowBorder;
            if(pipeline.mainLightShadowmapResolution!=resolution)pipeline.mainLightShadowmapResolution=resolution;
            if(pipeline.shadowCascadeCount!=cascades)pipeline.shadowCascadeCount=cascades;
            if(pipeline.cascade4Split!=splits)pipeline.cascade4Split=splits;
            if(pipeline.cascadeBorder!=border)pipeline.cascadeBorder=border;
            publishedShadowResolution=resolution;publishedShadowCascades=cascades;publishedShadowSplits=splits;publishedShadowBorder=border;
        }
        if(foliageShadowRevision!=TerrainVideoOptions.VegetationRevision)
        {foliageShadowRevision=TerrainVideoOptions.VegetationRevision;scatterDirty=true;}
    }
    void RestoreFoliageShadowDistance()
    {
        if(foliageShadowPipeline!=null&&foliageShadowPipeline.shadowDistance==publishedShadowDistance)
            foliageShadowPipeline.shadowDistance=originalShadowDistance;
        if(foliageShadowPipeline==null)return;
        if(foliageShadowPipeline.mainLightShadowmapResolution==publishedShadowResolution)foliageShadowPipeline.mainLightShadowmapResolution=originalShadowResolution;
        if(foliageShadowPipeline.shadowCascadeCount==publishedShadowCascades)foliageShadowPipeline.shadowCascadeCount=originalShadowCascades;
        if(foliageShadowPipeline.cascade4Split==publishedShadowSplits)foliageShadowPipeline.cascade4Split=originalShadowSplits;
        if(foliageShadowPipeline.cascadeBorder==publishedShadowBorder)foliageShadowPipeline.cascadeBorder=originalShadowBorder;
    }
    void BuildTreeShadowBatches()
    {
        foreach(var b in treeShadowBatches){b.Count=0;treeShadowPool.Add(b);}treeShadowBatches.Clear();treeShadowActive.Clear();treeShadowCardModels.Clear();TreeShadowObjects=TreeShadowCards=TreeShadowCoarse=0;
        var expired=new List<ArtModel>();foreach(var p in treeShadowCardMaterials)if(!p.Key.TreeBaked)expired.Add(p.Key);
        foreach(var owner in expired){Destroy(treeShadowCardMaterials[owner]);treeShadowCardMaterials.Remove(owner);}
        if(!UseTreeShadowProxies||vegetationShadowRadius<=0)return;
        PrepareTreeShadowFrustum();
        void Add(int code,int lod,Mesh mesh,Material material,Matrix4x4 matrix,Bounds bounds,float start,float end)
        {
            if(end<=start)return;
            // Keep spatially separate casters out of one enormous group bound;
            // Unity shadow-culls instanced groups, not their individual roots.
            var key=(code,lod,Mathf.FloorToInt(matrix.m03/128),Mathf.FloorToInt(matrix.m23/128));
            if(!treeShadowActive.TryGetValue(key,out var batch)||batch.Count==batch.Matrices.Length)
            {
                if(treeShadowPool.Count>0){int last=treeShadowPool.Count-1;batch=treeShadowPool[last];treeShadowPool.RemoveAt(last);}else batch=new TreeShadowBatch();
                batch.Mesh=mesh;batch.Material=material;batch.Count=0;treeShadowBatches.Add(batch);treeShadowActive[key]=batch;
            }
            batch.Matrices[batch.Count]=matrix;batch.Coverage[batch.Count]=new Vector4(start,end,0,0);
            if(batch.Count++==0)batch.Bounds=bounds;else batch.Bounds.Encapsulate(bounds);
        }
        bool built=false;float lightCardWeight=(float)VegetationShadowBudget.CardWeight(vegetationShadowUp);
        foreach(var chunk in scatterChunks.Values)
        {
            if(chunk.Bounds==null)PrepareScatterBounds(chunk);
            if(!MayCastVisibleShadow(chunk.AllBounds,scatterEye)||!TreeShadowReachesView(chunk.AllBounds))continue;
            foreach(var group in chunk.Groups)
            {
                if(!group.Trees||!MayCastVisibleShadow(group.Bounds,scatterEye)||!TreeShadowReachesView(group.Bounds))continue;
                foreach(int index in group.Indices)
                {
                    var plant=chunk.Source.Instances[index];var bounds=chunk.Bounds[index];
                    if(!TerrainVideoOptions.KeepVegetation(plant.Kind,plant.Id)||!MayCastVisibleShadow(bounds,scatterEye)||!TreeShadowReachesView(bounds))continue;
                    float distance=Mathf.Sqrt(bounds.SqrDistance(scatterEye)),coverage=(float)VegetationShadowBudget.CasterFade(distance,vegetationShadowDistance);
                    if(coverage<=0)continue;
                    int code=TreeFlora.RenderCode(plant.Variant);
                    if(!treeShadowMeshes.TryGetValue(code,out var mesh))
                    {
                        if(built){scatterDirty=true;continue;}if(treeShadowMeshes.Count>=96)continue;
                        mesh=LocalScatterMeshes.BuildTreeShadowProxy(code);treeShadowMeshes.Add(code,mesh);built=true;
                    }
                    float far=TerrainVideoOptions.DistantTreeShadowCards?(float)TreeRenderingLod.ShadowWeight(distance):0;
                    Mesh coarse=null;
                    if(far>0&&!treeShadowFarMeshes.TryGetValue(code,out coarse))
                    {
                        if(built){scatterDirty=true;far=0;}
                        else{coarse=LocalScatterMeshes.BuildTreeShadowProxy(code,2);treeShadowFarMeshes.Add(code,coarse);built=true;}
                    }
                    float card=far>0&&TerrainVideoOptions.TreeImpostors?lightCardWeight:0;ArtModel owner=null;
                    if(card>0)
                    {
                        long artKey=ScatterArtKey(ScatterKind.GeneratedTree,code,2);scatterArt.TryGetValue(artKey,out owner);
                        if((owner==null||!owner.TreeBaked)&&scatterArt.TryGetValue(artKey|(1L<<49),out var stand))owner=stand;
                        if(owner==null)owner=Art(ScatterKind.GeneratedTree,code,2);
                        owner.LastUsed=Time.realtimeSinceStartupAsDouble;treeShadowCardModels.Add(owner);
                        if(!owner.TreeBaked)card=0;
                    }
                    float nearEnd=coverage*(1-far),coarseEnd=coverage*(1-far*card);
                    Add(code,0,mesh,treeShadowMaterial,chunk.Matrices[index],bounds,0,nearEnd);
                    if(far>0&&coarseEnd>nearEnd)
                    {Add(code,1,coarse,treeShadowMaterial,chunk.Matrices[index],bounds,nearEnd,coarseEnd);TreeShadowCoarse++;}
                    if(far*card>0)
                    {
                        if(!treeShadowCardMaterials.TryGetValue(owner,out var material))
                        {
                            material=new Material(treeShadowMaterial){name="Baked tree shadow card / "+code,enableInstancing=true};
                            material.SetFloat("_ShadowCard",1);material.SetTexture("_TreeColourViews",owner.TreeColour);
                            material.SetVector("_ImpostorBounds",owner.Parts[0].Material.GetVector("_ImpostorBounds"));treeShadowCardMaterials.Add(owner,material);
                        }
                        Add(code,2,owner.Parts[0].Mesh,material,chunk.Matrices[index],bounds,coarseEnd,coverage);TreeShadowCards++;
                    }
                    TreeShadowObjects++;
                }
            }
        }
        // Coverage changes with the batch, not every stationary draw frame.
        foreach(var batch in treeShadowBatches)batch.Properties.SetVectorArray("_ShadowCoverage",batch.Coverage);
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
        TreeShadowDraws=0;TreeShadowTriangles=0;
        if(!UseTreeShadowProxies||localSun==null||localSun.shadows==LightShadows.None||localSun.intensity<=0)return;
        foreach(var batch in treeShadowBatches)
        {
            if(batch.Count==0)continue;
            var parameters=new RenderParams(batch.Material){camera=view,layer=30,worldBounds=batch.Bounds,matProps=batch.Properties,shadowCastingMode=ShadowCastingMode.ShadowsOnly,receiveShadows=false};
            Graphics.RenderMeshInstanced(in parameters,batch.Mesh,0,batch.Matrices,batch.Count);TreeShadowDraws++;TreeShadowTriangles+=(long)batch.Mesh.GetIndexCount(0)/3*batch.Count;
        }
    }
    void DisposeFoliageShadows()
    {
        DisposeCanopyShade();
        RestoreFoliageShadowDistance();foliageShadowPipeline=null;
        TerrainVideoOptions.ForestLightUp=1;
        foreach(var mesh in treeShadowMeshes.Values)Destroy(mesh);treeShadowMeshes.Clear();
        foreach(var mesh in treeShadowFarMeshes.Values)Destroy(mesh);treeShadowFarMeshes.Clear();
        if(treeShadowMaterial!=null)Destroy(treeShadowMaterial);treeShadowMaterial=null;
        foreach(var material in treeShadowCardMaterials.Values)Destroy(material);treeShadowCardMaterials.Clear();treeShadowCardModels.Clear();
        treeShadowBatches.Clear();treeShadowPool.Clear();treeShadowActive.Clear();
        Shader.SetGlobalVector("_WorldGenFoliageShadowSettings",Vector4.zero);
    }
}

static partial class LocalScatterMeshes
{
    // Shadows inherit the actual shared art's leaf bounds, including palms,
    // needles and regional crown strategies. No new placement or giant sphere.
    public static Mesh BuildTreeShadowProxy(int code,int divisions=4)
    {
        divisions=Math.Max(1,Math.Min(4,divisions));
        var source=BuildTree(code,1);var points=source.vertices;var masks=new List<Vector2>();source.GetUVs(1,masks);
        var envelope=source.bounds;var cells=new Dictionary<Vector3Int,Bounds>();
        for(int i=0;i<points.Length;i++)
        {
            if(masks.Count!=points.Length||masks[i].y<.5f)continue;
            var p=points[i];var uv=p-envelope.min;
            var key=new Vector3Int(Mathf.Clamp((int)(uv.x/Mathf.Max(.001f,envelope.size.x)*divisions),0,divisions-1),Mathf.Clamp((int)(uv.y/Mathf.Max(.001f,envelope.size.y)*divisions),0,divisions-1),Mathf.Clamp((int)(uv.z/Mathf.Max(.001f,envelope.size.z)*divisions),0,divisions-1));
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
            {
                if(top==2){builder.Triangle(first+top,first+4,first);builder.Triangle(first+top,first+1,first+4);builder.Triangle(first+top,first+5,first+1);builder.Triangle(first+top,first,first+5);}
                else{builder.Triangle(first+top,first,first+4);builder.Triangle(first+top,first+4,first+1);builder.Triangle(first+top,first+1,first+5);builder.Triangle(first+top,first+5,first);}
            }
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
