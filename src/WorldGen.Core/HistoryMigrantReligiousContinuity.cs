using System;

namespace WorldGen.Core
{
    // An observed departing community, not a belief census. Public and remembered
    // household rites may differ; the single lay association is not every family.
    public sealed class HistoricalMigrantReligiousContinuity
    {
        public int SourceCell=-1,Public=-1,Household=-1,DestinationPublic=-1,DestinationHousehold=-1;
        public bool CultureFallback,Incumbent;
        public int FoundingPublic=>Incumbent?DestinationPublic:Public;
    }
    public sealed partial class Prehistory
    {
        static bool MigrantReligiousHistory(PrehistoryState s)
            =>(s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version??0)>=29&&ReligiousNetworkHistory(s);

        // Read-only bounded projection: one actual supplying town/cell and one
        // destination. A culture's homeland, ruler and contact teacher are never
        // substituted for the supplied physical departure.
        public static HistoricalMigrantReligiousContinuity MigrantReligiousContinuityAt(PrehistoryState s,int parent,int site,int sourceCell,int sourceTown,long previousResidents)
        {
            var result=new HistoricalMigrantReligiousContinuity();var h=s?.History;var a=h?.Aggregates;
            if(a==null||!ReligiousNetworkHistory(s))return result;
            int count=HistoricalCells.Count(s);
            bool Faith(int faith)=>faith>=0&&faith<h.Religions.Length;
            if(sourceTown>=0&&sourceTown<s.Society.Settlements.Length&&a.Living?.Cities!=null&&sourceTown<a.Living.Cities.Length
                &&a.Living.Cities[sourceTown].AbandonedYear<0&&s.Society.Settlements[sourceTown].Population>0
                &&s.Society.Settlements[sourceTown].Cell>=0&&s.Society.Settlements[sourceTown].Cell<count)
                sourceCell=s.Society.Settlements[sourceTown].Cell;
            if(sourceCell>=0&&sourceCell<count)
            {
                result.SourceCell=sourceCell;int faith=HistoricalCells.Religion(s,sourceCell);
                if(Faith(faith))result.Public=faith;
                faith=a.Empires.Connections.Religion.Read(sourceCell,FaithHousehold);
                if(Faith(faith))result.Household=faith;
            }
            // Only the already-debited migrant cohort's known inherited practice
            // is a fallback. No distant capital, best cell or lay-carrier lottery.
            if(result.Public<0&&parent>=0&&parent<h.Cultures.Length&&Faith(h.Cultures[parent].ReligionId))
            {result.Public=h.Cultures[parent].ReligionId;result.CultureFallback=true;}
            if(result.Household<0)result.Household=result.Public;
            if(site>=0&&site<count)
            {
                int faith=HistoricalCells.Religion(s,site);if(Faith(faith))result.DestinationPublic=faith;
                faith=a.Empires.Connections.Religion.Read(site,FaithHousehold);if(Faith(faith))result.DestinationHousehold=faith;
                result.Incumbent=previousResidents>0&&result.DestinationPublic>=0;
            }
            return result;
        }

        int FoundMigrantAggregateCulture(PrehistoryState s,int patch,int site,int parent,int sourceCell,int sourceTown,long previousResidents)
        {
            if(!MigrantReligiousHistory(s))
            {
                FoundAggregateCulture(s,patch,site,parent);int oldChild=s.Society.Cultures.Length-1;
                InheritDevelopmentCommunityFrom(s,oldChild,parent,site,sourceCell,sourceTown);return oldChild;
            }
            var continuity=MigrantReligiousContinuityAt(s,parent,site,sourceCell,sourceTown,previousResidents);
            FoundAggregateCultureWithFaith(s,patch,site,parent,continuity.FoundingPublic);int child=s.Society.Cultures.Length-1;
            InheritDevelopmentCommunityWithReligiousProfile(s,child,parent,site,sourceCell,sourceTown,false);
            ApplyMigrantReligiousContinuity(s,site,continuity);
            if(continuity.SourceCell>=0){if(previousResidents<=0)InheritSpokenHouseholds(s,site,continuity.SourceCell,0,1);else RecordSpokenContact(s,continuity.SourceCell,site);}
            return child;
        }

        void InheritMigrantTownReligiousHouseholds(PrehistoryState s,int target,int source,int previousFaith,long previousResidents)
        {
            int culture=HistoricalCells.Culture(s,source);if(culture<0)culture=HistoricalCells.Culture(s,target);
            var continuity=MigrantReligiousContinuityAt(s,culture,target,source,-1,previousResidents);
            // FoundLivingTown has already written the incoming culture default
            // into the public layer. Its explicit PRE-transfer snapshot, not
            // that overwritten layer, is evidence of resident public practice.
            continuity.DestinationPublic=previousFaith;
            continuity.Incumbent=previousResidents>0&&previousFaith>=0&&previousFaith<s.History.Religions.Length;
            ApplyMigrantReligiousContinuity(s,target,continuity);
        }

        void ApplyMigrantReligiousContinuity(PrehistoryState s,int site,HistoricalMigrantReligiousContinuity continuity)
        {
            if(!MigrantReligiousHistory(s)||continuity.Public<0)return;
            var r=s.History.Aggregates.Empires.Connections.Religion;
            if(continuity.Incumbent)
            {
                // Real residents retain both their public rites and remembered
                // household identity. Arrival is contact, not instant conversion.
                SetAggregateLayer(s,site,2,continuity.DestinationPublic);
                if(r.Read(site,FaithHousehold)<0)r.Write(site,FaithHousehold,continuity.DestinationHousehold>=0?continuity.DestinationHousehold:continuity.DestinationPublic);
                int visitor=continuity.Household!=continuity.DestinationPublic?continuity.Household:continuity.Public;
                if(visitor>=0&&visitor!=continuity.DestinationPublic)
                {
                    int former=r.Read(site,FaithVisitor);
                    if(former==visitor){r.Write(site,FaithContactYear,s.Years);r.Write(site,FaithCarrier,0);return;}
                    if(former>=0&&former<s.History.Religions.Length&&s.Years-r.Read(site,FaithContactYear)<=80)
                    {
                        int oldStrength=HistoricalReligiousNetworkEffects.CarrierStrength(s.History.Religions[former].Reach,Math.Max(0,r.Read(site,FaithCarrier)));
                        int newStrength=HistoricalReligiousNetworkEffects.CarrierStrength(s.History.Religions[visitor].Reach,0);
                        if(newStrength<oldStrength||newStrength==oldStrength&&visitor>former)return;
                    }
                    r.Write(site,FaithVisitor,visitor);r.Write(site,FaithContactYear,s.Years);r.Write(site,FaithContactSince,s.Years);r.Write(site,FaithCarrier,0);
                }
                return;
            }
            SetAggregateLayer(s,site,2,continuity.Public);
            r.Write(site,FaithHousehold,continuity.Household);r.Write(site,FaithReviewed,s.Years);r.Write(site,FaithChanged,s.Years);
            r.Write(site,FaithInstitution,0);r.Write(site,FaithPressure,0);r.Write(site,FaithDissent,0);r.Write(site,FaithBlend,0);
            r.Write(site,FaithCandidate,-1);r.Write(site,FaithVisitor,-1);r.Write(site,FaithContactYear,-1);r.Write(site,FaithContactSince,-1);r.Write(site,FaithCarrier,0);r.Write(site,FaithReformed,-1);
            // An actual community can bring rites, not a paid temple, donated
            // reserve or ready association. A later town must organize normally.
            SeedLayReligiousCommunity(s,site,continuity.Household);
        }
    }
}
