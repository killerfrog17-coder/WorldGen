using System;
using System.Collections;
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
using WorldGen.Core;
using WorldGen.Presentation;

public static class GameModeCheck
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Review()
    {
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        string output=Path.Combine(repo,"artifacts/game-modes/review");Directory.CreateDirectory(output);
        var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new InvalidDataException(label);checks.Add("PASS "+label);}
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var library=Resources.Load<WorldGenHumanLibrary>("Humans/HumanLibrary");
        var root=new GameObject("Mode review");var explorer=root.AddComponent<LocalTerrainExplorer>();
        void Field(string name,object value)=>typeof(LocalTerrainExplorer).GetField(name,Private).SetValue(explorer,value);
        object Get(string name)=>typeof(LocalTerrainExplorer).GetField(name,Private).GetValue(explorer);
        Field("humanLibrary",library);
        var actors=new List<object>();
        object Actor(int variant,int id)
        {
            var a=typeof(LocalTerrainExplorer).GetMethod("CreateHuman",Private).Invoke(explorer,new object[]{variant,id});actors.Add(a);return a;
        }
        GameObject Body(object a)=>(GameObject)a.GetType().GetField("Root").GetValue(a);
        var player=Actor(0,1);Field("humanPlayer",player);var npc=Actor(1,2);Body(npc).transform.position=new Vector3(0,0,1.2f);
        var motor=(TerrainMotion)Get("motion");
        bool Command(string text)=>explorer.ExecuteExplorerCommand(text)?.Success??false;
        Check(explorer.Mode==GameMode.Creative&&Body(player).activeInHierarchy,"legacy/default mode is creative with a real body");
        Check(Command("/gamemode spectator")&&motor.Flying&&motor.NoClip&&!Body(player).activeInHierarchy,"spectator flies without body or ground collision");
        motor.Y=-25;motor.Move(.1,0,0,-1,0,0,false,false,(x,z)=>100);
        Check(motor.Y< -25,"spectator moves through a high ground surface");
        Check(Body(npc).activeInHierarchy,"spectator does not hide other humans");
        explorer.Vitals.Health=42;explorer.Vitals.Thirst=60;
        Check(Command("/gamemode survival")&&!motor.Flying&&!motor.NoClip&&Body(player).activeInHierarchy,"survival restores body and disables flight/noclip");
        Check(explorer.Vitals.Health==42&&explorer.Vitals.Thirst==60,"spectator exit preserves survival state");
        Check(!Command("/gamemode invalid")&&!Command("/gamemode survival extra")&&explorer.Mode==GameMode.Survival,"invalid commands do not change mode");
        Check(Command("/gamemode")&&explorer.Mode==GameMode.Survival,"explicit mode query reports state without a HUD");
        explorer.Vitals.Health=0;explorer.Vitals.DeathCause=PlayerDeathCause.Cold;
        Check(Command("/gamemode creative")&&explorer.UnlimitedResources&&!motor.NoClip&&explorer.Vitals.Health==100&&explorer.Vitals.Stamina==100,"creative is unlimited and can recover a dead prototype player");
        Check(Command("/gamemode spectator")&&Command("/spawn human 20")&&(long)Get("humanSpawnQueued")==20,"manual spawns remain available in spectator");
        Check(Command("/spawn clear")&&(long)Get("humanSpawnQueued")==0,"shared clear still cancels human requests");
        Check(explorer.ExecuteExplorerCommand("/humans")==null&&explorer.ExecuteExplorerCommand("/runstyle")==null,"removed audition commands remain removed");
        var camera=new GameObject("Review camera").AddComponent<Camera>();camera.enabled=false;
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.18f);camera.allowHDR=false;camera.fieldOfView=42;
        camera.transform.position=new Vector3(4,1.4f,-1);camera.transform.LookAt(new Vector3(0,.85f,.4f));
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
        var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(45,-35,0);RenderSettings.sun=light;
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.localScale=Vector3.one*4;
        floor.GetComponent<Renderer>().sharedMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Shader.SetGlobalFloat("_WorldGenFirstPersonHumanId",0);
        foreach(var actor in actors)foreach(var r in Body(actor).GetComponentsInChildren<SkinnedMeshRenderer>())r.forceMatrixRecalculationPerRender=true;
        Color32[] Capture(string name)
        {
            var target=new RenderTexture(640,480,24);var pixels=new Texture2D(640,480,TextureFormat.RGB24,false);var prior=RenderTexture.active;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,640,480),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());return pixels.GetPixels32();}
            finally{camera.targetTexture=null;RenderTexture.active=prior;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
        }
        try
        {
            Command("/gamemode creative");var creative=Capture("creative-body");
            Command("/gamemode spectator");var spectator=Capture("spectator-no-body");
            Check(creative.Zip(spectator,(a,b)=>Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)))).Count(d=>d>3)>1000,"actual rendered player vanishes in spectator");
            Check(Body(npc).GetComponentsInChildren<Renderer>().All(r=>r.enabled&&r.gameObject.activeInHierarchy),"NPC remains visible in spectator render");
            Command("/gamemode survival");var survival=Capture("survival-body");
            Check(creative.Zip(survival,(a,b)=>Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)))).Count(d=>d>3)<5,"survival restores the same full third-person body");
            File.WriteAllLines(Path.Combine(output,"checks.txt"),checks);Debug.Log("GAME MODE CHECK PASS "+checks.Count);
        }
        finally
        {
            Field("humanPlayer",null);((IList)Get("humanNpcs")).Clear();
            foreach(var actor in actors)
            {
                var t=actor.GetType();((PlayableGraph)t.GetField("Graph").GetValue(actor)).Destroy();
                foreach(Material material in (IEnumerable)t.GetField("OwnedMaterials").GetValue(actor))UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(Body(actor));
            }
            UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(floor.GetComponent<Renderer>().sharedMaterial);
        }
    }
}
