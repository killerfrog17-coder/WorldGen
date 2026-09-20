using System;

namespace WorldGen.Core
{
    /// <summary>Independently implemented 3D simplex gradient noise, sampled in sphere coordinates.</summary>
    public static class SphereNoise
    {
        public static double Sample(V p, int seed, double frequency)
        {
            double x = p.X * frequency, y = p.Y * frequency, z = p.Z * frequency;
            double skew = (x + y + z) / 3;
            int i = (int)Math.Floor(x + skew), j = (int)Math.Floor(y + skew), k = (int)Math.Floor(z + skew);
            double unskew = (i + j + k) / 6.0;
            double a = x - i + unskew, b = y - j + unskew, c = z - k + unskew;
            // Stable axis ranking defines the two intermediate tetrahedral corners, including ties.
            int rx = (a >= b ? 1 : 0) + (a >= c ? 1 : 0);
            int ry = (b > a ? 1 : 0) + (b >= c ? 1 : 0);
            int rz = (c > a ? 1 : 0) + (c > b ? 1 : 0);
            int x1 = rx == 2 ? 1 : 0, y1 = ry == 2 ? 1 : 0, z1 = rz == 2 ? 1 : 0;
            int x2 = rx >= 1 ? 1 : 0, y2 = ry >= 1 ? 1 : 0, z2 = rz >= 1 ? 1 : 0;
            double sum = Corner(i, j, k, a, b, c, seed)
                + Corner(i + x1, j + y1, k + z1, a - x1 + 1 / 6.0, b - y1 + 1 / 6.0, c - z1 + 1 / 6.0, seed)
                + Corner(i + x2, j + y2, k + z2, a - x2 + 1 / 3.0, b - y2 + 1 / 3.0, c - z2 + 1 / 3.0, seed)
                + Corner(i + 1, j + 1, k + 1, a - .5, b - .5, c - .5, seed);
            return D.Q(D.Clamp(sum * 32, -1, 1), 1e6);
        }
        static double Corner(int i, int j, int k, double x, double y, double z, int seed)
        {
            double falloff = .6 - x * x - y * y - z * z; if (falloff <= 0) return 0;
            uint hash = Rng.Mix(unchecked((uint)seed ^ (uint)i * 0x8da6b343u ^ (uint)j * 0xd8163841u ^ (uint)k * 0xcb1ab31fu));
            int gradient = (int)(hash % 12), plane = gradient / 4;
            double first = plane == 2 ? y : x, second = plane == 0 ? y : z;
            double dot = ((gradient & 1) == 0 ? first : -first) + ((gradient & 2) == 0 ? second : -second);
            falloff *= falloff; return falloff * falloff * dot;
        }
    }
}
