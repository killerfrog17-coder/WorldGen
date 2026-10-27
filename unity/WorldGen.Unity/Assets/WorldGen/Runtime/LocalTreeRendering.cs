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
    const int TreeBakeLimit=96;
    public int BakedTreeModels=>scatterArt.Values.Count(m=>m.TreeBaked&&!m.TreeStand);
    public int BakedForestModels=>scatterArt.Values.Count(m=>m.TreeBaked&&m.TreeStand);
    public int PendingTreeModels=>scatterArt.Values.Count(m=>m.TreeBake&&!m.TreeBaked);
    public long TreeBakeBytes=>(BakedTreeModels+BakedForestModels)*(long)LocalTreeImpostor.Size*LocalTreeImpostor.Size*LocalTreeImpostor.Views*8*4/3;
    ArtModel CreateRebuiltTree(long key,int code,int lod)
    {
        if(rebuiltLeaves==null)LocalTreeTextures.Create(out rebuiltLeaves,out rebuiltLeafNormals);
        // Keep a complete middle mesh until its shared-tree bake is ready. Never
        // show a blank card, and never fade the incoming model in over time.
        var mesh=LocalScatterMeshes.Build(ScatterKind.GeneratedTree,Math.Min(1,lod),code);ownedScatterMeshes.Add(mesh);
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
            material.SetFloat("_TreeLeafShape",(int)m.LeafShape);material.SetFloat("_TreeBarkPattern",(int)m.BarkPattern);
            material.SetFloat("_Wind",(float)m.Flexibility);material.SetVector("_PlantTraits",new Vector4((float)m.Flexibility,.5f,.7f,1));
            material.SetColor("_AmbientTint",landMaterial.GetColor("_AmbientTint"));material.SetColor("_FogColor",landMaterial.GetColor("_FogColor"));
            material.SetFloat("_FogDistance",landMaterial.GetFloat("_FogDistance"));RegisterScatterMaterial(material);treeMaterials.Add(materialKey,material);
        }
        var model=new ArtModel{Parts=new[]{new ArtPart{Mesh=mesh,Material=material,Submesh=0,Local=Matrix4x4.identity}},Scale=Matrix4x4.identity,IdentityScale=true,Fades=true,Generated=true,LastUsed=Time.realtimeSinceStartupAsDouble,TreeCode=code,TreeBake=lod==2};
        scatterArt.Add(key,model);return model;
    }
    void UpdateTreeImpostors()
    {
        if(treeBakeTask!=null&&treeBakeTask.IsCompleted)
        {
            var owner=treeBakeOwner;owner.TreeQueued=false;
            if(treeBakeTask.IsCompletedSuccessfully&&scatterArt.Values.Contains(owner))
            {
                var data=treeBakeTask.Result;
                LocalTreeTextures.Upload(data.Prepared,LocalTreeImpostor.Size,"Tree "+owner.TreeCode+" views",out owner.TreeColour,out owner.TreeNormal);
                var old=owner.Parts[0];var quad=new Mesh{name="Multi-view tree quad"};
                quad.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0)};
                quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};quad.triangles=new[]{0,1,2,1,3,2};
                quad.bounds=new Bounds(data.Center,Vector3.one*data.Radius*2.1f);
                var material=owner.TreeStand?new Material(Resources.Load<Shader>("WorldGenForest")):new Material(old.Material);
                if(owner.TreeStand){material.CopyPropertiesFromMaterial(old.Material);material.DisableKeyword("WORLDGEN_TREE_QUAD_ALPHA");}
                material.name="Multi-view tree "+owner.TreeCode;
                material.SetFloat("_Impostor",1);material.SetFloat("_Wind",0);material.SetTexture("_TreeColourViews",owner.TreeColour);material.SetTexture("_TreeNormalViews",owner.TreeNormal);
                material.SetVector("_ImpostorBounds",new Vector4(data.Center.x,data.Center.y,data.Center.z,data.Radius));
                // Keep depth-writing cutouts opaque, but establish terrain and
                // close structural depth before the expensive impostor pixels.
                // Forest batches use camera-centred conservative sort bounds;
                // without explicit tiers they can sort ahead of all terrain.
                material.renderQueue=owner.TreeStand?2002:2001;
                ownedScatterMeshes.Remove(old.Mesh);Destroy(old.Mesh);ownedScatterMeshes.Add(quad);RegisterScatterMaterial(material);
                owner.Parts=new[]{new ArtPart{Mesh=quad,Material=material,Submesh=0,Local=Matrix4x4.identity}};owner.TreeBaked=true;scatterDirty=true;forestHashDirty=true;
            }
            else if(treeBakeTask.IsFaulted)
            {treeBakeFailures.Add(owner);Debug.LogWarning("Tree bake retained full-mesh fallback: "+treeBakeTask.Exception.GetBaseException().Message);}
            treeBakeTask=null;treeBakeOwner=null;
        }
        if(treeBakeTask!=null||!TerrainVideoOptions.TreeRebuild||!TerrainVideoOptions.TreeImpostors||!TerrainVideoOptions.GeneratedTrees&&!TerrainVideoOptions.FarForest||!CenterReady||NearTerrainPending())return;
        bool preferStand=BakedForestModels<3||(BakedTreeModels+BakedForestModels)%3==0;
        var pending=scatterArt.Values.Where(m=>m.TreeBake&&!m.TreeBaked&&(m.TreeStand?TerrainVideoOptions.FarForest:TerrainVideoOptions.GeneratedTrees)&&!treeBakeFailures.Contains(m)&&Time.realtimeSinceStartupAsDouble-m.LastUsed<3).OrderBy(m=>m.TreeStand==preferStand?0:1).ThenBy(m=>(m.TreeCode&7)==0?0:1).ThenByDescending(m=>m.LastUsed).FirstOrDefault();
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
                foreach(var part in expired.Value.Parts){ownedScatterMeshes.Remove(part.Mesh);Destroy(part.Mesh);ownedScatterMaterials.Remove(part.Material);Destroy(part.Material);}
                ReleaseTreeTextures(expired.Value);scatterArt.Remove(expired.Key);
            }
        }
        long nearKey=ScatterArtKey(ScatterKind.GeneratedTree,pending.TreeCode,0);Mesh source;bool temporary=false;
        if(scatterArt.TryGetValue(nearKey,out var near))source=near.Parts[0].Mesh;
        else{source=LocalScatterMeshes.Build(ScatterKind.GeneratedTree,0,pending.TreeCode);temporary=true;}
        var snapshot=new LocalTreeImpostor.Source(source,(int)new TreeMorphology(pending.TreeCode).LeafShape);if(temporary)Destroy(source);
        var token=cancellation.Token;pending.TreeQueued=true;treeBakeOwner=pending;
        treeBakeTask=Task.Run(()=>{var result=LocalTreeImpostor.Bake(snapshot,token);result.Prepared=LocalTreeTextures.Prepare(result.Colours,result.Normals,LocalTreeImpostor.Size,token);return result;},token);
    }
    static void ReleaseTreeTextures(ArtModel model)
    {if(model.TreeColour!=null)UnityEngine.Object.Destroy(model.TreeColour);if(model.TreeNormal!=null)UnityEngine.Object.Destroy(model.TreeNormal);model.TreeColour=model.TreeNormal=null;model.TreeBaked=false;}
    void DisposeTreeImpostors()
    {
        foreach(var model in scatterArt.Values)ReleaseTreeTextures(model);
        if(rebuiltLeaves!=null)Destroy(rebuiltLeaves);if(rebuiltLeafNormals!=null)Destroy(rebuiltLeafNormals);
    }
}
