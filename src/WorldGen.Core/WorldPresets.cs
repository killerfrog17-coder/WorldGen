using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    public sealed class PresetWindow
    {
        public string Field; public double Min,Max; public int Bias;
        public PresetWindow(string field,double min,double max,int bias=0){Field=field;Min=min;Max=max;Bias=bias;}
        public static double Unit(double u,int bias)=>bias==1?(Math.Pow(100,u)-1)/99:bias==2?1-(Math.Pow(100,1-u)-1)/99:u;
        public static double Round(double value,bool integer)
        {
            if(value==0||double.IsNaN(value)||double.IsInfinity(value))return value;
            double step=Math.Pow(10,Math.Floor(Math.Log10(Math.Abs(value)))-(integer?2:1));if(integer)step=Math.Max(1,step);
            return Math.Round(value/step,0,MidpointRounding.AwayFromZero)*step;
        }
        public double Sample(int seed,bool integer)
        {
            double unit=Unit(new Rng(seed,"setup-range-"+Field).Unit(),Bias),value=Min+(Max-Min+(integer?1:0))*unit;
            return Math.Max(Min,Math.Min(Max,Round(integer?Math.Floor(value):value,integer)));
        }
    }
    /// <summary>Shared play-first catalogue. Windows resolve to normal, serializable recipe values.</summary>
    public static class WorldPresets
    {
        public static readonly string[] Names={"Continents plus","Continents","Earthlike","Pangeae","Earth","Terra","Archipelago","Fractal","Two continents","Large continents","Small continents","Large Islands","Shuffle","Inland Sea","Crater Sea","Donut","Aborea","Boreal","Ice age","Lakes","Desert world"};
        public static PresetWindow[] Windows(string name)
        {
            var list=new List<PresetWindow>();
            void W(string f,double a,double b,int bias=0)=>list.Add(new PresetWindow(f,a,b,bias));
            void Continents(double a,double b){W(nameof(Recipe.ContinentCount),a,b);W(nameof(Recipe.LandFraction),.28,.42);W(nameof(Recipe.MicroplateCount),80,160);}
            switch(name)
            {
                case "Continents":Continents(2,8);break;
                case "Earthlike":W(nameof(Recipe.ContinentCount),5,7);W(nameof(Recipe.LandFraction),.25,.35);W(nameof(Recipe.MicroplateCount),80,160);break;
                case "Continents plus":case "Ice age":Continents(4,10);W(nameof(Recipe.Interconnectedness),10,50);break;
                case "Pangeae":W(nameof(Recipe.LandFraction),.3,.5);W(nameof(Recipe.MicroplateCount),80,150);break;
                case "Two continents":W(nameof(Recipe.LandFraction),.3,.45);W(nameof(Recipe.MicroplateCount),140,240);break;
                case "Large continents":Continents(2,3);list.RemoveAll(x=>x.Field==nameof(Recipe.LandFraction));W(nameof(Recipe.LandFraction),.3,.5);break;
                case "Small continents":Continents(8,16);list.RemoveAll(x=>x.Field==nameof(Recipe.MicroplateCount));W(nameof(Recipe.MicroplateCount),200,320);W(nameof(Recipe.ContinentVariation),.1,.35);break;
                case "Archipelago":W(nameof(Recipe.MicroplateCount),350,500);W(nameof(Recipe.ContinentCount),20,50);W(nameof(Recipe.LandFraction),.23,.35,1);W(nameof(Recipe.Interconnectedness),1,3);break;
                case "Large Islands":W(nameof(Recipe.MicroplateCount),180,300);W(nameof(Recipe.ContinentCount),10,20);W(nameof(Recipe.LandFraction),.28,.38);break;
                case "Fractal":W(nameof(Recipe.MicroplateCount),220,400);W(nameof(Recipe.ContinentCount),8,22);W(nameof(Recipe.LandFraction),.25,.42);W(nameof(Recipe.Interconnectedness),15,40);break;
                case "Aborea":case "Boreal":Continents(3,7);W(nameof(Recipe.PrecipitationOffset),.7,1);break;
                case "Shuffle":
                    W(nameof(Recipe.MicroplateCount),60,400);W(nameof(Recipe.ContinentCount),1,50,1);W(nameof(Recipe.Interconnectedness),1,10);
                    W(nameof(Recipe.LandFraction),.15,.8,1);W(nameof(Recipe.ContinentVariation),.05,.9);W(nameof(Recipe.Relief),.15,.5);
                    W(nameof(Recipe.TerrainWarp),.15,1);W(nameof(Recipe.Smoothing),.03,.35,1);W(nameof(Recipe.Glacial),.1,.9);
                    W(nameof(Recipe.Hydraulic),.2,.8);W(nameof(Recipe.Thermal),.05,.4,1);W(nameof(Recipe.RidgePreservation),.2,.8);
                    W(nameof(Recipe.MountainStrengthPercent),50,150);W(nameof(Recipe.LakeAmountPercent),50,150);
                    W(nameof(Recipe.TemperatureOffset),-8,6);W(nameof(Recipe.PrecipitationOffset),-.4,.6);break;
            }
            return list.ToArray();
        }
        public static void Apply(string name,Recipe r)
        {
            switch(name)
            {
                case "Earthlike":r.ContinentVariation=.95;r.Interconnectedness=50;break;
                case "Donut":r.LandShape="Donut-v1";r.ContinentCount=1;r.LandFraction=.32;break;
                case "Crater Sea":r.LandShape="Crater-v1";r.ContinentCount=2;r.LandFraction=.8;break;
                case "Inland Sea":r.LandShape="Inland-v1";r.ContinentCount=1;r.LandFraction=.84;break;
                case "Terra":r.LandShape="Terra-v1";r.ContinentCount=4;r.MicroplateCount=160;break;
                case "Lakes":r.LandShape="AllLand-v1";r.LandFraction=1;r.ContinentCount=1;r.InteriorRainMm=850;r.PrecipitationOffset=.6;r.LakeAmountPercent=20;break;
                case "Desert world":r.LandShape="AllLand-v1";r.LandFraction=1;r.ContinentCount=1;r.TemperatureOffset=8;r.PrecipitationOffset=-.9;r.LakeAmountPercent=50;break;
                case "Pangeae":r.ContinentCount=1;r.ContinentVariation=0;break;
                case "Two continents":r.ContinentCount=2;r.ContinentVariation=0;break;
                case "Large Islands":r.ContinentVariation=0;break;
                case "Archipelago":r.ContinentVariation=.55;r.Relief=.32;r.TerrainWarp=.85;break;
                case "Fractal":r.TerrainWarp=.95;r.ContinentVariation=.65;break;
                case "Aborea":r.TemperatureOffset=-2;r.MountainStrengthPercent=70;r.InteriorRainMm=900;break;
                case "Boreal":r.TemperatureOffset=-12;r.Glacial=.8;r.MountainStrengthPercent=70;r.InteriorRainMm=500;break;
                case "Ice age":r.TemperatureOffset=-7;r.Glacial=.95;break;
            }
            foreach(var window in Windows(name))
            {
                var field=typeof(Recipe).GetField(window.Field);bool integer=field.FieldType==typeof(int);double value=window.Sample(r.Seed,integer);
                field.SetValue(r,integer?(object)(int)value:value);
            }
        }
    }
}
