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
            ForestRoot Root(float x=512,float z=512)=>new ForestRoot{Position=new Vector3(x,0,z),Width=33,Height=15,Padding=3,Face=8};
            ForestGroundGrid Sample(ref ForestRoot group,double y,LocalTerrainExplorer terrain=null,GroundPublication publication=null)
            {ApplyForestRootGround(ref group,y,key.Level,tile,key,out var grid,terrain,publication);return grid;}
            float Height(ForestGroundGrid grid,int i)=>i<4?grid.A[i]:i<8?grid.B[i-4]:grid.C.x;
            Plane();var group=Root();var flat=Sample(ref group,10);
            Check(flat.C.y==511&&flat.C.z==511,"flat terrain admits every support sample");
            Check(flat.C.w==0,"flat support needs no extra vertical bounds");
            for(int i=0;i<9;i++)Check(Math.Abs(Height(flat,i)-10)<.0001,"flat absolute height");
            Plane(.15f,-.07f);group=Root();var inclined=Sample(ref group,10+(.15-.07)*512);
            Check(inclined.C.y==511&&inclined.C.z==511,"inclined terrain admits every support sample");
            for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)Check(Math.Abs(Height(inclined,(z+1)*3+x+1)-(10+.15*(512+x*24.75)-.07*(512+z*24.75)))<.001,"inclined sample follows analytical plane");
            group=Root();group.Padding=80;var packetSupport=Sample(ref group,10+(.15-.07)*512);
            Check(group.Padding==80&&packetSupport.C.z==511,"packet support span survives grounding independently of tree width");
            Check(Math.Abs(Height(packetSupport,0)-(10+.15*452-.07*452))<.001,"packet support uses its own 60m radius");
            Plane();tile.Heights[8*side+8]=18;group=Root();var curved=Sample(ref group,18);
            Check(group.Padding==3&&curved.C.y==511&&curved.C.z==511,"curved supported terrain retains the group");
            Check(Math.Abs(Height(curved,4)-18)<.001&&Height(curved,0)>10&&Height(curved,0)<18,"curved support preserves changing height");
            Check(curved.C.w>0,"curved support expands vertical bounds");
            Plane();group=Root(x:5);var edge=Sample(ref group,10);
            Check((int)edge.C.z==(511&~73),"outside owner leaves only missing left ground samples unset");
            tile.HasWater=tile.WaterBoundsKnown=true;tile.WaterBounds=new Bounds(new Vector3(512,10,512),new Vector3(512,1,512));group=Root();var wet=Sample(ref group,10);
            Check(wet.C.y==511&&wet.C.z==511,"broad water mesh bounds never erase admitted dry habitat");
            tile.ForestCover[(8*side+8)*8+3]=0;group=Root();wet=Sample(ref group,10);
            Check(wet.C.y==0&&wet.C.z==511,"actual submerged habitat rejects forest while retaining ground support");Plane();
            group=Root();group.Face|=16u;var tidal=Sample(ref group,10);Check(tidal.C.y==511&&tidal.C.z==511,"mapped tidal forest retains wet support");
            tile.ForestCover[(8*side+8)*8+3]=0;group=Root();group.Face|=16u;tidal=Sample(ref group,10);Check(tidal.C.y==0&&tidal.C.z==511,"tidal exception never bypasses habitat admission");Plane();
            tile.HasWater=false;
            for(int i=0;i<side*side;i++){neighbour.Heights[i]=30;fine.Heights[i]=40;neighbour.ForestCover[i*8+3]=fine.ForestCover[i*8+3]=1;}
            var adjacent=new WorldGen.Presentation.TerrainTileKey(1,0,1,.5);owner.tiles.Add(key,tile);owner.tiles.Add(adjacent,neighbour);
            group=Root(x:(float)key.SizeM-5);var joined=Sample(ref group,10,owner);Check(joined.C.z==511&&Height(joined,5)==30&&Height(joined,3)==10,"neighbor owner contributes its own height");
            owner.tiles.Remove(adjacent);group=Root(x:(float)key.SizeM-5);var missing=Sample(ref group,10,owner);Check((int)missing.C.z==(511&~292),"missing neighbor leaves right samples unset");
            var incoming=new GroundPublication{Key=adjacent,Product=neighbour};group=Root(x:(float)key.SizeM-5);var future=Sample(ref group,10,owner,incoming);Check(future.C.z==511&&Height(future,5)==30,"future publication contributes newly arriving neighbor height");
            var child=new WorldGen.Presentation.TerrainTileKey(1,1,0,.5);tile.CoverageChildren=new System.Collections.Generic.List<WorldGen.Presentation.TerrainTileKey>{child};owner.tiles.Add(child,fine);
            group=Root();var finer=Sample(ref group,40,owner);Check(Height(finer,8)==40&&Height(finer,0)==10,"finer owner supplies only its footprint");
            var retirement=new GroundPublication{Key=child,Product=fine,Retiring=true};group=Root();var retired=Sample(ref group,10,owner,retirement);Check(Height(retired,8)==10,"retiring fine owner reveals retained parent height");
            return checks+" flat/inclined/curved/edge/water/cross-owner/publication ground-grid controls pass\n";
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
        var roots=new GraphicsBuffer(GraphicsBuffer.Target.Structured,128,64);var visible=new GraphicsBuffer(GraphicsBuffer.Target.Append,32768,4);var args=new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,1,GraphicsBuffer.IndirectDrawArgs.size);args.SetData(new[]{new GraphicsBuffer.IndirectDrawArgs{vertexCountPerInstance=6}});
        var flags=new GraphicsBuffer(GraphicsBuffer.Target.Structured,1,4);flags.SetData(new[]{1});var hash=new GraphicsBuffer(GraphicsBuffer.Target.Structured,16,16);hash.SetData(new ForestHash[16]);
        var groundGrid=new GraphicsBuffer(GraphicsBuffer.Target.Structured,128,128);groundGrid.SetData(new[]{new ForestGroundGrid{C=new Vector4(0,511,511,0)},new ForestGroundGrid{C=new Vector4(0,511,511,0)}});
        compute.SetBuffer(0,"_Ground",groundGrid);
        var birth=new GraphicsBuffer(GraphicsBuffer.Target.Structured,1,4);birth.SetData(new[]{0f});compute.SetBuffer(0,"_ForestBirth",birth);
        var data=new[]{new ForestRoot{Position=new Vector3(-.58f,0,0),Width=.35f,Height=1,Yaw=0,Tint=Vector3.one},new ForestRoot{Position=new Vector3(.58f,0,0),Width=.35f,Height=1,Yaw=.7f,Tint=Vector3.one}};roots.SetData(data);
        var planes=GeometryUtility.CalculateFrustumPlanes(camera);var planeVectors=new Vector4[6];for(int i=0;i<6;i++)planeVectors[i]=new Vector4(planes[i].normal.x,planes[i].normal.y,planes[i].normal.z,planes[i].distance);
        compute.SetVectorArray("_Planes",planeVectors);compute.SetVector("_Eye",camera.transform.position);compute.SetVector("_Chart",new Vector4(0,0,0,1));compute.SetVector("_Ranges",new Vector4(0,1,100,15));compute.SetInt("_RootCount",2);compute.SetBuffer(0,"_Roots",roots);compute.SetBuffer(0,"_Visible",visible);compute.SetBuffer(0,"_Fields",flags);compute.SetBuffer(0,"_Tiles",hash);
        int checkRootCount=2;uint Count(){visible.SetCounterValue(0);compute.Dispatch(0,(checkRootCount+63)/64,1,1);GraphicsBuffer.CopyCount(visible,args,4);var counts=new GraphicsBuffer.IndirectDrawArgs[1];args.GetData(counts);return counts[0].instanceCount;}
        if(Count()!=2)throw new Exception("Forest GPU culler missed visible roots");
        flags.SetData(new[]{0});if(Count()!=0)throw new Exception("Disabled forest fields draw");flags.SetData(new[]{1});
        compute.SetVector("_Ranges",new Vector4(10,11,100,15));if(Count()!=0)throw new Exception("Near forest rejection failed");
        flags.SetData(new[]{3});if(Count()!=2)throw new Exception("Prepared forest fallback disappeared before local detail was ready");flags.SetData(new[]{1});compute.SetVector("_Ranges",new Vector4(0,1,100,15));
        var table=new ForestHash[16];for(int x=-1;x<=0;x++){int slot=(int)(ForestKeyHash(x,0,0)&15);while(table[slot].State!=0)slot=(slot+1)&15;table[slot]=new ForestHash{X=x,Z=0,Level=0,State=9};}hash.SetData(table);
        data[0].Field=data[1].Field=1u<<16;roots.SetData(data);if(Count()!=0)throw new Exception("Coarse forest not suppressed by ready finer fields");
        data[0].Face=data[1].Face=1;roots.SetData(data);if(Count()!=2)throw new Exception("Different cube face incorrectly owns these trees");
        hash.SetData(new ForestHash[16]);data[0].Field=data[1].Field=0;data[0].Face=data[1].Face=0;roots.SetData(data);
        compute.SetVector("_Chart",Vector4.zero);if(Count()!=0)throw new Exception("Zero tree density must suppress distant trees too");compute.SetVector("_Chart",new Vector4(0,0,0,1));
        // Production compute expansion must agree with the interaction rule,
        // including sparse density and stable 256-bit water/slope masks.
        var ordinary=(ForestRoot[])data.Clone();data[0].Face=4u|8u|32u;data[0].CellX=64;data[0].CellY=128;data[0].Padding=4;data[1].Height=0;roots.SetData(data);
        compute.SetInt("_ForestSeed",731);compute.SetInt("_ForestLatticeCount",65536);
        var plantingMasks=new[]{0xb7ffebff7fffffdfUL,0x0000000080000001UL,0x8001000000000010UL,0x8b77ebff7fffffdfUL};
        groundGrid.SetData(new[]{new ForestGroundGrid{C=new Vector4(0,511,511,0),D=new Vector4(.12f,.8f,.3f,.65f),
            E=ForestMaskVector(plantingMasks[0]),F=ForestMaskVector(plantingMasks[1]),G=ForestMaskVector(plantingMasks[2]),H=ForestMaskVector(plantingMasks[3])},default(ForestGroundGrid)});
        foreach(float density in new[]{1f,.25f,0f})
        {
            compute.SetVector("_Chart",new Vector4(0,0,0,density));var expected=new System.Collections.Generic.HashSet<uint>();
            for(int y=0;y<16;y++)for(int x=0;x<16;x++)
            {
                int child=y*16+x;if((plantingMasks[child/64]&(1UL<<(child&63)))==0)continue;
                uint h=LocalScatter.PlantingHash(4,64+x,128+y,731);LocalScatter.PlantingOffset(h,out float ox,out float oy);float u=(x+ox)/16,v=(y+oy)/16;
                float p=(.12f+(.8f-.12f)*u)*(1-v)+(.3f+(.65f-.3f)*u)*v;
                if(LocalScatter.PlantingUnit(h^0x27d4eb2du)<p*density)expected.Add((uint)child);
            }
            uint actual=Count();var indices=new uint[actual];if(actual>0)visible.GetData(indices,0,0,(int)actual);
            if(actual!=expected.Count||!expected.SetEquals(indices))throw new Exception("GPU planting differs from interaction cells at density "+density);
        }
        // Exercise divergent species masks across complete GPU waves. A
        // single active root cannot expose batch-size or divergent-loop faults.
        var waveRoots=new ForestRoot[128];var waveGround=new ForestGroundGrid[128];var waveExpected=new System.Collections.Generic.HashSet<uint>();
        for(int i=0;i<waveRoots.Length;i++)
        {
            waveRoots[i]=data[0];var mask=new ForestPlantingMask();
            for(int child=0;child<256;child++)if(LocalScatter.PlantingUnit((uint)(i*257+child)^0x92a13fu)>.5f)
            {mask.Add(child);waveExpected.Add((uint)(i*256+child));}
            waveGround[i]=new ForestGroundGrid{C=new Vector4(0,511,511,0),D=Vector4.one,E=ForestMaskVector(mask.A),F=ForestMaskVector(mask.B),G=ForestMaskVector(mask.C),H=ForestMaskVector(mask.D)};
        }
        roots.SetData(waveRoots);groundGrid.SetData(waveGround);checkRootCount=waveRoots.Length;compute.SetInt("_RootCount",checkRootCount);compute.SetVector("_Chart",new Vector4(0,0,0,1));
        uint waveCount=Count();var waveActual=new uint[waveCount];visible.GetData(waveActual,0,0,(int)waveCount);
        if(waveCount!=waveExpected.Count||!waveExpected.SetEquals(waveActual))throw new Exception("Divergent full-wave species masks differ from CPU membership: "+waveCount+" vs "+waveExpected.Count);
        checkRootCount=2;compute.SetInt("_RootCount",checkRootCount);
        data=ordinary;roots.SetData(data);compute.SetVector("_Chart",new Vector4(0,0,0,1));
        groundGrid.SetData(new[]{new ForestGroundGrid{C=new Vector4(0,511,511,0)},new ForestGroundGrid{C=new Vector4(0,511,511,0)}});
        var props=new MaterialPropertyBlock();props.SetBuffer("_Roots",roots);props.SetBuffer("_Ground",groundGrid);props.SetBuffer("_Visible",visible);props.SetBuffer("_Fields",flags);props.SetBuffer("_ForestBirth",birth);props.SetBuffer("_Tiles",hash);props.SetInt("_ForestHashMask",15);props.SetVector("_ForestChart",Vector4.zero);props.SetVector("_ForestRange",new Vector4(0,1,0,0));
        string report="GPU fixed planting indices match CPU at full/quarter/zero density, including 256-bit exclusions and 128 divergent roots\n"+supportReport+"GPU admission/disable/near-range/finer-field ownership pass\n";
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
        // Isolate GPU-expanded tree groups from placement and streaming.
        // Every child uses the same ordinary prepared tree atlas as exact roots.
        var fixtureArguments=Environment.GetCommandLineArgs();int prototypeArgument=Array.IndexOf(fixtureArguments,"--worldgen-forest-prototype-code"),prototypeCode=0x00002600;
        if(prototypeArgument>=0)
        {
            if(prototypeArgument+1>=fixtureArguments.Length)throw new ArgumentException("Missing forest prototype hex code");
            string hex=fixtureArguments[prototypeArgument+1];if(hex.StartsWith("0x",StringComparison.OrdinalIgnoreCase))hex=hex.Substring(2);
            prototypeCode=int.Parse(hex,System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture);
        }
        var prototype=LocalTreePrototype.Load(prototypeCode);
        if(prototype==null||!prototype.Valid)throw new Exception("Prepared tree group fixture asset unavailable");
        camera.transform.position=new Vector3(0,500,-2000);camera.transform.LookAt(new Vector3(0,8,0));camera.farClipPlane=10000;
        planes=GeometryUtility.CalculateFrustumPlanes(camera);for(int i=0;i<6;i++)planeVectors[i]=new Vector4(planes[i].normal.x,planes[i].normal.y,planes[i].normal.z,planes[i].distance);
        compute.SetVectorArray("_Planes",planeVectors);compute.SetVector("_Eye",camera.transform.position);compute.SetVector("_Ranges",new Vector4(0,1,10000,15));compute.SetInt("_RootCount",1);
        flags.SetData(new[]{3});roots.SetData(new[]{new ForestRoot{Position=Vector3.zero,Width=80,Height=20,Padding=100,Tint=Vector3.one,Face=4,Field=4u<<16,CellX=32736,CellY=32736},default(ForestRoot)});
        args.SetData(new[]{new GraphicsBuffer.IndirectDrawArgs{vertexCountPerInstance=216}});
        Shader.SetGlobalVector("_WorldGenForestDistanceRange",new Vector4(9000,10000,0,0));
        material.SetTexture("_TreeColourViews",prototype.Colour);material.SetTexture("_TreeNormalViews",prototype.Normal);
        material.SetVector("_ImpostorBounds",new Vector4(prototype.Center.x,prototype.Center.y,prototype.Center.z,prototype.Radius));
        material.SetVector("_ImpostorExtents",new Vector4(prototype.Extents.x,prototype.Extents.y,prototype.Extents.z,4*prototype.Radius/LocalTreeImpostor.Size));props.SetFloat("_ForestMacro",2);
        props.SetVector("_ForestPlanet0",new Vector4(0,0,1,40960));props.SetVector("_ForestPlanet1",new Vector4(1,0,0,0));props.SetVector("_ForestPlanet2",new Vector4(0,1,0,0));props.SetInt("_ForestLatticeCount",65536);
        if(Count()!=1)throw new Exception("Prepared stand fixture root not admitted");
        var groundMesh=new Mesh{name="Stand fixture ground"};groundMesh.vertices=new[]{new Vector3(-5000,0,-5000),new Vector3(-5000,0,5000),new Vector3(5000,0,-5000),new Vector3(5000,0,5000)};
        groundMesh.triangles=new[]{0,1,2,2,1,3};groundMesh.normals=new[]{Vector3.up,Vector3.up,Vector3.up,Vector3.up};groundMesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.right,Vector2.one};groundMesh.colors=new[]{Color.white,Color.white,Color.white,Color.white};groundMesh.RecalculateBounds();
        var groundMaterial=new Material(Resources.Load<Shader>("WorldGenLocalScatter"));groundMaterial.SetColor("_BaseColor",new Color(.30f,.32f,.21f));groundMaterial.SetColor("_AmbientTint",new Color(.25f,.25f,.25f));groundMaterial.SetFloat("_FogDistance",10000);
        var ground=new GameObject("Stand fixture ground"){layer=30};ground.AddComponent<MeshFilter>().sharedMesh=groundMesh;var groundRenderer=ground.AddComponent<MeshRenderer>();groundRenderer.sharedMaterial=groundMaterial;groundRenderer.shadowCastingMode=ShadowCastingMode.Off;
        // The same child transforms on real middle meshes provide an actual
        // geometry reference, independent of billboard projection and masking.
        uint Mix(uint value){value^=value>>16;value*=0x7feb352du;value^=value>>15;value*=0x846ca68bu;value^=value>>16;return value;}
        float Unit(uint seed)=>(Mix(seed)&0xffffffu)/16777216f;
        float packetPhaseX=Unit(0x68bc21ebu),packetPhaseZ=Unit(0x02e5be93u);
        var parts=new CombineInstance[36];
        for(int z=0;z<6;z++)for(int x=0;x<6;x++)
        {
            uint seed=Mix((uint)(z*6+x+1)*0x9e3779b9u);float single=80/LocalTreePrototype.StandSpan;
            int child=z*6+x;
            float qx=Mathf.Repeat(packetPhaseX+child*.754877666f,1),qz=Mathf.Repeat(packetPhaseZ+child*.569840291f,1);
            qx+=(Unit(seed^0xa24baed5u)-.5f)*2*LocalTreePrototype.PacketFeather;
            qz+=(Unit(seed^0x9fb21c65u)-.5f)*2*LocalTreePrototype.PacketFeather;
            // Face4 is +Z with X/Y axes; this synthetic production chart maps
            // [32736,32800) of 65536 cells to [-40,40) metres on both axes.
            float px=((32736+qx*64)*2/65536-1)*40960,pz=((32736+qz*64)*2/65536-1)*40960;
            float cohort=.55f+.45f*Unit(seed^0x967a889bu),width=single*cohort*(.93f+.14f*Unit(seed^0x368cc8b7u))/.78645317f;
            parts[z*6+x]=new CombineInstance{mesh=prototype.Middle,transform=Matrix4x4.TRS(new Vector3(px,-.045f,pz),Quaternion.Euler(0,Unit(seed^0x9e3779b9u)*360,0),new Vector3(width,20*cohort/.78581168f,width))};
        }
        var referenceMesh=new Mesh{name="Stand fixture 36-tree reference",indexFormat=IndexFormat.UInt32};referenceMesh.CombineMeshes(parts,true,true,false);referenceMesh.RecalculateBounds();
        var referenceMaterial=new Material(Resources.Load<Material>("TreeSurfaceMaterial"));var standShape=new TreeMorphology(prototype.Code);
        referenceMaterial.SetTexture("_TreeLeaves",leaves);referenceMaterial.SetTexture("_TreeLeafNormals",leafNormals);LocalTreeTextures.BindBark(referenceMaterial);
        referenceMaterial.SetFloat("_TreeLeafShape",(int)standShape.LeafShape);referenceMaterial.SetFloat("_TreeBarkPattern",LocalTreeTextures.BarkLayer(standShape));referenceMaterial.SetFloat("_Wind",0);referenceMaterial.SetColor("_AmbientTint",new Color(.25f,.25f,.25f));referenceMaterial.SetFloat("_FogDistance",10000);
        var reference=new GameObject("Stand fixture 36-tree reference"){layer=30};reference.AddComponent<MeshFilter>().sharedMesh=referenceMesh;var referenceRenderer=reference.AddComponent<MeshRenderer>();referenceRenderer.sharedMaterial=referenceMaterial;referenceRenderer.shadowCastingMode=ShadowCastingMode.Off;reference.SetActive(false);
        var standImage=new Texture2D(1280,720,TextureFormat.RGB24,false);
        Color32[] ReadStandImage()
        {
            var previous=RenderTexture.active;RenderTexture.active=target;standImage.ReadPixels(new Rect(0,0,1280,720),0,0);standImage.Apply();RenderTexture.active=previous;return standImage.GetPixels32();
        }
        report+="Expanded packet group "+prototype.Code.ToString("X8")+" eye=(0,500,-2000), physical width=80, height=20, support span=100, feather="+LocalTreePrototype.PacketFeather+", vertices=216, children=36, production spherical mapping ON: face4 level4 left=(32736,32736), lattice65536 radius40960; finer ownership hash empty, reference bounds="+referenceMesh.bounds+"\n";
        float productionStart=(float)WorldGen.Presentation.TreeRenderingLod.ViewStart(2),productionPixels=(float)WorldGen.Presentation.TreeRenderingLod.PixelStart(2);
        var productionRanges=new Vector4(productionStart,productionStart*1.5f,productionPixels,productionPixels*.5f);
        foreach(var fixture in new[]{
            (Name:"stand-detail-no-ground",Ground:false,Reference:false,Distance:2000f,Production:false,Nearest:false),
            (Name:"stand-detail-ground",Ground:true,Reference:false,Distance:2000f,Production:false,Nearest:false),
            (Name:"stand-reference-ground",Ground:true,Reference:true,Distance:2000f,Production:false,Nearest:false),
            (Name:"stand-production-2km",Ground:true,Reference:false,Distance:2000f,Production:true,Nearest:false),
            (Name:"stand-production-reference-2km",Ground:true,Reference:true,Distance:2000f,Production:true,Nearest:false),
            (Name:"stand-production-5km",Ground:true,Reference:false,Distance:5000f,Production:true,Nearest:false),
            (Name:"stand-production-reference-5km",Ground:true,Reference:true,Distance:5000f,Production:true,Nearest:false),
            (Name:"stand-production-nearest-5km",Ground:true,Reference:false,Distance:5000f,Production:true,Nearest:true),
            (Name:"group-no-forest",Ground:true,Reference:false,Distance:2000f,Production:true,Nearest:false),
            (Name:"group-no-support",Ground:true,Reference:false,Distance:2000f,Production:true,Nearest:false),
            (Name:"group-near-suppressed",Ground:true,Reference:false,Distance:2000f,Production:true,Nearest:false),
            (Name:"group-near-fallback",Ground:true,Reference:false,Distance:2000f,Production:true,Nearest:false)})
        {
            reference.SetActive(false);ground.SetActive(fixture.Ground);
            bool noForest=fixture.Name=="group-no-forest",noSupport=fixture.Name=="group-no-support",nearSuppressed=fixture.Name=="group-near-suppressed",nearFallback=fixture.Name=="group-near-fallback";
            groundGrid.SetData(new[]{new ForestGroundGrid{C=new Vector4(0,noForest?0:511,noSupport?0:511,0)},default(ForestGroundGrid)});
            flags.SetData(new[]{nearSuppressed?1:3});props.SetVector("_ForestRange",nearSuppressed||nearFallback?new Vector4(3000,3100,0,0):new Vector4(0,1,0,0));
            camera.transform.position=new Vector3(0,500,-fixture.Distance);camera.transform.LookAt(new Vector3(0,8,0));
            planes=GeometryUtility.CalculateFrustumPlanes(camera);for(int i=0;i<6;i++)planeVectors[i]=new Vector4(planes[i].normal.x,planes[i].normal.y,planes[i].normal.z,planes[i].distance);
            compute.SetVectorArray("_Planes",planeVectors);compute.SetVector("_Eye",camera.transform.position);
            if(fixture.Production)material.EnableKeyword("WORLDGEN_TREE_EFFICIENT");else material.DisableKeyword("WORLDGEN_TREE_EFFICIENT");
            var viewRanges=fixture.Production?productionRanges:Vector4.zero;
            if(fixture.Nearest){viewRanges.z=1000000;viewRanges.w=500000;}
            Shader.SetGlobalVector("_WorldGenTreeViewRanges",viewRanges);Shader.SetGlobalFloat("_WorldGenTreeViewProjection",camera.pixelHeight/Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f));
            // Clear the previous frame's indirect submission before the baseline.
            yield return null;
            camera.Render();var baseline=ReadStandImage();
            if(fixture.Reference)reference.SetActive(true);
            else{var parameters=new RenderParams(material){camera=camera,layer=30,worldBounds=new Bounds(Vector3.zero,Vector3.one*500),matProps=props,shadowCastingMode=ShadowCastingMode.Off};Graphics.RenderPrimitivesIndirect(in parameters,MeshTopology.Triangles,args,1);}
            camera.Render();var pixels=ReadStandImage();int changed=0,minX=1280,maxX=-1,minY=720,maxY=-1;
            for(int y=0;y<720;y++)for(int x=0;x<1280;x++){int i=y*1280+x;if(Math.Abs(pixels[i].r-baseline[i].r)+Math.Abs(pixels[i].g-baseline[i].g)+Math.Abs(pixels[i].b-baseline[i].b)>20){changed++;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}}
            File.WriteAllBytes(Path.Combine(output,fixture.Name+".png"),standImage.EncodeToPNG());
            report+=fixture.Name+" visible pixels="+changed+", pixel bounds="+minX+","+minY+".."+maxX+","+maxY+", view ranges="+viewRanges+", projection="+(camera.pixelHeight/Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f))+"\n";
            if(noForest||noSupport||nearSuppressed){if(changed!=0)throw new Exception("Masked expanded group still visible: "+fixture.Name+" pixels="+changed);}
            else if((!fixture.Ground||nearFallback)&&changed<10)throw new Exception("Expanded group absent: "+fixture.Name+" pixels="+changed);
            else if(changed==0)throw new Exception("Expanded group/reference has no visible pixels: "+fixture.Name);
        }
        reference.SetActive(false);ground.SetActive(false);Destroy(standImage);Destroy(reference);Destroy(referenceMesh);Destroy(referenceMaterial);Destroy(ground);Destroy(groundMesh);Destroy(groundMaterial);
        roots.Dispose();groundGrid.Dispose();visible.Dispose();args.Dispose();flags.Dispose();hash.Dispose();birth.Dispose();File.WriteAllText(Path.Combine(output,"PASS.txt"),SystemInfo.graphicsDeviceName+"\n"+report);Debug.Log("PACKAGED FOREST SHADER PASS "+report);Application.Quit(0);
    }
}
