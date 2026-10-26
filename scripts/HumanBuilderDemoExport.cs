// Isolated UMA appearance audition. Copied into the downloaded demo project only.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UMA;
using UMA.CharacterSystem;

public static class HumanBuilderDemoExport
{
    static bool rigOnly;
    [Serializable] public class RigBone { public string name, parent, human; public Vector3 position; public Quaternion rotation; }
    [Serializable] public class SkinPart { public string name; public Vector3[] vertices; public string[] bones; public int[] counts,indices; public float[] weights; }
    [Serializable] public class RigSnapshot { public string variant; public RigBone[] bones; public SkinPart[] meshes; }
    [Serializable] public class MeshPart { public string name; public Vector3[] vertices; public Vector3[] normals; public Vector2[] uv; public Submesh[] submeshes; public MaterialInfo[] materials; }
    [Serializable] public class Submesh { public int[] triangles; }
    [Serializable] public class MaterialInfo { public string name; public string shader; public Color color; public Map[] maps; }
    [Serializable] public class Map { public string property; public string path; }
    [Serializable] public class Character { public string name; public string race; public MeshPart[] meshes; public string[] dnaNames; public float[] dnaValues; public string recipe; }
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../human-builder-demo/uma"));

    public static void Export()
    {
        Directory.CreateDirectory(Root);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        UMAAssetIndexer.Instance.AddEverything(true);
        UMAAssetIndexer.Instance.RebuildRaceRecipes();
        var races = AssetDatabase.FindAssets("t:RaceData").Select(AssetDatabase.GUIDToAssetPath).ToArray();
        File.WriteAllText(Path.Combine(Root,"available-races.json"), JsonUtility.ToJson(new StringList { values = races }, true));
        for(int i=0;i<6;i++) BuildVariant(i);
        Debug.Log("HUMAN_DEMO_UMA_ALL_READY " + Root);
    }

    public static void ExportRigs()
    {
        rigOnly=true;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        UMAAssetIndexer.Instance.AddEverything(true);
        UMAAssetIndexer.Instance.RebuildRaceRecipes();
        for(int i=0;i<6;i++)BuildVariant(i);
        Debug.Log("HUMAN_ANIMATION_UMA_SKINS_READY");
    }

