using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public static IEnumerator CheckGroundCover(string output)
    {
        Directory.CreateDirectory(output);
        Application.logMessageReceived+=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        var host=new GameObject("Ground cover fixture");var e=host.AddComponent<LocalTerrainExplorer>();e.enabled=false;e.closed=true;
        e.InitializeGroundCover();if(e.groundCoverMaterial==null)throw new Exception("Ground cover GPU path unavailable");
        var tile=new Tile{Segments=32,Cover=new float[33*33*16]};
        for(int z=0;z<33;z++)for(int x=0;x<33;x++)
        {
            int at=(z*33+x)*16;tile.Cover[at]=.31f;tile.Cover[at+1]=.44f;tile.Cover[at+2]=.16f;tile.Cover[at+3]=.92f;
            int form=x<13?0:x<20?1:x<26?5:6;tile.Cover[at+4+form]=.92f;tile.Cover[at+13]=x<13?1:x<20?2.3f:1;tile.Cover[at+14]=1;tile.Cover[at+15]=x*.0125f;
        }
        var key=new TerrainTileKey(0,0,0,.1);var gpu=e.PrepareGroundCover(key,tile,0,0);tile.Grass=gpu;
        yield return new WaitForSecondsRealtime(2.2f);
        var counts=new uint[1];gpu.Count.GetData(counts);
        if(counts[0]<1000)throw new Exception("No compact GPU roots");
        var cameraRoot=new GameObject("Cover check camera",typeof(Camera),typeof(UnityEngine.Rendering.Universal.UniversalAdditionalCameraData));
        var camera=cameraRoot.GetComponent<Camera>();camera.cullingMask=1<<30;camera.nearClipPlane=.08f;camera.farClipPlane=400;
        camera.transform.position=new Vector3(18,1.75f,5);camera.transform.LookAt(new Vector3(23,.7f,30));camera.backgroundColor=new Color(.5f,.65f,.8f);
        camera.clearFlags=CameraClearFlags.SolidColor;
        var sunRoot=new GameObject("Cover sunlight",typeof(Light));var sun=sunRoot.GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1;sun.transform.rotation=Quaternion.Euler(45,-30,0);RenderSettings.sun=sun;
        var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.layer=30;ground.transform.position=new Vector3(50,50*.4f/102.4f,50);ground.transform.localScale=Vector3.one*30;ground.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan(.4f/102.4f)*Mathf.Rad2Deg);
        var groundMaterial=new Material(Resources.Load<Shader>("WorldGenLocalTerrain"));groundMaterial.SetFloat("_Water",1);groundMaterial.SetFloat("_Daylight",1);ground.GetComponent<MeshRenderer>().sharedMaterial=groundMaterial;
        e.groundCoverMaterial.SetFloat("_FogDistance",10000);e.groundCoverMaterial.SetFloat("_Daylight",1);e.groundCoverMaterial.SetColor("_AmbientTint",new Color(.22f,.24f,.20f));
        var target=new RenderTexture(1280,800,24);camera.targetTexture=target;
        Color32[] Capture(bool cover,string name)
        {
            if(cover)
            {
                e.CullGroundCover(gpu,new Vector4(0,0,0,gpu.Born),camera);
                for(int lod=0;lod<3;lod++){var material=lod==2?e.groundCanopyMaterial:e.groundCoverMaterial;if(material==null)continue;gpu.Properties.SetInt("_CoverLod",lod);gpu.Properties.SetBuffer("_VisibleRoots",gpu.Visible[lod]);var rp=new RenderParams(material){camera=camera,layer=30,worldBounds=new Bounds(new Vector3(51,1,51),new Vector3(106,10,106)),matProps=gpu.Properties,shadowCastingMode=ShadowCastingMode.Off};Graphics.RenderPrimitivesIndirect(in rp,MeshTopology.Triangles,gpu.Args,1,lod);}
            }
            camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());var pixels=image.GetPixels32();Destroy(image);RenderTexture.active=old;return pixels;
        }
        var covered=Capture(true,"near");yield return null;var bare=Capture(false,"bare");int changed=0;
        for(int i=0;i<bare.Length;i++)if(Math.Abs(bare[i].r-covered[i].r)+Math.Abs(bare[i].g-covered[i].g)+Math.Abs(bare[i].b-covered[i].b)>12)changed++;
        if(changed<2000)throw new Exception("GPU grass not visible: "+changed);
        camera.transform.position=new Vector3(54,12,4);camera.transform.LookAt(new Vector3(54,0,48));yield return null;Capture(true,"overview");
        camera.transform.position=new Vector3(48,1.75f,20);camera.transform.LookAt(new Vector3(49,1.70f,60));yield return null;Capture(true,"tallgrass");
        File.WriteAllText(Path.Combine(output,"ground-cover-check.txt"),"PASS native GPU generation/indirect rendering: "+counts[0]+" compact roots; "+changed+" changed pixels; "+(gpu.Bytes/1048576d).ToString("F2")+"MiB fixture buffers. Not actual-world performance/art acceptance.");
        ReleaseGroundCover(tile);Application.Quit(0);
    }
}
