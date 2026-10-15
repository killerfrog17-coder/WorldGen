using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    public static class HistoricalCropCalendarEffects
    {
        public static int GrowingWindowWarmth(int spring,int summer,int autumn,int winter,int group)
        {
            int[] temperatures={spring,summer,autumn,winter};double best=-1;int selected=0;
            double optimum=group==6?180:group==7?320:group==1||group==2||group==3?270:220;
            for(int k=0;k<4;k++)
            {
                int average=(temperatures[k]+temperatures[(k+1)%4])/2;
                double fit=Math.Max(0,1-Math.Abs(average-optimum)/180.0);
                if(fit>best){best=fit;selected=average;}
            }
            return selected;
        }
    }
    public sealed partial class Prehistory
    {
        readonly Dictionary<int,HabitatCell> communityAgronomicHabitats=new Dictionary<int,HabitatCell>();
        readonly Queue<int> communityAgronomicOrder=new Queue<int>();
        HabitatCell CommunityAgronomicHabitat(PrehistoryState s,int cell,int group)
        {
            int key=cell*8+group;if(communityAgronomicHabitats.TryGetValue(key,out var cached))return cached;
            var h=CropHabitat(s,cell);var climate=World.Climate;
            int warm=HistoricalCropCalendarEffects.GrowingWindowWarmth(climate.NorthernSpring.TemperatureTenthsC[cell],climate.NorthernSummer.TemperatureTenthsC[cell],climate.NorthernAutumn.TemperatureTenthsC[cell],climate.NorthernWinter.TemperatureTenthsC[cell],group);
            // A crop's growing season is not necessarily the hottest part of
            // the year. In warm dry places winter cereals are possible; a short
            // cool continental summer does not count as year-round warm yield.
            var result=new HabitatCell{PetMm=h.PetMm,AridityMilli=h.AridityMilli,WaterDeficitMm=h.WaterDeficitMm,GrowingDays=h.GrowingDays,ColdTenthsC=h.ColdTenthsC,WarmTenthsC=warm,MeanTenthsC=h.MeanTenthsC,WarmSeasonDryPermille=h.WarmSeasonDryPermille,
                WaterTableCm=h.WaterTableCm,PermafrostPermille=h.PermafrostPermille,FloodFractionPermille=h.FloodFractionPermille,FloodDaysPerYear=h.FloodDaysPerYear,FloodPulsesPerYear=h.FloodPulsesPerYear,
                SalinityMilliPpt=h.SalinityMilliPpt,SoilFertilityPermille=h.SoilFertilityPermille,FirePotentialPermille=h.FirePotentialPermille,ProductivityGCM2Year=h.ProductivityGCM2Year,ReliefM=h.ReliefM,Soil=h.Soil,Flooding=h.Flooding,Biome=h.Biome,Habitat=h.Habitat};
            if(communityAgronomicHabitats.Count>=16384)communityAgronomicHabitats.Remove(communityAgronomicOrder.Dequeue());
            communityAgronomicHabitats[key]=result;communityAgronomicOrder.Enqueue(key);return result;
        }
        double CommunityTransferFit(PrehistoryState s,int cell,HistoricalCultivar cultivar,bool managed=false)
        {
            if(cultivar==null)return 0;
            int group=PassageHistory(s)&&cultivar.CropId==10?6:PassageHistory(s)&&cultivar.CropId==12?7:HistoricalCropCatalog.Crops[cultivar.CropId].Group;
            var habitat=CommunityHistory(s)?CommunityAgronomicHabitat(s,cell,group):CropHabitat(s,cell);
            double fit=HistoricalCropCatalog.TransferFit(habitat,cultivar,CropLatitude(cell),riverSites[cell],managed);
            return PassageHistory(s)?D.Q(fit*HistoricalCropEcology.ReproductiveFit(habitat,cultivar),1000000):fit;
        }
        double CommunityWildCropFit(PrehistoryState s,int cell,int group)
            =>HistoricalCrops.Suitability(CommunityAgronomicHabitat(s,cell,group),group,riverSites[cell]);
        int ActiveCommunityCropYears(PrehistoryState s,int culture,int group,int elapsed,int urbanSite)
        {
            var crops=s.History.Agriculture[culture];var cultivar=crops.Cultivars[group];
            double fit=Math.Max(CommunityTransferFit(s,crops.PracticeCell[group],cultivar),CommunityTransferFit(s,urbanSite,cultivar));
            double effort=D.Clamp(s.Society.Cultures[culture].Farming/300.0)*D.Clamp((fit-.12)/.45);
            double years=elapsed*effort;int whole=(int)years;
            if(HistoryRoll("community-active-crop-"+group,culture,s.Years)<years-whole)whole++;
            return whole;
        }
        int CommunityNativeCropMask(PrehistoryState s,int cell)
        {
            int mask=0,province=World.Ecology.Province[cell];
            var climate=World.Climate;
            double low=Math.Min(Math.Min(climate.NorthernSpring.PrecipitationMm[cell],climate.NorthernSummer.PrecipitationMm[cell]),Math.Min(climate.NorthernAutumn.PrecipitationMm[cell],climate.NorthernWinter.PrecipitationMm[cell]));
            double high=Math.Max(Math.Max(climate.NorthernSpring.PrecipitationMm[cell],climate.NorthernSummer.PrecipitationMm[cell]),Math.Max(climate.NorthernAutumn.PrecipitationMm[cell],climate.NorthernWinter.PrecipitationMm[cell]));
            double season=(high-low)/Math.Max(1,high);
            for(int k=0;k<6;k++){double fit=CommunityWildCropFit(s,cell,k),occurrence=Rng.Value(World.Recipe.Seed,"crop-wild-province",unchecked(province*31+k));double niche=PassageHistory(s)?HistoricalCropEcology.WildAnnualOpportunity(CropHabitat(s,cell),season,k):1;if(fit>.28&&occurrence<(.35+.55*fit)*niche)mask|=1<<k;}
            return mask;
        }
        double CommunityCropLandscape(PrehistoryState s,int culture,int cell,bool storage)
        {
            var h=CropHabitat(s,cell);var crops=s.History.Agriculture[culture];var hc=s.History.Cultures[culture];
            bool managed=HistoricalTechnologies.Has(hc,HistoricalTechnology.CropRotation);double best=0;
            double fraction=Math.Min(1,Math.Max(.08,D.Clamp((h.AridityMilli-100)/550.0)+riverAccess[cell]));
            var climate=World.Climate;double low=Math.Min(Math.Min(climate.NorthernSpring.PrecipitationMm[cell],climate.NorthernSummer.PrecipitationMm[cell]),Math.Min(climate.NorthernAutumn.PrecipitationMm[cell],climate.NorthernWinter.PrecipitationMm[cell]));
            double high=Math.Max(Math.Max(climate.NorthernSpring.PrecipitationMm[cell],climate.NorthernSummer.PrecipitationMm[cell]),Math.Max(climate.NorthernAutumn.PrecipitationMm[cell],climate.NorthernWinter.PrecipitationMm[cell]));
            double rainfallSeason=(high-low)/Math.Max(1,high);
            for(int k=0;k<6;k++)if(crops.AdoptedYear[k]>0)
            {
                var cultivar=crops.Cultivars?[k];var agronomic=CommunityAgronomicHabitat(s,cell,PassageHistory(s)&&cultivar?.CropId==10?6:PassageHistory(s)&&cultivar?.CropId==12?7:k);
                double fit=cultivar==null?CommunityWildCropFit(s,cell,k):CommunityTransferFit(s,cell,cultivar,managed);
                double area=HistoricalLandscapeEffects.CropAreaYield(agronomic,riverAccess[cell],rainfallSeason)*HistoricalDevelopmentEffects.GrowingSeason(agronomic);
                double quality=D.Clamp(area/fraction);
                if(HistoricalLandscapeEffects.EverWet(h))
                {
                    double husbandry=D.Clamp((crops.HusbandryYears?[k]??0)/15000.0);
                    quality=Math.Max(quality,(managed?.35:.30*husbandry)*HistoricalCropCatalog.LeachingPenalty(h,true));
                }
                double preservation=storage?HistoricalHusbandryEffects.StoragePermille(k,cultivar,HistoricalTechnologies.Has(hc,HistoricalTechnology.Pottery))/1000.0*(.65+.35*rainfallSeason):1;
                best=Math.Max(best,quality*fit*preservation);
            }
            return D.Q(D.Clamp(best),1000000);
        }
    }
}
