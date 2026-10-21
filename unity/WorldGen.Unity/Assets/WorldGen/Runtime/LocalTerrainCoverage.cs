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
            var tile=pair.Value;var tree=children.TryGetValue(pair.Key,out var finer)?TerrainCoverage.BuildFaded(pair.Key,finer.Select(k=>TerrainTiming(k,true))):new TerrainFadeTree();
            var nodes=tree.Nodes;
            if(tile.CoverageNodes==null||!tile.CoverageNodes.SequenceEqual(nodes)||tile.CoverageTimingNodes==null||!tile.CoverageTimingNodes.SequenceEqual(tree.Timings))
            {
                tile.CoverageNodes=nodes;tile.CoverageTimingNodes=tree.Timings;
                if(tile.Coverage!=null)Destroy(tile.Coverage);if(tile.CoverageTimes!=null)Destroy(tile.CoverageTimes);
                Texture2D Texture(float[] values)
                {
                    if(values.Length==0)return null;int count=values.Length/4,height=(count+255)/256;var pixels=new Color[256*height];
                    for(int i=0;i<count;i++)pixels[i]=new Color(values[i*4],values[i*4+1],values[i*4+2],values[i*4+3]);
                    var t=new Texture2D(256,height,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};t.SetPixels(pixels);t.Apply(false,true);return t;
                }
                tile.Coverage=Texture(nodes);tile.CoverageTimes=Texture(tree.Timings);
            }
            var properties=new MaterialPropertyBlock();properties.SetFloat("_CoverageEnabled",nodes.Length>0?1:0);
            properties.SetFloat("_CoverageTileSize",(float)pair.Key.SizeM);
            if(tile.Coverage!=null)properties.SetTexture("_TerrainCoverage",tile.Coverage);
            if(tile.CoverageTimes!=null)properties.SetTexture("_CoverageFadeTimes",tile.CoverageTimes);
            var timing=TerrainTiming(pair.Key);properties.SetFloat("_TransitionEnabled",1);
            properties.SetFloat("_LodSafetyRadius",view==null?0:(float)System.Math.Max(12,TerrainLod.BaseSizeM*HorizontalScale/64*3));
            properties.SetVector("_LodFade",new Vector4(timing.In,timing.Out,0,0));
            foreach(var renderer in tile.Root.GetComponentsInChildren<MeshRenderer>())renderer.SetPropertyBlock(properties);
            if(previousTerrain.TryGetValue(pair.Key,out var previous))
            {
                properties.SetVector("_LodFade",new Vector4(timing.In,timing.Out,1,0));
                foreach(var renderer in previous.Root.GetComponentsInChildren<MeshRenderer>())renderer.SetPropertyBlock(properties);
            }
        }
    }
}
