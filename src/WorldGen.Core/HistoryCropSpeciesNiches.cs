using System;
namespace WorldGen.Core
{
    public static class HistoricalCropSpeciesNiches
    {
        // Potato is a cool-season crop, not a tropical perennial root garden.
        // Keep the catalog's actual cultivar physiology/photoperiod checks and
        // all physical/cover/soil penalties; only replace the wrong group niche.
        public static double TransferFit(HabitatCell h,HistoricalCultivar cultivar,double latitude,double freshwater=0,bool managed=false)
        {
            if(cultivar==null||cultivar.CropId!=10)return HistoricalCropCatalog.TransferFit(h,cultivar,latitude,freshwater,managed);
            double warm=h.WarmTenthsC/10.0,water=h.AridityMilli/1000.0;
            bool fresh=h.Flooding==FloodRegime.RiverSeasonal||h.Flooding==FloodRegime.RiverPersistent||freshwater>.2;
            if(fresh)water=Math.Max(water,.9);
            double season=D.Clamp(h.GrowingDays/180.0)*(1-h.PermafrostPermille/1000.0);
            double slope=D.Clamp(1-h.ReliefM/2200.0),salt=D.Clamp(1-h.SalinityMilliPpt/2500.0);
            // FAO Ecocrop's broad7C lower range /15C favorable range informs
            // this authored ramp. Existing ReproductiveFit supplies heat stress.
            // Annual winter cold is not continuous frost during the crop window.
            double niche=D.Clamp((warm-7)/8)*D.Clamp((water-.25)/.5);
            double suitable=D.Q(D.Clamp(niche*season*slope*salt*(.4+.6*D.Clamp(h.SoilFertilityPermille/650.0))),1000000);
            double physical=HistoricalCrops.AbioticPotential(h,freshwater);
            double system=Math.Min(1.2,suitable/Math.Max(.01,physical));
            double clearing=h.Biome==Biome.TropicalRainforest?(managed?.85:.55):(h.Habitat&Habitat.Forest)!=0?.85:1;
            double factor=D.Q(system*D.Clamp(physical/.2)*HistoricalCropCatalog.LeachingPenalty(h,managed)*clearing,1000000);
            return D.Q(HistoricalCropCatalog.Fit(h,cultivar,latitude,freshwater)*factor,1000000);
        }
    }
    public sealed partial class Prehistory
    {
        static bool SpeciesCropNicheHistory(PrehistoryState s)
            =>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=19;
    }
}
