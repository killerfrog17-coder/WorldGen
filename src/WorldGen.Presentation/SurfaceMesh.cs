using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public sealed class ViewOptions
    {
        public Projection Projection; public Layer Layer; public int SeaLevel, GridSpacing = 30;
        public double CenterLongitude; public bool Wireframe, ShowPlates, ShowRivers;
        public bool CachedDisplay;
        public bool Natural = true;
        public ViewOptions Clone() => (ViewOptions)MemberwiseClone();
    }
    public static class SurfaceMesh
    {
        // Bounded pieces let adapters upload a few milliseconds of geometry per
        // frame while the next piece is prepared on a worker. No resampling.
        public const int EdgesPerChunk = 8192;
        public const int RiverReachesPerChunk = RiverMesh.ReachesPerChunk;
        public static int ExpectedChunkCount(World world, ViewOptions options)
        {
            int surfaces = (world.Surface.Edges.Length + EdgesPerChunk - 1) / EdgesPerChunk;
            int rivers = options.ShowRivers ? (world.Hydrology.Rivers.Length + RiverReachesPerChunk - 1) / RiverReachesPerChunk : 0;
            return surfaces + rivers + (options.GridSpacing == 0 ? 0 : 1);
        }
        public static IEnumerable<ViewData> Chunks(World world, ViewOptions options, CancellationToken token = default)
        {
            var layer = options.Layer;
            // Cached geometry only needs height gray; main palettes are prepared separately.
            var values = options.CachedDisplay ? null : GlobeView.CellValues(world, layer, options.SeaLevel, token: token);
            var s = world.Surface; var renderHeight = Array.ConvertAll(world.ElevationM, h => (double)h);
            bool showWaterSurface = options.SeaLevel == 0 && (layer == Layer.Terrain || layer == Layer.Satellite || layer == Layer.Climate || layer == Layer.WaterBodies || layer == Layer.LakesAndInlandSeas);
            if (showWaterSurface) for (int i = 0; i < renderHeight.Length; i++)
                if (GlobeView.TryVisibleWater(world, i, out var water) && water.Kind != WaterKind.Ocean) renderHeight[i] = water.SurfaceElevationM;
            double SignedHeight(int i)=>world.Hydrology.OceanConnected.Length>i?(world.Hydrology.OceanConnected[i]?Math.Min(-1,renderHeight[i]):Math.Max(1,renderHeight[i])):renderHeight[i];
            var cornerHeight = new double[s.Corners.Length];
            for (int t = 0; t < cornerHeight.Length; t++)
                cornerHeight[t] = Math.Truncate((renderHeight[s.Triangles[t * 3]] + renderHeight[s.Triangles[t * 3 + 1]] + renderHeight[s.Triangles[t * 3 + 2]]) / 3);
            V At(V p, double height, double lift = 0)
            {
                double radius = 1 + Math.Max(0, height - options.SeaLevel) * (14.0 / 6371000) + lift;
                return new V(p.X * radius, p.Z * radius, -p.Y * radius);
            }
            for (int first = 0; first < s.Edges.Length; first += EdgesPerChunk)
            {
                token.ThrowIfCancellationRequested(); int end = Math.Min(first + EdgesPerChunk, s.Edges.Length);
                int count = (end - first) * 6, at = 0, index = 0; var lines = new List<float>(); var lineColors = new List<float>();
                var vertices = new Dictionary<(int owner, int point), int>();
                var data = new ViewData { Positions = new float[count * 3], Colors = new float[count * 3], CellIds = new int[count], Triangles = new int[count], Unlit = values?.Unlit ?? false };
                if(options.CachedDisplay){data.NaturalHeight=new float[count];data.BlendCellIds=new int[count*2];}
                void Emit(V p, int owner, double height, int point)
                {
                    // Share within a cell, never across cells: cell colors stay crisp.
                    if (vertices.TryGetValue((owner, point), out int existing)) { data.Triangles[index++] = existing; return; }
                    vertices.Add((owner, point), at);
                    int k = at * 3; data.Positions[k] = (float)p.X; data.Positions[k + 1] = (float)p.Y; data.Positions[k + 2] = (float)p.Z;
                    if (layer == Layer.Heightmap || options.CachedDisplay)
                    { float gray = (float)D.Clamp((height + 5000) / 11000.0); data.Colors[k] = gray; data.Colors[k + 1] = gray; data.Colors[k + 2] = gray; }
                    else for (int j = 0; j < 3; j++) data.Colors[k + j] = values!.Colors[owner * 3 + j];
                    if(options.CachedDisplay)
                    {
                        int a=owner,b=owner;if(point>=0){a=s.Triangles[point*3];b=s.Triangles[point*3+1];int third=s.Triangles[point*3+2];if(a==owner)a=third;else if(b==owner)b=third;}
                        data.BlendCellIds[at*2]=a;data.BlendCellIds[at*2+1]=b;data.NaturalHeight[at]=(float)((SignedHeight(owner)+SignedHeight(a)+SignedHeight(b))/3);
                    }
                    data.CellIds[at] = owner; data.Triangles[index++] = at; at++;
                }
                for (int e = first; e < end; e++)
                {
                    if ((e & 1023) == 0) token.ThrowIfCancellationRequested();
                    var edge = s.Edges[e]; int ta = edge.TriangleA, tb = edge.TriangleB;
                    V aPoint = s.Corners[ta], bPoint = s.Corners[tb];
                    void Fan(int cell, V leftPoint, V rightPoint, double leftHeight, double rightHeight, int leftId, int rightId)
                    {
                        if (showWaterSurface && GlobeView.TryVisibleWater(world, cell, out var water) && water.Kind != WaterKind.Ocean)
                            leftHeight = rightHeight = water.SurfaceElevationM;
                        V left = At(leftPoint, leftHeight), right = At(rightPoint, rightHeight);
                        V center = At(s.Cells[cell].Position, renderHeight[cell]);
                        if (V.Dot(V.Cross(left - center, right - center), center) < 0)
                        { var swap = left; left = right; right = swap; double h = leftHeight; leftHeight = rightHeight; rightHeight = h; int id = leftId; leftId = rightId; rightId = id; }
                        Emit(center, cell, renderHeight[cell], -1); Emit(left, cell, leftHeight, leftId); Emit(right, cell, rightHeight, rightId);
                    }
                    Fan(edge.A, aPoint, bPoint, cornerHeight[ta], cornerHeight[tb], ta, tb);
                    Fan(edge.B, bPoint, aPoint, cornerHeight[tb], cornerHeight[ta], tb, ta);
                    bool border = options.Layer == Layer.Microplates && world.CellMicroplate[edge.A] != world.CellMicroplate[edge.B];
                    bool ecologicalBorder=layer==Layer.Ecoregions && world.Ecology!=null && world.Ecology.Region[edge.A]!=world.Ecology.Region[edge.B];
                    if (options.Wireframe || border || ecologicalBorder)
                    {
                        Add(lines, At(aPoint, cornerHeight[ta], .00015)); Add(lines, At(bPoint, cornerHeight[tb], .00015));
                        V color = border ? new V(.96, .83, .43) : new V(.055, .075, .085); Add(lineColors, color); Add(lineColors, color);
                    }
                }
                Array.Resize(ref data.Positions, at * 3); Array.Resize(ref data.Colors, at * 3); Array.Resize(ref data.CellIds, at);
                if(options.CachedDisplay){Array.Resize(ref data.NaturalHeight,at);Array.Resize(ref data.BlendCellIds,at*2);}
                data.Lines = lines.ToArray(); data.LineColors = lineColors.ToArray();
                yield return options.Projection == Projection.Map ? MapView.Project(data, options.CenterLongitude) : data;
            }
            if (options.ShowRivers)
                foreach(var river in RiverMesh.Chunks(world,token))
                    yield return options.Projection==Projection.Map?MapView.Project(river,options.CenterLongitude,-.003):river;
            if (options.GridSpacing != 0)
            {
                var grid = Grid(options.GridSpacing, world);
                yield return options.Projection == Projection.Map ? MapView.Project(grid, options.CenterLongitude) : grid;
            }
        }
        static void Add(List<float> list, V p) { list.Add((float)p.X); list.Add((float)p.Y); list.Add((float)p.Z); }
        public static ViewData Grid(int spacing, World? world = null)
        {
            if (spacing != 5 && spacing != 10 && spacing != 15 && spacing != 30) throw new ArgumentOutOfRangeException(nameof(spacing));
            var lines = new List<float>(); int nearest = 0;
            V Point(double lon, double lat)
            {
                double l = lon * Math.PI / 180, t = lat * Math.PI / 180; V point = new V(Math.Cos(l) * Math.Cos(t), Math.Sin(l) * Math.Cos(t), Math.Sin(t));
                double radius = 1.0005;
                if (world != null) { nearest = Erosion.Nearest(world.Surface, point, nearest); radius += Math.Max(0, world.ElevationM[nearest]) * (14.0 / 6371000); }
                return new V(point.X, point.Z, -point.Y) * radius;
            }
            for (int lat = -90 + spacing; lat < 90; lat += spacing) for (int lon = -180; lon < 180; lon++) { Add(lines, Point(lon, lat)); Add(lines, Point(lon + 1, lat)); }
            for (int lon = -180; lon < 180; lon += spacing) for (int lat = -90; lat < 90; lat++) { Add(lines, Point(lon, lat)); Add(lines, Point(lon, lat + 1)); }
            return new ViewData { Lines = lines.ToArray(), LineColors = Enumerable.Repeat(.36f, lines.Count).ToArray(), Unlit = true };
        }
        public static ViewData Combine(IEnumerable<ViewData> pieces)
        {
            var data = pieces.ToArray(); var result = new ViewData { Positions = new float[data.Sum(p => p.Positions.Length)], Colors = new float[data.Sum(p => p.Colors.Length)],
                Lines = new float[data.Sum(p => p.Lines.Length)], LineColors = new float[data.Sum(p => p.LineColors.Length)], Triangles = new int[data.Sum(p => p.Triangles.Length)], CellIds = new int[data.Sum(p => p.CellIds.Length)], IsMap = data[0].IsMap, Unlit = data[0].Unlit };
            int vertex = 0, triangle = 0, line = 0;
            foreach (var piece in data)
            {
                Array.Copy(piece.Positions, 0, result.Positions, vertex * 3, piece.Positions.Length); Array.Copy(piece.Colors, 0, result.Colors, vertex * 3, piece.Colors.Length);
                Array.Copy(piece.CellIds, 0, result.CellIds, vertex, piece.CellIds.Length);
                for (int i = 0; i < piece.Triangles.Length; i++) result.Triangles[triangle + i] = vertex + piece.Triangles[i];
                Array.Copy(piece.Lines, 0, result.Lines, line, piece.Lines.Length); Array.Copy(piece.LineColors, 0, result.LineColors, line, piece.LineColors.Length);
                vertex += piece.CellIds.Length; triangle += piece.Triangles.Length; line += piece.Lines.Length;
            }
            return result;
        }
    }
}
