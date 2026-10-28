using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Xml;
using WorldGen.Core;

namespace WorldGen.Serialization
{
    // Binary primitive arrays avoid enormous JSON number lists for blood masks.
    internal static class SaveStateFile
    {
        public static void Write<T>(string path,string magic,T state,long decodedLimit,CancellationToken token=default,CompressionLevel compression=CompressionLevel.Fastest)
        {
            using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {
                var header=Encoding.ASCII.GetBytes(magic);file.Write(header,0,header.Length);
                using(var gzip=new GZipStream(file,compression,true))
                using(var bounded=new Bounded(gzip,decodedLimit,token))
                using(var buffered=new BufferedStream(bounded,65536))
                using(var writer=XmlDictionaryWriter.CreateBinaryWriter(buffered,null,null,false))
                using(var transport=HistoricalCellTree.CreateBinaryTransport(token))
                {var serializer=new DataContractSerializer(typeof(T),new DataContractSerializerSettings{MaxItemsInObjectGraph=int.MaxValue});serializer.SetSerializationSurrogateProvider(transport);serializer.WriteObject(writer,state);}
                if(file.Length>WorldFile.MaximumFileBytes)throw new InvalidDataException("Save state exceeds the supported file size.");
                file.Flush(true);
            }
        }
        public static T Read<T>(string path,string magic,long decodedLimit,CancellationToken token=default)
        {
            if(new FileInfo(path).Length>WorldFile.MaximumFileBytes)throw new InvalidDataException("Save state exceeds the supported file size.");
            using(var file=File.OpenRead(path))
            {
                var expected=Encoding.ASCII.GetBytes(magic);foreach(byte b in expected)if(file.ReadByte()!=b)throw new InvalidDataException("Unsupported save state format.");
                using(var gzip=new GZipStream(file,CompressionMode.Decompress))
                using(var bounded=new Bounded(gzip,decodedLimit,token))
                using(var buffered=new BufferedStream(bounded,65536))
                using(var reader=XmlDictionaryReader.CreateBinaryReader(buffered,new XmlDictionaryReaderQuotas{MaxArrayLength=80000000,MaxBytesPerRead=640000000,MaxDepth=64,MaxStringContentLength=1000000,MaxNameTableCharCount=1048576}))
                using(var transport=HistoricalCellTree.CreateBinaryTransport(token))
                using(HistoricalCellTree.BeginReadInterning(token))
                {var serializer=new DataContractSerializer(typeof(T),new DataContractSerializerSettings{MaxItemsInObjectGraph=int.MaxValue});serializer.SetSerializationSurrogateProvider(transport);return (T)serializer.ReadObject(reader);}
            }
        }
        sealed class Bounded:Stream
        {
            readonly Stream inner;readonly long limit;readonly CancellationToken token;long total;
            public Bounded(Stream inner,long limit,CancellationToken token){this.inner=inner;this.limit=limit;this.token=token;}
            void Count(int n){token.ThrowIfCancellationRequested();total+=n;if(total>limit)throw new InvalidDataException("Save state exceeds its decoded-size limit.");}
            public override int Read(byte[] b,int offset,int count){token.ThrowIfCancellationRequested();int n=inner.Read(b,offset,count);Count(n);return n;}
            public override void Write(byte[] b,int offset,int count){Count(count);inner.Write(b,offset,count);}
            public override void Flush(){token.ThrowIfCancellationRequested();inner.Flush();}
            public override bool CanRead=>inner.CanRead;public override bool CanWrite=>inner.CanWrite;public override bool CanSeek=>false;
            public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
            public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
        }
    }
}
