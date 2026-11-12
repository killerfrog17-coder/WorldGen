using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Reflection;
using System.Threading;
using System.Xml;
using WorldGen.Core;

namespace WorldGen.Serialization
{
    /// <summary>Full, lossless, offline world snapshots without an intermediate enormous JSON string.</summary>
    public static partial class WorldFile
    {
        static readonly byte[] Magic = { 87, 71, 87, 82, 76, 68, 48, 49 }; // WGWRLD01
        static readonly byte[] PackedMagic = { 87, 71, 87, 82, 76, 68, 48, 51 }; // WGWRLD03
        // Rich deep-time ledgers legitimately exceed the former 256MB cap.
        // The separate four-GB decoded guard remains in force.
        public const int MaximumFileBytes = 1000000000;
        static DataContractSerializer Serializer(ISerializationSurrogateProvider transport)
        {
            var serializer=new DataContractSerializer(typeof(World),new DataContractSerializerSettings{MaxItemsInObjectGraph=int.MaxValue});
            serializer.SetSerializationSurrogateProvider(transport);return serializer;
        }
        public static byte[] Write(World world, CancellationToken cancellation = default,CompressionLevel compression=CompressionLevel.Optimal)
        {
            using (var memory = new MemoryStream())
            {
                Write(world,memory,cancellation,compression);
                return memory.ToArray();
            }
        }
        /// <summary>Write the same lossless snapshot directly to a caller-owned
        /// stream, without retaining a second compressed snapshot in memory.</summary>
        public static void Write(World world,Stream destination,CancellationToken cancellation=default,CompressionLevel compression=CompressionLevel.Optimal)
        {
            cancellation.ThrowIfCancellationRequested();
            if(destination==null||!destination.CanWrite)throw new ArgumentException("Writable snapshot destination required.",nameof(destination));
            using(var compressed=new GuardedStream(destination,cancellation,true))
            using (var transport=new BinaryWorldTransport(cancellation,world.Prehistory?.Foragers!=null?3:1))
            {
                compressed.Write(world.Prehistory?.Foragers!=null?PackedMagic:Magic, 0, Magic.Length);
                // Old worlds retain version1. New forager worlds use version3
                // with bit-exact numeric and record packing; gzip and integrity checks still
                // cover the whole record. No historical records are discarded.
                using (var gzip = new GZipStream(compressed, compression, true))
                using (var guarded = new GuardedStream(gzip, cancellation))
                using (var buffered = new BufferedStream(guarded, 65536))
                using (var writer = XmlDictionaryWriter.CreateBinaryWriter(buffered, null, null, false))
                    Serializer(transport).WriteObject(writer, world);
                cancellation.ThrowIfCancellationRequested();
            }
        }
        /// <summary>Existing immutable planetary fields, with the separately
        /// simulated history omitted. Arrays are borrowed, never cloned.</summary>
        public static void WritePlanetary(World world,Stream destination,CancellationToken cancellation=default,CompressionLevel compression=CompressionLevel.Fastest)
        {
            if(world==null)throw new ArgumentNullException(nameof(world));
            var planet=new World();
            foreach(var field in typeof(World).GetFields(BindingFlags.Public|BindingFlags.Instance))field.SetValue(planet,field.GetValue(world));
            planet.Prehistory=null;
            Write(planet,destination,cancellation,compression);
        }
        public static World Read(byte[] bytes, CancellationToken cancellation = default, Action<string> status = null)
            =>ReadSnapshot(bytes,cancellation,status,false);
        // Geometry workers need the exact original planet, never its potentially
        // enormous human archive. Normal game/save reads always retain history.
        public static World ReadPlanetary(byte[] bytes,CancellationToken cancellation=default,Action<string> status=null)
            =>ReadSnapshot(bytes,cancellation,status,true);
        static World ReadSnapshot(byte[] bytes,CancellationToken cancellation,Action<string> status,bool terrainOnly)
        {
            cancellation.ThrowIfCancellationRequested();
            if (bytes == null || bytes.Length < Magic.Length + 18 || bytes.Length > MaximumFileBytes) throw new ArgumentException("Invalid compact world-file size.");
            for (int i = 0; i < Magic.Length-1; i++) if (bytes[i] != Magic[i]) throw new ArgumentException("Unsupported world-file format; use a .world snapshot or the JSON import option.");
            if(bytes[Magic.Length-1]<49||bytes[Magic.Length-1]>51)throw new ArgumentException("Unsupported compact world-file version.");
            int version=bytes[Magic.Length-1]-48;bool packed=version>=2;
            using (var memory = new MemoryStream(bytes, Magic.Length, bytes.Length - Magic.Length, false))
            using (var gzip = new GZipStream(memory, CompressionMode.Decompress))
            using (var guarded = new GuardedStream(gzip, cancellation))
            using (var buffered = new BufferedStream(guarded, 65536))
            using (var reader = XmlDictionaryReader.CreateBinaryReader(buffered, new XmlDictionaryReaderQuotas
            // Mono reads a packed primitive array as one block, unlike CoreCLR.
            // A maximum-detail triangle array alone is about61MB. Match the
            // existing array-item allowance (largest primitive is an8-byte double)
            // instead of rejecting valid worlds above roughly40k terrain cells.
            { MaxArrayLength = packed?640000000:80000000, MaxDepth = 64, MaxStringContentLength = 1000000, MaxBytesPerRead = 640000000, MaxNameTableCharCount = 1048576 }))
            {
                status?.Invoke("Reading terrain and saved history");
                World world;
                using(var transport=new BinaryWorldTransport(cancellation,version,terrainOnly))
                using(HistoricalCellTree.BeginReadInterning(cancellation))world=(World)Serializer(transport).ReadObject(reader);
                status?.Invoke("Checking world and history integrity");
                return WorldJson.ValidateWorld(world, cancellation, status);
            }
        }
        sealed class GuardedStream : Stream
        {
            readonly Stream inner; readonly CancellationToken token;readonly bool compressed; long bytes;
            public GuardedStream(Stream inner, CancellationToken token,bool compressed=false) { this.inner = inner; this.token = token;this.compressed=compressed; }
            void Count(int count)
            {
                token.ThrowIfCancellationRequested();bytes+=count;
                if(compressed&&bytes>MaximumFileBytes)throw new ArgumentException("This snapshot exceeds the supported compressed world-file size.");
                if(!compressed&&bytes>4000000000L)throw new InvalidDataException("World snapshot exceeds the decoded-size limit.");
            }
            public override int Read(byte[] buffer, int offset, int count) { token.ThrowIfCancellationRequested(); int read = inner.Read(buffer, offset, count); Count(read); return read; }
            public override void Write(byte[] buffer, int offset, int count) { Count(count); inner.Write(buffer, offset, count); }
            public override void Flush() { token.ThrowIfCancellationRequested(); inner.Flush(); }
            public override bool CanRead => inner.CanRead; public override bool CanWrite => inner.CanWrite; public override bool CanSeek => false;
            public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
