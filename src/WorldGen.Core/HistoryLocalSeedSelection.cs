using System;

namespace WorldGen.Core
{
    public static class HistoricalLocalSeedSelectionEffects
    {
        // Revision14 only. Standing seed variation can respond to repeated
        // selection; this is an authored bounded prior, not a measured universal
        // degrees/year mutation rate. Fit and actual fields gate effort first.
        public static int LatitudeMovement(int current,int target,int practiceYears,double roll)
            =>HistoricalCropSelectionEffects.FractionalYears(Math.Min(Math.Abs(target-current),Math.Max(0,practiceYears)*.1),roll);
        public static void Apply(HistoricalCultivar cultivar,int practiceYears,int target,int movement)
        {
            if(practiceYears<=0)return;
            HistoricalCropSelectionEffects.Apply(cultivar,practiceYears,target,true,movement);
            // The restored dense physiological curve must not weaken the
            // previously active aggregate curve for FRESH seed. Preserve both
            // its floor and inherited stronger phenotypes, without changing the
            // literal revision10-13 helper or granting traits at upgrade.
            var definition=HistoricalCropCatalog.Crops[cultivar.CropId];int selected=cultivar.SelectedYears;
            cultivar.YieldPermille=Math.Max(cultivar.YieldPermille,Math.Min(1500,650+selected/5));
            cultivar.MaturityDays=Math.Min(cultivar.MaturityDays,Math.Max(definition.MaturityDays-35,definition.MaturityDays-selected/100));
            cultivar.MinimumWarmTenthsC=Math.Min(cultivar.MinimumWarmTenthsC,Math.Max((definition.MinimumWarmC-4)*10,definition.MinimumWarmC*10-selected/100));
        }
    }
}
