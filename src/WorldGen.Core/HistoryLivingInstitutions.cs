using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void AdvanceLivingCityCondition(PrehistoryState s,int id,int elapsed)
        {
            var a=s.History.Aggregates;var t=s.Society.Settlements[id];var life=a.Living.Cities[id];var hc=s.History.Cultures[t.CultureId];int owner=a.Resolve(t.PolityId,s.History);
            double insecurity=owner>=0?s.History.Polities[owner].Exhaustion/1000.0:0;
            double water=riverAccess[t.Cell],mitigation=HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Sanitation,s.Years);
            double hazard=(.00035+.00060*water+.0012*insecurity)*(1-.75*mitigation);
            if(life.DisruptedUntil<=s.Years&&HistoryRoll("living-site-disruption",id,s.Years)<1-Math.Exp(-hazard*elapsed))
            {
                life.Productivity=.18+.40*HistoryRoll("living-site-loss",id,s.Years);life.DisruptedUntil=s.Years+40+(int)(140*HistoryRoll("living-site-recovery",id,s.Years));
                HistoryEvent(s,"local-decline",t.Name+" loses part of its provisioning base"+(insecurity>.4?" during prolonged conflict.":water>.05?" after disruption of local water and farmland.":" as local land use and exchange routes change."),t.Cell,t.CultureId,owner,town:id,category:ChronicleCategory.Society);
            }
            if(life.DisruptedUntil<s.Years)life.Productivity=Math.Min(1,life.Productivity+elapsed*.004);
            if(s.Years-life.LastContactYear>=40)
            {
                life.LastContactYear=s.Years;var neighbors=NearbyLivingTowns(s,t.Cell,280);
                foreach(int peer in neighbors)if(peer!=id&&LivingTownConnection(s,id,peer))LinkLivingTowns(s,id,peer);
            }
        }
        void TryLivingInstitutions(PrehistoryState s,int id)
        {
            var h=s.History;var a=h.Aggregates;var t=s.Society.Settlements[id];var city=a.Cities[id];var record=h.Towns[id];var hc=h.Cultures[t.CultureId];
            int elapsed=Math.Max(1,s.Years-city.LastPoliticalYear);double fiscal=LivingCropLandscape(s,t.CultureId,t.Cell,true);
            int husbandry=h.Agriculture[t.CultureId].HusbandryYears?.Max()??0;
            if(t.Population<1200||fiscal<.10&&(fiscal<.04||husbandry<12000)||t.FoodReserve<t.Population*.3||a.Living.Cities[id].DeclineYears>0)
            {record.CivicPreparationYears=Math.Max(0,record.CivicPreparationYears-elapsed);return;}
            int teacher=-1;
            foreach(int neighbor in city.Neighbors)
            {int polity=a.Resolve(s.Society.Settlements[neighbor].PolityId,h);if(polity>=0&&s.Years-h.Polities[polity].FoundedYear>=50){teacher=polity;break;}}
            var regional=a.Living.Regions[CellPatch[t.Cell]];
            if(teacher<0&&regional.CivicTeacher>=0&&regional.CivicExposure>=150)
            {teacher=regional.CivicTeacher;record.CivicExampleExposure=Math.Max(record.CivicExampleExposure,regional.CivicExposure/2);}
            if(teacher<0&&a.Living.CivicOrigins[t.CultureId]>=0)
            {teacher=a.Living.CivicOrigins[t.CultureId];record.CivicExampleExposure=Math.Max(600,record.CivicExampleExposure);}
            if(teacher<0&&record.CivicTeacherPolityId>=0)
            {teacher=record.CivicTeacherPolityId;record.CivicExampleExposure=Math.Max(500,record.CivicExampleExposure);}
            if(teacher>=0){record.CivicTeacherPolityId=teacher;record.CivicExampleExposure=Math.Min(1000,record.CivicExampleExposure+elapsed*3);}
            else record.CivicExampleExposure=Math.Max(0,record.CivicExampleExposure-elapsed);
            // Returning communities retain institutions; this is not a pristine origin.
            int raw=a.Cells[t.Cell*5+1];bool restoration=raw>=0&&h.Polities[raw].EndedYear>=0;
            if(restoration){record.CivicTeacherPolityId=raw;record.CivicExampleExposure=Math.Max(record.CivicExampleExposure,600);}
            record.CivicPreparationYears+=elapsed;
            double concentration=D.Clamp(t.Population/3500.0),institutions=D.Clamp(hc.Institution/600.0);
            double waterCoordination=D.Clamp(riverAccess[t.Cell]*4),exchange=D.Clamp(city.Neighbors.Count(n=>s.Society.Settlements[n].Population>=500)/4.0);
            double seasonal=.25+.75*landscapeSeason[t.Cell];
            double core=D.Clamp(fiscal/.35)*concentration*institutions*(.60+.25*waterCoordination+.15*exchange)*(.6+.4*seasonal);
            double independent=.00000003+.00015*Math.Pow(core,8),transmitted=.008*Math.Pow(core,2)*record.CivicExampleExposure/1000.0;
            if(record.CivicPreparationYears>=120&&s.Society.Cultures[t.CultureId].Farming>=250&&HistoryRoll("living-state-foundation",id,s.Years)<1-Math.Exp(-(independent+transmitted)*elapsed))
            {
                FoundAggregateState(s,id,restoration?raw:-1);
                HistoryEvent(s,restoration?"institutional-restoration":teacher>=0?"institutional-diffusion":"independent-institutions",t.Name+(restoration?" restores local state institutions.":teacher>=0?" establishes a court through sustained contact with neighboring states.":" becomes an independent center of state institutions backed by storable agricultural surplus."),t.Cell,t.CultureId,t.PolityId,town:id,category:ChronicleCategory.Politics);
            }
        }
    }
}
