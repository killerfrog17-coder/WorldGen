using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>One surface, reusable cell palettes, and independently switchable overlays.</summary>
    public static class DisplayCache
    {
        public static readonly Layer[] MainLayers = { Layer.Microplates, Layer.Terrain, Layer.Satellite, Layer.Simulation, Layer.Climate, Layer.Heightmap, Layer.Ecoregions };
        public sealed class Piece { public string Kind = "surface"; public ViewData Data = new ViewData(); }
        public static int ExpectedChunkCount(World world) =>
            4 * ((world.Surface.Edges.Length + SurfaceMesh.EdgesPerChunk - 1) / SurfaceMesh.EdgesPerChunk)
            + (world.Hydrology.Rivers.Length + SurfaceMesh.RiverReachesPerChunk - 1) / SurfaceMesh.RiverReachesPerChunk + 4
            + (world.Surface.Triangles.Length+NaturalSurface.FacesPerChunk*3-1)/(NaturalSurface.FacesPerChunk*3)
            + ((world.Hydrology.LakeCoverageFraction.Count(f=>f>0)+(world.Subcells?.Islands.Length??0))*192+23999)/24000;
        public static IEnumerable<Piece> Geometry(World world, Projection projection, CancellationToken token)
        {
            var options = new ViewOptions { Projection=projection, Layer=Layer.Terrain, GridSpacing=0, ShowRivers=true, CachedDisplay=true };
            int surfaceCount=(world.Surface.Edges.Length+SurfaceMesh.EdgesPerChunk-1)/SurfaceMesh.EdgesPerChunk, index=0;
            foreach(var data in SurfaceMesh.Chunks(world,options,token))
                yield return new Piece { Kind=index++<surfaceCount?"surface":"rivers",Data=data };
            if(GenerationPreview.Available(world,Layer.Terrain))
            {
                foreach(var data in NaturalSurface.Chunks(world,projection,token))yield return new Piece{Kind="natural",Data=data};
                foreach(var data in NaturalSurface.WaterAndIslands(world,projection,token))yield return new Piece{Kind="smallfeatures",Data=data};
            }
            ViewData Project(ViewData data) => projection==Projection.Map?MapView.Project(data,0):data;
            var s=world.Surface;
            for(int first=0;first<s.Edges.Length;first+=SurfaceMesh.EdgesPerChunk)
            {
                token.ThrowIfCancellationRequested();
                var wire=new List<float>();var plates=new List<float>();var ecology=new List<float>();
                void Add(List<float> list,int corner)
                {
                    double h=(world.ElevationM[s.Triangles[corner*3]]+world.ElevationM[s.Triangles[corner*3+1]]+world.ElevationM[s.Triangles[corner*3+2]])/3.0;
                    var p=s.Corners[corner]*(1.0002+Math.Max(0,h)*14/6371000);
                    list.Add((float)p.X);list.Add((float)p.Z);list.Add((float)-p.Y);
                }
                for(int e=first;e<Math.Min(first+SurfaceMesh.EdgesPerChunk,s.Edges.Length);e++)
                {
                    var edge=s.Edges[e];Add(wire,edge.TriangleA);Add(wire,edge.TriangleB);
                    if(world.CellMicroplate[edge.A]!=world.CellMicroplate[edge.B]){Add(plates,edge.TriangleA);Add(plates,edge.TriangleB);}
                    if(world.Ecology!=null && world.Ecology.Region[edge.A]!=world.Ecology.Region[edge.B]){Add(ecology,edge.TriangleA);Add(ecology,edge.TriangleB);}
                }
                Piece Lines(string kind,List<float> lines,float r,float g,float b)
                {
                    var colors=new float[lines.Count];for(int i=0;i<colors.Length;i+=3){colors[i]=r;colors[i+1]=g;colors[i+2]=b;}
                    return new Piece{Kind=kind,Data=Project(new ViewData{Lines=lines.ToArray(),LineColors=colors,Unlit=true})};
                }
                yield return Lines("wire",wire,.055f,.075f,.085f);
                yield return Lines("plates",plates,.96f,.83f,.43f);
                yield return Lines("ecology",ecology,.055f,.075f,.085f);
            }
            foreach(int spacing in new[]{5,10,15,30}){token.ThrowIfCancellationRequested();yield return new Piece{Kind="grid"+spacing,Data=Project(SurfaceMesh.Grid(spacing,world))};}
        }
    }
}
