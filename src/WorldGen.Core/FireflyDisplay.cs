using System;

namespace WorldGen.Core
{
    /// <summary>Display of the existing derived regional firefly lineages, not
    /// a new native taxon or an additional audio population.</summary>
    public static class FireflyDisplay
    {
        static double Smooth(double low,double high,double value)
        {double t=Math.Max(0,Math.Min(1,(value-low)/(high-low)));return t*t*(3-2*t);}
        public static double HabitatGain(AmbientFaunaSpecies species,HabitatCell habitat,LocalTerrainSample local)
        {
            if(species?.Family!=AmbientFaunaFamily.Firefly||habitat==null||local.Underwater||local.Biome==Biome.Ocean||local.Biome==Biome.Ice)return 0;
            double cover=Math.Min(1,local.Cover.Coverage+local.MidCover.Coverage+local.Forest.Coverage*.5);
            return AmbientFauna.HabitatSuitability(species,habitat)*Math.Max(0,cover)*(1-Math.Max(0,Math.Min(1,local.Ground.Salinity)));
        }
        public static double ActivityGain(AmbientFaunaSpecies species,double hour,double year,double latitude,double temperature,double humidity,double rain,double wind)
        {
            if(species?.Display==null||species.Activity==null)return 0;
            hour=(hour%24+24)%24;
            // This visual lineage represents flashing dusk/night fireflies.
            // Diurnal, non-luminous fireflies are not a second spawned guild.
            double dark=hour>=12?Smooth(17.5,19,hour):1-Smooth(4.5,6,hour);
            if(dark<=0)return 0;
            var a=species.Activity;double elapsed=(hour-a.StartSolarHour+48)%24;
            double daily=Smooth(0,.5,elapsed)*(1-Smooth(a.DurationHours-.5,a.DurationHours,elapsed));
            double thermal=Smooth(a.MinTemperatureC,a.PreferredTemperatureC,temperature)*(1-Smooth(a.MaxTemperatureC-3,a.MaxTemperatureC,temperature));
            double season=1;
            if(a.SeasonCue==InsectSeasonCue.AnnualWarmSeason)
            {
                double day=(year-Math.Floor(year))*365,peak=(latitude>=0?.56:.06)*365+a.SeasonPeakOffsetDays;
                peak=(peak%365+365)%365;double offset=Math.Abs(day-peak);offset=Math.Min(offset,365-offset);
                season=1-Smooth(a.AdultDisplayWindowDays*.38,a.AdultDisplayWindowDays*.5+12,offset);
            }
            double weather=1/(1+Math.Pow(Math.Max(0,rain)/Math.Max(.1,a.RainSuppressionMmPerHour),2));
            weather/=1+Math.Pow(Math.Max(0,wind)/Math.Max(.1,a.WindSuppressionMps),2);
            return Math.Max(0,Math.Min(1,dark*daily*thermal*season*weather*Smooth(.3,.75,humidity)));
        }
        public static double Pulse(FireflyDisplayTraits display,double seconds,uint identity)
        {
            if(display==null||display.FlashOnSeconds<=0||display.FlashesPerBout<1)return 0;
            double on=display.FlashOnSeconds,step=on+Math.Max(0,display.WithinBoutGapSeconds);
            double period=(display.FlashesPerBout-1)*step+on+Math.Max(.1,display.BetweenBoutGapSeconds);
            double phase=(seconds+(display.Synchronous?0:identity/(double)uint.MaxValue*period))%period;if(phase<0)phase+=period;
            int flash=(int)(phase/step);if(flash>=display.FlashesPerBout)return 0;
            double t=phase-flash*step;if(t>=on)return 0;
            double edge=Math.Min(.035,on*.2);return Smooth(0,edge,t)*(1-Smooth(on-edge,on,t));
        }
        public static V Color(FireflyDisplayTraits display)
        {
            double t=Math.Max(0,Math.Min(1,((display?.WavelengthNm??570)-555)/35));
            return new V(.55+.45*t,1-.3*t,.08);
        }
    }
}
