using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldGen.Presentation;

// Isolated production-binding/shader check, not a world or FPS benchmark.
public static class WorldGenTerrainBatchReview
{
    const BindingFlags Instance=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    static Action finish,cleanup;
    public static void Run()
    {
        try{Review();finish();}catch(Exception error){cleanup?.Invoke();Debug.LogException(error);EditorApplication.Exit(1);}
    }
    static void Review()
    {
        string output=Path.GetFullPath("../../artifacts/terrain-srp-review");Directory.CreateDirectory(output);
        var shader=Resources.Load<Shader>("WorldGenLocalTerrain");if(shader==null||!shader.isSupported)throw new Exception("Terrain shader unsupported");
        var root=new GameObject("Terrain batching isolated fixture");var explorer=root.AddComponent<LocalTerrainExplorer>();explorer.enabled=false;
        // Compile the actual pinned pre-change shader in memory as the visual
        // reference. A newly buffered shader's fallback is not an old-renderer
        // reference, particularly for formerly non-serialized uniforms.
        var git=new System.Diagnostics.Process{StartInfo=new System.Diagnostics.ProcessStartInfo("git","show 75a4d5c:unity/WorldGen.Unity/Assets/WorldGen/Resources/WorldGenLocalTerrain.shader"){WorkingDirectory=Path.GetFullPath("../.."),UseShellExecute=false,RedirectStandardOutput=true,CreateNoWindow=true}};git.Start();string legacyText=git.StandardOutput.ReadToEnd();git.WaitForExit();if(git.ExitCode!=0)throw new Exception("Missing pinned terrain shader reference");
        legacyText=legacyText.Replace("WorldGen/LocalTerrain","WorldGen/TerrainReference").Replace("#include \"WorldGen","#include \"Assets/WorldGen/Resources/WorldGen");
        var legacyShader=ShaderUtil.CreateShaderAsset(legacyText);
        var type=typeof(LocalTerrainExplorer);var tileType=type.GetNestedType("Tile",BindingFlags.NonPublic);
        void Set(string field,object value)=>type.GetField(field,Instance).SetValue(explorer,value);
        object Call(string name,params object[] values)=>type.GetMethod(name,Instance).Invoke(explorer,values);
        cleanup=()=>Call("DisposeTerrainScene");
        var land=new Material(shader);land.SetFloat("_FogDistance",100000);land.SetFloat("_Daylight",1);land.SetFloat("_GroundFlora",1);land.SetFloat("_TextureMetres",2);
        land.SetVector("_AmbientTint",new Vector4(.20f,.24f,.28f,1));land.SetVector("_FogColor",new Vector4(.57f,.69f,.76f,1));
        land.SetVector("_PlanetEast",new Vector4(1,0,0,0));land.SetVector("_PlanetNorth",new Vector4(0,0,1,0));land.SetVector("_PlanetUp",new Vector4(0,1,0,0));
        land.SetVector("_LocalOrigin",new Vector4(17,23,0,0));land.SetVector("_TextureAnchor",new Vector4(4,7,3,0));land.SetVector("_AirGeometry",new Vector4(6371000,0,0,0));land.SetFloat("_AirObserverM",40);land.SetFloat("_AirHaze",.25f);
        var albedo=new Texture2DArray(2,2,14,TextureFormat.RGBA32,false,false);var normals=new Texture2DArray(2,2,14,TextureFormat.RGBA32,false,true);var masks=new Texture2DArray(2,2,14,TextureFormat.RGBA32,false,true);
        for(int layer=0;layer<14;layer++){albedo.SetPixels(Enumerable.Repeat(new Color(.5f,.48f,.4f,1),4).ToArray(),layer);normals.SetPixels(Enumerable.Repeat(new Color(.5f,.5f,1,1),4).ToArray(),layer);masks.SetPixels(Enumerable.Repeat(new Color(1,.9f,.5f,0),4).ToArray(),layer);}albedo.Apply();normals.Apply();masks.Apply();
        land.SetTexture("_GroundAlbedo",albedo);land.SetTexture("_GroundNormals",normals);land.SetTexture("_GroundMasks",masks);land.SetTexture("_GrassField",Texture2D.grayTexture);land.SetTexture("_MossField",Texture2D.grayTexture);land.SetTexture("_LichenField",Texture2D.grayTexture);
        var water=new Material(land);water.SetFloat("_Water",1);water.SetVector("_LocalOrigin",Vector4.zero);Set("landMaterial",land);Set("waterMaterial",water);
        Call("PublishTerrainScene");
        var tiles=new object[4];var renderers=new MeshRenderer[4];var materials=new Material[4];var keys=new TerrainTileKey[4];
        for(int i=0;i<4;i++)
        {
            var tile=tiles[i]=Activator.CreateInstance(tileType);var part=new GameObject("Fixture terrain "+i,typeof(MeshFilter),typeof(MeshRenderer));part.layer=30;part.transform.SetParent(root.transform,false);part.transform.position=new Vector3((i%2)*4,0,(i/2)*4);
            var mesh=new Mesh();mesh.vertices=new[]{Vector3.zero,new Vector3(4,0,0),new Vector3(0,0,4),new Vector3(4,0,4)};mesh.normals=Enumerable.Repeat(Vector3.up,4).ToArray();mesh.triangles=new[]{0,2,1,1,2,3};
            Color colour=i==3?new Color(0,0,.7f,2):new Color(.35f,.46f,.2f,.7f);mesh.colors=Enumerable.Repeat(colour,4).ToArray();mesh.SetUVs(1,Enumerable.Repeat(new Vector4(1,0,0,0),4).ToArray());mesh.SetUVs(2,new Vector4[4]);mesh.SetUVs(4,new Vector4[4]);mesh.SetUVs(5,new Vector2[4]);part.GetComponent<MeshFilter>().sharedMesh=mesh;
            renderers[i]=part.GetComponent<MeshRenderer>();renderers[i].sharedMaterial=i<2?land:water;tileType.GetField("Root",Instance).SetValue(tile,part);
            Texture2D Surface(Color value){var t=new Texture2D(2,2,TextureFormat.RGBAHalf,false,true);t.SetPixels(Enumerable.Repeat(value,4).ToArray());t.Apply();return t;}
            tileType.GetField("FloraSurface",Instance).SetValue(tile,Surface(new Color(.3f,.05f,.02f,.23f)));tileType.GetField("MidSurface",Instance).SetValue(tile,Surface(new Color(.2f,.3f,.1f,.4f)));tileType.GetField("ForestSurface",Instance).SetValue(tile,Surface(new Color(.1f,.3f,.15f,.6f)));
            if(i==0){tileType.GetField("CoverageNodes",Instance).SetValue(tile,new float[]{-1,0,0,0});tileType.GetField("Coverage",Instance).SetValue(tile,Surface(new Color(-1,0,0,0)));tileType.GetField("CoverageTimes",Instance).SetValue(tile,Surface(Color.clear));}
            keys[i]=new TerrainTileKey(i%2,i/2,0,1d/256);Call("BindTerrainTile",tile,tile,keys[i],Vector4.zero);materials[i]=renderers[i].sharedMaterial;
        }
        if(materials.Distinct().Count()!=4||renderers.Any(r=>r.HasPropertyBlock()))throw new Exception("Terrain retained property blocks or shared tile state");
        for(int i=0;i<4;i++)if(materials[i].GetTexture("_FloraSurface")!=(Texture)tileType.GetField("FloraSurface",Instance).GetValue(tiles[i]))throw new Exception("Lost per-tile flora texture");
        var reverse=new Vector4(3,7,1,0);Call("BindTerrainTile",tiles[1],tiles[0],keys[0],reverse);
        if(materials[1].GetVector("_LodFade")!=reverse||materials[1].GetTexture("_TerrainCoverage")!=materials[0].GetTexture("_TerrainCoverage")||materials[1].GetTexture("_FloraSurface")!=materials[0].GetTexture("_FloraSurface"))throw new Exception("Previous-product reverse fade lost incoming coverage/textures");
        Call("BindTerrainTile",tiles[1],tiles[1],keys[1],Vector4.zero);
        long tileUpdates=explorer.TerrainMaterialBindingUpdates,sceneUpdates=explorer.TerrainSceneUploads;
        for(int repeat=0;repeat<20;repeat++){for(int i=0;i<4;i++)Call("BindTerrainTile",tiles[i],tiles[i],keys[i],Vector4.zero);Call("PublishTerrainScene");}
        if(explorer.TerrainMaterialBindingUpdates!=tileUpdates||explorer.TerrainSceneUploads!=sceneUpdates)throw new Exception("Settled terrain repeatedly updates GPU/material state");
        land.SetFloat("_FogDistance",90000);Call("PublishTerrainScene");if(explorer.TerrainSceneUploads!=sceneUpdates+1||explorer.TerrainMaterialBindingUpdates!=tileUpdates)throw new Exception("Scene change rewrites tile materials");land.SetFloat("_FogDistance",100000);Call("PublishTerrainScene");
        var cameraRoot=new GameObject("Terrain SRP fixture camera");var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;camera.orthographic=true;camera.orthographicSize=4;camera.transform.position=new Vector3(4,20,4);camera.transform.rotation=Quaternion.Euler(90,0,0);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.nearClipPlane=.1f;cameraRoot.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        var target=new RenderTexture(256,256,24);target.Create();camera.targetTexture=target;
        var sunRoot=new GameObject("Terrain fixture sun");var sun=sunRoot.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.cullingMask=1<<30;sun.transform.rotation=Quaternion.Euler(45,-35,0);RenderSettings.sun=sun;
        Color32[] Capture(string name){camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(256,256,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();RenderTexture.active=old;File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());var pixels=image.GetPixels32();UnityEngine.Object.DestroyImmediate(image);return pixels;}
        var compatibilityMethod=typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityCode",Static);int earlyCode=(int)compatibilityMethod.Invoke(null,new object[]{shader,0});var reasonMethod=typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityIssueReason",Static);Debug.Log("TERRAIN SRP metadata="+earlyCode+" reason="+reasonMethod.Invoke(null,new object[]{shader,0,earlyCode})+" water="+materials[2].GetFloat("_Water")+" sourceEast="+land.GetVector("_PlanetEast"));
        var legacyMaterials=new[]{new Material(legacyShader),new Material(legacyShader)};
        for(int i=0;i<2;i++){var source=i==0?land:water;legacyMaterials[i].CopyPropertiesFromMaterial(source);foreach(string name in new[]{"_PlanetEast","_PlanetNorth","_PlanetUp","_LocalOrigin","_TextureAnchor","_AirGeometry"})legacyMaterials[i].SetVector(name,source.GetVector(name));legacyMaterials[i].SetFloat("_AirObserverM",source.GetFloat("_AirObserverM"));legacyMaterials[i].SetFloat("_AirHaze",source.GetFloat("_AirHaze"));}
        for(int i=0;i<4;i++){renderers[i].sharedMaterial=legacyMaterials[i<2?0:1];var block=new MaterialPropertyBlock();block.SetTexture("_FloraSurface",materials[i].GetTexture("_FloraSurface"));block.SetFloat("_FloraSurfaceEnabled",1);block.SetTexture("_MidSurface",materials[i].GetTexture("_MidSurface"));block.SetFloat("_MidSurfaceEnabled",1);block.SetTexture("_ForestSurface",materials[i].GetTexture("_ForestSurface"));block.SetFloat("_ForestSurfaceEnabled",1);block.SetFloat("_CoverageTileSize",4);block.SetFloat("_TransitionEnabled",1);block.SetFloat("_CoverageEnabled",materials[i].GetFloat("_CoverageEnabled"));block.SetTexture("_TerrainCoverage",materials[i].GetTexture("_TerrainCoverage"));block.SetTexture("_CoverageFadeTimes",materials[i].GetTexture("_CoverageFadeTimes"));renderers[i].SetPropertyBlock(block);}
        var before=Capture("legacy-bindings");
        var stats=typeof(UnityStats).GetProperty("srpBatcherDrawCalls",Static);string oldDraws=stats==null?"unavailable":stats.GetValue(null).ToString();
        for(int i=0;i<4;i++){renderers[i].SetPropertyBlock(null);renderers[i].sharedMaterial=materials[i];materials[i].SetFloat("_TerrainSceneEnabled",1);}
        var after=Capture("srp-bindings");string newDraws=stats==null?"unavailable":stats.GetValue(null).ToString();
        int changed=0,maxDelta=0;for(int i=0;i<before.Length;i++){int delta=Math.Max(Math.Abs(before[i].r-after[i].r),Math.Max(Math.Abs(before[i].g-after[i].g),Math.Abs(before[i].b-after[i].b)));maxDelta=Math.Max(maxDelta,delta);if(delta>2)changed++;}
        Debug.Log("Terrain pixel comparison changed="+changed+" maxDelta="+maxDelta+" draws before="+oldDraws+" after="+newDraws);
        var compatibility=typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityCode",Static);int code=(int)compatibility.Invoke(null,new object[]{shader,0});
        if(code!=0){var reason=typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityIssueReason",Static);throw new Exception("SRP shader incompatible "+code+": "+reason.Invoke(null,new object[]{shader,0,code}));}
        if(changed>0)throw new Exception("Terrain appearance changed beyond2 RGB levels: "+changed+" pixels, max "+maxDelta);
        if(!GraphicsSettings.useScriptableRenderPipelineBatching)throw new Exception("SRP Batcher disabled in pipeline");
        string report="PASS shader SRP compatibility code0;4 distinct tile materials; no renderer property blocks; independent tile textures;20 settled repeats: zero tile/material or scene-buffer uploads; scene change:1 shared upload/0 tile updates.\nPixel comparison >2 levels="+changed+", maxDelta="+maxDelta+".\nEditor SRP draw counter baseline="+oldDraws+", compatible="+newDraws+" (not FPS).\n";
        File.WriteAllText(Path.Combine(output,"PASS.txt"),report);Debug.Log(report);
        // UnityStats describes completed frames, not the synchronous Camera.Render
        // above. Give each binding path its own short, isolated Editor frames.
        finish=()=>
        {
            int frame=0,baseline=0,batched=0;EditorApplication.CallbackFunction tick=null;
            tick=()=>
            {
                try
                {
                    int Read(string name){var p=typeof(UnityStats).GetProperty(name,Static);return p==null?-1:(int)p.GetValue(null);}
                    if(frame==0)for(int i=0;i<4;i++){renderers[i].sharedMaterial=legacyMaterials[i<2?0:1];var block=new MaterialPropertyBlock();block.SetFloat("_CoverageTileSize",4);renderers[i].SetPropertyBlock(block);}
                    if(frame==5){baseline=Read("srpBatcherDrawCalls");for(int i=0;i<4;i++){renderers[i].SetPropertyBlock(null);renderers[i].sharedMaterial=materials[i];}}
                    if(frame==10)
                    {
                        batched=Read("srpBatcherDrawCalls");string counters="Completed-frame Editor draws: baseline SRP="+baseline+", new SRP="+batched+", hybrid="+Read("hybridBatcherDrawCalls")+", standard="+Read("standardDrawCalls")+".\n";
                        File.AppendAllText(Path.Combine(output,"PASS.txt"),counters);Debug.Log(counters);EditorApplication.update-=tick;cleanup();EditorApplication.Exit(0);return;
                    }
                    camera.Render();frame++;EditorApplication.QueuePlayerLoopUpdate();
                }
                catch(Exception error){EditorApplication.update-=tick;cleanup();Debug.LogException(error);EditorApplication.Exit(1);}
            };
            EditorApplication.update+=tick;EditorApplication.QueuePlayerLoopUpdate();
        };
        // No scene, project settings or owner state is saved.
    }
}
