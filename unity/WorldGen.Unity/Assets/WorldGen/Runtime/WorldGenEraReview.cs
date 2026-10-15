using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckHistoricalEras()
    {
        string folder=Argument("--worldgen-output","");Directory.CreateDirectory(folder);
        double deadline=Time.realtimeSinceStartupAsDouble+900;
        while((Busy||humans==null||!worldReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(Busy||humans==null||!worldReady){Debug.LogError("ERA REVIEW load failed: "+error);Application.Quit(1);yield break;}
        var recorded=world.Prehistory;var first=WorldEraView.FirstYears(recorded);
        var samples=new List<(int Era,int Year)>();
        for(int era=2;era<=10;era++)
        {
            if(first[era]<0||Enumerable.Range(era+1,10-era).Any(later=>first[later]>=0&&first[later]<=first[era]))continue;
            int next=Enumerable.Range(era+1,10-era).Where(later=>first[later]>first[era]).Select(later=>first[later]).DefaultIfEmpty(recorded.Years).Min();
            samples.Add((era,era==10?recorded.Years:first[era]+Math.Max(0,(next-first[era])/2)));
        }
        var initialization=Task.Run(()=>new SimulationSession(humans,humans.Start(humans.Prepare(recorded.Origin))));
        while(!initialization.IsCompleted)yield return null;
        if(initialization.IsFaulted){Debug.LogException(initialization.Exception);Application.Quit(1);yield break;}
        var session=initialization.Result;humanPlaying=false;screen="Simulation";simulationPaused=false;
        SetProjection(Projection.Map);mapPan=Vector3.zero;centerLongitude=0;
        layer=preferredLayer=Layer.Climate;satelliteOpacity=.35f;humanOverlayOpacity=1;historyOverlay=HistoryOverlay.States;
        peoplesOpen=false;detailsOpen=false;chronicleOpen=true;selectedCell=-1;gridSpacing=30;Refresh();
        foreach(var sample in samples.OrderBy(s=>s.Year).ThenBy(s=>s.Era))
        {
            long target=sample.Year*SimulationTime.Year;
            var advance=Task.Run(()=>session.AdvanceSeconds(target-session.Seconds,int.MaxValue));
            while(!advance.IsCompleted)yield return null;
            if(advance.IsFaulted){Debug.LogException(advance.Exception);Application.Quit(1);yield break;}
            world.Prehistory=advance.Result;humanSession=session;UpdateHumanPalette();
            double readyDeadline=Time.realtimeSinceStartupAsDouble+180;
            do{yield return null;}while((Busy||borderLoading||humanPaletteJob!=null||humanPaletteReady!=null||humanPalettePending)&&Time.realtimeSinceStartupAsDouble<readyDeadline);
            if(Time.realtimeSinceStartupAsDouble>=readyDeadline){Debug.LogError("ERA REVIEW presentation timed out.");Application.Quit(1);yield break;}
            for(int frame=0;frame<12;frame++)yield return new WaitForEndOfFrame();
            string name=sample.Era.ToString("00")+"-"+WorldEraView.Name(sample.Era).Replace(" ","-");
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+"-hud.png"));
            // CaptureScreenshot reads the frame asynchronously. A manual
            // offscreen camera render in the same frame can replace its buffer.
            for(int frame=0;frame<3;frame++)yield return new WaitForEndOfFrame();
            CaptureHumanMap(Path.Combine(folder,name+"-map.png"));
            Debug.Log("ERA REVIEW "+name+" year "+sample.Year+" hash "+session.Hash);
            for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
        }
        if(session.Hash!=recorded.Hash){Debug.LogError("ERA REVIEW final replay hash mismatch: "+session.Hash+" != "+recorded.Hash);Application.Quit(1);yield break;}
        File.WriteAllText(Path.Combine(folder,"verified.txt"),"Native era replay matched "+session.Hash+Environment.NewLine);
        Debug.Log("ERA REVIEW PASS "+session.Hash);Application.Quit(0);
    }
}
