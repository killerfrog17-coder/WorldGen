using System;
using System.Collections;
using System.IO;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    [Serializable] sealed class DockEvidence
    {
        public bool PanelsMeet,Resizable,Clamped,EraCorrect,MusicLoaded,MusicWaitsForPlay,CheckpointUnchanged,ContextualSelections,AtlasProfiles,LanguageProfiles,BaseCard,Passed;
        public string[] GuiErrors;
        public int Era;public string EraName,MusicDirectory;public Rect[] Panels;
    }
    IEnumerator CheckHistoryDock()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);
        double deadline=Time.realtimeSinceStartupAsDouble+180;
        while((!worldReady||Busy||humans==null)&&error.Length==0&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(!worldReady||Busy||humans==null){Debug.LogError("DOCK CHECK initialization failed "+error);Application.Quit(1);yield break;}
        string before=world.Prehistory.ComputeHash();screen="Simulation";humanPlaying=false;peoplesOpen=chronicleOpen=detailsOpen=true;historyOverlay=HistoryOverlay.States;UpdateHumanPalette();
        var evidence=new DockEvidence{PanelsMeet=Mathf.Abs(SocietyPanel(0).xMax-SocietyPanel(1).x)<.1f&&Mathf.Abs(SocietyPanel(1).xMax-SocietyPanel(2).x)<.1f};
        var guiErrors=new System.Collections.Generic.List<string>();Application.LogCallback recordError=(message,trace,type)=>{if(type==LogType.Error||type==LogType.Exception)guiErrors.Add(message);};Application.logMessageReceived+=recordError;
        evidence.Resizable=true;evidence.Clamped=true;
        for(int i=0;i<3;i++)
        {
            resizingSociety=i;societyDragY=300;societyDragHeight=SocietyPanel(i).height;float initial=societyDragHeight;
            ResizeSocietyPanel(200);evidence.Resizable&=Mathf.Abs(SocietyPanel(i).height-initial-100)<.1f;
            ResizeSocietyPanel(100000);evidence.Clamped&=SocietyPanel(i).height==100;
            ResizeSocietyPanel(-100000);evidence.Clamped&=SocietyPanel(i).y>=64;
            societyHeights[i]=i==0?360:i==1?200:400;
        }
        resizingSociety=-1;
        evidence.Era=CurrentMusicEra();evidence.EraName=WorldEraView.Name(evidence.Era);evidence.EraCorrect=evidence.Era==WorldEraView.Of(world.Prehistory);
        // Isolate this check from tracks already decoded from the real library.
        // This modifies only the disposable diagnostic player, never preferences.
        musicSimulationStarted=false;deadline=Time.realtimeSinceStartupAsDouble+15;
        while(musicBusy&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        musicSource.Stop();if(musicSource.clip!=null)Destroy(musicSource.clip);musicSource.clip=null;currentMusicTrack=null;recentMusic.Clear();musicGain=0;
        // A silent, tiny WAV in the isolated check output proves the real async
        // file/decoder path without adding an audio asset to the owner's library.
        musicFolder=Path.Combine(output,"music-fixture");Directory.CreateDirectory(musicFolder);brokenMusic.Clear();
        string track=Path.Combine(musicFolder,"Silent integration check 1-10.wav");
        using(var writer=new BinaryWriter(File.Create(track)))
        {
            const int samples=8000;writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(8000);writer.Write(16000);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples*2);writer.Write(new byte[samples*2]);
        }
        yield return null;musicSimulationStarted=false;evidence.MusicWaitsForPlay=DesiredMusicEra()==0;
        musicSimulationStarted=true;nextMusicCheck=0;deadline=Time.realtimeSinceStartupAsDouble+15;
        while(recentMusic.Count==0&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        evidence.MusicLoaded=recentMusic.Contains(track)&&musicSource.clip!=null;evidence.MusicDirectory=musicFolder;
        var checkpoint=world.Prehistory;evidence.LanguageProfiles=checkpoint.History?.Languages==null;
        if(checkpoint.History?.Cultures.Length>0)
        {
            selectedCell=checkpoint.Society.Cultures[0].OriginCell;historyOverlay=HistoryOverlay.Technology;mapSettings=false;UpdateHumanPalette();
            evidence.ContextualSelections=HistoryAtlas.Selection(checkpoint,selectedCell,historyOverlay)?.Kind=="technology"&&CurrentSelectionCard().Subtitle.Contains("technology");
            for(int i=0;i<8;i++)yield return null;
            OpenAtlas("technology",0);atlasSection="Knowledge and deployment";for(int i=0;i<8;i++)yield return null;
            evidence.AtlasProfiles=atlasDocument.Kind=="technology";
            if(checkpoint.History.Polities.Length>0){OpenAtlas("state",0);for(int i=0;i<8;i++)yield return null;evidence.AtlasProfiles&=atlasDocument.Kind=="state";}
            if(checkpoint.History.Languages!=null)
            {
                OpenAtlas("people",0);atlasSection="Language and names";for(int i=0;i<8;i++)yield return null;
                evidence.LanguageProfiles=Array.Exists(atlasDocument.Sections,x=>x.Title=="Language and names"&&x.Timeline.Length==6)&&LanguageGenerator.Valid(checkpoint.History.Languages.Profiles[0]);
            }
            atlasOpen=false;humanOverlayOpacity=0;layer=preferredLayer=Layer.Ecoregions;Refresh();while(Busy)yield return null;
            evidence.BaseCard=CurrentBaseCard().Facts.Length>0;for(int i=0;i<8;i++)yield return null;
            humanOverlayOpacity=1;historyOverlay=HistoryOverlay.States;UpdateHumanPalette();
        }
        for(int i=0;i<12;i++)yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output,"dock-hud.png"));
        for(int i=0;i<8;i++)yield return null;
        evidence.Panels=new[]{SocietyPanel(0),SocietyPanel(1),SocietyPanel(2)};evidence.CheckpointUnchanged=world.Prehistory.ComputeHash()==before;
        Application.logMessageReceived-=recordError;evidence.GuiErrors=guiErrors.ToArray();
        evidence.Passed=evidence.PanelsMeet&&evidence.Resizable&&evidence.Clamped&&evidence.EraCorrect&&evidence.MusicLoaded&&evidence.MusicWaitsForPlay&&evidence.CheckpointUnchanged&&evidence.ContextualSelections&&evidence.AtlasProfiles&&evidence.LanguageProfiles&&evidence.BaseCard&&guiErrors.Count==0&&error.Length==0;
        File.WriteAllText(Path.Combine(output,"dock-check.json"),JsonUtility.ToJson(evidence,true));Debug.Log("DOCK CHECK "+JsonUtility.ToJson(evidence));Application.Quit(evidence.Passed?0:1);
    }
}
