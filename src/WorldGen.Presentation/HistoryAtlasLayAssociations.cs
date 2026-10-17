using System.Collections.Generic;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        static AtlasSection? ReligiousLaySection(PrehistoryState s,int religion)
        {
            if(s.History.Aggregates.Empires.Connections.Religion.LayAssociations==null)return null;
            int potential=0,active=0,restricted=0;var sample=new List<Settlement>();
            // An explicit on-demand actor inspection, not a recurring census.
            foreach(var town in s.Society.Settlements)
            {
                if(town.Population<=0||s.History.Aggregates.Living.Cities[town.Id].AbandonedYear>=0)continue;
                var lay=Prehistory.LayAssociationAt(s,town.Cell);if(lay==null||!lay.Active||lay.Town!=town.Id||lay.Faith!=religion)continue;
                potential++;if(lay.Support<100)continue;active++;
                if(lay.Dispute==ReligiousAssociationDispute.SuppressedWorship)restricted++;
                int at=0;while(at<sample.Count&&(sample[at].Population>town.Population||sample[at].Population==town.Population&&sample[at].Id<town.Id))at++;
                if(at<12){sample.Insert(at,town);if(sample.Count>12)sample.RemoveAt(12);}
            }
            if(potential==0)return null;var links=new List<AtlasLink>();
            foreach(var town in sample){var link=TownLink(s,town);link.Detail="Locally provisioned association; public predominance is separate";links.Add(link);}
            return new AtlasSection{Title="Household associations",Facts=new[]{F("Recorded local potentials",N(potential)+" towns"),F("Provisioned local associations",N(active)+" towns"),F("Currently restricted meetings",N(restricted)+" towns")},Links=links.ToArray(),Text="Recorded household communities can sustain observances, ritual calendars and mutual aid without controlling public worship. A potential is not a working institution: actual local gifts are required. Restrictions can reduce activity without changing inherited identity. These are observed organizations, not numbers of believers or a unified global clergy."};
        }
    }
}
