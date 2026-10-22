using System;
using System.Collections;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WorldGen.Core;

public sealed partial class WorldGenRuntime
{
    // Exercise the shipped Resources material and instanced variant, not the
    // Editor's unstripped shader. No world loads or user preference writes.
    IEnumerator CheckTreeShader(string output)
    {
        Directory.CreateDirectory(output);
        Application.logMessageReceived+=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        QualitySettings.vSyncCount=0;Application.targetFrameRate=30;
        yield return null;
        var template=Resources.Load<Material>("TreeSurfaceMaterial");
        if(template==null||!template.enableInstancing)throw new Exception("Tree instancing template unavailable");
        var material=new Material(template);LocalTreeTextures.Create(out var leaves,out var leafNormals);
        material.SetTexture("_TreeLeaves",leaves);material.SetTexture("_TreeLeafNormals",leafNormals);material.SetFloat("_Wind",0);
        material.SetColor("_AmbientTint",new Color(.25f,.25f,.25f));material.SetFloat("_FogDistance",10000);
        var root=new GameObject("Packaged tree shader fixture");var camera=root.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.42f,.53f,.67f);camera.cullingMask=1<<30;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.fieldOfView=45;camera.aspect=1280f/720;
        root.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        camera.transform.position=new Vector3(0,.55f,-3);camera.transform.rotation=Quaternion.identity;
        var target=new RenderTexture(1280,720,24);target.Create();camera.targetTexture=target;
        var lightRoot=new GameObject("Tree fixture sun");var light=lightRoot.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2.3f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(48,-35,0);RenderSettings.sun=light;
        var props=new MaterialPropertyBlock();props.SetVectorArray("_Tint",new[]{Vector4.one,new Vector4(.55f,.9f,.55f,1)});props.SetVectorArray("_LodTransition",new[]{new Vector4(0,1,0,0),new Vector4(0,1,0,0)});
        var matrices=new[]{Matrix4x4.TRS(new Vector3(-.58f,0,0),Quaternion.identity,Vector3.one),Matrix4x4.TRS(new Vector3(.58f,0,0),Quaternion.Euler(0,45,0),Vector3.one)};
        Shader.SetGlobalVector("_AirGeometry",new Vector4(6371000,0,0,0));Shader.SetGlobalFloat("_AirObserverM",0);Shader.SetGlobalFloat("_AirHaze",0);
        var report="";
        foreach(int code in new[]{0x02345600,0x12345600,0x22345600,0x32345600,0x52345600})
        {
            var mesh=LocalScatterMeshes.Build(ScatterKind.GeneratedTree,0,code);material.SetFloat("_TreeLeafShape",(int)new TreeMorphology(code).LeafShape);
            for(int stage=0;stage<2;stage++)
            {
                Mesh drawn=mesh;var drawMaterial=material;
                if(stage==1)
                {
                    var data=LocalTreeImpostor.Bake(new LocalTreeImpostor.Source(mesh,(int)new TreeMorphology(code).LeafShape),CancellationToken.None);
                    LocalTreeTextures.Upload(data.Colours,data.Normals,128,"Native check views",out var colour,out var normals);
                    drawMaterial=new Material(material);drawMaterial.SetFloat("_Impostor",1);drawMaterial.SetTexture("_TreeColourViews",colour);drawMaterial.SetTexture("_TreeNormalViews",normals);drawMaterial.SetVector("_ImpostorBounds",new Vector4(data.Center.x,data.Center.y,data.Center.z,data.Radius));
                    drawn=new Mesh();drawn.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0)};drawn.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};drawn.triangles=new[]{0,1,2,1,3,2};drawn.bounds=new Bounds(data.Center,Vector3.one*data.Radius*2.1f);
                }
                var parameters=new RenderParams(drawMaterial){camera=camera,layer=30,worldBounds=new Bounds(Vector3.up*.5f,Vector3.one*5),matProps=props,shadowCastingMode=ShadowCastingMode.Off};
                Graphics.RenderMeshInstanced(in parameters,drawn,0,matrices,2);camera.Render();
                var prior=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();RenderTexture.active=prior;
                var pixels=image.GetPixels32();var bg=pixels[0];int left=0,right=0;
                for(int y=0;y<720;y++)for(int x=0;x<1280;x++){var p=pixels[y*1280+x];if(Math.Abs(p.r-bg.r)+Math.Abs(p.g-bg.g)+Math.Abs(p.b-bg.b)>20){if(x<640)left++;else right++;}}
                File.WriteAllBytes(Path.Combine(output,code+"-"+stage+".png"),image.EncodeToPNG());
                if(left<1000||right<1000)throw new Exception("Packaged trees missing: "+code+" stage="+stage+" left="+left+" right="+right);
                report+=code+" stage="+stage+" left="+left+" right="+right+"\n";Destroy(image);
                yield return null;
            }
        }
        File.WriteAllText(Path.Combine(output,"PASS.txt"),SystemInfo.graphicsDeviceName+"\n"+report);Debug.Log("PACKAGED TREE SHADER PASS "+report);Application.Quit(0);
    }
}
