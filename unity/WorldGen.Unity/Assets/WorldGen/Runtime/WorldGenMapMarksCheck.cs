using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    IEnumerator CheckMapMarks()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);AudioListener.volume=0;
        double deadline=Time.realtimeSinceStartupAsDouble+180;
        while((Busy||humans==null||!worldReady||humanPaletteJob!=null||humanPaletteReady!=null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(Busy||humans==null||!worldReady){Debug.LogError("MAP MARKS load failed: "+error);Application.Quit(1);yield break;}
        string hash=world.Prehistory.ComputeHash();screen="Simulation";humanPlaying=false;simulationPaused=false;atlasOpen=false;saveDialog=false;GUIUtility.keyboardControl=0;
        SetProjection(Projection.Map);layer=preferredLayer=Layer.Climate;satelliteOpacity=0;humanOverlayOpacity=.35f;gridSpacing=0;showRivers=true;selectedCell=-1;
        while(Busy||humanPaletteJob!=null||humanPaletteReady!=null)yield return null;
        int cell=cachedSettlementMarkers.Length==5&&cachedSettlementMarkers[1].Length>0?cachedSettlementMarkers[1][0].Cell:humans.Patches[world.Prehistory.Origin].Cell;
        var point=world.Surface.Cells[cell].Position;centerLongitude=(float)(Math.Atan2(point.Y,point.X)*180/Math.PI);mapPan=new Vector3(0,(float)(Math.Asin(point.Z)*2/Math.PI),0);ApplyCachedDisplay();
        string previousImage="";
        foreach(float zoom in new[]{.30f,.09f,.015f})
        {
            mapZoom=zoom;for(int i=0;i<5;i++)yield return new WaitForEndOfFrame();
            string image=Path.Combine(output,"rivers-"+zoom.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+".png");CaptureHumanMap(image,true);
            string signature;using(var sha=System.Security.Cryptography.SHA256.Create())signature=Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(image)));
            if(signature==previousImage){Debug.LogError("MAP MARKS zoom captures are identical.");Application.Quit(1);yield break;}previousImage=signature;
        }
        var testKeyboard=InputSystem.AddDevice<Keyboard>();testKeyboard.MakeCurrent();GUIUtility.keyboardControl=0;
        var priorRoot=globe;bool riverBefore=showRivers;InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.R));yield return null;yield return null;
        InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());yield return null;
        if(showRivers==riverBefore||globe!=priorRoot){Debug.LogError("MAP MARKS R hotkey failed or rebuilt the view: visible="+showRivers+", before="+riverBefore+", focus="+GUIUtility.keyboardControl);Application.Quit(1);yield break;}
        CaptureHumanMap(Path.Combine(output,"rivers-off.png"),true);
        settlementMarkerMode=SettlementMarkerMode.Off;
        foreach(var expected in new[]{SettlementMarkerMode.CapitalsOnly,SettlementMarkerMode.All,SettlementMarkerMode.CitiesOnly,SettlementMarkerMode.LargeCitiesOnly,SettlementMarkerMode.Off})
        {
            testKeyboard.MakeCurrent();GUIUtility.keyboardControl=0;InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.C));yield return null;yield return null;
            InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());yield return null;
            if(settlementMarkerMode!=expected){Debug.LogError("MAP MARKS C hotkey failed at "+expected);Application.Quit(1);yield break;}
        }
        if(world.Prehistory.ComputeHash()!=hash){Debug.LogError("MAP MARKS changed historical state.");Application.Quit(1);yield break;}
        File.WriteAllText(Path.Combine(output,"result.txt"),"PASS R cached visibility; C five-mode cycle; three river zoom captures; unchanged history "+hash);
        Debug.Log("MAP MARKS PASS "+hash);Application.Quit(0);
    }
}
