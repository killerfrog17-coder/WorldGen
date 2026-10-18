using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        static void DomesticPoliticsSections(PrehistoryState s,int state,Prehistory engine,List<AtlasSection> sections)
        {
            var v=engine.DomesticProfile(s,state);if(v==null)return;
            sections.Add(new AtlasSection{Title="Domestic power groups",Bars=true,Unit="/ 1,000",Text="Enduring institutions bargain over policy and public resources. Influence is modeled leverage, not a vote count or an exhaustive census of parties.",
                Links=v.Groups.Select(g=>new AtlasLink{Kind=g.Leader>=0?"notable":"city",Id=g.Leader>=0?g.Leader:g.Seat,Name=g.Name+(g.Governing?" · governing":" · opposition"),Value=g.Influence,
                    Detail=(g.Governing?"Governing coalition":"Opposition")+" · organization "+g.Organization/10.0+"% · grievance "+g.Grievance/10.0+"%",
                    Context=g.Interest+". Prioritizes "+HistoricalDomesticEffects.Priorities[g.Priority].ToLowerInvariant()+"; seeks local voice "+g.Representation/10.0+"% and central authority "+g.Centralization/10.0+"%. Open its recorded public figure or meeting place."}).ToArray()});
            sections.Add(new AtlasSection{Title="Institutional bargaining",Text="Coalitions can outlive rulers. Funded settlements alter actual program spending and constitutional practice; organized exclusion and material hardship can weaken tax cooperation and intensify succession or local autonomy disputes. An officer takeover needs actual command, resources and severe political failure, not an automatic national expiry date.",
                Facts=new[]{F("Latest outcome",HistoricalDomesticEffects.Decision(v.LastDecision)),F("Last reviewed",Y(s,v.LastYear)),F("Decision recorded",v.LastDecision==0?"None yet":Y(s,v.DecisionYear)),
                    F("Governing partners",string.Join("; ",v.Groups.Where(g=>g.Governing).Select(g=>g.Name))),F("Mobilized opposition",v.Opposition/10.0+"%"),F("Latest negotiation cost",N(v.NegotiationCost)+" treasury units"),F("Contested office transfers",N(v.Contests)),F("Organization recorded since",Y(s,v.Founded))}});
        }
    }
}
