using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
public sealed partial class WorldGenRuntime
{
    [Serializable] sealed class PlaybackEvidence
    {
        public int Cells,Frames,PublishedCheckpoints;public double DurationSeconds,SimulatedYears,AchievedYearsPerSecond,MeanFrameMs,P95FrameMs,MaximumFrameMs;
        public bool SelectionStayedFolded,AcceptancePassed;public string Error="";
        public double MaximumYearMilliseconds,MinimumRollingYearsPerSecond;
        public int FirstMaximumTechnologyYear=-1,RequestedTailYears,FinalYear,FinalTowns,FinalStates;
    }
    System.Collections.IEnumerator CheckHistoricalPlayback()
    {
        string output=Argument("--worldgen-output","");System.IO.Directory.CreateDirectory(output);double deadline=Time.realtimeSinceStartupAsDouble+300;
        while((!worldReady||Busy||humans==null)&&error.Length==0&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!worldReady||Busy||humans==null){Debug.LogError("PLAYBACK CHECK initialization failed "+error);Application.Quit(1);yield break;}
        screen="Simulation";simulationPaused=false;atlasOpen=false;detailsOpen=false;peoplesOpen=false;chronicleOpen=false;layer=preferredLayer=Layer.Climate;satelliteOpacity=0;humanOverlayOpacity=1;historyOverlay=HistoryOverlay.States;
        SetProjection(Projection.Map);Refresh();UpdateHumanPalette();while(Busy||humanPaletteJob!=null||humanPaletteReady!=null||humanPalettePending)yield return null;
        for(int i=0;i<30;i++)yield return null;
        // Loading, validation and initial private ownership are setup, not a
        // simulated year. Playback below still includes every publication copy.
        var prepare=System.Threading.Tasks.Task.Run(()=>new SimulationSession(humans,world.Prehistory));while(!prepare.IsCompleted)yield return null;
        if(prepare.IsFaulted){Debug.LogException(prepare.Exception);Application.Quit(1);yield break;}humanSession=prepare.Result;
        var yearCosts=new Dictionary<int,double>();humans.YearWorkObserver=(year,ms)=>{lock(yearCosts)yearCosts[year]=(yearCosts.TryGetValue(year,out double previous)?previous:0)+ms;};
        humanPlayback.SetRate(1000*SimulationTime.Year);SyncRateInput();humanPlaying=true;
        double duration=double.Parse(Argument("--worldgen-duration","45"),System.Globalization.CultureInfo.InvariantCulture),start=Time.realtimeSinceStartupAsDouble,last=start;long seconds=SimulationTime.Seconds(world.Prehistory);var state=world.Prehistory;
        int tail=int.Parse(Argument("--worldgen-history-tail","0"));long target=seconds+tail*SimulationTime.Year;
        var evidence=new PlaybackEvidence{Cells=world.Surface.Cells.Length,SelectionStayedFolded=true,RequestedTailYears=tail,MinimumRollingYearsPerSecond=double.MaxValue};var frames=new List<double>();bool selected=false;
        long rollingSeconds=seconds;double rollingStart=start,nextProgress=start+10;
        while(Time.realtimeSinceStartupAsDouble-start<duration&&(tail==0||SimulationTime.Seconds(world.Prehistory)<target))
        {
            yield return null;double now=Time.realtimeSinceStartupAsDouble;frames.Add((now-last)*1000);last=now;
            if(!ReferenceEquals(state,world.Prehistory)){evidence.PublishedCheckpoints++;state=world.Prehistory;}
            if(!selected&&now-start>duration*.3){selectedCell=humans.Patches[world.Prehistory.Origin].Cell;selected=true;}
            evidence.SelectionStayedFolded&=!detailsOpen;if(error.Length>0)break;
            long simulated=SimulationTime.Seconds(world.Prehistory);if(simulated-rollingSeconds>=50*SimulationTime.Year){evidence.MinimumRollingYearsPerSecond=Math.Min(evidence.MinimumRollingYearsPerSecond,(simulated-rollingSeconds)/(double)SimulationTime.Year/(now-rollingStart));rollingStart=now;rollingSeconds=simulated;}
            if(now>=nextProgress){nextProgress=now+10;Debug.Log("PLAYBACK PROGRESS year "+world.Prehistory.Years+" elapsed "+(now-start).ToString("F1")+"s");}
        }
        humanPlaying=false;while(humanStep!=null)yield return null;
        evidence.DurationSeconds=Time.realtimeSinceStartupAsDouble-start;evidence.SimulatedYears=(SimulationTime.Seconds(world.Prehistory)-seconds)/(double)SimulationTime.Year;
        evidence.AchievedYearsPerSecond=evidence.SimulatedYears/evidence.DurationSeconds;evidence.Frames=frames.Count;evidence.MeanFrameMs=frames.DefaultIfEmpty(0).Average();evidence.MaximumFrameMs=frames.DefaultIfEmpty(0).Max();frames.Sort();evidence.P95FrameMs=frames.Count==0?0:frames[Math.Min(frames.Count-1,(int)(frames.Count*.95))];evidence.Error=error;
        while(humanStep!=null||humanPaletteJob!=null||humanPaletteReady!=null||humanPalettePending)yield return null;
        evidence.MaximumYearMilliseconds=yearCosts.Values.DefaultIfEmpty(0).Max();evidence.FirstMaximumTechnologyYear=world.Prehistory.History.Aggregates?.FirstMaximumTechnologyYear??-1;evidence.FinalYear=world.Prehistory.Years;evidence.FinalTowns=world.Prehistory.Society.Settlements.Length;evidence.FinalStates=world.Prehistory.History.Polities.Count(p=>p.EndedYear<0);
        evidence.AcceptancePassed=evidence.PublishedCheckpoints>0&&evidence.SelectionStayedFolded&&evidence.Error.Length==0&&(tail==0||evidence.Cells==200000&&evidence.SimulatedYears>=tail&&evidence.FirstMaximumTechnologyYear>=0&&evidence.FinalYear>=evidence.FirstMaximumTechnologyYear+1000&&evidence.AchievedYearsPerSecond>=3&&evidence.MinimumRollingYearsPerSecond>=3&&evidence.MaximumYearMilliseconds<=1000);
        System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"year-work.csv"),new[]{"year,milliseconds"}.Concat(yearCosts.OrderBy(p=>p.Key).Select(p=>p.Key+","+p.Value.ToString("F6",System.Globalization.CultureInfo.InvariantCulture))));
        System.IO.File.WriteAllText(System.IO.Path.Combine(output,"playback.json"),JsonUtility.ToJson(evidence,true));
        CaptureHumanMap(System.IO.Path.Combine(output,"playback-map.png"));
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output,"playback-hud.png"));for(int i=0;i<8;i++)yield return null;
        Debug.Log("PLAYBACK CHECK "+JsonUtility.ToJson(evidence));Application.Quit(evidence.AcceptancePassed?0:1);
    }
}
