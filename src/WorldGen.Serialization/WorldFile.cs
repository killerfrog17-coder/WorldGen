using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Threading;
using System.Xml;
using WorldGen.Core;

namespace WorldGen.Serialization
{
    /// <summary>Full, lossless, offline world snapshots without an intermediate enormous JSON string.</summary>
    public static class WorldFile
    {
        static readonly byte[] Magic = { 87, 71, 87, 82, 76, 68, 48, 49 }; // WGWRLD01
        public const int MaximumFileBytes = 256000000;
        static DataContractSerializer Serializer() => new DataContractSerializer(typeof(World), new DataContractSerializerSettings { MaxItemsInObjectGraph = int.MaxValue });
        public static byte[] Write(World world, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            using (var memory = new MemoryStream())
            {
                memory.Write(Magic, 0, Magic.Length);
                using (var gzip = new GZipStream(memory, CompressionLevel.Fastest, true))
                using (var guarded = new GuardedStream(gzip, cancellation))
                using (var buffered = new BufferedStream(guarded, 65536))
                using (var writer = XmlDictionaryWriter.CreateBinaryWriter(buffered, null, null, false))
                    Serializer().WriteObject(writer, world);
                cancellation.ThrowIfCancellationRequested();
                if (memory.Length > MaximumFileBytes) throw new ArgumentException("This snapshot exceeds the 256 MB compact world-file limit. Use a lower terrain detail.");
                return memory.ToArray();
            }
        }
        public static World Read(byte[] bytes, CancellationToken cancellation = default, Action<string> status = null)
        {
            cancellation.ThrowIfCancellationRequested();
            if (bytes == null || bytes.Length < Magic.Length + 18 || bytes.Length > MaximumFileBytes) throw new ArgumentException("Invalid compact world-file size.");
            for (int i = 0; i < Magic.Length; i++) if (bytes[i] != Magic[i]) throw new ArgumentException("Unsupported world-file format; use a .world snapshot or the JSON import option.");
            using (var memory = new MemoryStream(bytes, Magic.Length, bytes.Length - Magic.Length, false))
            using (var gzip = new GZipStream(memory, CompressionMode.Decompress))
            using (var guarded = new GuardedStream(gzip, cancellation))
            using (var buffered = new BufferedStream(guarded, 65536))
            using (var reader = XmlDictionaryReader.CreateBinaryReader(buffered, new XmlDictionaryReaderQuotas
            // Mono reads a packed primitive array as one block, unlike CoreCLR.
            // A maximum-detail triangle array alone is about61MB. Match the
            // existing array-item allowance (largest primitive is an8-byte double)
            // instead of rejecting valid worlds above roughly40k terrain cells.
            { MaxArrayLength = 80000000, MaxDepth = 64, MaxStringContentLength = 1000000, MaxBytesPerRead = 640000000, MaxNameTableCharCount = 1048576 }))
            {
                status?.Invoke("Reading terrain and saved history");
                var world=(World)Serializer().ReadObject(reader);
                status?.Invoke("Checking world and history integrity");
                return WorldJson.ValidateWorld(world, cancellation, status);
            }
        }
        sealed class GuardedStream : Stream
        {
            readonly Stream inner; readonly CancellationToken token; long bytes;
            public GuardedStream(Stream inner, CancellationToken token) { this.inner = inner; this.token = token; }
            void Count(int count) { token.ThrowIfCancellationRequested(); bytes += count; if (bytes > 4000000000L) throw new InvalidDataException("World snapshot exceeds the decoded-size limit."); }
            public override int Read(byte[] buffer, int offset, int count) { token.ThrowIfCancellationRequested(); int read = inner.Read(buffer, offset, count); Count(read); return read; }
            public override void Write(byte[] buffer, int offset, int count) { Count(count); inner.Write(buffer, offset, count); }
            public override void Flush() { token.ThrowIfCancellationRequested(); inner.Flush(); }
            public override bool CanRead => inner.CanRead; public override bool CanWrite => inner.CanWrite; public override bool CanSeek => false;
            public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
