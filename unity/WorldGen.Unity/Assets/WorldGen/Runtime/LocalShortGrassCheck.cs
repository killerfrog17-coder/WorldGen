using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed partial class LocalTerrainExplorer
{
    [StructLayout(LayoutKind.Sequential)]
    struct ShortGrassRoot {public Vector4 Position,ColourHeight,Shape,Normal,Grasses,Herbs;}
    // Opt-in packaged production-shader regression. No world, display prefs,
    // vegetation settings, source data or user save is opened/changed.
    public static IEnumerator CheckShortGrass(string output)
    {
        Directory.CreateDirectory(output);
        Application.logMessageReceived+=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        QualitySettings.vSyncCount=0;Application.targetFrameRate=30;yield return null;
        var material=new Material(Resources.Load<Shader>("WorldGenGroundCover"));
        material.SetFloat("_GrassWind",0);material.SetFloat("_GrassNear",45);material.SetFloat("_GrassRange",350);material.SetFloat("_FogDistance",10000);material.SetFloat("_Daylight",1);material.SetColor("_AmbientTint",new Color(.3f,.3f,.3f));
        Shader.SetGlobalVector("_AirGeometry",new Vector4(6371000,0,0,0));Shader.SetGlobalFloat("_AirObserverM",0);Shader.SetGlobalFloat("_AirHaze",0);
        var cameraRoot=new GameObject("Short grass shader fixture");var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.65f,.70f,.80f);
        camera.cullingMask=1<<30;camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.orthographic=true;camera.orthographicSize=.50f;camera.aspect=1;
        camera.transform.position=new Vector3(0,.55f,-1.5f);camera.transform.LookAt(new Vector3(0,.12f,0));cameraRoot.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        var target=new RenderTexture(768,768,24);target.Create();camera.targetTexture=target;
        var sunRoot=new GameObject("Grass fixture sun");var sun=sunRoot.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2;sun.cullingMask=1<<30;sun.transform.rotation=Quaternion.Euler(45,-35,0);RenderSettings.sun=sun;
        var roots=new GraphicsBuffer(GraphicsBuffer.Target.Structured,1,96);var visible=new GraphicsBuffer(GraphicsBuffer.Target.Structured,1,4);visible.SetData(new uint[]{0});
        var args=new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,1,GraphicsBuffer.IndirectDrawArgs.size);
        var props=new MaterialPropertyBlock();props.SetBuffer("_CoverRoots",roots);props.SetBuffer("_VisibleRoots",visible);props.SetVector("_CoverOrigin",Vector4.zero);props.SetVector("_CoverTileOrigin",Vector4.zero);props.SetFloat("_GrassNear",45);props.SetFloat("_GrassRange",350);
        var root=new ShortGrassRoot{Position=Vector4.zero,ColourHeight=new Vector4(.3f,.45f,.15f,.25f),Shape=new Vector4(2,.31f,.73f,0),Normal=new Vector4(0,1,0,.45f),Grasses=new Vector4(1,0,0,0)};
        Color32[] Capture(bool split,string name)
        {
            material.SetFloat("_SplitShortGrass",split?1:0);
            var parameters=new RenderParams(material){camera=camera,layer=30,worldBounds=new Bounds(Vector3.zero,Vector3.one*10),matProps=props,shadowCastingMode=ShadowCastingMode.Off};
            Graphics.RenderPrimitivesIndirect(in parameters,MeshTopology.Triangles,args,1);camera.Render();
            var prior=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(768,768,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();RenderTexture.active=prior;
            File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());var pixels=image.GetPixels32();Destroy(image);return pixels;
        }
        int Coverage(Color32[] pixels){var bg=pixels[0];int count=0;foreach(var p in pixels)if(Math.Abs(p.r-bg.r)+Math.Abs(p.g-bg.g)+Math.Abs(p.b-bg.b)>20)count++;return count;}
        string report="";
        for(int segments=1;segments<=4;segments++)
        {
            roots.SetData(new[]{root});props.SetInt("_CoverLod",0);props.SetVector("_BladeCounts",new Vector4(1,1,1,segments));
            args.SetData(new[]{new GraphicsBuffer.IndirectDrawArgs{vertexCountPerInstance=(uint)(segments*6),instanceCount=1}});
            var single=Capture(false,"short-"+segments+"-single");yield return null;
            var pair=Capture(true,"short-"+segments+"-split");yield return null;
            int before=Coverage(single),after=Coverage(pair);
            if(before<100||after<before*1.20)throw new Exception("Short grass did not gain coverage with same vertex budget: "+segments+" "+before+"/"+after);
            report+="short segments="+segments+" vertices="+(segments*6)+" pixels="+before+" -> "+after+"\n";
        }
        foreach(int form in new[]{0,1,2,3,4,5,6,7,8})
        {
            // Tall grasses and all non-grass shapes must remain pixel-identical.
            root.ColourHeight.w=form<4?1.3f:.25f;root.Grasses=Vector4.zero;root.Herbs=Vector4.zero;
            if(form<4)root.Grasses[form]=1;else if(form<8)root.Herbs[form-4]=1;
            roots.SetData(new[]{root});props.SetVector("_BladeCounts",new Vector4(1,1,1,3));args.SetData(new[]{new GraphicsBuffer.IndirectDrawArgs{vertexCountPerInstance=18,instanceCount=1}});
            var single=Capture(false,"control-"+form+"-single");yield return null;var pair=Capture(true,"control-"+form+"-split");yield return null;
            for(int i=0;i<single.Length;i++)if(!single[i].Equals(pair[i]))throw new Exception("Split altered tall/non-grass form "+form);
            report+="unchanged form="+form+"\n";
        }
        roots.Dispose();visible.Dispose();args.Dispose();File.WriteAllText(Path.Combine(output,"PASS.txt"),SystemInfo.graphicsDeviceName+"\n"+report);Debug.Log("SHORT GRASS SPLIT PASS "+report);Application.Quit(0);
    }
}
