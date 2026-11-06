using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Stopwatch=System.Diagnostics.Stopwatch;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

// Experimental bounded native upload. Not used by normal fauna until the
// packaged render/byte-equivalence and moving-player comparisons pass.
public sealed class FaunaTextureUpload : IDisposable
{
    public static readonly string TrialDirectory=FindTrialDirectory();
    static string FindTrialDirectory()
    {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--worldgen-fauna-stream-directory");return i>=0&&i+1<args.Length?Path.GetFullPath(args[i+1]):null;}
    public readonly int Size,Layers,Mips;
    const int PageBytes=1024*64,PageCount=4;
    sealed class Page {public readonly byte[] Bytes=new byte[PageBytes];public int Layer,Mip,Width,Rows,Y,Length;}
    readonly BlockingCollection<Page> available=new BlockingCollection<Page>(PageCount),ready=new BlockingCollection<Page>(PageCount);
    readonly CancellationTokenSource cancellation=new CancellationTokenSource();
    readonly Task producer;
    readonly Dictionary<int,Texture2D> staging=new Dictionary<int,Texture2D>();
    Texture2DArray result;
    bool disposed;
    public Texture2DArray Result=>result;
    public int Pieces {get;private set;}
    public Task Completion=>producer;
    public int RetainedBufferBytes=>PageBytes*PageCount;
    public FaunaTextureUpload(string path)
    {
        var reader=new BinaryReader(File.OpenRead(path));
        try
        {
            if(reader.ReadInt32()!=0x57474331)throw new InvalidDataException("Coat stream header");
            Size=reader.ReadInt32();Layers=reader.ReadInt32();
            if((Size!=512&&Size!=1024)||Layers<1||Layers>16)throw new InvalidDataException("Coat stream dimensions");
            Mips=1+(int)Math.Log(Size,2);
            for(int i=0;i<PageCount;i++)available.Add(new Page());
            producer=Task.Run(()=>Produce(reader));
        }
        catch{reader.Dispose();available.Dispose();ready.Dispose();cancellation.Dispose();throw;}
    }
    void Produce(BinaryReader reader)
    {
        try
        {
            for(int layer=0;layer<Layers;layer++)
            {
                if(reader.ReadInt32()!=Mips)throw new InvalidDataException("Coat stream mip chain");
                for(int mip=0;mip<Mips;mip++)
                {
                    int width=Math.Max(1,Size>>mip),rows=Math.Min(64,width),bytes=Math.Max(1,(width+3)/4)*Math.Max(1,(width+3)/4)*16;
                    if(reader.ReadInt32()!=bytes)throw new InvalidDataException("Coat stream payload");
                    for(int y=0;y<width;y+=rows)
                    {
                        var page=available.Take(cancellation.Token);page.Layer=layer;page.Mip=mip;page.Width=width;page.Rows=rows;page.Y=y;
                        page.Length=Math.Max(1,(width+3)/4)*Math.Max(1,(rows+3)/4)*16;
                        int read=0;while(read<page.Length){cancellation.Token.ThrowIfCancellationRequested();int n=reader.Read(page.Bytes,read,page.Length-read);if(n==0)throw new EndOfStreamException();read+=n;}
                        ready.Add(page,cancellation.Token);
                    }
                }
            }
            if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new InvalidDataException("Coat stream tail");
        }
        finally{reader.Dispose();ready.CompleteAdding();}
    }
    public IEnumerator Upload()
    {
        if(disposed)throw new ObjectDisposedException(nameof(FaunaTextureUpload));
        var format=GraphicsFormat.RGBA_BC7_SRGB;
        result=new Texture2DArray(Size,Size,Layers,format,TextureCreationFlags.MipChain|TextureCreationFlags.DontInitializePixels|TextureCreationFlags.DontUploadUponCreate)
            {name="Streamed native coat pigments",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp,anisoLevel=2};
        // GPU-only destination: do not upload an unused full-sized CPU image
        // before the bounded copies. Apply would reintroduce the same stall.
        yield return null;
        long slice=Stopwatch.GetTimestamp();int uploadedBytes=0;
        while(true)
        {
            if(!ready.TryTake(out var page))
            {
                if(producer.IsCompleted)
                {if(producer.IsFaulted)throw producer.Exception.GetBaseException();cancellation.Token.ThrowIfCancellationRequested();break;}
                yield return null;slice=Stopwatch.GetTimestamp();uploadedBytes=0;continue;
            }
            int width=page.Width,rows=page.Rows,sourceMip=width==1?2:width==2?1:0;
            if(!staging.TryGetValue(width,out var source))
            {
                source=new Texture2D(Math.Max(4,width),Math.Max(4,rows),format,TextureCreationFlags.DontInitializePixels|(width<4?TextureCreationFlags.MipChain:0))
                    {name="Bounded BC7 upload page"};staging.Add(width,source);
            }
            source.SetPixelData(page.Bytes,sourceMip);source.Apply(false,false);
            if(width<=64)Graphics.CopyTexture(source,0,sourceMip,result,page.Layer,page.Mip);
            else Graphics.CopyTexture(source,0,0,0,0,width,rows,result,page.Layer,page.Mip,0,page.Y);
            Pieces++;uploadedBytes+=page.Length;available.Add(page,cancellation.Token);
            if(uploadedBytes>=256*1024||(Stopwatch.GetTimestamp()-slice)*1000.0/Stopwatch.Frequency>=.35)
            {yield return null;slice=Stopwatch.GetTimestamp();uploadedBytes=0;}
        }
    }
    public Texture2DArray TakeResult(){var value=result;result=null;return value;}
    public void Dispose()
    {
        if(disposed)return;disposed=true;StopReader();
        foreach(var texture in staging.Values)UnityEngine.Object.Destroy(texture);
        staging.Clear();if(result!=null)UnityEngine.Object.Destroy(result);result=null;
    }
    // A constructor task may finish after its scene was closed. This path has
    // not created Unity objects and is safe on that task's continuation thread.
    public void CancelUnstarted()
    {if(disposed)return;if(!ReferenceEquals(result,null))throw new InvalidOperationException("Upload already started");disposed=true;StopReader();}
    void StopReader()
    {
        cancellation.Cancel();
        // Cancellation wakes a producer waiting for a page. Never join a disk
        // read on the render/game thread, and never dispose a queue in use.
        producer.ContinueWith(t=>{_ = t.Exception;available.Dispose();ready.Dispose();cancellation.Dispose();},TaskScheduler.Default);
    }
}
