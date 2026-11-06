using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using WorldGen.Core;
using WorldGen.Serialization;

static class HabitatStorageTests
{
    public static void World(string input,string baseline,bool capture)
    {
        var world=WorldFile.ReadFile(input);
        var hashes=new List<string>{StateHash.Compute(world),StateHash.Compute(world.Ecology.Habitat)};
        var projection=ForagerHabitat.For(world);
        var locations=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>world.Prehistory.Foragers.CellDistrict[i]>=0).ToArray();
        var dated=new List<ForagerHabitat.Local>();
        for(int i=0;i<locations.Length;i+=Math.Max(1,locations.Length/64))dated.Add(projection.Cell(locations[i]));
        hashes.Add(StateHash.Compute(dated.ToArray()));
        if(!capture)
        {
            if(RuntimeHelpers.IsReferenceOrContainsReferences<HabitatCell>())throw new Exception("Habitat records still require reference tracing");
            var sample=world.Ecology.Habitat.Where((_,i)=>i%389==0).ToArray();
            string expected=StateHash.Compute(sample);
            if(StateHash.Compute(WorldJson.Read<HabitatCell[]>(WorldJson.Write(sample)))!=expected)throw new Exception("Current habitat JSON roundtrip changed values");
            using var wire=new MemoryStream();
            using(var writer=new BinaryWriter(wire,Encoding.UTF8,true))RegionalWire.Write(writer,sample);
            wire.Position=0;
            using(var reader=new BinaryReader(wire,Encoding.UTF8,true))
                if(StateHash.Compute(RegionalWire.Read<HabitatCell[]>(reader))!=expected)throw new Exception("Current habitat worker transport changed values");
            var packed=WorldFile.Write(world,compression:CompressionLevel.Fastest);var reloaded=WorldFile.Read(packed);
            if(StateHash.Compute(reloaded)!=hashes[0]||reloaded.Prehistory.ComputeHash()!=world.Prehistory.ComputeHash())throw new Exception("Current world save/reload changed planet or history");
            const int count=200000;long before=GC.GetAllocatedBytesForCurrentThread();var storage=new HabitatCell[count];
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            GC.KeepAlive(storage);
            if(bytes>count*(long)Marshal.SizeOf<HabitatCell>()+256)throw new Exception("Habitat array allocates per-cell objects");
            Console.WriteLine($"PASS habitat current JSON/worker/full world roundtrips; {count} records allocate {bytes} bytes, {packed.Length} save bytes.");
        }
        Ecology.BuildHabitats(world,default);
        hashes.Add(StateHash.Compute(world.Ecology.Habitat));
        if(capture)File.WriteAllLines(baseline,hashes);
        else if(!hashes.SequenceEqual(File.ReadAllLines(baseline)))throw new Exception("Planet, loaded/regenerated ecology, or dated habitat differs from pre-change values");
        Console.WriteLine($"PASS {world.Surface.Cells.Length} habitat records, complete planet hash, regenerated habitats and {dated.Count} dated projections; baseline {(capture?"captured":"identical")}.");
    }
}
