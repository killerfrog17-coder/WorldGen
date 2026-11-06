using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckFaunaTextureUpload(string input,string output)
    {
        enabled=false;Directory.CreateDirectory(output);QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
        var cameraRoot=new GameObject("Upload fixture camera");cameraRoot.AddComponent<Camera>();
        for(int i=0;i<60;i++)yield return null;
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--fauna-reference-directory");
        string reference=at>=0?Path.GetFullPath(args[at+1]):Path.Combine(Application.streamingAssetsPath,"fauna");
        bool multiple=Directory.Exists(input);
        var files=multiple?Directory.GetFiles(input,"*.coats").OrderBy(p=>p,StringComparer.Ordinal).ToArray():new[]{input};
        if(files.Length==0)throw new InvalidOperationException("No coat streams to verify");
        foreach(string file in files)
        {
            string slug=Path.GetFileNameWithoutExtension(file),destination=multiple?Path.Combine(output,slug):output;
            yield return CheckOneFaunaTextureUpload(file,destination,Path.Combine(reference,slug+".fauna"));
        }
        if(multiple)File.WriteAllText(Path.Combine(output,"complete.txt"),$"PASS {files.Length} animal coat arrays: each native all-layer/mip comparison and lifetime/error checks passed.\n");
        Destroy(cameraRoot);Application.Quit(0);
    }
    IEnumerator CheckOneFaunaTextureUpload(string input,string output,string originalPath)
    {
        Directory.CreateDirectory(output);
        var rows=new List<string>{"phase,frame_ms"};string phase="original-bundle";
        var bundleRequest=AssetBundle.LoadFromFileAsync(originalPath);
        while(!bundleRequest.isDone){yield return null;rows.Add(phase+","+(Time.unscaledDeltaTime*1000).ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
        var bundle=bundleRequest.assetBundle;var assetRequest=bundle.LoadAllAssetsAsync<WorldGenFaunaAsset>();
        while(!assetRequest.isDone){yield return null;rows.Add(phase+","+(Time.unscaledDeltaTime*1000).ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
        var originalAsset=assetRequest.allAssets.OfType<WorldGenFaunaAsset>().Single();var original=originalAsset.Coats;
        if(original==null)throw new InvalidDataException("Comparison requires the original embedded coat array: "+originalPath);
        for(int i=0;i<10;i++){yield return null;rows.Add(phase+","+(Time.unscaledDeltaTime*1000).ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
        var read=Task.Run(()=>new FaunaTextureUpload(input));while(!read.IsCompleted)yield return null;
        using var upload=read.Result;var iterator=upload.Upload();phase="sliced-upload";
        while(iterator.MoveNext()){yield return iterator.Current;rows.Add(phase+","+(Time.unscaledDeltaTime*1000).ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
        for(int i=0;i<10;i++){yield return null;rows.Add(phase+","+(Time.unscaledDeltaTime*1000).ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
        File.WriteAllText(Path.Combine(output,"memory.txt"),
            $"threaded_creation={Texture.allowThreadedTextureCreation}\neager_creation={FaunaTextureUpload.EagerResourceCreation}\n"+
            $"original_native_bytes={UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(original)}\n"+
            $"streamed_native_bytes={UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(upload.Result)}\n"+
            $"original_readable={original.isReadable}\nstreamed_readable={upload.Result.isReadable}\n");
        File.WriteAllLines(Path.Combine(output,"uploads.csv"),rows);
        long comparisons=0;var compare=Resources.Load<ComputeShader>("FaunaTextureCompare");
        if(compare==null)throw new InvalidOperationException("Missing coat comparison kernel");
        using var differences=new GraphicsBuffer(GraphicsBuffer.Target.Structured,1,4);differences.SetData(new uint[]{0});
        compare.SetBuffer(0,"_Mismatch",differences);compare.SetTexture(0,"_Original",original);compare.SetTexture(0,"_Current",upload.Result);compare.SetInt("_CurrentLayerOffset",0);
        for(int mip=0;mip<original.mipmapCount;mip++)
        {
            int width=Math.Max(1,original.width>>mip);compare.SetInt("_Width",width);compare.SetInt("_Mip",mip);
            for(int layer=0;layer<original.depth;layer++)
            {
                compare.SetInt("_Layer",layer);compare.Dispatch(0,(width+7)/8,(width+7)/8,1);comparisons+=(long)width*width*4;
            }
        }
        var readback=AsyncGPUReadback.Request(differences);while(!readback.done)yield return null;
        if(readback.hasError||readback.GetData<uint>()[0]!=0)throw new InvalidDataException("Coat texel mismatch: "+(readback.hasError?"readback error":readback.GetData<uint>()[0].ToString()));
        compare.SetInt("_CurrentLayerOffset",1);compare.SetInt("_Layer",0);compare.SetInt("_Mip",0);compare.SetInt("_Width",original.width);
        compare.Dispatch(0,(original.width+7)/8,(original.width+7)/8,1);
        var positive=AsyncGPUReadback.Request(differences);while(!positive.done)yield return null;
        if(positive.hasError||positive.GetData<uint>()[0]==0)throw new InvalidOperationException("Coat comparison failed its deliberately wrong-layer control");
        // The streamed package also repacks the model. Compare its authored
        // motion/metadata and actual native mesh bytes, not just texture pixels.
        string streamedPath=Path.ChangeExtension(input,".fauna");
        if(File.Exists(streamedPath))
        {
            // Unity identifies both bundles by their serialized-file identity.
            // Snapshot the original before unloading it, then load the repack.
            var expectedSlug=originalAsset.Slug;var expectedSource=originalAsset.SourceSha256;
            var expectedBones=originalAsset.BoneCount;var expectedHeight=originalAsset.Height;
            var expectedPoses=originalAsset.Poses;var expectedClips=originalAsset.Clips;
            var expectedVariations=originalAsset.Variations;var expectedMorphs=originalAsset.Morphs;
            FaunaMeshSnapshot expectedMesh=null;
            yield return ReadFaunaMeshBuffers(originalAsset.Mesh,value=>expectedMesh=value);
            bundle.Unload(true);bundle=null;
            var streamedRequest=AssetBundle.LoadFromFileAsync(streamedPath);yield return streamedRequest;
            var streamedBundle=streamedRequest.assetBundle;
            if(streamedBundle==null)throw new InvalidDataException("Missing streamed model bundle");
            var modelRequest=streamedBundle.LoadAllAssetsAsync<WorldGenFaunaAsset>();yield return modelRequest;
            var current=modelRequest.allAssets.OfType<WorldGenFaunaAsset>().Single();
            if(current.Coats!=null||current.Slug!=expectedSlug||current.SourceSha256!=expectedSource||current.BoneCount!=expectedBones||current.Height!=expectedHeight||
                !current.Poses.SequenceEqual(expectedPoses)||!SameItems(current.Clips,expectedClips)||!SameItems(current.Variations,expectedVariations)||
                !(current.Morphs??Array.Empty<float>()).SequenceEqual(expectedMorphs??Array.Empty<float>()))
                throw new InvalidDataException("Streamed model metadata or authored motion changed: "+current.Slug);
            FaunaMeshSnapshot actualMesh=null;yield return ReadFaunaMeshBuffers(current.Mesh,value=>actualMesh=value);
            if(!expectedMesh.SameAs(actualMesh))throw new InvalidDataException("Streamed native mesh bytes or layout changed: "+current.Slug);
            File.WriteAllText(Path.Combine(output,"model.txt"),"PASS authored motion, coat metadata, source identity and all native mesh vertex/index bytes.\n");
            streamedBundle.Unload(true);
        }
        var cancelRead=Task.Run(()=>new FaunaTextureUpload(input));while(!cancelRead.IsCompleted)yield return null;
        var canceled=cancelRead.Result;var cancelIterator=canceled.Upload();cancelIterator.MoveNext();var discarded=canceled.Result;
        canceled.Dispose();double deadline=Time.realtimeSinceStartupAsDouble+5;
        while(!canceled.Completion.IsCompleted&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        yield return null;
        if(!canceled.Completion.IsCompleted||discarded!=null)throw new InvalidOperationException("Coat cancellation retained its worker or native array");
        string truncated=Path.Combine(output,"truncated.coats");
        using(var file=File.OpenRead(input)){var prefix=new byte[1024];file.Read(prefix,0,prefix.Length);File.WriteAllBytes(truncated,prefix);}
        var badRead=Task.Run(()=>new FaunaTextureUpload(truncated));while(!badRead.IsCompleted)yield return null;
        using(var bad=badRead.Result)
        {
            deadline=Time.realtimeSinceStartupAsDouble+5;while(!bad.Completion.IsCompleted&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(!bad.Completion.IsFaulted||!(bad.Completion.Exception.GetBaseException() is EndOfStreamException))throw new InvalidOperationException("Truncated coat file was not rejected");
        }
        File.WriteAllLines(Path.Combine(output,"uploads.csv"),rows);
        File.WriteAllText(Path.Combine(output,"complete.txt"),$"PASS {comparisons} exact native decoded RGBA channels across BC7 layers/mips, {upload.Pieces} bounded uploads, {upload.RetainedBufferBytes} reader bytes; canceled worker/native array release and truncated source rejection. Isolated fixture timings are not game FPS.\n");
        if(bundle!=null)bundle.Unload(true);
    }
    static bool SameItems<T>(T[] a,T[] b)=>a.Length==b.Length&&a.Zip(b,(x,y)=>JsonUtility.ToJson(x)==JsonUtility.ToJson(y)).All(equal=>equal);
    sealed class FaunaMeshSnapshot
    {
        public int Vertices;public IndexFormat Index;public Bounds Bounds;
        public VertexAttributeDescriptor[] Attributes;public SubMeshDescriptor[] Submeshes;
        public readonly List<byte[]> Buffers=new List<byte[]>();
        public bool SameAs(FaunaMeshSnapshot other)=>Vertices==other.Vertices&&Index==other.Index&&Bounds.Equals(other.Bounds)&&
            Attributes.SequenceEqual(other.Attributes)&&Submeshes.SequenceEqual(other.Submeshes)&&Buffers.Count==other.Buffers.Count&&
            Buffers.Zip(other.Buffers,(a,b)=>a.SequenceEqual(b)).All(equal=>equal);
    }
    static IEnumerator ReadFaunaMeshBuffers(Mesh mesh,Action<FaunaMeshSnapshot> complete)
    {
        var result=new FaunaMeshSnapshot{Vertices=mesh.vertexCount,Index=mesh.indexFormat,Bounds=mesh.bounds,Attributes=mesh.GetVertexAttributes(),
            Submeshes=Enumerable.Range(0,mesh.subMeshCount).Select(mesh.GetSubMesh).ToArray()};
        for(int stream=-1;stream<mesh.vertexBufferCount;stream++)
        {
            using var buffer=stream<0?mesh.GetIndexBuffer():mesh.GetVertexBuffer(stream);
            var read=AsyncGPUReadback.Request(buffer);while(!read.done)yield return null;
            if(read.hasError)throw new InvalidOperationException("Fauna native mesh readback failed");
            result.Buffers.Add(read.GetData<byte>().ToArray());
        }
        complete(result);
    }
}
