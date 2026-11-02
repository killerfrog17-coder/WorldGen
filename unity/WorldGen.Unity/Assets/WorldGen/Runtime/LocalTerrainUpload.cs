using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    [StructLayout(LayoutKind.Sequential)]
    struct TerrainVertex
    {
        public Vector3 Position,Normal;
        public Color Color;
        public Vector2 Uv;
        public Vector4 GroundA,GroundB,RockA;
        public Vector2 RockB;
    }

    // Allocate the native container on the main thread. Its MeshData can then
    // be filled by the existing worker without constructing Unity objects there.
    sealed class TerrainUpload : IDisposable
    {
        static readonly VertexAttributeDescriptor[] Layout={
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
            new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3),
            new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord4,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord5,VertexAttributeFormat.Float32,2)};
        Mesh.MeshDataArray buffers;
        int live;
        Bounds[] bounds;
        IndexFormat[] formats;
        public double EastM,NorthM,BuildMilliseconds,BufferMilliseconds;
        public int Segments;
        public long BufferBytes;
        public float[] Heights,Cover,MidCover,ForestCover;
        bool hasWater;
        public bool HasWater=>hasWater;
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
            return System.Threading.Tasks.Task.Run(()=>{try{token.ThrowIfCancellationRequested();return Prepare(build(),token,allocated);}catch{allocated.Dispose();throw;}});
        }

        public static TerrainUpload Prepare(LocalTerrainMesh source,CancellationToken token)
        {token.ThrowIfCancellationRequested();return Prepare(source,token,Allocate());}
        static TerrainUpload Prepare(LocalTerrainMesh source,CancellationToken token,TerrainUpload result)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            result.EastM=source.EastM;result.NorthM=source.NorthM;result.Segments=source.Segments;result.BuildMilliseconds=source.BuildMilliseconds;
            result.Heights=new float[source.GroundVertices];result.Cover=source.GroundCover;result.MidCover=source.MidCover;result.ForestCover=source.ForestCover;result.hasWater=source.WaterTriangles.Length>0;result.bounds=new Bounds[2];
            result.formats=new IndexFormat[2];
            try
            {
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
                    if(compact){var target=data.GetIndexData<ushort>();for(int i=0;i<indices.Length;i++)target[i]=(ushort)indices[i];}
                    else data.GetIndexData<int>().CopyFrom(indices);
                    result.bounds[part]=new Bounds((low+high)*.5f,high-low);
                    if(water)result.bounds[part].Expand(new Vector3(0,3.2f,0)); // shader swell can move above/below the stored datum
                    data.subMeshCount=1;
                    data.SetSubMesh(0,new SubMeshDescriptor(0,indices.Length,MeshTopology.Triangles){bounds=result.bounds[part],vertexCount=count},
                        MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);
                }
                result.BufferBytes+=(long)(result.Heights.Length+result.Cover.Length+result.MidCover.Length+result.ForestCover.Length)*4;result.BufferMilliseconds=watch.Elapsed.TotalMilliseconds;return result;
            }
            catch{result.Dispose();throw;}
        }
        public Mesh[] Apply()
        {
            if(live==0)throw new ObjectDisposedException(nameof(TerrainUpload));
            var meshes=new Mesh[bounds.Length];for(int i=0;i<meshes.Length;i++)meshes[i]=new Mesh{indexFormat=formats[i]};
            try{Mesh.ApplyAndDisposeWritableMeshData(buffers,meshes,MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);}
            catch{foreach(var mesh in meshes)UnityEngine.Object.Destroy(mesh);throw;}
            live=0;for(int i=0;i<meshes.Length;i++)meshes[i].bounds=bounds[i];
            if(!hasWater){UnityEngine.Object.Destroy(meshes[1]);return new[]{meshes[0]};}return meshes;
        }
        public void Dispose()
        {
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
        var task=TerrainUpload.Schedule(()=>source,CancellationToken.None);
        while(!task.IsCompleted)yield return null;
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
        System.IO.File.WriteAllText(System.IO.Path.Combine(output,"upload-buffers.txt"),"PASS worker-prepared native positions, normals, colors, lithology, indices, bounds, water, controller heights and cancelled-buffer disposal.");
    }
}
