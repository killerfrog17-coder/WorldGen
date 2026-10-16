using System;
using System.Linq;
namespace WorldGen.Core
{
    public static class HistoricalLandscapeEffects
    {
        public static bool EverWet(HabitatCell h)=>h.ColdTenthsC>=180&&h.AridityMilli>=1100&&(h.WaterDeficitMm<200||h.Soil==SoilKind.Leached||h.Biome==Biome.TropicalRainforest);
        public static double CropAreaYield(HabitatCell h,double freshwater,double rainSeasonality)
        {
            double heat=D.Clamp((h.WarmTenthsC-80)/120.0)*D.Clamp(h.GrowingDays/180.0)*(1-h.PermafrostPermille/1000.0);
            double physical=heat*D.Clamp(1-h.ReliefM/1800.0)*D.Clamp(1-h.SalinityMilliPpt/3000.0);
            // Wetland/floodplain labels can conceal the surrounding forest.
            bool evergreen=EverWet(h);
            double clearing=evergreen?.20:(h.Habitat&Habitat.Forest)!=0?.70:1;
            double soil=D.Clamp(h.SoilFertilityPermille/700.0);if(evergreen)soil=Math.Min(soil,.35+.4*D.Clamp(freshwater));
            double rainfed=D.Clamp((h.AridityMilli-100)/550.0)*soil*clearing;
            double alluvial=.9*(.55+.45*soil)*(evergreen?.22:1);
            return D.Q(physical*(rainfed*(1-D.Clamp(freshwater))+alluvial*D.Clamp(freshwater)),1000000);
        }
        public static double LivelihoodContinuity(HabitatCell origin,HabitatCell local,HabitatCell target)
        {
            double Contrast(HabitatCell a,HabitatCell b)
            {
                double moisture=Math.Abs(Math.Log((a.AridityMilli+100.0)/(b.AridityMilli+100.0)));
                double cover=(a.Habitat&Habitat.Forest)==(b.Habitat&Habitat.Forest)?0:1.4;
                return moisture*1.4+Math.Abs(a.ColdTenthsC-b.ColdTenthsC)/150.0+cover+Math.Abs(a.GrowingDays-b.GrowingDays)/150.0;
            }
            return D.Q(Math.Exp(-Contrast(local,target)-.7*Contrast(origin,target)),1000000);
        }
    }
    public sealed partial class Prehistory
    {
        static bool RealismHistory(PrehistoryState s)=>s.History?.Version=="history-16"||IsAggregateHistoryVersion(s.History?.Version);
        double[] landscapeYield,landscapeStored,landscapeOpportunity,landscapeSeason;
        void PrepareLandscape()
        {
            if(landscapeYield!=null)return;PrepareHistoricalEnvironment();int n=World.Surface.Cells.Length;
            landscapeYield=new double[n];landscapeStored=new double[n];landscapeOpportunity=new double[n];landscapeSeason=new double[n];
            for(int i=0;i<n;i++)
            {
                var h=World.Ecology.Habitat[i];var c=World.Climate;
                double[] rain={c.NorthernSpring.PrecipitationMm[i],c.NorthernSummer.PrecipitationMm[i],c.NorthernAutumn.PrecipitationMm[i],c.NorthernWinter.PrecipitationMm[i]};
                double season=(rain.Max()-rain.Min())/Math.Max(1,rain.Max());
                landscapeSeason[i]=Math.Max(season,D.Clamp((h.WarmTenthsC-h.ColdTenthsC)/220.0));
                landscapeYield[i]=World.Hydrology.OceanConnected[i]?agriculturalYield[i]:HistoricalLandscapeEffects.CropAreaYield(h,riverAccess[i],season);
                double storage=.22+.78*Math.Max(season,D.Clamp((h.WarmTenthsC-h.ColdTenthsC)/220.0));
                if(HistoricalLandscapeEffects.EverWet(h))storage=.25;
                landscapeStored[i]=D.Q(landscapeYield[i]*storage,1000000);
                bool coast=World.Surface.Cells[i].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]);
                double aquatic=(coast?.25:0)+Math.Min(.25,riverAccess[i]*1.5);
                landscapeOpportunity[i]=D.Q(Math.Max(landscapeStored[i],aquatic),1000000);
            }
        }
        double AgriculturalYield(PrehistoryState s,int cell){if(!RealismHistory(s))return HistoricalAgriculturalYield(cell);PrepareLandscape();return landscapeYield[cell];}
        double AgriculturalSiteYield(PrehistoryState s,int cell)
        {
            var h=World.Ecology.Habitat[cell];double cultivable=Math.Min(1,Math.Max(.04,D.Clamp((h.AridityMilli-100)/550.0)+HistoricalRiverAccess(cell)));
            // Local court readiness considers the inhabited irrigable strip;
            // catchment food still integrates its actual, much smaller acreage.
            return D.Q(Math.Min(1,AgriculturalYield(s,cell)/cultivable),1000000);
        }
        double StoredYield(PrehistoryState s,int cell){if(!RealismHistory(s))return HistoricalStoredYield(cell);PrepareLandscape();return landscapeStored[cell];}
        double LandscapeCropFactor(int cell){PrepareLandscape();return D.Clamp(landscapeYield[cell]/Math.Max(.01,agriculturalYield[cell]));}
    }
}
