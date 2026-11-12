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
    static string CheckForestStandSupport()
    {
        int checks=0;
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("Forest stand support: "+text);checks++;}
        var key=new WorldGen.Presentation.TerrainTileKey(0,0,1,.5);const int n=16,side=n+1;
        var tile=new Tile{Segments=n,Heights=new float[side*side],ForestCover=new Unity.Collections.NativeArray<float>(side*side*8,Unity.Collections.Allocator.Temp)};
        var neighbour=new Tile{Segments=n,Heights=new float[side*side],ForestCover=new Unity.Collections.NativeArray<float>(side*side*8,Unity.Collections.Allocator.Temp)};
        var fine=new Tile{Segments=n,Heights=new float[side*side],ForestCover=new Unity.Collections.NativeArray<float>(side*side*8,Unity.Collections.Allocator.Temp)};
        var host=new GameObject("Forest stand owner support fixture");var owner=host.AddComponent<LocalTerrainExplorer>();owner.enabled=false;owner.explorationSettings.HorizontalScale=.5;
        try
        {
            void Plane(float sx=0,float sz=0)
            {for(int z=0;z<side;z++)for(int x=0;x<side;x++){tile.Heights[z*side+x]=(float)(10+sx*x*key.SizeM/n+sz*z*key.SizeM/n);tile.ForestCover[(z*side+x)*8+3]=1;}}
            ForestRoot Root(bool stand,float x=512,float z=512)=>new ForestRoot{Position=new Vector3(x,0,z),Width=stand?33:33/LocalTreePrototype.StandSpan,Height=15,Padding=stand?3:2,Face=8};
            void Pair(bool supported,double y,float x=512,float z=512,LocalTerrainExplorer terrain=null,GroundPublication publication=null)
            {
                var stand=Root(true,x,z);var single=Root(false,x,z);
                ApplyForestRootGround(ref stand,y,key.Level,tile,key,terrain,publication);ApplyForestRootGround(ref single,y,key.Level,tile,key,terrain,publication);
                Check((stand.Padding>0)==supported,"stand selection");Check((single.Padding>0)!=supported,"exactly one prepared representation draws");
            }
            Plane();Pair(true,10);
            Plane(.15f,-.07f);Pair(true,10+(.15-.07)*512);
            var inclined=Root(true);ApplyForestRootGround(ref inclined,10+(.15-.07)*512,key.Level,tile,key);
            Check(Math.Abs(inclined.SlopeX-.15)<.0001&&Math.Abs(inclined.SlopeZ+.07)<.0001,"stand follows the supporting plane");
            Plane();tile.Heights[8*side+8]=18;Pair(false,18);
            Plane();Pair(false,10,x:5);
            tile.HasWater=tile.WaterBoundsKnown=true;tile.WaterBounds=new Bounds(new Vector3(512,10,512),new Vector3(2,1,40));Pair(false,10);
            var tidal=Root(true);tidal.Face|=16u;ApplyForestRootGround(ref tidal,10,key.Level,tile,key);Check(tidal.Padding>0,"mapped shallow tidal forest retains its stand");
            tile.ForestCover[(8*side+8)*8+3]=0;ApplyForestRootGround(ref tidal,10,key.Level,tile,key);Check(tidal.Padding<0,"tidal exception retains canonical habitat admission");Plane();
            tile.WaterBounds=new Bounds(new Vector3(512,0,512),new Vector3(2,1,40));Pair(true,10);tile.HasWater=false;
            for(int i=0;i<side*side;i++){neighbour.Heights[i]=fine.Heights[i]=10;neighbour.ForestCover[i*8+3]=fine.ForestCover[i*8+3]=1;}
            var adjacent=new WorldGen.Presentation.TerrainTileKey(1,0,1,.5);owner.tiles.Add(key,tile);owner.tiles.Add(adjacent,neighbour);
            Pair(true,10,x:(float)key.SizeM-5,terrain:owner);
            owner.tiles.Remove(adjacent);Pair(false,10,x:(float)key.SizeM-5,terrain:owner);owner.tiles.Add(adjacent,neighbour);
            var child=new WorldGen.Presentation.TerrainTileKey(1,1,0,.5);tile.CoverageChildren=new System.Collections.Generic.List<WorldGen.Presentation.TerrainTileKey>{child};owner.tiles.Add(child,fine);
            Pair(true,10,terrain:owner);
            for(int i=0;i<side*side;i++)fine.ForestCover[i*8+3]=0;
            Pair(false,10,terrain:owner);
            var retirement=new GroundPublication{Key=child,Product=fine,Retiring=true};Pair(true,10,terrain:owner,publication:retirement);
            owner.tiles.Remove(child);
            var incoming=new GroundPublication{Key=child,Product=fine};Pair(false,10,terrain:owner,publication:incoming);
            return checks+" flat/inclined/curved/edge/water/cross-owner/finer-owner/publication stand controls pass\n";
        }
        finally{owner.tiles.Clear();tile.ForestCover.Dispose();neighbour.ForestCover.Dispose();fine.ForestCover.Dispose();DestroyImmediate(host);}
    }
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
        var compute=Resources.Load<ComputeShader>("WorldGenForestCull");var material=new Material(Resources.Load<Shader>("WorldGenForest"));LocalTreeTextures.BindViews(material);material.SetColor("_AmbientTint",new Color(.25f,.25f,.25f));material.SetFloat("_FogDistance",10000);
        string supportReport=CheckForestStandSupport();
        var roots=new GraphicsBuffer(GraphicsBuffer.Target.Structured,2,64);var visible=new GraphicsBuffer(GraphicsBuffer.Target.Append,2,4);var args=new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,1,GraphicsBuffer.IndirectDrawArgs.size);args.SetData(new[]{new GraphicsBuffer.IndirectDrawArgs{vertexCountPerInstance=6}});
        var flags=new GraphicsBuffer(GraphicsBuffer.Target.Structured,1,4);flags.SetData(new[]{1});var hash=new GraphicsBuffer(GraphicsBuffer.Target.Structured,16,16);hash.SetData(new ForestHash[16]);
        var birth=new GraphicsBuffer(GraphicsBuffer.Target.Structured,1,4);birth.SetData(new[]{0f});compute.SetBuffer(0,"_ForestBirth",birth);
        var data=new[]{new ForestRoot{Position=new Vector3(-.58f,0,0),Width=.35f,Height=1,Yaw=0,Tint=Vector3.one},new ForestRoot{Position=new Vector3(.58f,0,0),Width=.35f,Height=1,Yaw=.7f,Tint=Vector3.one}};roots.SetData(data);
        var planes=GeometryUtility.CalculateFrustumPlanes(camera);var planeVectors=new Vector4[6];for(int i=0;i<6;i++)planeVectors[i]=new Vector4(planes[i].normal.x,planes[i].normal.y,planes[i].normal.z,planes[i].distance);
        compute.SetVectorArray("_Planes",planeVectors);compute.SetVector("_Eye",camera.transform.position);compute.SetVector("_Chart",new Vector4(0,0,0,1));compute.SetVector("_Ranges",new Vector4(0,1,100,15));compute.SetInt("_RootCount",2);compute.SetBuffer(0,"_Roots",roots);compute.SetBuffer(0,"_Visible",visible);compute.SetBuffer(0,"_Fields",flags);compute.SetBuffer(0,"_Tiles",hash);
        uint Count(){visible.SetCounterValue(0);compute.Dispatch(0,1,1,1);GraphicsBuffer.CopyCount(visible,args,4);var counts=new GraphicsBuffer.IndirectDrawArgs[1];args.GetData(counts);return counts[0].instanceCount;}
        if(Count()!=2)throw new Exception("Forest GPU culler missed visible roots");
        flags.SetData(new[]{0});if(Count()!=0)throw new Exception("Disabled forest fields draw");flags.SetData(new[]{1});
        compute.SetVector("_Ranges",new Vector4(10,11,100,15));if(Count()!=0)throw new Exception("Near forest rejection failed");
        flags.SetData(new[]{3});if(Count()!=2)throw new Exception("Prepared forest fallback disappeared before local detail was ready");flags.SetData(new[]{1});compute.SetVector("_Ranges",new Vector4(0,1,100,15));
        var table=new ForestHash[16];for(int x=-1;x<=0;x++){int slot=(int)(ForestKeyHash(x,0,0)&15);while(table[slot].State!=0)slot=(slot+1)&15;table[slot]=new ForestHash{X=x,Z=0,Level=0,State=9};}hash.SetData(table);
        data[0].Field=data[1].Field=1u<<16;roots.SetData(data);if(Count()!=0)throw new Exception("Coarse forest not suppressed by ready finer fields");
        data[0].Face=data[1].Face=1;roots.SetData(data);if(Count()!=2)throw new Exception("Different cube face incorrectly owns these trees");
        hash.SetData(new ForestHash[16]);data[0].Field=data[1].Field=0;data[0].Face=data[1].Face=0;roots.SetData(data);
        compute.SetVector("_Chart",Vector4.zero);if(Count()!=0)throw new Exception("Zero tree density must suppress distant trees too");compute.SetVector("_Chart",new Vector4(0,0,0,1));
        var props=new MaterialPropertyBlock();props.SetBuffer("_Roots",roots);props.SetBuffer("_Visible",visible);props.SetBuffer("_Fields",flags);props.SetBuffer("_ForestBirth",birth);props.SetBuffer("_Tiles",hash);props.SetInt("_ForestHashMask",15);props.SetVector("_ForestChart",Vector4.zero);props.SetVector("_ForestRange",new Vector4(0,1,0,0));
        string report=supportReport+"GPU admission/disable/near-range/finer-field ownership pass\n";
        foreach(int code in new[]{0x02345600,0x12345600})
        {
            var morphology=new TreeMorphology(code);
            var mesh=LocalScatterMeshes.Build(ScatterKind.GeneratedTree,0,code);var bake=LocalTreeImpostor.Bake(new LocalTreeImpostor.Source(mesh,(int)morphology.LeafShape,LocalTreeTextures.BarkLayer(morphology)),CancellationToken.None);
            LocalTreeTextures.Upload(bake.Colours,bake.Normals,128,"Forest check",out var colour,out var normals);material.SetTexture("_TreeColourViews",colour);material.SetTexture("_TreeNormalViews",normals);material.SetVector("_ImpostorBounds",new Vector4(bake.Center.x,bake.Center.y,bake.Center.z,bake.Radius));
            material.SetVector("_ImpostorExtents",LocalTreeImpostor.QuadExtents(bake));
            if(Count()!=2)throw new Exception("Forest culler failed restored draw");var parameters=new RenderParams(material){camera=camera,layer=30,worldBounds=new Bounds(Vector3.zero,Vector3.one*10),matProps=props,shadowCastingMode=ShadowCastingMode.Off};Graphics.RenderPrimitivesIndirect(in parameters,MeshTopology.Triangles,args,1);camera.Render();
            var prior=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();RenderTexture.active=prior;var pixels=image.GetPixels32();var bg=pixels[0];int left=0,right=0;
            for(int y=0;y<720;y++)for(int x=0;x<1280;x++){var p=pixels[y*1280+x];if(Math.Abs(p.r-bg.r)+Math.Abs(p.g-bg.g)+Math.Abs(p.b-bg.b)>20){if(x<640)left++;else right++;}}
            File.WriteAllBytes(Path.Combine(output,code+"-forest.png"),image.EncodeToPNG());if(left<1000||right<1000)throw new Exception("Packaged forest stands missing "+left+"/"+right);
            material.SetFloat("_TreeEarlyAlpha",0);Graphics.RenderPrimitivesIndirect(in parameters,MeshTopology.Triangles,args,1);camera.Render();prior=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();RenderTexture.active=prior;var literal=image.GetPixels32();int delta=0;
            for(int i=0;i<pixels.Length;i++)delta=Math.Max(delta,Math.Max(Math.Abs(pixels[i].r-literal[i].r),Math.Max(Math.Abs(pixels[i].g-literal[i].g),Math.Abs(pixels[i].b-literal[i].b))));
            if(delta>1)throw new Exception("Forest quad-alpha early rejection changed pixels "+delta);report+="early alpha max RGB delta "+delta+"\n";material.SetFloat("_TreeEarlyAlpha",1);
            material.SetFloat("_TreeTightQuad",0);Graphics.RenderPrimitivesIndirect(in parameters,MeshTopology.Triangles,args,1);camera.Render();prior=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();RenderTexture.active=prior;
            var wide=image.GetPixels32();int quadDelta=0,significant=0;long colourError=0;for(int i=0;i<pixels.Length;i++){int dr=Math.Abs(pixels[i].r-wide[i].r),dg=Math.Abs(pixels[i].g-wide[i].g),db=Math.Abs(pixels[i].b-wide[i].b),difference=Math.Max(dr,Math.Max(dg,db));quadDelta=Math.Max(quadDelta,difference);if(difference>2)significant++;colourError+=dr+dg+db;}
            double mean=colourError/(pixels.Length*3d);
            if(significant>64||mean>.02){File.WriteAllBytes(Path.Combine(output,code+"-wide-quad.png"),image.EncodeToPNG());throw new Exception("Trimmed forest quad changed pixels "+quadDelta+" / count "+significant+" / mean "+mean);}
            report+="tight quad max RGB delta "+quadDelta+", significant="+significant+", mean="+mean+"\n";material.SetFloat("_TreeTightQuad",1);
            report+=code+" pixels="+left+"/"+right+"\n";Destroy(image);Destroy(mesh);Destroy(colour);Destroy(normals);yield return null;
        }
        roots.Dispose();visible.Dispose();args.Dispose();flags.Dispose();hash.Dispose();birth.Dispose();File.WriteAllText(Path.Combine(output,"PASS.txt"),SystemInfo.graphicsDeviceName+"\n"+report);Debug.Log("PACKAGED FOREST SHADER PASS "+report);Application.Quit(0);
    }
}
