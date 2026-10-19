using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        public static AtlasSection[] CelestialSections(PrehistoryState s,int id,bool faith)
        {
            var sky=s.History?.Sky;if(sky==null)return Array.Empty<AtlasSection>();var profiles=faith?sky.Faiths:sky.Cultures;
            if(id<0||id>=profiles.Length)return Array.Empty<AtlasSection>();var p=profiles[id];
            string calendar=p.Basis==CelestialCalendarBasis.SeasonalSolar?"Seasonal solar year":p.Basis==CelestialCalendarBasis.Lunar?"Lunar cycle":"Lunisolar calendar";
            if(p.CalendarMoon>=0)calendar+=" · "+sky.Settings.Moons[p.CalendarMoon].Name+" · "+p.MonthsPerCycle+" ordinary months of "+p.LunationDays.ToString("F2")+" days";
            string correction=p.Intercalation?"An extra month approximately every "+(p.LunationDays/Math.Max(.0001,365-p.MonthsPerCycle*p.LunationDays)).ToString("F1")+" seasonal years":"No lunar intercalation";
            string[] turns={"northern vernal equinox","northern summer solstice","northern autumnal equinox","northern winter solstice"};
            string festival=p.Festival==CelestialFestival.LocalCommemoration?"Local commemorative observance, not tied to the sky":p.Festival==CelestialFestival.SolarTurning?"At the "+turns[p.Quarter]:"At "+(p.FestivalPhase==0?"new":"full")+" "+sky.Settings.Moons[p.SacredMoon].Name;
            string next="Not an astronomical date";
            if(p.Festival==CelestialFestival.LunarObservance){double now=SimulationTime.Seconds(s)/(double)SimulationTime.Day;next="In "+(CelestialMechanics.NextPhase(sky.Settings.Moons[p.SacredMoon],now,p.FestivalPhase)-now).ToString("F1")+" calendar days";}
            else if(p.Festival==CelestialFestival.SolarTurning){double phase=CelestialMechanics.Fraction(SimulationTime.Seconds(s)/(double)SimulationTime.Year-.218);next="In "+(100*CelestialMechanics.Fraction(p.Quarter*.25-phase)).ToString("F1")+"% of a seasonal year";}
            return new[]{new AtlasSection{Title=faith?"Ritual astronomy and festivals":"Calendar and the observed sky",CompactGrid=true,Facts=new[]{F("Calendar",calendar),F("Seasonal correction",correction),F("Principal observance",festival),F("Next astronomical observance",next),F("Moon symbolism",p.MoonMeaning==CelestialMoonMeaning.None?"No recorded sacred lunar role":sky.Settings.Moons[p.SacredMoon].Name+" · "+ReligiousDoctrines.Name(p.MoonMeaning)),F("Stars",ReligiousDoctrines.Name(p.StarMeaning))},Text="These are inherited community traditions, not universal beliefs. Practical lunar timekeeping does not require worship of a moon."}};
        }
    }
}
