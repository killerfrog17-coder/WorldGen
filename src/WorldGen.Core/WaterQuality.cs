using System;

namespace WorldGen.Core
{
    public readonly struct WaterQuality
    {
        public readonly LocalWaterKind Kind;public readonly double SalinityPpt,Contamination,Turbidity;
        public WaterQuality(LocalWaterKind kind,double salinity,double contamination,double turbidity)
        {Kind=kind;SalinityPpt=Math.Max(0,salinity);Contamination=D.Clamp(contamination);Turbidity=D.Clamp(turbidity);}
        public bool Fresh=>SalinityPpt<.5;
        public string Name=>SalinityPpt>=30?"Salt water":!Fresh?"Brackish / saline water":Kind==LocalWaterKind.River?"Fresh river water":"Fresh lake water";
        public string Description=>Fresh?"Untreated; safety is uncertain":SalinityPpt>=7?"Too salty to hydrate you":"Salty, untreated water";
        public static WaterQuality From(LocalTerrainSample sample,HabitatCell habitat=null,double lakeSalinity=double.NaN)
        {
            double salinity=sample.WaterKind==LocalWaterKind.Ocean?35:.08;
            if(sample.WaterKind==LocalWaterKind.Lake)salinity=double.IsFinite(lakeSalinity)?lakeSalinity:(habitat?.SalinityMilliPpt??80)*.001;
            if(sample.WaterKind==LocalWaterKind.River&&habitat?.Flooding==FloodRegime.Tidal&&sample.WaterSurfaceM<2)
                salinity=Math.Max(salinity,habitat.SalinityMilliPpt*.001*D.Clamp(1-Math.Max(0,sample.WaterSurfaceM)/2));
            double mud=D.Clamp(sample.Sediment*.75+sample.WaterTurbulence*.1+(habitat?.FloodFractionPermille??0)*.00025);
            double risk=sample.WaterKind==LocalWaterKind.River?.015+.12*mud+.035/(1+Math.Max(0,sample.WaterSpeedMPerS)*3):.12+.18*mud;
            return new WaterQuality(sample.WaterKind,salinity,risk,mud);
        }
    }
    public static class WaterDrinking
    {
        public static void Drink(PlayerSurvival state,WaterQuality water,double litres,double chance,GameMode mode)
        {
            if(state==null||state.Incapacitated||water.Kind==LocalWaterKind.None||!double.IsFinite(litres)||litres<=0)return;
            if(mode!=GameMode.Survival)return;
            litres=Math.Min(1,litres);
            double net=litres*Math.Max(-1.8,1-Math.Pow(water.SalinityPpt/7,1.2));
            state.Thirst=Math.Max(0,Math.Min(100,state.Thirst-net*100/3));
            double dose=1-Math.Pow(1-water.Contamination,litres*.4);
            if(chance<dose)
            {
                state.WaterSicknessDelaySeconds=state.WaterSicknessSeconds>0?state.WaterSicknessDelaySeconds:8*3600+water.Contamination*12*3600;
                state.WaterSicknessSeconds=Math.Max(state.WaterSicknessSeconds,12*3600+water.Contamination*24*3600);
                state.WaterSicknessSeverity=Math.Max(state.WaterSicknessSeverity,.25+water.Contamination*.75);
            }
        }
        public static void AdvanceIllness(PlayerSurvival state,double seconds)
        {
            if(state.WaterSicknessSeconds<=0||seconds<=0)return;
            double active=Math.Max(0,seconds-state.WaterSicknessDelaySeconds);state.WaterSicknessDelaySeconds=Math.Max(0,state.WaterSicknessDelaySeconds-seconds);
            active=Math.Min(active,state.WaterSicknessSeconds);if(active<=0)return;
            state.WaterSicknessSeconds-=active;double hours=active/3600*state.WaterSicknessSeverity;
            state.Thirst=Math.Min(100,state.Thirst+hours*8);state.Energy=Math.Max(0,state.Energy-hours*3);
            if(state.WaterSicknessSeconds<=0)state.WaterSicknessSeverity=0;
        }
    }
}
