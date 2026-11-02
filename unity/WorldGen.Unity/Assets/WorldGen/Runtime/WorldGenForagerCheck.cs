using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
public sealed partial class WorldGenRuntime
{
    IEnumerator CheckForagerView()
    {
        string folder=Argument("--worldgen-output","");Directory.CreateDirectory(folder);double deadline=Time.realtimeSinceStartupAsDouble+300;
        while((Busy||humans==null||!worldReady)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(Busy||humans==null||!Prehistory.Foraging(world.Prehistory)){Debug.LogError("FORAGER VIEW setup failed: "+error);Application.Quit(1);yield break;}
        var s=world.Prehistory;if(historyOverlay!=HistoryOverlay.Cultures){Debug.LogError("FORAGER VIEW loaded a forager world into an empty political layer");Application.Quit(1);yield break;}string original=s.Hash;screen="Simulation";humanPlaying=false;simulationPaused=false;SetProjection(Projection.Map);mapPan=Vector3.zero;centerLongitude=0;
        int runtimeErrors=0;Application.LogCallback errorCapture=(message,trace,type)=>{if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)runtimeErrors++;};Application.logMessageReceived+=errorCapture;
        layer=preferredLayer=Layer.Climate;satelliteOpacity=.55f;humanOverlayOpacity=1;gridSpacing=30;Refresh();
        peoplesOpen=true;detailsOpen=true;chronicleOpen=false;mapSettings=false;int checks=0;
        foreach(var mode in new[]{HistoryOverlay.Cultures,HistoryOverlay.Languages,HistoryOverlay.Religions,HistoryOverlay.Ethnicities,HistoryOverlay.Technology,HistoryOverlay.HumanSpecies,HistoryOverlay.Territories})
        {
            historyOverlay=mode;var entries=SocietyView.Legend(s,mode,engine:humans);selectedCell=entries.FirstOrDefault()?.Cell??-1;UpdateHumanPalette();ApplyCachedDisplay();
            double until=Time.realtimeSinceStartupAsDouble+60;
            do{yield return null;}while((humanPalettePending||humanPaletteJob!=null||humanPaletteReady!=null||borderLoading)&&Time.realtimeSinceStartupAsDouble<until);
            for(int i=0;i<6;i++)yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,mode+"-hud.png"));CaptureHumanMap(Path.Combine(folder,mode+"-map.png"),true);
            for(int i=0;i<6;i++)yield return new WaitForEndOfFrame();
            if(mode==HistoryOverlay.Territories?entries.Length!=0:entries.Length==0){Debug.LogError("FORAGER VIEW wrong entries: "+mode);Application.Quit(1);yield break;}checks++;
            File.WriteAllText(Path.Combine(folder,mode+"-card.json"),WorldGen.Serialization.WorldJson.Write(HistoryAtlas.Card(s,selectedCell,mode,humans)));
        }
        // Inspect separated populated regions at a useful local scale, with
        // the same real palettes/cards used by the player's map controls.
        peoplesOpen=detailsOpen=false;selectedCell=-1;gridSpacing=0;
        var regionalCells=new System.Collections.Generic.List<int>();
        foreach(var district in s.Foragers.Cohorts.Where(c=>c.Population>=1).GroupBy(c=>c.District).OrderByDescending(g=>g.Sum(c=>c.Population)))
        {
            int cell=s.Foragers.Districts[district.Key].Cell;var point=world.Surface.Cells[cell].Position;
            if(regionalCells.Any(previous=>world.Recipe.RadiusKm*Math.Acos(D.Clamp(V.Dot(point,world.Surface.Cells[previous].Position),-1,1))<1200))continue;
            regionalCells.Add(cell);if(regionalCells.Count==3)break;
        }
        for(int region=0;region<regionalCells.Count;region++)
        {
            var point=world.Surface.Cells[regionalCells[region]].Position;centerLongitude=(float)(Math.Atan2(point.Y,point.X)*180/Math.PI);mapPan=new Vector3(0,(float)(Math.Asin(point.Z)*2/Math.PI),0);mapZoom=.20f;
            foreach(var mode in new[]{HistoryOverlay.Cultures,HistoryOverlay.Languages,HistoryOverlay.Religions,HistoryOverlay.Technology,HistoryOverlay.HumanSpecies})
            {
                historyOverlay=mode;UpdateHumanPalette();double until=Time.realtimeSinceStartupAsDouble+60;
                do{yield return null;}while((humanPalettePending||humanPaletteJob!=null||humanPaletteReady!=null)&&Time.realtimeSinceStartupAsDouble<until);
                for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"region-"+region+"-"+mode+".png"));for(int frame=0;frame<4;frame++)yield return new WaitForEndOfFrame();checks++;
            }
        }
        mapZoom=1;centerLongitude=0;mapPan=Vector3.zero;
        foreach(var category in new[]{"world","technology","language","religion","human-species","people"})
        {
            var entry=category=="world"?null:HistoryAtlas.Directory(s,category).FirstOrDefault();if(category!="world"&&entry==null)continue;
            OpenAtlas(category,entry?.Id??-1);for(int i=0;i<8;i++)yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"atlas-"+category+".png"));for(int i=0;i<6;i++)yield return new WaitForEndOfFrame();checks++;
            File.WriteAllText(Path.Combine(folder,"atlas-"+category+".json"),WorldGen.Serialization.WorldJson.Write(atlasDocument));
            if(category=="technology"){atlasSection="Local knowledge and use";for(int i=0;i<8;i++)yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"atlas-toolkit.png"));for(int i=0;i<6;i++)yield return new WaitForEndOfFrame();}
        }
        atlasOpen=false;peoplesOpen=detailsOpen=false;selectedCell=-1;humanOverlayOpacity=0;satelliteOpacity=1;
        foreach(var landscape in new[]{Layer.Climate,Layer.Satellite,Layer.Biomes,Layer.Aridity,Layer.Permafrost,Layer.TemperatureWinter,Layer.PrecipitationSummer})
        {
            layer=preferredLayer=landscape;Refresh();double until=Time.realtimeSinceStartupAsDouble+90;
            do{yield return null;}while((Busy||humanPalettePending||humanPaletteJob!=null||humanPaletteReady!=null||paletteLoading.Contains(landscape))&&Time.realtimeSinceStartupAsDouble<until);
            for(int i=0;i<8;i++)yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"landscape-"+landscape+".png"));for(int i=0;i<4;i++)yield return new WaitForEndOfFrame();checks++;
        }
        layer=preferredLayer=Layer.Climate;humanOverlayOpacity=1;satelliteOpacity=.55f;historyOverlay=HistoryOverlay.Languages;UpdateHumanPalette();
        foreach(float longitude in new[]{-120f,0f,120f})
        {
            centerLongitude=longitude;Refresh();for(int i=0;i<14;i++)yield return new WaitForEndOfFrame();CaptureHumanMap(Path.Combine(folder,"language-longitude-"+longitude+".png"),true);
        }
        if(Environment.GetCommandLineArgs().Contains("--worldgen-forager-playback"))
        {
            atlasOpen=peoplesOpen=detailsOpen=false;selectedCell=-1;humanPlayback.SetRate(100d*SimulationTime.Year);humanClockSample=Time.realtimeSinceStartupAsDouble;humanPlaying=true;
            long beginning=SimulationTime.Seconds(s);double until=Time.realtimeSinceStartupAsDouble+180;
            while(SimulationTime.Seconds(world.Prehistory)<beginning+30*SimulationTime.Year&&Time.realtimeSinceStartupAsDouble<until&&string.IsNullOrEmpty(error))yield return null;
            humanPlaying=false;
            while(humanStep!=null&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            if(humanStep!=null||SimulationTime.Seconds(world.Prehistory)<beginning+30*SimulationTime.Year||!string.IsNullOrEmpty(error)){Debug.LogError("FORAGER PLAYBACK failed to publish live progress: "+error);Application.Quit(1);yield break;}checks++;
            var final=world.Prehistory;long advanced=SimulationTime.Seconds(final)-beginning;
            var reference=System.Threading.Tasks.Task.Run(()=>humans.AdvanceSeconds(s,advanced));
            while(!reference.IsCompleted&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            if(!reference.IsCompleted||reference.IsFaulted||reference.Result.Hash!=final.Hash){Debug.LogError("FORAGER PLAYBACK differs from direct reference");Application.Quit(1);yield break;}checks++;
            while((humanPalettePending||humanPaletteJob!=null||humanPaletteReady!=null)&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            if(legendState?.Hash!=final.Hash||!historyLandscapeEpochs.TryGetValue(Layer.Climate,out long epoch)||epoch!=LandscapeEpoch(final)){Debug.LogError("FORAGER PLAYBACK left stale human/climate palettes");Application.Quit(1);yield break;}checks++;
            File.WriteAllText(Path.Combine(folder,"playback.json"),"{\"startYear\":"+s.Years+",\"endYear\":"+final.Years+",\"hash\":\""+final.Hash+"\"}");
            for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"live-playback.png"));for(int frame=0;frame<4;frame++)yield return new WaitForEndOfFrame();
        }
        Application.logMessageReceived-=errorCapture;
        if(s.Hash!=s.ComputeHash()||s.Hash!=original||runtimeErrors>0){Debug.LogError("FORAGER VIEW failed: hash or "+runtimeErrors+" runtime errors");Application.Quit(1);yield break;}
        File.WriteAllText(Path.Combine(folder,"result.json"),"{\"checks\":"+checks+",\"year\":"+s.Years+",\"hash\":\""+s.Hash+"\"}");Debug.Log("FORAGER VIEW PASS "+checks+" "+s.Hash);Application.Quit(0);
    }
}
