using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class HumanMorphFrame
    {public HumanFormTrait Trait;public bool High;public int[] Indices;public Vector3[] Positions,Normals;}
    sealed class HumanMorphSource
    {public Vector3[] Positions,Normals;public HumanMorphFrame[] Frames;}
    WorldGenHumanPhenotypeLibrary humanPhenotypeLibrary;bool humanPhenotypeLoaded;
    readonly Dictionary<Mesh,HumanPhenotypeMesh> humanPhenotypeMeshes=new Dictionary<Mesh,HumanPhenotypeMesh>();
    readonly Dictionary<Mesh,HumanMorphSource> humanMorphSources=new Dictionary<Mesh,HumanMorphSource>();
    readonly Dictionary<(Material,int,int,int,int),Material> humanPigments=new Dictionary<(Material,int,int,int,int),Material>();
    readonly Dictionary<Texture,float> humanPigmentReferences=new Dictionary<Texture,float>();
    void EnsureHumanPhenotypes()
    {
        if(humanPhenotypeLoaded)return;humanPhenotypeLoaded=true;
        humanPhenotypeLibrary=Resources.Load<WorldGenHumanPhenotypeLibrary>("HumanPhenotypes/Library");
        if(humanPhenotypeLibrary==null)return;
        foreach(var entry in humanPhenotypeLibrary.Meshes)humanPhenotypeMeshes[entry.Original]=entry;
        foreach(var entry in humanPhenotypeLibrary.Pigments)if(entry.Texture!=null)humanPigmentReferences[entry.Texture]=entry.Luminance;
    }
    HumanMorphSource HumanMorphData(HumanMorphSurface surface)
    {
        Mesh source=surface.Mesh;
        if(humanMorphSources.TryGetValue(source,out var data))return data;
        var frames=new List<HumanMorphFrame>();
        foreach(var shape in surface.Frames)
        {
            string name=shape.Target;bool high=name.EndsWith("High",StringComparison.Ordinal);
            string trait=name.Substring(0,name.Length-(high?4:3));if(!Enum.TryParse(trait,out HumanFormTrait axis))continue;
            frames.Add(new HumanMorphFrame{Trait=axis,High=high,Indices=shape.Vertices,Positions=shape.Positions,Normals=shape.Normals});
        }
        data=new HumanMorphSource{Positions=source.vertices,Normals=source.normals,Frames=frames.ToArray()};humanMorphSources[source]=data;return data;
    }
    Mesh BakeInheritedHumanMesh(HumanActor actor,HumanMorphSurface surface,HumanPhenotype phenotype)
    {
        Mesh source=surface.Mesh;var data=HumanMorphData(surface);var positions=(Vector3[])data.Positions.Clone();var normals=(Vector3[])data.Normals.Clone();
        foreach(var shape in data.Frames)
        {
            float signed=(float)(phenotype.Trait(shape.Trait)*2-1);float weight=shape.High?Mathf.Max(0,signed):Mathf.Max(0,-signed);
            if(weight<.0001f)continue;
            for(int n=0;n<shape.Indices.Length;n++){int v=shape.Indices[n];positions[v]+=shape.Positions[n]*weight;normals[v]+=shape.Normals[n]*weight;}
        }
        for(int i=0;i<normals.Length;i++)normals[i].Normalize();
        var mesh=Instantiate(source);mesh.name="Inherited human "+actor.Id;mesh.ClearBlendShapes();mesh.vertices=positions;mesh.normals=normals;mesh.RecalculateBounds();actor.PhenotypeMeshes.Add(mesh);return mesh;
    }
    Material HumanPigment(Material original,int mode,V swatch)
    {
        // Quantize material colours only, preserving the saved continuous traits.
        // Shared palettes keep UnityPerMaterial/SRP batching; ordinary NPCs do
        // not acquire one per-renderer property block for skin colour.
        int r=Mathf.RoundToInt((float)swatch.X*63),g=Mathf.RoundToInt((float)swatch.Y*63),b=Mathf.RoundToInt((float)swatch.Z*63);
        var key=(original,mode,r,g,b);if(humanPigments.TryGetValue(key,out var material))return material;
        material=new Material(original);material.name="Human pigment "+mode+" "+r+"-"+g+"-"+b;
        material.SetFloat("_PigmentMode",mode);material.SetColor("_PigmentColor",new Color(r/63f,g/63f,b/63f,1).linear);
        var texture=original.GetTexture("_BaseMap");material.SetFloat("_PigmentReference",texture!=null&&humanPigmentReferences.TryGetValue(texture,out float reference)?reference:.12f);
        humanPigments[key]=material;return material;
    }
    void ApplyInheritedHumanAppearance(HumanActor actor,ForagerPersonSample person)
    {
        if(person?.Phenotype==null)return;EnsureHumanPhenotypes();EnsureHumanDetail();
        var phenotype=person.Phenotype;phenotype.Validate();actor.Phenotype=phenotype;
        string variant=actor.Definition.Variant+(actor.AgeModel==null?"":"-"+actor.AgeModel.Years);
        bool styled=humanPhenotypeLibrary?.Hair.Any(h=>h.Variant==variant)==true;
        var bodySkin=actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name.ToLowerInvariant().EndsWith(".body")||r.name.EndsWith("_Body"));
        var cullBounds=bodySkin.localBounds;cullBounds.Expand(cullBounds.size);
        foreach(var renderer in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            string name=renderer.name.ToLowerInvariant();
            bool eyes=name.Contains("high-poly")||name.EndsWith("_eyes");
            bool beard=name.Contains("beard")||name.Contains("moustache");
            bool headHair=!beard&&!name.EndsWith(".body")&&!name.EndsWith("_body")&&!eyes&&!name.Contains("teeth")&&!name.Contains("eyebrow")&&!name.Contains("eyelash");
            if(headHair&&(styled||person.Grooming?.Hair==HumanHairStyle.Shaved)||beard&&(person.Female||person.Grooming?.Beard==HumanBeardStyle.None))
            {renderer.gameObject.SetActive(false);continue;}
            if(beard)renderer.name="hair "+renderer.name;
            Mesh original=renderer.sharedMesh;
            if(humanPhenotypeMeshes.TryGetValue(original,out var entry))
            {
                Mesh full=BakeInheritedHumanMesh(actor,entry.Full,phenotype);renderer.sharedMesh=full;
                if(entry.Middle?.Mesh!=null&&entry.Far?.Mesh!=null)
                {
                    var details=new HumanDetailMesh{Original=full,Middle=BakeInheritedHumanMesh(actor,entry.Middle,phenotype),Far=BakeInheritedHumanMesh(actor,entry.Far,phenotype)};
                    humanDetailMeshes[details.Original]=humanDetailMeshes[details.Middle]=humanDetailMeshes[details.Far]=details;
                }
            }
            int mode=name.EndsWith(".body")||name.EndsWith("_body")?1:eyes?3:name.Contains("teeth")?0:2;
            if(mode>0)
            {
                V color=mode==1?HumanPhenotypes.SkinColor(phenotype):mode==2?HumanPhenotypes.HairColor(phenotype,person.AgeYears):HumanPhenotypes.IrisColor(phenotype);
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    // Preserve camera visibility identity on the local body.
                    var shared=HumanPigment(materials[i],mode,color);
                    if(actor.Id==1){var own=new Material(shared);own.SetFloat("_HumanViewId",1);actor.OwnedMaterials.Add(own);materials[i]=own;}else materials[i]=shared;
                }
                renderer.sharedMaterials=materials;
            }
            // Imported eye/brow bounds are tight rest-pose boxes. Child age
            // correction and individual facial shapes can move beyond them.
            // Reuse the body's conservative bounds in the same root-bone frame,
            // keeping offscreen skin evaluation disabled.
            if(renderer.rootBone==bodySkin.rootBone)renderer.localBounds=cullBounds;
            if(actor.Id!=1)renderer.updateWhenOffscreen=false;
        }
        if(styled)CreateInheritedHair(actor,person);
        // Native animation data remains in its original metre scale; root scale
        // controls actual drawn anatomy. Child growth multiplies this later.
        float height=(float)(phenotype.AdultStatureM/(actor.Definition.EyeHeight+.12));
        float compact=(float)(.94+.12*phenotype.Trait(HumanFormTrait.Compactness));
        actor.PhenotypeScale=new Vector3(height*compact,height,height*compact);actor.Root.transform.localScale=actor.PhenotypeScale;
    }
    void CreateInheritedHair(HumanActor actor,ForagerPersonSample person)
    {
        HumanHairStyle style=person.Grooming?.Hair??HumanHairStyle.LooseShort;if(style==HumanHairStyle.Shaved)return;
        int curl=person.Phenotype.Trait(HumanFormTrait.Curl)>.58?1:0;
        string variant=actor.Definition.Variant+(actor.AgeModel==null?"":"-"+actor.AgeModel.Years);
        var candidates=humanPhenotypeLibrary.Hair.Where(h=>h.Variant==variant&&h.Style==style&&(h.Curl<0||h.Curl==curl)).ToArray();
        if(candidates.Length==0)return;
        var chosen=candidates[(int)(HumanHash(person.Id)%(uint)candidates.Length)];
        var body=actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name.ToLowerInvariant().EndsWith(".body")||r.name.EndsWith("_Body"));
        var root=new GameObject("hair "+chosen.Part);root.layer=30;root.transform.SetParent(body.transform.parent,false);root.transform.localPosition=body.transform.localPosition;root.transform.localRotation=body.transform.localRotation;root.transform.localScale=body.transform.localScale;
        var renderer=root.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=BakeInheritedHumanMesh(actor,chosen.Surface,person.Phenotype);renderer.bones=body.bones;renderer.rootBone=body.rootBone;
        // Both skins share the original root bone. Its imported skin bounds
        // are in that bone's reference frame, unlike the unposed mesh AABB.
        renderer.localBounds=body.localBounds;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;renderer.receiveShadows=true;renderer.updateWhenOffscreen=false;
        var pigment=HumanPigment(chosen.Material,2,HumanPhenotypes.HairColor(person.Phenotype,person.AgeYears));
        if(actor.Id==1){pigment=new Material(pigment);pigment.SetFloat("_HumanViewId",1);actor.OwnedMaterials.Add(pigment);}renderer.sharedMaterial=pigment;
    }
    float HumanActualEye(HumanActor actor)=>(actor.AgeModel?.EyeHeight??actor.Definition.EyeHeight)*actor.Root.transform.localScale.y;
    void DisposeInheritedHuman(HumanActor actor)
    {
        foreach(var mesh in actor.PhenotypeMeshes)
        {
            humanDetailMeshes.Remove(mesh);
            foreach(var key in garmentMeshes.Keys.Where(k=>k.Item1==mesh).ToArray()){if(garmentMeshes[key]!=null)Destroy(garmentMeshes[key]);garmentMeshes.Remove(key);}
            if(mesh!=null)Destroy(mesh);
        }
        actor.PhenotypeMeshes.Clear();
    }
    void DisposeHumanPhenotypes()
    {
        foreach(var material in humanPigments.Values)if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
        humanPigments.Clear();humanPigmentReferences.Clear();humanMorphSources.Clear();humanPhenotypeMeshes.Clear();humanPhenotypeLibrary=null;humanPhenotypeLoaded=false;
    }
}
