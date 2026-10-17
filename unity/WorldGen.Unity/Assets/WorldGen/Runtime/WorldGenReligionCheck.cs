using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckReligiousView()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);AudioListener.volume=0;
        double deadline=Time.realtimeSinceStartupAsDouble+300;
        while((Busy||humans==null||!worldReady||humanPaletteJob!=null||humanPaletteReady!=null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(Busy||humans==null||!worldReady){Debug.LogError("RELIGION CHECK load failed: "+error);Application.Quit(1);yield break;}
        string hash=world.Prehistory.ComputeHash();screen="Simulation";humanPlaying=false;simulationPaused=false;
        if(world.Prehistory.History.Aggregates.Empires.Connections.Religion.Traditions==null){Debug.LogError("RELIGION CHECK requires an explicitly upgraded/new history.");Application.Quit(1);yield break;}
        SetProjection(Projection.Map);layer=preferredLayer=Layer.Climate;satelliteOpacity=0;humanOverlayOpacity=1;gridSpacing=30;
        foreach(var mode in new[]{HistoryOverlay.Religions,HistoryOverlay.ReligiousInstitutions})
        {
            historyOverlay=mode;var entries=SocietyView.Legend(world.Prehistory,mode);if(entries.Length==0){Debug.LogError("RELIGION CHECK missing "+mode+" actors.");Application.Quit(1);yield break;}
            selectedCell=entries[0].Cell;UpdateHumanPalette();ApplyCachedDisplay();
            while((Busy||humanPaletteJob!=null||humanPaletteReady!=null||humanPalettePending)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            var point=world.Surface.Cells[selectedCell].Position;centerLongitude=(float)(Math.Atan2(point.Y,point.X)*180/Math.PI);mapPan=new Vector3(0,(float)(Math.Asin(point.Z)*2/Math.PI),0);mapZoom=.3f;ApplyCachedDisplay();
            for(int frame=0;frame<5;frame++)yield return new WaitForEndOfFrame();
            CaptureHumanMap(Path.Combine(output,mode+".png"),true);
            var subject=HistoryAtlas.Selection(world.Prehistory,selectedCell,mode);
            if(subject==null||mode==HistoryOverlay.ReligiousInstitutions&&subject.Kind!="religious-body"){Debug.LogError("RELIGION CHECK wrong focused subject.");Application.Quit(1);yield break;}
            var document=HistoryAtlas.Read(world.Prehistory,subject.Kind,subject.Id,world,humans);File.WriteAllText(Path.Combine(output,mode+".json"),WorldJson.Write(document));
            if(mode==HistoryOverlay.Religions&&!document.Sections.Any(x=>x.Title=="Social teachings")){Debug.LogError("RELIGION CHECK stripped/missing doctrinal profile.");Application.Quit(1);yield break;}
        }
        if(world.Prehistory.ComputeHash()!=hash){Debug.LogError("RELIGION CHECK presentation mutated history.");Application.Quit(1);yield break;}
        File.WriteAllText(Path.Combine(output,"result.txt"),"PASS native doctrinal/institutional overlays, actual selection/atlas and unchanged history "+hash);Debug.Log("RELIGION CHECK PASS "+hash);Application.Quit(0);
    }
}
