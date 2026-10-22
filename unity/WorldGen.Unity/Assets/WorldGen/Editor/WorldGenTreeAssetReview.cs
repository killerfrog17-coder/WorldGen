using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldGen.Core;

// Small, isolated asset check. No player launch, world load or preferences.
public static class WorldGenTreeAssetReview
{
    public static void Run()
    {
        try
        {
            var assembly=typeof(WorldGenRuntime).Assembly;
            var generator=assembly.GetType("LocalScatterMeshes");var texture=assembly.GetType("LocalTreeTextures");var baker=assembly.GetType("LocalTreeImpostor");
            texture.GetMethod("Generate",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/tree-baked-assets"));Directory.CreateDirectory(folder);
            foreach(int code in new[]{0x02345600,0x12345600})
            {
                var timer=System.Diagnostics.Stopwatch.StartNew();
                var near=(Mesh)generator.GetMethod("Build").Invoke(null,new object[]{ScatterKind.GeneratedTree,0,code});
                var middle=(Mesh)generator.GetMethod("Build").Invoke(null,new object[]{ScatterKind.GeneratedTree,1,code});
                foreach(var mesh in new[]{near,middle})
                {
                    if(mesh.vertexCount<100||mesh.triangles.Length<300||mesh.triangles.Any(i=>i<0||i>=mesh.vertexCount))throw new Exception("Invalid tree topology");
                    if(mesh.vertices.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)))throw new Exception("Non-finite tree vertex");
                    if(mesh.bounds.min.y<-.01||mesh.bounds.max.y>1.3||mesh.bounds.size.x>1.4||mesh.bounds.size.z>1.4)throw new Exception("Tree exceeds existing conservative bounds");
                }
                var source=Activator.CreateInstance(baker.GetNestedType("Source"),near,(int)new TreeMorphology(code).LeafShape);
                object result=baker.GetMethod("Bake").Invoke(null,new[]{source,(object)CancellationToken.None});
                var pixels=(Color32[][])result.GetType().GetField("Colours").GetValue(result);
                if(pixels.Length!=16||pixels.Any(p=>p.Count(c=>c.a>=102)<100))throw new Exception("Missing tree bake views/coverage");
                // A CPU contact sheet makes view orientation/silhouette inspectable.
                var atlas=new Texture2D(512,512,TextureFormat.RGBA32,false);
                for(int i=0;i<16;i++)atlas.SetPixels32((i%4)*128,(i/4)*128,128,128,pixels[i]);atlas.Apply();
                File.WriteAllBytes(Path.Combine(folder,code+"-views.png"),atlas.EncodeToPNG());
                if(!Environment.GetCommandLineArgs().Contains("-nographics"))Capture(folder,code,near,result,texture);
                string line=code+" near="+near.triangles.Length/3+" mid="+middle.triangles.Length/3+" views=16 ms="+timer.Elapsed.TotalMilliseconds.ToString("F1");
                File.AppendAllText(Path.Combine(folder,"results.txt"),line+"\n");Debug.Log("TREE ASSET PASS "+line);
                UnityEngine.Object.DestroyImmediate(near);UnityEngine.Object.DestroyImmediate(middle);UnityEngine.Object.DestroyImmediate(atlas);
            }
            Debug.Log("TREE ASSET REVIEW PASSED");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Capture(string folder,int code,Mesh mesh,object result,Type textures)
    {
        var args=new object[]{result.GetType().GetField("Colours").GetValue(result),result.GetType().GetField("Normals").GetValue(result),128,"Review tree",null,null};
        textures.GetMethod("Upload").Invoke(null,args);var colours=(Texture2DArray)args[4];var normals=(Texture2DArray)args[5];
        var leaves=new object[]{null,null};textures.GetMethod("Create").Invoke(null,leaves);
        var morphology=new TreeMorphology(code);var shader=Shader.Find("WorldGen/TreeSurface2");if(shader==null||!shader.isSupported)throw new Exception("Prototype shader unsupported");
        var material=new Material(shader){enableInstancing=true};material.SetTexture("_TreeLeaves",(Texture)leaves[0]);material.SetTexture("_TreeLeafNormals",(Texture)leaves[1]);material.SetFloat("_TreeLeafShape",(int)morphology.LeafShape);material.SetFloat("_Wind",0);material.SetFloat("_FogDistance",10000);
        material.SetColor("_AmbientTint",new Color(.16f,.19f,.23f));var far=new Material(material);far.SetFloat("_Impostor",1);far.SetTexture("_TreeColourViews",colours);far.SetTexture("_TreeNormalViews",normals);
        var center=(Vector3)result.GetType().GetField("Center").GetValue(result);float radius=(float)result.GetType().GetField("Radius").GetValue(result);far.SetVector("_ImpostorBounds",new Vector4(center.x,center.y,center.z,radius));
        var quad=new Mesh();quad.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0)};quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};quad.triangles=new[]{0,1,2,1,3,2};quad.bounds=new Bounds(center,Vector3.one*radius*2.1f);
        var cameraRoot=new GameObject("Tree asset review camera");var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.42f,.53f,.67f);camera.cullingMask=1<<30;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.fieldOfView=45;
        cameraRoot.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        var target=new RenderTexture(1280,720,24);target.Create();camera.targetTexture=target;camera.aspect=1280f/720;
        var lightRoot=new GameObject("Tree review sunlight");var light=lightRoot.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2.3f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(48,-35,0);RenderSettings.sun=light;
        var properties=new MaterialPropertyBlock();properties.SetVectorArray("_Tint",new[]{Vector4.one});properties.SetVectorArray("_LodTransition",new[]{new Vector4(0,1,0,0)});
        Shader.SetGlobalVector("_AirGeometry",new Vector4(6371000,0,0,0));Shader.SetGlobalFloat("_AirObserverM",0);Shader.SetGlobalFloat("_AirHaze",0);
        foreach(float yaw in new[]{0f,45f,115f})
        {
            camera.transform.position=new Vector3(0,.55f,-3);camera.transform.rotation=Quaternion.identity;
            var a=Matrix4x4.TRS(new Vector3(-.58f,0,0),Quaternion.Euler(0,yaw,0),Vector3.one);var b=Matrix4x4.TRS(new Vector3(.58f,0,0),Quaternion.Euler(0,yaw,0),Vector3.one);
            var parameters=new RenderParams(material){camera=camera,layer=30,worldBounds=new Bounds(Vector3.up*.5f,Vector3.one*5),matProps=properties,shadowCastingMode=ShadowCastingMode.Off};
            Graphics.RenderMeshInstanced(in parameters,mesh,0,new[]{a},1);parameters.material=far;Graphics.RenderMeshInstanced(in parameters,quad,0,new[]{b},1);camera.Render();
            var prior=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();RenderTexture.active=prior;
            var data=pixels.GetPixels32();var bg=data[0];int changed=data.Count(p=>Math.Abs(p.r-bg.r)+Math.Abs(p.g-bg.g)+Math.Abs(p.b-bg.b)>15);if(changed<1000)throw new Exception("Tree review render blank");
            File.WriteAllBytes(Path.Combine(folder,code+"-mesh-vs-bake-"+yaw+".png"),pixels.EncodeToPNG());UnityEngine.Object.DestroyImmediate(pixels);
        }
        foreach(var obj in new UnityEngine.Object[]{cameraRoot,lightRoot,target,material,far,quad,colours,normals,(Texture)leaves[0],(Texture)leaves[1]})UnityEngine.Object.DestroyImmediate(obj);
    }
}
