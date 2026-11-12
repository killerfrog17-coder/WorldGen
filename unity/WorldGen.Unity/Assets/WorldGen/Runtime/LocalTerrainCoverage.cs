using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    bool coverageDirty;
    float[] coveragePixelScratch=Array.Empty<float>();
    readonly List<KeyValuePair<TerrainTileKey,int>> coverageSurfaces=new List<KeyValuePair<TerrainTileKey,int>>();
    readonly struct CoverageResident
    {
        public readonly Tile Tile;public readonly float In;public readonly int Segments;public readonly bool CoverageOnly;
        public CoverageResident(Tile tile){Tile=tile;In=tile.FadeIn;Segments=tile.Segments;CoverageOnly=tile.CoverageOnly;}
    }
    readonly Dictionary<TerrainTileKey,CoverageResident> coverageResidents=new Dictionary<TerrainTileKey,CoverageResident>();
    // A resident key contributes to at most 24 ancestor masks. Keeping this
    // reverse relation lets a publication update its real masks directly;
    // unrelated tiles do not rebuild coverage masks or materials.
    readonly Dictionary<TerrainTileKey,HashSet<TerrainTileKey>> coverageDescendants=new Dictionary<TerrainTileKey,HashSet<TerrainTileKey>>();
    readonly List<TerrainTileKey> coverageMissing=new List<TerrainTileKey>();
    readonly HashSet<TerrainTileKey> coverageTrees=new HashSet<TerrainTileKey>(),coverageBindings=new HashSet<TerrainTileKey>();
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
    void UpdateTerrainCoverage()
    {
        if(!coverageDirty)return;long started=WorldGenPerformance.TraceBegin();coverageDirty=false;
        coverageTrees.Clear();coverageBindings.Clear();coverageMissing.Clear();
        bool qualityChanged=!coverageObserved;
        bool coverageChanged=!coverageObserved;
        float safety=view==null?0:TerrainMorphSafetyRadius;
        bool bindAll=!coverageObserved||coverageHard!=TerrainVideoOptions.HardChunkUpdates||coverageSafety!=safety;
        foreach(var pair in coverageResidents)if(!tiles.ContainsKey(pair.Key))coverageMissing.Add(pair.Key);
        foreach(var key in coverageMissing)
        {
            var old=coverageResidents[key];CoverageIndex(key,false);CoverageAncestorTrees(key);
            if(!old.CoverageOnly)qualityChanged=true;
            coverageResidents.Remove(key);
            coverageChanged=true;
        }
        foreach(var pair in tiles)
        {
            var current=new CoverageResident(pair.Value);
            if(!coverageResidents.TryGetValue(pair.Key,out var old))
            {
                CoverageIndex(pair.Key,true);CoverageAncestorTrees(pair.Key);coverageTrees.Add(pair.Key);coverageBindings.Add(pair.Key);
                coverageChanged=true;
                if(!current.CoverageOnly)qualityChanged=true;
            }
            else
            {
                if(!ReferenceEquals(old.Tile,current.Tile)){coverageTrees.Add(pair.Key);coverageBindings.Add(pair.Key);}
                if(old.In!=current.In)coverageBindings.Add(pair.Key);
                if(old.CoverageOnly!=current.CoverageOnly||!current.CoverageOnly&&old.Segments!=current.Segments)qualityChanged=true;
            }
            if(bindAll)coverageBindings.Add(pair.Key);coverageResidents[pair.Key]=current;
        }
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
        if(loadingCoverage==null)loadingCoverage=new TerrainCoverageFootprint(tiles.Keys);
        else if(coverageChanged)loadingCoverage.Reset(tiles.Keys);
        foreach(var key in coverageTrees)
        {
            if(!tiles.TryGetValue(key,out var tile))continue;
            if(tile.CoverageChildren==null)tile.CoverageChildren=new List<TerrainTileKey>();else tile.CoverageChildren.Clear();
            if(coverageDescendants.TryGetValue(key,out var descendants))foreach(var child in descendants)tile.CoverageChildren.Add(child);
            // Ownership is opaque and geometric. A vertex morph never opens
            // pixel holes in a different-height ancestor or keeps two floors.
            var nodes=TerrainCoverage.Build(key,tile.CoverageChildren);
            if(tile.CoverageNodes==null||!tile.CoverageNodes.SequenceEqual(nodes))
            {tile.CoverageNodes=nodes;tile.Coverage=CoverageTexture(tile.Coverage,nodes);}
            coverageBindings.Add(key);
        }
        if(coverageBindings.Count>0)coverVisibilityRevision++;
        foreach(var key in coverageBindings)
        {
            if(!tiles.TryGetValue(key,out var tile))continue;
            BindTerrainTile(tile,tile,key,new Vector4(tile.FadeIn,0,0,0));
        }
        coverageObserved=true;coverageHard=TerrainVideoOptions.HardChunkUpdates;coverageSafety=safety;
        WorldGenPerformance.TraceEnd(6,started);
    }
}