    static void BuildVariant(int variant)
    {
        var go = new GameObject("UMA_Appearance_Audition_"+variant);
        var avatar = go.AddComponent<DynamicCharacterAvatar>();
        avatar.predefinedDNA = new UMAPredefinedDNA();
        avatar.preloadWardrobeRecipes.loadDefaultRecipes = false;
        avatar.preloadWardrobeRecipes.recipes.Clear();
        bool female=variant==0||variant==2||variant==4;
        var race = AssetDatabase.LoadAssetAtPath<RaceData>("Assets/UMA/UMA3/Races/"+(female?"HumanFemale30":"HumanMale30")+".asset");
        avatar.activeRace.name = race.raceName;
        avatar.activeRace.data = race;
        avatar.InitializeAvatar();
        if (avatar.activeRace.racedata == null) throw new Exception("UMA HumanMale30 race unavailable");
        avatar.BuildNow();
        var dna = avatar.GetDNA();
        File.WriteAllText(Path.Combine(Root,"available-dna.json"), JsonUtility.ToJson(new StringList { values = dna.Keys.ToArray() }, true));
        File.WriteAllText(Path.Combine(Root,"available-recipes.json"), JsonUtility.ToJson(new StringList { values = AssetDatabase.FindAssets("t:UMATextRecipe").Select(AssetDatabase.GUIDToAssetPath).ToArray() }, true));
        var values = new [] {
            new Dictionary<string,float>{{"noseWidth",.67f},{"noseFlatten",.65f},{"lipsSize",.65f},{"mouthSize",.55f},{"cheekWidth",.55f},{"height",.52f},{"upperMuscle",.57f},{"lowerMuscle",.55f},{"jawsSize",.45f}},
            new Dictionary<string,float>{{"noseWidth",.49f},{"noseFlatten",.61f},{"nosePronounced",.43f},{"eyeSize",.43f},{"eyeSpacing",.53f},{"eyeRotation",.56f},{"cheekWidth",.59f},{"cheekPronounced",.58f},{"headWidth",.56f},{"height",.64f},{"upperWeight",.43f},{"lowerWeight",.44f}},
            new Dictionary<string,float>{{"noseWidth",.43f},{"nosePronounced",.53f},{"headWidth",.43f},{"chinSize",.45f},{"eyeSize",.52f},{"lipsSize",.47f},{"height",.65f},{"upperWeight",.43f},{"lowerWeight",.44f}},
            new Dictionary<string,float>{{"nosePronounced",.65f},{"noseSize",.57f},{"noseWidth",.52f},{"lipsSize",.54f},{"eyeSize",.46f},{"cheekWidth",.49f},{"shoulderWidth",.60f},{"height",.51f},{"upperMuscle",.64f},{"headWidth",.51f}},
            new Dictionary<string,float>{{"noseFlatten",.61f},{"noseWidth",.54f},{"eyeSize",.42f},{"eyeRotation",.57f},{"cheekWidth",.62f},{"cheekPronounced",.62f},{"headWidth",.57f},{"height",.39f},{"lipsSize",.55f}},
            new Dictionary<string,float>{{"jawsSize",.65f},{"mandibleSize",.61f},{"headWidth",.61f},{"noseSize",.57f},{"neckThickness",.61f},{"height",.40f},{"upperWeight",.64f},{"lowerWeight",.58f},{"belly",.62f},{"waist",.57f}}
        };
        foreach(var item in values[variant])
        {
            if(!dna.ContainsKey(item.Key))throw new Exception("UMA DNA missing: "+item.Key);
            dna[item.Key].Set(item.Value);
        }
        var skins = new[]{new Color(.34f,.17f,.095f),new Color(.88f,.72f,.56f),new Color(1f,.92f,.84f),new Color(.54f,.32f,.18f),new Color(.33f,.18f,.105f),new Color(.78f,.65f,.46f)};
        var hairs = new[]{new Color(.025f,.018f,.012f),new Color(.025f,.019f,.014f),new Color(.28f,.075f,.025f),new Color(.035f,.023f,.015f),new Color(.035f,.025f,.018f),new Color(.48f,.46f,.43f)};
        var eyes = new[]{new Color(.13f,.065f,.025f),new Color(.10f,.05f,.025f),new Color(.32f,.53f,.23f),new Color(.19f,.09f,.025f),new Color(.29f,.18f,.055f),new Color(.40f,.52f,.59f)};
        avatar.SetColor("Skin",skins[variant],UpdateTexture:false);
        avatar.SetColor("Hair",hairs[variant],UpdateTexture:false);
        avatar.SetColor("Eyes",eyes[variant],UpdateTexture:false);
        string[] hairRecipes={"Hair_Bun_Recipe","Hair_VeryShort_Recipe","Hair_Bun_Recipe","Hair_LeftPart_Recipe","Hair_Bob_Recipe","Hair_VeryShort_Recipe"};
        AddWardrobe(avatar,"Assets/UMA/UMA3/Wearables/HairAndBeards/"+hairRecipes[variant]+".asset");
        AddWardrobe(avatar,"Assets/UMA/UMA3/Wearables/eyebrows/HDEyeBrows_Wardrobe.asset");
        AddWardrobe(avatar,"Assets/UMA/UMA3/Wearables/Wardrobe/"+(female?"tanktop_yellow_Recipe":"male_tshirt_white_Recipe")+".asset");
        AddWardrobe(avatar,"Assets/UMA/UMA3/Wearables/Wardrobe/"+(female?"shorts_turquoise_Recipe":"male_shorts_black_cotton_Recipe")+".asset");
        if(variant==5)AddWardrobe(avatar,"Assets/UMA/UMA3/Textures/AlternateFaces/Senior_U_Wardrobe.asset");
        avatar.predefinedDNA=new UMAPredefinedDNA();
        foreach(var item in values[variant])avatar.predefinedDNA.AddDNA(item.Key,item.Value);
        avatar.umaGenerator.useAsyncConversion=false;
        avatar.BuildNow();
        var animator=avatar.GetComponent<Animator>();
        if(animator!=null && animator.avatar!=null && animator.avatar.isHuman)
        {
            foreach(var pair in new[]{new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm},new[]{HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm}})
            {
                var bone=animator.GetBoneTransform(pair[0]);var elbow=animator.GetBoneTransform(pair[1]);if(bone==null||elbow==null)continue;
                float direction=bone.position.x>0?1:-1;
                var desired=new Vector3(direction*.375f,-.927f,0);
                bone.rotation=Quaternion.FromToRotation(elbow.position-bone.position,desired)*bone.rotation;
            }
        }
        foreach(var renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(var material in renderer.sharedMaterials)
        {
            if(material!=null&&material.name.IndexOf("Hair",StringComparison.OrdinalIgnoreCase)>=0)
            {
                foreach(string property in new[]{"_BaseColor","_RootColor","_Tip_Color"})if(material.HasProperty(property))material.SetColor(property,hairs[variant]);
            }
        }
        if(rigOnly)ExportSkin(avatar, ((char)('A'+variant)).ToString());
        else ExportCharacter(avatar, ((char)('A'+variant)).ToString());
        UnityEngine.Object.DestroyImmediate(go);
    }

    static void AddWardrobe(DynamicCharacterAvatar avatar,string path)
    {
        var recipe=AssetDatabase.LoadAssetAtPath<UMATextRecipe>(path);
        if(recipe==null)throw new Exception("Required UMA wardrobe missing: "+path);
        avatar.preloadWardrobeRecipes.recipes.Add(new DynamicCharacterAvatar.WardrobeRecipeListItem(recipe));
    }
    [Serializable] public class StringList { public string[] values; }

