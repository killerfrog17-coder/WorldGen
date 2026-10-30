using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;

/// <summary>Additional palettes from the identical original rig, never a foreign retarget.</summary>
sealed class FaunaAdditionalMotion
{
    [Serializable] sealed class Header {public string slug,source_sha256;public int bones,frames;public FaunaRuntimeClip[] clips;}
    readonly Header header;readonly Matrix4x4[] poses;readonly float[] morphs;
    public FaunaAdditionalMotion(string path,string slug,string source,int bones)
    {
        using(var file=File.OpenRead(path))using(var zip=new GZipStream(file,CompressionMode.Decompress))using(var reader=new BinaryReader(zip))
        {
            if(System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8))!="WGEXTRA1")throw new InvalidDataException("Additional animal motion signature.");
            string id=BitConverter.ToString(reader.ReadBytes(32)).Replace("-","").ToLowerInvariant();int boneCount=reader.ReadInt32(),frames=reader.ReadInt32(),jsonSize=reader.ReadInt32();
            if(id!=source||boneCount!=bones||frames<2||frames>12000||jsonSize<2||jsonSize>1024*1024)throw new InvalidDataException("Additional motion/source mismatch.");
            header=JsonUtility.FromJson<Header>(System.Text.Encoding.UTF8.GetString(reader.ReadBytes(jsonSize)));
            if(header.slug!=slug||header.source_sha256!=source||header.bones!=bones||header.frames!=frames||header.clips==null||header.clips.Length>64)throw new InvalidDataException("Additional motion metadata mismatch.");
            foreach(var c in header.clips)if(c.start<0||c.frames<2||c.start+c.frames>frames||c.duration<=0||c.duration>90)throw new InvalidDataException("Additional motion range.");
            poses=new Matrix4x4[checked(frames*bones)];
            for(int i=0;i<poses.Length;i++)for(int row=0;row<4;row++)for(int col=0;col<4;col++){float v=reader.ReadSingle();if(!float.IsFinite(v))throw new InvalidDataException("Non-finite animal palette.");var m=poses[i];m[row,col]=v;poses[i]=m;}
            morphs=new float[frames];for(int i=0;i<frames;i++){morphs[i]=reader.ReadSingle();if(!float.IsFinite(morphs[i]))throw new InvalidDataException("Non-finite animal morph.");}
            if(zip.ReadByte()!=-1)throw new InvalidDataException("Additional motion trailing bytes.");
        }
    }
    public void Append(WorldGenFaunaAsset asset)
    {
        int first=asset.Poses.Length/asset.BoneCount;
        foreach(var clip in header.clips)clip.start+=first;
        asset.Clips=asset.Clips.Concat(header.clips).ToArray();asset.Poses=asset.Poses.Concat(poses).ToArray();
        var old=asset.Morphs??new float[first];asset.Morphs=old.Concat(morphs).ToArray();
    }
}
