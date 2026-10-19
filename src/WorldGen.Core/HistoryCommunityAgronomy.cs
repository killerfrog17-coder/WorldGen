using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    public static class HistoricalCropCalendarEffects
    {
        public static int MaturityWindowWarmth(int spring,int summer,int autumn,int winter,int group,int maturityDays)
        {
            if(maturityDays<1||maturityDays>365)throw new ArgumentOutOfRangeException(nameof(maturityDays));
            // Only the four accepted seasonal observations are available. Treat
            // each as three equal months, not invented daily climate. At most
            // twelve contiguous windows are evaluated, including year wrap.
            int optimum=group==6?180:group==7?320:group==1||group==2||group==3?270:220;
            int selected=0,best=int.MaxValue,units=maturityDays*12;
            for(int start=0;start<12;start++)
            {
                long sum=0;int remaining=units;
                for(int offset=0;remaining>0;offset++)
                {int quarter=((start+offset)%12)/3,take=Math.Min(365,remaining);sum+=(long)take*(quarter==0?spring:quarter==1?summer:quarter==2?autumn:winter);remaining-=take;}
                int average=(int)(sum/units),distance=(int)Math.Min(int.MaxValue,Math.Abs((long)average-optimum));
                if(distance<best){best=distance;selected=average;}
            }
            return selected;
        }
        public static int GrowingWindowWarmth(int spring,int summer,int autumn,int winter,int group)
        {
            double best=-1;int selected=0;
            double optimum=group==6?180:group==7?320:group==1||group==2||group==3?270:220;
            for(int k=0;k<4;k++)
            {
                int current=k==0?spring:k==1?summer:k==2?autumn:winter;
                int next=k==0?summer:k==1?autumn:k==2?winter:spring;
                int average=(current+next)/2;
                double fit=Math.Max(0,1-Math.Abs(average-optimum)/180.0);
                if(fit>best){best=fit;selected=average;}
            }
            return selected;
        }
    }
    public sealed partial class Prehistory
    {
        readonly Dictionary<long,HabitatCell> communityAgronomicHabitats=new Dictionary<long,HabitatCell>();
        readonly Queue<long> communityAgronomicOrder=new Queue<long>();
        HabitatCell CommunityAgronomicHabitat(PrehistoryState s,int cell,int group)=>CommunityCalendarHabitat(s,cell,group,0);
        HabitatCell CommunityCalendarHabitat(PrehistoryState s,int cell,int group,int maturityDays)
        {
            long key=(long)cell*4096+group*512+maturityDays+(GroundHistory(s)?1L<<62:0);if(communityAgronomicHabitats.TryGetValue(key,out var cached)){agronomicHabitatHits++;return cached;}
            agronomicHabitatMisses++;
            var h=CropHabitat(s,cell);var climate=World.Climate;
            int spring=climate.NorthernSpring.TemperatureTenthsC[cell],summer=climate.NorthernSummer.TemperatureTenthsC[cell],autumn=climate.NorthernAutumn.TemperatureTenthsC[cell],winter=climate.NorthernWinter.TemperatureTenthsC[cell];
            int warm=maturityDays==0?HistoricalCropCalendarEffects.GrowingWindowWarmth(spring,summer,autumn,winter,group):HistoricalCropCalendarEffects.MaturityWindowWarmth(spring,summer,autumn,winter,group,maturityDays);
            // A crop's growing season is not necessarily the hottest part of
            // the year. In warm dry places winter cereals are possible; a short
            // cool continental summer does not count as year-round warm yield.
            var result=new HabitatCell{PetMm=h.PetMm,AridityMilli=h.AridityMilli,WaterDeficitMm=h.WaterDeficitMm,GrowingDays=h.GrowingDays,ColdTenthsC=h.ColdTenthsC,WarmTenthsC=warm,MeanTenthsC=h.MeanTenthsC,WarmSeasonDryPermille=h.WarmSeasonDryPermille,
                WaterTableCm=h.WaterTableCm,PermafrostPermille=h.PermafrostPermille,FloodFractionPermille=h.FloodFractionPermille,FloodDaysPerYear=h.FloodDaysPerYear,FloodPulsesPerYear=h.FloodPulsesPerYear,
                SalinityMilliPpt=h.SalinityMilliPpt,SoilFertilityPermille=h.SoilFertilityPermille,FirePotentialPermille=h.FirePotentialPermille,ProductivityGCM2Year=h.ProductivityGCM2Year,ReliefM=h.ReliefM,Soil=h.Soil,Flooding=h.Flooding,Biome=h.Biome,Habitat=h.Habitat};
            // Immutable world/cell/group fits are shared by many annual town
            // actors. A late 200k-cell world can exceed16k active crop niches;
            // Use a bounded256k niche working set for large late worlds rather
            // than continually reallocating the same immutable soil records.
            if(communityAgronomicHabitats.Count>=262144)communityAgronomicHabitats.Remove(communityAgronomicOrder.Dequeue());
            communityAgronomicHabitats[key]=result;communityAgronomicOrder.Enqueue(key);return result;
        }
        double CommunityTransferFit(PrehistoryState s,int cell,HistoricalCultivar cultivar,bool managed=false)
        {
            if(cultivar==null)return 0;
            return UseAgronomicFitCache&&LivingWorldHistory(s)?CachedCommunityTransferFit(s,cell,cultivar,managed):LiteralCommunityTransferFit(s,cell,cultivar,managed);
        }
        double LiteralCommunityTransferFit(PrehistoryState s,int cell,HistoricalCultivar cultivar,bool managed)
        {
            int group=PassageHistory(s)&&cultivar.CropId==10?6:PassageHistory(s)&&cultivar.CropId==12?7:HistoricalCropCatalog.Crops[cultivar.CropId].Group;
            // Orchard flowering/chilling and perennial survival are not an
            // annual field-maturation window. Keep their preceding calendar.
            int duration=s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=22&&HistoricalCropCatalog.Crops[cultivar.CropId].Group!=4?Math.Min(365,cultivar.MaturityDays):0;
            var habitat=CommunityHistory(s)?CommunityCalendarHabitat(s,cell,group,duration):CropHabitat(s,cell);
            double fit=SpeciesCropNicheHistory(s)?HistoricalCropSpeciesNiches.TransferFit(habitat,cultivar,CropLatitude(cell),riverSites[cell],managed):HistoricalCropCatalog.TransferFit(habitat,cultivar,CropLatitude(cell),riverSites[cell],managed);
            double result=PassageHistory(s)?D.Q(fit*HistoricalCropEcology.ReproductiveFit(habitat,cultivar)*(EnvironmentalHistory(s)?EnvironmentalCropTransfer(s,cell,cultivar):1),1000000):fit;
            return GroundHistory(s)?D.Q(result*GroundCropConstraint(s,cell,HistoricalCropCatalog.Crops[cultivar.CropId].Group),1000000):result;
        }
        double CommunityWildCropFit(PrehistoryState s,int cell,int group)
            =>GroundHistory(s)?D.Q(HistoricalCrops.Suitability(CommunityAgronomicHabitat(s,cell,group),group,riverSites[cell])*GroundCropConstraint(s,cell,group),1000000):HistoricalCrops.Suitability(CommunityAgronomicHabitat(s,cell,group),group,riverSites[cell]);
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
        double CommunityCropLandscape(PrehistoryState s,int culture,int cell,bool storage)=>CommunitySeededCropLandscape(s,culture,cell,storage,null,false);
        double CommunitySeededCropLandscape(PrehistoryState s,int culture,int cell,bool storage,HistoricalTownSeeds seedContext,bool carried)
        {
            var crops=s.History.Agriculture[culture];var hc=s.History.Cultures[culture];
            var localSeeds=LocalSeedlotHistory(s)?(carried?seedContext:LocalSeedHolder(s,culture,cell)):null;
            bool managed=HistoricalTechnologies.Has(hc,HistoricalTechnology.CropRotation),pottery=HistoricalTechnologies.Has(hc,HistoricalTechnology.Pottery);
            if(TrySeedLandscape(s,localSeeds,cell,managed,pottery,carried,storage,out double memo))return memo;
            var h=CropHabitat(s,cell);double best=0;
            double fraction=Math.Min(1,Math.Max(.08,D.Clamp((h.AridityMilli-100)/550.0)+riverAccess[cell]));
            var climate=World.Climate;double low=Math.Min(Math.Min(climate.NorthernSpring.PrecipitationMm[cell],climate.NorthernSummer.PrecipitationMm[cell]),Math.Min(climate.NorthernAutumn.PrecipitationMm[cell],climate.NorthernWinter.PrecipitationMm[cell]));
            double high=Math.Max(Math.Max(climate.NorthernSpring.PrecipitationMm[cell],climate.NorthernSummer.PrecipitationMm[cell]),Math.Max(climate.NorthernAutumn.PrecipitationMm[cell],climate.NorthernWinter.PrecipitationMm[cell]));
            double rainfallSeason=(high-low)/Math.Max(1,high);
            for(int k=0;k<6;k++)if(crops.AdoptedYear[k]>0||localSeeds?[k]!=null)
            {
                var cultivar=LocalSeedlotHistory(s)?LocalSeedProfile(s,culture,k,localSeeds):crops.Cultivars?[k];if(localSeeds!=null&&cultivar==null)continue;
                int group=PassageHistory(s)&&cultivar?.CropId==10?6:PassageHistory(s)&&cultivar?.CropId==12?7:k;var agronomic=CommunityAgronomicHabitat(s,cell,group);
                double fit=cultivar==null?CommunityWildCropFit(s,cell,k):CommunityTransferFit(s,cell,cultivar,managed);
                double area=CommunityAreaYield(s,cell,group,agronomic,rainfallSeason);
                double quality=D.Clamp(area/fraction);
                if(HistoricalLandscapeEffects.EverWet(h))
                {
                    double husbandry=D.Clamp((LocalSeedlotHistory(s)?LocalSeedPracticeAt(s,culture,k,localSeeds,cell,carried):crops.HusbandryYears?[k]??0)/15000.0);
                    quality=Math.Max(quality,(managed?.35:(EnvironmentalHistory(s)?.20:.30)*husbandry)*HistoricalCropCatalog.LeachingPenalty(h,EnvironmentalHistory(s)?managed:true));
                }
                double preservation=storage?HistoricalHusbandryEffects.StoragePermille(k,cultivar,pottery)/1000.0*(.65+.35*rainfallSeason):1;
                best=Math.Max(best,quality*fit*preservation);
            }
            double result=D.Q(D.Clamp(best),1000000);
            RememberSeedLandscape(s,localSeeds,cell,managed,pottery,carried,storage,result);
            return result;
        }
    }
}
