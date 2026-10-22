using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldGen.Presentation;
using Debug=UnityEngine.Debug;
using Object=UnityEngine.Object;

// Isolated synthetic surface, no world loading, owner player, or preferences.
public static class WorldGenTerrainLabReview
{
    public static void Run()
    {
        try{Review();EditorApplication.Exit(0);}catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    static void Review()
    {
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        string output=Path.Combine(repo,"artifacts/terrain-lab-review");Directory.CreateDirectory(output);
        ShaderUtil.allowAsyncCompilation=false;RenderSettings.reflectionIntensity=0;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;
        var shader=Resources.Load<Shader>("WorldGenLocalTerrain");if(shader==null||!shader.isSupported)throw new Exception("Terrain shader unavailable");
        var git=new Process{StartInfo=new ProcessStartInfo("git","show 05736c5be3c7267ccb0e0235bdfec3e4127fd2c0:unity/WorldGen.Unity/Assets/WorldGen/Resources/WorldGenLocalTerrain.shader"){WorkingDirectory=repo,UseShellExecute=false,RedirectStandardOutput=true,CreateNoWindow=true}};
        git.Start();string original=git.StandardOutput.ReadToEnd();git.WaitForExit();if(git.ExitCode!=0)throw new Exception("Missing pre-lab shader reference");
        original=original.Replace("WorldGen/LocalTerrain","WorldGen/TerrainLabReference").Replace("#include \"WorldGen","#include \"Assets/WorldGen/Resources/WorldGen");
        var reference=ShaderUtil.CreateShaderAsset(original);
        var material=new Material(shader);material.SetFloat("_TerrainSceneEnabled",0);
        material.SetFloat("_FogDistance",100000);material.SetFloat("_Daylight",1);material.SetFloat("_TextureMetres",2);material.SetFloat("_RockLayers",1);material.SetFloat("_GroundFlora",1);
        material.SetVector("_PlanetEast",new Vector4(1,0,0));material.SetVector("_PlanetNorth",new Vector4(0,0,1));material.SetVector("_PlanetUp",new Vector4(0,1,0));
        material.SetVector("_AmbientTint",new Vector4(.16f,.2f,.23f,1));material.SetVector("_FogColor",new Vector4(.55f,.65f,.75f,1));
        material.SetVector("_AirGeometry",new Vector4(6371000,0,0,0));material.SetFloat("_AirObserverM",100);material.SetFloat("_AirHaze",.3f);
        var albedo=new Texture2DArray(8,8,14,TextureFormat.RGBA32,true,false);var normals=new Texture2DArray(8,8,14,TextureFormat.RGBA32,true,true);var masks=new Texture2DArray(8,8,14,TextureFormat.RGBA32,true,true);
        for(int layer=0;layer<14;layer++)
        {
            var colours=new Color[64];var ns=new Color[64];var ms=new Color[64];
            for(int i=0;i<64;i++){float v=(i%3)*.12f;colours[i]=new Color(.3f+v,.4f+layer*.015f,.2f+v,1);ns[i]=new Color(.4f+v,.5f,.9f,1);ms[i]=new Color(.6f+v,.5f+v,0,.08f);}
            albedo.SetPixels(colours,layer);normals.SetPixels(ns,layer);masks.SetPixels(ms,layer);
        }
        albedo.Apply(true);normals.Apply(true);masks.Apply(true);
        material.SetTexture("_GroundAlbedo",albedo);material.SetTexture("_GroundNormals",normals);material.SetTexture("_GroundMasks",masks);
        Texture2D Surface(Color colour){var t=new Texture2D(2,2,TextureFormat.RGBAHalf,false,true);t.SetPixels(Enumerable.Repeat(colour,4).ToArray());t.Apply();return t;}
        material.SetTexture("_GrassField",Surface(new Color(.6f,.6f,.4f,1)));material.SetTexture("_MossField",Surface(new Color(.4f,.6f,.5f,1)));material.SetTexture("_LichenField",Surface(new Color(.65f,.4f,.5f,1)));
        material.SetTexture("_FloraSurface",Surface(new Color(.2f,.08f,.06f,.15f)));material.SetFloat("_FloraSurfaceEnabled",1);
        material.SetTexture("_MidSurface",Surface(new Color(.18f,.4f,.13f,.4f)));material.SetFloat("_MidSurfaceEnabled",1);
        material.SetTexture("_ForestSurface",Surface(new Color(.12f,.3f,.1f,.5f)));material.SetFloat("_ForestSurfaceEnabled",1);material.SetFloat("_ForestFarEnabled",1);material.SetFloat("_CoverageTileSize",40);
        var legacy=new Material(reference);legacy.CopyPropertiesFromMaterial(material);
        var root=new GameObject("Terrain lab isolated surface",typeof(MeshFilter),typeof(MeshRenderer));root.layer=29;
        var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-20,0,-20),new Vector3(20,0,-20),new Vector3(-20,0,20),new Vector3(20,0,20)};
        mesh.normals=Enumerable.Repeat(new Vector3(.3f,1,.2f).normalized,4).ToArray();mesh.colors=Enumerable.Repeat(new Color(.4f,.35f,.25f,.2f),4).ToArray();mesh.triangles=new[]{0,2,1,1,2,3};
        mesh.SetUVs(1,Enumerable.Repeat(new Vector4(.6f,.4f,0,0),4).ToArray());mesh.SetUVs(2,Enumerable.Repeat(new Vector4(.2f,.1f,0,0),4).ToArray());mesh.SetUVs(4,Enumerable.Repeat(new Vector4(.4f,.3f,.3f,0),4).ToArray());mesh.SetUVs(5,new Vector2[4]);
        root.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
        var cameraRoot=new GameObject("Terrain lab isolated camera");var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<29;
        camera.orthographic=true;camera.orthographicSize=25;camera.transform.rotation=Quaternion.Euler(90,0,0);camera.nearClipPlane=.1f;camera.farClipPlane=5000;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        cameraRoot.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        var target=new RenderTexture(128,128,24);target.Create();camera.targetTexture=target;
        var sun=new GameObject("Terrain lab isolated light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=.2f;sun.cullingMask=1<<29;sun.transform.rotation=Quaternion.Euler(45,-25,0);RenderSettings.sun=sun;
        Color32[] Capture(TerrainDiagnosticFlags flags)
        {
            if(flags!=0)renderer.sharedMaterial.EnableKeyword("WORLDGEN_TERRAIN_LAB");else renderer.sharedMaterial.DisableKeyword("WORLDGEN_TERRAIN_LAB");
            Shader.SetGlobalInteger("_TerrainDiagnosticFlags",(int)flags);Shader.SetGlobalFloat("_TerrainDiagnosticRadius",153.6f);camera.Render();
            var prior=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(128,128,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();RenderTexture.active=prior;
            var pixels=image.GetPixels32();Object.DestroyImmediate(image);return pixels;
        }
        int Difference(Color32[] a,Color32[] b){int maximum=0;for(int i=0;i<a.Length;i++)maximum=Math.Max(maximum,Math.Max(Math.Abs(a[i].r-b[i].r),Math.Max(Math.Abs(a[i].g-b[i].g),Math.Abs(a[i].b-b[i].b))));return maximum;}
        var lines=new List<string>();int checks=0;
        foreach(float height in new[]{100f,700f,2000f})
        {
            camera.transform.position=new Vector3(0,height,0);renderer.sharedMaterial=legacy;Capture(0);Capture(0);var before=Capture(0);renderer.sharedMaterial=material;Capture(0);var normal=Capture(0);
            int delta=Difference(before,normal);if(delta>2)throw new Exception("Normal terrain changed against pre-lab shader at "+height+"m: "+delta);checks++;lines.Add("Normal reference "+height+"m max RGB delta "+delta);
            if(normal.Count(p=>p.r+p.g+p.b>10)<4000)throw new Exception("Surface fixture is empty");
            foreach(TerrainDiagnosticFlags flag in Enum.GetValues(typeof(TerrainDiagnosticFlags)))
            {var tested=Capture(flag);lines.Add(height+"m "+flag+" RGB delta "+Difference(normal,tested));checks++;}
            int flatDelta=Difference(normal,Capture(TerrainDiagnosticFlags.FlatColour)),unlitDelta=Difference(normal,Capture(TerrainDiagnosticFlags.Unlit));
            Debug.Log("TERRAIN LAB "+height+"m normal "+normal[8256]+" flat delta "+flatDelta+" unlit delta "+unlitDelta+" global "+Shader.GetGlobalInteger("_TerrainDiagnosticFlags"));
            File.WriteAllLines(Path.Combine(output,"progress.txt"),lines);
            if(flatDelta<10||unlitDelta<10)throw new Exception("Flat/unlit diagnostics did not reach terrain at "+height+"m: "+flatDelta+" / "+unlitDelta);
            var restored=Capture(0);int restoredDelta=Difference(normal,restored);
            if(restoredDelta>2)throw new Exception("Restoring flags changed normal rendering at "+height+"m: "+restoredDelta+"; expected "+normal[8256]+" got "+restored[8256]);checks++;
        }
        var compatibility=typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityCode",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static);
        if((int)compatibility.Invoke(null,new object[]{shader,0})!=0)throw new Exception("Terrain lost SRP Batcher compatibility");checks++;
        TerrainDiagnostics.Flags=TerrainDiagnosticFlags.NoTextures;TerrainDiagnostics.DistanceOnly=true;TerrainDiagnostics.Grid=8;TerrainDiagnostics.HideLand=TerrainDiagnostics.HoldTerrain=TerrainDiagnostics.NoReceiveShadows=true;
        var options=new TerrainDetailOptions();TerrainDiagnostics.Detail(options);if(options.EstimateRelief||options.FinestSegments!=8)throw new Exception("Diagnostic detail options were not copied");
        TerrainDiagnostics.Reset();if(TerrainDiagnostics.Active||TerrainDiagnostics.Grid!=64)throw new Exception("Reset retained diagnostic overrides");checks++;
        // Exercise the real cached-tile visibility/keyword and scene-shadow path.
        const BindingFlags instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var explorer=new GameObject("Terrain lab binding fixture").AddComponent<LocalTerrainExplorer>();explorer.enabled=false;
        var explorerType=typeof(LocalTerrainExplorer);var tileType=explorerType.GetNestedType("Tile",BindingFlags.NonPublic);var tile=Activator.CreateInstance(tileType);
        void Set(string field,object value)=>explorerType.GetField(field,instance).SetValue(explorer,value);
        object Call(string method,params object[] args)=>explorerType.GetMethod(method,instance).Invoke(explorer,args);
        Set("landMaterial",material);Set("waterMaterial",new Material(material));Set("localSun",sun);
        var waterRoot=new GameObject("Fixture water",typeof(MeshFilter),typeof(MeshRenderer));waterRoot.transform.SetParent(root.transform,false);
        waterRoot.GetComponent<MeshFilter>().sharedMesh=mesh;waterRoot.GetComponent<MeshRenderer>().sharedMaterial=(Material)explorerType.GetField("waterMaterial",instance).GetValue(explorer);
        tileType.GetField("Root",instance).SetValue(tile,root);
        ((System.Collections.IDictionary)explorerType.GetField("tiles",instance).GetValue(explorer)).Add(new TerrainTileKey(0,0,0,.1),tile);
        Call("EnsureTerrainMaterials",tile);sun.shadows=LightShadows.Soft;
        TerrainDiagnostics.HideLand=TerrainDiagnostics.NoReceiveShadows=TerrainDiagnostics.NoShadowMaps=TerrainDiagnostics.CullBackFaces=true;TerrainDiagnostics.Changed();Call("ApplyTerrainDiagnostics");
        var landRenderer=root.GetComponent<MeshRenderer>();var waterRenderer=waterRoot.GetComponent<MeshRenderer>();
        if(landRenderer.enabled||!waterRenderer.enabled||sun.shadows!=LightShadows.None||!landRenderer.sharedMaterial.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF")||landRenderer.sharedMaterial.GetFloat("_TerrainCull")!=2||waterRenderer.sharedMaterial.GetFloat("_TerrainCull")!=0)throw new Exception("Land/shadow/cull switches did not reach actual tile bindings");checks++;
        TerrainDiagnostics.HideLand=false;TerrainDiagnostics.HideWater=true;TerrainDiagnostics.Changed();Call("EnsureTerrainMaterials",tile);
        if(!landRenderer.enabled||waterRenderer.enabled)throw new Exception("Cached tile did not adopt visibility switches");checks++;
        TerrainDiagnostics.Reset();Call("ApplyTerrainDiagnostics");
        if(!landRenderer.enabled||!waterRenderer.enabled||sun.shadows!=LightShadows.Soft||landRenderer.sharedMaterial.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF")||landRenderer.sharedMaterial.GetFloat("_TerrainCull")!=0)throw new Exception("Restore did not restore visibility, shadow receiving, sunlight and culling");checks++;
        Call("RestoreTerrainDiagnostics");
        Shader.SetGlobalInteger("_TerrainDiagnosticFlags",0);lines.Add("PASS "+checks+" isolated terrain shader/state checks; no world loaded; no FPS result.");
        File.WriteAllLines(Path.Combine(output,"PASS.txt"),lines);Debug.Log(lines.Last());
    }
}
