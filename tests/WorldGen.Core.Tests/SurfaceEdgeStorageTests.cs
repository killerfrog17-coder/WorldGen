using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using WorldGen.Core;
using WorldGen.Serialization;

public static class SurfaceEdgeStorageTests
{
    public static void Run()
    {
        int checks=0;
        void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        Check(!RuntimeHelpers.IsReferenceOrContainsReferences<Edge>(),"Mesh edges must not create collector-traced references");
        Check(Marshal.SizeOf<Edge>()==24,"Mesh edge is six packed integers");
        var edges=new Edge[4099];
        for(int i=0;i<edges.Length;i++)edges[i]=new Edge{Id=i,A=i-3,B=int.MaxValue-i,TriangleA=-i,TriangleB=int.MinValue+i,LengthM=i*7919};
        using var canonical=new MemoryStream();
        using(var writer=new BinaryWriter(canonical,Encoding.UTF8,true))
        {
            writer.Write((byte)1);writer.Write("WorldGen.Core.Edge[]");
            foreach(var e in edges)
            {
                writer.Write((byte)1);writer.Write((byte)1);writer.Write("WorldGen.Core.Edge");
                void Field(string name,int value){writer.Write(name);writer.Write((byte)1);writer.Write("System.Int32");writer.Write(value);}
                Field("A",e.A);Field("B",e.B);Field("Id",e.Id);Field("LengthM",e.LengthM);Field("TriangleA",e.TriangleA);Field("TriangleB",e.TriangleB);
            }
            writer.Write((byte)0);
        }
        string expected=Convert.ToHexString(SHA256.HashData(canonical.ToArray())).ToLowerInvariant();
        Check(StateHash.Compute(edges)==expected,"Mesh-edge canonical values changed");
        var jsonCopy=WorldJson.Read<Edge[]>(WorldJson.Write(edges));
        Check(StateHash.Compute(jsonCopy)==expected,"JSON mesh-edge round trip");
        using var wire=new MemoryStream();
        using(var writer=new BinaryWriter(wire,Encoding.UTF8,true))RegionalWire.Write(writer,edges);
        wire.Position=0;using var reader=new BinaryReader(wire,Encoding.UTF8,true);var copy=RegionalWire.Read<Edge[]>(reader);
        Check(copy.Length==edges.Length&&StateHash.Compute(copy)==expected,"Worker wire mesh-edge round trip");
        for(int i=0;i<edges.Length;i++)Check(copy[i].Id==i&&copy[i].TriangleB==int.MinValue+i&&copy[i].LengthM==i*7919,"Complete edge payload");
        const int count=600000;long before=GC.GetAllocatedBytesForCurrentThread();
        var storage=new Edge[count];long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
        for(int i=0;i<count;i++)storage[i]=new Edge{Id=i,A=i,B=i+1,LengthM=17};
        GC.KeepAlive(storage);
        Check(bytes<=count*24L+256,$"Edge allocation must be one inline array, without per-edge heap objects: measured {bytes}, payload {count*24L}");
        Console.WriteLine($"PASS {checks} edge storage/hash/current JSON+worker transport checks; {count} edges allocate {bytes} bytes. Not native GC frame-time evidence.");
    }
}
