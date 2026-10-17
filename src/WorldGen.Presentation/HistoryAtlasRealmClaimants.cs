using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        static AtlasSection? LocalClaimantSection(PrehistoryState s,int city)
        {
            var c=HistoricalRealmClaimantHistory.At(s,city);if(c==null)return null;
            bool active=HistoricalRealmClaimantHistory.Active(s,c);
            string history=c.ResolvedYear>=0?c.Name+"'s recorded coalition "+c.Outcome+".":c.Name+"'s remaining coalition has insufficient support or no longer controls its recorded center.";
            return new AtlasSection{Title="Local political coalition",Text=active?c.Name+" leads a supported local challenge to the central court. Its dispute can outlast the central succession crisis; reconciliation or a genuine successor government remain possible. Support is an organizational indicator, not a vote or population share.":history+" A continuing town, country and reigning court are different kinds of persistence.",Facts=new[]{F("Claim declared","Year "+Y(s,c.DeclaredYear)),F("Local support",N(c.SupportMilli)+" / 1,000"),F("Status",active?"Unresolved local claim":c.ResolvedYear>=0?"Resolved in year "+Y(s,c.ResolvedYear):"No active supported claim")}};
        }
        static AtlasSection? RealmClaimantsSection(PrehistoryState s,int polity)
        {
            if(!HistoricalRealmClaimantHistory.Enabled(s))return null;
            // Explicit on-demand actor-record query. Never a simulation census
            // or a walk through the country's terrain holdings.
            var entries=s.History.Aggregates.Empires.Connections.Realms.Claimants.Records.Where(c=>c.Owner==polity).OrderByDescending(c=>c.LastYear).ThenBy(c=>c.City).Take(12).ToArray();if(entries.Length==0)return null;
            return new AtlasSection{Title="Local succession coalitions",Text="Up to twelve recently recorded city-led contests. A central crisis deadline does not itself settle every local claim. No extra army, treasury or population is created by a coalition; an actual new court must divide existing resources.",Links=entries.Select(c=>{var town=s.Society.Settlements[c.City];return new AtlasLink{Kind="city",Id=c.City,Cell=town.Cell,Name=town.Name,Detail=c.Name+" · "+(HistoricalRealmClaimantHistory.Active(s,c)?"unresolved local claim":string.IsNullOrEmpty(c.Outcome)?"no active supported claim":c.Outcome)+" · support "+N(c.SupportMilli)+" / 1,000"};}).ToArray()};
        }
    }
}
