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
        public bool PanelsMeet,Resizable,Clamped,EraCorrect,MusicLoaded,CheckpointUnchanged,Passed;
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
        // A silent, tiny WAV in the isolated check output proves the real async
        // file/decoder path without adding an audio asset to the owner's library.
        musicFolder=Path.Combine(output,"music-fixture");Directory.CreateDirectory(musicFolder);brokenMusic.Clear();
        string track=Path.Combine(musicFolder,"Silent integration check 1-10.wav");
        using(var writer=new BinaryWriter(File.Create(track)))
        {
            const int samples=8000;writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(8000);writer.Write(16000);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples*2);writer.Write(new byte[samples*2]);
        }
        nextMusicCheck=0;deadline=Time.realtimeSinceStartupAsDouble+15;
        while(recentMusic.Count==0&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        evidence.MusicLoaded=recentMusic.Contains(track)&&musicSource.clip!=null;evidence.MusicDirectory=musicFolder;
        for(int i=0;i<12;i++)yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output,"dock-hud.png"));
        for(int i=0;i<8;i++)yield return null;
        evidence.Panels=new[]{SocietyPanel(0),SocietyPanel(1),SocietyPanel(2)};evidence.CheckpointUnchanged=world.Prehistory.ComputeHash()==before;
        evidence.Passed=evidence.PanelsMeet&&evidence.Resizable&&evidence.Clamped&&evidence.EraCorrect&&evidence.MusicLoaded&&evidence.CheckpointUnchanged&&error.Length==0;
        File.WriteAllText(Path.Combine(output,"dock-check.json"),JsonUtility.ToJson(evidence,true));Debug.Log("DOCK CHECK "+JsonUtility.ToJson(evidence));Application.Quit(evidence.Passed?0:1);
    }
}
