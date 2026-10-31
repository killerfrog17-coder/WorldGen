using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    public static IEnumerator CheckFeedbackShaders(string output)
    {
        Directory.CreateDirectory(output);var report=new List<string>();
        Application.logMessageReceived+=(m,t,k)=>{if(k==LogType.Error||k==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),m+"\n"+t);Application.Quit(1);}};
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("FEEDBACK "+text);report.Add("PASS "+text);}
        yield return null;QualitySettings.vSyncCount=0;Application.targetFrameRate=30;
        Shader.SetGlobalFloat("_WorldGenFirstPersonHumanId",0);Shader.SetGlobalVector("_WorldGenSkyReflection",new Vector4(0,0,0,1));
        var library=Resources.Load<WorldGenHumanLibrary>("Humans/HumanLibrary");
        var fixture=new GameObject("Feedback fixture").AddComponent<LocalTerrainExplorer>();fixture.enabled=false;
        fixture.humanLibrary=library;
        foreach(var def in library.People)
        {
            var actor=fixture.CreateHuman(Array.IndexOf(library.People,def),20);actor.Root.transform.position=new Vector3(3,2,4);actor.Root.transform.localScale=Vector3.one*1.3f;actor.Graph.Evaluate(0);
            foreach(var renderer in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=renderer.sharedMesh;var rest=mesh.vertices;var weights=mesh.boneWeights;var binds=mesh.bindposes;var bones=renderer.bones;if(weights.Length!=rest.Length)continue;
                var posed=fixture.PosedHuman(renderer).Vertices;double worst=0;
                for(int i=0;i<rest.Length;i+=7)
                {
                    var w=weights[i];Vector3 expected=Vector3.zero;
                    void Add(int b,float weight){if(weight>0)expected+=bones[b].localToWorldMatrix.MultiplyPoint3x4(binds[b].MultiplyPoint3x4(rest[i]))*weight;}
                    Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);worst=Math.Max(worst,Vector3.Distance(expected,posed[i]));
                }
                Check(worst<.001,def.Variant+" posed combat skin matches independent bone positions at scaled size: "+renderer.name);
            }
            fixture.DestroyHuman(actor);
        }
        yield return null;
        var display=fixture.CreateHuman(0,20);foreach(var t in display.Root.GetComponentsInChildren<Transform>())t.gameObject.layer=29;
        var bounds=display.Root.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
        var camera=new GameObject("Feedback shader camera",typeof(Camera),typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<29;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.nearClipPlane=.01f;camera.farClipPlane=50;camera.orthographic=true;camera.orthographicSize=bounds.extents.y*1.15f;camera.aspect=1;
        camera.transform.position=bounds.center+Vector3.forward*4;camera.transform.LookAt(bounds.center);camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))light.enabled=false;
        var data=new Vector4[TerrainSceneVectors];data[2]=new Vector4(1e9f,1,0,0);var buffer=new GraphicsBuffer(GraphicsBuffer.Target.Constant,TerrainSceneVectors,16);
        var target=new RenderTexture(512,512,24);target.Create();camera.targetTexture=target;var oldActive=RenderTexture.active;
        double Capture(string name,float ambient,bool scene)
        {
            data[1]=new Vector4(ambient,ambient,ambient,1);buffer.SetData(data);Shader.SetGlobalConstantBuffer(TerrainSceneId,buffer,0,TerrainSceneVectors*16);
            foreach(var r in display.Root.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(var m in r.sharedMaterials)m.SetFloat("_VegetationSceneEnabled",scene?1:0);
            camera.Render();RenderTexture.active=target;var image=new Texture2D(512,512,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());double mean=image.GetPixels().Average(c=>(double)(c.r+c.g+c.b)/3);Destroy(image);return mean;
        }
        double day=Capture("human-day",.22f,true),night=Capture("human-night",.0002f,true),legacy=Capture("human-night-old-fallback",.0002f,false);
        Check(day>.005&&night<day*.15&&legacy>night*3,"Actual human shader follows day/night buffer and removes the constant night ambient: "+day+" / "+night+" / "+legacy);
        var audio=new GameObject("Feedback spatial source",typeof(AudioSource)).GetComponent<AudioSource>();audio.spatialBlend=1;ConfigureWildlifeProjection(audio,45);
        Check(audio.rolloffMode==AudioRolloffMode.Custom&&audio.maxDistance==45&&audio.GetCustomCurve(AudioSourceCurveType.CustomRolloff).Evaluate(.5f)==1,"Native source retains3D direction while manual physical distance gain is applied exactly once");
        Check(WildlifeAcoustics.Gain(20,1.5,45)<WildlifeAcoustics.Gain(2,1.5,45)*.11&&WildlifeAcoustics.Gain(46,1.5,45)==0,"Walking away strongly attenuates a fixed caller and stops outside its range");
        RenderTexture.active=oldActive;buffer.Dispose();target.Release();fixture.DestroyHuman(display);Destroy(camera.gameObject);Destroy(audio.gameObject);
        File.WriteAllLines(Path.Combine(output,"checks.txt"),report);Debug.Log("FEEDBACK SHADER PASS "+report.Count);Application.Quit(0);
    }
    public IEnumerator CheckFeedbackCombat(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("FEEDBACK COMBAT "+text);report.Add("PASS "+text);Debug.Log("FEEDBACK COMBAT PASS "+text);}
        PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();TestHumanView(0);paused=false;ExecuteAnimalSpawnCommand("/spawn deer");
        double end=Time.unscaledTimeAsDouble+35;while(!animals.Values.Any(a=>a.Manual&&!a.Dead&&a.Spawn.Profile.slug=="deer")&&Time.unscaledTimeAsDouble<end)yield return null;
        paused=true;var deer=animals.Values.First(a=>a.Manual&&!a.Dead&&a.Spawn.Profile.slug=="deer");deer.Fade=1;deer.X=motion.X+4;deer.Z=motion.Z;deer.Planet=frame.Position(deer.X,deer.Z);deer.GroundY=Ground(deer.X,deer.Z);deer.Speed=deer.TargetSpeed=0;
        var spear=PortableTechnology.Create(PossessionKind.StoneTippedSpear,Position);
        foreach(string role in new[]{"idle","walk","run","eating"})
        {
            var clip=deer.Model.Asset.Clips.FirstOrDefault(c=>c.role==role);if(clip==null)continue;
            deer.Clip=clip;deer.OldClip=null;deer.Time=.31;deer.BlendAge=1;deer.CollisionFrame=-1;UpdateAnimal(deer,0);
            var vertices=PosedAnimal(deer);var surface=AnimalSurface(deer);var candidates=new List<Vector3>();
            for(int t=0;t<surface.Triangles.Length;t+=9){int a=surface.Triangles[t],b=surface.Triangles[t+1],c=surface.Triangles[t+2];if(surface.Visible(a,deer.Parts))candidates.Add((vertices[a]+vertices[b]+vertices[c])/3);}
            foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right})
            {
                CombatHit found=null;
                foreach(var point in candidates){var ray=new Ray(point-direction*1.2f,direction);var hit=TraceLiving(ray,1.4f);if(hit?.Animal==deer&&TorsoContact(hit)){found=hit;break;}}
                Check(found!=null,"Real posed "+role+" torso can be hit from "+direction+" at spear distance");
                if(role=="idle"&&direction==Vector3.left)
                {
                    ApplyCombatHit(found,deer.MaximumHealth*.15,direction,3,spear);
                    Check(deer.Bleeding?.Rate>0&&!deer.Dead,"A nonlethal real spear contact creates a bleeding wound on the living deer");
                }
            }
        }
        Check(deer.Blood!=null&&deer.Blood.Capture().Any(v=>v>0),"The wound paints local blood onto the original animal skin");
        deer.Bleeding=new BleedingState();BleedingRules.Wound(deer.Bleeding,.25,.9,1,.2,true);deer.Bleeding.LastUpdatedDays=PlayerRules.SolarDays;
        DropBloodSign(deer.Spawn.Id,deer.Bleeding,deer.Planet,0,Time.unscaledTimeAsDouble+10);UpdateBloodSigns();
        Check(Wildlife.BloodSigns.Count>0&&bloodTraceMesh!=null&&bloodTraceMesh.vertexCount>0,"A bleeding animal leaves saved terrain-fitted tracking signs");
        CaptureWildlife();var saved=WorldJson.Read<PlaySession>(WorldJson.Write(PlayerRules));saved.Validate();
        Check(saved.Wildlife.BloodSigns.Count>0&&saved.Wildlife.Individuals.Single(p=>p.Id==deer.Spawn.Id).Bleeding.Rate>0,"Full player save retains actual bleeding and the blood trail");
        var id=deer.Spawn.Id;var spawn=deer.Spawn;var model=deer.Model;RemoveAnimal(id);AddAnimal(spawn,model);deer=animals[id];
        Check(deer.Bleeding?.Rate>0&&!deer.Dead,"Streaming the injured deer restores its wound without an instant death");
        AdvanceWound(deer.Bleeding,200,1);paused=false;UpdateCombatBleeding();paused=true;
        Check(deer.Dead&&deer.Ragdoll!=null,"Severe progressive blood loss eventually produces a ragdoll without another weapon hit");
        foreach(int variant in Enumerable.Range(0,humanLibrary.People.Length))
        {
            var human=CreateHuman(variant,100+variant);humanNpcs.Add(human);human.Root.transform.localPosition=new Vector3((float)(motion.X-originX)+8,(float)Ground(motion.X,motion.Z),(float)(motion.Z-originZ));human.Graph.Evaluate(0);
            foreach(var direction in new[]{Vector3.forward,Vector3.back})
            {
                CombatHit found=null;
                foreach(var renderer in human.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var surface=PosedHuman(renderer);
                    for(int t=0;t<surface.Triangles.Length;t+=15)
                    {
                        var point=(surface.Vertices[surface.Triangles[t]]+surface.Vertices[surface.Triangles[t+1]]+surface.Vertices[surface.Triangles[t+2]])/3;var hit=TraceLiving(new Ray(point-direction*1.1f,direction),1.3f);
                        if(hit?.Human==human&&TorsoContact(hit)){found=hit;break;}
                    }
                    if(found!=null)break;
                }
                Check(found!=null,"Native human "+variant+" torso hit works from "+direction+" at close range");
            }
            humanNpcs.Remove(human);DestroyHuman(human);
        }
        yield return CheckWildlifeAudioFlow(output,report);
    }
}
