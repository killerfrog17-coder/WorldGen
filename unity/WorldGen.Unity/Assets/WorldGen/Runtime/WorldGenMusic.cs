using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    AudioSource musicSource;
    string musicFolder;
    bool musicBusy;
    float musicVolume=.6f,musicGain;
    bool musicSimulationStarted;
    string musicScreen;
    EraMusicTrack currentMusicTrack;
    float nextMusicCheck;
    readonly List<string> recentMusic=new List<string>();
    readonly HashSet<string> brokenMusic=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    PrehistoryState eraState;int simulationEra=2;
    int CurrentMusicEra()
    {
        if(screen!="Simulation")return 1;
        if(!SameHumanDomain(eraState,world?.Prehistory))
        {eraState=world?.Prehistory;simulationEra=WorldEraView.Of(eraState);}
        return Math.Max(2,simulationEra);
    }
    void InitializeMusic()
    {
        string root=Path.GetDirectoryName(Application.dataPath);
        for(var directory=new DirectoryInfo(root);directory!=null;directory=directory.Parent)
            if(File.Exists(Path.Combine(directory.FullName,"WorldGen.sln"))){root=directory.FullName;break;}
        // The launcher can live outside the checkout (including on another drive).
        if(Directory.Exists(Path.Combine(Environment.CurrentDirectory,"music")))root=Environment.CurrentDirectory;
        musicFolder=Argument("--worldgen-music-root",Path.Combine(root,"music"));
        musicVolume=Mathf.Clamp01(PlayerPrefs.GetFloat("music-volume",.6f));
        musicSource=gameObject.AddComponent<AudioSource>();musicSource.playOnAwake=false;musicSource.loop=false;musicSource.spatialBlend=0;musicSource.volume=0;
        if(FindAnyObjectByType<AudioListener>()==null)gameObject.AddComponent<AudioListener>();
    }
    void UpdateMusic()
    {
        if(musicSource==null)return;
        if(screen!=musicScreen){if(screen=="Simulation")musicSimulationStarted=false;musicScreen=screen;nextMusicCheck=0;}
        if(screen=="Simulation"&&(humanPlaying||terrainExplorer!=null))musicSimulationStarted=true;
        int desired=DesiredMusicEra();bool compatible=currentMusicTrack!=null&&currentMusicTrack.Supports(desired);
        float target=musicSource.isPlaying&&compatible?1:0;
        musicGain=Mathf.MoveTowards(musicGain,target,Time.unscaledDeltaTime/(target==0?1.1f:.8f));musicSource.volume=musicVolume*musicGain;
        if(musicSource.isPlaying&&!compatible){if(musicGain>0)return;musicSource.Stop();currentMusicTrack=null;nextMusicCheck=0;}
        if(desired==0||musicBusy||musicSource.isPlaying||Time.unscaledTime<nextMusicCheck)return;
        musicBusy=true;StartCoroutine(PlayNextMusic());
    }
    int DesiredMusicEra()=>screen=="Simulation"&&!musicSimulationStarted?0:CurrentMusicEra();
    void DrawAudioOptions()
    {
        GUILayout.Label("AUDIO",new GUIStyle(GUI.skin.label){fontStyle=FontStyle.Bold});
        GUILayout.Label("Music  "+Mathf.RoundToInt(musicVolume*100)+"%");
        float next=GUILayout.HorizontalSlider(musicVolume,0,1);
        if(next!=musicVolume){musicVolume=next;PlayerPrefs.SetFloat("music-volume",musicVolume);}
        GUILayout.Space(12);
    }
    IEnumerator PlayNextMusic()
    {
        // Scan names off the main thread. No audio is imported into the project
        // and only the selected file is decoded, asynchronously, by Unity.
        var scan=Task.Run(()=>Directory.Exists(musicFolder)?Directory.GetFiles(musicFolder):Array.Empty<string>());
        while(!scan.IsCompleted)yield return null;
        if(scan.IsFaulted){nextMusicCheck=Time.unscaledTime+10;musicBusy=false;yield break;}
        var tracks=new List<EraMusicTrack>();
        foreach(string file in scan.Result.OrderBy(p=>p,StringComparer.OrdinalIgnoreCase))
            if(!brokenMusic.Contains(file)&&EraMusicTrack.TryParse(file,out var track))tracks.Add(track);
        var candidates=EraMusic.Candidates(tracks,DesiredMusicEra(),recentMusic);
        if(candidates.Length==0){nextMusicCheck=Time.unscaledTime+10;musicBusy=false;yield break;}
        // Soundtrack-only entropy: never touches a world, recipe or simulation stream.
        int choice=UnityEngine.Random.Range(0,candidates.Length);var selected=candidates[choice];
        AudioType type=Path.GetExtension(selected.Path).Equals(".ogg",StringComparison.OrdinalIgnoreCase)?AudioType.OGGVORBIS:
            Path.GetExtension(selected.Path).Equals(".wav",StringComparison.OrdinalIgnoreCase)?AudioType.WAV:AudioType.MPEG;
        using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(selected.Path).AbsoluteUri,type))
        {
            ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio=true;
            yield return request.SendWebRequest();
            if(request.result!=UnityWebRequest.Result.Success)
            {brokenMusic.Add(selected.Path);Debug.LogWarning("Music skipped: "+Path.GetFileName(selected.Path)+" ("+request.error+")");}
            else
            {
                var clip=DownloadHandlerAudioClip.GetContent(request);
                if(clip==null||clip.length<=0)brokenMusic.Add(selected.Path);
                else if(!selected.Supports(DesiredMusicEra()))Destroy(clip);
                else
                {
                    var previous=musicSource.clip;musicSource.clip=clip;if(previous!=null)Destroy(previous);
                    currentMusicTrack=selected;musicGain=0;musicSource.volume=0;musicSource.Play();EraMusic.Remember(recentMusic,selected.Path);
                }
            }
        }
        nextMusicCheck=Time.unscaledTime+.5f;musicBusy=false;
    }
    void DisposeMusic(){if(musicSource!=null){musicSource.Stop();if(musicSource.clip!=null)Destroy(musicSource.clip);}}
}
