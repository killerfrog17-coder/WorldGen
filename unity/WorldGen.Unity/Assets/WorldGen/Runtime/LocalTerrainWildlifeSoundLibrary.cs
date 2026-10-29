using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class CachedWildlifeSound
    {public AudioClip Clip;public string Hash;public double Used;public long Bytes;}
    sealed class WildlifeScan
    {public WildlifeSoundCatalogue Catalogue;public int Missing;}
    WildlifeSoundCatalogue wildlifeSounds=new WildlifeSoundCatalogue();
    readonly Dictionary<string,CachedWildlifeSound> wildlifeSoundCache=new Dictionary<string,CachedWildlifeSound>(StringComparer.Ordinal);
    readonly Queue<WildlifeSoundTake> wildlifeSoundQueue=new Queue<WildlifeSoundTake>();
    readonly Queue<WildlifeSoundTake> wildlifeSoundUrgent=new Queue<WildlifeSoundTake>();
    readonly HashSet<string> wildlifeSoundQueued=new HashSet<string>(StringComparer.Ordinal),wildlifeSoundBad=new HashSet<string>(StringComparer.Ordinal);
    Task<WildlifeScan> wildlifeSoundScan;
    Coroutine wildlifeSoundLoader;UnityWebRequest wildlifeSoundRequest;
    string wildlifeSoundRoot;double wildlifeNextScan,wildlifeNextEvict;int wildlifeMissing,wildlifeCatalogueVersion;
    bool wildlifeLibraryReady;
    const long WildlifeDecodedBudget=96L*1024*1024;
    public string WildlifeSoundFolder=>wildlifeSoundRoot??Path.Combine(WeatherSoundsRoot(),"fauna");
    public int FaunaClipCount(int group,int method=0)=>group>=0&&group<FaunaAmbience.KindCount?wildlifeSounds.Count(FaunaAmbience.Banks[group]):0;
    long WildlifeCachedBytes=>wildlifeSoundCache.Values.Sum(c=>c.Bytes);

    void ServiceWildlifeLibrary()
    {
        if(wildlifeSoundRoot==null)wildlifeSoundRoot=Path.Combine(WeatherSoundsRoot(),"fauna");
        double now=Time.unscaledTimeAsDouble;
        if(wildlifeSoundScan!=null&&wildlifeSoundScan.IsCompleted)
        {
            if(wildlifeSoundScan.Status==TaskStatus.RanToCompletion)
            {
                var result=wildlifeSoundScan.Result;wildlifeSounds=result.Catalogue;wildlifeMissing=result.Missing;wildlifeLibraryReady=true;wildlifeCatalogueVersion++;
                var present=new HashSet<string>(wildlifeSounds.Takes.Select(t=>t.File),StringComparer.Ordinal);
                foreach(string key in wildlifeSoundCache.Keys.Where(k=>!present.Contains(k)).ToArray())DiscardWildlifeClip(key);
                wildlifeSoundBad.RemoveWhere(key=>!present.Contains(key));
            }
            else if(wildlifeSoundScan.IsFaulted)Debug.LogWarning("Wildlife sound catalogue: "+wildlifeSoundScan.Exception.GetBaseException().Message);
            wildlifeSoundScan=null;
        }
        if(wildlifeSoundScan==null&&now>=wildlifeNextScan)
        {
            wildlifeNextScan=now+10;string root=wildlifeSoundRoot;
            wildlifeSoundScan=Task.Run(()=>
            {
                var result=new WildlifeScan{Catalogue=new WildlifeSoundCatalogue()};string manifest=Path.Combine(root,"library.tsv");
                if(!File.Exists(manifest))return result;
                var catalogue=WildlifeSoundCatalogue.Parse(File.ReadAllText(manifest));
                foreach(var entry in catalogue.Takes)
                {
                    var info=new FileInfo(Path.Combine(root,entry.File));
                    if(info.Exists&&info.Length>=100&&info.Length<=12*1024*1024)result.Catalogue.Add(entry);else result.Missing++;
                }
                return result;
            },faunaCancel.Token);
        }
        if(wildlifeSoundLoader==null&&wildlifeSoundQueue.Count+wildlifeSoundUrgent.Count>0)wildlifeSoundLoader=StartCoroutine(LoadWildlifeSoundQueue());
        if(now>=wildlifeNextEvict)
        {
            wildlifeNextEvict=now+2;
            foreach(var entry in wildlifeSoundCache.OrderBy(p=>p.Value.Used).ToArray())
            {
                if(WildlifeCachedBytes<=WildlifeDecodedBudget)break;
                if(!WildlifeClipPinned(entry.Value.Clip))DiscardWildlifeClip(entry.Key);
            }
        }
    }
    public void ReloadWildlifeAudio(){wildlifeNextScan=0;wildlifeSoundBad.Clear();}
    AudioClip WildlifeClip(WildlifeSoundTake take,bool urgent=false)
    {
        if(take==null||faunaDisposed||wildlifeSoundBad.Contains(take.File))return null;
        if(wildlifeSoundCache.TryGetValue(take.File,out var cached)&&cached.Hash==take.Sha256)
        {cached.Used=Time.unscaledTimeAsDouble;return cached.Clip;}
        if(urgent){if(wildlifeSoundQueued.Add(take.File))wildlifeSoundUrgent.Enqueue(take);else if(!wildlifeSoundUrgent.Any(t=>t.File==take.File))wildlifeSoundUrgent.Enqueue(take);}
        else if(wildlifeSoundQueued.Add(take.File))wildlifeSoundQueue.Enqueue(take);
        return null;
    }
    IEnumerator LoadWildlifeSoundQueue()
    {
        while(wildlifeSoundQueue.Count+wildlifeSoundUrgent.Count>0&&!faunaDisposed)
        {
            var entry=wildlifeSoundUrgent.Count>0?wildlifeSoundUrgent.Dequeue():wildlifeSoundQueue.Dequeue();string path=Path.Combine(wildlifeSoundRoot,entry.File);
            if(wildlifeSoundCache.TryGetValue(entry.File,out var existing)&&existing.Hash==entry.Sha256){wildlifeSoundQueued.Remove(entry.File);continue;}
            var verify=Task.Run(()=>
            {
                try{using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()==entry.Sha256;}
                catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
            },faunaCancel.Token);
            while(!verify.IsCompleted&&!faunaDisposed)yield return null;if(faunaDisposed)yield break;
            if(verify.Status!=TaskStatus.RanToCompletion||!verify.Result){wildlifeSoundBad.Add(entry.File);wildlifeSoundQueued.Remove(entry.File);continue;}
            wildlifeSoundRequest=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,AudioType.WAV);
            ((DownloadHandlerAudioClip)wildlifeSoundRequest.downloadHandler).streamAudio=false;
            yield return wildlifeSoundRequest.SendWebRequest();if(faunaDisposed)yield break;
            AudioClip clip=wildlifeSoundRequest.result==UnityWebRequest.Result.Success?DownloadHandlerAudioClip.GetContent(wildlifeSoundRequest):null;
            if(clip!=null&&clip.channels==1&&clip.length>=.05f&&clip.length<=120&&Math.Abs(clip.length-entry.Seconds)<.15)
            {
                clip.name=entry.File;if(wildlifeSoundCache.ContainsKey(entry.File))DiscardWildlifeClip(entry.File);
                wildlifeSoundCache[entry.File]=new CachedWildlifeSound{Clip=clip,Hash=entry.Sha256,Used=Time.unscaledTimeAsDouble,Bytes=(long)clip.samples*clip.channels*4};
            }
            else{if(clip!=null)Destroy(clip);wildlifeSoundBad.Add(entry.File);}
            wildlifeSoundRequest.Dispose();wildlifeSoundRequest=null;wildlifeSoundQueued.Remove(entry.File);
            yield return null;
        }
        wildlifeSoundLoader=null;
    }
    bool WildlifeClipPinned(AudioClip clip)
    {
        if(faunaPreview!=null&&faunaPreview.clip==clip)return true;
        foreach(var voice in faunaVoices)if(voice!=null)foreach(var source in voice.Sources)if(source.clip==clip)return true;
        foreach(var voice in creatureVoices)if(voice.Source.clip==clip)return true;
        return false;
    }
    void DiscardWildlifeClip(string key)
    {
        if(!wildlifeSoundCache.TryGetValue(key,out var value))return;
        if(faunaPreview!=null&&faunaPreview.clip==value.Clip)StopFaunaPreview();
        foreach(var voice in faunaVoices)if(voice!=null&&Array.Exists(voice.Sources,s=>s.clip==value.Clip))StopFaunaVoice(voice);
        foreach(var voice in creatureVoices)if(voice.Source.clip==value.Clip){voice.Source.Stop();voice.Source.clip=null;voice.Actor=null;}
        Destroy(value.Clip);wildlifeSoundCache.Remove(key);
    }
    void DisposeWildlifeLibrary()
    {
        if(wildlifeSoundLoader!=null)StopCoroutine(wildlifeSoundLoader);wildlifeSoundLoader=null;
        if(wildlifeSoundRequest!=null){wildlifeSoundRequest.Abort();wildlifeSoundRequest.Dispose();wildlifeSoundRequest=null;}
        foreach(var clip in wildlifeSoundCache.Values)Destroy(clip.Clip);wildlifeSoundCache.Clear();wildlifeSoundQueue.Clear();wildlifeSoundUrgent.Clear();wildlifeSoundQueued.Clear();
        if(wildlifeSoundScan!=null)wildlifeSoundScan.ContinueWith(t=>{if(t.IsFaulted){var observed=t.Exception;}},TaskScheduler.Default);
    }
}
