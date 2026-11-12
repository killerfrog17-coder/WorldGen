using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Export a representative subset first. Runtime never invokes this authoring
// path; nearby and distant renderers load the same prepared prototype assets.
public static class TreePrototypeBuilder
{
    const string Output="Assets/WorldGen/Resources/TreePrototypesV1";
    [MenuItem("WorldGen/Art/Build representative tree prototypes")]
    public static void BuildRepresentative()=>Build(false);
    [MenuItem("WorldGen/Art/Build complete tree prototype bank")]
    public static void BuildComplete()=>Build(true);
    public static void BatchBuild()
    {
        try{Build(Environment.GetCommandLineArgs().Contains("--tree-prototype-all"));EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Build(bool all)
    {
        string[] args=Environment.GetCommandLineArgs();int codesAt=Array.IndexOf(args,"--tree-prototype-codes"),reviewAt=Array.IndexOf(args,"--tree-prototype-review-output");
        IEnumerable<int> selected=all?LocalTreePrototype.Codes:new[]{LocalTreePrototype.Codes[0],LocalTreePrototype.Codes[64],LocalTreePrototype.Codes[72]};
        if(codesAt>=0)
        {
            if(codesAt+1>=args.Length)throw new ArgumentException("--tree-prototype-codes needs comma-separated hexadecimal codes");
            selected=args[codesAt+1].Split(',').Select(s=>Convert.ToInt32(s.Trim().Replace("0x",""),16));
        }
        if(reviewAt>=0&&reviewAt+1>=args.Length)throw new ArgumentException("--tree-prototype-review-output needs a folder");
        string review=reviewAt>=0?Path.GetFullPath(args[reviewAt+1]):Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/tree-prototype-review"));
        Directory.CreateDirectory(review);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
        var lines=new List<string>{"Prepared tree prototypes, bank "+LocalTreePrototype.BankVersion};
        foreach(int code in selected.Distinct())
        {
            if(!LocalTreePrototype.Codes.Contains(code))throw new ArgumentException("Code is outside the prepared shape bank: "+code.ToString("X8"));
            var timer=System.Diagnostics.Stopwatch.StartNew();var source=LocalTreePrototype.PrepareExport(code);
            string path=Output+"/"+code.ToString("X8")+".asset";
            var asset=AssetDatabase.LoadAssetAtPath<LocalTreePrototype>(path);
            if(asset==null){asset=ScriptableObject.CreateInstance<LocalTreePrototype>();asset.name="Tree prototype "+code.ToString("X8");AssetDatabase.CreateAsset(asset,path);}
            asset.Version=LocalTreePrototype.BankVersion;asset.Code=code;asset.Center=source.Center;asset.Extents=source.Extents;asset.Radius=source.Radius;
            asset.Near=Publish(source.Near,asset.Near,asset,"Near");asset.Middle=Publish(source.Middle,asset.Middle,asset,"Middle");
            asset.Colour=Publish(Compress(source.Colours,false),asset.Colour,asset,"Billboard colour");
            asset.Normal=Publish(Compress(source.Normals,true),asset.Normal,asset,"Billboard normal and depth");
            asset.StandCenter=source.StandCenter;asset.StandExtents=source.StandExtents;asset.StandRadius=source.StandRadius;
            asset.StandColour=Publish(Compress(source.StandColours,false),asset.StandColour,asset,"Stand billboard colour");
            asset.StandNormal=Publish(Compress(source.StandNormals,true),asset.StandNormal,asset,"Stand billboard normal and depth");
            ContactSheet(source.Colours,Path.Combine(review,code.ToString("X8")+"-views.png"));
            ContactSheet(source.StandColours,Path.Combine(review,code.ToString("X8")+"-stand-views.png"));
            EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();
            string line=code.ToString("X8")+" near="+asset.Near.triangles.Length/3+" middle="+asset.Middle.triangles.Length/3+" views="+asset.Colour.depth+" format="+asset.Colour.format+" standTrees=36 standSpan="+LocalTreePrototype.StandSpan+" standValid="+asset.StandValid+" ms="+timer.Elapsed.TotalMilliseconds.ToString("F1",System.Globalization.CultureInfo.InvariantCulture);
            lines.Add(line);Debug.Log("TREE PROTOTYPE EXPORTED "+line);
        }
        File.WriteAllLines(Path.Combine(review,"export.txt"),lines);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
    }
    static T Publish<T>(T created,T existing,LocalTreePrototype owner,string name) where T:UnityEngine.Object
    {
        created.name=name;
        if(existing==null){AssetDatabase.AddObjectToAsset(created,owner);return created;}
        EditorUtility.CopySerialized(created,existing);UnityEngine.Object.DestroyImmediate(created);EditorUtility.SetDirty(existing);return existing;
    }
    static Texture2DArray Compress(Color32[][][] pixels,bool linear)
    {
        int size=(int)Math.Sqrt(pixels[0][0].Length);
        var array=new Texture2DArray(size,size,pixels.Length,TextureFormat.BC7,true,linear){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=2};
        for(int layer=0;layer<pixels.Length;layer++)
        {
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,linear);
            try
            {
                for(int mip=0;mip<pixels[layer].Length;mip++)texture.SetPixels32(pixels[layer][mip],mip);
                texture.Apply(false,false);EditorUtility.CompressTexture(texture,TextureFormat.BC7,TextureCompressionQuality.Best);
                for(int mip=0;mip<texture.mipmapCount;mip++)array.SetPixelData(texture.GetPixelData<byte>(mip),mip,layer);
            }
            finally{UnityEngine.Object.DestroyImmediate(texture);}
        }
        array.Apply(false,true);return array;
    }
    static void ContactSheet(Color32[][][] pixels,string path)
    {
        int size=(int)Math.Sqrt(pixels[0][0].Length);var sheet=new Texture2D(size*4,size*4,TextureFormat.RGBA32,false);
        try{for(int view=0;view<pixels.Length;view++)sheet.SetPixels32(view%4*size,view/4*size,size,size,pixels[view][0]);sheet.Apply(false,false);File.WriteAllBytes(path,sheet.EncodeToPNG());}
        finally{UnityEngine.Object.DestroyImmediate(sheet);}
    }
}
