using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class WorldGenRuntime
{
    [StructLayout(LayoutKind.Sequential)] struct ClimateMapInput {public Vector4 Temperature,Rain,Geo,Mask;}
    World climateMapWorld;ComputeShader climateMapShader;ComputeBuffer climateMapInputs,climateMapColors;
    RenderTexture climateMapOutput;Layer climateMapLayer;double climateMapYear=double.NaN;
    static int ClimateMapMode(Layer layer)
    {
        switch(layer){case Layer.Climate:return 0;case Layer.Satellite:return 1;
            case Layer.TemperatureSpring:return 2;case Layer.TemperatureSummer:return 3;case Layer.TemperatureAutumn:return 4;case Layer.TemperatureWinter:return 5;
            case Layer.PrecipitationSpring:return 6;case Layer.PrecipitationSummer:return 7;case Layer.PrecipitationAutumn:return 8;case Layer.PrecipitationWinter:return 9;default:return -1;}
    }
    Texture DatedClimateTexture()
    {
        int mode=ClimateMapMode(layer);if(mode<0||!SystemInfo.supportsComputeShaders||!palettes.TryGetValue(layer,out var source))return null;
        if(climateMapWorld!=world)
        {
            climateMapInputs?.Dispose();climateMapColors?.Dispose();if(climateMapOutput!=null){climateMapOutput.Release();Destroy(climateMapOutput);}
            climateMapShader=Resources.Load<ComputeShader>("WorldGenDatedClimate");if(climateMapShader==null)return null;
            int n=world.Surface.Cells.Length;var input=new ClimateMapInput[n];var f=world.Prehistory.Foragers;
            var seasons=new[]{world.Climate.NorthernSpring,world.Climate.NorthernSummer,world.Climate.NorthernAutumn,world.Climate.NorthernWinter};
            for(int cell=0;cell<n;cell++)
            {
                int region=f.CellDistrict[cell];if(region<0||world.Hydrology.OceanConnected[cell])continue;
                var d=f.Districts[region];double lat=world.Surface.Cells[cell].Position.Z;
                var dry=PaleolithicRules.Climate(world.Recipe.Seed,179000,lat,world.ElevationM[cell],d.Province);
                var row=new ClimateMapInput{Geo=new Vector4((float)lat,world.ElevationM[cell],(float)(1-dry.PrecipitationMultiplier),(float)d.OceanMoistureAccess),Mask=new Vector4(GlobeView.TryVisibleWater(world,cell,out _)?2:1,0,0,0)};
                for(int q=0;q<4;q++){row.Temperature[q]=seasons[q].TemperatureTenthsC[cell]/10f;row.Rain[q]=seasons[q].PrecipitationMm[cell]/3f;}input[cell]=row;
            }
            climateMapInputs=new ComputeBuffer(n,64);climateMapInputs.SetData(input);
            var colors=Enum.GetValues(typeof(Koppen)).Cast<Koppen>().Select(k=>{var c=ClimateColors.Classification(k);return new Vector4((float)c.X,(float)c.Y,(float)c.Z,1);}).ToArray();
            climateMapColors=new ComputeBuffer(colors.Length,16);climateMapColors.SetData(colors);
            climateMapOutput=new RenderTexture(2048,(n+2047)/2048,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear){enableRandomWrite=true,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};climateMapOutput.Create();
            climateMapShader.SetBuffer(0,"_Cells",climateMapInputs);climateMapShader.SetBuffer(0,"_ClimateColors",climateMapColors);climateMapShader.SetTexture(0,"_Output",climateMapOutput);climateMapShader.SetInt("_Count",n);
            climateMapWorld=world;climateMapYear=double.NaN;
        }
        double year=world.Prehistory.Foragers.Seconds/(double)SimulationTime.Year;
        if(climateMapYear!=year||climateMapLayer!=layer)
        {
            const double interval=250;double from=Math.Floor(year/interval)*interval;
            climateMapShader.SetTexture(0,"_BaseColors",source);climateMapShader.SetInt("_Layer",mode);climateMapShader.SetInt("_Linear",QualitySettings.activeColorSpace==ColorSpace.Linear?1:0);
            climateMapShader.SetFloat("_Year0",(float)from);climateMapShader.SetFloat("_Year1",(float)(from+interval));climateMapShader.SetFloat("_TimeBlend",(float)((year-from)/interval));
            climateMapShader.SetFloat("_Ice0",(float)PaleolithicRules.Climate(world.Recipe.Seed,from,0).Ice);climateMapShader.SetFloat("_Ice1",(float)PaleolithicRules.Climate(world.Recipe.Seed,from+interval,0).Ice);
            climateMapShader.Dispatch(0,(world.Surface.Cells.Length+127)/128,1,1);climateMapYear=year;climateMapLayer=layer;
        }
        return climateMapOutput;
    }
}
