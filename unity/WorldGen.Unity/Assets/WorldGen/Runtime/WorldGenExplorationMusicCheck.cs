using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckExplorationMusic()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);var report=new List<string>();var errors=new List<string>();
        Application.LogCallback listener=(message,trace,kind)=>{if(kind==LogType.Exception||kind==LogType.Error)errors.Add(message);};Application.logMessageReceived+=listener;
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("EXPLORATION MUSIC CHECK: "+message);report.Add("PASS "+message);}
        IEnumerator Until(Func<bool> condition,string label,double timeout=60)
        {double deadline=Time.realtimeSinceStartupAsDouble+timeout;while(!condition()&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;Check(condition(),label);}
        string actual=Path.Combine(musicFolder,"exploration");screen="Simulation";musicSimulationStarted=false;
        yield return Until(()=>!musicBusy,"initial asynchronous music work drains");musicSource.Stop();currentMusicTrack=null;currentExplorationTrack=null;musicGain=0;
        musicFolder=Path.Combine(output,"isolated-music");Directory.CreateDirectory(Path.Combine(musicFolder,"exploration"));
        foreach(var pair in new[]{("The Unhurried Road","Day [time=day].ogg"),("Lanternless Paths","Night [time=night].ogg")})
        {string source=Directory.GetFiles(actual,pair.Item1+" *.ogg").Single();File.Copy(source,Path.Combine(musicFolder,"exploration",pair.Item2),true);}
        string era=Path.Combine(musicFolder,"Era fixture 1-10.wav");
        using(var writer=new BinaryWriter(File.Create(era)))
        {
            const int count=8000*20;writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(8000);writer.Write(16000);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
            for(int i=0;i<count;i++)writer.Write((short)(1000*Math.Sin(i*2*Math.PI*220/8000)));
        }
        musicVolume=.15f;AudioListener.volume=.1f;screen="Menu";nextMusicCheck=0;
        yield return Until(()=>currentMusicTrack!=null&&musicSource.isPlaying,"menu plays only the separate era collection");
        recipe=Recipe.Preset("Earth");recipe.Detail=5000;setup.From(recipe);Generate();
        yield return Until(()=>worldReady&&!Busy&&humans!=null,"original Earth pipeline completes",240);StartHumanWorld();
        yield return Until(()=>!musicSource.isPlaying&&!musicBusy,"starting a paused simulation silences setup music");
        play.SolarDays=.125;ExploreTerrain(TerrainMapViewport.FromDegrees(0,32));
        yield return Until(()=>terrainExplorer!=null&&terrainExplorer.Ready,"gameplay terrain starts",180);terrainExplorer.Pause();
        yield return Until(()=>currentExplorationTrack?.Title=="Day"&&musicSource.isPlaying,"daylight selects the actual generated OGG exploration track");
        Check(currentMusicTrack==null&&DesiredMusicEra()==0,"historical soundtrack cannot leak into exploration");
        yield return Until(()=>musicGain>.95,"exploration volume fades in");
        var samples=new float[1024];double energy=0,until=Time.realtimeSinceStartupAsDouble+8;
        while(energy<1e-10&&Time.realtimeSinceStartupAsDouble<until){musicSource.GetOutputData(samples,0);energy=samples.Sum(x=>(double)x*x)/samples.Length;yield return null;}
        Check(energy>1e-10,"Unity produces non-silent decoded music output");report.Add("Output RMS "+Math.Sqrt(energy).ToString("G6")+"; clip "+musicSource.clip.length.ToString("F2")+"s");
        var dayClip=musicSource.clip;play.SolarDays=.625;yield return new WaitForSecondsRealtime(1);
        Check(currentExplorationTrack?.Title=="Day"&&musicSource.clip==dayClip&&musicSource.isPlaying,"environment changes do not cut a playing exploration song");
        Check((CurrentExplorationMusicContext().Time&MusicTime.Night)!=0,"music uses the live visual day/night clock");
        musicSource.Stop();yield return null;yield return null;
        Check(nextMusicCheck-Time.unscaledTime>=9&&nextMusicCheck-Time.unscaledTime<=35,"natural track completion schedules a real-time quiet interval");
        nextMusicCheck=0;yield return Until(()=>currentExplorationTrack?.Title=="Night"&&musicSource.isPlaying,"the next selection respects nighttime requirements");
        musicVolume=0;yield return null;yield return null;Check(musicSource.volume==0,"the existing music slider mutes exploration too");musicVolume=.15f;
        string geography=world.CoarseHash;ReturnFromPlay("Simulation");musicSimulationStarted=false;
        yield return Until(()=>!musicSource.isPlaying&&!musicBusy,"leaving gameplay fades out exploration rather than playing it over the simulator");
        Check(!humanPlaying&&world.CoarseHash==geography,"audio selection does not change geography or turn on simulation playback");
        musicSimulationStarted=true;nextMusicCheck=0;yield return Until(()=>currentMusicTrack!=null&&musicSource.isPlaying,"the simulation can resume its original era soundtrack");
        Application.logMessageReceived-=listener;Check(errors.Count==0,"no Unity audio/runtime errors");File.WriteAllLines(Path.Combine(output,"result.txt"),report);Debug.Log("EXPLORATION MUSIC PASS "+report.Count+" checks");Application.Quit(0);
    }
}
