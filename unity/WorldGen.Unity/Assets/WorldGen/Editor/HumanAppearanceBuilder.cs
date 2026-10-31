using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>New appearance meshes, rebound onto the unchanged production rigs.</summary>
public static class HumanAppearanceBuilder
{
    const string Input="Assets/WorldGen/GeneratedHumanAppearance",Output="Assets/WorldGen/Resources/HumanAppearance";
    [Serializable] sealed class Info{public string variant,preview_variant,source_sha256;public float eye_height;public Surface[] materials;}
    [Serializable] sealed class Surface{public string @object,kind,texture;public bool alpha;public float roughness;public int texture_size;}
    static void Check(bool ok,string text){if(!ok)throw new InvalidDataException("Human appearance: "+text);}
    public static void Review()
    {
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        var run=LocalTerrainExplorer.CheckHumanAppearance(Path.Combine(repo,"artifacts/human-appearance-integration/editor-review"),false);
        while(run.MoveNext()){}
    }
    public static void Build()
    {
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        string source=Path.Combine(repo,"assets-source/humans/appearance-runtime/exports");
        string proof=Path.Combine(repo,"artifacts/human-appearance-integration");Directory.CreateDirectory(proof);
        Directory.CreateDirectory(Input);Directory.CreateDirectory(Output);
        foreach(string folder in Directory.GetDirectories(source))
        {
            string target=Path.Combine(Input,Path.GetFileName(folder));Directory.CreateDirectory(target);
            foreach(string file in Directory.GetFiles(folder))File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true);
        }
        AssetDatabase.Refresh();var original=Resources.Load<WorldGenHumanLibrary>("Humans/HumanLibrary");
        var people=new List<HumanRuntimeDefinition>();var report=new List<string>();
        var shader=Shader.Find("WorldGen/Human");Check(shader!=null,"human shader missing");
        foreach(var person in original.People)
        {
            string folder=Input+"/human-"+person.Variant.ToLowerInvariant();
            var info=JsonUtility.FromJson<Info>(File.ReadAllText(folder+"/human.json"));Check(info.variant==person.Variant,"stable identity");
            string model=folder+"/human-"+person.Variant.ToLowerInvariant()+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(model);
            importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=false;importer.isReadable=true;
            importer.optimizeGameObjects=false;importer.importBlendShapes=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
            var newRoot=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(model));
            var root=UnityEngine.Object.Instantiate(person.Prefab);root.name="Human "+person.Variant+" appearance";
            try
            {
                // Recover actual bind transforms from existing production skins,
                // independent of FBX's default preview pose or its unit scale.
                var bones=root.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                var rest=new Dictionary<string,Matrix4x4>();
                foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    for(int i=0;i<r.bones.Length;i++)if(r.bones[i]!=null&&!rest.ContainsKey(r.bones[i].name))rest[r.bones[i].name]=r.localToWorldMatrix*r.sharedMesh.bindposes[i].inverse;
                var saved=bones.Values.ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
                foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {Check(r.transform.childCount==0,"renderer owns skeletal children");UnityEngine.Object.DestroyImmediate(r.gameObject);}
                var bounds=new Bounds();bool first=true;int triangleCount=0;
                foreach(var incoming in newRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var surface=info.materials.Single(m=>m.@object==incoming.name);
                    string stem=Path.GetFileNameWithoutExtension(surface.texture);
                    string texturePath=folder+"/"+surface.texture;
                    var ti=(TextureImporter)AssetImporter.GetAtPath(texturePath);ti.sRGBTexture=true;ti.alphaSource=TextureImporterAlphaSource.FromInput;
                    ti.alphaIsTransparency=surface.alpha;ti.mipmapEnabled=true;ti.filterMode=FilterMode.Trilinear;ti.anisoLevel=8;
                    ti.wrapMode=TextureWrapMode.Clamp;ti.textureCompression=TextureImporterCompression.CompressedHQ;
                    ti.maxTextureSize=surface.texture_size;ti.SaveAndReimport();
                    string directory=Output+"/"+person.Variant;Directory.CreateDirectory(directory);
                    string materialPath=directory+"/"+stem+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}else material.shader=shader;
                    material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                    material.SetFloat("_Roughness",surface.roughness);material.SetFloat("_Cutoff",surface.alpha?.3f:0);
                    var holder=new GameObject(incoming.name);holder.layer=30;holder.transform.SetParent(root.transform,false);
                    var r=holder.AddComponent<SkinnedMeshRenderer>();var mesh=UnityEngine.Object.Instantiate(incoming.sharedMesh);
                    mesh.name=person.Variant+" "+stem+" production-bound";
                    var transform=incoming.localToWorldMatrix;
                    mesh.vertices=mesh.vertices.Select(p=>transform.MultiplyPoint3x4(p)).ToArray();
                    var normal=transform.inverse.transpose;mesh.normals=mesh.normals.Select(n=>normal.MultiplyVector(n).normalized).ToArray();
                    var mapped=incoming.bones.Select(b=>{Check(b!=null&&bones.ContainsKey(b.name)&&rest.ContainsKey(b.name),"unknown bone "+b?.name);return bones[b.name];}).ToArray();
                    mesh.bindposes=mapped.Select(b=>rest[b.name].inverse*holder.transform.localToWorldMatrix).ToArray();
                    mesh.RecalculateBounds();mesh.RecalculateTangents();
                    string meshPath=directory+"/"+stem+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(existing==null){AssetDatabase.CreateAsset(mesh,meshPath);existing=mesh;}
                    else{EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);}
                    r.sharedMesh=existing;r.bones=mapped;r.rootBone=mapped.FirstOrDefault(b=>b.name=="Root")??mapped[0];
                    r.sharedMaterials=new[]{material};r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
                    r.lightProbeUsage=LightProbeUsage.Off;r.updateWhenOffscreen=true;var local=existing.bounds;local.Expand(.5f);r.localBounds=local;
                    Check(existing.colors.Length==existing.vertexCount,"missing first-person head channel");
                    foreach(var p in existing.vertices){Check(float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z),"nonfinite skin");if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
                    triangleCount+=existing.triangles.Length/3;
                }
                foreach(var row in saved.Where(row=>row.Key!=null))Check(row.Key.localPosition==row.Value.Item1&&row.Key.localRotation==row.Value.Item2&&row.Key.localScale==row.Value.Item3,"production skeleton changed");
                Check(bounds.size.y>1.2f&&bounds.size.y<2.4f,"metre scale "+bounds);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,Output+"/Human-"+person.Variant+".prefab");
                people.Add(new HumanRuntimeDefinition{Variant=person.Variant,SourceSha256=person.SourceSha256,Prefab=prefab,EyeHeight=info.eye_height,Clips=person.Clips,Roles=person.Roles,StrideMeters=person.StrideMeters});
                report.Add("PASS "+person.Variant+" <- preview "+info.preview_variant+": unchanged native rig/legacy clips, "+triangleCount+" triangles, bounds "+bounds.size);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(newRoot);}
        }
        Check(people.Count==6,"six stable identities");string path=Output+"/Library.asset";
        var library=AssetDatabase.LoadAssetAtPath<WorldGenHumanLibrary>(path);if(library==null){library=ScriptableObject.CreateInstance<WorldGenHumanLibrary>();AssetDatabase.CreateAsset(library,path);}
        library.People=people.ToArray();EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
        File.WriteAllLines(Path.Combine(proof,"import-checks.txt"),report);Debug.Log("HUMAN APPEARANCE READY "+report.Count);
    }
}
