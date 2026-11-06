using System;
using System.Buffers;
using System.IO;

namespace WorldGen.Presentation
{
    // Each terrain TCP response used to allocate a fresh 64KiB BufferedStream
    // array. Requests own their reader exclusively; only returned byte buffers
    // are shared. Keep the existing buffered wire protocol and bounded storage.
    public sealed class TerrainReadStream : Stream
    {
        const int Capacity=65536;
        static readonly ArrayPool<byte> Buffers=ArrayPool<byte>.Create(Capacity,8);
        readonly Stream source;readonly bool leaveOpen;
        byte[]? buffer;int next,end;bool closed;
        public TerrainReadStream(Stream source,bool leaveOpen=true)
        {this.source=source??throw new ArgumentNullException(nameof(source));if(!source.CanRead)throw new ArgumentException("Readable source required",nameof(source));this.leaveOpen=leaveOpen;}
        void CheckOpen(){if(closed)throw new ObjectDisposedException(nameof(TerrainReadStream));}
        int Fill()
        {buffer=buffer??Buffers.Rent(Capacity);next=0;return end=source.Read(buffer,0,Capacity);}
        public override int ReadByte()
        {CheckOpen();if(next==end&&Fill()==0)return -1;return buffer![next++];}
        public override int Read(byte[] destination,int offset,int count)
        {
            CheckOpen();if(destination==null)throw new ArgumentNullException(nameof(destination));
            if(offset<0||count<0||offset>destination.Length-count)throw new ArgumentOutOfRangeException();
            if(count==0)return 0;
            if(next==end)
            {
                if(count>=Capacity)return source.Read(destination,offset,count);
                if(Fill()==0)return 0;
            }
            int length=Math.Min(count,end-next);Buffer.BlockCopy(buffer!,next,destination,offset,length);next+=length;return length;
        }
        protected override void Dispose(bool disposing)
        {
            if(closed)return;closed=true;
            if(buffer!=null){Buffers.Return(buffer);buffer=null;}next=end=0;
            if(disposing&&!leaveOpen)source.Dispose();base.Dispose(disposing);
        }
        public override bool CanRead=>!closed&&source.CanRead;
        public override bool CanSeek=>false;
        public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();
        public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush(){CheckOpen();}
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
}
