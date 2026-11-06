using System.Linq;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    bool coverageDirty;
    float[] coveragePixelScratch=System.Array.Empty<float>();
    Texture2D CoverageTexture(Texture2D texture,float[] values)
    {
        if(values.Length==0){if(texture!=null)Destroy(texture);return null;}
        int height=(values.Length/4+255)/256,count=256*height*4;
        if(texture==null||texture.width!=256||texture.height!=height)
        {
            if(texture!=null)Destroy(texture);
            texture=new Texture2D(256,height,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        }
        if(coveragePixelScratch.Length<count)coveragePixelScratch=new float[count];
        System.Array.Copy(values,coveragePixelScratch,values.Length);
        System.Array.Clear(coveragePixelScratch,values.Length,count-values.Length);
        texture.SetPixelData(coveragePixelScratch,0);texture.Apply(false,false);return texture;
    }
    void UpdateTerrainCoverage()
    {
        if(!coverageDirty)return;coverVisibilityRevision++;long started=WorldGenPerformance.TraceBegin();coverageDirty=false;var keys=tiles.Keys.ToArray();
        // Coverage masks can use a coarse fallback, but the reveal frontier must
        // wait for the requested sampling detail. Include resolution changes.
        var surfaces=tiles.Select(p=>new System.Collections.Generic.KeyValuePair<TerrainTileKey,int>(p.Key,p.Value.Segments));
        if(fogFootprint==null||!fogFootprint.Matches(surfaces)){fogFootprint=new TerrainQualityFootprint(surfaces);fogCheckAt=0;}
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
                // Keep native texture objects across timing/content updates.
                // One shared padded float buffer replaces a fresh Color[] per
                // upload; the texture owns its readable copy after SetPixelData.
                tile.Coverage=CoverageTexture(tile.Coverage,nodes);tile.CoverageTimes=CoverageTexture(tile.CoverageTimes,tree.Timings);
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
