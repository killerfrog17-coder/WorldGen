using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldGen.Core;

public static class PortableTechnologyReview
{
    public static void Run()
    {
        try
        {
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/portable-technology/review"));Directory.CreateDirectory(output);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);int checks=0;
            void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
            var art=typeof(LocalTerrainExplorer).Assembly.GetType("LocalScatterMeshes");var shader=Resources.Load<Shader>("WorldGenPortableItem");Check(shader!=null&&shader.isSupported,"Portable item shader supported");
            var material=new Material(shader);material.SetFloat("_Preview",1);
            var camera=new GameObject("Review camera",typeof(Camera)).GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.18f;camera.transform.position=new Vector3(1.9f,1.3f,-5);camera.transform.LookAt(new Vector3(0,.65f,0));camera.backgroundColor=new Color(.13f,.16f,.18f);camera.clearFlags=CameraClearFlags.SolidColor;
            var target=new RenderTexture(280,300,24);target.Create();camera.targetTexture=target;
            int columns=9,rows=(PortableTechnology.Items.Length+columns-1)/columns;var sheet=new Texture2D(columns*280,rows*330,TextureFormat.RGB24,false);
            var background=Enumerable.Repeat(new Color(.13f,.16f,.18f),sheet.width*sheet.height).ToArray();sheet.SetPixels(background);
            for(int i=0;i<PortableTechnology.Items.Length;i++)
            {
                var def=PortableTechnology.Items[i];var p=PortableTechnology.Create(def.Kind,new V(1,0,0));
                var mesh=(Mesh)art.GetMethod("BuildPortable").Invoke(null,new object[]{p});Check(mesh!=null&&mesh.vertexCount>3&&mesh.triangles.Length>0,def.Name+" geometry");
                Check(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),def.Name+" finite vertices");Check(mesh.uv.Length==mesh.vertexCount&&mesh.colors.Length==mesh.vertexCount,def.Name+" UV and materials");Check(mesh.triangles.Length/3<10000,def.Name+" mesh budget");
                var obj=new GameObject(def.Name,typeof(MeshFilter),typeof(MeshRenderer));obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=material;
                float enlarge=Mathf.Min(2.6f,1.5f/(float)Math.Max(p.HeightM,p.WidthM));obj.transform.localScale=new Vector3((float)p.WidthM,(float)p.HeightM,(float)p.DepthM)*enlarge;obj.transform.position=Vector3.zero;
                camera.Render();RenderTexture.active=target;var image=new Texture2D(280,300,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,280,300),0,0);image.Apply();
                sheet.SetPixels((i%columns)*280,(rows-1-i/columns)*330+30,280,300,image.GetPixels());File.WriteAllBytes(Path.Combine(output,i.ToString("D2")+"-"+def.Kind+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(obj);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(image);
            }
            sheet.Apply();File.WriteAllBytes(Path.Combine(output,"items.png"),sheet.EncodeToPNG());
            var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();Check(errors.Length==0,string.Join("; ",errors.Select(e=>e.message)));
            var iconShader=Resources.Load<Shader>("WorldGenPortableIcon");Check(iconShader!=null&&iconShader.isSupported,"Source coat icon shader supported");Check(!ShaderUtil.GetShaderMessages(iconShader).Any(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),"No icon shader errors");
            File.WriteAllText(Path.Combine(output,"checks.txt"),"PASS "+checks+" portable mesh/shader checks");File.WriteAllLines(Path.Combine(output,"labels.txt"),PortableTechnology.Items.Select((d,i)=>i+" "+d.Name));Debug.Log("PORTABLE ASSETS PASS "+checks);EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
