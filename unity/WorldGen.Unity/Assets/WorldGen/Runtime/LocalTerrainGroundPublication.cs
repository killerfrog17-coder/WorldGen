using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    // Prepare root corrections while the old terrain remains visible. Only the
    // final publication changes live terrain, matrices or GPU roots. Reused
    // value-type edit lists keep this work out of the managed allocation stream.
    sealed class GroundPublication
    {
        public TerrainTileKey Key;public Tile Product;public bool Retiring,Fallback;public LocalTerrainFrame Frame;
        public double OriginX,OriginZ;public int Resident,ResidentCount,Canopy,Forest;public IEnumerator Work;
        public readonly GroundChanges Changes=new GroundChanges();
        public readonly List<ScatterGroundEdit> Scatter=new List<ScatterGroundEdit>();
        public readonly List<ScatterGroupEdit> Groups=new List<ScatterGroupEdit>();
        public readonly List<ScatterChunkEdit> Chunks=new List<ScatterChunkEdit>();
        public readonly List<ForestGroundEdit> Roots=new List<ForestGroundEdit>();
        public readonly List<ForestFieldEdit> Fields=new List<ForestFieldEdit>();
        public readonly List<ScatterChunk> SourceChunks=new List<ScatterChunk>();
        public readonly List<ForestField> SourceFields=new List<ForestField>();
        public void ClearEdits(){(Work as IDisposable)?.Dispose();Scatter.Clear();Groups.Clear();Chunks.Clear();Roots.Clear();Fields.Clear();SourceChunks.Clear();SourceFields.Clear();Changes.Clear();Work=null;}
    }
    struct ScatterGroundEdit {public ScatterChunk Chunk;public int Index,Level;public float Y;}
    struct ScatterGroupEdit {public ScatterGroup Group;public Bounds Bounds,RootBounds;public int MaximumGroundLevel;}
    struct ScatterChunkEdit {public ScatterChunk Chunk;public Bounds Bounds;}
    struct ForestGroundEdit {public ForestBatch Batch;public int Slot,Level;public ForestRoot Root;public bool Changed;}
    struct ForestFieldEdit {public ForestField Field;public Bounds Bounds;}
    readonly GroundPublication groundPublication=new GroundPublication();
    bool GroundPublicationPending=>groundPublication.Product!=null;
    int groundPublicationSlices,groundPublicationCommits,groundPublicationRestarts;
    double groundPublicationMaxPrepareMs,groundPublicationMaxCommitMs;
    public string PerformanceGroundPublication=>"cumulative_including_loading slices="+groundPublicationSlices+" commits="+groundPublicationCommits+" restarts="+groundPublicationRestarts+" max_prepare_ms="+groundPublicationMaxPrepareMs.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+" max_commit_ms="+groundPublicationMaxCommitMs.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+" pending="+GroundPublicationPending+" key="+groundPublication.Key.X+","+groundPublication.Key.Z+","+groundPublication.Key.Level+" resident="+groundPublication.Resident+"/"+residentRevision+" canopy="+groundPublication.Canopy+"/"+canopySourceRevision+" topology="+groundPublication.Forest+"/"+forestRootTopologyRevision;

    bool StageGroundPublication(TerrainTileKey key,Tile product,bool fallback=false)
    {
        if(!WorldGenPerformance.StagedGroundPublication){groundStageBypass=GroundStageBypass.Disabled;return false;}
        if(GroundPublicationPending){groundStageBypass=GroundStageBypass.Pending;return false;}
        if(!wanted.ContainsKey(key)){groundStageBypass=GroundStageBypass.Backdrop;return false;}
        if(scatterGroundChanges.Count!=0||scatterGroundRevision!=projectedGroundRevision){groundStageBypass=GroundStageBypass.PreviousChanges;return false;}
        // Empty census metadata is not physical topology. Far publications
        // with no possibly affected roots need no staged root correction.
        if(TerrainRootFree(key)){groundStageBypass=GroundStageBypass.NoRoots;return false;}
        foreach(var chunk in scatterChunks.Values)if(chunk.Groups==null||!ReferenceEquals(chunk.GroupsFrame,frame)){groundStageBypass=GroundStageBypass.UnpreparedGroups;return false;}
        groundStageBypass=GroundStageBypass.Staged;
        product.Root.SetActive(false);groundPublication.Retiring=false;groundPublication.Fallback=fallback;groundPublication.Key=key;groundPublication.Product=product;
        RestartGroundPublication();
        if(groundPublication.Changes.Count==0){groundStageBypass=GroundStageBypass.Unchanged;groundPublication.Product=null;groundPublication.ClearEdits();product.Root.SetActive(true);return false;}
        return true;
    }
    bool StageGroundRetirement(TerrainTileKey key,Tile tile)
    {
        if(!WorldGenPerformance.StagedTerrainRetirement||GroundPublicationPending||
            scatterGroundChanges.Count!=0||scatterGroundRevision!=projectedGroundRevision)return false;
        if(TerrainRootFree(key))return false;
        foreach(var chunk in scatterChunks.Values)if(chunk.Groups==null||!ReferenceEquals(chunk.GroupsFrame,frame))return false;
        // Product denotes the old, still-visible surface during a retirement.
        // Its objects remain owned by tiles until the atomic commit.
        groundPublication.Retiring=true;groundPublication.Key=key;groundPublication.Product=tile;
        RestartGroundPublication();return true;
    }
    void RestartGroundPublication()
    {
        var plan=groundPublication;if(plan.Work!=null)groundPublicationRestarts++;plan.ClearEdits();plan.Frame=frame;plan.OriginX=originX;plan.OriginZ=originZ;
        plan.Resident=residentRevision;plan.ResidentCount=tiles.Count;plan.Canopy=canopySourceRevision;plan.Forest=forestRootTopologyRevision;
        tiles.TryGetValue(plan.Key,out var old);if(plan.Retiring)plan.Changes.Add(plan.Key);else plan.Changes.Add(plan.Key,old,plan.Product);
        plan.Work=PrepareGroundPublication(plan);
    }
    void CancelGroundPublication()
    {
        var plan=groundPublication;if(plan.Product==null)return;
        if(!plan.Retiring)DestroyTile(plan.Product);plan.Product=null;plan.ClearEdits();
    }
    void AdvanceGroundPublication()
    {
        var plan=groundPublication;if(plan.Product==null)return;
        if(!ReferenceEquals(plan.Frame,frame)){CancelGroundPublication();return;}
        if(plan.Retiring)
        {
            if(wanted.ContainsKey(plan.Key)||!tiles.TryGetValue(plan.Key,out var retiring)||!ReferenceEquals(retiring,plan.Product)||!CanRetireTransition(plan.Key))
            {plan.Product=null;plan.ClearEdits();return;}
        }
        else if(plan.Fallback? !backdrops.ContainsKey(plan.Key)||!BackdropNeeded(plan.Key):
            !wanted.TryGetValue(plan.Key,out int segments)||segments!=plan.Product.Segments||
            !wantedEdges.TryGetValue(plan.Key,out var edges)||!TerrainEdges.Equal(edges,plan.Product.Edges))
        {var obsolete=plan.Product;plan.Product=null;plan.ClearEdits();CacheTerrain(plan.Key,obsolete);return;}
        if(plan.Resident!=residentRevision||plan.ResidentCount!=tiles.Count||plan.Canopy!=canopySourceRevision||plan.Forest!=forestRootTopologyRevision||
            plan.OriginX!=originX||plan.OriginZ!=originZ)RestartGroundPublication();
        long started=System.Diagnostics.Stopwatch.GetTimestamp();
        long trace=WorldGenPerformance.TraceBegin();
        bool pending=plan.Work.MoveNext();
        groundPublicationSlices++;groundPublicationMaxPrepareMs=Math.Max(groundPublicationMaxPrepareMs,(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency);
        installationsThisFrame++;
        if(pending){frameUploadMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency;WorldGenPerformance.TraceEnd(32,trace);return;}
        long commitStarted=System.Diagnostics.Stopwatch.GetTimestamp();
        // No rendering or other update runs inside this publication. All edits
        // were computed against this exact resident/chart/topology revision.
        ApplyGroundPublicationEdits(plan);
        var product=plan.Product;var key=plan.Key;plan.Product=null;
        if(plan.Retiring){tiles.Remove(key);CacheTerrain(key,product);}
        else
        {
            tiles.TryGetValue(key,out var old);BeginTerrainReplacement(key,product,old);
            product.Root.transform.localPosition=new Vector3((float)(key.X*key.SizeM-originX),0,(float)(key.Z*key.SizeM-originZ));
            tiles[key]=product;product.Root.SetActive(true);
        }
        residentRevision++;coverageDirty=true;
        foreach(var change in plan.Changes)scatterGroundChanges.Add(change);
        InvalidateInstallingScatterGround();scatterGroundChanges.Clear();scatterGroundRevision++;projectedGroundRevision=scatterGroundRevision;
        forestRevision++;nextForestVisibility=0;scatterDirty=true;plan.ClearEdits();
        groundPublicationCommits++;groundPublicationMaxCommitMs=Math.Max(groundPublicationMaxCommitMs,(System.Diagnostics.Stopwatch.GetTimestamp()-commitStarted)*1000d/System.Diagnostics.Stopwatch.Frequency);
        frameUploadMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency;
        WorldGenPerformance.TraceEnd(32,trace);
    }
    void ApplyGroundPublicationEdits(GroundPublication plan)
    {
        foreach(var edit in plan.Scatter)
        {
            var chunk=edit.Chunk;int i=edit.Index;var root=chunk.Roots[i];float delta=edit.Y-root.y;
            chunk.GroundLevels[i]=edit.Level;if(delta==0)continue;
            root.y=edit.Y;chunk.Roots[i]=root;var matrix=chunk.Matrices[i];matrix.m13=edit.Y;chunk.Matrices[i]=matrix;
            var bounds=chunk.Bounds[i];bounds.center+=Vector3.up*delta;chunk.Bounds[i]=bounds;
        }
        foreach(var edit in plan.Groups){edit.Group.Bounds=edit.Bounds;edit.Group.RootBounds=edit.RootBounds;edit.Group.MaximumGroundLevel=edit.MaximumGroundLevel;}
        foreach(var edit in plan.Chunks){edit.Chunk.AllBounds=edit.Bounds;edit.Chunk.RootRevision++;edit.Chunk.RangeValid=false;canopySourceRevision++;}
        foreach(var edit in plan.Roots)
        {edit.Batch.GroundLevels[edit.Slot]=edit.Level;if(edit.Changed){edit.Batch.Data[edit.Slot]=edit.Root;DirtyForestRoot(edit.Batch,edit.Slot);}}
        foreach(var edit in plan.Fields)edit.Field.Bounds=edit.Bounds;
    }
    bool PublicationChanges(GroundPublication plan,double x,double z,double margin,int level)
    {
        foreach(var key in plan.Changes)if(key.Level<=level&&plan.Changes.Affects(key,x,z,margin))return true;
        return false;
    }
    double PublicationGround(GroundPublication plan,double x,double z,double fallback,out int level)
        =>PublicationGround(plan,x,z,fallback,out level,out _,out _);
    double PublicationGround(GroundPublication plan,double x,double z,double fallback,out int level,out Tile tile,out TerrainTileKey key)
    {
        double current=VisibleGround(x,z,fallback,out level),size=plan.Key.SizeM;
        key=default;tile=null;
        if(plan.Key.Level>level||(int)Math.Floor(x/size)!=plan.Key.X||(int)Math.Floor(z/size)!=plan.Key.Z)
        {
            if(level<17)
            {
                double currentSize=TerrainLod.BaseSizeM*HorizontalScale*(1<<level);
                key=new TerrainTileKey((int)Math.Floor(x/currentSize),(int)Math.Floor(z/currentSize),level,HorizontalScale);
                tiles.TryGetValue(key,out tile);
            }
            return current;
        }
        key=plan.Key;tile=plan.Product;
        if(plan.Retiring)
        {
            tile=null;
            for(int candidate=plan.Key.Level+1;candidate<=16;candidate++)
            {
                double candidateSize=TerrainLod.BaseSizeM*HorizontalScale*(1<<candidate);
                var ancestor=new TerrainTileKey((int)Math.Floor(x/candidateSize),(int)Math.Floor(z/candidateSize),candidate,HorizontalScale);
                if(tiles.TryGetValue(ancestor,out var found)&&found.Heights!=null){key=ancestor;tile=found;break;}
            }
            if(tile==null){level=17;return fallback;}
            size=key.SizeM;
        }
        level=key.Level;
        if(tile.Contact!=null&&tile.Contact.TryHeight(x-key.X*size,z-key.Z*size,out double drawn))return drawn;
        int n=tile.Segments,side=n+1;
        double gx=(x/size-key.X)*n,gz=(z/size-key.Z)*n;
        int a=Math.Min(n-1,Math.Max(0,(int)gx)),b=Math.Min(n-1,Math.Max(0,(int)gz));double u=gx-a,v=gz-b;
        double h00=tile.Heights[b*side+a],h10=tile.Heights[b*side+a+1],h01=tile.Heights[(b+1)*side+a],h11=tile.Heights[(b+1)*side+a+1];
        return u+v<=1?h00+(h10-h00)*u+(h01-h00)*v:h11+(h01-h11)*(1-u)+(h10-h11)*(1-v);
    }
    IEnumerator PrepareGroundPublication(GroundPublication plan)
    {
        if(plan.Changes.Count==0)yield break;
        // No dictionary enumerator may survive a slice. Empty forest census
        // certificates can change while physical-root topology stays intact.
        // Reuse per-plan lists; actual root additions/removals still restart
        // the plan through its existing canopy/topology revision guards.
        plan.SourceChunks.Clear();foreach(var chunk in scatterChunks.Values)plan.SourceChunks.Add(chunk);
        plan.SourceFields.Clear();foreach(var field in forestFields.Values)if(field.Roots.Count>0)plan.SourceFields.Add(field);
        int visited=0;long started=System.Diagnostics.Stopwatch.GetTimestamp();
        bool Due()=>++visited%32==0&&System.Diagnostics.Stopwatch.GetTimestamp()-started>System.Diagnostics.Stopwatch.Frequency*.0007;
        bool Affected(Bounds bounds,double offsetX,double offsetZ,double margin=0)
        {double x0=plan.Key.X*plan.Key.SizeM-offsetX,z0=plan.Key.Z*plan.Key.SizeM-offsetZ;return bounds.max.x>=x0-margin&&bounds.min.x<=x0+plan.Key.SizeM+margin&&bounds.max.z>=z0-margin&&bounds.min.z<=z0+plan.Key.SizeM+margin;}
        foreach(var chunk in plan.SourceChunks)
        {
            // A chart reset uses the existing complete projection path instead.
            if(chunk.Groups==null||!ReferenceEquals(chunk.GroupsFrame,frame))continue;
            if(!Affected(chunk.AllBounds,originX,originZ))continue;
            var chunkMin=chunk.AllBounds.min;var chunkMax=chunk.AllBounds.max;bool changed=false;
            foreach(var group in chunk.Groups)
            {
                // Match the literal correction's conservative group guard.
                if(!Affected(group.RootBounds,originX,originZ,.01))continue;
                var groupMin=group.Bounds.min;var groupMax=group.Bounds.max;
                var rootMin=group.RootBounds.min;var rootMax=group.RootBounds.max;bool groupChanged=false;int maximumGroundLevel=-1;
                foreach(int i in group.Indices)
                {
                    var root=chunk.Roots[i];int currentGroundLevel=chunk.GroundLevels[i];
                    if(PublicationChanges(plan,root.x+originX,root.z+originZ,.01,chunk.GroundLevels[i]))
                    {
                        var p=chunk.Source.Instances[i];double dot=Math.Max(.1,V.Dot(p.Position,frame.Origin));
                        double x=V.Dot(p.Position,frame.East)*frame.RadiusM/dot,z=V.Dot(p.Position,frame.North)*frame.RadiusM/dot;
                        float y=(float)PublicationGround(plan,x,z,frame.SurfaceY(p.Position,p.GroundM),out int level)-ScatterEmbedding(p);
                        currentGroundLevel=level;
                        float delta=y-root.y;
                        plan.Scatter.Add(new ScatterGroundEdit{Chunk=chunk,Index=i,Level=level,Y=y});
                        if(delta!=0)
                        {
                            root.y=y;var bounds=chunk.Bounds[i];bounds.center+=Vector3.up*delta;
                            float bottom=bounds.center.y-bounds.extents.y,top=bounds.center.y+bounds.extents.y;
                            groupMin.y=Math.Min(groupMin.y,bottom);groupMax.y=Math.Max(groupMax.y,top);
                            rootMin.y=Math.Min(rootMin.y,root.y);rootMax.y=Math.Max(rootMax.y,root.y);
                            chunkMin.y=Math.Min(chunkMin.y,bottom);chunkMax.y=Math.Max(chunkMax.y,top);changed=groupChanged=true;
                        }
                    }
                    maximumGroundLevel=Math.Max(maximumGroundLevel,currentGroundLevel);
                    if(Due()){yield return null;started=System.Diagnostics.Stopwatch.GetTimestamp();}
                }
                if(groupChanged||maximumGroundLevel!=group.MaximumGroundLevel){var bounds=group.Bounds;var roots=group.RootBounds;if(groupChanged){bounds.SetMinMax(groupMin,groupMax);roots.SetMinMax(rootMin,rootMax);}plan.Groups.Add(new ScatterGroupEdit{Group=group,Bounds=bounds,RootBounds=roots,MaximumGroundLevel=maximumGroundLevel});}
            }
            if(changed){var bounds=chunk.AllBounds;bounds.SetMinMax(chunkMin,chunkMax);plan.Chunks.Add(new ScatterChunkEdit{Chunk=chunk,Bounds=bounds});}
        }
        foreach(var field in plan.SourceFields)
        {
            if(!Affected(field.Bounds,0,0))continue;
            var bounds=field.Bounds;bool changed=false;
            foreach(var pair in field.Roots)foreach(int slot in pair.Value)
            {
                var root=pair.Key.Data[slot];
                if(PublicationChanges(plan,root.Position.x,root.Position.z,.01,pair.Key.GroundLevels[slot]))
                {
                    var before=root;double x=root.Position.x,z=root.Position.z,y=PublicationGround(plan,x,z,root.Position.y+.045f,out int level,out Tile owner,out TerrainTileKey ownerKey);
                    ApplyForestRootGround(ref root,y,level,owner,ownerKey);
                    bool moved=root.Position.y!=before.Position.y||root.SlopeX!=before.SlopeX||root.SlopeZ!=before.SlopeZ||root.Padding!=before.Padding;
                    plan.Roots.Add(new ForestGroundEdit{Batch=pair.Key,Slot=slot,Level=level,Root=root,Changed=moved});
                    if(moved){bounds.Encapsulate(ForestRootBounds(root));changed=true;}
                }
                if(Due()){yield return null;started=System.Diagnostics.Stopwatch.GetTimestamp();}
            }
            if(changed)plan.Fields.Add(new ForestFieldEdit{Field=field,Bounds=bounds});
        }
    }
}
