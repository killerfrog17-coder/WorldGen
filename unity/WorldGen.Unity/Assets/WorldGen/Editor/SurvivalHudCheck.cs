using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using WorldGen.Presentation;

public static class SurvivalHudCheck
{
    [MenuItem("WorldGen/Review survival HUD")]
    public static void Review()
    {
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        string output=Path.Combine(repo,"artifacts/survival-hud/review");Directory.CreateDirectory(output);
        string imagePath="Assets/WorldGen/Resources/SurvivalHud/Shapes.png";
        AssetDatabase.ImportAsset(imagePath,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(imagePath);
        importer.sRGBTexture=false;importer.mipmapEnabled=false;importer.alphaIsTransparency=false;
        importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=1024;importer.SaveAndReimport();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var checks=new List<string>();void Check(bool b,string label){if(!b)throw new InvalidDataException(label);checks.Add("PASS "+label);}
        var shader=Resources.Load<Shader>("WorldGenSurvivalHud");Check(shader!=null&&shader.isSupported,"HUD shader available");
        var camera=new GameObject("HUD review camera").AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;
        ColorUtility.TryParseHtmlString("#080A0E",out var background);camera.backgroundColor=background;camera.allowHDR=false;camera.allowMSAA=false;
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        var root=new GameObject("HUD review",typeof(RectTransform),typeof(Canvas));var canvas=root.GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var group=new GameObject("Approved layout",typeof(RectTransform),typeof(CanvasRenderer),typeof(SurvivalHudView));group.transform.SetParent(root.transform,false);
        var view=group.GetComponent<SurvivalHudView>();var rect=view.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(600,600);
        Color32[] Capture(string name,SurvivalHudValues values,int width=600,int height=600)
        {
            var rt=new RenderTexture(width,height,24);var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);var prior=RenderTexture.active;
            try
            {
                camera.targetTexture=rt;view.SetValues(values,true);view.SetAllDirty();Canvas.ForceUpdateCanvases();camera.Render();
                RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());return pixels.GetPixels32();
            }
            finally{camera.targetTexture=null;RenderTexture.active=prior;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
        }
        Color32 Pixel(Color32[] data,int x,int y)=>data[(599-y)*600+x];
        bool Red(Color32 c)=>c.r>c.g*2&&c.r>c.b*2;
        bool Blue(Color32 c)=>c.b>c.r*2&&c.b>c.g*1.5f;
        bool Cyan(Color32 c)=>c.g>c.r*2&&c.b>c.r*2;
        bool White(Color32 c)=>c.r>220&&c.g>220&&c.b>220;
        var value=new SurvivalHudValues{Health=.9f,Energy=.9f,Stamina=.85f,Hydration=.75f,Fullness=.65f,FeelsLikeC=22};
        var neutral=Capture("neutral",value);
        Check(!view.raycastTarget,"HUD never captures player clicks");
        value.FeelsLikeC=13;value.BodyStress=-.25f;Capture("chilly",value);
        value.FeelsLikeC=31;value.BodyStress=.25f;Capture("warm",value);
        value.FeelsLikeC=-12;value.BodyStress=-1;Capture("cold-100",value);
        value.FeelsLikeC=47;value.BodyStress=.99f;var p99=Capture("hot-99",value);value.BodyStress=1;var p100=Capture("hot-100",value);
        int maximum=p99.Zip(p100,(a,b)=>Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)))).Max();
        Check(maximum<12,"continuous actual 99-to100 glow; maximum RGB change="+maximum);
        value.FeelsLikeC=22;value.BodyStress=-.8f;Capture("neutral-air-cold-body",value);
        value.BodyStress=0;value.Health=value.Energy=value.Stamina=value.Hydration=value.Fullness=.5f;
        var half=Capture("half",value);
        Check(Red(Pixel(half,137,463))&&!Red(Pixel(half,137,137)),"health drains from top, lower half remains");
        Check(Blue(Pixel(half,463,463))&&!Blue(Pixel(half,463,137)),"energy drains from top");
        Check(Cyan(Pixel(half,451,451))&&!Cyan(Pixel(half,451,149)),"stamina drains from top");
        Check(Blue(Pixel(half,238,305))&&!Blue(Pixel(half,238,240)),"water drains from top inside droplet");
        Check(White(Pixel(half,360,380))&&!White(Pixel(half,400,240)),"stomach white fill remains at bottom");
        value.Health=value.Energy=value.Stamina=value.Hydration=value.Fullness=0;var empty=Capture("empty",value);
        Check(!Red(Pixel(empty,137,463))&&!Blue(Pixel(empty,463,463))&&!Cyan(Pixel(empty,451,451)),"zero arcs have no coloured caps or fill");
        value.Health=value.Energy=value.Stamina=value.Hydration=value.Fullness=1;var full=Capture("full",value);
        Check(Red(Pixel(full,137,137))&&Blue(Pixel(full,463,137))&&Cyan(Pixel(full,451,149)),"full arcs cover upper halves");
        Check(!Red(Pixel(full,300,69))&&!Blue(Pixel(full,300,69))&&!Red(Pixel(full,300,531))&&!Blue(Pixel(full,300,531)),"top and bottom arc gaps remain at full");
        value.Hydration=.05f;var low=Capture("water-five-percent",value);Check(Blue(Pixel(low,238,325)),"small water fill reaches the true bottom");
        value.Hydration=1;value.Energy=.3f;value.Stamina=1;var capped=Capture("stamina-capped",value);
        Check(!Cyan(Pixel(capped,451,149)),"shader also caps stamina at energy");
        value.Health=.9f;value.Energy=.9f;value.Stamina=.85f;value.Hydration=.75f;value.Fullness=.65f;
        var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=1;
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,0);rect.sizeDelta=new Vector2(256,256);rect.anchoredPosition=new Vector2(-16,16);
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(2560,1080)})
        {
            // Exercise the same height-based CanvasScaler policy with the actual target camera.
            var rt=new RenderTexture(size.x,size.y,24);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();
            typeof(CanvasScaler).GetMethod("Handle",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(scaler,null);
            camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            Capture("anchored-"+size.x+"x"+size.y,value,size.x,size.y);
            Check(rect.anchorMin==new Vector2(1,0)&&rect.pivot==new Vector2(1,0)&&rect.sizeDelta.x==rect.sizeDelta.y,"bottom-right uniform composition at "+size);
        }
        File.WriteAllLines(Path.Combine(output,"checks.txt"),checks);Debug.Log("SURVIVAL HUD CHECK PASS "+checks.Count);
        UnityEngine.Object.DestroyImmediate(root);
    }
}
