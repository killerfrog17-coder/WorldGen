using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    readonly Dictionary<string,List<AudioClip>> bodyClips=new Dictionary<string,List<AudioClip>>();
    readonly Dictionary<ulong,Vector3> sleepingAnimalPositions=new Dictionary<ulong,Vector3>();
    AudioSource bodyVoice,pantVoice;LocalSurvivalScreen bodyScreen;Coroutine bodyAudioLoading;
    UnityWebRequest bodyAudioRequest;bool bodyAudioStarted;double nextBodyCue,nextPainCue,nextSleepNoise;float bodyLastHealth=-1,bodyPain,bodyDamage;uint bodySoundSequence=29283;
    IEnumerator LoadBodyAudio()
    {
        string root=Path.Combine(WeatherSoundsRoot(),"player-survival"),index=Path.Combine(root,"screened.tsv");
        if(File.Exists(index))foreach(string line in File.ReadAllLines(index))
        {
            var parts=line.Split('\t');if(parts.Length!=2||Path.GetFileName(parts[0])!=parts[0])continue;
            string path=Path.Combine(root,parts[0]);if(!File.Exists(path))continue;
            using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=parts[1])continue;
            bodyAudioRequest=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,AudioType.WAV);yield return bodyAudioRequest.SendWebRequest();
            if(bodyAudioRequest.result==UnityWebRequest.Result.Success)
            {
                var clip=DownloadHandlerAudioClip.GetContent(bodyAudioRequest);
                if(clip!=null&&clip.channels==1&&clip.length>.2&&clip.length<12)
                {string key=parts[0].Split('_')[0];if(!bodyClips.TryGetValue(key,out var bank)){bank=new List<AudioClip>();bodyClips.Add(key,bank);}clip.name=parts[0];bank.Add(clip);}
                else if(clip!=null)Destroy(clip);
            }
            bodyAudioRequest.Dispose();bodyAudioRequest=null;
        }
        bodyAudioLoading=null;
    }
    AudioSource BodyVoice(bool pant)
    {
        var source=pant?pantVoice:bodyVoice;if(source!=null)return source;
        var root=new GameObject(pant?"Recovery breathing":"Player body foley");root.transform.SetParent(transform,false);
        source=root.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.dopplerLevel=0;source.priority=pant?90:35;source.volume=0;
        if(pant)pantVoice=source;else bodyVoice=source;return source;
    }
    void PlayBodyCue(string category,bool urgent=false)
    {
        if(paused||DeathVisible||!bodyClips.TryGetValue(category,out var clips)||clips.Count==0)return;
        var voice=BodyVoice(false);if(voice.isPlaying&&!urgent)return;
        bodySoundSequence=Rng.Mix(bodySoundSequence+1);voice.clip=clips[(int)(bodySoundSequence%(uint)clips.Count)];voice.loop=false;voice.volume=ItemEffectVolume*(urgent?.72f:.34f);voice.pitch=.97f+(bodySoundSequence%7)*.01f;voice.Play();
    }
    void UpdateBodyFeedback()
    {
        if(!Ready||view==null)return;
        if(!bodyAudioStarted){bodyAudioStarted=true;bodyAudioLoading=StartCoroutine(LoadBodyAudio());}
        if(bodyScreen==null)bodyScreen=view.gameObject.AddComponent<LocalSurvivalScreen>();
        bool active=SurvivalActive&&!DeathVisible&&!paused&&!mapOpen&&!chatOpen&&!ecologyOpen;
        if(bodyLastHealth<0)bodyLastHealth=(float)Vitals.Health;
        float lost=bodyLastHealth-(float)Vitals.Health;bodyLastHealth=(float)Vitals.Health;
        if(active&&lost>0){bodyDamage+=lost;bodyPain=Mathf.Min(1,bodyPain+lost*.15f);}
        if(active&&bodyDamage>.15f&&Time.unscaledTimeAsDouble>nextPainCue)
        {PlayBodyCue("pain",true);bodyDamage=0;nextPainCue=Time.unscaledTimeAsDouble+1.2;nextBodyCue=Math.Max(nextBodyCue,Time.unscaledTimeAsDouble+5);}
        bodyPain=Mathf.MoveTowards(bodyPain,0,Time.unscaledDeltaTime*1.2f);
        float cold=active?Mathf.Clamp01((35.5f-(float)Vitals.CoreTemperatureC)/4.5f):0,hot=active?Mathf.Clamp01(((float)Vitals.CoreTemperatureC-38.3f)/2.8f):0;
        float weak=active?Mathf.Clamp01((35-(float)Vitals.Health)/35):0;
        bodyScreen.Set(new Vector4(cold,hot,weak,active?bodyPain:0));
        bool moving=active&&!campSleeping&&!campResting&&!inventoryOpen;
        if(moving&&cold>.08f)
        {
            float shake=cold*cold*.27f,t=Time.unscaledTime;
            view.transform.localRotation*=Quaternion.Euler(Mathf.Sin(t*41)*shake,Mathf.Sin(t*37+1)*shake*.55f,Mathf.Sin(t*29)*shake*.35f);
            view.transform.localPosition+=Vector3.up*Mathf.Sin(t*43)*cold*.0015f;
        }
        bool breath=moving&&(Vitals.Exertion?.Breath>.55||Vitals.Stamina<Math.Min(22,Vitals.Energy*.4))&&Vitals.BreathDebtSeconds<.1;
        if(breath&&bodyClips.TryGetValue("pant",out var pants)&&pants.Count>0)
        {
            var source=BodyVoice(true);if(!source.isPlaying){bodySoundSequence=Rng.Mix(bodySoundSequence+1);source.clip=pants[(int)(bodySoundSequence%(uint)pants.Count)];source.loop=true;source.Play();}
            source.volume=Mathf.MoveTowards(source.volume,.35f*ItemEffectVolume,Time.unscaledDeltaTime*.4f);
        }
        else if(pantVoice!=null){pantVoice.volume=Mathf.MoveTowards(pantVoice.volume,0,Time.unscaledDeltaTime*.4f);if(pantVoice.volume<.001f)pantVoice.Stop();}
        if(!active){if(bodyVoice!=null)bodyVoice.Stop();return;}
        if(campSleeping||campResting||inventoryOpen||Time.unscaledTimeAsDouble<nextBodyCue)return;
        var cues=new List<string>();if(Vitals.Thirst>65)cues.Add("throat");if(SleepRules.Sleepiness(Vitals,LocalSolarHour)>65)cues.Add("yawn");if(cold>.25)cues.Add("shiver");if(Vitals.Hunger>75)cues.Add("stomach");
        if(cues.Count>0){bodySoundSequence=Rng.Mix(bodySoundSequence+1);PlayBodyCue(cues[(int)(bodySoundSequence%(uint)cues.Count)]);nextBodyCue=Time.unscaledTimeAsDouble+Math.Max(15,Math.Min(90,PlayerRules.Settings.RealMinutesPerDay*.9))*(.8+bodySoundSequence%60*.01);}
    }
    void ReportSleepNoise(Vector3 point,float loudness)
    {
        if(!campSleeping||paused||loudness<=0)return;
        float distance=Vector3.Distance(point,transform.TransformPoint(PlayerFeet()));
        double audible=loudness/(1+distance*distance/36),threshold=Vitals.Tiredness>55&&Vitals.SleepBoutSeconds>1800?.6:.24;
        if(audible>threshold)WakeCamp("A nearby sound wakes you.");
    }
    void UpdateSleepDisturbances()
    {
        if(!campSleeping){sleepingAnimalPositions.Clear();return;}
        if(Time.unscaledTimeAsDouble<nextSleepNoise)return;nextSleepNoise=Time.unscaledTimeAsDouble+.35;
        foreach(var animal in animals.Values)
        {
            if(animal.Dead||animal.Retired||animal.Root==null||animal.MassKg<.3)continue;
            Vector3 point=animal.Root.transform.position;
            if((point-transform.TransformPoint(PlayerFeet())).sqrMagnitude>10*10)continue;
            if(sleepingAnimalPositions.TryGetValue(animal.Spawn.Id,out var old)&&(point-old).sqrMagnitude>.015f)
                ReportSleepNoise(point,(float)Math.Min(3,.4+.1*Math.Sqrt(animal.MassKg)));
            sleepingAnimalPositions[animal.Spawn.Id]=point;if(!campSleeping)break;
        }
    }
    void DisposeBodyFeedback()
    {
        if(bodyAudioLoading!=null)StopCoroutine(bodyAudioLoading);bodyAudioLoading=null;
        if(bodyAudioRequest!=null){bodyAudioRequest.Abort();bodyAudioRequest.Dispose();bodyAudioRequest=null;}
        if(bodyVoice!=null)Destroy(bodyVoice.gameObject);if(pantVoice!=null)Destroy(pantVoice.gameObject);
        foreach(var bank in bodyClips.Values)foreach(var clip in bank)Destroy(clip);bodyClips.Clear();
        if(bodyScreen!=null)Destroy(bodyScreen);bodyAudioStarted=false;
    }
}
