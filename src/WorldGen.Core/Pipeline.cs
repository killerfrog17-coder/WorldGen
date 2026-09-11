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
        public static string Compute(object value)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) Write(writer, value);
                using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(stream.ToArray()).Select(b => b.ToString("x2")));
            }
        }
        static void Write(BinaryWriter w, object o)
        {
            if (o == null) { w.Write((byte)0); return; }
            w.Write((byte)1); var t = o.GetType(); w.Write(TypeName(t));
            if (o is string text) { w.Write(text); return; }
            if (o is int i) { w.Write(i); return; }
            if (o is long l) { w.Write(l); return; }
            if (o is bool b) { w.Write(b); return; }
            if (o is double d) { w.Write(D.Q(d, 1e6)); return; }
            if (t.IsEnum) { w.Write(Convert.ToInt32(o)); return; }
            if (o is IEnumerable seq) { foreach (var item in seq) { w.Write((byte)1); Write(w, item); } w.Write((byte)0); return; }
            foreach (var f in Fields.GetOrAdd(t, type => type.GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray()))
            { if (t == typeof(World) && f.Name == nameof(World.Stages)) continue; w.Write(f.Name); Write(w, f.GetValue(o)); }
        }
        static string TypeName(Type t) => TypeNames.GetOrAdd(t, type => type.IsGenericType ? (type.GetGenericTypeDefinition().FullName ?? type.Name).Split('`')[0] + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">" : type.FullName ?? type.Name);
    }
    public sealed class WorldPipeline
    {
        public World Generate(Recipe recipe, Action<GenerationProgress> progress = null, CancellationToken cancellation = default)
        {
            var r = recipe.Clone(); r.Validate(); var w = new World { Recipe = r };
            void Stage(string name, int percent, Action action)
            { cancellation.ThrowIfCancellationRequested(); progress?.Invoke(new GenerationProgress(name, percent)); action(); cancellation.ThrowIfCancellationRequested(); w.Stages.Add(new StageRecord { Name = name, Hash = StateHash.Compute(w), Entities = w.Surface.Cells.Length }); }
            Stage("geometry", 0, () => { w.Surface = SphericalMesh.Build(r, cancellation); if (w.Surface.Cells.Length != r.MacroCellCount) w.Warnings.Add("Canonical resolution raised from " + r.MacroCellCount + " to " + w.Surface.Cells.Length + " cells (closed geodesic mesh; at least 16 cells per microplate on average)."); });
            Stage("microplates", 10, () => PlateHierarchy.Microplates(w, cancellation));
            Stage("parents", 20, () => PlateHierarchy.Parents(w, cancellation));
            Stage("continents", 32, () => Continents.Build(w, cancellation));
            Stage("boundaries", 40, () => Tectonics.Build(w, cancellation));
            Stage("terrain", 48, () => Terrain.Build(w, cancellation));
            Stage("erosion", 72, () => Erosion.Apply(w, cancellation));
            Stage("hydrology", 82, () => w.Hydrology = Hydrology.Solve(w.Surface, w.ElevationM, r, w.ProtectedInlandSea, w.Features, cancellation));
            Stage("complete", 96, () =>
            {
                long total = w.Surface.Cells.Sum(c => c.AreaM2); w.Metric("canonical_cells", w.Surface.Cells.Length); w.Metric("land_fraction", w.Surface.Cells.Where(c => w.ElevationM[c.Id] >= 0).Sum(c => c.AreaM2) / (double)total);
                w.Metric("landmass_count", w.Landmass.Where(i => i >= 0).Distinct().Count()); w.Metric("min_elevation", w.ElevationM.Min(), "m"); w.Metric("max_elevation", w.ElevationM.Max(), "m"); w.Metric("lake_count", w.Hydrology.Waters.Count(b => b.Kind != WaterKind.Ocean)); w.Metric("inland_sea_count", w.Hydrology.Waters.Count(b => b.Kind == WaterKind.TectonicInlandSea)); w.Metric("river_reaches", w.Hydrology.Rivers.Length);
                var bridges = w.Features.Where(f => f.Kind == FeatureKind.LandBridge).SelectMany(f => f.Cells).Distinct().ToArray(); w.Metric("isthmus_cells", bridges.Count(i => w.ElevationM[i] >= 0)); w.Metric("near_strait_cells", bridges.Count(i => w.ElevationM[i] < 0));
                foreach (int level in new[] { -120, -250, -500 }) w.Metric("landmasses_at_" + level + "m", Graph.Components(w.Surface, i => w.ElevationM[i] >= level).Where(i => i >= 0).Distinct().Count());
            });
            progress?.Invoke(new GenerationProgress("complete", 100)); return w;
        }
        public World Reapply(Recipe recipe, Action<GenerationProgress> progress = null, CancellationToken cancellation = default) => Generate(recipe, progress, cancellation);
        public World Reconstruct(World snapshot, Action<GenerationProgress> progress = null, CancellationToken cancellation = default) => Generate(snapshot.Recipe, progress, cancellation);
    }
}
