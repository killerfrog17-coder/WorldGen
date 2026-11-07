using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Publish generated material data into the existing17-layer layout.</summary>
public static class TerrainSurfaceArtBuilder
{
    const string Inputs="Assets/WorldGen/Editor/TerrainSurfaceArtV1";
    const string Output="Assets/WorldGen/Resources/TerrainSurfaceArtV1";
    [MenuItem("WorldGen/Art/Build generated ground surfaces")]
    public static void Build()
    {
        Directory.CreateDirectory(Output);AssetDatabase.Refresh();
        var arrays=new Texture2DArray[3];var suffix=new[]{"albedo","normal","mask"};var sizes=new[]{1024,512,128};
        for(int channel=0;channel<3;channel++)
        {
            int size=sizes[channel];bool linear=channel!=0;
            var array=new Texture2DArray(size,size,17,TextureFormat.BC7,true,linear){name="Generated terrain "+suffix[channel],wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            try
            {
                for(int layer=0;layer<17;layer++)
                {
                    string input=Inputs+"/layer-"+layer.ToString("D2")+"-"+suffix[channel]+".png";
                    var source=new Texture2D(2,2,TextureFormat.RGBA32,false,linear);
                    try
                    {
                        if(!ImageConversion.LoadImage(source,File.ReadAllBytes(input),false)||source.width!=size||source.height!=size)throw new InvalidDataException(input);
                        var levels=Mips(source.GetPixels32(),size,channel);
                        var scaled=new Texture2D(size,size,TextureFormat.RGBA32,true,linear);
                        try
                        {
                            for(int mip=0;mip<levels.Count;mip++)scaled.SetPixels32(levels[mip],mip);
                            scaled.Apply(false,false);EditorUtility.CompressTexture(scaled,TextureFormat.BC7,TextureCompressionQuality.Best);
                            if(scaled.format!=TextureFormat.BC7)throw new InvalidOperationException("BC7 compression failed");
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
        foreach(string name in new[]{"GroundGrassV2","GroundMossV1","GroundLichenV1"})
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/WorldGen/Resources/"+name+".png");
            if(importer==null)throw new InvalidDataException(name);
            importer.sRGBTexture=name=="GroundGrassV2";importer.isReadable=false;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;importer.maxTextureSize=name=="GroundLichenV1"?256:1024;
            var settings=importer.GetDefaultPlatformTextureSettings();settings.format=TextureImporterFormat.BC7;settings.maxTextureSize=importer.maxTextureSize;importer.SetPlatformTextureSettings(settings);
            settings=importer.GetPlatformTextureSettings("Standalone");settings.overridden=true;settings.format=TextureImporterFormat.BC7;settings.maxTextureSize=importer.maxTextureSize;importer.SetPlatformTextureSettings(settings);importer.SaveAndReimport();
        }
        var art=AssetDatabase.LoadAssetAtPath<LocalTerrainArtSet>("Assets/WorldGen/Resources/LocalTerrainArtSet.asset");
        if(art==null)throw new InvalidOperationException("Terrain art set missing");
        art.Albedo=arrays[0];art.Normals=arrays[1];art.Masks=arrays[2];EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
        Debug.Log("GENERATED GROUND PUBLISHED:17 BC7 layers1024/512/128, grass/moss1024, lichen256; no new samples/passes.");
    }
    public static List<Color32[]> Mips(Color32[] pixels,int size,int channel)
    {
        Color Decode(Color32 value){Color c=value;if(channel==0)return c.linear;if(channel==1)return new Color(c.r*2-1,c.g*2-1,c.b*2-1,1);return c;}
        Color32 Encode(Color value){if(channel==0)return value.gamma;if(channel==1){var n=new Vector3(value.r,value.g,value.b).normalized;return new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);}return value;}
        var levels=new List<Color32[]>();var current=(Color32[])pixels.Clone();
        for(int width=size;;width/=2)
        {
            if(width>1)for(int axis=0;axis<2;axis++)for(int k=0;k<width;k++)
            {int a=axis==0?k:k*width,b=axis==0?(width-1)*width+k:k*width+width-1;current[a]=current[b]=Encode((Decode(current[a])+Decode(current[b]))*.5f);}
            levels.Add(current);if(width==1)break;
            int next=width/2;var reduced=new Color32[next*next];
            for(int y=0;y<next;y++)for(int x=0;x<next;x++)
            {Color sum=Color.clear;for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)sum+=Decode(current[(y*2+dy)*width+x*2+dx]);reduced[y*next+x]=Encode(sum*.25f);}
            current=reduced;
        }
        return levels;
    }
    public static void BatchReview(){try{Build();WorldGenGroundArtReview.Run();}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
