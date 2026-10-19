using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    AudioClip[] thunderClips=Array.Empty<AudioClip>();
    readonly List<AudioClip> weatherRecordings=new List<AudioClip>();
    AudioSource rainAudio;AudioClip rainClip;RainSynth rainSynth;bool rainPaused;
    Coroutine weatherSoundLoading;UnityWebRequest weatherSoundRequest;
    bool weatherSoundDisposed;
    public bool WeatherSoundsReady {get;private set;}
    public int WeatherRecordingCount=>weatherRecordings.Count;
    public bool WeatherSoundBanksReady=>thunderClips.Length>=2&&rainSynth!=null;
    public int RainRecordingVoices=>rainAudio!=null&&rainAudio.isPlaying?1:0;
    public int ActiveWeatherBedVoices=>RainRecordingVoices;
    public float RainRecordingGain=>rainAudio==null?0:rainAudio.volume*(1-Mathf.Exp(-rainSynth.MillimetresPerHour/7));
    public int LastThunderVariant {get;private set;}=-1;
    public string WeatherSoundDebug=>"Audio · original rain "+RainRecordingVoices+" · thunder "+ActiveThunderCount+" / queued "+PendingThunderCount;
    public bool WeatherRecordingSignalsValid=>WeatherSoundBanksReady&&ThunderSignalsValid;

    static string WeatherSoundsRoot()
    {
        var args=Environment.GetCommandLineArgs();
        for(int i=0;i<args.Length-1;i++)if(args[i]=="--worldgen-sounds-root")return Path.GetFullPath(args[i+1]);
        if(Directory.Exists(Path.Combine(Environment.CurrentDirectory,"sounds")))return Path.Combine(Environment.CurrentDirectory,"sounds");
        for(var at=new DirectoryInfo(Path.GetDirectoryName(Application.dataPath));at!=null;at=at.Parent)
            if(File.Exists(Path.Combine(at.FullName,"WorldGen.sln")))return Path.Combine(at.FullName,"sounds");
        return Path.Combine(Path.GetDirectoryName(Application.dataPath),"sounds");
    }
    void InitializeWeatherAudio()
    {
        // Exact pre-SA3 rainfall generator and original22050Hz playback rate.
        rainSynth=new RainSynth();rainAudio=view.gameObject.AddComponent<AudioSource>();
        rainAudio.loop=true;rainAudio.playOnAwake=false;rainAudio.spatialBlend=0;rainAudio.volume=0;
        rainClip=AudioClip.Create("Original continuous rainfall",22050*60,2,22050,true,rainSynth.Read);rainAudio.clip=rainClip;
        InitializeFootsteps();
        weatherSoundLoading=StartCoroutine(LoadWeatherRecordings());
    }
    IEnumerator LoadWeatherRecordings()
    {
        string root=Path.Combine(WeatherSoundsRoot(),"weather-isolated");
        var screened=new Dictionary<string,string>(StringComparer.Ordinal);var clips=new List<AudioClip>();
        string screenFile=Path.Combine(root,"screened.tsv");
        if(File.Exists(screenFile))foreach(string line in File.ReadAllLines(screenFile))
        {var fields=line.Split('\t');if(fields.Length==2)screened[fields[0]]=fields[1];}
        for(int index=1;index<=4;index++)
        {
            string filename="thunder-"+index.ToString("D2")+".wav",path=Path.Combine(root,filename);
            if(!screened.TryGetValue(filename,out string expectedHash)||!File.Exists(path))continue;
            string actualHash;using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())actualHash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            if(actualHash!=expectedHash){Debug.LogWarning("Changed thunder recording skipped: "+filename);continue;}
            var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,AudioType.WAV);weatherSoundRequest=request;
            ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio=false;yield return request.SendWebRequest();
            if(weatherSoundDisposed)yield break;
            if(request.result==UnityWebRequest.Result.Success)
            {
                var clip=DownloadHandlerAudioClip.GetContent(request);
                if(clip!=null&&clip.length>=2&&clip.channels==1){clip.name=filename;clips.Add(clip);weatherRecordings.Add(clip);}
                else if(clip!=null)Destroy(clip);
            }
            request.Dispose();weatherSoundRequest=null;
        }
        thunderClips=clips.ToArray();if(clips.Count<2)Debug.LogWarning("Thunder recordings missing; no synthetic thunder fallback.");
        WeatherSoundsReady=true;weatherSoundLoading=null;
    }
    void UpdateRecordedWeather(bool audioPaused,bool aboveCloud)
    {
        if(rainSynth==null)return;
        var mix=new WeatherAudioMix(0,0,CurrentWeather.RainMmPerHour,aboveCloud,CurrentWeather.CloudCover,CurrentWeather.Thunderstorm);
        if(!mix.RainAllowed){rainSynth.MillimetresPerHour=0;rainAudio.Stop();rainAudio.volume=0;return;}
        rainSynth.MillimetresPerHour=(float)CurrentWeather.RainMmPerHour;
        if(audioPaused){if(!rainPaused)rainAudio.Pause();rainPaused=true;return;}
        if(rainPaused){rainAudio.UnPause();rainPaused=false;}
        if(!rainAudio.isPlaying)rainAudio.Play();
        rainAudio.volume=Mathf.Lerp(rainAudio.volume,WeatherVideoOptions.Current.RainVolume,1-Mathf.Exp(-Time.unscaledDeltaTime*2));
    }
    void ResetRecordedWeather(){if(rainSynth!=null)rainSynth.MillimetresPerHour=0;if(rainAudio!=null){rainAudio.Stop();rainAudio.volume=0;}}
    void DisposeWeatherAudio()
    {
        weatherSoundDisposed=true;if(weatherSoundLoading!=null)StopCoroutine(weatherSoundLoading);weatherSoundLoading=null;
        if(weatherSoundRequest!=null){weatherSoundRequest.Abort();weatherSoundRequest.Dispose();weatherSoundRequest=null;}
        ResetRecordedWeather();if(rainClip!=null)Destroy(rainClip);
        foreach(var clip in weatherRecordings)if(clip!=null)Destroy(clip);
        weatherRecordings.Clear();thunderClips=Array.Empty<AudioClip>();
    }
}
