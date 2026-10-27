using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Import only the read-only MPFB runtime derivatives; no wildlife rebuild.</summary>
public static class HumanRuntimeBuilder
{
    const string Input="Assets/WorldGen/GeneratedHumans",Output="Assets/WorldGen/Resources/Humans";
    [Serializable] sealed class Metadata {public string variant,source_sha256;public float eye_height;public Surface[] materials;public MotionInfo[] clips;}
    [Serializable] sealed class MotionInfo{public string label;public float travel_m;}
    [Serializable] sealed class Surface {public string @object,kind,texture;public bool alpha;public float roughness;}
    static readonly string[] Roles={"Idle","Walk","Run","Sprint","Crouch","Jump","Swim"};
    public static void AuditHeadMasks()
    {
        var library=Resources.Load<WorldGenHumanLibrary>("Humans/HumanLibrary");var rows=new List<string>();
        foreach(var person in library.People)foreach(var renderer in person.Prefab.GetComponentsInChildren<SkinnedMeshRenderer>())
        {var colors=renderer.sharedMesh.colors;rows.Add(person.Variant+" "+renderer.name+" head="+colors.Count(c=>c.r>.5f)+"/"+colors.Length+" red="+(colors.Length>0?colors.Max(c=>c.r):0));}
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));File.WriteAllLines(Path.Combine(repo,"artifacts/human-runtime/head-mask-audit.txt"),rows);
    }
    public static void Build()
    {
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        string source=Path.Combine(repo,"artifacts/human-runtime/exports");
        Directory.CreateDirectory(Input);Directory.CreateDirectory(Output);
        foreach(string directory in Directory.GetDirectories(source))
        {
            string target=Path.Combine(Input,Path.GetFileName(directory));Directory.CreateDirectory(target);
            foreach(string file in Directory.GetFiles(directory))File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true);
        }
        AssetDatabase.Refresh();var people=new List<HumanRuntimeDefinition>();var checks=new List<string>();
        var shader=Shader.Find("WorldGen/Human");if(shader==null)throw new InvalidDataException("Human shader missing");
        foreach(string directory in Directory.GetDirectories(Input).OrderBy(p=>p,StringComparer.Ordinal))
        {
            var info=JsonUtility.FromJson<Metadata>(File.ReadAllText(Path.Combine(directory,"human.json")));
            string modelPath=(directory+"/human-"+info.variant.ToLowerInvariant()+".fbx").Replace('\\','/');
            var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation=true;importer.isReadable=true;importer.importBlendShapes=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.animationCompression=ModelImporterAnimationCompression.Off;
            var selected=new List<ModelImporterClipAnimation>();
            foreach(var clip in importer.defaultClipAnimations)
            {
                string role=Roles.FirstOrDefault(r=>clip.name.EndsWith("| "+r,StringComparison.Ordinal)||clip.name==r);
                if(role==null)continue;
                clip.name=role;clip.loopTime=role!="Jump";clip.loopPose=role!="Jump";
                clip.lockRootRotation=true;clip.keepOriginalOrientation=true;
                clip.lockRootPositionXZ=true;clip.keepOriginalPositionXZ=true;clip.keepOriginalPositionY=true;
                selected.Add(clip);
            }
            if(selected.Select(c=>c.name).Distinct().Count()!=Roles.Length)throw new InvalidDataException("Human motion takes: "+modelPath+" : "+string.Join(",",importer.defaultClipAnimations.Select(c=>c.name)));
            importer.clipAnimations=selected.ToArray();importer.SaveAndReimport();
            var sourceRoot=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);var root=UnityEngine.Object.Instantiate(sourceRoot);
            root.name="Human "+info.variant;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            var animator=root.GetComponent<Animator>()??root.AddComponent<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var bounds=new Bounds();bool first=true;
            foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var surface=info.materials.FirstOrDefault(m=>m.@object==renderer.name);
                if(surface==null)throw new InvalidDataException("Unknown human mesh "+renderer.name);
                string texturePath=(directory+"/"+surface.texture).Replace('\\','/');
                var textureImporter=(TextureImporter)AssetImporter.GetAtPath(texturePath);
                textureImporter.sRGBTexture=true;textureImporter.alphaSource=TextureImporterAlphaSource.FromInput;
                textureImporter.alphaIsTransparency=surface.alpha;textureImporter.mipmapEnabled=true;textureImporter.wrapMode=TextureWrapMode.Clamp;
                textureImporter.textureCompression=TextureImporterCompression.Compressed;textureImporter.maxTextureSize=surface.kind=="Basemesh"?2048:1024;
                textureImporter.SaveAndReimport();
                string matPath=Output+"/"+info.variant+"-"+Path.GetFileNameWithoutExtension(surface.texture)+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,matPath);}else material.shader=shader;
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                material.SetFloat("_Roughness",surface.roughness);material.SetFloat("_Cutoff",surface.alpha?.3f:0);
                renderer.sharedMaterials=new[]{material};renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.updateWhenOffscreen=true;
                // Importer preview bounds can describe its first crouch take.
                // Validate physical bind geometry independently of that preview.
                foreach(var vertex in renderer.sharedMesh.vertices)
                {var point=renderer.transform.TransformPoint(vertex);if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}
                var colors=renderer.sharedMesh.colors;
                if(colors.Length!=renderer.sharedMesh.vertexCount)throw new InvalidDataException("Missing first-person head attribute");
            }
            if(bounds.size.y<1.2||bounds.size.y>2.4)throw new InvalidDataException("Human scale "+info.variant+" "+bounds);
            string prefabPath=Output+"/Human-"+info.variant+".prefab";
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,prefabPath);UnityEngine.Object.DestroyImmediate(root);
            var clips=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
            var ordered=Roles.Select(role=>clips.Single(c=>c.name==role)).ToArray();
            people.Add(new HumanRuntimeDefinition{Variant=info.variant,SourceSha256=info.source_sha256,Prefab=prefab,EyeHeight=info.eye_height,Clips=ordered,Roles=Roles,StrideMeters=Roles.Select(role=>info.clips?.FirstOrDefault(c=>c.label==role)?.travel_m??0).ToArray()});
            checks.Add(info.variant+" "+bounds.size+"; "+ordered.Length+" clips; "+prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(r=>r.sharedMesh.triangles.Length/3)+" triangles");
        }
        if(people.Count!=6)throw new InvalidDataException("Expected six MPFB variants");
        string libraryPath=Output+"/HumanLibrary.asset";var library=AssetDatabase.LoadAssetAtPath<WorldGenHumanLibrary>(libraryPath);
        if(library==null){library=ScriptableObject.CreateInstance<WorldGenHumanLibrary>();AssetDatabase.CreateAsset(library,libraryPath);}
        library.People=people.ToArray();EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
        File.WriteAllLines(Path.Combine(repo,"artifacts/human-runtime/unity-import.txt"),checks);
        Debug.Log("HUMANS READY "+string.Join(" / ",checks));
    }
}
