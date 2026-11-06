using WorldGen.Presentation;

static class TerrainUploadBufferTests
{
    public static void Run()
    {
        int checks=0;
        void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        float[] Values(int n,int salt)=>Enumerable.Range(0,n).Select(i=>(i-salt)*.03125f).ToArray();
        var source=new LocalTerrainMesh{Segments=4,GroundVertices=25,EastM=-125,NorthM=40,SizeM=128,MinHeightM=-10,MaxHeightM=24,BuildMilliseconds=1.25,
            Positions=Values(75,11),Normals=Values(75,12),Colors=Values(100,13),SurfaceWeights=Values(100,14),GroundWeightsB=Values(100,15),Lithology=Values(150,16),
            WaterPositions=Values(18,17),WaterColors=Values(24,18),Triangles=new[]{0,5,1,1,5,6},WaterTriangles=new[]{0,1,2,3,4,5},
            GroundCover=Values(400,20),MidCover=Values(150,21),ForestCover=Values(200,22)};
        byte[] Encode(LocalTerrainMesh mesh){using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);TerrainMeshWire.WriteMesh(writer,mesh);return stream.ToArray();}
        var bytes=Encode(source);using var pool=new TerrainUploadBufferPool();
        LocalTerrainMesh Read(byte[] data){using var stream=new MemoryStream(data);using var incoming=new TerrainReadStream(stream);using var reader=new BinaryReader(incoming);return TerrainMeshWire.ReadMesh(reader,pool);}
        var first=Read(bytes);var second=Read(bytes);
        Check(!ReferenceEquals(first.Positions,second.Positions),"Concurrent leases aliased positions");
        var ground=first.GroundCover;var mid=first.MidCover;var forest=first.ForestCover;
        Check(Encode(first).SequenceEqual(bytes)&&Encode(second).SequenceEqual(bytes),"Pooled transport changed payload bits");
        first.ReleaseUploadBuffers();first.ReleaseUploadBuffers();second.ReleaseUploadBuffers();long warmed=pool.CreatedBytes;
        for(int i=0;i<128;i++)
        {
            var mesh=Read(bytes);Check(Encode(mesh).SequenceEqual(bytes),"Reused mesh attributes changed payload bits");mesh.ReleaseUploadBuffers();
        }
        Check(pool.CreatedBytes==warmed&&pool.ReusedBytes>0,"Stable mesh sizes kept allocating transport attributes");
        Check(first.Positions.Length==0&&first.Triangles.Length==0,"Returned lease retained its transient references");
        Check(ReferenceEquals(first.GroundCover,ground)&&ground.SequenceEqual(source.GroundCover)&&ReferenceEquals(first.MidCover,mid)&&mid.SequenceEqual(source.MidCover)&&ReferenceEquals(first.ForestCover,forest)&&forest.SequenceEqual(source.ForestCover),"Returning upload attributes invalidated resident cover grids");
        try{Read(bytes.Take(bytes.Length/2).ToArray());throw new Exception("Truncated mesh was accepted");}catch(EndOfStreamException){}
        long afterTruncation=pool.CreatedBytes;var recovered=Read(bytes);recovered.ReleaseUploadBuffers();
        Check(pool.CreatedBytes==afterTruncation,"Failed read stranded a leased transport buffer");
        Parallel.For(0,32,_=>{var mesh=Read(bytes);if(!Encode(mesh).SequenceEqual(bytes))throw new Exception("Concurrent pool use corrupted mesh");mesh.ReleaseUploadBuffers();});
        Check(ground.SequenceEqual(source.GroundCover),"Concurrent reuse modified a retained resident cover grid");
        using(var small=new TerrainUploadBufferPool(1024))
        {using var stream=new MemoryStream(bytes);using var reader=new BinaryReader(stream);var mesh=TerrainMeshWire.ReadMesh(reader,small);mesh.ReleaseUploadBuffers();Check(small.RetainedBytes<=1024,"Pool exceeded its byte budget");}
        var privateMethods=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var rentFloat=typeof(TerrainUploadBufferPool).GetMethod("Floats",privateMethods)!;
        var returnFloat=typeof(TerrainUploadBufferPool).GetMethod("Return",privateMethods,null,new[]{typeof(float[])},null)!;
        float[] Rent(TerrainUploadBufferPool owner,int length)=>(float[])rentFloat.Invoke(owner,new object[]{length})!;
        void Give(TerrainUploadBufferPool owner,float[] data)=>returnFloat.Invoke(owner,new object[]{data});
        using(var changing=new TerrainUploadBufferPool(1024*1024))
        {
            for(int length=1;length<=256;length++)Give(changing,Rent(changing,length));
            var recent=Rent(changing,256);Give(changing,recent);
            Check(ReferenceEquals(recent,Rent(changing,256)),"The first 128 historical lengths permanently excluded new mesh sizes");
            Check(changing.RetainedBytes<=1024*1024,"Adaptive lengths exceeded their byte limit");
        }
        using(var pressure=new TerrainUploadBufferPool(1024))
        {
            var held=Rent(pressure,100);held[0]=123;
            Give(pressure,Rent(pressure,200));
            var recent=Rent(pressure,240);recent[0]=456;Give(pressure,recent);
            Check(pressure.RetainedBytes<=1024,"Replacement exceeded its byte limit");
            var reused=Rent(pressure,240);
            Check(ReferenceEquals(recent,reused)&&reused[0]==456,"Budget saturation rejected the current working set forever");
            Check(held[0]==123&&!ReferenceEquals(held,reused),"Cache eviction changed an outstanding lease");
            Give(pressure,held);Give(pressure,reused);
        }
        var last=Read(bytes);pool.Dispose();last.ReleaseUploadBuffers();Check(pool.RetainedBytes==0,"Late upload completion refilled a closed pool");
        using(var transport=new TerrainUploadBufferPool())
        using(var covers=new TerrainUploadBufferPool())
        {
            LocalTerrainMesh Covered(byte[] payload){using var stream=new MemoryStream(payload);using var reader=new BinaryReader(stream);return TerrainMeshWire.ReadMesh(reader,transport,covers);}
            var decoded=Covered(bytes);Check(Encode(decoded).SequenceEqual(bytes),"Resident pooling changed the wire payload");
            var lease=decoded.TakeCoverBuffers()!;var held=lease.Ground;
            Check(decoded.GroundCover.Length==0&&decoded.TakeCoverBuffers()==null,"Cover ownership was not transferred exactly once");
            decoded.ReleaseUploadBuffers();
            Check(held.SequenceEqual(source.GroundCover)&&covers.RetainedBytes==0,"Upload completion returned live cover grids");
            var competing=Covered(bytes);
            Check(!ReferenceEquals(competing.GroundCover,held),"A live resident cover grid was leased twice");
            competing.ReleaseUploadBuffers();lease.Dispose();lease.Dispose();
            Check(lease.Ground.Length==0&&lease.Mid.Length==0&&lease.Forest.Length==0,"Retired cover owner retained returned arrays");
            long warmedCovers=covers.CreatedBytes;
            for(int i=0;i<128;i++)
            {
                var decodedAgain=Covered(bytes);Check(Encode(decodedAgain).SequenceEqual(bytes),"Reused resident grids changed their payload");
                using var resident=decodedAgain.TakeCoverBuffers();decodedAgain.ReleaseUploadBuffers();
                Check(resident!.Ground.SequenceEqual(source.GroundCover),"Transport disposal corrupted transferred grids");
            }
            Check(covers.CreatedBytes==warmedCovers&&covers.ReusedBytes>0,"Resident cover grids still allocated after retirement warmup");
            foreach(int missing in new[]{1,500,1200,2100})
            {
                try{Covered(bytes.Take(bytes.Length-missing).ToArray());throw new Exception("Truncated resident grids were accepted");}catch(EndOfStreamException){}
                var a=Covered(bytes);var b=Covered(bytes);
                Check(!ReferenceEquals(a.GroundCover,b.GroundCover)&&!ReferenceEquals(a.MidCover,b.MidCover)&&!ReferenceEquals(a.ForestCover,b.ForestCover),"Failed resident read returned an array twice");
                Check(Encode(a).SequenceEqual(bytes)&&Encode(b).SequenceEqual(bytes),"Recovery after truncated resident read changed values");
                a.ReleaseUploadBuffers();b.ReleaseUploadBuffers();
            }
            Parallel.For(0,32,_=>{var m=Covered(bytes);if(!Encode(m).SequenceEqual(bytes))throw new Exception("Concurrent resident read changed values");using var owner=m.TakeCoverBuffers();m.ReleaseUploadBuffers();if(!owner!.Ground.SequenceEqual(source.GroundCover))throw new Exception("Concurrent upload completion corrupted a resident");});
            var originalForest=source.ForestCover;source.ForestCover=Values(199,22);var invalidGrid=Encode(source);source.ForestCover=originalForest;
            try{Covered(invalidGrid);throw new Exception("Invalid cover dimensions were accepted");}catch(InvalidDataException){}
            var validAfterInvalid=Covered(bytes);Check(Encode(validAfterInvalid).SequenceEqual(bytes),"Cover validation failure damaged subsequent ownership");validAfterInvalid.ReleaseUploadBuffers();
            var late=Covered(bytes);var lateLease=late.TakeCoverBuffers();late.ReleaseUploadBuffers();covers.Dispose();lateLease!.Dispose();
            Check(covers.RetainedBytes==0,"Late resident retirement refilled a closed pool");
        }
        Console.WriteLine($"PASS {checks} terrain transport/resident pool checks: exact payloads, concurrent ownership, zero new arrays after warmup, transferred grids, truncated/invalid reads, bounded and late disposal; {warmed} bytes transient warmup");
    }
}
