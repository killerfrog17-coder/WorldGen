using System;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    PlayCommandResult DescribeMidFlora(string[] words)
    {
        if(!Ready)return new PlayCommandResult(false,"Terrain is not ready yet.");
        var sample=terrain.Patch(Position,100).Sample(Position);var region=terrain.FloraRegion(sample.Cell);var species=region.MidPlants;
        if(words.Length==1)
        {
            var lines=new string[species.Length+2];lines[0]="Potential mid-height flora · region "+region.RegionId+" · "+sample.Biome;
            for(int i=0;i<species.Length;i++)lines[i+1]=(i+1)+". "+species[i].Name+" · "+species[i].Form+" · "+species[i].HeightM.ToString("F2")+"m";
            lines[lines.Length-1]="/midflora N: habitat, flowers/fruit, relative toxicity. Potential taxa, not a census of visible plants.";return new PlayCommandResult(true,lines);
        }
        if(words.Length!=2||!int.TryParse(words[1],out int index)||index<1||index>species.Length)return new PlayCommandResult(false,"Usage: /midflora [1-"+species.Length+"]");
        var s=species[index-1];var r=s.Reproduction;
        string Hazard(double value)=>value<=0?"none generated":value<.25?"low":value<.6?"moderate":"high";
        return new PlayCommandResult(true,s.Name+" · "+s.Form+" · founder "+s.Founder+" · four structural variants",
            "Height "+s.HeightM.ToString("F2")+"m; width "+s.WidthM.ToString("F2")+"m; colony "+s.ColonyM.ToString("F0")+"m; shade "+(s.Shade*100).ToString("F0")+"%.",
            "Warm niche "+s.MinWarmC.ToString("F0")+"–"+s.MaxWarmC.ToString("F0")+"°C; moisture index "+s.Moisture.ToString("F2")+"; pH "+s.MinPH.ToString("F1")+"–"+s.MaxPH.ToString("F1")+".",
            "Flowers: "+(r.HasFlowers?r.Pollination+" pollination":"none; spore-bearing")+"; fruit/seed structure: "+r.Fruit+"; dispersal: "+r.Dispersal+".",
            "Relative ingestion hazard · fruit flesh: "+Hazard(r.FleshToxicity)+" ("+r.FleshToxicity.ToString("F2")+"); seeds: "+Hazard(r.SeedToxicity)+"; leaves: "+Hazard(r.LeafToxicity)+". Contact irritation: "+Hazard(r.ContactIrritation)+".",
            "Fictional potential traits only: no edible-item gameplay, calibrated dosage, seasonal fruiting or animal-specific tolerance yet.");
    }
    static Texture2D CreateMidSurface(Tile tile)
    {
        if(tile.MidCover==null||tile.MidCover.Length==0)return null;
        int side=tile.Segments+1;var pixels=SurfacePixelStaging(side*side);
        for(int i=0;i<pixels.Length;i++){int at=i*6;pixels[i]=new Color(tile.MidCover[at],tile.MidCover[at+1],tile.MidCover[at+2],tile.MidCover[at+3]);}
        var texture=new Texture2D(side,side,TextureFormat.RGBAHalf,false,true){name="Mid-height cover colour/coverage",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        texture.SetPixels(pixels);texture.Apply(false,true);return texture;
    }
}
