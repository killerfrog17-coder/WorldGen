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
        GameObject Quad(string name,float size,float y,Color color)
        {
            var root=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            var mesh=new Mesh{vertices=new[]{new Vector3(0,y,0),new Vector3(size,y,0),new Vector3(0,y,size),new Vector3(size,y,size)},
                normals=new[]{Vector3.up,Vector3.up,Vector3.up,Vector3.up},colors=new[]{color,color,color,color},triangles=new[]{0,2,1,1,2,3}};
            root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<MeshRenderer>().sharedMaterial=material;return root;
        }
        var parent=new TerrainTileKey(0,0,1,1d/512);var child=new TerrainTileKey(0,0,0,1d/512);
        explorer.tiles.Add(parent,new Tile{Root=Quad("coarse fallback",4,2,Color.red)});
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
        explorer.previousTerrain.Remove(child);explorer.DestroyTile(tile);tile=identical;tile.FadeIn=0;
        explorer.coverageDirty=true;explorer.UpdateTerrainCoverage();yield return null;
        while(Time.time<1.6f)yield return null;
        tile.FadeIn=Time.time-1.5f;explorer.coverageDirty=true;explorer.UpdateTerrainCoverage();yield return null;
        var half=Capture("half-transition");int reds=0,greens=0,holes=0;
        for(int y=8;y<56;y++)for(int x=8;x<56;x++){var p=half[y*128+x];if(p.r>p.g+.05)reds++;else if(p.g>p.r+.05)greens++;else holes++;}
        if(reds<500||greens<500||holes>10)throw new Exception("Complementary half fade loses coverage or draws only one LOD: "+reds+"/"+greens+"/"+holes);
        tile.FadeIn=0;tile.FadeOut=Time.time-1.5f;explorer.coverageDirty=true;explorer.UpdateTerrainCoverage();yield return null;
        var outgoing=Capture("half-coarsening");reds=greens=holes=0;
        for(int y=8;y<56;y++)for(int x=8;x<56;x++){var p=outgoing[y*128+x];if(p.r>p.g+.05)reds++;else if(p.g>p.r+.05)greens++;else holes++;}
        if(reds<500||greens<500||holes>10)throw new Exception("Outgoing fine/coarse complement loses coverage");
        tile.FadeOut=0;
        var oldParent=explorer.tiles[parent];var newParent=new Tile{Root=Quad("same-key replacement",4,2,Color.red),Edges=new double[4]};
        explorer.BeginTerrainReplacement(parent,newParent,oldParent);explorer.tiles[parent]=newParent;
        if(explorer.TerrainTiming(child).In>0)throw new Exception("Replacing a parent suppressed already-resident children");
        explorer.previousTerrain.Remove(parent);explorer.DestroyTile(oldParent);explorer.tiles.Remove(parent);
        var grand=new TerrainTileKey(0,0,2,1d/512);explorer.tiles[grand]=new Tile{Root=Quad("grand fallback",8,3,Color.red),Edges=new double[4]};
        explorer.BeginTerrainReplacement(parent,newParent,null);explorer.tiles[parent]=newParent;
        if(newParent.FadeIn!=0)throw new Exception("Coarsening unnecessarily faded through a grandparent");
        explorer.wanted.Clear();explorer.wanted.Add(child,64);tile.Segments=64;tile.FadeIn=tile.FadeOut=0;
        newParent.FadeOut=Math.Max(.001f,Time.time-4);explorer.UpdateTerrainTransitions();
        if(newParent.FadeOut!=0||explorer.TerrainTiming(child).Out!=0||explorer.TerrainTiming(child).In!=0)
            throw new Exception("Reversing coarsening left wanted fine terrain inheriting an obsolete parent fade");
        explorer.coverageDirty=true;explorer.UpdateTerrainCoverage();yield return null;
        var reversed=Capture("coarsening-reversed");
        if(reversed[32*128+32].g<=reversed[32*128+32].r)throw new Exception("Reversed coarsening did not restore the wanted fine surface");
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
        fallbackOwner.forestFields.Add(new WorldGen.Core.ForestPageKey(0,0,0,0),new ForestField{Source=new WorldGen.Core.ForestPage()});
        var fallbackTile=new Tile{Root=Quad("Valid nonfinal coverage",1024,0,Color.green),Segments=8,Edges=fallbackEdges,Heights=new float[81]};
        bool oldStaging=WorldGenPerformance.StagedGroundPublication;WorldGenPerformance.StagedGroundPublication=true;
        if(!fallbackOwner.StageGroundPublication(fallbackKey,fallbackTile,fallback:true))throw new Exception("Fallback regression did not exercise staged publication");
        for(int i=0;i<10&&fallbackOwner.GroundPublicationPending;i++)fallbackOwner.AdvanceGroundPublication();
        if(fallbackOwner.GroundPublicationPending||!fallbackOwner.tiles.TryGetValue(fallbackKey,out var shown)||!ReferenceEquals(shown,fallbackTile)||!shown.Root.activeSelf)
            throw new Exception("Useful fallback was rejected merely for differing from the final mesh detail");
        WorldGenPerformance.StagedGroundPublication=oldStaging;
        File.WriteAllText(Path.Combine(output,"fallback-publication.txt"),"PASS: a useful staged 8-segment fallback publishes while its same-address final request still requires 16 segments and different edges; no reject/restage loop.");
        bool oldHard=TerrainVideoOptions.HardChunkUpdates;TerrainVideoOptions.HardChunkUpdates=false;
        var hardReplacement=new Tile{Root=Quad("Hard-update replacement",1024,-1,Color.blue),Segments=16,Edges=new[]{64d,64d,64d,64d},Heights=new float[289]};
        fallbackOwner.BeginTerrainReplacement(fallbackKey,hardReplacement,fallbackTile);fallbackOwner.tiles[fallbackKey]=hardReplacement;
        if(fallbackOwner.previousTerrain.Count!=1||hardReplacement.FadeIn==0)throw new Exception("Hard-update regression did not begin with a live smooth transition");
        TerrainVideoOptions.HardChunkUpdates=true;fallbackOwner.UpdateTerrainTransitions();fallbackOwner.UpdateTerrainCoverage();
        if(fallbackOwner.previousTerrain.Count!=0||fallbackTile.Root.activeSelf||hardReplacement.FadeIn!=0||Array.Exists(hardReplacement.Materials,m=>m.GetFloat("_TransitionEnabled")!=0))
            throw new Exception("Hard chunk updates retained an old surface or an active fade");
        var hardImage=Capture("hard-replacement");if(hardImage[32*128+32].b<=hardImage[32*128+32].g)throw new Exception("Old higher surface still occludes the instant replacement");
        var nextHard=new Tile{Root=Quad("Next instant replacement",1024,-2,Color.green),Segments=16,Edges=hardReplacement.Edges,Heights=new float[289]};
        fallbackOwner.BeginTerrainReplacement(fallbackKey,nextHard,hardReplacement);fallbackOwner.tiles[fallbackKey]=nextHard;
        fallbackOwner.UpdateTerrainCoverage();
        if(nextHard.FadeIn!=0||fallbackOwner.previousTerrain.Count!=0||hardReplacement.Root.activeSelf)throw new Exception("New hard replacement still waited for a fade");
        var hardNextImage=Capture("hard-next-replacement");if(hardNextImage[32*128+32].g<=hardNextImage[32*128+32].b)throw new Exception("Subsequent instant replacement was not visible in the production shader");
        TerrainVideoOptions.HardChunkUpdates=false;fallbackOwner.UpdateTerrainTransitions();fallbackOwner.UpdateTerrainCoverage();
        if(Array.Exists(nextHard.Materials,m=>m.GetFloat("_TransitionEnabled")!=1))throw new Exception("Turning hard chunk updates off did not restore smooth rendering");
        TerrainVideoOptions.HardChunkUpdates=oldHard;
        File.WriteAllText(Path.Combine(output,"hard-chunk-updates.txt"),"PASS: toggling during an active same-address replacement removes the old draw and all fade stamps immediately; subsequent ready replacements snap; disabling restores smooth shader transitions.");
        File.WriteAllText(Path.Combine(output,"coverage-shader.txt"),"PASS actual production shader: solid/fallback coverage, cache restore/invalidation including identical-height no-op, complementary incoming/outgoing frames without holes, planned backdrop retained/unplanned backdrop releasable, finer children retained during parent replacement and coarsening without grandparent regression; paced retirement releases one redundant tile per shared installation allowance without dropping wanted terrain.");
        TerrainVideoOptions.HardChunkUpdates=savedHardSetting;Application.Quit(0);
    }
}
