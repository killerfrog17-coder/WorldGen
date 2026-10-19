using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        static bool GroundHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=38;
        readonly Dictionary<int,HabitatCell> groundCropHabitats=new Dictionary<int,HabitatCell>();
        HabitatCell GroundCropHabitat(int cell,HabitatCell h)
        {
            if(groundCropHabitats.TryGetValue(cell,out var result))return result;
            var g=GroundEnvironment.For(World).Cell(cell);
            result=new HabitatCell{PetMm=h.PetMm,AridityMilli=h.AridityMilli,WaterDeficitMm=h.WaterDeficitMm,GrowingDays=h.GrowingDays,ColdTenthsC=h.ColdTenthsC,WarmTenthsC=h.WarmTenthsC,MeanTenthsC=h.MeanTenthsC,WarmSeasonDryPermille=h.WarmSeasonDryPermille,
                WaterTableCm=h.WaterTableCm,PermafrostPermille=h.PermafrostPermille,FloodFractionPermille=h.FloodFractionPermille,FloodDaysPerYear=h.FloodDaysPerYear,FloodPulsesPerYear=h.FloodPulsesPerYear,
                SalinityMilliPpt=h.SalinityMilliPpt,SoilFertilityPermille=(int)Math.Round(g.Fertility*1000),FirePotentialPermille=h.FirePotentialPermille,ProductivityGCM2Year=h.ProductivityGCM2Year,ReliefM=h.ReliefM,Soil=h.Soil,Flooding=h.Flooding,Biome=h.Biome,Habitat=h.Habitat};
            if(groundCropHabitats.Count<65536)groundCropHabitats.Add(cell,result);return result;
        }
        public double GroundCropConstraint(PrehistoryState s,int cell,int crop)
        {
            if(!GroundHistory(s)||World.Hydrology.OceanConnected[cell])return 1; // Preserve existing fractional-island land proxy.
            var g=GroundEnvironment.For(World).Cell(cell);
            // Nutrients and salt already enter CropHabitat's existing agronomy.
            // Here add only depth/texture/root-zone storage/acidity/aeration.
            double physical=g.FarmFit(crop)/Math.Max(.001,(.2+.8*g.Fertility)*(1-.95*g.Salinity));
            return D.Q(.2+.8*D.Clamp(physical),1000000);
        }
    }
}
