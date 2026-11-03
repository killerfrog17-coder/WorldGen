using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    sealed class RegionalMapLayer
    {
        public Texture2D Texture;public Color32[] Colors;public PrehistoryState State;public int Focus;
        public HistoryLegendEntry[] Legend,Labels;public double Updated;
    }
    World regionalMapWorld;Texture2D regionalCells;
    readonly Dictionary<HistoryOverlay,RegionalMapLayer> regionalLayers=new Dictionary<HistoryOverlay,RegionalMapLayer>();
    readonly Dictionary<Layer,RegionalMapLayer> regionalBackgrounds=new Dictionary<Layer,RegionalMapLayer>();
    Texture2D RegionalTexture(int count)=>new Texture2D(2048,Math.Max(1,(count+2047)/2048),TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
    void BindRegional(Material material,Texture2D colors,Texture2D background)
    {
        if(material==null)return;material.SetFloat("_Regional",1);material.SetTexture("_RegionalCells",regionalCells);material.SetTexture("_RegionalColors",colors);material.SetFloat("_RegionalHeight",colors.height);
        material.SetFloat("_RegionalBackground",background==null?0:1);if(background!=null)material.SetTexture("_RegionalBack",background);
    }
    bool PollRegionalMap()
    {
        if(!Prehistory.Foraging(world?.Prehistory)||humans==null||displayedWorld!=world||humanColors==null)return false;
        var state=world.Prehistory;var f=state.Foragers;
        if(regionalMapWorld!=world)
        {
            if(regionalCells!=null)Destroy(regionalCells);foreach(var item in regionalLayers.Values)Destroy(item.Texture);foreach(var item in regionalBackgrounds.Values)Destroy(item.Texture);
            regionalLayers.Clear();regionalBackgrounds.Clear();regionalMapWorld=world;regionalCells=RegionalTexture(world.Surface.Cells.Length);
            var pixels=new Color32[regionalCells.width*regionalCells.height];
            for(int i=0;i<world.Surface.Cells.Length;i++)
            {
                int id=f.CellDistrict[i]+1;bool water=world.Hydrology.OceanConnected[i]||world.Hydrology.LakeCoverageFraction[i]>=.9;
                pixels[i]=new Color32((byte)id,(byte)(id>>8),(byte)(id>>16),(byte)(water?0:85));
            }
            regionalCells.SetPixels32(pixels);regionalCells.Apply(false,true);
        }
        double time=Time.realtimeSinceStartupAsDouble;int focus=selectedCell<0?-1:ForagerMaps.For(state).Id(selectedCell,historyOverlay);
        if(!regionalLayers.TryGetValue(historyOverlay,out var cache))
        {cache=new RegionalMapLayer{Texture=RegionalTexture(f.Districts.Length),Focus=int.MinValue};cache.Colors=new Color32[cache.Texture.width*cache.Texture.height];regionalLayers[historyOverlay]=cache;}
        bool update=cache.State==null||cache.Focus!=focus||humanPalettePending||!PrehistoryState.SameDomain(cache.State,state)&&time-cache.Updated>=.1;
        if(update)
        {
            var map=ForagerMaps.For(state);
            foreach(var d in f.Districts)
            {
                int id=map.Id(d.Cell,historyOverlay);if(id<0){cache.Colors[d.Id]=new Color32();continue;}
                var color=map.Color(id,historyOverlay);cache.Colors[d.Id]=new Color32((byte)Math.Round(color.X*255),(byte)Math.Round(color.Y*255),(byte)Math.Round(color.Z*255),(byte)(id==focus?245:235));
            }
            cache.Texture.SetPixels32(cache.Colors);cache.Texture.Apply(false,false);cache.State=state;cache.Focus=focus;cache.Updated=time;
            cache.Legend=map.Legend(historyOverlay,societySort);cache.Labels=map.MapLabels(historyOverlay);
        }
        Texture2D background=null;
        // The background is invisible at zero satellite opacity. Never build
        // hundreds of thousands of invisible weather/habitat cells per tick.
        if(satelliteOpacity>.001f&&ForagerLandscape.Affected(layer))
        {
            if(!regionalBackgrounds.TryGetValue(layer,out var back))
            {back=new RegionalMapLayer{Texture=RegionalTexture(f.Districts.Length)};back.Colors=new Color32[back.Texture.width*back.Texture.height];regionalBackgrounds[layer]=back;}
            if(back.State==null||time-back.Updated>=.5&&!PrehistoryState.SameDomain(back.State,state))
            {
                foreach(var d in f.Districts)if(ForagerLandscape.TryColor(world,state,d.Cell,layer,out var color))
                {float Channel(double x)=>QualitySettings.activeColorSpace==ColorSpace.Linear?Mathf.GammaToLinearSpace((float)x):(float)x;back.Colors[d.Id]=new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(Channel(color.X)*255),0,255),(byte)Mathf.Clamp(Mathf.RoundToInt(Channel(color.Y)*255),0,255),(byte)Mathf.Clamp(Mathf.RoundToInt(Channel(color.Z)*255),0,255),255);}
                back.Texture.SetPixels32(back.Colors);back.Texture.Apply(false,false);back.State=state;back.Updated=time;
            }
            background=back.Texture;
        }
        BindRegional(surfaceMaterial,cache.Texture,background);BindRegional(naturalMaterial,cache.Texture,background);BindRegional(smallFeatureMaterial,cache.Texture,background);
        legendState=cache.State;legendEngine=humans;legendOverlay=historyOverlay;legendSort=societySort;cachedHistoryLegend=cache.Legend;cachedMapLabels=cache.Labels;cachedHeadlines=f.Events.Select(e=>new ChronicleEvent{Year=(int)(e.Seconds/SimulationTime.Year),Category=ChronicleCategory.Society,Text=e.Text}).ToArray();
        cachedSettlementMarkers=Array.Empty<SettlementMarker[]>();lastHumanSelection=selectedCell;humanPalettePending=false;return true;
    }
}
