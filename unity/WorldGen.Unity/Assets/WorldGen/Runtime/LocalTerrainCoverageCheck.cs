using System;
using System.Collections;
using System.IO;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public static IEnumerator CheckCoverageShader(string output)
    {
        Directory.CreateDirectory(output);
        Application.logMessageReceived+=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        var host=new GameObject("Coverage shader check");var explorer=host.AddComponent<LocalTerrainExplorer>();explorer.enabled=false;
        explorer.terrainRoot=host;
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
        tile.FadeIn=0;explorer.coverageDirty=true;explorer.UpdateTerrainCoverage();yield return null;var reused=Capture("cached-replacement");
        if(reused[32*128+32].g<=reused[32*128+32].r||explorer.TerrainCacheHits!=1)throw new Exception("Cached mesh failed to render after restore");
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
        var grand=new TerrainTileKey(0,0,2,1d/512);explorer.tiles[grand]=new Tile{Root=Quad("grand fallback",8,3,Color.red)};
        explorer.BeginTerrainReplacement(parent,newParent,null);explorer.tiles[parent]=newParent;
        if(newParent.FadeIn!=0)throw new Exception("Coarsening unnecessarily faded through a grandparent");
        explorer.tiles.Remove(child);explorer.CacheTerrain(child,tile);explorer.ResetStreamingReuse(false);
        if(explorer.CachedTerrainTiles!=0)throw new Exception("Chart invalidation retained old products");
        File.WriteAllText(Path.Combine(output,"coverage-shader.txt"),"PASS actual production shader: solid/fallback coverage, exact cache restore/invalidation, complementary halfway incoming/outgoing frames without holes, retained finer children during same-key parent replacement and coarsening without grandparent regression.");
        Application.Quit(0);
    }
}
