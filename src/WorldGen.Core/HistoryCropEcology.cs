using System;
namespace WorldGen.Core
{
    public static class HistoricalCropEcology
    {
        // Broad agronomic priors, not a crop-yield simulator or latitude bans.
        public static double WildAnnualOpportunity(HabitatCell h,double rainSeason,int group)
        {
            double season=Math.Max(D.Clamp(rainSeason),D.Clamp((h.WarmTenthsC-h.ColdTenthsC)/180.0));
            double open=(h.Habitat&Habitat.Forest)!=0?.25:1;
            if(group==0)return D.Q(.025+.975*season*season*open,1000000);
            if(group==1)return D.Q(.08+.92*season*open,1000000);
            if(group==5)return D.Q(.25+.75*season*Math.Sqrt(open),1000000);
            return 1; // Wetland rice, tubers and orchards have other niches.
        }
        public static double ReproductiveFit(HabitatCell h,HistoricalCultivar cultivar)
        {
            double selection=D.Clamp(cultivar.SelectedYears/4000.0),cold=h.ColdTenthsC,warm=h.WarmTenthsC,fit=1;
            switch(cultivar.CropId)
            {
                case 0:case 1:fit=D.Clamp((340+selection*20-warm)/100.0);break;
                case 2:case 3:fit=D.Clamp((300+selection*20-warm)/100.0);break;
                case 8:case 9:fit=D.Clamp((cold+10)/100.0);break; // Frost-sensitive tropical perennials.
                case 10:fit=D.Clamp((290+selection*20-warm)/100.0);break; // Tuberization suffers in persistent heat.
                case 11:fit=D.Clamp((230+selection*20-cold)/100.0)*D.Clamp((cold+80)/80.0);break; // Chilling and frost survival.
                case 12:fit=(.05+.95*D.Clamp(h.WarmSeasonDryPermille/650.0))*D.Clamp((cold+50)/100.0);break; // Dry fruit ripening, not absence of irrigation.
                case 13:fit=D.Clamp((cold-80)/100.0)*D.Clamp((h.AridityMilli-350)/500.0);break;
            }
            return D.Q(fit,1000000);
        }
    }
}
