using System;
using System.Collections.Generic;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        public static AtlasSection? ReligiousLocalSection(PrehistoryState s,int cell)
        {
            var local=Prehistory.ReligiousCommunityAt(s,cell);if(local==null)return null;
            var facts=new List<AtlasFact>();var links=new List<AtlasLink>();
            void Identity(string label,int religion)
            {
                if(religion<0||religion>=s.History.Religions.Length)return;
                facts.Add(F(label,s.History.Religions[religion].Name));links.Add(FaithLink(s.History.Religions[religion]));
            }
            Identity("Public tradition",local.Dominant);Identity("Household inheritance",local.Household);
            if(local.Visitor>=0&&local.Visitor!=local.Dominant)Identity("Contacted tradition",local.Visitor);
            facts.Add(F("Institutional support",local.Institution+" / 1,000"));
            if(local.Pressure>0)facts.Add(F("Competing public influence",local.Pressure+" / 1,000"));
            if(local.Blending>0)facts.Add(F("Shared ritual familiarity",local.Blending+" / 1,000"));
            if(local.LastContact>=0)facts.Add(F("Last religious contact","Year "+Y(s,local.LastContact)));
            facts.Add(F("Court policy",local.Policy));
            return new AtlasSection{Title="Public worship and household practice",Facts=facts.ToArray(),Links=links.ToArray(),Text="Public predominance can change through sustained local adoption while household rites continue. Institutions need continuing provisions. Influence, support and familiarity are indices, not percentages of believers."};
        }
        public static AtlasSection[] ReligiousNetworkSections(PrehistoryState s,int religion)
        {
            if(s?.History?.Aggregates?.Empires?.Connections?.Religion==null||religion<0||religion>=s.History.Religions.Length)return Array.Empty<AtlasSection>();
            var faith=s.History.Religions[religion];
            string form=faith.Reach==ReligiousReach.Missionary?"Traveling teachers and supported outreach":faith.Reach==ReligiousReach.Portable?"Portable household and teaching traditions":"Local civic and household rites";
            var formSection=new AtlasSection{Title="Ritual community and institutions",Facts=new[]{F("Network form",form),F("Ethos",faith.Ethos),F("Observance",faith.Ritual)},Text="Ritual form, institutional support and political patronage are distinct. A portable teaching can survive a change of government, and a favored court does not establish the beliefs of all its subjects."};
            // Explicit on-demand atlas inspection of existing settlement actors.
            // Retain at most twelve examples; never enumerate terrain holdings.
            int predominant=0,inherited=0,visiting=0,supported=0;var sample=new List<(Settlement Town,HistoricalReligiousCommunity Local)>();
            foreach(var town in s.Society.Settlements)
            {
                if(town.Population<=0||s.History.Aggregates.Living.Cities[town.Id].AbandonedYear>=0)continue;
                var local=Prehistory.ReligiousCommunityAt(s,town.Cell);
                bool publicFaith=local.Dominant==religion,household=local.Household==religion,visitor=local.Visitor==religion;
                if(!publicFaith&&!household&&!visitor)continue;
                if(publicFaith)predominant++;if(household&&!publicFaith)inherited++;if(visitor&&!publicFaith)visiting++;
                if(publicFaith&&local.Institution>=400)supported++;
                int at=0;while(at<sample.Count&&(sample[at].Town.Population>town.Population||sample[at].Town.Population==town.Population&&sample[at].Town.Id<town.Id))at++;
                if(at<12){sample.Insert(at,(town,local));if(sample.Count>12)sample.RemoveAt(12);}
            }
            var links=new List<AtlasLink>();
            foreach(var item in sample)
            {
                string role=item.Local.Dominant==religion?"public tradition":item.Local.Household==religion?"retained household tradition":"contacted teaching";
                var link=TownLink(s,item.Town);link.Detail=role+"; institutional support "+item.Local.Institution+" / 1,000";links.Add(link);
            }
            var communitySection=new AtlasSection{Title="Recorded communities",Facts=new[]{F("Public predominance",N(predominant)+" towns"),F("Household continuity elsewhere",N(inherited)+" towns"),F("Encountered teaching elsewhere",N(visiting)+" towns"),F("Supported public institutions",N(supported)+" towns")},Links=links.ToArray(),Text="These observations describe existing towns, with up to twelve larger examples shown. They are neither adherent counts nor a complete inventory of countryside worship. A contacted teaching may be accepted, adapted, contested or forgotten."};
            return new[]{formSection,communitySection};
        }
    }
}
