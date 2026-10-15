using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    HistoryOverlay historyOverlay=HistoryOverlay.States;
    Prehistory humans;World humanWorld;Task<Prehistory> humanPreparation;
    SimulationSession humanSession;
    sealed class HumanPlaybackWork { public Task<PrehistoryState> Task;public SimulationSession Session; }
    HumanPlaybackWork humanPlaybackWork;System.Threading.CancellationTokenSource humanCancellation;
    Task<PrehistoryState> humanStep;bool humanPlaying;int humanVariant,lastHumanSelection=-2;
    float satelliteOpacity=1f,humanOverlayOpacity=.6f;Color32[] humanColors;
    Task<HumanPaletteResult> humanPaletteJob;bool humanPalettePending;int humanPaletteUpload;
    HumanPaletteResult humanPaletteReady;
    Color32[] humanSpareColors,humanUploadBlock,humanUploadTail;byte[] humanCodesScratch;
    sealed class HumanPaletteResult { public World World;public PrehistoryState State;public HistoryOverlay Overlay;public int Selection;public string Sort;public Color32[] Colors;public HistoryLegendEntry[] Legend,MapLabels; }
    readonly SimulationPlayback humanPlayback=new SimulationPlayback();double humanClockSample,calendarWorkStarted;string rateInput="500";int rateUnit=6;bool timeRateFocused;
    bool simulationPaused,resumeHumanPlayback;string simulationTab="";
    bool technologyCeilingOpen;
    HistoricalTechnologyCeiling preferredTechnologyCeiling=HistoricalTechnologyCeiling.Modern;
    float SimulationSidebar=>0;
    void ToggleSimulationPause(){if(!simulationPaused){resumeHumanPlayback=humanPlaying;humanPlaying=false;simulationPaused=true;}else{simulationPaused=false;humanPlaying=resumeHumanPlayback;}}
    void PollHumans()
    {
        PollHumanPalette();
        if(!worldReady||world==null||world.Ecology==null)return;
        if(humanWorld!=world)
        {
            humanPlaying=false;humans=null;humanWorld=world;humanStep=null;lastHumanSelection=-2;atlasOpen=false;atlasSearchFocused=false;
            humanCancellation?.Cancel();humanCancellation?.Dispose();humanCancellation=new System.Threading.CancellationTokenSource();
            humanSession=null;humanPlaybackWork=null;
            cachedHistoryLegend=Array.Empty<HistoryLegendEntry>();cachedMapLabels=Array.Empty<HistoryLegendEntry>();legendState=null;legendEngine=null;
            var captured=world;humanPreparation=Task.Run(()=>new Prehistory(captured));
        }
        if(humanPreparation!=null&&humanPreparation.IsCompleted)
        {
            var task=humanPreparation;humanPreparation=null;
            try{humans=task.GetAwaiter().GetResult();if(world.Prehistory==null){world.Prehistory=humans.Prepare();if(preferredTechnologyCeiling!=HistoricalTechnologyCeiling.Modern)world.Prehistory=humans.SetTechnologyCeiling(world.Prehistory,preferredTechnologyCeiling);}else{humans.Validate(world.Prehistory);if(world.Prehistory.History?.Modern!=null)preferredTechnologyCeiling=world.Prehistory.History.Modern.TechnologyCeiling;}UpdateHumanPalette();}
            catch(Exception ex){error=ex.Message;humans=null;}
        }
        if(humanStep!=null&&humanStep.IsCompleted)
        {
            var task=humanStep;var playbackWork=humanPlaybackWork;humanStep=null;humanPlaybackWork=null;
            try{var prior=world.Prehistory;world.Prehistory=task.GetAwaiter().GetResult();if(playbackWork?.Task==task)humanSession=playbackWork.Session;humanPlayback.Consume(Math.Max(0,SimulationTime.Seconds(world.Prehistory)-(long)calendarWorkStarted));if(!SameHumanDomain(prior,world.Prehistory))UpdateHumanPalette();}catch(Exception ex){error=ex.Message;humanPlaying=false;}
        }
        if(humans==null)return;
        if(displayedWorld==world)RequestHistoryBorders();
        if((screen=="Simulation"||layer==Layer.Simulation)&&lastHumanSelection!=selectedCell)UpdateHumanPalette();
        double clockNow=Time.realtimeSinceStartupAsDouble,elapsed=humanClockSample==0?0:clockNow-humanClockSample;humanClockSample=clockNow;
        double oldRate=humanPlayback.Rate;humanPlayback.Phase(SocietyState.DefaultStep(world.Prehistory.Society?.Phase??SocietyStage.Foragers));if(oldRate!=humanPlayback.Rate)SyncRateInput();
        bool run=screen=="Simulation"&&!atlasOpen&&!simulationPaused&&humanPlaying&&!Busy&&!saveDialog&&exportJob==null;
        humanPlayback.Accumulate(elapsed,run);
        if(run&&humanStep==null&&humanPlayback.Pending>0)
        {
            var engine=humans;var state=engine.UpgradeScheduling(world.Prehistory);if(!state.Started){state=engine.Start(state);world.Prehistory=state;UpdateHumanPalette();}
            calendarWorkStarted=SimulationTime.Seconds(state);var slice=SimulationClockView.Publication((long)calendarWorkStarted,humanPlayback.Pending,humanPlayback.Rate);long seconds=slice.Seconds;int maximumEvents=slice.MaximumEvents;
            // Session validation/copies are background work too. Fine clocks
            // also use the private input: public validation is not a cheap tick.
            var reusable=humanSession;var work=new HumanPlaybackWork();humanPlaybackWork=work;var token=humanCancellation.Token;
            humanStep=work.Task=Task.Run(()=>{
                token.ThrowIfCancellationRequested();var session=reusable;
                if(session==null||!ReferenceEquals(session.LastSnapshot,state))session=new SimulationSession(engine,state);
                // Task-local ownership prevents an abandoned old-world worker
                // from installing/advancing the new world's session.
                work.Session=session;return session.AdvanceSeconds(seconds,maximumEvents,token,slice.MaximumGlobalWork);
            });
        }
    }
    void UpdateHumanPalette()
    {
        RequestHistoryBorders();
        humanPalettePending=true;lastHumanSelection=selectedCell;
    }
    void PollHumanPalette()
    {
        if(humanPaletteJob!=null&&humanPaletteJob.IsCompleted)
        {
            try{humanPaletteReady=humanPaletteJob.GetAwaiter().GetResult();humanPaletteUpload=0;}catch(Exception ex){error=ex.Message;}
            humanPaletteJob=null;
        }
        if(humanPaletteReady!=null)
        {
            var ready=humanPaletteReady;
            if(ready.World!=world||displayedWorld!=world||ready.Overlay!=historyOverlay||ready.Selection!=selectedCell||!palettes.TryGetValue(Layer.Simulation,out var texture)){humanSpareColors=ready.Colors;humanPaletteReady=null;}
            else
            {
                // CPU color calculations are background work. Bound native
                // texture copies per frame; the old GPU texture remains usable
                // until all rows are ready for one atomic Apply.
                int rows=Math.Min(64,texture.height-humanPaletteUpload);
                if(rows==64&&humanUploadBlock==null)humanUploadBlock=new Color32[64*2048];
                if(rows!=64&&(humanUploadTail==null||humanUploadTail.Length!=rows*2048))humanUploadTail=new Color32[rows*2048];
                var block=rows==64?humanUploadBlock:humanUploadTail;Array.Copy(ready.Colors,humanPaletteUpload*2048,block,0,block.Length);
                texture.SetPixels32(0,humanPaletteUpload,2048,rows,block);humanPaletteUpload+=rows;
                if(humanPaletteUpload==texture.height){texture.Apply(false,false);humanSpareColors=humanColors;humanColors=ready.Colors;legendState=ready.State;legendEngine=humans;legendOverlay=ready.Overlay;legendSort=ready.Sort;cachedHistoryLegend=ready.Legend;cachedMapLabels=ready.MapLabels;humanPaletteReady=null;}
            }
        }
        if(!humanPalettePending||humanPaletteJob!=null||humanPaletteReady!=null||humans==null||humanColors==null||displayedWorld!=world)return;
        humanPalettePending=false;var captured=world;var state=world.Prehistory;var engine=humans;int selection=selectedCell;var overlay=historyOverlay;string sort=societySort;var baseline=humanColors;
        var colors=humanSpareColors!=null&&humanSpareColors.Length==baseline.Length?humanSpareColors:new Color32[baseline.Length];humanSpareColors=null;
        if(humanCodesScratch==null||humanCodesScratch.Length!=captured.Surface.Cells.Length)humanCodesScratch=new byte[captured.Surface.Cells.Length];var scratch=humanCodesScratch;
        humanPaletteJob=Task.Run(()=>{
            Array.Copy(baseline,colors,baseline.Length);var codes=PrehistoryView.Codes(captured,engine,state,selection,overlay,scratch);
            var display=HistoricalMapSnapshot.For(state);
            var tint=new System.Collections.Generic.Dictionary<int,Color32>();
            for(int i=0;i<codes.Length;i++)
            {
                colors[i].a=codes[i];int owner=display.ColorId(i,overlay);if(owner<0)continue;
                if(!tint.TryGetValue(owner,out var rgb)){var v=SocietyView.OverlayColor(state,owner,overlay);rgb=new Color32((byte)Math.Round(v.X*255),(byte)Math.Round(v.Y*255),(byte)Math.Round(v.Z*255),0);tint.Add(owner,rgb);}
                colors[i].r=rgb.r;colors[i].g=rgb.g;colors[i].b=rgb.b;
            }
            return new HumanPaletteResult{World=captured,State=state,Selection=selection,Overlay=overlay,Sort=sort,Colors=colors,Legend=SocietyView.Legend(state,overlay,sort,engine),MapLabels=SocietyView.MapLabels(captured,state,overlay,engine)};
        });
    }
    void StepHumans(int steps)
    {
        if(humans==null||humanStep!=null||world.Prehistory==null)return;
        if(!world.Prehistory.Started){if(screen!="Simulation")return;world.Prehistory=humans.Start(world.Prehistory);UpdateHumanPalette();}
        var engine=humans;var state=world.Prehistory;calendarWorkStarted=SimulationTime.Seconds(state);humanStep=Task.Run(()=>engine.Advance(state,steps));
    }
    void StepHumanEvent()
    {if(humans==null||humanStep!=null||world.Prehistory==null)return;if(!world.Prehistory.Started)world.Prehistory=humans.Start(world.Prehistory);humanPlaying=false;humanPlayback.Reset();var engine=humans;var state=engine.UpgradeScheduling(world.Prehistory);var reusable=humanSession;var work=new HumanPlaybackWork();humanPlaybackWork=work;calendarWorkStarted=SimulationTime.Seconds(state);humanStep=work.Task=Task.Run(()=>{var session=reusable;if(session==null||!ReferenceEquals(session.LastSnapshot,state))session=new SimulationSession(engine,state);work.Session=session;return session.AdvanceSeconds(session.NextEventDue-session.Seconds,1,humanCancellation.Token);});}
    void SyncRateInput(){rateUnit=humanPlayback.Rate>=SimulationTime.Year?6:humanPlayback.Rate>=30*SimulationTime.Day?5:humanPlayback.Rate>=7*SimulationTime.Day?4:humanPlayback.Rate>=SimulationTime.Day?3:humanPlayback.Rate>=SimulationTime.Hour?2:humanPlayback.Rate>=60?1:0;rateInput=(humanPlayback.Rate/SimulationClockView.UnitSeconds[rateUnit]).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture);}
    void StartHumanWorld()
    {
        if(humans==null)return;
        try{world.Prehistory=humans.Start(world.Prehistory);screen="Simulation";simulationTab="";simulationPaused=false;expandedInspection=false;layer=preferredLayer=Layer.Climate;satelliteOpacity=0;humanOverlayOpacity=1;historyOverlay=HistoryOverlay.States;humanPlaying=false;FocusHumanOrigin();UpdateHumanPalette();Refresh();}catch(Exception ex){error=ex.Message;}
    }
    void FocusHumanOrigin()
    {
        if(humans==null)return;var patch=humans.Patches[world.Prehistory.Origin];var p=world.Surface.Cells[patch.Cells[patch.Cells.Length/2]].Position;
        float lon=(float)(Math.Atan2(p.Y,p.X)*180/Math.PI),lat=(float)(Math.Asin(p.Z)*180/Math.PI);
        if(projection==Projection.Globe){yaw=90-lon;pitch=-lat;}else centerLongitude=lon;
    }
    void DrawHumanSetup()
    {
        GUILayout.Label("Sapients",new GUIStyle(GUI.skin.label){fontStyle=FontStyle.Bold});GUILayout.Label("Homo sapiens");
        if(humans==null){GUILayout.Label(worldReady?"Preparing human habitat…":"Generate the world to place humans");return;}
        GUILayout.Label(PrehistoryView.Status(humans,world.Prehistory));
        DrawTechnologyCeiling();
        GUI.enabled=!Busy&&humanStep==null&&!world.Prehistory.Started;
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Choose another")){PrepareHumanOrigin(humans.ChooseOrigin(++humanVariant));layer=preferredLayer=Layer.Simulation;FocusHumanOrigin();UpdateHumanPalette();Refresh();}
        bool valid=selectedCell>=0&&humans.CellPatch[selectedCell]>=0&&humans.Patches[humans.CellPatch[selectedCell]].Capacity>=25;
        GUI.enabled=GUI.enabled&&valid;
        if(GUILayout.Button("Move here")){PrepareHumanOrigin(humans.CellPatch[selectedCell]);layer=preferredLayer=Layer.Simulation;UpdateHumanPalette();Refresh();}
        GUILayout.EndHorizontal();GUI.enabled=true;
        if(GUILayout.Button("Show starting region")){layer=preferredLayer=Layer.Simulation;FocusHumanOrigin();UpdateHumanPalette();Refresh();}
    }
    void PrepareHumanOrigin(int origin)
    {
        var ceiling=world.Prehistory.History?.Modern?.TechnologyCeiling;
        world.Prehistory=humans.Prepare(origin);
        if(ceiling.HasValue)world.Prehistory=humans.SetTechnologyCeiling(world.Prehistory,ceiling.Value);
    }
    void DrawTechnologyCeiling()
    {
        if(world?.Prehistory?.History?.Modern==null)return;
        bool prior=GUI.enabled;GUI.enabled=prior&&humanStep==null;
        if(GUILayout.Button("Technology limit: "+world.Prehistory.History.Modern.TechnologyCeiling+" ▾"))technologyCeilingOpen=!technologyCeilingOpen;
        if(technologyCeilingOpen)foreach(HistoricalTechnologyCeiling ceiling in Enum.GetValues(typeof(HistoricalTechnologyCeiling)))if(GUILayout.Button(ceiling.ToString()))
        {preferredTechnologyCeiling=ceiling;var engine=humans;var state=world.Prehistory;calendarWorkStarted=SimulationTime.Seconds(state);humanStep=Task.Run(()=>engine.SetTechnologyCeiling(state,ceiling));technologyCeilingOpen=false;}
        GUI.enabled=prior;
    }
    void DrawHumanPlayback()
    {
        if(humans==null){GUILayout.Label("Preparing human habitat…");return;}
        var state=world.Prehistory;
        GUILayout.BeginHorizontal();GUILayout.Label(SimulationClockView.Date(state,humanPlayback.Rate));
        GUILayout.Label(SimulationClockView.Rate(humanPlayback.Rate));GUILayout.FlexibleSpace();
        if(GUILayout.Button(humanPlaying?"Pause":"Play")){if(!state.Started)StartHumanWorld();humanPlaying=!humanPlaying;}
        if(GUILayout.Button("Previous speed"))ChangeHumanSpeed(-1);
        if(GUILayout.Button("Next speed"))ChangeHumanSpeed(1);
        GUI.enabled=humanStep==null&&!Busy;if(GUILayout.Button("Next event"))StepHumanEvent();GUI.enabled=true;GUILayout.EndHorizontal();
    }
    System.Collections.IEnumerator CheckPrehistory()
    {
        string folder=Argument("--worldgen-prehistory-check","");System.IO.Directory.CreateDirectory(folder);
        double deadline=Time.realtimeSinceStartupAsDouble+300;
        while((!worldReady||Busy||humans==null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(humans==null||Busy){Debug.LogError("PREHISTORY CHECK initialization failed "+error);Application.Quit(1);yield break;}
        SetProjection(Projection.Globe);FocusHumanOrigin();yield return null;
        var originPatch=humans.Patches[world.Prehistory.Origin];int originCell=originPatch.Cells[originPatch.Cells.Length/2];var point=world.Surface.Cells[originCell].Position;
        Pick(cameraView.WorldToScreenPoint(globe.transform.TransformPoint(new Vector3((float)point.X,(float)point.Z,(float)-point.Y))));
        if(selectedCell!=originCell){Debug.LogError("PREHISTORY CHECK globe picking mismatch "+selectedCell+" / "+originCell);Application.Quit(1);yield break;}
        layer=preferredLayer=Layer.Simulation;SetProjection(Projection.Map);gridSpacing=0;Refresh();UpdateHumanPalette();
        while(Busy)yield return null;
        yield return new WaitForEndOfFrame();CaptureHumanMap(System.IO.Path.Combine(folder,"starting-region.png"));yield return null;
        StartHumanWorld();satelliteOpacity=0;ApplyCachedDisplay();StepHumans(50000/Prehistory.YearsPerStep(world.Prehistory));
        while(humanStep!=null)yield return null;
        yield return new WaitForEndOfFrame();CaptureHumanMap(System.IO.Path.Combine(folder,"year-50000.png"));yield return null;
        StepHumans(150000/Prehistory.YearsPerStep(world.Prehistory));while(humanStep!=null)yield return null;
        satelliteOpacity=.35f;ApplyCachedDisplay();selectedCell=humans.Patches[world.Prehistory.Origin].Cell;UpdateHumanPalette();
        yield return new WaitForEndOfFrame();CaptureHumanMap(System.IO.Path.Combine(folder,"year-200000.png"));yield return null;
        var checkpoint=world.Prehistory;var saved=Task.Run(()=>WorldGen.Serialization.WorldFile.Write(world));while(!saved.IsCompleted)yield return null;
        if(saved.IsFaulted){Debug.LogError(saved.Exception);Application.Quit(1);yield break;}
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,"prehistory.world"),saved.Result);
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"state.json"),WorldGen.Serialization.WorldJson.Write(checkpoint));
        if(!Application.isBatchMode)
        {
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"simulation-hud.png"));
            for(int frame=0;frame<10;frame++)yield return new WaitForEndOfFrame();
        }
        Debug.Log("PREHISTORY CHECK PASS "+PrehistoryView.Status(humans,checkpoint));Application.Quit(checkpoint.Years==200000?0:1);
    }
    void CaptureHumanMap(string path)
    {
        var target=new RenderTexture(1600,1000,24);var priorRect=cameraView.rect;var priorPosition=cameraView.transform.position;float priorSize=cameraView.orthographicSize;
        bool priorOrthographic=cameraView.orthographic;var priorRotation=globe.transform.rotation;var priorTarget=cameraView.targetTexture;
        cameraView.targetTexture=target;cameraView.rect=new Rect(0,0,1,1);cameraView.orthographic=true;cameraView.orthographicSize=1.3f;cameraView.transform.position=new Vector3(0,0,-3.2f);globe.transform.rotation=Quaternion.identity;
        cameraView.Render();var active=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
        System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());RenderTexture.active=active;cameraView.targetTexture=priorTarget;cameraView.rect=priorRect;cameraView.transform.position=priorPosition;cameraView.orthographicSize=priorSize;cameraView.orthographic=priorOrthographic;globe.transform.rotation=priorRotation;
        Destroy(texture);target.Release();Destroy(target);
    }
    async void CheckHistoricalResume()
    {
        try
        {
            string source=Argument("--worldgen-history-resume","");
            int years=int.Parse(Argument("--worldgen-history-years","37"));
            string secondsArgument=Argument("--worldgen-history-seconds","");
            bool benchmark=Environment.GetCommandLineArgs().Contains("--worldgen-history-benchmark");
            string roundtrip=Argument("--worldgen-history-save","");
            string workProfile=Argument("--worldgen-work-profile","");
            string hashProfile=Argument("--worldgen-hash-profile","");
            string loadProfile=Argument("--worldgen-load-profile","");
            string hash=await Task.Run(()=>{var loadWatch=System.Diagnostics.Stopwatch.StartNew();var loadRows=new System.Collections.Generic.List<string>{"stage,elapsedMilliseconds"};var w=WorldGen.Serialization.WorldFile.Read(System.IO.File.ReadAllBytes(source),status:stage=>loadRows.Add(FormattableString.Invariant($"{stage},{loadWatch.Elapsed.TotalMilliseconds:F3}")));loadRows.Add(FormattableString.Invariant($"Complete,{loadWatch.Elapsed.TotalMilliseconds:F3}"));if(loadProfile.Length>0)System.IO.File.WriteAllLines(loadProfile,loadRows);
                if(hashProfile.Length>0){var rows=new System.Collections.Generic.List<string>{"stage,milliseconds"};foreach(var metric in SimulationHashProfile.Capture(w.Prehistory).OrderByDescending(m=>m.Milliseconds))rows.Add(FormattableString.Invariant($"{metric.Stage},{metric.Milliseconds:F3}"));System.IO.File.WriteAllLines(hashProfile,rows);}
                var engine=new Prehistory(w){MaximumWorkers=int.Parse(Argument("--worldgen-workers","4"))};var metrics=new System.Collections.Generic.List<SimulationWorkMetric>();if(workProfile.Length>0)engine.WorkObserver=metrics.Add;
                PrehistoryState s;
                if(Environment.GetCommandLineArgs().Contains("--worldgen-session-profile"))
                {
                    var session=new SimulationSession(engine,w.Prehistory);long target=session.Seconds+years*SimulationTime.Year;
                    while(session.Seconds<target){var slice=SimulationClockView.Publication(session.Seconds,target-session.Seconds,1000*SimulationTime.Year);session.AdvanceSeconds(slice.Seconds,slice.MaximumEvents,maximumGlobalWork:slice.MaximumGlobalWork);}
                    s=session.Snapshot();
                }
                else s=secondsArgument.Length>0?engine.AdvanceSeconds(w.Prehistory,long.Parse(secondsArgument)):engine.AdvanceYears(w.Prehistory,years);engine.Validate(s);
                if(workProfile.Length>0){var rows=new System.Collections.Generic.List<string>{"stage,count,totalMilliseconds,maxMilliseconds,meanMilliseconds"};foreach(var g in metrics.GroupBy(m=>m.Stage).OrderByDescending(g=>g.Sum(m=>m.Milliseconds)))rows.Add(FormattableString.Invariant($"{g.Key},{g.Count()},{g.Sum(m=>m.Milliseconds):F3},{g.Max(m=>m.Milliseconds):F3},{g.Average(m=>m.Milliseconds):F3}"));System.IO.File.WriteAllLines(workProfile,rows);}
                if(roundtrip.Length>0)
                {
                    w.Prehistory=s;var bytes=WorldGen.Serialization.WorldFile.Write(w);var restored=WorldGen.Serialization.WorldFile.Read(bytes);
                    string resumed=new Prehistory(restored).AdvanceYears(restored.Prehistory,37).Hash;
                    if(restored.Prehistory.Hash!=s.Hash||resumed!=engine.AdvanceYears(s,37).Hash)throw new InvalidOperationException("Native historical save/reload changed the checkpoint or its continuation.");
                    System.IO.File.WriteAllBytes(roundtrip,bytes);System.IO.File.WriteAllText(roundtrip+".resume-hash",resumed);
                    Debug.Log("HISTORY SAVE ROUNDTRIP PASS "+resumed);
                }
                if(benchmark){var current=w.Prehistory;var samples=new double[20];var watch=new System.Diagnostics.Stopwatch();for(int i=0;i<samples.Length;i++){watch.Restart();current=engine.AdvanceYears(current,1);samples[i]=watch.Elapsed.TotalMilliseconds;}Array.Sort(samples);Debug.Log($"HISTORY BENCHMARK {w.Surface.Cells.Length} cells: median {samples[10]:F1}ms, p95 {samples[18]:F1}ms per annual tick");}
                return s.Hash;});
            System.IO.File.WriteAllText(Argument("--worldgen-output",source+".replay-hash"),hash);
            Debug.Log("HISTORY RESUME PASS "+hash);Application.Quit(0);
        }
        catch(Exception ex){Debug.LogException(ex);Application.Quit(1);}
    }
    System.Collections.IEnumerator CheckHistoricalView()
    {
        string folder=Argument("--worldgen-output","");System.IO.Directory.CreateDirectory(folder);
        double deadline=Time.realtimeSinceStartupAsDouble+300;
        while((Busy||humans==null||!worldReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(Busy||humans==null){Debug.LogError("HISTORY VIEW failed: "+error);Application.Quit(1);yield break;}
        string unchangedCheckpoint=world.Prehistory.ComputeHash();
        if(double.TryParse(Argument("--worldgen-time-rate",""),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double captureRate)){humanPlayback.SetRate(captureRate);SyncRateInput();}
        screen="Simulation";humanPlaying=false;SetProjection(Projection.Map);mapPan=Vector3.zero;centerLongitude=0;
        while(Busy||borderLoading)yield return null;
        for(int frame=0;frame<5;frame++)yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"default-map-hud.png"));
        for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
        layer=preferredLayer=Layer.Climate;satelliteOpacity=.45f;humanOverlayOpacity=1;gridSpacing=30;Refresh();
        peoplesOpen=true;chronicleOpen=true;detailsOpen=true;mapSettings=false;
        foreach(var mode in new[]{HistoryOverlay.States,HistoryOverlay.Territories,HistoryOverlay.Provinces,HistoryOverlay.Religions,HistoryOverlay.Cultures,HistoryOverlay.Technology})
        {
            historyOverlay=mode;var entries=SocietyView.Legend(world.Prehistory,mode);selectedCell=entries.Length>0?entries[0].Cell:-1;UpdateHumanPalette();ApplyCachedDisplay();
            for(int frame=0;frame<12;frame++)yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,mode.ToString()+"-hud.png"));
            CaptureHumanMap(System.IO.Path.Combine(folder,mode.ToString()+"-map.png"));
            for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
            if(mode==HistoryOverlay.States)foreach(string sort in new[]{"Population","Land area"})
            {
                societySort=sort;for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,sort.Replace(" ","-")+"-ranking.png"));
                for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
            }
            societySort="Default";
        }
        OpenAtlas();
        for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"atlas-world.png"));
        for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
        atlasSection="Political centers";
        for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"atlas-population.png"));
        var atlasState=HistoryAtlas.Directory(world.Prehistory,"state").FirstOrDefault();
        if(atlasState!=null)
        {
            OpenAtlas("state",atlasState.Id);
            for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"atlas-state.png"));
            for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
            atlasSection="Diplomatic neighborhood";
            for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"atlas-diplomacy.png"));
        }
        foreach(var entry in new[]{("city","atlas-city"),("people","atlas-people"),("religion","atlas-religion"),("ruler","atlas-ruler")})
        {
            var record=HistoryAtlas.Directory(world.Prehistory,entry.Item1).FirstOrDefault();if(record==null)continue;
            OpenAtlas(record.Kind,record.Id);for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,entry.Item2+".png"));
            for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
            string modelSection=entry.Item1=="city"?"Population and movement":entry.Item1=="people"?"Overseas communities":"";
            if(modelSection.Length>0&&atlasDocument.Sections.Any(s=>s.Title==modelSection))
            {
                atlasSection=modelSection;for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,entry.Item2+"-model.png"));for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
            }
            atlasSection="Recorded history";for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,entry.Item2+"-history.png"));
        }
        for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();atlasOpen=false;
        peoplesOpen=false;chronicleOpen=false;detailsOpen=false;selectedCell=-1;historyOverlay=HistoryOverlay.States;UpdateHumanPalette();
        for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"folded-hud.png"));
        for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
        if(world.Prehistory.ComputeHash()!=unchangedCheckpoint){Debug.LogError("Atlas browsing changed the simulation checkpoint.");Application.Quit(1);yield break;}
        Debug.Log("HISTORY VIEW PASS "+world.Prehistory.Hash);Application.Quit(0);
    }
}
