using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class GroundProofState
    {
        sealed class ChunkState
        {
            public ScatterChunk Chunk;public Matrix4x4[] Matrices;public Vector3[] Roots;public Bounds[] Bounds;
            public int[] Levels;public Bounds All;public int Revision;public bool RangeValid;
            public (ScatterGroup group,Bounds bounds,Bounds roots,int level)[] Groups;
        }
        readonly List<ChunkState> chunks=new List<ChunkState>();
        readonly List<(ForestBatch batch,ForestRoot[] roots,int[] levels,bool dirty,bool allDirty,bool[] blocks)> forests=new List<(ForestBatch,ForestRoot[],int[],bool,bool,bool[])>();
        readonly List<(ForestField field,Bounds bounds)> fields=new List<(ForestField,Bounds)>();
        readonly LocalTerrainExplorer explorer;readonly int canopy,forest;readonly double visibility;
        public GroundProofState(LocalTerrainExplorer owner)
        {
            explorer=owner;canopy=owner.canopySourceRevision;forest=owner.forestRevision;visibility=owner.nextForestVisibility;
            foreach(var chunk in owner.scatterChunks.Values)chunks.Add(new ChunkState{Chunk=chunk,Matrices=(Matrix4x4[])chunk.Matrices.Clone(),Roots=(Vector3[])chunk.Roots.Clone(),Bounds=(Bounds[])chunk.Bounds.Clone(),Levels=(int[])chunk.GroundLevels.Clone(),All=chunk.AllBounds,Revision=chunk.RootRevision,RangeValid=chunk.RangeValid,Groups=chunk.Groups.Select(g=>(g,g.Bounds,g.RootBounds,g.MaximumGroundLevel)).ToArray()});
            foreach(var batch in owner.forestBatches.Values)forests.Add((batch,batch.Data.ToArray(),batch.GroundLevels.ToArray(),batch.Dirty,batch.AllDirty,(bool[])batch.DirtyBlocks.Clone()));
            foreach(var field in owner.forestFields.Values)fields.Add((field,field.Bounds));
        }
        public int Check()
        {
            int count=0;
            foreach(var saved in chunks)
            {
                var chunk=saved.Chunk;
                if(!chunk.AllBounds.Equals(saved.All)||chunk.RootRevision!=saved.Revision)throw new InvalidOperationException("Staged terrain changed chunk publication/bounds");
                for(int i=0;i<saved.Roots.Length;i++)
                {
                    if(!chunk.Matrices[i].Equals(saved.Matrices[i])||!chunk.Roots[i].Equals(saved.Roots[i])||!chunk.Bounds[i].Equals(saved.Bounds[i])||chunk.GroundLevels[i]!=saved.Levels[i])throw new InvalidOperationException("Staged terrain differs at a scatter root: "+i);
                    count++;
                }
                foreach(var group in saved.Groups)if(!group.group.Bounds.Equals(group.bounds)||!group.group.RootBounds.Equals(group.roots)||group.group.MaximumGroundLevel!=group.level)throw new InvalidOperationException("Staged terrain differs in group bounds/ground-owner limit");
            }
            foreach(var saved in forests)
            {
                if(saved.batch.Data.Count!=saved.roots.Length)throw new InvalidOperationException("Forest topology changed during ground proof");
                for(int i=0;i<saved.roots.Length;i++)
                {if(!saved.batch.Data[i].Equals(saved.roots[i])||saved.batch.GroundLevels[i]!=saved.levels[i])throw new InvalidOperationException("Staged terrain differs at a forest root: "+i);count++;}
            }
            foreach(var saved in fields)if(!saved.field.Bounds.Equals(saved.bounds))throw new InvalidOperationException("Staged terrain differs in forest bounds");
            return count;
        }
        public void Restore()
        {
            foreach(var saved in chunks)
            {
                var chunk=saved.Chunk;Array.Copy(saved.Matrices,chunk.Matrices,saved.Matrices.Length);Array.Copy(saved.Roots,chunk.Roots,saved.Roots.Length);Array.Copy(saved.Bounds,chunk.Bounds,saved.Bounds.Length);Array.Copy(saved.Levels,chunk.GroundLevels,saved.Levels.Length);
                chunk.AllBounds=saved.All;chunk.RootRevision=saved.Revision;chunk.RangeValid=saved.RangeValid;
                foreach(var group in saved.Groups){group.group.Bounds=group.bounds;group.group.RootBounds=group.roots;group.group.MaximumGroundLevel=group.level;}
            }
            foreach(var saved in forests)
            {
                for(int i=0;i<saved.roots.Length;i++){saved.batch.Data[i]=saved.roots[i];saved.batch.GroundLevels[i]=saved.levels[i];}
                saved.batch.Dirty=saved.dirty;saved.batch.AllDirty=saved.allDirty;saved.batch.DirtyBlocks=(bool[])saved.blocks.Clone();saved.batch.CullValid=false;
            }
            foreach(var saved in fields)saved.field.Bounds=saved.bounds;
            explorer.canopySourceRevision=canopy;explorer.forestRevision=forest;explorer.nextForestVisibility=visibility;
        }
    }
    public IEnumerator PerformanceVerifyGroundPublication(string output)
    {
        while(GroundPublicationPending)yield return null;
        bool holdTerrain=TerrainDiagnostics.HoldTerrain,holdPlants=TerrainDiagnostics.HoldPlants;
        TerrainDiagnostics.HoldTerrain=TerrainDiagnostics.HoldPlants=true;
        int checks=0,slices=0,edits=0,queries=0;var baseline=new GroundProofState(this);
        var keys=tiles.Keys.Where(k=>k.Level==0).OrderBy(k=>TerrainLod.DistanceSquared(k,motion.X,motion.Z)).Take(2).ToList();
        var coarse=tiles.Keys.Where(k=>k.Level>=1).OrderBy(k=>TerrainLod.DistanceSquared(k,motion.X,motion.Z)).First();keys.Add(coarse);
        try
        {
            foreach(var key in keys)foreach(bool partial in new[]{false,true})
            {
                var old=tiles[key];var heights=(float[])old.Heights.Clone();
                if(partial)heights[heights.Length/2]+=.35f;else for(int i=0;i<heights.Length;i++)heights[i]+=.25f;
                var next=new Tile{Heights=heights,Segments=old.Segments};
                var plan=new GroundPublication{Key=key,Product=next,Frame=frame};plan.Changes.Add(key,old,next);
                var work=PrepareGroundPublication(plan);int steps=0;
                while(work.MoveNext())
                {
                    if(!ReferenceEquals(tiles[key],old))throw new InvalidOperationException("Terrain published before root preparation completed");
                    slices++;steps++;if(steps>1000)throw new InvalidOperationException("Staged root preparation did not terminate");yield return null;
                }
                checks+=baseline.Check(); // Preparation must not alter any live root.
                var samples=new List<(double x,double z,double y,int level)>();
                foreach(double x in new[]{0d,.13,.5,.999999,1})foreach(double z in new[]{0d,.37,.5,.999999,1})
                {double px=(key.X+x)*key.SizeM,pz=(key.Z+z)*key.SizeM;double y=PublicationGround(plan,px,pz,-12345,out int level);samples.Add((px,pz,y,level));}
                try
                {
                    tiles[key]=next;residentRevision++;
                    foreach(var sample in samples)
                    {double actual=VisibleGround(sample.x,sample.z,-12345,out int level);if(level!=sample.level||BitConverter.DoubleToInt64Bits(actual)!=BitConverter.DoubleToInt64Bits(sample.y))throw new InvalidOperationException("Future terrain owner differs from actual publication");queries++;}
                    scatterGroundChanges.Clear();scatterGroundChanges.Add(key,old,next);
                    foreach(var chunk in scatterChunks.Values)
                    {
                        double x0=key.X*key.SizeM-originX,z0=key.Z*key.SizeM-originZ;var b=chunk.AllBounds;
                        if(b.max.x>=x0&&b.min.x<=x0+key.SizeM&&b.max.z>=z0&&b.min.z<=z0+key.SizeM)ProjectScatterGround(chunk);
                    }
                    ProjectForestGround();var expected=new GroundProofState(this);
                    baseline.Restore();ApplyGroundPublicationEdits(plan);checks+=expected.Check();edits+=plan.Scatter.Count+plan.Roots.Count;
                }
                finally{tiles[key]=old;residentRevision++;baseline.Restore();scatterGroundChanges.Clear();}
            }
            if(edits==0)throw new InvalidOperationException("Staged terrain proof did not exercise any root corrections");
            yield return PerformanceVerifyCachedGroundPublication(output);
            File.WriteAllText(Path.Combine(output,"staged-ground-proof.txt"),"PASS "+checks+" exact scatter/forest component and level comparisons, group/chunk/field bounds, "+queries+" bit-exact future owner/height queries, "+edits+" affected roots, "+slices+" yielded preparation slices; live roots unchanged throughout preparation.\n");
        }
        finally{baseline.Restore();TerrainDiagnostics.HoldTerrain=holdTerrain;TerrainDiagnostics.HoldPlants=holdPlants;scatterDirty=true;}
    }
    public IEnumerator PerformanceVerifyCachedGroundPublication(string output)
    {
        var key=tiles.Keys.Where(k=>k.Level==0&&!previousTerrain.ContainsKey(k)).OrderBy(k=>TerrainLod.DistanceSquared(k,motion.X,motion.Z)).First();
        var old=tiles[key];var baseline=new GroundProofState(this);float fadeIn=old.FadeIn,fadeOut=old.FadeOut;
        var root=UnityEngine.Object.Instantiate(old.Root,terrainRoot.transform);root.name="Cached ground publication proof";root.SetActive(false);
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            filter.sharedMesh=UnityEngine.Object.Instantiate(filter.sharedMesh);
            if(filter.gameObject.name!="Land and seabed")continue;
            var vertices=filter.sharedMesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i].y+=.25f;
            filter.sharedMesh.vertices=vertices;filter.sharedMesh.RecalculateBounds();
        }
        var heights=(float[])old.Heights.Clone();for(int i=0;i<heights.Length;i++)heights[i]+=.25f;
        var product=new Tile{Root=root,Heights=heights,Segments=old.Segments,Edges=old.Edges,BufferBytes=old.BufferBytes};
        bool selected=false;int slices=0;
        try
        {
            // Obtain the literal result before exercising the actual cache API.
            tiles[key]=product;residentRevision++;scatterGroundChanges.Add(key,old,product);
            foreach(var chunk in scatterChunks.Values)
            {
                double x0=key.X*key.SizeM-originX,z0=key.Z*key.SizeM-originZ;var bounds=chunk.AllBounds;
                if(bounds.max.x>=x0&&bounds.min.x<=x0+key.SizeM&&bounds.max.z>=z0&&bounds.min.z<=z0+key.SizeM)ProjectScatterGround(chunk);
            }
            ProjectForestGround();var expected=new GroundProofState(this);
            tiles[key]=old;residentRevision++;baseline.Restore();scatterGroundChanges.Clear();
            TerrainProducts.Put(new TerrainProductKey(key,product.Segments,product.Edges),product,product.BufferBytes);
            installationsThisFrame=0;frameUploadMs=0;
            if(!RestoreTerrain(key,product.Segments,product.Edges)||!GroundPublicationPending)throw new InvalidOperationException("Cached tile bypassed staged root preparation");
            selected=true;
            while(GroundPublicationPending)
            {
                if(!ReferenceEquals(tiles[key],old)||product.Root.activeSelf)throw new InvalidOperationException("Cached terrain became visible before its roots were ready");
                baseline.Check();AdvanceGroundPublication();slices++;if(slices>1000)throw new InvalidOperationException("Cached root publication did not terminate");
                if(GroundPublicationPending)yield return null;
            }
            if(!ReferenceEquals(tiles[key],product)||!product.Root.activeSelf)throw new InvalidOperationException("Prepared cached terrain was not published");
            int checkedRoots=expected.Check();
            File.WriteAllText(Path.Combine(output,"cached-ground-publication.txt"),"PASS actual cache take/preparation/publication, "+slices+" bounded steps, "+checkedRoots+" exact final root/level/bounds comparisons; old ground and roots retained until commit, cloned land mesh raised with the height grid.\n");
        }
        finally
        {
            bool cancelled=GroundPublicationPending;if(cancelled)CancelGroundPublication();
            tiles[key]=old;residentRevision++;previousTerrain.Remove(key);old.FadeIn=fadeIn;old.FadeOut=fadeOut;old.Root.SetActive(true);
            baseline.Restore();scatterGroundChanges.Clear();coverageDirty=true;
            if(!cancelled&&selected&&product.Root!=null)DestroyTile(product);
            else if(!selected&&product.Root!=null&&!TerrainProducts.Contains(new TerrainProductKey(key,product.Segments,product.Edges)))DestroyTile(product);
        }
    }
}
