using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class TerrainFaceHandleTests
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Type Face = typeof(HyperAmplification).GetNestedType("Face", BindingFlags.NonPublic)!;
    static readonly Type FaceRef = typeof(HyperAmplification).GetNestedType("FaceRef", BindingFlags.NonPublic)!;
    static readonly FieldInfo Target = FaceRef.GetField("target", Hidden)!;
    static readonly JsonSerializerOptions Json = new() { IncludeFields = true, IgnoreReadOnlyProperties = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };

    [MethodImpl(MethodImplOptions.NoInlining)]
    static object[] References()
    {
        var face = Activator.CreateInstance(Face)!;
        Face.GetField("Root")!.SetValue(face, 12);
        Face.GetField("Depth")!.SetValue(face, 2);
        Face.GetField("Path")!.SetValue(face, 7UL);
        return Enumerable.Range(0, 3).Select(_ => Activator.CreateInstance(FaceRef, face)!).ToArray();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool Alive(object reference)
    {
        var weak = Target.GetValue(reference)!;
        return (bool)weak.GetType().GetMethod("TryGetTarget")!.Invoke(weak, new object[] { null })!;
    }

    public static void Handles()
    {
        var refs = References();
        if (!ReferenceEquals(Target.GetValue(refs[0]), Target.GetValue(refs[1])) ||
            !ReferenceEquals(Target.GetValue(refs[0]), Target.GetValue(refs[2])))
            throw new Exception("Three references to one face created distinct weak handles");
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        if (refs.Any(Alive)) throw new Exception("Shared handle retained an otherwise unreachable face");
        var replacement = Activator.CreateInstance(Face)!;
        Face.GetField("Root")!.SetValue(replacement, 12);
        Face.GetField("Depth")!.SetValue(replacement, 2);
        Face.GetField("Path")!.SetValue(replacement, 7UL);
        FaceRef.GetMethod("Refresh")!.Invoke(refs[0], new[] { replacement });
        FaceRef.GetMethod("Refresh")!.Invoke(refs[1], new[] { replacement });
        if (!Alive(refs[0]) || !Alive(refs[1]) || Alive(refs[2]) ||
            !ReferenceEquals(Target.GetValue(refs[0]), Target.GetValue(refs[1])))
            throw new Exception("Refreshing an expired address mutated a shared old handle");
        GC.KeepAlive(replacement);
        Console.WriteLine("PASS shared face handles: one per live face, no strong lifetime retention, safe refresh of expired addresses.");
    }

    public static void World(string input, string baseline, bool capture)
    {
        var world = WorldFile.ReadFile(input);
        var terrain = LocalTerrainDerivationCache.Create(world, horizontalScale: .1);
        var selected = new HashSet<int> { 108692, 145551, 130822 };
        foreach (var group in Enumerable.Range(0, world.Surface.Cells.Length).GroupBy(i => terrain.Profile(i).Biome))
            selected.Add(group.First());
        foreach (var cell in world.Surface.Cells.Where(c => terrain.Profile(c.Id).Biome != Biome.Ocean &&
            c.Neighbors.Any(n => terrain.Profile(n).Biome == Biome.Ocean)).Take(8)) selected.Add(cell.Id);
        var probes = new List<(int cell, V point)>();
        foreach (int cell in selected.Order())
        {
            var patch = terrain.Patch(world.Surface.Cells[cell].Position, 20480);
            foreach (double x in new[] { -18000d, -7000, 0, 7000, 18000 })
                foreach (double z in new[] { -18000d, -7000, 0, 7000, 18000 }) probes.Add((cell, patch.Position(x, z)));
        }
        string[] Samples(bool reversed)
        {
            var result = new string[probes.Count];
            foreach (int i in reversed ? Enumerable.Range(0, probes.Count).Reverse() : Enumerable.Range(0, probes.Count))
            {
                var probe = probes[i];
                var patch = terrain.Patch(world.Surface.Cells[probe.cell].Position, 20480);
                result[i] = JsonSerializer.Serialize(patch.Sample(probe.point), Json);
            }
            return result;
        }
        var actual = Samples(false);
        if (capture) File.WriteAllLines(baseline, actual);
        else
        {
            var expected = File.ReadAllLines(baseline);
            if (!actual.SequenceEqual(expected)) throw new Exception("Current terrain differs from pre-change complete samples");
        }
        ((Queue<object>)typeof(HyperAmplification).GetField("retained", Hidden)!.GetValue(terrain.Amplification)!).Clear();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var rebuilt = Samples(true);
        if (!actual.SequenceEqual(rebuilt)) throw new Exception("Terrain differs after cache retirement and reverse-order reconstruction");
        Console.WriteLine($"PASS {actual.Length} complete terrain samples across {selected.Count} biome/coastal sites, baseline {(capture ? "captured" : "identical")}; all repeated after cache retirement/reconstruction.");
    }
}
