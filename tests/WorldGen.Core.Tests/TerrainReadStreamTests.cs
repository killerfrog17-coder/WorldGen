using WorldGen.Presentation;
static class TerrainReadStreamTests
{
    sealed class ShortSource : MemoryStream
    {
        readonly int maximum;
        public ShortSource(byte[] bytes,int maximum):base(bytes){this.maximum=maximum;}
        public override int Read(byte[] b,int start,int count)=>base.Read(b,start,Math.Min(maximum,count));
    }
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var bytes=new byte[262149];new Random(187).NextBytes(bytes);
        foreach(int maximum in new[]{1,3,91,65536,1000000})
        {
            using var source=new ShortSource(bytes,maximum);using var reader=new TerrainReadStream(source);int at=0;var chunk=new byte[70003];
            while(at<bytes.Length)
            {
                if((at&7)==0){Check(reader.ReadByte()==bytes[at++],"Buffered byte changed");continue;}
                int got=reader.Read(chunk,3,Math.Min(chunk.Length-3,bytes.Length-at));
                Check(got>0&&bytes.AsSpan(at,got).SequenceEqual(chunk.AsSpan(3,got)),"Short/block read changed response bytes");at+=got;
            }
            Check(reader.ReadByte()==-1&&reader.Read(chunk,0,1)==0,"EOF changed");
            reader.Dispose();Check(source.CanRead,"Reader closed independently owned socket stream");
            try{reader.ReadByte();throw new Exception("Disposed reader remained readable");}catch(ObjectDisposedException){}
        }
        using(var source=new MemoryStream(bytes))
        {
            using(var warm=new TerrainReadStream(source))warm.ReadByte();source.Position=0;
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<1000;i++){source.Position=0;using var reader=new TerrainReadStream(source);reader.ReadByte();}
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(allocated<256000,"Warm readers still allocate fresh 64KiB arrays");Console.WriteLine("1000 warm reader allocations: "+allocated+" bytes");
        }
        Parallel.For(0,64,i=>
        {
            using var source=new ShortSource(bytes,19+i);using var reader=new BinaryReader(new TerrainReadStream(source));
            if(!reader.ReadBytes(bytes.Length).SequenceEqual(bytes))throw new Exception("Concurrent response buffers aliased");
        });
        var owned=new MemoryStream(bytes);new TerrainReadStream(owned,false).Dispose();Check(!owned.CanRead,"Owned source was not disposed");
        Console.WriteLine($"PASS {checks} pooled terrain-reader checks: partial reads, direct blocks, mixed byte/block, EOF, disposal, concurrency, bounded warm allocation");
    }
}
