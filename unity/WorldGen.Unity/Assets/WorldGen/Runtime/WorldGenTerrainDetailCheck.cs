using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Serialization;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckTerrainDetail(string output,string hash)
    {
        var report=new List<string>();
        Application.logMessageReceived+=(message,trace,kind)=>{if(kind==LogType.Error||kind==LogType.Exception){File.WriteAllText(Path.Combine(output,"failure.txt"),message+"\n"+trace);Application.Quit(1);}};
        var options=TerrainVideoOptions.Current;options.Quality=2;options.Effort=1;options.ViewDistanceM=60000;options.FullDetailTiles=1.5;TerrainVideoOptions.Revision++;
        var session=WorldLibrary.ReadPlaySession(Argument("--worldgen-local-terrain-check",""));play.Settings.HorizontalScale=.1;
        double start=Time.realtimeSinceStartupAsDouble,deadline=start+120;ExploreTerrain(session.PlayerPosition);
        while((terrainExplorer==null||!terrainExplorer.Ready)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        var e=terrainExplorer;if(e==null||!e.Ready)throw new Exception("Adaptive explorer unavailable");e.TestView(120,20,12);e.BeginLoadingProfile();
        bool near=false,horizon=false;
        while(Time.realtimeSinceStartupAsDouble<deadline)
        {
            if(e.Error!="")throw new Exception(e.Error);
            if(!near&&e.NearReady){near=true;report.Add("Cold near ready "+(Time.realtimeSinceStartupAsDouble-start).ToString("F3")+"s; "+e.Diagnostics);}
            if(!horizon&&e.StreamingFogDistance>=58000){horizon=true;report.Add("Horizon reveal "+(Time.realtimeSinceStartupAsDouble-start).ToString("F3")+"s; "+e.Diagnostics);}
            if(near&&e.AllReady&&!e.DetailPlanning)break;
            yield return null;
        }
        report.Add((e.AllReady?"Full requested detail ":"PARTIAL requested detail ")+(Time.realtimeSinceStartupAsDouble-start).ToString("F3")+"s; "+e.Diagnostics);
        report.Add(e.EndLoadingProfile());e.Capture(Path.Combine(output,"terrain.png"));
        File.WriteAllLines(Path.Combine(output,"probes.csv"),new[]{"originX,originY,originZ,east,north,height"}.Concat(e.Probes()));
        if(!near)throw new Exception("Near terrain never ready");
        e.Pause();playPauseTab="Game settings";
        for(int tab=0;tab<SettingsTabs.Length;tab++)
        {
            settingsCategory=tab;settingsScroll=Vector2.zero;yield return null;yield return new WaitForEndOfFrame();
            var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,"settings-"+SettingsTabs[tab]+".png"),image.EncodeToPNG());Destroy(image);
        }
        report.Add("All five shared pause-settings categories rendered without GUI exceptions. Screen captures require visual review.");
        int originalQuality=options.Quality;options.Quality=0;options.Effort=0;TerrainVideoOptions.Revision++;yield return null;yield return null;
        double until=Time.realtimeSinceStartupAsDouble+3;while(e.DetailPlanning&&Time.realtimeSinceStartupAsDouble<until)yield return null;
        report.Add("Live Low / Low: "+e.TestDebugText().Split('\n').First(t=>t.StartsWith("Adaptive terrain")));
        options.Quality=originalQuality;options.Effort=1;TerrainVideoOptions.Revision++;
        var monitor=Screen.mainWindowDisplayInfo;report.Add("Current monitor "+monitor.width+"x"+monitor.height+"; fullscreen uses these dimensions, not window dimensions.");
        if(StateHash.Compute(world)!=hash)throw new Exception("Terrain/settings altered world/history");
        report.Add("PASS source "+hash);File.WriteAllLines(Path.Combine(output,"terrain-detail.txt"),report);
        e.Close();Application.Quit(0);
    }
}
