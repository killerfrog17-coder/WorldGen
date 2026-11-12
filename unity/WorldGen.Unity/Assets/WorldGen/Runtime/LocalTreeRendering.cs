using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    Texture2DArray rebuiltLeaves,rebuiltLeafNormals;
    Task<LocalTreeImpostor.Result> treeBakeTask;ArtModel treeBakeOwner;
    readonly HashSet<ArtModel> treeBakeFailures=new HashSet<ArtModel>();
    sealed class SharedTreeBake
    {
        public Texture2DArray Colour,Normal;public Vector3 Center,Extents;public float Radius;public int References;public bool Borrowed;
    }
    readonly Dictionary<int,SharedTreeBake> sharedTreeBakes=new Dictionary<int,SharedTreeBake>();
    readonly HashSet<int> missingTreePrototypes=new HashSet<int>();
    const int TreeBakeLimit=96;
    public int BakedTreeModels=>scatterArt.Values.Count(m=>m.TreeBaked&&!m.TreeStand);
    public int BakedForestModels=>scatterArt.Values.Count(m=>m.TreeBaked&&m.TreeStand&&m.TreeColour!=null);
    public int PendingTreeModels=>scatterArt.Values.Count(m=>m.TreeBake&&!m.TreeBaked);
    public long TreeBakeBytes=>sharedTreeBakes.Values.Sum(b=>(long)LocalTreeImpostor.Size*LocalTreeImpostor.Size*LocalTreeImpostor.Views*(b.Borrowed?2:8)*4/3);
    ArtModel CreateRebuiltTree(long key,int code,int lod,bool stand=false)
    {
        long modelStarted=WorldGenPerformance.TraceEnabled?System.Diagnostics.Stopwatch.GetTimestamp():0;
        if(rebuiltLeaves==null)LocalTreeTextures.Create(out rebuiltLeaves,out rebuiltLeafNormals);
        var prototype=LocalTreePrototype.Load(code);
        if(prototype==null&&missingTreePrototypes.Add(code))Debug.LogWarning("Prepared tree prototype missing: "+LocalTreePrototype.ResourcePath(code)+"; using temporary procedural fallback.");
        if(lod==2&&prototype!=null&&!sharedTreeBakes.ContainsKey(code))
            sharedTreeBakes.Add(code,new SharedTreeBake{Colour=prototype.Colour,Normal=prototype.Normal,Center=prototype.Center,Extents=prototype.Extents,Radius=prototype.Radius,Borrowed=true});
        // Keep a complete middle mesh until its shared-tree bake is ready. Never
        // show a blank card, and never fade the incoming model in over time.
        // Forest pages are hidden until their shared prototype is prepared.
        // They need no second procedural fallback mesh while the same code is
        // being prepared for the individual-tree renderer.
        bool prepared=lod==2&&sharedTreeBakes.ContainsKey(code);
        Mesh mesh=null;
        if(!stand&&!prepared)
        {
            if(prototype!=null)mesh=lod==0?prototype.Near:prototype.Middle;
            else{mesh=LocalScatterMeshes.Build(ScatterKind.GeneratedTree,Math.Min(1,lod),code);ownedScatterMeshes.Add(mesh);}
        }
        long materialKey=ScatterArtKey(ScatterKind.GeneratedTree,code&~7,0);
        if(!treeMaterials.TryGetValue(materialKey,out var material))
        {
            var shader=Resources.Load<Shader>("WorldGenTreeSurface");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Tree prototype shader unavailable");
            // Advertise runtime instancing to Unity's player-build stripper,
            // exactly as the existing scatter renderer's Resources material does.
            var template=Resources.Load<Material>("TreeSurfaceMaterial");
            if(template==null||template.shader!=shader||!template.enableInstancing)throw new InvalidOperationException("Tree instancing template missing or invalid");
            material=new Material(template){enableInstancing=true};var m=new TreeMorphology(code);
            // Keep the literal SM4 variant for older devices. Fine derivatives
            // are required only by the conservative empty-quad optimization.
            if(SystemInfo.graphicsShaderLevel>=45)material.EnableKeyword("WORLDGEN_TREE_QUAD_ALPHA");
            material.SetTexture("_TreeLeaves",rebuiltLeaves);material.SetTexture("_TreeLeafNormals",rebuiltLeafNormals);
            LocalTreeTextures.BindBark(material);
            material.SetFloat("_TreeLeafShape",(int)m.LeafShape);material.SetFloat("_TreeBarkPattern",LocalTreeTextures.BarkLayer(m));
            material.SetFloat("_Wind",(float)m.Flexibility);material.SetVector("_PlantTraits",new Vector4((float)m.Flexibility,.5f,.7f,1));
            material.SetColor("_AmbientTint",landMaterial.GetColor("_AmbientTint"));material.SetColor("_FogColor",landMaterial.GetColor("_FogColor"));
            material.SetFloat("_FogDistance",landMaterial.GetFloat("_FogDistance"));RegisterScatterMaterial(material);treeMaterials.Add(materialKey,material);
        }
        var model=new ArtModel{Parts=new[]{new ArtPart{Mesh=mesh,Material=material,Submesh=0,Local=Matrix4x4.identity}},Scale=Matrix4x4.identity,IdentityScale=true,Fades=true,Generated=true,LastUsed=Time.realtimeSinceStartupAsDouble,TreeCode=code,TreeBake=lod==2,TreeStand=stand};
        scatterArt.Add(key,model);
        if(prepared)InstallTreeBake(model,sharedTreeBakes[code]);
        if(modelStarted!=0)
        {
            double elapsed=(System.Diagnostics.Stopwatch.GetTimestamp()-modelStarted)*1000d/System.Diagnostics.Stopwatch.Frequency;
            if(elapsed>=8)Debug.Log("TREE MODEL CREATE code="+code+" lod="+lod+" ms="+elapsed.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" realtime="+Time.realtimeSinceStartupAsDouble.ToString("F6",System.Globalization.CultureInfo.InvariantCulture));
        }
        return model;
    }
    void InstallTreeBake(ArtModel owner,SharedTreeBake data)
    {
        if(owner.TreeBaked)return;
        var old=owner.Parts[0];var quad=new Mesh{name="Multi-view tree quad"};
        quad.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0)};
        quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};quad.triangles=new[]{0,1,2,1,3,2};
        quad.bounds=new Bounds(data.Center,Vector3.one*data.Radius*2.1f);
        var material=owner.TreeStand?new Material(Resources.Load<Shader>("WorldGenForest")):new Material(old.Material);
        if(owner.TreeStand){material.CopyPropertiesFromMaterial(old.Material);material.DisableKeyword("WORLDGEN_TREE_QUAD_ALPHA");}
        material.enableInstancing=true;material.name="Multi-view tree "+owner.TreeCode;
        material.SetFloat("_Impostor",1);material.SetFloat("_Wind",0);material.SetTexture("_TreeColourViews",data.Colour);material.SetTexture("_TreeNormalViews",data.Normal);
        material.SetVector("_ImpostorBounds",new Vector4(data.Center.x,data.Center.y,data.Center.z,data.Radius));
        material.SetVector("_ImpostorExtents",new Vector4(data.Extents.x,data.Extents.y,data.Extents.z,4*data.Radius/LocalTreeImpostor.Size));
        material.renderQueue=owner.TreeStand?2002:2001;
        if(old.Mesh!=null&&ownedScatterMeshes.Remove(old.Mesh))Destroy(old.Mesh);
        ownedScatterMeshes.Add(quad);RegisterScatterMaterial(material);
        owner.TreeColour=data.Colour;owner.TreeNormal=data.Normal;data.References++;
        owner.Parts=new[]{new ArtPart{Mesh=quad,Material=material,Submesh=0,Local=Matrix4x4.identity}};owner.TreeBaked=true;
        scatterDirty=true;forestHashDirty=true;
    }
    void UpdateTreeImpostors()
    {
        if(!CanInstall)return;
        long installed=System.Diagnostics.Stopwatch.GetTimestamp();
        if(treeBakeTask!=null&&treeBakeTask.IsCompleted)
        {
            var owner=treeBakeOwner;owner.TreeQueued=false;
            if(treeBakeTask.IsCompletedSuccessfully&&scatterArt.Values.Contains(owner))
            {
                var data=treeBakeTask.Result;
                var shared=new SharedTreeBake{Center=data.Center,Extents=data.Extents,Radius=data.Radius};
                LocalTreeTextures.Upload(data.Prepared,LocalTreeImpostor.Size,"Tree "+owner.TreeCode+" views",out shared.Colour,out shared.Normal);
                sharedTreeBakes.Add(owner.TreeCode,shared);
                // Local and indirect forest rendering have different shaders,
                // but consume one bake of the same physical prototype.
                foreach(var model in scatterArt.Values)
                    if(model.TreeBake&&!model.TreeBaked&&model.TreeCode==owner.TreeCode)InstallTreeBake(model,shared);
                installationsThisFrame++;frameUploadMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-installed)*1000d/System.Diagnostics.Stopwatch.Frequency;
            }
            else if(treeBakeTask.IsFaulted)
            {treeBakeFailures.Add(owner);Debug.LogWarning((owner.TreeStand?"Forest prototype remains unpublished: ":"Tree bake retained full-mesh fallback: ")+treeBakeTask.Exception.GetBaseException().Message);}
            treeBakeTask=null;treeBakeOwner=null;
        }
        if(!CanInstall)return;
        installed=System.Diagnostics.Stopwatch.GetTimestamp();
        if(treeBakeTask!=null||!TerrainVideoOptions.TreeRebuild||!TerrainVideoOptions.TreeImpostors||!TerrainVideoOptions.GeneratedTrees&&!TerrainVideoOptions.FarForest)return;
        double now=Time.realtimeSinceStartupAsDouble;int trees=0,stands=0;bool any=false;
        foreach(var model in scatterArt.Values)
        {
            if(model.TreeBaked){if(model.TreeStand)stands++;else trees++;}
            if(model.TreeBake&&!model.TreeBaked&&(model.TreeStand?TerrainVideoOptions.FarForest:TerrainVideoOptions.GeneratedTrees)&&!treeBakeFailures.Contains(model)&&now-model.LastUsed<3)any=true;
        }
        // An idle renderer needs no iterator/sort allocations. Preserve the
        // same stable priority and recency ordering when work actually exists.
        if(!any)return;
        bool preferStand=stands<3||(trees+stands)%3==0;ArtModel pending=null;int priority=int.MaxValue;
        foreach(var model in scatterArt.Values)
        {
            if(!model.TreeBake||model.TreeBaked||!(model.TreeStand?TerrainVideoOptions.FarForest:TerrainVideoOptions.GeneratedTrees)||treeBakeFailures.Contains(model)||now-model.LastUsed>=3)continue;
            int rank=(model.TreeStand==preferStand?0:2)+((model.TreeCode&7)==0?0:1);
            if(pending==null||rank<priority||rank==priority&&model.LastUsed>pending.LastUsed){pending=model;priority=rank;}
        }
        if(pending==null)return;
        if(!pending.TreeStand&&BakedTreeModels>=TreeBakeLimit)
        {
            // Hard texture budget. Retire only models not referenced by current
            // draw batches; never destroy textures still used by a draw.
            var used=new HashSet<ArtModel>(drawBatches.Select(b=>b.Model));
            // A camera turn must not evict and re-bake the forest behind us.
            var expired=scatterArt.Where(p=>p.Value.TreeBaked&&!p.Value.TreeStand&&!used.Contains(p.Value)&&Time.realtimeSinceStartupAsDouble-p.Value.LastUsed>30).OrderBy(p=>p.Value.LastUsed).FirstOrDefault();
            if(expired.Value==null)
            {
                // A full visible local-tree cache must not starve all horizon
                // bakes indefinitely in a high-diversity forest.
                pending=scatterArt.Values.FirstOrDefault(m=>m.TreeStand&&!m.TreeBaked&&!treeBakeFailures.Contains(m)&&Time.realtimeSinceStartupAsDouble-m.LastUsed<3);
                if(pending==null)return;
            }
            else
            {
                foreach(var part in expired.Value.Parts){if(ownedScatterMeshes.Remove(part.Mesh))Destroy(part.Mesh);ownedScatterMaterials.Remove(part.Material);Destroy(part.Material);}
                ReleaseTreeTextures(expired.Value);scatterArt.Remove(expired.Key);
            }
        }
        ScheduleTreeBake(pending,installed);
    }
    void ScheduleTreeBake(ArtModel pending,long installed)
    {
        long nearKey=ScatterArtKey(ScatterKind.GeneratedTree,pending.TreeCode,0);Mesh source;bool temporary=false;
        if(scatterArt.TryGetValue(nearKey,out var near))source=near.Parts[0].Mesh;
        else{source=LocalScatterMeshes.Build(ScatterKind.GeneratedTree,0,pending.TreeCode);temporary=true;}
        var morphology=new TreeMorphology(pending.TreeCode);
        var snapshot=new LocalTreeImpostor.Source(source,(int)morphology.LeafShape,LocalTreeTextures.BarkLayer(morphology));if(temporary)Destroy(source);
        var token=cancellation.Token;pending.TreeQueued=true;treeBakeOwner=pending;
        treeBakeTask=Task.Run(()=>{var result=LocalTreeImpostor.Bake(snapshot,token);result.Prepared=LocalTreeTextures.Prepare(result.Colours,result.Normals,LocalTreeImpostor.Size,token);return result;},token);
        installationsThisFrame++;frameUploadMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-installed)*1000d/System.Diagnostics.Stopwatch.Frequency;
    }
    void ReleaseTreeTextures(ArtModel model)
    {
        if(model.TreeBaked&&sharedTreeBakes.TryGetValue(model.TreeCode,out var shared)&&--shared.References==0)
        {
            if(!shared.Borrowed){if(shared.Colour!=null)Destroy(shared.Colour);if(shared.Normal!=null)Destroy(shared.Normal);}
            sharedTreeBakes.Remove(model.TreeCode);
        }
        model.TreeColour=model.TreeNormal=null;model.TreeBaked=false;
    }
    void DisposeTreeImpostors()
    {
        foreach(var model in scatterArt.Values)ReleaseTreeTextures(model);
        // Shared immutable Resources assets; destroying them would invalidate a
        // second explorer/revisit and force a duplicate allocation.
        rebuiltLeaves=null;rebuiltLeafNormals=null;
    }
}
