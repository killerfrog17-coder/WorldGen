using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using WorldGen.Core;

// Shared shoot art. CPU pixels are retained once for bounded asynchronous model
// baking; they are never sampled on the CPU to animate/draw standing trees.
public static class LocalTreeTextures
{
    public const int Size=256,Layers=32,BarkLayers=5;
    public static Color32[][] Colour,Normals,BarkColour,BarkNormals;
    static readonly Vector4[] hulls=new Vector4[8];
    static readonly bool[] hullReady=new bool[8];
    const string Assets="TreeSurfaceArtV1/";
    public static void Create(out Texture2DArray colour,out Texture2DArray normals)
    {
        if(Colour==null)Generate();
        colour=Resources.Load<Texture2DArray>(Assets+"shoot-albedo");normals=Resources.Load<Texture2DArray>(Assets+"shoot-normal");
        if(colour==null||normals==null)throw new InvalidOperationException("Publish scanned tree surface art before running.");
    }
    public static void BindBark(Material material)
    {
        material.SetTexture("_TreeBark",Resources.Load<Texture2DArray>(Assets+"bark-albedo"));
        material.SetTexture("_TreeBarkNormals",Resources.Load<Texture2DArray>(Assets+"bark-normal"));
    }
    // A conservative trapezoid removes empty card corners with the SAME vertex
    // and triangle count. Union all four variants, including faint edge pixels.
    // Both mesh LODs retain identical UV/alpha support and branch positions.
    public static Vector4 ShootHull(int shape)
    {
        if(hullReady[shape])return hulls[shape];if(Colour==null)Generate();
        float lo=1,hi=0;
        for(int variant=0;variant<4;variant++)
        {
            var pixels=Colour[shape*4+variant];
            for(int i=0;i<pixels.Length;i++)if(pixels[i].a>=8){float u=(i%Size+.5f)/Size;lo=Mathf.Min(lo,u);hi=Mathf.Max(hi,u);}
        }
        float leftTop=1,rightTop=0,leftBottom=1,rightBottom=0;
        for(int variant=0;variant<4;variant++)
        {
            var pixels=Colour[shape*4+variant];
            for(int i=0;i<pixels.Length;i++)if(pixels[i].a>=8)
            {
                float u=(i%Size+.5f)/Size,v=(i/Size+.5f)/Size;
                leftTop=Mathf.Min(leftTop,lo+(u-lo)/v);rightTop=Mathf.Max(rightTop,hi+(u-hi)/v);
                leftBottom=Mathf.Min(leftBottom,lo+(u-lo)/(1-v));rightBottom=Mathf.Max(rightBottom,hi+(u-hi)/(1-v));
            }
        }
        const float pad=3f/Size;
        var top=new Vector4(lo-pad,hi+pad,leftTop-pad,rightTop+pad);
        var bottom=new Vector4(leftBottom-pad,rightBottom+pad,lo-pad,hi+pad);
        var result=(top.y-top.x+top.w-top.z)<(bottom.y-bottom.x+bottom.w-bottom.z)?top:bottom;
        for(int i=0;i<4;i++)result[i]=Mathf.Clamp01(result[i]);hulls[shape]=result;hullReady[shape]=true;return result;
    }
    static void Generate()
    {
        var asset=Resources.Load<TextAsset>(Assets+"pixels");
        if(asset==null)throw new InvalidOperationException("Tree CPU surface bank missing.");
        try
        {
            using(var input=new MemoryStream(asset.bytes,false))
            using(var zip=new DeflateStream(input,CompressionMode.Decompress))
            using(var reader=new BinaryReader(zip))
            {
                if(reader.ReadInt32()!=0x54524131||reader.ReadInt32()!=Size||reader.ReadInt32()!=Layers||reader.ReadInt32()!=BarkLayers)throw new InvalidDataException("Tree surface bank header");
                Color32[][] Read(int count)
                {
                    var result=new Color32[count][];
                    for(int layer=0;layer<count;layer++)
                    {
                        var bytes=reader.ReadBytes(Size*Size*4);if(bytes.Length!=Size*Size*4)throw new EndOfStreamException("Tree surface bank");
                        var pixels=result[layer]=new Color32[Size*Size];
                        for(int i=0;i<pixels.Length;i++){int at=i*4;pixels[i]=new Color32(bytes[at],bytes[at+1],bytes[at+2],bytes[at+3]);}
                    }
                    return result;
                }
                Colour=Read(Layers);Normals=Read(Layers);BarkColour=Read(BarkLayers);BarkNormals=Read(BarkLayers);
            }
        }
        finally{Resources.UnloadAsset(asset);}
    }
    public static void Upload(Color32[][] colours,Color32[][] normals,int size,string name,out Texture2DArray colour,out Texture2DArray normal)
        =>Upload(Prepare(colours,normals,size),size,name,out colour,out normal);
    public sealed class Prepared {public Color32[][][] Colours,Normals;}
    public static Prepared Prepare(Color32[][] colours,Color32[][] normals,int size,System.Threading.CancellationToken token=default)
    {
        var prepared=new Prepared{Colours=new Color32[colours.Length][][],Normals=new Color32[colours.Length][][]};
        for(int layer=0;layer<colours.Length;layer++)
        {
            token.ThrowIfCancellationRequested();var cs=new System.Collections.Generic.List<Color32[]>();var nsLevels=new System.Collections.Generic.List<Color32[]>();
            var c=colours[layer];var n=normals[layer];int covered=0;foreach(var p in c)if(p.a>=102)covered++;
            float target=covered/(float)c.Length;
            for(int mip=0,width=size;;mip++,width/=2)
            {
                token.ThrowIfCancellationRequested();cs.Add(c);nsLevels.Add(n);if(width==1)break;
                int nextWidth=width/2;var next=new Color32[nextWidth*nextWidth];var ns=new Color32[next.Length];
                for(int y=0;y<nextWidth;y++)for(int x=0;x<nextWidth;x++)
                {
                    int weight=0,alpha=0,cr=0,cg=0,cb=0,nr=0,ng=0,nb=0,nd=0;
                    for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)
                    {int at=(y*2+dy)*width+x*2+dx;var p=c[at];var q=n[at];int a=p.a;weight+=a;alpha+=a;cr+=p.r*a;cg+=p.g*a;cb+=p.b*a;nr+=q.r*a;ng+=q.g*a;nb+=q.b*a;nd+=q.a*a;}
                    int index=y*nextWidth+x,div=Math.Max(1,weight);
                    next[index]=new Color32((byte)(cr/div),(byte)(cg/div),(byte)(cb/div),(byte)(alpha/4));
                    ns[index]=new Color32((byte)(nr/div),(byte)(ng/div),(byte)(nb/div),(byte)(nd/div));
                }
                float lo=0,hi=8;
                for(int k=0;k<10;k++){float scale=(lo+hi)*.5f;int count=0;foreach(var p in next)if(p.a*scale>=102)count++;if(count/(float)next.Length<target)lo=scale;else hi=scale;}
                float gain=(lo+hi)*.5f;for(int i=0;i<next.Length;i++){var p=next[i];p.a=(byte)Math.Min(255,p.a*gain);next[i]=p;}
                c=next;n=ns;
            }
            prepared.Colours[layer]=cs.ToArray();prepared.Normals[layer]=nsLevels.ToArray();
        }
        return prepared;
    }
    public static void Upload(Prepared data,int size,string name,out Texture2DArray colour,out Texture2DArray normal)
    {
        colour=new Texture2DArray(size,size,data.Colours.Length,TextureFormat.RGBA32,true,false){name=name+" albedo",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=2};
        normal=new Texture2DArray(size,size,data.Colours.Length,TextureFormat.RGBA32,true,true){name=name+" normal/depth",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=2};
        for(int layer=0;layer<data.Colours.Length;layer++)for(int mip=0;mip<data.Colours[layer].Length;mip++)
        {colour.SetPixels32(data.Colours[layer][mip],layer,mip);normal.SetPixels32(data.Normals[layer][mip],layer,mip);}
        colour.Apply(false,true);normal.Apply(false,true);
    }
}
