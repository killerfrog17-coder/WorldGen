using System.Linq;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    bool coverageDirty;
    void UpdateTerrainCoverage()
    {
        if(!coverageDirty)return;coverageDirty=false;var keys=tiles.Keys.ToArray();
        foreach(var pair in tiles)
        {
            var tile=pair.Value;var nodes=TerrainCoverage.Build(pair.Key,keys);
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
