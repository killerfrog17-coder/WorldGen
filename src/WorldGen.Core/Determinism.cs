using System;
using System.Collections.Generic;

namespace WorldGen.Core
{
    public static class D
    {
        public const double Pi = 3.14159265358979323846;
        public static double Q(double x, double scale = 1000000000) => Math.Round(x * scale, MidpointRounding.AwayFromZero) / scale;
        public static int M(double x) => (int)Math.Round(x, MidpointRounding.AwayFromZero);
        public static double Clamp(double x, double a = 0, double b = 1) => Math.Max(a, Math.Min(b, x));
        // Fixed iteration scalar math avoids platform transcendental implementations.
        public static double Sqrt(double x)
        {
            if (x <= 0) return 0;
            // An exponent-derived starting value avoids dozens of redundant iterations
            // for the small tangent vectors used by circulation and spherical geometry.
            double y = BitConverter.Int64BitsToDouble((BitConverter.DoubleToInt64Bits(x) >> 1) + 0x1ff8000000000000L);
            for (int i = 0; i < 8; i++) y = .5 * (y + x / y);
            return Q(y);
        }
        public static double Atan(double x)
        {
            if (x < 0) return -Atan(-x);
            if (x > 1) return Pi / 2 - Atan(1 / x);
            if (x > .414213562373095) return Pi / 4 + Atan((x - 1) / (x + 1));
            double t = x, sum = x, xx = x * x;
            for (int i = 1; i < 20; i++) { t *= -xx; sum += t / (2 * i + 1); }
            return Q(sum, 1e12);
        }
        public static double Angle(V a, V b) { var c = V.Cross(a, b).Length; var d = V.Dot(a, b); return d >= 0 ? Atan(c / Math.Max(1e-15, d)) : Pi - Atan(c / -d); }
        public static double Sin(double x)
        {
            x -= Math.Floor((x + Pi) / (2 * Pi)) * 2 * Pi;
            double term = x, sum = x;
            for (int i = 1; i < 12; i++) { term *= -x * x / ((2 * i) * (2 * i + 1)); sum += term; }
            return Q(sum);
        }
        public static double Kernel(double distance, double width) { double x = Clamp(1 - distance / width); return x * x * (3 - 2 * x); }
    }
    [Serializable]
    public struct V
    {
        public double X, Y, Z;
        public V(double x, double y, double z) { X = D.Q(x); Y = D.Q(y); Z = D.Q(z); }
        public double Length => D.Sqrt(X * X + Y * Y + Z * Z);
        public V Unit => this / Math.Max(1e-12, Length);
        public static V operator +(V a, V b) => new V(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V operator -(V a, V b) => new V(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V operator *(V a, double b) => new V(a.X * b, a.Y * b, a.Z * b);
        public static V operator /(V a, double b) => a * (1 / b);
        public static double Dot(V a, V b) => D.Q(a.X * b.X + a.Y * b.Y + a.Z * b.Z);
        public static V Cross(V a, V b) => new V(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static V Tangent(V at, V toward) => (toward - at * Dot(at, toward)).Unit;
        public static V Transport(V direction, V from, V to)
        {
            double denom = 1 + Dot(from, to);
            if (denom < .000001) return Tangent(to, direction);
            return (direction - (from + to) * (Dot(direction, to) / denom)).Unit;
        }
    }
    public struct Rng
    {
        private uint state;
        public Rng(int seed, string stream, int id = 0)
        {
            uint h = 2166136261; foreach (char c in stream) h = unchecked((h ^ c) * 16777619);
            state = Mix(unchecked((uint)seed) ^ h ^ Mix((uint)id)); if (state == 0) state = 1;
        }
        public static uint Mix(uint x) { unchecked { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; return x ^ (x >> 16); } }
        public uint Next() { uint x = state; x ^= x << 13; x ^= x >> 17; x ^= x << 5; return state = x; }
        public double Unit() => (Next() >> 8) / 16777216.0;
        public V Vector() => new V(Unit() * 2 - 1, Unit() * 2 - 1, Unit() * 2 - 1).Unit;
        public static double Value(int seed, string stream, int id) { var r = new Rng(seed, stream, id); return r.Unit(); }
    }
    public sealed class Heap
    {
        public struct Item { public long Key; public int Id, From; public Item(long k, int i, int f) { Key = k; Id = i; From = f; } }
        private readonly List<Item> a = new List<Item>();
        public int Count => a.Count;
        static bool Less(Item x, Item y) => x.Key != y.Key ? x.Key < y.Key : x.Id != y.Id ? x.Id < y.Id : x.From < y.From;
        public void Push(long k, int id, int from = -1)
        { var v = new Item(k, id, from); a.Add(v); int i = a.Count - 1; while (i > 0) { int p = (i - 1) / 2; if (!Less(v, a[p])) break; a[i] = a[p]; i = p; } a[i] = v; }
        public Item Pop()
        { var first = a[0]; var last = a[a.Count - 1]; a.RemoveAt(a.Count - 1); if (a.Count == 0) return first; int i = 0; while (i * 2 + 1 < a.Count) { int c = i * 2 + 1; if (c + 1 < a.Count && Less(a[c + 1], a[c])) c++; if (!Less(a[c], last)) break; a[i] = a[c]; i = c; } a[i] = last; return first; }
    }
}
