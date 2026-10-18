using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        static string EthnicName(PrehistoryState s,int id)=>id<0?"Not recorded":s.History.Identities.Peoples[id].Name;
        static AtlasLink EthnicLink(PrehistoryState s,int id,int cell)=>new AtlasLink{Kind="ethnicity",Id=id,Cell=cell,Name=EthnicName(s,id),Detail="Ethnic identity",Context="Recorded since "+Y(s,s.History.Identities.Peoples[id].FoundedYear)};
        public static AtlasSection? EthnicLocalSection(PrehistoryState s,int cell)
        {
            var p=Prehistory.EthnicCommunityAt(s,cell);if(p==null)return null;
            var main=EthnicLink(s,p.Primary,cell);main.Value=1000-p.MinorityPermille;var links=new List<AtlasLink>{main};
            if(p.Minority>=0&&p.MinorityPermille>0){var minor=EthnicLink(s,p.Minority,cell);minor.Value=p.MinorityPermille;links.Add(minor);}
            return new AtlasSection{Title="Identity and belonging",Facts=new[]{F("Community cohesion",p.Cohesion+" / 1,000"),F("Homeland attachment",p.HomeAttachment+" / 1,000")},Links=links.ToArray(),Bars=true,Unit="/ 1,000",Text=(p.RegionalEstimate?"Regional estimate. ":"Local community estimate. ")+"Shared heritage and historical experience are distinct from genes, appearance, home language, religion and citizenship. Bars estimate household shares. The map shows the main identity; a resident minority may also be present. Two communities are retained as a coarse estimate, not an exhaustive census."};
        }
        static void AddIdentityLocalSections(PrehistoryState s,int cell,List<AtlasSection> sections)
        {
            var ethnic=EthnicLocalSection(s,cell);if(ethnic!=null)sections.Add(ethnic);
            int town=s.History.Aggregates?.TownAt[cell]??-1;var memory=LocalMemorySection(s,town);if(memory!=null)sections.Add(memory);
            var sovereignty=SovereigntySection(s,town);if(sovereignty!=null)sections.Add(sovereignty);
        }
        public static AtlasSection? LocalMemorySection(PrehistoryState s,int town)
        {
            var p=Prehistory.LocalHistoricalMemoryAt(s,town);if(p==null)return null;var facts=new List<AtlasFact>();var links=new List<AtlasLink>();
            if(p.PreviousCourt>=0){facts.Add(F("Remembered previous court",s.History.Polities[p.PreviousCourt].Name));links.Add(StateLink(s,s.History.Polities[p.PreviousCourt]));}
            if(p.SelfRuleSince>=0)facts.Add(F("Local governing tradition recorded",Y(s,p.SelfRuleSince)));
            facts.Add(F("Established local rights",p.LocalRights+" / 1,000"));facts.Add(F("Recent inclusive experience",N(p.InclusiveYears)+" years"));
            if(p.Harm>0&&p.WrongingState>=0){facts.Add(F("Remembered violent capture",s.History.Polities[p.WrongingState].Name+" · "+p.Harm+" / 1,000"));links.Add(StateLink(s,s.History.Polities[p.WrongingState]));}
            if(p.RelatedTown>=0){var link=TownLink(s,s.Society.Settlements[p.RelatedTown]);link.Detail=p.Relationship< -100?"Remembered attacking center":p.Relationship>100?"Established exchange relationship":"A remembered local connection";links.Add(link);}
            string account=s.Society.Settlements[town].Name+(p.PreviousCourt>=0?" remembers rule by "+s.History.Polities[p.PreviousCourt].Name+".":" has no earlier governing court recorded in this memory.");
            if(p.SelfRuleSince>=0)account+=" Its own governing tradition has been recorded since year "+Y(s,p.SelfRuleSince)+".";
            if(p.Harm>0&&p.WrongingState>=0)account+=" The violent capture by "+s.History.Polities[p.WrongingState].Name+" in year "+Y(s,p.LastViolence)+" remains a local grievance.";
            if(p.InclusiveYears>0)account+=" Recent inclusive experience spans "+N(p.InclusiveYears)+" accumulated years.";
            return new AtlasSection{Title="Local historical memory",Facts=facts.ToArray(),Links=links.ToArray(),Text=account+" A change of ruler does not erase identity or earned privileges. Actual violence and revoked rights matter; routine transfers do not invent atrocities. Older dated episodes remain in recorded history."};
        }
        static void EthnicPeople(PrehistoryState s,int id,AtlasDocument d,List<AtlasSection> sections)
        {
            var r=s.History.Identities;var p=r.Peoples[id];d.Title=p.Name;d.Cell=p.HomeCell;d.Subtitle="Ethnic identity · "+(p.Origin==PeopleIdentityOrigin.MixedCommunity?"Mixed local heritage":p.Origin==PeopleIdentityOrigin.LocalBranch?"Distinct local branch":"Recorded regional community");
            d.Introduction=p.Name+" is a community of shared heritage and historical belonging first recorded here in year "+Y(s,p.FoundedYear)+". "+(p.Parent<0?"Its earlier unrecorded past is not reconstructed.":"Its traditions descend from "+EthnicName(s,p.Parent)+(p.OtherParent>=0?" and "+EthnicName(s,p.OtherParent):"")+".")+" Members can speak different languages, follow different faiths or live under different states. Neither appearance nor genetic ancestry defines this identity.";
            d.Facts=new[]{F("Recorded since",Y(s,p.FoundedYear)),F("Origin",p.Origin==PeopleIdentityOrigin.MixedCommunity?"Sustained local mixing":p.Origin==PeopleIdentityOrigin.LocalBranch?"Longstanding local differentiation":"Recorded shared community")};
            var context=new List<AtlasLink>();if(p.Culture>=0)context.Add(CultureLink(s.Society.Cultures[p.Culture]));if(p.Language>=0)context.Add(SpokenLanguageLink(s,p.Language,s.History.Languages.Spoken.Varieties[p.Language].Cell));if(p.ReligiousFamily>=0)context.Add(FaithLink(s.History.Religions[p.ReligiousFamily]));
            sections.Add(Links("Founding context",context,"Recorded cultural, linguistic and religious surroundings at recognition. These are historical associations, not membership requirements or the beliefs and speech of every member today."));
            var parents=new List<AtlasLink>();if(p.Parent>=0)parents.Add(EthnicLink(s,p.Parent,r.Peoples[p.Parent].HomeCell));if(p.OtherParent>=0)parents.Add(EthnicLink(s,p.OtherParent,r.Peoples[p.OtherParent].HomeCell));
            parents.AddRange(r.Peoples.Where(x=>x.Parent==id||x.OtherParent==id).Take(12).Select(x=>EthnicLink(s,x.Id,x.HomeCell)));
            sections.Add(Links("Inherited and emerging identities",parents,"Inherited customs do not imply unchanged political allegiance. Descendant identities retain provenance rather than erasing the preceding people."));
            var inherit=p.Lineages.Select((origin,k)=>{var link=EthnicLink(s,origin,r.Peoples[origin].HomeCell);link.Value=p.Weights[k];return link;});
            sections.Add(Links("Inherited traditions",inherit,"A compiled social-heritage index, not DNA or a measure of ethnic purity.",true,"/ 1,000"));
            var samples=s.Society.Settlements.Where(t=>t.Population>0&&s.History.Aggregates.Living.Cities[t.Id].AbandonedYear<0).Select(t=>new{Town=t,Identity=Prehistory.EthnicCommunityAt(s,t.Cell)}).Where(x=>x.Identity!=null&&(x.Identity.Primary==id||x.Identity.Minority==id)).OrderByDescending(x=>x.Town.Population).ThenBy(x=>x.Town.Id).Take(16).Select(x=>{var link=TownLink(s,x.Town);link.Detail=x.Identity.Primary==id?"Main local identity":"Resident minority identity";return link;});
            sections.Add(Links("Recorded communities",samples,"Current town examples, not worldwide population counts or a territorial claim."));
            var local=EthnicLocalSection(s,p.HomeCell);if(local!=null)sections.Add(local);
            if(p.Culture<0)sections.Add(Text("Before advanced cultures","This regional community has an inherited identity without a named advanced-culture actor. Foraging communities are not culturally or physically interchangeable; later cultural organization need not replace this heritage."));
        }
    }
}
