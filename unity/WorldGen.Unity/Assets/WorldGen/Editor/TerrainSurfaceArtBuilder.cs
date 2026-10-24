using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Editor-only art publication; runtime keeps its current shader/samples.</summary>
public static class TerrainSurfaceArtBuilder
{
    const string Inputs="Assets/WorldGen/Editor/TerrainSurfaceArtV1";
    const string Output="Assets/WorldGen/Resources/TerrainSurfaceArtV1";
    [MenuItem("WorldGen/Art/Build first terrain surface textures")]
    public static void Build()
    {
        Directory.CreateDirectory(Output);AssetDatabase.Refresh();
        var fallback=LocalGroundTextures.Create(true);var arrays=new Texture2DArray[3];
        var suffix=new[]{"albedo","normal","mask"};var sizes=new[]{512,256,128};
        try
        {
            for(int channel=0;channel<3;channel++)
            {
                int size=sizes[channel];bool linear=channel!=0;
                var array=new Texture2DArray(size,size,14,TextureFormat.BC7,true,linear){name="Terrain surface "+suffix[channel],wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
                arrays[channel]=array;
                for(int layer=0;layer<14;layer++)
                {
                    var texture=new Texture2D(128,128,TextureFormat.RGBA32,false,linear){wrapMode=TextureWrapMode.Repeat};
                    try
                    {
                        string material=layer==0?"soil":layer==4||layer==8?"granite":null;
                        if(material!=null)
                        {
                            string input=Inputs+"/"+material+"-"+suffix[channel]+".png";
                            if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(input),false))throw new InvalidDataException(input);
                        }
                        else{texture.SetPixels(fallback[channel].GetPixels(layer));texture.Apply(false,false);}
                        Color[] pixels=new Color[size*size];
                        for(int y=0;y<size;y++)for(int x=0;x<size;x++)pixels[y*size+x]=texture.GetPixelBilinear((x+.5f)/size,(y+.5f)/size);
                        var scaled=new Texture2D(size,size,TextureFormat.RGBA32,true,linear);
                        try
                        {
                            scaled.SetPixels(pixels);scaled.Apply(true,false);
                            EditorUtility.CompressTexture(scaled,TextureFormat.BC7,TextureCompressionQuality.Best);
                            if(scaled.format!=TextureFormat.BC7)throw new InvalidOperationException("BC7 compression failed");
                            for(int mip=0;mip<scaled.mipmapCount;mip++)array.SetPixelData(scaled.GetPixelData<byte>(mip),mip,layer);
                        }
                        finally{UnityEngine.Object.DestroyImmediate(scaled);}
                    }
                    finally{UnityEngine.Object.DestroyImmediate(texture);}
                }
                array.Apply(false,true);string path=Output+"/"+suffix[channel]+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
                if(existing==null)AssetDatabase.CreateAsset(array,path);
                else{EditorUtility.CopySerialized(array,existing);UnityEngine.Object.DestroyImmediate(array);arrays[channel]=existing;EditorUtility.SetDirty(existing);}
            }
            var art=AssetDatabase.LoadAssetAtPath<LocalTerrainArtSet>("Assets/WorldGen/Resources/LocalTerrainArtSet.asset");
            if(art==null)throw new InvalidOperationException("Existing terrain art set missing");
            art.Albedo=arrays[0];art.Normals=arrays[1];art.Masks=arrays[2];
            EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
            Debug.Log("TerrainSurfaceArtV1 published: soil and granite replace slots 0/4/8; BC7 512/256/128, 14 layers, all mip levels. Shader/scene constants/quality ranges unchanged.");
        }
        finally{foreach(var texture in fallback)UnityEngine.Object.DestroyImmediate(texture);}
    }
}
