using System;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Double-precision planet anchor; local meshes are disposable, metre-scale render products.</summary>
    public sealed class LocalTerrainFrame
    {
        public readonly V Origin, East, North;
        public readonly double RadiusM, AltitudeM;
        public LocalTerrainFrame(V origin, double radiusM, double altitudeM)
        {
            Origin = origin.Unit; RadiusM = radiusM; AltitudeM = altitudeM;
            East = V.Cross(Math.Abs(Origin.Z) < .99 ? new V(0, 0, 1) : new V(0, 1, 0), Origin).Unit;
            North = V.Cross(Origin, East).Unit;
        }
        public V Position(double x, double z) => (Origin + East * (x / RadiusM) + North * (z / RadiusM)).Unit;
        public double SurfaceY(V position, double heightM) => (RadiusM + heightM) *
            (position.X * Origin.X + position.Y * Origin.Y + position.Z * Origin.Z) - RadiusM - AltitudeM;
    }

    public sealed class LocalTerrainMesh
    {
        public float[] Positions = Array.Empty<float>(), Normals = Array.Empty<float>(), Colors = Array.Empty<float>();
        public float[] WaterPositions = Array.Empty<float>(), WaterColors = Array.Empty<float>();
        public int[] Triangles = Array.Empty<int>();
        public double EastM, NorthM, SizeM, MinHeightM, MaxHeightM, BuildMilliseconds;
        public int Segments, GroundVertices;
        public static LocalTerrainMesh Build(LocalTerrain terrain, LocalTerrainFrame frame, double eastM, double northM,
            double sizeM = 2048, int segments = 64, CancellationToken token = default)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            if (segments < 4 || segments > 128 || sizeM <= 0 || sizeM > 10000 || double.IsNaN(sizeM)) throw new ArgumentOutOfRangeException(nameof(segments));
            var mesh = new LocalTerrainMesh { EastM = eastM, NorthM = northM, SizeM = sizeM, Segments = segments, MinHeightM = double.MaxValue, MaxHeightM = double.MinValue };
            double step = sizeM / segments;
            var patch = terrain.Patch(frame.Position(eastM + sizeM / 2, northM + sizeM / 2), sizeM * 1.1 + step * 2);
            int side = segments + 1, halo = side + 2, ground = side * side, perimeter = segments * 4;
            mesh.GroundVertices = ground;
            var heights = new double[halo * halo]; var samples = new LocalTerrainSample[ground];
            for (int z = -1; z <= side; z++)
            {
                token.ThrowIfCancellationRequested();
                for (int x = -1; x <= side; x++)
                {
                    var position = frame.Position(eastM + x * step, northM + z * step); var sample = patch.Sample(position);
                    double height = frame.SurfaceY(position, sample.ElevationM); heights[(z + 1) * halo + x + 1] = height;
                    if (x >= 0 && x < side && z >= 0 && z < side) samples[z * side + x] = sample;
                }
            }
            mesh.Positions = new float[(ground + perimeter) * 3]; mesh.Normals = new float[mesh.Positions.Length]; mesh.Colors = new float[(ground + perimeter) * 4];
            mesh.WaterPositions = new float[ground * 3]; mesh.WaterColors = new float[ground * 4];
            for (int z = 0; z < side; z++) for (int x = 0; x < side; x++)
            {
                int i = z * side + x, at = (z + 1) * halo + x + 1; var sample = samples[i]; double y = heights[at];
                mesh.MinHeightM = Math.Min(mesh.MinHeightM, y); mesh.MaxHeightM = Math.Max(mesh.MaxHeightM, y);
                mesh.Positions[i * 3] = (float)(x * step); mesh.Positions[i * 3 + 1] = (float)y; mesh.Positions[i * 3 + 2] = (float)(z * step);
                mesh.WaterPositions[i * 3] = (float)(x * step); mesh.WaterPositions[i * 3 + 1] = (float)frame.SurfaceY(frame.Position(eastM + x * step, northM + z * step), 0); mesh.WaterPositions[i * 3 + 2] = (float)(z * step);
                mesh.WaterColors[i * 4] = .13f; mesh.WaterColors[i * 4 + 1] = .37f; mesh.WaterColors[i * 4 + 2] = .46f; mesh.WaterColors[i * 4 + 3] = (float)sample.OceanInfluence;
                double nx = (heights[at - 1] - heights[at + 1]) / (2 * step), nz = (heights[at - halo] - heights[at + halo]) / (2 * step);
                double length = Math.Sqrt(nx * nx + 1 + nz * nz); double up = 1 / length;
                mesh.Normals[i * 3] = (float)(nx / length); mesh.Normals[i * 3 + 1] = (float)up; mesh.Normals[i * 3 + 2] = (float)(nz / length);
                double rock = D.Clamp((1 - up - .08) / .37), snow = sample.Biome == Biome.Ice ? D.Clamp(up * 2 - .4) : 0;
                V color = sample.CoverColor;
                if (sample.ElevationM < 8) color = color * .4 + new V(.48, .43, .32) * .6;
                color = color * (1 - rock) + new V(.39, .37, .34) * rock;
                color = color * (1 - snow) + new V(.89, .92, .94) * snow;
                mesh.Colors[i * 4] = (float)color.X; mesh.Colors[i * 4 + 1] = (float)color.Y; mesh.Colors[i * 4 + 2] = (float)color.Z; mesh.Colors[i * 4 + 3] = 1;
            }
            mesh.Triangles = new int[segments * segments * 6 + perimeter * 6]; int cursor = 0;
            void Triangle(int a, int b, int c) { mesh.Triangles[cursor++] = a; mesh.Triangles[cursor++] = b; mesh.Triangles[cursor++] = c; }
            for (int z = 0; z < segments; z++) for (int x = 0; x < segments; x++)
            { int a = z * side + x; Triangle(a, a + side, a + 1); Triangle(a + 1, a + side, a + side + 1); }
            // Render-only skirts conceal coarse/fine LOD T-junctions. They are NEVER used by walking queries.
            var edge = new int[perimeter]; int k = 0;
            for (int x = 0; x < segments; x++) edge[k++] = x;
            for (int z = 0; z < segments; z++) edge[k++] = z * side + segments;
            for (int x = segments; x > 0; x--) edge[k++] = segments * side + x;
            for (int z = segments; z > 0; z--) edge[k++] = z * side;
            double skirt = Math.Max(80, (mesh.MaxHeightM - mesh.MinHeightM) * .5);
            for (int i = 0; i < perimeter; i++)
            {
                int src = edge[i], dst = ground + i;
                Array.Copy(mesh.Positions, src * 3, mesh.Positions, dst * 3, 3); mesh.Positions[dst * 3 + 1] -= (float)skirt;
                Array.Copy(mesh.Normals, src * 3, mesh.Normals, dst * 3, 3); Array.Copy(mesh.Colors, src * 4, mesh.Colors, dst * 4, 4);
                int next = (i + 1) % perimeter; Triangle(src, dst, edge[next]); Triangle(edge[next], dst, ground + next);
            }
            mesh.BuildMilliseconds = watch.Elapsed.TotalMilliseconds; return mesh;
        }
    }
}
