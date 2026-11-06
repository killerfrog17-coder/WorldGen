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
        var original=assetRequest.allAssets.OfType<WorldGenFaunaAsset>().Single().Coats;
        if(original==null)throw new InvalidDataException("Comparison requires the original embedded coat array: "+originalPath);
        for(int i=0;i<10;i++){yield return null;rows.Add(phase+","+(Time.unscaledDeltaTime*1000).ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
        var read=Task.Run(()=>new FaunaTextureUpload(input));while(!read.IsCompleted)yield return null;
        using var upload=read.Result;var iterator=upload.Upload();phase="sliced-upload";
        while(iterator.MoveNext()){yield return iterator.Current;rows.Add(phase+","+(Time.unscaledDeltaTime*1000).ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
        for(int i=0;i<10;i++){yield return null;rows.Add(phase+","+(Time.unscaledDeltaTime*1000).ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
        File.WriteAllText(Path.Combine(output,"memory.txt"),
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
        bundle.Unload(true);
    }
}
