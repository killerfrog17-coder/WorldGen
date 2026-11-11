using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    readonly Dictionary<TerrainTileKey,Tile> previousTerrain=new Dictionary<TerrainTileKey,Tile>();
    readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<TerrainTileKey,Tile>> transitionSnapshot=new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<TerrainTileKey,Tile>>();
    Dictionary<TerrainTileKey,int> transitionWantedPlan;
    int transitionWantedCount=-1,transitionWantedMaximumLevel;double transitionWantedScale;bool transitionWantedMixedScales;
    readonly Dictionary<TerrainTileKey,List<TerrainTileKey>> transitionWantedDescendants=new Dictionary<TerrainTileKey,List<TerrainTileKey>>();
    readonly Stack<List<TerrainTileKey>> transitionWantedLists=new Stack<List<TerrainTileKey>>();

    void PrepareTransitionWanted()
    {
        // Production replaces the dictionary when adopting a plan. Keep keys,
        // not requested segment values: those are always read from the live
        // dictionary, including the native controls' in-place detail changes.
        if(ReferenceEquals(transitionWantedPlan,wanted)&&transitionWantedCount==wanted.Count)return;
        transitionWantedPlan=wanted;transitionWantedCount=wanted.Count;transitionWantedMixedScales=false;transitionWantedMaximumLevel=-1;
        foreach(var list in transitionWantedDescendants.Values){list.Clear();transitionWantedLists.Push(list);}
        transitionWantedDescendants.Clear();bool first=true;
        foreach(var key in wanted.Keys)
        {
            transitionWantedMaximumLevel=Math.Max(transitionWantedMaximumLevel,key.Level);
            if(first){transitionWantedScale=key.Scale;first=false;}else transitionWantedMixedScales|=transitionWantedScale!=key.Scale;
            for(int depth=1;depth<24-key.Level;depth++)
            {
                var ancestor=new TerrainTileKey(key.X>>depth,key.Z>>depth,key.Level+depth,key.Scale);
                if(!transitionWantedDescendants.TryGetValue(ancestor,out var list))
                {list=transitionWantedLists.Count>0?transitionWantedLists.Pop():new List<TerrainTileKey>();transitionWantedDescendants.Add(ancestor,list);}
                list.Add(key);
            }
        }
    }
    bool HasWantedTerrainOverlap(TerrainTileKey key)
    {
        PrepareTransitionWanted();
        if(transitionWantedMixedScales||wanted.Count>0&&key.Scale!=transitionWantedScale)
        {foreach(var candidate in wanted.Keys)if(Overlaps(key,candidate))return true;return false;}
        if(wanted.ContainsKey(key)||transitionWantedDescendants.ContainsKey(key))return true;
        for(int depth=1;depth<=transitionWantedMaximumLevel-key.Level;depth++)
            if(wanted.ContainsKey(new TerrainTileKey(key.X>>depth,key.Z>>depth,key.Level+depth,key.Scale)))return true;
        return false;
    }
    float TransitionNow=>Math.Max(.001f,Time.time);
    bool terrainHardUpdates;
    bool FadeComplete(Tile tile)=>TerrainVideoOptions.HardChunkUpdates||tile.FadeOut==0&&(tile.FadeIn==0||Time.time-tile.FadeIn>=TerrainCoverage.FadeSeconds);
    void BeginTerrainReplacement(TerrainTileKey key,Tile next,Tile old)
    {
        if(previousTerrain.TryGetValue(key,out var earlier)){CacheTerrain(key,earlier);previousTerrain.Remove(key);}
        bool finer=tiles.Keys.Any(k=>k.Level<key.Level&&Overlaps(key,k));
        // An old same-key parent has a different interior from the current
        // parent to which its children are fitted. Its descendant coverage
        // cuts therefore cannot safely serve as a fading solid underlay.
        if(old!=null){if(TerrainVideoOptions.HardChunkUpdates||finer)CacheTerrain(key,old);else{old.FadeIn=old.FadeOut=0;previousTerrain[key]=old;}}
        bool parent=false;
        for(int depth=1;depth<24-key.Level;depth++)
        {
            var ancestor=new TerrainTileKey(key.X>>depth,key.Z>>depth,key.Level+depth,key.Scale);
            if(!tiles.TryGetValue(ancestor,out var resident))continue;parent=true;
            // Finish an existing same-key parent replacement before this
            // child can introduce an internal coverage cut. Ordinary parent
            // to child fades have no previous same-key draw and stay smooth.
            if(previousTerrain.TryGetValue(ancestor,out var previous))
            {resident.FadeIn=0;previousTerrain.Remove(ancestor);CacheTerrain(ancestor,previous);}
        }
        next.FadeIn=!TerrainVideoOptions.HardChunkUpdates&&!finer&&(old!=null||parent)?TransitionNow:0;next.FadeOut=0;
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
        if(terrainHardUpdates!=TerrainVideoOptions.HardChunkUpdates){terrainHardUpdates=TerrainVideoOptions.HardChunkUpdates;coverageDirty=true;}
        if(terrainHardUpdates)
        {
            ClearTerrainTransitions();
            foreach(var tile in tiles.Values)if(tile.FadeIn!=0||tile.FadeOut!=0){tile.FadeIn=tile.FadeOut=0;coverageDirty=true;}
            return;
        }
        transitionSnapshot.Clear();foreach(var pair in previousTerrain)transitionSnapshot.Add(pair);
        foreach(var pair in transitionSnapshot)
            if(!tiles.TryGetValue(pair.Key,out var current)||FadeComplete(current))
            {previousTerrain.Remove(pair.Key);CacheTerrain(pair.Key,pair.Value);}
        foreach(var pair in tiles)
        {
            var tile=pair.Value;
            if(tile.FadeIn>0&&Time.time-tile.FadeIn>=TerrainCoverage.FadeSeconds){tile.FadeIn=0;coverageDirty=true;}
            if(tile.FadeOut>0&&!wanted.ContainsKey(pair.Key)&&HasFinerWantedOverlap(pair.Key))
            {
                // The camera reversed a coarsening decision. New fine products
                // inherit this parent's visibility, so an old outgoing stamp
                // would make every replacement vanish until the whole subtree
                // eventually retires. Restore the fallback before revealing
                // those descendants; never fade from an already empty region.
                tile.FadeIn=tile.FadeOut=0;coverageDirty=true;
            }
            else if(tile.FadeOut>0&&wanted.ContainsKey(pair.Key))
            {
                double p=TerrainCoverage.Fade(Time.time,tile.FadeIn,tile.FadeOut),lo=0,hi=1;
                for(int i=0;i<16;i++){double mid=(lo+hi)*.5;if(mid*mid*(3-2*mid)<p)lo=mid;else hi=mid;}
                tile.FadeOut=0;tile.FadeIn=(float)(Time.time-(lo+hi)*.5*TerrainCoverage.FadeSeconds);coverageDirty=true;
            }
        }
    }
    bool HasFinerWantedOverlap(TerrainTileKey key)
    {
        PrepareTransitionWanted();
        if(transitionWantedMixedScales||wanted.Count>0&&key.Scale!=transitionWantedScale)
        {foreach(var candidate in wanted.Keys)if(candidate.Level<key.Level&&Overlaps(key,candidate))return true;return false;}
        if(transitionWantedDescendants.TryGetValue(key,out var descendants))foreach(var candidate in descendants)if(wanted.ContainsKey(candidate))return true;
        return false;
    }
    void RetireObsoleteTerrain()
    {
        if(GroundPublicationPending)return;
        // Retiring several tiles at once forces all displaced vegetation to
        // correct in the same frame. Leave redundant ground resident until its
        // turn; the existing coverage shader still chooses the finest surface.
        // Share the install allowance so removal cannot add another correction
        // on top of a newly published terrain or forest payload.
        bool paced=WorldGenPerformance.PacedTerrainRetirement;
        if(paced&&!CanInstall)return;
        terrainRetirementScratch.Clear();
        foreach(var key in tiles.Keys)if(!wanted.ContainsKey(key))terrainRetirementScratch.Add(key);
        foreach(var key in terrainRetirementScratch)
        {
            if(!CanRetireTransition(key))continue;
            if(StageGroundRetirement(key,tiles[key])){installationsThisFrame++;break;}
            CacheTerrain(key,tiles[key]);tiles.Remove(key);residentRevision++;
            scatterGroundRevision++;scatterGroundChanges.Add(key);NoteGroundChange(key,GroundOrigin.Retirement);
            if(paced){installationsThisFrame++;break;}
        }
    }
    bool CanRetireTransition(TerrainTileKey key)
    {
        // These few coarse tiles supply a stable fallback for the current
        // view. Retiring/rebuilding them on every fine-plan change caused
        // repeated whole-forest corrections. Normal coverage still selects
        // the finest ready surface; an unplanned backdrop can retire normally.
        if(WorldGenPerformance.KeepBackdrops&&backdrops.ContainsKey(key))return false;
        PrepareTransitionWanted();bool any=false,coarser=false,complete=true;
        bool Ready(TerrainTileKey replacement,int segments)
        {
            any=true;
            if(!tiles.TryGetValue(replacement,out var ready)||ready.CoverageOnly||ready.Segments!=segments)return false;
            coarser|=replacement.Level>key.Level;complete&=FadeComplete(ready);return true;
        }
        if(transitionWantedMixedScales||wanted.Count>0&&key.Scale!=transitionWantedScale)
        {
            foreach(var replacement in wanted)if(Overlaps(key,replacement.Key)&&!Ready(replacement.Key,replacement.Value))return false;
        }
        else
        {
            // Dyadic same-scale tiles overlap only through ancestry. One
            // containing planned tile plus this key's actual descendants
            // replace the previous scan of the entire world-view plan.
            for(int depth=0;depth<=transitionWantedMaximumLevel-key.Level;depth++)
            {
                var ancestor=new TerrainTileKey(key.X>>depth,key.Z>>depth,key.Level+depth,key.Scale);
                if(wanted.TryGetValue(ancestor,out int segments)&&!Ready(ancestor,segments))return false;
            }
            if(transitionWantedDescendants.TryGetValue(key,out var descendants))
                foreach(var replacement in descendants)if(wanted.TryGetValue(replacement,out int segments)&&!Ready(replacement,segments))return false;
        }
        if(!any)return true;
        var tile=tiles[key];
        if(coarser)
        {
            if(TerrainVideoOptions.HardChunkUpdates)return true;
            if(previousTerrain.TryGetValue(key,out var previous)){previousTerrain.Remove(key);CacheTerrain(key,previous);tile.FadeIn=0;}
            if(tile.FadeOut==0){tile.FadeIn=0;tile.FadeOut=TransitionNow;coverageDirty=true;}
            return Time.time-tile.FadeOut>=TerrainCoverage.FadeSeconds;
        }
        return complete;
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
