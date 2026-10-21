using System.Linq;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    bool coverageDirty;
    void UpdateTerrainCoverage()
    {
        if(!coverageDirty)return;coverageDirty=false;var keys=tiles.Keys.ToArray();
        // Only actual resident ancestors can need holes. Avoid scanning all
        // resident tiles for every tile whenever a single product arrives.
        var children=new System.Collections.Generic.Dictionary<TerrainTileKey,System.Collections.Generic.List<TerrainTileKey>>();
        int maximum=keys.Length==0?0:keys.Max(k=>k.Level);
        foreach(var fine in keys)for(int depth=1;depth<=maximum-fine.Level;depth++)
        {
            var ancestor=new TerrainTileKey(fine.X>>depth,fine.Z>>depth,fine.Level+depth,fine.Scale);
            if(!tiles.ContainsKey(ancestor))continue;
            if(!children.TryGetValue(ancestor,out var list)){list=new System.Collections.Generic.List<TerrainTileKey>();children.Add(ancestor,list);}list.Add(fine);
        }
        foreach(var pair in tiles)
        {
            var tile=pair.Value;var nodes=children.TryGetValue(pair.Key,out var finer)?TerrainCoverage.Build(pair.Key,finer):System.Array.Empty<float>();
            if(tile.CoverageNodes!=null&&tile.CoverageNodes.SequenceEqual(nodes))continue;
            tile.CoverageNodes=nodes;
            if(tile.Coverage!=null){Destroy(tile.Coverage);tile.Coverage=null;}
            if(nodes.Length>0)
            {
                int count=nodes.Length/4,height=(count+255)/256;var pixels=new Color[256*height];
                for(int i=0;i<count;i++)pixels[i]=new Color(nodes[i*4],nodes[i*4+1],nodes[i*4+2],nodes[i*4+3]);
                tile.Coverage=new Texture2D(256,height,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                tile.Coverage.SetPixels(pixels);tile.Coverage.Apply(false,true);
            }
            var properties=new MaterialPropertyBlock();properties.SetFloat("_CoverageEnabled",nodes.Length>0?1:0);
            properties.SetFloat("_CoverageTileSize",(float)pair.Key.SizeM);
            if(tile.Coverage!=null)properties.SetTexture("_TerrainCoverage",tile.Coverage);
            foreach(var renderer in tile.Root.GetComponentsInChildren<MeshRenderer>())renderer.SetPropertyBlock(properties);
        }
    }
}
