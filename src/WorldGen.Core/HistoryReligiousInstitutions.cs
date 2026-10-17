using System;
using System.Linq;

namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        static string ReligiousPolicyName(int policy)=>policy<0?"Court religious policy not yet observed":policy==2?"Court establishment with restrictions on competing public worship":policy==1?"Favored court tradition with permitted local worship":"Plural patronage and permitted local worship";
        void EnsureReligiousPolicy(PrehistoryState s,int polity)
        {
            if(ReligiousInstitutionHistory(s)){EnsureEndowedReligiousPolicy(s,polity);return;}
            var h=s.History;var r=h.Aggregates.Empires.Connections.Religion;int key=polity*4,leader=h.Dynamics.States[polity].LeaderId;
            if(r.Policies[key]==leader&&r.Policies[key+1]>=0)return;
            var person=leader>=0?h.Dynamics.Leaders[leader]:null;var constitution=h.Aggregates.Communities.Governments[polity];
            double disposition=HistoryRoll("religion-court-policy-1",polity,person?.StartedYear??h.Polities[polity].FoundedYear);
            double openness=.20+.40*constitution.RepresentationMilli/1000.0+.20*(person?.Diplomacy??500)/1000.0;
            int policy=disposition<openness?0:disposition>.90&&constitution.RepresentationMilli<650?2:1;
            int former=r.Policies[key+1];r.Policies=r.Policies.With(key,leader).With(key+1,policy).With(key+2,s.Years);
            if(former>=0&&former!=policy)
            {
                var p=h.Polities[polity];HistoryEvent(s,"religious-policy",p.Name+" adopts "+ReligiousPolicyName(policy).ToLowerInvariant()+". The policy applies through local officials and funded institutions rather than converting all inhabitants.",s.Society.Settlements[p.CapitalId].Cell,p.CultureId,polity,religion:p.ReligionId);
            }
        }
        int AdvanceReligiousInstitution(PrehistoryState s,int cell,int city,int owner,int elapsed)
        {
            var h=s.History;var r=h.Aggregates.Empires.Connections.Religion;int previous=Math.Max(0,r.Read(cell,FaithInstitution));
            bool supported=false,funded=false;
            if(city>=0&&h.Aggregates.Living.Cities[city].AbandonedYear<0)
            {
                var town=s.Society.Settlements[city];supported=h.Aggregates.Cities[city].Supported&&town.Population>=600&&town.FoodReserve>=town.Population/3;
                long cost=HistoricalReligiousNetworkEffects.FoodCost(town.Population,elapsed);
                // Gifts and provisions support an institution's workers. These
                // are debits from the existing town reserve, never free output.
                if(supported&&town.FoodReserve-town.Population/2>=cost){town.FoodReserve-=cost;funded=true;}
            }
            int institution=HistoricalReligiousNetworkEffects.Institution(previous,elapsed,funded,supported);
            r.Write(cell,FaithInstitution,institution);
            int faith=HistoricalCells.Religion(s,cell);var tradition=h.Religions[faith];
            bool network=city>=0&&h.Aggregates.Cities[city].Neighbors.Length>0;
            bool revised=ReligiousInstitutionHistory(s),recent=r.Read(cell,FaithContactYear)>=0&&s.Years-r.Read(cell,FaithContactYear)<=80;
            var vocation=revised?ReligiousInstitutionVocation(s,faith):ReligiousVocation.TravelingTeaching;
            bool portable=!revised||HistoricalReligiousInstitutionEffects.Portable(vocation,institution,funded,recent);
            if(funded&&network&&institution>=400&&tradition.Reach==ReligiousReach.Local&&portable)
            {
                tradition.Reach=ReligiousReach.Portable;
                HistoryEvent(s,"religious-institution",ReligiousPlace(s,cell,city)+" sustains a provisioned ritual house and visiting teachers of "+tradition.Name+". Its practices can travel with household and exchange networks.",cell,s.Society.Settlements[city].CultureId,owner,town:city,religion:faith);
            }
            // Portable oral traditions need not pass through a literacy stage.
            // Deliberate outreach is one institutional choice, not an upgrade
            // which every religion or every literate society must acquire.
            bool outreach=revised?vocation==ReligiousVocation.TravelingTeaching&&recent:HistoryRoll("religion-outreach-vocation-1",faith,0)<.24;
            if(funded&&network&&institution>=650&&tradition.Reach==ReligiousReach.Portable&&outreach)
            {
                var town=s.Society.Settlements[city];long mission=HistoricalReligiousNetworkEffects.FoodCost(town.Population,elapsed)*2;
                if(town.FoodReserve-town.Population/2>=mission)
                {
                    town.FoodReserve-=mission;tradition.Reach=ReligiousReach.Missionary;
                    HistoryEvent(s,"religious-mission",tradition.Name+" establishes supported traveling teachers at "+town.Name+". Missions use existing reachable communities and require local reception.",cell,town.CultureId,owner,town:city,religion:faith);
                }
            }
            return institution;
        }
        bool ReligiousPatronagePaid(PrehistoryState s,int owner,int cell,int elapsed)
        {
            var p=s.History.Polities[owner];int city=s.History.Aggregates.TownAt[cell];
            long residents=city>=0?s.Society.Settlements[city].Population:Math.Min(5000,RuralCellPopulation(s,cell));
            long cost=Math.Max(1,(long)Math.Ceiling(residents*Math.Max(0,elapsed)/4000.0));
            // Running an established religious policy does not spend funds
            // already promised to the state government's immediate services.
            long service=s.History.Aggregates.Polities[owner].ServiceCost;
            if(p.Treasury-Math.Max(0,service)<cost)return false;
            p.Treasury-=cost;return true;
        }
        void AdvanceReligiousCourt(PrehistoryState s,int owner,int city,int local,int elapsed)
        {
            var h=s.History;var p=h.Polities[owner];if(p.CapitalId!=city||p.ReligionId==local)return;
            var r=h.Aggregates.Empires.Connections.Religion;int cell=s.Society.Settlements[city].Cell;
            if(s.Years-r.Read(cell,FaithChanged)<150||HistoryRoll("religion-court-adoption-1",owner,s.Years)>=1-Math.Exp(-elapsed/800.0))return;
            long cost=Math.Max(1,s.Society.Settlements[city].Population/200);if(p.Treasury<cost)return;
            p.Treasury-=cost;p.ReligionId=local;p.Legitimacy=Math.Max(100,p.Legitimacy-15);
            HistoryEvent(s,"court-conversion",p.Ruler+" adopts "+h.Religions[local].Name+" for the court of "+p.Name+" after its durable establishment in the capital. Other communities retain their own traditions.",cell,p.CultureId,owner,town:city,religion:local);
        }
        void TryReligiousRenewal(PrehistoryState s,int culture,int cell,int city,int faith,int institution,int elapsed,bool excluded)
        {
            if(city<0||institution<600)return;
            var h=s.History;var r=h.Aggregates.Empires.Connections.Religion;var town=s.Society.Settlements[city];
            if(town.Population<1200||!h.Aggregates.Cities[city].Supported)return;
            int owner=h.Aggregates.Resolve(town.PolityId,h);
            bool mixed=r.Read(cell,FaithBlend)>=400&&r.Read(cell,FaithVisitor)>=0&&r.Read(cell,FaithVisitor)!=faith;
            // In the explicit lay revision, external repression preserves the
            // existing faith; genuine internal renewal is separately organized.
            // Keep the preceding contact-synthesis path and all <=19 arithmetic.
            if(LayReligiousHistory(s)&&!mixed)return;
            bool ethical=ReligiousInstitutionHistory(s)&&CapturedReligiousEndowment(s,owner,faith,town.Stability);
            int dissent=Math.Max(0,r.Read(cell,FaithDissent));
            // Material patronage, competing rites and exclusion provide actual
            // local reasons for a renewal. Calendar age alone creates no sect.
            dissent=Math.Max(0,Math.Min(1000,dissent+(excluded?elapsed:mixed?elapsed/3:ethical?elapsed:-elapsed/2)));
            r.Write(cell,FaithDissent,dissent);
            int recent=Math.Max(h.Religions[faith].FoundedYear,Math.Max(r.SinceYear,Math.Max(r.Read(cell,FaithChanged),r.Read(cell,FaithReformed))));
            if(dissent<600||s.Years-recent<250||HistoryRoll("religion-supported-renewal-1",city,s.Years)>=1-Math.Exp(-elapsed/1500.0))return;
            long cost=Math.Max(10,town.Population/50);if(town.FoodReserve-town.Population/2<cost)return;
            town.FoodReserve-=cost;int id=h.Religions.Length;var parent=h.Religions[faith];int visitor=r.Read(cell,FaithVisitor);
            var renewal=new HistoricalReligion{Id=id,ParentId=faith,CultureId=culture,HolyCell=cell,FoundedYear=s.Years,
                Name=LanguageName(s,culture,LanguageNameKind.Religion,id),Ethos=parent.Ethos,Ritual=mixed?"Inherited household rites with practices received from "+h.Religions[visitor].Name:parent.Ritual,
                Tradition=mixed?"A local synthesis of "+parent.Name+" and practices encountered from "+h.Religions[visitor].Name:"An autonomous renewal of "+parent.Name+" amid disputes over court patronage",
                Reach=ReligiousReach.Portable};
            if(ReligiousInstitutionHistory(s))
            {
                if(excluded&&!mixed){renewal.Tradition="An autonomous local renewal of "+parent.Name+" rejecting court control of its ritual houses";if(ReligiousInstitutionVocation(s,faith)==ReligiousVocation.SacredPlaces)renewal.Reach=ReligiousReach.Local;}
                if(ethical&&!mixed&&!excluded){renewal.Ethos="Ethical restraint, mutual aid and independence from captured court patronage";renewal.Ritual="Inherited household observances, communal recitation and instruction supported by local gifts";renewal.Tradition="An ethical renewal of "+parent.Name+" amid elite capture and recorded local instability";}
            }
            h.Religions=h.Religions.Concat(new[]{renewal}).ToArray();SetAggregateLayer(s,cell,2,id);
            r.Write(cell,FaithHousehold,faith);r.Write(cell,FaithChanged,s.Years);r.Write(cell,FaithReformed,s.Years);r.Write(cell,FaithDissent,0);r.Write(cell,FaithPressure,0);r.Write(cell,FaithBlend,0);
            bool ethicalRenewal=ethical&&!mixed&&!excluded;
            HistoryEvent(s,mixed?"religious-synthesis":ethicalRenewal?"religious-renewal":"religious-schism",renewal.Name+" establishes an autonomous provisioned community at "+town.Name+(mixed?", reinterpreting inherited rites after sustained exchange.":ethicalRenewal?", renewing ethical restraint and mutual aid amid elite capture and local instability.":", disputing court religious authority while retaining its inherited ritual lineage."),cell,culture,owner,town:city,religion:id);
        }
    }
}
