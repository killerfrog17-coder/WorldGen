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
    float masterVolume=1,musicPauseRemaining;
    bool masterMuted,musicMuted,musicMenuPaused,musicResumePending;
    bool MusicPauseRequested=>terrainExplorer!=null?terrainExplorer.Paused:screen=="Simulation"&&simulationPaused;
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
        musicMuted=PlayerPrefs.GetInt("music-muted",0)!=0;
        musicSource=gameObject.AddComponent<AudioSource>();musicSource.playOnAwake=false;musicSource.loop=false;musicSource.spatialBlend=0;musicSource.volume=0;
        if(FindAnyObjectByType<AudioListener>()==null)gameObject.AddComponent<AudioListener>();
    }
    void UpdateMusic()
    {
        if(musicSource==null)return;
        if(MusicPauseRequested)
        {
            HoldMusicForMenu();ApplyMusicVolume();return;
        }
        if(musicMenuPaused)
        {
            if(musicResumePending)musicSource.UnPause();
            musicResumePending=false;musicMenuPaused=false;nextMusicCheck=Time.unscaledTime+musicPauseRemaining;
        }
        if(screen!=musicScreen){if(screen=="Simulation")musicSimulationStarted=false;musicScreen=screen;nextMusicCheck=0;}
        bool exploring=terrainExplorer!=null;
        if(exploring!=musicInExploration){musicInExploration=exploring;nextMusicCheck=0;}
        if(screen=="Simulation"&&humanPlaying)musicSimulationStarted=true;
        int desired=DesiredMusicEra();bool compatible=exploring?currentExplorationTrack!=null:currentExplorationTrack==null&&currentMusicTrack!=null&&currentMusicTrack.Supports(desired);
        float target=musicSource.isPlaying&&compatible?1:0;
        musicGain=Mathf.MoveTowards(musicGain,target,Time.unscaledDeltaTime/(target==0?1.1f:.8f));ApplyMusicVolume();
        if(musicSource.isPlaying&&!compatible){if(musicGain>0)return;musicSource.Stop();currentMusicTrack=null;currentExplorationTrack=null;nextMusicCheck=0;}
        // Eligibility is checked when selecting a track, not at every biome edge.
        // Only switching between gameplay and the historical/menu soundtrack interrupts it.
        if(!musicSource.isPlaying&&currentExplorationTrack!=null){currentExplorationTrack=null;nextMusicCheck=Time.unscaledTime+UnityEngine.Random.Range(10f,35f);}
        if(terrainExplorer!=null)terrainExplorer.MusicDebugText=currentExplorationTrack==null?"Music: quiet":("Music: "+currentExplorationTrack.Title);
        if((exploring?!terrainExplorer.Ready:desired==0)||musicBusy||musicSource.isPlaying||Time.unscaledTime<nextMusicCheck)return;
        musicBusy=true;StartCoroutine(PlayNextMusic());
    }
    int DesiredMusicEra()=>terrainExplorer!=null||screen=="Simulation"&&!musicSimulationStarted?0:CurrentMusicEra();
    ExplorationMusicContext CurrentExplorationMusicContext()=>terrainExplorer.MusicContext(terrainExplorer.LocalSolarHour,PlayCalendar.Seconds(play,SimulationTime.Seconds(world.Prehistory))/(double)SimulationTime.Year);
    void DrawAudioOptions()
    {
        GUILayout.Label("AUDIO",new GUIStyle(GUI.skin.label){fontStyle=FontStyle.Bold});
        DrawMasterVolume();
        DrawMusicVolume();
        GUILayout.Label("Footsteps  "+Mathf.RoundToInt(LocalTerrainExplorer.FootstepVolume*100)+"%");
        float steps=GUILayout.HorizontalSlider(LocalTerrainExplorer.FootstepVolume,0,1);if(steps!=LocalTerrainExplorer.FootstepVolume)LocalTerrainExplorer.FootstepVolume=steps;
        var weather=WeatherVideoOptions.Current;
        GUILayout.Label("Rain  "+Mathf.RoundToInt(weather.RainVolume*100)+"%");float rain=GUILayout.HorizontalSlider(weather.RainVolume,0,1);
        GUILayout.Label("Thunder  "+Mathf.RoundToInt(weather.ThunderVolume*100)+"%");float thunder=GUILayout.HorizontalSlider(weather.ThunderVolume,0,1);
        if(rain!=weather.RainVolume||thunder!=weather.ThunderVolume){weather.RainVolume=rain;weather.ThunderVolume=thunder;weather.Save();}
        DrawFaunaAudioOptions();
        GUILayout.Space(12);
    }
    void DrawMasterVolume()
    {
        GUILayout.BeginHorizontal();GUILayout.Label("Master volume  "+Mathf.RoundToInt(masterVolume*100)+"%");
        bool muted=GUILayout.Toggle(masterMuted,"Mute",GUILayout.Width(75));GUILayout.EndHorizontal();
        float volume=GUILayout.HorizontalSlider(masterVolume,0,1);
        if(volume!=masterVolume||muted!=masterMuted)
        {
            masterVolume=volume;masterMuted=muted;AudioListener.volume=masterMuted?0:masterVolume;
            PlayerPrefs.SetFloat("master-volume",masterVolume);PlayerPrefs.SetInt("master-muted",masterMuted?1:0);PlayerPrefs.Save();
        }
    }
    void DrawMusicVolume()
    {
        GUILayout.BeginHorizontal();GUILayout.Label("Music  "+Mathf.RoundToInt(musicVolume*100)+"%");
        bool muted=GUILayout.Toggle(musicMuted,"Mute",GUILayout.Width(75));GUILayout.EndHorizontal();
        float volume=GUILayout.HorizontalSlider(musicVolume,0,1);
        if(volume!=musicVolume||muted!=musicMuted)
        {
            musicVolume=volume;musicMuted=muted;ApplyMusicVolume();
            PlayerPrefs.SetFloat("music-volume",musicVolume);PlayerPrefs.SetInt("music-muted",musicMuted?1:0);PlayerPrefs.Save();
        }
    }
    void ApplyMusicVolume(){if(musicSource!=null)musicSource.volume=musicMuted?0:musicVolume*musicGain;}
    void HoldMusicForMenu()
    {
        if(!musicMenuPaused)
        {musicMenuPaused=true;musicPauseRemaining=Math.Max(0,nextMusicCheck-Time.unscaledTime);}
        // Pause reports isPlaying=false, so return before the normal track-ended
        // path. A clip which finishes asynchronous loading in a menu is held too.
        if(musicSource.isPlaying){musicSource.Pause();musicResumePending=true;}
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
        // Escape the basename as data: Unity's file transport otherwise treats
        // '+' in multi-biome tags as a space on some Windows players.
        using(var request=UnityWebRequestMultimedia.GetAudioClip(MusicFileUri(path),type))
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
                    currentMusicTrack=selected;currentExplorationTrack=selectedExploration;musicGain=0;musicSource.volume=0;musicSource.Play();
                    if(MusicPauseRequested)HoldMusicForMenu();EraMusic.Remember(exploring?recentExplorationMusic:recentMusic,path);
                }
            }
        }
        nextMusicCheck=Time.unscaledTime+.5f;musicBusy=false;
    }
    static string MusicFileUri(string path)=>new Uri(Path.GetDirectoryName(path)+Path.DirectorySeparatorChar).AbsoluteUri+Uri.EscapeDataString(Path.GetFileName(path));
    void DisposeMusic(){if(musicSource!=null){musicSource.Stop();if(musicSource.clip!=null)Destroy(musicSource.clip);}}
}
