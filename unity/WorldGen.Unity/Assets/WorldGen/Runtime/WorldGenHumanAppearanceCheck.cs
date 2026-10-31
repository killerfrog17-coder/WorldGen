using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Playables;

public sealed partial class LocalTerrainExplorer
{
    public static IEnumerator CheckHumanAppearance(string output,bool quit=true)
    {
        Directory.CreateDirectory(output);var report=new List<string>();
        if(quit)Application.logMessageReceived+=(m,t,k)=>{if(k==LogType.Error||k==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),m+"\n"+t);Application.Quit(1);}};
        void Check(bool ok,string label){if(!ok)throw new InvalidOperationException("APPEARANCE "+label);report.Add("PASS "+label);}
        yield return null;
        var library=Resources.Load<WorldGenHumanLibrary>("HumanAppearance/Library");var original=Resources.Load<WorldGenHumanLibrary>("Humans/HumanLibrary");
        Check(library!=null&&library.People.Length==6,"six revised character appearances loaded");
        var fixture=new GameObject("Appearance validation fixture").AddComponent<LocalTerrainExplorer>();fixture.enabled=false;fixture.humanLibrary=library;
        fixture.kimodoHumanLibrary=Resources.Load<WorldGenHumanMotionBank>("HumanKimodo/Library");
        var cam=new GameObject("Appearance camera",typeof(Camera),typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();cam.enabled=false;cam.cullingMask=1<<29;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.11f,.13f,.15f);cam.nearClipPlane=.01f;cam.farClipPlane=50;
        cam.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        var light=new GameObject("Appearance key",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.LookRotation(new Vector3(-.3f,-1,-.8f));
        var data=new Vector4[TerrainSceneVectors];data[1]=new Vector4(.22f,.22f,.22f,1);data[2]=new Vector4(1e9f,1,0,0);
        var buffer=new GraphicsBuffer(GraphicsBuffer.Target.Constant,TerrainSceneVectors,16);buffer.SetData(data);Shader.SetGlobalConstantBuffer(TerrainSceneId,buffer,0,TerrainSceneVectors*16);Shader.SetGlobalFloat("_WorldGenFirstPersonHumanId",0);Shader.SetGlobalVector("_WorldGenSkyReflection",new Vector4(0,0,0,1));
        var target=new RenderTexture(900,1000,24);target.Create();cam.targetTexture=target;var oldActive=RenderTexture.active;
        void Image(string name,Vector3 position,Vector3 focus,float orthographic)
        {
            cam.orthographic=orthographic>0;cam.orthographicSize=Mathf.Max(.1f,orthographic);cam.fieldOfView=65;cam.transform.position=position;cam.transform.LookAt(focus);cam.Render();RenderTexture.active=target;
            var image=new Texture2D(900,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,900,1000),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());DestroyImmediate(image);
        }
        bool oldKimodo=HumanAnimationOptions.Kimodo;
        try
        {
            for(int i=0;i<library.People.Length;i++)
            {
                var def=library.People[i];var prior=original.People.Single(p=>p.Variant==def.Variant);
                Check(def.Clips.SequenceEqual(prior.Clips)&&def.Roles.SequenceEqual(prior.Roles),def.Variant+" retains all original legacy clip references and identity");
                var actor=fixture.CreateHuman(i,1);actor.Root.transform.position=Vector3.zero;
                try
                {
                    foreach(var t in actor.Root.GetComponentsInChildren<Transform>())t.gameObject.layer=29;
                    Check(actor.Shared!=null&&actor.Shared.Clips.Count>=67,def.Variant+" retains complete Kimodo bank");
                    HumanAnimationOptions.Kimodo=true;
                    foreach(var pose in new[]{("idle",0f),("walk",1.5f),("run",3.4f)})
                    {
                        for(int frame=0;frame<24;frame++){fixture.AnimateHuman(actor,pose.Item2,false,false,1f/30);actor.Shared.Action("Empty",0,false,false,null,0,false,1f/30);actor.Graph.Evaluate(0);}
                        var bounds=new Bounds();bool first=true,finite=true;int count=0;
                        foreach(var renderer in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            var mesh=new Mesh();renderer.BakeMesh(mesh,true);
                            foreach(var v in mesh.vertices)
                            {
                                var p=renderer.transform.TransformPoint(v);finite&=float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);count++;
                                if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                            }
                            DestroyImmediate(mesh);
                        }
                        Check(finite&&count>10000,def.Variant+" "+pose.Item1+" finite evaluated skin vertices");
                        Check(bounds.size.y>1.1f&&bounds.size.y<2.5f&&bounds.size.x<2.2f,def.Variant+" "+pose.Item1+" correct posed metre scale");
                        Image(def.Variant+"-"+pose.Item1,new Vector3(0,.95f,3.4f),new Vector3(0,.88f,0),1.02f);
                    }
                    Image(def.Variant+"-legs",new Vector3(0,.47f,2),new Vector3(0,.47f,0),.48f);
                    Image(def.Variant+"-face",new Vector3(0,def.EyeHeight,1.5f),new Vector3(0,def.EyeHeight-.025f,0),.3f);
                    HumanAnimationOptions.Kimodo=false;for(int f=0;f<24;f++)fixture.AnimateHuman(actor,1.5f,false,false,1f/30);
                    Check(actor.Output.GetSourcePlayable().GetHandle().Equals(actor.Mixer.GetHandle()),def.Variant+" Legacy fallback still drives the revised body");
                    Image(def.Variant+"-legacy-walk",new Vector3(0,.95f,3.4f),new Vector3(0,.88f,0),1.02f);
                }
                finally
                {
                    if(Application.isPlaying)fixture.DestroyHuman(actor);
                    else
                    {
                        if(actor.Graph.IsValid())actor.Graph.Destroy();
                        if(actor.Shared!=null)foreach(var mask in (List<AvatarMask>)typeof(WorldGenSharedHumanMotion).GetField("masks",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(actor.Shared))DestroyImmediate(mask);
                        foreach(var material in actor.OwnedMaterials)DestroyImmediate(material);DestroyImmediate(actor.Root);
                    }
                }
                yield return null;
            }
        }
        finally{HumanAnimationOptions.Kimodo=oldKimodo;RenderTexture.active=oldActive;cam.targetTexture=null;target.Release();buffer.Dispose();DestroyImmediate(target);DestroyImmediate(cam.gameObject);DestroyImmediate(light.gameObject);DestroyImmediate(fixture.gameObject);}
        File.WriteAllLines(Path.Combine(output,"checks.txt"),report);Debug.Log("HUMAN APPEARANCE PASS "+report.Count);if(quit)Application.Quit(0);
    }
}
