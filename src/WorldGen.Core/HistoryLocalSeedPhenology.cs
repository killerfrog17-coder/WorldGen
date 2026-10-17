using System;

namespace WorldGen.Core
{
    public static class HistoricalLocalSeedPhenology
    {
        // A successful annual crop can reproduce every growing season even
        // when its yield is poor. These are bounded scenario priors, not a
        // universal measured latitude-per-year genetic adaptation rate.
        public static double Generations(int elapsed,int group,long plantedFarmers,double fit,bool harvested)
            =>elapsed<=0||plantedFarmers<10||fit<.08||!harvested?0:elapsed/(group==4?20.0:1.0);

        public static int Movement(int current,int target,double generations,double roll)
            =>HistoricalCropSelectionEffects.FractionalYears(Math.Min(Math.Abs(target-current),Math.Max(0,generations)*.3),roll);
    }

    public sealed partial class Prehistory
    {
        double LocalSeedGenerations(PrehistoryState s,int town,int group,HistoricalLocalSeedlot lot,double fit,int elapsed)
        {
            var t=s.Society.Settlements[town];var a=s.History.Aggregates;
            var harvest=s.History.Towns[town].Cropping;
            // A stored imported variety is not proof of a planted crop. Use
            // the actual local crop survey and a bounded working-field share.
            if(!a.Cities[town].Supported||harvest==null||harvest.Year>s.Years||s.Years-harvest.Year>40||harvest.CropIds==null||harvest.SharesPermille==null)return 0;
            int share=0;
            for(int k=0;k<Math.Min(harvest.CropIds.Length,harvest.SharesPermille.Length);k++)
                if(harvest.CropIds[k]==lot.Profile.CropId)share+=Math.Max(0,harvest.SharesPermille[k]);
            long farmers=(long)(Math.Max(0,a.Cities[town].Rural)*D.Clamp(t.CultivatedPermille/1000.0)*D.Clamp(share/1000.0));
            return HistoricalLocalSeedPhenology.Generations(Math.Min(elapsed,Math.Max(0,s.Years-lot.AcquiredYear)),group,farmers,fit,share>0);
        }
    }
}
