using System;

namespace WorldGen.Core
{
    public static class HistoricalReligiousProfiles
    {
        public static string Ethos(ReligiousVocation vocation,bool oath)
            =>vocation==ReligiousVocation.SacredPlaces?oath?"Ancestral obligations, public oaths and care of sacred places":"Ancestral remembrance, reciprocal offerings and sacred-place stewardship"
            :vocation==ReligiousVocation.HouseholdTradition?oath?"Hospitality, reliable oaths and mutual household aid":"Care of kin, commemoration and reciprocal household obligations"
            :oath?"Ethical discipline, compassionate service and shared teaching":"Restraint, healing and hospitality toward those seeking instruction";
        public static string Ritual(ReligiousVocation vocation,bool oath)
            =>vocation==ReligiousVocation.SacredPlaces?oath?"Public oath-taking, shared feasts and offerings at the founding community's sacred places":"Household ancestor offerings and seasonal gatherings at remembered sacred places"
            :vocation==ReligiousVocation.HouseholdTradition?oath?"Household commemorations, oral recitation and reciprocal hospitality":"Family offerings, shared meals and oral remembrance carried by households"
            :oath?"Communal recitation, periods of restraint and instruction supported by shared gifts":"Healing observances, oral instruction and hospitality to visiting ritual specialists";
    }
    public sealed partial class Prehistory
    {
        void InitializeReligiousProfile(PrehistoryState s,int religion)
        {
            if(!ReligiousInstitutionHistory(s))return;var faith=s.History.Religions[religion];
            var vocation=ReligiousInstitutionVocation(s,religion);bool oath=HistoryRoll("religion-observance-root-2",religion,0)<.5;
            faith.Ethos=HistoricalReligiousProfiles.Ethos(vocation,oath);faith.Ritual=HistoricalReligiousProfiles.Ritual(vocation,oath);
            faith.Tradition="A locally established household and ritual community rooted among "+s.Society.Cultures[faith.CultureId].Name+"; its obligations and observances are transmitted orally";
        }
        void InheritReligiousProfile(PrehistoryState s,int child,int parent)
        {
            if(!ReligiousInstitutionHistory(s))return;var h=s.History;var target=h.Religions[h.Cultures[child].ReligionId];var source=h.Religions[h.Cultures[parent].ReligionId];
            if(target.Id==source.Id)return;
            // Actual migrant-community inheritance, not the unrelated regional
            // culture that happens to be credited with a technical lesson.
            target.ParentId=source.Id;target.Ethos=source.Ethos;target.Ritual=source.Ritual;
            target.Tradition="Household rites and obligations brought by migrants from "+source.Name+", re-established at the new community without a transported priesthood or endowed temple";
            if(source.Reach!=ReligiousReach.Local&&ReligiousInstitutionVocation(s,target.Id)!=ReligiousVocation.SacredPlaces)target.Reach=ReligiousReach.Portable;
        }
        bool CapturedReligiousEndowment(PrehistoryState s,int owner,int faith,int stability)
            =>owner>=0&&s.History.Polities[owner].ReligionId==faith&&(s.History.Dynamics.States[owner].Cycle?.EliteCapture??0)>=650&&stability<500;
    }
}
