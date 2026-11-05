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
        LocalTerrainMesh Read(byte[] data){using var stream=new MemoryStream(data);using var reader=new BinaryReader(stream);return TerrainMeshWire.ReadMesh(reader,pool);}
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
        var last=Read(bytes);pool.Dispose();last.ReleaseUploadBuffers();Check(pool.RetainedBytes==0,"Late upload completion refilled a closed pool");
        Console.WriteLine($"PASS {checks} terrain transport pool checks: exact payloads, concurrent ownership, zero new transient arrays after warmup, retained grids, truncated reads, bounded and late disposal; {warmed} bytes warmup");
    }
}
