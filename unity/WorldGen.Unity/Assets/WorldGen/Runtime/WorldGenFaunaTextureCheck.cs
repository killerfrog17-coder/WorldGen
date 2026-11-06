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
        var rows=new List<string>{"phase,frame_ms"};string phase="warmup";
        var cameraRoot=new GameObject("Upload fixture camera");cameraRoot.AddComponent<Camera>();
        for(int i=0;i<60;i++)yield return null;
        phase="original-bundle";
        var bundleRequest=AssetBundle.LoadFromFileAsync(Path.Combine(Application.streamingAssetsPath,"fauna/hare.fauna"));
        while(!bundleRequest.isDone){yield return null;rows.Add(phase+","+(Time.unscaledDeltaTime*1000).ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
        var bundle=bundleRequest.assetBundle;var assetRequest=bundle.LoadAllAssetsAsync<WorldGenFaunaAsset>();
        while(!assetRequest.isDone){yield return null;rows.Add(phase+","+(Time.unscaledDeltaTime*1000).ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
        var original=assetRequest.allAssets.OfType<WorldGenFaunaAsset>().Single().Coats;
        for(int i=0;i<10;i++){yield return null;rows.Add(phase+","+(Time.unscaledDeltaTime*1000).ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
        var read=Task.Run(()=>new FaunaTextureUpload(input));while(!read.IsCompleted)yield return null;
        using var upload=read.Result;var iterator=upload.Upload();phase="sliced-upload";
        while(iterator.MoveNext()){yield return iterator.Current;rows.Add(phase+","+(Time.unscaledDeltaTime*1000).ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
        for(int i=0;i<10;i++){yield return null;rows.Add(phase+","+(Time.unscaledDeltaTime*1000).ToString("R",System.Globalization.CultureInfo.InvariantCulture));}
        File.WriteAllLines(Path.Combine(output,"uploads.csv"),rows);
        long comparisons=0;
        for(int mip=0;mip<original.mipmapCount;mip++)
        {
            var old=AsyncGPUReadback.Request(original,mip,TextureFormat.RGBA32);var current=AsyncGPUReadback.Request(upload.Result,mip,TextureFormat.RGBA32);
            while(!old.done||!current.done)yield return null;
            if(old.hasError||current.hasError)throw new InvalidOperationException("Coat native readback failed");
            for(int layer=0;layer<original.depth;layer++)
            {
                var a=old.GetData<byte>(layer);var b=current.GetData<byte>(layer);
                if(a.Length!=b.Length)throw new InvalidDataException("Coat native byte count");
                for(int i=0;i<a.Length;i++){if(a[i]!=b[i])throw new InvalidDataException($"Coat mismatch layer {layer}, mip {mip}, byte {i}: {a[i]}/{b[i]}");comparisons++;}
            }
        }
        File.WriteAllLines(Path.Combine(output,"uploads.csv"),rows);
        File.WriteAllText(Path.Combine(output,"complete.txt"),$"PASS {comparisons} exact native decoded RGBA bytes across BC7 layers/mips, {upload.Pieces} bounded uploads. Isolated fixture timings are not game FPS.\n");
        bundle.Unload(true);Destroy(cameraRoot);Application.Quit(0);
    }
}
