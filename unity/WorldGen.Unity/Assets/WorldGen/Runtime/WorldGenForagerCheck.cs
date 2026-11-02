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
        var s=world.Prehistory;string original=s.Hash;screen="Simulation";humanPlaying=false;simulationPaused=false;SetProjection(Projection.Map);mapPan=Vector3.zero;centerLongitude=0;
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
        foreach(var category in new[]{"world","technology","language","religion","human-species","people"})
        {
            var entry=category=="world"?null:HistoryAtlas.Directory(s,category).FirstOrDefault();if(category!="world"&&entry==null)continue;
            OpenAtlas(category,entry?.Id??-1);for(int i=0;i<8;i++)yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"atlas-"+category+".png"));for(int i=0;i<6;i++)yield return new WaitForEndOfFrame();checks++;
            File.WriteAllText(Path.Combine(folder,"atlas-"+category+".json"),WorldGen.Serialization.WorldJson.Write(atlasDocument));
            if(category=="technology"){atlasSection="Local knowledge and use";for(int i=0;i<8;i++)yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"atlas-toolkit.png"));for(int i=0;i<6;i++)yield return new WaitForEndOfFrame();}
        }
        atlasOpen=false;peoplesOpen=detailsOpen=false;selectedCell=-1;humanOverlayOpacity=0;
        foreach(var landscape in new[]{Layer.Climate,Layer.Satellite,Layer.Biomes,Layer.Aridity,Layer.Permafrost,Layer.TemperatureWinter,Layer.PrecipitationSummer})
        {
            layer=preferredLayer=landscape;Refresh();double until=Time.realtimeSinceStartupAsDouble+90;
            do{yield return null;}while((Busy||humanPalettePending||humanPaletteJob!=null||humanPaletteReady!=null||paletteLoading.Contains(landscape))&&Time.realtimeSinceStartupAsDouble<until);
            for(int i=0;i<8;i++)yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"landscape-"+landscape+".png"));for(int i=0;i<4;i++)yield return new WaitForEndOfFrame();checks++;
        }
        layer=preferredLayer=Layer.Climate;humanOverlayOpacity=1;historyOverlay=HistoryOverlay.Languages;UpdateHumanPalette();
        foreach(float longitude in new[]{-120f,0f,120f})
        {
            centerLongitude=longitude;Refresh();for(int i=0;i<14;i++)yield return new WaitForEndOfFrame();CaptureHumanMap(Path.Combine(folder,"language-longitude-"+longitude+".png"),true);
        }
        Application.logMessageReceived-=errorCapture;
        if(s.Hash!=s.ComputeHash()||s.Hash!=original||runtimeErrors>0){Debug.LogError("FORAGER VIEW failed: hash or "+runtimeErrors+" runtime errors");Application.Quit(1);yield break;}
        File.WriteAllText(Path.Combine(folder,"result.json"),"{\"checks\":"+checks+",\"year\":"+s.Years+",\"hash\":\""+s.Hash+"\"}");Debug.Log("FORAGER VIEW PASS "+checks+" "+s.Hash);Application.Quit(0);
    }
}
