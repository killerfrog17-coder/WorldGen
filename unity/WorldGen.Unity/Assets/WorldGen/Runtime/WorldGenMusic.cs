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
    ExplorationMusicTrack currentExplorationTrack;
    readonly List<string> recentExplorationMusic=new List<string>();
    bool musicInExploration;
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
        bool exploring=terrainExplorer!=null;
        if(exploring!=musicInExploration){musicInExploration=exploring;nextMusicCheck=0;}
        if(screen=="Simulation"&&humanPlaying)musicSimulationStarted=true;
        int desired=DesiredMusicEra();bool compatible=exploring?currentExplorationTrack!=null:currentExplorationTrack==null&&currentMusicTrack!=null&&currentMusicTrack.Supports(desired);
        float target=musicSource.isPlaying&&compatible?1:0;
        musicGain=Mathf.MoveTowards(musicGain,target,Time.unscaledDeltaTime/(target==0?1.1f:.8f));musicSource.volume=musicVolume*musicGain;
        if(musicSource.isPlaying&&!compatible){if(musicGain>0)return;musicSource.Stop();currentMusicTrack=null;currentExplorationTrack=null;nextMusicCheck=0;}
        // Eligibility is checked when selecting a track, not at every biome edge.
        // Only switching between gameplay and the historical/menu soundtrack interrupts it.
        if(!musicSource.isPlaying&&currentExplorationTrack!=null){currentExplorationTrack=null;nextMusicCheck=Time.unscaledTime+UnityEngine.Random.Range(10f,35f);}
        if(terrainExplorer!=null)terrainExplorer.MusicDebugText=currentExplorationTrack==null?"Music: quiet":("Music: "+currentExplorationTrack.Title);
        if((exploring?!terrainExplorer.Ready:desired==0)||musicBusy||musicSource.isPlaying||Time.unscaledTime<nextMusicCheck)return;
        musicBusy=true;StartCoroutine(PlayNextMusic());
    }
    int DesiredMusicEra()=>terrainExplorer!=null||screen=="Simulation"&&!musicSimulationStarted?0:CurrentMusicEra();
    ExplorationMusicContext CurrentExplorationMusicContext()=>terrainExplorer.MusicContext(play.SolarHour,(SimulationTime.Seconds(world.Prehistory)+play.PendingHistorySeconds)/(double)SimulationTime.Year);
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
        bool exploring=terrainExplorer!=null;string folder=exploring?Path.Combine(musicFolder,"exploration"):musicFolder;
        var scan=Task.Run(()=>Directory.Exists(folder)?Directory.GetFiles(folder):Array.Empty<string>());
        while(!scan.IsCompleted)yield return null;
        if(scan.IsFaulted){nextMusicCheck=Time.unscaledTime+10;musicBusy=false;yield break;}
        EraMusicTrack selected=null;ExplorationMusicTrack selectedExploration=null;
        if(exploring)
        {
            if(terrainExplorer==null||!terrainExplorer.Ready){musicBusy=false;yield break;}
            var tracks=new List<ExplorationMusicTrack>();foreach(string file in scan.Result.OrderBy(p=>p,StringComparer.OrdinalIgnoreCase))
                if(!brokenMusic.Contains(file)&&ExplorationMusicTrack.TryParse(file,out var track))tracks.Add(track);
            selectedExploration=ExplorationMusic.Choose(ExplorationMusic.Candidates(tracks,CurrentExplorationMusicContext(),recentExplorationMusic),UnityEngine.Random.value);
        }
        else
        {
        var tracks=new List<EraMusicTrack>();
        foreach(string file in scan.Result.OrderBy(p=>p,StringComparer.OrdinalIgnoreCase))
            if(!brokenMusic.Contains(file)&&EraMusicTrack.TryParse(file,out var track))tracks.Add(track);
        var candidates=EraMusic.Candidates(tracks,DesiredMusicEra(),recentMusic);
        if(candidates.Length>0)selected=candidates[UnityEngine.Random.Range(0,candidates.Length)];
        }
        string path=selectedExploration?.Path??selected?.Path;
        if(path==null){nextMusicCheck=Time.unscaledTime+10;musicBusy=false;yield break;}
        // Soundtrack-only entropy: never touches a world, recipe or simulation stream.
        AudioType type=Path.GetExtension(path).Equals(".ogg",StringComparison.OrdinalIgnoreCase)?AudioType.OGGVORBIS:
            Path.GetExtension(path).Equals(".wav",StringComparison.OrdinalIgnoreCase)?AudioType.WAV:AudioType.MPEG;
        using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,type))
        {
            ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio=true;
            yield return request.SendWebRequest();
            if(request.result!=UnityWebRequest.Result.Success)
            {brokenMusic.Add(path);Debug.LogWarning("Music skipped: "+Path.GetFileName(path)+" ("+request.error+")");}
            else
            {
                var clip=DownloadHandlerAudioClip.GetContent(request);
                if(clip==null||clip.length<=0)brokenMusic.Add(path);
                else if(exploring!=(terrainExplorer!=null)||(exploring?(!terrainExplorer.Ready||!selectedExploration.Supports(CurrentExplorationMusicContext())):!selected.Supports(DesiredMusicEra())))Destroy(clip);
                else
                {
                    var previous=musicSource.clip;musicSource.clip=clip;if(previous!=null)Destroy(previous);
                    currentMusicTrack=selected;currentExplorationTrack=selectedExploration;musicGain=0;musicSource.volume=0;musicSource.Play();EraMusic.Remember(exploring?recentExplorationMusic:recentMusic,path);
                }
            }
        }
        nextMusicCheck=Time.unscaledTime+.5f;musicBusy=false;
    }
    void DisposeMusic(){if(musicSource!=null){musicSource.Stop();if(musicSource.clip!=null)Destroy(musicSource.clip);}}
}
