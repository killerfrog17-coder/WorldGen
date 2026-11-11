using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    // The terrain worker returns its managed wire leases immediately after
    // copying. Resident grids remain available for CPU stealth sampling and GPU
    // rebuilding without keeping those large arrays in the managed heap.
    sealed class ResidentCoverBuffers : IDisposable
    {
        public NativeArray<float> Ground,Mid,Forest;
        int disposed;
        long bytes;
        static long residentBytes;
        public static long ResidentBytes=>Interlocked.Read(ref residentBytes);
        public ResidentCoverBuffers(float[] ground,float[] mid,float[] forest)
        {
            try{Ground=Copy(ground);Mid=Copy(mid);Forest=Copy(forest);}
            catch{Dispose();throw;}
        }
        NativeArray<float> Copy(float[] source)
        {
            if(source==null||source.Length==0)return default;
            var result=new NativeArray<float>(source.Length,Allocator.Persistent,NativeArrayOptions.UninitializedMemory);
            try{result.CopyFrom(source);}catch{result.Dispose();throw;}
            long count=(long)source.Length*sizeof(float);bytes+=count;Interlocked.Add(ref residentBytes,count);return result;
        }
        public void Dispose()
        {
            if(Interlocked.Exchange(ref disposed,1)!=0)return;
            if(Ground.IsCreated)Ground.Dispose();if(Mid.IsCreated)Mid.Dispose();if(Forest.IsCreated)Forest.Dispose();
            Ground=Mid=Forest=default;Interlocked.Add(ref residentBytes,-bytes);bytes=0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct TerrainVertex
    {
        public Vector3 Position,Normal;
        public Color Color;
        public Vector2 Uv;
        public Vector4 GroundA,GroundB;
        public float EdgeIndex;
        public Vector4 RockA;
        public Vector2 RockB;
    }

    // Only this explorer's retired terrain meshes enter the pool. Size classes
    // tolerate different seam/water vertex counts without reallocating buffers.
    // Submesh ranges and bounds still describe ONLY the actual geometry.
    // A frame quarantine avoids immediately rewriting recently submitted draws;
    // MarkDynamic also lets the driver rename storage if an older draw is live.
    sealed class TerrainMeshPool : IDisposable
    {
        readonly struct Shape : IEquatable<Shape>
        {
            public readonly int Vertices,Indices;public readonly IndexFormat Format;
            public Shape(int vertices,int indices,IndexFormat format){Vertices=Capacity(vertices);Indices=Capacity(indices);Format=format;}
            static int Capacity(int count){if(count==0)return 0;int size=1;while(size<count)size=checked(size*2);return size;}
            public long Bytes=>(long)Vertices*Marshal.SizeOf<TerrainVertex>()+(long)Indices*(Format==IndexFormat.UInt16?2:4);
            public bool Equals(Shape other)=>Vertices==other.Vertices&&Indices==other.Indices&&Format==other.Format;
        }
        readonly struct Retired
        {
            public readonly Mesh Mesh;public readonly Shape Shape;public readonly int ReadyFrame;
            public Retired(Mesh mesh,Shape shape){Mesh=mesh;Shape=shape;ReadyFrame=Time.frameCount+4;}
        }
        readonly System.Collections.Generic.List<Retired> retired=new System.Collections.Generic.List<Retired>(64);
        readonly System.Collections.Generic.Dictionary<Mesh,Shape> leased=new System.Collections.Generic.Dictionary<Mesh,Shape>();
        readonly long limit;bool disposed;
        public long Bytes{get;private set;}public int Created{get;private set;}public int Reused{get;private set;}
        public int Retained=>retired.Count;
        public long CapacityBytes(Mesh mesh)=>leased[mesh].Bytes;
        public bool TryCapacityBytes(Mesh mesh,out long bytes){if(leased.TryGetValue(mesh,out var shape)){bytes=shape.Bytes;return true;}bytes=0;return false;}
        public TerrainMeshPool(long maximumBytes=32L*1024*1024){limit=maximumBytes;}
        public Mesh Acquire(int vertices,int indices,IndexFormat format)
        {
            if(disposed)throw new ObjectDisposedException(nameof(TerrainMeshPool));
            var shape=new Shape(vertices,indices,format);Mesh mesh=null;
            for(int i=0;i<retired.Count;i++)
            {
                var candidate=retired[i];
                if(Time.frameCount<candidate.ReadyFrame||!candidate.Shape.Equals(shape))continue;
                mesh=candidate.Mesh;retired.RemoveAt(i);Bytes-=shape.Bytes;Reused++;break;
            }
            if(mesh==null)
            {
                mesh=new Mesh{name="Reusable terrain geometry"};
                try{mesh.MarkDynamic();mesh.SetVertexBufferParams(shape.Vertices,TerrainUpload.Layout);mesh.SetIndexBufferParams(shape.Indices,format);mesh.subMeshCount=1;Created++;}
                catch{UnityEngine.Object.Destroy(mesh);throw;}
            }
            leased.Add(mesh,shape);return mesh;
        }
        public bool Release(Mesh mesh)
        {
            if(!leased.TryGetValue(mesh,out var shape))return false;
            leased.Remove(mesh);
            if(disposed||shape.Bytes>limit){UnityEngine.Object.Destroy(mesh);return true;}
            // Retiring a large one-off shape must not destroy a whole queue of
            // small buffers in one frame. Evict at most one; reject if still full.
            if(retired.Count>0&&(Bytes+shape.Bytes>limit||retired.Count>=64))
            {var oldest=retired[0];retired.RemoveAt(0);Bytes-=oldest.Shape.Bytes;UnityEngine.Object.Destroy(oldest.Mesh);}
            if(Bytes+shape.Bytes>limit){UnityEngine.Object.Destroy(mesh);return true;}
            retired.Add(new Retired(mesh,shape));Bytes+=shape.Bytes;return true;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            foreach(var item in retired)UnityEngine.Object.Destroy(item.Mesh);
            foreach(var mesh in leased.Keys)UnityEngine.Object.Destroy(mesh);
            retired.Clear();leased.Clear();Bytes=0;
        }
    }

    // Used only after a real missing resident-profile knot is found. The
    // original CPU mesh snapshot supplies attributes; no world resampling or
    // vertical filler geometry is involved. Only boundary triangles change.
    bool SplitResidentLandBoundary(TerrainTileKey key,Tile tile,System.Collections.Generic.List<double>[] cuts)
    {
        var previous=tile.ResidentEdges;var oldMesh=previous.Mesh;if(oldMesh==null)return false;
        TerrainVertex[] original;int[] oldIndices;int oldIndexBytes;
        using(var read=Mesh.AcquireReadOnlyMeshData(oldMesh))
        {
            var source=read[0];var sub=source.GetSubMesh(0);original=new TerrainVertex[sub.vertexCount];
            var sourceVertices=source.GetVertexData<TerrainVertex>();for(int i=0;i<original.Length;i++)original[i]=sourceVertices[i];
            oldIndices=new int[sub.indexCount];oldIndexBytes=source.indexFormat==IndexFormat.UInt16?2:4;
            if(source.indexFormat==IndexFormat.UInt16){var indices=source.GetIndexData<ushort>();for(int i=0;i<oldIndices.Length;i++)oldIndices[i]=indices[sub.indexStart+i]+sub.baseVertex;}
            else{var indices=source.GetIndexData<int>();for(int i=0;i<oldIndices.Length;i++)oldIndices[i]=indices[sub.indexStart+i]+sub.baseVertex;}
        }
        var vertices=new System.Collections.Generic.List<TerrainVertex>(original);
        var triangles=new System.Collections.Generic.List<int>(oldIndices.Length+64);
        var inserted=new System.Collections.Generic.Dictionary<(int,int),int[]>();
        var boundaryMasks=new int[original.Length];foreach(var point in previous.Points)boundaryMasks[point.Vertex]=point.Mask;
        double size=key.SizeM;
        int[] Edge(int a,int b)
        {
            int low=Math.Min(a,b),high=Math.Max(a,b);if(inserted.TryGetValue((low,high),out var existing))return existing;
            var pa=original[low].Position;var pb=original[high].Position;
            int mask=boundaryMasks[low]&boundaryMasks[high],side=(mask&1)!=0?0:(mask&2)!=0?1:(mask&4)!=0?2:(mask&8)!=0?3:-1;
            if(side<0||cuts[side]==null||cuts[side].Count==0)return Array.Empty<int>();
            bool vertical=side<2;double from=vertical?pa.z:pa.x,to=vertical?pb.z:pb.x;
            double first=Math.Min(from,to),last=Math.Max(from,to);var added=new System.Collections.Generic.List<int>();
            foreach(double cut in cuts[side])
            {
                if((float)cut<=(float)first||(float)cut>=(float)last)continue;
                var vertex=InterpolateResidentVertex(original[low],original[high],(float)((cut-from)/(to-from)));
                var position=vertex.Position;if(vertical)position.z=(float)cut;else position.x=(float)cut;vertex.Position=position;
                added.Add(vertices.Count);vertices.Add(vertex);
            }
            if(from>to)added.Reverse();var result=added.ToArray();inserted[(low,high)]=result;return result;
        }
        void AddEdgePolygon(System.Collections.Generic.List<int> polygon,int a,int b,int[] additions)
        {
            polygon.Add(a);if(a<b){foreach(int vertex in additions)polygon.Add(vertex);}
            else for(int i=additions.Length-1;i>=0;i--)polygon.Add(additions[i]);
        }
        for(int at=0;at<oldIndices.Length;at+=3)
        {
            int a=oldIndices[at],b=oldIndices[at+1],c=oldIndices[at+2];var ab=Edge(a,b);var bc=Edge(b,c);var ca=Edge(c,a);
            int changed=(ab.Length>0?1:0)+(bc.Length>0?1:0)+(ca.Length>0?1:0);
            if(changed==0){triangles.Add(a);triangles.Add(b);triangles.Add(c);continue;}
            if(changed==1)
            {
                int start=ab.Length>0?a:bc.Length>0?b:c,end=ab.Length>0?b:bc.Length>0?c:a,opposite=ab.Length>0?c:bc.Length>0?a:b;
                var additions=ab.Length>0?ab:bc.Length>0?bc:ca;int last=start;
                for(int i=0;i<additions.Length;i++){int next=additions[start<end?i:additions.Length-1-i];triangles.Add(last);triangles.Add(next);triangles.Add(opposite);last=next;}
                triangles.Add(last);triangles.Add(end);triangles.Add(opposite);continue;
            }
            // A corner triangle can have knots on two sides. A point on its
            // original plane preserves both complete edge chains without a
            // degenerate fan that silently discards one chain's knots.
            var polygon=new System.Collections.Generic.List<int>(3+ab.Length+bc.Length+ca.Length);
            AddEdgePolygon(polygon,a,b,ab);AddEdgePolygon(polygon,b,c,bc);AddEdgePolygon(polygon,c,a,ca);
            int centre=vertices.Count;vertices.Add(InterpolateResidentVertex(InterpolateResidentVertex(original[a],original[b],.5f),original[c],1f/3));
            for(int i=0;i<polygon.Count;i++){triangles.Add(polygon[i]);triangles.Add(polygon[(i+1)%polygon.Count]);triangles.Add(centre);}
        }
        int addedCount=vertices.Count-original.Length;if(addedCount==0)return false;
        var positions=new float[vertices.Count*3];for(int i=0;i<vertices.Count;i++){var p=vertices[i].Position;positions[i*3]=p.x;positions[i*3+1]=p.y;positions[i*3+2]=p.z;}
        var finalIndices=triangles.ToArray();var wire=new LocalTerrainMesh{Positions=positions,Triangles=finalIndices,Segments=tile.Segments,SizeM=size,GroundVertices=tile.Heights.Length};
        var contact=TerrainContactSurface.Build(wire);ResidentEdgeData data=null;Mesh mesh=null;
        try
        {
            using(var native=new NativeArray<TerrainVertex>(vertices.ToArray(),Allocator.Temp))
            {
                data=ResidentEdgeData.Build(wire,native);mesh=new Mesh{name="Resident terrain boundary knot union"};mesh.MarkDynamic();
                mesh.SetVertexBufferParams(native.Length,TerrainUpload.Layout);mesh.SetVertexBufferData(native,0,0,native.Length);
                bool compact=native.Length<=65535;mesh.SetIndexBufferParams(finalIndices.Length,compact?IndexFormat.UInt16:IndexFormat.UInt32);
                if(compact){var indices=new ushort[finalIndices.Length];for(int i=0;i<indices.Length;i++)indices[i]=(ushort)finalIndices[i];mesh.SetIndexBufferData(indices,0,0,indices.Length);}
                else mesh.SetIndexBufferData(finalIndices,0,0,finalIndices.Length);
                mesh.subMeshCount=1;mesh.bounds=oldMesh.bounds;mesh.SetSubMesh(0,new SubMeshDescriptor(0,finalIndices.Length){vertexCount=native.Length,bounds=mesh.bounds},MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);
            }
        }
        catch{data?.Dispose();if(mesh!=null)Destroy(mesh);throw;}
        long oldLand=terrainMeshPool.TryCapacityBytes(oldMesh,out long capacity)?capacity:(long)original.Length*Marshal.SizeOf<TerrainVertex>()+(long)oldIndices.Length*oldIndexBytes;
        long newLand=(long)vertices.Count*Marshal.SizeOf<TerrainVertex>()+(long)finalIndices.Length*(vertices.Count<=65535?2:4);
        tile.BufferBytes+=newLand-oldLand+data.Bytes-previous.Bytes-(previous.Texture!=null?(long)previous.Points.Length*4:0)+(contact?.Bytes??0)-(tile.Contact?.Bytes??0);
        data.Mesh=mesh;data.TopologyChanged=true;tile.Root.transform.Find("Land and seabed").GetComponent<MeshFilter>().sharedMesh=mesh;
        tile.ResidentEdges=data;tile.Contact=contact;
        for(int i=0;i<tile.Heights.Length;i++){tile.Heights[i]=positions[i*3+1];if(tile.Cover.IsCreated&&i*16+15<tile.Cover.Length)tile.Cover[i*16+15]=tile.Heights[i];}
        tile.Materials[0].SetFloat("_ResidentEdgeCount",0);tile.Materials[0].SetTexture("_ResidentEdgeHeights",Texture2D.blackTexture);
        previous.Dispose();if(!terrainMeshPool.Release(oldMesh))Destroy(oldMesh);
        residentEdgeSplits++;residentEdgeInsertedVertices+=addedCount;return true;
    }
    static TerrainVertex InterpolateResidentVertex(TerrainVertex a,TerrainVertex b,float t)=>new TerrainVertex{
        Position=Vector3.LerpUnclamped(a.Position,b.Position,t),Normal=Vector3.LerpUnclamped(a.Normal,b.Normal,t),Color=Color.LerpUnclamped(a.Color,b.Color,t),Uv=Vector2.LerpUnclamped(a.Uv,b.Uv,t),
        GroundA=Vector4.LerpUnclamped(a.GroundA,b.GroundA,t),GroundB=Vector4.LerpUnclamped(a.GroundB,b.GroundB,t),RockA=Vector4.LerpUnclamped(a.RockA,b.RockA,t),RockB=Vector2.LerpUnclamped(a.RockB,b.RockB,t)};
    readonly TerrainMeshPool terrainMeshPool=new TerrainMeshPool();

    // Allocate the native container on the main thread. Its MeshData can then
    // be filled by the existing worker without constructing Unity objects there.
    sealed class TerrainUpload : IDisposable
    {
        public static readonly VertexAttributeDescriptor[] Layout={
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
            new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3),
            new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3,VertexAttributeFormat.Float32,1),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord4,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord5,VertexAttributeFormat.Float32,2)};
        Mesh.MeshDataArray buffers;
        int live;
        Bounds[] bounds;
        IndexFormat[] formats;
        public double EastM,NorthM,BuildMilliseconds,BufferMilliseconds;
        public int Segments;
        public long BufferBytes;
        public float[] Heights;public TerrainContactSurface Contact;public NativeArray<float> Cover,MidCover,ForestCover;
        ResidentCoverBuffers coverBuffers;
        ResidentEdgeData residentEdges;
        public ResidentCoverBuffers TakeCoverBuffers()=>Interlocked.Exchange(ref coverBuffers,null);
        public ResidentEdgeData TakeResidentEdges()=>Interlocked.Exchange(ref residentEdges,null);
        bool hasWater;
        public bool HasWater=>hasWater;
        public Bounds WaterBounds=>hasWater?bounds[1]:default;
        static SynchronizationContext mainContext;static int mainThread,pendingReleases;
        public static int PendingReleases=>Volatile.Read(ref pendingReleases);
        static TerrainUpload Allocate()
        {
            var context=SynchronizationContext.Current;
            if(context==null)throw new InvalidOperationException("Schedule native terrain buffers from the Unity main thread.");
            mainContext=context;mainThread=Thread.CurrentThread.ManagedThreadId;
            return new TerrainUpload{buffers=Mesh.AllocateWritableMeshData(2),live=1};
        }
        public static System.Threading.Tasks.Task<TerrainUpload> Schedule(Func<LocalTerrainMesh> build,CancellationToken token)
        {
            var allocated=Allocate();
            // Always execute the cleanup path, including cancellation before the
            // task starts. Passing token to Task.Run could strand its container.
            return System.Threading.Tasks.Task.Run(()=>{LocalTerrainMesh source=null;try{token.ThrowIfCancellationRequested();source=build();return Prepare(source,token,allocated);}catch{allocated.Dispose();throw;}finally{source?.ReleaseUploadBuffers();}});
        }

        public static TerrainUpload Prepare(LocalTerrainMesh source,CancellationToken token)
        {token.ThrowIfCancellationRequested();return Prepare(source,token,Allocate());}
        static TerrainUpload Prepare(LocalTerrainMesh source,CancellationToken token,TerrainUpload result)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            result.EastM=source.EastM;result.NorthM=source.NorthM;result.Segments=source.Segments;result.BuildMilliseconds=source.BuildMilliseconds;
            result.Heights=new float[source.GroundVertices];result.Contact=TerrainContactSurface.Build(source);result.hasWater=source.WaterTriangles.Length>0;result.bounds=new Bounds[2];
            result.formats=new IndexFormat[2];
            try
            {
                result.coverBuffers=new ResidentCoverBuffers(source.GroundCover,source.MidCover,source.ForestCover);
                result.Cover=result.coverBuffers.Ground;result.MidCover=result.coverBuffers.Mid;result.ForestCover=result.coverBuffers.Forest;
                source.TakeCoverBuffers()?.Dispose();
                for(int i=0;i<result.Heights.Length;i++)result.Heights[i]=source.Positions[i*3+1];
                for(int part=0;part<result.bounds.Length;part++)
                {
                    bool water=part==1;
                    if(water&&!result.hasWater)
                    {var empty=result.buffers[part];empty.SetVertexBufferParams(0,Layout);empty.SetIndexBufferParams(0,IndexFormat.UInt16);empty.subMeshCount=0;continue;}
                    var positions=water?source.WaterPositions:source.Positions;
                    var colors=water?source.WaterColors:source.Colors;
                    var indices=water?source.WaterTriangles:source.Triangles;
                    var data=result.buffers[part];int count=positions.Length/3;
                    // Decide from FINAL appended geometry, not the regular grid.
                    bool compact=count<=65535;foreach(int index in indices)if(index<0||index>=count)throw new InvalidOperationException("Terrain index outside final vertex buffer");
                    var format=result.formats[part]=compact?IndexFormat.UInt16:IndexFormat.UInt32;
                    result.BufferBytes+=(long)count*Marshal.SizeOf<TerrainVertex>()+(long)indices.Length*(compact?2:4);
                    data.SetVertexBufferParams(count,Layout);data.SetIndexBufferParams(indices.Length,format);
                    var vertices=data.GetVertexData<TerrainVertex>();
                    Vector3 low=new Vector3(float.MaxValue,float.MaxValue,float.MaxValue),high=new Vector3(float.MinValue,float.MinValue,float.MinValue);
                    for(int i=0;i<count;i++)
                    {
                        if((i&1023)==0)token.ThrowIfCancellationRequested();
                        var p=new Vector3(positions[i*3],positions[i*3+1],positions[i*3+2]);low=Vector3.Min(low,p);high=Vector3.Max(high,p);
                        var v=new TerrainVertex{Position=p,Normal=water?Vector3.up:new Vector3(source.Normals[i*3],source.Normals[i*3+1],source.Normals[i*3+2]),
                            Color=new Color(colors[i*4],colors[i*4+1],colors[i*4+2],colors[i*4+3]),Uv=new Vector2((float)(source.EastM+p.x)*.5f,(float)(source.NorthM+p.z)*.5f)};
                        if(!water)
                        {
                            v.GroundA=new Vector4(source.SurfaceWeights[i*4],source.SurfaceWeights[i*4+1],source.SurfaceWeights[i*4+2],source.SurfaceWeights[i*4+3]);
                            v.GroundB=new Vector4(source.GroundWeightsB[i*4],source.GroundWeightsB[i*4+1],source.GroundWeightsB[i*4+2],source.GroundWeightsB[i*4+3]);
                            v.RockA=new Vector4(source.Lithology[i*6],source.Lithology[i*6+1],source.Lithology[i*6+2],source.Lithology[i*6+3]);
                            v.RockB=new Vector2(source.Lithology[i*6+4],source.Lithology[i*6+5]);
                        }
                        vertices[i]=v;
                    }
                    if(!water)
                    {
                        result.residentEdges=ResidentEdgeData.Build(source,vertices);
                        result.BufferBytes+=result.residentEdges.Bytes;
                    }
                    if(compact){var target=data.GetIndexData<ushort>();for(int i=0;i<indices.Length;i++)target[i]=(ushort)indices[i];}
                    else data.GetIndexData<int>().CopyFrom(indices);
                    result.bounds[part]=new Bounds((low+high)*.5f,high-low);
                    if(water)result.bounds[part].Expand(new Vector3(0,3.2f,0)); // shader swell can move above/below the stored datum
                    data.subMeshCount=1;
                    data.SetSubMesh(0,new SubMeshDescriptor(0,indices.Length,MeshTopology.Triangles){bounds=result.bounds[part],vertexCount=count},
                        MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);
                }
                result.BufferBytes+=(long)(result.Heights.Length+result.Cover.Length+result.MidCover.Length+result.ForestCover.Length)*4+(result.Contact?.Bytes??0);result.BufferMilliseconds=watch.Elapsed.TotalMilliseconds;return result;
            }
            catch{result.Dispose();throw;}
        }
        public Mesh[] Apply(TerrainMeshPool pool=null)
        {
            if(live==0)throw new ObjectDisposedException(nameof(TerrainUpload));
            if(pool!=null)return ApplyReused(pool);
            var meshes=new Mesh[bounds.Length];for(int i=0;i<meshes.Length;i++)meshes[i]=new Mesh{indexFormat=formats[i]};
            try{Mesh.ApplyAndDisposeWritableMeshData(buffers,meshes,MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);}
            catch{foreach(var mesh in meshes)UnityEngine.Object.Destroy(mesh);throw;}
            live=0;for(int i=0;i<meshes.Length;i++)meshes[i].bounds=bounds[i];
            if(residentEdges!=null)residentEdges.Mesh=meshes[0];
            if(!hasWater){UnityEngine.Object.Destroy(meshes[1]);return new[]{meshes[0]};}return meshes;
        }
        Mesh[] ApplyReused(TerrainMeshPool pool)
        {
            var meshes=new Mesh[hasWater?2:1];
            const MeshUpdateFlags flags=MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices;
            try
            {
                for(int i=0;i<meshes.Length;i++)
                {
                    var data=buffers[i];var submesh=data.GetSubMesh(0);
                    var mesh=meshes[i]=pool.Acquire(data.vertexCount,submesh.indexCount,formats[i]);
                    // The terrain cache must account for retained capacity too,
                    // not just the smaller live geometry stored in this lease.
                    BufferBytes+=pool.CapacityBytes(mesh)-(long)data.vertexCount*Marshal.SizeOf<TerrainVertex>()-(long)submesh.indexCount*(formats[i]==IndexFormat.UInt16?2:4);
                    // Never reset buffer parameters on a reused mesh: doing so
                    // would recreate the storage this path exists to preserve.
                    mesh.SetVertexBufferData(data.GetVertexData<TerrainVertex>(),0,0,data.vertexCount,0,flags);
                    if(formats[i]==IndexFormat.UInt16)mesh.SetIndexBufferData(data.GetIndexData<ushort>(),0,0,submesh.indexCount,flags);
                    else mesh.SetIndexBufferData(data.GetIndexData<uint>(),0,0,submesh.indexCount,flags);
                    mesh.SetSubMesh(0,submesh,flags);mesh.bounds=bounds[i];
                }
                if(residentEdges!=null)residentEdges.Mesh=meshes[0];
                return meshes;
            }
            catch{foreach(var mesh in meshes)if(mesh!=null)pool.Release(mesh);throw;}
            finally{buffers.Dispose();live=0;}
        }
        public void Dispose()
        {
            TakeCoverBuffers()?.Dispose();
            TakeResidentEdges()?.Dispose();
            if(Interlocked.Exchange(ref live,0)==0)return;
            var owned=buffers;
            if(Thread.CurrentThread.ManagedThreadId==mainThread){owned.Dispose();return;}
            // Unity also requires releasing the container on its main thread.
            // The context outlives an explorer, so cancellation after leaving a
            // world still releases it while the main menu is running.
            Interlocked.Increment(ref pendingReleases);
            mainContext.Post(_=>{try{owned.Dispose();}finally{Interlocked.Decrement(ref pendingReleases);}},null);
        }
    }

    readonly System.Collections.Generic.List<double> loadingFrames=new System.Collections.Generic.List<double>();
    public string PerformanceUploadPool=>(meshProcess?.UploadBufferStatistics??"no worker")+" native_cover_resident_bytes="+ResidentCoverBuffers.ResidentBytes+" mesh_created="+terrainMeshPool.Created+" mesh_reused="+terrainMeshPool.Reused+" mesh_retained="+terrainMeshPool.Retained+" mesh_retained_bytes="+terrainMeshPool.Bytes;
    bool loadingProfile;
    int loadingSkip,loadingCollections;
    double largestGroundMs;
    public void BeginLoadingProfile(){loadingFrames.Clear();loadingProfile=true;loadingSkip=2;loadingCollections=GC.CollectionCount(0);largestGroundMs=0;}
    public string EndLoadingProfile()
    {
        loadingProfile=false;loadingFrames.Sort();if(loadingFrames.Count==0)return "No frame samples";
        double Percent(double p)=>loadingFrames[Math.Min(loadingFrames.Count-1,(int)((loadingFrames.Count-1)*p))];
        return loadingFrames.Count+" frames; median "+Percent(.5).ToString("F2")+"ms; p99 "+Percent(.99).ToString("F2")+"ms; worst "+Percent(1).ToString("F2")+"ms; over100ms "+loadingFrames.FindAll(t=>t>100).Count+"; collections "+(GC.CollectionCount(0)-loadingCollections)+"; largest mesh apply/bind "+LargestUploadMs.ToString("F2")+"ms; largest controller query "+largestGroundMs.ToString("F2")+"ms; incremental "+UnityEngine.Scripting.GarbageCollector.isIncremental+" / "+UnityEngine.Scripting.GarbageCollector.GCMode;
    }
    void RecordLoadingFrame(){if(!loadingProfile)return;if(loadingSkip-->0)return;loadingFrames.Add(Time.unscaledDeltaTime*1000);}

    public static System.Collections.IEnumerator CheckUploadBuffers(string output)
    {
        var source=new LocalTerrainMesh{GroundVertices=3,Segments=4,EastM=10,NorthM=20,
            Positions=new float[]{0,2,0,3,4,0,0,6,3},Normals=new float[]{0,1,0,0,1,0,0,1,0},
            Colors=new float[]{.1f,.2f,.3f,1,.1f,.2f,.3f,1,.1f,.2f,.3f,1},
            SurfaceWeights=new float[]{.1f,.2f,.3f,.4f,.1f,.2f,.3f,.4f,.1f,.2f,.3f,.4f},
            GroundWeightsB=new float[]{.4f,.3f,.2f,.1f,.4f,.3f,.2f,.1f,.4f,.3f,.2f,.1f},
            Lithology=new float[]{.1f,.2f,.3f,.1f,.2f,.1f,.1f,.2f,.3f,.1f,.2f,.1f,.1f,.2f,.3f,.1f,.2f,.1f},
            Triangles=new[]{0,1,2},WaterPositions=new float[]{0,3,0,3,3,0,0,3,3},
            WaterColors=new float[]{0,0,1,1,0,0,1,1,0,0,1,1},WaterTriangles=new[]{0,1,2}};
        using var pool=new TerrainUploadBufferPool();
        LocalTerrainMesh Lease()
        {
            using var stream=new System.IO.MemoryStream();
            using(var writer=new System.IO.BinaryWriter(stream,System.Text.Encoding.UTF8,true))TerrainMeshWire.WriteMesh(writer,source);
            stream.Position=0;using var reader=new System.IO.BinaryReader(stream);return TerrainMeshWire.ReadMesh(reader,pool);
        }
        var borrowed=Lease();var task=TerrainUpload.Schedule(()=>borrowed,CancellationToken.None);
        while(!task.IsCompleted)yield return null;
        if(borrowed.Positions.Length!=0)throw new InvalidOperationException("Native upload did not return transport attributes");
        // Overwrite the returned arrays before Apply. The native prepared mesh
        // must already own independent vertex/index data at this point.
        var reused=Lease();for(int i=0;i<reused.Positions.Length;i++)reused.Positions[i]=999;
        using(var upload=task.GetAwaiter().GetResult())
        {
            var meshes=upload.Apply();
            try
            {
                var a=new System.Collections.Generic.List<Vector4>();var b=new System.Collections.Generic.List<Vector2>();
                meshes[0].GetUVs(4,a);meshes[0].GetUVs(5,b);
                if(meshes[0].vertexCount!=3||meshes[1].vertexCount!=3||meshes[0].vertices[1]!=new Vector3(3,4,0)
                    ||meshes[0].normals[1]!=Vector3.up||meshes[0].triangles[2]!=2||a[0]!=new Vector4(.1f,.2f,.3f,.1f)
                    ||b[0]!=new Vector2(.2f,.1f)||meshes[0].colors[0]!=new Color(.1f,.2f,.3f,1)
                    ||!meshes[0].bounds.Contains(new Vector3(0,6,3))||upload.Heights[2]!=6||meshes[1].vertices[0].y!=3)
                    throw new InvalidOperationException("Worker-prepared terrain upload changed vertex/index/material/bounds data.");
            }
            finally{foreach(var mesh in meshes)UnityEngine.Object.Destroy(mesh);}
        }
        reused.ReleaseUploadBuffers();
        bool cancelled=false;
        try{using(var ignored=TerrainUpload.Prepare(source,new CancellationToken(true))){} }catch(OperationCanceledException){cancelled=true;}
        if(!cancelled)throw new InvalidOperationException("Cancelled native buffer preparation was not cancelled/disposed.");
        var cancelledTask=TerrainUpload.Schedule(()=>source,new CancellationToken(true));while(!cancelledTask.IsCompleted)yield return null;
        cancelled=false;try{cancelledTask.GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
        if(!cancelled)throw new InvalidOperationException("Queued cancelled upload retained its native allocation.");
        double releaseDeadline=Time.realtimeSinceStartupAsDouble+3;while(TerrainUpload.PendingReleases>0&&Time.realtimeSinceStartupAsDouble<releaseDeadline)yield return null;
        if(TerrainUpload.PendingReleases!=0)throw new InvalidOperationException("Cancelled mesh containers did not release on the Unity main thread.");
        foreach(int count in new[]{65535,65536,70003})
        {
            var large=new LocalTerrainMesh{GroundVertices=3,Segments=4,Positions=new float[count*3],Normals=new float[count*3],Colors=new float[count*4],SurfaceWeights=new float[count*4],GroundWeightsB=new float[count*4],Lithology=new float[count*6],Triangles=new[]{0,count-2,count-1}};
            using(var upload=TerrainUpload.Prepare(large,CancellationToken.None))
            {var meshes=upload.Apply();try{if(meshes[0].indexFormat!=(count<=65535?IndexFormat.UInt16:IndexFormat.UInt32)||meshes[0].triangles[2]!=count-1)throw new InvalidOperationException("Final terrain geometry exceeded compact index format");}finally{foreach(var mesh in meshes)UnityEngine.Object.Destroy(mesh);}}
        }
        yield return CheckResidentCoverOwnership(source,output);
        yield return CheckReusedTerrainMeshes(source,output);
        yield return CheckInactiveTerrainMeshRelease(output);
        System.IO.File.WriteAllText(System.IO.Path.Combine(output,"upload-buffers.txt"),"PASS worker-prepared native positions, normals, colors, lithology, indices, bounds, water, controller heights, returned/overwritten transport arrays and cancelled-buffer disposal.");
    }
    static System.Collections.IEnumerator CheckReusedTerrainMeshes(LocalTerrainMesh source,string output)
    {
        using var pool=new TerrainMeshPool();long compared=0;Mesh first=null;
        void Equal(Mesh expected,Mesh actual)
        {
            if(expected.vertexCount>actual.vertexCount||expected.indexFormat!=actual.indexFormat||expected.bounds!=actual.bounds||!expected.GetSubMesh(0).Equals(actual.GetSubMesh(0)))
                throw new InvalidOperationException("Reused terrain mesh shape/bounds differ");
            using var a=Mesh.AcquireReadOnlyMeshData(expected);using var b=Mesh.AcquireReadOnlyMeshData(actual);
            void Bytes(NativeArray<byte> left,NativeArray<byte> right)
            {if(left.Length>right.Length)throw new InvalidOperationException("Reused mesh buffer is too short");for(int i=0;i<left.Length;i++){compared++;if(left[i]!=right[i])throw new InvalidOperationException("Reused mesh byte differs at "+i);}}
            Bytes(a[0].GetVertexData<byte>(),b[0].GetVertexData<byte>());Bytes(a[0].GetIndexData<byte>(),b[0].GetIndexData<byte>());
        }
        for(int round=0;round<3;round++)
        {
            source.Positions[1]+=1.25f;source.Colors[0]+=.125f;source.WaterPositions[1]+=.5f;
            using var original=TerrainUpload.Prepare(source,CancellationToken.None);
            using var replacement=TerrainUpload.Prepare(source,CancellationToken.None);
            var expected=original.Apply();var actual=replacement.Apply(pool);
            try
            {
                if(round==0)first=actual[0];else if(actual[0]!=first)throw new InvalidOperationException("Matching retired mesh was recreated instead of reused");
                for(int part=0;part<actual.Length;part++)
                {
                    Equal(expected[part],actual[part]);
                    using var gpu=actual[part].GetVertexBuffer(0);
                    var readback=AsyncGPUReadback.Request(gpu);while(!readback.done)yield return null;
                    if(readback.hasError)throw new InvalidOperationException("Reused terrain GPU readback failed");
                    using var reference=Mesh.AcquireReadOnlyMeshData(expected[part]);
                    var referenceBytes=reference[0].GetVertexData<byte>();var gpuBytes=readback.GetData<byte>();
                    if(referenceBytes.Length>gpuBytes.Length)throw new InvalidOperationException("Reused terrain GPU buffer is too short");
                    for(int i=0;i<referenceBytes.Length;i++){compared++;if(referenceBytes[i]!=gpuBytes[i])throw new InvalidOperationException("Reused terrain GPU payload is stale");}
                }
            }
            finally{foreach(var mesh in expected)UnityEngine.Object.Destroy(mesh);foreach(var mesh in actual)if(!pool.Release(mesh))throw new InvalidOperationException("Terrain lease was lost");}
            // A newly released buffer must not be borrowed in the same frame.
            var quarantined=pool.Acquire(3,3,IndexFormat.UInt16);
            if(quarantined==first)throw new InvalidOperationException("Terrain buffer quarantine was bypassed");
            pool.Release(quarantined);
            for(int frame=0;frame<4;frame++)yield return null;
        }
        if(pool.Reused<4||pool.Bytes>32L*1024*1024)throw new InvalidOperationException("Terrain reuse did not occur or exceeded its bound");
        var changedShape=new LocalTerrainMesh{GroundVertices=4,Segments=4,Positions=new float[]{0,1,0,1,2,0,1,3,1,0,4,1},Normals=new float[12],Colors=new float[16],SurfaceWeights=new float[16],GroundWeightsB=new float[16],Lithology=new float[24],Triangles=new[]{0,2,3}};
        using(var original=TerrainUpload.Prepare(changedShape,CancellationToken.None))
        using(var replacement=TerrainUpload.Prepare(changedShape,CancellationToken.None))
        {
            var expected=original.Apply();var actual=replacement.Apply(pool);
            try
            {
                Equal(expected[0],actual[0]);if(actual[0]!=first)throw new InvalidOperationException("Size-class reuse missed changed geometry within the same capacity");
                using var vertices=actual[0].GetVertexBuffer(0);using var indices=actual[0].GetIndexBuffer();
                var vertexRead=AsyncGPUReadback.Request(vertices);var indexRead=AsyncGPUReadback.Request(indices);
                while(!vertexRead.done||!indexRead.done)yield return null;
                if(vertexRead.hasError||indexRead.hasError)throw new InvalidOperationException("Changed-size terrain GPU readback failed");
                using var reference=Mesh.AcquireReadOnlyMeshData(expected[0]);
                var expectedVertices=reference[0].GetVertexData<byte>();var expectedIndices=reference[0].GetIndexData<byte>();
                var actualVertices=vertexRead.GetData<byte>();var actualIndices=indexRead.GetData<byte>();
                for(int i=0;i<expectedVertices.Length;i++){compared++;if(expectedVertices[i]!=actualVertices[i])throw new InvalidOperationException("Changed-size GPU vertices stale");}
                for(int i=0;i<expectedIndices.Length;i++){compared++;if(expectedIndices[i]!=actualIndices[i])throw new InvalidOperationException("Changed-size GPU topology stale");}
            }
            finally{foreach(var mesh in expected)UnityEngine.Object.Destroy(mesh);foreach(var mesh in actual)pool.Release(mesh);}
        }
        foreach(int vertices in new[]{65535,65536,70003})
        {
            var large=new LocalTerrainMesh{GroundVertices=3,Segments=4,Positions=new float[vertices*3],Normals=new float[vertices*3],Colors=new float[vertices*4],SurfaceWeights=new float[vertices*4],GroundWeightsB=new float[vertices*4],Lithology=new float[vertices*6],Triangles=new[]{0,vertices-2,vertices-1}};
            using var original=TerrainUpload.Prepare(large,CancellationToken.None);using var replacement=TerrainUpload.Prepare(large,CancellationToken.None);
            var expected=original.Apply();var actual=replacement.Apply(pool);
            try{if(actual.Length!=1)throw new InvalidOperationException("Dry terrain unexpectedly created a water mesh");Equal(expected[0],actual[0]);}
            finally{foreach(var mesh in expected)UnityEngine.Object.Destroy(mesh);foreach(var mesh in actual)pool.Release(mesh);}
        }
        using(var tiny=new TerrainMeshPool(1))
        {
            var rejected=tiny.Acquire(3,3,IndexFormat.UInt16);tiny.Release(rejected);
            if(tiny.Bytes!=0||tiny.Retained!=0)throw new InvalidOperationException("Oversized terrain buffer retained");
            yield return null;if(rejected!=null)throw new InvalidOperationException("Oversized terrain buffer not destroyed");
        }
        using(var bounded=new TerrainMeshPool(1400))
        {
            var a=bounded.Acquire(3,3,IndexFormat.UInt16);var b=bounded.Acquire(3,3,IndexFormat.UInt16);var c=bounded.Acquire(3,3,IndexFormat.UInt16);
            bounded.Release(a);bounded.Release(b);bounded.Release(c);
            var large=bounded.Acquire(5,3,IndexFormat.UInt16);bounded.Release(large);
            if(bounded.Retained!=2||bounded.Bytes>1400)throw new InvalidOperationException("Large retirement burst-evicted multiple small meshes");
            yield return null;
            if(a!=null||large!=null||b==null||c==null)throw new InvalidOperationException("Bounded mesh eviction destroyed the wrong resources");
        }
        int reused=pool.Reused;pool.Dispose();yield return null;
        if(first!=null||pool.Bytes!=0)throw new InvalidOperationException("Disposed terrain pool retained meshes");
        System.IO.File.WriteAllText(System.IO.Path.Combine(output,"reused-terrain-meshes.txt"),"PASS "+compared+" exact CPU/GPU byte comparisons, wet/dry geometry, 16/32-bit indices, quarantine, ownership, capacity and disposal; reused="+reused+". GPU resource creation/frame-time benefit requires presented measurement.\n");
    }
    static System.Collections.IEnumerator CheckResidentCoverOwnership(LocalTerrainMesh source,string output)
    {
        source.GroundCover=new float[source.GroundVertices*16];source.MidCover=new float[source.GroundVertices*6];source.ForestCover=new float[source.GroundVertices*8];
        foreach(var grid in new[]{source.GroundCover,source.MidCover,source.ForestCover})for(int i=0;i<grid.Length;i++)grid[i]=(i+1)*.03125f;
        // Encoded morphology and signed zero must survive as bits, not merely
        // equal numeric values. The grid is also read back from a real GPU buffer.
        source.ForestCover[7]=BitConverter.Int32BitsToSingle(unchecked((int)0xffc12345));source.GroundCover[0]=-0f;
        long nativeBefore=ResidentCoverBuffers.ResidentBytes;
        long expectedBytes=(long)(source.GroundCover.Length+source.MidCover.Length+source.ForestCover.Length)*sizeof(float);
        using var transport=new TerrainUploadBufferPool();using var covers=new TerrainUploadBufferPool();
        LocalTerrainMesh Lease()
        {
            using var stream=new System.IO.MemoryStream();using(var writer=new System.IO.BinaryWriter(stream,System.Text.Encoding.UTF8,true))TerrainMeshWire.WriteMesh(writer,source);
            stream.Position=0;using var reader=new System.IO.BinaryReader(stream);return TerrainMeshWire.ReadMesh(reader,transport,covers);
        }
        void Exact(Tile tile)
        {
            var actual=new[]{tile.Cover,tile.MidCover,tile.ForestCover};var expected=new[]{source.GroundCover,source.MidCover,source.ForestCover};
            for(int g=0;g<actual.Length;g++)for(int i=0;i<expected[g].Length;i++)if(BitConverter.SingleToInt32Bits(actual[g][i])!=BitConverter.SingleToInt32Bits(expected[g][i]))throw new InvalidOperationException("Resident cover changed before retirement");
        }
        var host=new GameObject("Resident cover ownership proof");var explorer=host.AddComponent<LocalTerrainExplorer>();explorer.enabled=false;
        Tile tile=null;var key=new TerrainTileKey(0,0,0);var edges=new double[4];
        try
        {
            var task=TerrainUpload.Schedule(Lease,CancellationToken.None);while(!task.IsCompleted)yield return null;
            using(var upload=task.GetAwaiter().GetResult())
            {
                var meshes=upload.Apply();var root=new GameObject("Resident cover tile");
                foreach(var mesh in meshes){var child=new GameObject("Uploaded mesh",typeof(MeshFilter));child.transform.SetParent(root.transform,false);child.GetComponent<MeshFilter>().sharedMesh=mesh;}
                tile=new Tile{Root=root,Segments=source.Segments,Edges=edges,Cover=upload.Cover,MidCover=upload.MidCover,ForestCover=upload.ForestCover,CoverBuffers=upload.TakeCoverBuffers()};
            }
            Exact(tile);
            if(covers.RetainedBytes!=expectedBytes||ResidentCoverBuffers.ResidentBytes!=nativeBefore+expectedBytes)throw new InvalidOperationException("Native resident grids did not release managed transport leases immediately");
            using(var gpu=new GraphicsBuffer(GraphicsBuffer.Target.Structured,tile.ForestCover.Length,sizeof(float)))
            {
                gpu.SetData(tile.ForestCover);var readback=new float[tile.ForestCover.Length];gpu.GetData(readback);
                for(int i=0;i<readback.Length;i++)if(BitConverter.SingleToInt32Bits(readback[i])!=BitConverter.SingleToInt32Bits(source.ForestCover[i]))throw new InvalidOperationException("Native cover GPU upload altered encoded values");
            }
            explorer.CacheTerrain(key,tile);Exact(tile);
            if(ResidentCoverBuffers.ResidentBytes!=nativeBefore+expectedBytes)throw new InvalidOperationException("Caching released native resident cover");
            var other=Lease();foreach(var grid in new[]{other.GroundCover,other.MidCover,other.ForestCover})for(int i=0;i<grid.Length;i++)grid[i]=999;
            Exact(tile);other.ReleaseUploadBuffers();
            if(!explorer.TerrainProducts.Take(new TerrainProductKey(key,source.Segments,edges),out var restored)||!ReferenceEquals(restored,tile))throw new InvalidOperationException("Cover tile cache did not preserve ownership");
            Exact(restored);explorer.DestroyTile(tile);
            if(ResidentCoverBuffers.ResidentBytes!=nativeBefore||tile.Cover.IsCreated||tile.MidCover.IsCreated||tile.ForestCover.IsCreated)throw new InvalidOperationException("Actual retirement did not release native cover ownership");tile=null;
            var cancelled=TerrainUpload.Schedule(()=>{var m=Lease();m.Triangles[0]=-1;return m;},CancellationToken.None);
            while(!cancelled.IsCompleted)yield return null;
            bool failed=false;try{cancelled.GetAwaiter().GetResult();}catch(InvalidOperationException){failed=true;}
            if(!failed)throw new InvalidOperationException("Invalid native upload unexpectedly succeeded");
            if(ResidentCoverBuffers.ResidentBytes!=nativeBefore)throw new InvalidOperationException("Failed upload stranded native cover allocation");
            using(var cancel=new CancellationTokenSource())
            {
                var stopped=TerrainUpload.Schedule(()=>{var m=Lease();cancel.Cancel();return m;},cancel.Token);
                while(!stopped.IsCompleted)yield return null;
                bool observed=false;try{stopped.GetAwaiter().GetResult();}catch(OperationCanceledException){observed=true;}
                if(!observed||ResidentCoverBuffers.ResidentBytes!=nativeBefore)throw new InvalidOperationException("Cancellation during preparation retained native cover");
            }
            long warmed=covers.CreatedBytes;var recovered=Lease();recovered.ReleaseUploadBuffers();
            if(covers.CreatedBytes!=warmed)throw new InvalidOperationException("Failed native preparation stranded cover buffers");
            System.IO.File.WriteAllText(System.IO.Path.Combine(output,"resident-cover-ownership.txt"),"PASS actual worker-owned native ground/mid/forest buffers: immediate managed lease return, bit-exact GPU readback including encoded NaN and signed zero, cache retention/take, competing overwritten managed leases, tile retirement, failed preparation and in-flight cancellation. Native bytes return to baseline; pool reuses released transport buffers.");
        }
        finally{explorer.terrainProducts?.Clear();if(tile!=null)explorer.DestroyTile(tile);UnityEngine.Object.Destroy(host);}
        yield return null;
    }
    static System.Collections.IEnumerator CheckInactiveTerrainMeshRelease(string output)
    {
        var root=new GameObject("Inactive terrain retirement proof");var meshes=new System.Collections.Generic.List<Mesh>();
        Mesh Triangle()=>new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.forward},triangles=new[]{0,1,2}};
        var unrelated=Triangle();var flat=new GameObject("Original flat terrain retirement control");var flatMeshes=new System.Collections.Generic.List<Mesh>();
        try
        {
            Transform parent=root.transform;
            for(int i=0;i<3;i++)
            {
                var child=new GameObject("Land/water child "+i,typeof(MeshFilter));child.transform.SetParent(parent,false);
                var mesh=Triangle();child.GetComponent<MeshFilter>().sharedMesh=mesh;meshes.Add(mesh);
                if(i==1)child.SetActive(false);parent=child.transform;
            }
            root.SetActive(false);
            int oldQuery=root.GetComponentsInChildren<MeshFilter>().Length;
            for(int i=0;i<2;i++)
            {var child=new GameObject("Original land/water part "+i,typeof(MeshFilter));child.transform.SetParent(flat.transform,false);var mesh=Triangle();child.GetComponent<MeshFilter>().sharedMesh=mesh;flatMeshes.Add(mesh);}
            flat.SetActive(false);var originalFilters=flat.GetComponentsInChildren<MeshFilter>();
            foreach(var filter in originalFilters)UnityEngine.Object.Destroy(filter.sharedMesh);UnityEngine.Object.Destroy(flat);
            int released=ReleaseTerrainMeshes(root,new System.Collections.Generic.List<MeshFilter>());UnityEngine.Object.Destroy(root);
            yield return null;yield return null;
            foreach(var mesh in meshes)if(mesh!=null)throw new InvalidOperationException("Retired inactive terrain retained a generated native mesh");
            if(unrelated==null||released!=3)throw new InvalidOperationException("Terrain retirement released an unrelated mesh or missed a nested child");
            int flatRemaining=0;foreach(var mesh in flatMeshes)if(mesh!=null)flatRemaining++;
            System.IO.File.WriteAllText(System.IO.Path.Combine(output,"terrain-mesh-retirement.txt"),"PASS includeInactive release destroyed3 nested meshes and retained unrelated control. Original nested query count="+oldQuery+"/3; original actual flat terrain query count="+originalFilters.Length+"/2; original flat meshes remaining after deferred deletion="+flatRemaining+". Root-disabled omission diagnosis is NOT established unless the flat control retains meshes.\n");
        }
        finally{if(root!=null)UnityEngine.Object.Destroy(root);if(flat!=null)UnityEngine.Object.Destroy(flat);foreach(var mesh in meshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);foreach(var mesh in flatMeshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(unrelated);}
    }
}
