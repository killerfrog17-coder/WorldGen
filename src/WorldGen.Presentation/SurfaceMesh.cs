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
        public double CenterLongitude; public bool Wireframe, ShowPlates;
        public ViewOptions Clone() => (ViewOptions)MemberwiseClone();
    }
    public static class SurfaceMesh
    {
        // Bounded pieces let adapters upload a few milliseconds of geometry per
        // frame while the next piece is prepared on a worker. No resampling.
        public const int EdgesPerChunk = 8192;
        public static IEnumerable<ViewData> Chunks(World world, ViewOptions options, CancellationToken token = default)
        {
            var layer = options.ShowPlates && options.Layer == Layer.Terrain ? Layer.Microplates : options.Layer;
            var values = GlobeView.CellValues(world, layer, options.SeaLevel, token: token);
            var s = world.Surface; var cornerHeight = new int[s.Corners.Length];
            for (int t = 0; t < cornerHeight.Length; t++)
                cornerHeight[t] = (world.ElevationM[s.Triangles[t * 3]] + world.ElevationM[s.Triangles[t * 3 + 1]] + world.ElevationM[s.Triangles[t * 3 + 2]]) / 3;
            V At(V p, int height, double lift = 0)
            {
                double radius = 1 + Math.Max(0, height - options.SeaLevel) * (14.0 / 6371000) + lift;
                return new V(p.X * radius, p.Z * radius, -p.Y * radius);
            }
            for (int first = 0; first < s.Edges.Length; first += EdgesPerChunk)
            {
                token.ThrowIfCancellationRequested(); int end = Math.Min(first + EdgesPerChunk, s.Edges.Length);
                int count = (end - first) * 6, at = 0; var lines = new List<float>(); var lineColors = new List<float>();
                var data = new ViewData { Positions = new float[count * 3], Colors = new float[count * 3], CellIds = new int[count], Triangles = new int[count], Unlit = values.Unlit };
                void Emit(V p, int owner, int height)
                {
                    int k = at * 3; data.Positions[k] = (float)p.X; data.Positions[k + 1] = (float)p.Y; data.Positions[k + 2] = (float)p.Z;
                    if (layer == Layer.Heightmap)
                    { float gray = (float)D.Clamp((height + 5000) / 11000.0); data.Colors[k] = gray; data.Colors[k + 1] = gray; data.Colors[k + 2] = gray; }
                    else for (int j = 0; j < 3; j++) data.Colors[k + j] = values.Colors[owner * 3 + j];
                    data.CellIds[at] = owner; data.Triangles[at] = at; at++;
                }
                for (int e = first; e < end; e++)
                {
                    if ((e & 1023) == 0) token.ThrowIfCancellationRequested();
                    var edge = s.Edges[e]; int ta = edge.TriangleA, tb = edge.TriangleB;
                    V a = At(s.Corners[ta], cornerHeight[ta]), b = At(s.Corners[tb], cornerHeight[tb]);
                    void Fan(int cell, V left, V right, int leftHeight, int rightHeight)
                    {
                        V center = At(s.Cells[cell].Position, world.ElevationM[cell]);
                        if (V.Dot(V.Cross(left - center, right - center), center) < 0)
                        { var swap = left; left = right; right = swap; int h = leftHeight; leftHeight = rightHeight; rightHeight = h; }
                        Emit(center, cell, world.ElevationM[cell]); Emit(left, cell, leftHeight); Emit(right, cell, rightHeight);
                    }
                    Fan(edge.A, a, b, cornerHeight[ta], cornerHeight[tb]);
                    Fan(edge.B, b, a, cornerHeight[tb], cornerHeight[ta]);
                    bool border = options.ShowPlates && world.Microplates[world.CellMicroplate[edge.A]].ParentAffinityId != world.Microplates[world.CellMicroplate[edge.B]].ParentAffinityId;
                    if (options.Wireframe || border)
                    {
                        Add(lines, At(s.Corners[ta], cornerHeight[ta], .00015)); Add(lines, At(s.Corners[tb], cornerHeight[tb], .00015));
                        V color = border ? new V(.96, .83, .43) : new V(.055, .075, .085); Add(lineColors, color); Add(lineColors, color);
                    }
                }
                data.Lines = lines.ToArray(); data.LineColors = lineColors.ToArray();
                yield return options.Projection == Projection.Map ? MapView.Project(data, options.CenterLongitude) : data;
            }
            if (options.GridSpacing != 0)
            {
                var grid = Grid(options.GridSpacing, world);
                yield return options.Projection == Projection.Map ? MapView.Project(grid, options.CenterLongitude) : grid;
            }
        }
        static void Add(List<float> list, V p) { list.Add((float)p.X); list.Add((float)p.Y); list.Add((float)p.Z); }
        public static ViewData Grid(int spacing, World? world = null)
        {
            if (spacing != 15 && spacing != 30) throw new ArgumentOutOfRangeException(nameof(spacing));
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
