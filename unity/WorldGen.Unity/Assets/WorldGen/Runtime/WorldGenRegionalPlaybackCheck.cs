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
    [Serializable] sealed class RegionalPlaybackEvidence
    {
        public int Cells,Regions,Aggregates,Checks,RuntimeErrors;
        public double FineSeconds,FineDays,FineMaximumFrameMs,FineP95FrameMs,FastSeconds,FastYears,FastYearsPerSecond,FastMaximumFrameMs,FastP95FrameMs,MaximumPublicationPauseMs,MaximumLayerSwitchMs;
        public string Hash="";
        public long[] ProcessItems=new long[Prehistory.RegionalProcessCount];
        public double[] ProcessMilliseconds=new double[Prehistory.RegionalProcessCount];
        public double DirectYearsPerSecond;
    }
    IEnumerator CheckRegionalPlayback()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);double deadline=Time.realtimeSinceStartupAsDouble+180;
        while((Busy||humans==null||!worldReady)&&error.Length==0&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(Busy||humans==null||world?.Prehistory?.Foragers?.Version!=ForagerState.RegionalVersion){Debug.LogError("REGIONAL PLAYBACK setup failed "+error);Application.Quit(1);yield break;}
        var evidence=new RegionalPlaybackEvidence{Cells=world.Surface.Cells.Length,Regions=world.Prehistory.Foragers.Districts.Length,Aggregates=world.Prehistory.Foragers.Cohorts.Length};
        Application.LogCallback capture=(message,stack,type)=>{if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)evidence.RuntimeErrors++;};Application.logMessageReceived+=capture;
        screen="Simulation";simulationPaused=false;atlasOpen=peoplesOpen=detailsOpen=chronicleOpen=false;historyOverlay=HistoryOverlay.Cultures;selectedCell=-1;layer=preferredLayer=Layer.Climate;
        SetProjection(Projection.Map);Refresh();UpdateHumanPalette();
        while(humanPalettePending||humanPaletteJob!=null||humanPaletteReady!=null)yield return null;
        var preparation=System.Threading.Tasks.Task.Run(()=>new SimulationSession(humans,world.Prehistory));while(!preparation.IsCompleted)yield return null;
        if(preparation.IsFaulted){Debug.LogError("REGIONAL PLAYBACK session failed "+preparation.Exception);Application.Quit(1);yield break;}
        humanSession=preparation.Result;humanPlayback.SetRate(SimulationTime.Day);humanPlaying=true;humanClockSample=Time.realtimeSinceStartupAsDouble;
        long first=SimulationTime.Seconds(world.Prehistory);double started=Time.realtimeSinceStartupAsDouble,previous=started;var frames=new List<double>();
        while(Time.realtimeSinceStartupAsDouble-started<12&&error.Length==0)
        {yield return null;double now=Time.realtimeSinceStartupAsDouble;frames.Add((now-previous)*1000);previous=now;}
        humanPlaying=false;while(humanStep!=null)yield return null;
        evidence.FineSeconds=Time.realtimeSinceStartupAsDouble-started;evidence.FineDays=(SimulationTime.Seconds(world.Prehistory)-first)/(double)SimulationTime.Day;
        frames.Sort();evidence.FineMaximumFrameMs=frames.LastOrDefault();evidence.FineP95FrameMs=frames.Count>0?frames[(int)((frames.Count-1)*.95)]:0;
        if(evidence.FineDays<9){Debug.LogError("REGIONAL PLAYBACK fine clock cannot sustain one day per second");Application.Quit(1);yield break;}evidence.Checks++;
        var direct=System.Threading.Tasks.Task.Run(()=>{var session=new SimulationSession(humans,world.Prehistory);long target=session.Seconds+1000*SimulationTime.Year;var timer=System.Diagnostics.Stopwatch.StartNew();while(session.Seconds<target)session.AdvanceSeconds(target-session.Seconds,1000000);return 1000/timer.Elapsed.TotalSeconds;});
        while(!direct.IsCompleted)yield return null;evidence.DirectYearsPerSecond=direct.GetAwaiter().GetResult();
        humans.WorkObserver=metric=>{if(metric.Stage.StartsWith("regional process ")&&int.TryParse(metric.Stage.Substring(17),out int process)){evidence.ProcessItems[process]+=metric.Items;evidence.ProcessMilliseconds[process]+=metric.Milliseconds;}};
        humanPlayback.SetRate(50000d*SimulationTime.Year);humanPlaying=true;humanClockSample=Time.realtimeSinceStartupAsDouble;
        first=SimulationTime.Seconds(world.Prehistory);started=previous=Time.realtimeSinceStartupAsDouble;frames.Clear();
        long observed=first;double lastPublication=started;
        while(Time.realtimeSinceStartupAsDouble-started<12&&error.Length==0)
        {yield return null;double now=Time.realtimeSinceStartupAsDouble;frames.Add((now-previous)*1000);previous=now;long next=SimulationTime.Seconds(world.Prehistory);if(next!=observed){evidence.MaximumPublicationPauseMs=Math.Max(evidence.MaximumPublicationPauseMs,(now-lastPublication)*1000);lastPublication=now;observed=next;}}
        humanPlaying=false;while(humanStep!=null)yield return null;
        humans.WorkObserver=null;
        evidence.FastSeconds=Time.realtimeSinceStartupAsDouble-started;evidence.FastYears=(SimulationTime.Seconds(world.Prehistory)-first)/(double)SimulationTime.Year;evidence.FastYearsPerSecond=evidence.FastYears/evidence.FastSeconds;
        frames.Sort();evidence.FastMaximumFrameMs=frames.LastOrDefault();evidence.FastP95FrameMs=frames.Count>0?frames[(int)((frames.Count-1)*.95)]:0;
        evidence.Checks++;
        if(world.Prehistory.Foragers.Cohorts.Length>evidence.Regions){Debug.LogError("REGIONAL PLAYBACK population records exceeded regions");Application.Quit(1);yield break;}evidence.Checks++;
        foreach(var mode in new[]{HistoryOverlay.Cultures,HistoryOverlay.Languages,HistoryOverlay.Technology,HistoryOverlay.HumanSpecies,HistoryOverlay.Religions})
        {
            historyOverlay=mode;double switchStarted=Time.realtimeSinceStartupAsDouble;UpdateHumanPalette();double until=switchStarted+20;
            while((humanPalettePending||humanPaletteJob!=null||humanPaletteReady!=null)&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            if(humanPalettePending||humanPaletteJob!=null||humanPaletteReady!=null){Debug.LogError("REGIONAL PLAYBACK stale map "+mode);Application.Quit(1);yield break;}
            evidence.MaximumLayerSwitchMs=Math.Max(evidence.MaximumLayerSwitchMs,(Time.realtimeSinceStartupAsDouble-switchStarted)*1000);
            for(int frame=0;frame<4;frame++)yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,mode+".png"));for(int frame=0;frame<4;frame++)yield return new WaitForEndOfFrame();evidence.Checks++;
        }
        evidence.Hash=world.Prehistory.Hash;Application.logMessageReceived-=capture;
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(evidence,true));
        bool passed=evidence.RuntimeErrors==0&&error.Length==0&&evidence.FastYearsPerSecond>=10000&&evidence.MaximumLayerSwitchMs<200&&evidence.MaximumPublicationPauseMs<250;Debug.Log("REGIONAL PLAYBACK "+(passed?"PASS ":"FAIL ")+JsonUtility.ToJson(evidence));Application.Quit(passed?0:1);
    }
}
