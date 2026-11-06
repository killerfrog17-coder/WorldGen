using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

// Experimental bounded native upload. Not used by normal fauna until the
// packaged render/byte-equivalence and moving-player comparisons pass.
public sealed class FaunaTextureUpload : IDisposable
{
    public readonly int Size,Layers,Mips;
    readonly byte[][] data;
    readonly Dictionary<int,Texture2D> staging=new Dictionary<int,Texture2D>();
    readonly Dictionary<int,byte[]> scratch=new Dictionary<int,byte[]>();
    Texture2DArray result;
    public Texture2DArray Result=>result;
    public int Pieces {get;private set;}
    public FaunaTextureUpload(string path)
    {
        using var reader=new BinaryReader(File.OpenRead(path));
        if(reader.ReadInt32()!=0x57474331)throw new InvalidDataException("Coat stream header");
        Size=reader.ReadInt32();Layers=reader.ReadInt32();
        if((Size!=512&&Size!=1024)||Layers<1||Layers>16)throw new InvalidDataException("Coat stream dimensions");
        Mips=1+(int)Math.Log(Size,2);data=new byte[Layers*Mips][];
        for(int layer=0;layer<Layers;layer++)
        {
            if(reader.ReadInt32()!=Mips)throw new InvalidDataException("Coat stream mip chain");
            for(int mip=0;mip<Mips;mip++)
            {
                int width=Math.Max(1,Size>>mip),bytes=Math.Max(1,(width+3)/4)*Math.Max(1,(width+3)/4)*16;
                if(reader.ReadInt32()!=bytes)throw new InvalidDataException("Coat stream payload");
                data[layer*Mips+mip]=reader.ReadBytes(bytes);
                if(data[layer*Mips+mip].Length!=bytes)throw new EndOfStreamException();
            }
        }
        if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new InvalidDataException("Coat stream tail");
    }
    public IEnumerator Upload()
    {
        var format=GraphicsFormat.RGBA_BC7_SRGB;
        result=new Texture2DArray(Size,Size,Layers,format,TextureCreationFlags.MipChain|TextureCreationFlags.DontInitializePixels|TextureCreationFlags.DontUploadUponCreate)
            {name="Streamed native coat pigments",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp,anisoLevel=2};
        // GPU-only destination: do not upload an unused full-sized CPU image
        // before the bounded copies. Apply would reintroduce the same stall.
        yield return null;
        for(int layer=0;layer<Layers;layer++)for(int mip=0;mip<Mips;mip++)
        {
            int width=Math.Max(1,Size>>mip),rows=Math.Min(64,width),sourceMip=width==1?2:width==2?1:0;
            if(!staging.TryGetValue(width,out var source))
            {
                source=new Texture2D(Math.Max(4,width),Math.Max(4,rows),format,TextureCreationFlags.DontInitializePixels|(width<4?TextureCreationFlags.MipChain:0))
                    {name="Bounded BC7 upload page"};staging.Add(width,source);
                scratch.Add(width,new byte[Math.Max(1,(width+3)/4)*Math.Max(1,(rows+3)/4)*16]);
            }
            var bytes=scratch[width];
            for(int y=0,offset=0;y<width;y+=rows,offset+=bytes.Length)
            {
                Buffer.BlockCopy(data[layer*Mips+mip],offset,bytes,0,bytes.Length);
                source.SetPixelData(bytes,sourceMip);source.Apply(false,false);
                if(width<=64)Graphics.CopyTexture(source,0,sourceMip,result,layer,mip);
                else Graphics.CopyTexture(source,0,0,0,0,width,rows,result,layer,mip,0,y);
                Pieces++;yield return null;
            }
        }
    }
    public Texture2DArray TakeResult(){var value=result;result=null;return value;}
    public void Dispose()
    {
        foreach(var texture in staging.Values)UnityEngine.Object.Destroy(texture);
        staging.Clear();scratch.Clear();if(result!=null)UnityEngine.Object.Destroy(result);result=null;
    }
}
