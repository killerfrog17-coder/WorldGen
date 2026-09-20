using System;
using System.Collections.Generic;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Equirectangular presentation of the completed world. No generation or resampling of authoritative state.</summary>
    public static class MapView
    {
        struct Vertex
        {
            public double X, Y;
            public V Color;
            public int Cell;
            public Vertex(double x, double y, V color, int cell) { X = x; Y = y; Color = color; Cell = cell; }
        }

        public static ViewData Build(World world, Layer layer = Layer.Terrain, int seaLevel = 0, double centerLongitude = 0, int detail = 0, double irregularity = .75)
        {
            return SurfaceMesh.Combine(SurfaceMesh.Chunks(world, new ViewOptions { Projection = Projection.Map, Layer = layer, SeaLevel = seaLevel, CenterLongitude = centerLongitude, GridSpacing = 0 }));
        }
        internal static ViewData Project(ViewData globe, double centerLongitude)
        {
            var positions = new List<float>(); var colors = new List<float>(); var ids = new List<int>(); var triangles = new List<int>(); var lines = new List<float>();
            Vertex Project(int cell)
            {
                var p = new V(globe.Positions[cell * 3], -globe.Positions[cell * 3 + 2], globe.Positions[cell * 3 + 1]).Unit;
                double x = Math.Atan2(p.Y, p.X) * 2 / Math.PI - centerLongitude / 90;
                x -= 4 * Math.Floor((x + 2) / 4);
                return new Vertex(x, Math.Asin(Math.Max(-1, Math.Min(1, p.Z))) * 2 / Math.PI,
                    new V(globe.Colors[cell * 3], globe.Colors[cell * 3 + 1], globe.Colors[cell * 3 + 2]), globe.CellIds[cell]);
            }
            void Emit(Vertex v)
            {
                positions.Add((float)v.X); positions.Add((float)v.Y); positions.Add(0);
                colors.Add((float)v.Color.X); colors.Add((float)v.Color.Y); colors.Add((float)v.Color.Z); ids.Add(v.Cell);
            }
            void Face(Vertex[] vertices)
            {
                // Longitude at the pole is undefined. Use the midpoint of this face's other two vertices.
                for (int j = 0; j < 3; j++) if (Math.Abs(vertices[j].Y) > .999999)
                {
                    double a = vertices[(j + 1) % 3].X, b = vertices[(j + 2) % 3].X;
                    if (b - a > 2) b -= 4; if (b - a < -2) b += 4;
                    vertices[j].X = (a + b) / 2;
                }
                for (int j = 1; j < 3; j++)
                { while (vertices[j].X - vertices[0].X > 2) vertices[j].X -= 4; while (vertices[j].X - vertices[0].X < -2) vertices[j].X += 4; }
                for (int j = 0; j < 3; j++) if (Math.Abs(vertices[j].Y) > .999999)
                {
                    var pole = vertices[j]; var a = vertices[(j + 1) % 3]; var b = vertices[(j + 2) % 3];
                    vertices = new[] { a, b, new Vertex(b.X, pole.Y, pole.Color, pole.Cell), new Vertex(a.X, pole.Y, pole.Color, pole.Cell) };
                    break;
                }
                if (vertices.Length == 3 && vertices[0].X >= -2 && vertices[0].X <= 2 && vertices[1].X >= -2 && vertices[1].X <= 2 && vertices[2].X >= -2 && vertices[2].X <= 2)
                {
                    int start = ids.Count; Emit(vertices[0]); Emit(vertices[1]); Emit(vertices[2]);
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2); return;
                }
                for (int shift = -1; shift <= 1; shift++)
                {
                    var polygon = new List<Vertex>(3);
                    foreach (var v in vertices) polygon.Add(new Vertex(v.X + shift * 4, v.Y, v.Color, v.Cell));
                    polygon = Clip(Clip(polygon, -2, true), 2, false);
                    for (int j = 1; j + 1 < polygon.Count; j++)
                    {
                        var a = polygon[0]; var b = polygon[j]; var c = polygon[j + 1];
                        double area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                        if (Math.Abs(area) < 1e-14) continue;
                        int start = ids.Count; Emit(a); Emit(b); Emit(c);
                        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    }
                }
            }
            V SpherePoint(int vertex) => new V(globe.Positions[vertex * 3], -globe.Positions[vertex * 3 + 2], globe.Positions[vertex * 3 + 1]).Unit;
            for (int t = 0; t < globe.Triangles.Length; t += 3)
            {
                int ia = globe.Triangles[t], ib = globe.Triangles[t + 1], ic = globe.Triangles[t + 2];
                var vertices = new[] { Project(ia), Project(ib), Project(ic) };
                V a = SpherePoint(ia), b = SpherePoint(ib), c = SpherePoint(ic);
                double ab = V.Cross(a, b).Z, bc = V.Cross(b, c).Z, ca = V.Cross(c, a).Z;
                int pole = ab > 0 && bc > 0 && ca > 0 ? 1 : ab < 0 && bc < 0 && ca < 0 ? -1 : 0;
                if (pole != 0 && Math.Abs(vertices[0].Y) < .999999 && Math.Abs(vertices[1].Y) < .999999 && Math.Abs(vertices[2].Y) < .999999)
                {
                    // A face can enclose a pole even when none of its vertices is the pole.
                    // Split that cap before longitude unwrapping so it covers the top/bottom edge.
                    var tip = new Vertex(0, pole, (vertices[0].Color + vertices[1].Color + vertices[2].Color) / 3, vertices[0].Cell);
                    for (int j = 0; j < 3; j++) Face(new[] { vertices[j], vertices[(j + 1) % 3], tip });
                }
                else Face(vertices);
            }
            var lineColors = new List<float>();
            for (int i = 0; i < globe.Lines.Length; i += 6)
            {
                Vertex LinePoint(int k)
                {
                    var p = new V(globe.Lines[k], -globe.Lines[k + 2], globe.Lines[k + 1]).Unit;
                    double x = Math.Atan2(p.Y, p.X) * 2 / Math.PI - centerLongitude / 90; x -= 4 * Math.Floor((x + 2) / 4);
                    return new Vertex(x, Math.Asin(Math.Max(-1, Math.Min(1, p.Z))) * 2 / Math.PI, new V(), 0);
                }
                var a = LinePoint(i); var b = LinePoint(i + 3);
                if (Math.Abs(a.Y) > .99999) a.X = b.X;
                if (Math.Abs(b.Y) > .99999) b.X = a.X;
                if (b.X - a.X > 2) b.X -= 4; if (b.X - a.X < -2) b.X += 4;
                for (int shift = -1; shift <= 1; shift++)
                {
                    var p = new Vertex(a.X + shift * 4, a.Y, a.Color, 0); var q = new Vertex(b.X + shift * 4, b.Y, b.Color, 0);
                    if ((p.X < -2 && q.X < -2) || (p.X > 2 && q.X > 2)) continue;
                    if (p.X < -2 || p.X > 2) p = Intersect(p, q, p.X < -2 ? -2 : 2);
                    if (q.X < -2 || q.X > 2) q = Intersect(q, p, q.X < -2 ? -2 : 2);
                    lines.Add((float)p.X); lines.Add((float)p.Y); lines.Add(-.002f); lines.Add((float)q.X); lines.Add((float)q.Y); lines.Add(-.002f);
                    for (int k = 0; k < 6; k++) lineColors.Add(globe.LineColors.Length > i + k ? globe.LineColors[i + k] : .2f);
                }
            }
            return new ViewData { Positions = positions.ToArray(), Colors = colors.ToArray(), Triangles = triangles.ToArray(), CellIds = ids.ToArray(), Lines = lines.ToArray(), LineColors = lineColors.ToArray(), IsMap = true, Unlit = true };
        }
        static Vertex Intersect(Vertex a, Vertex b, double x)
        {
            double t = (x - a.X) / (b.X - a.X);
            return new Vertex(x, a.Y + (b.Y - a.Y) * t, a.Color * (1 - t) + b.Color * t, t < .5 ? a.Cell : b.Cell);
        }
        static List<Vertex> Clip(List<Vertex> polygon, double edge, bool keepGreater)
        {
            var result = new List<Vertex>(); if (polygon.Count == 0) return result;
            Vertex a = polygon[polygon.Count - 1]; bool insideA = keepGreater ? a.X >= edge : a.X <= edge;
            foreach (var b in polygon)
            {
                bool insideB = keepGreater ? b.X >= edge : b.X <= edge;
                if (insideA != insideB) result.Add(Intersect(a, b, edge));
                if (insideB) result.Add(b); a = b; insideA = insideB;
            }
            return result;
        }
    }
}
