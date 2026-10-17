using WorldGen.Core;

public static class HistoryReligiousInstitutionTests
{
    public static void Run(Action<bool,string> check)
    {
        check(HistoricalReligiousInstitutionEffects.Vocation(.1,ReligiousReach.Local)==ReligiousVocation.SacredPlaces,"A durable place-bound ritual vocation is a valid institutional form.");
        check(!HistoricalReligiousInstitutionEffects.Portable(ReligiousVocation.SacredPlaces,1000,true,true),"Large funded sacred-place institutions are not forced into portable teaching.");
        check(HistoricalReligiousInstitutionEffects.Portable(ReligiousVocation.HouseholdTradition,600,true,true)&&!HistoricalReligiousInstitutionEffects.Portable(ReligiousVocation.HouseholdTradition,600,true,false),"Portable teaching needs actual contact and provisions, not only wealth or a map-neighbor edge.");
        check(HistoricalReligiousInstitutionEffects.Vocation(.9,ReligiousReach.Local)==HistoricalReligiousInstitutionEffects.Vocation(.9,ReligiousReach.Portable),"A stable traveling-teacher vocation survives its actual establishment of portability.");
        check(HistoricalReligiousInstitutionEffects.Vocation(.1,ReligiousReach.Missionary)==ReligiousVocation.TravelingTeaching,"A previously recorded outreach tradition is not erased during an explicit rules update.");
        double endowed=HistoricalReligiousInstitutionEffects.PolicyReformChance(900,300,200,1,0,800),fragile=HistoricalReligiousInstitutionEffects.PolicyReformChance(0,10,200,1,0,800);
        check(endowed<fragile/3,"Continuing endowed ritual institutions damp personal succession policy reversals.");
        check(HistoricalReligiousInstitutionEffects.PolicyReformChance(500,100,900,2,0,800)>HistoricalReligiousInstitutionEffects.PolicyReformChance(500,100,200,2,0,800),"A restrictive legal settlement can face real pressure from broader political representation.");
        check(!HistoricalReligiousInstitutionEffects.OrdinaryCarrier(true,false,900,900,true,2),"An active war interrupts the ordinary city religious-carrier shortcut.");
        check(!HistoricalReligiousInstitutionEffects.OrdinaryCarrier(false,true,900,900,false,2),"A disrupted city cannot continue ordinary religious trips merely because its old graph edge survives.");
        check(!HistoricalReligiousInstitutionEffects.OrdinaryCarrier(false,false,300,900,true,2)&&HistoricalReligiousInstitutionEffects.OrdinaryCarrier(false,false,900,900,true,2),"Peaceful viable foreign households can exchange observances; instability matters independently of ancestry or religion.");
        check(!HistoricalReligiousInstitutionEffects.OrdinaryCarrier(false,false,900,900,false,.1),"A community below the ordinary travel provision floor cannot dispatch free household carriers.");
        var forms=Enum.GetValues<ReligiousVocation>().SelectMany(v=>new[]{HistoricalReligiousProfiles.Ethos(v,false),HistoricalReligiousProfiles.Ethos(v,true)}).ToArray();
        check(forms.Distinct().Count()==6&&forms.Any(x=>x.Contains("Ancestral"))&&forms.Any(x=>x.Contains("Ethical"))&&forms.Any(x=>x.Contains("household")),"Fresh profiles distinguish inherited obligations, civic observance and ethical teaching rather than one identical generic ethos.");
        check(HistoricalReligiousProfiles.Ritual(ReligiousVocation.SacredPlaces,true).Contains("sacred places")&&HistoricalReligiousProfiles.Ritual(ReligiousVocation.HouseholdTradition,false).Contains("carried by households"),"Profile practice describes the actual local versus household transmission form without a biome-to-theology rule.");
    }
}
