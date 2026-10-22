using System;
using System.Collections;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    public static IEnumerator CheckForestShader(string output)
    {
        Directory.CreateDirectory(output);
        Application.logMessageReceived+=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        QualitySettings.vSyncCount=0;Application.targetFrameRate=30;yield return null;
        LocalTreeTextures.Create(out var leaves,out var leafNormals);
        var root=new GameObject("Packaged forest shader check");var camera=root.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.42f,.53f,.67f);
        camera.cullingMask=1<<30;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.fieldOfView=45;camera.aspect=1280f/720;camera.transform.position=new Vector3(0,.55f,-3);camera.transform.rotation=Quaternion.identity;root.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        var target=new RenderTexture(1280,720,24);target.Create();camera.targetTexture=target;
        var sunRoot=new GameObject("Forest fixture sun");var sun=sunRoot.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2.3f;sun.cullingMask=1<<30;sun.transform.rotation=Quaternion.Euler(48,-35,0);RenderSettings.sun=sun;
        Shader.SetGlobalVector("_AirGeometry",new Vector4(6371000,0,0,0));Shader.SetGlobalFloat("_AirObserverM",0);Shader.SetGlobalFloat("_AirHaze",0);
        var compute=Resources.Load<ComputeShader>("WorldGenForestCull");var material=new Material(Resources.Load<Shader>("WorldGenForest"));material.SetColor("_AmbientTint",new Color(.25f,.25f,.25f));material.SetFloat("_FogDistance",10000);
        var roots=new GraphicsBuffer(GraphicsBuffer.Target.Structured,2,48);var visible=new GraphicsBuffer(GraphicsBuffer.Target.Append,2,4);var args=new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,1,GraphicsBuffer.IndirectDrawArgs.size);args.SetData(new[]{new GraphicsBuffer.IndirectDrawArgs{vertexCountPerInstance=6}});
        var flags=new GraphicsBuffer(GraphicsBuffer.Target.Structured,1,4);flags.SetData(new[]{1});var hash=new GraphicsBuffer(GraphicsBuffer.Target.Structured,16,16);hash.SetData(new ForestHash[16]);
        var data=new[]{new ForestRoot{Position=new Vector3(-.58f,0,0),Width=.35f,Height=1,Yaw=0,Tint=Vector3.one},new ForestRoot{Position=new Vector3(.58f,0,0),Width=.35f,Height=1,Yaw=.7f,Tint=Vector3.one}};roots.SetData(data);
        var planes=GeometryUtility.CalculateFrustumPlanes(camera);var planeVectors=new Vector4[6];for(int i=0;i<6;i++)planeVectors[i]=new Vector4(planes[i].normal.x,planes[i].normal.y,planes[i].normal.z,planes[i].distance);
        compute.SetVectorArray("_Planes",planeVectors);compute.SetVector("_Eye",camera.transform.position);compute.SetVector("_Chart",new Vector4(0,0,0,51.2f));compute.SetVector("_Ranges",new Vector4(0,1,100,15));compute.SetInt("_RootCount",2);compute.SetBuffer(0,"_Roots",roots);compute.SetBuffer(0,"_Visible",visible);compute.SetBuffer(0,"_Fields",flags);compute.SetBuffer(0,"_Tiles",hash);
        uint Count(){visible.SetCounterValue(0);compute.Dispatch(0,1,1,1);GraphicsBuffer.CopyCount(visible,args,4);var counts=new GraphicsBuffer.IndirectDrawArgs[1];args.GetData(counts);return counts[0].instanceCount;}
        if(Count()!=2)throw new Exception("Forest GPU culler missed visible roots");
        flags.SetData(new[]{0});if(Count()!=0)throw new Exception("Disabled forest fields draw");flags.SetData(new[]{1});
        compute.SetVector("_Ranges",new Vector4(10,11,100,15));if(Count()!=0)throw new Exception("Near forest rejection failed");compute.SetVector("_Ranges",new Vector4(0,1,100,15));
        var table=new ForestHash[16];for(int x=-1;x<=0;x++){int slot=(int)(ForestKeyHash(x,0,0)&15);while(table[slot].State!=0)slot=(slot+1)&15;table[slot]=new ForestHash{X=x,Z=0,Level=0,State=1};}hash.SetData(table);
        data[0].Field=data[1].Field=1u<<16;roots.SetData(data);if(Count()!=0)throw new Exception("Coarse forest not suppressed by ready finer fields");hash.SetData(new ForestHash[16]);data[0].Field=data[1].Field=0;roots.SetData(data);
        var props=new MaterialPropertyBlock();props.SetBuffer("_Roots",roots);props.SetBuffer("_Visible",visible);props.SetVector("_ForestChart",Vector4.zero);props.SetVector("_ForestRange",new Vector4(0,1,0,0));
        string report="GPU admission/disable/near-range/finer-field ownership pass\n";
        foreach(int code in new[]{0x02345600,0x12345600})
        {
            var mesh=LocalScatterMeshes.Build(ScatterKind.GeneratedTree,0,code);var bake=LocalTreeImpostor.Cluster(LocalTreeImpostor.Bake(new LocalTreeImpostor.Source(mesh,(int)new TreeMorphology(code).LeafShape),CancellationToken.None),code,CancellationToken.None);
            LocalTreeTextures.Upload(bake.Colours,bake.Normals,128,"Forest check",out var colour,out var normals);material.SetTexture("_TreeColourViews",colour);material.SetTexture("_TreeNormalViews",normals);material.SetVector("_ImpostorBounds",new Vector4(bake.Center.x,bake.Center.y,bake.Center.z,bake.Radius));
            if(Count()!=2)throw new Exception("Forest culler failed restored draw");var parameters=new RenderParams(material){camera=camera,layer=30,worldBounds=new Bounds(Vector3.zero,Vector3.one*10),matProps=props,shadowCastingMode=ShadowCastingMode.Off};Graphics.RenderPrimitivesIndirect(in parameters,MeshTopology.Triangles,args,1);camera.Render();
            var prior=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();RenderTexture.active=prior;var pixels=image.GetPixels32();var bg=pixels[0];int left=0,right=0;
            for(int y=0;y<720;y++)for(int x=0;x<1280;x++){var p=pixels[y*1280+x];if(Math.Abs(p.r-bg.r)+Math.Abs(p.g-bg.g)+Math.Abs(p.b-bg.b)>20){if(x<640)left++;else right++;}}
            File.WriteAllBytes(Path.Combine(output,code+"-forest.png"),image.EncodeToPNG());if(left<1000||right<1000)throw new Exception("Packaged forest stands missing "+left+"/"+right);
            report+=code+" pixels="+left+"/"+right+"\n";Destroy(image);Destroy(mesh);Destroy(colour);Destroy(normals);yield return null;
        }
        roots.Dispose();visible.Dispose();args.Dispose();flags.Dispose();hash.Dispose();File.WriteAllText(Path.Combine(output,"PASS.txt"),SystemInfo.graphicsDeviceName+"\n"+report);Debug.Log("PACKAGED FOREST SHADER PASS "+report);Application.Quit(0);
    }
}
