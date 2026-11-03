using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class HumanAgeBuilder
{
    const string Input="Assets/WorldGen/GeneratedHumanAges",Output="Assets/WorldGen/Resources/HumanAges";
    [Serializable] sealed class BodyInfo{public string variant;public int years;public float eye_height;public Surface[] surfaces;}
    [Serializable] sealed class Surface{public string @object,kind;}
    public static void BuildAllNpc(){KimodoHumanBankBuilder.BuildNpc();Build();}
    static string Kind(string name)=>name.IndexOf("body",StringComparison.OrdinalIgnoreCase)>=0?"Body":name.Contains("high-poly")?"Eyes":name.Contains("teeth")?"Teeth":name.Contains("eyebrow")?"Eyebrows":name.Contains("eyelashes")?"Eyelashes":"Hair";
    public static void Build()
    {
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../..")),source=Path.Combine(repo,"assets-source/humans/npc-ages");
        Directory.CreateDirectory(Input);Directory.CreateDirectory(Output);
        var rows=new List<BodyInfo>();
        foreach(string folder in Directory.GetDirectories(source))
        {
            string json=Path.Combine(folder,"body.json");if(!File.Exists(json))continue;var info=JsonUtility.FromJson<BodyInfo>(File.ReadAllText(json));rows.Add(info);
            string name=Path.GetFileName(folder);File.Copy(Path.Combine(folder,name+".fbx"),Input+"/"+name+".fbx",true);
        }
        foreach(string hair in new[]{"ponytail01","short02"})File.Copy(Path.Combine(repo,"assets-source/humans/research/builders/mpfb-assets/hair/"+hair+"/"+hair+"_diffuse.png"),Input+"/"+hair+".png",true);
        AssetDatabase.Refresh();var originals=Resources.Load<WorldGenHumanLibrary>("HumanAppearance/Library");var result=new List<HumanAgeModel>();
        var hairMaterials=new Dictionary<string,Material>();
        foreach(string hair in new[]{"ponytail01","short02"})
        {
            string texture=Input+"/"+hair+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(texture);importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.maxTextureSize=512;importer.SaveAndReimport();
            string materialPath=Output+"/"+hair+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(Shader.Find("WorldGen/Human"));AssetDatabase.CreateAsset(material,materialPath);}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texture));material.SetFloat("_Roughness",.85f);material.SetFloat("_Cutoff",.25f);hairMaterials[hair]=material;EditorUtility.SetDirty(material);
        }
        foreach(var info in rows.OrderBy(p=>p.variant).ThenBy(p=>p.years))
        {
            string name=info.variant+"-"+info.years,path=Input+"/"+name+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=false;importer.importBlendShapes=false;importer.isReadable=true;importer.optimizeGameObjects=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
            var adult=originals.People.Single(p=>p.Variant==info.variant);var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));root.name="Human "+info.variant+" age "+info.years;
            try
            {
                if(root.GetComponent<Animator>()==null)root.AddComponent<Animator>();
                var paths=new HashSet<string>(root.GetComponentsInChildren<Transform>(true).Select(t=>AnimationUtility.CalculateTransformPath(t,root.transform)));
                var missing=AnimationUtility.GetCurveBindings(adult.Clips[0]).Where(b=>b.type==typeof(Transform)&&!paths.Contains(b.path)).Select(b=>b.path).Distinct().ToArray();
                if(missing.Length>0)throw new InvalidDataException("Age model native path mismatch "+name+": "+string.Join(",",missing));
                foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    string kind=info.surfaces.Single(s=>s.@object==renderer.name).kind;
                    var donor=adult.Prefab.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r=>Kind(r.name)==kind);
                    if(donor==null)throw new InvalidDataException("No age material donor for "+name+"/"+kind);
                    renderer.sharedMaterials=Enumerable.Repeat(kind=="Hair"?hairMaterials[info.variant=="A"||info.variant=="C"||info.variant=="E"?"ponytail01":"short02"]:donor.sharedMaterial,renderer.sharedMesh.subMeshCount).ToArray();renderer.updateWhenOffscreen=false;
                }
                foreach(var node in root.GetComponentsInChildren<Transform>(true))node.gameObject.layer=30;
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,Output+"/"+name+".prefab");
                var movements=Resources.Load<WorldGenHumanMotionBank>("HumanKimodo/Library").Find(info.variant).Clips.Where(c=>new[]{"Idle","Walk","Run","Sprint"}.Contains(c.Role));
                var camp=Resources.Load<WorldGenHumanMotionBank>("HumanNpcMotion/Library").Find(info.variant).Clips;
                var grounding=movements.Concat(camp).Select(c=>new HumanGroundOffset{Role=c.Role,Metres=HumanMotionGrounding.Offset(prefab,c.Clip,adult.Prefab,info.eye_height/adult.EyeHeight)}).ToArray();
                result.Add(new HumanAgeModel{Variant=info.variant,Years=info.years,EyeHeight=info.eye_height,Prefab=prefab,Grounding=grounding});
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        if(result.Count!=18)throw new InvalidDataException("Expected eighteen native age bodies");
        string bankPath=Output+"/Library.asset";var bank=AssetDatabase.LoadAssetAtPath<WorldGenHumanAgeLibrary>(bankPath);
        if(bank==null){bank=ScriptableObject.CreateInstance<WorldGenHumanAgeLibrary>();AssetDatabase.CreateAsset(bank,bankPath);}bank.People=result.ToArray();EditorUtility.SetDirty(bank);AssetDatabase.SaveAssets();Debug.Log("NPC AGE MODELS READY "+result.Count);
    }
}
