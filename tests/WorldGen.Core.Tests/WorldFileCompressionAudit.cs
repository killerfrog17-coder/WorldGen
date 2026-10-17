using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Xml;
using WorldGen.Core;
using WorldGen.Serialization;

// Explicit transport experiment: never called by ordinary simulation or saves.
public static class WorldFileCompressionAudit
{
    public static void Run(string source,string output)
    {
        if(Directory.Exists(output))throw new IOException("Use a new compression audit folder.");
        Directory.CreateDirectory(output);var world=WorldFile.Read(File.ReadAllBytes(source));string original=world.Prehistory?.Hash;
        var rows=new List<object>();
        foreach(var variant in new[]{(Level:CompressionLevel.Optimal,References:false),(Level:CompressionLevel.Fastest,References:true),(Level:CompressionLevel.Optimal,References:true)})
        {
            var watch=Stopwatch.StartNew();long decoded=0;byte[] bytes;
            using(var memory=new MemoryStream())
            {
                memory.Write(new byte[]{87,71,87,82,76,68,48,49});
                using(var gzip=new GZipStream(memory,variant.Level,true))
                using(var counter=new ByteCounter(gzip,n=>decoded+=n))
                using(var buffered=new BufferedStream(counter,65536))
                using(var writer=XmlDictionaryWriter.CreateBinaryWriter(buffered,null,null,false))
                    new DataContractSerializer(typeof(World),new DataContractSerializerSettings{MaxItemsInObjectGraph=int.MaxValue,PreserveObjectReferences=variant.References}).WriteObject(writer,world);
                bytes=memory.ToArray();
            }
            double saveSeconds=watch.Elapsed.TotalSeconds;string file=Path.Combine(output,$"{variant.Level}-refs{variant.References}.world");File.WriteAllBytes(file,bytes);
            watch.Restart();string restored="";
            if(bytes.Length<=WorldFile.MaximumFileBytes){var loaded=WorldFile.Read(bytes);restored=loaded.Prehistory?.Hash;if(restored!=original)throw new Exception("Transport experiment changed history hash.");}
            rows.Add(new{variant.Level,variant.References,CompressedBytes=bytes.Length,DecodedBytes=decoded,SaveSeconds=saveSeconds,ReadSeconds=watch.Elapsed.TotalSeconds,Hash=restored});
            File.WriteAllText(Path.Combine(output,"compression.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine(JsonSerializer.Serialize(rows.Last()));
        }
        if(world.Prehistory?.Hash!=original)throw new Exception("Compression audit mutated its source.");
    }
    sealed class ByteCounter:Stream
    {
        readonly Stream inner;readonly Action<int> count;
        public ByteCounter(Stream inner,Action<int> count){this.inner=inner;this.count=count;}
        public override void Write(byte[] buffer,int offset,int length){count(length);inner.Write(buffer,offset,length);}
        public override void Flush()=>inner.Flush();public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override int Read(byte[] b,int o,int n)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
    }
}
