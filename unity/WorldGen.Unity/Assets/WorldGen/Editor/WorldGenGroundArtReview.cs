using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Isolated production-shader material review. No owner session/preferences/world.
public static class WorldGenGroundArtReview
{
    public static void Run()
    {
        try{Review();EditorApplication.Exit(0);}catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    static void Review()
    {
        var command=Environment.GetCommandLineArgs();int at=Array.IndexOf(command,"--ground-review-output");
        string output=Path.GetFullPath(command[at+1]);Directory.CreateDirectory(output);
        var art=Resources.Load<LocalTerrainArtSet>("LocalTerrainArtSet");var arrays=new[]{art.Albedo,art.Normals,art.Masks};int checks=0;
        for(int c=0;c<3;c++)
        {
            int size=c==0?1024:c==1?512:128;var array=arrays[c];
            if(array.width!=size||array.height!=size||array.depth!=17||array.format!=TextureFormat.BC7||array.wrapMode!=TextureWrapMode.Repeat)throw new Exception("Ground array budget/layout changed");checks++;
            for(int layer=0;layer<17;layer++)
            {
                string suffix=c==0?"albedo":c==1?"normal":"mask";
                var t=new Texture2D(2,2,TextureFormat.RGBA32,false,c!=0);
                try
                {
                    ImageConversion.LoadImage(t,File.ReadAllBytes("Assets/WorldGen/Editor/TerrainSurfaceArtV1/layer-"+layer.ToString("D2")+"-"+suffix+".png"));
                    var levels=TerrainSurfaceArtBuilder.Mips(t.GetPixels32(),size,c);int width=size;
                    foreach(var pixels in levels)
                    {
                        for(int i=0;i<width;i++)
                        {if(!pixels[i].Equals(pixels[(width-1)*width+i])||!pixels[i*width].Equals(pixels[i*width+width-1]))throw new Exception("Ground mip boundary split");checks+=2;}
                        if(c==1)for(int i=0;i<pixels.Length;i+=17)
                        {var p=pixels[i];float length=new Vector3(p.r/127.5f-1,p.g/127.5f-1,p.b/127.5f-1).magnitude;if(Mathf.Abs(length-1)>.014f)throw new Exception("Non-unit relief normal");checks++;}
                        width/=2;
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(t);}
            }
        }
        var control=new[]{new Color32(0,0,0,255),new Color32(255,255,255,255),new Color32(255,255,255,255),new Color32(0,0,0,255)};
        var mean=TerrainSurfaceArtBuilder.Mips(control,2,0).Last()[0];if(mean.r<185||mean.r>190)throw new Exception("Colour mip lost linear-light energy");checks++;
        ShaderUtil.allowAsyncCompilation=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;RenderSettings.reflectionIntensity=0;
        var shader=Resources.Load<Shader>("WorldGenLocalTerrain");if(shader==null||!shader.isSupported)throw new Exception("Production ground shader unavailable");
        var material=new Material(shader);material.SetFloat("_TerrainSceneEnabled",0);material.SetFloat("_FogDistance",100000);material.SetFloat("_Daylight",1);material.SetFloat("_TextureMetres",2);material.SetFloat("_RockLayers",1);material.SetFloat("_GroundFlora",1);
        material.SetVector("_PlanetEast",new Vector4(1,0,0));material.SetVector("_PlanetNorth",new Vector4(0,0,1));material.SetVector("_PlanetUp",new Vector4(0,1,0));material.SetVector("_AmbientTint",new Vector4(.30f,.33f,.36f,1));material.SetVector("_FogColor",new Vector4(.55f,.65f,.75f,1));material.SetVector("_AirGeometry",new Vector4(6371000,0,0,0));
        material.SetTexture("_GroundAlbedo",art.Albedo);material.SetTexture("_GroundNormals",art.Normals);material.SetTexture("_GroundMasks",art.Masks);
        var grass=Resources.Load<Texture2D>("GroundGrassV2");var moss=Resources.Load<Texture2D>("GroundMossV1");var lichen=Resources.Load<Texture2D>("GroundLichenV1");
        if(grass==null||moss==null||lichen==null||grass.width!=1024||moss.width!=1024||lichen.width!=256||grass.format!=TextureFormat.BC7||moss.format!=TextureFormat.BC7||lichen.format!=TextureFormat.BC7)throw new Exception("Generated cover format/budget");checks++;
        material.SetTexture("_GrassField",grass);material.SetTexture("_MossField",moss);material.SetTexture("_LichenField",lichen);
        Shader.SetGlobalVector("_TerrainSurfaceRanges",new Vector4(1000,2000,2000,2000));Shader.SetGlobalVector("_TerrainSurfaceControls",new Vector4(1,3,3,2));Shader.SetGlobalInteger("_TerrainDiagnosticFlags",0);
        var root=new GameObject("Generated ground review",typeof(MeshFilter),typeof(MeshRenderer));root.layer=29;
        var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-20,0,-20),new Vector3(20,0,-20),new Vector3(-20,0,20),new Vector3(20,0,20)};mesh.normals=Enumerable.Repeat(Vector3.up,4).ToArray();mesh.triangles=new[]{0,2,1,1,2,3};root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<MeshRenderer>().sharedMaterial=material;
        var camera=new GameObject("Generated ground camera").AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<29;camera.transform.position=new Vector3(0,3,-3);camera.transform.LookAt(Vector3.zero);camera.fieldOfView=50;camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        var sun=new GameObject("Generated ground light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=.8f;sun.transform.rotation=Quaternion.Euler(50,-25,0);sun.cullingMask=1<<29;RenderSettings.sun=sun;
        var target=new RenderTexture(960,640,24);target.Create();camera.targetTexture=target;
        var map=new Texture2D(2,2,TextureFormat.RGBAFloat,false,true);map.wrapMode=TextureWrapMode.Clamp;material.SetTexture("_ForestFloor",map);material.SetTexture("_FloraSurface",map);material.SetTexture("_ForestSurface",map);
        string[] names={"soil","sand","gravel","clay","rock","organic","salt","ice","granite","basalt","sandstone","limestone","mudstone","metamorphic","broadleaf","needles","damp","grass","moss","lichen"};
        for(int layer=0;layer<20;layer++)
        {
            material.SetFloat("_RockLayers",layer==4?0:1);
            var a=Vector4.zero;var b=Vector4.zero;var rocks=Vector4.zero;var rocksB=Vector2.zero;
            if(layer<4)a[layer]=1;else if(layer<8)b[layer-4]=1;else if(layer<14){b.x=1;if(layer<12)rocks[layer-8]=1;else rocksB[layer-12]=1;}else a.x=1;
            Color macro=layer==1?new Color(.65f,.59f,.45f):layer==6||layer==7?new Color(.85f,.85f,.81f):layer>=17?new Color(.35f,.45f,.23f):new Color(.48f,.40f,.31f);
            macro.a=0;mesh.colors=Enumerable.Repeat(macro,4).ToArray();mesh.SetUVs(1,Enumerable.Repeat(a,4).ToArray());mesh.SetUVs(2,Enumerable.Repeat(b,4).ToArray());mesh.SetUVs(4,Enumerable.Repeat(rocks,4).ToArray());mesh.SetUVs(5,Enumerable.Repeat(rocksB,4).ToArray());
            var mix=Color.clear;if(layer>=14&&layer<17){mix[layer-14]=1;mix.a=1;}else if(layer>=17){if(layer==17)mix.a=.45f;else mix[layer-17]=1;}
            map.SetPixels(Enumerable.Repeat(mix,4).ToArray());map.Apply(false);material.SetFloat("_ForestSurfaceEnabled",layer>=14&&layer<17?1:0);material.SetFloat("_ForestFloorEnabled",layer>=14&&layer<17?1:0);material.SetFloat("_FloraSurfaceEnabled",layer>=17?1:0);
            camera.Render();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(960,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,640),0,0);image.Apply();RenderTexture.active=previous;
            var pixels=image.GetPixels32();if(pixels.Count(p=>p.r+p.g+p.b>15)<pixels.Length*.95||pixels.Count(p=>p.r>220&&p.b>220&&p.g<30)>10)throw new Exception("Ground surface empty/error: "+names[layer]);checks++;
            File.WriteAllBytes(Path.Combine(output,names[layer]+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        File.WriteAllText(Path.Combine(output,"PASS.txt"),"PASS "+checks+" ground layout, periodic mip, unit-normal, linear-light and20 production-shader surface renders.17 terrain layers plus3 covers; existing sizes/BC7. No FPS measurement.");
        Debug.Log("GENERATED GROUND ART REVIEW PASS "+checks);
    }
}
