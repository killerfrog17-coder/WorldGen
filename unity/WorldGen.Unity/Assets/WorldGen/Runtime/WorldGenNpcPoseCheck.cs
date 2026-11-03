using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed partial class LocalTerrainExplorer
{
    public static IEnumerator CheckNpcPoses(string output)
    {
        Directory.CreateDirectory(output);RecordNpcCheckPackage(output);var checks=new List<string>();
        Application.logMessageReceived+=(m,t,k)=>{if(k==LogType.Error||k==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),m+"\n"+t);Application.Quit(1);}};
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("NPC POSE "+message);checks.Add("PASS "+message);}
        yield return null;Application.targetFrameRate=30;
        var owner=new GameObject("NPC pose fixture").AddComponent<LocalTerrainExplorer>();owner.enabled=false;
        owner.humanLibrary=Resources.Load<WorldGenHumanLibrary>("HumanAppearance/Library");owner.kimodoHumanLibrary=Resources.Load<WorldGenHumanMotionBank>("HumanKimodo/Library");
        Check(Resources.Load<WorldGenHumanMotionBank>("HumanNpcMotion/Library")?.People.Length==6,"All six fitted NPC motion sets are packaged");
        var camera=new GameObject("NPC pose camera",typeof(Camera),typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<29;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.15f,.18f);camera.nearClipPlane=.01f;camera.farClipPlane=20;
        camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        var light=new GameObject("NPC pose key",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.transform.rotation=Quaternion.LookRotation(new Vector3(-.3f,-1,-.8f));
        var data=new Vector4[TerrainSceneVectors];data[1]=new Vector4(.3f,.3f,.3f,1);data[2]=new Vector4(1e9f,1,0,0);
        var buffer=new GraphicsBuffer(GraphicsBuffer.Target.Constant,TerrainSceneVectors,16);buffer.SetData(data);Shader.SetGlobalConstantBuffer(TerrainSceneId,buffer,0,TerrainSceneVectors*16);Shader.SetGlobalFloat("_WorldGenFirstPersonHumanId",0);
        var target=new RenderTexture(1000,800,24);target.Create();camera.targetTexture=target;var prior=RenderTexture.active;
        var ages=Resources.Load<WorldGenHumanAgeLibrary>("HumanAges/Library");Check(ages!=null&&ages.People.Length==18,"Eighteen fitted child/teen bodies are packaged");
        var fixtures=Enumerable.Range(0,6).Select(i=>(variant:i,age:(HumanAgeModel)null)).Concat(ages.People.Select(a=>(variant:Array.FindIndex(owner.humanLibrary.People,p=>p.Variant==a.Variant),age:a)));
        foreach(var sample in fixtures)
        {
            int variant=sample.variant;var actor=owner.CreateHuman(variant,100+variant,true,sample.age);string label=actor.Definition.Variant+(sample.age==null?"":"-age-"+sample.age.Years);foreach(var node in actor.Root.GetComponentsInChildren<Transform>())node.gameObject.layer=29;
            foreach(string role in new[]{"SleepSide","SitGround","Social","WorkKneeling","Crawl"})
            {
                Check(actor.Shared.Clips.ContainsKey(role),actor.Definition.Variant+" has "+role);
                for(int i=0;i<45;i++){owner.AnimateHuman(actor,0,false,false,1f/30);actor.Shared.Routine(role,i/30d,1f/30);actor.Graph.Evaluate(0);ApplyHumanAgePose(actor);}
                float lift=sample.age!=null?sample.age.Offset(role):actor.Shared.Clips[role].GroundOffsetM;actor.Root.transform.localPosition=Vector3.up*lift;
                // Native GPU skinning may reuse a renderer's first uploaded pose
                // within one frame. Let the frame publish the new skin before
                // capturing a different action; CPU BakeMesh alone is not proof.
                yield return null;
                var bounds=new Bounds();bool first=true,finite=true;int vertices=0;
                foreach(var renderer in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh=new Mesh();renderer.BakeMesh(mesh,true);foreach(var vertex in mesh.vertices)
                    {var point=renderer.transform.TransformPoint(vertex);finite&=float.IsFinite(point.x)&&float.IsFinite(point.y)&&float.IsFinite(point.z);vertices++;if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}Destroy(mesh);
                }
                File.AppendAllText(Path.Combine(output,"bounds.txt"),label+" "+role+" "+bounds.ToString("F4")+"\n");
                Check(finite&&vertices>10000,actor.Definition.Variant+" "+role+" has a finite fully evaluated skin");
                Check(bounds.min.y>-.035f&&bounds.max.y<2.5f,label+" "+role+" stays near its real ground plane");
                if(role=="SleepSide")Check(bounds.size.y<1,actor.Definition.Variant+" really lies down to sleep");
                if(role=="Crawl")Check(bounds.size.y<1.1f,actor.Definition.Variant+" fits through a low shelter entrance");
                if(role=="SitGround"||role=="WorkKneeling")Check(bounds.size.y<1.5f,actor.Definition.Variant+" works/rests near the ground");
                if(variant==1||variant==4&&sample.age==null)
                foreach(int side in new[]{0,1})
                {
                    var focus=bounds.center;camera.orthographic=true;camera.orthographicSize=1.05f;camera.transform.position=focus+(side==0?new Vector3(0,.1f,4):new Vector3(4,.1f,0));camera.transform.LookAt(focus);
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;var image=new Texture2D(1000,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1000,800),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,label+"-"+role+"-"+side+".png"),image.EncodeToPNG());Destroy(image);RenderTexture.active=prior;
                }
            }
            owner.DestroyHuman(actor);yield return null;
        }
        yield return CheckNpcCarrying(owner,camera,target,output);
        File.WriteAllLines(Path.Combine(output,"checks.txt"),checks.Where(c=>!c.EndsWith("Finite NPC skin")));Debug.Log("NPC POSE PASS "+checks.Count);buffer.Dispose();camera.targetTexture=null;target.Release();Destroy(target);Application.Quit(0);
    }
}
