using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    bool coverageDirty;
    float[] coveragePixelScratch=Array.Empty<float>();
    readonly List<TerrainFadeStamp> coverageTimingScratch=new List<TerrainFadeStamp>();
    readonly List<KeyValuePair<TerrainTileKey,int>> coverageSurfaces=new List<KeyValuePair<TerrainTileKey,int>>();
    readonly struct CoverageResident
    {
        public readonly Tile Tile,Previous;public readonly float In,Out;public readonly int Segments;public readonly bool CoverageOnly;
        public CoverageResident(Tile tile,Tile previous){Tile=tile;Previous=previous;In=tile.FadeIn;Out=tile.FadeOut;Segments=tile.Segments;CoverageOnly=tile.CoverageOnly;}
        public bool HasTiming=>In!=0||Out!=0||Previous!=null;
    }
    readonly Dictionary<TerrainTileKey,CoverageResident> coverageResidents=new Dictionary<TerrainTileKey,CoverageResident>();
    // A resident key contributes to at most 24 ancestor masks. Keeping this
    // reverse relation lets a publication update its real masks directly;
    // unrelated tiles do not resample inherited fades or rebuild materials.
    readonly Dictionary<TerrainTileKey,HashSet<TerrainTileKey>> coverageDescendants=new Dictionary<TerrainTileKey,HashSet<TerrainTileKey>>();
    readonly Dictionary<TerrainTileKey,TerrainFadeStamp> coverageMaskStamps=new Dictionary<TerrainTileKey,TerrainFadeStamp>();
    readonly List<TerrainTileKey> coverageMissing=new List<TerrainTileKey>();
    readonly HashSet<TerrainTileKey> coverageTrees=new HashSet<TerrainTileKey>(),coverageBindings=new HashSet<TerrainTileKey>(),coverageTimingChanges=new HashSet<TerrainTileKey>();
    bool coverageObserved,coverageHard;float coverageSafety;

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
        Array.Copy(values,coveragePixelScratch,values.Length);
        Array.Clear(coveragePixelScratch,values.Length,count-values.Length);
        texture.SetPixelData(coveragePixelScratch,0);texture.Apply(false,false);return texture;
    }
    void CoverageAncestorTrees(TerrainTileKey key)
    {
        for(int depth=1;depth<=24&&key.Level+depth<=24;depth++)
        {
            var ancestor=new TerrainTileKey(key.X>>depth,key.Z>>depth,key.Level+depth,key.Scale);
            if(tiles.ContainsKey(ancestor))coverageTrees.Add(ancestor);
        }
    }
    void CoverageIndex(TerrainTileKey key,bool add)
    {
        for(int depth=1;depth<=24&&key.Level+depth<=24;depth++)
        {
            var ancestor=new TerrainTileKey(key.X>>depth,key.Z>>depth,key.Level+depth,key.Scale);
            if(add)
            {
                if(!coverageDescendants.TryGetValue(ancestor,out var descendants))coverageDescendants.Add(ancestor,descendants=new HashSet<TerrainTileKey>());
                descendants.Add(key);
            }
            else if(coverageDescendants.TryGetValue(ancestor,out var remaining))
            {remaining.Remove(key);if(remaining.Count==0)coverageDescendants.Remove(ancestor);}
        }
    }
    TerrainFadeStamp CoverageMaskStamp(TerrainTileKey key)
    {
        if(coverageMaskStamps.TryGetValue(key,out var stamp))return stamp;
        stamp=TerrainTiming(key,true);coverageMaskStamps.Add(key,stamp);return stamp;
    }
    void CoverageTimingChanged(TerrainTileKey key)
    {
        if(tiles.ContainsKey(key))coverageBindings.Add(key);CoverageAncestorTrees(key);
        // Ancestor fades cap descendant fades, and an outgoing same-key draw
        // changes which incoming stamp the descendant inherits. Their stamps
        // also feed every resident ancestor's coverage tree.
        if(coverageDescendants.TryGetValue(key,out var descendants))foreach(var descendant in descendants)
        {coverageBindings.Add(descendant);CoverageAncestorTrees(descendant);}
    }
    void UpdateTerrainCoverage()
    {
        if(!coverageDirty)return;long started=WorldGenPerformance.TraceBegin();coverageDirty=false;
        coverageTrees.Clear();coverageBindings.Clear();coverageTimingChanges.Clear();coverageMissing.Clear();coverageMaskStamps.Clear();
        bool qualityChanged=!coverageObserved;
        float safety=view==null?0:(float)Math.Max(12,TerrainLod.BaseSizeM*HorizontalScale/64*3);
        bool bindAll=!coverageObserved||coverageHard!=TerrainVideoOptions.HardChunkUpdates||coverageSafety!=safety;
        foreach(var pair in coverageResidents)if(!tiles.ContainsKey(pair.Key))coverageMissing.Add(pair.Key);
        foreach(var key in coverageMissing)
        {
            var old=coverageResidents[key];CoverageIndex(key,false);CoverageAncestorTrees(key);
            if(old.HasTiming)coverageTimingChanges.Add(key);if(!old.CoverageOnly)qualityChanged=true;
            coverageResidents.Remove(key);
        }
        foreach(var pair in tiles)
        {
            previousTerrain.TryGetValue(pair.Key,out var previous);var current=new CoverageResident(pair.Value,previous);
            if(!coverageResidents.TryGetValue(pair.Key,out var old))
            {
                CoverageIndex(pair.Key,true);CoverageAncestorTrees(pair.Key);coverageTrees.Add(pair.Key);coverageBindings.Add(pair.Key);
                if(current.HasTiming)coverageTimingChanges.Add(pair.Key);if(!current.CoverageOnly)qualityChanged=true;
            }
            else
            {
                if(!ReferenceEquals(old.Tile,current.Tile)){coverageTrees.Add(pair.Key);coverageBindings.Add(pair.Key);}
                if(old.In!=current.In||old.Out!=current.Out||(old.Previous==null)!=(current.Previous==null))coverageTimingChanges.Add(pair.Key);
                else if(!ReferenceEquals(old.Previous,current.Previous))coverageBindings.Add(pair.Key);
                if(old.CoverageOnly!=current.CoverageOnly||!current.CoverageOnly&&old.Segments!=current.Segments)qualityChanged=true;
            }
            if(bindAll)coverageBindings.Add(pair.Key);coverageResidents[pair.Key]=current;
        }
        foreach(var key in coverageTimingChanges)CoverageTimingChanged(key);
        if(fogFootprint==null||qualityChanged)
        {
            coverageSurfaces.Clear();foreach(var pair in tiles)if(!pair.Value.CoverageOnly)coverageSurfaces.Add(new KeyValuePair<TerrainTileKey,int>(pair.Key,pair.Value.Segments));
            if(fogFootprint==null){fogFootprint=new TerrainQualityFootprint(coverageSurfaces);fogCheckAt=0;}
            // Keep the normal 150 ms reveal-query cadence during a stream of
            // publications. The footprint is current immediately; fade-only
            // changes do not invalidate it, and ordinary updates do not force
            // the expensive reveal query to run again in this same frame.
            else fogFootprint.Reset(coverageSurfaces);
        }
        foreach(var key in coverageTrees)
        {
            if(!tiles.TryGetValue(key,out var tile))continue;
            coverageTimingScratch.Clear();
            if(tile.CoverageChildren==null)tile.CoverageChildren=new List<TerrainTileKey>();else tile.CoverageChildren.Clear();
            if(coverageDescendants.TryGetValue(key,out var descendants))foreach(var child in descendants)
            {tile.CoverageChildren.Add(child);coverageTimingScratch.Add(CoverageMaskStamp(child));}
            if(tile.CoverageInputs==null)tile.CoverageInputs=new TerrainFadeCache();
            var tree=tile.CoverageInputs.Update(key,coverageTimingScratch);
            if(tile.CoverageNodes==null||!tile.CoverageNodes.SequenceEqual(tree.Nodes)||tile.CoverageTimingNodes==null||!tile.CoverageTimingNodes.SequenceEqual(tree.Timings))
            {
                tile.CoverageNodes=tree.Nodes;tile.CoverageTimingNodes=tree.Timings;
                tile.Coverage=CoverageTexture(tile.Coverage,tree.Nodes);tile.CoverageTimes=CoverageTexture(tile.CoverageTimes,tree.Timings);
            }
            coverageBindings.Add(key);
        }
        if(coverageBindings.Count>0)coverVisibilityRevision++;
        foreach(var key in coverageBindings)
        {
            if(!tiles.TryGetValue(key,out var tile))continue;var timing=TerrainTiming(key);
            BindTerrainTile(tile,tile,key,new Vector4(timing.In,timing.Out,0,0));
            if(previousTerrain.TryGetValue(key,out var previous))BindTerrainTile(previous,tile,key,new Vector4(timing.In,timing.Out,1,0));
        }
        coverageObserved=true;coverageHard=TerrainVideoOptions.HardChunkUpdates;coverageSafety=safety;
        WorldGenPerformance.TraceEnd(6,started);
    }
}
