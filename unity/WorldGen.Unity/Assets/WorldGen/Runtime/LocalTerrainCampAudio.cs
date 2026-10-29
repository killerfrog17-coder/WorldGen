using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    readonly List<AudioClip> campRecordings=new List<AudioClip>();
    readonly Dictionary<string,AudioSource> campVoices=new Dictionary<string,AudioSource>();
    Coroutine campAudioLoading;UnityWebRequest campAudioRequest;bool campAudioStarted;
    public bool CampAudioReady{get;private set;}
    public int CampRecordingCount=>campRecordings.Count;

    IEnumerator LoadCampRecordings()
    {
        string root=Path.Combine(WeatherSoundsRoot(),"campfire"),screen=Path.Combine(root,"screened.tsv");
        if(File.Exists(screen))foreach(string line in File.ReadAllLines(screen))
        {
            var fields=line.Split('\t');if(fields.Length!=2||Path.GetFileName(fields[0])!=fields[0]||!fields[0].EndsWith(".wav",StringComparison.OrdinalIgnoreCase))continue;
            string path=Path.Combine(root,fields[0]);if(!File.Exists(path))continue;
            string hash;using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            if(hash!=fields[1]){Debug.LogWarning("Changed campfire recording skipped: "+fields[0]);continue;}
            var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,AudioType.WAV);campAudioRequest=request;
            ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio=false;yield return request.SendWebRequest();
            if(request.result==UnityWebRequest.Result.Success)
            {
                var clip=DownloadHandlerAudioClip.GetContent(request);
                if(clip!=null&&clip.channels==1&&clip.length>=10&&clip.length<=90){clip.name=fields[0];campRecordings.Add(clip);}
                else if(clip!=null)Destroy(clip);
            }
            request.Dispose();campAudioRequest=null;
        }
        CampAudioReady=true;campAudioLoading=null;
    }
    void UpdateCampVoice(CampObject camp,CampVisual visual)
    {
        // Voices survive fuel-mesh rebuilds, so burning down wood does not
        // restart the recording or create an audible repeated onset.
        if(!camp.Burning||campRecordings.Count==0||(camp.Position-Position).Length*frame.RadiusM>24)return;
        if(!campVoices.TryGetValue(camp.Id,out var voice))
        {
            var root=new GameObject("Campfire recording");root.transform.SetParent(transform,false);
            voice=root.AddComponent<AudioSource>();voice.playOnAwake=false;voice.loop=true;voice.spatialBlend=1;voice.rolloffMode=AudioRolloffMode.Logarithmic;voice.minDistance=1.2f;voice.maxDistance=24;
            uint choice=Rng.Mix((uint)camp.Id.GetHashCode());voice.clip=campRecordings[(int)(choice%(uint)campRecordings.Count)];voice.time=(choice%10007)/10007f*voice.clip.length;
            campVoices.Add(camp.Id,voice);voice.Play();
        }
        voice.transform.position=visual.Root.transform.position+Vector3.up*.2f;
        voice.volume=.4f*ItemEffectVolume*Mathf.Clamp((float)camp.FirePowerKw*.5f,.25f,1);voice.mute=paused;visual.Sound=voice;
    }
    void RetireCampVoices()
    {
        foreach(string id in campVoices.Keys.Where(id=>!Camp.Objects.Any(c=>c.Id==id&&c.Burning&&(c.Position-Position).Length*frame.RadiusM<24)).ToArray())
        {Destroy(campVoices[id].gameObject);campVoices.Remove(id);}
    }
    void DisposeCampAudio()
    {
        if(campAudioLoading!=null)StopCoroutine(campAudioLoading);campAudioLoading=null;
        if(campAudioRequest!=null){campAudioRequest.Abort();campAudioRequest.Dispose();campAudioRequest=null;}
        foreach(var voice in campVoices.Values)if(voice!=null)Destroy(voice.gameObject);campVoices.Clear();
        foreach(var clip in campRecordings)if(clip!=null)Destroy(clip);campRecordings.Clear();campAudioStarted=CampAudioReady=false;
    }
}
