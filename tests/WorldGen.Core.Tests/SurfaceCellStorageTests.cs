using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

static class SurfaceCellStorageTests
{
    public static void Run(string input,string baseline,bool capture)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var world=WorldFile.ReadFile(input);
        var locator=new CellLocator(world.Surface.Cells);
        var queries=new List<int>();
        for(int i=0;i<1000;i++)
        {
            var p=new Rng(317,"cell-storage-query",i).Vector().Unit;
            queries.Add(locator.Nearest(p));queries.AddRange(locator.Within(p,.0001));queries.Add(-1);
        }
        var mesh=SphericalMesh.Build(new Recipe{Detail=4096,Seed=419},CancellationToken.None);
        var hashes=new[]{StateHash.Compute(world.Surface.Cells),StateHash.Compute(queries.ToArray()),StateHash.Compute(mesh)};
        if(capture){if(File.Exists(baseline))throw new IOException("Baseline already exists");File.WriteAllText(baseline,JsonSerializer.Serialize(hashes));}
        else Check(JsonSerializer.Deserialize<string[]>(File.ReadAllText(baseline))!.SequenceEqual(hashes),"Complete real-world cells, spatial queries and regenerated topology retain their literal baseline values");
        var sample=world.Surface.Cells.Take(257).ToArray();string expected=StateHash.Compute(sample);
        Check(StateHash.Compute(WorldJson.Read<Cell[]>(WorldJson.Write(sample)))==expected,"Current cell JSON round trip preserves every field");
        using(var wire=new MemoryStream())
        {
            using(var writer=new BinaryWriter(wire,System.Text.Encoding.UTF8,true))RegionalWire.Write(writer,sample);
            wire.Position=0;using var reader=new BinaryReader(wire);
            Check(StateHash.Compute(RegionalWire.Read<Cell[]>(reader))==expected,"Current worker wire preserves every cell field");
        }
        Check(StateHash.Compute(WorldFile.Read(WorldFile.Write(world)).Surface)==StateHash.Compute(world.Surface),"Current world save/reload preserves cells and topology");
        const int count=200000;long start=GC.GetAllocatedBytesForCurrentThread();
        var cells=new Cell[count];for(int i=0;i<count;i++)cells[i]=new Cell{Id=i,Position=new V(1,0,0),AreaM2=17};
        long bytes=GC.GetAllocatedBytesForCurrentThread()-start;GC.KeepAlive(cells);
        if(!capture){Check(typeof(Cell).IsValueType,"Cell records must be stored inline");Check(bytes<count*64L+256,"Cell storage must avoid a separate managed object per record");}
        Console.WriteLine($"PASS {checks} cell storage checks; actual cells={world.Surface.Cells.Length}, query entries={queries.Count}, 200k records allocate={bytes}. Not native frame-time evidence.");
    }
}