    static void ExportSkin(DynamicCharacterAvatar avatar,string variant)
    {
        var transforms=new HashSet<Transform>();var parts=new List<SkinPart>();
        foreach(var renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if(renderer.sharedMesh==null)continue;
            foreach(var bone in renderer.bones)
                for(var t=bone;t!=null&&t!=avatar.transform;t=t.parent)transforms.Add(t);
            var baked=new Mesh();renderer.BakeMesh(baked);
            var matrix=avatar.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
            var counts=renderer.sharedMesh.GetBonesPerVertex();var weights=renderer.sharedMesh.GetAllBoneWeights();
            parts.Add(new SkinPart{name=renderer.name,vertices=baked.vertices.Select(matrix.MultiplyPoint3x4).ToArray(),
                bones=renderer.bones.Select(b=>b.name).ToArray(),counts=counts.Select(v=>(int)v).ToArray(),
                indices=weights.Select(v=>v.boneIndex).ToArray(),weights=weights.Select(v=>v.weight).ToArray()});
            counts.Dispose();weights.Dispose();UnityEngine.Object.DestroyImmediate(baked);
        }
        var animator=avatar.GetComponent<Animator>();var human=new Dictionary<Transform,string>();
        if(animator!=null&&animator.avatar!=null&&animator.avatar.isHuman)
            foreach(HumanBodyBones id in Enum.GetValues(typeof(HumanBodyBones)))
            {if(id==HumanBodyBones.LastBone)continue;var t=animator.GetBoneTransform(id);if(t!=null)human[t]=id.ToString();}
        var bones=transforms.Select(t=>new RigBone{name=t.name,parent=t.parent!=null&&transforms.Contains(t.parent)?t.parent.name:null,
            human=human.ContainsKey(t)?human[t]:null,position=avatar.transform.InverseTransformPoint(t.position),
            rotation=Quaternion.Inverse(avatar.transform.rotation)*t.rotation}).ToArray();
        string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../human-animation-preview/uma-skin"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,variant+".json"),JsonUtility.ToJson(new RigSnapshot{variant=variant,bones=bones,meshes=parts.ToArray()}));
        Debug.Log("HUMAN_ANIMATION_SKIN_EXPORTED "+variant+" bones="+bones.Length);
    }

    static void ExportCharacter(DynamicCharacterAvatar avatar, string name)
    {
        string directory = Path.Combine(Root, name); Directory.CreateDirectory(directory);
        var parts = new List<MeshPart>();
        foreach (var renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (renderer.sharedMesh == null) continue;
            var mesh = new Mesh(); renderer.BakeMesh(mesh);
            var matrix = avatar.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var vertices = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
            var normals = mesh.normals.Select(v=>matrix.MultiplyVector(v).normalized).ToArray();
            var materials = renderer.sharedMaterials.Select((material, index)=>ExportMaterial(material,directory,parts.Count,index)).ToArray();
            parts.Add(new MeshPart { name=renderer.name, vertices=vertices, normals=normals, uv=mesh.uv, submeshes=Enumerable.Range(0,mesh.subMeshCount).Select(i=>new Submesh {triangles=mesh.GetTriangles(i)}).ToArray(),materials=materials });
            UnityEngine.Object.DestroyImmediate(mesh);
        }
        if (parts.Count==0||parts.Sum(p=>p.vertices.Length)==0) throw new Exception("UMA generated no mesh vertices");
        var dna = avatar.GetDNA();
        var output = new Character { name=name,race=avatar.activeRace.name,meshes=parts.ToArray(),dnaNames=dna.Keys.ToArray(),dnaValues=dna.Values.Select(d=>d.Value).ToArray(),recipe=avatar.GetAvatarDefinitionString(false,false) };
        File.WriteAllText(Path.Combine(directory,"character.json"),JsonUtility.ToJson(output));
        Debug.Log("HUMAN_DEMO_EXPORTED "+name+" vertices="+parts.Sum(p=>p.vertices.Length));
    }

    static MaterialInfo ExportMaterial(Material material, string directory, int part, int index)
    {
        if(material==null)return new MaterialInfo{name="Missing material",color=Color.magenta,maps=new Map[0]};
        var maps=new List<Map>();
        foreach(string property in material.GetTexturePropertyNames())
        {
            if(!new[]{"_BaseMap","_MainTex","_BumpMap","_NormalMap","_MaskMap","_MetallicGlossMap"}.Contains(property))continue;
            Texture texture=material.GetTexture(property);if(texture==null || texture.dimension != UnityEngine.Rendering.TextureDimension.Tex2D)continue;
            var rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default);
            Graphics.Blit(texture,rt);
            var old=RenderTexture.active;RenderTexture.active=rt;
            var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();RenderTexture.active=old;
            byte[] bytes=image.EncodeToPNG();
            string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            string textureDirectory=Path.Combine(Root,"textures");Directory.CreateDirectory(textureDirectory);
            string output=Path.Combine(textureDirectory,hash+".png");if(!File.Exists(output)||new FileInfo(output).Length!=bytes.Length)File.WriteAllBytes(output,bytes);
            UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt);
            maps.Add(new Map{property=property,path=Path.GetRelativePath(directory,output)});
        }
        return new MaterialInfo{name=material.name,shader=material.shader!=null?material.shader.name:"missing",color=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):material.HasProperty("_Color")?material.GetColor("_Color"):Color.white,maps=maps.ToArray()};
    }
}
