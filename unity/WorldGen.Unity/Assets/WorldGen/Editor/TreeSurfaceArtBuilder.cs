using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

// Authoring only. Publish real shoot/bark scans once, not during world loading.
public static class TreeSurfaceArtBuilder
{
    const string Inputs="Assets/WorldGen/Editor/TreeSurfaceArtV1";
    const string Output="Assets/WorldGen/Resources/TreeSurfaceArtV1";
    [MenuItem("WorldGen/Art/Build scanned tree surfaces")]
    public static void Build()
    {
        Directory.CreateDirectory(Output);
        var cpu=new Color32[4][][];
        for(int group=0;group<2;group++)
        {
            int layers=group==0?32:5;string prefix=group==0?"shoot":"bark";
            var colours=new Color32[layers][];var normals=new Color32[layers][];
            for(int layer=0;layer<layers;layer++)for(int channel=0;channel<2;channel++)
            {
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,channel==1);
                try
                {
                    string path=Inputs+"/"+prefix+"-"+layer.ToString("D2")+(channel==0?"-albedo.png":"-normal.png");
                    if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(path),false)||texture.width!=512||texture.height!=512)throw new InvalidDataException(path);
                    (channel==0?colours:normals)[layer]=texture.GetPixels32();
                }
                finally{UnityEngine.Object.DestroyImmediate(texture);}
            }
            var prepared=group==0?LocalTreeTextures.Prepare(colours,normals,512):LocalTreeTextures.PrepareBark(colours,normals,512);
            for(int channel=0;channel<2;channel++)
            {
                var levels=channel==0?prepared.Colours:prepared.Normals;
                var array=new Texture2DArray(512,512,layers,TextureFormat.BC7,true,channel==1){name="Scanned tree "+prefix+(channel==0?" albedo":" normals"),wrapMode=group==0?TextureWrapMode.Clamp:TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
                for(int layer=0;layer<layers;layer++)
                {
                    var scaled=new Texture2D(512,512,TextureFormat.RGBA32,true,channel==1);
                    try
                    {
                        for(int mip=0;mip<levels[layer].Length;mip++)scaled.SetPixels32(levels[layer][mip],mip);
                        scaled.Apply(false,false);EditorUtility.CompressTexture(scaled,TextureFormat.BC7,TextureCompressionQuality.Best);
                        for(int mip=0;mip<scaled.mipmapCount;mip++)array.SetPixelData(scaled.GetPixelData<byte>(mip),mip,layer);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(scaled);}
                }
                array.Apply(false,true);string destination=Output+"/"+prefix+(channel==0?"-albedo.asset":"-normal.asset");
                var existing=AssetDatabase.LoadAssetAtPath<Texture2DArray>(destination);
                if(existing==null)AssetDatabase.CreateAsset(array,destination);
                else{EditorUtility.CopySerialized(array,existing);UnityEngine.Object.DestroyImmediate(array);EditorUtility.SetDirty(existing);}
                cpu[group*2+channel]=new Color32[layers][];
                for(int layer=0;layer<layers;layer++)cpu[group*2+channel][layer]=levels[layer][1];
            }
        }
        // The impostor rasterizer itself supersamples at256 then reduces to128.
        // Retain only matching256 source mips on the CPU, not512 GPU duplicates.
        using(var stream=File.Create(Output+"/pixels.bytes"))
        using(var zip=new DeflateStream(stream,System.IO.Compression.CompressionLevel.Optimal))
        using(var writer=new BinaryWriter(zip))
        {
            writer.Write(0x54524131);writer.Write(256);writer.Write(32);writer.Write(5);
            foreach(var group in cpu)foreach(var layer in group)foreach(var p in layer){writer.Write(p.r);writer.Write(p.g);writer.Write(p.b);writer.Write(p.a);}
        }
        AssetDatabase.Refresh();AssetDatabase.SaveAssets();
        Debug.Log("TREE SURFACE ART PUBLISHED: BC7 32x512 shoot pairs, 5x512 bark pairs,256 CPU bake bank.");
    }
    public static void BatchBuild()
    {
        try{Build();EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void BatchReview()
    {
        try{Build();WorldGenTreeAssetReview.Run();}
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
