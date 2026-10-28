using System;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    PlayCommandResult DescribeTreeFlora(string[] words)
    {
        if(!Ready)return new PlayCommandResult(false,"Terrain is not ready yet.");
        var at=terrain.Patch(Position,100).Sample(Position);var region=terrain.FloraRegion(at.Cell);var species=region.Trees;
        if(words.Length==1)
        {
            var lines=new string[species.Length+2];lines[0]="Generated canopy lineages · region "+region.RegionId+" · "+at.Biome;
            for(int i=0;i<species.Length;i++)lines[i+1]=(i+1)+". "+species[i].Name+" · "+species[i].Form+" · "+species[i].LeafHabit+" · "+species[i].HeightM.ToString("F1")+"m";
            lines[lines.Length-1]="/flora: ancestry, anatomy, wood and reproduction. Potential catalogue, not a census.";return new PlayCommandResult(true,lines);
        }
        if(words.Length!=2||!int.TryParse(words[1],out int index)||index<1||index>species.Length)return new PlayCommandResult(false,"Usage: /flora [1-"+species.Length+"]");
        var s=species[index-1];var m=s.Morphology;var r=s.Reproduction;
        return new PlayCommandResult(true,s.Name+" · family "+s.FamilyName+" → province "+s.ProvinceId+" → region "+s.RegionId,
            s.Form+" · "+m.LeafShape+" · "+s.LeafHabit+" · "+m.BarkPattern+" bark; canopy potential "+s.HeightM.ToString("F1")+" × "+s.WidthM.ToString("F1")+"m; "+m.Strategy+" architecture, size cohorts and shared render archetypes.",
            "Wood density "+m.WoodDensityKgM3.ToString("F0")+" kg/m³; relative hardness "+m.HardnessIndex.ToString("F2")+"; decay resistance "+m.DecayResistance.ToString("F2")+". Physical proxies, not tested timber grades.",
            "Warm niche "+s.MinWarmC.ToString("F0")+"–"+s.MaxWarmC.ToString("F0")+"°C; cold limit "+s.MinColdC.ToString("F0")+"°C; moisture "+s.Moisture.ToString("F2")+"; shade tolerance "+s.ShadeTolerance.ToString("F2")+".",
            "Flowers "+(r.HasFlowers?"yes":"no")+"; reproductive form "+r.Fruit+"; relative tissue hazards: flesh "+r.FleshToxicity.ToString("F2")+", seeds "+r.SeedToxicity.ToString("F2")+", leaves "+r.LeafToxicity.ToString("F2")+".",
            "Static potential traits: no live growth, seasonal leaf loss/fruit crops, harvesting or poison effects yet.");
    }
    static Texture2D CreateForestSurface(Tile tile)
    {
        if(tile.ForestCover==null||tile.ForestCover.Length==0)return null;
        int side=tile.Segments+1;var pixels=SurfacePixelStaging(side*side);
        for(int i=0;i<pixels.Length;i++){int at=i*8;pixels[i]=new Color(tile.ForestCover[at],tile.ForestCover[at+1],tile.ForestCover[at+2],tile.ForestCover[at+3]);}
        var texture=new Texture2D(side,side,TextureFormat.RGBAHalf,false,true){name="Forest canopy colour/coverage",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        texture.SetPixels(pixels);texture.Apply(false,true);return texture;
    }
}
