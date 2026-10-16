using System;
namespace WorldGen.Core
{
    public static class HistoricalHusbandryEffects
    {
        // Plot-management priors, not a claim that an entire forest cell is
        // cleared or that charcoal alone creates fertile Amazonian dark earth.
        public static double ClearingMultiplier(HabitatCell habitat,int practicedYears)
            =>D.Q(1+(habitat.Biome==Biome.TropicalRainforest?.8:(habitat.Habitat&Habitat.Forest)!=0?.35:0)*D.Clamp(practicedYears/4000.0),1000000);
        public static double LeachingPenalty(HabitatCell habitat,int practicedYears,bool soilManagement)
        {
            double first=HistoricalCropCatalog.LeachingPenalty(habitat,false),last=HistoricalCropCatalog.LeachingPenalty(habitat,true);
            return D.Q(first+(last-first)*(soilManagement?1:D.Clamp(practicedYears/4000.0)),1000000);
        }
        public static int StoragePermille(int group,HistoricalCultivar cultivar,bool pottery)
        {
            int basic=HistoricalCrops.Storage[group];
            // Drying/processing can make roots a stored staple. The lineage
            // carries food-processing experience, not universally fertile land.
            return basic+(group==3&&pottery?(int)Math.Round(300*D.Clamp((cultivar?.SelectedYears??0)/3000.0)):0);
        }
    }
    public sealed partial class Prehistory
    {
        int LocalHusbandryYears(PrehistoryState s,int culture,int cell,int crop)
        {
            var a=s.History.Agriculture[culture];int years=a.HusbandryYears?[crop]??0;if(years==0)return 0;
            var learned=CropHabitat(s,a.PracticeCell[crop]);var destination=CropHabitat(s,cell);
            double match=(learned.Soil==destination.Soil?1:.25)*((learned.Habitat&Habitat.Forest)==(destination.Habitat&Habitat.Forest)?1:.5);
            return (int)Math.Round(years*match);
        }
        double CropSoilPenalty(PrehistoryState s,int culture,int cell,int crop)
            =>HistoricalHusbandryEffects.LeachingPenalty(CropHabitat(s,cell),LocalHusbandryYears(s,culture,cell,crop),HistoricalTechnologies.Has(s.History.Cultures[culture],HistoricalTechnology.CropRotation));
        double ManagedLandMultiplier(PrehistoryState s,int culture,int cell)
        {
            if(!ModernHistory(s))return 1;var habitat=CropHabitat(s,cell);bool everWet=RealismHistory(s)&&HistoricalLandscapeEffects.EverWet(habitat);if((habitat.Habitat&Habitat.Forest)==0&&!everWet)return 1;
            var a=s.History.Agriculture[culture];int years=0;
            for(int k=0;k<6;k++)if(a.AdoptedYear[k]>0&&LocalCultivarFit(s,culture,cell,k)>=.2)years=Math.Max(years,LocalHusbandryYears(s,culture,cell,k));
            return everWet?D.Q(1+5*D.Clamp(years/16000.0),1000000):HistoricalHusbandryEffects.ClearingMultiplier(habitat,years);
        }
        int CropStorage(PrehistoryState s,int culture,int crop)
            =>ModernHistory(s)?HistoricalHusbandryEffects.StoragePermille(crop,s.History.Agriculture[culture].Cultivars[crop],HistoricalTechnologies.Has(s.History.Cultures[culture],HistoricalTechnology.Pottery)):HistoricalCrops.Storage[crop];
    }
}
