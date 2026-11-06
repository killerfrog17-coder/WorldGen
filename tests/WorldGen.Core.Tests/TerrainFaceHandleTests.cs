using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
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

    public static void RootControls(string input, string baseline, bool capture)
    {
        var world = WorldFile.ReadFile(input);
        var terrain = LocalTerrainDerivationCache.Create(world, horizontalScale: .1);
        var controls = (Array)typeof(HyperAmplification).GetField("rootNodes", Hidden)!.GetValue(terrain.Amplification)!;
        var fields = controls.GetType().GetElementType()!.GetFields().Where(f =>
            f.Name is not ("River" or "DetailGradient" or "DetailGradientReady")).OrderBy(f => f.Name).ToArray();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var buffer = new MemoryStream(); using var writer = new BinaryWriter(buffer);
        void Value(object value)
        {
            switch(value)
            {
                case double x: writer.Write(x); break;
                case float x: writer.Write(x); break;
                case ulong x: writer.Write(x); break;
                case Enum x: writer.Write(Convert.ToInt32(x)); break;
                default:
                    foreach(var field in value.GetType().GetFields().OrderBy(f => f.Name)) Value(field.GetValue(value)!);
                    break;
            }
        }
        writer.Write(string.Join(",", fields.Select(f => f.Name)));
        hash.AppendData(buffer.GetBuffer(), 0, (int)buffer.Length);
        foreach(var node in controls)
        {
            buffer.SetLength(0);
            foreach(var field in fields) Value(field.GetValue(node)!);
            hash.AppendData(buffer.GetBuffer(), 0, (int)buffer.Length);
        }
        string actual = Convert.ToHexString(hash.GetHashAndReset());
        if(capture) File.WriteAllText(baseline, actual);
        else if(File.ReadAllText(baseline) != actual) throw new Exception("Coarse terrain/lake/river control values changed");
        Console.WriteLine($"PASS {controls.Length} complete root control records: {actual}");
        var cacheField = typeof(HyperAmplification).GetField("rootVertices", Hidden);
        if(cacheField != null)
        {
            var cache = (Array)cacheField.GetValue(terrain.Amplification)!;
            int realized = cache.Cast<object>().Count(v => v != null);
            if(realized >= controls.Length / 10) throw new Exception("Cached-world preparation eagerly materialized global terrain vertices");
            var rootVertex = typeof(HyperAmplification).GetMethod("RootVertex", Hidden)!;
            object gate = typeof(HyperAmplification).GetField("gate", Hidden)!.GetValue(terrain.Amplification)!;
            int checkedVertices = 0;
            for(int id = 0; id < controls.Length; id += 997)
            {
                object vertex;
                lock(gate)
                {
                    vertex = rootVertex.Invoke(terrain.Amplification, new object[]{id})!;
                    if(!ReferenceEquals(vertex, rootVertex.Invoke(terrain.Amplification, new object[]{id})))
                        throw new Exception("Adjacent topology would receive different root vertex identities");
                }
                foreach(var field in fields)
                    if(!Equals(field.GetValue(controls.GetValue(id)), vertex.GetType().GetField(field.Name)!.GetValue(vertex)))
                        throw new Exception("Root materialization omitted or changed " + field.Name);
                checkedVertices++;
            }
            Console.WriteLine($"PASS lazy root vertices: {realized}/{controls.Length} initially realized, {checkedVertices} complete field/identity checks.");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static object[] References()
    {
        var face = Family().GetValue(3)!;
        return Enumerable.Range(0, 3).Select(_ => Activator.CreateInstance(FaceRef, face)!).ToArray();
    }

    static Array Family()
    {
        var split=typeof(HyperAmplification).GetNestedType("FaceSplit",BindingFlags.NonPublic)!;
        var family=Activator.CreateInstance(split)!;var faces=Array.CreateInstance(Face,4);
        split.GetField("Faces")!.SetValue(family,faces);
        for(int i=0;i<4;i++)
        {
            var face=Activator.CreateInstance(Face)!;faces.SetValue(face,i);
            Face.GetField("Root")!.SetValue(face,12);Face.GetField("Depth")!.SetValue(face,2);
            Face.GetField("Path")!.SetValue(face,(ulong)(4+i));Face.GetField("Lease")!.SetValue(face,family);
        }
        return faces;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static object[] FamilyReferences()
    {
        var faces=Family();var refs=new object[12];
        for(int i=0;i<refs.Length;i++)refs[i]=Activator.CreateInstance(FaceRef,faces.GetValue(i/3)!)!;
        if(refs.Any(r=>!ReferenceEquals(Target.GetValue(r),Target.GetValue(refs[0]))))
            throw new Exception("Sibling faces allocated separate weak lifetime handles");
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        for(int i=0;i<refs.Length;i++)
            if(!ReferenceEquals(faces.GetValue(i/3),FaceRef.GetMethod("Resolve")!.Invoke(refs[i],new object?[]{null})))
                throw new Exception("Shared-family address resolved the wrong sibling or lost a live family");
        GC.KeepAlive(faces);return refs;
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
        var replacement = Family().GetValue(3)!;
        FaceRef.GetMethod("Refresh")!.Invoke(refs[0], new[] { replacement });
        FaceRef.GetMethod("Refresh")!.Invoke(refs[1], new[] { replacement });
        if (!Alive(refs[0]) || !Alive(refs[1]) || Alive(refs[2]) ||
            !ReferenceEquals(Target.GetValue(refs[0]), Target.GetValue(refs[1])))
            throw new Exception("Refreshing an expired address mutated a shared old handle");
        GC.KeepAlive(replacement);
        var shared=SharedAddresses();
        if(!ReferenceEquals(shared[0],shared[1])||!ReferenceEquals(shared[0],shared[2]))
            throw new Exception("Incident edges allocated separate copies of the same face address");
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        if(shared.Any(Alive))throw new Exception("Shared topology address retained its face");
        var siblings=FamilyReferences();
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        if(siblings.Any(Alive))throw new Exception("Sibling addresses retained an otherwise unreachable family");
        Console.WriteLine("PASS shared family handles: twelve incident addresses share one weak handle, exact sibling resolution, no strong lifetime retention, safe refresh of expired addresses.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static object[] SharedAddresses()
    {
        var face=Family().GetValue(3)!;
        var edgeType=typeof(HyperAmplification).GetNestedType("Edge",BindingFlags.NonPublic)!;
        var result=new object[3];
        for(int i=0;i<3;i++)
        {
            var edge=Activator.CreateInstance(edgeType)!;
            edgeType.GetMethod("Attach")!.Invoke(edge,new[]{face});
            result[i]=edgeType.GetField("Left")!.GetValue(edge)!;
            // A repeated attachment follows the Refresh path and must retain
            // both the shared address and the original topology identity.
            edgeType.GetMethod("Attach")!.Invoke(edge,new[]{face});
            if(!ReferenceEquals(result[i],edgeType.GetField("Left")!.GetValue(edge)))throw new Exception("Repeated attachment replaced its address");
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void CheckLiveFamilies(HyperAmplification owner)
    {
        var retained=(Queue<object>)typeof(HyperAmplification).GetField("retained",Hidden)!.GetValue(owner)!;
        int groups=0,addresses=0;
        foreach(var family in retained)
        {
            if(family.GetType().Name!="FaceSplit")continue;
            var handle=family.GetType().GetField("Reference")!.GetValue(family);
            var parent=family.GetType().GetField("Parent")!.GetValue(family)!;
            if(!ReferenceEquals(handle,Face.GetField("Children")!.GetValue(parent)))
                throw new Exception("Live parent and family do not share their weak handle");
            foreach(var face in (Array)family.GetType().GetField("Faces")!.GetValue(family)!)
            {
                var address=Face.GetProperty("Address")!.GetValue(face)!;
                if(!ReferenceEquals(handle,Target.GetValue(address))||
                    !ReferenceEquals(face,FaceRef.GetMethod("Resolve")!.Invoke(address,new object[]{owner})))
                    throw new Exception("Actual terrain family failed handle sharing or sibling identity");
                addresses++;
            }
            groups++;
        }
        if(groups==0)throw new Exception("Actual terrain handle check visited no families");
        Console.WriteLine($"PASS actual terrain: {addresses} face addresses + {groups} parent lookups share {groups} weak handles.");
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
        CheckLiveFamilies(terrain.Amplification!);
        ((Queue<object>)typeof(HyperAmplification).GetField("retained", Hidden)!.GetValue(terrain.Amplification)!).Clear();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var rebuilt = Samples(true);
        if (!actual.SequenceEqual(rebuilt)) throw new Exception("Terrain differs after cache retirement and reverse-order reconstruction");
        var concurrent=new string[probes.Count];
        Parallel.For(0,probes.Count,new ParallelOptions{MaxDegreeOfParallelism=4},i=>
        {
            var probe=probes[i];var patch=terrain.Patch(world.Surface.Cells[probe.cell].Position,20480);
            concurrent[i]=JsonSerializer.Serialize(patch.Sample(probe.point),Json);
        });
        if(!actual.SequenceEqual(concurrent))throw new Exception("Concurrent neighbour resolution changed terrain after cache reconstruction");
        Console.WriteLine($"PASS {actual.Length} complete terrain samples across {selected.Count} biome/coastal sites, baseline {(capture ? "captured" : "identical")}; all repeated after cache retirement/reconstruction.");
    }
}
