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

    // MeshData is explicitly thread-accessible. Build native upload buffers on
    // the existing terrain worker; the main thread only applies them and binds
    // Unity objects. Do not force synchronous UploadMeshData on every new tile.
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
        public double EastM,NorthM,BuildMilliseconds,BufferMilliseconds;
        public int Segments;
        public float[] Heights;
        public bool HasWater=>bounds.Length==2;

        public static TerrainUpload Prepare(LocalTerrainMesh source,CancellationToken token)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            var result=new TerrainUpload{EastM=source.EastM,NorthM=source.NorthM,Segments=source.Segments,BuildMilliseconds=source.BuildMilliseconds,
                Heights=new float[source.GroundVertices],bounds=new Bounds[source.WaterTriangles.Length>0?2:1]};
            result.buffers=Mesh.AllocateWritableMeshData(result.bounds.Length);result.live=1;
            try
            {
                for(int i=0;i<result.Heights.Length;i++)result.Heights[i]=source.Positions[i*3+1];
                for(int part=0;part<result.bounds.Length;part++)
                {
                    bool water=part==1;
                    var positions=water?source.WaterPositions:source.Positions;
                    var colors=water?source.WaterColors:source.Colors;
                    var indices=water?source.WaterTriangles:source.Triangles;
                    var data=result.buffers[part];int count=positions.Length/3;
                    data.SetVertexBufferParams(count,Layout);data.SetIndexBufferParams(indices.Length,IndexFormat.UInt32);
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
                    data.GetIndexData<int>().CopyFrom(indices);
                    result.bounds[part]=new Bounds((low+high)*.5f,high-low);
                    data.subMeshCount=1;
                    data.SetSubMesh(0,new SubMeshDescriptor(0,indices.Length,MeshTopology.Triangles){bounds=result.bounds[part],vertexCount=count},
                        MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);
                }
                result.BufferMilliseconds=watch.Elapsed.TotalMilliseconds;return result;
            }
            catch{result.Dispose();throw;}
        }
        public Mesh[] Apply()
        {
            if(live==0)throw new ObjectDisposedException(nameof(TerrainUpload));
            var meshes=new Mesh[bounds.Length];for(int i=0;i<meshes.Length;i++)meshes[i]=new Mesh{indexFormat=IndexFormat.UInt32};
            try{Mesh.ApplyAndDisposeWritableMeshData(buffers,meshes,MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);}
            catch{foreach(var mesh in meshes)UnityEngine.Object.Destroy(mesh);throw;}
            live=0;for(int i=0;i<meshes.Length;i++)meshes[i].bounds=bounds[i];return meshes;
        }
        public void Dispose(){if(Interlocked.Exchange(ref live,0)!=0)buffers.Dispose();}
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
        var task=System.Threading.Tasks.Task.Run(()=>TerrainUpload.Prepare(source,CancellationToken.None));
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
        System.IO.File.WriteAllText(System.IO.Path.Combine(output,"upload-buffers.txt"),"PASS worker-prepared native positions, normals, colors, lithology, indices, bounds, water, controller heights and cancelled-buffer disposal.");
    }
}
