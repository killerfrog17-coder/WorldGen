using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    // Only the hidden handoff uses this preparation. Ordinary scatter placement
    // and its visible-ground lookup remain unchanged.
    bool PrepareGroundScatter(PreparedFrame pending,double x,double z)
    {
        double ox=Math.Floor(x/2048)*2048,oz=Math.Floor(z/2048)*2048;
        if(pending.ScatterRevision!=pending.Revision||pending.ScatterOriginX!=ox||pending.ScatterOriginZ!=oz)
        {pending.Scatter.Clear();pending.ScatterRevision=pending.Revision;pending.ScatterOriginX=ox;pending.ScatterOriginZ=oz;}
        foreach(var key in pending.Scatter.Keys.Where(k=>!scatterChunks.ContainsKey(k)).ToArray())pending.Scatter.Remove(key);
        var timer=System.Diagnostics.Stopwatch.StartNew();
        foreach(var pair in scatterChunks)
        {
            if(pending.Scatter.TryGetValue(pair.Key,out var prior)&&ReferenceEquals(prior.Source,pair.Value.Source))continue;
            var chunk=new ScatterChunk{Source=pair.Value.Source};
            ProjectPreparedScatter(chunk,pending,ox,oz);pending.Scatter[pair.Key]=chunk;
            if(timer.Elapsed.TotalMilliseconds>=1)return false;
        }
        return true;
    }
    void ProjectPreparedScatter(ScatterChunk chunk,PreparedFrame pending,double ox,double oz)
    {
        var frame=pending.Frame;
        double Height(double x,double z,double fallback,out int groundLevel)
        {
            groundLevel=17;
            for(int level=0;level<=16;level++)
            {
                double size=TerrainLod.BaseSizeM*HorizontalScale*(1<<level);
                var key=new TerrainTileKey((int)Math.Floor(x/size),(int)Math.Floor(z/size),level,HorizontalScale);
                if(!pending.Tiles.TryGetValue(key,out var tile)||tile.Heights==null)continue;
                groundLevel=level;
                if(tile.Contact!=null&&tile.Contact.TryHeight(x-key.X*size,z-key.Z*size,out double drawn))return drawn;
                int n=tile.Segments,side=n+1;
                double gx=(x/size-key.X)*n,gz=(z/size-key.Z)*n;
                int a=Math.Min(n-1,Math.Max(0,(int)gx)),b=Math.Min(n-1,Math.Max(0,(int)gz));double u=gx-a,v=gz-b;
                double h00=tile.Heights[b*side+a],h10=tile.Heights[b*side+a+1],h01=tile.Heights[(b+1)*side+a],h11=tile.Heights[(b+1)*side+a+1];
                return u+v<=1?h00+(h10-h00)*u+(h01-h00)*v:h11+(h01-h11)*(1-u)+(h10-h11)*(1-v);
            }
            return fallback;
        }
        var source=chunk.Source.Instances;chunk.Matrices=new Matrix4x4[source.Length];chunk.Roots=new Vector3[source.Length];chunk.GroundLevels=new int[source.Length];
        for(int i=0;i<source.Length;i++)
        {
            var p=source[i];double dot=Math.Max(.1,V.Dot(p.Position,frame.Origin));
            double x=V.Dot(p.Position,frame.East)*frame.RadiusM/dot,z=V.Dot(p.Position,frame.North)*frame.RadiusM/dot;
            double y=Height(x,z,frame.SurfaceY(p.Position,p.GroundM),out int level);
            ProjectScatterRootAt(chunk,i,frame,ox,oz,x,z,y,level);
        }
        PrepareScatterBounds(chunk,frame);
    }
    // Both ordinary placement and hidden chart preparation use exactly the
    // same root orientation, embedding and canonical cliff placement. Ground
    // lookup stays with the caller so live placement keeps its owner memo.
    void ProjectScatterRootAt(ScatterChunk chunk,int i,LocalTerrainFrame projectionFrame,double ox,double oz,double x,double z,double y,int level)
    {
        var frame=projectionFrame;var p=chunk.Source.Instances[i];chunk.GroundLevels[i]=level;
            bool cliff=p.Kind==ScatterKind.Outcrop&&CliffRocks.Is(p.Variant);
            if(cliff){y=frame.SurfaceY(p.Position,p.GroundM);chunk.GroundLevels[i]=-1;} // Geological mass, not a root fitted to a coarse terrain triangle.
            bool herb=IsSpecialist(p.Kind),vertical=cliff||herb||p.Kind==ScatterKind.GeneratedTree||p.Kind==ScatterKind.MidPlant||p.Kind==ScatterKind.Broadleaf||p.Kind==ScatterKind.Conifer||p.Kind==ScatterKind.SavannaTree;
            V up=vertical?p.Position:p.Normal;var normal=new Vector3((float)V.Dot(up,frame.East),(float)V.Dot(up,frame.Origin),(float)V.Dot(up,frame.North)).normalized;
            // An ellipsoidal mesh touching at its bottom point looks levitating.
            // Embed its broad lower body; outcrops are bedrock, not balanced eggs.
            var position=new Vector3((float)(x-ox),(float)y-ScatterEmbedding(p),(float)(z-oz));chunk.Roots[i]=position;
            var rotation=Quaternion.FromToRotation(Vector3.up,normal)*Quaternion.Euler(0,(float)p.YawDegrees,0);
            if(cliff)
            {
                var outward=new Vector3((float)V.Dot(p.Normal,frame.East),(float)V.Dot(p.Normal,frame.Origin),(float)V.Dot(p.Normal,frame.North));
                rotation=Quaternion.LookRotation(outward,normal);
                // Center the scanned face on the wall and lean its vertical
                // axis uphill. A radial upright slab protrudes above the hill.
                position=new Vector3((float)(x-ox),(float)y,(float)(z-oz))
                    -(rotation*Vector3.up)*(float)(p.HeightM*.5)-outward*(float)(p.WidthM*.03);
                chunk.Roots[i]=position;
            }
            chunk.Matrices[i]=Matrix4x4.TRS(position,rotation,ScatterScale(p));
    }
    void AdoptScatterFrame(PreparedFrame pending,bool preparedGround)
    {
        double modulus=1024;V p=frame.Origin*(frame.RadiusM+frame.AltitudeM);
        landMaterial.SetVector("_TextureAnchor",new Vector4((float)(p.X%modulus),(float)(p.Y%modulus),(float)(p.Z%modulus),0));
        landMaterial.SetVector("_PlanetEast",new Vector4((float)frame.East.X,(float)frame.East.Y,(float)frame.East.Z,0));
        landMaterial.SetVector("_PlanetNorth",new Vector4((float)frame.North.X,(float)frame.North.Y,(float)frame.North.Z,0));
        landMaterial.SetVector("_PlanetUp",new Vector4((float)frame.Origin.X,(float)frame.Origin.Y,(float)frame.Origin.Z,0));
        PublishTerrainScene();
        foreach(var pair in scatterChunks)
        {
            if(preparedGround)
            {
                var prepared=pending.Scatter[pair.Key];
                pair.Value.Matrices=prepared.Matrices;pair.Value.Roots=prepared.Roots;pair.Value.Bounds=prepared.Bounds;pair.Value.AllBounds=prepared.AllBounds;
                pair.Value.GroundLevels=prepared.GroundLevels;
                pair.Value.Groups=prepared.Groups;pair.Value.GroupsFrame=prepared.GroupsFrame;
                pair.Value.RangeValid=false; // Cached bins reference the previous chart's group objects.
                pair.Value.RootRevision++; // Existing Burst buffers must adopt the new chart too.
            }
            else ProjectScatter(pair.Value);
        }
        if(preparedGround)canopySourceRevision++;
        scatterGroundChanges.Clear();projectedGroundRevision=scatterGroundRevision;scatterDirty=true;
    }
    public double TestPreparedScatterError()
    {
        double error=0;
        foreach(var chunk in scatterChunks.Values)
        {
            var reference=new ScatterChunk{Source=chunk.Source};ProjectScatter(reference);
            for(int i=0;i<chunk.Matrices.Length;i++)for(int j=0;j<16;j++)error=Math.Max(error,Math.Abs(chunk.Matrices[i][j]-reference.Matrices[i][j]));
            if(!ReferenceEquals(chunk.GroupsFrame,frame)||chunk.Groups.Length!=reference.Groups.Length)throw new InvalidOperationException("Prepared scatter retained old-chart spatial bins");
            for(int i=0;i<chunk.Groups.Length;i++)
            {
                var actual=chunk.Groups[i];var expected=reference.Groups[i];
                if(actual.Trees!=expected.Trees||!actual.Indices.SequenceEqual(expected.Indices))throw new InvalidOperationException("Prepared scatter changed spatial-bin membership");
                error=Math.Max(error,(actual.RootBounds.min-expected.RootBounds.min).magnitude);error=Math.Max(error,(actual.RootBounds.max-expected.RootBounds.max).magnitude);
                error=Math.Max(error,(actual.Bounds.min-expected.Bounds.min).magnitude);error=Math.Max(error,(actual.Bounds.max-expected.Bounds.max).magnitude);
            }
        }
        return error;
    }
}
