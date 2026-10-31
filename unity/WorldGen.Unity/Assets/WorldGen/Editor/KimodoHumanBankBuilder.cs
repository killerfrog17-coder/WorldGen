using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Imports animation-only derivatives. Does not rebuild the original people.</summary>
public static class KimodoHumanBankBuilder
{
    const string Input="Assets/WorldGen/GeneratedHumanKimodo",Output="Assets/WorldGen/Resources/HumanKimodo";
    [Serializable] sealed class Manifest {public Person[] people;public string source_sha256,model;}
    [Serializable] sealed class Person {public string variant,file,fbx_sha256;public float eye_forward;public Clip[] clips;}
    [Serializable] sealed class Clip {public string role,action,source;public bool loop;public float contact,release,travel_m,duration;}
    [Serializable] sealed class LegacyManifest {public LegacyClip[] clips;}
    [Serializable] sealed class LegacyClip {public string name;}
    static string Repo=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
    public static void Probe()=>Import(false);
    public static void Build()=>Import(true);
    public static void ProbeAnatomyAxes()
    {
        var library=Resources.Load<WorldGenHumanLibrary>("Humans/HumanLibrary");var lines=new List<string>();
        foreach(var person in library.People)
        {
            var obj=UnityEngine.Object.Instantiate(person.Prefab);var idle=Array.IndexOf(person.Roles,"Idle");person.Clips[idle].SampleAnimation(obj,0);
            var bones=obj.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            foreach(string name in new[]{"pelvis","head","upperarm_r","upperarm_l","hand_r","hand_l"})lines.Add(person.Variant+" "+name+" "+obj.transform.InverseTransformPoint(bones[name].position).ToString("F5"));
            var measure=typeof(LocalTerrainExplorer).Assembly.GetType("WorldGenPropClearance").GetMethod("Measure");
            var shape=measure.Invoke(null,new object[]{obj.transform,person.EyeHeight,.15f});
            lines.Add(person.Variant+" SHAPE "+string.Join(" ",shape.GetType().GetFields().Select(f=>f.Name+"="+f.GetValue(shape))));
            foreach(var renderer in obj.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=new Mesh();renderer.BakeMesh(mesh,true);var vertices=mesh.vertices;var center=Vector3.zero;foreach(var v in vertices)center+=obj.transform.InverseTransformPoint(renderer.transform.TransformPoint(v));
                lines.Add(person.Variant+" MESH "+renderer.name+" "+renderer.sharedMesh.vertexCount+" weights="+renderer.sharedMesh.boneWeights.Length+" mean="+(center/vertices.Length).ToString("F5"));UnityEngine.Object.DestroyImmediate(mesh);
            }
            UnityEngine.Object.DestroyImmediate(obj);
        }
        File.WriteAllLines(Path.Combine(Repo,"artifacts/human-natural-20261031/native-anatomy-axes.txt"),lines);Debug.Log(string.Join(" / ",lines));
    }
    static void Import(bool complete)
    {
        var source=Path.Combine(Repo,"assets/human-motion-kimodo");var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(source,"bank.json")));
        Directory.CreateDirectory(Input);Directory.CreateDirectory(Output);
        foreach(var person in manifest.people)File.Copy(Path.Combine(source,person.file),Path.Combine(Input,person.file),true);
        AssetDatabase.Refresh();var library=Resources.Load<WorldGenHumanLibrary>("Humans/HumanLibrary");
        var result=new List<KimodoPersonMotion>();var report=new List<string>();
        foreach(var person in manifest.people)
        {
            string path=Input+"/"+person.file;var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.animationCompression=ModelImporterAnimationCompression.Off;importer.optimizeGameObjects=false;
            var takes=new List<ModelImporterClipAnimation>();
            foreach(var take in importer.defaultClipAnimations)
            {
                string role=take.name.Substring(take.name.LastIndexOf('|')+1).Trim();var info=person.clips.FirstOrDefault(c=>c.role==role);if(info==null)continue;
                take.name=role;take.loopTime=take.loopPose=info.loop;
                take.lockRootRotation=true;take.keepOriginalOrientation=true;take.lockRootPositionXZ=true;take.keepOriginalPositionXZ=true;take.keepOriginalPositionY=true;
                takes.Add(take);
            }
            if(takes.Count!=person.clips.Length)throw new InvalidDataException("Kimodo takes missing "+person.variant+": "+takes.Count+"/"+person.clips.Length+" "+string.Join(",",importer.defaultClipAnimations.Select(c=>c.name)));
            importer.clipAnimations=takes.ToArray();importer.SaveAndReimport();
            var clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__",StringComparison.Ordinal)).ToDictionary(c=>c.name,StringComparer.Ordinal);
            var original=library.People.Single(p=>p.Variant==person.variant);var root=original.Prefab.transform;
            var paths=new HashSet<string>(root.GetComponentsInChildren<Transform>(true).Select(t=>AnimationUtility.CalculateTransformPath(t,root)),StringComparer.Ordinal);
            foreach(var clip in clips.Values)
            {
                var bindings=AnimationUtility.GetCurveBindings(clip).Where(b=>b.type==typeof(Transform)).ToArray();
                var skeletal=bindings.Where(b=>b.path.IndexOf("pelvis",StringComparison.Ordinal)>=0).ToArray();
                if(skeletal.Length<53*4||skeletal.Any(b=>!paths.Contains(b.path)))
                    throw new InvalidDataException("Kimodo binding mismatch "+person.variant+" "+clip.name+": "+string.Join(",",skeletal.Select(b=>b.path).Distinct().Take(8)));
            }
            var motions=person.clips.Select(c=>new KimodoHumanClip{Role=c.role,Clip=clips[c.role],Loop=c.loop,Contact=c.contact,Release=c.release,TravelMeters=c.travel_m}).ToArray();
            if(complete)
            {
                var legacy=JsonUtility.FromJson<LegacyManifest>(File.ReadAllText("Assets/WorldGen/Resources/FirstPerson/clips.json"));
                var expected=new[]{"Idle","Walk","Run","Sprint","Crouch","CrouchIdle","Jump","Swim","WalkBack","StrafeLeft","StrafeRight"}.Concat(legacy.clips.Select(c=>c.name)).ToArray();
                var missing=expected.Except(motions.Select(c=>c.Role)).ToArray();
                if(missing.Length>0)throw new InvalidDataException("Initial Kimodo bank is incomplete: "+person.variant+" "+string.Join(",",missing));
            }
            result.Add(new KimodoPersonMotion{Variant=person.variant,EyeForward=person.eye_forward,Clips=motions});
            report.Add(person.variant+" "+motions.Length+" native clips; original skeleton paths matched; no character model replacement");
        }
        if(complete)
        {
            if(result.Count!=6)throw new InvalidDataException("Six native human variants required");
            string path=Output+"/Library.asset";var bank=AssetDatabase.LoadAssetAtPath<WorldGenHumanMotionBank>(path);
            if(bank==null){bank=ScriptableObject.CreateInstance<WorldGenHumanMotionBank>();AssetDatabase.CreateAsset(bank,path);}
            bank.People=result.ToArray();EditorUtility.SetDirty(bank);AssetDatabase.SaveAssets();
        }
        File.WriteAllLines(Path.Combine(Repo,"artifacts/kimodo-human-bank/unity-"+(complete?"import":"probe")+".txt"),report);
        Debug.Log("KIMODO HUMAN BANK "+(complete?"READY ":"PROBE ")+string.Join(" / ",report));
    }
}
