using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StoneCraftingAssetCheck
{
    public static void Run()
    {
        try
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../..")),output=Path.Combine(root,"artifacts/stone-crafting/asset-review");Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            int checks=0;void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
            var type=typeof(LocalTerrainExplorer);var art=type.Assembly.GetType("LocalScatterMeshes");
            var original=(Mesh)art.GetMethod("Build").Invoke(null,new object[]{WorldGen.Core.ScatterKind.Stone,1,3});
            var worked=(Mesh)type.GetMethod("WorkedStoneMesh",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{original,2});
            var flake=(Mesh)type.GetMethod("CuttingFlakeMesh",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
            foreach(var mesh in new[]{original,worked,flake})
            {Check(mesh.vertexCount>10&&mesh.triangles.Length>20,"nonempty worked geometry");Check(mesh.vertices.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)),"finite stone geometry");Check(mesh.normals.Length==mesh.vertexCount&&mesh.uv.Length==mesh.vertexCount,"render channels retained");}
            Check(worked.bounds.size.x<original.bounds.size.x,"flaking removes an exterior region");
            var sounds=Resources.LoadAll<AudioClip>("Crafting/Stone");Check(sounds.Length==6,"six packaged AI stone impacts");
            foreach(var clip in sounds)
            {Check(clip.channels==1&&clip.frequency==44100&&clip.length>=.08&&clip.length<=.85,"mono isolated impact");var pcm=new float[clip.samples];Check(clip.GetData(pcm,0)&&pcm.Any(x=>Math.Abs(x)>.005)&&pcm.All(x=>float.IsFinite(x)&&Math.Abs(x)<.66),"decoded impact finite, audible and bounded");}
            var material=new Material(Resources.Load<Shader>("WorldGenInventoryPreview"));material.SetColor("_Color",new Color(.42f,.45f,.44f));
            var meshes=new[]{original,worked,flake};
            for(int i=0;i<3;i++)
            {var obj=new GameObject("Stone review "+i,typeof(MeshFilter),typeof(MeshRenderer));obj.GetComponent<MeshFilter>().sharedMesh=meshes[i];obj.GetComponent<MeshRenderer>().sharedMaterial=material;obj.transform.position=new Vector3((i-1)*1.4f,0,0);obj.transform.localScale=i==2?new Vector3(1,.12f,.7f):Vector3.one;}
            var camera=new GameObject("Stone review camera",typeof(Camera)).GetComponent<Camera>();camera.transform.position=new Vector3(2.9f,2.7f,-5.5f);camera.transform.LookAt(new Vector3(0,.35f,0));camera.backgroundColor=new Color(.12f,.16f,.17f);camera.clearFlags=CameraClearFlags.SolidColor;
            var target=new RenderTexture(1300,700,24);target.Create();camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var image=new Texture2D(1300,700,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1300,700),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,"stones.png"),image.EncodeToPNG());
            File.WriteAllText(Path.Combine(output,"checks.txt"),"PASS "+checks+" geometry and audio asset checks");Debug.Log("STONE CRAFT ASSETS PASS "+checks);EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
