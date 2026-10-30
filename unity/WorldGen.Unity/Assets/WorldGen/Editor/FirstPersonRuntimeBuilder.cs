using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class FirstPersonRuntimeBuilder
{
    [Serializable] sealed class Manifest {public Motion[] clips;public string generator_sha256,fbx_sha256,grip_sha256,base_sha256;}
    [Serializable] sealed class Motion {public string name;public bool loop;}
    const string Folder="Assets/WorldGen/Resources/FirstPerson";
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static string TextHash(string file)=>Hash(Encoding.UTF8.GetBytes(File.ReadAllText(file).Replace("\r\n","\n")));
    public static void ValidateSource()
    {
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../..")),source=Path.Combine(root,"assets/first-person");
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(source,"clips.json")));
        if(manifest.generator_sha256!=TextHash(Path.Combine(root,"scripts/author-first-person-rig.py"))||manifest.fbx_sha256!=Hash(File.ReadAllBytes(Path.Combine(source,"Arms.fbx")))||manifest.grip_sha256!=TextHash(Path.Combine(source,"spear-grip.json"))||manifest.base_sha256!=Hash(File.ReadAllBytes(Path.Combine(source,"FirstPersonBase.blend"))))
            throw new InvalidDataException("First-person export is stale or incomplete. Re-run Blender with --python-exit-code 1 and author-first-person-rig.py before importing.");
    }
    public static void Build()
    {
        ValidateSource();
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));string source=Path.Combine(root,"assets/first-person");
        Directory.CreateDirectory(Folder);
        foreach(string name in new[]{"Arms.fbx","clips.json","A.png","B.png","C.png","D.png","E.png","F.png"})
        {
            string input=Path.Combine(source,name),output=Path.Combine(Folder,name);
            if(!File.Exists(output)||!File.ReadAllBytes(input).SequenceEqual(File.ReadAllBytes(output)))File.Copy(input,output,true);
        }
        AssetDatabase.Refresh();string model=Folder+"/Arms.fbx";
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(source,"clips.json")));
        var importer=(ModelImporter)AssetImporter.GetAtPath(model);
        importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation=true;importer.isReadable=true;importer.importBlendShapes=false;importer.optimizeGameObjects=false;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.animationCompression=ModelImporterAnimationCompression.Off;
        var clips=importer.defaultClipAnimations;
        foreach(var clip in clips)
        {
            string name=clip.name.Substring(clip.name.LastIndexOf('|')+1);var motion=manifest.clips.SingleOrDefault(c=>c.name==name);
            if(motion==null)throw new InvalidDataException("Unexpected first-person take: "+clip.name);
            clip.name=name;clip.loopTime=motion.loop;clip.loopPose=motion.loop;clip.keepOriginalOrientation=true;
            clip.keepOriginalPositionXZ=true;clip.keepOriginalPositionY=true;clip.lockRootRotation=true;clip.lockRootPositionXZ=true;clip.lockRootHeightY=true;
        }
        if(clips.Length!=manifest.clips.Length)throw new InvalidDataException("First-person native takes missing.");
        importer.clipAnimations=clips;importer.SaveAndReimport();
        foreach(char variant in "ABCDEF")
        {
            var texture=(TextureImporter)AssetImporter.GetAtPath(Folder+"/"+variant+".png");texture.sRGBTexture=true;texture.mipmapEnabled=true;
            texture.alphaSource=TextureImporterAlphaSource.None;texture.wrapMode=TextureWrapMode.Clamp;texture.maxTextureSize=2048;
            texture.textureCompression=TextureImporterCompression.CompressedHQ;texture.SaveAndReimport();
        }
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(model);var renderer=prefab.GetComponentInChildren<SkinnedMeshRenderer>();
        if(renderer==null||renderer.sharedMesh.vertexCount<10000||prefab.GetComponentInChildren<Animator>()==null)throw new InvalidDataException("First-person native mesh/Animator import failed.");
        string[] required={"Grip.R","Grip.L","SpearGrip.R","SpearGrip.L","Nock.R"};var bones=prefab.GetComponentsInChildren<Transform>().Select(t=>t.name).ToArray();
        if(required.Except(bones).Any())throw new InvalidDataException("First-person sockets missing.");
        var actual=AssetDatabase.LoadAllAssetsAtPath(model).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
        if(manifest.clips.Select(c=>c.name).Except(actual.Select(c=>c.name)).Any())throw new InvalidDataException("First-person action import failed.");
        AssetDatabase.SaveAssets();Debug.Log("AUTHORED FIRST PERSON READY: "+actual.Length+" native clips, "+renderer.sharedMesh.vertexCount+" skin vertices; six pigments.");
    }
}
