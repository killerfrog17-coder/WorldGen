using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using UnityEngine;

/// <summary>Additional palettes from the identical original rig, never a foreign retarget.</summary>
sealed class FaunaAdditionalMotion
{
    [Serializable] sealed class Header {public string slug,source_sha256;public int bones,frames;public FaunaRuntimeClip[] clips;}
    readonly Header header;readonly Matrix4x4[] poses,basePoses;readonly float[] morphs,baseMorphs;readonly FaunaRuntimeClip[] clips,baseClips;
    public FaunaAdditionalMotion(string path,string slug,string source,int bones,Matrix4x4[] originalPoses,float[] originalMorphs,FaunaRuntimeClip[] originalClips,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        if(bones<=0||originalPoses==null||originalPoses.Length%bones!=0||originalClips==null)throw new InvalidDataException("Invalid base animal motion.");
        basePoses=originalPoses;baseMorphs=originalMorphs;baseClips=originalClips;int first=originalPoses.Length/bones;
        using(var file=File.OpenRead(path))using(var zip=new GZipStream(file,CompressionMode.Decompress))using(var reader=new BinaryReader(zip))
        {
            if(System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8))!="WGEXTRA1")throw new InvalidDataException("Additional animal motion signature.");
            string id=BitConverter.ToString(reader.ReadBytes(32)).Replace("-","").ToLowerInvariant();int boneCount=reader.ReadInt32(),frames=reader.ReadInt32(),jsonSize=reader.ReadInt32();
            if(id!=source||boneCount!=bones||frames<2||frames>12000||jsonSize<2||jsonSize>1024*1024)throw new InvalidDataException("Additional motion/source mismatch.");
            header=JsonUtility.FromJson<Header>(System.Text.Encoding.UTF8.GetString(reader.ReadBytes(jsonSize)));
            if(header.slug!=slug||header.source_sha256!=source||header.bones!=bones||header.frames!=frames||header.clips==null||header.clips.Length>64)throw new InvalidDataException("Additional motion metadata mismatch.");
            foreach(var c in header.clips)if(c.start<0||c.frames<2||c.start+c.frames>frames||c.duration<=0||c.duration>90)throw new InvalidDataException("Additional motion range.");
            // Allocate the final palette once on the loading worker, then read
            // additions directly into their final range. No concatenation or
            // multi-megabyte allocation remains on the presentation thread.
            poses=new Matrix4x4[checked(originalPoses.Length+frames*bones)];Array.Copy(originalPoses,poses,originalPoses.Length);
            for(int i=originalPoses.Length;i<poses.Length;i++)
            {
                if((i&1023)==0)token.ThrowIfCancellationRequested();
                for(int row=0;row<4;row++)for(int col=0;col<4;col++){float v=reader.ReadSingle();if(!float.IsFinite(v))throw new InvalidDataException("Non-finite animal palette.");var m=poses[i];m[row,col]=v;poses[i]=m;}
            }
            morphs=new float[checked(first+frames)];if(originalMorphs!=null)Array.Copy(originalMorphs,morphs,Math.Min(first,originalMorphs.Length));
            for(int i=first;i<morphs.Length;i++){morphs[i]=reader.ReadSingle();if(!float.IsFinite(morphs[i]))throw new InvalidDataException("Non-finite animal morph.");}
            if(zip.ReadByte()!=-1)throw new InvalidDataException("Additional motion trailing bytes.");
            clips=new FaunaRuntimeClip[originalClips.Length+header.clips.Length];Array.Copy(originalClips,clips,originalClips.Length);
            for(int i=0;i<header.clips.Length;i++){header.clips[i].start+=first;clips[originalClips.Length+i]=header.clips[i];}
            token.ThrowIfCancellationRequested();
        }
    }
    public void Append(WorldGenFaunaAsset asset)
    {
        if(!ReferenceEquals(asset.Poses,basePoses)||!ReferenceEquals(asset.Morphs,baseMorphs)||!ReferenceEquals(asset.Clips,baseClips))throw new InvalidOperationException("Animal base motion changed during preparation.");
        asset.Clips=clips;asset.Poses=poses;asset.Morphs=morphs;
    }
    public static System.Collections.IEnumerator Check(string output)
    {
        Directory.CreateDirectory(output);Application.runInBackground=true;
        Application.logMessageReceived+=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        long values=0;int models=0;long largestPublicationBytes=0;bool failureChecks=false;
        foreach(string path in Directory.GetFiles(Path.Combine(Application.streamingAssetsPath,"fauna-motion"),"*.bin.gz").OrderBy(p=>p,StringComparer.Ordinal))
        {
            string slug=Path.GetFileName(path).Replace(".bin.gz","");
            var request=AssetBundle.LoadFromFileAsync(Path.Combine(Application.streamingAssetsPath,"fauna",slug+".fauna"));yield return request;
            var bundle=request.assetBundle;if(bundle==null)throw new InvalidDataException("Missing original fauna bundle: "+slug);
            try
            {
                var assetRequest=bundle.LoadAllAssetsAsync<WorldGenFaunaAsset>();yield return assetRequest;
                var asset=assetRequest.allAssets.OfType<WorldGenFaunaAsset>().Single();
                var originalPoses=asset.Poses;var originalMorphs=asset.Morphs;var originalClips=asset.Clips;
                int bones=asset.BoneCount,first=originalPoses.Length/bones;string source=asset.SourceSha256;
                var task=System.Threading.Tasks.Task.Run(()=>new FaunaAdditionalMotion(path,slug,source,bones,originalPoses,originalMorphs,originalClips));
                while(!task.IsCompleted)yield return null;
                var prepared=task.GetAwaiter().GetResult();
                if(!ReferenceEquals(asset.Poses,originalPoses)||!ReferenceEquals(asset.Morphs,originalMorphs)||!ReferenceEquals(asset.Clips,originalClips))throw new InvalidOperationException("Worker mutated the published asset");
                long allocation=GC.GetAllocatedBytesForCurrentThread();prepared.Append(asset);allocation=GC.GetAllocatedBytesForCurrentThread()-allocation;
                largestPublicationBytes=Math.Max(largestPublicationBytes,allocation);
                if(allocation>1024)throw new InvalidOperationException("Main-thread publication allocated a palette: "+allocation);
                void Equal(float a,float b){values++;if(BitConverter.SingleToInt32Bits(a)!=BitConverter.SingleToInt32Bits(b))throw new InvalidDataException("Changed palette/morph bits: "+slug);}
                for(int i=0;i<originalPoses.Length;i++)for(int row=0;row<4;row++)for(int col=0;col<4;col++)Equal(asset.Poses[i][row,col],originalPoses[i][row,col]);
                for(int i=0;i<first;i++)Equal(asset.Morphs[i],originalMorphs!=null&&i<originalMorphs.Length?originalMorphs[i]:0);
                for(int i=0;i<originalClips.Length;i++)if(!ReferenceEquals(asset.Clips[i],originalClips[i]))throw new InvalidDataException("Base clip identity changed");
                // Independent file walk: compare every added component and the
                // original clip-offset rule without calling the new preparation.
                using(var file=File.OpenRead(path))using(var zip=new GZipStream(file,CompressionMode.Decompress))using(var reader=new BinaryReader(zip))
                {
                    reader.ReadBytes(40);int count=reader.ReadInt32(),frames=reader.ReadInt32(),size=reader.ReadInt32();
                    var header=JsonUtility.FromJson<Header>(System.Text.Encoding.UTF8.GetString(reader.ReadBytes(size)));
                    if(count!=bones||asset.Poses.Length!=originalPoses.Length+frames*bones||asset.Morphs.Length!=first+frames||asset.Clips.Length!=originalClips.Length+header.clips.Length)throw new InvalidDataException("Combined dimensions changed");
                    for(int i=originalPoses.Length;i<asset.Poses.Length;i++)for(int row=0;row<4;row++)for(int col=0;col<4;col++)Equal(asset.Poses[i][row,col],reader.ReadSingle());
                    for(int i=first;i<asset.Morphs.Length;i++)Equal(asset.Morphs[i],reader.ReadSingle());
                    for(int i=0;i<header.clips.Length;i++)
                    {var a=asset.Clips[originalClips.Length+i];var b=header.clips[i];if(a.name!=b.name||a.role!=b.role||a.start!=b.start+first||a.frames!=b.frames||a.duration!=b.duration)throw new InvalidDataException("Additional clip mapping changed");}
                    if(zip.ReadByte()!=-1)throw new InvalidDataException("Reference stream trailing bytes");
                }
                if(!failureChecks)
                {
                    bool cancelled=false;try{new FaunaAdditionalMotion(path,slug,source,bones,originalPoses,originalMorphs,originalClips,new CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}
                    if(!cancelled)throw new InvalidOperationException("Cancelled motion preparation continued");
                    bool stale=false;try{prepared.Append(asset);}catch(InvalidOperationException){stale=true;}
                    if(!stale)throw new InvalidOperationException("Stale preparation overwritten published palettes");
                    string bad=Path.Combine(output,"truncated-motion.bin.gz");var bytes=File.ReadAllBytes(path);using(var file=File.Create(bad))file.Write(bytes,0,bytes.Length/2);
                    bool rejected=false;try{new FaunaAdditionalMotion(bad,slug,source,bones,originalPoses,originalMorphs,originalClips);}catch(InvalidDataException){rejected=true;}catch(EndOfStreamException){rejected=true;}
                    File.Delete(bad);if(!rejected)throw new InvalidOperationException("Truncated extra motion accepted");failureChecks=true;
                }
                models++;File.AppendAllText(Path.Combine(output,"models.txt"),slug+" values="+values+" publication_bytes="+allocation+"\n");
            }
            finally{bundle.Unload(true);}
            yield return null;
        }
        if(models==0)throw new InvalidDataException("No extra animations checked");
        File.WriteAllText(Path.Combine(output,"complete.txt"),"PASS "+models+" original animal motion packs, "+values+" bit-exact palette/morph components, all original clip identities and extra frame offsets. Worker preparation leaves asset unchanged; stale/cancelled/truncated inputs rejected. Largest main-thread publication allocation="+largestPublicationBytes+" bytes. No FPS claim.");Application.Quit(0);
    }

}
