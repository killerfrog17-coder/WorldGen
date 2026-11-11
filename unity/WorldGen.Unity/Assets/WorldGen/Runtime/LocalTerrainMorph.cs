using System;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    long terrainMorphPrepared,terrainMorphVertices;
    string TerrainMorphReceipt=>"opaque terrain morphs="+terrainMorphPrepared+" sampled vertices="+terrainMorphVertices;
    // Only distant dry leaf terrain without vegetation can change shape.
    // Interactive terrain/contact and all object roots remain fully final.
    void PrepareTerrainMorph(TerrainTileKey key,TerrainUpload data)
    {
        if(TerrainVideoOptions.HardChunkUpdates||frame==null||data.HasWater||!data.UnrefinedLand||data.HasForestPotential||
            TerrainLod.DistanceSquared(key,motion.X,motion.Z)<=TerrainMorphSafetyRadius*TerrainMorphSafetyRadius)return;
        foreach(var candidate in tiles.Keys)if(candidate.Level<key.Level&&Overlaps(key,candidate))return;
        if(!TerrainRootFree(key))return;
        Tile source=null;TerrainTileKey sourceKey=key;
        if(tiles.TryGetValue(key,out var same))
        {
            if(same.Segments>data.Segments)return; // Coarsening snaps to ready geometry.
            source=same;
        }
        else for(int depth=1;depth<24-key.Level;depth++)
        {
            var ancestor=new TerrainTileKey(key.X>>depth,key.Z>>depth,key.Level+depth,key.Scale);
            if(tiles.TryGetValue(ancestor,out source)){sourceKey=ancestor;break;}
        }
        if(source==null||source.Heights==null||source.Segments<=0)return;
        // CPU samples below describe final source triangles. Finish its old
        // morph before sampling so the capture and the visible source agree.
        if(source.FadeIn!=0||source.FadeOut!=0){source.FadeIn=source.FadeOut=0;coverageDirty=true;}
        data.PrepareMorph(sourceKey,source);
        if(data.MorphPrepared){terrainMorphPrepared++;terrainMorphVertices+=data.Heights.Length;}
    }
    bool TerrainRootFree(TerrainTileKey key)
    {
        double x0=key.X*key.SizeM,z0=key.Z*key.SizeM,x1=x0+key.SizeM,z1=z0+key.SizeM;
        foreach(var field in forestFields.Values)
        {
            var b=field.Bounds;
            if(field.Roots.Count>0&&b.max.x>=x0&&b.min.x<=x1&&b.max.z>=z0&&b.min.z<=z1)return false;
        }
        foreach(var chunk in scatterChunks.Values)
        {
            if(chunk.Source.Instances.Length==0)continue;
            if(!ReferenceEquals(chunk.GroupsFrame,frame))return false;
            var b=chunk.AllBounds;
            if(b.max.x+originX>=x0&&b.min.x+originX<=x1&&b.max.z+originZ>=z0&&b.min.z+originZ<=z1)return false;
        }
        return true;
    }
}
