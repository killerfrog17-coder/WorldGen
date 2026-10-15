using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    World displayedWorld;
    Projection VisibleProjection => committedOptions?.Projection ?? projection;
    Dictionary<Layer,Color32[]> pendingPalettes;
    readonly Dictionary<Layer,Texture2D> palettes=new Dictionary<Layer,Texture2D>();
    static Dictionary<Layer,Color32[]> BuildPalettes(World w,Layer selected,bool linear,CancellationToken token,bool main=true)
    {
        var result=new Dictionary<Layer,Color32[]>();
        var layers=main?new List<Layer>(DisplayCache.MainLayers):new List<Layer>();if(!layers.Contains(selected))layers.Add(selected);
        if(main){layers.Add((Layer)1000);layers.Add((Layer)1001);}
        foreach(var candidate in layers)
        {
            bool natural=(int)candidate>=1000;var actual=natural?(Layer)((int)candidate-1000):candidate;
            if(!GenerationPreview.Available(w,actual))continue;
            var values=natural?NaturalSurface.Colors(w,actual,token):GlobeView.CellValues(w,candidate,token:token).Colors;
            var colors=new Color32[((w.Surface.Cells.Length+2047)/2048)*2048];
            byte C(float v)=>(byte)Mathf.Clamp(Mathf.RoundToInt((linear?Mathf.GammaToLinearSpace(v):v)*255),0,255);
            for(int i=0;i<values.Length/3;i++){if((i&2047)==0)token.ThrowIfCancellationRequested();colors[i]=new Color32(C(values[i*3]),C(values[i*3+1]),C(values[i*3+2]),255);}
            if(candidate==Layer.Simulation){var codes=PrehistoryView.Codes(w);for(int i=0;i<codes.Length;i++)colors[i].a=codes[i];}
            result[candidate]=colors;
        }
        return result;
    }
    void InstallPalettes()
    {
        foreach(var texture in palettes.Values)Destroy(texture);palettes.Clear();
        foreach(var item in pendingPalettes)
        {
            if(item.Key==Layer.Simulation)humanColors=(Color32[])item.Value.Clone();
            var texture=new Texture2D(2048,item.Value.Length/2048,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(item.Value);texture.Apply(false,item.Key!=Layer.Simulation);palettes[item.Key]=texture;
        }
        pendingPalettes=null;
    }
    bool ApplyCachedDisplay()
    {
        if(presentationCamera!=null)presentationCamera.renderPostProcessing=screen=="Simulation"?false:originalPostProcessing;
        if(globe==null || (displayedWorld!=world && job==null) || committedOptions==null || (committedOptions.Projection!=projection && job==null))return false;
        if(!palettes.TryGetValue(layer,out var texture))
        {
            // Diagnostic colors are computed once, without remeshing the surface.
            if(job!=null)return true;
            StartCoroutine(LoadDiagnosticPalette(layer));return true;
        }
        surfaceMaterial.SetTexture("_Palette",texture);surfaceMaterial.SetFloat("_UsePalette",layer==Layer.Heightmap?0:1);
        foreach(var material in new[]{surfaceMaterial,naturalMaterial,smallFeatureMaterial})
        {
            material.SetFloat("_Simulation",screen=="Simulation"||layer==Layer.Simulation?1:0);material.SetFloat("_SatelliteOpacity",satelliteOpacity);material.SetFloat("_OverlayOpacity",humanOverlayOpacity);material.SetFloat("_Linear",QualitySettings.activeColorSpace==ColorSpace.Linear?1:0);
            if(palettes.TryGetValue(Layer.Simulation,out var mask))material.SetTexture("_HumanMask",mask);
        }
        surfaceMaterial.SetFloat("_MapMode",VisibleProjection==Projection.Map?1:0);lineMaterial.SetFloat("_MapMode",VisibleProjection==Projection.Map?1:0);
        surfaceMaterial.SetFloat("_Unlit",VisibleProjection==Projection.Map || layer==Layer.Climate || layer==Layer.Heightmap || EcologyView.IsLayer(layer)?1:0);
        bool natural=naturalTerrain && (layer==Layer.Terrain || layer==Layer.Satellite) && palettes.TryGetValue((Layer)(1000+(int)layer),out _);
        bool crispHistory=natural&&screen=="Simulation";
        surfaceMaterial.SetFloat("_Natural",crispHistory?1:0);surfaceMaterial.SetFloat("_BlendCells",crispHistory?1:0);surfaceMaterial.SetFloat("_PaletteHeight",texture.height);
        if(crispHistory)surfaceMaterial.SetTexture("_Palette",palettes[(Layer)(1000+(int)layer)]);
        if(natural){naturalMaterial.SetTexture("_Palette",palettes[(Layer)(1000+(int)layer)]);naturalMaterial.SetFloat("_UsePalette",1);}
        smallFeatureMaterial.SetFloat("_MapMode",VisibleProjection==Projection.Map?1:0);naturalMaterial.SetFloat("_MapMode",VisibleProjection==Projection.Map?1:0);naturalMaterial.SetFloat("_Unlit",VisibleProjection==Projection.Map?1:0);
        foreach(Transform child in globe.transform)
        {
            string kind=child.name;child.gameObject.SetActive(kind=="surface"&&(!natural||crispHistory) || kind=="natural"&&natural&&!crispHistory || kind=="smallfeatures"&&natural || kind=="rivers"&&showRivers || kind=="wire"&&wireframe ||
                kind=="plates"&&layer==Layer.Microplates || kind=="ecology"&&layer==Layer.Ecoregions || kind=="grid"+gridSpacing || kind=="history"&&screen=="Simulation");
            foreach(Transform copy in child)copy.gameObject.SetActive(copy.name=="copy0" || VisibleProjection==Projection.Map);
        }
        ApplyHistoryBorders();RequestHistoryBorders();
        committedOptions.Layer=layer;committedOptions.Wireframe=wireframe;committedOptions.ShowRivers=showRivers;committedOptions.GridSpacing=gridSpacing;
        return true;
    }
    readonly HashSet<Layer> paletteLoading=new HashSet<Layer>();
    System.Collections.IEnumerator LoadDiagnosticPalette(Layer candidate)
    {
        if(!paletteLoading.Add(candidate))yield break;
        var captured=displayedWorld;bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
        var task=System.Threading.Tasks.Task.Run(()=>BuildPalettes(captured,candidate,linear,CancellationToken.None,false));
        while(!task.IsCompleted)yield return null;
        paletteLoading.Remove(candidate);
        if(task.IsFaulted){error=task.Exception.GetBaseException().Message;yield break;}
        if(world!=captured || displayedWorld!=captured)yield break;
        var colors=task.Result[candidate];var texture=new Texture2D(2048,colors.Length/2048,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        texture.SetPixels32(colors);texture.Apply(false,true);palettes[candidate]=texture;
        if(layer==candidate)ApplyCachedDisplay();
    }
}
