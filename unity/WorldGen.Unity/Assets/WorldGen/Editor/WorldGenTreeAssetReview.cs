using System;
using System.IO;
using System.Collections.Generic;
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
            var command=Environment.GetCommandLineArgs();int outputAt=Array.IndexOf(command,"--tree-review-output");
            string folder=outputAt>=0?Path.GetFullPath(command[outputAt+1]):Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/tree-baked-assets"));Directory.CreateDirectory(folder);
            CheckSurfaceSupport(folder);
            CheckBarkSupport(folder);
            var codes=new List<int>{0x02345600,0x12345600,0x22345600,0x32345600,0x42345600,0x52345600,0x02745600,0x02b45600};
            var needles=Enumerable.Range(0,128).Select(i=>0x10000000|(i<<8)).OrderBy(c=>new TreeMorphology(c).Openness).ToArray();
            codes.Add(needles[0]);codes.Add(needles[64]);codes.Add(needles[127]);
            foreach(int code in codes)
            {
                var timer=System.Diagnostics.Stopwatch.StartNew();
                var near=(Mesh)generator.GetMethod("Build").Invoke(null,new object[]{ScatterKind.GeneratedTree,0,code});
                var middle=(Mesh)generator.GetMethod("Build").Invoke(null,new object[]{ScatterKind.GeneratedTree,1,code});
                foreach(var mesh in new[]{near,middle})
                {
                    if(mesh.vertexCount<100||mesh.triangles.Length<300||mesh.triangles.Any(i=>i<0||i>=mesh.vertexCount))throw new Exception("Invalid tree topology");
                    if(mesh.vertices.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)))throw new Exception("Non-finite tree vertex");
                    if(mesh.bounds.min.y<-.01||mesh.bounds.max.y>1.3||mesh.bounds.size.x>1.4||mesh.bounds.size.z>1.4)throw new Exception("Tree exceeds existing conservative bounds: "+code+" / "+mesh.name+" / "+mesh.bounds);
                    CheckWoodSeams(mesh);
                }
                var morphology=new TreeMorphology(code);
                var source=Activator.CreateInstance(baker.GetNestedType("Source"),near,(int)morphology.LeafShape,LocalTreeTextures.BarkLayer(morphology));
                object result=baker.GetMethod("Bake").Invoke(null,new[]{source,(object)CancellationToken.None});
                var pixels=(Color32[][])result.GetType().GetField("Colours").GetValue(result);
                if(pixels.Length!=16||pixels.Any(p=>p.Count(c=>c.a>=102)<100))throw new Exception("Missing tree bake views/coverage");
                var box=near.bounds;float sphere=box.extents.magnitude*1.025f,areaSum=0;
                for(int view=0;view<16;view++)
                {
                    var direction=(Vector3)baker.GetMethod("Direction").Invoke(null,new object[]{view});var right=Vector3.Cross(Vector3.up,direction);if(right.sqrMagnitude<.0001f)right=Vector3.right;right.Normalize();var up=Vector3.Cross(direction,right).normalized;
                    float Projection(Vector3 axis)=>Mathf.Abs(axis.x)*box.extents.x+Mathf.Abs(axis.y)*box.extents.y+Mathf.Abs(axis.z)*box.extents.z;
                    float halfWidth=Mathf.Min(sphere,Projection(right)*1.025f+4*sphere/128),halfHeight=Mathf.Min(sphere,Projection(up)*1.025f+4*sphere/128);
                    foreach(var vertex in near.vertices){var relative=vertex-box.center;if(Mathf.Abs(Vector3.Dot(relative,right))>halfWidth+.00001f||Mathf.Abs(Vector3.Dot(relative,up))>halfHeight+.00001f)throw new Exception("Tree vertex outside trimmed billboard support");}
                    areaSum+=halfWidth*halfHeight/(sphere*sphere);
                }
                File.AppendAllText(Path.Combine(folder,"quad-area.txt"),code+" mean-area-ratio="+(areaSum/16).ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+"\n");
                // A CPU contact sheet makes view orientation/silhouette inspectable.
                var atlas=new Texture2D(512,512,TextureFormat.RGBA32,false);
                for(int i=0;i<16;i++)atlas.SetPixels32((i%4)*128,(i/4)*128,128,128,pixels[i]);atlas.Apply();
                File.WriteAllBytes(Path.Combine(folder,code+"-views.png"),atlas.EncodeToPNG());
                if(!Environment.GetCommandLineArgs().Contains("-nographics"))Capture(folder,code,near,result,texture);
                string line=code+" form="+morphology.Form+" bark="+morphology.BarkPattern+" openness="+morphology.Openness.ToString("F3")+" near="+near.triangles.Length/3+" mid="+middle.triangles.Length/3+" views=16 ms="+timer.Elapsed.TotalMilliseconds.ToString("F1");
                File.AppendAllText(Path.Combine(folder,"results.txt"),line+"\n");Debug.Log("TREE ASSET PASS "+line);
                UnityEngine.Object.DestroyImmediate(near);UnityEngine.Object.DestroyImmediate(middle);UnityEngine.Object.DestroyImmediate(atlas);
            }
            Debug.Log("TREE ASSET REVIEW PASSED");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    [Serializable] sealed class SurfaceSupport
    {
        public string[] Formats;
        public long ShootGpuBytes,BarkGpuBytes,PreviousShootGpuBytes,CpuPixelBytes;
        public LayerSupport[] Layers;
    }
    static void CheckWoodSeams(Mesh mesh)
    {
        var vertices=mesh.vertices;var uv=mesh.uv;var mask=mesh.uv2;var normals=mesh.normals;var tangents=mesh.tangents;
        var groups=new Dictionary<Vector3,List<int>>();int seams=0;
        for(int i=0;i<vertices.Length;i++)if(mask[i].y<.5f)
        {
            if(!groups.TryGetValue(vertices[i],out var indices)){indices=new List<int>();groups.Add(vertices[i],indices);}
            foreach(int j in indices)if(Mathf.Abs(uv[i].x-uv[j].x)>.9f&&Mathf.Abs(uv[i].y-uv[j].y)<.00001f)
            {
                seams++;
                if(Vector3.Dot(normals[i],normals[j])<.999f||Vector3.Dot((Vector3)tangents[i],(Vector3)tangents[j])<.99f||tangents[i].w!=tangents[j].w)
                    throw new Exception("Visible normal/tangent split at wrapped wood seam: "+mesh.name+" / "+i+" / "+j);
            }
            indices.Add(i);
        }
        if(mask.Any(m=>m.y<.5f)&&seams<2)throw new Exception("Wood seam check has no wrapped-ring pairs: "+mesh.name);
    }
    static void CheckBarkSupport(string folder)
    {
        int checks=0;var flat=new Color32[16];var vector=new Color32[16];
        for(int i=0;i<16;i++){flat[i]=i%2==0?new Color32(0,0,0,255):new Color32(255,255,255,255);vector[i]=new Color32(128,128,255,255);}
        var prepared=LocalTreeTextures.PrepareBark(new[]{flat},new[]{vector},4);
        if(prepared.Colours[0][2][0].r<180||prepared.Colours[0][2][0].r>194)throw new Exception("Bark mips must average light in linear space");checks++;
        for(int layer=0;layer<5;layer++)
        {
            var pixels=LocalTreeTextures.BarkColour[layer];var normals=LocalTreeTextures.BarkNormals[layer];const int size=LocalTreeTextures.Size;
            for(int i=0;i<size;i++)
            {
                if(!pixels[i].Equals(pixels[(size-1)*size+i])||!pixels[i*size].Equals(pixels[i*size+size-1]))throw new Exception("Published CPU bark bank has an open repeat boundary");checks+=2;
            }
            foreach(var p in normals)
            {
                var n=new Vector3(p.r/127.5f-1,p.g/127.5f-1,p.b/127.5f-1);
                if(Mathf.Abs(n.magnitude-1)>.008f)throw new Exception("Published bark normal is not unit length");checks++;
            }
        }
        File.WriteAllText(Path.Combine(folder,"bark-support.txt"),"PASS "+checks+" published bark boundary, unit-normal and linear-mip checks; existing5x512 BC7 colour/normal arrays.");
    }
    [Serializable] sealed class LayerSupport
    {
        public int Layer,VisiblePixels,ClippedPixels;
        public Vector4 Hull;
        public float CardArea;
    }
    static void CheckSurfaceSupport(string folder)
    {
        var edgeColour=new Color32[16];var edgeNormal=new Color32[16];
        edgeColour[5]=new Color32(70,150,30,255);edgeNormal[5]=new Color32(128,140,240,170);
        edgeColour[6]=new Color32(120,175,75,127);edgeNormal[6]=new Color32(160,128,248,175);
        var originalColour=(Color32[])edgeColour.Clone();var originalNormal=(Color32[])edgeNormal.Clone();
        var edge=LocalTreeTextures.Prepare(new[]{edgeColour},new[]{edgeNormal},4);
        for(int i=0;i<16;i++)
        {
            if(edge.Colours[0][0][i].a!=originalColour[i].a)throw new Exception("Padding changes alpha silhouette");
            if(originalColour[i].a>0&&(!edge.Colours[0][0][i].Equals(originalColour[i])||!edge.Normals[0][0][i].Equals(originalNormal[i])))throw new Exception("Padding changes a real surface texel");
            if(edge.Normals[0][0][i].a<170||edge.Colours[0][0][i].g<150)throw new Exception("Padding leaves an empty normal/depth fringe");
        }
        for(int mip=0;mip<edge.Colours[0].Length;mip++)for(int i=0;i<edge.Colours[0][mip].Length;i++)
            if(edge.Normals[0][mip][i].a<170||edge.Normals[0][mip][i].a>175||edge.Colours[0][mip][i].g<150)throw new Exception("A lower mip loses padded surface attributes");
        Debug.Log("TREE EDGE PADDING PASS: alpha and real surfaces unchanged; empty texels retain valid colour/normal/depth.");
        var names=new[]{"shoot-albedo","shoot-normal","bark-albedo","bark-normal"};
        var report=new SurfaceSupport{Formats=new string[4],Layers=new LayerSupport[20],CpuPixelBytes=74L*256*256*4};
        long Bytes(int size,int layers,bool compressed)
        {long bytes=0;for(int width=size;width>0;width/=2)bytes+=(compressed?Math.Max(1,(width+3)/4)*Math.Max(1,(width+3)/4)*16L:width*width*4L)*layers;return bytes;}
        for(int i=0;i<4;i++)
        {
            var array=Resources.Load<Texture2DArray>("TreeSurfaceArtV1/"+names[i]);
            int layers=i<2?32:5;
            if(array==null||array.width!=512||array.height!=512||array.depth!=layers||array.mipmapCount!=10||array.format!=TextureFormat.BC7)throw new Exception("Tree GPU array contract "+names[i]);
            if(array.wrapMode!=(i<2?TextureWrapMode.Clamp:TextureWrapMode.Repeat))throw new Exception("Tree array wrapping "+names[i]);
            report.Formats[i]=array.graphicsFormat.ToString();
            if(i<2)report.ShootGpuBytes+=Bytes(512,layers,true);else report.BarkGpuBytes+=Bytes(512,layers,true);
        }
        report.PreviousShootGpuBytes=Bytes(256,32,false)*2;
        // Validate the actual runtime hull against higher-resolution authored
        // texels, rather than checking it against its own smaller input pixels.
        for(int layer=0;layer<20;layer++)
        {
            var hull=LocalTreeTextures.ShootHull(layer/4);var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                ImageConversion.LoadImage(texture,File.ReadAllBytes("Assets/WorldGen/Editor/TreeSurfaceArtV1/shoot-"+layer.ToString("D2")+"-albedo.png"));
                var pixels=texture.GetPixels32();var row=new LayerSupport{Layer=layer,Hull=hull,CardArea=(hull.y-hull.x+hull.w-hull.z)*.5f};
                for(int i=0;i<pixels.Length;i++)if(pixels[i].a>=102)
                {
                    row.VisiblePixels++;float u=(i%512+.5f)/512,v=(i/512+.5f)/512;
                    if(u<Mathf.Lerp(hull.x,hull.z,v)||u>Mathf.Lerp(hull.y,hull.w,v))row.ClippedPixels++;
                }
                if(row.ClippedPixels!=0||row.VisiblePixels<1000)throw new Exception("Tree hull cuts visible photo texels: "+layer+" / "+row.ClippedPixels);
                report.Layers[layer]=row;
            }
            finally{UnityEngine.Object.DestroyImmediate(texture);}
        }
        File.WriteAllText(Path.Combine(folder,"surface-support.json"),JsonUtility.ToJson(report,true));
        Debug.Log("TREE SURFACE SUPPORT PASS: all20 cutout layers preserve512 source alpha; same mesh counts, compressed GPU support.");
    }
    static void Capture(string folder,int code,Mesh mesh,object result,Type textures)
    {
        var args=new object[]{result.GetType().GetField("Colours").GetValue(result),result.GetType().GetField("Normals").GetValue(result),128,"Review tree",null,null};
        textures.GetMethods().Single(m=>m.Name=="Upload"&&m.GetParameters().Length==6).Invoke(null,args);var colours=(Texture2DArray)args[4];var normals=(Texture2DArray)args[5];
        var leaves=new object[]{null,null};textures.GetMethod("Create").Invoke(null,leaves);
        var morphology=new TreeMorphology(code);var shader=Shader.Find("WorldGen/TreeSurface2");if(shader==null||!shader.isSupported)throw new Exception("Prototype shader unsupported");
        var material=new Material(shader){enableInstancing=true};material.SetTexture("_TreeLeaves",(Texture)leaves[0]);material.SetTexture("_TreeLeafNormals",(Texture)leaves[1]);material.SetFloat("_TreeLeafShape",(int)morphology.LeafShape);material.SetFloat("_TreeBarkPattern",LocalTreeTextures.BarkLayer(morphology));textures.GetMethod("BindBark").Invoke(null,new object[]{material});material.SetFloat("_Wind",0);material.SetFloat("_FogDistance",10000);
        material.SetColor("_AmbientTint",new Color(.16f,.19f,.23f));var far=new Material(material);far.SetFloat("_Impostor",1);far.SetTexture("_TreeColourViews",colours);far.SetTexture("_TreeNormalViews",normals);
        var center=(Vector3)result.GetType().GetField("Center").GetValue(result);float radius=(float)result.GetType().GetField("Radius").GetValue(result);far.SetVector("_ImpostorBounds",new Vector4(center.x,center.y,center.z,radius));
        var extents=(Vector3)result.GetType().GetField("Extents").GetValue(result);far.SetVector("_ImpostorExtents",new Vector4(extents.x,extents.y,extents.z,4*radius/128));
        var quad=new Mesh();quad.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0)};quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};quad.triangles=new[]{0,1,2,1,3,2};quad.bounds=new Bounds(center,Vector3.one*radius*2.1f);
        var cameraRoot=new GameObject("Tree asset review camera");var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.42f,.53f,.67f);camera.cullingMask=1<<30;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.fieldOfView=45;
        cameraRoot.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        var target=new RenderTexture(1280,720,24);target.Create();camera.targetTexture=target;camera.aspect=1280f/720;
        var lightRoot=new GameObject("Tree review sunlight");var light=lightRoot.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2.3f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(48,-35,0);RenderSettings.sun=light;
        var properties=new MaterialPropertyBlock();properties.SetVectorArray("_Tint",new[]{Vector4.one});properties.SetVectorArray("_LodTransition",new[]{new Vector4(0,1,0,0)});
        Shader.SetGlobalVector("_AirGeometry",new Vector4(6371000,0,0,0));Shader.SetGlobalFloat("_AirObserverM",0);Shader.SetGlobalFloat("_AirHaze",0);
        foreach(string shot in new[]{"0","45","115","canopy","bark"})
        {
            float yaw=shot=="canopy"||shot=="bark"?45:float.Parse(shot,System.Globalization.CultureInfo.InvariantCulture);
            camera.transform.position=new Vector3(0,.55f,-3);camera.transform.rotation=Quaternion.identity;
            if(shot=="canopy"||shot=="bark")
            {
                var focus=new Vector3(-.58f,shot=="bark"?.17f:.72f,0);
                camera.transform.position=focus+new Vector3(0,.015f,shot=="bark"?-.12f:-.42f);camera.transform.LookAt(focus);
            }
            var a=Matrix4x4.TRS(new Vector3(-.58f,0,0),Quaternion.Euler(0,yaw,0),Vector3.one);var b=Matrix4x4.TRS(new Vector3(.58f,0,0),Quaternion.Euler(0,yaw,0),Vector3.one);
            var parameters=new RenderParams(material){camera=camera,layer=30,worldBounds=new Bounds(Vector3.up*.5f,Vector3.one*5),matProps=properties,shadowCastingMode=ShadowCastingMode.Off};
            Graphics.RenderMeshInstanced(in parameters,mesh,0,new[]{a},1);parameters.material=far;Graphics.RenderMeshInstanced(in parameters,quad,0,new[]{b},1);camera.Render();
            var prior=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();RenderTexture.active=prior;
            var data=pixels.GetPixels32();var bg=data[0];int changed=data.Count(p=>Math.Abs(p.r-bg.r)+Math.Abs(p.g-bg.g)+Math.Abs(p.b-bg.b)>15);if(changed<1000)throw new Exception("Tree review render blank");
            File.WriteAllBytes(Path.Combine(folder,code+"-mesh-vs-bake-"+shot+".png"),pixels.EncodeToPNG());UnityEngine.Object.DestroyImmediate(pixels);
            if(shot=="0"||shot=="45"||shot=="115")
            {
                far.SetFloat("_TreeTightQuad",0);parameters.material=material;Graphics.RenderMeshInstanced(in parameters,mesh,0,new[]{a},1);parameters.material=far;Graphics.RenderMeshInstanced(in parameters,quad,0,new[]{b},1);camera.Render();
                prior=RenderTexture.active;RenderTexture.active=target;var literal=new Texture2D(1280,720,TextureFormat.RGB24,false);literal.ReadPixels(new Rect(0,0,1280,720),0,0);literal.Apply();RenderTexture.active=prior;var wide=literal.GetPixels32();int delta=0;
                int significant=0;long colourError=0;
                for(int i=0;i<data.Length;i++){int dr=Math.Abs(data[i].r-wide[i].r),dg=Math.Abs(data[i].g-wide[i].g),db=Math.Abs(data[i].b-wide[i].b),difference=Math.Max(dr,Math.Max(dg,db));delta=Math.Max(delta,difference);if(difference>2)significant++;colourError+=dr+dg+db;}
                // Different quad vertices change floating-point interpolation.
                // Permit isolated alpha-threshold flips, not a missing contour.
                double mean=colourError/(data.Length*3d);
                if(significant>64||mean>.02){File.WriteAllBytes(Path.Combine(folder,code+"-wide-quad-"+shot+".png"),literal.EncodeToPNG());throw new Exception("Trimmed billboard changed pixels "+code+" / "+shot+" / "+delta+" / count "+significant+" / mean "+mean);}
                File.AppendAllText(Path.Combine(folder,"quad-pixels.txt"),code+" yaw="+shot+" delta="+delta+" significant="+significant+" mean="+mean.ToString("F8",System.Globalization.CultureInfo.InvariantCulture)+"\n");far.SetFloat("_TreeTightQuad",1);UnityEngine.Object.DestroyImmediate(literal);
            }
        }
        foreach(var obj in new UnityEngine.Object[]{cameraRoot,lightRoot,target,material,far,quad,colours,normals})UnityEngine.Object.DestroyImmediate(obj);
    }
}
