using System.Linq;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    bool coverageDirty;
    void UpdateTerrainCoverage()
    {
        if(!coverageDirty)return;coverVisibilityRevision++;long started=WorldGenPerformance.TraceBegin();coverageDirty=false;var keys=tiles.Keys.ToArray();
        // Reuse this index through timestamp/material-only changes. Do not key
        // fog readiness to the requested detail plan or inactive mesh cache.
        if(fogFootprint==null||!fogFootprint.Matches(keys)){fogFootprint=new TerrainCoverageFootprint(keys);fogCheckAt=0;}
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
            var timing=TerrainTiming(pair.Key);
            BindTerrainTile(tile,tile,pair.Key,new Vector4(timing.In,timing.Out,0,0));
            if(previousTerrain.TryGetValue(pair.Key,out var previous))
            {
                BindTerrainTile(previous,tile,pair.Key,new Vector4(timing.In,timing.Out,1,0));
            }
        }
        WorldGenPerformance.TraceEnd(6,started);
    }
}
