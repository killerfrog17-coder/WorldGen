using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    public static class HistoricalIslandCropEnvironment
    {
        // A fractional island shares its host cell's air climate, not its
        // seawater soil. This is a history-only land proxy; accepted terrain,
        // habitat records and island areas are never edited.
        public static HabitatCell FromOcean(HabitatCell air,long areaM2,int peakM)
        {
            double size=D.Clamp(Math.Sqrt(Math.Max(0,areaM2)/1e6)/3.0);
            bool cay=peakM<15;
            return new HabitatCell{
                WarmTenthsC=air.WarmTenthsC,ColdTenthsC=air.ColdTenthsC,MeanTenthsC=air.MeanTenthsC,
                GrowingDays=air.GrowingDays,AridityMilli=air.AridityMilli,PetMm=air.PetMm,
                WarmSeasonDryPermille=air.WarmSeasonDryPermille,PermafrostPermille=air.PermafrostPermille,
                Soil=cay?SoilKind.Sand:SoilKind.Loam,SoilFertilityPermille=(int)Math.Round((cay?250:450)*(.5+.5*size)),
                SalinityMilliPpt=(int)Math.Round((cay?600:100)*(1-.5*size)),ReliefM=(int)Math.Round(Math.Max(0,peakM)*.35),
                Biome=air.WarmTenthsC<100?Biome.Tundra:air.AridityMilli<250?Biome.Desert:Biome.Grassland,
                Habitat=Habitat.Coastal|Habitat.Open,Flooding=FloodRegime.None
            };
        }
    }
    public sealed partial class Prehistory
    {
        readonly Dictionary<int,HabitatCell> islandCropHabitat=new Dictionary<int,HabitatCell>();
        HabitatCell CropHabitat(PrehistoryState s,int cell)
        {
            if(!ModernHistory(s)||!World.Hydrology.OceanConnected[cell])return GroundHistory(s)?GroundCropHabitat(cell,World.Ecology.Habitat[cell]):World.Ecology.Habitat[cell];
            if(islandCropHabitat.TryGetValue(cell,out var habitat))return habitat;
            var group=SubcellGeography.Find(World,cell);if(group==null)return World.Ecology.Habitat[cell];
            SmallIsland largest=null;for(int k=0;k<group.IslandCount;k++){var island=World.Subcells.Islands[group.FirstIsland+k];if(largest==null||island.AreaM2>largest.AreaM2)largest=island;}
            habitat=HistoricalIslandCropEnvironment.FromOcean(World.Ecology.Habitat[cell],largest.AreaM2,largest.PeakM);islandCropHabitat.Add(cell,habitat);return habitat;
        }
        double CropSuitability(PrehistoryState s,int cell,int crop)
        {
            if(GroundHistory(s)){PrepareHistoricalEnvironment();return D.Q(HistoricalCrops.Suitability(CropHabitat(s,cell),crop,riverSites[cell])*GroundCropConstraint(s,cell,crop),1000000);}
            return ModernHistory(s)&&World.Hydrology.OceanConnected[cell]?HistoricalCrops.Suitability(CropHabitat(s,cell),crop):CropSuitability(cell,crop);
        }
    }
}
