using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class PlayerDeathCheck
{
    public static void Review()
    {
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/death-update/review"));Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var checks=new List<string>();void Check(bool pass,string text){if(!pass)throw new InvalidDataException(text);checks.Add("PASS "+text);}
        var library=Resources.Load<WorldGenHumanLibrary>("Humans/HumanLibrary");Check(library.People.Length==6,"six existing MPFB models loaded");
        var camera=new GameObject("Review camera",typeof(Camera),typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();camera.enabled=false;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.17f,.2f);camera.allowHDR=false;camera.fieldOfView=45;
        var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(45,-35,0);
        RenderSettings.sun=light;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
        Shader.SetGlobalFloat("_WorldGenFirstPersonHumanId",0);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=new Color(.22f,.26f,.24f);floor.GetComponent<Renderer>().sharedMaterial=material;
        var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
        void Capture(string name)
        {
            camera.transform.position=new Vector3(2.6f,2,-2.6f);camera.transform.LookAt(new Vector3(0,.45f,0));
            var target=new RenderTexture(800,600,24);var pixels=new Texture2D(800,600,TextureFormat.RGB24,false);var prior=RenderTexture.active;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,800,600),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=prior;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
        }
        try
        {
            for(int variant=0;variant<6;variant++)
            {
                var root=new GameObject("Death model "+variant);var explorer=root.AddComponent<LocalTerrainExplorer>();
                typeof(LocalTerrainExplorer).GetField("humanLibrary",flags).SetValue(explorer,library);
                var actor=typeof(LocalTerrainExplorer).GetMethod("CreateHuman",flags).Invoke(explorer,new object[]{variant,1});var type=actor.GetType();
                var body=(GameObject)type.GetField("Root").GetValue(actor);var graph=(PlayableGraph)type.GetField("Graph").GetValue(actor);
                body.transform.position=Vector3.up*.02f;graph.Evaluate(0);graph.Stop();body.GetComponent<Animator>().enabled=false;
                var renderers=body.GetComponentsInChildren<SkinnedMeshRenderer>();foreach(var r in renderers){r.forceMatrixRecalculationPerRender=true;r.updateWhenOffscreen=true;}
                File.WriteAllLines(Path.Combine(output,"bones-"+variant+".txt"),body.GetComponentsInChildren<Transform>().Select(t=>t.name+" "+t.position+" scale "+t.lossyScale));
                var ragdoll=root.AddComponent<WorldGenHumanRagdoll>();ragdoll.Begin(body,(x,z)=>0,Vector3.zero);
                Check(ragdoll.BodyCount==13,"fitted physical bodies model "+variant);
                var start=ragdoll.Center;
                for(int step=0;step<300;step++){Physics.Simulate(1f/60);ragdoll.Synchronize();if(step==59)Capture("human-"+variant+"-falling");}
                Capture("human-"+variant+"-rest");
                Check(ragdoll.Center.y<start.y-.2f&&ragdoll.Center.y>0&&ragdoll.Center.magnitude<4,"corpse collapses onto nearby ground "+variant);
                foreach(var rb in root.GetComponentsInChildren<Rigidbody>())Check(float.IsFinite(rb.position.x)&&float.IsFinite(rb.position.y)&&rb.position.y>-.3f&&rb.position.magnitude<5,"finite bounded body "+variant+" "+rb.name);
                var mesh=new Mesh();float lowest=float.MaxValue,highest=float.MinValue;
                // BakeMesh already applies this FBX renderer's 100x scale.
                foreach(var r in renderers){r.BakeMesh(mesh);foreach(var v in mesh.vertices){float y=(r.transform.position+r.transform.rotation*v).y;lowest=Mathf.Min(lowest,y);highest=Mathf.Max(highest,y);}}
                File.AppendAllText(Path.Combine(output,"surfaces.txt"),variant+" lowest="+lowest+" highest="+highest+"\n");
                Check(lowest>-.25f&&highest<1.2f,"evaluated skin remains near ground, no physics explosion "+variant);
                UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(ragdoll);graph.Destroy();
                foreach(Material m in (System.Collections.IEnumerable)type.GetField("OwnedMaterials").GetValue(actor))UnityEngine.Object.DestroyImmediate(m);
                UnityEngine.Object.DestroyImmediate(root);
            }
            File.WriteAllLines(Path.Combine(output,"checks.txt"),checks);Debug.Log("PLAYER DEATH CHECK PASS "+checks.Count);
        }
        finally{Physics.simulationMode=mode;UnityEngine.Object.DestroyImmediate(material);}
    }
}
