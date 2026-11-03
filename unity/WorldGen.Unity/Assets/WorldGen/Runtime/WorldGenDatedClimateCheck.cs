using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
public sealed partial class WorldGenRuntime
{
    void CaptureDatedMap(string path)
    {
        var target=new RenderTexture(1600,800,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cameraView,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});
        var previous=RenderTexture.active;RenderTexture.active=target;var capture=new Texture2D(1600,800,TextureFormat.RGB24,false);capture.ReadPixels(new Rect(0,0,1600,800),0,0);capture.Apply();RenderTexture.active=previous;
        File.WriteAllBytes(path,capture.EncodeToPNG());Destroy(capture);target.Release();Destroy(target);
    }
    IEnumerator CheckDatedClimate()
    {
        string output=Argument("--worldgen-output","");Directory.CreateDirectory(output);double deadline=Time.realtimeSinceStartupAsDouble+180;int checks=0;
        while((Busy||humans==null||!worldReady)&&error.Length==0&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(humans==null||!worldReady||Busy)throw new Exception("Dated map setup failed: "+error);
        if(historyOverlay!=HistoryOverlay.Civilizations)throw new Exception("Civilizations is not the default map");checks++;
        humanPlaying=false;simulationPaused=false;screen="Simulation";SetProjection(Projection.Map);atlasOpen=peoplesOpen=detailsOpen=chronicleOpen=false;layer=preferredLayer=Layer.Climate;
        var initial=world.Prehistory;satelliteOpacity=0;humanOverlayOpacity=1;Refresh();UpdateHumanPalette();
        while(Busy||humanPalettePending)yield return null;for(int i=0;i<4;i++)yield return new WaitForEndOfFrame();CaptureDatedMap(Path.Combine(output,"Civilizations.png"));
        if(globe.transform.Cast<Transform>().Any(t=>t.name=="natural"||t.name=="smallfeatures"))throw new Exception("Smoothed map geometry was still generated");checks++;
        var rows=new System.Collections.Generic.List<string>();
        foreach(int year in new[]{0,85000,100000,179000,200000})foreach(var selectedLayer in new[]{Layer.Climate,Layer.Satellite})
        {
            world.Prehistory=initial.Copy();world.Prehistory.Foragers.Seconds=year*SimulationTime.Year;world.Prehistory.Years=year;layer=selectedLayer;satelliteOpacity=1;humanOverlayOpacity=0;humanPalettePending=true;ApplyCachedDisplay();PollRegionalMap();
            if(climateMapOutput==null)throw new Exception("Dated climate compute did not run");
            yield return new WaitForEndOfFrame();var previous=RenderTexture.active;RenderTexture.active=climateMapOutput;var copy=new Texture2D(climateMapOutput.width,climateMapOutput.height,TextureFormat.RGBAFloat,false,true);copy.ReadPixels(new Rect(0,0,copy.width,copy.height),0,0);copy.Apply();RenderTexture.active=previous;var colors=copy.GetPixels();Destroy(copy);
            int compared=0,mismatched=0;float maximumError=0;
            for(int cell=0;cell<world.Surface.Cells.Length;cell+=173)
            {
                if(!ForagerLandscape.TryColor(world,world.Prehistory,cell,layer,out var expected))continue;compared++;Color actual=colors[cell];
                float C(double x)=>QualitySettings.activeColorSpace==ColorSpace.Linear?Mathf.GammaToLinearSpace((float)x):(float)x;
                float difference=Mathf.Max(Mathf.Abs(actual.r-C(expected.X)),Mathf.Abs(actual.g-C(expected.Y)),Mathf.Abs(actual.b-C(expected.Z)));maximumError=Mathf.Max(maximumError,difference);if(difference>.006f)mismatched++;
            }
            string row=year+" "+layer+" compared="+compared+" mismatched="+mismatched+" maxError="+maximumError;Debug.Log(row);rows.Add(row);
            if(compared<100||mismatched>Math.Max(1,compared/200))throw new Exception("GPU climate differs from local CPU climate: "+row);checks++;
            for(int frame=0;frame<3;frame++)yield return new WaitForEndOfFrame();CaptureDatedMap(Path.Combine(output,layer+"-"+year+".png"));for(int frame=0;frame<3;frame++)yield return new WaitForEndOfFrame();
        }
        if(iceCycleIcon!=null)File.WriteAllBytes(Path.Combine(output,"ice-cycle.png"),iceCycleIcon.EncodeToPNG());
        File.WriteAllLines(Path.Combine(output,"gpu-reference.txt"),rows);File.WriteAllText(Path.Combine(output,"checks.txt"),"PASS "+checks);Debug.Log("DATED CLIMATE PASS "+checks);Application.Quit(0);
    }
}
