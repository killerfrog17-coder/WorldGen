using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>CPU-only native-asset conversion and per-animal streaming bundles.</summary>
public static class FaunaRuntimeBuilder
{
    const string Cache="Assets/WorldGen/Editor/GeneratedFauna";
    [Serializable] sealed class Model
    {
        public string slug,name,source_sha256;
        public int bones,parts,vertices,indices,textureSize;
        public int influences=4;
        public bool hasMorph;
        public float[] boundsMin,boundsMax;
        public float height;
        public FaunaRuntimeClip[] clips;
        public FaunaRuntimeCoat[] coats;
    }
    public static void Build()
    {
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        string input=Path.Combine(root,"artifacts/fauna-runtime-20261026/exports");
        var catalogue=JsonUtility.FromJson<WorldGen.Presentation.FaunaPrototypeCatalogue>(File.ReadAllText("Assets/WorldGen/Resources/WorldGenFaunaCatalogue.json"));
        var mapped=WorldGen.Presentation.FaunaPrototypePlanner.BindProfiles(WorldGen.Core.EarthFaunaPack.Create(),catalogue.animals);
        var missing=catalogue.animals.Where(p=>!p.derivedFirefly&&!mapped.Values.Contains(p)).Select(p=>p.slug).ToArray();
        if(missing.Length>0)throw new InvalidDataException("Animals without ecology mappings: "+string.Join(", ",missing));
        Debug.Log("FAUNA ECOLOGY MAP "+mapped.Count+" taxa; all "+catalogue.animals.Length+" models mapped or explicitly derived fireflies.");
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--fauna-slugs");
        var selected=at>=0?new HashSet<string>(args[at+1].Split(',')):null;
        Directory.CreateDirectory(Cache);AssetDatabase.Refresh();var builds=new List<AssetBundleBuild>();
        foreach(var directory in Directory.GetDirectories(input).OrderBy(p=>p,StringComparer.Ordinal))
        {
            string slug=Path.GetFileName(directory);if(selected!=null&&!selected.Contains(slug))continue;
            if(!File.Exists(Path.Combine(directory,"receipt.json")))throw new InvalidDataException("Incomplete animal export: "+slug);
            var meta=JsonUtility.FromJson<Model>(File.ReadAllText(Path.Combine(directory,"model.json")));
            string path=Cache+"/"+slug+".asset",stamp=Cache+"/"+slug+".stamp";
            string signature;
            using(var hash=SHA256.Create())signature=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,"WorldGen/Editor/FaunaRuntimeBuilder.cs"))));
            signature+="\n"+File.ReadAllText(Path.Combine(directory,"receipt.json"));
            if(!File.Exists(path)||!File.Exists(stamp)||File.ReadAllText(stamp)!=signature)
            {
                if(File.Exists(path))AssetDatabase.DeleteAsset(path);
                var asset=ScriptableObject.CreateInstance<WorldGenFaunaAsset>();asset.name=slug;
                asset.Slug=slug;asset.SourceSha256=meta.source_sha256;asset.BoneCount=meta.bones;asset.Height=meta.height;
                asset.Clips=meta.clips;asset.Variations=meta.coats;
                AssetDatabase.CreateAsset(asset,path);
                asset.Mesh=ReadMesh(directory,meta);AssetDatabase.AddObjectToAsset(asset.Mesh,asset);
                asset.Poses=ReadPoses(directory,meta.bones);
                if(meta.hasMorph)
                {
                    using var morph=new BinaryReader(File.OpenRead(Path.Combine(directory,"morph.bin")));
                    asset.Morphs=new float[asset.Poses.Length/meta.bones];for(int i=0;i<asset.Morphs.Length;i++)asset.Morphs[i]=F(morph);
                    if(morph.BaseStream.Position!=morph.BaseStream.Length)throw new InvalidDataException("Fauna morph count");
                }
                asset.Coats=CoatArray(directory,meta);AssetDatabase.AddObjectToAsset(asset.Coats,asset);
                asset.Mask=Texture(directory,"mask.png",true);AssetDatabase.AddObjectToAsset(asset.Mask,asset);
                asset.Normal=Texture(directory,"normal.png",true);AssetDatabase.AddObjectToAsset(asset.Normal,asset);
                EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();File.WriteAllText(stamp,signature);
                Debug.Log("FAUNA ASSET "+slug+" "+meta.vertices+" vertices / "+meta.clips.Length+" clips / "+meta.coats.Length+" coats");
            }
            builds.Add(new AssetBundleBuild{assetBundleName=slug+".fauna",assetNames=new[]{path}});
        }
        string output=Path.Combine(Application.streamingAssetsPath,"fauna");Directory.CreateDirectory(output);
        var result=BuildPipeline.BuildAssetBundles(output,builds.ToArray(),BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64);
        if(result==null)throw new InvalidOperationException("Fauna asset bundle build failed");
        Debug.Log("FAUNA BUNDLES READY "+builds.Count+"; source natives were read-only; no game launched.");
    }
    static float F(BinaryReader r)
    {float x=r.ReadSingle();if(float.IsNaN(x)||float.IsInfinity(x))throw new InvalidDataException("Nonfinite fauna data");return x;}
    static Mesh ReadMesh(string directory,Model meta)
    {
        using var r=new BinaryReader(File.OpenRead(Path.Combine(directory,"mesh.bin")));
        if(new string(r.ReadChars(8))!="WGFAUNA1")throw new InvalidDataException("Fauna mesh version");
        int count=r.ReadInt32(),indices=r.ReadInt32(),bones=r.ReadInt32(),parts=r.ReadInt32();
        if(count!=meta.vertices||indices!=meta.indices||bones!=meta.bones||parts!=meta.parts||count<3||count>200000||bones>128||parts>64)throw new InvalidDataException("Fauna mesh counts");
        if(meta.influences!=4&&meta.influences!=8)throw new InvalidDataException("Fauna influence format");
        var p=new Vector3[count];var n=new Vector3[count];var uv=new Vector2[count];var ids=new List<Vector4>(count);var weights=new List<Vector4>(count);var part=new List<Vector2>(count);
        var extraIds=new List<Vector4>(count);var extraWeights=new List<Vector4>(count);
        var morphs=new List<Vector3>(count);
        for(int i=0;i<count;i++)
        {
            p[i]=new Vector3(F(r),F(r),F(r));n[i]=new Vector3(F(r),F(r),F(r));uv[i]=new Vector2(F(r),F(r));
            var id=new Vector4(F(r),F(r),F(r),F(r));var moreId=meta.influences==8?new Vector4(F(r),F(r),F(r),F(r)):Vector4.zero;
            var w=new Vector4(F(r),F(r),F(r),F(r));var moreW=meta.influences==8?new Vector4(F(r),F(r),F(r),F(r)):Vector4.zero;float item=F(r);
            morphs.Add(meta.hasMorph?new Vector3(F(r),F(r),F(r)):Vector3.zero);
            for(int k=0;k<4;k++)if(id[k]<0||id[k]>=bones||w[k]<0||moreId[k]<0||moreId[k]>=bones||moreW[k]<0)throw new InvalidDataException("Fauna skin index/weight");
            if(Math.Abs(w.x+w.y+w.z+w.w+moreW.x+moreW.y+moreW.z+moreW.w-1)>1e-4||item<0||item>=parts)throw new InvalidDataException("Fauna skin normalization/part");
            ids.Add(id);weights.Add(w);extraIds.Add(moreId);extraWeights.Add(moreW);part.Add(new Vector2(item,0));
        }
        var tri=new int[indices];for(int i=0;i<indices;i++){tri[i]=r.ReadInt32();if(tri[i]<0||tri[i]>=count)throw new InvalidDataException("Fauna triangle");}
        if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("Trailing mesh data");
        var mesh=new Mesh{name=meta.slug+" original skin",indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
        mesh.vertices=p;mesh.normals=n;mesh.uv=uv;mesh.SetUVs(2,ids);mesh.SetUVs(3,weights);mesh.SetUVs(4,part);
        mesh.SetUVs(5,extraIds);mesh.SetUVs(6,extraWeights);mesh.SetUVs(7,morphs);mesh.triangles=tri;mesh.RecalculateTangents();
        Vector3 low=new Vector3(meta.boundsMin[0],meta.boundsMin[1],meta.boundsMin[2]),high=new Vector3(meta.boundsMax[0],meta.boundsMax[1],meta.boundsMax[2]);
        mesh.bounds=new Bounds((low+high)*.5f,(high-low)*1.15f+Vector3.one*.03f);mesh.UploadMeshData(true);return mesh;
    }
    static Matrix4x4[] ReadPoses(string directory,int expectedBones)
    {
        using var r=new BinaryReader(File.OpenRead(Path.Combine(directory,"motion.bin")));
        if(new string(r.ReadChars(8))!="WGPOSES1")throw new InvalidDataException("Fauna motion version");
        int count=r.ReadInt32(),bones=r.ReadInt32();if(count<2||count>30000||bones!=expectedBones)throw new InvalidDataException("Fauna motion counts");
        var result=new Matrix4x4[count*bones];for(int i=0;i<result.Length;i++)for(int row=0;row<4;row++)for(int col=0;col<4;col++)result[i][row,col]=F(r);
        if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("Trailing motion data");return result;
    }
    static Texture2D Texture(string directory,string name,bool linear)
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,true,linear){name=Path.GetFileNameWithoutExtension(name),wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=2};
        if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(Path.Combine(directory,name)),false))throw new InvalidDataException(name);
        texture.Apply(true,false);EditorUtility.CompressTexture(texture,TextureFormat.BC7,TextureCompressionQuality.Fast);return texture;
    }
    static Texture2DArray CoatArray(string directory,Model meta)
    {
        var array=new Texture2DArray(meta.textureSize,meta.textureSize,meta.coats.Length,TextureFormat.BC7,true,false){name="Coat pigments",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp,anisoLevel=2};
        for(int i=0;i<meta.coats.Length;i++)
        {
            var source=Texture(directory,"coat-"+i+".png",false);
            if(source.width!=meta.textureSize||source.height!=meta.textureSize)throw new InvalidDataException("Coat size");
            for(int mip=0;mip<source.mipmapCount;mip++)array.SetPixelData(source.GetPixelData<byte>(mip),mip,i);
            UnityEngine.Object.DestroyImmediate(source);
        }
        array.Apply(false,true);return array;
    }
}
