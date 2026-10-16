using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        static AtlasLink OrganizationLink(PrehistoryState s,HistoricalInternationalOrganization o)=>new AtlasLink{Kind="organization",Id=o.Id,Name=o.Name,Historical=o.EndedYear>=0,Value=o.Members.Count,Detail=(o.Kind==HistoricalOrganizationKind.TradeLeague?"Trade league":"Peace congress")+" · "+N(o.Members.Count)+" members",Context="Founded "+Y(s,o.FoundedYear)};
        static void Organization(PrehistoryState s,int id,AtlasDocument d,List<AtlasSection> sections)
        {
            var system=s.History.International;
            if(system==null||id<0||id>=system.Organizations.Length){d.Title="Organization unavailable";d.Introduction="No diplomatic institution with this record exists in the current save.";return;}
            var o=system.Organizations[id];bool trade=o.Kind==HistoricalOrganizationKind.TradeLeague;d.Title=o.Name;
            d.Subtitle=(trade?"Trade league":"Peace congress")+" · "+(o.EndedYear<0?"Active":"Ended "+Y(s,o.EndedYear));
            d.Introduction=(trade?"Member governments use this league to negotiate commerce along existing connections.":"Member governments use this congress to negotiate disputes after costly conflict.")+" It was founded in "+Y(s,o.FoundedYear)+" under the initiative of "+s.History.Polities[o.FounderPolity].Name+". Members retain their own governments, can refuse proposals and may violate the charter.";
            d.Facts=new[]{F("Member governments",N(o.Members.Count)),F("Standing",o.EndedYear<0?"Active":"Historical"),F("Common treasury",N(o.Reserve)+" treasury units")};
            if(!trade)d.Facts=d.Facts.Concat(new[]{F("Accepted mediations",N(o.Mediations))}).ToArray();
            sections.Add(new AtlasSection{Title="Mandate and limits",Text=trade?"Funded administration can modestly reduce the costs of trade between actual connected members. It creates neither materials nor routes, and membership cannot connect isolated markets by itself.":"A funded diplomatic effort may secure a ceasefire when the actual belligerents are receptive. Mediation can fail, and the congress cannot order every state in the world to comply.",Facts=new[]{F("Institutional authority",(o.Authority/10.0).ToString("0.#",CultureInfo.InvariantCulture)+" / 100 index"),F("Unsuccessful mediations",N(o.FailedMediations)),F("Most recent funded action",Y(s,o.LastActionYear))}});
            sections.Add(Links("Member governments",Enumerable.Range(0,o.Members.Count).Select(o.Members.At).Where(k=>k>=0&&k<s.History.Polities.Length).Select(k=>StateLink(s,s.History.Polities[k],s.History.Polities[k].Government)),"Roster of the organization, not every government in the world."));
            sections.Add(new AtlasSection{Title="Common finances",Text="All amounts are abstract model treasury units. Contributions leave member treasuries; administration and mediation consume the organization's actual reserve.",Facts=new[]{F("Contributions received",N(o.Contributions)),F("Administration paid",N(o.ServiceSpending)),F("Mediation paid",N(o.MediationSpending)),F("Unspent reserve",N(o.Reserve))}});
            if(system.Finances!=null&&id<system.Finances.Organizations.Length)
            {
                var budget=system.Finances.Organizations[id];long annual=HistoricalInternationalBudgetEffects.OperatingCost(budget.AnnualAssessments,o.Members.Count);
                sections.Add(new AtlasSection{Title="Operating budget",Text="Members contribute toward current services and a three-year contingency reserve. Unpaid work remains an obligation; a large historical treasury alone does not prove that services are being delivered.",Facts=new[]{F("Annual operating budget",N(annual)+" treasury units"),F("Reserve coverage",(o.Reserve/(double)Math.Max(1,annual)).ToString("0.#",CultureInfo.InvariantCulture)+" years"),F("Unpaid operating work",N(budget.UnfundedWork)+" treasury units")}});
            }
            sections.Add(Links("Founding initiative",new[]{StateLink(s,s.History.Polities[o.FounderPolity],"Founding government; later successors may retain membership")}));
        }
    }
}
