using System;
using System.Collections;
using System.IO;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public static IEnumerator CheckCoverageShader(string output)
    {
        bool savedHardSetting=TerrainVideoOptions.HardChunkUpdates;TerrainVideoOptions.HardChunkUpdates=false;
        Directory.CreateDirectory(output);
        Application.logMessageReceived+=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        var arguments=Environment.GetCommandLineArgs();int seamProof=Array.IndexOf(arguments,"--worldgen-resident-seam-proof");
        if(Array.IndexOf(arguments,"--worldgen-priority-order-proof")>=0)
        {
            try{CheckTerrainPriorityOrder(output);}finally{TerrainVideoOptions.HardChunkUpdates=savedHardSetting;}
            Application.Quit(0);yield break;
        }
        if(seamProof>=0){CheckRefinedResidentSeam(arguments[seamProof+1],output);TerrainVideoOptions.HardChunkUpdates=savedHardSetting;Application.Quit(0);yield break;}
        var host=new GameObject("Coverage shader check");var explorer=host.AddComponent<LocalTerrainExplorer>();explorer.enabled=false;
        explorer.terrainRoot=host;
        Texture2D reused=null;int payloadFloats=0,reusedObjects=0;
        foreach(int length in new[]{4,64,1020,1024,1028,2048,12,0,4})
        {
            var values=new float[length];for(int i=0;i<length;i++)values[i]=(float)Math.Sin(i*.173+length)*17;
            var previous=reused;int previousHeight=previous!=null?previous.height:0;
            reused=explorer.CoverageTexture(reused,values);
            if(length==0){if(reused!=null)throw new Exception("Empty coverage retained a texture");continue;}
            int height=(length/4+255)/256;
            if(previous!=null&&previousHeight==height){if(!ReferenceEquals(previous,reused))throw new Exception("Same-sized coverage upload replaced its native texture");reusedObjects++;}
            var literal=new Texture2D(256,height,TextureFormat.RGBAFloat,false,true);
            var expected=new Color[256*height];for(int i=0;i<length/4;i++)expected[i]=new Color(values[4*i],values[4*i+1],values[4*i+2],values[4*i+3]);
            literal.SetPixels(expected);literal.Apply(false,false);
            var a=literal.GetRawTextureData<float>();var b=reused.GetRawTextureData<float>();
            if(a.Length!=b.Length)throw new Exception("Coverage upload changed native format or dimensions");
            for(int i=0;i<a.Length;i++){if(BitConverter.SingleToInt32Bits(a[i])!=BitConverter.SingleToInt32Bits(b[i]))throw new Exception("Coverage upload changed float payload or zero padding");payloadFloats++;}
            Destroy(literal);
        }
        if(reused!=null)Destroy(reused);
        if(reusedObjects<3)throw new Exception("Coverage check did not exercise object reuse");
        File.WriteAllText(Path.Combine(output,"coverage-texture-reuse.txt"),$"PASS {payloadFloats} literal/reused native float components including shrink/grow, zero padding, empty uploads and {reusedObjects} same-sized object reuses.");
        var shader=Resources.Load<Shader>("WorldGenLocalTerrain");
        if(shader==null||!shader.isSupported)throw new Exception("Coverage shader unsupported");
        var material=new Material(shader);material.SetFloat("_Water",1);material.SetFloat("_Daylight",1);material.SetFloat("_FogDistance",1e9f);
        GameObject Quad(string name,float size,float y,Color color,float? fromY=null)
        {
            var root=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            var positions=new[]{new Vector3(0,y,0),new Vector3(size,y,0),new Vector3(0,y,size),new Vector3(size,y,size)};var vertices=new TerrainVertex[4];
            for(int i=0;i<vertices.Length;i++)vertices[i]=new TerrainVertex{Position=positions[i],Normal=Vector3.up,Color=color,Uv=new Vector2(fromY??y,fromY.HasValue?1:0),EdgeIndex=0};
            // Match production declarations, including explicit TEXCOORD3=0;
            // absent vertex attributes are not a valid edge-index fixture.
            var mesh=new Mesh{name=name};mesh.SetVertexBufferParams(vertices.Length,TerrainUpload.Layout);mesh.SetVertexBufferData(vertices,0,0,vertices.Length);
            var indices=new ushort[]{0,2,1,1,2,3};mesh.SetIndexBufferParams(indices.Length,UnityEngine.Rendering.IndexFormat.UInt16);mesh.SetIndexBufferData(indices,0,0,indices.Length);
            mesh.bounds=new Bounds(new Vector3(size*.5f,(y+(fromY??y))*.5f,size*.5f),new Vector3(size,Math.Abs(y-(fromY??y))+.01f,size));mesh.subMeshCount=1;
            mesh.SetSubMesh(0,new UnityEngine.Rendering.SubMeshDescriptor(0,indices.Length){vertexCount=vertices.Length,bounds=mesh.bounds},UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<MeshRenderer>().sharedMaterial=material;return root;
        }
        var parent=new TerrainTileKey(0,0,1,1d/512);var child=new TerrainTileKey(0,0,0,1d/512);
        explorer.tiles.Add(parent,new Tile{Root=Quad("coarse fallback",4,2,Color.red),Edges=new double[4]});
        var cameraRoot=new GameObject("Coverage camera",typeof(Camera));var camera=cameraRoot.GetComponent<Camera>();
        camera.orthographic=true;camera.orthographicSize=2;camera.transform.position=new Vector3(2,10,2);camera.transform.rotation=Quaternion.Euler(90,0,0);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.nearClipPlane=.1f;
        var target=new RenderTexture(128,128,24);target.Create();camera.targetTexture=target;
        var pixels=new Texture2D(128,128,TextureFormat.RGB24,false);
        Color[] Capture(string name)
        {
            camera.Render();var prior=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,128,128),0,0);pixels.Apply();RenderTexture.active=prior;
            File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());return pixels.GetPixels();
        }
        explorer.coverageDirty=true;explorer.UpdateTerrainCoverage();yield return null;
        var before=Capture("fallback-before");if(before[32*128+32].r<=before[32*128+32].g)throw new Exception("Fallback absent before child loads");
        explorer.tiles.Add(child,new Tile{Root=Quad("ready fine terrain",2,0,Color.green),Edges=new double[4]});
        explorer.coverageDirty=true;explorer.UpdateTerrainCoverage();yield return null;
        var after=Capture("partial-replacement");
        if(after[32*128+32].g<=after[32*128+32].r)throw new Exception("Coarse mesh still covers ready fine terrain");
        if(after[96*128+96].r<=after[96*128+96].g)throw new Exception("Unloaded neighbouring area lost fallback coverage");
        var tile=explorer.tiles[child];explorer.CacheTerrain(child,tile);explorer.tiles.Remove(child);explorer.coverageDirty=true;explorer.UpdateTerrainCoverage();yield return null;
        var restored=Capture("fallback-restored");if(restored[32*128+32].r<=restored[32*128+32].g)throw new Exception("Fallback not restored when fine coverage is removed");
        explorer.installationsThisFrame=0;
        if(!explorer.RestoreTerrain(child,0,new double[4]))
        {
            // Synthetic tiles have zero segments and no stitching requirements.
            throw new Exception("Native mesh product cache failed to restore");
        }
        tile.FadeIn=0;explorer.coverageDirty=true;explorer.UpdateTerrainCoverage();yield return null;var cachedImage=Capture("cached-replacement");
        if(cachedImage[32*128+32].g<=cachedImage[32*128+32].r||explorer.TerrainCacheHits!=1)throw new Exception("Cached mesh failed to render after restore");
        // Restoring a different owned mesh with the same height grid must not
        // invalidate/reproject the whole vegetation population.
        tile.Segments=4;tile.Heights=new float[25];explorer.scatterGroundChanges.Clear();
        var identical=new Tile{Root=Quad("identical cached terrain",2,0,Color.green),Segments=4,Heights=new float[25],Edges=new double[4]};
        explorer.TerrainProducts.Put(new TerrainProductKey(child,4,identical.Edges),identical,0);
        explorer.installationsThisFrame=0;explorer.frameUploadMs=0;int groundRevision=explorer.scatterGroundRevision;
        if(!explorer.RestoreTerrain(child,4,identical.Edges)||explorer.scatterGroundRevision!=groundRevision||explorer.scatterGroundChanges.Count!=0)throw new Exception("Identical cached heights unnecessarily invalidated vegetation");
        tile=identical;tile.FadeIn=0;
        explorer.coverageDirty=true;explorer.UpdateTerrainCoverage();yield return null;
        while(Time.time<3.2f)yield return null;
        (int Red,int Green,int Holes) SurfaceCounts(Color[] image)
        {
            int red=0,green=0,holes=0;
            for(int y=8;y<56;y++)for(int x=8;x<56;x++){var p=image[y*128+x];if(p.r>p.g+.05)red++;else if(p.g>p.r+.05)green++;else holes++;}
            return(red,green,holes);
        }
        void ChildMorph(float finalY,float fromY,float elapsed)
        {
            var mesh=tile.Root.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;
            var uv=new Vector2[vertices.Length];for(int i=0;i<vertices.Length;i++){vertices[i].y=finalY;uv[i]=new Vector2(fromY,1);}
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.bounds=new Bounds(new Vector3(1,(finalY+fromY)*.5f,1),new Vector3(2,Math.Abs(finalY-fromY)+.01f,2));
            tile.FadeIn=Math.Max(.001f,Time.time-elapsed);tile.FadeOut=0;explorer.coverageDirty=true;explorer.UpdateTerrainCoverage();
        }
        // A ready child owns its complete footprint from the first frame.
        // Height changes move its opaque geometry; there is no pixel discard
        // and no outgoing second surface at either side of the old height.
        foreach(bool lower in new[]{true,false})foreach(float elapsed in new[]{0f,1.5f,3.1f})
        {
            ChildMorph(lower?0:4,lower?4:0,elapsed);yield return null;
            var counts=SurfaceCounts(Capture((lower?"lower":"upper")+"-opaque-morph-"+elapsed.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)));
            if(counts.Green<2200||counts.Red>10||counts.Holes>10)throw new Exception("A current opaque child lost coverage during geometry morph: "+counts);
        }
        var topPosition=camera.transform.position;var topRotation=camera.transform.rotation;float topSize=camera.orthographicSize;
        explorer.tiles[parent].Root.SetActive(false);
        camera.transform.position=new Vector3(1,7,-5);camera.transform.LookAt(new Vector3(1,1,1));camera.orthographicSize=4;
        float GreenMeanY(Color[] image)
        {
            int count=0;double sum=0;for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {var p=image[y*128+x];if(p.g>p.r+.05f&&p.g>p.b+.05f){count++;sum+=y;}}
            if(count<50)throw new Exception("Geometry morph fixture is not visible from its oblique camera");return(float)(sum/count);
        }
        string morphStatePath=Path.Combine(output,"geometry-morph-state.txt");File.WriteAllText(morphStatePath,"Actual shader morph fixture state before/after each manual camera render.\n");
        void RecordMorphState(string label)
        {
            var renderer=tile.Root.GetComponent<MeshRenderer>();var mesh=tile.Root.GetComponent<MeshFilter>().sharedMesh;var activeMaterial=renderer.sharedMaterial;
            var text=new System.Text.StringBuilder();var culture=System.Globalization.CultureInfo.InvariantCulture;
            text.AppendLine(label+" frame="+Time.frameCount+" time="+Time.time.ToString("R",culture)+" levelTime="+Time.timeSinceLevelLoad.ToString("R",culture)+" realtime="+Time.realtimeSinceStartupAsDouble.ToString("R",culture)+" delta="+Time.deltaTime.ToString("R",culture));
            text.AppendLine("tileIn="+tile.FadeIn.ToString("R",culture)+" material="+activeMaterial.GetEntityId()+" expectedMaterial="+tile.Materials[0].GetEntityId()+" shader="+activeMaterial.shader.name+" active="+tile.Root.activeInHierarchy+" propertyBlock="+renderer.HasPropertyBlock());
            text.AppendLine("LodFade="+activeMaterial.GetVector("_LodFade").ToString("R")+" transition="+activeMaterial.GetFloat("_TransitionEnabled").ToString("R",culture)+" safety="+activeMaterial.GetFloat("_LodSafetyRadius").ToString("R",culture)+" residentCount="+activeMaterial.GetFloat("_ResidentEdgeCount").ToString("R",culture));
            text.AppendLine("global_Time="+Shader.GetGlobalVector("_Time").ToString("R")+" global_TimeParameters="+Shader.GetGlobalVector("_TimeParameters").ToString("R")+" camera="+camera.transform.position.ToString("R"));
            var vertices=mesh.vertices;var uv=mesh.uv;text.AppendLine("mesh="+mesh.GetEntityId()+" vertices="+vertices.Length+" uv="+uv.Length);
            foreach(var attribute in mesh.GetVertexAttributes())text.AppendLine("attribute="+attribute.attribute+" format="+attribute.format+" dimension="+attribute.dimension+" stream="+attribute.stream);
            for(int i=0;i<Math.Min(4,vertices.Length);i++)text.AppendLine("v"+i+"="+vertices[i].ToString("R")+" uv0="+(i<uv.Length?uv[i].ToString("R"):"missing"));
            File.AppendAllText(morphStatePath,text.ToString());
        }
        Color[] CaptureMorphState(string name)
        {RecordMorphState(name+" before");var image=Capture(name);RecordMorphState(name+" after");return image;}
        ChildMorph(2,2,3.1f);yield return null;float referenceMid=GreenMeanY(CaptureMorphState("geometry-reference-midpoint"));
        ChildMorph(0,4,1.5f);yield return null;float lowerMid=GreenMeanY(CaptureMorphState("geometry-lower-midpoint"));
        ChildMorph(4,0,1.5f);yield return null;float upperMid=GreenMeanY(CaptureMorphState("geometry-upper-midpoint"));
        File.AppendAllText(morphStatePath,"pixel mean Y reference/lower/upper="+referenceMid+"/"+lowerMid+"/"+upperMid+"\n");
        if(Math.Abs(lowerMid-referenceMid)>2||Math.Abs(upperMid-referenceMid)>2)throw new Exception("Shader did not move opaque vertices through the expected halfway height: "+referenceMid+"/"+lowerMid+"/"+upperMid);
        ChildMorph(4,4,3.1f);yield return null;float referenceEnd=GreenMeanY(Capture("geometry-reference-final"));
        ChildMorph(4,0,0);tile.Materials[0].SetFloat("_LodSafetyRadius",100);yield return null;
        if(Math.Abs(GreenMeanY(Capture("geometry-physical-near-final"))-referenceEnd)>2)throw new Exception("Physical-near guard did not force the final geometry");
        tile.Materials[0].SetFloat("_LodSafetyRadius",0);
        ChildMorph(4,0,0);tile.Materials[0].SetFloat("_TransitionEnabled",0);yield return null;
        if(Math.Abs(GreenMeanY(Capture("geometry-hard-final"))-referenceEnd)>2)throw new Exception("Hard geometry updates did not force the final geometry");
        tile.Materials[0].SetFloat("_TransitionEnabled",1);
        tile.FadeIn=0;explorer.coverageDirty=true;explorer.UpdateTerrainCoverage();yield return null;
        if(Math.Abs(GreenMeanY(Capture("geometry-unstamped-final"))-referenceEnd)>2)throw new Exception("A cached start-height marker without an active stamp did not use final geometry");
        // The moving interior must not move its shared boundary. A final-edge
        // height texture pins the perimeter to its flat coarse neighbour.
        var quadMesh=tile.Root.GetComponent<MeshFilter>().sharedMesh;
        var fanVertices=new TerrainVertex[5];var positions=new[]{new Vector3(0,4,0),new Vector3(2,4,0),new Vector3(0,4,2),new Vector3(2,4,2),new Vector3(1,4,1)};
        for(int i=0;i<5;i++)fanVertices[i]=new TerrainVertex{Position=positions[i],Normal=Vector3.up,Color=Color.green,Uv=new Vector2(0,1),EdgeIndex=i<4?i+1:0};
        var fan=new Mesh{name="Opaque morph shared boundary fixture"};fan.SetVertexBufferParams(5,TerrainUpload.Layout);fan.SetVertexBufferData(fanVertices,0,0,5);
        var fanIndices=new ushort[]{0,2,4,2,3,4,3,1,4,1,0,4};fan.SetIndexBufferParams(fanIndices.Length,UnityEngine.Rendering.IndexFormat.UInt16);fan.SetIndexBufferData(fanIndices,0,0,fanIndices.Length);
        fan.bounds=new Bounds(new Vector3(1,2,1),new Vector3(2,4,2));fan.subMeshCount=1;fan.SetSubMesh(0,new UnityEngine.Rendering.SubMeshDescriptor(0,fanIndices.Length){vertexCount=5,bounds=fan.bounds},UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
        tile.Root.GetComponent<MeshFilter>().sharedMesh=fan;
        var edgeHeights=new Texture2D(4,1,TextureFormat.RFloat,false,true);edgeHeights.SetPixelData(new float[4],0);edgeHeights.Apply(false,false);
        var parentMesh=explorer.tiles[parent].Root.GetComponent<MeshFilter>().sharedMesh;var parentVertices=parentMesh.vertices;
        for(int i=0;i<parentVertices.Length;i++)parentVertices[i].y=0;parentMesh.vertices=parentVertices;parentMesh.RecalculateBounds();explorer.tiles[parent].Root.SetActive(true);
        camera.transform.position=new Vector3(2,7,-5);camera.transform.LookAt(new Vector3(2,0,2));
        foreach(float elapsed in new[]{0f,1.5f,3.1f})
        {
            tile.FadeIn=Math.Max(.001f,Time.time-elapsed);explorer.coverageDirty=true;explorer.UpdateTerrainCoverage();
            tile.Materials[0].SetTexture("_ResidentEdgeHeights",edgeHeights);tile.Materials[0].SetFloat("_ResidentEdgeCount",4);yield return null;
            var image=Capture("perspective-pinned-boundary-"+elapsed.ToString("F1",System.Globalization.CultureInfo.InvariantCulture));int holes=0;
            for(int z=0;z<20;z++)for(int x=0;x<20;x++)
            {
                var view=camera.WorldToViewportPoint(new Vector3(.1f+x*.2f,0,.1f+z*.2f));
                if(view.z<=0||view.x<=0||view.x>=1||view.y<=0||view.y>=1)throw new Exception("Perspective coverage sample escaped its fixture camera");
                var p=image[Math.Min(127,(int)(view.y*128))*128+Math.Min(127,(int)(view.x*128))];if(Math.Max(p.r,p.g)<=p.b+.05f)holes++;
            }
            if(holes!=0)throw new Exception("Opaque interior morph opened a perspective gap at its pinned boundary: "+holes);
        }
        tile.Root.GetComponent<MeshFilter>().sharedMesh=quadMesh;Destroy(fan);Destroy(edgeHeights);tile.Materials[0].SetFloat("_ResidentEdgeCount",0);
        for(int i=0;i<parentVertices.Length;i++)parentVertices[i].y=2;parentMesh.vertices=parentVertices;parentMesh.RecalculateBounds();
        camera.transform.position=topPosition;camera.transform.rotation=topRotation;camera.orthographicSize=topSize;
        ChildMorph(0,0,3.1f);tile.FadeIn=0;explorer.coverageDirty=true;explorer.UpdateTerrainCoverage();yield return null;
        File.WriteAllText(Path.Combine(output,"opaque-geometry-morph.txt"),"PASS actual shader: upper/lower current terrain remains opaque at start/mid/end; oblique midpoint heights match a static reference; physical-near/hard guards use final geometry; moving interior retains its final shared boundary without perspective ground holes.");
        var oldParent=explorer.tiles[parent];var newParent=new Tile{Root=Quad("same-key replacement",4,2,Color.red),Edges=new double[4]};
        explorer.BeginTerrainReplacement(parent,newParent,oldParent);explorer.tiles[parent]=newParent;
        if(explorer.TerrainTiming(child).In>0)throw new Exception("Replacing a parent suppressed already-resident children");
        if(explorer.previousTerrain.ContainsKey(parent)||newParent.FadeIn!=0||oldParent.Root.activeSelf)throw new Exception("A same-key parent replacement retained an incompatible interior underlay beside existing children");
        explorer.tiles.Remove(parent);
        var grand=new TerrainTileKey(0,0,2,1d/512);explorer.tiles[grand]=new Tile{Root=Quad("grand fallback",8,3,Color.red),Edges=new double[4]};
        explorer.BeginTerrainReplacement(parent,newParent,null);explorer.tiles[parent]=newParent;
        if(newParent.FadeIn!=0)throw new Exception("Coarsening unnecessarily faded through a grandparent");
        explorer.wanted.Clear();explorer.wanted.Add(child,64);tile.Segments=64;tile.FadeIn=tile.FadeOut=0;
        explorer.coverageDirty=true;explorer.UpdateTerrainCoverage();yield return null;
        var finest=Capture("fine-after-parent-publication");
        if(finest[32*128+32].g<=finest[32*128+32].r)throw new Exception("Publishing an ancestor obscured the already resident fine surface");
        if(!explorer.CanRetireTransition(parent))throw new Exception("A fully ready fine replacement did not release its parent");
        bool oldKeep=WorldGenPerformance.KeepBackdrops;WorldGenPerformance.KeepBackdrops=true;explorer.backdrops[parent]=new double[4];
        if(explorer.CanRetireTransition(parent))throw new Exception("A still-planned coarse backdrop was unnecessarily retired");
        explorer.backdrops.Clear();
        if(!explorer.CanRetireTransition(parent))throw new Exception("An unplanned backdrop could not retire");
        WorldGenPerformance.KeepBackdrops=oldKeep;
        explorer.wanted[child]=128;
        if(explorer.CanRetireTransition(parent))throw new Exception("Retirement accepted the wrong replacement detail");
        explorer.wanted[child]=64;tile.FadeIn=Time.time;
        if(explorer.CanRetireTransition(parent))throw new Exception("Retirement discarded a parent before its fine fade completed");
        tile.FadeIn=0;explorer.wanted.Add(new TerrainTileKey(1,0,0,1d/512),64);
        if(explorer.CanRetireTransition(parent))throw new Exception("Retirement discarded a parent with a missing replacement quadrant");
        explorer.wanted.Clear();explorer.wanted.Add(child,64);
        bool oldPaced=WorldGenPerformance.PacedTerrainRetirement;WorldGenPerformance.PacedTerrainRetirement=true;
        int residentBefore=explorer.tiles.Count;explorer.installationsThisFrame=1;
        explorer.RetireObsoleteTerrain();
        if(explorer.tiles.Count!=residentBefore)throw new Exception("Retirement did not share the current installation budget");
        explorer.installationsThisFrame=0;explorer.RetireObsoleteTerrain();
        if(explorer.tiles.Count!=residentBefore-1||!explorer.tiles.ContainsKey(child))throw new Exception("Paced retirement removed more than one tile or wanted terrain");
        explorer.RetireObsoleteTerrain();
        if(explorer.tiles.Count!=residentBefore-1)throw new Exception("Paced retirement reused the same frame's allowance");
        explorer.installationsThisFrame=0;explorer.RetireObsoleteTerrain();
        if(explorer.tiles.Count!=1||!explorer.tiles.ContainsKey(child))throw new Exception("Paced retirement never released its remaining redundant tiles");
        WorldGenPerformance.PacedTerrainRetirement=oldPaced;
        explorer.wanted.Clear();
        explorer.tiles.Remove(child);explorer.CacheTerrain(child,tile);explorer.ResetStreamingReuse(false);
        if(explorer.CachedTerrainTiles!=0)throw new Exception("Chart invalidation retained old products");
        // A fallback and the final requested mesh can share one address while
        // deliberately using different resolutions/edges. This used to stage,
        // reject and restage forever, consuming every balanced upload slot.
        var fallbackHost=new GameObject("Fallback publication regression");var fallbackOwner=fallbackHost.AddComponent<LocalTerrainExplorer>();fallbackOwner.enabled=false;fallbackOwner.terrainRoot=fallbackHost;
        var fallbackKey=new TerrainTileKey(0,0,0,1);fallbackOwner.wanted[fallbackKey]=16;fallbackOwner.wantedEdges[fallbackKey]=new[]{64d,64d,64d,64d};
        var fallbackEdges=new[]{128d,128d,128d,128d};fallbackOwner.backdrops[fallbackKey]=fallbackEdges;
        var fallbackBatch=new ForestBatch();fallbackBatch.Data.Add(new ForestRoot{Position=new Vector3(64,1,64),Width=2,Height=4,Tint=Vector3.one});fallbackBatch.GroundLevels.Add(17);
        var fallbackField=new ForestField{Source=new WorldGen.Core.ForestPage(),Bounds=new Bounds(new Vector3(64,3,64),new Vector3(2,4,2))};
        fallbackField.Roots.Add(fallbackBatch,new System.Collections.Generic.List<int>{0});fallbackOwner.forestFields.Add(new WorldGen.Core.ForestPageKey(0,0,0,0),fallbackField);
        var fallbackTile=new Tile{Root=Quad("Valid nonfinal coverage",1024,0,Color.green),Segments=8,Edges=fallbackEdges,Heights=new float[81]};
        bool oldStaging=WorldGenPerformance.StagedGroundPublication;WorldGenPerformance.StagedGroundPublication=true;
        if(!fallbackOwner.StageGroundPublication(fallbackKey,fallbackTile,fallback:true))throw new Exception("Fallback regression did not exercise staged publication");
        for(int i=0;i<10&&fallbackOwner.GroundPublicationPending;i++)fallbackOwner.AdvanceGroundPublication();
        if(fallbackOwner.GroundPublicationPending||!fallbackOwner.tiles.TryGetValue(fallbackKey,out var shown)||!ReferenceEquals(shown,fallbackTile)||!shown.Root.activeSelf)
            throw new Exception("Useful fallback was rejected merely for differing from the final mesh detail");
        if(Math.Abs(fallbackBatch.Data[0].Position.y+.045f)>.0001f)throw new Exception("Accepted fallback did not ground its actual affected forest root");
        WorldGenPerformance.StagedGroundPublication=oldStaging;
        fallbackOwner.forestFields.Clear();fallbackBatch.Dispose();
        File.WriteAllText(Path.Combine(output,"fallback-publication.txt"),"PASS: a useful staged 8-segment fallback publishes and grounds an actual affected forest root while its same-address final request still requires 16 segments and different edges; no reject/restage loop.");
        bool oldHard=TerrainVideoOptions.HardChunkUpdates;TerrainVideoOptions.HardChunkUpdates=false;
        var hardReplacement=new Tile{Root=Quad("Hard-update replacement",1024,-1,Color.blue,0),Segments=16,Edges=new[]{64d,64d,64d,64d},Heights=new float[289],FadeIn=fallbackOwner.TransitionNow};
        fallbackOwner.BeginTerrainReplacement(fallbackKey,hardReplacement,fallbackTile);fallbackOwner.tiles[fallbackKey]=hardReplacement;
        if(fallbackOwner.previousTerrain.Count!=0||hardReplacement.FadeIn==0||fallbackTile.Root.activeSelf)throw new Exception("Prepared opaque morph lost its stamp or retained a second old draw");
        fallbackOwner.UpdateTerrainCoverage();yield return null;
        var preparedImage=Capture("prepared-opaque-before-hard");if(preparedImage[32*128+32].b<=preparedImage[32*128+32].g)throw new Exception("Prepared opaque terrain did not own its footprint immediately");
        TerrainVideoOptions.HardChunkUpdates=true;fallbackOwner.UpdateTerrainTransitions();fallbackOwner.UpdateTerrainCoverage();
        if(fallbackOwner.previousTerrain.Count!=0||fallbackTile.Root.activeSelf||hardReplacement.FadeIn!=0||Array.Exists(hardReplacement.Materials,m=>m.GetFloat("_TransitionEnabled")!=0))
            throw new Exception("Hard chunk updates retained an old surface or an active fade");
        var hardImage=Capture("hard-replacement");if(hardImage[32*128+32].b<=hardImage[32*128+32].g)throw new Exception("Old higher surface still occludes the instant replacement");
        var nextHard=new Tile{Root=Quad("Next instant replacement",1024,-2,Color.green,-1),Segments=16,Edges=hardReplacement.Edges,Heights=new float[289],FadeIn=fallbackOwner.TransitionNow};
        fallbackOwner.BeginTerrainReplacement(fallbackKey,nextHard,hardReplacement);fallbackOwner.tiles[fallbackKey]=nextHard;
        fallbackOwner.UpdateTerrainCoverage();
        if(nextHard.FadeIn!=0||fallbackOwner.previousTerrain.Count!=0||hardReplacement.Root.activeSelf)throw new Exception("New hard replacement still waited for a fade");
        var hardNextImage=Capture("hard-next-replacement");if(hardNextImage[32*128+32].g<=hardNextImage[32*128+32].b)throw new Exception("Subsequent instant replacement was not visible in the production shader");
        TerrainVideoOptions.HardChunkUpdates=false;fallbackOwner.UpdateTerrainTransitions();fallbackOwner.UpdateTerrainCoverage();
        if(Array.Exists(nextHard.Materials,m=>m.GetFloat("_TransitionEnabled")!=1))throw new Exception("Turning hard chunk updates off did not restore smooth rendering");
        TerrainVideoOptions.HardChunkUpdates=oldHard;
        File.WriteAllText(Path.Combine(output,"hard-chunk-updates.txt"),"PASS: prepared opaque replacements retain their morph stamp with no outgoing draw; hard mode clears the active stamp and forces final geometry, subsequent hard replacements snap, and disabling restores geometry morph support.");
        var overlapHost=new GameObject("Overlapping same-key fade regression");var overlapOwner=overlapHost.AddComponent<LocalTerrainExplorer>();overlapOwner.enabled=false;overlapOwner.terrainRoot=overlapHost;
        var priorParent=new Tile{Root=Quad("Parent before a leaf revision",4,2,Color.red),Edges=new double[4]};
        float parentStamp=overlapOwner.TransitionNow;
        var leafParent=new Tile{Root=Quad("Parent leaf revision",4,1,Color.red,2),Edges=new double[4],FadeIn=parentStamp};
        overlapOwner.tiles[parent]=priorParent;
        overlapOwner.BeginTerrainReplacement(parent,leafParent,priorParent);overlapOwner.tiles[parent]=leafParent;
        if(overlapOwner.previousTerrain.Count!=0||priorParent.Root.activeSelf||leafParent.FadeIn!=parentStamp)
            throw new Exception("A prepared leaf did not retain its stamp on its single opaque mesh");
        float childStamp=overlapOwner.TransitionNow;
        var arrivingChild=new Tile{Root=Quad("Child arriving during a parent revision",2,0,Color.green,1),Edges=new double[4],FadeIn=childStamp};
        overlapOwner.BeginTerrainReplacement(child,arrivingChild,null);overlapOwner.tiles[child]=arrivingChild;
        if(overlapOwner.previousTerrain.ContainsKey(parent)||leafParent.FadeIn!=0||priorParent.Root.activeSelf)
            throw new Exception("An arriving child retained an incompatible previous parent interior");
        if(arrivingChild.FadeIn!=childStamp)throw new Exception("Finishing an ancestor morph discarded the child's own prepared stamp");
        overlapOwner.UpdateTerrainCoverage();
        if(leafParent.Materials[0].GetVector("_LodFade").x!=0||arrivingChild.Materials[0].GetVector("_LodFade").x!=childStamp)
            throw new Exception("Ancestor completion and the child's independent morph stamp were not uploaded");
        var overlappingParent=new Tile{Root=Quad("Parent revision over existing children",4,3,Color.red,1),Edges=new double[4],FadeIn=overlapOwner.TransitionNow};
        overlapOwner.BeginTerrainReplacement(parent,overlappingParent,leafParent);overlapOwner.tiles[parent]=overlappingParent;
        if(overlapOwner.previousTerrain.ContainsKey(parent)||overlappingParent.FadeIn!=0||leafParent.Root.activeSelf)
            throw new Exception("Existing children retained a fading previous same-key parent");
        float leafStamp=overlapOwner.TransitionNow;
        var leafChild=new Tile{Root=Quad("Child leaf revision",2,.5f,Color.green,0),Edges=new double[4],FadeIn=leafStamp};
        overlapOwner.BeginTerrainReplacement(child,leafChild,arrivingChild);overlapOwner.tiles[child]=leafChild;
        if(overlapOwner.previousTerrain.Count!=0||arrivingChild.Root.activeSelf||leafChild.FadeIn!=leafStamp)
            throw new Exception("A child leaf revision did not preserve its prepared opaque morph");
        foreach(var resident in overlapOwner.tiles.Values)resident.Root.SetActive(false);
        Destroy(overlapHost);
        File.WriteAllText(Path.Combine(output,"opaque-parent-morphs.txt"),"PASS: prepared leaf stamps survive single-mesh publication without outgoing draws; an arriving child finishes its ancestor before exposing an interior cut, child stamps remain independent, and ancestor replacements with existing children snap.");
        CheckPartialForestEdgeGround(output);CheckHiddenResidentRetirement(output);
        File.WriteAllText(Path.Combine(output,"coverage-shader.txt"),"PASS actual production shader: opaque binary finest coverage, cache restore/invalidation including identical-height no-op, upper/lower geometry morph at start/mid/end, oblique halfway-height references, pinned boundaries without perspective holes, near/hard final-geometry guards, prepared-leaf and ancestor-completion lifecycle, valid fallback publication, planned/unplanned backdrop handling, finer-child retention, paced retirement and owner-inert covered-parent removal.");
        TerrainVideoOptions.HardChunkUpdates=savedHardSetting;Application.Quit(0);
    }
    static void CheckTerrainPriorityOrder(string output)
    {
        var host=new GameObject("CPU-only terrain priority proof");var owner=host.AddComponent<LocalTerrainExplorer>();owner.enabled=false;
        var receipts=new System.Collections.Generic.List<string>();
        try
        {
            foreach(double scale in new[]{.1,1d})
            {
                owner.explorationSettings=new WorldGen.Core.PlaySettings{HorizontalScale=scale};owner.priority.Clear();
                for(int z=-15;z<15;z++)for(int x=-30;x<30;x++)owner.priority.Add(new TerrainTileKey(x,z,(x+30+z+15)%6,scale));
                for(int reversed=0;reversed<2;reversed++)
                {
                    if(reversed!=0)owner.priority.Reverse();var original=owner.priority.ToArray();
                    double predictionX=owner.streamingPriorityX,predictionZ=owner.streamingPriorityZ;
                    string proof=owner.PerformanceVerifyTerrainPrioritySort();
                    if(owner.priority.Count!=original.Length)throw new Exception("Priority proof changed the caller's key count");
                    for(int i=0;i<original.Length;i++)if(!owner.priority[i].Equals(original[i]))throw new Exception("Priority proof did not restore the original key order");
                    if(!owner.streamingPriorityX.Equals(predictionX)||!owner.streamingPriorityZ.Equals(predictionZ))throw new Exception("Priority proof did not restore the prediction state");
                    receipts.Add("scale="+scale+" reversed="+(reversed!=0)+" "+proof);
                }
            }
            File.WriteAllText(Path.Combine(output,"priority-order.txt"),string.Join("\n",receipts)+"\nPASS CPU-only Mono priority proof: 1800 negative/mixed-level keys, two scales, original/reversed orders, all three policies and caller-state restoration; no shader/camera/draw setup.\n");
        }
        finally{Destroy(host);}
    }
    static void CheckRefinedResidentSeam(string fixture,string output)
    {
        var host=new GameObject("Actual refined resident seam proof");var owner=host.AddComponent<LocalTerrainExplorer>();owner.enabled=false;
        owner.terrainRoot=host;owner.explorationSettings=new WorldGen.Core.PlaySettings{HorizontalScale=.1};
        owner.frame=new LocalTerrainFrame(new WorldGen.Core.V(1,0,0),6371000,0,.1);
        var keys=new[]{new TerrainTileKey(0,-1,1,.1),new TerrainTileKey(-1,-1,0,.1),new TerrainTileKey(-1,-2,0,.1)};
        var plan=new System.Collections.Generic.Dictionary<TerrainTileKey,int>{{keys[0],32},{keys[1],32},{keys[2],8}};var edges=TerrainEdges.Build(plan);
        var shader=Resources.Load<Shader>("WorldGenLocalTerrain");if(shader==null||!shader.isSupported)throw new Exception("Production terrain material unavailable for seam proof");
        try
        {
            for(int index=0;index<keys.Length;index++)
            {
                LocalTerrainMesh source;using(var stream=File.OpenRead(Path.Combine(fixture,index+".mesh")))using(var reader=new BinaryReader(stream))source=TerrainMeshWire.ReadMesh(reader);
                var key=keys[index];if(source.Segments!=plan[key]||Math.Abs(source.SizeM-key.SizeM)>.00001||Math.Abs(source.EastM-key.X*key.SizeM)>.00001||Math.Abs(source.NorthM-key.Z*key.SizeM)>.00001)
                    throw new Exception("Refined seam fixture does not match its real tile address/detail");
                var heights=new float[source.GroundVertices];for(int i=0;i<heights.Length;i++)heights[i]=source.Positions[i*3+1];
                var root=new GameObject("Resident seam tile "+index);root.transform.SetParent(host.transform,false);root.transform.localPosition=new Vector3((float)source.EastM,0,(float)source.NorthM);
                var material=new Material(shader);ResidentEdgeData data;
                using(var vertices=new Unity.Collections.NativeArray<TerrainVertex>(source.Positions.Length/3,Unity.Collections.Allocator.Temp))
                {
                    var writable=vertices;for(int i=0;i<writable.Length;i++)writable[i]=new TerrainVertex{Position=new Vector3(source.Positions[i*3],source.Positions[i*3+1],source.Positions[i*3+2])};
                    data=ResidentEdgeData.Build(source,vertices);
                }
                owner.tiles.Add(key,new Tile{Root=root,Materials=new[]{material},Segments=source.Segments,Edges=edges[key],Heights=heights,ResidentEdges=data,Contact=TerrainContactSurface.Build(source)});
                owner.CoverageIndex(key,true);
            }
            var coarse=owner.tiles[keys[0]];var fine=owner.tiles[keys[1]];int witness=-1;
            for(int i=0;i<fine.ResidentEdges.Points.Length;i++)if(fine.ResidentEdges.Points[i].Vertex==1055){witness=i;break;}
            if(witness<0||(fine.ResidentEdges.Points[witness].Mask&2)==0||fine.Contact==null)throw new Exception("Actual used refined east-edge witness1055 is absent");
            var point=fine.ResidentEdges.Points[witness].Position;double x=keys[1].X*keys[1].SizeM+point.x,z=keys[1].Z*keys[1].SizeM+point.z;
            if(!ResidentHeight(keys[0],coarse,x,z,out double beforeCoarse))throw new Exception("Raw coarse seam profile missing");
            double beforeGap=Math.Abs(point.y-beforeCoarse);if(beforeGap<=.1)throw new Exception("Fixture does not exercise the reported used-vertex raw gap: "+beforeGap);
            owner.residentRevision++;owner.UpdateResidentEdges();
            int shared=0,published=0;double maximumGap=0,maximumContact=0;
            foreach(int index in fine.ResidentEdges.Sides[1])
            {
                var p=fine.ResidentEdges.Points[index];double xx=keys[1].X*keys[1].SizeM+p.Position.x,zz=keys[1].Z*keys[1].SizeM+p.Position.z;
                if(zz<keys[0].Z*keys[0].SizeM||zz>(keys[0].Z+1)*keys[0].SizeM)continue;
                if(!ResidentHeight(keys[0],coarse,xx,zz,out double coarseHeight)||!ResidentHeight(keys[1],fine,xx,zz,out double fineHeight))throw new Exception("Fitted shared boundary profile absent");
                maximumGap=Math.Max(maximumGap,Math.Abs(fineHeight-coarseHeight));
                if(!fine.Contact.TryHeight(p.Position.x,p.Position.z,out double contact))throw new Exception("Actual fine contact triangle absent at used edge vertex");
                maximumContact=Math.Max(maximumContact,Math.Abs(contact-fineHeight));
                if(coarse.Contact!=null)
                {
                    if(!coarse.Contact.TryHeight(xx-keys[0].X*keys[0].SizeM,zz-keys[0].Z*keys[0].SizeM,out double otherContact))throw new Exception("Actual coarse contact triangle absent at shared edge");
                    maximumContact=Math.Max(maximumContact,Math.Abs(otherContact-coarseHeight));
                }
                shared++;
            }
            if(shared<2||maximumGap>=.001||maximumContact>=.001)throw new Exception("Production fitted shared/contact gap exceeds1mm: shared="+shared+" gap="+maximumGap+" contact="+maximumContact);
            foreach(var tile in owner.tiles.Values)
            {
                var data=tile.ResidentEdges;if(!data.Conformed)continue;
                if(data.Texture==null||data.Texture.format!=TextureFormat.RFloat||tile.Materials[0].GetTexture("_ResidentEdgeHeights")!=data.Texture||tile.Materials[0].GetFloat("_ResidentEdgeCount")!=data.Points.Length)
                    throw new Exception("Production edge texture/material binding is missing");
                var values=data.Texture.GetRawTextureData<float>();if(values.Length!=data.Heights.Length)throw new Exception("Published RFloat row has incorrect size");
                for(int i=0;i<values.Length;i++)
                {
                    if(BitConverter.SingleToInt32Bits(values[i])!=BitConverter.SingleToInt32Bits(data.Heights[i])||BitConverter.SingleToInt32Bits(values[i])!=BitConverter.SingleToInt32Bits(data.Published[i]))throw new Exception("Published GPU height differs from fitted CPU edge height");
                    published++;
                }
            }
            if(!fine.ResidentEdges.Conformed||published==0)throw new Exception("Refined witness did not exercise actual RFloat publication");
            File.WriteAllText(Path.Combine(output,"resident-refined-seam.txt"),"PASS actual3-mesh used-vertex fixture; raw gap="+beforeGap.ToString("R")+"m; raw fine/coarse="+point.y.ToString("R")+"/"+beforeCoarse.ToString("R")+"; shared used points="+shared+"; fitted maximum gap="+maximumGap.ToString("R")+"m; contact maximum="+maximumContact.ToString("R")+"m; published bit-exact RFloat components="+published+"; actual material/count bindings verified.\n"+owner.ResidentEdgeReceipt);
        }
        finally
        {
            foreach(var tile in owner.tiles.Values){owner.ReleaseResidentEdges(tile);foreach(var material in tile.Materials)Destroy(material);Destroy(tile.Root);}
            owner.tiles.Clear();Destroy(host);
        }
    }
    static void CheckPartialForestEdgeGround(string output)
    {
        var host=new GameObject("Partial forest edge grounding check");var owner=host.AddComponent<LocalTerrainExplorer>();owner.enabled=false;
        int n=4;double size=TerrainLod.BaseSizeM*owner.HorizontalScale,step=size/n;var key=new TerrainTileKey(0,0,0,owner.HorizontalScale);
        var heights=new float[(n+1)*(n+1)];for(int i=0;i<heights.Length;i++)heights[i]=100;
        owner.tiles[key]=new Tile{Segments=n,Heights=heights};
        var batch=new ForestBatch();var field=new ForestField{Source=new WorldGen.Core.ForestPage(),Bounds=new Bounds(new Vector3((float)size*.5f,100,(float)size*.5f),new Vector3((float)size,20,(float)size))};
        var slots=new System.Collections.Generic.List<int>();
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            var root=new ForestRoot{Position=new Vector3((float)((x+.5)*step),100,(float)((z+.5)*step)),Width=1,Height=4,Padding=2,Tint=Vector3.one};
            owner.ProjectForestRootGround(ref root,out int level);slots.Add(batch.Data.Count);batch.Data.Add(root);batch.GroundLevels.Add(level);
        }
        field.Roots.Add(batch,slots);owner.forestFields.Add(new WorldGen.Core.ForestPageKey(0,0,0,0),field);
        var baseline=batch.Data.ToArray();var baselineLevels=batch.GroundLevels.ToArray();var bounds=field.Bounds;
        int changedVertex=(n/2)*(n+1);heights[changedVertex]+=20;
        var partial=TerrainHeightChange.FromVertices(n,new[]{changedVertex});if(partial==null)throw new Exception("Changed edge vertex produced no affected cells");
        owner.scatterGroundChanges.Add(key);owner.ProjectForestGround();var expected=batch.Data.ToArray();var expectedLevels=batch.GroundLevels.ToArray();var expectedBounds=field.Bounds;
        for(int i=0;i<baseline.Length;i++){batch.Data[i]=baseline[i];batch.GroundLevels[i]=baselineLevels[i];}field.Bounds=bounds;
        owner.scatterGroundChanges.Clear();owner.scatterGroundChanges.Add(key,partial);
        if(owner.scatterGroundChanges.Affects(key,size*.8,size*.8,.01))throw new Exception("Edge-only notification invalidated the unchanged tile interior");
        owner.ProjectForestGround();int moved=0,unchanged=0;
        for(int i=0;i<baseline.Length;i++)
        {
            if(!batch.Data[i].Equals(expected[i])||batch.GroundLevels[i]!=expectedLevels[i])throw new Exception("Partial edge grounding differs from literal full forest reprojection");
            if(batch.Data[i].SlopeX!=0||batch.Data[i].SlopeZ!=0)throw new Exception("Partial edge grounding tilted an upright tree");
            if(batch.Data[i].Position.y!=baseline[i].Position.y)moved++;else unchanged++;
        }
        if(moved==0||unchanged==0||!field.Bounds.Equals(expectedBounds))throw new Exception("Partial forest fixture did not preserve both changed roots and unchanged interior/bounds");
        owner.scatterGroundChanges.Clear();owner.scatterGroundChanges.Add(key);owner.scatterGroundChanges.Add(key,partial);
        if(!owner.scatterGroundChanges.Affects(key,size*.8,size*.8,.01))throw new Exception("A partial edge change narrowed an existing whole-tile notification");
        owner.scatterGroundChanges.Clear();owner.scatterGroundChanges.Add(new TerrainTileKey(key.X>>2,key.Z>>2,key.Level+2,key.Scale));
        owner.ProjectForestGround();
        for(int i=0;i<expected.Length;i++)
            if(!batch.Data[i].Equals(expected[i])||batch.GroundLevels[i]!=expectedLevels[i])throw new Exception("A changed coarse ancestor moved a root still owned by finer resident ground");
        if(!field.Bounds.Equals(expectedBounds))throw new Exception("Coarse ancestor changed finer-owned forest bounds");
        owner.scatterGroundChanges.Clear();owner.forestFields.Clear();owner.tiles.Clear();batch.Dispose();Destroy(host);
        File.WriteAllText(Path.Combine(output,"partial-forest-edge-ground.txt"),"PASS: edge-cell notification produces the same forest positions, owner levels, upright poses and bounds as literal full reprojection; changed roots move, unchanged interior remains untouched, coarse ancestors preserve finer-owned roots, and whole-tile invalidation cannot be narrowed.");
    }
    static void CheckHiddenResidentRetirement(string output)
    {
        var host=new GameObject("Covered parent edge-retirement check");var owner=host.AddComponent<LocalTerrainExplorer>();owner.enabled=false;
        double scale=owner.HorizontalScale,unit=TerrainLod.BaseSizeM*scale;var parent=new TerrainTileKey(-1,-1,1,scale);var grand=new TerrainTileKey(-1,-1,2,scale);
        Tile Surface(TerrainTileKey key,float height)
        {
            float size=(float)key.SizeM;var positions=new[]{0f,height,0f,size,height,0f,0f,height,size,size,height,size};
            var vertices=new TerrainVertex[4];for(int i=0;i<4;i++)vertices[i]=new TerrainVertex{Position=new Vector3(positions[i*3],height,positions[i*3+2])};
            using(var native=new Unity.Collections.NativeArray<TerrainVertex>(vertices,Unity.Collections.Allocator.Temp))
                return new Tile{Segments=1,Edges=new double[4],Heights=new[]{height,height,height,height},ResidentEdges=ResidentEdgeData.Build(new LocalTerrainMesh{SizeM=key.SizeM,Segments=1,GroundVertices=4,Positions=positions,Triangles=new[]{0,2,1,1,2,3}},native)};
        }
        var surfaces=new System.Collections.Generic.List<Tile>();
        void Add(TerrainTileKey key,float height){var tile=Surface(key,height);surfaces.Add(tile);owner.tiles.Add(key,tile);owner.CoverageIndex(key,true);}
        Add(grand,2000);Add(parent,1000);
        for(int z=0;z<2;z++)for(int x=0;x<2;x++)Add(new TerrainTileKey(-2+x,-2+z,0,scale),20);
        var parentTile=owner.tiles[parent];
        if(!owner.ResidentDescendantsCover(parent))throw new Exception("Four current height-bearing descendants did not prove complete parent coverage");
        double epsilon=.002;var offsets=new[]{-epsilon,0,epsilon,unit-epsilon,unit,unit+epsilon,unit*2-epsilon,unit*2,unit*2+epsilon};
        var beforeOwners=new Tile[offsets.Length*offsets.Length];var beforeKeys=new TerrainTileKey[beforeOwners.Length];var beforeHeights=new double[beforeOwners.Length];
        int at=0;foreach(double z in offsets)foreach(double x in offsets)
        {
            double xx=-unit*2+x,zz=-unit*2+z;var tile=owner.VisibleGroundOwner(xx,zz,out var key);beforeOwners[at]=tile;beforeKeys[at]=key;
            if(tile!=null&&!ResidentHeight(key,tile,xx,zz,out beforeHeights[at]))throw new Exception("Retirement owner probe had no height");at++;
        }
        owner.tiles.Remove(parent);at=0;
        foreach(double z in offsets)foreach(double x in offsets)
        {
            double xx=-unit*2+x,zz=-unit*2+z;var tile=owner.VisibleGroundOwner(xx,zz,out var key);
            if(!ReferenceEquals(tile,beforeOwners[at])||!key.Equals(beforeKeys[at]))throw new Exception("Covered-parent removal changed an edge/corner incident owner");
            if(tile!=null&&(!ResidentHeight(key,tile,xx,zz,out double height)||height!=beforeHeights[at]))throw new Exception("Covered-parent removal changed an incident height profile");at++;
        }
        // Leave the reverse index stale deliberately: it is never sufficient
        // positive evidence when a quadrant is actually missing or nonphysical.
        owner.tiles[parent]=parentTile;var hole=new TerrainTileKey(-1,-2,0,scale);var holeTile=owner.tiles[hole];owner.tiles.Remove(hole);
        if(owner.ResidentDescendantsCover(parent))throw new Exception("A stale descendant index hid a remaining visible parent patch");
        var leftKey=new TerrainTileKey(-2,-2,0,scale);var left=owner.tiles[leftKey];owner.edgeOwners.Clear();float beforeTarget=owner.ResidentTarget(leftKey,left,3);
        owner.tiles.Remove(parent);owner.edgeOwners.Clear();float afterTarget=owner.ResidentTarget(leftKey,left,3);
        if(beforeTarget!=1000||afterTarget!=2000)throw new Exception("Removing a visible parent patch did not require the neighbouring edge to refit to its new coarse profile");
        owner.tiles[hole]=new Tile{Segments=1};
        if(owner.ResidentDescendantsCover(parent))throw new Exception("A non-height-bearing placeholder was counted as physical descendant coverage");
        owner.tiles[hole]=holeTile;
        foreach(var tile in surfaces)owner.ReleaseResidentEdges(tile);owner.tiles.Clear();Destroy(host);
        File.WriteAllText(Path.Combine(output,"covered-parent-retirement.txt"),"PASS: 81 negative-coordinate owner/height probes, including outer edge/corner incident positions, remain identical after a fully covered parent is removed. With a missing quadrant the proof fails and the real neighbouring ResidentTarget changes from 1000 to 2000; heightless placeholders cannot satisfy coverage.");
    }
}
