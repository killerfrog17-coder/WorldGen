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
        long comparisons=0;var compare=Resources.Load<ComputeShader>("FaunaTextureCompare");
        if(compare==null)throw new InvalidOperationException("Missing coat comparison kernel");
        using var differences=new GraphicsBuffer(GraphicsBuffer.Target.Structured,1,4);differences.SetData(new uint[]{0});
        compare.SetBuffer(0,"_Mismatch",differences);compare.SetTexture(0,"_Original",original);compare.SetTexture(0,"_Current",upload.Result);
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
        File.WriteAllLines(Path.Combine(output,"uploads.csv"),rows);
        File.WriteAllText(Path.Combine(output,"complete.txt"),$"PASS {comparisons} exact native decoded RGBA channels across BC7 layers/mips, {upload.Pieces} bounded uploads. Isolated fixture timings are not game FPS.\n");
        bundle.Unload(true);Destroy(cameraRoot);Application.Quit(0);
    }
}
