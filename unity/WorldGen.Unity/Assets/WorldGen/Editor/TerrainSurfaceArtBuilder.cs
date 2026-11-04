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
                var array=new Texture2DArray(size,size,17,TextureFormat.BC7,true,linear){name="Terrain surface "+suffix[channel],wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
                arrays[channel]=array;
                for(int layer=0;layer<17;layer++)
                {
                    var texture=new Texture2D(128,128,TextureFormat.RGBA32,false,linear){wrapMode=TextureWrapMode.Repeat};
                    try
                    {
                        string material=layer==0?"soil":layer==1?"sand":layer==2?"gravel":layer==3?"clay":layer==6?"crust":layer==4||layer==8?"granite":null;
                        if(material!=null&&!File.Exists(Inputs+"/"+material+"-"+suffix[channel]+".png"))material=null;
                        string rockAsset=layer==10?"cliff_side":layer==11?"marble_cliff_01":layer==12?"sandstone_cracks":layer==13?"dark_rock":null;
                        if(layer>=14||rockAsset!=null)
                        {
                            string[] assets={"dry_decay_leaves","forrest_ground_03","brown_mud_leaves_01"};
                            string input=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../assets-source/terrain/surfaces",layer>=14?"cc0-forest-floor":"cc0-rock",layer>=14?assets[layer-14]:rockAsset,suffix[channel]+".jpg"));
                            if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(input),false))throw new InvalidDataException(input);
                        }
                        else if(material!=null)
                        {
                            string input=Inputs+"/"+material+"-"+suffix[channel]+".png";
                            if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(input),false))throw new InvalidDataException(input);
                        }
                        else{texture.SetPixels(fallback[channel].GetPixels(layer));texture.Apply(false,false);}
                        Color[] pixels=new Color[size*size];
                        for(int y=0;y<size;y++)for(int x=0;x<size;x++)pixels[y*size+x]=texture.GetPixelBilinear((x+.5f)/size,(y+.5f)/size);
                        if((layer>=14||rockAsset!=null)&&channel==0)
                        {
                            // The ground's ecological/material tint supplies
                            // the mean. Preserve scanned leaf/needle contrast
                            // without multiplying two unrelated brown tints.
                            Color average=Color.black;
                            foreach(var pixel in pixels)average+=pixel.linear/pixels.Length;
                            float target=Mathf.GammaToLinearSpace(.5f);
                            for(int i=0;i<pixels.Length;i++)
                            {
                                var p=pixels[i].linear;
                                p.r=Mathf.Clamp01(p.r*target/Mathf.Max(.001f,average.r));
                                p.g=Mathf.Clamp01(p.g*target/Mathf.Max(.001f,average.g));
                                p.b=Mathf.Clamp01(p.b*target/Mathf.Max(.001f,average.b));
                                pixels[i]=p.gamma;pixels[i].a=1;
                            }
                        }
                        if((layer>=14||rockAsset!=null)&&channel==2)
                            for(int i=0;i<pixels.Length;i++){var p=pixels[i];pixels[i]=new Color(p.r,p.g,.5f,p.b);}
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
            Debug.Log("TerrainSurfaceArtV1 published: original14 plus broadleaf/needle/damp floor scans; BC7 512/256/128,17 layers, all mip levels. Maximum sampled layers and texture ranges unchanged.");
        }
        finally{foreach(var texture in fallback)UnityEngine.Object.DestroyImmediate(texture);}
    }
}
