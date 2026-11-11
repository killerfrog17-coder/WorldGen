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
    bool FadeComplete(Tile tile)=>TerrainVideoOptions.HardChunkUpdates||tile.FadeIn==0||Time.time-tile.FadeIn>=TerrainCoverage.FadeSeconds;
    void BeginTerrainReplacement(TerrainTileKey key,Tile next,Tile old)
    {
        if(previousTerrain.TryGetValue(key,out var earlier)){CacheTerrain(key,earlier);previousTerrain.Remove(key);}
        bool finer=tiles.Keys.Any(k=>k.Level<key.Level&&Overlaps(key,k));
        if(old!=null)CacheTerrain(key,old);
        for(int depth=1;depth<24-key.Level;depth++)
        {
            var ancestor=new TerrainTileKey(key.X>>depth,key.Z>>depth,key.Level+depth,key.Scale);
            if(!tiles.TryGetValue(ancestor,out var resident))continue;
            // Child boundaries are fitted to the parent's final triangles,
            // including interior coverage cuts. Finish a parent morph first.
            resident.FadeIn=resident.FadeOut=0;
            if(previousTerrain.TryGetValue(ancestor,out var previous))
            {previousTerrain.Remove(ancestor);CacheTerrain(ancestor,previous);}
        }
        // Only PrepareTerrainMorph may opt a safe distant leaf into morphing.
        // A cached product or an unprepared/interactive replacement snaps.
        if(TerrainVideoOptions.HardChunkUpdates||finer||next.FadeIn>0&&!TerrainRootFree(key))next.FadeIn=0;next.FadeOut=0;
        coverageDirty=true;
    }
    void UpdateTerrainTransitions()
    {
        if(terrainHardUpdates!=TerrainVideoOptions.HardChunkUpdates){terrainHardUpdates=TerrainVideoOptions.HardChunkUpdates;coverageDirty=true;}
        if(previousTerrain.Count>0)ClearTerrainTransitions();
        foreach(var pair in tiles)
        {
            var tile=pair.Value;
            if(tile.FadeOut!=0||tile.FadeIn>0&&(terrainHardUpdates||FadeComplete(tile)||
                TerrainLod.DistanceSquared(pair.Key,motion.X,motion.Z)<=TerrainMorphSafetyRadius*TerrainMorphSafetyRadius))
            {
                tile.FadeIn=tile.FadeOut=0;coverageDirty=true;
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
        foreach(var key in tiles.Keys)if(!wanted.ContainsKey(key)||SupersededCoverageTerrain(key))terrainRetirementScratch.Add(key);
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
        // A genuine ancestor already supplies connected land and water. A
        // statistically finer fallback must not cover its canonical channel.
        if(SupersededCoverageTerrain(key))return true;
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
        // A ready opaque parent already supplies the entire coarsened area.
        return coarser||complete;
    }
    TerrainFadeStamp TerrainTiming(TerrainTileKey key,bool coverage=false)
    {
        return new TerrainFadeStamp(key,tiles[key].FadeIn,0);
    }
    void ClearTerrainTransitions()
    {
        foreach(var pair in previousTerrain){if(closed)DestroyTile(pair.Value);else CacheTerrain(pair.Key,pair.Value);}previousTerrain.Clear();
    }
}
