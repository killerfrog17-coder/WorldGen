using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Two generated wall materials; independent of the ground bank.</summary>
public static class CliffSurfaceArtBuilder
{
    const string Inputs="Assets/WorldGen/Editor/CliffSurfaceArtV1";
    const string Output="Assets/WorldGen/Resources/CliffSurfaceArtV1";
    public static void EnsureBuilt()
    {
        if(!Directory.Exists(Inputs))return;
        string stamp=Output+"/source-hash.txt";
        using(var hash=System.Security.Cryptography.SHA256.Create())
        {
            string signature="cliff-surfaces-v1";
            foreach(string channel in new[]{"albedo","normal"})for(int layer=0;layer<2;layer++)
                signature+=Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(Inputs+"/layer-"+layer.ToString("D2")+"-"+channel+".png")));
            if(File.Exists(stamp)&&File.ReadAllText(stamp)==signature&&AssetDatabase.LoadAssetAtPath<Texture2DArray>(Output+"/normal.asset")!=null)return;
            Build();File.WriteAllText(stamp,signature);AssetDatabase.Refresh();
        }
    }
    [MenuItem("WorldGen/Art/Build generated cliff surfaces")]
    public static void Build()
    {
        Directory.CreateDirectory(Output);AssetDatabase.Refresh();
        var arrays=new Texture2DArray[2];var suffix=new[]{"albedo","normal"};var sizes=new[]{1024,512};
        for(int channel=0;channel<2;channel++)
        {
            int size=sizes[channel];bool linear=channel!=0;
            var array=new Texture2DArray(size,size,2,TextureFormat.BC7,true,linear){name="Generated cliff "+suffix[channel],wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            try
            {
                for(int layer=0;layer<2;layer++)
                {
                    string input=Inputs+"/layer-"+layer.ToString("D2")+"-"+suffix[channel]+".png";
                    var source=new Texture2D(2,2,TextureFormat.RGBA32,false,linear);
                    try
                    {
                        if(!ImageConversion.LoadImage(source,File.ReadAllBytes(input),false)||source.width!=size||source.height!=size)throw new InvalidDataException(input);
                        var levels=TerrainSurfaceArtBuilder.Mips(source.GetPixels32(),size,channel);
                        var scaled=new Texture2D(size,size,TextureFormat.RGBA32,true,linear);
                        try
                        {
                            for(int mip=0;mip<levels.Count;mip++)scaled.SetPixels32(levels[mip],mip);
                            scaled.Apply(false,false);EditorUtility.CompressTexture(scaled,TextureFormat.BC7,TextureCompressionQuality.Best);
                            if(scaled.format!=TextureFormat.BC7)throw new InvalidOperationException("BC7 cliff compression failed");
                            for(int mip=0;mip<scaled.mipmapCount;mip++)array.SetPixelData(scaled.GetPixelData<byte>(mip),mip,layer);
                        }
                        finally{UnityEngine.Object.DestroyImmediate(scaled);}
                    }
                    finally{UnityEngine.Object.DestroyImmediate(source);}
                }
                array.Apply(false,true);string path=Output+"/"+suffix[channel]+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
                if(existing==null){AssetDatabase.CreateAsset(array,path);arrays[channel]=array;array=null;}
                else{EditorUtility.CopySerialized(array,existing);arrays[channel]=existing;EditorUtility.SetDirty(existing);}
            }
            finally{if(array!=null)UnityEngine.Object.DestroyImmediate(array);}
        }
        var art=AssetDatabase.LoadAssetAtPath<LocalTerrainArtSet>("Assets/WorldGen/Resources/LocalTerrainArtSet.asset");
        if(art==null)throw new InvalidOperationException("Terrain art set missing");
        art.CliffAlbedo=arrays[0];art.CliffNormals=arrays[1];EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
        Debug.Log("GENERATED CLIFFS PUBLISHED:2 BC7 layers1024/512; existing ground bank unchanged.");
    }
}
