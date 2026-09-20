using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace WorldGen.Core
{
    public static class StateHash
    {
        static readonly ConcurrentDictionary<Type, string> TypeNames = new ConcurrentDictionary<Type, string>();
        static readonly ConcurrentDictionary<Type, FieldInfo[]> Fields = new ConcurrentDictionary<Type, FieldInfo[]>();
        static StateHash()
        {
            CheckRecord(typeof(Cell), "AreaM2:System.Int64", "DistanceM:System.Int32[]", "Id:System.Int32", "Neighbors:System.Int32[]", "Position:WorldGen.Core.V");
            CheckRecord(typeof(Edge), "A:System.Int32", "B:System.Int32", "Id:System.Int32", "LengthM:System.Int32", "TriangleA:System.Int32", "TriangleB:System.Int32");
            CheckRecord(typeof(V), "X:System.Double", "Y:System.Double", "Z:System.Double");
        }
        static void CheckRecord(Type type, params string[] expected)
        {
            var actual = type.GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => f.Name + ":" + f.FieldType.FullName);
            if (!actual.SequenceEqual(expected)) throw new InvalidOperationException("Update the canonical hash writer for " + type.Name + " before changing its record layout.");
        }
        public static string Compute(object value, CancellationToken cancellation = default)
        {
            using (var sha = SHA256.Create())
            using (var crypto = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
            using (var buffer = new BufferedStream(new CancellationStream(crypto, cancellation), 65536))
            {
                using (var writer = new BinaryWriter(buffer, Encoding.UTF8, true)) Write(writer, value);
                buffer.Flush(); crypto.FlushFinalBlock();
                return string.Concat(sha.Hash.Select(b => b.ToString("x2")));
            }
        }
        sealed class CancellationStream : Stream
        {
            readonly Stream target; readonly CancellationToken token;
            public CancellationStream(Stream target, CancellationToken token) { this.target = target; this.token = token; }
            public override void Write(byte[] bytes, int offset, int count) { token.ThrowIfCancellationRequested(); target.Write(bytes, offset, count); }
            public override void Flush() => target.Flush();
            public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override int Read(byte[] b, int o, int c) => throw new NotSupportedException();
            public override long Seek(long o, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
        static void Write(BinaryWriter w, object o)
        {
            if (o == null) { w.Write((byte)0); return; }
            w.Write((byte)1);
            // Preserve the exact canonical byte format while avoiding type lookup
            // and reflection on the millions of scalar values hashed in WASM.
            if (o is string text) { w.Write("System.String"); w.Write(text); return; }
            if (o is int i) { w.Write("System.Int32"); w.Write(i); return; }
            if (o is long l) { w.Write("System.Int64"); w.Write(l); return; }
            if (o is bool b) { w.Write("System.Boolean"); w.Write(b); return; }
            if (o is double d) { w.Write("System.Double"); Double(w, d); return; }
            if (o is V vector) { VectorBody(w, vector); return; }
            var t = o.GetType(); w.Write(TypeName(t));
            // Mesh records dominate large snapshots. Preserve the canonical bytes
            // while avoiding reflection and boxing for every cell, edge and corner.
            if (t == typeof(Cell[])) { foreach (var item in (Cell[])o) { w.Write((byte)1); CellRecord(w, item); } w.Write((byte)0); return; }
            if (t == typeof(Edge[])) { foreach (var item in (Edge[])o) { w.Write((byte)1); EdgeRecord(w, item); } w.Write((byte)0); return; }
            if (t == typeof(V[])) { foreach (var item in (V[])o) { w.Write((byte)1); w.Write((byte)1); VectorBody(w, item); } w.Write((byte)0); return; }
            // CLR enum arrays can satisfy an underlying integer-array cast; exact
            // type checks keep the enum's distinct type name in the hash.
            if (t == typeof(int[])) { foreach (int item in (int[])o) { w.Write((byte)1); w.Write((byte)1); w.Write("System.Int32"); w.Write(item); } w.Write((byte)0); return; }
            if (t == typeof(long[])) { foreach (long item in (long[])o) { w.Write((byte)1); w.Write((byte)1); w.Write("System.Int64"); w.Write(item); } w.Write((byte)0); return; }
            if (t == typeof(bool[])) { foreach (bool item in (bool[])o) { w.Write((byte)1); w.Write((byte)1); w.Write("System.Boolean"); w.Write(item); } w.Write((byte)0); return; }
            if (t == typeof(double[])) { foreach (double item in (double[])o) { w.Write((byte)1); w.Write((byte)1); w.Write("System.Double"); Double(w, item); } w.Write((byte)0); return; }
            if (t.IsEnum) { w.Write(Convert.ToInt32(o)); return; }
            if (o is IEnumerable seq) { foreach (var item in seq) { w.Write((byte)1); Write(w, item); } w.Write((byte)0); return; }
            foreach (var f in Fields.GetOrAdd(t, type => type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(f => !(type == typeof(World) && f.Name == nameof(World.Stages)) && f.GetCustomAttribute<DisplaySettingAttribute>() == null)
                .OrderBy(f => f.Name, StringComparer.Ordinal).ToArray()))
            { w.Write(f.Name); Write(w, f.GetValue(o)); }
        }
        static void Integer(BinaryWriter writer, int value) { writer.Write((byte)1); writer.Write("System.Int32"); writer.Write(value); }
        static void Real(BinaryWriter writer, double value) { writer.Write((byte)1); writer.Write("System.Double"); Double(writer, value); }
        static void VectorBody(BinaryWriter writer, V value)
        {
            writer.Write("WorldGen.Core.V"); writer.Write("X"); Real(writer, value.X); writer.Write("Y"); Real(writer, value.Y); writer.Write("Z"); Real(writer, value.Z);
        }
        static void CellRecord(BinaryWriter writer, Cell cell)
        {
            if (cell == null) { writer.Write((byte)0); return; }
            writer.Write((byte)1); writer.Write("WorldGen.Core.Cell");
            writer.Write("AreaM2"); writer.Write((byte)1); writer.Write("System.Int64"); writer.Write(cell.AreaM2);
            writer.Write("DistanceM"); Write(writer, cell.DistanceM); writer.Write("Id"); Integer(writer, cell.Id);
            writer.Write("Neighbors"); Write(writer, cell.Neighbors); writer.Write("Position"); writer.Write((byte)1); VectorBody(writer, cell.Position);
        }
        static void EdgeRecord(BinaryWriter writer, Edge edge)
        {
            if (edge == null) { writer.Write((byte)0); return; }
            writer.Write((byte)1); writer.Write("WorldGen.Core.Edge");
            writer.Write("A"); Integer(writer, edge.A); writer.Write("B"); Integer(writer, edge.B); writer.Write("Id"); Integer(writer, edge.Id);
            writer.Write("LengthM"); Integer(writer, edge.LengthM); writer.Write("TriangleA"); Integer(writer, edge.TriangleA); writer.Write("TriangleB"); Integer(writer, edge.TriangleB);
        }
        static void Double(BinaryWriter writer, double value) { double quantized = D.Q(value, 1e6); writer.Write(quantized == 0 ? 0.0 : quantized); }
        static string TypeName(Type t) => TypeNames.GetOrAdd(t, type => type.IsGenericType ? (type.GetGenericTypeDefinition().FullName ?? type.Name).Split('`')[0] + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">" : type.FullName ?? type.Name);
    }
    public sealed class WorldPipeline
    {
        public World Generate(Recipe recipe, Action<GenerationProgress> progress = null, CancellationToken cancellation = default)
        {
            var r = recipe.Clone(); r.Validate(); var w = new World { Recipe = r };
            string previous = StateHash.Compute(r);
            void Stage(string name, int percent, Action action, Func<object> output)
            {
                cancellation.ThrowIfCancellationRequested(); progress?.Invoke(new GenerationProgress(name, percent)); action();
                cancellation.ThrowIfCancellationRequested();
                string hash = StateHash.Compute(new object[] { previous, name, output() }, cancellation); previous = hash;
                w.Stages.Add(new StageRecord { Name = name, Hash = hash, Entities = w.Surface.Cells.Length });
            }
            Stage("coarse-plates", 0, () =>
            {
                var coarseRecipe = r.Clone(); coarseRecipe.Detail = SphericalMesh.CanonicalCellCount; coarseRecipe.Irregularity = .75;
                var coarse = new World { Recipe = coarseRecipe, Surface = SphericalMesh.BuildCoarse(r, cancellation) };
                PlateHierarchy.Microplates(coarse, cancellation); Continents.Build(coarse, cancellation);
                w.Metrics.AddRange(coarse.Metrics);
                w.CoarseSurface = coarse.Surface; w.CoarsePlate = coarse.CellMicroplate; w.CoarseContinent = coarse.Continent;
                w.CoarseHash = StateHash.Compute(new object[] { coarse.Surface, coarse.CellMicroplate, coarse.Continent, coarse.Microplates }, cancellation);
                w.Microplates = coarse.Microplates;
            }, () => w.CoarseHash);
            Stage("geometry", 8, () => w.Surface = SphericalMesh.Build(r, cancellation), () => w.Surface);
            Stage("plate-projection", 18, () => PlateProjection.Apply(w, cancellation), () => new object[] { w.CellMicroplate, w.Continent, w.Microplates });
            Stage("plate-physics", 26, () => PlatePhysics.Apply(w, cancellation), () => new object[] { w.Microplates, w.MantlePermille });
            Stage("superplates", 32, () => PlateHierarchy.Parents(w, cancellation), () => new object[] { w.Parents, w.Microplates });
            Stage("boundaries", 38, () => Tectonics.Build(w, cancellation), () => new object[] { w.Boundaries, w.Stress, w.BoundaryDistanceM });
            Stage("terrain", 45, () => Terrain.Build(w, cancellation), () => new object[] { w.TectonicM, w.Features, w.CoastDistanceM, w.FoldPermille, w.CratonPermille, w.BasinPermille, w.PlateauPermille });
            Stage("erosion", 65, () => Erosion.Apply(w, cancellation), () => new object[] { w.ElevationM, w.PreErosionM, w.Landmass, w.Hydrology.OceanConnected });
            Stage("climate", 80, () => Climate.Build(w, cancellation), () => w.Climate);
            progress?.Invoke(new GenerationProgress("finalizing", 94));
            long total = w.Surface.Cells.Sum(c => c.AreaM2);
            w.Metric("coarse_cells", w.CoarseSurface.Cells.Length); w.Metric("terrain_cells", w.Surface.Cells.Length);
            w.Metric("land_fraction", w.Surface.Cells.Where(c => !w.Hydrology.OceanConnected[c.Id]).Sum(c => c.AreaM2) / (double)total);
            long continentalArea = w.Surface.Cells.Where(c => w.Continent[c.Id] >= 0).Sum(c => c.AreaM2);
            long continentalBelowZero = w.Surface.Cells.Where(c => w.Continent[c.Id] >= 0 && w.ElevationM[c.Id] < 0).Sum(c => c.AreaM2);
            long inlandBelowZero = w.Surface.Cells.Where(c => w.Continent[c.Id] >= 0 && w.ElevationM[c.Id] < 0 && !w.Hydrology.OceanConnected[c.Id]).Sum(c => c.AreaM2);
            w.Metric("continental_below_zero_fraction", continentalBelowZero / (double)continentalArea);
            w.Metric("inland_below_zero_fraction", inlandBelowZero / (double)continentalArea);
            w.Metric("landmass_count", w.Landmass.Where(i => i >= 0).Distinct().Count());
            w.Metric("min_elevation", w.ElevationM.Min(), "m"); w.Metric("max_elevation", w.ElevationM.Max(), "m");
            w.Stages.Add(new StageRecord { Name = "complete", Hash = StateHash.Compute(w, cancellation), Entities = w.Surface.Cells.Length });
            progress?.Invoke(new GenerationProgress("complete", 100)); return w;
        }
        public World Reapply(Recipe recipe, Action<GenerationProgress> progress = null, CancellationToken cancellation = default) => Generate(recipe, progress, cancellation);
        public World Reconstruct(World snapshot, Action<GenerationProgress> progress = null, CancellationToken cancellation = default) => Generate(snapshot.Recipe, progress, cancellation);
    }
}
