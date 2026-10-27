using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Animations;
using UnityEngine.Playables;

public static class HumanMotionRevisionCheck
{
    static string Skin(SkinnedMeshRenderer renderer)
    {
        var mesh=renderer.sharedMesh;
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            foreach(var p in mesh.vertices){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
            foreach(var p in mesh.normals){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
            foreach(var p in mesh.uv){writer.Write(p.x);writer.Write(p.y);}
            foreach(var p in mesh.colors){writer.Write(p.r);writer.Write(p.g);writer.Write(p.b);writer.Write(p.a);}
            foreach(int i in mesh.triangles)writer.Write(i);
            using(var counts=mesh.GetBonesPerVertex())foreach(byte count in counts)writer.Write(count);
            using(var weights=mesh.GetAllBoneWeights())foreach(var weight in weights){writer.Write(renderer.bones[weight.boneIndex].name);writer.Write(weight.weight);}
            foreach(int i in Enumerable.Range(0,mesh.bindposes.Length).OrderBy(i=>renderer.bones[i].name,StringComparer.Ordinal))
            {writer.Write(renderer.bones[i].name);for(int k=0;k<16;k++)writer.Write(mesh.bindposes[i][k]);}
            writer.Flush();using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","");
        }
    }
    static Dictionary<string,string> Skins(WorldGenHumanLibrary library)
        =>library.People.SelectMany(p=>p.Prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r=>(Key:p.Variant+"/"+r.name,Hash:Skin(r)))).ToDictionary(p=>p.Key,p=>p.Hash);
    static Dictionary<string,float[]> Channels(WorldGenHumanLibrary library)
    {
        var result=new Dictionary<string,float[]>();
        foreach(var p in library.People)foreach(var r in p.Prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            string key=p.Variant+"/"+r.name+"/";var m=r.sharedMesh;
            result[key+"positions"]=m.vertices.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray();
            result[key+"normals"]=m.normals.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray();
            result[key+"UV"]=m.uv.SelectMany(v=>new[]{v.x,v.y}).ToArray();
            result[key+"colours"]=m.colors.SelectMany(v=>new[]{v.r,v.g,v.b,v.a}).ToArray();
            result[key+"triangles"]=m.triangles.Select(v=>(float)v).ToArray();
            using(var c=m.GetBonesPerVertex())result[key+"weight-counts"]=c.Select(v=>(float)v).ToArray();
            using(var w=m.GetAllBoneWeights())result[key+"weights"]=w.SelectMany(v=>new[]{(float)Array.IndexOf(r.bones.Select(b=>b.name).OrderBy(n=>n,StringComparer.Ordinal).ToArray(),r.bones[v.boneIndex].name),v.weight}).ToArray();
            result[key+"bind"]=Enumerable.Range(0,m.bindposes.Length).OrderBy(i=>r.bones[i].name,StringComparer.Ordinal).SelectMany(i=>Enumerable.Range(0,16).Select(k=>m.bindposes[i][k])).ToArray();
        }
        return result;
    }
    public static void BuildAndReview()
    {
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../..")),output=Path.Combine(repo,"artifacts/human-running-runtime/review");Directory.CreateDirectory(output);
        bool reviewOnly=Environment.GetCommandLineArgs().Contains("--worldgen-review-only");
        if(!reviewOnly)foreach(string file in Directory.GetFiles(Path.Combine(repo,"artifacts/human-running-runtime/prior-exports"),"*.fbx",SearchOption.AllDirectories))
        {string destination="Assets/WorldGen/GeneratedHumans/"+Path.GetFileName(Path.GetDirectoryName(file))+"/"+Path.GetFileName(file);File.Copy(file,destination,true);AssetDatabase.ImportAsset(destination,ImportAssetOptions.ForceUpdate);}
        var before=Skins(Resources.Load<WorldGenHumanLibrary>("Humans/HumanLibrary"));var beforeChannels=Channels(Resources.Load<WorldGenHumanLibrary>("Humans/HumanLibrary"));
        if(!reviewOnly)HumanRuntimeBuilder.Build();var library=Resources.Load<WorldGenHumanLibrary>("Humans/HumanLibrary");var after=Skins(library);var afterChannels=Channels(library);
        File.WriteAllLines(Path.Combine(output,"skin-channel-differences.txt"),beforeChannels.Select(row=>{var next=afterChannels[row.Key];return row.Key+" lengths "+row.Value.Length+"/"+next.Length+" max "+(row.Value.Length==next.Length?row.Value.Zip(next,(a,b)=>Math.Abs(a-b)).DefaultIfEmpty(0).Max():-1); }));
        var report=new List<string>();void Check(bool ok,string text){if(!ok)throw new InvalidDataException(text);report.Add("PASS "+text);}
        Check(beforeChannels.Count==afterChannels.Count,"same skin channels and renderer count");
        foreach(var row in beforeChannels)
        {float tolerance=row.Key.EndsWith("/bind",StringComparison.Ordinal)?1e-6f:0;var next=afterChannels[row.Key];Check(row.Value.Length==next.Length&&row.Value.Zip(next,(a,b)=>Math.Abs(a-b)).All(d=>d<=tolerance),"unchanged skin channel (FBX bind rounding <=1e-6 only): "+row.Key);}
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
        var lamp=new GameObject("Review sunlight").AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=2;lamp.shadows=LightShadows.Soft;lamp.transform.rotation=Quaternion.Euler(45,-35,0);RenderSettings.sun=lamp;
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.localScale=Vector3.one*4;
        var floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMaterial.SetColor("_BaseColor",new Color(.28f,.32f,.36f));floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
        var camera=new GameObject("Owning review camera").AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.18f);camera.fieldOfView=34;camera.allowHDR=false;camera.allowMSAA=false;
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        camera.transform.position=new Vector3(3.5f,1.4f,0);camera.transform.LookAt(new Vector3(0,.85f,0));
        bool mask=false;
        void Bind(ScriptableRenderContext context,Camera current)
        {float id=mask&&current==camera?1:0;Shader.SetGlobalFloat("_WorldGenFirstPersonHumanId",id);var commands=CommandBufferPool.Get("Review body identity");commands.SetGlobalFloat("_WorldGenFirstPersonHumanId",id);context.ExecuteCommandBuffer(commands);CommandBufferPool.Release(commands);}
        RenderPipelineManager.beginCameraRendering+=Bind;
        Color32[] Capture(Camera target,string name)
        {
            var texture=new RenderTexture(640,640,24);var pixels=new Texture2D(640,640,TextureFormat.RGB24,false);var prior=RenderTexture.active;
            try{target.targetTexture=texture;target.Render();RenderTexture.active=texture;pixels.ReadPixels(new Rect(0,0,640,640),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());return pixels.GetPixels32();}
            finally{target.targetTexture=null;RenderTexture.active=prior;texture.Release();UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(pixels);}
        }
        int Difference(Color32[] a,Color32[] b,int threshold=3)=>a.Zip(b,(x,y)=>Math.Max(Math.Abs(x.r-y.r),Math.Max(Math.Abs(x.g-y.g),Math.Abs(x.b-y.b)))).Count(d=>d>threshold);
        try
        {
            foreach(var person in library.People)
            {
                Check(person.Roles.Length==8&&!person.Roles.Any(r=>r.StartsWith("KayKit",StringComparison.Ordinal)),"eight selected clips, rejected running options removed: "+person.Variant);
                var actor=UnityEngine.Object.Instantiate(person.Prefab);actor.transform.position=Vector3.zero;
                var graph=PlayableGraph.Create("Captured motion review");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var animationOutput=AnimationPlayableOutput.Create(graph,"Body",actor.GetComponent<Animator>());AnimationClipPlayable active=default;AnimationClip currentClip=null;
                void Sample(AnimationClip clip,float time)
                {
                    if(currentClip!=clip){if(active.IsValid())graph.DestroyPlayable(active);active=AnimationClipPlayable.Create(graph,clip);active.SetApplyFootIK(false);active.SetApplyPlayableIK(false);active.SetSpeed(0);animationOutput.SetSourcePlayable(active);currentClip=clip;graph.Play();}
                    active.SetTime(time);graph.Evaluate(0);
                    Debug.Log("POSE "+person.Variant+" "+clip.name+" "+time+" "+actor.GetComponentsInChildren<Transform>().First(t=>t.name=="foot_l").localRotation);
                }
                foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                {renderer.forceMatrixRecalculationPerRender=true;var block=new MaterialPropertyBlock();block.SetFloat("_HumanViewId",1);renderer.SetPropertyBlock(block);renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>{var copy=new Material(m);copy.SetFloat("_HumanViewId",1);return copy;}).ToArray();}
                foreach(string role in new[]{"Run","Sprint","Relaxed run"})
                {
                    int index=Array.IndexOf(person.Roles,role);var clip=person.Clips[index];
                    Check(clip.length>.4f&&clip.length<1.3f&&person.StrideMeters[index]>.5f,"captured cycle duration and measured stride: "+person.Variant+" "+role);
                    Color32[] priorPhase=null;
                    foreach(float phase in new[]{.15f,.4f}){Sample(clip,clip.length*phase);var rendered=Capture(camera,person.Variant+"-"+role.Replace(' ','-')+"-"+phase.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));if(priorPhase!=null)Check(Difference(priorPhase,rendered)>500,"actual pose changes across captured stride: "+person.Variant+" "+role);priorPhase=rendered;}
                }
                if(person.Variant=="A")
                {
                    Sample(person.Clips[Array.IndexOf(person.Roles,"Idle")],0);mask=false;var visible=Capture(camera,"body-visible");mask=true;var hidden=Capture(camera,"body-hidden-own-camera");
                    Check(Difference(visible,hidden)>1000,"own camera hides the full local body in actual pixels");
                    var renderers=actor.GetComponentsInChildren<SkinnedMeshRenderer>();foreach(var r in renderers)r.shadowCastingMode=ShadowCastingMode.ShadowsOnly;
                    mask=false;var reference=Capture(camera,"body-shadow-only-reference");Check(Difference(hidden,reference)<=4,"hidden body matches a shadow-only reference without invisible colour/depth occlusion");
                    foreach(var r in renderers)r.shadowCastingMode=ShadowCastingMode.Off;mask=true;var noShadow=Capture(camera,"body-no-shadow-reference");
                    Check(Difference(hidden,noShadow)>20,"hidden local body still casts its complete real shadow");
                    foreach(var r in renderers)r.shadowCastingMode=ShadowCastingMode.On;
                    var other=UnityEngine.Object.Instantiate(camera.gameObject).GetComponent<Camera>();var external=Capture(other,"body-visible-other-camera");
                    Check(Shader.GetGlobalFloat("_WorldGenFirstPersonHumanId")==0&&Difference(visible,external)<=4,"other camera sees the full same person");UnityEngine.Object.DestroyImmediate(other.gameObject);mask=false;
                }
                graph.Destroy();var owned=actor.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(r=>r.sharedMaterials).Distinct().ToArray();UnityEngine.Object.DestroyImmediate(actor);foreach(var material in owned)UnityEngine.Object.DestroyImmediate(material);
            }
            File.WriteAllLines(Path.Combine(output,"checks.txt"),report);Debug.Log("HUMAN MOTION REVISION PASS "+report.Count);
        }
        finally{RenderPipelineManager.beginCameraRendering-=Bind;Shader.SetGlobalFloat("_WorldGenFirstPersonHumanId",0);UnityEngine.Object.DestroyImmediate(floorMaterial);}
    }
}
