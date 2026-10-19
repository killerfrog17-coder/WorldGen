using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    AudioClip[] thunderClips=Array.Empty<AudioClip>();
    readonly List<AudioClip> weatherRecordings=new List<AudioClip>();
    RecordedWeatherBed[] weatherBeds;
    Coroutine weatherSoundLoading;UnityWebRequest weatherSoundRequest;
    bool weatherSoundDisposed;
    public bool WeatherSoundsReady {get;private set;}
    public int WeatherRecordingCount=>weatherRecordings.Count;
    public int ActiveWeatherBedVoices {get{int n=0;if(weatherBeds!=null)foreach(var bed in weatherBeds)n+=bed.Playing;return n;}}
    public int WeatherBedTransitions {get{int n=0;if(weatherBeds!=null)foreach(var bed in weatherBeds)n+=bed.Transitions;return n;}}
    public float RainRecordingGain=>weatherBeds==null?0:weatherBeds[3].Gain+weatherBeds[4].Gain+weatherBeds[5].Gain;
    public int LastThunderVariant {get;private set;}=-1;
    public bool WeatherRecordingSignalsValid
    {
        get
        {
            if(weatherRecordings.Count!=17)return false;
            foreach(var clip in weatherRecordings)
            {
                var samples=new float[clip.samples*clip.channels];
                if(!clip.GetData(samples,0)||clip.frequency!=44100)return false;
                double energy=0;
                foreach(float sample in samples){if(float.IsNaN(sample)||float.IsInfinity(sample)||Math.Abs(sample)>.701)return false;energy+=sample*sample;}
                if(energy/samples.Length<.000001)return false;
            }
            return true;
        }
    }

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
        weatherBeds=new RecordedWeatherBed[6];
        for(int i=0;i<weatherBeds.Length;i++)weatherBeds[i]=new RecordedWeatherBed(view.transform,"Weather recorded bed "+i,(uint)(413+i*7919));
        weatherSoundLoading=StartCoroutine(LoadWeatherRecordings());
    }
    IEnumerator LoadWeatherRecordings()
    {
        string[] groups={"wind-light","wind-strong","wind-gale","rain-light","rain-medium","rain-heavy","thunder"};
        int[] counts={3,2,2,2,2,2,4};var missing=new List<string>();
        string root=Path.Combine(WeatherSoundsRoot(),"weather");
        for(int group=0;group<groups.Length;group++)
        {
            var clips=new List<AudioClip>();
            for(int index=1;index<=counts[group];index++)
            {
                string filename=groups[group]+"-"+index.ToString("D2")+".wav",path=Path.Combine(root,filename);
                if(!File.Exists(path)){missing.Add(filename);continue;}
                var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,AudioType.WAV);weatherSoundRequest=request;
                ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio=false;
                yield return request.SendWebRequest();
                if(weatherSoundDisposed)yield break;
                if(request.result!=UnityWebRequest.Result.Success)missing.Add(filename+" ("+request.error+")");
                else
                {
                    var clip=DownloadHandlerAudioClip.GetContent(request);
                    if(clip==null||clip.length<2||(group==6&&clip.channels!=1)){if(clip!=null)Destroy(clip);missing.Add(filename+" (invalid recording)");}
                    else {clip.name=filename;clips.Add(clip);weatherRecordings.Add(clip);}
                }
                request.Dispose();weatherSoundRequest=null;
            }
            if(group==6)thunderClips=clips.ToArray();else weatherBeds[group].Clips=clips.ToArray();
        }
        if(missing.Count>0)Debug.LogWarning("Weather recordings unavailable (silence, no synthetic fallback): "+string.Join(", ",missing));
        WeatherSoundsReady=true;weatherSoundLoading=null;
    }
    void UpdateRecordedWeather(bool audioPaused,bool aboveCloud)
    {
        if(weatherBeds==null)return;
        double speed=Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps);
        var mix=new WeatherAudioMix(speed,CurrentWeather.GustMps,CurrentWeather.RainMmPerHour,aboveCloud);var o=WeatherVideoOptions.Current;
        // Only gains/selection are dynamic. All audible samples come from the SA3 recordings.
        float delta=Time.unscaledDeltaTime;
        weatherBeds[0].Update((float)mix.WindLight*o.WindVolume,audioPaused,delta);
        weatherBeds[1].Update((float)mix.WindStrong*o.WindVolume,audioPaused,delta);
        weatherBeds[2].Update((float)mix.WindGale*o.WindVolume,audioPaused,delta);
        weatherBeds[3].Update((float)mix.RainLight*o.RainVolume,audioPaused,delta);
        weatherBeds[4].Update((float)mix.RainMedium*o.RainVolume,audioPaused,delta);
        weatherBeds[5].Update((float)mix.RainHeavy*o.RainVolume,audioPaused,delta);
    }
    void ResetRecordedWeather(){if(weatherBeds!=null)foreach(var bed in weatherBeds)bed.Reset();}
    void DisposeWeatherAudio()
    {
        weatherSoundDisposed=true;
        if(weatherSoundLoading!=null)StopCoroutine(weatherSoundLoading);
        weatherSoundLoading=null;
        if(weatherSoundRequest!=null){weatherSoundRequest.Abort();weatherSoundRequest.Dispose();weatherSoundRequest=null;}
        ResetRecordedWeather();
        foreach(var clip in weatherRecordings)if(clip!=null)Destroy(clip);
        weatherRecordings.Clear();thunderClips=Array.Empty<AudioClip>();
    }
    sealed class RecordedWeatherBed
    {
        readonly AudioSource[] voices=new AudioSource[2];
        readonly float[] age=new float[2];readonly bool[] active=new bool[2];
        const float Fade=3;
        int current=-1,lastClip=-1;uint choice;bool paused;
        public AudioClip[] Clips=Array.Empty<AudioClip>();
        public float Gain {get;private set;}
        public int Transitions {get;private set;}
        public int Playing=>(voices[0].isPlaying?1:0)+(voices[1].isPlaying?1:0);
        public RecordedWeatherBed(Transform parent,string name,uint seed)
        {
            choice=seed;
            for(int i=0;i<2;i++)
            {
                var root=new GameObject(name+" / "+i);root.transform.SetParent(parent,false);
                var voice=root.AddComponent<AudioSource>();voice.playOnAwake=false;voice.loop=false;voice.spatialBlend=0;voice.volume=0;
                voices[i]=voice;
            }
        }
        void Start(int voice)
        {
            choice=Rng.Mix(choice+0x9e3779b9u);
            int next=(int)(choice%(uint)Clips.Length);
            if(Clips.Length>1&&next==lastClip)next=(next+1)%Clips.Length;
            lastClip=next;voices[voice].clip=Clips[next];voices[voice].volume=0;voices[voice].pitch=1;voices[voice].Play();
            age[voice]=0;active[voice]=true;current=voice;
        }
        public void Update(float target,bool pause,float delta)
        {
            if(pause!=paused){foreach(var voice in voices){if(pause)voice.Pause();else voice.UnPause();}paused=pause;}
            if(pause)return;
            Gain=Mathf.Lerp(Gain,target,1-Mathf.Exp(-delta*.8f));
            if(Clips.Length==0)return;
            if(target<.0001f&&Gain<.0001f){Reset();return;}
            if(current<0)Start(0);
            for(int i=0;i<2;i++)if(active[i])age[i]+=delta;
            if(age[current]>=voices[current].clip.length-Fade&&!active[1-current]){Start(1-current);Transitions++;}
            for(int i=0;i<2;i++)if(active[i])
            {
                float remaining=voices[i].clip.length-age[i];
                if(remaining<=0){voices[i].Stop();active[i]=false;continue;}
                float envelope=Mathf.Sin(Mathf.Clamp01(age[i]/Fade)*Mathf.PI*.5f)*Mathf.Sin(Mathf.Clamp01(remaining/Fade)*Mathf.PI*.5f);
                voices[i].volume=Gain*envelope;
            }
        }
        public void Reset(){foreach(var voice in voices)voice.Stop();active[0]=active[1]=false;current=-1;Gain=0;}
    }
}
