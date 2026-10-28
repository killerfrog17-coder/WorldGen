using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldGen.Core;

public static class WoodworkingAssetReview
{
    public static void Run()
    {
        try
        {
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/woodworking/asset-review"));Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);int checks=0;
            void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
            var art=typeof(LocalTerrainExplorer).Assembly.GetType("LocalScatterMeshes");
            var material=new Material(Resources.Load<Shader>("WorldGenInventoryPreview"));material.SetFloat("_VertexColours",1);
            void Item(Mesh mesh,Vector3 at,Vector3 scale,Color tint)
            {
                Check(mesh.vertexCount>10&&mesh.triangles.Length>20,"nonempty geometry");Check(mesh.vertices.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)),"finite mesh");
                Check(mesh.colors.Length==mesh.vertexCount&&mesh.uv.Length==mesh.vertexCount,"colour and UV channels");
                var obj=new GameObject("Craft stage",typeof(MeshFilter),typeof(MeshRenderer));obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=material;
                var props=new MaterialPropertyBlock();props.SetColor("_Color",tint);obj.GetComponent<MeshRenderer>().SetPropertyBlock(props);obj.transform.position=at;obj.transform.localScale=scale;
            }
            var kinds=new[]{PossessionKind.Branch,PossessionKind.TrimmedPole,PossessionKind.TrimmedPole,PossessionKind.WoodenSpear,PossessionKind.DiggingStick};
            for(int i=0;i<kinds.Length;i++)Item((Mesh)art.GetMethod("BuildWood").Invoke(null,new object[]{0,kinds[i],i==2,false}),new Vector3((i-2)*.5f,0,0),new Vector3(.065f,i==4?1:1.9f,.065f),Color.white);
            var stones=new[]{ToolStoneMaterial.Granite,ToolStoneMaterial.FineBasalt,ToolStoneMaterial.Chert,ToolStoneMaterial.Quartzite,ToolStoneMaterial.Quartz};
            for(int i=0;i<stones.Length;i++){int variant=ToolStone.Encode(stones[i],0);V tint=ToolStone.Color(variant,new V(.6,.6,.6));Item((Mesh)art.GetMethod("Build").Invoke(null,new object[]{ScatterKind.Stone,1,variant}),new Vector3((i-2)*.5f,-.4f,0),new Vector3(.24f,.2f,.24f),new Color((float)tint.X,(float)tint.Y,(float)tint.Z));}
            var clips=Resources.LoadAll<AudioClip>("Crafting/Wood");Check(clips.Length==3,"three scraping variants import");foreach(var clip in clips)Check(clip.channels==1&&clip.frequency==44100&&clip.length>.2&&clip.length<1,"short mono wood stroke");
            var camera=new GameObject("Craft review camera",typeof(Camera)).GetComponent<Camera>();camera.transform.position=new Vector3(2.8f,1.6f,-5.4f);camera.transform.LookAt(new Vector3(0,.65f,0));camera.backgroundColor=new Color(.14f,.17f,.18f);camera.clearFlags=CameraClearFlags.SolidColor;
            var target=new RenderTexture(1400,900,24);target.Create();camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var image=new Texture2D(1400,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,"stages.png"),image.EncodeToPNG());
            File.WriteAllText(Path.Combine(output,"checks.txt"),"PASS "+checks+" woodworking geometry/audio checks");Debug.Log("WOOD ASSETS PASS "+checks);EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
