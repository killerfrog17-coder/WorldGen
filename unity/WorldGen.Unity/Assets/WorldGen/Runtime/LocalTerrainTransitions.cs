using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    readonly Dictionary<TerrainTileKey,Tile> previousTerrain=new Dictionary<TerrainTileKey,Tile>();
    float TransitionNow=>Math.Max(.001f,Time.time);
    bool FadeComplete(Tile tile)=>tile.FadeOut==0&&(tile.FadeIn==0||Time.time-tile.FadeIn>=TerrainCoverage.FadeSeconds);
    void BeginTerrainReplacement(TerrainTileKey key,Tile next,Tile old)
    {
        if(previousTerrain.TryGetValue(key,out var earlier)){CacheTerrain(key,earlier);previousTerrain.Remove(key);}
        if(old!=null){old.FadeIn=old.FadeOut=0;previousTerrain[key]=old;}
        bool parent=false;
        for(int depth=1;depth<24-key.Level;depth++)if(tiles.ContainsKey(new TerrainTileKey(key.X>>depth,key.Z>>depth,key.Level+depth,key.Scale))){parent=true;break;}
        bool finer=tiles.Keys.Any(k=>k.Level<key.Level&&Overlaps(key,k));
        next.FadeIn=old!=null||parent&&!finer?TransitionNow:0;next.FadeOut=0;
        // No fade from missing ground/sky. Finer coverage supplies the fade-out
        // when coarsening, rather than making the whole coarse patch transparent.
        while(previousTerrain.Count>32)
        {
            var oldest=previousTerrain.OrderBy(p=>tiles.TryGetValue(p.Key,out var t)?t.FadeIn:0).First();
            if(tiles.TryGetValue(oldest.Key,out var t))t.FadeIn=0;
            previousTerrain.Remove(oldest.Key);CacheTerrain(oldest.Key,oldest.Value);
        }
        coverageDirty=true;
    }
    void UpdateTerrainTransitions()
    {
        foreach(var pair in previousTerrain.ToArray())
            if(!tiles.TryGetValue(pair.Key,out var current)||FadeComplete(current))
            {previousTerrain.Remove(pair.Key);CacheTerrain(pair.Key,pair.Value);}
        foreach(var pair in tiles)
        {
            var tile=pair.Value;
            if(tile.FadeIn>0&&Time.time-tile.FadeIn>=TerrainCoverage.FadeSeconds){tile.FadeIn=0;coverageDirty=true;}
            if(tile.FadeOut>0&&wanted.ContainsKey(pair.Key))
            {
                double p=TerrainCoverage.Fade(Time.time,tile.FadeIn,tile.FadeOut),lo=0,hi=1;
                for(int i=0;i<16;i++){double mid=(lo+hi)*.5;if(mid*mid*(3-2*mid)<p)lo=mid;else hi=mid;}
                tile.FadeOut=0;tile.FadeIn=(float)(Time.time-(lo+hi)*.5*TerrainCoverage.FadeSeconds);coverageDirty=true;
            }
        }
    }
    bool CanRetireTransition(TerrainTileKey key)
    {
        var replacements=wanted.Where(w=>Overlaps(key,w.Key)).ToArray();
        if(!replacements.All(w=>tiles.TryGetValue(w.Key,out var t)&&t.Segments==w.Value))return false;
        if(replacements.Length==0)return true;
        var tile=tiles[key];
        if(replacements.Any(w=>w.Key.Level>key.Level))
        {
            if(previousTerrain.TryGetValue(key,out var previous)){previousTerrain.Remove(key);CacheTerrain(key,previous);tile.FadeIn=0;}
            if(tile.FadeOut==0){tile.FadeIn=0;tile.FadeOut=TransitionNow;coverageDirty=true;}
            return Time.time-tile.FadeOut>=TerrainCoverage.FadeSeconds;
        }
        return replacements.All(w=>FadeComplete(tiles[w.Key]));
    }
    TerrainFadeStamp TerrainTiming(TerrainTileKey key,bool coverage=false)
    {
        var tile=tiles[key];float incoming=coverage&&previousTerrain.ContainsKey(key)?0:tile.FadeIn,outgoing=tile.FadeOut;
        for(int depth=1;depth<24-key.Level;depth++)
        {
            var ancestorKey=new TerrainTileKey(key.X>>depth,key.Z>>depth,key.Level+depth,key.Scale);
            if(!tiles.TryGetValue(ancestorKey,out var ancestor))continue;
            if(!previousTerrain.ContainsKey(ancestorKey))incoming=Math.Max(incoming,ancestor.FadeIn);
            if(ancestor.FadeOut>0)outgoing=outgoing<=0?ancestor.FadeOut:Math.Min(outgoing,ancestor.FadeOut);
        }
        return new TerrainFadeStamp(key,incoming,outgoing);
    }
    void ClearTerrainTransitions()
    {
        foreach(var pair in previousTerrain){if(closed)DestroyTile(pair.Value);else CacheTerrain(pair.Key,pair.Value);}previousTerrain.Clear();
    }
}
